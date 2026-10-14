// SPDX-License-Identifier: MIT. Synthetic records only; no transport.
using System;
using System.Collections.Generic;
using System.Linq;
public static class GearLinkAuraFrameTests {
 static int checks;static void Check(bool value){checks++;if(!value)throw new Exception("Scoped Aura case "+checks);}
 static Dictionary<string,object> Mouse(){return GearLinkWorker.Obj("modelPid",7548,"lightingSoftwareMode",false,"windowsLightingEnabled",false,"lighting",GearLinkWorker.Obj("currentMode",0,"area",0,"brightness",100,"color",new[]{10,20,30},"direction",1,"random",false,"speed",23));}
 static Dictionary<string,object> Keyboard(){return GearLinkWorker.Obj("modelPid",7588,"lightingSoftwareMode",false,"windowsLightingEnabled",false,"lighting",GearLinkWorker.Obj("currentMode",0,"recipeMode",0,"area",0,"brightness",100,"color",new[]{10,20,30},"secondaryColor",new[]{0,0,0},"backgroundColor",new[]{0,0,0},"flags",0,"speed",23));}
 static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected);}
 public static int Main(){try{
  foreach(string kind in new[]{"mouse","keyboard","headset","omni","unknown"})foreach(string op in new[]{"lightingRead","lightingSet","read","set","rawHid","frame"})Check(GearLinkWorker.LightingScope(kind,op)==((kind=="mouse"||kind=="keyboard")&&(op=="lightingRead"||op=="lightingSet")));
  var request=GearLinkWorker.Obj("color",new[]{10,20,30},"brightness",100);
  foreach(string kind in new[]{"mouse","keyboard"}){
   var before=kind=="mouse"?Mouse():Keyboard();var after=kind=="mouse"?Mouse():Keyboard();Check(GearLinkWorker.MatchesLightingScopeAfter(kind,"staticLighting",request,before,after));Check(GearLinkWorker.IsFeatureWrite(kind,GearLinkWorker.BuildLightingScope(kind,"staticLighting",request,before)));
   var light=(Dictionary<string,object>)after["lighting"];light["speed"]=24;Check(!GearLinkWorker.MatchesLightingScopeAfter(kind,"staticLighting",request,before,after));light["speed"]=23;after["lightingSoftwareMode"]=true;Check(!GearLinkWorker.MatchesLightingScopeAfter(kind,"staticLighting",request,before,after));after["lightingSoftwareMode"]=false;after["windowsLightingEnabled"]=true;Check(!GearLinkWorker.MatchesLightingScopeAfter(kind,"staticLighting",request,before,after));after["windowsLightingEnabled"]=false;after["modelPid"]=kind=="mouse"?7546:7586;Check(!GearLinkWorker.MatchesLightingScopeAfter(kind,"staticLighting",request,before,after));
   foreach(string setting in new[]{"dpiStage","pollingRate","sensorFrameRate","macro","rawPacket","buttonBinding","mouseLighting"})Reject(()=>GearLinkWorker.BuildLightingScope(kind,setting,request,before));
   var ownership=kind=="mouse"?Mouse():Keyboard();ownership["lightingSoftwareMode"]=true;Check(GearLinkWorker.MatchesLightingScopeAfter(kind,"lightingSoftwareMode",true,before,ownership));ownership["windowsLightingEnabled"]=true;Check(!GearLinkWorker.MatchesLightingScopeAfter(kind,"lightingSoftwareMode",true,before,ownership));
  }
  foreach(string setting in new[]{"brightness","lightingEffect","lightingColor","lightingEnabled"}){
   var guard=GearLinkWorker.Obj("requireLightingOwnership",true);Check(GearLinkWorker.HeadsetLightingOwnedByHardware("headset",setting,guard,GearLinkWorker.Obj("auraEnabled",false)));Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset",setting,guard,GearLinkWorker.Obj("auraEnabled",true)));Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset",setting,guard,GearLinkWorker.Obj()));Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset",setting,guard,GearLinkWorker.Obj("auraEnabled",0)));Reject(()=>GearLinkWorker.HeadsetLightingOwnedByHardware("mouse",setting,guard,GearLinkWorker.Obj("auraEnabled",false)));Reject(()=>GearLinkWorker.HeadsetLightingOwnedByHardware("headset",setting,GearLinkWorker.Obj("requireLightingOwnership",1),GearLinkWorker.Obj("auraEnabled",false)));
  }
  var uniformRequest=GearLinkWorker.Obj("requireUniformStaticLighting",true);var uniformState=GearLinkWorker.Obj("auraEnabled",false,"lightingEffectCode",1,"brightnessRaw",100,"lightingEnabled",true);
  Check(GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));
  foreach(string field in new[]{"auraEnabled","lightingEffectCode","brightnessRaw","lightingEnabled"}){object original=uniformState[field];uniformState.Remove(field);Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));uniformState[field]=original;}
  foreach(int mode in new[]{0,2,3,4,5}){uniformState["lightingEffectCode"]=mode;Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));}uniformState["lightingEffectCode"]=1;
  uniformState["brightnessRaw"]=99;Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));uniformState["brightnessRaw"]=100;uniformState["lightingEnabled"]=false;Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));uniformState["lightingEnabled"]=true;uniformState["auraEnabled"]=true;Check(!GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",uniformRequest,uniformState));
  Reject(()=>GearLinkWorker.HeadsetLightingOwnedByHardware("headset","brightness",uniformRequest,uniformState));Reject(()=>GearLinkWorker.HeadsetLightingOwnedByHardware("headset","lightingColor",GearLinkWorker.Obj("requireUniformStaticLighting",1),uniformState));
  Check(GearLinkWorker.HeadsetLightingOwnedByHardware("headset","sideToneLevel",GearLinkWorker.Obj(),GearLinkWorker.Obj()));
  Console.WriteLine(GearLinkWorker.Json.Serialize(GearLinkWorker.Obj("ok",true,"checks",checks,"hardwareAccess",false,"suite","scoped_aura_frames")));return 0;
 }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
