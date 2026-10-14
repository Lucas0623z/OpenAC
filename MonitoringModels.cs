using System;
using System.Collections.Generic;

namespace RogControl {
 public sealed class MonitoringValue {
  public string Key,Name,Unit,Source,Quality,DeviceName;
  public double? Value,Min,Max;
  public bool Confirmed;
  public DateTime Captured;
 }
 public sealed class MonitoringSample {
  public double? CpuUsage;public uint? MemoryUsage;public double? GpuUsage,GpuClock,GpuTemperature;
  public DateTime Captured=DateTime.Now;
  public List<MonitoringValue> Frequency=new List<MonitoringValue>(),Temperature=new List<MonitoringValue>(),Usage=new List<MonitoringValue>(),Fans=new List<MonitoringValue>(),Voltage=new List<MonitoringValue>();
  public MonitoringSample(){
   Frequency.Add(Unknown("cpuClock","CPU","MHz"));Frequency.Add(Unknown("cpuBclk","CPU BCLK","MHz"));
   Temperature.Add(Unknown("cpuTemperature","CPU","°C"));Temperature.Add(Unknown("cpuPackageTemperature","CPU Package","°C"));Temperature.Add(Unknown("boardTemperature","Motherboard","°C"));
   Usage.Add(Unknown("cpuUsage","CPU (Average) Usage","%"));Usage.Add(Unknown("memoryUsage","RAM Usage","%"));
   Fans.Add(Unknown("cpuFan","CPU Fan","RPM"));
   Voltage.Add(Unknown("cpuCoreVoltage","CPU Core Voltage","V"));
  }
  public static MonitoringValue Unknown(string key,string name,string unit){return new MonitoringValue{Key=key,Name=name,Unit=unit,Captured=DateTime.Now};}
  public void Set(List<MonitoringValue> group,string key,string name,string unit,double? value,string source,double? minimum=null,double? maximum=null){
   var item=group.Find(entry=>entry.Key==key);if(item==null){item=Unknown(key,name,unit);group.Add(item);}item.Name=name;item.Unit=unit;item.Source=source;item.Value=value;item.Confirmed=value.HasValue&&!Double.IsNaN(value.Value)&&!Double.IsInfinity(value.Value);item.Min=minimum;item.Max=maximum;item.Captured=Captured;
  }
  public void SetBasicReadings(){
   Set(Usage,"cpuUsage","CPU (Average) Usage","%",CpuUsage,"Windows GetSystemTimes",0,100);
   Set(Usage,"memoryUsage","RAM Usage","%",MemoryUsage.HasValue?(double?)MemoryUsage.Value:null,"Windows GlobalMemoryStatusEx",0,100);
   if(GpuClock.HasValue||GpuTemperature.HasValue||GpuUsage.HasValue){Set(Frequency,"nvidiaGraphicsClock","GPU Clock","MHz",GpuClock,"NVIDIA nvidia-smi");Set(Temperature,"nvidiaTemperature","GPU","°C",GpuTemperature,"NVIDIA nvidia-smi");Set(Usage,"nvidiaUsage","GPU","%",GpuUsage,"NVIDIA nvidia-smi",0,100);}
  }
 }
}
