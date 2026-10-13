// Independent Windows Core Audio endpoint adapter. No HID or vendor SDK is used.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

internal static class AudioEndpointWorker {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32};
 static readonly Guid EventContext=new Guid("3B8F3AE1-6A92-49AA-901A-2E71D39C75A3");
 internal const string Source="Windows MMDevice / IAudioEndpointVolume";
 static Dictionary<string,object> Obj(params object[] fields){var o=new Dictionary<string,object>();for(int i=0;i<fields.Length;i+=2)o[(string)fields[i]]=fields[i+1];return o;}
 static string Text(IDictionary<string,object> request,string key){object value;return request!=null&&request.TryGetValue(key,out value)&&value is string?(string)value:"";}
 [MTAThread] static int Main(string[] args){
  Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
  if(args.Contains("--self-test")){SelfTest();return 0;}
  if(args.Contains("--inventory")){Console.WriteLine(Json.Serialize(Obj("source",Source,"endpoints",Enumerate(true))));return 0;}
  string line;while((line=Console.ReadLine())!=null){object id=null;try{
   if(line.Length>65536)throw new ArgumentException("Request is too large.");var request=Json.Deserialize<Dictionary<string,object>>(line);if(request==null)throw new ArgumentException("A request object is required.");request.TryGetValue("id",out id);
   Console.WriteLine(Json.Serialize(Obj("id",id,"ok",true,"data",Handle(request))));
  }catch(Exception ex){Console.WriteLine(Json.Serialize(Obj("id",id,"ok",false,"error",Obj("code",ex is ArgumentException?"invalid_request":"audio_endpoint_error","message",ex.Message))));}}
  return 0;
 }
 internal static object Handle(Dictionary<string,object> request){
  string operation=Text(request,"op");
  if(operation=="scan")return Obj("source",Source,"endpoints",Enumerate(false),"capturedAt",DateTime.Now.ToString("o"));
  if(operation!="read"&&operation!="set")throw new ArgumentException("Only scan, read and set are supported.");
  string flow=Text(request,"flow"),id=Text(request,"endpointId");ValidateTarget(id,flow);
  string setting="";object value=null;if(operation=="set"){
   object initiated;if(!request.TryGetValue("userInitiated",out initiated)||!Object.Equals(initiated,true))throw new ArgumentException("The user must explicitly apply this setting.");
   setting=Text(request,"setting");request.TryGetValue("value",out value);ValidateSetting(setting,value);
  }
  object enumeratorObject=null,volumeObject=null;IMMDevice device=null;
  try{
   enumeratorObject=new MMDeviceEnumerator();var enumerator=(IMMDeviceEnumerator)enumeratorObject;Hr(enumerator.GetDevice(id,out device));var metadata=Metadata(device);
   if(!Object.Equals(metadata["matchConfirmed"],true)||Convert.ToString(metadata["flow"])!=flow||Convert.ToUInt32(metadata["stateCode"])!=1)throw new ArgumentException("The exact ROG DELTA II-KJP endpoint is no longer active or does not match the selected direction. Scan again.");
   var volume=Volume(device,out volumeObject);var before=ReadVolume(metadata,volume);
   if(operation=="read")return before;
   float scalar=Convert.ToSingle(before["volumeScalar"],CultureInfo.InvariantCulture);bool muted=(bool)before["muted"];
   bool already=setting=="mute"?muted==(bool)value:Math.Abs(scalar-(int)value/100f)<0.00001;
   if(already){before["writeAttempted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}
   var context=EventContext;if(setting=="mute")Hr(volume.SetMute((bool)value,ref context));else Hr(volume.SetMasterVolumeLevelScalar((int)value/100f,ref context));
   // A successful HRESULT is acceptance. Only a matching fresh getter confirms the state.
   var after=ReadVolume(metadata,volume);bool confirmed=setting=="mute"?(bool)after["muted"]==(bool)value:Math.Abs(Convert.ToDouble(after["volumePercent"],CultureInfo.InvariantCulture)-(int)value)<=0.51;
   after["setting"]=setting;after["requestedValue"]=value;after["writeAttempted"]=true;after["writeConfirmed"]=confirmed;after["appliedState"]=confirmed?"confirmed":"unconfirmed";return after;
  }finally{Release(volumeObject);Release(device);Release(enumeratorObject);}
 }
 internal static bool MatchesModel(string friendlyName,string interfaceName){
  // The full KJP identity is required. A generic DELTA II name or a default endpoint is never sufficient.
  // A user can rename an endpoint's friendly name. The adapter interface identity must match.
  string normalized=Regex.Replace((interfaceName??"").ToUpperInvariant(),"[^A-Z0-9]","");return Regex.IsMatch(normalized,@"^ROGDELTA(?:II|2)KJP(?:24GHZ|USB|BLUETOOTH|BT)?$");
 }
 internal static void ValidateTarget(string id,string flow){if(String.IsNullOrWhiteSpace(id)||id.Length>2048||id.Any(c=>Char.IsControl(c))||(flow!="render"&&flow!="capture"))throw new ArgumentException("An exact endpoint ID and render/capture direction are required.");}
 internal static void ValidateSetting(string setting,object value){if(setting=="volume"){if(!(value is int)||(int)value<0||(int)value>100)throw new ArgumentException("Volume must be an integer from 0 to 100.");}else if(setting=="mute"){if(!(value is bool))throw new ArgumentException("Mute must be a Boolean.");}else throw new ArgumentException("Only volume and mute can be changed.");}
 static object[] Enumerate(bool inventory){
  object enumeratorObject=null;IMMDeviceCollection collection=null;var found=new List<object>();
  try{
   enumeratorObject=new MMDeviceEnumerator();var enumerator=(IMMDeviceEnumerator)enumeratorObject;Hr(enumerator.EnumAudioEndpoints(2,1,out collection));uint count;Hr(collection.GetCount(out count));if(count>1024)throw new InvalidOperationException("Unexpected audio endpoint count.");
   for(uint i=0;i<count;i++){IMMDevice device=null;object volumeObject=null;try{
    Hr(collection.Item(i,out device));var metadata=Metadata(device);bool match=Object.Equals(metadata["matchConfirmed"],true);if(!match&&!inventory)continue;
    if(match)try{var volume=Volume(device,out volumeObject);ReadVolume(metadata,volume);}catch(Exception ex){metadata["readbackConfirmed"]=false;metadata["error"]=ex.Message;}
    found.Add(metadata);
   }finally{Release(volumeObject);Release(device);}}
   return found.ToArray();
  }finally{Release(collection);Release(enumeratorObject);}
 }
 static Dictionary<string,object> Metadata(IMMDevice device){
  string id;uint state;int direction;Hr(device.GetId(out id));Hr(device.GetState(out state));Hr(((IMMEndpoint)device).GetDataFlow(out direction));
  IPropertyStore properties=null;string name="",interfaceName="";
  try{Hr(device.OpenPropertyStore(0,out properties));name=Property(properties,new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),14));interfaceName=Property(properties,new PropertyKey(new Guid("026E516E-B814-414B-83CD-856D6FEF4822"),2));}finally{Release(properties);}
  return Obj("endpointId",id,"flow",direction==0?"render":direction==1?"capture":"unknown","name",name,"interfaceName",interfaceName,"stateCode",state,"active",state==1,"matchConfirmed",MatchesModel(name,interfaceName),"identityEvidence","PKEY_DeviceInterface_FriendlyName","source",Source,"volumePercent",null,"muted",null,"readbackConfirmed",false);
 }
 static string Property(IPropertyStore properties,PropertyKey key){PropVariant value;int result=properties.GetValue(ref key,out value);if(result<0)return "";try{return value.Kind==31&&value.Pointer!=IntPtr.Zero?Marshal.PtrToStringUni(value.Pointer):"";}finally{PropVariantClear(ref value);}}
 static IAudioEndpointVolume Volume(IMMDevice device,out object volumeObject){var iid=new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");Hr(device.Activate(ref iid,23,IntPtr.Zero,out volumeObject));return (IAudioEndpointVolume)volumeObject;}
 static Dictionary<string,object> ReadVolume(Dictionary<string,object> metadata,IAudioEndpointVolume volume){float scalar;bool muted;uint channels,support;Hr(volume.GetMasterVolumeLevelScalar(out scalar));Hr(volume.GetMute(out muted));Hr(volume.GetChannelCount(out channels));if(Single.IsNaN(scalar)||Single.IsInfinity(scalar)||scalar<0||scalar>1)throw new InvalidOperationException("Invalid endpoint volume readback.");metadata["volumeScalar"]=scalar;metadata["volumePercent"]=Math.Round(scalar*100.0,2);metadata["muted"]=muted;metadata["channels"]=channels;metadata["hardwareSupport"]=volume.QueryHardwareSupport(out support)>=0?(object)support:null;metadata["capturedAt"]=DateTime.Now.ToString("o");metadata["readbackConfirmed"]=true;return metadata;}
 static void Hr(int result){if(result<0)Marshal.ThrowExceptionForHR(result);}
 static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
 static void SelfTest(){int checks=0;Action<bool> check=valid=>{checks++;if(!valid)throw new Exception("Audio endpoint rule test failed.");};
  check(MatchesModel("Headphones","ROG DELTA II-KJP (2.4GHz)"));check(MatchesModel("Microphone","ROG DELTA II KJP"));check(MatchesModel("耳机","ROG DELTA 2-KJP"));check(!MatchesModel("Headphones (ROG DELTA II)","ROG DELTA II"));check(!MatchesModel("Realtek Audio","Default"));check(!MatchesModel("ROG DELTA II-KJP","Realtek Audio"));check(!MatchesModel("ROG DELTA II-KJP",""));check(!MatchesModel("ROG DELTA III KJP","ROG DELTA III KJP"));
  Action<Action> reject=action=>{bool failed=false;try{action();}catch(ArgumentException){failed=true;}check(failed);};
  foreach(object value in new object[]{-1,101,50.5,"50",null,true})reject(()=>ValidateSetting("volume",value));foreach(object value in new object[]{0,"false",null})reject(()=>ValidateSetting("mute",value));reject(()=>ValidateSetting("defaultEndpoint",true));reject(()=>ValidateTarget("","render"));reject(()=>ValidateTarget("id","all"));reject(()=>ValidateTarget("id\n","capture"));ValidateTarget("{opaque-id}","render");ValidateSetting("volume",0);ValidateSetting("volume",100);ValidateSetting("mute",false);check(true);check(Marshal.SizeOf(typeof(PropVariant))==24);Console.WriteLine("Audio endpoint rules: "+checks+" checks passed. No COM activation or audio change.");
 }
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref PropVariant value);
 [StructLayout(LayoutKind.Sequential)]internal struct PropertyKey{internal Guid Format;internal uint Id;internal PropertyKey(Guid format,uint id){Format=format;Id=id;}}
 [StructLayout(LayoutKind.Explicit,Size=24)]internal struct PropVariant{[FieldOffset(0)]internal ushort Kind;[FieldOffset(8)]internal IntPtr Pointer;}
 [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]internal class MMDeviceEnumerator{}
 [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMDeviceEnumerator{
  [PreserveSig]int EnumAudioEndpoints(int flow,uint mask,out IMMDeviceCollection collection);[PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);[PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);[PreserveSig]int RegisterEndpointNotificationCallback(IntPtr callback);[PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr callback);
 }
 [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMDeviceCollection{[PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint index,out IMMDevice device);}
 [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMDevice{
  [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);[PreserveSig]int OpenPropertyStore(uint access,out IPropertyStore store);[PreserveSig]int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);[PreserveSig]int GetState(out uint state);
 }
 [ComImport,Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IMMEndpoint{[PreserveSig]int GetDataFlow(out int flow);}
 [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IPropertyStore{
  [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out PropertyKey key);[PreserveSig]int GetValue(ref PropertyKey key,out PropVariant value);[PreserveSig]int SetValue(ref PropertyKey key,ref PropVariant value);[PreserveSig]int Commit();
 }
 [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface IAudioEndpointVolume{
  [PreserveSig]int RegisterControlChangeNotify(IntPtr callback);[PreserveSig]int UnregisterControlChangeNotify(IntPtr callback);[PreserveSig]int GetChannelCount(out uint count);
  [PreserveSig]int SetMasterVolumeLevel(float value,ref Guid context);[PreserveSig]int SetMasterVolumeLevelScalar(float value,ref Guid context);[PreserveSig]int GetMasterVolumeLevel(out float value);[PreserveSig]int GetMasterVolumeLevelScalar(out float value);
  [PreserveSig]int SetChannelVolumeLevel(uint channel,float value,ref Guid context);[PreserveSig]int SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context);[PreserveSig]int GetChannelVolumeLevel(uint channel,out float value);[PreserveSig]int GetChannelVolumeLevelScalar(uint channel,out float value);
  [PreserveSig]int SetMute([MarshalAs(UnmanagedType.Bool)]bool mute,ref Guid context);[PreserveSig]int GetMute([MarshalAs(UnmanagedType.Bool)]out bool mute);[PreserveSig]int GetVolumeStepInfo(out uint step,out uint count);[PreserveSig]int VolumeStepUp(ref Guid context);[PreserveSig]int VolumeStepDown(ref Guid context);[PreserveSig]int QueryHardwareSupport(out uint mask);[PreserveSig]int GetVolumeRange(out float min,out float max,out float increment);
 }
}
