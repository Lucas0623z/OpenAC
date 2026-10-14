// SPDX-License-Identifier: MIT
// Manual, session-bound SCI controls. Selection never sends a setting.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using RogControlNotebook;

namespace RogControl {
 public interface INotebookGuiControlBackend:INotebookGuiBackend {
  object ControlSessionIdentity {get;}
  Task<NotebookGuiReply> RequestControl(Dictionary<string,object> request,object expectedSession,Action beforeSend);
 }
 public sealed partial class NotebookWorkerGuiBackend {
  public object ControlSessionIdentity {get{return disposed?null:client.SessionIdentity;}}
  public async Task<NotebookGuiReply> RequestControl(Dictionary<string,object> request,object expectedSession,Action beforeSend){
   if(request==null)throw new NotebookGuiFailure("INVALID_REQUEST",false);
   object raw;string op=request!=null&&request.TryGetValue("op",out raw)?raw as string:null;
   if(op!="controlRead"&&op!="set")throw new NotebookGuiFailure("INVALID_REQUEST",false);
   string[] fields=op=="controlRead"?new[]{"op"}:new[]{"op","setting","value","experimental","modelCode","identityToken","sessionToken","readToken","acceptDisplayDisruption","acceptReboot"};
   if(request.Keys.Any(k=>!fields.Contains(k)))throw new NotebookGuiFailure("INVALID_REQUEST",false);
   if(op=="set"&&(!request.TryGetValue("experimental",out raw)||!Object.Equals(raw,true)))throw new NotebookGuiFailure("CONTROL_DISABLED",false);
   if(op=="set"&&beforeSend==null)throw new NotebookGuiFailure("CONTROL_DISABLED",false);
   if(expectedSession==null||!Object.ReferenceEquals(expectedSession,ControlSessionIdentity))throw new NotebookGuiFailure("IDENTITY_CHANGED",false);
   if(disposed)throw new ObjectDisposedException("NotebookWorker");if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);var guard=SharedRequestGate();
   if(!await guard.WaitAsync(0).ConfigureAwait(false))throw new NotebookGuiFailure("INTERFACE_BUSY",false);
   try{if(disposed||!Object.ReferenceEquals(expectedSession,ControlSessionIdentity))throw new NotebookGuiFailure("IDENTITY_CHANGED",false);if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);return await client.Request<NotebookGuiReply>(request,expectedSession,10000,beforeSend).ConfigureAwait(false);}finally{guard.Release();}
  }
 }
 public sealed class NotebookGuiControlOption {
  public string Feature {get;internal set;} public string Caption {get;internal set;} public IList<int> AllowedValues {get;internal set;} public int? CurrentValue {get;internal set;} public bool ValueConfirmed {get;internal set;} public bool Supported {get;internal set;}
 }
 public sealed class NotebookGuiControlCapture {
  public string ModelCode {get;internal set;} public string IdentityToken {get;internal set;} public string SessionToken {get;internal set;} public string ReadToken {get;internal set;}
  public IList<NotebookGuiControlOption> Options {get;internal set;} public DateTime CapturedUtc {get;internal set;}
  internal object WorkerSession;internal bool Consumed;
 }
 public sealed class NotebookGuiControlOutcome {public bool WriteAttempted,WriteConfirmed,NeedsRestart;public string AppliedState;}
 public sealed partial class NotebookGuiReader {
  async Task<NotebookGuiReply> ControlRequest(Dictionary<string,object> request,object session,CancellationToken cancel,Action beforeSend=null){
   cancel.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException("NotebookGuiReader");if(RestartRequired)throw new NotebookGuiFailure("RESTART_REQUIRED",true);
   var provider=backend as INotebookGuiControlBackend;if(provider==null||session==null||!Object.ReferenceEquals(session,provider.ControlSessionIdentity))throw new NotebookGuiFailure("IDENTITY_CHANGED",false);
   var operation=Task.Run(()=>provider.RequestControl(request,session,beforeSend));var deadline=Task.Delay(deadlineMs,cancel);var winner=await Task.WhenAny(operation,deadline).ConfigureAwait(false);
   if(winner!=operation){Abandon(operation);if(cancel.IsCancellationRequested){try{backend.Dispose();}catch{}cancel.ThrowIfCancellationRequested();}Quarantine();throw new NotebookGuiFailure("QUERY_TIMEOUT",true);}
   NotebookGuiReply reply;try{reply=await operation.ConfigureAwait(false);}catch(TimeoutException){Quarantine();throw new NotebookGuiFailure("QUERY_TIMEOUT",true);}catch(NotebookGuiFailure){throw;}catch(OperationCanceledException){throw;}catch{throw new NotebookGuiFailure("CONNECTION_FAILED",false);}
   cancel.ThrowIfCancellationRequested();if(reply==null)throw new NotebookGuiFailure("INVALID_RESPONSE",false);string error=ErrorCode(reply);
   if(reply.restartRequired||error=="QUERY_TIMEOUT"||error=="RESTART_REQUIRED"){Quarantine();throw new NotebookGuiFailure(error=="RESTART_REQUIRED"?error:"QUERY_TIMEOUT",true);}if(!reply.ok)throw new NotebookGuiFailure(error.Length==0?"QUERY_FAILED":error,false);
   if(!Object.ReferenceEquals(session,provider.ControlSessionIdentity))throw new NotebookGuiFailure("IDENTITY_CHANGED",false);return reply;
  }
  static NotebookGuiControlCapture ParseControls(Dictionary<string,object> data,NotebookGuiSnapshot identity,object session){
   if(data==null||Integer(Value(data,"controlSchema"))!=1||!Prepared(data)||Text(data,"manufacturer")!=identity.Manufacturer||Text(data,"model")!=identity.Model||Text(data,"modelCode")!=identity.ModelCode||Text(data,"family")!=identity.Family||!(Value(data,"supportsWrite") is bool)||!(Value(data,"driverQueryPerformed") is bool)||!Flag(data,"supportsFanCurve",false)||!Flag(data,"supportsPowerLimits",false)||!Flag(data,"supportsLedWrites",false))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   var rows=Items(data,"controlStates");if(rows==null)throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   if(rows.Length==0&&Flag(data,"supportsWrite",false)&&Flag(data,"driverQueryPerformed",false))return null;
   var specs=NotebookControlRules.Specs.Where(s=>!s.NotebookOnly||identity.Family!="handheld").ToArray();
   if(rows.Length!=specs.Length||!Flag(data,"driverQueryPerformed",true)||!NotebookControlRules.Token(Text(data,"controlSessionToken"))||!NotebookControlRules.Token(Text(data,"controlReadToken"))||!NotebookControlRules.IdentityToken(Text(data,"controlIdentityToken")))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   var options=new List<NotebookGuiControlOption>();foreach(var spec in specs){var matches=rows.OfType<Dictionary<string,object>>().Where(r=>Text(r,"feature")==spec.Name).ToArray();if(matches.Length!=1)throw new NotebookGuiFailure("INVALID_RESPONSE",false);var row=matches[0];var values=Items(row,"allowedValues");
    if(values==null||values.Length>61||values.Any(v=>!Integer(v).HasValue||!spec.LegalValue(Integer(v).Value))||values.Select(v=>Integer(v).Value).Distinct().Count()!=values.Length||!(Value(row,"endpointPresent") is bool)||!(Value(row,"writeSupported") is bool)||!(Value(row,"valueReadbackConfirmed") is bool)||Text(row,"unit")!=spec.Unit||Integer(Value(row,"minimum"))!=spec.Minimum||Integer(Value(row,"maximum"))!=spec.Maximum||Text(row,"valueSemantics")!=(spec.CanConfirm?"brightness":"nativeStatus"))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
    bool supported=Flag(row,"writeSupported",true);int? current=Integer(Value(row,"currentValue"));bool confirmed=Flag(row,"valueReadbackConfirmed",true);
    if(supported!=(values.Length>0)||supported&&!Flag(row,"endpointPresent",true)||confirmed&&(!Flag(row,"endpointPresent",true)||!spec.CanConfirm||!current.HasValue||!spec.LegalValue(current.Value))||!confirmed&&Value(row,"currentValue")!=null)throw new NotebookGuiFailure("INVALID_RESPONSE",false);
    options.Add(new NotebookGuiControlOption{Feature=spec.Name,Caption=spec.Caption,Supported=supported,AllowedValues=Array.AsReadOnly(values.Select(v=>Integer(v).Value).ToArray()),CurrentValue=confirmed?current:null,ValueConfirmed=confirmed});
   }
   var setterNames=Items(data,"setCapabilities");if(setterNames==null||setterNames.Length>6||setterNames.Any(v=>!(v is string))||setterNames.Cast<string>().Distinct().Count()!=setterNames.Length||!setterNames.Cast<string>().OrderBy(v=>v,StringComparer.Ordinal).SequenceEqual(options.Where(o=>o.Supported).Select(o=>o.Feature).OrderBy(v=>v,StringComparer.Ordinal))||Flag(data,"supportsWrite",true)!=options.Any(o=>o.Supported))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
   return new NotebookGuiControlCapture{ModelCode=identity.ModelCode,IdentityToken=Text(data,"controlIdentityToken"),ReadToken=Text(data,"controlReadToken"),SessionToken=Text(data,"controlSessionToken"),WorkerSession=session,CapturedUtc=DateTime.UtcNow,Options=options.AsReadOnly()};
  }
  async Task ReadControls(NotebookGuiSnapshot snapshot,CancellationToken cancel){
   var provider=backend as INotebookGuiControlBackend;if(provider==null)return;object session=provider.ControlSessionIdentity;if(session==null)throw new NotebookGuiFailure("IDENTITY_CHANGED",false);
   var reply=await ControlRequest(new Dictionary<string,object>{{"op","controlRead"}},session,cancel).ConfigureAwait(false);snapshot.Controls=ParseControls(reply.data,snapshot,session);
  }
  public bool ControlCaptureCurrent(NotebookGuiControlCapture capture,DateTime now){return capture!=null&&!capture.Consumed&&ControlSubmissionCurrent(capture,now);}
  public bool ControlSubmissionCurrent(NotebookGuiControlCapture capture,DateTime now){var provider=backend as INotebookGuiControlBackend;var age=capture==null?TimeSpan.MaxValue:now-capture.CapturedUtc;return !disposed&&!RestartRequired&&provider!=null&&capture!=null&&capture.WorkerSession!=null&&Object.ReferenceEquals(capture.WorkerSession,provider.ControlSessionIdentity)&&age>=TimeSpan.FromSeconds(-5)&&age<TimeSpan.FromSeconds(30);}
  public async Task<NotebookGuiControlOutcome> ApplyControlAsync(NotebookGuiSnapshot snapshot,string setting,int value,bool controlEnabled,bool acceptDisplay,bool acceptReboot,CancellationToken cancel,Action demandBeforeSend=null){
   if(!controlEnabled)throw new NotebookGuiFailure("CONTROL_DISABLED",false);if(!await gate.WaitAsync(0,cancel).ConfigureAwait(false))throw new NotebookGuiFailure("INTERFACE_BUSY",false);
   try{var capture=snapshot==null?null:snapshot.Controls;if(!ControlCaptureCurrent(capture,DateTime.UtcNow))throw new NotebookGuiFailure("STALE_CAPTURE",false);var option=capture.Options.SingleOrDefault(o=>o.Feature==setting);if(option==null||!option.Supported||!option.AllowedValues.Contains(value))throw new NotebookGuiFailure("CONTROL_UNSUPPORTED",false);if((setting=="gpuEco"||setting=="gpuMux")&&!acceptDisplay||setting=="gpuMux"&&!acceptReboot)throw new NotebookGuiFailure("CONTROL_ACK_REQUIRED",false);
    capture.Consumed=true;var request=new Dictionary<string,object>{{"op","set"},{"setting",setting},{"value",value},{"experimental",true},{"modelCode",capture.ModelCode},{"identityToken",capture.IdentityToken},{"sessionToken",capture.SessionToken},{"readToken",capture.ReadToken},{"acceptDisplayDisruption",acceptDisplay},{"acceptReboot",acceptReboot}};
    Action demand=()=>{cancel.ThrowIfCancellationRequested();if(!controlEnabled||!Object.ReferenceEquals(snapshot.Controls,capture)||snapshot.ModelCode!=capture.ModelCode||!ControlSubmissionCurrent(capture,DateTime.UtcNow)||!option.Supported||!option.AllowedValues.Contains(value))throw new NotebookGuiFailure("STALE_CAPTURE",false);if(demandBeforeSend!=null)demandBeforeSend();};
    var reply=await ControlRequest(request,capture.WorkerSession,cancel,demand).ConfigureAwait(false);var data=reply.data;string applied=Text(data,"appliedState");
    if(data==null||!Prepared(data)||Text(data,"modelCode")!=snapshot.ModelCode||Text(data,"manufacturer")!=snapshot.Manufacturer||Text(data,"model")!=snapshot.Model||Integer(Value(data,"controlSchema"))!=1||Text(data,"setting")!=setting||Integer(Value(data,"requestedValue"))!=value||new[]{"writeAttempted","outputCompleted","driverAcknowledged","writeConfirmed","requiresRestart","restorable"}.Any(key=>!(Value(data,key) is bool))||!Flag(data,"restorable",false)||!new[]{"alreadySet","confirmed","submittedUnconfirmed","writeEffectUnknown"}.Contains(applied)||Flag(data,"writeConfirmed",true)&&(setting!="keyboardBrightness"||applied!="confirmed"&&applied!="alreadySet")||applied=="alreadySet"&&(!Flag(data,"writeAttempted",false)||!Flag(data,"writeConfirmed",true))||applied!="alreadySet"&&!Flag(data,"writeAttempted",true))throw new NotebookGuiFailure("INVALID_RESPONSE",false);
    if(Flag(data,"writeConfirmed",true)&&Integer(Value(data,"readbackValue"))!=value||applied=="confirmed"&&(!Flag(data,"writeConfirmed",true)||!Flag(data,"driverAcknowledged",true)||!Flag(data,"outputCompleted",true))||applied=="submittedUnconfirmed"&&(!Flag(data,"outputCompleted",true)||!Flag(data,"writeConfirmed",false))||Flag(data,"requiresRestart",true)&&setting!="gpuMux")throw new NotebookGuiFailure("INVALID_RESPONSE",false);
    return new NotebookGuiControlOutcome{WriteAttempted=Flag(data,"writeAttempted",true),WriteConfirmed=Flag(data,"writeConfirmed",true),NeedsRestart=Flag(data,"requiresRestart",true),AppliedState=applied};
   }finally{gate.Release();}
  }
 }
 public partial class MainWindow {
  Task notebookControlTask;
  bool notebookControlSubmitting;
  readonly Dictionary<string,int> notebookControlDraft=new Dictionary<string,int>();
  string notebookControlDraftToken;bool notebookAcceptDisplay,notebookAcceptReboot;
  bool NotebookMayApplyControl(NotebookGuiSnapshot snapshot,string feature,int value){return snapshot!=null&&!render&&!closing&&!busy&&!pageReadRunning&&!NotebookReadBusy&&HardwareWritesEnabled&&Object.ReferenceEquals(snapshot,notebookSnapshot)&&!notebookWaitingForRead&&NotebookCurrent(snapshot.CapturedUtc,DateTime.UtcNow)&&notebookReader.ControlCaptureCurrent(snapshot.Controls,DateTime.UtcNow)&&snapshot.Controls.Options.Any(o=>o.Feature==feature&&o.Supported&&o.AllowedValues.Contains(value))&&((feature!="gpuEco"&&feature!="gpuMux")||notebookAcceptDisplay)&&(feature!="gpuMux"||notebookAcceptReboot);}
  static string NotebookControlChoice(string feature,int value){if(feature=="performanceMode")return value==0?"平衡":value==1?"增强":"静音";if(feature=="chargeLimit")return value+"%";if(feature=="keyboardBrightness")return value+" / 3";if(feature=="gpuEco")return value==0?"标准":"节能";if(feature=="gpuMux")return value==0?"独显直连":"混合";return value==0?"关闭":"开启";}
  void AddNotebookControls(bool current){
   var snapshot=notebookSnapshot;var capture=snapshot==null?null:snapshot.Controls;if(capture==null)return;if(notebookControlDraftToken!=capture.ReadToken){notebookControlDraft.Clear();notebookControlDraftToken=capture.ReadToken;notebookAcceptDisplay=notebookAcceptReboot=false;}
   var panel=Box("设备设置","");foreach(var option in capture.Options){var row=new Grid{Margin=new Thickness(0,7,0,7)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(Label(option.Caption,13,muted));int chosen;bool edited=notebookControlDraft.TryGetValue(option.Feature,out chosen);int? initial=edited?(int?)chosen:option.ValueConfirmed?option.CurrentValue:null;
    var choices=new[]{"—"}.Concat(option.AllowedValues.Select(v=>NotebookControlChoice(option.Feature,v))).ToArray();var combo=FeatureCombo(choices,initial.HasValue?NotebookControlChoice(option.Feature,initial.Value):"—");combo.MinWidth=120;combo.MaxWidth=260;combo.HorizontalAlignment=HorizontalAlignment.Left;combo.IsEnabled=current&&!render&&!closing&&!busy&&!NotebookReadBusy&&option.Supported&&HardwareWritesEnabled&&notebookReader.ControlCaptureCurrent(capture,DateTime.UtcNow);combo.ToolTip=option.ValueConfirmed?null:"当前配置未确认；选择后点击应用。";Grid.SetColumn(combo,1);row.Children.Add(combo);
    var apply=Button("应用",async()=>{int value;if(notebookControlDraft.TryGetValue(option.Feature,out value))await ApplyNotebookControl(snapshot,option.Feature,value);});apply.Margin=new Thickness(8,0,0,0);apply.IsEnabled=edited&&NotebookMayApplyControl(snapshot,option.Feature,chosen);Grid.SetColumn(apply,2);row.Children.Add(apply);combo.SelectionChanged+=(s,e)=>{if(!combo.IsEnabled||!Object.ReferenceEquals(snapshot,notebookSnapshot)||!notebookReader.ControlCaptureCurrent(capture,DateTime.UtcNow)||combo.SelectedIndex<1)return;notebookControlDraft[option.Feature]=option.AllowedValues[combo.SelectedIndex-1];apply.IsEnabled=NotebookMayApplyControl(snapshot,option.Feature,notebookControlDraft[option.Feature]);};panel.Children.Add(row);
   }
   if(capture.Options.Any(o=>o.Supported&&(o.Feature=="gpuEco"||o.Feature=="gpuMux"))){var display=new CheckBox{Content="允许更改显示连接",IsChecked=notebookAcceptDisplay,Foreground=muted,Margin=new Thickness(0,10,0,4),IsEnabled=current&&!render&&!closing&&!busy&&HardwareWritesEnabled};display.Checked+=(s,e)=>{notebookAcceptDisplay=true;ShowPage(page);};display.Unchecked+=(s,e)=>{notebookAcceptDisplay=false;ShowPage(page);};panel.Children.Add(display);var reboot=new CheckBox{Content="了解 MUX 更改可能需要重新启动",IsChecked=notebookAcceptReboot,Foreground=muted,Margin=new Thickness(0,4,0,4),IsEnabled=display.IsEnabled};reboot.Checked+=(s,e)=>{notebookAcceptReboot=true;ShowPage(page);};reboot.Unchecked+=(s,e)=>{notebookAcceptReboot=false;ShowPage(page);};panel.Children.Add(reboot);}
   content.Children.Add(Card(panel));
  }
  void DemandNotebookControlSend(NotebookGuiSnapshot snapshot,NotebookGuiControlCapture capture,string setting,int value){int chosen;if(!notebookControlSubmitting||!busy||render||closing||!HardwareWritesEnabled||!Object.ReferenceEquals(snapshot,notebookSnapshot)||snapshot==null||!Object.ReferenceEquals(snapshot.Controls,capture)||notebookWaitingForRead||!NotebookCurrent(snapshot.CapturedUtc,DateTime.UtcNow)||!notebookReader.ControlSubmissionCurrent(capture,DateTime.UtcNow)||notebookControlDraftToken!=capture.ReadToken||!notebookControlDraft.TryGetValue(setting,out chosen)||chosen!=value||((setting=="gpuEco"||setting=="gpuMux")&&!notebookAcceptDisplay)||setting=="gpuMux"&&!notebookAcceptReboot)throw new NotebookGuiFailure("CONTROL_DISABLED",false);}
  void SetNotebookControlObservation(NotebookGuiSnapshot snapshot,string setting,int? confirmed){if(snapshot==null||setting!="keyboardBrightness")return;bool known=confirmed.HasValue&&confirmed>=0&&confirmed<=3;snapshot.Readings=snapshot.Readings.Select(r=>r.Feature!=setting?r:new NotebookGuiReading{Feature=r.Feature,Caption=r.Caption,Text=known?confirmed.Value.ToString(CultureInfo.InvariantCulture)+" / 3":"—",Confirmed=known}).ToList();}
  async Task ApplyNotebookControl(NotebookGuiSnapshot snapshot,string setting,int value){if(!NotebookMayApplyControl(snapshot,setting,value)||!PermitHardwareWrite())return;notebookControlSubmitting=true;busy=true;try{notebookControlTask=ApplyNotebookControlCore(snapshot,setting,value);if(page=="笔记本与掌机")ShowPage(page);await notebookControlTask;}finally{notebookControlTask=null;notebookControlSubmitting=false;busy=false;if(snapshot!=null)snapshot.Controls=null;notebookControlDraft.Clear();if(!closing&&page=="笔记本与掌机")ShowPage(page);}}
  async Task ApplyNotebookControlCore(NotebookGuiSnapshot snapshot,string setting,int value){SetNotebookControlObservation(snapshot,setting,null);try{var capture=snapshot.Controls;var result=await notebookReader.ApplyControlAsync(snapshot,setting,value,HardwareWritesEnabled,notebookAcceptDisplay,notebookAcceptReboot,notebookCancellation.Token,()=>DemandNotebookControlSend(snapshot,capture,setting,value));if(result.WriteConfirmed)SetNotebookControlObservation(snapshot,setting,value);if(!closing)notebookMessage=result.AppliedState=="alreadySet"?"当前已是此设置。":result.WriteConfirmed?"设置已应用并读回确认。":result.AppliedState=="submittedUnconfirmed"?"设置已提交，当前值未确认。":"应用未确认，请重新读取。";if(!closing&&result.NeedsRestart)notebookMessage+=" MUX 更改可能需要重新启动。";}catch(Exception){if(!closing)notebookMessage=NotebookRestartRequired?"应用超时，请重新打开应用并检查当前设置。":"应用未确认，请重新读取。";}}
 }
}
