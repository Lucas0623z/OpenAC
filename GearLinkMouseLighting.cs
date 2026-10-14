// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independent protocol encoder. Facts: official GearLink p725 getEffect/setEffect
// (7548 v1.00.15) and that exact model's single Logo effect list. No vendor code.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal static partial class GearLinkWorker {
 internal static int MouseLightingInteger(object value,int min,int max){if(!(value is int)||(int)value<min||(int)value>max)throw new ArgumentException("Mouse lighting requires an integer in the verified range.");return (int)value;}
 internal static int[] MouseLightingRgb(object value){var sequence=value as IEnumerable;if(sequence==null||value is string)throw new ArgumentException("Mouse lighting requires three integer RGB bytes.");var result=new List<int>();foreach(object component in sequence){if(result.Count==3)throw new ArgumentException("Mouse lighting requires exactly three RGB bytes.");result.Add(MouseLightingInteger(component,0,255));}if(result.Count!=3)throw new ArgumentException("Mouse lighting requires exactly three RGB bytes.");return result.ToArray();}
 internal static bool VerifiedMouseLightingPid(int pid){return pid==7546||pid==7548;}
 internal static bool VerifiedMouseLightingEffect(int effect){return effect==0||effect==1||effect==2||effect==4||effect==6;}
 internal static bool MouseLightingHasColor(int effect){return effect==0||effect==1||effect==4;}
 internal static object[] MouseLightingOptions(int pid) {
  if(!VerifiedMouseLightingPid(pid))return new object[0];
  return new[]{0,1,2,4,6}.Select(mode=>(object)Obj("value",mode,"name",mode==0?"Static":mode==1?"Breathing":mode==2?"ColorCycle":mode==4?"Reactive":"Power","color",MouseLightingHasColor(mode),"brightness",true,"direction",false,"speed",false,"random",false)).ToArray();
 }
 internal static bool MouseLightingReady(int pid,Dictionary<string,object> snapshot) {
  object model,software,windows;if(!VerifiedMouseLightingPid(pid)||snapshot==null||!snapshot.TryGetValue("modelPid",out model)||!(model is int)||(int)model!=pid||!snapshot.TryGetValue("lightingSoftwareMode",out software)||!(software is bool)||(bool)software||!snapshot.TryGetValue("windowsLightingEnabled",out windows)||!(windows is bool)||(bool)windows)return false;
  try{var lighting=snapshot["lighting"] as Dictionary<string,object>;if(lighting==null)return false;int mode=MouseLightingInteger(lighting["currentMode"],0,240);if(!(mode<=6||mode==240))return false;MouseLightingInteger(lighting["brightness"],0,100);MouseLightingInteger(lighting["speed"],0,255);MouseLightingInteger(lighting["direction"],0,1);MouseLightingRgb(lighting["color"]);return MouseLightingInteger(lighting["area"],0,0)==0&&lighting["random"] is bool;}catch(ArgumentException){return false;}catch(KeyNotFoundException){return false;}
 }
 internal static void AddMouseLightingCapabilities(string kind,bool online,Dictionary<string,object> state,Dictionary<string,object> snapshot,List<string> caps) {
  if(kind!="mouse")return;object value;int pid=state.TryGetValue("pid",out value)&&value is int?(int)value:0;
  bool available=online&&caps!=null&&new[]{"lighting","lightingSoftwareMode","windowsLightingEnabled"}.All(caps.Contains)&&MouseLightingReady(pid,snapshot);
  snapshot["mouseLightingProtocolAvailable"]=available;
  var options=state.TryGetValue("settingOptions",out value)?value as Dictionary<string,object>:null;if(options==null){options=Obj();state["settingOptions"]=options;}options["mouseLightingEffects"]=MouseLightingOptions(pid);
  var setters=state.TryGetValue("setCapabilities",out value)?value as string[]:null;state["setCapabilities"]=(setters??new string[0]).Where(setting=>setting!="mouseLighting").Concat(available?new[]{"mouseLighting"}:new string[0]).Distinct().ToArray();
 }
 internal static byte[] BuildMouseLighting(object value,Dictionary<string,object> before) {
  var request=value as Dictionary<string,object>;
  if(request==null||(request.Count!=2&&request.Count!=3)||!request.ContainsKey("effect")||!request.ContainsKey("brightness")||request.Keys.Any(key=>key!="effect"&&key!="brightness"&&key!="color"))throw new ArgumentException("mouseLighting requires effect, brightness and an optional supported color.");
  object model;if(before==null||!before.TryGetValue("modelPid",out model)||!(model is int)||!MouseLightingReady((int)model,before))throw new ArgumentException("Exact mouse lighting readback and both hardware lighting owners must be confirmed.");
  int mode=MouseLightingInteger(request["effect"],0,240);if(!VerifiedMouseLightingEffect(mode))throw new ArgumentException("This effect is not verified for the current mouse model.");int brightness=MouseLightingInteger(request["brightness"],0,100);var prior=(Dictionary<string,object>)before["lighting"];bool random=(bool)prior["random"];int[] rgb=MouseLightingRgb(prior["color"]);
  if(request.ContainsKey("color")){if(!MouseLightingHasColor(mode)||random)throw new ArgumentException("The selected effect has no confirmed fixed-color input.");rgb=MouseLightingRgb(request["color"]);}
  // The device's cycle/random command format requires zero RGB. Battery mode
  // has no color editor: preserve its read parameters, never invent thresholds.
  if(mode==2||random)rgb=new[]{0,0,0};
  return Packet(0x51,0x28,0,(byte)mode,(byte)brightness,(byte)rgb[0],(byte)rgb[1],(byte)rgb[2],(byte)MouseLightingInteger(prior["direction"],0,1),random?(byte)1:(byte)0,(byte)MouseLightingInteger(prior["speed"],0,255));
 }
 internal static bool IsMouseLightingWrite(string kind,byte[] packet) {
  if(kind!="mouse"||packet==null||packet.Length!=12||packet[0]!=0x51||packet[1]!=0x28||packet[2]!=0||packet[3]!=0||!VerifiedMouseLightingEffect(packet[4])||packet[5]>100||packet[9]>1||packet[10]>1)return false;
  return packet[4]!=2&&packet[10]==0||packet[6]==0&&packet[7]==0&&packet[8]==0;
 }
 // Use before for BOTH same-value/no-output and post-write comparisons. Comparing
 // only requested fields could falsely confirm changed direction/speed/random.
 internal static bool MatchesMouseLighting(object value,Dictionary<string,object> before,Dictionary<string,object> after) {
  try {
   byte[] expected=BuildMouseLighting(value,before);object model;if(after==null||!before.TryGetValue("modelPid",out model)||!MouseLightingReady((int)model,after))return false;
   var actual=(Dictionary<string,object>)after["lighting"];return MouseLightingInteger(actual["currentMode"],0,240)==expected[4]&&MouseLightingInteger(actual["brightness"],0,100)==expected[5]&&MouseLightingRgb(actual["color"]).SequenceEqual(expected.Skip(6).Take(3).Select(b=>(int)b))&&MouseLightingInteger(actual["direction"],0,1)==expected[9]&&BooleanByte(actual["random"])==expected[10]&&MouseLightingInteger(actual["speed"],0,255)==expected[11];
  }catch(ArgumentException){return false;}catch(KeyNotFoundException){return false;}catch(InvalidCastException){return false;}
 }
}
