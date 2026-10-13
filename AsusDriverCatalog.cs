// OpenAC-owned public ASUS catalog reader. Protocol facts only; no G-Helper code.
// No authentication, package downloads, installation, flashing or hardware settings.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RogControl {
 public sealed class AsusCatalogIdentity {
  public string Model,ModelSource,InstalledBios,Platform;
 }
 public sealed class AsusCatalogOs { public int Id;public string Name; }
 public sealed class AsusCatalogFile {
  public string Category,Title,Version,ReleaseDate,Description,SizeText,DownloadUrl,ExpectedSha256,ReleaseFlag;
  public string InstalledVersion,Comparison;
  public string[] HardwareIds;
  public bool CatalogSignaturePresent,DownloadVerified,IsSystemBios;
 }
 public sealed class AsusCatalogResult {
  public string Model,ModelSource,Platform,CheckedAtUtc,InstalledBios,OperatingSystem;
  public int ProductId,OsId;
  public bool ExactModelConfirmed,HardwareSettingsChanged,PackageDownloaded;
  public AsusCatalogOs[] OperatingSystems;
  public AsusCatalogFile[] BiosAndFirmware,DriversAndTools;
  public string[] SourceUrls,Warnings;
  public Dictionary<string,string> ResponseSha256;
 }
 public static class AsusDriverCatalog {
  const int MaxBody=4194304;
  const string ApiOrigin="https://rog.asus.com/support/webapi/";
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=MaxBody,RecursionLimit=40};
  static readonly string[] Methods={"product/CheckModelExists","product/GetPDOS","ProductV2/GetPDBIOS","ProductV2/GetPDDrivers"};
  public static Task<AsusCatalogResult> ReadLocalAsync(int osId=52) {return Task.Run(()=>Read(IdentifyLocal(),osId));}
  public static AsusCatalogResult ReadLocal(int osId=52) {return Read(IdentifyLocal(),osId);}
  public static AsusCatalogIdentity IdentifyLocal() {
   // Deliberately do not request serial numbers, UUIDs, device paths or user data.
   string boardMaker=LocalValue("Win32_BaseBoard","Manufacturer"),board=LocalValue("Win32_BaseBoard","Product");
   string systemMaker=LocalValue("Win32_ComputerSystem","Manufacturer"),system=LocalValue("Win32_ComputerSystem","Model");
   return SelectIdentity(boardMaker,board,systemMaker,system,LocalValue("Win32_BIOS","SMBIOSBIOSVersion"));
  }
  internal static AsusCatalogIdentity SelectIdentity(string boardMaker,string board,string systemMaker,string system,string bios) {
   bool asusBoard=AsusManufacturer(boardMaker),asusSystem=AsusManufacturer(systemMaker);
   if(!asusBoard&&!asusSystem)throw new InvalidDataException("No ASUS system or motherboard identity was confirmed.");
   // Motherboard Product is also the exact hardware model on many ASUS notebooks
   // and handhelds. No marketing-name substring or SKU guessing is performed.
   if(asusBoard&&UsableModel(board))return new AsusCatalogIdentity{Model=ExactModel(board),ModelSource="Win32_BaseBoard.Product",InstalledBios=bios,Platform=UsableModel(system)?"system":"motherboard"};
   if(asusSystem&&UsableModel(system))return new AsusCatalogIdentity{Model=ExactModel(system),ModelSource="Win32_ComputerSystem.Model",InstalledBios=bios,Platform="system"};
   throw new InvalidDataException("The local ASUS model is a placeholder or is unavailable.");
  }
  static string LocalValue(string type,string field) {
   var options=new EnumerationOptions{Timeout=TimeSpan.FromSeconds(3),ReturnImmediately=false};
   using(var query=new ManagementObjectSearcher("root\\CIMV2","SELECT "+field+" FROM "+type,options))
   using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){return Convert.ToString(row[field],CultureInfo.InvariantCulture);}
   return null;
  }
  static bool AsusManufacturer(string value) {return String.Equals(value,"ASUS",StringComparison.OrdinalIgnoreCase)||String.Equals(value,"ASUSTeK COMPUTER INC.",StringComparison.OrdinalIgnoreCase);}
  static bool UsableModel(string model) {return !String.IsNullOrWhiteSpace(model)&&!new[]{"System Product Name","System Version","Default string","To be filled by O.E.M.","Not Applicable","Unknown"}.Contains(model.Trim(),StringComparer.OrdinalIgnoreCase);}
  internal static string ExactModel(string supplied) {
   if(supplied==null)throw new ArgumentException("An exact ASUS model is required.");string model=supplied.Trim();
   if(!UsableModel(model)||!Regex.IsMatch(model,@"\A[A-Za-z0-9][A-Za-z0-9 ._()+/\-]{0,159}\z"))throw new ArgumentException("An exact ASUS model is required.");return model;
  }
  public static AsusCatalogResult Read(AsusCatalogIdentity identity,int osId=52) {
   if(identity==null)throw new ArgumentNullException("identity");string model=ExactModel(identity.Model);
   if(osId<=0||osId>1000)throw new ArgumentOutOfRangeException("osId");
   var urls=new List<string>();var hashes=new Dictionary<string,string>();var clock=Stopwatch.StartNew();
   Uri resolveUrl=ApiUri(Methods[0],model,0,0);string resolved=Fetch(resolveUrl,clock);urls.Add(resolveUrl.AbsoluteUri);hashes[Methods[0]]=Digest(resolved);
   int productId=ResolveProduct(resolved,model);
   Uri osUrl=ApiUri(Methods[1],model,productId,0);string osBody=Fetch(osUrl,clock);urls.Add(osUrl.AbsoluteUri);hashes[Methods[1]]=Digest(osBody);
   AsusCatalogOs[] systems=ParseOs(osBody);AsusCatalogOs selected=systems.FirstOrDefault(item=>item.Id==osId);
   if(selected==null)throw new InvalidDataException("The requested operating system is not listed for this exact ASUS model.");
   Uri biosUrl=ApiUri(Methods[2],model,productId,0),driversUrl=ApiUri(Methods[3],model,productId,osId);
   // Independent requests share a 16-second overall budget; no implicit retries.
   var biosTask=Task.Run(()=>Fetch(biosUrl,clock));var driversTask=Task.Run(()=>Fetch(driversUrl,clock));Task.WaitAll(biosTask,driversTask);
   urls.Add(biosUrl.AbsoluteUri);urls.Add(driversUrl.AbsoluteUri);hashes[Methods[2]]=Digest(biosTask.Result);hashes[Methods[3]]=Digest(driversTask.Result);
   var warnings=new List<string>();
   AsusCatalogFile[] bios=ParseFiles(biosTask.Result,model,true,identity.InstalledBios,warnings),drivers=ParseFiles(driversTask.Result,model,false,null,warnings);
   warnings.Add("Driver installed versions and per-adapter applicability have not been verified; catalog entries are not an installation recommendation.");
   warnings.Add("Catalog SHA-256 values describe expected downloads; no package has been downloaded or its signature verified.");
   return new AsusCatalogResult{Model=model,ModelSource=identity.ModelSource,Platform=identity.Platform,InstalledBios=identity.InstalledBios,ProductId=productId,OsId=osId,OperatingSystem=selected.Name,OperatingSystems=systems,BiosAndFirmware=bios,DriversAndTools=drivers,SourceUrls=urls.ToArray(),ResponseSha256=hashes,Warnings=warnings.Distinct().ToArray(),CheckedAtUtc=DateTime.UtcNow.ToString("o"),ExactModelConfirmed=true,HardwareSettingsChanged=false,PackageDownloaded=false};
  }
  internal static Uri ApiUri(string method,string model,int productId,int osId) {
   if(!Methods.Contains(method))throw new ArgumentException("Unverified ASUS API method.");model=ExactModel(model);
   string query="website=global&model="+Uri.EscapeDataString(model)+"&systemCode=rog";
   if(method!=Methods[0])query+="&pdid="+productId.ToString(CultureInfo.InvariantCulture)+"&m1id=0&cpu=";
   if(method==Methods[1]||method==Methods[3])query+="&mode=";
   if(method==Methods[3])query+="&osid="+osId.ToString(CultureInfo.InvariantCulture)+"&active=";
   return new Uri(ApiOrigin+method+"?"+query);
  }
  internal static int ResolveProduct(string body,string requestedModel) {
   var result=Result(body);var record=Map(Value(result,"Obj"));
   if(!SameModel(Field(record,"model"),requestedModel))throw new InvalidDataException("ASUS did not confirm the exact requested model.");
   int id;if(!Int32.TryParse(Convert.ToString(Value(record,"productId"),CultureInfo.InvariantCulture),out id)||id<=0)throw new InvalidDataException("ASUS returned no valid product identity.");return id;
  }
  internal static AsusCatalogOs[] ParseOs(string body) {
   var list=new List<AsusCatalogOs>();foreach(object raw in Items(Value(Result(body),"Obj"))){var row=Map(raw);int id;
    if(!Int32.TryParse(Field(row,"Id"),out id)||id<=0||id>1000)throw new InvalidDataException("Unexpected operating-system identifier.");
    if(list.Any(item=>item.Id==id))throw new InvalidDataException("Duplicate operating-system identifier.");list.Add(new AsusCatalogOs{Id=id,Name=Text(row,"Name",120,true)});
   }return list.ToArray();
  }
  internal static AsusCatalogFile[] ParseFiles(string body,string requestedModel,bool biosCatalog,string installedBios,List<string> warnings) {
   var result=Result(body);if(!SameModel(Field(result,"Model"),requestedModel))throw new InvalidDataException("The ASUS catalog model does not match the requested model.");
   var files=new List<AsusCatalogFile>();var groups=Items(Value(result,"Obj"));if(groups.Length>100)throw new InvalidDataException("Unexpected catalog group count.");
   foreach(object groupRaw in groups){var group=Map(groupRaw);string category=Text(group,"Name",160,true);
    foreach(object raw in Items(Value(group,"Files"))){if(files.Count>=2000)throw new InvalidDataException("ASUS catalog is too large.");var row=Map(raw);
     string version=Text(row,"Version",160,false),title=Text(row,"Title",500,false),digest=Field(row,"sha256");
     if(digest.Length!=0&&!Regex.IsMatch(digest,@"\A[0-9a-fA-F]{64}\z"))throw new InvalidDataException("Unexpected catalog SHA-256.");
     string link=Field(Map(Value(row,"DownloadUrl")),"Global");string download=null;try{download=DownloadUri(link).AbsoluteUri;}catch(InvalidDataException){warnings.Add("One or more catalog entries have no accepted official package URL.");}
     var hardware=new List<string>();object hardwareRows=Value(row,"HardwareInfoList");if(hardwareRows!=null)foreach(object hw in Items(hardwareRows)){string id=Text(Map(hw),"hardwareid",256,false);if(id.Length!=0)hardware.Add(id);}
     bool systemBios=biosCatalog&&category.Equals("BIOS",StringComparison.OrdinalIgnoreCase);
     var file=new AsusCatalogFile{Category=category,Title=title.Length==0?category+" "+version:title,Version=version,ReleaseDate=Text(row,"ReleaseDate",40,false),Description=PlainDescription(Text(row,"Description",65536,false)),SizeText=Text(row,"FileSize",80,false),DownloadUrl=download,ExpectedSha256=digest.Length==0?null:digest.ToLowerInvariant(),ReleaseFlag=Text(row,"IsRelease",8,false),HardwareIds=hardware.ToArray(),CatalogSignaturePresent=Field(row,"Sign").Length!=0,DownloadVerified=false,IsSystemBios=systemBios,InstalledVersion=systemBios?installedBios:null,Comparison=systemBios?CompareBios(requestedModel,version,installedBios):"installedVersionUnknown"};
     files.Add(file);
    }
   }return files.ToArray();
  }
  internal static string CompareBios(string model,string available,string installed) {
   if(String.IsNullOrEmpty(installed)||String.IsNullOrEmpty(available))return "unknown";
   // ASUS notebook SMBIOS can prefix the numeric BIOS revision with exact model.
   string local=installed.Trim(),remote=available.Trim();string prefix=model+".";
   if(local.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))local=local.Substring(prefix.Length);
   if(remote.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))remote=remote.Substring(prefix.Length);
   uint lhs,rhs;if(!Regex.IsMatch(local,@"\A[0-9]{1,8}\z")||!Regex.IsMatch(remote,@"\A[0-9]{1,8}\z")||!UInt32.TryParse(local,out lhs)||!UInt32.TryParse(remote,out rhs))return "unknown";
   return rhs>lhs?"newerAvailable":rhs==lhs?"sameVersion":"olderThanInstalled";
  }
  internal static Uri DownloadUri(string supplied) {
   if(String.IsNullOrWhiteSpace(supplied)||supplied.Length>2048||supplied.Contains("\\")||Regex.IsMatch(supplied,@"[\x00-\x20]"))throw new InvalidDataException("Unexpected package URL.");
   Uri uri;if(supplied.StartsWith("/pub/ASUS/",StringComparison.Ordinal))uri=new Uri("https://dlcdnets.asus.com"+supplied);else if(!Uri.TryCreate(supplied,UriKind.Absolute,out uri))throw new InvalidDataException("Unexpected package URL.");
   if(uri.Scheme!="https"||!new[]{"dlcdnets.asus.com","dlcdn.asus.com","dlcdnet.asus.com"}.Contains(uri.Host,StringComparer.OrdinalIgnoreCase)||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||!uri.AbsolutePath.StartsWith("/pub/ASUS/",StringComparison.Ordinal)||!Regex.IsMatch(uri.AbsolutePath,@"\.(zip|exe|msi|cap|rar|7z)\z",RegexOptions.IgnoreCase)||Uri.UnescapeDataString(supplied).Split('/').Contains("..")||supplied.Contains("%"))throw new InvalidDataException("Package URL is outside accepted ASUS download origins.");return uri;
  }
  static string Fetch(Uri uri,Stopwatch clock) {
   if(uri.Scheme!="https"||uri.Host!="rog.asus.com"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||!Methods.Any(method=>uri.AbsolutePath=="/support/webapi/"+method))throw new InvalidDataException("Unverified ASUS catalog origin.");
   int remaining=16000-(int)clock.ElapsedMilliseconds;if(remaining<500)throw new TimeoutException("Official ASUS catalog timed out.");int timeout=Math.Min(6000,remaining-200);
   string executable=Path.Combine(Environment.SystemDirectory,"curl.exe");if(!File.Exists(executable))throw new IOException("Windows HTTPS reader is unavailable.");
   string seconds=(timeout/1000.0).ToString("0.000",CultureInfo.InvariantCulture);
   var start=new ProcessStartInfo(executable,"--disable --silent --show-error --fail --proto =https --max-time "+seconds+" --connect-timeout 2 --max-filesize "+MaxBody+" --dump-header - "+uri.AbsoluteUri){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   string output;using(var process=Process.Start(start)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(timeout+150)){try{process.Kill();}catch{}throw new TimeoutException("Official ASUS catalog timed out.");}output=stdout.GetAwaiter().GetResult();stderr.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new IOException("Official ASUS catalog request failed (HTTPS reader code "+process.ExitCode+").");}
   if(output.Length>MaxBody+65536)throw new InvalidDataException("Official catalog response is too large.");
   int offset=0;string headers;do{int end=output.IndexOf("\r\n\r\n",offset,StringComparison.Ordinal);if(end<0||end-offset>65536)throw new InvalidDataException("Unexpected official response headers.");headers=output.Substring(offset,end-offset);offset=end+4;}while(output.IndexOf("HTTP/",offset,StringComparison.Ordinal)==offset);
   string status=headers.Split(new[]{"\r\n"},StringSplitOptions.None)[0];if(!Regex.IsMatch(status,@"\AHTTP/[0-9.]+ 200(?: |$)"))throw new InvalidDataException("Official ASUS catalog did not return HTTP 200.");
   string body=output.Substring(offset);if(Encoding.UTF8.GetByteCount(body)>MaxBody)throw new InvalidDataException("Official ASUS catalog is too large.");return body;
  }
  static Dictionary<string,object> Result(string body) {
   if(body==null||Encoding.UTF8.GetByteCount(body)>MaxBody)throw new InvalidDataException("Unexpected ASUS catalog body.");
   var row=Map(Json.DeserializeObject(body));if(Field(row,"Status")!="SUCCESS")throw new InvalidDataException("ASUS did not return a successful catalog response.");return Map(Value(row,"Result"));
  }
  static Dictionary<string,object> Map(object value){var row=value as Dictionary<string,object>;if(row==null)throw new InvalidDataException("Unexpected ASUS catalog object.");return row;}
  static object[] Items(object value){var items=value as object[];if(items==null)throw new InvalidDataException("Unexpected ASUS catalog list.");return items;}
  static object Value(Dictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?value:null;}
  static string Field(Dictionary<string,object> row,string key){object value=Value(row,key);return value==null?"":Convert.ToString(value,CultureInfo.InvariantCulture);}
  static string Text(Dictionary<string,object> row,string key,int max,bool required){string value=Field(row,key);if(value.Length>max||required&&value.Length==0)throw new InvalidDataException("Unexpected catalog field: "+key+".");return value;}
  static bool SameModel(string response,string request){return String.Equals(response,request,StringComparison.OrdinalIgnoreCase);}
  static string PlainDescription(string html){return WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html,@"<(br|/p)\b[^>]*>","\n",RegexOptions.IgnoreCase),@"<[^>]*>",""));}
  static string Digest(string body){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-","").ToLowerInvariant();}
 }
}
