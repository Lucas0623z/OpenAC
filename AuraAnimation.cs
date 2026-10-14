// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independent uniform-RGB software animation. No native/vendor code or I/O.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RogControl {
 public sealed class AuraAnimationOptions {
  public string Effect="breathe",Color="#FF466A";
  public int Brightness=100;
  public double PeriodSeconds=6,FramesPerSecond=2;
  public AuraAnimationOptions Copy(){return new AuraAnimationOptions{Effect=Effect,Color=Color,Brightness=Brightness,PeriodSeconds=PeriodSeconds,FramesPerSecond=FramesPerSecond};}
  public void Validate(){if(!AuraAnimationPhase.SupportsUniform(Effect))throw new ArgumentException("This effect has no confirmed uniform RGB animation route.");AuraSyncCoordinator.OutputColor("static",Color,Brightness);if(Double.IsNaN(PeriodSeconds)||Double.IsInfinity(PeriodSeconds)||PeriodSeconds<1||PeriodSeconds>60||Double.IsNaN(FramesPerSecond)||Double.IsInfinity(FramesPerSecond)||FramesPerSecond<0.2||FramesPerSecond>5)throw new ArgumentException("Animation period or frame rate is outside its bounded range.");}
 }
 public static class AuraAnimationPhase {
  public static bool SupportsUniform(string effect){return effect=="breathe"||effect=="cycle"||effect=="flash";}
  // A single frame color is sampled once for the entire group. Autonomous
  // device effects are never used to claim matching physical phase.
  public static string Sample(AuraAnimationOptions options,long elapsedMilliseconds){if(options==null)throw new ArgumentNullException("options");options.Validate();if(elapsedMilliseconds<0)throw new ArgumentOutOfRangeException("elapsedMilliseconds");double phase=(elapsedMilliseconds%(options.PeriodSeconds*1000))/(options.PeriodSeconds*1000);if(options.Effect=="cycle"){double h=phase*6;int sector=(int)h;double f=h-sector;int high=255,low=0,r=0,g=0,b=0,up=(int)Math.Round(f*255),down=255-up;switch(sector){case 0:r=high;g=up;b=low;break;case 1:r=down;g=high;b=low;break;case 2:r=low;g=high;b=up;break;case 3:r=low;g=down;b=high;break;case 4:r=up;g=low;b=high;break;default:r=high;g=low;b=down;break;}return AuraSyncCoordinator.OutputColor("static","#"+r.ToString("X2")+g.ToString("X2")+b.ToString("X2"),options.Brightness);}double gain=options.Effect=="flash"?(phase<0.5?1:0):(1+Math.Cos(phase*2*Math.PI))/2;int rgb=Int32.Parse(options.Color.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);return "#"+String.Concat(new[]{(rgb>>16)&255,(rgb>>8)&255,rgb&255}.Select(v=>((int)Math.Round(v*options.Brightness/100.0*gain)).ToString("X2")));}
 }
 public sealed class AuraAnimationBinding {
  public AuraSyncTarget Target;
  public object WorkerSession;
  public long ConnectionGeneration;
 }
 public sealed class AuraAnimationReport {
  public string State="idle",Error,LastColor;
  public long Frames,Commands,SkippedFrames,LastDispatchSkewMilliseconds,LastTransactionMilliseconds;
  public bool AnyWriteAttempted,AllLastFrameReadbackConfirmed;
  public double RequestedFramesPerSecond,ObservedFramesPerSecond;
 }
 public sealed class AuraAnimationController {
  readonly Func<long> clock;
  readonly Func<int,CancellationToken,Task> delay;
  readonly Func<AuraAnimationBinding,string,bool,AuraSyncCommand[]> framePlan;
  AuraAnimationBinding[] bindings;
  AuraAnimationOptions options;
  Func<AuraAnimationBinding,Task<AuraAnimationBinding>> refresh;
  Func<AuraSyncCommand,Task<AuraSyncCommandReply>> send;
  Func<AuraAnimationBinding,bool> alive;
  Action demand;
  CancellationTokenSource cancellation;
  bool preparing,ticking,looping,firstFrame;
  long epoch,nextDue,lastCompleted=-1;
  string lastColor;
  public AuraAnimationReport Report {get;private set;}
  public bool Active {get{return preparing||Report.State=="running"||ticking||looping;}}
  public bool FrameInFlight {get{return ticking;}}
  public AuraAnimationController(Func<long> milliseconds,Func<int,CancellationToken,Task> pause):this(milliseconds,pause,FrameCommands){}
  // Internal pure seam permits empty/malformed future plans to be tested at
  // the complete-group boundary without involving a worker or native device.
  internal AuraAnimationController(Func<long> milliseconds,Func<int,CancellationToken,Task> pause,Func<AuraAnimationBinding,string,bool,AuraSyncCommand[]> buildFrame){if(milliseconds==null||pause==null||buildFrame==null)throw new ArgumentNullException();clock=milliseconds;delay=pause;framePlan=buildFrame;Report=new AuraAnimationReport();}
  public void RequestStop(){if(cancellation!=null)cancellation.Cancel();if(Report.State=="running"||preparing)Report.State="stopping";}
  public async Task<bool> PrepareAsync(IEnumerable<AuraAnimationBinding> targets,AuraAnimationOptions draft,Func<AuraAnimationBinding,Task<AuraAnimationBinding>> read,Func<AuraSyncCommand,Task<AuraSyncCommandReply>> writer,Action requirePermission,Func<AuraAnimationBinding,bool> isAlive){
   if(Active)throw new InvalidOperationException("Animation already has an active operation.");if(targets==null||draft==null||read==null||writer==null||requirePermission==null||isAlive==null)throw new ArgumentNullException();draft.Validate();var selected=targets.ToArray();if(selected.Length==0||selected.Length>32||selected.Any(b=>b==null||b.Target==null||b.WorkerSession==null||!b.Target.CanStaticRgb||!b.Target.Online)||selected.Select(b=>b.Target.Id).Distinct(StringComparer.Ordinal).Count()!=selected.Length)throw new InvalidOperationException("Select unique, session-bound RGB targets only.");
   if(cancellation!=null)cancellation.Dispose();cancellation=new CancellationTokenSource();options=draft.Copy();refresh=read;send=writer;demand=requirePermission;alive=isAlive;Report=new AuraAnimationReport{State="preparing",RequestedFramesPerSecond=options.FramesPerSecond};preparing=true;lastColor=null;lastCompleted=-1;firstFrame=true;
   try{var fresh=new List<AuraAnimationBinding>();foreach(var original in selected){CheckAlive(original);var value=await refresh(original);CheckAlive(original);if(value==null||value.Target==null||value.Target.Id!=original.Target.Id||!Object.ReferenceEquals(value.WorkerSession,original.WorkerSession)||value.ConnectionGeneration!=original.ConnectionGeneration)throw new InvalidOperationException("Device binding changed during animation preflight.");CheckAlive(value);AuraSyncCoordinator.BuildStaticCommands(value.Target,AuraAnimationPhase.Sample(options,0));fresh.Add(value);}bindings=fresh.ToArray();epoch=clock();if(epoch<0)throw new InvalidOperationException("The animation clock is invalid.");nextDue=epoch;Report.State="running";return true;}catch(OperationCanceledException){Report.State="stopped";return false;}catch(Exception error){Report.State="faulted";Report.Error=error.GetBaseException().Message;return false;}finally{preparing=false;}
  }
  void CheckAlive(AuraAnimationBinding binding){if(cancellation==null||cancellation.IsCancellationRequested)throw new OperationCanceledException("Animation stopped.");if(binding==null||binding.Target==null||!alive(binding))throw new InvalidOperationException("Lighting connection or ownership binding expired.");}
  public static AuraSyncCommand[] FrameCommands(AuraAnimationBinding binding,string output,bool initial){
   if(binding==null||binding.Target==null||binding.WorkerSession==null)throw new InvalidOperationException("A live worker binding is required.");
   var plan=AuraSyncCoordinator.BuildStaticCommands(binding.Target,output);
   // The PID7489 lighting scope uses one explicit Static1 transaction on every
   // frame. Only the older multi-command path has a color-only later frame.
   bool scopedHeadset=binding.Target.Kind=="headset"&&binding.Target.State!=null&&binding.Target.State.ContainsKey("controlScope")&&Object.Equals(binding.Target.State["controlScope"],"headsetLightingOnly");
   if(!initial&&binding.Target.Kind=="headset"&&!scopedHeadset)plan=plan.Where(c=>c.Setting=="lightingColor").ToArray();
   foreach(var command in plan){command.ExpectedSession=binding.WorkerSession;command.ConnectionGeneration=binding.ConnectionGeneration;command.IsAnimationFrame=true;if(command.Backend=="gear"){command.Request["op"]=command.Setting=="staticLighting"||binding.Target.Kind=="keyboard"||binding.Target.Kind=="mouse"?"lightingSet":"set";if(binding.Target.Kind=="headset")command.Request["requireLightingOwnership"]=true;}}
   RequireFramePlan(binding,plan);return plan;
  }
  static void RequireFramePlan(AuraAnimationBinding binding,AuraSyncCommand[] plan){if(binding==null||binding.Target==null||plan==null||plan.Length==0||plan.Any(command=>command==null||command.Request==null||command.TargetId!=binding.Target.Id||command.Backend!=binding.Target.Backend||command.Kind!=binding.Target.Kind||!Object.ReferenceEquals(command.ExpectedSession,binding.WorkerSession)||command.ConnectionGeneration!=binding.ConnectionGeneration||!command.IsAnimationFrame))throw new InvalidOperationException("A selected lighting target has no complete session-bound frame plan.");}
  public async Task TickAsync(){
   if(Report.State!="running"||ticking)return;long started=clock();if(started<epoch){Fault("The animation clock moved backwards.");return;}if(started<nextDue)return;ticking=true;long firstDispatch=-1,lastDispatch=-1;bool allConfirmed=true;
   try{foreach(var binding in bindings)CheckAlive(binding);string output=AuraAnimationPhase.Sample(options,started-epoch);if(!firstFrame&&output==lastColor){Report.SkippedFrames++;nextDue=started+MinimumInterval();return;}var plans=bindings.Select(b=>new{Binding=b,Commands=framePlan(b,output,firstFrame)}).ToArray();foreach(var target in plans)RequireFramePlan(target.Binding,target.Commands);foreach(var target in plans){foreach(var command in target.Commands){CheckAlive(target.Binding);demand();CheckAlive(target.Binding);long at=clock();if(firstDispatch<0)firstDispatch=at;lastDispatch=at;var reply=await send(command);if(reply!=null)Report.AnyWriteAttempted|=reply.WriteAttempted;CheckAlive(target.Binding);if(reply==null||!reply.Ok)throw new InvalidOperationException(reply==null?"No frame response.":reply.Error??"Lighting frame rejected.");if(command.Backend!="direct"&&!reply.WriteConfirmed)throw new InvalidOperationException("Lighting frame was not confirmed; animation stopped without retry.");Report.Commands++;allConfirmed&=reply.WriteConfirmed;}}
    long completed=clock();if(completed<started)throw new InvalidOperationException("The animation clock moved backwards.");Report.LastTransactionMilliseconds=completed-started;Report.LastDispatchSkewMilliseconds=Math.Max(0,lastDispatch-firstDispatch);Report.AllLastFrameReadbackConfirmed=allConfirmed;Report.LastColor=output;Report.Frames++;Report.ObservedFramesPerSecond=lastCompleted>=0&&completed>lastCompleted?1000.0/(completed-lastCompleted):0;lastCompleted=completed;lastColor=output;firstFrame=false;nextDue=completed+MinimumInterval();
   }catch(OperationCanceledException){Report.State="stopped";}catch(Exception error){Fault(error.GetBaseException().Message);}finally{ticking=false;if(Report.State=="stopping")Report.State="stopped";}
  }
  int MinimumInterval(){return Math.Max(200,(int)Math.Ceiling(1000/options.FramesPerSecond));}
  void Fault(string error){Report.Error=error;Report.State="faulted";if(cancellation!=null)cancellation.Cancel();}
  public async Task RunAsync(){if(Report.State!="running"||looping)return;looping=true;try{while(Report.State=="running"){await TickAsync();if(Report.State!="running")break;long remaining=nextDue-clock();await delay((int)Math.Max(25,Math.Min(60000,remaining)),cancellation.Token);}}catch(OperationCanceledException){if(Report.State!="faulted")Report.State="stopped";}catch(Exception error){Fault(error.GetBaseException().Message);}finally{looping=false;if(Report.State=="stopping"||Report.State=="running")Report.State="stopped";}}
 }
}
