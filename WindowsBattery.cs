// SPDX-License-Identifier: MIT
// Independent read-only Windows battery telemetry; no charge-limit control.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RogControl {
 public enum WindowsBatteryDataKind {Status,StaticData,FullChargedCapacity,CycleCount}
 public sealed class WindowsBatteryPowerStatus {
  public byte AcLineStatus,BatteryFlag,BatteryLifePercent,SystemStatusFlag;
  public uint BatteryLifeTime,BatteryFullLifeTime;
 }
 // Backend inputs are opaque outside this assembly. Only the sanitized
 // snapshot is returned to UI/serialization; serials are never queried.
 public sealed class WindowsBatteryInputRow {
  internal readonly string InstanceName;
  internal readonly Dictionary<string,object> Values;
  static readonly string[] Allowed={"Active","Tag","RemainingCapacity","ChargeRate","DischargeRate","Voltage","PowerOnline","Charging","Discharging","Critical","Capabilities","DesignedCapacity","FullChargedCapacity","CycleCount"};
  public WindowsBatteryInputRow(string instanceName,IDictionary<string,object> values){InstanceName=instanceName;Values=new Dictionary<string,object>(StringComparer.Ordinal);if(values!=null)foreach(string key in Allowed){object value;if(values.TryGetValue(key,out value))Values[key]=value;}}
 }
 public sealed class WindowsBatterySourceError {
  public string Source {get;internal set;}
  public string Category {get;internal set;}
  public int? NativeErrorCode {get;internal set;}
  public int? HResult {get;internal set;}
 }
 public sealed class WindowsBatteryRecord {
  public string StableId {get;internal set;}
  public string Name {get;internal set;}
  public bool? PowerOnline {get;internal set;}
  public bool? Charging {get;internal set;}
  public bool? Discharging {get;internal set;}
  public bool? Critical {get;internal set;}
  public bool? RelativeCapacityUnits {get;internal set;}
  public bool? IsSystemBattery {get;internal set;}
  public bool StateConflict {get;internal set;}
  public bool MetadataConsistent {get;internal set;}
  public uint? DesignedCapacityMilliwattHours {get;internal set;}
  public uint? FullChargedCapacityMilliwattHours {get;internal set;}
  public uint? RemainingCapacityMilliwattHours {get;internal set;}
  public uint? VoltageMillivolts {get;internal set;}
  public uint? ChargeRateMilliwatts {get;internal set;}
  public uint? DischargeRateMilliwatts {get;internal set;}
  public double? SignedPowerMilliwatts {get;internal set;}
  public double? HealthPercent {get;internal set;}
  public double? ChargePercent {get;internal set;}
  public uint? CycleCount {get;internal set;}
  public DateTime CapturedUtc {get;internal set;}
 }
 public sealed class WindowsBatterySnapshot {
  public bool? HasSystemBattery {get;internal set;}
  public bool? AcOnline {get;internal set;}
  public bool? Charging {get;internal set;}
  public bool? Critical {get;internal set;}
  public bool? BatterySaver {get;internal set;}
  public int? ChargePercent {get;internal set;}
  public uint? RemainingSeconds {get;internal set;}
  public uint? FullLifeSeconds {get;internal set;}
  public ReadOnlyCollection<WindowsBatteryRecord> Batteries {get;internal set;}
  public ReadOnlyCollection<WindowsBatterySourceError> Errors {get;internal set;}
  public DateTime CapturedUtc {get;internal set;}
 }
 public interface IWindowsBatteryBackend {
  WindowsBatteryPowerStatus ReadSystemStatus();
  IList<WindowsBatteryInputRow> Query(WindowsBatteryDataKind kind,TimeSpan timeout,int maximumRows);
 }

 public sealed class WindowsBatteryNativeBackend : IWindowsBatteryBackend {
  [StructLayout(LayoutKind.Sequential)]struct NativePowerStatus {public byte Ac,Battery,Percent,Saver;public uint Remaining,FullLife;}
  [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GetSystemPowerStatus(out NativePowerStatus status);
  // WMI's enumeration timeout does not bound Connect(). A separate wait
  // bounds the caller. The System semaphore is stored in the AppDomain so
  // byte-loaded preview assemblies cannot each create another pending read.
  const string QueryGateKey="OpenAC.WindowsBattery.WmiQueryGate.v1";
  static SemaphoreSlim SharedQueryGate(){
   var domain=AppDomain.CurrentDomain;
   lock(domain){var existing=domain.GetData(QueryGateKey);var gate=existing as SemaphoreSlim;if(existing!=null&&gate==null)throw new InvalidOperationException("Battery query gate unavailable.");if(gate==null){gate=new SemaphoreSlim(1,1);domain.SetData(QueryGateKey,gate);}return gate;}
  }
  public WindowsBatteryPowerStatus ReadSystemStatus(){NativePowerStatus value;if(!GetSystemPowerStatus(out value))throw new Win32Exception(Marshal.GetLastWin32Error());return new WindowsBatteryPowerStatus{AcLineStatus=value.Ac,BatteryFlag=value.Battery,BatteryLifePercent=value.Percent,SystemStatusFlag=value.Saver,BatteryLifeTime=value.Remaining,BatteryFullLifeTime=value.FullLife};}
  public static string ClassName(WindowsBatteryDataKind kind){switch(kind){case WindowsBatteryDataKind.Status:return "BatteryStatus";case WindowsBatteryDataKind.StaticData:return "BatteryStaticData";case WindowsBatteryDataKind.FullChargedCapacity:return "BatteryFullChargedCapacity";case WindowsBatteryDataKind.CycleCount:return "BatteryCycleCount";default:throw new ArgumentOutOfRangeException("kind");}}
  static string[] Properties(WindowsBatteryDataKind kind){switch(kind){case WindowsBatteryDataKind.Status:return new[]{"InstanceName","Active","Tag","RemainingCapacity","ChargeRate","DischargeRate","Voltage","PowerOnline","Charging","Discharging","Critical"};case WindowsBatteryDataKind.StaticData:return new[]{"InstanceName","Active","Tag","Capabilities","DesignedCapacity"};case WindowsBatteryDataKind.FullChargedCapacity:return new[]{"InstanceName","Active","Tag","FullChargedCapacity"};case WindowsBatteryDataKind.CycleCount:return new[]{"InstanceName","Active","Tag","CycleCount"};default:throw new ArgumentOutOfRangeException("kind");}}
  public IList<WindowsBatteryInputRow> Query(WindowsBatteryDataKind kind,TimeSpan timeout,int maximumRows){
   if(timeout<=TimeSpan.Zero||timeout>TimeSpan.FromSeconds(2)||maximumRows<1||maximumRows>128)throw new ArgumentOutOfRangeException("limits");
   ClassName(kind);var gate=SharedQueryGate();if(!gate.Wait(0))throw new TimeoutException("Battery query already pending.");
   Task<IList<WindowsBatteryInputRow>> task;
   try{task=Task.Run(()=>{try{return QueryCore(kind,timeout,maximumRows);}finally{gate.Release();}});}catch{gate.Release();throw;}
   // Observe late faults even when the caller has already returned timeout.
   task.ContinueWith(fault=>{var observed=fault.Exception;},TaskContinuationOptions.OnlyOnFaulted);
   try{if(!task.Wait(timeout))throw new TimeoutException("Battery WMI query timed out.");return task.Result;}catch(AggregateException ex){throw ex.GetBaseException();}
  }
  static IList<WindowsBatteryInputRow> QueryCore(WindowsBatteryDataKind kind,TimeSpan timeout,int maximumRows){
   var fields=Properties(kind);var result=new List<WindowsBatteryInputRow>();var clock=Stopwatch.StartNew();
   var scope=new ManagementScope(@"\\.\root\wmi",new ConnectionOptions{EnablePrivileges=false});
   var options=new EnumerationOptions{Timeout=timeout,ReturnImmediately=true,Rewindable=false,BlockSize=1};
   using(var search=new ManagementObjectSearcher(scope,new ObjectQuery("SELECT "+String.Join(",",fields)+" FROM "+ClassName(kind)),options))
   using(var values=search.Get())foreach(ManagementBaseObject row in values){using(row){
    if(clock.Elapsed>timeout)throw new TimeoutException("Battery WMI enumeration timed out.");if(result.Count>=maximumRows)throw new InvalidOperationException("Battery WMI row limit exceeded.");
    var data=new Dictionary<string,object>(StringComparer.Ordinal);string instance=null;
    foreach(string field in fields){object value=null;try{value=row[field];}catch(ManagementException){}if(field=="InstanceName")instance=value as string;else data[field]=value;}
    result.Add(new WindowsBatteryInputRow(instance,data));
   }}return result;
  }
 }

 public sealed class WindowsBatteryReader {
  public const uint RelativeCapacityFlag=0x40000000,SystemBatteryFlag=0x80000000;
  public static readonly TimeSpan PerQueryTimeout=TimeSpan.FromSeconds(2),ReadBudget=TimeSpan.FromSeconds(6);
  public const int MaximumRows=128;
  readonly IWindowsBatteryBackend backend;
  readonly object readGate=new object();
  public WindowsBatteryReader():this(new WindowsBatteryNativeBackend()){}
  public WindowsBatteryReader(IWindowsBatteryBackend value){if(value==null)throw new ArgumentNullException("value");backend=value;}
  static object Value(WindowsBatteryInputRow row,string name){object value;return row!=null&&row.Values.TryGetValue(name,out value)&&value!=DBNull.Value?value:null;}
  static bool? Flag(WindowsBatteryInputRow row,string name){object value=Value(row,name);return value is bool?(bool?)value:null;}
  static uint? Number(WindowsBatteryInputRow row,string name){
   object value=Value(row,name);if(value==null||value is bool||value is float||value is double)return null;
   if(!(value is byte||value is sbyte||value is short||value is ushort||value is int||value is uint||value is long||value is ulong||value is decimal||value is string))return null;
   try{decimal number;if(value is string){if(!Decimal.TryParse((string)value,NumberStyles.Integer,CultureInfo.InvariantCulture,out number))return null;}else number=Convert.ToDecimal(value,CultureInfo.InvariantCulture);return number>=0&&number<UInt32.MaxValue&&Decimal.Truncate(number)==number?(uint?)number:null;}catch{return null;}
  }
  static bool Identity(WindowsBatteryInputRow row){return row!=null&&!String.IsNullOrWhiteSpace(row.InstanceName)&&row.InstanceName.Length<=2048&&!row.InstanceName.Any(Char.IsControl);}
  static string HashIdentity(string name){using(var hash=SHA256.Create())return String.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes("OpenAC.WindowsBattery\0"+name)).Select(b=>b.ToString("x2",CultureInfo.InvariantCulture)));}
  static WindowsBatterySourceError Error(string source,Exception error){var native=error as Win32Exception;return new WindowsBatterySourceError{Source=source,Category=error is TimeoutException?"Timeout":error is ManagementException?"WmiUnavailable":error is InvalidOperationException?"InvalidData":"ReadFailed",NativeErrorCode=native==null?(int?)null:native.NativeErrorCode,HResult=error.HResult};}
  static void DataError(IList<WindowsBatterySourceError> errors,string source,string category){if(!errors.Any(e=>e.Source==source&&e.Category==category))errors.Add(new WindowsBatterySourceError{Source=source,Category=category});}
  Dictionary<string,WindowsBatteryInputRow> Rows(WindowsBatteryDataKind kind,Stopwatch clock,IList<WindowsBatterySourceError> errors,bool status){
   string source=WindowsBatteryNativeBackend.ClassName(kind);var result=new Dictionary<string,WindowsBatteryInputRow>(StringComparer.Ordinal);var ambiguous=new HashSet<string>(StringComparer.Ordinal);
   try{
    TimeSpan remaining=ReadBudget-clock.Elapsed;if(remaining<=TimeSpan.Zero)throw new TimeoutException("Battery read budget exhausted.");
    var rows=backend.Query(kind,remaining<PerQueryTimeout?remaining:PerQueryTimeout,MaximumRows);if(rows==null||rows.Count>MaximumRows)throw new InvalidOperationException("Invalid battery row set.");
    foreach(var row in rows){
     if(!Identity(row)){DataError(errors,source,"InvalidIdentity");continue;}
     bool? active=Flag(row,"Active");if(status&&active!=true||!status&&active==false)continue;
     uint? tag=Number(row,"Tag");if(status&&tag==0)continue;
     if(result.ContainsKey(row.InstanceName)){ambiguous.Add(row.InstanceName);DataError(errors,source,"AmbiguousIdentity");}else result[row.InstanceName]=row;
    }
    foreach(string name in ambiguous)result.Remove(name);
   }catch(Exception ex){errors.Add(Error(source,ex));result.Clear();}
   return result;
  }
  static WindowsBatteryInputRow Match(Dictionary<string,WindowsBatteryInputRow> data,WindowsBatteryInputRow status,IList<WindowsBatterySourceError> errors,string source,ref bool consistent){
   WindowsBatteryInputRow value;if(!data.TryGetValue(status.InstanceName,out value))return null;uint? currentTag=Number(status,"Tag"),tag=Number(value,"Tag");
   if(currentTag.HasValue&&tag.HasValue&&currentTag!=tag){consistent=false;DataError(errors,source,"BatteryTagChanged");return null;}return value;
  }
  public WindowsBatterySnapshot ReadSnapshot(){lock(readGate){
   var clock=Stopwatch.StartNew();var errors=new List<WindowsBatterySourceError>();var records=new List<WindowsBatteryRecord>();var snapshot=new WindowsBatterySnapshot{CapturedUtc=DateTime.UtcNow};
   try{
    var system=backend.ReadSystemStatus();if(system==null)throw new InvalidOperationException("Missing system power status.");
    snapshot.AcOnline=system.AcLineStatus==0?(bool?)false:system.AcLineStatus==1?(bool?)true:null;
    snapshot.BatterySaver=system.SystemStatusFlag==0?(bool?)false:system.SystemStatusFlag==1?(bool?)true:null;
    snapshot.HasSystemBattery=system.BatteryFlag==255?(bool?)null:(system.BatteryFlag&128)!=0?(bool?)false:true;
    if(system.BatteryFlag!=255&&snapshot.HasSystemBattery!=false){snapshot.Charging=(system.BatteryFlag&8)!=0;snapshot.Critical=(system.BatteryFlag&4)!=0;}
    if(snapshot.HasSystemBattery!=false){if(system.BatteryLifePercent<=100)snapshot.ChargePercent=system.BatteryLifePercent;if(snapshot.AcOnline!=true){if(system.BatteryLifeTime!=UInt32.MaxValue)snapshot.RemainingSeconds=system.BatteryLifeTime;if(system.BatteryFullLifeTime!=UInt32.MaxValue)snapshot.FullLifeSeconds=system.BatteryFullLifeTime;}}
   }catch(Exception ex){errors.Add(Error("GetSystemPowerStatus",ex));}
   if(snapshot.HasSystemBattery!=false){
    var status=Rows(WindowsBatteryDataKind.Status,clock,errors,true);
    if(status.Count>0){
     var data=Rows(WindowsBatteryDataKind.StaticData,clock,errors,false);var full=Rows(WindowsBatteryDataKind.FullChargedCapacity,clock,errors,false);var cycles=Rows(WindowsBatteryDataKind.CycleCount,clock,errors,false);
     foreach(var entry in status.OrderBy(e=>HashIdentity(e.Key),StringComparer.Ordinal)){
      var row=entry.Value;bool consistent=true;var staticRow=Match(data,row,errors,"BatteryStaticData",ref consistent);var fullRow=Match(full,row,errors,"BatteryFullChargedCapacity",ref consistent);var cycleRow=Match(cycles,row,errors,"BatteryCycleCount",ref consistent);
      uint? capabilities=Number(staticRow,"Capabilities");bool? relative=capabilities.HasValue?(bool?)((capabilities.Value&RelativeCapacityFlag)!=0):null;bool? systemBattery=capabilities.HasValue?(bool?)((capabilities.Value&SystemBatteryFlag)!=0):null;
      // Do not label a provider's explicitly non-system battery as the
      // computer battery; this feature is separate from ROG peripheral cells.
      if(systemBattery==false)continue;
      var battery=new WindowsBatteryRecord{StableId=HashIdentity(entry.Key),Name="系统电池 "+(records.Count+1),PowerOnline=Flag(row,"PowerOnline"),Charging=Flag(row,"Charging"),Discharging=Flag(row,"Discharging"),Critical=Flag(row,"Critical"),RelativeCapacityUnits=relative,IsSystemBattery=systemBattery,VoltageMillivolts=Number(row,"Voltage"),MetadataConsistent=consistent,CapturedUtc=DateTime.UtcNow};
      uint? cycle=Number(cycleRow,"CycleCount");if(cycle>0)battery.CycleCount=cycle;
      battery.StateConflict=battery.Charging==true&&battery.Discharging==true;
      if(relative==false){
       battery.DesignedCapacityMilliwattHours=Number(staticRow,"DesignedCapacity");battery.FullChargedCapacityMilliwattHours=Number(fullRow,"FullChargedCapacity");battery.RemainingCapacityMilliwattHours=Number(row,"RemainingCapacity");
       if(battery.DesignedCapacityMilliwattHours>0&&battery.FullChargedCapacityMilliwattHours.HasValue)battery.HealthPercent=100.0*battery.FullChargedCapacityMilliwattHours.Value/battery.DesignedCapacityMilliwattHours.Value;
       if(battery.FullChargedCapacityMilliwattHours>0&&battery.RemainingCapacityMilliwattHours.HasValue&&battery.RemainingCapacityMilliwattHours<=battery.FullChargedCapacityMilliwattHours)battery.ChargePercent=100.0*battery.RemainingCapacityMilliwattHours.Value/battery.FullChargedCapacityMilliwattHours.Value;
       if(!battery.StateConflict){
        uint? charge=Number(row,"ChargeRate"),discharge=Number(row,"DischargeRate");if(charge>Int32.MaxValue)charge=null;if(discharge>Int32.MaxValue)discharge=null;
        if(battery.Charging==true&&battery.Discharging==false){battery.ChargeRateMilliwatts=charge;if(charge.HasValue)battery.SignedPowerMilliwatts=charge.Value;}
        else if(battery.Discharging==true&&battery.Charging==false){battery.DischargeRateMilliwatts=discharge;if(discharge.HasValue)battery.SignedPowerMilliwatts=-(double)discharge.Value;}
        else if(battery.Charging==false&&battery.Discharging==false&&charge==0&&discharge==0){battery.ChargeRateMilliwatts=0;battery.DischargeRateMilliwatts=0;battery.SignedPowerMilliwatts=0;}
       }
      }
      records.Add(battery);
     }
    }
   }
   snapshot.Batteries=records.AsReadOnly();snapshot.Errors=errors.AsReadOnly();return snapshot;
  }}
 }
}
