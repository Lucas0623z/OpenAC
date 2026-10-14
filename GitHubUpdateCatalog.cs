using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RogControl {
 public sealed class ReleaseVersion : IComparable<ReleaseVersion> {
  public string Text {get;private set;}
  public int Major {get;private set;}public int Minor {get;private set;}public int Patch {get;private set;}
  public string[] Prerelease {get;private set;}
  public bool IsPrerelease {get{return Prerelease.Length!=0;}}
  public static bool TryParse(string input,out ReleaseVersion version) {
   version=null;if(input==null||input.Length>160)return false;
   var match=Regex.Match(input,@"\Av?(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z");
   if(!match.Success)return false;
   string[] prerelease=match.Groups[4].Success?match.Groups[4].Value.Split('.'):new string[0];
   if(prerelease.Any(part=>part.Length>64||Regex.IsMatch(part,@"\A[0-9]+\z")&&part.Length>1&&part[0]=='0'))return false;
   version=new ReleaseVersion{Text=input.StartsWith("v",StringComparison.Ordinal)?input.Substring(1):input,Major=Int32.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture),Minor=Int32.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture),Patch=Int32.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture),Prerelease=prerelease};return true;
  }
  public int CompareTo(ReleaseVersion other) {
   if(other==null)return 1;int result=Major.CompareTo(other.Major);if(result!=0)return result;result=Minor.CompareTo(other.Minor);if(result!=0)return result;result=Patch.CompareTo(other.Patch);if(result!=0)return result;
   if(!IsPrerelease||!other.IsPrerelease)return IsPrerelease==other.IsPrerelease?0:IsPrerelease?-1:1;
   for(int i=0;i<Math.Min(Prerelease.Length,other.Prerelease.Length);i++) {
    string left=Prerelease[i],right=other.Prerelease[i];bool ln=Regex.IsMatch(left,@"\A[0-9]+\z"),rn=Regex.IsMatch(right,@"\A[0-9]+\z");
    if(ln&&rn){result=left.Length.CompareTo(right.Length);if(result==0)result=String.CompareOrdinal(left,right);}
    else if(ln!=rn)result=ln?-1:1;else result=String.CompareOrdinal(left,right);
    if(result!=0)return result;
   }
   return Prerelease.Length.CompareTo(other.Prerelease.Length);
  }
 }
 public sealed class OpenAcRelease {
  public string Tag,Version,ReleaseUrl,InstallerName,InstallerUrl,InstallerSha256,PublishedAt;
  public long InstallerSize;
  public bool IsPrerelease;
 }
 public sealed class OpenAcUpdateResult {
  public string InstalledVersion,CheckedAt;
  public bool UpdateAvailable;
  public OpenAcRelease Latest;
 }
 public static class GitHubUpdateCatalog {
  public const string InstalledVersion="0.3.0-preview";
  public const string RepositoryUrl="https://github.com/Lucas0623z/OpenAC";
  public const string ApiUrl="https://api.github.com/repos/Lucas0623z/OpenAC/releases";
  const int MaxBody=4194304;
  public static OpenAcUpdateResult NormalizeCachedResult(OpenAcUpdateResult cached) {
   if(cached==null||cached.Latest==null)return null;
   ReleaseVersion installed,version;
   if(!ReleaseVersion.TryParse(InstalledVersion,out installed)||!ReleaseVersion.TryParse(cached.Latest.Tag,out version)||cached.InstalledVersion!=InstalledVersion||cached.Latest.Version!=version.Text)return null;
   string releaseUrl=RepositoryUrl+"/releases/tag/"+Uri.EscapeDataString(cached.Latest.Tag);
   if(!ExactUrl(cached.Latest.ReleaseUrl,releaseUrl)||!installed.IsPrerelease&&(version.IsPrerelease||cached.Latest.IsPrerelease))return null;
   var latest=new OpenAcRelease{Tag=cached.Latest.Tag,Version=version.Text,ReleaseUrl=releaseUrl,IsPrerelease=version.IsPrerelease||cached.Latest.IsPrerelease,PublishedAt=cached.Latest.PublishedAt};
   string installer="OpenAC-"+version.Text+"-Setup.exe",installerUrl=RepositoryUrl+"/releases/download/"+Uri.EscapeDataString(latest.Tag)+"/"+Uri.EscapeDataString(installer);
   if(cached.Latest.InstallerName==installer&&ExactUrl(cached.Latest.InstallerUrl,installerUrl)&&cached.Latest.InstallerSize>0&&cached.Latest.InstallerSize<=1073741824){latest.InstallerName=installer;latest.InstallerUrl=installerUrl;latest.InstallerSize=cached.Latest.InstallerSize;if(Regex.IsMatch(cached.Latest.InstallerSha256??"",@"\A[0-9a-fA-F]{64}\z"))latest.InstallerSha256=cached.Latest.InstallerSha256.ToLowerInvariant();}
   return new OpenAcUpdateResult{InstalledVersion=InstalledVersion,CheckedAt=cached.CheckedAt,Latest=latest,UpdateAvailable=version.CompareTo(installed)>0};
  }
  public static string ValidatedReleaseLink(OpenAcUpdateResult result,bool installer) {
   var normalized=NormalizeCachedResult(result);if(normalized==null)throw new InvalidDataException("更新链接无效，请重新检查更新。");
   string url=installer?normalized.Latest.InstallerUrl:normalized.Latest.ReleaseUrl;
   if(String.IsNullOrEmpty(url))throw new InvalidDataException("此版本暂无安装包，请重新检查更新。");return url;
  }
  public static Task<OpenAcUpdateResult> CheckAsync(){return Task.Run(()=>Check());}
  public static OpenAcUpdateResult Check() {
   var releases=new List<object>();var clock=System.Diagnostics.Stopwatch.StartNew();
   for(int page=1;page<=10;page++) {
    if(clock.ElapsedMilliseconds>=30000)throw new TimeoutException("检查更新超时，请稍后重试。");
    string body=GetPublicJson(ApiUrl+"?per_page=100&page="+page,Math.Min(10000,30000-(int)clock.ElapsedMilliseconds));
    var items=new JavaScriptSerializer{MaxJsonLength=MaxBody}.DeserializeObject(body) as object[];
    if(items==null)throw new InvalidDataException("GitHub 返回的版本列表无效。");releases.AddRange(items);
    if(items.Length<100)return Evaluate(releases,InstalledVersion);
   }
   // Never call an incomplete bounded scan 'up to date'.
   throw new InvalidDataException("版本列表过长，未完成更新检查。");
  }
  public static OpenAcUpdateResult Evaluate(IEnumerable<object> entries,string installedText) {
   ReleaseVersion installed;if(!ReleaseVersion.TryParse(installedText,out installed))throw new InvalidDataException("当前版本号无效。");
   OpenAcRelease selected=null;ReleaseVersion selectedVersion=null;
   foreach(var raw in entries) {
    var row=raw as IDictionary<string,object>;if(row==null||Flag(row,"draft")!=false||Flag(row,"prerelease")==null)continue;
    string tag=StringField(row,"tag_name");ReleaseVersion version;
    if(!ReleaseVersion.TryParse(tag,out version))continue;
    bool preview=Flag(row,"prerelease")==true||version.IsPrerelease;
    if(preview&&!installed.IsPrerelease)continue;
    DateTime published;string publication=StringField(row,"published_at");
    if(!DateTime.TryParse(publication,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out published))continue;
    string expectedUrl=RepositoryUrl+"/releases/tag/"+Uri.EscapeDataString(tag);
    if(!ExactUrl(StringField(row,"html_url"),expectedUrl))continue;
    if(selectedVersion!=null&&version.CompareTo(selectedVersion)<=0)continue;
    selectedVersion=version;
    selected=new OpenAcRelease{Tag=tag,Version=version.Text,ReleaseUrl=expectedUrl,IsPrerelease=preview,PublishedAt=publication};
    string installerName="OpenAC-"+version.Text+"-Setup.exe";
    var assets=Value(row,"assets") as object[];
    if(assets!=null)foreach(var assetRaw in assets) {
     var asset=assetRaw as IDictionary<string,object>;if(asset==null||StringField(asset,"name")!=installerName||StringField(asset,"state")!="uploaded")continue;
     string expectedAsset=RepositoryUrl+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(installerName);
     long size;if(!ExactUrl(StringField(asset,"browser_download_url"),expectedAsset)||!Int64.TryParse(Convert.ToString(Value(asset,"size"),CultureInfo.InvariantCulture),out size)||size<=0||size>1073741824)continue;
     selected.InstallerName=installerName;selected.InstallerUrl=expectedAsset;selected.InstallerSize=size;
     string digest=StringField(asset,"digest");if(Regex.IsMatch(digest,@"\Asha256:[0-9a-fA-F]{64}\z"))selected.InstallerSha256=digest.Substring(7).ToLowerInvariant();
     break;
    }
   }
   if(selected==null)throw new InvalidDataException("未找到有效的 OpenAC 发布版本。");
   return new OpenAcUpdateResult{InstalledVersion=installed.Text,CheckedAt=DateTime.UtcNow.ToString("o"),Latest=selected,UpdateAvailable=selectedVersion.CompareTo(installed)>0};
  }
  static string GetPublicJson(string url,int timeout) {
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(url);request.Method="GET";request.UserAgent="OpenAC/"+InstalledVersion;request.Accept="application/vnd.github+json";request.Headers["X-GitHub-Api-Version"]="2022-11-28";
   request.AllowAutoRedirect=false;request.Timeout=timeout;request.ReadWriteTimeout=timeout;
   using(var deadline=new System.Threading.Timer(s=>{try{request.Abort();}catch{}},null,timeout,System.Threading.Timeout.Infinite))try {using(var response=(HttpWebResponse)request.GetResponse()) {
    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>MaxBody)throw new InvalidDataException("GitHub 返回的更新列表无效。");
    using(var stream=response.GetResponseStream())using(var output=new MemoryStream()) {
     var buffer=new byte[8192];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+count>MaxBody)throw new InvalidDataException("GitHub 版本列表过大。");output.Write(buffer,0,count);}return Encoding.UTF8.GetString(output.ToArray());
    }
   }}catch(WebException ex) {
    using(var response=ex.Response as HttpWebResponse) {
     if(response!=null&&((int)response.StatusCode==403||(int)response.StatusCode==429))throw new InvalidOperationException("GitHub 暂时限制请求，请稍后重试。");
     if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("未找到 OpenAC 发布仓库。");
    }
    if(ex.Status==WebExceptionStatus.Timeout||ex.Status==WebExceptionStatus.RequestCanceled)throw new TimeoutException("检查更新超时，请稍后重试。");
    throw new InvalidOperationException("无法连接 GitHub，请检查网络后重试。");
   }
  }
  static object Value(IDictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?value:null;}
  static string StringField(IDictionary<string,object> row,string key){return Value(row,key) as string??"";}
  static bool? Flag(IDictionary<string,object> row,string key){object value=Value(row,key);return value is bool?(bool?)value:null;}
  static bool ExactUrl(string supplied,string expected){Uri url;return Uri.TryCreate(supplied,UriKind.Absolute,out url)&&url.Scheme=="https"&&url.Host=="github.com"&&url.IsDefaultPort&&url.UserInfo==""&&url.Query==""&&url.Fragment==""&&String.Equals(url.AbsoluteUri,expected,StringComparison.Ordinal);}
 }
}
