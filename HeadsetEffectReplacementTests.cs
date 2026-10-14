// SPDX-License-Identifier: MIT. Synthetic values only; no transport or hardware operation.
using System;
using System.Collections.Generic;
using System.Linq;
#if HEADSET_GUI
using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RogControl;
#endif

#if !HEADSET_GUI
public static class HeadsetEffectReplacementTests
{
    static int checks;
    static Dictionary<string,object> Recipe(int mode=0){return GearLinkWorker.Obj("modelPid",7489,"lightingEffectCode",mode,"brightnessRaw",47,"color",new[]{82,0,111},"lightingEnabled",true,"auraEnabled",false);}
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    static void Reject(Action action,string name){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,name);}
    static Dictionary<string,object> Copy(Dictionary<string,object> r){return GearLinkWorker.Json.Deserialize<Dictionary<string,object>>(GearLinkWorker.Json.Serialize(r));}
    public static int Main(){try{Run();Console.WriteLine("Headset effect replacement: "+checks+" pure protocol checks; HID/native/writes=0.");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(){
        foreach(int mode in Enumerable.Range(0,6))foreach(int requested in Enumerable.Range(1,5)){
            var before=Recipe(mode);byte[] packet=GearLinkWorker.BuildHeadsetSetting("lightingEffect",requested,before);
            Check(packet.SequenceEqual(new byte[]{81,40,0,0,(byte)requested,47,82,0,111}),"explicit named payload with unrounded preserved values");
            Check((int)before["lightingEffectCode"]==mode,"current raw mode was not relabelled");
            var after=Copy(before);after["lightingEffectCode"]=requested;Check(GearLinkWorker.MatchesHeadsetEffectReplacement(requested,before,after),"named effect and all preserved bytes verified");
            foreach(string field in new[]{"brightnessRaw","color","lightingEnabled","auraEnabled","modelPid"}){var wrong=Copy(after);wrong[field]=field=="brightnessRaw"?(object)50:field=="color"?new[]{82,0,110}:field=="modelPid"?(object)6906:field=="auraEnabled"?(object)true:false;Check(!GearLinkWorker.MatchesHeadsetEffectReplacement(requested,before,wrong),"changed preserved/readback ownership field rejected "+field);}
            var currentZero=Copy(after);currentZero["lightingEffectCode"]=0;Check(!GearLinkWorker.MatchesHeadsetEffectReplacement(requested,before,currentZero),"raw zero is never named effect confirmation");
        }
        foreach(object requested in new object[]{null,true,"1",1.0,1.5,0,-1,6,255,(long)1})Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(requested,Recipe()),"strict named effect input");
        foreach(object raw in new object[]{null,true,"0",0.0,-1,6,255}){var s=Recipe();s["lightingEffectCode"]=raw;Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"invalid original raw mode");}
        foreach(object brightness in new object[]{null,true,"50",50.0,-1,101,255}){var s=Recipe();s["brightnessRaw"]=brightness;Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"invalid original native brightness");}
        foreach(object rgb in new object[]{null,"RGB",new[]{1,2},new[]{1,2,3,4},new[]{-1,2,3},new[]{1,256,3},new object[]{1,true,3},new object[]{1,2.0,3}}){var s=Recipe();s["color"]=rgb;Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"invalid preserved RGB");}
        foreach(string owner in new[]{"lightingEnabled","auraEnabled"})foreach(object wrong in new object[]{null,"false",1,owner=="lightingEnabled"?(object)false:true}){var s=Recipe();s[owner]=wrong;Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"unknown or wrong lighting ownership");}
        foreach(int pid in new[]{6906,7488,7490,0}){var s=Recipe();s["modelPid"]=pid;Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"no related headset PID inheritance");}
        foreach(string missing in new[]{"modelPid","lightingEffectCode","brightnessRaw","color","lightingEnabled","auraEnabled"}){var s=Recipe();s.Remove(missing);Reject(()=>GearLinkWorker.BuildHeadsetEffectReplacement(1,s),"complete original fields required");}
        var zero=Recipe();var reads=new List<string>{"lighting","lightingEnabled","auraEnabled"};var setters=new List<string>();GearLinkWorker.AddHeadsetLightingSetCapabilities(zero,reads,setters);Check(setters.SequenceEqual(new[]{"lightingEffect"}),"raw0 exposes only explicit named replacement");Check((int)zero["lightingEffectCode"]==0&&(bool)zero["lightingModeMappingUnconfirmed"],"zero remains unknown snapshot");Reject(()=>GearLinkWorker.BuildHeadsetSetting("brightness",50,zero),"no old brightness semantics from raw0");Reject(()=>GearLinkWorker.BuildHeadsetSetting("lightingColor",new[]{1,2,3},zero),"no old color semantics from raw0");
        foreach(string missing in reads){var caps=new List<string>();GearLinkWorker.AddHeadsetLightingSetCapabilities(Recipe(),reads.Where(v=>v!=missing).ToList(),caps);Check(caps.Count==0,"every source getter capability required");}
        foreach(int mode in Enumerable.Range(1,5)){var caps=new List<string>();GearLinkWorker.AddHeadsetLightingSetCapabilities(Recipe(mode),reads,caps);Check(caps.Contains("lightingEffect")&&caps.Contains("brightness")&&caps.Contains("lightingColor")== (mode<=3),"named original modes retain matching controls");}
    }
}
#else
public static class HeadsetEffectReplacementGuiTests
{
    static int checks;static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;}
    static Dictionary<string,object> Map(params object[] pairs){var o=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)o[(string)pairs[i]]=pairs[i+1];return o;}
    static Dictionary<string,object> State(int mode=0){return Map("deviceId","hid-0123456789abcdef","kind","headset","pid",7489,"connected",true,"online",true,"readbackConfirmed",true,"readCapabilities",new[]{"lighting","lightingEnabled","auraEnabled"},"setCapabilities",new[]{"lightingEffect","lightingEnabled","auraEnabled"},"snapshot",Map("modelPid",7489,"lightingEffectCode",mode,"brightnessRaw",47,"color",new[]{82,0,111},"lightingEnabled",true,"auraEnabled",false,"lightingModeMappingUnconfirmed",mode==0));}
    static Dictionary<string,object> Snapshot(Dictionary<string,object> state){return (Dictionary<string,object>)state["snapshot"];}
    static IEnumerable<T> Tree<T>(DependencyObject node)where T:DependencyObject{if(node is T)yield return (T)node;foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item==null)continue;foreach(T result in Tree<T>(item))yield return result;}}
    [STAThread]public static int Main(){try{
        foreach(int mode in Enumerable.Range(1,5)){var named=State(mode);var observed=HeadsetLightingUiFacts.Read(named);Check(observed.Effect==mode&&observed.CanSetEffect,"known current mode retains explicit selection");}
        var state=State();var read=HeadsetLightingUiFacts.Read(state);Check(read.IdentityConfirmed&&!read.Effect.HasValue&&read.CanSetEffect&&!read.CanSetBrightness&&!read.CanSetColor,"unknown raw0 enables only explicit new selection");Check((int)Snapshot(state)["lightingEffectCode"]==0,"reading UI facts never rewrites raw0");
        foreach(string field in new[]{"lightingEnabled","auraEnabled"})foreach(object bad in new object[]{null,1,"false",field=="lightingEnabled"?(object)false:true}){var wrong=State();Snapshot(wrong)[field]=bad;Check(!HeadsetLightingUiFacts.Read(wrong).CanSetEffect,"owner and LED status gate replacement");}
        foreach(object mode in new object[]{null,"0",0.0,-1,6,255}){var wrong=State();Snapshot(wrong)["lightingEffectCode"]=mode;Check(!HeadsetLightingUiFacts.Read(wrong).CanSetEffect,"invalid raw effect blocked");}
        foreach(string missing in new[]{"brightnessRaw","color","lightingEffectCode"}){var wrong=State();Snapshot(wrong).Remove(missing);Check(!HeadsetLightingUiFacts.Read(wrong).CanSetEffect,"incomplete recipe blocked");}
        foreach(string missing in new[]{"lighting","lightingEnabled","auraEnabled"}){var wrong=State();wrong["readCapabilities"]=new[]{"lighting","lightingEnabled","auraEnabled"}.Where(v=>v!=missing).ToArray();Check(!HeadsetLightingUiFacts.Read(wrong).CanSetEffect,"missing independent getter blocks replacement");}
        var sessions=new Dictionary<string,Dictionary<string,object>>{{"headset",state}};Check(HeadsetLightingUiFacts.SessionMatches(sessions,state,read),"original snapshot reference remains bound");sessions["headset"]=State();Check(!HeadsetLightingUiFacts.SessionMatches(sessions,state,read),"same ID replacement invalidates stale UI");
        var app=new Application();var window=new MainWindow(true);try{var target=new StackPanel();typeof(MainWindow).GetMethod("OriginalHeadsetLightingPage",Hidden).Invoke(window,new object[]{target,state});target.Measure(new Size(1246,605));target.Arrange(new Rect(0,0,1246,605));target.UpdateLayout();Check(Tree<Button>(target).Count(b=>Convert.ToString(b.Tag).StartsWith("HeadsetLighting.Effect."))==5,"all five named effect entries remain visible");Check(Tree<TextBlock>(target).Single(t=>Convert.ToString(t.Tag)=="HeadsetLighting.CurrentTitle").Text=="—","no static1 current selection fabricated");Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"render creates no HWND");}finally{window.Close();app.Shutdown();}
        Console.WriteLine("Headset effect replacement GUI: "+checks+" pure facts/unshown layout checks; workers/native/HWND=0.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
#endif
