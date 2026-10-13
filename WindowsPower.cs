// SPDX-License-Identifier: MIT
// Independently implemented from documented Windows power-management APIs.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RogControl {
 public enum WindowsProcessorBoostMode : uint {
  Disabled=0,Enabled=1,Aggressive=2,EfficientEnabled=3,EfficientAggressive=4,AggressiveAtGuaranteed=5,EfficientAggressiveAtGuaranteed=6
 }
 public sealed class WindowsPowerScheme {
  public Guid Id {get;private set;}
  public string Name {get;private set;}
  public WindowsPowerScheme(Guid id,string name){Id=id;Name=name??"";}
 }
 public sealed class WindowsPowerBoostValues {
  public Guid Scheme {get;internal set;}
  public uint? AcBoost {get;internal set;}
  public uint? DcBoost {get;internal set;}
  public uint? RawAcBoost {get;internal set;}
  public uint? RawDcBoost {get;internal set;}
  public string AcError {get;internal set;}
  public string DcError {get;internal set;}
  public DateTime CapturedUtc {get;internal set;}
 }
 public sealed class WindowsPowerSnapshot {
  public Guid ActiveScheme {get;internal set;}
  public ReadOnlyCollection<WindowsPowerScheme> Schemes {get;internal set;}
  public WindowsPowerBoostValues Boost {get;internal set;}
  public bool Consistent {get;internal set;}
  public DateTime CapturedUtc {get;internal set;}
 }
 public sealed class WindowsPowerChangeResult {
  public bool Succeeded {get;internal set;}
  public bool Changed {get;internal set;}
  public bool WriteAttempted {get;internal set;}
  public string Error {get;internal set;}
  public uint? NativeErrorCode {get;internal set;}
  public bool RollbackAttempted {get;internal set;}
  public bool RollbackSucceeded {get;internal set;}
  public string RollbackError {get;internal set;}
  public WindowsPowerSnapshot Before {get;internal set;}
  public WindowsPowerSnapshot After {get;internal set;}
  public WindowsPowerRestorePoint RestorePoint {get;internal set;}
 }
 // Restore points are issued by one service instance and require an explicit
 // Restore call. Disposal, startup, timers and read methods never apply them.
 public sealed class WindowsPowerRestorePoint {
  internal Guid Owner;
  internal bool SchemeOnly;
  public Guid OriginalScheme {get;internal set;}
  public Guid ExpectedScheme {get;internal set;}
  public uint? OriginalAcBoost {get;internal set;}
  public uint? OriginalDcBoost {get;internal set;}
  public uint? ExpectedAcBoost {get;internal set;}
  public uint? ExpectedDcBoost {get;internal set;}
  internal WindowsPowerRestorePoint(){}
 }
 public sealed class WindowsPowerNativeException : Exception {
  public uint ErrorCode {get;private set;}
  public string Operation {get;private set;}
  public WindowsPowerNativeException(string operation,uint code):base("Windows 电源操作失败（"+code+"）："+new Win32Exception(unchecked((int)code)).Message){Operation=operation;ErrorCode=code;}
 }
 public interface IWindowsPowerBackend {
  IList<WindowsPowerScheme> EnumerateSchemes();
  Guid GetActiveScheme();
  uint ReadBoost(Guid scheme,bool ac);
  void WriteBoost(Guid scheme,bool ac,uint value);
  void SetActiveScheme(Guid scheme);
 }

 public sealed class WindowsPowerNativeBackend : IWindowsPowerBackend {
  public static readonly Guid ProcessorSubgroup=new Guid("54533251-82be-4824-96c1-47b60b740d00");
  public static readonly Guid ProcessorBoostSetting=new Guid("be337238-0d82-4146-a960-4f3749d470c7");
  const uint AccessScheme=16,MoreData=234,NoMoreItems=259;
  [DllImport("powrprof.dll")]static extern uint PowerEnumerate(IntPtr root,IntPtr scheme,IntPtr subgroup,uint access,uint index,[Out] byte[] buffer,ref uint size);
  [DllImport("powrprof.dll")]static extern uint PowerReadFriendlyName(IntPtr root,ref Guid scheme,IntPtr subgroup,IntPtr setting,[Out] byte[] buffer,ref uint size);
  [DllImport("powrprof.dll")]static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr scheme);
  [DllImport("powrprof.dll")]static extern uint PowerSetActiveScheme(IntPtr root,ref Guid scheme);
  [DllImport("powrprof.dll")]static extern uint PowerReadACValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,out uint value);
  [DllImport("powrprof.dll")]static extern uint PowerReadDCValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,out uint value);
  [DllImport("powrprof.dll")]static extern uint PowerWriteACValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,uint value);
  [DllImport("powrprof.dll")]static extern uint PowerWriteDCValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,uint value);
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr LocalFree(IntPtr memory);
  static void Success(string operation,uint result){if(result!=0)throw new WindowsPowerNativeException(operation,result);}

  public IList<WindowsPowerScheme> EnumerateSchemes(){
   var schemes=new List<WindowsPowerScheme>();var seen=new HashSet<Guid>();
   for(uint index=0;index<4096;index++){
    byte[] bytes=new byte[16];uint size=16;uint result=PowerEnumerate(IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,AccessScheme,index,bytes,ref size);
    if(result==NoMoreItems)return schemes;
    Success("PowerEnumerate",result);
    if(size!=16)throw new InvalidOperationException("Windows 返回了无效的电源方案标识。");
    var id=new Guid(bytes);if(id==Guid.Empty||!seen.Add(id))throw new InvalidOperationException("电源方案列表已变更，请重新读取。");
    schemes.Add(new WindowsPowerScheme(id,ReadName(id)));
   }
   throw new InvalidOperationException("电源方案列表超出读取范围。");
  }
  string ReadName(Guid scheme){
   uint size=0;uint result=PowerReadFriendlyName(IntPtr.Zero,ref scheme,IntPtr.Zero,IntPtr.Zero,null,ref size);
   if(result!=0&&result!=MoreData)Success("PowerReadFriendlyName",result);
   for(int attempt=0;attempt<3;attempt++){
    if(size<2||size>16384||(size&1)!=0)throw new InvalidOperationException("Windows 返回了无效的电源方案名称。");
    byte[] bytes=new byte[(int)size];uint readSize=size;
    result=PowerReadFriendlyName(IntPtr.Zero,ref scheme,IntPtr.Zero,IntPtr.Zero,bytes,ref readSize);
    if(result==MoreData){size=readSize;continue;}
    Success("PowerReadFriendlyName",result);
    if(readSize>bytes.Length||(readSize&1)!=0)throw new InvalidOperationException("Windows 返回了无效的电源方案名称。");
    string value=Encoding.Unicode.GetString(bytes,0,(int)readSize);int terminator=value.IndexOf('\0');if(terminator>=0)value=value.Substring(0,terminator);
    return new string(value.Where(c=>!Char.IsControl(c)).Take(240).ToArray()).Trim();
   }
   throw new InvalidOperationException("电源方案名称已变更，请重新读取。");
  }
  public Guid GetActiveScheme(){
   IntPtr memory=IntPtr.Zero;
   try{Success("PowerGetActiveScheme",PowerGetActiveScheme(IntPtr.Zero,out memory));if(memory==IntPtr.Zero)throw new InvalidOperationException("Windows 未返回当前电源方案。");return (Guid)Marshal.PtrToStructure(memory,typeof(Guid));}
   finally{if(memory!=IntPtr.Zero)LocalFree(memory);}
  }
  public uint ReadBoost(Guid scheme,bool ac){uint value;Guid subgroup=ProcessorSubgroup,setting=ProcessorBoostSetting;uint result=ac?PowerReadACValueIndex(IntPtr.Zero,ref scheme,ref subgroup,ref setting,out value):PowerReadDCValueIndex(IntPtr.Zero,ref scheme,ref subgroup,ref setting,out value);Success(ac?"PowerReadACValueIndex":"PowerReadDCValueIndex",result);return value;}
  public void WriteBoost(Guid scheme,bool ac,uint value){if(value>6)throw new ArgumentOutOfRangeException("value");Guid subgroup=ProcessorSubgroup,setting=ProcessorBoostSetting;Success(ac?"PowerWriteACValueIndex":"PowerWriteDCValueIndex",ac?PowerWriteACValueIndex(IntPtr.Zero,ref scheme,ref subgroup,ref setting,value):PowerWriteDCValueIndex(IntPtr.Zero,ref scheme,ref subgroup,ref setting,value));}
  public void SetActiveScheme(Guid scheme){if(scheme==Guid.Empty)throw new ArgumentException("scheme");Success("PowerSetActiveScheme",PowerSetActiveScheme(IntPtr.Zero,ref scheme));}
 }

 public sealed class WindowsPowerService {
  sealed class BoostMutation {public bool Ac,Attempted;public uint Original,Target,Expected;}
  readonly IWindowsPowerBackend backend;
  readonly object operationGate=new object();
  readonly Guid owner=Guid.NewGuid();
  public WindowsPowerService():this(new WindowsPowerNativeBackend()){}
  public WindowsPowerService(IWindowsPowerBackend value){if(value==null)throw new ArgumentNullException("value");backend=value;}
  public static bool IsBoostMode(uint value){return value<=6;}

  List<WindowsPowerScheme> Schemes(){
   var values=backend.EnumerateSchemes();if(values==null||values.Count>4096||values.Any(s=>s==null||s.Id==Guid.Empty)||values.Select(s=>s.Id).Distinct().Count()!=values.Count)throw new InvalidOperationException("Windows 返回了无效的电源方案列表。");return values.ToList();
  }
  static void Listed(IList<WindowsPowerScheme> schemes,Guid id){if(id==Guid.Empty||!schemes.Any(s=>s.Id==id))throw new InvalidOperationException("此电源方案不在当前列表中。");}
  void Active(Guid expected){if(expected==Guid.Empty||backend.GetActiveScheme()!=expected)throw new InvalidOperationException("电源方案已变更，请重新读取。");}
  WindowsPowerBoostValues ReadValues(Guid id){
   var values=new WindowsPowerBoostValues{Scheme=id,CapturedUtc=DateTime.UtcNow};
   try{uint value=backend.ReadBoost(id,true);values.RawAcBoost=value;if(IsBoostMode(value))values.AcBoost=value;else values.AcError="Windows 返回了不支持的 Boost 值（"+value+"）。";}catch(Exception ex){values.AcError=ex.Message;}
   try{uint value=backend.ReadBoost(id,false);values.RawDcBoost=value;if(IsBoostMode(value))values.DcBoost=value;else values.DcError="Windows 返回了不支持的 Boost 值（"+value+"）。";}catch(Exception ex){values.DcError=ex.Message;}
   return values;
  }
  WindowsPowerSnapshot Capture(Guid expected,IList<WindowsPowerScheme> schemes){
   return new WindowsPowerSnapshot{ActiveScheme=expected,Schemes=new List<WindowsPowerScheme>(schemes).AsReadOnly(),Boost=ReadValues(expected),CapturedUtc=DateTime.UtcNow,Consistent=backend.GetActiveScheme()==expected};
  }
  public WindowsPowerSnapshot ReadSnapshot(){lock(operationGate){Guid active=backend.GetActiveScheme();var schemes=Schemes();var snapshot=Capture(active,schemes);snapshot.Consistent=snapshot.Consistent&&schemes.Any(s=>s.Id==active);return snapshot;}}
  public WindowsPowerBoostValues ReadCpuBoost(Guid scheme){lock(operationGate){Listed(Schemes(),scheme);return ReadValues(scheme);}}
  static WindowsPowerChangeResult Failure(Exception error,WindowsPowerSnapshot before=null){var native=error as WindowsPowerNativeException;return new WindowsPowerChangeResult{Error=error.Message,NativeErrorCode=native==null?(uint?)null:native.ErrorCode,Before=before};}
  WindowsPowerSnapshot AfterOrNull(){try{return ReadSnapshot();}catch{return null;}}

  public WindowsPowerChangeResult ChangeActiveScheme(Guid target,Guid expectedActive){lock(operationGate){
   WindowsPowerSnapshot before=null;bool attempted=false;
   try{
    var schemes=Schemes();Listed(schemes,expectedActive);Listed(schemes,target);Active(expectedActive);before=Capture(expectedActive,schemes);Active(expectedActive);
    if(!before.Consistent)throw new InvalidOperationException("电源方案已变更，请重新读取。");
    if(target==expectedActive)return new WindowsPowerChangeResult{Succeeded=true,Before=before,After=before};
    // Refresh enumeration as well as active identity immediately before the
    // manual switch; a UI-provided GUID never authorizes an unlisted plan.
    Listed(Schemes(),target);Active(expectedActive);attempted=true;backend.SetActiveScheme(target);Active(target);
    var after=ReadSnapshot();if(!after.Consistent||after.ActiveScheme!=target)throw new InvalidOperationException("电源方案读回与请求不一致。");
    return new WindowsPowerChangeResult{Succeeded=true,Changed=true,WriteAttempted=true,Before=before,After=after,RestorePoint=new WindowsPowerRestorePoint{Owner=owner,SchemeOnly=true,OriginalScheme=expectedActive,ExpectedScheme=target}};
   }catch(Exception ex){
    var result=Failure(ex,before);result.WriteAttempted=attempted;
    if(attempted){result.RollbackAttempted=true;try{RollbackScheme(expectedActive,target);result.RollbackSucceeded=true;}catch(Exception rollback){result.RollbackError=rollback.Message;}}
    result.After=AfterOrNull();result.Changed=attempted&&result.After!=null&&result.After.ActiveScheme==target&&target!=expectedActive;
    if(attempted&&result.After!=null&&result.After.Consistent&&result.After.ActiveScheme==target)result.RestorePoint=new WindowsPowerRestorePoint{Owner=owner,SchemeOnly=true,OriginalScheme=expectedActive,ExpectedScheme=target};
    return result;
   }
  }}
  void RollbackScheme(Guid original,Guid target){
   Guid active=backend.GetActiveScheme();if(active==original)return;
   if(active!=target)throw new InvalidOperationException("其他程序已更改电源方案，未覆盖其选择。");
   Listed(Schemes(),original);Active(target);backend.SetActiveScheme(original);Active(original);
  }

  public WindowsPowerChangeResult ChangeCpuBoost(Guid expectedActive,uint? ac,uint? dc){lock(operationGate){return ChangeCpuBoostCore(expectedActive,ac,dc,null,null,false);}}
  WindowsPowerChangeResult ChangeCpuBoostCore(Guid expectedActive,uint? ac,uint? dc,uint? expectedAc,uint? expectedDc,bool restoring){
   WindowsPowerSnapshot before=null;var changes=new List<BoostMutation>();
   try{
    if(!ac.HasValue&&!dc.HasValue)throw new ArgumentException("请指定 AC 或 DC 的 CPU Boost 值。");
    if(ac.HasValue&&!IsBoostMode(ac.Value)||dc.HasValue&&!IsBoostMode(dc.Value))throw new ArgumentOutOfRangeException("boost","CPU Boost 值必须为 0–6。");
    var schemes=Schemes();Listed(schemes,expectedActive);Active(expectedActive);before=Capture(expectedActive,schemes);Active(expectedActive);
    if(!before.Consistent)throw new InvalidOperationException("电源方案已变更，请重新读取。");
    if(restoring&&(expectedAc.HasValue&&before.Boost.AcBoost!=expectedAc||expectedDc.HasValue&&before.Boost.DcBoost!=expectedDc))throw new InvalidOperationException("CPU Boost 已被其他程序更改，未覆盖其数值。");
    if(ac.HasValue)changes.Add(Mutation(true,before.Boost.AcBoost,ac.Value));if(dc.HasValue)changes.Add(Mutation(false,before.Boost.DcBoost,dc.Value));
    GuardBoost(expectedActive,changes);
    if(changes.All(c=>c.Original==c.Target))return new WindowsPowerChangeResult{Succeeded=true,Before=before,After=before};
    foreach(var change in changes){
     if(change.Original==change.Target)continue;
     GuardBoost(expectedActive,changes);change.Attempted=true;backend.WriteBoost(expectedActive,change.Ac,change.Target);
     if(backend.ReadBoost(expectedActive,change.Ac)!=change.Target)throw new InvalidOperationException("CPU Boost 读回与请求不一致。");change.Expected=change.Target;Active(expectedActive);
    }
    GuardBoost(expectedActive,changes);backend.SetActiveScheme(expectedActive);GuardBoost(expectedActive,changes);
    var after=ReadSnapshot();if(!after.Consistent||after.ActiveScheme!=expectedActive)throw new InvalidOperationException("电源方案已变更，请重新读取。");
    foreach(var change in changes){uint? value=change.Ac?after.Boost.AcBoost:after.Boost.DcBoost;if(value!=change.Target)throw new InvalidOperationException("CPU Boost 读回与请求不一致。");}
    return new WindowsPowerChangeResult{Succeeded=true,Changed=true,WriteAttempted=true,Before=before,After=after,RestorePoint=BoostRestorePoint(expectedActive,changes)};
   }catch(Exception ex){
    var result=Failure(ex,before);result.WriteAttempted=changes.Any(c=>c.Attempted);
    if(result.WriteAttempted){result.RollbackAttempted=true;try{RollbackBoost(expectedActive,changes);result.RollbackSucceeded=true;}catch(Exception rollback){result.RollbackError=rollback.Message;}}
    result.After=AfterOrNull();if(result.WriteAttempted){result.RestorePoint=BoostRestorePoint(expectedActive,changes);result.Changed=result.RestorePoint!=null||!result.RollbackSucceeded;}
    return result;
   }
  }
  static BoostMutation Mutation(bool ac,uint? original,uint target){if(!original.HasValue||!IsBoostMode(original.Value))throw new InvalidOperationException("无法读取可恢复的原 CPU Boost 值，未更改设置。");return new BoostMutation{Ac=ac,Original=original.Value,Expected=original.Value,Target=target};}
  void GuardBoost(Guid scheme,IList<BoostMutation> changes){Active(scheme);Listed(Schemes(),scheme);foreach(var change in changes)if(backend.ReadBoost(scheme,change.Ac)!=change.Expected)throw new InvalidOperationException("CPU Boost 已被其他程序更改，请重新读取。");Active(scheme);}
  void RollbackBoost(Guid scheme,IList<BoostMutation> changes){
   var errors=new List<string>();bool restored=false;
   foreach(var change in changes.Reverse().Where(c=>c.Attempted)){
    try{
     Listed(Schemes(),scheme);uint value=backend.ReadBoost(scheme,change.Ac);if(value==change.Original)continue;
     if(value!=change.Target)throw new InvalidOperationException("CPU Boost 已被其他程序更改，未覆盖其数值。");
     backend.WriteBoost(scheme,change.Ac,change.Original);restored=true;if(backend.ReadBoost(scheme,change.Ac)!=change.Original)throw new InvalidOperationException("CPU Boost 原值恢复读回失败。");
    }catch(Exception ex){errors.Add((change.Ac?"AC":"DC")+"："+ex.Message);}
   }
   // Restore our explicit scheme values even if it became inactive, but
   // never reactivate it over another application's newly selected scheme.
   if(restored){try{if(backend.GetActiveScheme()==scheme){foreach(var change in changes.Where(c=>c.Attempted))if(backend.ReadBoost(scheme,change.Ac)!=change.Original)throw new InvalidOperationException("CPU Boost 未全部恢复，未重新应用方案。");Active(scheme);backend.SetActiveScheme(scheme);Active(scheme);}}catch(Exception ex){errors.Add(ex.Message);}}
   if(errors.Count>0)throw new InvalidOperationException(String.Join("；",errors));
  }
  WindowsPowerRestorePoint BoostRestorePoint(Guid scheme,IList<BoostMutation> changes){
   try{
    if(backend.GetActiveScheme()!=scheme)return null;var point=new WindowsPowerRestorePoint{Owner=owner,OriginalScheme=scheme,ExpectedScheme=scheme};bool remaining=false;
    foreach(var change in changes.Where(c=>c.Attempted)){
     uint value=backend.ReadBoost(scheme,change.Ac);if(value!=change.Original&&value!=change.Target)return null;if(value==change.Original)continue;remaining=true;
     if(change.Ac){point.OriginalAcBoost=change.Original;point.ExpectedAcBoost=value;}else{point.OriginalDcBoost=change.Original;point.ExpectedDcBoost=value;}
    }
    return remaining?point:null;
   }catch{return null;}
  }
  public WindowsPowerChangeResult Restore(WindowsPowerRestorePoint point){lock(operationGate){
   try{
    if(point==null||point.Owner!=owner)throw new ArgumentException("恢复记录无效或来自其他会话。");Active(point.ExpectedScheme);
    if(point.SchemeOnly)return ChangeActiveScheme(point.OriginalScheme,point.ExpectedScheme);
    if(point.ExpectedAcBoost.HasValue&&backend.ReadBoost(point.ExpectedScheme,true)!=point.ExpectedAcBoost.Value||point.ExpectedDcBoost.HasValue&&backend.ReadBoost(point.ExpectedScheme,false)!=point.ExpectedDcBoost.Value)throw new InvalidOperationException("CPU Boost 已被其他程序更改，未覆盖其数值。");
    Active(point.ExpectedScheme);return ChangeCpuBoostCore(point.ExpectedScheme,point.OriginalAcBoost,point.OriginalDcBoost,point.ExpectedAcBoost,point.ExpectedDcBoost,true);
   }catch(Exception ex){return Failure(ex);}
  }}
 }
}
