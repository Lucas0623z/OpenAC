using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static class PeripheralLightingTests {
 static int checks;
 static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
 static void Reject(Action action,string name){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}catch(IOException){rejected=true;}Check(rejected,name);}
 static Dictionary<string,object> O(params object[] values){return GearLinkWorker.Obj(values);}
 static Dictionary<string,object> Mouse(){return O("modelPid",7548,"lightingSoftwareMode",false,"windowsLightingEnabled",false,"lighting",GearLinkWorker.ParseMouseLighting(new byte[]{18,3,0,0,2,100,48,0,109,9,1,0,7}));}
 static Dictionary<string,object> Keyboard(){var state=O("modelPid",7588,"profileCount",6,"lightingEffectCode",1,"lightingSoftwareMode",false,"windowsLightingEnabled",false);state["lighting"]=GearLinkWorker.ParseKeyboardStaticRecipe(new byte[]{37,14,0,0,9,50,0,255,255,1,2,3,0,0,0,0,0,0},state);return state;}
 static int Main(){try{Run();Console.WriteLine("Peripheral lighting: "+checks+" pure checks passed; HID opened 0, setting writes 0.");return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
 static void Run(){
  var mouse=Mouse();var key=Keyboard();var value=O("color",new[]{10,20,30},"brightness",80);
  Check(GearLinkWorker.PeripheralLightingReady("mouse",7548,mouse),"Exact mouse readable");Check(GearLinkWorker.PeripheralLightingReady("keyboard",7588,key),"Exact keyboard readable");
  Check(!GearLinkWorker.PeripheralLightingReady("mouse",7549,mouse),"Related mouse not inferred");Check(!GearLinkWorker.PeripheralLightingReady("keyboard",7587,key),"Related keyboard not inferred");
  Check(GearLinkWorker.BuildFeatureSetting("mouse","staticLighting",value,mouse).SequenceEqual(new byte[]{81,40,0,0,0,80,10,20,30,1,0,7}),"Mouse getter/setter direction offset differs, speed retained");
  Check(GearLinkWorker.BuildFeatureSetting("keyboard","staticLighting",value,key).SequenceEqual(new byte[]{81,44,0,0,9,80,0,255,255,10,20,30,0,0,0,0,0,0}),"Keyboard one static recipe, random/secondary/background cleared");
  foreach(var tuple in new[]{Tuple.Create("mouse",mouse),Tuple.Create("keyboard",key)}) {
   Check(GearLinkWorker.IsFeatureWrite(tuple.Item1,GearLinkWorker.BuildFeatureSetting(tuple.Item1,"staticLighting",value,tuple.Item2)),"Exact encoded packet allowlisted");
   tuple.Item2["lightingSoftwareMode"]=true;Check(GearLinkWorker.PeripheralLightingReady(tuple.Item1,(int)tuple.Item2["modelPid"],tuple.Item2),"Streaming mode still readable");Reject(()=>GearLinkWorker.BuildFeatureSetting(tuple.Item1,"staticLighting",value,tuple.Item2),"No implicit ownership write");
   var release=GearLinkWorker.BuildFeatureSetting(tuple.Item1,"lightingSoftwareMode",false,tuple.Item2);var restore=GearLinkWorker.BuildFeatureSetting(tuple.Item1,"lightingSoftwareMode",true,tuple.Item2);
   Check(release.Length==5&&release[4]==0&&restore[4]==1&&GearLinkWorker.IsFeatureWrite(tuple.Item1,release)&&GearLinkWorker.IsFeatureWrite(tuple.Item1,restore),"Explicit reversible SW flag only");
   tuple.Item2["lightingSoftwareMode"]=false;tuple.Item2["windowsLightingEnabled"]=true;Check(!GearLinkWorker.PeripheralLightingReady(tuple.Item1,(int)tuple.Item2["modelPid"],tuple.Item2),"Active WDL cannot be overwritten");Reject(()=>GearLinkWorker.BuildFeatureSetting(tuple.Item1,"lightingSoftwareMode",false,tuple.Item2),"No WDL ownership guessing");tuple.Item2["windowsLightingEnabled"]=false;
  }
  Check(GearLinkWorker.IsFeatureQuery("keyboard",new byte[]{37,14,0,0}),"Static recipe read allowed");Check(GearLinkWorker.IsFeatureQuery("keyboard",new byte[]{39,0,0,0}),"SW read allowed");Check(!GearLinkWorker.IsFeatureQuery("keyboard",new byte[]{37,14,1,0}),"Other effect query not exposed");Check(!GearLinkWorker.IsFeatureQuery("mouse",new byte[]{39,0,0,0}),"Different collection protocol not guessed");
  var parsed=(Dictionary<string,object>)mouse["lighting"];Check((int)parsed["direction"]==1&&(int)parsed["speed"]==7&&((int[])parsed["color"]).SequenceEqual(new[]{48,0,109}),"Mouse response offsets");Check(!GearLinkWorker.MatchesFeatureSetting("staticLighting",O("color",new[]{48,0,109},"brightness",100),mouse).Value,"Stored RGB in cycle not static confirmation");parsed["currentMode"]=0;Check(GearLinkWorker.MatchesFeatureSetting("staticLighting",O("color",new[]{48,0,109},"brightness",100),mouse)==true,"Static getter strict match");parsed["random"]=true;Check(GearLinkWorker.MatchesFeatureSetting("staticLighting",O("color",new[]{48,0,109},"brightness",100),mouse)==false,"Random colors cannot confirm static");
  var k=(Dictionary<string,object>)key["lighting"];k["currentMode"]=0;Check(GearLinkWorker.MatchesFeatureSetting("staticLighting",O("color",new[]{1,2,3},"brightness",50),key)==true,"Keyboard same-value no-output match");k["backgroundColor"]=new[]{1,0,0};Check(GearLinkWorker.MatchesFeatureSetting("staticLighting",O("color",new[]{1,2,3},"brightness",50),key)==false,"Nonuniform background not same request");
  foreach(object invalid in new object[]{new[]{1,2},new[]{1,2,256},new object[]{1,2,3.1},"1,2,3"})Reject(()=>GearLinkWorker.BuildFeatureSetting("mouse","staticLighting",O("color",invalid,"brightness",50),Mouse()),"Invalid RGB rejected");
  foreach(object invalid in new object[]{-1,101,50.5,"50",true})Reject(()=>GearLinkWorker.BuildFeatureSetting("mouse","staticLighting",O("color",new[]{1,2,3},"brightness",invalid),Mouse()),"Invalid brightness rejected");
  Reject(()=>GearLinkWorker.BuildFeatureSetting("mouse","staticLighting",O("color",new[]{1,2,3},"brightness",50,"extra",true),Mouse()),"Extra fields rejected");
  Reject(()=>GearLinkWorker.ParseMouseLighting(new byte[12]),"Short mouse report rejected");Reject(()=>GearLinkWorker.ParseKeyboardStaticRecipe(new byte[17],key),"Short key report rejected");var malformed=new byte[]{18,3,0,0,255,101,1,2,3,0,3,2,0};Reject(()=>GearLinkWorker.ParseMouseLighting(malformed),"Unknown mode/brightness rejected");
  var caps=new List<string>{"lighting","lightingSoftwareMode"};var state=O("pid",7548,"setCapabilities",new string[0]);mouse=Mouse();mouse["lightingSoftwareMode"]=true;GearLinkWorker.AddFeatureSetCapabilities("mouse",true,state,mouse,caps);Check(((string[])state["setCapabilities"]).Contains("lightingSoftwareMode")&&!((string[])state["setCapabilities"]).Contains("staticLighting"),"SW active exposes release, no RGB setter");mouse["lightingSoftwareMode"]=false;GearLinkWorker.AddFeatureSetCapabilities("mouse",true,state,mouse,caps);Check(((string[])state["setCapabilities"]).Contains("staticLighting"),"Readback release enables RGB setter");
  state=O("pid",7548,"setCapabilities",new string[0]);mouse.Remove("windowsLightingEnabled");GearLinkWorker.AddFeatureSetCapabilities("mouse",true,state,mouse,caps);Check(!((string[])state["setCapabilities"]).Any(),"Missing ownership getter disables all lighting setters");
  state=O("pid",7588,"setCapabilities",new string[0]);key=Keyboard();key["profileCount"]=0;key["lighting"]=GearLinkWorker.ParseKeyboardStaticRecipe(new byte[]{37,14,0,0,9,50,0,255,255,1,2,3,0,0,0,0,0,0},key);Check(!GearLinkWorker.PeripheralLightingReady("keyboard",7588,key),"Placeholder profile cannot confirm current mode");
  Check(GearLinkWorker.ResponseMatches(7588,2,new byte[]{37,14,0,0},new byte[]{2,37,14,8,9,1},6),"Exact keyboard legacy two-byte matching");Check(!GearLinkWorker.ResponseMatches(7548,3,new byte[]{18,3,0,0},new byte[]{3,18,3,1,0,1},6),"Mouse zone prefix must match");
 }
}
