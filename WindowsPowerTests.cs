// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using RogControl;

public static class WindowsPowerTests {
 static int checks;
 static readonly Guid A=new Guid("11111111-1111-1111-1111-111111111111"),B=new Guid("22222222-2222-2222-2222-222222222222"),C=new Guid("33333333-3333-3333-3333-333333333333");
 sealed class Fake : IWindowsPowerBackend {
  public Guid Active=A;
  public List<WindowsPowerScheme> Plans=new List<WindowsPowerScheme>{new WindowsPowerScheme(A,"Scheme A"),new WindowsPowerScheme(B,"Scheme B"),new WindowsPowerScheme(C,"Scheme C")};
  public readonly Dictionary<Guid,uint[]> Boost=new Dictionary<Guid,uint[]>{{A,new uint[]{2,3}},{B,new uint[]{0,1}},{C,new uint[]{4,5}}};
  public readonly Dictionary<string,int> Calls=new Dictionary<string,int>();
  public readonly Dictionary<string,uint> ErrorsBefore=new Dictionary<string,uint>(),ErrorsAfter=new Dictionary<string,uint>();
  public readonly List<string> Writes=new List<string>();
  public Action<Fake,string,int> Before;
  public bool IgnoreBoostWrites,IgnoreSchemeWrites;
  int Hit(string tag){int count;Calls.TryGetValue(tag,out count);Calls[tag]=++count;if(Before!=null)Before(this,tag,count);uint error;if(ErrorsBefore.TryGetValue(tag+":"+count,out error))throw new WindowsPowerNativeException(tag,error);return count;}
  void Finish(string tag,int count){uint error;if(ErrorsAfter.TryGetValue(tag+":"+count,out error))throw new WindowsPowerNativeException(tag,error);}
  public IList<WindowsPowerScheme> EnumerateSchemes(){int count=Hit("Enum");var result=Plans.ToList();Finish("Enum",count);return result;}
  public Guid GetActiveScheme(){int count=Hit("Active");var result=Active;Finish("Active",count);return result;}
  public uint ReadBoost(Guid scheme,bool ac){string tag=ac?"ReadAC":"ReadDC";int count=Hit(tag);uint result=Boost[scheme][ac?0:1];Finish(tag,count);return result;}
  public void WriteBoost(Guid scheme,bool ac,uint value){string tag=ac?"WriteAC":"WriteDC";int count=Hit(tag);Writes.Add(tag+" "+scheme+" "+value);if(!IgnoreBoostWrites)Boost[scheme][ac?0:1]=value;Finish(tag,count);}
  public void SetActiveScheme(Guid scheme){int count=Hit("Set");Writes.Add("Set "+scheme);if(!IgnoreSchemeWrites)Active=scheme;Finish("Set",count);}
 }
 static void Check(bool condition,string label){checks++;if(!condition)throw new Exception(label);}
 static int Called(Fake f,string key){int value;return f.Calls.TryGetValue(key,out value)?value:0;}
 static void NoWrites(Fake f,string label){Check(Called(f,"WriteAC")==0&&Called(f,"WriteDC")==0&&Called(f,"Set")==0,label);}
 static void Error(WindowsPowerChangeResult r,bool attempted,string label){Check(!r.Succeeded&&r.WriteAttempted==attempted&&!String.IsNullOrWhiteSpace(r.Error),label);}
 static void Pure(){
  Check(WindowsPowerNativeBackend.ProcessorSubgroup==new Guid("54533251-82be-4824-96c1-47b60b740d00"),"documented processor subgroup");Check(WindowsPowerNativeBackend.ProcessorBoostSetting==new Guid("be337238-0d82-4146-a960-4f3749d470c7"),"documented boost setting");Check((uint)WindowsProcessorBoostMode.EfficientAggressiveAtGuaranteed==6,"extended boost enum");
  var f=new Fake();var service=new WindowsPowerService(f);var snapshot=service.ReadSnapshot();Check(snapshot.Consistent&&snapshot.ActiveScheme==A&&snapshot.Schemes.Count==3,"read scheme catalogue and active identity");Check(snapshot.Boost.AcBoost==2&&snapshot.Boost.DcBoost==3&&snapshot.Boost.AcError==null&&snapshot.Boost.DcError==null,"read AC and DC independently");NoWrites(f,"read snapshot never writes");var other=service.ReadCpuBoost(B);Check(other.Scheme==B&&other.AcBoost==0&&other.DcBoost==1,"read listed inactive scheme without selecting it");NoWrites(f,"inactive reads never activate");
  f=new Fake();f.ErrorsBefore["ReadAC:1"]=5;service=new WindowsPowerService(f);snapshot=service.ReadSnapshot();Check(!snapshot.Boost.AcBoost.HasValue&&snapshot.Boost.DcBoost==3&&!String.IsNullOrWhiteSpace(snapshot.Boost.AcError),"AC access failure does not manufacture values or erase DC");NoWrites(f,"read access failure never writes");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="ReadDC"&&count==1)fake.Active=B;};snapshot=new WindowsPowerService(f).ReadSnapshot();Check(!snapshot.Consistent&&snapshot.ActiveScheme==A,"snapshot reports scheme race");NoWrites(f,"racing snapshot never writes");
  f=new Fake();f.Boost[A][0]=99;snapshot=new WindowsPowerService(f).ReadSnapshot();Check(snapshot.Boost.AcBoost==null&&snapshot.Boost.RawAcBoost==99&&!String.IsNullOrEmpty(snapshot.Boost.AcError),"future raw index stays diagnostic and cannot enable a known-mode control");
  f.Boost[A][1]=7;snapshot=new WindowsPowerService(f).ReadSnapshot();Check(snapshot.Boost.DcBoost==null&&snapshot.Boost.RawDcBoost==7&&!String.IsNullOrEmpty(snapshot.Boost.DcError),"undefined DC mode remains unknown");
  for(uint ac=0;ac<=6;ac++)for(uint dc=0;dc<=6;dc++){
   f=new Fake();service=new WindowsPowerService(f);var r=service.ChangeCpuBoost(A,ac,dc);bool changed=ac!=2||dc!=3;
   Check(r.Succeeded&&r.Changed==changed&&r.WriteAttempted==changed,"all legal AC/DC pairs succeed "+ac+"/"+dc);Check(f.Boost[A][0]==ac&&f.Boost[A][1]==dc&&f.Active==A,"requested pair readback "+ac+"/"+dc);Check(r.Before.Boost.AcBoost==2&&r.Before.Boost.DcBoost==3,"original pair captured "+ac+"/"+dc);
   if(changed){Check(r.RestorePoint!=null,"explicit restore receipt "+ac+"/"+dc);var restore=service.Restore(r.RestorePoint);Check(restore.Succeeded&&f.Boost[A][0]==2&&f.Boost[A][1]==3&&f.Active==A,"manual restore original pair "+ac+"/"+dc);}else{NoWrites(f,"no-op pair has no writes or reactivation");Check(r.RestorePoint==null,"no-op has no restore mutation");}
  }
  f=new Fake();service=new WindowsPowerService(f);var acOnly=service.ChangeCpuBoost(A,6,null);Check(acOnly.Succeeded&&f.Boost[A][0]==6&&f.Boost[A][1]==3&&Called(f,"WriteDC")==0,"AC-only leaves DC unchanged");Check(Called(f,"Set")==1,"changed active settings reapplied explicitly");Check(acOnly.RestorePoint.OriginalAcBoost==2&&!acOnly.RestorePoint.OriginalDcBoost.HasValue,"restore contains only owned changes");
  f=new Fake();service=new WindowsPowerService(f);var dcOnly=service.ChangeCpuBoost(A,null,0);Check(dcOnly.Succeeded&&f.Boost[A][0]==2&&f.Boost[A][1]==0&&Called(f,"WriteAC")==0,"DC-only leaves AC unchanged");
  foreach(uint invalid in new uint[]{7,99,UInt32.MaxValue}){f=new Fake();var invalidResult=new WindowsPowerService(f).ChangeCpuBoost(A,invalid,0);Error(invalidResult,false,"invalid AC rejected");NoWrites(f,"invalid AC never reaches native writer");f=new Fake();invalidResult=new WindowsPowerService(f).ChangeCpuBoost(A,0,invalid);Error(invalidResult,false,"invalid DC rejected");NoWrites(f,"invalid DC never reaches native writer");}
  f=new Fake();Error(new WindowsPowerService(f).ChangeCpuBoost(A,null,null),false,"missing change rejected");NoWrites(f,"empty request never writes");
  f=new Fake();f.Boost[A][0]=99;Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,null),false,"unknown original cannot be restored and must be rejected");NoWrites(f,"unknown original never replaced with guessed default");
  f=new Fake();f.ErrorsBefore["ReadDC:1"]=2;Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,1),false,"missing requested DC blocks whole transaction");NoWrites(f,"unreadable original blocks all writes");
  f=new Fake();f.ErrorsBefore["ReadDC:1"]=2;Check(new WindowsPowerService(f).ChangeCpuBoost(A,0,null).Succeeded&&Called(f,"WriteDC")==0,"unrequested unavailable channel does not block AC-only request");
  f=new Fake();f.Active=B;Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,1),false,"stale expected scheme rejected");NoWrites(f,"stale boost request never writes");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="Enum"&&count==2)fake.Active=B;};Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,1),false,"scheme race at final precheck rejected");NoWrites(f,"preflight scheme race never writes");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="Enum"&&count==2)fake.Boost[A][0]=4;};Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,1),false,"boost race at final precheck rejected");NoWrites(f,"preflight boost race never writes");Check(f.Boost[A][0]==4,"foreign preflight boost preserved");
  f=new Fake();f.ErrorsBefore["WriteAC:1"]=5;var fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Error(fail,true,"native AC failure is reported");Check(fail.NativeErrorCode==5&&fail.RollbackAttempted&&fail.RollbackSucceeded&&f.Boost[A][0]==2&&f.Boost[A][1]==3,"native denial leaves original values");
  f=new Fake();f.ErrorsBefore["WriteDC:1"]=5;fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Error(fail,true,"second-channel failure is reported");Check(fail.RollbackSucceeded&&f.Boost[A][0]==2&&f.Boost[A][1]==3,"partial AC success rolled back after DC failure");Check(fail.Before.Boost.AcBoost==2&&fail.Before.Boost.DcBoost==3&&fail.RestorePoint==null,"failed transaction keeps original snapshot after successful rollback");
  f=new Fake();f.ErrorsAfter["WriteAC:1"]=31;fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,null);Check(!fail.Succeeded&&fail.NativeErrorCode==31&&fail.RollbackSucceeded&&f.Boost[A][0]==2,"writer that mutates before reporting failure is rolled back");
  f=new Fake();f.IgnoreBoostWrites=true;fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Check(!fail.Succeeded&&fail.RollbackSucceeded&&f.Boost[A][0]==2&&Called(f,"WriteDC")==0,"write return success alone never claims apply success");
  f=new Fake();f.ErrorsBefore["Set:1"]=5;fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Check(!fail.Succeeded&&fail.RollbackSucceeded&&f.Boost[A][0]==2&&f.Boost[A][1]==3&&Called(f,"Set")==2,"activation failure restores and reapplies original values");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="WriteAC"&&count==1)fake.Active=B;};fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Check(!fail.Succeeded&&fail.RollbackSucceeded&&f.Active==B&&f.Boost[A][0]==2&&Called(f,"Set")==0,"race after precheck restores own inactive setting and preserves external active plan");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="WriteDC"&&count==1)fake.Boost[A][0]=4;};fail=new WindowsPowerService(f).ChangeCpuBoost(A,0,1);Check(!fail.Succeeded&&!fail.RollbackSucceeded&&f.Boost[A][0]==4&&f.Boost[A][1]==3&&!String.IsNullOrWhiteSpace(fail.RollbackError),"rollback never overwrites foreign changed boost value");Check(fail.RestorePoint==null,"foreign current value cannot issue safe restore token");
  f=new Fake();f.ErrorsBefore["WriteDC:1"]=5;f.ErrorsBefore["WriteAC:2"]=5;service=new WindowsPowerService(f);fail=service.ChangeCpuBoost(A,0,1);Check(!fail.Succeeded&&!fail.RollbackSucceeded&&f.Boost[A][0]==0&&fail.RestorePoint!=null&&fail.Before.Boost.AcBoost==2,"rollback failure retains original and safe manual restore point");var manual=service.Restore(fail.RestorePoint);Check(manual.Succeeded&&f.Boost[A][0]==2&&f.Boost[A][1]==3,"manual restore retries owned failed rollback");
  f=new Fake();service=new WindowsPowerService(f);var applied=service.ChangeCpuBoost(A,0,null);int writes=f.Writes.Count;f.Boost[A][0]=4;Error(service.Restore(applied.RestorePoint),false,"manual restore refuses a foreign value race");Check(f.Boost[A][0]==4&&f.Writes.Count==writes,"manual restore preserves external value");
  f=new Fake();service=new WindowsPowerService(f);applied=service.ChangeCpuBoost(A,0,null);writes=f.Writes.Count;int enumerations=Called(f,"Enum");f.Before=(fake,tag,count)=>{if(tag=="Enum"&&count==enumerations+1)fake.Boost[A][0]=4;};Error(service.Restore(applied.RestorePoint),false,"restore rechecks token expectation during transaction capture");Check(f.Boost[A][0]==4&&f.Writes.Count==writes,"restore race between initial check and capture is preserved");
  f=new Fake();service=new WindowsPowerService(f);applied=service.ChangeCpuBoost(A,0,null);writes=f.Writes.Count;f.Active=B;Error(service.Restore(applied.RestorePoint),false,"manual restore refuses changed plan");Check(f.Active==B&&f.Writes.Count==writes,"manual restore does not reselect old plan");Error(new WindowsPowerService(f).Restore(applied.RestorePoint),false,"other service cannot replay restore receipt");Error(service.Restore(null),false,"null restore receipt rejected");
  f=new Fake();service=new WindowsPowerService(f);var switched=service.ChangeActiveScheme(B,A);Check(switched.Succeeded&&switched.Changed&&switched.Before.ActiveScheme==A&&switched.After.ActiveScheme==B&&f.Active==B,"manual listed scheme switch read back");Check(switched.RestorePoint!=null&&Called(f,"WriteAC")==0&&Called(f,"WriteDC")==0,"scheme switch does not modify boost policy");manual=service.Restore(switched.RestorePoint);Check(manual.Succeeded&&f.Active==A,"manual original scheme restore");
  f=new Fake();switched=new WindowsPowerService(f).ChangeActiveScheme(A,A);Check(switched.Succeeded&&!switched.Changed&&!switched.WriteAttempted,"same active scheme is no-op");NoWrites(f,"same plan does not reapply");
  f=new Fake();Error(new WindowsPowerService(f).ChangeActiveScheme(Guid.NewGuid(),A),false,"unlisted scheme never selected");NoWrites(f,"unknown GUID is not authorization");
  f=new Fake();f.Active=B;Error(new WindowsPowerService(f).ChangeActiveScheme(C,A),false,"scheme switch requires current expected plan");NoWrites(f,"stale scheme choice does not activate");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="Enum"&&count==2)fake.Plans.RemoveAll(p=>p.Id==B);};Error(new WindowsPowerService(f).ChangeActiveScheme(B,A),false,"removed target rejected on fresh enumeration");NoWrites(f,"deleted scheme is not recreated or selected");
  f=new Fake();f.ErrorsBefore["Set:1"]=5;switched=new WindowsPowerService(f).ChangeActiveScheme(B,A);Check(!switched.Succeeded&&switched.NativeErrorCode==5&&switched.RollbackSucceeded&&f.Active==A,"scheme activation denial truthful and original preserved");
  f=new Fake();f.ErrorsAfter["Set:1"]=31;switched=new WindowsPowerService(f).ChangeActiveScheme(B,A);Check(!switched.Succeeded&&switched.RollbackSucceeded&&f.Active==A&&Called(f,"Set")==2,"scheme changed before failure is rolled back");
  f=new Fake();f.IgnoreSchemeWrites=true;switched=new WindowsPowerService(f).ChangeActiveScheme(B,A);Check(!switched.Succeeded&&switched.RollbackSucceeded&&f.Active==A,"scheme success requires identity readback");
  f=new Fake();f.ErrorsBefore["Set:2"]=5;f.ErrorsAfter["Set:1"]=31;service=new WindowsPowerService(f);switched=service.ChangeActiveScheme(B,A);Check(!switched.Succeeded&&!switched.RollbackSucceeded&&f.Active==B&&switched.RestorePoint!=null,"scheme rollback failure retains usable original receipt");manual=service.Restore(switched.RestorePoint);Check(manual.Succeeded&&f.Active==A,"manual restore after scheme rollback failure");
  f=new Fake();f.Before=(fake,tag,count)=>{if(tag=="Active"&&Called(fake,"Set")==1)fake.Active=C;};switched=new WindowsPowerService(f).ChangeActiveScheme(B,A);Check(!switched.Succeeded&&!switched.RollbackSucceeded&&f.Active==C&&Called(f,"Set")==1,"scheme rollback will not overwrite external third plan");
  f=new Fake();service=new WindowsPowerService(f);switched=service.ChangeActiveScheme(B,A);writes=f.Writes.Count;f.Plans.RemoveAll(p=>p.Id==A);Error(service.Restore(switched.RestorePoint),false,"manual restore refuses removed original plan");Check(f.Active==B&&f.Writes.Count==writes,"removed original never recreated");
  f=new Fake();f.Plans.Add(new WindowsPowerScheme(A,"Duplicate"));Error(new WindowsPowerService(f).ChangeActiveScheme(B,A),false,"ambiguous catalogue rejected");NoWrites(f,"duplicate list never permits setter");
  f=new Fake();f.Plans[0]=new WindowsPowerScheme(Guid.Empty,"Invalid");Error(new WindowsPowerService(f).ChangeCpuBoost(A,0,null),false,"invalid list GUID rejected");NoWrites(f,"invalid list never writes");
  f=new Fake();f.ErrorsBefore["Active:1"]=5;bool threw=false;try{new WindowsPowerService(f).ReadSnapshot();}catch(WindowsPowerNativeException ex){threw=ex.ErrorCode==5;}Check(threw,"failed active GET propagates native error");NoWrites(f,"failed active GET never writes");
  Console.WriteLine("Windows power: "+checks+" pure fake checks passed; no native setter, policy creation/deletion, registry write, elevation or timer.");
 }
 static void ReadOnly(string path){
  var snapshot=new WindowsPowerService().ReadSnapshot();
  if(!String.IsNullOrWhiteSpace(path))File.WriteAllText(path,new JavaScriptSerializer().Serialize(snapshot));
  Console.WriteLine("Read-only Windows power snapshot: plans="+snapshot.Schemes.Count+", consistent="+snapshot.Consistent+", AC Boost="+(snapshot.Boost.AcBoost.HasValue?snapshot.Boost.AcBoost.Value.ToString(CultureInfo.InvariantCulture):"unknown")+", DC Boost="+(snapshot.Boost.DcBoost.HasValue?snapshot.Boost.DcBoost.Value.ToString(CultureInfo.InvariantCulture):"unknown")+"; zero setter calls.");
 }
 public static int Main(string[] args){try{if(args.Length>0&&args[0]=="--read-only"){if(args.Length>2)throw new ArgumentException("Unexpected argument.");ReadOnly(args.Length==2?args[1]:null);}else{if(args.Length>0)throw new ArgumentException("Unknown mode.");Pure();}return 0;}catch(Exception ex){Console.Error.WriteLine("Windows power check failed after "+checks+" checks: "+ex.GetType().Name+" "+ex.Message);return 1;}}
}
