using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using RogControl;

static class GitHubUpdateCatalogTests {
 static int tests;
 static void Check(bool condition,string message){tests++;if(!condition)throw new Exception(message);}
 static ReleaseVersion Version(string value){ReleaseVersion parsed;if(!ReleaseVersion.TryParse(value,out parsed))throw new Exception("Test version invalid: "+value);return parsed;}
 static Dictionary<string,object> Release(string tag,bool preview=false,bool draft=false) {
  string version=tag.StartsWith("v")?tag.Substring(1):tag,name="OpenAC-"+version+"-Setup.exe";
  return new Dictionary<string,object>{{"tag_name",tag},{"prerelease",preview},{"draft",draft},{"published_at","2026-10-13T18:00:00Z"},{"html_url",GitHubUpdateCatalog.RepositoryUrl+"/releases/tag/"+Uri.EscapeDataString(tag)},
   {"assets",new object[]{new Dictionary<string,object>{{"name",name},{"state","uploaded"},{"size",12345},{"browser_download_url",GitHubUpdateCatalog.RepositoryUrl+"/releases/download/"+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(name)},{"digest","sha256:"+new string('a',64)}}}}};
 }
 static Dictionary<string,object> Asset(Dictionary<string,object> release){return (Dictionary<string,object>)((object[])release["assets"])[0];}
 static bool Rejected(object[] releases,string installed="0.3.0-preview"){try{GitHubUpdateCatalog.Evaluate(releases,installed);return false;}catch(System.IO.InvalidDataException){return true;}}
 static int Main(string[] arguments) {
  try {
   if(arguments.Length>0&&arguments[0]=="--live"){
    var liveResult=GitHubUpdateCatalog.Check();Console.WriteLine(new JavaScriptSerializer().Serialize(liveResult));return 0;
   }
   var order=new[]{"0.3.0-alpha","0.3.0-alpha.1","0.3.0-alpha.beta","0.3.0-beta","0.3.0-beta.2","0.3.0-beta.11","0.3.0-rc.1","0.3.0","0.3.1","0.10.0","1.0.0"};
   for(int i=1;i<order.Length;i++)Check(Version(order[i-1]).CompareTo(Version(order[i]))<0,"Semantic version order "+i);
   Check(Version("v0.3.0-preview.9").CompareTo(Version("0.3.0-preview.10"))<0,"Preview numerical order");
   Check(Version("0.3.0+build.7").CompareTo(Version("0.3.0+build.8"))==0,"Build metadata ignored");
   foreach(string invalid in new[]{"0.3","0.03.0","00.3.0","0.3.0-preview.01","v0.3.0/evil","v0.3.0\n","V0.3.0","1.0.0-","v1.0.0+","-1.2.3"}){ReleaseVersion parsed;Check(!ReleaseVersion.TryParse(invalid,out parsed),"Reject invalid tag "+invalid);}
   var current=Release("v0.3.0-preview",true);
   var result=GitHubUpdateCatalog.Evaluate(new object[]{current},"0.3.0-preview");
   Check(!result.UpdateAvailable,"Current preview is up to date");Check(result.Latest.InstallerName=="OpenAC-0.3.0-preview-Setup.exe","Exact installer picked");Check(result.Latest.InstallerSha256==new string('a',64),"Published hash read");
   result=GitHubUpdateCatalog.Evaluate(new object[]{current,Release("v0.3.0")},"0.3.0-preview");Check(result.UpdateAvailable&&result.Latest.Version=="0.3.0","Stable replaces preview");
   result=GitHubUpdateCatalog.Evaluate(new object[]{Release("v0.10.0-preview",true),Release("v0.4.0-preview",true)},"0.3.0-preview");Check(result.Latest.Version=="0.10.0-preview","Not lexical/release-list order");
   result=GitHubUpdateCatalog.Evaluate(new object[]{Release("v0.4.0-preview",true),Release("v0.3.1")},"0.3.0");Check(result.Latest.Version=="0.3.1","Stable channel excludes previews");
   result=GitHubUpdateCatalog.Evaluate(new object[]{current,Release("v99.0.0",false,true)},"0.3.0-preview");Check(!result.UpdateAvailable,"Draft ignored");
   Check(Rejected(new object[0]),"Empty catalogue isn't up-to-date");
   var missingDate=Release("v0.4.0");missingDate.Remove("published_at");Check(Rejected(new object[]{missingDate}),"Unpublished entry rejected");
   var noDraft=Release("v0.4.0");noDraft.Remove("draft");Check(Rejected(new object[]{noDraft}),"Missing publication state rejected");
   var foreign=Release("v0.4.0");foreign["html_url"]="https://github.com/another/OpenAC/releases/tag/v0.4.0";Check(Rejected(new object[]{foreign}),"Foreign release rejected");
   foreach(string url in new[]{"http://github.com/Lucas0623z/OpenAC/releases/tag/v0.4.0","https://github.com.evil.test/Lucas0623z/OpenAC/releases/tag/v0.4.0","https://github.com@evil.test/Lucas0623z/OpenAC/releases/tag/v0.4.0","https://github.com/Lucas0623z/OpenAC/releases/tag/v0.4.0?x=1","https://github.com/Lucas0623z/OpenAC/releases/tag/v0.4.0#fragment"}){
    var invalid=Release("v0.4.0");invalid["html_url"]=url;Check(Rejected(new object[]{invalid}),"Unsafe release URL rejected");
   }
   foreach(string url in new[]{"https://evil.test/Setup.exe","https://github.com/Lucas0623z/OpenAC/releases/download/v0.3.0/OpenAC-0.4.0-Setup.exe","https://github.com/another/OpenAC/releases/download/v0.4.0/OpenAC-0.4.0-Setup.exe","https://github.com/Lucas0623z/OpenAC/releases/download/v0.4.0/OpenAC-0.4.0-Setup.exe?download=evil"}) {
    var invalid=Release("v0.4.0");Asset(invalid)["browser_download_url"]=url;result=GitHubUpdateCatalog.Evaluate(new object[]{invalid},"0.3.0-preview");Check(result.UpdateAvailable&&result.Latest.InstallerUrl==null,"Wrong asset rejected without discarding valid release");
   }
   var assetMismatch=Release("v0.4.0");Asset(assetMismatch)["name"]="other.exe";result=GitHubUpdateCatalog.Evaluate(new object[]{assetMismatch},"0.3.0-preview");Check(result.Latest.InstallerUrl==null,"Wrong filename rejected");
   var unuploaded=Release("v0.4.0");Asset(unuploaded)["state"]="new";result=GitHubUpdateCatalog.Evaluate(new object[]{unuploaded},"0.3.0-preview");Check(result.Latest.InstallerUrl==null,"Incomplete upload not offered");
   var badDigest=Release("v0.4.0");Asset(badDigest)["digest"]="sha256:unverified";result=GitHubUpdateCatalog.Evaluate(new object[]{badDigest},"0.3.0-preview");Check(result.Latest.InstallerSha256==null,"Bad hash not displayed as verified");
   var cached=GitHubUpdateCatalog.Evaluate(new object[]{Release("v0.4.0")},GitHubUpdateCatalog.InstalledVersion);
   Check(GitHubUpdateCatalog.NormalizeCachedResult(cached).UpdateAvailable,"Valid cache accepted");
   cached.UpdateAvailable=false;Check(GitHubUpdateCatalog.NormalizeCachedResult(cached).UpdateAvailable,"Cache comparison recomputed");
   cached.Latest.InstallerUrl="C:\\malicious.exe";Check(GitHubUpdateCatalog.NormalizeCachedResult(cached).Latest.InstallerUrl==null,"Cached local executable dropped");
   bool badLink=false;try{GitHubUpdateCatalog.ValidatedReleaseLink(cached,true);}catch(System.IO.InvalidDataException){badLink=true;}Check(badLink,"Click-time cached installer validation");
   cached.Latest.ReleaseUrl="https://evil.test/update";Check(GitHubUpdateCatalog.NormalizeCachedResult(cached)==null,"Cached foreign release dropped");
   Check(GitHubUpdateCatalog.NormalizeCachedResult(new OpenAcUpdateResult())==null,"Incomplete cache dropped");
   Console.WriteLine("GitHub release updater: "+tests+" pure checks passed. No network, downloads, installer or hardware action.");return 0;
  }catch(Exception exception){Console.Error.WriteLine(exception);return 1;}
 }
}
