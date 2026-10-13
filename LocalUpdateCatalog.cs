// Owned read-only inventory of ASUS updater caches. Cache contents are not a live server check.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;

namespace RogControl {
 public sealed class CachedUpdateRequirement {
  public string name,profileId,category,registryKey,minimumVersion,cachedInstalledVersion,minimumStatus;
 }
 public sealed class CachedDeviceDependency {
  public string model,sourcePath,cacheModifiedUtc;
  public CachedUpdateRequirement[] devicePageRequirements;
 }
 public static class LocalUpdateCatalog {
  static readonly string BasePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"ASUS", "ROG Live Service");
  static readonly string CatalogPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"ASUS", "ROG Live Service", "DeviceUpdateList.xml");
  static readonly string[] Models={"ROG CROSSHAIR X870E EXTREME","ROG OMNI RECEIVER","ROG HARPE II EXTREME EDITION 20","ROG DELTA II-KJP","ROG AZOTH EXTREME EDITION 20","ROG EURUX","FanXpert","AURA Service","AuraLayerManager","ASUSWDLPlugin"};
  static Dictionary<string,object> O(params object[] fields){var result=new Dictionary<string,object>();for(int i=0;i<fields.Length;i+=2)result[(string)fields[i]]=fields[i+1];return result;}
  static string Attribute(XmlNode n,string key){return n.Attributes!=null&&n.Attributes[key]!=null?n.Attributes[key].Value:null;}
  public static Dictionary<string,object> Read(){
   var errors=new List<object>();var dependencies=new List<CachedDeviceDependency>();var diagnostics=new List<object>();var cache=O("items",new object[0]);var versions=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
   foreach(string filename in new[]{"versioninfo.ini","deviceinfo.ini"})try{IndexVersions(ReadBounded(Path.Combine(BasePath,filename),1024*1024),versions);}catch(Exception e){errors.Add(O("source",filename,"message",e.Message));}
   try{cache=ParseCatalog(ReadBounded(CatalogPath,1024*1024));cache["sourcePath"]=CatalogPath;cache["cacheModifiedUtc"]=File.GetLastWriteTimeUtc(CatalogPath).ToString("o");}catch(Exception e){errors.Add(O("source","DeviceUpdateList.xml","message",e.Message));}
   foreach(string model in Models.Where(m=>m=="ROG OMNI RECEIVER"||m=="ROG HARPE II EXTREME EDITION 20"||m=="ROG DELTA II-KJP"||m=="ROG AZOTH EXTREME EDITION 20")){
    string path=Path.Combine(BasePath,"DeviceDependentVersion",model,model+".json");
    try{var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(ReadBounded(path,128*1024));var rows=new List<CachedUpdateRequirement>();var raw=Get(json,"devicepage_requirement") as IEnumerable;if(raw==null)throw new InvalidDataException("Dependency requirements are unavailable.");foreach(object element in raw){var row=element as Dictionary<string,object>;if(row==null)throw new InvalidDataException("Unexpected requirement schema.");string key=Convert.ToString(Get(row,"regkey")),minimum=Convert.ToString(Get(row,"minVersion"));HashSet<string> installed;string current=versions.TryGetValue(key,out installed)&&installed.Count==1?installed.First():null;rows.Add(new CachedUpdateRequirement{name=Convert.ToString(Get(row,"name")),profileId=Convert.ToString(Get(row,"profileid")),category=Convert.ToString(Get(row,"Category")),registryKey=key,minimumVersion=minimum,cachedInstalledVersion=current,minimumStatus=CompareMinimum(current,minimum)});}dependencies.Add(new CachedDeviceDependency{model=model,sourcePath=path,cacheModifiedUtc=File.GetLastWriteTimeUtc(path).ToString("o"),devicePageRequirements=rows.ToArray()});}catch(Exception e){errors.Add(O("source",model+" dependency cache","message",e.Message));}
   }
   try{string setup=ReadBounded(Path.Combine(BasePath,"RLSSetup.log"),512*1024);foreach(Match m in Regex.Matches(setup,@"\[(?<when>[0-9 :-]{19})\].*MSI installation exit code: (?<code>[0-9]+)"))diagnostics.Add(O("source","RLSSetup.log","timeLocalUnspecified",m.Groups["when"].Value,"type","msi_exit_code","code",Int32.Parse(m.Groups["code"].Value),"interpretation",null));}catch(Exception e){errors.Add(O("source","RLSSetup.log","message",e.Message));}
   try{string logs=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"ASUS","ALL");if(Directory.Exists(logs))foreach(string path in Directory.GetFiles(logs,"Armoury Crate Full Installation Package_*.log").OrderByDescending(p=>p).Take(3)){string setup=ReadBounded(path,4*1024*1024);foreach(Match m in Regex.Matches(setup,@"(?<when>[0-9 :-]{19})\s+\[Superset\]\[wmain\] bRunCmd\(\) failed \((?<run>[0-9]+), (?<code>[0-9]+)\)"))diagnostics.Add(O("source",Path.GetFileName(path),"timeLocalUnspecified",m.Groups["when"].Value,"type","superset_command_failure","runCode",Int32.Parse(m.Groups["run"].Value),"code",Int32.Parse(m.Groups["code"].Value),"interpretation",null));}}catch(Exception e){errors.Add(O("source","Full Installation Package logs","message",e.Message));}
   return O("sourceKind","local_updater_cache","liveServerChecked",false,"latestKnown",false,"downloadEnabled",false,"installationEnabled",false,"hardwareAccess",false,"catalog",cache,"dependencies",dependencies.ToArray(),"diagnostics",diagnostics.ToArray(),"errors",errors.ToArray(),"limitation","Local server-version and installed-version fields are cache observations only. Server proof, package hash and ASUS Authenticode verification are required before enabling downloads or installation.");
  }
  static object Get(Dictionary<string,object> json,string key){object value;return json.TryGetValue(key,out value)?value:null;}
  static void IndexVersions(string ini,Dictionary<string,HashSet<string>> index){var section=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);Action save=()=>{foreach(var pair in section){Match m=Regex.Match(pair.Key,@"^(?<prefix>.+)_regkey_(?<i>[0-9]+)$",RegexOptions.IgnoreCase);if(!m.Success)continue;string version;if(!section.TryGetValue(m.Groups["prefix"].Value+"_LocalVersion_"+m.Groups["i"].Value,out version)||!Regex.IsMatch(version,@"^[0-9]+(?:\.[0-9]+){0,7}$"))continue;HashSet<string> known;if(!index.TryGetValue(pair.Value,out known)){known=new HashSet<string>(StringComparer.Ordinal);index[pair.Value]=known;}known.Add(version);}};using(var reader=new StringReader(ini)){string line;while((line=reader.ReadLine())!=null){line=line.Trim();if(line.StartsWith("[")){save();section.Clear();continue;}int equal=line.IndexOf('=');if(equal>0)section[line.Substring(0,equal).Trim()]=line.Substring(equal+1).Trim();}save();}}
  public static string CompareMinimum(string current,string minimum){if(String.IsNullOrEmpty(current)||String.IsNullOrEmpty(minimum)||!Regex.IsMatch(current,@"^[0-9]+(?:\.[0-9]+){0,7}$")||!Regex.IsMatch(minimum,@"^[0-9]+(?:\.[0-9]+){0,7}$"))return "unknown";int[] a,b;try{a=current.Split('.').Select(v=>Int32.Parse(v)).ToArray();b=minimum.Split('.').Select(v=>Int32.Parse(v)).ToArray();}catch{return "unknown";}for(int i=0;i<Math.Max(a.Length,b.Length);i++){int left=i<a.Length?a[i]:0,right=i<b.Length?b[i]:0;if(left<right)return "below_cached_minimum";if(left>right)return "meets_cached_minimum";}return "meets_cached_minimum";}
  static string ReadBounded(string path,int limit){var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("Updater cache is unavailable.");if(info.Length>limit)throw new IOException("Updater cache exceeds the supported size.");return File.ReadAllText(path,Encoding.UTF8);}
  public static Dictionary<string,object> ParseCatalog(string xml){
   var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024};var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(xml),settings))doc.Load(reader);
   if(doc.DocumentElement==null||doc.DocumentElement.Name!="root")throw new InvalidDataException("Unexpected updater catalog structure.");var groups=new List<object>();
   foreach(XmlNode group in doc.SelectNodes("/root/Name")){string name=Attribute(group,"key");if(!Models.Contains(name))continue;var items=new List<object>();
    foreach(XmlNode item in group.SelectNodes("item")){var local=item.SelectNodes("LocalVersion");var server=item.SelectNodes("ServerVersion");var dates=item.SelectNodes("ModifyDate");if(local.Count==0&&server.Count==0)continue;
     int count=Math.Max(local.Count,server.Count);for(int i=0;i<count;i++){string a=i<local.Count?local[i].InnerText:null,b=i<server.Count?server[i].InnerText:null;items.Add(O("module",Attribute(item,"key"),"instance",i+1,"cachedInstalledVersion",a,"cachedServerVersion",b,"cachedDate",i<dates.Count?dates[i].InnerText:null,"latestStatus","unknown","versionsEqualInCache",a!=null&&b!=null?(object)(a==b):null));}
    }
    groups.Add(O("name",name,"displayName",Attribute(group,"Displayname"),"type",Attribute(group,"type"),"cachedErrorCode",Attribute(group,"ErrorCode"),"cachedDependencyStatusCode",Attribute(group,"DependentAppStatus"),"cachedDownloadedFlag",Attribute(group,"Downloaded"),"items",items.ToArray()));
   }
   return O("schemaVersion",doc.SelectSingleNode("/root/Version")==null?null:doc.SelectSingleNode("/root/Version").InnerText,"items",groups.ToArray());
  }
 }
}
