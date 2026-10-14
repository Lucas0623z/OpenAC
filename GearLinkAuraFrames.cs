// SPDX-License-Identifier: MIT. Independently implemented bounded lighting scope.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class GearLinkWorker {
 internal static bool LightingScope(string kind,string op){return (kind=="mouse"||kind=="keyboard")&&(op=="lightingRead"||op=="lightingSet");}
 internal static Dictionary<string,object> ReadLightingScope(GearDescriptor descriptor){
  if(LightingOnlyFamily(descriptor.Pid)){
   if(!LightingFamilyCollection(descriptor))throw new ArgumentException("Exact lighting collection is not confirmed.");
   try{using(var hid=new GearHid(descriptor)){byte report=descriptor.SelectReport(null);return ReadLightingFamilyScope(descriptor,p=>hid.Query(report,p));}}
   catch(Exception error){return FailedLightingFamilyScope(descriptor,error);}
  }
  string kind=Kind(descriptor.Pid);if(!(kind=="mouse"&&MouseSensorModel(descriptor.Pid)||kind=="keyboard"&&LegacyKeyboard(descriptor.Pid)))throw new ArgumentException("Exact model lighting transport is not prepared.");
  var state=descriptor.Metadata();var snapshot=Obj("modelPid",(int)descriptor.Pid);var caps=new List<string>();var errors=new List<object>();state["snapshot"]=snapshot;state["capabilities"]=caps;state["readCapabilities"]=caps;state["setCapabilities"]=new string[0];state["errors"]=errors;state["online"]=false;state["connected"]=true;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;
  using(var hid=new GearHid(descriptor)){
   byte report=descriptor.SelectReport(null);byte[] alive=hid.Query(report,kind=="keyboard"?Packet(0x12,3,0):Packet(0x12,0,2));Require(alive,5);
   if(kind=="keyboard"?alive[4]>2&&alive[4]!=255:alive[4]>1)throw new IOException("Invalid current lighting device connection state.");
   state["readbackConfirmed"]=true;state["online"]=kind=="keyboard"?alive[4]!=255:alive[4]==1;if(!(bool)state["online"])return state;
   if(kind=="keyboard")QueryField(hid,report,Packet(0x12,0,0),"lightingBasicInfo",errors,b=>ApplyPeripheralInfo(kind,descriptor.Pid,b,snapshot));
   ReadPeripheralLighting(hid,descriptor,report,kind,snapshot,caps,errors);
  }
  AddFeatureSetCapabilities(kind,true,state,snapshot,caps);return state;
 }
 internal static byte[] BuildLightingScope(string kind,string setting,object value,Dictionary<string,object> snapshot){
  if(setting!="staticLighting"&&setting!="lightingSoftwareMode")throw new ArgumentException("Only static RGB and explicit software ownership are available in the lighting scope.");
  return BuildFeatureSetting(kind,setting,value,snapshot);
 }
 internal static object HandleLightingScope(Dictionary<string,object> req,GearDescriptor target,string kind,string op){
  if(op=="lightingRead")return ReadLightingScope(target);
  object flag;if(!req.TryGetValue("experimental",out flag)||!(flag is bool)||!(bool)flag)throw new ArgumentException("Explicit device control is required.");
  string setting=Text(req,"setting");object value;if(!req.TryGetValue("value",out value))throw new ArgumentException("Lighting value is required.");
  if(setting!="staticLighting"&&setting!="lightingSoftwareMode")throw new ArgumentException("Unsupported lighting scope setting.");
  if(LightingOnlyFamily(target.Pid)){
   ValidateLightingFamilyValue(setting,value);object noOpFlag;bool familyNoOp=false;if(req.TryGetValue("requireNoOp",out noOpFlag)){if(!(noOpFlag is bool))throw new ArgumentException("Read-only validation guard must be Boolean.");familyNoOp=(bool)noOpFlag;}
   if(!LightingFamilyCollection(target))throw new ArgumentException("Exact lighting collection is not confirmed.");
   using(var hid=new GearHid(target)){byte report=target.SelectReport(null);return ApplyLightingFamily(target,setting,value,familyNoOp,()=>ReadLightingFamilyScope(target,p=>hid.Query(report,p)),p=>hid.WriteSetting(report,p));}
  }
  var before=ReadLightingScope(target);if(!(bool)before["online"]||!((string[])before["setCapabilities"]).Contains(setting))throw new ArgumentException("Fresh lighting ownership or recipe is unavailable.");
  var prior=(Dictionary<string,object>)before["snapshot"];byte[] packet=BuildLightingScope(kind,setting,value,prior);
  if(MatchesSetting(setting,value,prior)){before["writeAttempted"]=false;before["accepted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}
  object noOp;if(req.TryGetValue("requireNoOp",out noOp)&&noOp is bool&&(bool)noOp)throw new ArgumentException("Read-only validation rejected a changed value.");
  SettingReceipt receipt;using(var hid=new GearHid(target))receipt=hid.WriteSetting(target.SelectReport(null),packet);
  var after=ReadLightingScope(target);var current=(Dictionary<string,object>)after["snapshot"];after["setting"]=setting;after["requestedValue"]=value;after["writeAttempted"]=true;after["accepted"]=receipt.OutputCompleted;after["settingAcknowledged"]=receipt.Acknowledged;after["settingRejected"]=receipt.Rejected;
  if(receipt.Warning!=null)((List<object>)after["errors"]).Add(Obj("field","settingAcknowledgement","message",receipt.Warning));
  after["writeConfirmed"]=receipt.OutputCompleted&&!receipt.Rejected&&(bool)after["online"]&&MatchesLightingScopeAfter(kind,setting,value,prior,current);after["appliedState"]=(bool)after["writeConfirmed"]?"confirmed":"unconfirmed";return after;
 }
 internal static bool MatchesLightingScopeAfter(string kind,string setting,object value,Dictionary<string,object> prior,Dictionary<string,object> current){
  try{
   object a,b;if(!prior.TryGetValue("modelPid",out a)||!current.TryGetValue("modelPid",out b)||!Equals(a,b)||!(a is int))return false;
   object windows;if(!current.TryGetValue("windowsLightingEnabled",out windows)||!(windows is bool)||(bool)windows)return false;
   if(setting=="lightingSoftwareMode")return value is bool&&current.ContainsKey(setting)&&current[setting] is bool&&Equals(value,current[setting]);
   if(setting!="staticLighting"||!PeripheralLightingReady(kind,(int)a,current)||!(current["lightingSoftwareMode"] is bool)||(bool)current["lightingSoftwareMode"]||!MatchesPeripheralStaticLighting(value,current))return false;
   var expected=BuildLightingScope(kind,setting,value,prior);var lighting=(Dictionary<string,object>)current["lighting"];
   if((int)lighting["speed"]!=expected[4+(kind=="keyboard"?0:7)])return false;
   return kind=="keyboard"||(int)lighting["direction"]==expected[9];
  }catch{return false;}
 }
 internal static bool HeadsetLightingOwnedByHardware(string kind,string setting,Dictionary<string,object> req,Dictionary<string,object> snapshot){
  object raw;bool ownership=false,uniform=false;
  if(req.TryGetValue("requireLightingOwnership",out raw)){if(!(raw is bool))throw new ArgumentException("Lighting ownership guard must be boolean.");ownership=(bool)raw;}
  if(req.TryGetValue("requireUniformStaticLighting",out raw)){if(!(raw is bool))throw new ArgumentException("Uniform lighting guard must be boolean.");uniform=(bool)raw;}
  if(!ownership&&!uniform)return true;
  if(kind!="headset"||!new[]{"brightness","lightingEffect","lightingColor","lightingEnabled"}.Contains(setting)||uniform&&setting!="lightingColor")throw new ArgumentException("Headset lighting guards apply only to their fixed output scope.");
  object owner;if(snapshot==null||!snapshot.TryGetValue("auraEnabled",out owner)||!(owner is bool)||(bool)owner)return false;
  if(!uniform)return true;object mode,brightness,enabled;
  return snapshot.TryGetValue("lightingEffectCode",out mode)&&mode is int&&(int)mode==1&&snapshot.TryGetValue("brightnessRaw",out brightness)&&brightness is int&&(int)brightness==100&&snapshot.TryGetValue("lightingEnabled",out enabled)&&enabled is bool&&(bool)enabled;
 }
}
