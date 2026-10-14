// SPDX-License-Identifier: MIT
// Pure injected worker replies and unshown WPF layouts; no mobile driver operation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RogControl;

public static class NotebookGuiTests {
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks,layouts,uiThread;
 static void Check(bool good,string label){checks++;if(!good)throw new Exception(label);}
 static void Set(MainWindow window,string field,object value){typeof(MainWindow).GetField(field,Hidden).SetValue(window,value);}
 static object Get(MainWindow window,string field){return typeof(MainWindow).GetField(field,Hidden).GetValue(window);}
 static object Invoke(MainWindow window,string method,params object[] args){return typeof(MainWindow).GetMethod(method,Hidden|BindingFlags.Public).Invoke(window,args);}
 static IEnumerable<T> Logical<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical<T>(child))yield return item;}
 static StackPanel Content(MainWindow window){return (StackPanel)Get(window,"content");}
 static string[] Texts(MainWindow window){return Logical<TextBlock>(Content(window)).Select(t=>t.Text).ToArray();}
 static Dictionary<string,object> Obj(params object[] pairs){var result=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)result[(string)pairs[i]]=pairs[i+1];return result;}
 static Dictionary<string,object> State(string feature,int value){
  string meaning=feature.EndsWith("Fan",StringComparison.Ordinal)?"fanSpeed":feature.EndsWith("Temperature",StringComparison.Ordinal)?"temperature":feature=="keyboardBrightness"?"brightness":"chargerType";
  string unit=meaning=="fanSpeed"?"rpm":meaning=="temperature"?"C":meaning=="brightness"?"level":"code";
  return Obj("feature",feature,"value",value,"queryValue",value,"unit",unit,"valueSemantics",meaning,"presenceConfirmed",true,"readbackConfirmed",true,"valueReadbackConfirmed",true,"writable",false,"physicalValidation",false);
 }
 static Dictionary<string,object> Data(bool read,string code="GA402RJ",string family="rogNotebook"){
  return Obj("manufacturer","ASUSTeK COMPUTER INC.","model","ROG Notebook "+code,"modelCode",code,"family",family,"chassisTypes",new[]{10},"identityReadConfirmed",true,"interfaceDriverConfirmed",true,"sourcePrepared",true,"eligibleForStatus",true,"driverQueryPerformed",read,"supportsWrite",false,"supportsFanCurve",false,"supportsPowerLimits",false,"supportsLedWrites",false,"hardwareReadValidated",false,"supportReason","SOURCE_PREPARED_READ_ONLY","identityScope","osReportedManufacturerModelChassis","states",read?new object[]{State("cpuFan",2300),State("gpuFan",3100),State("cpuTemperature",52),State("gpuTemperature",47),State("chargerMode",2),State("middleFan",1800),State("keyboardBrightness",2)}:new object[0]);
 }
 static NotebookGuiReply Reply(Dictionary<string,object> data){return new NotebookGuiReply{ok=true,data=data};}
 static NotebookGuiReply Clone(NotebookGuiReply reply){var json=new JavaScriptSerializer();return json.Deserialize<NotebookGuiReply>(json.Serialize(reply));}
 sealed class Fake:INotebookGuiBackend {
  public readonly Queue<NotebookGuiReply> Replies=new Queue<NotebookGuiReply>();
  public readonly List<Dictionary<string,object>> Requests=new List<Dictionary<string,object>>();
  public readonly List<int> Threads=new List<int>();
  public bool Locked,ThrowQuarantine;
  public int Disposals,Quarantines;
  public Func<Dictionary<string,object>,Task<NotebookGuiReply>> Handler;
  public bool RestartRequired {get{return Locked;}}
  public Task<NotebookGuiReply> Request(Dictionary<string,object> request){lock(Requests){Requests.Add(new Dictionary<string,object>(request));Threads.Add(Thread.CurrentThread.ManagedThreadId);}if(Handler!=null)return Handler(request);if(Replies.Count==0)throw new IOException("FAKE_PRIVATE_ERROR_NOT_FOR_UI");return Task.FromResult(Clone(Replies.Dequeue()));}
  public void Quarantine(){Quarantines++;if(ThrowQuarantine)throw new IOException("fake quarantine failure");Locked=true;Dispose();}
  public void Dispose(){Interlocked.Increment(ref Disposals);}
 }
 static Fake Pair(Dictionary<string,object> read=null,Dictionary<string,object> scan=null){var fake=new Fake();fake.Replies.Enqueue(Reply(scan??Data(false)));fake.Replies.Enqueue(Reply(read??Data(true)));return fake;}
 static NotebookGuiSnapshot Read(Fake fake){return new NotebookGuiReader(fake).ReadAsync(CancellationToken.None).GetAwaiter().GetResult();}
 static NotebookGuiFailure Failure(NotebookGuiReader reader,string expected){try{reader.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();throw new Exception("expected "+expected);}catch(NotebookGuiFailure error){Check(error.Code==expected,"failure category "+expected+" actual "+error.Code);return error;}}
 static NotebookGuiReading Reading(NotebookGuiSnapshot snapshot,string name){return snapshot.Readings.Single(r=>r.Feature==name);}
 static Dictionary<string,object> Find(Dictionary<string,object> data,string name){return ((object[])data["states"]).Cast<Dictionary<string,object>>().Single(s=>(string)s["feature"]==name);}
 static void Use(MainWindow window,NotebookGuiSnapshot snapshot){Invoke(window,"SetNotebookRead",snapshot);Invoke(window,"ShowPage","笔记本与掌机");}
 static void Layout(MainWindow window,int width,string scenario,string output){
  var root=(FrameworkElement)window.Content;int height=width==1180?726:826;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
  foreach(var block in Logical<TextBlock>(Content(window))){if(block.ActualWidth<=0||block.ActualHeight<=0)continue;var bounds=block.TransformToAncestor(root).TransformBounds(new Rect(0,0,block.ActualWidth,block.ActualHeight));Check(bounds.Left>=-1&&bounds.Right<=width+1,scenario+" fits horizontally at "+width+": "+block.Text);if(block.TextWrapping==TextWrapping.NoWrap&&block.TextTrimming==TextTrimming.None)Check(block.DesiredSize.Width-block.Margin.Left-block.Margin.Right<=block.ActualWidth+1,scenario+" no text clipping at "+width+": "+block.Text);}
  Check(Logical<Button>(Content(window)).All(b=>!Convert.ToString(b.Content).Contains("应用")&&!Convert.ToString(b.Content).Contains("安装")),scenario+" has no write or install controls");
  var button=Logical<Button>(Content(window)).Single(b=>Convert.ToString(b.Content)=="读取"||Convert.ToString(b.Content)=="正在读取…");Check(!button.IsEnabled,"render-only read disabled at "+width+"/"+scenario);
  var scroll=Logical<ScrollViewer>(root).Single(s=>Object.ReferenceEquals(s.Content,Content(window)));Check(scroll.ScrollableWidth<1,"no horizontal scroll needed "+width+"/"+scenario);if(scroll.ScrollableHeight>0){scroll.ScrollToBottom();root.UpdateLayout();Check(scroll.VerticalOffset>=scroll.ScrollableHeight-1,"last status row accessible "+width+"/"+scenario);scroll.ScrollToTop();root.UpdateLayout();}
  Check(!window.IsVisible&&PresentationSource.FromVisual(window)==null,"window unshown at "+width+"/"+scenario);
  if(output!=null){var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,scenario+"-"+width+".png")))encoder.Save(stream);}layouts++;
 }
 static void Both(MainWindow window,string scenario,string output){foreach(int width in new[]{1538,1180})Layout(window,width,scenario,output);}
 static void PureReader(){
  var normal=Pair();var snapshot=Read(normal);Check(snapshot.EligibleForStatus&&snapshot.DriverQueryPerformed&&snapshot.Readings.Count==7,"prepared notebook receives seven scoped telemetry rows");
  Check(Reading(snapshot,"cpuFan").Text=="2300 RPM"&&Reading(snapshot,"gpuTemperature").Text=="47 °C"&&Reading(snapshot,"keyboardBrightness").Text=="2 / 3"&&Reading(snapshot,"chargerMode").Text=="USB 电源","confirmed native values preserve their units");
  Check(normal.Requests.Count==2&&(string)normal.Requests[0]["op"]=="scan"&&(string)normal.Requests[1]["op"]=="read","identity scan precedes status request");
  var features=(string[])normal.Requests[1]["features"];Check(features.Length==7&&!features.Any(f=>new[]{"performanceMode","chargeLimit","gpuEco","gpuMux","displayOverdrive"}.Contains(f)),"raw setting status endpoints are never requested");Check(normal.Threads.All(id=>id!=uiThread),"startup and request callbacks execute off the GUI thread");
  foreach(string code in new[]{"RC71L","RC72LA","RC73YA","RC73XA"}){var fake=Pair(Data(true,code,"handheld"),Data(false,code,"handheld"));var handheld=Read(fake);Check(handheld.Readings.Count==5,"handheld has only five shared telemetry rows "+code);Check(((string[])fake.Requests[1]["features"]).Length==5&&!handheld.Readings.Any(r=>r.Feature=="middleFan"||r.Feature=="keyboardBrightness"),"handheld never receives notebook-only fields "+code);}
  foreach(string reason in new[]{"desktop","unprepared","unsigned-driver","forged-desktop","ambiguous-model","wrong-family","wrong-manufacturer","missing-chassis","model-prefix"}){
   var data=Data(false);if(reason=="desktop"){data["eligibleForStatus"]=false;data["chassisTypes"]=new[]{3};data["model"]="System Product Name";data["modelCode"]="";data["sourcePrepared"]=false;}
   else if(reason=="unprepared"){data["eligibleForStatus"]=false;data["model"]="ROG GA9999";data["modelCode"]="";data["sourcePrepared"]=false;}
   else if(reason=="unsigned-driver")data["interfaceDriverConfirmed"]=false;
   else if(reason=="forged-desktop")data["chassisTypes"]=new[]{3};
   else if(reason=="ambiguous-model")data["model"]="GA402RJ RC71L";
   else if(reason=="wrong-family")data["family"]="handheld";
   else if(reason=="wrong-manufacturer")data["manufacturer"]="Different maker";
   else if(reason=="missing-chassis")data["chassisTypes"]=new int[0];
   else data["model"]="GA402RJNEW";
   var fake=Pair(scan:data);var identity=Read(fake);Check(!identity.EligibleForStatus&&!identity.DriverQueryPerformed&&identity.Readings.Count==0&&fake.Requests.Count==1,reason+" stays identity-only without native read request");
  }
  foreach(string flag in new[]{"identityReadConfirmed","interfaceDriverConfirmed","eligibleForStatus","sourcePrepared","driverQueryPerformed","supportsWrite","supportsFanCurve","supportsPowerLimits","supportsLedWrites","hardwareReadValidated"}){var data=Data(false);data[flag]="false";var fake=Pair(scan:data);Failure(new NotebookGuiReader(fake),"INVALID_RESPONSE");Check(fake.Requests.Count==1,"non-boolean top-level flag rejected before read "+flag);}
  foreach(string flag in new[]{"supportsWrite","supportsFanCurve","supportsPowerLimits","supportsLedWrites"}){var data=Data(false);data[flag]=true;Failure(new NotebookGuiReader(Pair(scan:data)),"INVALID_RESPONSE");}
  var impossible=Data(true);impossible["eligibleForStatus"]=false;Failure(new NotebookGuiReader(Pair(impossible)),"INVALID_RESPONSE");
  var badScan=Data(true);Failure(new NotebookGuiReader(Pair(scan:badScan)),"INVALID_RESPONSE");
  var impossibleStates=Data(true);impossibleStates["eligibleForStatus"]=false;impossibleStates["driverQueryPerformed"]=false;Failure(new NotebookGuiReader(Pair(impossibleStates)),"INVALID_RESPONSE");
  foreach(string mode in new[]{"missing-presence","bad-presence","missing-readback","bad-value-confirmation","writable","raw-status","unknown-zero","outside-range","bad-unit","string-value","fractional-value","duplicate","endpoint-error","no-value"}){
   var data=Data(true);var cpu=Find(data,"cpuFan");switch(mode){case "missing-presence":cpu.Remove("presenceConfirmed");break;case "bad-presence":cpu["presenceConfirmed"]=1;break;case "missing-readback":cpu["readbackConfirmed"]=false;break;case "bad-value-confirmation":cpu["valueReadbackConfirmed"]="true";break;case "writable":cpu["writable"]=true;break;case "raw-status":cpu["valueSemantics"]="nativeStatus";break;case "unknown-zero":cpu["value"]=0;cpu["presenceConfirmed"]=false;break;case "outside-range":cpu["value"]=12100;break;case "bad-unit":cpu["unit"]="percent";break;case "string-value":cpu["value"]="2300";break;case "fractional-value":cpu["value"]=23.5;break;case "duplicate":data["states"]=((object[])data["states"]).Concat(new object[]{cpu}).ToArray();break;case "endpoint-error":cpu["error"]="ENDPOINT_UNSUPPORTED";break;case "no-value":cpu["value"]=null;cpu["queryValue"]=2300;break;}
   var observed=Read(Pair(data));Check(!Reading(observed,"cpuFan").Confirmed&&Reading(observed,"cpuFan").Text=="—",mode+" does not fabricate RPM");Check(Reading(observed,"gpuFan").Confirmed,"bad CPU endpoint preserves independent confirmed GPU endpoint "+mode);
  }
  var zero=Data(true);Find(zero,"cpuFan")["value"]=0;Find(zero,"keyboardBrightness")["value"]=0;var zeros=Read(Pair(zero));Check(Reading(zeros,"cpuFan").Text=="0 RPM"&&Reading(zeros,"keyboardBrightness").Text=="0 / 3","confirmed zero remains a valid stopped fan or dark keyboard");
  var raw=Data(true);raw["states"]=((object[])raw["states"]).Concat(new object[]{Obj("feature","performanceMode","queryValue",0,"value",null,"valueSemantics","nativeStatus"),Obj("feature","chargeLimit","queryValue",80,"value",null,"valueSemantics","nativeStatus"),Obj("feature","gpuEco","queryValue",1,"value",null,"valueSemantics","nativeStatus")}).ToArray();Check(Read(Pair(raw)).Readings.Count==7,"unsolicited raw settings are not projected into GUI telemetry");
  var changed=Data(true,"GU605MI");Failure(new NotebookGuiReader(Pair(changed)),"IDENTITY_CHANGED");
  var disconnected=Data(true);disconnected["eligibleForStatus"]=false;disconnected["interfaceDriverConfirmed"]=false;disconnected["driverQueryPerformed"]=false;disconnected["states"]=new object[0];var gone=Read(Pair(disconnected));Check(!gone.EligibleForStatus&&gone.Readings.Count==0,"driver lost between scan/read clears usable telemetry");
  var crash=new Fake();crash.Replies.Enqueue(Reply(Data(false)));Failure(new NotebookGuiReader(crash),"CONNECTION_FAILED");Check(crash.Requests.Count==2&&!crash.Locked,"worker disconnect does not silently retry");
  foreach(string code in new[]{"QUERY_TIMEOUT","RESTART_REQUIRED"}){var fake=new Fake();fake.Replies.Enqueue(new NotebookGuiReply{ok=false,error=Obj("code",code,"message","FAKE_PRIVATE_ERROR_NOT_FOR_UI"),restartRequired=true});var reader=new NotebookGuiReader(fake);Check(Failure(reader,code).RestartRequired&&reader.RestartRequired&&fake.Quarantines==1,"worker timeout locks route "+code);Failure(reader,"RESTART_REQUIRED");Check(fake.Requests.Count==1,"locked worker is not respawned on another read "+code);}
  var late=new TaskCompletionSource<NotebookGuiReply>();var timeout=new Fake{Handler=request=>late.Task};var bounded=new NotebookGuiReader(timeout,25);Check(Failure(bounded,"QUERY_TIMEOUT").RestartRequired&&bounded.RestartRequired,"outer deadline quarantines hung response");int requests=timeout.Requests.Count;late.SetResult(Reply(Data(false)));Failure(bounded,"RESTART_REQUIRED");Check(timeout.Requests.Count==requests,"late success cannot unquarantine or trigger further request");
  var failedQuarantine=new Fake{ThrowQuarantine=true,Handler=request=>new TaskCompletionSource<NotebookGuiReply>().Task};var ownLock=new NotebookGuiReader(failedQuarantine,25);Failure(ownLock,"QUERY_TIMEOUT");Failure(ownLock,"RESTART_REQUIRED");Check(ownLock.RestartRequired&&failedQuarantine.Requests.Count==1,"reader lock remains effective if backend quarantine throws");
  var cancellation=new CancellationTokenSource();var entered=new ManualResetEventSlim();var cancelProvider=new Fake{Handler=request=>{entered.Set();return new TaskCompletionSource<NotebookGuiReply>().Task;}};var cancelReader=new NotebookGuiReader(cancelProvider);var canceled=cancelReader.ReadAsync(cancellation.Token);Check(entered.Wait(1000),"fake pending request started");cancellation.Cancel();try{canceled.GetAwaiter().GetResult();throw new Exception("expected cancellation");}catch(OperationCanceledException){Check(cancelProvider.Disposals>=1&&!cancelProvider.Locked,"closing cancels pending reply and disposes without a timeout quarantine");}
  var lockedPending=new TaskCompletionSource<NotebookGuiReply>();var concurrent=new Fake{Handler=request=>lockedPending.Task};var concurrentReader=new NotebookGuiReader(concurrent,25);var first=concurrentReader.ReadAsync(CancellationToken.None);Failure(concurrentReader,"INTERFACE_BUSY");try{first.GetAwaiter().GetResult();throw new Exception("expected outer deadline");}catch(NotebookGuiFailure error){Check(error.Code=="QUERY_TIMEOUT"&&concurrent.Requests.Count==1,"concurrent read never starts a second worker request");}
 }
 static void SharedBackend(){
  const string quarantineKey="OpenAC.Notebook.StatusQuarantined.v1";object prior=AppDomain.CurrentDomain.GetData(quarantineKey);AppDomain.CurrentDomain.SetData(quarantineKey,false);
  var backend=new NotebookWorkerGuiBackend();var gate=(SemaphoreSlim)typeof(NotebookWorkerGuiBackend).GetMethod("SharedRequestGate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);bool held=false;
  try{
   held=gate.Wait(0);Check(held,"pure test holds shared request gate without process startup");
   try{backend.Request(Obj("op","set")).GetAwaiter().GetResult();throw new Exception("expected read-only request guard");}catch(NotebookGuiFailure error){Check(error.Code=="READ_ONLY"&&gate.CurrentCount==0,"backend rejects writes before process startup or gate use");}
   try{backend.Request(Obj("op","scan","method","arbitrary")).GetAwaiter().GetResult();throw new Exception("expected strict fields");}catch(NotebookGuiFailure error){Check(error.Code=="INVALID_REQUEST"&&gate.CurrentCount==0,"backend rejects caller-supplied commands before process startup");}
   try{backend.Request(Obj("op","scan")).GetAwaiter().GetResult();throw new Exception("expected shared gate busy");}catch(NotebookGuiFailure error){Check(error.Code=="INTERFACE_BUSY"&&gate.CurrentCount==0,"new backend cannot launch or release another pending operation");}
   var client=(WorkerClient)typeof(NotebookWorkerGuiBackend).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(backend);Check(!client.Running,"blocked real backend has not launched a worker");
   var copy=Assembly.Load(File.ReadAllBytes(typeof(MainWindow).Assembly.Location));var type=copy.GetType("RogControl.NotebookWorkerGuiBackend",true);var copyGate=(SemaphoreSlim)type.GetMethod("SharedRequestGate",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);Check(Object.ReferenceEquals(gate,copyGate),"hot-reloaded assembly shares AppDomain System semaphore");
   var replacement=Activator.CreateInstance(type);try{var task=(Task)type.GetMethod("Request").Invoke(replacement,new object[]{Obj("op","scan")});task.GetAwaiter().GetResult();throw new Exception("expected replacement busy");}catch(Exception error){Check(error.GetType().Name=="NotebookGuiFailure"&&(string)error.GetType().GetField("Code").GetValue(error)=="INTERFACE_BUSY"&&gate.CurrentCount==0,"replacement assembly cannot add a request while old operation remains pending");}
   backend.Quarantine();Check(backend.RestartRequired&&(bool)type.GetProperty("RestartRequired").GetValue(replacement,null),"timeout latch is shared with replacement assembly");
   var later=new NotebookWorkerGuiBackend();try{later.Request(Obj("op","scan")).GetAwaiter().GetResult();throw new Exception("expected session lock");}catch(NotebookGuiFailure error){Check(error.Code=="RESTART_REQUIRED","replacement backend does not automatically respawn quarantined worker");}later.Dispose();type.GetMethod("Dispose").Invoke(replacement,null);
  }finally{if(held)gate.Release();backend.Dispose();AppDomain.CurrentDomain.SetData(quarantineKey,prior);}
  Check(gate.CurrentCount==1,"only simulated original completion releases process-wide gate");
 }
 [STAThread]public static int Main(string[] args){try{
  string output=null;if(args.Length>0){if(args.Length!=2||args[0]!="--render")throw new ArgumentException("Expected optional --render <directory>.");output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);}uiThread=Thread.CurrentThread.ManagedThreadId;PureReader();SharedBackend();
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new MainWindow(true);Invoke(window,"BeginNotebookStateExpiry");Check(Get(window,"notebookStateExpiryTimer")==null,"render-only does not create expiry timer");
  var fake=Pair();var reader=new NotebookGuiReader(fake);Set(window,"notebookReader",reader);var normal=reader.ReadAsync(CancellationToken.None).GetAwaiter().GetResult();Use(window,normal);Both(window,"notebook",output);
  Check(Texts(window).Contains("2300 RPM")&&Texts(window).Contains("52 °C")&&Texts(window).Contains("2 / 3")&&!Texts(window).Any(t=>t.Contains("performanceMode")||t.Contains("chargeLimit")||t.Contains("queryValue")),"GUI shows telemetry without engineering or unconfirmed setting labels");
  int count=fake.Requests.Count;((Task)Invoke(window,"ReadNotebook")).GetAwaiter().GetResult();Check(fake.Requests.Count==count,"render-only callback does not invoke fake or native worker");
  Use(window,Read(Pair(Data(true,"RC71L","handheld"),Data(false,"RC71L","handheld"))));Both(window,"handheld",output);Check(!Texts(window).Contains("第三风扇")&&!Texts(window).Contains("键盘亮度"),"handheld page excludes notebook-only rows");
  var desktop=Data(false);desktop["eligibleForStatus"]=false;desktop["chassisTypes"]=new[]{3};desktop["model"]="System Product Name";desktop["modelCode"]="";desktop["sourcePrepared"]=false;Use(window,Read(Pair(scan:desktop)));Both(window,"desktop-identity",output);Check(Texts(window).Contains("仅识别 · 暂不支持读取")&&!Texts(window).Contains("CPU 风扇"),"desktop has identity only");
  var missing=Data(true);foreach(var state in ((object[])missing["states"]).Cast<Dictionary<string,object>>()){state["presenceConfirmed"]=false;state["value"]=0;}Use(window,Read(Pair(missing)));Both(window,"unknown-values",output);Check(Texts(window).Count(t=>t=="—")==7&&!Texts(window).Contains("0 RPM"),"unknown zero never appears as a working sensor");
  var longScan=Data(false);var longRead=Data(true);string model="ROG "+new string('长',220)+" GA402RJ";longScan["model"]=model;longRead["model"]=model;Use(window,Read(Pair(longRead,longScan)));Both(window,"long-model",output);Check(Logical<TextBlock>(Content(window)).Single(t=>t.Text==model).TextWrapping==TextWrapping.Wrap,"long model wraps instead of pushing values off page");
  foreach(string gate in new[]{"busy","pageReadRunning"}){
   var observed=Read(Pair());Use(window,observed);Check(Texts(window).Contains("2300 RPM"),gate+" starts with current visible values");var now=DateTime.UtcNow;typeof(NotebookGuiSnapshot).GetProperty("CapturedUtc").SetValue(observed,now.AddSeconds(-30),null);Set(window,gate,true);
   Invoke(window,"NotebookStateExpiryTick",now);Check(Texts(window).Contains("等待读取")&&!Texts(window).Contains("2300 RPM")&&observed.Readings.All(r=>!r.Confirmed&&r.Text=="—"),gate+" same-page tick clears old values and cache without navigation");Check(observed.CapturedUtc==now.AddSeconds(-30),gate+" expiry preserves original timestamp");Check(Convert.ToString(Get(window,"page"))=="笔记本与掌机",gate+" expiry keeps selected page");Set(window,gate,false);Both(window,"expired-"+gate,output);Invoke(window,"ShowPage","笔记本与掌机");Check(!Texts(window).Contains("2300 RPM"),gate+" re-render cannot make expired readings fresh");
  }
  var future=Read(Pair());Use(window,future);var point=DateTime.UtcNow;typeof(NotebookGuiSnapshot).GetProperty("CapturedUtc").SetValue(future,point.AddSeconds(6),null);Invoke(window,"NotebookStateExpiryTick",point);Both(window,"future",output);Check(Texts(window).Contains("等待读取")&&future.Readings.All(r=>!r.Confirmed),"future snapshot does not appear current");
  var fresh=Read(Pair());Use(window,fresh);var fixedNow=new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc);var currentMethod=typeof(MainWindow).GetMethod("NotebookCurrent",BindingFlags.Static|BindingFlags.NonPublic);Check((bool)currentMethod.Invoke(null,new object[]{fixedNow.AddSeconds(-30).AddTicks(1),fixedNow}),"age below 30 seconds remains current");Check(!(bool)currentMethod.Invoke(null,new object[]{fixedNow.AddSeconds(-30),fixedNow}),"age exactly 30 seconds expires");Check((bool)currentMethod.Invoke(null,new object[]{fixedNow.AddSeconds(5),fixedNow})&&!(bool)currentMethod.Invoke(null,new object[]{fixedNow.AddSeconds(5).AddTicks(1),fixedNow}),"future tolerance ends after five seconds");
  var pending=new TaskCompletionSource<NotebookGuiSnapshot>();Set(window,"notebookReadTask",pending.Task);Check(window.NotebookReadBusy&&!window.NotebookCanPreviewReload&&Object.ReferenceEquals(window.NotebookReadTask,pending.Task),"pending request blocks preview replacement");Set(window,"notebookReadTask",null);Check(window.NotebookCanPreviewReload,"settled status permits preview replacement");
  foreach(var error in new[]{new NotebookGuiFailure("IDENTITY_CHANGED",false),new NotebookGuiFailure("CONNECTION_FAILED",false),new NotebookGuiFailure("INVALID_RESPONSE",false),new NotebookGuiFailure("QUERY_TIMEOUT",true)}){Use(window,Read(Pair()));Invoke(window,"NotebookReadFailed",error);Invoke(window,"ShowPage","笔记本与掌机");Both(window,"error-"+error.Code,output);Check(Get(window,"notebookSnapshot")==null&&!Texts(window).Contains("2300 RPM"),error.Code+" removes previous current identity and sensor values");Check(!Texts(window).Any(t=>t.Contains("PRIVATE_ERROR")||t.Contains(error.Code)),error.Code+" uses short user text instead of raw worker details");}
  fake.Locked=true;Invoke(window,"SetNotebookRead",new object[]{null});Invoke(window,"ShowPage","笔记本与掌机");Both(window,"quarantined",output);Check(Texts(window).Contains("读取超时，请重新打开应用。"),"locked route has explicit recovery status");count=fake.Requests.Count;((Task)Invoke(window,"ReadNotebook")).GetAwaiter().GetResult();Check(fake.Requests.Count==count,"locked GUI cannot respawn worker");
  ((Task)Invoke(window,"DisposeNotebookStatusAsync")).GetAwaiter().GetResult();Check(fake.Disposals>=1,"lifecycle hook disposes injected worker");Check(!window.IsVisible&&PresentationSource.FromVisual(window)==null,"all GUI checks remain unshown");app.Shutdown();Console.WriteLine("Notebook GUI: "+checks+" pure checks, "+layouts+" unshown layouts passed; WMI/ATKACPI/native writes=0; shown windows=0.");return 0;
 }catch(Exception error){Console.Error.WriteLine("Notebook GUI checks failed after "+checks+": "+(error.InnerException??error));return 1;}}
}
