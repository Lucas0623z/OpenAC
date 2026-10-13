using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RogControl;

public static class ControlPolicyTests {
 static int checks;
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static object Invoke(MainWindow window,string name,params object[] arguments){return typeof(MainWindow).GetMethod(name,Private).Invoke(window,arguments);}
 static object Field(MainWindow window,string name){return typeof(MainWindow).GetField(name,Private).GetValue(window);}
 static void Flag(MainWindow window,string name,bool value){typeof(MainWindow).GetField(name,Private).SetValue(window,value);}
 static IEnumerable<T> Logical<T>(DependencyObject item) where T:DependencyObject {if(item is T)yield return (T)item;foreach(var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>())foreach(var result in Logical<T>(child))yield return result;}
 static Dictionary<string,object> Obj(params object[] entries){var result=new Dictionary<string,object>();for(int i=0;i<entries.Length;i+=2)result[(string)entries[i]]=entries[i+1];return result;}
 static void Finish(object pending){((Task)pending).GetAwaiter().GetResult();}
 [STAThread] public static void Main(){
  var app=new Application();var policy=new HardwareControlPolicy();Check(!policy.Enabled,"new session disabled");int sent=0;try{policy.Demand();sent++;}catch(InvalidOperationException){}Check(sent==0,"denied before side effect");policy.SetEnabled(true);policy.Demand();Check(policy.Enabled,"explicit session enable");policy.SetEnabled(false);Check(!policy.Enabled&&!new HardwareControlPolicy().Enabled,"disable and new session reset");
  var window=new MainWindow(true);Flag(window,"render",false);var session=(HardwareControlPolicy)Field(window,"hardwareControlPolicy");var state=Obj("online",true,"deviceId","synthetic-device","setCapabilities",new[]{"pollingRate","dpiStage","eqLevels"},"snapshot",Obj("pollingRate",1000));
  // Valid targets prevent missing-target checks from concealing a removed gate.
  // Every possible request is redirected to a harmless mock, including SDK.
  var mocks=new List<WorkerClient>();foreach(string name in new[]{"gearWorker","audioEndpointWorker"}){var mock=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockWorker.exe"),1500);mocks.Add(mock);typeof(MainWindow).GetField(name,Private).SetValue(window,mock);}
  var sdkMock=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockWorker.exe"),1500);mocks.Add(sdkMock);var sdkClients=(Dictionary<string,WorkerClient>)Field(window,"sdkWorkers");sdkClients["fan"]=sdkMock;
  ((List<Dictionary<string,object>>)Field(window,"audioEndpoints")).Add(Obj("endpointId","synthetic-endpoint","flow","render","matchConfirmed",true,"readbackConfirmed",true,"active",true,"volumePercent",50,"muted",false,"name","Synthetic endpoint"));
  var profile=new ScenarioProfile{Name="Synthetic gate test"};profile.Devices.Add(new ScenarioDeviceProfile{Kind="mouse",DeviceId="synthetic-device",Name="Synthetic mouse",Settings=new List<ScenarioSetting>{new ScenarioSetting{Setting="pollingRate",Value=1000}}});Check(ScenarioRules.BuildPlan(profile).Count==1,"denial scenario has a valid nonempty plan");typeof(MainWindow).GetField("scenarioDraft",Private).SetValue(window,profile);string repositoryPath=Path.Combine(Path.GetTempPath(),"ROG-Control-policy-test-"+Guid.NewGuid().ToString("N"));typeof(MainWindow).GetField("scenarioRepository",Private).SetValue(window,new ScenarioRepository(repositoryPath));
  bool capability=(bool)typeof(MainWindow).GetMethod("GearCanSet",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{state,"pollingRate"});Check(capability,"capability remains readable while policy disabled");Check(!(bool)Invoke(window,"CanApplyGear",state,"pollingRate"),"GUI cannot apply valid device capability by default");
  Finish(Invoke(window,"SetGearSetting","mouse",state,"pollingRate",1000));Finish(Invoke(window,"SetOriginalNative","fan","7576","fixed",Obj("portId","1","rpm",800)));Finish(Invoke(window,"SetOriginalWindowsAudio","render","synthetic-endpoint","volume",50));Finish(Invoke(window,"ApplyOriginalScenarioAndSave"));
  foreach(string source in new[]{"gear","audio"}){bool denied=false;try{Finish(Invoke(window,"SendScenarioSetting",new ScenarioCommand{Source=source,Kind="mouse",DeviceId="synthetic-device",Setting="pollingRate",Value=1000}));}catch(InvalidOperationException){denied=true;}Check(denied,"individual scenario command denied: "+source);}
  Check(sdkClients.Count==1&&Object.ReferenceEquals(sdkClients["fan"],sdkMock),"native gate leaves injected SDK client unchanged");foreach(var mock in mocks)Check(typeof(WorkerClient).GetField("process",Private).GetValue(mock)==null,"no mocked worker request started");Check(!Directory.Exists(repositoryPath),"disabled apply does not save a valid scenario");Check(((List<Dictionary<string,object>>)Field(window,"audioEndpoints")).Count==1,"denial preserves a valid audio snapshot");Check(!(bool)Field(window,"busy")&&!window.ScenarioFeaturesActive,"refusal leaves session idle");
  var panel=new StackPanel();Invoke(window,"AddGearChoice",panel,"mouse",state,"pollingRate","轮询率",new[]{"1000","2000"},null);Check(Logical<Button>(panel).Count()==1&&!Logical<Button>(panel).Single().IsEnabled,"apply disabled with valid snapshot");Check(Logical<ComboBox>(panel).Single().IsEnabled,"local draft remains editable");
  var content=(StackPanel)Field(window,"content");content.Children.Clear();Invoke(window,"OriginalHardwareControlSettings");var enable=Logical<CheckBox>(content).Single();Check(enable.IsChecked==false&&enable.IsEnabled,"settings starts unchecked and available");
  session.SetEnabled(true);Check((bool)Invoke(window,"CanApplyGear",state,"pollingRate"),"explicit enable permits supported setting");Check(!(bool)Invoke(window,"CanApplyGear",state,"unknown"),"enable cannot add unknown capability");Flag(window,"closing",true);Check(!(bool)Invoke(window,"CanApplyGear",state,"pollingRate"),"closing disables writes");Flag(window,"closing",false);Flag(window,"render",true);Check(!(bool)Invoke(window,"CanApplyGear",state,"pollingRate"),"render-only disables writes");Flag(window,"render",false);session.SetEnabled(false);
  content.Children.Clear();Invoke(window,"OriginalScenarioPage");Check(Logical<Button>(content).Single(b=>Convert.ToString(b.Content)=="保存").IsEnabled,"local scenario save available");Check(!Logical<Button>(content).Single(b=>Convert.ToString(b.Content)=="应用并保存").IsEnabled,"scenario apply disabled");var captures=Logical<Button>(content).Where(b=>Convert.ToString(b.Content)=="读取并捕获").ToList();Check(captures.Count==5&&captures.All(b=>b.IsEnabled),"capture capability not tied to write permission");
  var second=new MainWindow(true);Check(!((HardwareControlPolicy)Field(second,"hardwareControlPolicy")).Enabled,"another window does not inherit consent");
  foreach(var mock in mocks)mock.Dispose();Console.WriteLine("Session control policy: "+checks+" pure checks passed. No hardware request, file save, hook or shown window.");app.Shutdown();
 }
}
