// Independent OpenAC adapter for the installed AIO sensor ABI. See TelemetryWorker-protocol.md.
// Read allowlist: capability 0 and sensor query 36. No application functions copied from ASUS/G-Helper.
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace OpenAcTelemetry {
 public sealed class TelemetryError : Exception {
  public readonly string Code;
  public TelemetryError(string code,string message):base(message){Code=code;}
 }
 public sealed class TelemetrySensor {
  public string Id,NativeId,Name,DefaultName,Unit;
  public int Type;
  public bool Ambiguous;
  public Dictionary<string,object> Snapshot() {
   return TelemetryRules.Obj("id",Id,"nativeId",NativeId,"name",Name,"defaultName",DefaultName,"nativeType",Type,"category",TelemetryRules.Category(Type),"unit",Unit,"identityConfirmed",!Ambiguous,"selectable",!Ambiguous,"gpuIdentity",null,"value",null,"rawNativeValue",null,"nativeValueReadbackConfirmed",false,"valueReadbackConfirmed",false,"placeholder",false,"writable",false);
  }
 }
 public static class TelemetryRules {
  public static Dictionary<string,object> Obj(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];return d;}
  public static bool KnownModel(string id){return id=="7299"||id=="7124"||id=="7571";}
  public static string Category(int type){switch(type){case 0:return "voltage";case 1:return "temperature";case 2:return "fanSpeed";case 4:return "frequency";case 6:return "nativeOther";default:return "unknown";}}
  public static XmlDocument Parse(string text){if(text==null||text.Length>2097152)throw new TelemetryError("INVALID_NATIVE_RESPONSE","Response is too large");var d=new XmlDocument{XmlResolver=null};using(var r=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2097152}))d.Load(r);return d;}
  public static string Text(XmlNode n,string path){var c=n==null?null:n.SelectSingleNode(path);return c==null?null:c.InnerText;}
  static bool ValidText(string s){if(String.IsNullOrEmpty(s)||s.Length>256)return false;foreach(char c in s)if(Char.IsControl(c))return false;return true;}
  // A category is not a unit. Only an explicit recognized native unit can confirm a physical value.
  static string Unit(int type,string unit){if(type==0&&unit=="V"||type==1&&(unit=="C"||unit=="°C")||type==2&&(unit=="RPM"||unit=="rpm")||type==4&&(unit=="Hz"||unit=="kHz"||unit=="MHz"||unit=="GHz"))return unit;return null;}
  public static List<TelemetrySensor> Capabilities(string text,string model){
   if(!KnownModel(model))throw new TelemetryError("UNSUPPORTED_MODEL","Model is not on the source-prepared allowlist");
   var d=Parse(text);var matches=d.SelectNodes("/root/capability/device_list[deviceid='"+model+"']");
   if(matches.Count!=1)throw new TelemetryError("IDENTITY_UNCONFIRMED","Capability must identify exactly the requested model");
   var nodes=d.SelectNodes("/root/capability/player[@key='hw_monitor_player']/sensor_list/sensor");
   if(nodes.Count>256)throw new TelemetryError("INVALID_NATIVE_RESPONSE","Too many sensors");
   var result=new List<TelemetrySensor>();
   foreach(XmlNode n in nodes){
    uint nativeId;int type;string id=n.Attributes["key"]==null?null:n.Attributes["key"].Value,name=Text(n,"name"),defaultName=Text(n,"default_name");
    if(id==null||!UInt32.TryParse(id,NumberStyles.None,CultureInfo.InvariantCulture,out nativeId)||nativeId==0||!ValidText(name)||!ValidText(defaultName)||!Int32.TryParse(Text(n,"type"),NumberStyles.None,CultureInfo.InvariantCulture,out type)||type<0||type>65535)throw new TelemetryError("INVALID_NATIVE_RESPONSE","Invalid sensor identity");
    result.Add(new TelemetrySensor{Id="sensor-"+result.Count,NativeId=id,Name=name,DefaultName=defaultName,Type=type,Unit=Unit(type,Text(n,"unit"))});
   }
   foreach(var a in result)foreach(var b in result)if(!Object.ReferenceEquals(a,b)&&a.NativeId==b.NativeId&&a.DefaultName==b.DefaultName&&a.Type==b.Type)a.Ambiguous=true;
   return result;
  }
  public static List<TelemetrySensor> Select(List<TelemetrySensor> capabilities,object raw){
   var list=raw as IList;if(list==null||list.Count<1||list.Count>5)throw new TelemetryError("INVALID_REQUEST","sensorIds must contain 1 to 5 unique scanned IDs");
   var selected=new List<TelemetrySensor>();var seen=new HashSet<string>(StringComparer.Ordinal);
   foreach(object item in list){string id=item as string;TelemetrySensor found=null;if(id==null||!seen.Add(id))throw new TelemetryError("INVALID_REQUEST","Sensor IDs must be unique strings");foreach(var s in capabilities)if(s.Id==id)found=s;if(found==null)throw new TelemetryError("INVALID_REQUEST","Sensor ID was not scanned");if(found.Ambiguous)throw new TelemetryError("AMBIGUOUS_SENSOR","The SDK cannot distinguish this capability from another device");selected.Add(found);}return selected;
  }
  public static string Query(string model,int function,List<TelemetrySensor> selected){
   if(!KnownModel(model)||(function!=0&&function!=36))throw new TelemetryError("INVALID_REQUEST","Query is not on the read allowlist");
   var body=new StringBuilder();if(function==36){if(selected==null||selected.Count<1||selected.Count>5)throw new TelemetryError("INVALID_REQUEST","A bounded sensor selection is required");body.Append("<settings><get_sensor_data>");for(int i=0;i<selected.Count;i++){var s=selected[i];if(s.Ambiguous)throw new TelemetryError("AMBIGUOUS_SENSOR","Ambiguous capability");body.Append("<sensor key=\"").Append(i).Append("\"><id>").Append(System.Security.SecurityElement.Escape(s.NativeId)).Append("</id><default_name>").Append(System.Security.SecurityElement.Escape(s.DefaultName)).Append("</default_name><type>").Append(s.Type).Append("</type></sensor>");}body.Append("</get_sensor_data></settings>");}
   return "<root><device_type key=\"5\"><device key=\""+model+"\"><function key=\""+function+"\">"+body+"</function></device></device_type></root>";
  }
  public static List<object> Values(string text,List<TelemetrySensor> selected,List<TelemetrySensor> all){
   var d=Parse(text);var nodes=d.SelectNodes("/root/sensor | /sensors/sensor");if(nodes.Count>256)throw new TelemetryError("INVALID_NATIVE_RESPONSE","Too many returned sensors");var result=new List<object>();
   foreach(var s in selected){var value=s.Snapshot();XmlNode match=null;bool ambiguous=false;
    foreach(XmlNode n in nodes){if(Text(n,"id")!=s.NativeId)continue;string name=Text(n,"default_name"),type=Text(n,"type");if(name!=null&&name!=s.DefaultName||type!=null&&type!=s.Type.ToString(CultureInfo.InvariantCulture))continue;
     if(name==null||type==null)foreach(var other in all)if(!Object.ReferenceEquals(s,other)&&other.NativeId==s.NativeId&&(name==null||other.DefaultName==name)&&(type==null||other.Type.ToString(CultureInfo.InvariantCulture)==type))ambiguous=true;
     if(match!=null)ambiguous=true;match=n;
    }
    double number;string raw=Text(match,"value");
    if(ambiguous)value["error"]="AMBIGUOUS_NATIVE_IDENTITY";
    else if(!Double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out number)||Double.IsNaN(number)||Double.IsInfinity(number))value["error"]="NO_FINITE_NATIVE_VALUE";
    else {
     value["rawNativeValue"]=number;value["nativeValueReadbackConfirmed"]=true;
     // Observed on the powered local desktop: all selected clock/temperature getters returned zero.
     // No SDK validity flag was supplied. Preserve this response without presenting zero as live telemetry.
     if(number==0&&(s.Type==1||s.Type==4)){value["placeholder"]=true;value["error"]="ZERO_TEMPERATURE_OR_FREQUENCY_UNCONFIRMED";}
     else if(s.Unit==null)value["error"]="UNIT_UNCONFIRMED";
     else if(number<0)value["error"]="NEGATIVE_NATIVE_VALUE_UNCONFIRMED";
     else{value["value"]=number;value["valueReadbackConfirmed"]=true;}
    }result.Add(value);
   }return result;
  }
 }
 public sealed class TelemetrySession {
  readonly Func<string,string> Transport;
  List<TelemetrySensor> Sensors=new List<TelemetrySensor>();string Model;
  public TelemetrySession(Func<string,string> transport){Transport=transport;}
  static void Fields(Dictionary<string,object> r,params string[] allowed){var a=new HashSet<string>(allowed);foreach(string k in r.Keys)if(!a.Contains(k))throw new TelemetryError("INVALID_REQUEST","Unknown request field");}
  public object Handle(Dictionary<string,object> r){
   object opRaw;string op=r.TryGetValue("op",out opRaw)?opRaw as string:null;
   if(op=="status"){Fields(r,"id","op");return TelemetryRules.Obj("scannedModel",Model,"capabilityCount",Sensors.Count,"writable",false,"hardwareSettingsWrites",0);}
   if(op=="scan"){
    Fields(r,"id","op","deviceId");object raw;string requested=r.TryGetValue("deviceId",out raw)?raw as string:"7299";if(!TelemetryRules.KnownModel(requested))throw new TelemetryError("UNSUPPORTED_MODEL","Model is not on the source-prepared allowlist");
    Model=null;Sensors.Clear();var scanned=TelemetryRules.Capabilities(Transport(TelemetryRules.Query(requested,0,null)),requested);Model=requested;Sensors=scanned;var snapshots=new List<object>();foreach(var s in scanned)snapshots.Add(s.Snapshot());return TelemetryRules.Obj("deviceId",Model,"source","installedAioSensor36","sourcePrepared",true,"physicalValidation",false,"sensors",snapshots,"hardwareSettingsWrites",0);
   }
   if(op=="read"){
    Fields(r,"id","op","deviceId","sensorIds");object modelRaw;if(!r.TryGetValue("deviceId",out modelRaw)||!(modelRaw is string)||!TelemetryRules.KnownModel((string)modelRaw))throw new TelemetryError("UNSUPPORTED_MODEL","Choose an exact source-prepared model");object selection;if(!r.TryGetValue("sensorIds",out selection))throw new TelemetryError("INVALID_REQUEST","sensorIds is required");var selected=TelemetryRules.Select(Sensors,selection);if(Model!=(string)modelRaw)throw new TelemetryError("DEVICE_NOT_SCANNED","Scan this model before reading");return TelemetryRules.Obj("deviceId",Model,"source","installedAioSensor36","sensors",TelemetryRules.Values(Transport(TelemetryRules.Query(Model,36,selected)),selected,Sensors),"hardwareSettingsWrites",0);
   }
   throw new TelemetryError("INVALID_REQUEST","Only scan, read and status are supported");
  }
 }
 public static class TelemetryWorker {
  const string DirectoryPath=@"C:\Program Files (x86)\ASUS\ArmouryDevice\dll\AIOFanSDK";
  public const string AioMutexName="Local\\OpenAC.AioSdk";
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=65536};
  static Post NativePost;static Free NativeFree;static bool Loaded;
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetDllDirectory(string path);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibrary(string path);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr module,string name);
  [DllImport("kernel32.dll")]static extern IntPtr GetStdHandle(int id);
  [DllImport("kernel32.dll")]static extern bool SetStdHandle(int id,IntPtr handle);
  [DllImport("kernel32.dll")]static extern uint SetErrorMode(uint mode);
  [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr reserved,uint mode);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Start(int port);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Post(IntPtr zero1,IntPtr input,int bytes,IntPtr zero2,IntPtr zero3,out IntPtr output,out int outputBytes,out int flag);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int Free(ref IntPtr output);
  static IntPtr Export(IntPtr library,string name){var p=GetProcAddress(library,name);if(p==IntPtr.Zero)throw new TelemetryError("SDK_EXPORT_MISSING","Installed SDK export missing");return p;}
  static void Load(){
   if(Loaded)return;if(IntPtr.Size!=4)throw new TelemetryError("WRONG_ARCHITECTURE","Compile this worker for x86");string path=Path.Combine(DirectoryPath,"ArmouryAIOSDK.dll");if(!File.Exists(path))throw new TelemetryError("SDK_NOT_INSTALLED","Installed AIO SDK is required");
   System.IO.Directory.SetCurrentDirectory(DirectoryPath);if(!SetDllDirectory(DirectoryPath))throw new TelemetryError("SDK_LOAD_FAILED","Cannot select the installed SDK directory");int hr=CoInitializeEx(IntPtr.Zero,2);if(hr<0)throw new TelemetryError("COM_APARTMENT_FAILED","Cannot initialize isolated STA");var lib=LoadLibrary(path);if(lib==IntPtr.Zero)throw new TelemetryError("SDK_LOAD_FAILED","Cannot load the installed SDK");var start=(Start)Marshal.GetDelegateForFunctionPointer(Export(lib,"startService"),typeof(Start));NativePost=(Post)Marshal.GetDelegateForFunctionPointer(Export(lib,"Post_EntrypointReturn"),typeof(Post));NativeFree=(Free)Marshal.GetDelegateForFunctionPointer(Export(lib,"freeBuffer"),typeof(Free));if(start(0)!=0)throw new TelemetryError("SDK_LOADER_REJECTED","Ordinary SDK loader rejected the request");Loaded=true;
  }
  static string Native(string xml){
   // Same lock as SDKWorker's LCD transport. It covers loader initialization and each native query.
   using(var mutex=new Mutex(false,AioMutexName)){bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new TelemetryError("SDK_BUSY","Another OpenAC AIO query is active");Load();var input=Marshal.StringToHGlobalUni(xml);IntPtr output=IntPtr.Zero;try{int bytes,flag;int status=NativePost(IntPtr.Zero,input,Encoding.Unicode.GetByteCount(xml),IntPtr.Zero,IntPtr.Zero,out output,out bytes,out flag);if(status!=0)throw new TelemetryError("SDK_QUERY_REJECTED","Installed SDK rejected the read");if(output==IntPtr.Zero||bytes<2||bytes>4194304||bytes%2!=0)throw new TelemetryError("INVALID_NATIVE_RESPONSE","Invalid native output buffer");return Marshal.PtrToStringUni(output,bytes/2).TrimEnd('\0');}finally{if(output!=IntPtr.Zero)NativeFree(ref output);Marshal.FreeHGlobal(input);}}finally{if(held)mutex.ReleaseMutex();}}
  }
  public static string Reply(TelemetrySession session,string line){object id=null;try{if(line==null||line.Length>65536)throw new TelemetryError("INVALID_REQUEST","Request is too large");var r=Json.Deserialize<Dictionary<string,object>>(line);if(r==null)throw new TelemetryError("INVALID_REQUEST","Request must be an object");r.TryGetValue("id",out id);return Json.Serialize(TelemetryRules.Obj("id",id,"ok",true,"data",session.Handle(r)));}catch(Exception ex){var known=ex as TelemetryError;return Json.Serialize(TelemetryRules.Obj("id",id,"ok",false,"error",TelemetryRules.Obj("code",known==null?"QUERY_FAILED":known.Code,"message",known==null?"Sensor read failed":known.Message)));}}
  static object RequestId(string line){try{var r=Json.Deserialize<Dictionary<string,object>>(line);object id;if(r!=null&&r.TryGetValue("id",out id))return id;}catch{}return null;}
  public sealed class Executor {
   sealed class Job {public Func<string> Action;public string Result;public Exception Error;public readonly ManualResetEvent Done=new ManualResetEvent(false);}
   readonly BlockingCollection<Job> Queue=new BlockingCollection<Job>();bool Poisoned;
   public Executor(){var thread=new Thread(()=>{foreach(var job in Queue.GetConsumingEnumerable()){try{job.Result=job.Action();}catch(Exception ex){job.Error=ex;}finally{lock(job)job.Done.Set();}}}){IsBackground=true};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
   // One permanent STA owns native state. A timed-out job is never cancelled/reused in process.
   public string Run(Func<string> operation,int milliseconds,object id,out bool restart){if(Poisoned)throw new TelemetryError("RESTART_REQUIRED","The native worker is quarantined");var job=new Job{Action=operation};Queue.Add(job);if(!job.Done.WaitOne(milliseconds)){Poisoned=true;restart=true;return Json.Serialize(TelemetryRules.Obj("id",id,"ok",false,"restartRequired",true,"error",TelemetryRules.Obj("code","SDK_TIMEOUT","message","The isolated sensor request exceeded its deadline")));}lock(job)job.Done.Dispose();restart=false;if(job.Error!=null)throw job.Error;return job.Result;}
  }
  public static int Main(string[] args){
   if(args.Length!=0)return 2;var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};SetStdHandle(-11,GetStdHandle(-12));Console.SetOut(Console.Error);SetErrorMode(0x8007);var session=new TelemetrySession(Native);var executor=new Executor();string line;
   while((line=OpenAc.WorkerTextProtocol.ReadLine())!=null){bool restart;string requestLine=line;string response=executor.Run(()=>Reply(session,requestLine),25000,RequestId(requestLine),out restart);output.WriteLine(response);if(restart)return 4;}return 0;
  }
 }
}
