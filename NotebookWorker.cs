// Independently written OpenAC notebook adapter. See NotebookWorker-protocol.md.
// G-Helper / Linux were consulted for interface facts; no application functions were copied.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

namespace RogControlNotebook {
 public sealed class NotebookIdentity {
  public string Manufacturer, Model;
  public int[] ChassisTypes = new int[0];
  public bool IdentityReadConfirmed, InterfaceDriverConfirmed;
  public bool? AcOnline;
  public List<object> InterfaceDrivers = new List<object>();
 }
 public sealed class NotebookModel {
  public string Code, Family, Name, OfficialSource;
  public NotebookModel(string code,string family,string name,string source) { Code=code;Family=family;Name=name;OfficialSource=source; }
 }
 public sealed class NotebookGate {
  public bool Eligible;
  public string Reason;
  public NotebookModel Model;
 }
 public sealed class NotebookFeature {
  public string Name, Unit, Semantics;
  public uint Endpoint;
  public int Minimum, Maximum, Scale;
  public bool NotebookOnly;
  public NotebookFeature(string name,uint endpoint,string unit,string semantics,int minimum,int maximum,int scale,bool notebookOnly) {
   Name=name;Endpoint=endpoint;Unit=unit;Semantics=semantics;Minimum=minimum;Maximum=maximum;Scale=scale;NotebookOnly=notebookOnly;
  }
 }
 public static class NotebookRules {
  // Exact official model codes, not a declaration that every feature was physically tested.
  public static readonly NotebookModel[] Models = {
   new NotebookModel("RC71L","handheld","ROG Ally (2023)","https://rog.asus.com/gaming-handhelds/rog-ally/rog-ally-2023/"),
   new NotebookModel("RC72LA","handheld","ROG Ally X (2024)","https://rog.asus.com/gaming-handhelds/rog-ally/rog-ally-x-2024/"),
   new NotebookModel("RC73YA","handheld","ROG Xbox Ally (2025)","https://rog.asus.com/support/faq/1050220/"),
   new NotebookModel("RC73XA","handheld","ROG Xbox Ally X (2025)","https://rog.asus.com/support/faq/1050220/"),
   new NotebookModel("GA402RJ","rogNotebook","ROG Zephyrus G14 (2022)","https://www.asus.com/supportonly/ga402rj/helpdesk_download/"),
   new NotebookModel("GU605MI","rogNotebook","ROG Zephyrus G16 (2024)","https://rog.asus.com/ru/laptops/rog-zephyrus/rog-zephyrus-g16-2024/spec/"),
   new NotebookModel("FX507ZC4","tufNotebook","ASUS TUF Gaming F15", "https://www.asus.com/supportonly/fx507zc4/helpdesk_download/"),
   new NotebookModel("FA507NV","tufNotebook","ASUS TUF Gaming A15","https://www.asus.com/us/supportonly/fa507nv/helpdesk_download/"),
   new NotebookModel("GA403UI","rogNotebook","ROG Zephyrus G14 (2024)","https://www.asus.com/us/supportonly/ga403ui/helpdesk_download/"),
   new NotebookModel("GU605MZ","rogNotebook","ROG Zephyrus G16 (2024)","https://km-ap.asus.com/uploads/AttachLibrarys/8a53fc79-6a00-4a96-8408-a7e886967aef/20240506160932760_ASUS_System_Control_Interface_v3modellist.pdf")
  };
  static readonly NotebookFeature[] Features = {
   new NotebookFeature("cpuFan",0x00110013,"rpm","fanSpeed",0,120,100,false),
   new NotebookFeature("gpuFan",0x00110014,"rpm","fanSpeed",0,120,100,false),
   new NotebookFeature("middleFan",0x00110031,"rpm","fanSpeed",0,120,100,true),
   new NotebookFeature("cpuTemperature",0x00120094,"C","temperature",0,150,1,false),
   new NotebookFeature("gpuTemperature",0x00120097,"C","temperature",0,150,1,false),
   new NotebookFeature("chargeLimit",0x00120057,"percent","nativeStatus",0,100,1,false),
   new NotebookFeature("chargerMode",0x0012006C,"code","chargerType",1,2,1,false),
   new NotebookFeature("performanceMode",0x00120075,"code","nativeStatus",0,4,1,false),
   new NotebookFeature("gpuEco",0x00090020,"code","nativeStatus",0,1,1,true),
   new NotebookFeature("gpuMux",0x00090016,"code","nativeStatus",0,1,1,true),
   new NotebookFeature("keyboardBrightness",0x00050021,"level","brightness",0,3,1,true),
   new NotebookFeature("displayOverdrive",0x00050019,"code","nativeStatus",0,1,1,true)
  };
  public static Dictionary<string,object> Obj(params object[] pairs) { var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];return d; }
  public static bool IsAsus(string text) {
   if(text==null||text.Length>256)return false;
   string normalized=Regex.Replace(text.ToUpperInvariant(),"[^A-Z0-9]","");
   return normalized=="ASUS"||normalized=="ASUSTEKCOMPUTERINC";
  }
  public static NotebookGate Gate(NotebookIdentity identity) {
   var gate=new NotebookGate { Reason="IDENTITY_UNCONFIRMED" };
   if(identity==null||!identity.IdentityReadConfirmed)return gate;
   if(!IsAsus(identity.Manufacturer)){gate.Reason="NOT_ASUS_SYSTEM";return gate;}
   if(identity.Model==null||identity.Model.Length==0||identity.Model.Length>256)return gate;
   if(identity.ChassisTypes==null||identity.ChassisTypes.Length==0){gate.Reason="MOBILE_CHASSIS_UNCONFIRMED";return gate;}
   foreach(int chassis in identity.ChassisTypes)
    if(chassis!=8&&chassis!=9&&chassis!=10&&chassis!=11&&chassis!=14&&chassis!=30&&chassis!=31&&chassis!=32) {
     gate.Reason="NOT_CONFIRMED_MOBILE_CHASSIS";return gate;
    }
   foreach(var model in Models) if(Regex.IsMatch(identity.Model,"(?<![A-Z0-9])"+Regex.Escape(model.Code)+"(?![A-Z0-9])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)) {
    if(gate.Model!=null){gate.Model=null;gate.Reason="AMBIGUOUS_MODEL";return gate;}gate.Model=model;
   }
   if(gate.Model==null){gate.Reason="MODEL_NOT_PREPARED";return gate;}
   if(!identity.InterfaceDriverConfirmed){gate.Reason="SYSTEM_CONTROL_INTERFACE_UNCONFIRMED";return gate;}
   gate.Eligible=true;gate.Reason="SOURCE_PREPARED_READ_ONLY";return gate;
  }
  public static List<NotebookFeature> Select(NotebookModel model, object selection) {
   IList names=selection as IList;
   if(selection!=null&&(names==null||names.Count<1||names.Count>12))throw new NotebookError("INVALID_REQUEST","features must contain 1 to 12 unique feature names");
   if(names==null) names=new string[]{"cpuFan","gpuFan","performanceMode","chargeLimit"};
   var selected=new List<NotebookFeature>();var seen=new HashSet<string>(StringComparer.Ordinal);
   foreach(object raw in names) {
    var name=raw as string;NotebookFeature found=null;
    if(name==null||!seen.Add(name))throw new NotebookError("INVALID_REQUEST","Feature names must be strings and unique");
    foreach(var feature in Features)if(feature.Name==name)found=feature;
    if(found==null)throw new NotebookError("INVALID_REQUEST","Feature is not on the read allowlist");
    if(model!=null&&model.Family=="handheld"&&found.NotebookOnly)throw new NotebookError("UNSUPPORTED_FEATURE","This notebook feature is not prepared for the handheld model");
    selected.Add(found);
   }
   return selected;
  }
  public static bool AllowedEndpoint(uint endpoint) { foreach(var feature in Features)if(feature.Endpoint==endpoint)return true;return NotebookControlRules.SupportEndpoint(endpoint); }
  // No method-ID or endpoint input from JSON; DEVS has a separate fixed typed builder.
  public static byte[] StatusPacket(uint endpoint) {
   if(!AllowedEndpoint(endpoint))throw new NotebookError("INVALID_REQUEST","Endpoint is not on the status allowlist");
   byte[] packet=new byte[16];Put32(packet,0,0x53545344);Put32(packet,4,8);Put32(packet,8,endpoint);return packet;
  }
  static void Put32(byte[] target,int offset,uint value) { for(int i=0;i<4;i++)target[offset+i]=(byte)(value>>(8*i)); }
  static uint Get32(byte[] source) { return (uint)source[0]|((uint)source[1]<<8)|((uint)source[2]<<16)|((uint)source[3]<<24); }
  public static Dictionary<string,object> Decode(NotebookFeature feature,byte[] response) {
   var state=Obj("feature",feature.Name,"unit",feature.Unit,"value",null,"queryValue",null,"rawStatus",null,"presenceConfirmed",false,"readbackConfirmed",false,"valueReadbackConfirmed",false,"valueSemantics",feature.Semantics,"physicalValidation",false,"writable",false);
   if(response==null||response.Length<4||response.Length>16){state["error"]="INVALID_RESPONSE_SIZE";return state;}
   uint raw=Get32(response);state["rawStatus"]="0x"+raw.ToString("X8",CultureInfo.InvariantCulture);
   if(raw==0xFFFFFFFE||raw==0xFFFFFFFF){state["error"]="ENDPOINT_UNSUPPORTED";return state;}
   if((raw&0x00010000)==0){state["error"]="ENDPOINT_PRESENCE_UNCONFIRMED";return state;}
   // Known presence/user/BIOS status flags; unknown flag bits are not silently removed.
   if((raw&0xFFF80000)!=0){state["error"]="UNKNOWN_STATUS_FLAGS";return state;}
   state["presenceConfirmed"]=true;state["readbackConfirmed"]=true;
   int value=(int)(raw&0xFFFF);
   if(feature.Name=="keyboardBrightness"){value=(int)(raw&0x7F);if((raw&0x20000)!=0||(raw&0xFF00)>0x0300){state["error"]="BRIGHTNESS_UNKNOWN";return state;}}
   if(value<feature.Minimum||value>feature.Maximum){state["error"]="STATUS_VALUE_OUTSIDE_PREPARED_RANGE";return state;}
   state["queryValue"]=value;
   if(feature.Semantics!="nativeStatus") { state["value"]=checked(value*feature.Scale);state["valueReadbackConfirmed"]=true; }
   // Native status endpoints are not asserted to be the current configured setting.
   if(feature.Name=="chargerMode")state["label"]=value==1?"barrel":"usb";
   return state;
  }
 }
 public sealed class NotebookError:Exception {
  public readonly string Code;
  public NotebookError(string code,string message):base(message){Code=code;}
 }
 public interface INotebookTransport:IDisposable { byte[] ReadStatus(uint endpoint); }
 public sealed partial class NotebookService {
  readonly Func<NotebookIdentity> identity;
  readonly Func<INotebookTransport> open;
  readonly int budgetMs;
  readonly Func<long> clock;
  long operationDeadline;
  int executing;
  int quarantined;
  Thread operationThread;
  readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=16384 };
  public bool RestartRequired { get { return Interlocked.CompareExchange(ref quarantined,0,0)!=0; } }
  public NotebookService(Func<NotebookIdentity> identity,Func<INotebookTransport> open,int budgetMs,Func<long> clock=null) { if(identity==null||open==null||budgetMs<1||budgetMs>8000)throw new ArgumentException("Invalid notebook service configuration");this.identity=identity;this.open=open;this.budgetMs=budgetMs;var elapsed=Stopwatch.StartNew();this.clock=clock??(()=>elapsed.ElapsedMilliseconds); }
  void CheckUsable() { if(RestartRequired)throw new NotebookError("RESTART_REQUIRED","The timed out worker cannot be reused");if(Interlocked.CompareExchange(ref executing,0,0)!=0&&clock()>=operationDeadline){Interlocked.Exchange(ref quarantined,1);throw new NotebookError("QUERY_TIMEOUT","Notebook operation exceeded its deadline");} }
  Dictionary<string,object> Snapshot(NotebookIdentity current,NotebookGate gate) {
   return NotebookRules.Obj("manufacturer",current==null?null:current.Manufacturer,"model",current==null?null:current.Model,"chassisTypes",current==null?new int[0]:current.ChassisTypes,"identityReadConfirmed",current!=null&&current.IdentityReadConfirmed,"interfaceDriverConfirmed",current!=null&&current.InterfaceDriverConfirmed,"interfaceDrivers",current==null?new List<object>():current.InterfaceDrivers,"modelCode",gate.Model==null?null:gate.Model.Code,"family",gate.Model==null?null:gate.Model.Family,"name",gate.Model==null?null:gate.Model.Name,"eligibleForStatus",gate.Eligible,"supportReason",gate.Reason,"sourcePrepared",gate.Model!=null,"hardwareReadValidated",false,"supportsWrite",false,"supportsFanCurve",false,"supportsPowerLimits",false,"supportsLedWrites",false,"capabilities",new object[0],"states",new object[0],"driverQueryPerformed",false,"identityScope","osReportedManufacturerModelChassis");
  }
  object Run(Dictionary<string,object> request,string op) {
   CheckUsable();
   if(op=="controlRead"||op=="set")ValidateControlRequest(request,op);
   // Validate every caller field and feature before any WMI/driver I/O.
   if(op=="scan"||op=="read")foreach(string key in request.Keys)if(key!="id"&&key!="op"&&!(op=="read"&&key=="features"))throw new NotebookError("INVALID_REQUEST","Unknown request field "+key);
   object selection=null;if(request.ContainsKey("features")){selection=request["features"];if(selection==null)throw new NotebookError("INVALID_REQUEST","Explicit features must be an array");}
   var features=NotebookRules.Select(null,selection);
   CheckUsable();var current=identity();var gate=NotebookRules.Gate(current);var data=Snapshot(current,gate);
   CheckUsable();if(op=="controlRead"||op=="set")return NotebookControlRun(request,op,current,gate,data);
   controlLease=null;
   if(op=="scan"||!gate.Eligible)return data;
   features=NotebookRules.Select(gate.Model,selection);CheckUsable();
   var states=new List<object>();var capabilities=new List<object>();
   using(var transport=open()) {
    if(transport==null)throw new NotebookError("INTERFACE_UNAVAILABLE","No ASUS System Control Interface connection");
    foreach(var feature in features) {
     CheckUsable();Dictionary<string,object> state;
     try { state=NotebookRules.Decode(feature,transport.ReadStatus(feature.Endpoint)); }
     catch(NotebookError){throw;}
     catch(Win32Exception ex){throw new NotebookError("INTERFACE_QUERY_FAILED","Status query failed with Win32 error "+ex.NativeErrorCode.ToString(CultureInfo.InvariantCulture)+"; no further queries attempted");}
     CheckUsable();states.Add(state);capabilities.Add(NotebookRules.Obj("feature",feature.Name,"endpointPresent",(bool)state["presenceConfirmed"],"readSupported",state["queryValue"]!=null,"writeSupported",false,"physicalValidation",false));
    }
   }
   data["states"]=states;data["capabilities"]=capabilities;data["driverQueryPerformed"]=true;data["driverQueryMethod"]="DSTS";return data;
  }
  object Bounded(Dictionary<string,object> request,string op) {
   if(Interlocked.CompareExchange(ref executing,1,0)!=0)throw new NotebookError("INTERFACE_BUSY","Another notebook operation remains in progress");operationDeadline=clock()+budgetMs;
   object result=null;Exception error=null;
   // Synchronous vendor IOCTL may ignore cancellation. A timed out worker exits after replying.
   var done=new ManualResetEvent(false);
   var thread=new Thread(delegate() { try { result=Run(request,op); } catch(Exception ex) { error=ex; } finally { Interlocked.Exchange(ref executing,0);done.Set(); } });thread.IsBackground=true;operationThread=thread;thread.Start();
   if(!done.WaitOne(budgetMs)){Interlocked.Exchange(ref quarantined,1);throw new NotebookError("QUERY_TIMEOUT","Read exceeded the worker deadline; restart required");}
   done.Dispose();if(error!=null)throw error;return result;
  }
  // Pure tests join their fake delayed callback; production exits rather than draining a stuck native call.
  internal bool WaitForSyntheticCompletion(int timeoutMs) { return operationThread==null||operationThread.Join(timeoutMs); }
  public string HandleLine(string line) {
   object id=null;
   try {
    CheckUsable();if(line==null||line.Length>16384)throw new NotebookError("INVALID_REQUEST","Request is too large");
    var request=json.Deserialize<Dictionary<string,object>>(line);if(request==null)throw new NotebookError("INVALID_REQUEST","Request must be an object");
    request.TryGetValue("id",out id);object rawOp;string op=request.TryGetValue("op",out rawOp)?rawOp as string:null;
    if(op!="scan"&&op!="read"&&op!="controlRead"&&op!="set")throw new NotebookError("INVALID_REQUEST","Only fixed notebook operations are implemented");
    return json.Serialize(NotebookRules.Obj("id",id,"ok",true,"data",Bounded(request,op),"restartRequired",false));
   } catch(Exception ex) {
    var typed=ex as NotebookError;return json.Serialize(NotebookRules.Obj("id",id,"ok",false,"error",NotebookRules.Obj("code",typed==null?"QUERY_FAILED":typed.Code,"message",typed==null?"Notebook query failed ("+ex.GetType().Name+")":typed.Message),"restartRequired",RestartRequired));
   }
  }
 }
 public static class NotebookNativeIdentity {
  [StructLayout(LayoutKind.Sequential)]struct PowerStatus {public byte AcLineStatus,BatteryFlag,BatteryPercent,SystemFlag;public uint Life,FullLife;}
  [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetSystemPowerStatus(out PowerStatus status);
  public static NotebookIdentity Read() {
   var result=new NotebookIdentity();
   using(var search=new ManagementObjectSearcher("root\\cimv2","SELECT Manufacturer,Model FROM Win32_ComputerSystem")) {
    search.Options.Timeout=TimeSpan.FromSeconds(3);int count=0;
    using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){count++;result.Manufacturer=Convert.ToString(row["Manufacturer"],CultureInfo.InvariantCulture);result.Model=Convert.ToString(row["Model"],CultureInfo.InvariantCulture);}
    result.IdentityReadConfirmed=count==1&&!String.IsNullOrWhiteSpace(result.Manufacturer)&&!String.IsNullOrWhiteSpace(result.Model);
   }
   var chassis=new List<int>();
   using(var search=new ManagementObjectSearcher("root\\cimv2","SELECT ChassisTypes FROM Win32_SystemEnclosure")) {
    search.Options.Timeout=TimeSpan.FromSeconds(3);
    using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){var values=row["ChassisTypes"] as Array;if(values!=null)foreach(object value in values)chassis.Add(Convert.ToInt32(value,CultureInfo.InvariantCulture));}
   }
   result.ChassisTypes=chassis.ToArray();
   // Driver metadata is queried only on an ASUS mobile identity; it never opens ATKACPI.
   if(NotebookRules.IsAsus(result.Manufacturer)&&result.IdentityReadConfirmed) {
    bool mobile=result.ChassisTypes.Length>0;foreach(int c in result.ChassisTypes)if(c!=8&&c!=9&&c!=10&&c!=11&&c!=14&&c!=30&&c!=31&&c!=32)mobile=false;
    if(mobile){PowerStatus power;if(GetSystemPowerStatus(out power)&&(power.AcLineStatus==0||power.AcLineStatus==1))result.AcOnline=power.AcLineStatus==1;}
    if(mobile)using(var search=new ManagementObjectSearcher("root\\cimv2","SELECT DeviceName,DriverVersion,DriverProviderName,IsSigned FROM Win32_PnPSignedDriver WHERE DeviceName='ASUS System Control Interface' OR DeviceName='ASUS System Control Interface V1' OR DeviceName='ASUS System Control Interface V3'")) {
     search.Options.Timeout=TimeSpan.FromSeconds(3);
     using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){bool signed=row["IsSigned"] is bool&&(bool)row["IsSigned"];string provider=Convert.ToString(row["DriverProviderName"],CultureInfo.InvariantCulture);result.InterfaceDrivers.Add(NotebookRules.Obj("name",Convert.ToString(row["DeviceName"],CultureInfo.InvariantCulture),"version",Convert.ToString(row["DriverVersion"],CultureInfo.InvariantCulture),"provider",provider,"signed",signed));if(signed&&NotebookRules.IsAsus(provider))result.InterfaceDriverConfirmed=true;}
    }
   }
   return result;
  }
 }
 public sealed class NotebookNativeTransport:INotebookControlTransport {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,uint inputSize,byte[] output,uint outputSize,out uint returned,IntPtr overlapped);
  readonly SafeFileHandle handle;
  readonly Mutex mutex;
  public NotebookNativeTransport() {
   mutex=new Mutex(false,@"Local\OpenAC.Notebook.Status");bool owns=false;
   try { try { owns=mutex.WaitOne(0); }catch(AbandonedMutexException){owns=true;}if(!owns)throw new NotebookError("INTERFACE_BUSY","Another OpenAC notebook query is in progress");
    handle=CreateFile(@"\\.\ATKACPI",0xC0000000,3,IntPtr.Zero,3,0x80,IntPtr.Zero);if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error());
   }catch{if(handle!=null)handle.Dispose();if(owns)mutex.ReleaseMutex();mutex.Dispose();throw;}
  }
  public byte[] ReadStatus(uint endpoint) {
   return Invoke(NotebookRules.StatusPacket(endpoint));
  }
  public byte[] WriteControl(uint endpoint,uint parameter){return Invoke(NotebookControlRules.Packet(endpoint,parameter));}
  byte[] Invoke(byte[] input){var output=new byte[16];uint returned;
   if(!DeviceIoControl(handle,0x0022240C,input,(uint)input.Length,output,(uint)output.Length,out returned,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
   if(returned<4||returned>16)throw new NotebookError("INVALID_RESPONSE","Driver did not return a valid status word");
   var actual=new byte[returned];Array.Copy(output,actual,(int)returned);return actual;
  }
  public void Dispose(){handle.Dispose();mutex.ReleaseMutex();mutex.Dispose();}
 }
 public static class NotebookWorker {
  public static int Main(string[] args) {
   if(args.Length!=0)return 2;
   var service=new NotebookService(NotebookNativeIdentity.Read,delegate{return new NotebookNativeTransport();},8000);
   var writer=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};string line;
   while((line=OpenAc.WorkerTextProtocol.ReadLine())!=null){writer.WriteLine(service.HandleLine(line));if(service.RestartRequired){writer.Flush();Environment.Exit(4);}}
   return 0;
  }
 }
}
