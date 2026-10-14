// SPDX-License-Identifier: MIT
// Pure coordinator/immutable-record tests. No window, HID, native input or clock waits.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RogControl;

public static class MouseCalibrationWorkflowTests {
 static int checks;
 static void Check(bool good,string label){checks++;if(!good)throw new Exception(label);}
 static Dictionary<string,object> Map(params object[] items){var map=new Dictionary<string,object>();for(int i=0;i<items.Length;i+=2)map.Add((string)items[i],items[i+1]);return map;}
 static Dictionary<string,object> Snapshot(int pid=7548,int mode=0,int lodMode=0,int manual=4){return Map("modelPid",pid,"mouseCalibrationInfo",Map("mode",mode,"material",7,"kValue",new[]{12,34,56}),"mouseCalibrationLOD",Map("lodMode",lodMode,"normalModeLOD",1,"asymmetricLiftValue",6,"asymmetricDropValue",3,"manualCalibrationLOD",manual,"sensorTriggered",true));}
 static Dictionary<string,object> Info(Dictionary<string,object> data){return (Dictionary<string,object>)data["mouseCalibrationInfo"];}
 static Dictionary<string,object> Lod(Dictionary<string,object> data){return (Dictionary<string,object>)data["mouseCalibrationLOD"];}
 sealed class Fake {
  public Dictionary<string,object> Data=Snapshot();
  public int SuccessAt=1,Reads,Writes,Delays;
  public long Clock,Movement;
  public bool MoveDuringWait=true,Cancelled,Closing,SessionChanged,Policy=true;
  public int ReadCost,WriteCost,ThrowReadAt=-1,ThrowWriteAt=-1,UnconfirmedWriteAt=-1;
  public Action<Fake> AfterRead,AfterWrite,DuringDelay,OnMovement;
  public Action<Fake,int> OnProgress;
  public List<int> Progress=new List<int>();
  public List<KeyValuePair<string,Dictionary<string,object>>> Commands=new List<KeyValuePair<string,Dictionary<string,object>>>();
  public List<long> CommandTimes=new List<long>();
  public bool Active(){return Policy&&!SessionChanged&&!Cancelled&&!Closing;}
  public bool RestoreAllowed(){return Policy&&!SessionChanged;}
  public Task<MouseCalibrationRecord> Read(){Reads++;Clock+=ReadCost;if(Reads==ThrowReadAt)throw new IOException("fake restore read failed");if(AfterRead!=null)AfterRead(this);var lod=Lod(Data);lod["sensorTriggered"]=(int)lod["manualCalibrationLOD"]<SuccessAt;return Task.FromResult(new MouseCalibrationRecord(Data));}
  public Task<MouseCalibrationRecord> Write(string setting,object value){
   Writes++;CommandTimes.Add(Clock);Clock+=WriteCost;Dictionary<string,object> submitted;
   if(setting=="mouseCalibrationMode"){submitted=Map("mode",value);Info(Data)["mode"]=value;}
   else if(setting=="mouseCalibrationLOD"){submitted=new Dictionary<string,object>((Dictionary<string,object>)value);foreach(var item in submitted)Lod(Data)[item.Key]=item.Value;}
   else throw new Exception("unexpected calibration setting");
   Commands.Add(new KeyValuePair<string,Dictionary<string,object>>(setting,submitted));if(AfterWrite!=null)AfterWrite(this);if(Writes==ThrowWriteAt)throw new IOException("fake restore write failed");if(Writes==UnconfirmedWriteAt)throw new IOException("fake write unconfirmed");return Task.FromResult(new MouseCalibrationRecord(Data));
  }
  public Task Delay(int milliseconds){Check(milliseconds==2000,"real per-trial sensing interval remains 2000ms");Delays++;Clock+=milliseconds;if(MoveDuringWait)Movement++;if(DuringDelay!=null)DuringDelay(this);return Task.FromResult(0);}
  public long Motion(){if(OnMovement!=null)OnMovement(this);return Movement;}
  public void Report(int level){Progress.Add(level);if(OnProgress!=null)OnProgress(this,level);}
  public MouseCalibrationOutcome Run(){return MouseCalibrationCoordinator.Run(Read,Write,Active,RestoreAllowed,Motion,Delay,()=>Clock,Report).GetAwaiter().GetResult();}
  public int TrialWrites(){return Commands.Count(item=>item.Key=="mouseCalibrationLOD"&&(int)item.Value["lodMode"]==2&&(int)item.Value["manualCalibrationLOD"]>=1);}
 }
 static void Success(){
  for(int level=1;level<=11;level++)foreach(int mode in new[]{0,255}){var fake=new Fake{SuccessAt=level,Data=Snapshot(mode:mode)};var original=new MouseCalibrationRecord(fake.Data);var result=fake.Run();Check(result.Completed&&result.Level==level&&!result.Restored&&result.WriteAttempted,"successful sensor threshold is accepted at each legal level "+level);Check(fake.Progress.SequenceEqual(Enumerable.Range(1,level))&&fake.Delays==level,"trial order and sensing windows remain exact");Check(fake.Reads==1+level&&fake.Writes==level+(mode==0?1:0),"no extra scan, read or recovery after success");var final=new MouseCalibrationRecord(fake.Data);Check(original.Preserved(final)&&final.Mode==255&&final.LodMode==2&&final.Manual==level,"successful outcome changes only intended mode/LOD fields");Check(fake.Clock<MouseCalibrationCoordinator.TrialDeadlineMs&&fake.CommandTimes.All(time=>time<MouseCalibrationCoordinator.DeadlineMs),"success remains within declared deadlines");}
  var usb=new Fake{Data=Snapshot(7546),SuccessAt=3};Check(usb.Run().Completed,"exact USB model uses same scoped workflow");
 }
 static void CancellationAndMovement(){
  var startCancelled=new Fake{Cancelled=true};var result=startCancelled.Run();Check(!result.Completed&&!result.WriteAttempted&&!result.Restored&&startCancelled.Reads==0&&startCancelled.Writes==0,"cancel before start performs no device delegates");
  var still=new Fake{MoveDuringWait=false};var baseline=new MouseCalibrationRecord(still.Data);result=still.Run();Check(!result.Completed&&result.Restored&&result.WriteAttempted&&baseline.Same(new MouseCalibrationRecord(still.Data)),"no movement cancels and restores original fields");Check(still.Delays==1,"no-movement stop does not test more levels");
  var priorMotion=new Fake{MoveDuringWait=false,AfterWrite=f=>f.Movement++};result=priorMotion.Run();Check(!result.Completed&&result.Restored,"movement during SET I/O alone does not satisfy the sensing interval");
  foreach(string reason in new[]{"cancel","closing"}){var fake=new Fake{DuringDelay=f=>{if(reason=="cancel")f.Cancelled=true;else f.Closing=true;}};baseline=new MouseCalibrationRecord(fake.Data);result=fake.Run();Check(!result.Completed&&result.Restored&&baseline.Same(new MouseCalibrationRecord(fake.Data)),"cancel/closing waits for restoration on original authorized session: "+reason);Check(fake.Delays==1,"cancel/close does not begin next trial");}
  var atProgress=new Fake{OnProgress=(f,level)=>f.Cancelled=true};result=atProgress.Run();Check(!result.Completed&&result.Restored&&atProgress.Commands.Count(item=>item.Key=="mouseCalibrationLOD"&&(int)item.Value["lodMode"]==2)==0,"progress callback cancellation is demanded before trial SET");
  var duringSet=new Fake{AfterWrite=f=>{if(f.Writes==2)f.Cancelled=true;}};result=duringSet.Run();Check(!result.Completed&&result.Restored&&duringSet.Delays==0,"cancel during trial SET starts no sensing wait");
  var inMotion=new Fake{OnMovement=f=>f.Cancelled=true};result=inMotion.Run();Check(!result.Completed&&result.Restored&&inMotion.Delays==0,"cancellation when movement baseline is sampled starts no wait");
  foreach(string reason in new[]{"cancel","session","policy"}){var afterMotion=new Fake();afterMotion.OnMovement=f=>{if(f.Delays==0)return;if(reason=="cancel")f.Cancelled=true;else if(reason=="session")f.SessionChanged=true;else f.Policy=false;};result=afterMotion.Run();Check(!result.Completed&&result.Restored==(reason=="cancel"),"state changed by post-wait movement sample is rechecked before sensor GET: "+reason);Check(afterMotion.Reads==(reason=="cancel"?3:1),"post-wait cancellation/session/policy change performs no trial sensor read: "+reason);}
 }
 static void SessionPolicyAndUnconfirmed(){
  foreach(string reason in new[]{"session","policy"})foreach(string phase in new[]{"start","set","wait","read"}){
   var fake=new Fake();Action<Fake> flip=f=>{if(reason=="session")f.SessionChanged=true;else f.Policy=false;};if(phase=="start")flip(fake);else if(phase=="set")fake.AfterWrite=f=>{if(f.Writes==2)flip(f);};else if(phase=="wait")fake.DuringDelay=flip;else fake.AfterRead=f=>{if(f.Reads==2)flip(f);};var result=fake.Run();Check(!result.Completed&&!result.Restored,"session/policy loss cannot claim completion or restoration: "+reason+"/"+phase);if(phase=="start")Check(fake.Reads==0&&fake.Writes==0,"invalid initial session grants no delegates");else Check(fake.Writes==2,"session/policy loss grants no subsequent recovery writes");}
  var unconfirmed=new Fake{UnconfirmedWriteAt=2};var original=new MouseCalibrationRecord(unconfirmed.Data);var outcome=unconfirmed.Run();Check(!outcome.Completed&&outcome.Restored&&outcome.Message.Contains("unconfirmed")&&original.Same(new MouseCalibrationRecord(unconfirmed.Data)),"unconfirmed write cannot succeed; verified original-session rollback remains possible");
  var noRead=new Fake{ThrowReadAt=1};outcome=noRead.Run();Check(!outcome.Completed&&!outcome.WriteAttempted&&noRead.Writes==0,"missing initial recipe causes no SET or invented restoration");
 }
 static void DeadlinesAndRecovery(){
  var quickIo=new Fake{SuccessAt=11,ReadCost=10,WriteCost=10};var quickResult=quickIo.Run();Check(quickResult.Completed&&quickResult.Level==11&&quickIo.Clock==11L*2000+(quickIo.Reads+quickIo.Writes)*10L,"all eleven sensing windows remain possible with bounded nonzero I/O latency");Check(quickIo.CommandTimes.All(time=>time<MouseCalibrationCoordinator.TrialDeadlineMs),"fast successful I/O starts every command within trial budget");
  var slowIo=new Fake{SuccessAt=11,ReadCost=1000,WriteCost=1000};var slowResult=slowIo.Run();Check(!slowResult.Completed&&!slowResult.Restored&&slowResult.Message.Contains("恢复未确认"),"slow I/O exhausts the budget without claiming calibration or recovery");Check(slowIo.CommandTimes.All(time=>time<MouseCalibrationCoordinator.DeadlineMs),"slow I/O never starts another write at or after total deadline");Check(slowIo.Delays<11&&slowIo.Clock>=MouseCalibrationCoordinator.DeadlineMs,"I/O time reduces trial count and recovery remains bounded by the total deadline");
  var lateStart=new Fake{Clock=MouseCalibrationCoordinator.TrialDeadlineMs};var result=lateStart.Run();Check(!result.Completed&&lateStart.Reads==0&&lateStart.Writes==0,"trial deadline reached before start blocks all I/O");
  var timed=new Fake{SuccessAt=12};timed.DuringDelay=f=>{if(f.Delays==1)f.Clock=MouseCalibrationCoordinator.TrialDeadlineMs;};var original=new MouseCalibrationRecord(timed.Data);result=timed.Run();Check(!result.Completed&&result.Restored&&original.Same(new MouseCalibrationRecord(timed.Data)),"trial deadline stops new trials but retains bounded recovery reserve");
  var total=new Fake{DuringDelay=f=>f.Clock=MouseCalibrationCoordinator.DeadlineMs};result=total.Run();Check(!result.Completed&&!result.Restored&&total.Reads==1&&total.Writes==2,"exhausted total deadline cannot read/write restoration");
  var restoreReadLate=new Fake{MoveDuringWait=false};restoreReadLate.AfterRead=f=>{if(f.Reads==2)f.Clock=MouseCalibrationCoordinator.DeadlineMs;};result=restoreReadLate.Run();Check(!result.Completed&&!result.Restored&&restoreReadLate.Writes==2,"recovery read crossing total deadline starts no recovery SET");
  var restoreWriteLate=new Fake{MoveDuringWait=false};restoreWriteLate.AfterWrite=f=>{if(f.Writes==3)f.Clock=MouseCalibrationCoordinator.DeadlineMs;};result=restoreWriteLate.Run();Check(!result.Restored&&restoreWriteLate.Writes==3&&restoreWriteLate.CommandTimes.All(time=>time<MouseCalibrationCoordinator.DeadlineMs),"recovery write crossing deadline cannot start next mode SET");
  var firstRestoreRead=new Fake{MoveDuringWait=false,ThrowReadAt=2};result=firstRestoreRead.Run();Check(!result.Restored&&firstRestoreRead.Writes==2&&result.Message.Contains("恢复未确认"),"restoration read I/O failure is explicit and cannot continue writing");
  var restoreWrite=new Fake{MoveDuringWait=false,ThrowWriteAt=3};result=restoreWrite.Run();Check(!result.Restored&&restoreWrite.Writes==3&&result.Message.Contains("恢复未确认"),"restoration write failure is reported without further writes");
  var policyDuringRestore=new Fake{MoveDuringWait=false,AfterRead=f=>{if(f.Reads==2)f.Policy=false;}};result=policyDuringRestore.Run();Check(!result.Restored&&policyDuringRestore.Writes==2,"policy loss during recovery GET prevents recovery SET");
  var sessionAfterRestore=new Fake{MoveDuringWait=false,AfterWrite=f=>{if(f.Writes==3)f.SessionChanged=true;}};result=sessionAfterRestore.Run();Check(!result.Restored&&sessionAfterRestore.Writes==3,"session loss during recovery write prevents further writes/readback claim");
  var unreachable=new Fake{SuccessAt=12};result=unreachable.Run();Check(!result.Completed&&result.Restored&&unreachable.Delays==11,"eleven fully tested levels without threshold recover rather than claim success");
 }
 static void ChangedFields(){
  foreach(string field in new[]{"material","kValue","normalModeLOD","asymmetricLiftValue","asymmetricDropValue"}){var fake=new Fake();fake.DuringDelay=f=>{if(field=="material")Info(f.Data)[field]=8;else if(field=="kValue")Info(f.Data)[field]=new[]{12,34,57};else Lod(f.Data)[field]=(int)Lod(f.Data)[field]+1;};var result=fake.Run();Check(!result.Completed&&!result.Restored&&fake.Writes==2&&result.Message.Contains("参数"),"external unowned parameter change blocks automatic overwrite: "+field);}
  var changedMode=new Fake{DuringDelay=f=>Info(f.Data)["mode"]=0};var original=new MouseCalibrationRecord(changedMode.Data);var outcome=changedMode.Run();Check(!outcome.Completed&&outcome.Restored&&original.Same(new MouseCalibrationRecord(changedMode.Data)),"unexpected owned mode stops trial and verifies exact rollback");
  var malformedSet=new Fake{AfterWrite=f=>{if(f.Writes==2)Lod(f.Data)["manualCalibrationLOD"]=11;}};outcome=malformedSet.Run();Check(!outcome.Completed&&outcome.Restored&&malformedSet.Delays==0,"wrong returned trial level is rejected before movement sensing");
 }
 static void Records(){
  var source=Snapshot();var original=new MouseCalibrationRecord(source);((int[])Info(source)["kValue"])[0]=200;Check(original.Preserved(new MouseCalibrationRecord(Snapshot())),"record owns a cloned calibration recipe");var submitted=original.LodValue(2,5);submitted["normalModeLOD"]=2;submitted["asymmetricLiftValue"]=11;Check(original.Normal==1&&original.Lift==6&&original.Preserved(new MouseCalibrationRecord(Snapshot())),"mutable submitted dictionary cannot mutate original restoration recipe");
  Check(!original.Same(new MouseCalibrationRecord(Snapshot(7546))),"different exact transport model cannot masquerade as same record");
  foreach(int mode in new[]{0,1,2}){var data=Snapshot(lodMode:mode);Check(new MouseCalibrationRecord(data).Restorable,"valid baseline for each LOD mode");}
  foreach(Action<Dictionary<string,object>> corrupt in new Action<Dictionary<string,object>>[]{data=>Lod(data)["lodMode"]=1,data=>Lod(data)["lodMode"]=2}){var data=Snapshot();corrupt(data);if((int)Lod(data)["lodMode"]==1){Lod(data)["asymmetricDropValue"]=7;}else Lod(data)["manualCalibrationLOD"]=0;var record=new MouseCalibrationRecord(data);Check(!record.Restorable,"contradictory active baseline is not restorable");var fake=new Fake{Data=data};var result=fake.Run();Check(!result.Completed&&!result.WriteAttempted&&fake.Reads==1&&fake.Writes==0,"unrestorable recipe blocks first SET");}
  foreach(object pid in new object[]{0,7549,7588,"7548",7548.0,true,null}){var data=Snapshot();data["modelPid"]=pid;bool rejected=false;try{new MouseCalibrationRecord(data);}catch(ArgumentException){rejected=true;}Check(rejected,"only strictly typed prepared model accepted");}
  foreach(string key in new[]{"lodMode","normalModeLOD","asymmetricLiftValue","asymmetricDropValue","manualCalibrationLOD"})foreach(object invalid in new object[]{null,-1,256,"1",1.0,true}){var data=Snapshot();Lod(data)[key]=invalid;bool rejected=false;try{new MouseCalibrationRecord(data);}catch(ArgumentException){rejected=true;}Check(rejected,"bounded typed LOD field: "+key);}
  foreach(object values in new object[]{null,"12,34,56",new int[0],new[]{1,2},new[]{1,2,3,4},new object[]{1,2,"3"},new[]{1,-1,3},new[]{1,2,256}}){var data=Snapshot();Info(data)["kValue"]=values;bool rejected=false;try{new MouseCalibrationRecord(data);}catch(ArgumentException){rejected=true;}Check(rejected,"recipe must be exactly three complete bytes");}
  var zeros=Snapshot(mode:255,lodMode:0,manual:0);Info(zeros)["material"]=0;Info(zeros)["kValue"]=new[]{0,0,0};Lod(zeros)["normalModeLOD"]=0;Lod(zeros)["asymmetricLiftValue"]=0;Lod(zeros)["asymmetricDropValue"]=0;Lod(zeros)["sensorTriggered"]=false;Check(new MouseCalibrationRecord(zeros).Restorable&&!new MouseCalibrationRecord(zeros).Triggered,"inactive zero fields and false sensor are valid, not fabricated missing values");
 }
 public static int Main(){try{Records();Success();CancellationAndMovement();SessionPolicyAndUnconfirmed();DeadlinesAndRecovery();ChangedFields();Console.WriteLine("Mouse calibration workflow: "+checks+" pure engine checks; fake I/O/clock/movement only, native/HID/GUI=0.");return 0;}catch(Exception error){Console.Error.WriteLine(error.GetType().Name+": "+error.Message);return 1;}}
}
