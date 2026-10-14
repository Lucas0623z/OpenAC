// SPDX-License-Identifier: MIT
// Independent read-only GUI boundary for the supervised NotebookWorker.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RogControlNotebook;

namespace RogControl {
 public sealed class NotebookGuiReply {
  public int id {get;set;}
  public bool ok {get;set;}
  public object error {get;set;}
  public Dictionary<string,object> data {get;set;}
  public bool restartRequired {get;set;}
 }
 public interface INotebookGuiBackend:IDisposable {
  bool RestartRequired {get;}
  Task<NotebookGuiReply> Request(Dictionary<string,object> request);
  void Quarantine();
 }
 public sealed partial class NotebookWorkerGuiBackend:INotebookGuiBackend,INotebookGuiControlBackend {
  const string QuarantineKey="OpenAC.Notebook.StatusQuarantined.v1";
  const string RequestGateKey="OpenAC.Notebook.GuiRequestGate.v1";
  readonly WorkerClient client;
  volatile bool disposed;
  public NotebookWorkerGuiBackend(){
   string root=AppDomain.CurrentDomain.GetData("OpenAC.PreviewWorkerRoot") as string;
   client=new WorkerClient(Path.Combine(String.IsNullOrEmpty(root)?AppDomain.CurrentDomain.BaseDirectory:root,"NotebookWorker.exe"),10000);
  }
  public bool RestartRequired {get{return Object.Equals(AppDomain.CurrentDomain.GetData(QuarantineKey),true);}}
  static SemaphoreSlim SharedRequestGate(){var domain=AppDomain.CurrentDomain;lock(domain){var existing=domain.GetData(RequestGateKey);var gate=existing as SemaphoreSlim;if(existing!=null&&gate==null)throw new NotebookGuiFailure("INTERFACE_BUSY",false);if(gate==null){gate=new SemaphoreSlim(1,1);domain.SetData(RequestGateKey,gate);}return gate;}}
  public async Task<NotebookGuiReply> Request(Dictionary<string,object> request){
   object rawOp;string op=request!=null&&request.TryGetValue("op",out rawOp)?rawOp as string:null;
   if(op!="scan"&&op!="read")throw new NotebookGuiFailure("READ_ONLY",false);
   if(request.Keys.Any(key=>key!="op"&&!(op=="read"&&key=="features")))throw new NotebookGuiFailure("INVALID_REQUEST",false);
   if(disposed)throw new ObjectDisposedException("NotebookWorker");if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);var gate=SharedRequestGate();
   if(!await gate.WaitAsync(0).ConfigureAwait(false))throw new NotebookGuiFailure("INTERFACE_BUSY",false);
   try{if(disposed)throw new ObjectDisposedException("NotebookWorker");if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);return await client.Request<NotebookGuiReply>(request).ConfigureAwait(false);}finally{gate.Release();}
  }
  public void Quarantine(){AppDomain.CurrentDomain.SetData(QuarantineKey,true);client.Dispose();disposed=true;}
  public void Dispose(){disposed=true;client.Dispose();}
 }
 public sealed class NotebookGuiFailure:Exception {
  public readonly string Code;
  public readonly bool RestartRequired;
  public NotebookGuiFailure(string code,bool restart):base(code){Code=code;RestartRequired=restart;}
 }
 public sealed class NotebookGuiReading {
  public string Feature {get;internal set;}
  public string Caption {get;internal set;}
  public string Text {get;internal set;}
  public bool Confirmed {get;internal set;}
 }
 public sealed class NotebookGuiSnapshot {
  public string Manufacturer {get;internal set;}
  public string Model {get;internal set;}
  public string ModelCode {get;internal set;}
  public string Family {get;internal set;}
  public bool IdentityConfirmed {get;internal set;}
  public bool EligibleForStatus {get;internal set;}
  public bool DriverQueryPerformed {get;internal set;}
  public string SupportReason {get;internal set;}
  public DateTime CapturedUtc {get;internal set;}
  public IList<NotebookGuiReading> Readings {get;internal set;}
  public NotebookGuiControlCapture Controls {get;internal set;}
 }
 public sealed partial class NotebookGuiReader:IDisposable {
  readonly INotebookGuiBackend backend;
  readonly int deadlineMs;
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  volatile bool disposed,quarantined;
  public NotebookGuiReader(INotebookGuiBackend value,int outerDeadlineMs=10000){if(value==null)throw new ArgumentNullException("value");if(outerDeadlineMs<1||outerDeadlineMs>10000)throw new ArgumentOutOfRangeException("outerDeadlineMs");backend=value;deadlineMs=outerDeadlineMs;}
  public bool RestartRequired {get{return quarantined||backend.RestartRequired;}}
  static object Value(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  static bool Flag(IDictionary<string,object> map,string key,bool expected){return Value(map,key) is bool&&(bool)Value(map,key)==expected;}
  static string Text(IDictionary<string,object> map,string key){string value=Value(map,key) as string;return value==null?"":new string(value.Where(c=>!Char.IsControl(c)).Take(256).ToArray()).Trim();}
  static object[] Items(IDictionary<string,object> map,string key){var value=Value(map,key) as IEnumerable;return value==null||value is string?null:value.Cast<object>().Take(129).ToArray();}
  static int? Integer(object raw){if(raw==null||raw is bool||raw is string||raw is float||raw is double)return null;if(!(raw is byte||raw is sbyte||raw is short||raw is ushort||raw is int||raw is uint||raw is long||raw is ulong||raw is decimal))return null;try{decimal value=Convert.ToDecimal(raw,CultureInfo.InvariantCulture);return value>=Int32.MinValue&&value<=Int32.MaxValue&&Decimal.Truncate(value)==value?(int?)value:null;}catch{return null;}}
  static bool Prepared(IDictionary<string,object> data){
   if(!Flag(data,"eligibleForStatus",true)||!Flag(data,"identityReadConfirmed",true)||!Flag(data,"sourcePrepared",true)||!Flag(data,"interfaceDriverConfirmed",true))return false;
   string manufacturer=Regex.Replace(Text(data,"manufacturer").ToUpperInvariant(),"[^A-Z0-9]","");if(manufacturer!="ASUS"&&manufacturer!="ASUSTEKCOMPUTERINC")return false;
   string[] codes={"RC71L","RC72LA","RC73YA","RC73XA","GA402RJ","GA403UI","GU605MI","GU605MZ","FX507ZC4","FA507NV"};string code=Text(data,"modelCode"),model=Text(data,"model"),family=Text(data,"family");
   if(!codes.Contains(code,StringComparer.Ordinal)||!Regex.IsMatch(model,"(?<![A-Z0-9])"+Regex.Escape(code)+"(?![A-Z0-9])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))return false;
   if(codes.Count(c=>Regex.IsMatch(model,"(?<![A-Z0-9])"+Regex.Escape(c)+"(?![A-Z0-9])",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))!=1)return false;
   if(family!=NotebookControlRules.Family(code))return false;
   var chassis=Items(data,"chassisTypes");return chassis!=null&&chassis.Length>0&&chassis.Length<=32&&chassis.All(v=>{int? n=Integer(v);return n.HasValue&&new[]{8,9,10,11,14,30,31,32}.Contains(n.Value);});
  }
  static readonly string[] BasicFeatures={"cpuFan","gpuFan","cpuTemperature","gpuTemperature","chargerMode"};
  static string[] Features(string family){return family=="handheld"?BasicFeatures.ToArray():BasicFeatures.Concat(new[]{"middleFan","keyboardBrightness"}).ToArray();}
  static string Caption(string feature){switch(feature){case "cpuFan":return "CPU 风扇";case "gpuFan":return "GPU 风扇";case "middleFan":return "第三风扇";case "cpuTemperature":return "CPU 温度";case "gpuTemperature":return "GPU 温度";case "keyboardBrightness":return "键盘亮度";default:return "电源适配器";}}
  static NotebookGuiReading Reading(string feature,IDictionary<string,object> state){
   var result=new NotebookGuiReading{Feature=feature,Caption=Caption(feature),Text="—"};
   if(state==null||!Flag(state,"presenceConfirmed",true)||!Flag(state,"readbackConfirmed",true)||!Flag(state,"valueReadbackConfirmed",true)||!Flag(state,"writable",false)||Value(state,"error")!=null)return result;
   int? number=Integer(Value(state,"value"));if(!number.HasValue)return result;int value=number.Value;string unit=Text(state,"unit"),meaning=Text(state,"valueSemantics");
   if(feature.EndsWith("Fan",StringComparison.Ordinal)){if(unit!="rpm"||meaning!="fanSpeed"||value<0||value>12000||value%100!=0)return result;result.Text=value.ToString(CultureInfo.InvariantCulture)+" RPM";}
   else if(feature.EndsWith("Temperature",StringComparison.Ordinal)){if(unit!="C"||meaning!="temperature"||value<0||value>150)return result;result.Text=value.ToString(CultureInfo.InvariantCulture)+" °C";}
   else if(feature=="keyboardBrightness"){if(unit!="level"||meaning!="brightness"||value<0||value>3)return result;result.Text=value.ToString(CultureInfo.InvariantCulture)+" / 3";}
   else{if(unit!="code"||meaning!="chargerType"||(value!=1&&value!=2))return result;result.Text=value==1?"圆口电源":"USB 电源";}
   result.Confirmed=true;return result;
  }
  static NotebookGuiSnapshot Parse(Dictionary<string,object> data,bool scan){
   string[] flags={"identityReadConfirmed","interfaceDriverConfirmed","eligibleForStatus","sourcePrepared","driverQueryPerformed","supportsWrite","supportsFanCurve","supportsPowerLimits","supportsLedWrites","hardwareReadValidated"};
   if(data==null||flags.Any(key=>!(Value(data,key) is bool))||!Flag(data,"supportsWrite",false)||!Flag(data,"supportsFanCurve",false)||!Flag(data,"supportsPowerLimits",false)||!Flag(data,"supportsLedWrites",false)||Text(data,"identityScope")!="osReportedManufacturerModelChassis")throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   var states=Items(data,"states");if(states==null||states.Length>12||states.Any(s=>!(s is Dictionary<string,object>))||Flag(data,"driverQueryPerformed",false)&&states.Length!=0||!scan&&Flag(data,"eligibleForStatus",true)&&!Flag(data,"driverQueryPerformed",true)||Flag(data,"eligibleForStatus",false)&&!Flag(data,"driverQueryPerformed",false))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   if(scan&&(states.Length!=0||!Flag(data,"driverQueryPerformed",false)))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   bool prepared=Prepared(data);var snapshot=new NotebookGuiSnapshot{Manufacturer=Text(data,"manufacturer"),Model=Text(data,"model"),ModelCode=Text(data,"modelCode"),Family=Text(data,"family"),IdentityConfirmed=Flag(data,"identityReadConfirmed",true),EligibleForStatus=prepared,DriverQueryPerformed=!scan&&prepared&&Flag(data,"driverQueryPerformed",true),SupportReason=Text(data,"supportReason"),CapturedUtc=DateTime.UtcNow,Readings=new List<NotebookGuiReading>()};
   if(scan||!snapshot.DriverQueryPerformed)return snapshot;
   foreach(string feature in Features(snapshot.Family)){var matching=states.OfType<Dictionary<string,object>>().Where(s=>Text(s,"feature")==feature).ToArray();snapshot.Readings.Add(Reading(feature,matching.Length==1?matching[0]:null));}
   return snapshot;
  }
  static string ErrorCode(NotebookGuiReply reply){return Text(reply.error as IDictionary<string,object>,"code");}
  void Quarantine(){quarantined=true;try{backend.Quarantine();}catch{try{backend.Dispose();}catch{}}}
  void Abandon(Task task){task.ContinueWith(t=>{if(t.IsFaulted){var observed=t.Exception;}try{backend.Dispose();}catch{}},TaskScheduler.Default);}
  async Task<NotebookGuiReply> Request(Dictionary<string,object> request,CancellationToken cancel){
   cancel.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException("NotebookGuiReader");if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);
   // Process startup, JSON writes and reply waits all stay off the WPF thread.
   var operation=Task.Run(()=>backend.Request(request));var deadline=Task.Delay(deadlineMs,cancel);
   var winner=await Task.WhenAny(operation,deadline).ConfigureAwait(false);
   if(winner!=operation){Abandon(operation);if(cancel.IsCancellationRequested){try{backend.Dispose();}catch{}cancel.ThrowIfCancellationRequested();}Quarantine();throw new NotebookGuiFailure("QUERY_TIMEOUT",true);}
   NotebookGuiReply reply;
   try{reply=await operation.ConfigureAwait(false);}catch(TimeoutException){Quarantine();throw new NotebookGuiFailure("QUERY_TIMEOUT",true);}catch(NotebookGuiFailure){throw;}catch(OperationCanceledException){throw;}catch{throw new NotebookGuiFailure("CONNECTION_FAILED",false);}
   cancel.ThrowIfCancellationRequested();if(reply==null)throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   string error=ErrorCode(reply);if(reply.restartRequired||error=="QUERY_TIMEOUT"||error=="RESTART_REQUIRED"){Quarantine();throw new NotebookGuiFailure(error=="RESTART_REQUIRED"?error:"QUERY_TIMEOUT",true);}if(!reply.ok)throw new NotebookGuiFailure(error.Length==0?"QUERY_FAILED":error,false);return reply;
  }
  public async Task<NotebookGuiSnapshot> ReadAsync(CancellationToken cancel){
   if(!await gate.WaitAsync(0,cancel).ConfigureAwait(false))throw new NotebookGuiFailure("INTERFACE_BUSY",false);
   try{var scan=Parse((await Request(new Dictionary<string,object>{{"op","scan"}},cancel).ConfigureAwait(false)).data,true);if(!scan.EligibleForStatus)return scan;
    var read=Parse((await Request(new Dictionary<string,object>{{"op","read"},{"features",Features(scan.Family)}},cancel).ConfigureAwait(false)).data,false);
    if(read.Manufacturer!=scan.Manufacturer||read.Model!=scan.Model||read.ModelCode!=scan.ModelCode||read.Family!=scan.Family)throw new NotebookGuiFailure("IDENTITY_CHANGED",false);if(read.EligibleForStatus)await ReadControls(read,cancel).ConfigureAwait(false);return read;
   }finally{gate.Release();}
  }
  public void Dispose(){disposed=true;backend.Dispose();}
 }
 public partial class MainWindow {
  NotebookGuiReader notebookReader=new NotebookGuiReader(new NotebookWorkerGuiBackend());
  readonly CancellationTokenSource notebookCancellation=new CancellationTokenSource();
  NotebookGuiSnapshot notebookSnapshot;
  Task<NotebookGuiSnapshot> notebookReadTask;
  DispatcherTimer notebookStateExpiryTimer;
  bool notebookWaitingForRead;
  string notebookMessage="";
  public bool NotebookReadBusy {get{return notebookReadTask!=null&&!notebookReadTask.IsCompleted||notebookControlTask!=null&&!notebookControlTask.IsCompleted;}}
  public Task NotebookReadTask {get{return notebookControlTask??(Task)notebookReadTask;}}
  public bool NotebookCanPreviewReload {get{return !NotebookReadBusy;}}
  public bool NotebookRestartRequired {get{return notebookReader.RestartRequired;}}
  static bool NotebookCurrent(DateTime capturedUtc,DateTime nowUtc){var age=nowUtc-capturedUtc;return age<TimeSpan.FromSeconds(30)&&age>=TimeSpan.FromSeconds(-5);}
  bool ExpireNotebookState(DateTime nowUtc){
   if(notebookSnapshot==null||!notebookSnapshot.EligibleForStatus||notebookWaitingForRead||NotebookCurrent(notebookSnapshot.CapturedUtc,nowUtc))return false;
   notebookSnapshot.Readings=notebookSnapshot.Readings.Select(r=>new NotebookGuiReading{Feature=r.Feature,Caption=r.Caption,Text="—",Confirmed=false}).ToList();notebookSnapshot.Controls=null;notebookWaitingForRead=true;return true;
  }
  void BeginNotebookStateExpiry(){if(render)return;notebookStateExpiryTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};notebookStateExpiryTimer.Tick+=(s,e)=>{if(closing){notebookStateExpiryTimer.Stop();return;}if(IsLoaded&&page=="笔记本与掌机")NotebookStateExpiryTick(DateTime.UtcNow);};notebookStateExpiryTimer.Start();}
  void NotebookStateExpiryTick(DateTime nowUtc){if(!closing&&ExpireNotebookState(nowUtc)&&page=="笔记本与掌机")ShowPage(page);}
  void OriginalNotebookToolEntry(){var entry=Button("笔记本与掌机",()=>ShowPage("笔记本与掌机"));entry.HorizontalAlignment=HorizontalAlignment.Left;entry.MinWidth=180;content.Children.Add(entry);}
  void OriginalNotebookPage(){
   ExpireNotebookState(DateTime.UtcNow);
   Header("笔记本与掌机","");var commands=new WrapPanel{Margin=new Thickness(0,0,0,18)};
   var read=Button(NotebookReadBusy?"正在读取…":"读取",async()=>await ReadNotebook());read.IsEnabled=!render&&!closing&&!busy&&!pageReadRunning&&!NotebookReadBusy&&!NotebookRestartRequired;commands.Children.Add(read);commands.Children.Add(Button("返回工具",()=>ShowPage("工具")));content.Children.Add(commands);
   if(!String.IsNullOrEmpty(notebookMessage))content.Children.Add(Paragraph(notebookMessage,13,muted));else if(NotebookRestartRequired)content.Children.Add(Paragraph("读取超时，请重新打开应用。",13,muted));
   if(notebookSnapshot==null){if(String.IsNullOrEmpty(notebookMessage)&&!NotebookRestartRequired)content.Children.Add(Paragraph("等待读取",13,muted));return;}
   var identity=Box("设备信息","");NotebookMetric(identity,"制造商",notebookSnapshot.Manufacturer.Length==0?"—":notebookSnapshot.Manufacturer);NotebookMetric(identity,"型号",notebookSnapshot.Model.Length==0?"—":notebookSnapshot.Model);content.Children.Add(Card(identity));
   if(!notebookSnapshot.EligibleForStatus){content.Children.Add(Paragraph(notebookSnapshot.IdentityConfirmed?"仅识别 · 暂不支持读取":"设备身份未确认",13,muted));return;}
   bool current=!notebookWaitingForRead&&NotebookCurrent(notebookSnapshot.CapturedUtc,DateTime.UtcNow);
   var values=Box("状态","");if(!current||notebookSnapshot.Readings.Count==0){var pending=Paragraph("等待读取",13,muted);pending.ToolTip=notebookSnapshot.CapturedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);values.Children.Add(pending);}foreach(var value in notebookSnapshot.Readings)NotebookMetric(values,value.Caption,current&&value.Confirmed?value.Text:"—");content.Children.Add(Card(values));AddNotebookControls(current);
  }
  void NotebookMetric(StackPanel parent,string caption,string value){var row=new Grid{Margin=new Thickness(0,7,0,7)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(Label(caption,13,muted));var detail=Paragraph(value,14,text);Grid.SetColumn(detail,1);row.Children.Add(detail);parent.Children.Add(row);}
  static string NotebookFailureText(Exception error){var typed=error as NotebookGuiFailure;string code=typed==null?"QUERY_FAILED":typed.Code;switch(code){case "QUERY_TIMEOUT":case "RESTART_REQUIRED":return "读取超时，请重新打开应用。";case "IDENTITY_CHANGED":return "设备已改变，请重新读取。";case "INTERFACE_BUSY":return "设备正在使用，请稍后读取。";case "CONNECTION_FAILED":case "INTERFACE_UNAVAILABLE":case "INTERFACE_QUERY_FAILED":return "设备连接失败，请重新读取。";case "INVALID_RESPONSE":return "设备响应未确认，请重新读取。";default:return "读取失败，请重新读取。";}}
  void SetNotebookRead(NotebookGuiSnapshot snapshot){notebookSnapshot=snapshot;notebookWaitingForRead=false;notebookMessage="";}
  void NotebookReadFailed(Exception error){notebookSnapshot=null;notebookMessage=NotebookFailureText(error);}
  async Task ReadNotebook(){
   if(render||closing||busy||pageReadRunning||NotebookReadBusy||NotebookRestartRequired)return;busy=true;
   try{notebookReadTask=notebookReader.ReadAsync(notebookCancellation.Token);if(page=="笔记本与掌机")ShowPage(page);var result=await notebookReadTask;if(!closing)SetNotebookRead(result);}catch(Exception error){if(!closing)NotebookReadFailed(error);}finally{notebookReadTask=null;busy=false;if(!closing&&page=="笔记本与掌机")ShowPage(page);}
  }
  public async Task DisposeNotebookStatusAsync(){if(notebookStateExpiryTimer!=null)notebookStateExpiryTimer.Stop();notebookCancellation.Cancel();var pending=NotebookReadTask;await Task.Run(()=>notebookReader.Dispose()).ConfigureAwait(false);if(pending!=null)try{await pending.ConfigureAwait(false);}catch{}}
 }
}
