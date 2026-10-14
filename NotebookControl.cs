// SPDX-License-Identifier: MIT
// Independently implemented single-setting SCI transactions; no automatic policies or restore.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RogControlNotebook {
 public interface INotebookControlTransport:INotebookTransport { byte[] WriteControl(uint endpoint,uint parameter); }
 public sealed partial class NotebookService {
  sealed class ControlLease {public string ReadToken,IdentityToken,ModelCode;public string Family;public long Captured;public bool? AcOnline;public Dictionary<uint,NotebookControlStatus> States;}
  readonly string controlSessionToken=Guid.NewGuid().ToString("N");
  ControlLease controlLease;
  static string Field(Dictionary<string,object> map,string key){object value;return map.TryGetValue(key,out value)?value as string:null;}
  static void ValidateControlRequest(Dictionary<string,object> request,string op){
   var names=op=="controlRead"?new[]{"id","op"}:new[]{"id","op","setting","value","experimental","modelCode","identityToken","sessionToken","readToken","acceptDisplayDisruption","acceptReboot"};
   if(request.Keys.Any(key=>!names.Contains(key)))throw new NotebookError("INVALID_REQUEST","Unknown notebook control field");
   if(op=="controlRead")return;
   object value,permission;if(!request.TryGetValue("experimental",out permission)||!(permission is bool)||!(bool)permission)throw new NotebookError("CONTROL_DISABLED","Device control must be explicitly enabled");
   var spec=NotebookControlRules.Find(Field(request,"setting"));if(spec==null||!request.TryGetValue("value",out value)||!spec.LegalValue(value))throw new NotebookError("INVALID_REQUEST","Unknown setting or illegal control value");
   if(NotebookControlRules.Family(Field(request,"modelCode"))==null||!NotebookControlRules.IdentityToken(Field(request,"identityToken"))||!NotebookControlRules.Token(Field(request,"sessionToken"))||!NotebookControlRules.Token(Field(request,"readToken")))throw new NotebookError("INVALID_REQUEST","A current notebook control capture is required");
   foreach(string key in new[]{"acceptDisplayDisruption","acceptReboot"})if(request.ContainsKey(key)&&!(request[key] is bool))throw new NotebookError("INVALID_REQUEST","Control acknowledgements must be boolean");
   if((spec.Name=="gpuEco"||spec.Name=="gpuMux")&&(!request.TryGetValue("acceptDisplayDisruption",out permission)||!Object.Equals(permission,true)))throw new NotebookError("CONTROL_ACK_REQUIRED","GPU changes require explicit display-change acknowledgement");
   if(spec.Name=="gpuMux"&&(!request.TryGetValue("acceptReboot",out permission)||!Object.Equals(permission,true)))throw new NotebookError("CONTROL_ACK_REQUIRED","MUX changes may require a manual restart");
  }
  static bool ControlDriverIdentity(NotebookIdentity value){
   if(value==null||!value.InterfaceDriverConfirmed||value.InterfaceDrivers==null||value.InterfaceDrivers.Count<1||value.InterfaceDrivers.Count>4)return false;
   foreach(object item in value.InterfaceDrivers){var map=item as Dictionary<string,object>;if(map==null)return false;object raw;string name=Field(map,"name"),provider=Field(map,"provider"),version=Field(map,"version");
    if(!new[]{"ASUS System Control Interface","ASUS System Control Interface V1","ASUS System Control Interface V3"}.Contains(name)||!NotebookRules.IsAsus(provider)||!map.TryGetValue("signed",out raw)||!Object.Equals(raw,true)||version==null||version.Length>64||!Regex.IsMatch(version,@"^\d+(\.\d+){1,3}$"))return false;
   }return true;
  }
  static string ControlIdentityToken(NotebookIdentity value){
   string drivers=String.Join("|",value.InterfaceDrivers.Cast<Dictionary<string,object>>().Select(d=>Field(d,"name")+":"+Field(d,"provider")+":"+Field(d,"version")).OrderBy(v=>v,StringComparer.Ordinal));
   string data=value.Manufacturer.ToUpperInvariant()+"|"+value.Model.ToUpperInvariant()+"|"+String.Join(",",value.ChassisTypes.OrderBy(v=>v))+"|"+drivers+"|"+(value.AcOnline.HasValue?(value.AcOnline.Value?"ac":"dc"):"unknown");
   using(var sha=SHA256.Create())return String.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(data)).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture)));
  }
  Dictionary<uint,NotebookControlStatus> ReadControlStates(INotebookTransport transport,string family){
   var endpoints=NotebookControlRules.Specs.Where(s=>!s.NotebookOnly||family!="handheld").Select(s=>s.Endpoint).ToList();if(family!="handheld")endpoints.AddRange(new[]{NotebookControlRules.OverdriveSupport,NotebookControlRules.XgEnabled,NotebookControlRules.ExternalDisplay});
   var states=new Dictionary<uint,NotebookControlStatus>();foreach(uint endpoint in endpoints){CheckUsable();states.Add(endpoint,NotebookControlRules.Decode(endpoint,transport.ReadStatus(endpoint)));CheckUsable();}return states;
  }
  Dictionary<string,object> ControlCapabilities(NotebookControlSpec spec,ControlLease lease){
   NotebookControlStatus status;lease.States.TryGetValue(spec.Endpoint,out status);int[] values=NotebookControlRules.AllowedValues(spec,lease.Family,lease.AcOnline,lease.States);
   return NotebookRules.Obj("feature",spec.Name,"unit",spec.Unit,"endpointPresent",status!=null&&status.Presence,"writeSupported",values.Length>0,"allowedValues",values,"minimum",spec.Minimum,"maximum",spec.Maximum,"currentValue",status==null?(object)null:status.CurrentValue,"valueReadbackConfirmed",status!=null&&status.CurrentValue.HasValue,"queryValue",status==null?(object)null:status.QueryValue,"valueSemantics",spec.CanConfirm?"brightness":"nativeStatus","physicalValidation",false);
  }
  object NotebookControlRun(Dictionary<string,object> request,string op,NotebookIdentity current,NotebookGate gate,Dictionary<string,object> data){
   if(!gate.Eligible||!ControlDriverIdentity(current)){controlLease=null;if(op=="set")throw new NotebookError("IDENTITY_CHANGED","Current ASUS mobile interface is not confirmed");data["controlSchema"]=1;data["controlStates"]=new object[0];data["setCapabilities"]=new object[0];return data;}
   if(op=="controlRead"){
    controlLease=null;CheckUsable();var lease=new ControlLease{ReadToken=Guid.NewGuid().ToString("N"),IdentityToken=ControlIdentityToken(current),ModelCode=gate.Model.Code,Family=gate.Model.Family,AcOnline=current.AcOnline};
    using(var transport=open()){if(transport==null)throw new NotebookError("INTERFACE_UNAVAILABLE","No current SCI connection");lease.States=ReadControlStates(transport,lease.Family);}
    CheckUsable();var latest=identity();CheckUsable();if(!NotebookRules.Gate(latest).Eligible||!ControlDriverIdentity(latest)||ControlIdentityToken(latest)!=lease.IdentityToken)throw new NotebookError("IDENTITY_CHANGED","Notebook identity changed during capability read");
    lease.Captured=clock();controlLease=lease;var caps=NotebookControlRules.Specs.Where(s=>!s.NotebookOnly||lease.Family!="handheld").Select(s=>(object)ControlCapabilities(s,lease)).ToArray();
    data["controlSchema"]=1;data["controlSessionToken"]=controlSessionToken;data["controlReadToken"]=lease.ReadToken;data["controlIdentityToken"]=lease.IdentityToken;data["controlStates"]=caps;data["setCapabilities"]=caps.Cast<Dictionary<string,object>>().Where(c=>(bool)c["writeSupported"]).Select(c=>(string)c["feature"]).ToArray();data["supportsWrite"]=((string[])data["setCapabilities"]).Length>0;data["driverQueryPerformed"]=true;data["driverQueryMethod"]="DSTS";return data;
   }
   var capture=controlLease;controlLease=null;
   if(capture==null||Field(request,"sessionToken")!=controlSessionToken||Field(request,"readToken")!=capture.ReadToken||Field(request,"identityToken")!=capture.IdentityToken||Field(request,"modelCode")!=capture.ModelCode||clock()-capture.Captured<0||clock()-capture.Captured>=30000)throw new NotebookError("STALE_CAPTURE","Re-read notebook controls before applying");
   if(gate.Model.Code!=capture.ModelCode||ControlIdentityToken(current)!=capture.IdentityToken)throw new NotebookError("IDENTITY_CHANGED","Notebook model, power or driver identity changed");
   var spec=NotebookControlRules.Find(Field(request,"setting"));int desired=(int)request["value"];
   if(!NotebookControlRules.AllowedValues(spec,capture.Family,capture.AcOnline,capture.States).Contains(desired))throw new NotebookError("CONTROL_UNSUPPORTED","This value is unavailable in the captured current state");
   data["controlSchema"]=1;data["setting"]=spec.Name;data["requestedValue"]=desired;data["writeAttempted"]=false;data["outputCompleted"]=false;data["driverAcknowledged"]=false;data["writeConfirmed"]=false;data["restorable"]=false;data["requiresRestart"]=false;data["appliedState"]="unconfirmed";
   using(var transport=open()){
    var writer=transport as INotebookControlTransport;if(writer==null)throw new NotebookError("CONTROL_UNAVAILABLE","The current transport has no prepared control path");
    var fresh=ReadControlStates(transport,capture.Family);if(fresh.Any(pair=>!capture.States.ContainsKey(pair.Key)||pair.Value.Raw!=capture.States[pair.Key].Raw||pair.Value.Error!=capture.States[pair.Key].Error))throw new NotebookError("STALE_CAPTURE","Notebook control state changed; re-read before applying");
    if(!NotebookControlRules.AllowedValues(spec,capture.Family,current.AcOnline,fresh).Contains(desired))throw new NotebookError("CONTROL_UNSUPPORTED","Current endpoint or GPU/power guard no longer permits the value");
    CheckUsable();var lastIdentity=identity();CheckUsable();if(!NotebookRules.Gate(lastIdentity).Eligible||!ControlDriverIdentity(lastIdentity)||ControlIdentityToken(lastIdentity)!=capture.IdentityToken)throw new NotebookError("IDENTITY_CHANGED","Notebook identity changed before control submission");
    if(spec.CanConfirm&&fresh[spec.Endpoint].CurrentValue==desired){data["writeConfirmed"]=true;data["readbackValue"]=desired;data["appliedState"]="alreadySet";return data;}
    CheckUsable();data["writeAttempted"]=true;data["requiresRestart"]=spec.Name=="gpuMux";byte[] response;
    try{response=writer.WriteControl(spec.Endpoint,spec.Parameter(desired));CheckUsable();}catch(NotebookError){throw;}catch(Exception){data["appliedState"]="writeEffectUnknown";data["controlError"]="CONTROL_IO_FAILED";return data;}
    bool validResponse=response!=null&&response.Length>=4&&response.Length<=16;uint replyWord=validResponse?BitConverter.ToUInt32(response,0):UInt32.MaxValue;data["outputCompleted"]=validResponse;data["driverAcknowledged"]=replyWord==1;data["appliedState"]=replyWord<=1?"submittedUnconfirmed":"writeEffectUnknown";
    // Only the backlight endpoint has an established current-value decoder here.
    if(spec.CanConfirm&&replyWord==1){try{CheckUsable();var after=NotebookControlRules.Decode(spec.Endpoint,transport.ReadStatus(spec.Endpoint));CheckUsable();data["readbackValue"]=after.CurrentValue;data["writeConfirmed"]=after.Error==null&&after.CurrentValue==desired;if((bool)data["writeConfirmed"])data["appliedState"]="confirmed";}catch(NotebookError){throw;}catch(Exception){data["controlError"]="READBACK_FAILED";}}
   }
   CheckUsable();var finalIdentity=identity();CheckUsable();if(!NotebookRules.Gate(finalIdentity).Eligible||!ControlDriverIdentity(finalIdentity)||ControlIdentityToken(finalIdentity)!=capture.IdentityToken){data["writeConfirmed"]=false;data["appliedState"]="writeEffectUnknown";data["controlError"]="IDENTITY_CHANGED";}return data;
  }
 }
}
