// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// UI lifecycle for the independent animation scheduler. Construction has no I/O.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RogControl {
 public partial class MainWindow {
  AuraAnimationController auraAnimation;
  readonly Stopwatch auraAnimationClock=Stopwatch.StartNew();
  Task auraAnimationTask,auraAnimationPreparationTask,auraSyncOperationTask,auraScanTask,auraStopTask;
  bool auraReturningOwnership;
  double auraAnimationPeriodSeconds=6,auraAnimationFramesPerSecond=2;
  public bool AuraAnimationActive {get{return auraAnimation!=null&&auraAnimation.Active||auraAnimationTask!=null&&!auraAnimationTask.IsCompleted||auraAnimationPreparationTask!=null&&!auraAnimationPreparationTask.IsCompleted;}}
  public bool AuraAnimationCanPreviewReload {get{return !AuraAnimationActive&&(auraStopTask==null||auraStopTask.IsCompleted)&&(auraSyncOperationTask==null||auraSyncOperationTask.IsCompleted)&&(auraScanTask==null||auraScanTask.IsCompleted);}}
  public void RequestAuraAnimationStop(){auraSyncStopRequested=true;auraSyncCoordinator.RequestStop();if(auraAnimation!=null)auraAnimation.RequestStop();gearReadSessions.Clear();}
  static bool IsAuraView(string view){return view=="灯效 & Aura Sync"||view=="神光同步";}
  void OnAuraAnimationNavigation(string nextPage){if(!IsAuraView(nextPage)&&(AuraSyncBusy||auraSyncConnectionsActive||auraOwnershipResults.Values.Any(r=>r!=null&&r.CanReturnOriginalFlag))){RequestAuraAnimationStop();BeginAuraStopForNavigation();}}
  void BeginAuraStopForNavigation(){if(auraStopTask==null||auraStopTask.IsCompleted)auraStopTask=StopAuraSyncCoreAsync();}
  public async Task WaitAuraAnimationAsync(bool returnOwnership){RequestAuraAnimationStop();var stopping=auraStopTask;if(stopping!=null&&!stopping.IsCompleted){await stopping;return;}await AwaitAuraTasks();if(returnOwnership)await ReturnConfirmedAuraOwnership();}
  async Task AwaitAuraTasks(){var preparing=auraAnimationPreparationTask;if(preparing!=null)try{await preparing;}catch(Exception){}var loop=auraAnimationTask;if(loop!=null)try{await loop;}catch(Exception){}var operation=auraSyncOperationTask;if(operation!=null)try{await operation;}catch(Exception){}var scan=auraScanTask;if(scan!=null)try{await scan;}catch(Exception){}}
  AuraSyncTarget BindAuraTarget(AuraSyncTarget target,object session,long generation){if(target==null||session==null||generation!=DeviceHotplugGeneration)throw new InvalidOperationException("灯控读取的连接已失效，请重新扫描。");target.WorkerSession=session;target.ConnectionGeneration=generation;return target;}
  bool AuraBindingCurrent(AuraSyncTarget target){if(target==null||target.WorkerSession==null||target.ConnectionGeneration!=DeviceHotplugGeneration||DeviceHotplugPageReadsPaused)return false;var current=target.Backend=="direct"?worker.SessionIdentity:target.Backend=="gear"?gearWorker.SessionIdentity:null;return Object.ReferenceEquals(target.WorkerSession,current);}
  bool AuraAnimationBindingCurrent(AuraAnimationBinding binding){return !render&&!closing&&!auraSyncStopRequested&&HardwareWritesEnabled&&binding!=null&&binding.Target!=null&&Object.ReferenceEquals(binding.WorkerSession,binding.Target.WorkerSession)&&binding.ConnectionGeneration==binding.Target.ConnectionGeneration&&AuraBindingCurrent(binding.Target);}
  async Task<AuraAnimationBinding> RefreshAuraAnimationBinding(AuraAnimationBinding binding){if(!AuraAnimationBindingCurrent(binding))throw new InvalidOperationException("灯控会话已失效。");var fresh=await RefreshAuraSyncTarget(binding.Target);return new AuraAnimationBinding{Target=fresh,WorkerSession=fresh.WorkerSession,ConnectionGeneration=fresh.ConnectionGeneration};}
  async Task StartAuraAnimationAsync(){
   var selectedTargets=auraSyncTargets.Where(t=>auraSyncSelected.Contains(t.Id)).ToArray();if(selectedTargets.Length!=auraSyncSelected.Count||selectedTargets.Length==0)throw new InvalidOperationException("请重新确认要同步的设备。");if(auraAnimation==null)auraAnimation=new AuraAnimationController(()=>auraAnimationClock.ElapsedMilliseconds,(milliseconds,token)=>Task.Delay(milliseconds,token));
   var options=new AuraAnimationOptions{Effect=effect,Color=color,Brightness=brightness,PeriodSeconds=auraAnimationPeriodSeconds,FramesPerSecond=auraAnimationFramesPerSecond};var bindings=selectedTargets.Select(t=>new AuraAnimationBinding{Target=t,WorkerSession=t.WorkerSession,ConnectionGeneration=t.ConnectionGeneration}).ToArray();var preparation=auraAnimation.PrepareAsync(bindings,options,RefreshAuraAnimationBinding,SendAuraSyncCommand,()=>{hardwareControlPolicy.Demand();if(render||closing||auraSyncStopRequested)throw new OperationCanceledException("同步已停止。");},AuraAnimationBindingCurrent);auraAnimationPreparationTask=preparation;bool ready;try{ready=await preparation;}finally{if(Object.ReferenceEquals(auraAnimationPreparationTask,preparation))auraAnimationPreparationTask=null;}if(!ready){auraSyncMessage="动画未开始："+(auraAnimation.Report.Error??"已停止。");return;}if(auraSyncStopRequested||closing){auraAnimation.RequestStop();return;}
    auraSyncConnectionsActive=true;controlling=true;auraSyncMessage="接口核对已完成，正在等待首帧结果。";auraAnimationTask=RunAuraAnimationLifetimeAsync();
  }
   async Task RunAuraAnimationLifetimeAsync(){
    try{
     // The first submission belongs to this same tracked task. A prepared
     // controller has not yet submitted or confirmed any lighting frame.
     await auraAnimation.TickAsync();
     if(auraAnimation.Report.State=="running"&&auraAnimation.Report.Frames>0)auraSyncMessage="已提交首帧；配置读回与实体灯光效果分别确认。";
     else if(auraAnimation.Report.State=="faulted")auraSyncMessage="同步已停止："+auraAnimation.Report.Error;
     if(!closing&&IsAuraView(page))ShowPage(page);
     // TickAsync advanced nextDue after its completed frame. RunAsync therefore
     // waits for the next sample instead of submitting that first frame again.
     if(auraAnimation.Report.State=="running")await auraAnimation.RunAsync();
     if(auraAnimation.Report.State=="faulted")auraSyncMessage="同步已停止："+auraAnimation.Report.Error;
     else auraSyncMessage=auraAnimation.Report.AnyWriteAttempted?"软件动画已停止；最后一次提交的实体灯光效果仍需确认。":"同步已停止，未提交灯光设置。";
    }catch(Exception error){auraSyncMessage="同步已停止："+error.GetBaseException().Message;}
    finally{if(!closing&&IsAuraView(page))ShowPage(page);}
   }
  async Task ReturnConfirmedAuraOwnership(){
   foreach(var pair in auraOwnershipResults.ToArray()){var receipt=pair.Value;if(receipt==null||!receipt.OwnershipConfirmed||!receipt.CanReturnOriginalFlag||receipt.Target==null)continue;if(receipt.WorkerSession==null||!Object.ReferenceEquals(receipt.WorkerSession,receipt.Target.WorkerSession)||receipt.ConnectionGeneration!=receipt.Target.ConnectionGeneration||!AuraBindingCurrent(receipt.Target)){receipt.RestorationState="connectionExpired";receipt.Error="连接已变化，未向设备写入原灯控标志。";continue;}
    // Closing disables ordinary writes, but the already-authorized exact flag
    // return remains gated by the live policy and original connection receipt.
    if(!hardwareControlPolicy.Enabled||render){receipt.RestorationState="permissionUnavailable";receipt.Error="写入权限已关闭，原灯控标志未自动写回。";continue;}
    auraReturningOwnership=true;try{var returned=await auraSyncCoordinator.ReturnSoftwareLightingAsync(receipt,RefreshAuraSyncTarget,SendAuraSyncCommand,()=>hardwareControlPolicy.Demand());if(returned.RestorationConfirmed)auraOwnershipResults[pair.Key]=returned;else{receipt.RestorationState=returned.RestorationState;receipt.Error=returned.Error;}}catch(Exception error){receipt.RestorationState="unconfirmed";receipt.Error=error.GetBaseException().Message;}finally{auraReturningOwnership=false;}
   }
  }
  async Task StopAuraSyncCoreAsync(){
   RequestAuraAnimationStop();await AwaitAuraTasks();await ReturnConfirmedAuraOwnership();
   // Direct release only closes retained handles; it sends no RGB/default packet.
   var directSession=worker.SessionIdentity;if(directSession!=null)try{await worker.Request<AuraSyncNativeReply>(new Dictionary<string,object>{{"op","release"}},directSession,3000);}catch(Exception){}
   worker.Kill();gearWorker.Kill();discoveryWorker.Kill();WorkerClient sdk;if(sdkWorkers.TryGetValue("fan",out sdk))sdk.Kill();auraSyncConnectionsActive=false;controlling=false;auraSyncSelected.Clear();foreach(var target in auraSyncTargets){target.Online=false;target.CanStaticRgb=false;target.CanReleaseSoftwareLighting=false;target.WorkerSession=null;target.UnavailableReason="连接已释放，请重新扫描。";}int uncertain=auraOwnershipResults.Values.Count(r=>r!=null&&r.WriteAttempted&&!r.RestorationConfirmed);auraSyncMessage="同步已停止，不会主动恢复先前颜色。"+(uncertain>0?uncertain+" 项灯控标志归还未确认。":"已确认的接管标志仅在原连接仍有效时交还。");if(!closing&&IsAuraView(page))ShowPage(page);
  }
 }
}
