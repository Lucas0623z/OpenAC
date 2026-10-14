using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace RogControl {
 public class AuraDevice { public string id {get;set;} public string name {get;set;} public uint type {get;set;} public int leds {get;set;} public int zoneCount {get;set;} public string firmware {get;set;} public bool effectWriteValidated {get;set;} }
 public class Reply { public int id {get;set;} public bool ok {get;set;} public string error {get;set;} public bool controlled {get;set;} public string animationError {get;set;} public string backend {get;set;} public string[] warnings {get;set;} public List<AuraDevice> devices {get;set;} }
 public class Component { public string Name {get;set;} public string Version {get;set;} }
 public class ServiceInfo { public string Name {get;set;} public string Status {get;set;} }
 public class Peripheral { public string deviceId {get;set;} public string name {get;set;} public string connection {get;set;} public bool supportsWrite {get;set;} }
 public class SdkReply { public int id {get;set;} public bool ok {get;set;} public object error {get;set;} public Dictionary<string,object> data {get;set;} }
 public class MachineInfo {
  public string Board="读取中", Cpu="读取中", Memory="读取中", Bios="读取中", MemorySpeed="—", ArmouryVersion="未读取";
  public double VendorMiB; public int VendorProcesses;
  public List<Component> Components=new List<Component>(); public List<ServiceInfo> Services=new List<ServiceInfo>();
  public List<string> Devices=new List<string>(); public List<string> Findings=new List<string>();
 }
 public class Profile { public string Name {get;set;} public string Effect {get;set;} public string Color {get;set;} public int Brightness {get;set;} public double Speed {get;set;} public string[] Devices {get;set;} }
 public class WorkerClient : IDisposable {
  Process process; object sessionIdentity; BlockingCollection<string> lines; readonly SemaphoreSlim gate=new SemaphoreSlim(1,1); int nextId; bool disposed; readonly string workerPath,arguments; readonly int timeout;
  public WorkerClient(string path=null,int timeoutMs=15000,string args=""){
   string resolved=path??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DirectAuraWorker.exe");
   string previewRoot=AppDomain.CurrentDomain.GetData("OpenAC.PreviewWorkerRoot") as string;
   string name=Path.GetFileName(resolved);
   if(!String.IsNullOrEmpty(previewRoot)&&String.Equals(Path.GetDirectoryName(Path.GetFullPath(resolved)).TrimEnd(Path.DirectorySeparatorChar),AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)&&new[]{"DirectAuraWorker.exe","SDKWorker.exe","GearLinkWorker.exe","StorageWorker.exe","AudioEndpointWorker.exe","DiscoveryWorker.exe","NotebookWorker.exe","FanNativeWorker.exe","WindowsBrightnessWorker.exe"}.Contains(name,StringComparer.OrdinalIgnoreCase))resolved=Path.Combine(previewRoot,name);
   workerPath=resolved;timeout=timeoutMs;arguments=args;
  }
  public bool Running {get {try{return process!=null&&!process.HasExited;}catch{return false;}}}
  public object SessionIdentity {get{return Running?sessionIdentity:null;}}
  void Start() {
   if(disposed)throw new ObjectDisposedException("设备接口已关闭");
   if(Running)return;
   lines=new BlockingCollection<string>(); var channel=lines;
   if(process!=null){try{process.Dispose();}catch{}}
   process=new Process(); process.StartInfo=new ProcessStartInfo(workerPath,arguments) {
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
   process.OutputDataReceived+=(s,e)=>{if(e.Data!=null)channel.Add(e.Data);};
   process.ErrorDataReceived+=(s,e)=>{};
   sessionIdentity=null;process.Start();sessionIdentity=new object();process.BeginOutputReadLine();process.BeginErrorReadLine();
  }
  public Task<Reply> Request(Dictionary<string,object> request) {return Request<Reply>(request);}
  public Task<T> Request<T>(Dictionary<string,object> request) {return Request<T>(request,null);}
  public Task<T> Request<T>(Dictionary<string,object> request,object expectedSession) {return Request<T>(request,expectedSession,timeout);}
  public Task<T> Request<T>(Dictionary<string,object> request,object expectedSession,int budgetMs) {return Request<T>(request,expectedSession,budgetMs,null);}
  public async Task<T> Request<T>(Dictionary<string,object> request,object expectedSession,int budgetMs,Action beforeSend) {
   if(budgetMs<1||budgetMs>timeout)throw new ArgumentOutOfRangeException("budgetMs");var requestClock=Stopwatch.StartNew();
   if(!await gate.WaitAsync(budgetMs))throw new System.TimeoutException("设备请求排队超时，没有发送设置。");
   try {
    if(disposed)throw new ObjectDisposedException("设备接口已关闭");
    if(expectedSession!=null&&!Object.ReferenceEquals(SessionIdentity,expectedSession))throw new InvalidOperationException("设备读取会话已失效，请重新读取后应用。");
    if(requestClock.ElapsedMilliseconds>=budgetMs)throw new System.TimeoutException("设备请求排队超时，没有发送设置。");
    Start();
    if(expectedSession!=null&&!Object.ReferenceEquals(SessionIdentity,expectedSession))throw new InvalidOperationException("设备读取会话已改变，请重新读取后应用。");
    if(requestClock.ElapsedMilliseconds>=budgetMs){Kill();throw new System.TimeoutException("设备启动超时，没有发送设置。");}
    // Revalidate permission/topology after acquiring the lane. A rejected
    // queued command must not get an ID, write a frame or kill another session.
    if(beforeSend!=null)beforeSend();
    if(expectedSession!=null&&!Object.ReferenceEquals(SessionIdentity,expectedSession))throw new InvalidOperationException("设备读取会话已改变，请重新读取后应用。");
    if(requestClock.ElapsedMilliseconds>=budgetMs)throw new System.TimeoutException("设备请求验证超时，没有发送设置。");
    int id=++nextId;request["id"]=id;var json=new JavaScriptSerializer();
    // The Framework writer inherits the parent's console encoding and may
    // prepend a BOM. JSON-lines workers need a predictable UTF-8 frame.
    byte[] frame=new UTF8Encoding(false).GetBytes(json.Serialize(request)+"\n");var input=process.StandardInput.BaseStream;input.Write(frame,0,frame.Length);input.Flush();
    return await Task.Run(()=>{
     Stopwatch clock=requestClock;string line;
     while(clock.ElapsedMilliseconds<budgetMs){
      if(lines.TryTake(out line,Math.Max(1,Math.Min(250,budgetMs-(int)clock.ElapsedMilliseconds)))) {
       Dictionary<string,object> result;try{result=json.Deserialize<Dictionary<string,object>>(line);}catch{continue;}
       if(result!=null&&result.ContainsKey("id")&&Convert.ToInt32(result["id"])==id)return json.Deserialize<T>(line);
      }
      if(process.HasExited)throw new InvalidOperationException("硬件工作进程已退出。错误码 0x"+unchecked((uint)process.ExitCode).ToString("X8")+"。本次请求未成功，界面可继续操作。");
     }
     Kill();throw new System.TimeoutException("设备接口超时（"+(budgetMs/1000.0).ToString("0.##")+" 秒）。已结束工作进程，界面可继续操作。硬件设置可能保留最后一次应用的状态。");
    });
   } finally {gate.Release();}
  }
  public static void ObserveFailure(Task pending){pending.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
  public void Kill() { sessionIdentity=null;try{if(Running)process.Kill();}catch{} try{if(process!=null)process.Dispose();}catch{} process=null; }
  public void Dispose() {disposed=true;Kill();}
 }
 public static class Inventory {
  static string Query(string query,string property) {
   using(var search=new ManagementObjectSearcher("root\\CIMV2",query,new EnumerationOptions {Timeout=TimeSpan.FromSeconds(6)}))
   using(var objects=search.Get()){foreach(ManagementObject obj in objects){using(obj){return Convert.ToString(obj[property],CultureInfo.InvariantCulture);}}}
   return "未知";
  }
  static bool Vendor(string name){return name.StartsWith("Armoury",StringComparison.OrdinalIgnoreCase)||name.StartsWith("ASUS",StringComparison.OrdinalIgnoreCase)||name.StartsWith("ROG",StringComparison.OrdinalIgnoreCase)||name=="LightingService";}
  public static MachineInfo Scan() {
   var m=new MachineInfo();
   try{m.Board=Query("SELECT Product FROM Win32_BaseBoard","Product");}catch(Exception e){m.Board="主板信息暂不可用";m.Findings.Add("读取主板："+e.GetType().Name);}
   try{m.Cpu=Query("SELECT Name FROM Win32_Processor","Name").Trim();}catch{m.Cpu="CPU 信息暂不可用";}
   try{
    ulong memory=0;using(var search=new ManagementObjectSearcher("SELECT Capacity,ConfiguredClockSpeed FROM Win32_PhysicalMemory"))using(var modules=search.Get()){foreach(ManagementObject module in modules){using(module){ulong capacity;if(!UInt64.TryParse(Convert.ToString(module["Capacity"]),out capacity)||capacity==0)throw new InvalidOperationException("内存容量不可用");memory+=capacity;uint clock;if(UInt32.TryParse(Convert.ToString(module["ConfiguredClockSpeed"]),out clock)&&clock>0)m.MemorySpeed=clock.ToString();}}}
    m.Memory=memory>0?(memory/1073741824.0).ToString("F0")+" GB":"内存信息暂不可用";
   }catch{m.Memory="内存信息暂不可用";}
   try{m.Bios=Query("SELECT SMBIOSBIOSVersion FROM Win32_BIOS","SMBIOSBIOSVersion");}catch{m.Bios="—";}
   try{using(var package=new Process()){package.StartInfo=new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -NonInteractive -Command \"(Get-AppxPackage -Name B9ECED6F.ArmouryCrate).Version\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};package.Start();if(package.WaitForExit(5000)){Version version;string value=package.StandardOutput.ReadToEnd().Trim();if(Version.TryParse(value,out version))m.ArmouryVersion=version.ToString();}else package.Kill();}}catch{}
   foreach(var p in Process.GetProcesses()){using(p){try{if(Vendor(p.ProcessName)){m.VendorMiB+=p.WorkingSet64/1048576.0;m.VendorProcesses++;}}catch{}}}
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
    using(var h=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))
    using(var uninstall=h.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) {
     if(uninstall==null)continue;
     foreach(string key in uninstall.GetSubKeyNames()){using(var sub=uninstall.OpenSubKey(key)){if(sub==null)continue;string n=Convert.ToString(sub.GetValue("DisplayName"));if(n.IndexOf("ASUS",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("Armoury",StringComparison.OrdinalIgnoreCase)>=0||n.IndexOf("AURA",StringComparison.OrdinalIgnoreCase)>=0||n.StartsWith("ROG"))m.Components.Add(new Component{Name=n,Version=Convert.ToString(sub.GetValue("DisplayVersion"))});}}
    }
   }
   m.Components=m.Components.GroupBy(x=>x.Name+"|"+x.Version).Select(x=>x.First()).OrderBy(x=>x.Name).ToList();
   foreach(string name in new[]{"LightingService","ArmouryCrateService","ROG Live Service","AsusFanControlService","AsusCertService","asComSvc"}) {
    try{using(var s=new ServiceController(name)){m.Services.Add(new ServiceInfo{Name=name,Status=s.Status.ToString()});}}catch{m.Services.Add(new ServiceInfo{Name=name,Status="未安装 / 无法查询"});}
   }
   try {
    using(var search=new ManagementObjectSearcher("SELECT Name,PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%VID_0B05%'"))
    using(var devices=search.Get()){foreach(ManagementObject d in devices){using(d){string name=Convert.ToString(d["Name"]),id=Convert.ToString(d["PNPDeviceID"]);int at=id.IndexOf("PID_",StringComparison.OrdinalIgnoreCase);string pid=at>=0&&id.Length>=at+8?id.Substring(at+4,4):"----";if(name.StartsWith("ROG")||name.StartsWith("AURA")||name.IndexOf("LCD")>=0)m.Devices.Add(name+" · "+pid);}}}
    m.Devices=m.Devices.Distinct().ToList();
   }catch{m.Findings.Add("USB 设备读取失败，请稍后刷新。");}
   try {
    using(var events=new EventLog("Application")){
     int examined=0,found=0;
     for(int i=events.Entries.Count-1;i>=0&&examined++<2000&&found<5;i--){var entry=events.Entries[i];if(entry.TimeGenerated<DateTime.Now.AddDays(-7))break;if((entry.InstanceId&65535)==1000){string message=entry.Message;if(message.Contains("LightingService.exe")||message.Contains("ArmouryCrate")){m.Findings.Add(entry.TimeGenerated.ToString("MM-dd HH:mm")+" · "+(message.Contains("LightingService.exe")?"LightingService":"Armoury Crate")+" 崩溃"+(message.Contains("0xc0000005")?" · 0xc0000005（访问冲突）":""));found++;}}}
    }
   }catch{m.Findings.Add("Windows 崩溃日志暂不可用。");}
   try{
    string dir=@"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\AppLog";
    string recent=Directory.GetFiles(dir,"ArmouryCrate.Service*.log").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
    if(recent!=null){string tail=ReadTail(recent,100000);int errors=Count(tail,"sensorNum fail");if(errors>0)m.Findings.Add("最近服务日志末段包含 "+errors+" 次 sensorNum fail：传感器枚举重复失败（尚未确定根因）。");}
   }catch{}
   try {string folder=@"C:\ProgramData\ASUS\ARMOURY CRATE Diagnosis\ROG Live Service";string recent=Directory.GetFiles(folder,"ROGLiveService_*.log").OrderByDescending(File.GetLastWriteTime).FirstOrDefault();if(recent!=null&&ReadTail(recent,160000).Contains("Failed to find 'OmahaChecker' entry point"))m.Findings.Add("ROG Live Service 日志出现更新检查组件入口缺失；尚未确认它是否导致本次更新失败。");}catch{}
   m.Findings.Add("官方组件安装更新器尚未重建；组件版本清单用于定位问题，不代表已修复官方更新器。");
   return m;
  }
  static int Count(string text,string value){int count=0,p=0;while((p=text.IndexOf(value,p,StringComparison.OrdinalIgnoreCase))>=0){count++;p+=value.Length;}return count;}
  static string ReadTail(string path,int bytes){using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){stream.Seek(Math.Max(0,stream.Length-bytes),SeekOrigin.Begin);using(var reader=new StreamReader(stream,Encoding.UTF8,true)){return reader.ReadToEnd();}}}
 }
 public partial class MainWindow:Window {
  readonly Brush bg=Brush("#101010"),panel=Brush("#1C1C1C"),border=Brush("#3C3C3C"),text=Brush("#F1F1F1"),muted=Brush("#AAAAAA"),accent=Brush("#D7BF6C"),green=Brush("#89BFA4");
  readonly Dictionary<string,Button> navigation=new Dictionary<string,Button>();
  StackPanel content,fixedPageHeader; ScrollViewer pageScroll; string fixedPageHeaderKey=""; TextBlock status; readonly WorkerClient worker=new WorkerClient();
  readonly Dictionary<string,WorkerClient> sdkWorkers=new Dictionary<string,WorkerClient>();
  readonly Dictionary<string,List<Peripheral>> peripherals=new Dictionary<string,List<Peripheral>>();
  readonly Dictionary<string,Dictionary<string,object>> sdkSnapshots=new Dictionary<string,Dictionary<string,object>>();
  string peripheralMessage="选择一种设备后扫描。读取只通过已安装 SDK 查询，未开放设置写入。";
  MachineInfo info=new MachineInfo(); List<AuraDevice> devices=new List<AuraDevice>(); readonly List<string> history=new List<string>();
  string page="控制面板",auraMessage="尚未读取主板灯控。打开界面只读取状态。";bool closing,busy,controlling;
  bool closeReady;
  string effect="static",color="#FF466A"; int brightness=100; double speed=3; readonly HashSet<string> selected=new HashSet<string>();
  readonly string dataDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
  readonly bool render;
  public MainWindow(bool renderOnly):this(renderOnly,false){}
  public MainWindow(bool renderOnly,bool previewReload){
   render=renderOnly;RestoreLivePreviewControlSession(previewReload);InitializeWindowsTools();LoadPreviewAssets();LoadUiPreferences();Title="OpenAC · 非官方重制预览版 0.3";Width=1538;Height=860;MinWidth=1180;MinHeight=760;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=bg;Foreground=text;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;
   SourceInitialized+=(s,e)=>{try{int enabled=1;DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle,20,ref enabled,4);}catch{}};
   BuildStyles(); var root=new Grid{Background=bg};root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(248)});root.ColumnDefinitions.Add(new ColumnDefinition());
   root.Children.Add(BuildOriginalNavigation());
   var main=new Grid{Margin=new Thickness(22,18,22,8)};main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(main,1);root.Children.Add(main);
   fixedPageHeader=new StackPanel();main.Children.Add(fixedPageHeader);content=new StackPanel();pageScroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(pageScroll,1);main.Children.Add(pageScroll);
   status=Label("就绪",11,muted);status.TextWrapping=TextWrapping.Wrap;status.MaxHeight=42;status.Margin=new Thickness(0,14,0,0);Grid.SetRow(status,2);main.Children.Add(status);Content=root;BeginOriginalPixelLayout();
   Closing+=async(s,e)=>{if(closing){e.Cancel=!closeReady;return;}e.Cancel=true;closing=true;if(monitorTimer!=null)monitorTimer.Stop();if(windowsBatteryTimer!=null)windowsBatteryTimer.Stop();if(pageReadTimer!=null)pageReadTimer.Stop();if(previewStateTimer!=null)previewStateTimer.Stop();status.Text="正在结束设备连接与宏…";try{await WaitMouseCalibrationWorkflow();await StopAllAuraTargetsAsync();await WaitDeviceHotplugAsync();await DisposeFanNativeAsync();await DisposeWindowsBrightnessAsync();await WaitWindowsToolsAsync();await DisposeNotebookStatusAsync();await DisposeMonitoringAsync();await StopMacroFeaturesAsync();await StopScenarioFeaturesAsync();gearWorker.Dispose();storageWorker.Dispose();DisposeOriginalWindowsAudio();DisposeOriginalDiscovery();foreach(var sdk in sdkWorkers.Values)sdk.Dispose();worker.Dispose();}finally{FinishWindowClose();}};
   BeginDeviceHotplugMonitoring();BeginMouseCalibrationInput();BeginMonitoring();BeginWindowsBatteryPolling();BeginNotebookStateExpiry();BeginPageReads();BeginPreviewSnapshots();ShowPage(ResolveUiStartupPage());if(!render&&!previewReload)Loaded+=async(s,e)=>{await RefreshInventory();if(closing)return;await ScanAura();};
  }
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  static Brush Brush(string hex){var brush=(Brush)new BrushConverter().ConvertFromString(hex);return brush.IsFrozen?brush.Clone():brush;}
  void BuildStyles(){
   var buttonStyle=new Style(typeof(Button));buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty,text));buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty,panel));buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty,border));buttonStyle.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));buttonStyle.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(16,10,16,10)));buttonStyle.Setters.Add(new Setter(Control.FontSizeProperty,13.0));buttonStyle.Setters.Add(new Setter(Control.CursorProperty,System.Windows.Input.Cursors.Hand));
   var tpl=new ControlTemplate(typeof(Button));var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.CornerRadiusProperty,new CornerRadius(0));frame.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});frame.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});frame.SetBinding(Border.BorderThicknessProperty,new System.Windows.Data.Binding("BorderThickness"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});frame.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});frame.AppendChild(presenter);tpl.VisualTree=frame;var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,0.82));tpl.Triggers.Add(hover);var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.4));tpl.Triggers.Add(disabled);buttonStyle.Setters.Add(new Setter(Control.TemplateProperty,tpl));Resources.Add(typeof(Button),buttonStyle);
  }
  TextBlock Label(string value,double size,Brush ink,FontWeight? weight=null){return new TextBlock{Text=value,FontSize=size,Foreground=ink,FontWeight=weight??FontWeights.Normal,Margin=new Thickness(0,0,0,8)};}
  TextBlock Paragraph(string value,double size,Brush ink){var l=Label(value,size,ink);l.TextWrapping=TextWrapping.Wrap;l.LineHeight=size*1.7;return l;}
  Button Button(string label,Action click,bool primary=false){var b=new Button{Content=label,Margin=new Thickness(0,0,8,0),Background=primary?accent:panel,Foreground=primary?Brush("#151515"):text,HorizontalContentAlignment=HorizontalAlignment.Center};b.Click+=(s,e)=>click();return b;}
  Border Card(UIElement child){return new Border{Background=panel,BorderBrush=border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(0),Padding=new Thickness(22),Margin=new Thickness(0,0,0,16),Child=child};}
  StackPanel Box(string heading,string desc){var p=new StackPanel();p.Children.Add(Label(heading,18,text,FontWeights.SemiBold));if(!String.IsNullOrEmpty(desc))p.Children.Add(Paragraph(desc,12,muted));return p;}
  StackPanel Row(){return new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};}
  void Header(string title,string subtitle){content.Children.Add(Label(title,28,text,FontWeights.SemiBold));if(!String.IsNullOrEmpty(subtitle))content.Children.Add(Paragraph(subtitle,13,muted));content.Children.Add(new Border{Height=12});}
  void PinPageHeader(int childCount,string layoutKey){
   if(childCount<0||childCount>content.Children.Count)throw new ArgumentOutOfRangeException("childCount");
   for(int index=0;index<childCount;index++){var child=content.Children[0];content.Children.RemoveAt(0);fixedPageHeader.Children.Add(child);}
   if(fixedPageHeaderKey!=layoutKey){pageScroll.ScrollToTop();fixedPageHeaderKey=layoutKey;}
  }
  void FinishWindowClose(){closeReady=true;Dispatcher.BeginInvoke(new Action(Close));}
  void ShowPage(string value){if(value=="设置")value="更新库";OnAuraAnimationNavigation(value);OnMouseCalibrationNavigation(value);OnScenarioNavigation(value);OnMacroNavigation(value);if(page!=value)pageScroll.ScrollToTop();page=value;RememberUiPage(value);QueuePageRead(value);liveMonitorLabels.Clear();content.Children.Clear();fixedPageHeader.Children.Clear();UpdateOriginalNavigation();switch(value){case "控制面板":case "总览":OriginalDashboard();break;case "系统监控":OriginalSystemMonitoringPage();break;case "设备":OriginalDeviceGallery();break;case "灯效 & Aura Sync":case "神光同步":OriginalAuraPage();break;case "更新库":OriginalSettingsPage();break;case "情境设置档":OriginalScenarioPage();break;case "宏":OriginalMacroPage();break;case "游戏库":OriginalGameLibraryPage();break;case "GameFirst":OriginalGameFirstPage();break;case "工具":OriginalToolsPage();break;case "设备兼容":Header("设备兼容","");OriginalDiscoveryGallery(content);break;case "驱动程序更新":OriginalAsusDriverUpdatePage();break;case "Windows 电源":OriginalWindowsPowerPage();break;case "屏幕设置":OriginalWindowsDisplayPage();break;case "电池信息":OriginalWindowsBatteryPage();break;case "笔记本与掌机":OriginalNotebookPage();break;case "Fan Xpert":OriginalFanXpertPage();break;case "device:motherboard":OriginalMotherboardPage();break;case "device:fan":OriginalControllerPage();break;case "device:receiver":OriginalReceiverPage();break;case "device:gpu":OriginalGpuLightingPage();break;case "device:headset":OriginalGearDevicePage("headset");break;case "device:mouse":OriginalGearDevicePage("mouse");break;case "device:keyboard":OriginalGearDevicePage("keyboard");break;default:if(value.StartsWith("device:discovered:",StringComparison.Ordinal))OriginalDiscoveredDevicePage(value.Substring("device:discovered:".Length));else if(value.StartsWith("device:",StringComparison.Ordinal))OriginalUnknownDevice(value.Substring(7));else OriginalPendingPage(value);break;}}
  static IEnumerable<string> Flatten(object value,string prefix,int depth){
   if(depth>5||value==null)yield break;
   var map=value as IDictionary<string,object>;
   if(map!=null){foreach(var entry in map){string key=entry.Key;if(key.IndexOf("serial",StringComparison.OrdinalIgnoreCase)>=0||key.Equals("sn",StringComparison.OrdinalIgnoreCase)||key.IndexOf("deviceSN",StringComparison.OrdinalIgnoreCase)>=0||key.IndexOf("token",StringComparison.OrdinalIgnoreCase)>=0)continue;foreach(var line in Flatten(entry.Value,prefix.Length==0?Friendly(key):prefix+" / "+Friendly(key),depth+1))yield return line;}yield break;}
   var items=value as IEnumerable;if(items!=null&&!(value is string)){int n=0;foreach(var item in items){foreach(var line in Flatten(item,prefix+" "+(++n),depth+1))yield return line;}yield break;}
   yield return prefix+"："+Convert.ToString(value,CultureInfo.InvariantCulture);
  }
  static string Friendly(string key){switch(key){case "deviceId":return "型号编号";case "name":return "名称";case "deviceInfo":return "设备信息";case "performance":return "性能配置";case "power":return "电源配置";case "readbackConfirmed":return "读回已验证";case "errors":return "读取错误";case "ports":return "风扇端口";case "rpm":return "转速 RPM";case "confirmed":return "有效读数";case "error":return "错误";case "temperatures":return "温度";case "portId":return "端口编号";default:return key;}}
  WorkerClient Sdk(string kind){WorkerClient client;if(!sdkWorkers.TryGetValue(kind,out client)){client=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SDKWorker.exe"),kind=="lcd"?25000:15000,kind);sdkWorkers[kind]=client;}return client;}
  static string SdkError(object error){var map=error as IDictionary<string,object>;object message;if(map!=null&&map.TryGetValue("message",out message))return Convert.ToString(message);return Convert.ToString(error)??"设备接口未返回错误详情。";}
  async Task ReadOriginalSnapshot(string kind,string id){
   if(busy||AuraSyncBusy||closing||render)return;busy=true;status.Text="正在读取设备状态…";
   try{long generation=DeviceHotplugGeneration;var client=Sdk(kind);var result=await client.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",id}});if(!result.ok)throw new InvalidOperationException(SdkError(result.error));object confirmed;if(result.data==null||!result.data.TryGetValue("readbackConfirmed",out confirmed)||!Object.Equals(confirmed,true)||OriginalStateText(result.data,"deviceId","")!=id)throw new InvalidOperationException("接口未返回已确认的原设备数据。");RememberSdkRead(kind,id,result.data,client,generation);Log("设备状态已读回 · "+DateTime.Now.ToString("HH:mm:ss")+" · 显示本次设备快照。");}
   catch(Exception e){ForgetSdkRead(kind,id);Log("读取失败："+e.Message);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  async Task ScanPeripheral(string kind){if(busy||AuraSyncBusy||closing||render)return;busy=true;status.Text="正在读取 "+kind+" SDK…";try{foreach(string key in sdkReadBindings.Keys.Where(key=>key.StartsWith(kind+":",StringComparison.Ordinal)).ToArray())sdkReadBindings.Remove(key);long generation=DeviceHotplugGeneration;var result=await Sdk(kind).Request<SdkReply>(new Dictionary<string,object>{{"op","scan"},{"kind",kind}});if(!result.ok)throw new InvalidOperationException(SdkError(result.error));if(closing||generation!=DeviceHotplugGeneration)throw new OperationCanceledException("设备连接已改变，请重新读取。");object list;var json=new JavaScriptSerializer();peripherals[kind]=result.data!=null&&result.data.TryGetValue("devices",out list)?json.Deserialize<List<Peripheral>>(json.Serialize(list)):new List<Peripheral>();peripheralMessage="读取完成 · "+kind+" · "+peripherals[kind].Count+" 个设备。设置写入尚未接通。";Log(peripheralMessage);}catch(Exception e){peripheralMessage=kind+" 读取失败："+e.Message;Log(peripheralMessage);}finally{busy=false;if(!closing)ShowPage(page);}}
  async Task ReadPeripheral(string kind,string id){if(busy||AuraSyncBusy||closing||render)return;busy=true;status.Text="正在读取设备信息…";try{long generation=DeviceHotplugGeneration;var client=Sdk(kind);var result=await client.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",id}});if(!result.ok)throw new InvalidOperationException(SdkError(result.error));if(result.data==null||OriginalStateText(result.data,"deviceId","")!=id)throw new InvalidOperationException("设备读取目标不匹配。");RememberSdkRead(kind,id,result.data,client,generation);peripheralMessage="已返回设备读取结果。请查看各字段是否读回成功。";Log(peripheralMessage);}catch(Exception e){ForgetSdkRead(kind,id);peripheralMessage="设备读取失败："+e.Message;Log(peripheralMessage);}finally{busy=false;if(!closing)ShowPage(page);}}
  void Log(string value){history.Add(DateTime.Now.ToString("HH:mm:ss")+" · "+value);if(history.Count>200)history.RemoveAt(0);status.Text=value.StartsWith("原生 USB 已识别",StringComparison.Ordinal)||value.StartsWith("本机状态已刷新",StringComparison.Ordinal)||value.StartsWith("设备状态已读回",StringComparison.Ordinal)?"":value;}
  async Task RefreshInventory(){status.Text="正在读取本机状态…";try{info=await Task.Run(()=>Inventory.Scan());Log("本机状态已刷新；没有更改硬件设置。");}catch(Exception e){Log("状态读取失败："+e.Message);}ShowPage(page);}
  Task ScanAura(){return ScanAllAuraTargetsAsync();}
  Task ApplyAura(){return ApplyAllAuraTargetsAsync();}
  Task ReleaseAura(){return StopAllAuraTargetsAsync();}
  void SaveProfile(){try{Directory.CreateDirectory(dataDir);var dialog=new SaveFileDialog{Filter="灯效配置 (*.json)|*.json",InitialDirectory=dataDir,FileName="灯效配置.json"};if(dialog.ShowDialog(this)!=true)return;var profile=new Profile{Name=Path.GetFileNameWithoutExtension(dialog.FileName),Effect=effect,Color=color,Brightness=brightness,Speed=speed,Devices=selected.ToArray()};File.WriteAllText(dialog.FileName,new JavaScriptSerializer().Serialize(profile),new UTF8Encoding(false));Log("灯效配置已保存。配置不会自动应用。");}catch(Exception e){MessageBox.Show(this,e.Message,"保存失败");}}
  void LoadProfile(){try{Directory.CreateDirectory(dataDir);var dialog=new OpenFileDialog{Filter="灯效配置 (*.json)|*.json",InitialDirectory=dataDir};if(dialog.ShowDialog(this)!=true)return;if(new FileInfo(dialog.FileName).Length>1024*1024)throw new InvalidOperationException("配置文件过大。");var p=new JavaScriptSerializer().Deserialize<Profile>(File.ReadAllText(dialog.FileName));if(p==null||!new[]{"static","breathe","cycle","off"}.Contains(p.Effect)||!System.Text.RegularExpressions.Regex.IsMatch(p.Color??"","^#[0-9a-fA-F]{6}$")||Double.IsNaN(p.Speed)||Double.IsInfinity(p.Speed))throw new InvalidOperationException("配置格式无效。");effect=p.Effect;color=p.Color;brightness=Math.Max(0,Math.Min(100,p.Brightness));speed=Math.Max(1,Math.Min(10,p.Speed));selected.Clear();foreach(string id in p.Devices??new string[0])if(devices.Any(d=>d.id==id))selected.Add(id);Log("配置已载入；点击应用后才会改变灯效。");ShowPage(page);}catch(Exception e){MessageBox.Show(this,e.Message,"载入失败");}}
  void ExportReport(){
   try{
    var dialog=new SaveFileDialog{Filter="诊断报告 (*.txt)|*.txt",FileName="ROG-Control-诊断.txt"};if(dialog.ShowDialog(this)!=true)return;
    var report=new StringBuilder();report.AppendLine("OpenAC 研究版 0.3 诊断报告");report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
    report.AppendLine("主板："+info.Board);report.AppendLine("CPU："+info.Cpu);report.AppendLine("内存："+info.Memory);
    report.AppendLine("华硕进程工作集求和（含共享页）："+info.VendorMiB.ToString("F0")+" MB");report.AppendLine("Aura 状态："+auraMessage);
    foreach(var d in devices)report.AppendLine("Aura 设备："+d.name+" / "+d.zoneCount+" 个逻辑通道 / 写入未实机验证");
    foreach(var s in info.Services)report.AppendLine("服务："+s.Name+" / "+s.Status);foreach(var c in info.Components)report.AppendLine("组件："+c.Name+" / "+c.Version);foreach(var f in info.Findings)report.AppendLine("线索："+f);
    foreach(var snapshot in sdkSnapshots)AppendReportSnapshot(report,"设备读取快照："+snapshot.Key,snapshot.Value);
    foreach(var snapshot in gearStates)AppendReportSnapshot(report,"外设读取快照："+snapshot.Key,snapshot.Value);
    foreach(var endpoint in audioEndpoints)AppendReportSnapshot(report,"Windows 音频读取快照",endpoint);
    foreach(var catalog in firmwareCatalogs)AppendReportSnapshot(report,"官网固件工具目录："+catalog.Key,catalog.Value);
    report.AppendLine("USB/HID 身份扫描："+discoveryMessage);
    foreach(var discovered in discoveredHardware)report.AppendLine("扫描时存在："+DiscoveryText(discovered,"name","未读取名称")+" / VID "+DiscoveryText(discovered,"vid")+" / PID "+DiscoveryText(discovered,"pid","未确认")+" / "+DiscoveryTransport(discovered)+" / 只识别身份，此快照不确认控制功能");
    foreach(string line in history.Take(60))report.AppendLine("会话："+line);
    report.AppendLine("未完成：完整神光同步、主板 Fan Xpert、音效增强、OLED 上传、硬件宏、官方组件更新、固件刷写和部分主板功能；键鼠离线尚待实机验证。");
    File.WriteAllText(dialog.FileName,report.ToString(),new UTF8Encoding(true));Log("诊断报告已导出。");
   }catch(Exception e){MessageBox.Show(this,e.Message,"导出失败");}
  }
  static void AppendReportSnapshot(StringBuilder report,string label,IDictionary<string,object> snapshot){report.AppendLine(label);foreach(string line in Flatten(snapshot,"",0))report.AppendLine(line);}
  async Task RestartLighting(){
   if(busy)return;
   if(MessageBox.Show(this,"将重启 LightingService，灯效会短暂中断。风扇服务不受本操作控制。\nWindows 随后会请求管理员授权。","重启 AURA 服务",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
   if(controlling)await ReleaseAura();worker.Kill();
   busy=true;status.Text="等待管理员授权及服务重启…";
   try{var p=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--restart-lighting"){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});await Task.Run(()=>p.WaitForExit());int code=p.ExitCode;p.Dispose();if(code!=0)throw new InvalidOperationException("重启失败。详情见 data/service-recovery.txt（退出码 "+code+"）。");controlling=false;Log("AURA 服务已重启。请重新扫描，并观察同步是否恢复。");}catch(Exception e){Log("AURA 服务重启未完成："+e.Message);}finally{busy=false;}
   await RefreshInventory();await ScanAura();
  }
  public void Render(string path,string screen="控制面板"){info=Inventory.Scan();ShowPage(screen);var frame=(FrameworkElement)Content;frame.Measure(new Size(1538,826));frame.Arrange(new Rect(0,0,1538,826));frame.UpdateLayout();var image=new RenderTargetBitmap(1538,826,96,96,PixelFormats.Pbgra32);image.Render(frame);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))encoder.Save(stream);}
 }
 static class Program {
  [STAThread]static int Main(string[] args){
   if(args.Length>0&&args[0]=="--restart-lighting")return RestartService();
   if(args.Length>0&&args[0]=="--inventory"){try{File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(Inventory.Scan()),new UTF8Encoding(false));return 0;}catch{return 1;}}
   var app=new Application();app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"OpenAC");e.Handled=true;};
   if(args.Length>1&&args[0]=="--render"){try{var window=new MainWindow(true);window.Render(args[1],args.Length>2?args[2]:"控制面板");return 0;}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"render-error.txt"),e.ToString());return 1;}}
   bool owns;using(var instance=new Mutex(true,"Local\\ROGControl-0.1",out owns)){if(!owns){MessageBox.Show("OpenAC 已在运行，请切换到已有窗口。","OpenAC");return 0;}app.Run(new MainWindow(false));return 0;}
  }
  static int RestartService(){string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");try{Directory.CreateDirectory(folder);using(var svc=new ServiceController("LightingService")){if(svc.DependentServices.Any(s=>s.Status==ServiceControllerStatus.Running))throw new InvalidOperationException("存在运行中的依赖服务；未执行重启。");if(svc.Status==ServiceControllerStatus.StopPending)svc.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));if(svc.Status!=ServiceControllerStatus.Stopped){svc.Stop();svc.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));}svc.Start();svc.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20));}File.WriteAllText(Path.Combine(folder,"service-recovery.txt"),DateTime.Now.ToString("s")+" LightingService 已重启。",Encoding.UTF8);return 0;}catch(Exception e){try{Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"service-recovery.txt"),DateTime.Now.ToString("s")+" "+e.Message,Encoding.UTF8);}catch{}return 1;}}
 }
}
