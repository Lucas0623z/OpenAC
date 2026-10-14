// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RogControl {
 public sealed class MouseCalibrationRecord {
  public readonly int ModelPid,Mode,Material,LodMode,Normal,Lift,Drop,Manual;
  public readonly bool Triggered;
  readonly int[] k;
  static int Number(IDictionary<string,object> data,string key,int max){object value;if(data==null||!data.TryGetValue(key,out value)||!(value is int)||(int)value<0||(int)value>max)throw new ArgumentException("校准数据未完整读回。");return (int)value;}
  static Dictionary<string,object> Map(IDictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
  public MouseCalibrationRecord(IDictionary<string,object> snapshot){
   int pid=Number(snapshot,"modelPid",65535);ModelPid=pid;if(pid!=7546&&pid!=7548)throw new ArgumentException("此型号的校准协议尚未确认。");
   var info=Map(snapshot,"mouseCalibrationInfo");var lod=Map(snapshot,"mouseCalibrationLOD");
   Mode=Number(info,"mode",255);Material=Number(info,"material",255);if(Mode!=0&&Mode!=255||Material>28&&Material!=255)throw new ArgumentException("未知校准模式。");
   object values;if(info==null||!info.TryGetValue("kValue",out values)||values is string||!(values is IEnumerable))throw new ArgumentException("校准参数未读回。");
   var bytes=((IEnumerable)values).Cast<object>().Take(4).ToArray();if(bytes.Length!=3||bytes.Any(v=>!(v is int)||(int)v<0||(int)v>255))throw new ArgumentException("校准参数不完整。");k=bytes.Cast<int>().ToArray();
   LodMode=Number(lod,"lodMode",2);Normal=Number(lod,"normalModeLOD",2);Lift=Number(lod,"asymmetricLiftValue",11);Drop=Number(lod,"asymmetricDropValue",11);Manual=Number(lod,"manualCalibrationLOD",11);
   object trigger;if(lod==null||!lod.TryGetValue("sensorTriggered",out trigger)||!(trigger is bool))throw new ArgumentException("感应器状态未读回。");Triggered=(bool)trigger;
  }
  public bool Restorable {get{return (LodMode!=1||Lift>=1&&Drop>=1&&Lift>=Drop)&&(LodMode!=2||Manual>=1);}}
  public Dictionary<string,object> LodValue(int mode,int manual){return new Dictionary<string,object>{{"lodMode",mode},{"normalModeLOD",Normal},{"asymmetricLiftValue",Lift},{"asymmetricDropValue",Drop},{"manualCalibrationLOD",manual}};}
  public bool Preserved(MouseCalibrationRecord other){return other!=null&&ModelPid==other.ModelPid&&Material==other.Material&&k.SequenceEqual(other.k)&&Normal==other.Normal&&Lift==other.Lift&&Drop==other.Drop;}
  public bool Same(MouseCalibrationRecord other){return Preserved(other)&&Mode==other.Mode&&LodMode==other.LodMode&&Manual==other.Manual;}
 }
 public sealed class MouseCalibrationOutcome {
  public bool Completed,Restored,WriteAttempted;public int Level;public string Message;
 }
 // Delegates separate the state machine from device access, time and input.
 // No operation can discover or silently restart a connection during a run.
 public static class MouseCalibrationCoordinator {
  public const int DeadlineMs=25000,TrialDeadlineMs=23000;
  public static async Task<MouseCalibrationOutcome> Run(Func<Task<MouseCalibrationRecord>> read,Func<string,object,Task<MouseCalibrationRecord>> write,Func<bool> active,Func<bool> restoreAllowed,Func<long> movement,Func<int,Task> delay,Func<long> elapsed,Action<int> progress,Action beginRestore=null){
   var result=new MouseCalibrationOutcome();MouseCalibrationRecord original=null;
   Action demand=()=>{if(!active())throw new OperationCanceledException("校准已取消。");if(elapsed()>=TrialDeadlineMs)throw new TimeoutException("校准超时。");};
   try {
    demand();original=await read();demand();if(original==null||!original.Restorable)throw new InvalidOperationException("原设置无法完整恢复，请先重新读取。");
    if(original.Mode!=255){result.WriteAttempted=true;var switched=await write("mouseCalibrationMode",255);demand();if(!original.Preserved(switched)||switched.Mode!=255)throw new InvalidOperationException("切换校准模式未确认。");}
    for(int level=1;level<=11;level++){
     demand();progress(level);demand();result.WriteAttempted=true;var trial=await write("mouseCalibrationLOD",original.LodValue(2,level));demand();
     if(!original.Preserved(trial)||trial.Mode!=255||trial.LodMode!=2||trial.Manual!=level)throw new InvalidOperationException("校准设置未完整读回。");
     long moved=movement();demand();await delay(2000);demand();long afterMovement=movement();demand();if(afterMovement==moved)throw new InvalidOperationException("校准期间请持续移动鼠标。");
     var current=await read();demand();if(!original.Preserved(current)||current.Mode!=255||current.LodMode!=2||current.Manual!=level)throw new InvalidOperationException("校准设置已改变。");
     if(!current.Triggered){result.Completed=true;result.Level=level;result.Message="校准完成。";return result;}
    }
    throw new InvalidOperationException("未找到适用的抬升距离。");
   }catch(Exception e){result.Message=e.Message;}
   if(result.WriteAttempted&&original!=null&&original.Restorable){
    try {
     if(beginRestore!=null)beginRestore();
     Action restoreDemand=()=>{if(!restoreAllowed())throw new InvalidOperationException("原设备连接已失效。");if(elapsed()>=DeadlineMs)throw new TimeoutException("恢复设置超时。");};
     restoreDemand();var current=await read();restoreDemand();if(!original.Preserved(current))throw new InvalidOperationException("其他校准参数已改变。");
     if(current.Mode!=255){restoreDemand();current=await write("mouseCalibrationMode",255);restoreDemand();if(!original.Preserved(current)||current.Mode!=255)throw new InvalidOperationException("恢复模式未确认。");}
     restoreDemand();current=await write("mouseCalibrationLOD",original.LodValue(original.LodMode,original.Manual));restoreDemand();
     if(!original.Preserved(current)||current.LodMode!=original.LodMode||current.Manual!=original.Manual)throw new InvalidOperationException("恢复距离未确认。");
     if(original.Mode!=255){restoreDemand();current=await write("mouseCalibrationMode",original.Mode);restoreDemand();}
     restoreDemand();current=await read();restoreDemand();result.Restored=original.Same(current);if(!result.Restored)throw new InvalidOperationException("原设置尚未确认。");
    }catch(Exception e){result.Message+=" 原设置恢复未确认："+e.Message;}
   }
   return result;
  }
 }
 public partial class MainWindow {
  bool mouseCalibrationWorkflowRunning,mouseCalibrationCancel;
  Task mouseCalibrationWorkflowTask;long mouseCalibrationMovement;Point? mouseCalibrationLastPosition;
  void BeginMouseCalibrationInput(){MouseMove+=(s,e)=>{if(!mouseCalibrationWorkflowRunning)return;var point=e.GetPosition(this);if(!mouseCalibrationLastPosition.HasValue||point!=mouseCalibrationLastPosition.Value){mouseCalibrationMovement++;mouseCalibrationLastPosition=point;}};}
  bool MouseCalibrationMayStart(Dictionary<string,object> state){
   object session;if(!HardwareWritesEnabled||busy||AuraSyncBusy||mouseCalibrationWorkflowRunning||!GearCanSet(state,"mouseCalibrationMode")||!CurrentGearReadSession("mouse",state,out session))return false;
   try{var record=new MouseCalibrationRecord(GearMap(state,"snapshot"));object pid;return state.TryGetValue("pid",out pid)&&pid is int&&(int)pid==record.ModelPid&&record.Restorable;}catch{return false;}
  }
  void CancelMouseCalibrationWorkflow(){mouseCalibrationCancel=true;}
  void OnMouseCalibrationNavigation(string destination){string tab;if(mouseCalibrationWorkflowRunning&&(destination!="device:mouse"||!gearTabs.TryGetValue("mouse",out tab)||tab!="校准"))CancelMouseCalibrationWorkflow();}
  async Task WaitMouseCalibrationWorkflow(){CancelMouseCalibrationWorkflow();var pending=mouseCalibrationWorkflowTask;if(pending!=null)await pending;}
  async Task StartMouseCalibrationWorkflow(Dictionary<string,object> state){
   if(!MouseCalibrationMayStart(state))return;object session;if(!CurrentGearReadSession("mouse",state,out session))return;
   mouseCalibrationCancel=false;mouseCalibrationWorkflowRunning=true;mouseCalibrationLastPosition=null;busy=true;
   mouseCalibrationWorkflowTask=RunMouseCalibrationWorkflow(state,session);await mouseCalibrationWorkflowTask;
  }
  async Task RunMouseCalibrationWorkflow(Dictionary<string,object> state,object session){
   string id=OriginalStateText(state,"deviceId","");int pid=Convert.ToInt32(state["pid"]);var timer=Stopwatch.StartNew();bool restoring=false;
   Func<bool> alive=()=>{object bound;return !render&&hardwareControlPolicy.Enabled&&gearReadSessions.TryGetValue("mouse",out bound)&&Object.ReferenceEquals(bound,session)&&Object.ReferenceEquals(session,gearWorker.SessionIdentity);};
   Func<Dictionary<string,object>,Task<MouseCalibrationRecord>> request=async command=>{
    if(!alive())throw new InvalidOperationException("原设备连接已失效，请重新读取。");
    int remaining=MouseCalibrationCoordinator.DeadlineMs-(int)timer.ElapsedMilliseconds;if(remaining<=0)throw new TimeoutException("校准超时。");
    command["kind"]="mouse";command["deviceId"]=id;
    var reply=await gearWorker.Request<SdkReply>(command,session,Math.Min(4000,remaining),()=>{if(!alive())throw new InvalidOperationException("原设备连接已失效，没有发送校准命令。");if(!restoring&&(closing||mouseCalibrationCancel))throw new OperationCanceledException("校准已取消。");if(timer.ElapsedMilliseconds>=(restoring?MouseCalibrationCoordinator.DeadlineMs:MouseCalibrationCoordinator.TrialDeadlineMs))throw new TimeoutException("校准命令已超时，没有发送。");});if(!alive()||reply==null||!reply.ok||reply.data==null||!GearOnline(reply.data)||OriginalStateText(reply.data,"deviceId","")!=id||!reply.data.ContainsKey("pid")||Convert.ToInt32(reply.data["pid"])!=pid)throw new InvalidOperationException(reply==null?"设备没有返回校准数据。":SdkError(reply.error));
    if(command["op"].ToString()=="calibrationSet"&&OriginalStateFlag(reply.data,"writeConfirmed")!=true)throw new InvalidOperationException("校准设置未确认，请重新读取。");
    var record=new MouseCalibrationRecord(GearMap(reply.data,"snapshot"));if(record.ModelPid!=pid)throw new InvalidOperationException("校准回读型号不一致。");return record;
   };
   try {
    ShowPage(page);
    var outcome=await MouseCalibrationCoordinator.Run(()=>request(new Dictionary<string,object>{{"op","calibrationRead"}}),(setting,value)=>request(new Dictionary<string,object>{{"op","calibrationSet"},{"setting",setting},{"value",value},{"experimental",true}}),()=>alive()&&!closing&&!mouseCalibrationCancel,alive,()=>mouseCalibrationMovement,async milliseconds=>{var wait=Stopwatch.StartNew();while(wait.ElapsedMilliseconds<milliseconds){if(mouseCalibrationCancel||closing)break;await Task.Delay(Math.Min(50,milliseconds-(int)wait.ElapsedMilliseconds));}},()=>timer.ElapsedMilliseconds,level=>{status.Text="校准中…";},()=>restoring=true);
    Log(outcome.Message);status.ToolTip=outcome.WriteAttempted&&!outcome.Completed?(outcome.Restored?"已恢复校准前的设置。":"请重新读取并检查当前校准设置。"):null;
   }catch(Exception e){Log("校准未完成："+e.Message);}
   finally {mouseCalibrationWorkflowRunning=false;busy=false;gearStates.Remove("mouse");gearReadSessions.Remove("mouse");pageReadAttempts.Remove("device:mouse");if(!closing)ShowPage(page);}
  }
 }
}
