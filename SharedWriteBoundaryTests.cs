// Copyright (c) OpenAC contributors. Licensed under the MIT License.
// Synthetic JSON-lines processes only. No native libraries, WMI, HID or shown windows.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using RogControl;

public static class SharedWriteBoundaryTests {
 static int checks;static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static Dictionary<string,object> Map(params object[] pairs){var map=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)map[(string)pairs[i]]=pairs[i+1];return map;}
 static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static object Get(MainWindow window,string name){return typeof(MainWindow).GetField(name,Hidden).GetValue(window);}
 static void Set(MainWindow window,string name,object value){typeof(MainWindow).GetField(name,Hidden).SetValue(window,value);}
 static object Call(MainWindow window,string name,params object[] args){try{return typeof(MainWindow).GetMethod(name,Hidden).Invoke(window,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static Task Invoke(MainWindow window,string name,params object[] args){return (Task)Call(window,name,args);}
 static WorkerClient Mock(){return new WorkerClient(Assembly.GetExecutingAssembly().Location,25000,"--synthetic-worker");}
 static async Task<int> Counter(WorkerClient worker,string key){var reply=await worker.Request<SdkReply>(Map("op","inspect"));return Convert.ToInt32(reply.data[key],CultureInfo.InvariantCulture);}
 static MainWindow Window(){var window=new MainWindow(true);Set(window,"render",false);Set(window,"page","工具");((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(true);return window;}
 static void Reset(MainWindow window){Set(window,"render",false);Set(window,"closing",false);Set(window,"busy",false);Set(window,"auraSyncBusy",false);((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(true);Set(window,"deviceHotplugQueue",new DeviceHotplugQueue());}
 static Dictionary<string,Dictionary<string,object>> Snapshots(MainWindow window){return (Dictionary<string,Dictionary<string,object>>)Get(window,"sdkSnapshots");}
 static Dictionary<string,SharedWriteReadBinding> Receipts(MainWindow window,string name){return (Dictionary<string,SharedWriteReadBinding>)Get(window,name);}
 static List<Dictionary<string,object>> Endpoints(MainWindow window){return (List<Dictionary<string,object>>)Get(window,"audioEndpoints");}
 static Dictionary<string,object> Clone(Dictionary<string,object> state){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(state));}
 static bool Rejected(Action action){try{action();return false;}catch(InvalidOperationException){return true;}catch(OperationCanceledException){return true;}}
 [STAThread] public static int Main(string[] args) {
  if(args.Length==1&&args[0]=="--synthetic-worker")return MockMain();
  try {var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));Task task=Run();var frame=new DispatcherFrame();task.ContinueWith(t=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();app.Shutdown();Console.WriteLine("Shared write boundary: "+checks+" checks passed. Synthetic processes; hardware/native/shown windows = 0.");return 0;}
  catch(Exception e){Console.Error.WriteLine("Shared write boundary failed after "+checks+": "+e);return 1;}
 }
 static void BindingChecks() {
  object session=new object();var state=Map("deviceId","synthetic","capturedAt","12:00:00","supportsWrite",true);var receipt=new SharedWriteReadBinding(state,session,3,"deviceId","synthetic",null,"lcd");
  Check(receipt.Matches(state,session,3),"a current receipt matches its exact source");Check(!receipt.Matches(Clone(state),session,3),"same timestamp and contents do not replace the source reference");Check(!receipt.Matches(state,new object(),3),"session replacement rejected");Check(!receipt.Matches(state,session,4),"generation replacement rejected");
  state["deviceId"]="other";Check(!receipt.Matches(state,session,3),"in-place identity mutation rejected");state["deviceId"]="synthetic";state["capturedAt"]="12:00:01";Check(!receipt.Matches(state,session,3),"in-place capture mutation rejected");state["capturedAt"]="12:00:00";
  Check(Rejected(()=>new SharedWriteReadBinding(state,null,3,"deviceId","synthetic",null,"lcd")),"an unstarted worker cannot create a receipt");Check(Rejected(()=>new SharedWriteReadBinding(state,session,3,"deviceId","other",null,"lcd")),"read identity must match requested identity");
  var audio=Map("endpointId","same-id","flow","render","capturedAt","12:00:00");var render=new SharedWriteReadBinding(audio,session,3,"endpointId","same-id","flow","render");audio["flow"]="capture";Check(!render.Matches(audio,session,3),"identical audio ID cannot substitute the other direction");
  int grants=0;Action permission=()=>grants++;receipt.Demand(state,session,3,true,false,false,false,true,permission,()=>true,CancellationToken.None);Check(grants==1,"valid actual boundary invokes permission");
  var cancellation=new CancellationTokenSource();cancellation.Cancel();Check(Rejected(()=>receipt.Demand(state,session,3,true,false,false,false,true,permission,()=>true,cancellation.Token)),"cancelled boundary cannot send");Check(grants==1,"cancelled boundary does not request another permission");cancellation.Dispose();
  foreach(string changed in new[]{"closing","render","aura","inactive","topology","capability","permission"})Check(Rejected(()=>receipt.Demand(state,session,3,changed!="topology",changed=="closing",changed=="render",changed=="aura",changed!="inactive",()=>{if(changed=="permission")throw new InvalidOperationException();},()=>changed!="capability",CancellationToken.None)),"boundary blocks "+changed);
 }
 static async Task SdkChecks() {
  var window=Window();using(var worker=Mock()) {
   ((Dictionary<string,WorkerClient>)Get(window,"sdkWorkers"))["lcd"]=worker;
   foreach(string changed in new[]{"closing","render","permission","aura","reference","stamp","capability","generation","restart"}) {
    Reset(window);await Invoke(window,"ReadOriginalSnapshot","lcd","7299");Check(Receipts(window,"sdkReadBindings").ContainsKey("lcd:7299"),"explicit SDK read binds the current worker: "+changed+"; "+String.Join(" | ",(List<string>)Get(window,"history")));int before=await Counter(worker,"setFrames");object old=worker.SessionIdentity;
    var occupying=worker.Request<SdkReply>(Map("op","holdLane"));var pending=(Task<Dictionary<string,object>>)Call(window,"SetOriginalNative","lcd","7299","brightness",Map("value",50),null);Check(!pending.IsCompleted,"SDK apply actually waits in a held lane: "+changed);
    if(changed=="closing")Set(window,"closing",true);else if(changed=="render")Set(window,"render",true);else if(changed=="permission")((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(false);else if(changed=="aura")Set(window,"auraSyncBusy",true);else if(changed=="reference")Snapshots(window)["lcd:7299"]=Clone(Snapshots(window)["lcd:7299"]);else if(changed=="stamp")Snapshots(window)["lcd:7299"]["capturedAt"]="changed";else if(changed=="capability")Snapshots(window)["lcd:7299"]["brightnessWritable"]=false;else if(changed=="generation")((DeviceHotplugQueue)Get(window,"deviceHotplugQueue")).MarkChanged(0);
    if(changed=="restart"){worker.Kill();try{await occupying;}catch(Exception){}await worker.Request<SdkReply>(Map("op","read","kind","lcd","deviceId","7299"));}else await occupying;
    Check(await pending==null,"SDK blocked apply returns no success: "+changed);int after=await Counter(worker,"setFrames");Check(after==(changed=="restart"?0:before),"no SDK SET frame after "+changed);Check(changed=="restart"?!Object.ReferenceEquals(old,worker.SessionIdentity):Object.ReferenceEquals(old,worker.SessionIdentity),"rejection preserves expected worker lifecycle: "+changed);
   }
   Reset(window);await Invoke(window,"ReadOriginalSnapshot","lcd","7299");var source=Snapshots(window)["lcd:7299"];int frames=await Counter(worker,"setFrames");var result=await (Task<Dictionary<string,object>>)Call(window,"SetOriginalNative","lcd","7299","brightness",Map("value",50),null);Check(result!=null&&Convert.ToString(result["setting"])=="brightness","SDK valid explicit field succeeds");Check(await Counter(worker,"setFrames")==frames+1,"one SDK field emits exactly one SET");Check(!Object.ReferenceEquals(source,Snapshots(window)["lcd:7299"])&&Receipts(window,"sdkReadBindings")["lcd:7299"].State==Snapshots(window)["lcd:7299"],"post-SET GET creates a new current bound source");
   frames=await Counter(worker,"setFrames");result=await (Task<Dictionary<string,object>>)Call(window,"SetOriginalNative","lcd","7299","brightness",Map("value",60),new object());Check(result==null&&await Counter(worker,"setFrames")==frames,"explicit opaque session mismatch cannot fall back to default receipt");
   Reset(window);await Invoke(window,"ReadOriginalSnapshot","lcd","7299");var bindings=Receipts(window,"sdkReadBindings");bindings.Clear();frames=await Counter(worker,"setFrames");result=await (Task<Dictionary<string,object>>)Call(window,"SetOriginalNative","lcd","7299","brightness",Map("value",60),null);Check(result==null&&await Counter(worker,"setFrames")==frames,"display-only snapshot cannot grant a missing read receipt");
   Reset(window);await Invoke(window,"ReadOriginalSnapshot","lcd","7299");source=Snapshots(window)["lcd:7299"];source["supportedSettings"]=new[]{"hardwareMonitor","brightness"};source["hardwareMonitorWritable"]=true;source["hardwareSensors"]=new[]{Map("id","synthetic-sensor","nativeId","1","nativeType",0,"name","CPU","defaultName","CPU")};Call(window,"BindLcdMonitorRead",source,worker.SessionIdentity,new Func<object>(()=>worker.SessionIdentity));var monitor=(LcdMonitorDraft)Get(window,"lcdMonitorDraft");monitor.Resize(1);monitor.SetCategory(source,0,0);Check(monitor.Select(source,0,"synthetic-sensor"),"opaque monitor fixture has a real bound sensor selection");Set(window,"lcdMonitorBackground",8);Set(window,"lcdMonitorInfoCount",1);
   Check((bool)Call(window,"SdkSettingSupported","lcd",source,"hardwareMonitor",Map("backgroundId",8,"sensorIds",new[]{"synthetic-sensor"})),"exact bound opaque parameters remain supported");Check(!(bool)Call(window,"SdkSettingSupported","lcd",source,"hardwareMonitor",Map("backgroundId",9,"sensorIds",new[]{"synthetic-sensor"})),"opaque background cannot change after preparation");Check(!(bool)Call(window,"SdkSettingSupported","lcd",source,"hardwareMonitor",Map("backgroundId",8,"sensorIds",new[]{"different-sensor"})),"opaque sensor cannot change after preparation");((Dictionary<string,object>)((object[])source["hardwareSensors"])[0])["nativeId"]="2";Check(!(bool)Call(window,"SdkSettingSupported","lcd",source,"hardwareMonitor",Map("backgroundId",8,"sensorIds",new[]{"synthetic-sensor"})),"in-place opaque descriptor mutation rejects the current source");
   var fan=Map("deviceId","7576","deviceAlive",true,"readbackConfirmed",true,"supportsWrite",true,"supportedSettings",new[]{"fixed","smart"},"supportsFixedRpm",true,"supportsSmartCurve",true,"ports",new[]{Map("portId","1","supportsWrite",false,"supportsFixedRpm",false,"supportsSmartCurve",false),Map("portId","2","supportsWrite",true,"supportsFixedRpm",true,"supportsSmartCurve",true)});
   Check(!(bool)Call(window,"SdkSettingSupported","fan",fan,"fixed",Map("portId","1","rpm",1000)),"other port's controller capability cannot grant this port");Check((bool)Call(window,"SdkSettingSupported","fan",fan,"fixed",Map("portId","2","rpm",1000)),"the exact confirmed supported port remains usable");Check(!(bool)Call(window,"SdkSettingSupported","fan",fan,"fixed",Map("portId","3","rpm",1000)),"an absent port cannot receive an inferred capability");
   Check(new WindowInteropHelper(window).Handle==IntPtr.Zero,"SDK test window never has an HWND");
  }Set(window,"render",true);window.Close();
 }
 static async Task AudioChecks() {
  var window=Window();using(var worker=Mock()) {
   Set(window,"audioEndpointWorker",worker);
   foreach(string changed in new[]{"closing","permission","reference","flow","selection","generation","restart"}) {
    Reset(window);await Invoke(window,"ReadOriginalWindowsAudio");var endpoint=Endpoints(window).Single(e=>Convert.ToString(e["flow"])=="render");int before=await Counter(worker,"setFrames");var occupying=worker.Request<SdkReply>(Map("op","holdLane"));var pending=Invoke(window,"SetOriginalWindowsAudioFromRead",endpoint,"render","synthetic-audio","volume",0);Check(!pending.IsCompleted,"audio apply waits in held lane: "+changed);
    if(changed=="closing")Set(window,"closing",true);else if(changed=="permission")((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(false);else if(changed=="reference"){Endpoints(window).Remove(endpoint);Endpoints(window).Add(Clone(endpoint));}else if(changed=="flow")endpoint["flow"]="capture";else if(changed=="selection")((Dictionary<string,string>)Get(window,"audioEndpointSelection"))["render"]="other-audio";else if(changed=="generation")((DeviceHotplugQueue)Get(window,"deviceHotplugQueue")).MarkChanged(0);
    if(changed=="restart"){worker.Kill();try{await occupying;}catch(Exception){}await worker.Request<SdkReply>(Map("op","scan"));}else await occupying;await pending;Check(await Counter(worker,"setFrames")== (changed=="restart"?0:before),"audio changes emit no SET: "+changed);
   }
   Reset(window);await Invoke(window,"ReadOriginalWindowsAudio");var original=Endpoints(window).Single(e=>Convert.ToString(e["flow"])=="render");await Invoke(window,"ReadOriginalWindowsAudio");int count=await Counter(worker,"setFrames");await Invoke(window,"SetOriginalWindowsAudioFromRead",original,"render","synthetic-audio","volume",0);Check(await Counter(worker,"setFrames")==count,"an old widget cannot target a newly enumerated same-ID endpoint");await Invoke(window,"SetOriginalWindowsAudio","render","synthetic-audio","volume",0);Check(await Counter(worker,"setFrames")==count,"ID-only legacy entry cannot infer a draft source from a newer read");
   int writes=await Counter(worker,"writes");await Invoke(window,"SetOriginalWindowsAudioFromRead",Endpoints(window).Single(e=>Convert.ToString(e["flow"])=="render"),"render","synthetic-audio","volume",0);Check(await Counter(worker,"writes")==writes+1,"zero volume remains a valid explicit value");Check(Convert.ToInt32(Endpoints(window).Single(e=>Convert.ToString(e["flow"])=="render")["volumePercent"])==0,"zero actual mock readback retained");writes=await Counter(worker,"writes");await Invoke(window,"SetOriginalWindowsAudioFromRead",Endpoints(window).Single(e=>Convert.ToString(e["flow"])=="render"),"render","synthetic-audio","volume",0);Check(await Counter(worker,"writes")==writes,"same audio value is an observed no-op");Check(Convert.ToString(Get(window,"audioEndpointMessage")).Contains("已是此设置"),"no-op does not become an extra successful hardware write");Check(new WindowInteropHelper(window).Handle==IntPtr.Zero,"audio test window never has an HWND");
  }Set(window,"render",true);window.Close();
 }
 static async Task ScenarioChecks() {
  var window=Window();using(var worker=Mock()) {
   Set(window,"gearWorker",worker);
   var command=new ScenarioCommand{Source="gear",Kind="mouse",DeviceId="synthetic-mouse",Setting="pollingRate",Value=500};
   foreach(string changed in new[]{"cancel","closing","permission","reference","capability","generation","restart"}) {
    Reset(window);Set(window,"busy",true);var cancel=new CancellationTokenSource();Set(window,"scenarioCancel",cancel);await Invoke(window,"PreflightScenario",new List<ScenarioCommand>{command},cancel.Token);int before=await Counter(worker,"setFrames");var occupying=worker.Request<SdkReply>(Map("op","holdLane"));var pending=(Task<ScenarioApplyResult>)Call(window,"SendScenarioSetting",command);Check(!pending.IsCompleted,"scenario waits before its fresh GET: "+changed);
    if(changed=="cancel")cancel.Cancel();else if(changed=="closing")Set(window,"closing",true);else if(changed=="permission")((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(false);else if(changed=="reference"){var states=(Dictionary<string,Dictionary<string,object>>)Get(window,"gearStates");states["mouse"]=Clone(states["mouse"]);}else if(changed=="capability")((Dictionary<string,Dictionary<string,object>>)Get(window,"gearStates"))["mouse"]["setCapabilities"]=new object[0];else if(changed=="generation")((DeviceHotplugQueue)Get(window,"deviceHotplugQueue")).MarkChanged(0);
    if(changed=="restart"){worker.Kill();try{await occupying;}catch(Exception){}await worker.Request<SdkReply>(Map("op","read","kind","mouse","deviceId","synthetic-mouse"));}else await occupying;bool failed=false;try{await pending;}catch(Exception){failed=true;}Check(failed&&await Counter(worker,"setFrames")== (changed=="restart"?0:before),"scenario invalidation sends no SET: "+changed);Set(window,"scenarioCancel",null);cancel.Dispose();
   }
   foreach(string changed in new[]{"cancel","closing","permission","reference"}) {
    Reset(window);Set(window,"busy",true);var cancel=new CancellationTokenSource();Set(window,"scenarioCancel",cancel);await Invoke(window,"PreflightScenario",new List<ScenarioCommand>{command},cancel.Token);int before=await Counter(worker,"setFrames");await worker.Request<SdkReply>(Map("op","configure","readDelay",80));var pending=(Task<ScenarioApplyResult>)Call(window,"SendScenarioSetting",command);var holdRequest=Map("op","holdLane");var held=worker.Request<SdkReply>(holdRequest);var clock=System.Diagnostics.Stopwatch.StartNew();while(!holdRequest.ContainsKey("id")&&!pending.IsCompleted&&clock.ElapsedMilliseconds<3000)await Task.Delay(5);Check(holdRequest.ContainsKey("id")&&!pending.IsCompleted,"scenario SET is queued after its completed fresh GET: "+changed);
    if(changed=="cancel")cancel.Cancel();else if(changed=="closing")Set(window,"closing",true);else if(changed=="permission")((HardwareControlPolicy)Get(window,"hardwareControlPolicy")).SetEnabled(false);else {var states=(Dictionary<string,Dictionary<string,object>>)Get(window,"gearStates");states["mouse"]=Clone(states["mouse"]);}await held;bool failed=false;try{await pending;}catch(Exception){failed=true;}Check(failed&&await Counter(worker,"setFrames")==before,"late scenario SET boundary emits no frame after "+changed);await worker.Request<SdkReply>(Map("op","configure","readDelay",0));Set(window,"scenarioCancel",null);cancel.Dispose();
   }
   Reset(window);Set(window,"busy",true);var source=new CancellationTokenSource();Set(window,"scenarioCancel",source);var commands=new List<ScenarioCommand>{command,new ScenarioCommand{Source="gear",Kind="mouse",DeviceId="synthetic-mouse",Setting="pollingRate",Value=1000}};await Invoke(window,"PreflightScenario",commands,source.Token);int reads=await Counter(worker,"reads"),frames=await Counter(worker,"setFrames");foreach(var item in commands){var result=await (Task<ScenarioApplyResult>)Call(window,"SendScenarioSetting",item);Check(result.Confirmed,"next scenario field accepts its own preceding readback receipt");}Check(await Counter(worker,"reads")==reads+2,"every scenario SET performs one fresh GET in the same connection");Check(await Counter(worker,"setFrames")==frames+2,"sequential scenario changes send exactly their two explicit fields");Set(window,"scenarioCancel",null);source.Dispose();
   using(var audio=Mock()) {Set(window,"audioEndpointWorker",audio);Reset(window);Set(window,"busy",true);source=new CancellationTokenSource();Set(window,"scenarioCancel",source);commands=new List<ScenarioCommand>{new ScenarioCommand{Source="audio",Flow="render",DeviceId="synthetic-audio",Setting="volume",Value=0},new ScenarioCommand{Source="audio",Flow="render",DeviceId="synthetic-audio",Setting="mute",Value=true}};await Invoke(window,"PreflightScenario",commands,source.Token);reads=await Counter(audio,"reads");foreach(var item in commands)Check((await (Task<ScenarioApplyResult>)Call(window,"SendScenarioSetting",item)).Confirmed,"sequential audio scene uses its own new receipt: "+item.Setting);Check(await Counter(audio,"reads")==reads+2,"audio scene also requires fresh GET for every field");var current=Endpoints(window).Single(endpoint=>Convert.ToString(endpoint["flow"])=="render");Check(Convert.ToInt32(current["volumePercent"])==0&&(bool)current["muted"],"scene zero volume and true mute remain independently read back");Set(window,"scenarioCancel",null);source.Dispose();}
   Check(new WindowInteropHelper(window).Handle==IntPtr.Zero,"scenario test window never has an HWND");
  }Set(window,"render",true);window.Close();
 }
 static async Task Run(){BindingChecks();await SdkChecks();await AudioChecks();await ScenarioChecks();}

 // This process only maintains synthetic dictionaries. Even the SET opcode has
 // no native/backend action; counters distinguish emitted frames from no-ops.
 static int MockMain() {
  Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);var json=new JavaScriptSerializer();int frames=0,writes=0,reads=0,brightness=30,rate=1000,volume=40,readDelay=0;bool muted=false;string line;
  while((line=OpenAc.WorkerTextProtocol.ReadLine())!=null) {
   var request=json.Deserialize<Dictionary<string,object>>(line);string op=Convert.ToString(request["op"]);if(op=="holdLane")Thread.Sleep(220);string kind=request.ContainsKey("kind")?Convert.ToString(request["kind"]):"audio",id=request.ContainsKey("deviceId")?Convert.ToString(request["deviceId"]):"synthetic-audio",flow=request.ContainsKey("flow")?Convert.ToString(request["flow"]):"render";Dictionary<string,object> data;
   if(op=="inspect")data=Map("setFrames",frames,"writes",writes,"reads",reads);
   else if(op=="configure"){readDelay=Convert.ToInt32(request["readDelay"]);data=Map("configured",true);}
   else {
    if(op=="read"||op=="scan"){reads++;if(readDelay>0)Thread.Sleep(readDelay);}bool already=false;string setting=request.ContainsKey("setting")?Convert.ToString(request["setting"]):"";
    if(op=="set") {frames++;if(kind=="lcd"){int desired=Convert.ToInt32(request["value"]);already=brightness==desired;brightness=desired;}else if(kind=="mouse"){int desired=Convert.ToInt32(request["value"]);already=rate==desired;rate=desired;}else if(setting=="volume"){int desired=Convert.ToInt32(request["value"]);already=volume==desired;volume=desired;}else if(setting=="mute"){bool desired=(bool)request["value"];already=muted==desired;muted=desired;}if(!already)writes++;}
    if(kind=="lcd")data=Map("deviceId",id,"deviceAlive",true,"readbackConfirmed",true,"supportsWrite",true,"supportedSettings",new[]{"brightness","powerS5"},"brightnessWritable",true,"brightness",brightness,"powerS5",false,"firmware","AB12");
    else if(kind=="mouse")data=Map("deviceId",id,"pid",7548,"model","Synthetic mouse","online",true,"readbackConfirmed",true,"setCapabilities",new[]{"pollingRate"},"snapshot",Map("pollingRate",rate,"motionSync",false,"dpiStageCount",4));
    else data=Map("endpointId","synthetic-audio","flow",flow,"name","Synthetic audio","active",true,"matchConfirmed",true,"readbackConfirmed",true,"volumePercent",volume,"muted",muted);
    data["capturedAt"]="2026-01-01T12:00:00Z";
    if(op=="scan")data=Map("endpoints",new[]{data,Map("endpointId","synthetic-capture","flow","capture","name","Synthetic microphone","active",true,"matchConfirmed",true,"readbackConfirmed",true,"volumePercent",volume,"muted",muted,"capturedAt","2026-01-01T12:00:00Z")});
    if(op=="set"){data["setting"]=setting;data["writeAttempted"]=!already;data["writeConfirmed"]=true;data["appliedState"]=already?"alreadySet":kind=="lcd"?"verified":"confirmed";}
   }
   Console.WriteLine(json.Serialize(new{id=request["id"],ok=true,data=data}));
  }return 0;
 }
}
