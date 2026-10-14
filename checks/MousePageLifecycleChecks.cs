// SPDX-License-Identifier: MIT
// Data restoration and unshown composition only; no device or input calls.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using RogControl;

public static class MousePageLifecycleChecks {
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static object Field(MainWindow window,string name){return typeof(MainWindow).GetField(name,Hidden).GetValue(window);}
 static void Set(MainWindow window,string name,object value){typeof(MainWindow).GetField(name,Hidden).SetValue(window,value);}
 static object Call(MainWindow window,string name,params object[] args){return typeof(MainWindow).GetMethod(name,Hidden).Invoke(window,args);}
 static Dictionary<string,object> Obj(params object[] pairs){var map=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)map[(string)pairs[i]]=pairs[i+1];return map;}
 [STAThread] public static int Main(){try{Run();return 0;}catch(Exception error){Console.Error.WriteLine("Mouse page lifecycle failed after "+checks+": "+error);return 1;}}
 static void Run(){
  var app=new Application();var source=new MainWindow(true);const string id="hid-0123456789abcdef-7548";
  var snapshot=Obj("modelPid",7548);var state=Obj("deviceId",id,"kind","mouse","pid",7548,"capturedAt","12:34:56","online",true,"connected",true,"readbackConfirmed",true,"snapshot",snapshot);
  ((Dictionary<string,Dictionary<string,object>>)Field(source,"gearStates"))["mouse"]=state;
  ((Dictionary<string,object>)Field(source,"gearReadSessions"))["mouse"]=new object();
  var tabs=(Dictionary<string,string>)Field(source,"gearTabs");tabs["mouse"]="校准";Set(source,"page","device:mouse");
  Set(source,"mouseButtonsDraft",new MouseButtonsDraft{DeviceId=id,CapturedAt="12:34:56",SelectedButton="sideKeyLeftForward",Type="mouse",Target="MouseClickLeft",BaselineTarget="MouseLeftForward",BaselineCode=485,Shift=true,Dirty=true});
  Set(source,"mouseLightingDraft",new MouseLightingDraft{DeviceId=id,SelectedEffect=0,Brightness=0,Color=new[]{0,0,0},Dirty=true});
  Set(source,"mouseZoneDraft",new MouseZoneDraft{DeviceId=id,CapturedAt="12:34:56",Enabled=false,Dpi=1600,DpiY=3200,PollingRate=4000,TxPower=0,Dirty=true});
  Set(source,"mouseCalibrationAutoExpanded",true);Set(source,"mouseCalibrationCustomExpanded",true);Set(source,"mouseZoneExpanded",false);Set(source,"mouseZoneYExpanded",true);
  ((Dictionary<string,DateTime>)Field(source,"pageReadAttempts"))["device:mouse"]=DateTime.UtcNow;
  ((HardwareControlPolicy)Field(source,"hardwareControlPolicy")).SetEnabled(true);
  string json=source.CapturePreviewState();var serializer=new JavaScriptSerializer{MaxJsonLength=4194304};var captured=serializer.Deserialize<PreviewSessionState>(json);
  Check(captured.MouseButtonsDraft.Shift&&captured.MouseButtonsDraft.Dirty&&captured.MouseButtonsDraft.SelectedButton=="sideKeyLeftForward","button selection and draft captured as data");
  Check(captured.MouseLightingDraft.Brightness==0&&captured.MouseLightingDraft.SelectedEffect==0&&captured.MouseLightingDraft.Color.SequenceEqual(new[]{0,0,0}),"zero lighting values survive capture");
  Check(captured.MouseZoneDraft.Dpi==1600&&captured.MouseZoneDraft.DpiY==3200&&captured.MouseZoneDraft.TxPower==0&&captured.MouseZoneDraft.Enabled==false,"independent Y and false/zero Zone values retained");
  Check(captured.MouseCalibrationAutoExpanded&&captured.MouseCalibrationCustomExpanded&&!captured.MouseZoneExpanded&&captured.MouseZoneYExpanded,"local expanded views captured");
  Check(json.IndexOf("gearReadSessions",StringComparison.Ordinal)<0&&json.IndexOf("SessionIdentity",StringComparison.Ordinal)<0,"live worker bindings excluded from serialized state");
  var target=new MainWindow(true);target.RestorePreviewState(json);
  var buttons=(MouseButtonsDraft)Field(target,"mouseButtonsDraft");var lighting=(MouseLightingDraft)Field(target,"mouseLightingDraft");var zone=(MouseZoneDraft)Field(target,"mouseZoneDraft");
  Check(buttons!=null&&buttons.SelectedButton=="sideKeyLeftForward"&&buttons.Target=="MouseClickLeft"&&buttons.Shift&&buttons.Dirty,"button local draft restored");
  Check(lighting!=null&&lighting.SelectedEffect==0&&lighting.Brightness==0&&lighting.Color.SequenceEqual(new[]{0,0,0})&&lighting.Dirty,"static lighting local draft restored");
  Check(zone!=null&&zone.Dpi==1600&&zone.DpiY==3200&&zone.TxPower==0&&zone.Enabled==false&&zone.Dirty,"Zone local draft restored without joining axes");
  Check(!Object.ReferenceEquals(lighting.Color,((MouseLightingDraft)Field(source,"mouseLightingDraft")).Color),"replacement arrays detached from old assembly data");
  Check((bool)Field(target,"mouseCalibrationAutoExpanded")&&(bool)Field(target,"mouseCalibrationCustomExpanded")&&!(bool)Field(target,"mouseZoneExpanded")&&(bool)Field(target,"mouseZoneYExpanded"),"view expansion restored");
  Check(target.PreviewPage=="device:mouse"&&((Dictionary<string,string>)Field(target,"gearTabs"))["mouse"]=="校准","page and exact device tab restored");
  Check(!((HardwareControlPolicy)Field(target,"hardwareControlPolicy")).Enabled&&!target.PreviewHardwareWritesEnabled,"write permission not restored");
  Check(((Dictionary<string,object>)Field(target,"gearReadSessions")).Count==0,"live gear sessions not restored");
  Check(!((Dictionary<string,DateTime>)Field(target,"pageReadAttempts")).ContainsKey("device:mouse"),"cached gear cooldown removed so fresh read can run");
  var cached=((Dictionary<string,Dictionary<string,object>>)Field(target,"gearStates"))["mouse"];
  Check(!(bool)Call(target,"CurrentGearReadSession",new object[]{"mouse",cached,null}),"cached snapshot cannot satisfy fresh process binding");
  Check(!(bool)Call(target,"PreserveVisiblePageReadDraft","device:mouse"),"restored draft does not block fresh read");
  Check(!((WorkerClient)Field(source,"gearWorker")).Running&&!((WorkerClient)Field(target,"gearWorker")).Running,"capture and restore never launch a worker");
  Check(target.CanPreviewReload,"idle restored page allows replacement");
  foreach(string applying in new[]{"mouseButtonsApplying","mouseLightingApplying","mouseZoneApplying"}){Set(target,applying,true);Check(!target.CanPreviewReload,"active apply delays replacement: "+applying);Set(target,applying,false);}
  var targetTabs=(Dictionary<string,string>)Field(target,"gearTabs");var viewKeys=new HashSet<string>();foreach(string tab in new[]{"按键","灯光","校准","Zone 模式"}){targetTabs["mouse"]=tab;string key=(string)Call(target,"PageReadViewKey","device:mouse");Check(key.EndsWith(":"+tab,StringComparison.Ordinal),"auto-read view key includes exact tab: "+tab);viewKeys.Add(key);}
  Check(viewKeys.Count==4,"different mouse tabs cannot share a same-view draft decision");
  captured.MouseLightingDraft.Brightness=101;captured.MouseZoneDraft.DpiY=99;captured.MouseButtonsDraft.Target="not-a-protocol-target";captured.Page="控制面板";var invalid=new MainWindow(true);invalid.RestorePreviewState(serializer.Serialize(captured));
  Check(Field(invalid,"mouseLightingDraft")==null,"invalid restored lighting discarded");Check(Field(invalid,"mouseZoneDraft")==null,"invalid restored Zone discarded");Check(Field(invalid,"mouseButtonsDraft")==null,"invalid restored binding discarded");
  Check(!((WorkerClient)Field(invalid,"gearWorker")).Running&&!invalid.PreviewHardwareWritesEnabled,"invalid restore cannot start a worker or grant writes");
  app.Shutdown();Console.WriteLine("Mouse page lifecycle: "+checks+" pure checks passed. No hardware operation or shown test window.");
 }
}
