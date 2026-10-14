using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

// Local development only. Source assemblies are loaded from bytes so the
// visible preview does not lock the compiler's output. Not shipped by setup.
internal sealed class LivePreviewHost {
 readonly Application app;
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 readonly string previewFolder,payload;
 readonly DispatcherTimer timer;
 Window current;
 string currentRuntime;
 string attemptedFingerprint,pendingFingerprint,lastError="",loadedHash="";
 DateTime sourceChangedAt=DateTime.UtcNow,lastStatus=DateTime.MinValue;
 byte[] pendingAssembly;
 bool building,swapping,stopped;
 int reloads;
 static readonly string[] workers={"DirectAuraWorker.exe","SDKWorker.exe","GearLinkWorker.exe","StorageWorker.exe","AudioEndpointWorker.exe","DiscoveryWorker.exe","NotebookWorker.exe","FanNativeWorker.exe","WindowsBrightnessWorker.exe"};
 LivePreviewHost(Application application,bool startInactive) {
  app=application;previewFolder=Path.Combine(root,".preview");payload=Path.Combine(previewFolder,"payload.exe");
  Directory.CreateDirectory(previewFolder);
  currentRuntime=PrepareWorkers();
  AppDomain.CurrentDomain.SetData("OpenAC.PreviewWorkerRoot",currentRuntime);
  string resume=null,resumeFile=Path.Combine(previewFolder,"resume.json");
  if(File.Exists(resumeFile)&&new FileInfo(resumeFile).Length<=4194304)resume=File.ReadAllText(resumeFile);
  try{current=NewWindow(File.ReadAllBytes(payload),resume!=null,resume);}catch{if(resume==null)throw;current=NewWindow(File.ReadAllBytes(payload),false,null);}
  loadedHash=Hash(File.ReadAllBytes(payload));
  attemptedFingerprint=pendingFingerprint=SourceFingerprint();
  AttachClose(current);app.MainWindow=current;current.ShowActivated=!startInactive;current.Show();WriteStatus();
  timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};
  timer.Tick+=async(s,e)=>await Tick();timer.Start();
 }
 void AttachClose(Window window){window.Closed+=(s,e)=>{if(!swapping&&ReferenceEquals(current,window)){stopped=true;if(timer!=null)timer.Stop();WriteStatus();app.Shutdown();}};}
 async Task Tick() {
  if(stopped)return;
  try {
   string fingerprint=SourceFingerprint();
   if(fingerprint!=pendingFingerprint){pendingFingerprint=fingerprint;sourceChangedAt=DateTime.UtcNow;}
   if(!building&&fingerprint!=attemptedFingerprint&&(DateTime.UtcNow-sourceChangedAt).TotalMilliseconds>=900){
    building=true;attemptedFingerprint=fingerprint;WriteStatus();
    try {
     string output=await Compile();
     File.WriteAllText(Path.Combine(previewFolder,"build.log"),output,new UTF8Encoding(false));
     pendingAssembly=File.ReadAllBytes(payload);lastError="";
    }catch(Exception ex){lastError=ex.Message;}
    finally{building=false;WriteStatus();}
   }
   if(pendingAssembly!=null&&!swapping&&CanReload(current))await ReplaceWindow();
   if((DateTime.UtcNow-lastStatus).TotalSeconds>=2)WriteStatus();
  }catch(Exception ex){lastError=Unwrap(ex).Message;WriteStatus();}
 }
 async Task<string> Compile() {
  return await Task.Run(()=>{
   string temporary=Path.Combine(previewFolder,"payload-building.exe");
   var start=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(root,"build.ps1")+"\" -OutputPath \""+temporary+"\""){
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root};
   using(var process=Process.Start(start)){
    Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
    if(!process.WaitForExit(30000)){process.Kill();throw new TimeoutException("预览编译超时；保留上一版。");}
    string log=output.Result+error.Result;
    if(process.ExitCode!=0){File.WriteAllText(Path.Combine(previewFolder,"build.log"),log,new UTF8Encoding(false));throw new InvalidOperationException("预览编译失败；保留上一版。详情见 .preview/build.log。");}
    if(File.Exists(payload))File.Replace(temporary,payload,null);else File.Move(temporary,payload);
    return log;
   }
  });
 }
 async Task ReplaceWindow() {
  // Construct and restore before closing the good window. Constructor/state
  // errors therefore leave the current preview visible and usable.
  byte[] bytes=pendingAssembly;pendingAssembly=null;
  Window previous=current,next=null;
  string state=(string)previous.GetType().GetMethod("CapturePreviewState").Invoke(previous,null);
  bool activate=previous.IsActive;
  string nextRuntime=null;
  try{nextRuntime=PrepareWorkers();AppDomain.CurrentDomain.SetData("OpenAC.PreviewWorkerRoot",nextRuntime);next=NewWindow(bytes,true,state);}
  catch{AppDomain.CurrentDomain.SetData("OpenAC.PreviewWorkerRoot",currentRuntime);if(next!=null)next.Close();throw;}
  swapping=true;WriteStatus();
  try {
   var closed=new TaskCompletionSource<bool>();
   previous.Closed+=(s,e)=>closed.TrySetResult(true);
   previous.Close();await closed.Task;
   currentRuntime=nextRuntime;current=next;AttachClose(next);app.MainWindow=next;next.ShowActivated=activate;next.Show();
   reloads++;loadedHash=Hash(bytes);lastError="";
  }finally{swapping=false;WriteStatus();}
 }
 static Window NewWindow(byte[] bytes,bool reload,string state) {
  var assembly=Assembly.Load(bytes);
  var type=assembly.GetType("RogControl.MainWindow",true);
  var window=(Window)Activator.CreateInstance(type,new object[]{false,reload});
  try{if(state!=null)type.GetMethod("RestorePreviewState").Invoke(window,new object[]{state});return window;}
  catch{window.Close();throw;}
 }
 static bool CanReload(Window window){return window!=null&&(bool)window.GetType().GetProperty("CanPreviewReload").GetValue(window,null);}
 string SourceFingerprint() {
  string script=File.ReadAllText(Path.Combine(root,"build.ps1"));
  var data=new StringBuilder(script);
  foreach(Match match in Regex.Matches(script,"'([A-Za-z][A-Za-z0-9]*\\.cs)'")){
   string file=Path.Combine(root,match.Groups[1].Value);data.Append('\n').Append(file).Append('\n').Append(File.ReadAllText(file));
  }
  data.Append(File.ReadAllText(Path.Combine(root,"app.manifest")));
  foreach(string name in workers){string path=Path.Combine(root,name);data.Append(name).Append(File.Exists(path)?Hash(File.ReadAllBytes(path)):"missing");}
  return Hash(Encoding.UTF8.GetBytes(data.ToString()));
 }
 string PrepareWorkers() {
  // Each generation gets its own paths. A killed worker may retain its image
  // lock briefly; it can never block preparing the next visible window.
  string runtime=Path.Combine(previewFolder,"runtime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(runtime);
  foreach(string name in workers) {
   string source=Path.Combine(root,name),destination=Path.Combine(runtime,name);
   if(!File.Exists(source))throw new FileNotFoundException("请先运行 build-all.ps1。",source);
   File.Copy(source,destination,false);
   string config=source+".config";if(File.Exists(config))File.Copy(config,destination+".config",true);
  }
  return runtime;
 }
 static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 static Exception Unwrap(Exception exception){while(exception is TargetInvocationException&&exception.InnerException!=null)exception=exception.InnerException;return exception;}
 void WriteStatus() {
  try {
   string page=current==null?"":(string)current.GetType().GetProperty("PreviewPage").GetValue(current,null);
   bool writes=current!=null&&(bool)current.GetType().GetProperty("PreviewHardwareWritesEnabled").GetValue(current,null);
   string json=new JavaScriptSerializer().Serialize(new {pid=Process.GetCurrentProcess().Id,updatedAt=DateTime.UtcNow.ToString("o"),page=page,reloads=reloads,building=building,swapping=swapping,stopped=stopped,error=lastError,payloadSha256=loadedHash,hardwareWritesEnabled=writes});
   string temporary=Path.Combine(previewFolder,"status.new"),destination=Path.Combine(previewFolder,"status.json");
   File.WriteAllText(temporary,json,new UTF8Encoding(false));if(File.Exists(destination))File.Replace(temporary,destination,null);else File.Move(temporary,destination);
   lastStatus=DateTime.UtcNow;
  }catch{}
 }
 [STAThread]static int Main(string[] arguments) {
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  app.DispatcherUnhandledException+=(s,e)=>{try{Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview"));File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","host.log"),DateTime.UtcNow.ToString("o")+" "+e.Exception+Environment.NewLine);}catch{}e.Handled=true;};
  if(arguments.Length>0&&arguments[0]=="--verify-state")return VerifyState();
  bool owns;using(var mutex=new Mutex(true,"Local\\ROGControl-0.1",out owns)) {
   if(!owns){MessageBox.Show("OpenAC 已在运行，请先关闭已有窗口。","OpenAC");return 1;}
   try{new LivePreviewHost(app,Array.IndexOf(arguments,"--no-activate")>=0);app.Run();return 0;}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","host.log"),Unwrap(ex).ToString());return 1;}
  }
 }
 static int VerifyState() {
  try {
   byte[] bytes=File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","payload.exe"));
   var first=Assembly.Load(bytes).GetType("RogControl.MainWindow",true);
   var second=Assembly.Load(bytes).GetType("RogControl.MainWindow",true);
   if(first==second)throw new Exception("Reload types must belong to separate assemblies.");
   var window=(Window)Activator.CreateInstance(first,new object[]{true,true});
   string seed="{\"Schema\":1,\"Page\":\"设置\",\"SettingsTab\":\"关于\",\"Left\":120,\"Top\":90,\"Width\":1400,\"Height\":800,\"GearTabs\":{\"keyboard\":\"OLED\"},\"Brightness\":80,\"Speed\":3,\"Color\":\"#123456\",\"Effect\":\"static\"}";
   first.GetMethod("RestorePreviewState").Invoke(window,new object[]{seed});
   string json=(string)first.GetMethod("CapturePreviewState").Invoke(window,null);
   var replacement=(Window)Activator.CreateInstance(second,new object[]{true,true});
   second.GetMethod("RestorePreviewState").Invoke(replacement,new object[]{json});
   var state=new JavaScriptSerializer().DeserializeObject((string)second.GetMethod("CapturePreviewState").Invoke(replacement,null)) as System.Collections.Generic.Dictionary<string,object>;
   if(Convert.ToString(state["Page"])!="更新库"||Convert.ToString(state["SettingsTab"])!="关于"||Convert.ToDouble(state["Left"])!=120||Convert.ToDouble(state["Width"])!=1400)throw new Exception("Page, tab or geometry was not restored.");
   var tabs=state["GearTabs"] as System.Collections.Generic.Dictionary<string,object>;
   if(tabs==null||Convert.ToString(tabs["keyboard"])!="OLED")throw new Exception("Device tab was not restored.");
   if((bool)second.GetProperty("PreviewHardwareWritesEnabled").GetValue(replacement,null))throw new Exception("Preview restored hardware permission.");
   bool rejected=false;try{Assembly.Load(new byte[]{1,2,3});}catch(BadImageFormatException){rejected=true;}
   if(!rejected)throw new Exception("Corrupt payload was not rejected.");
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","verify-state.json"),"{\"ok\":true,\"separateAssemblies\":true,\"pageAndTabPreserved\":true,\"geometryPreserved\":true,\"hardwarePermissionRestored\":false,\"corruptPayloadRejected\":true,\"hardwareExecuted\":false}");
   return 0;
  }catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","verify-state-error.txt"),Unwrap(ex).ToString());return 1;}
 }
}
