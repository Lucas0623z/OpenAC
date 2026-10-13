using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls;

namespace RogControl {
 public sealed class MonitorReader:IDisposable {
  readonly WindowsTelemetryReader windows=new WindowsTelemetryReader();
  public MonitoringSample Read(){
   var sample=FromWindowsSample(windows.Read());AddVolumeUsage(sample);sample.Usage.Sort((left,right)=>UsageOrder(left).CompareTo(UsageOrder(right)));return sample;
  }
  // This transformation has no system queries; it can be checked with synthetic samples.
  public static MonitoringSample FromWindowsSample(WindowsTelemetrySample raw){
   if(raw==null)throw new ArgumentNullException("raw");var sample=new MonitoringSample{Captured=raw.CapturedAtUtc.ToLocalTime()};
   sample.Frequency.Clear();sample.Temperature.Clear();sample.Usage.Clear();sample.Fans.Clear();sample.Voltage.Clear();
   Add(sample.Frequency,"cpuClock","CPU",raw.CpuFrequencyMhz);
   Add(sample.Frequency,"cpuBclk","CPU BCLK",raw.BclkMhz);
   Add(sample.Temperature,"cpuTemperature","CPU",raw.CpuTemperatureC);
   sample.Temperature.Add(MonitoringSample.Unknown("cpuPackageTemperature","CPU Package","°C"));
   Add(sample.Usage,"cpuUsage","CPU (Average) Usage",raw.CpuUsagePercent,0,100);
   foreach(var gpu in raw.Gpus){string suffix=raw.Gpus.Length>1?" #"+(gpu.Index+1).ToString(CultureInfo.InvariantCulture):"";string key="gpu:"+gpu.StableId;
    Add(sample.Frequency,key+":memoryClock","GPU Memory Clock"+suffix,gpu.MemoryClockMhz);
    Add(sample.Frequency,key+":graphicsClock","GPU Clock"+suffix,gpu.GraphicsClockMhz);
    Add(sample.Temperature,key+":temperature","GPU"+suffix,gpu.TemperatureC);
    Add(sample.Usage,key+":usage","GPU"+suffix,gpu.UsagePercent,0,100);
    Add(sample.Usage,key+":memoryUsage","GPU Memory Usage"+suffix,gpu.MemoryUsedPercent,0,100);
    if(gpu.FanSpeedPercent!=null&&gpu.FanSpeedPercent.Value.HasValue)Add(sample.Fans,key+":fan","GPU Fan"+suffix,gpu.FanSpeedPercent,0,100);
    Add(sample.Voltage,key+":voltage","GPU Voltage"+suffix,gpu.VoltageV);
    foreach(var group in new[]{sample.Frequency,sample.Temperature,sample.Usage,sample.Fans,sample.Voltage})foreach(var row in group)if(row.Key.StartsWith(key+":",StringComparison.Ordinal))row.DeviceName=gpu.Name;
    if(!sample.GpuClock.HasValue){sample.GpuClock=RowValue(sample.Frequency,key+":graphicsClock");sample.GpuTemperature=RowValue(sample.Temperature,key+":temperature");sample.GpuUsage=RowValue(sample.Usage,key+":usage");}
   }
   Add(sample.Frequency,"cpuPeakClock","Cores (Max)",raw.CpuPeakFrequencyMhz);
   Add(sample.Frequency,"dramConfiguredRate","DRAM Data Rate",raw.MemoryConfiguredDataRateMTs);
   foreach(var cpu in raw.LogicalProcessors)Add(sample.Frequency,"cpu:"+cpu.Group+":"+cpu.Number,"CPU Logical Processor "+cpu.Group+":"+cpu.Number,cpu.FrequencyMhz);
   Add(sample.Temperature,"boardTemperature","Motherboard",raw.MotherboardTemperatureC);
   Add(sample.Usage,"memoryUsage","RAM Usage",raw.MemoryUsagePercent,0,100);
   foreach(var disk in raw.Disks)Add(sample.Usage,"disk:"+disk.Index+":activity","Physical Disk "+disk.Index+" Activity",disk.ActivePercent,0,100);
   Add(sample.Voltage,"cpuCoreVoltage","CPU Core Voltage",raw.CpuCoreVoltageV);
   Add(sample.Voltage,"dramVoltage","DRAM Voltage",raw.MemoryVoltageV);
   sample.Fans.Insert(0,MonitoringSample.Unknown("cpuFan","CPU Fan","RPM"));
   sample.Usage.Sort((left,right)=>UsageOrder(left).CompareTo(UsageOrder(right)));sample.Voltage.Sort((left,right)=>VoltageOrder(left).CompareTo(VoltageOrder(right)));
   sample.CpuUsage=RowValue(sample.Usage,"cpuUsage");double? memory=RowValue(sample.Usage,"memoryUsage");
   if(memory.HasValue)sample.MemoryUsage=(uint)memory.Value;
   return sample;
  }
  static double? RowValue(List<MonitoringValue> group,string key){var item=group.Find(row=>row.Key==key);return item==null?null:item.Value;}
  static int UsageOrder(MonitoringValue row){return row.Key=="cpuUsage"?0:row.Key.StartsWith("gpu:")&&row.Key.EndsWith(":usage")?1:row.Key=="memoryUsage"?2:row.Key.StartsWith("volume:")?3:row.Key.StartsWith("gpu:")?4:5;}
  static int VoltageOrder(MonitoringValue row){return row.Key=="cpuCoreVoltage"?0:row.Key.StartsWith("gpu:")?2:1;}
  static void Add(List<MonitoringValue> group,string key,string name,TelemetryReading raw,double? minimum=null,double? maximum=null){
   if(raw==null)return;bool stale=raw.IsStale(DateTime.UtcNow,TimeSpan.FromSeconds(raw.Quality=="configured"?90:15));
   double? value=stale?null:raw.Value;bool finite=value.HasValue&&!Double.IsNaN(value.Value)&&!Double.IsInfinity(value.Value);
   group.Add(new MonitoringValue{Key=key,Name=name,Unit=raw.Unit,Source=raw.Source,Quality=raw.Quality,Value=finite?value:null,Confirmed=finite,Min=minimum,Max=maximum,Captured=raw.CapturedAtUtc.ToLocalTime()});
  }
  static void AddVolumeUsage(MonitoringSample sample){
   foreach(var drive in DriveInfo.GetDrives())try{
    if(drive.DriveType!=DriveType.Fixed||!drive.IsReady||drive.TotalSize<=0)continue;
    long total=drive.TotalSize,free=drive.TotalFreeSpace;if(free<0||free>total)continue;
    string name=drive.Name.TrimEnd('\\');sample.Set(sample.Usage,"volume:"+name,"Disc ("+name+")","%",100.0*(1.0-(double)free/total),"Windows volume total/free capacity",0,100);
   }catch{}
  }
  public void Dispose(){windows.Dispose();}
 }
 public partial class MainWindow {
  MonitoringSample monitor=new MonitoringSample();readonly MonitorReader monitorReader=new MonitorReader();bool monitorBusy;DispatcherTimer monitorTimer;Task<MonitoringSample> monitorReadTask;
  readonly Dictionary<string,TextBlock> liveMonitorLabels=new Dictionary<string,TextBlock>();
  void BeginMonitoring(){
   if(render)return;monitorTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
   monitorTimer.Tick+=async(s,e)=>{if(!IsLoaded||closing||page!="控制面板"&&page!="总览"&&page!="系统监控")return;UpdateMonitorReadings();if(monitorBusy)return;monitorBusy=true;
    try{monitorReadTask=Task.Run(()=>monitorReader.Read());var next=await monitorReadTask;if(!closing){monitor=next;AddKnownMotherboardMonitorRows();UpdateMonitorReadings();}}
    catch(Exception ex){if(!closing){monitor=new MonitoringSample();AddKnownMotherboardMonitorRows();UpdateMonitorReadings();string failure="监控读取失败："+ex.GetType().Name;if(history.Count==0||history[history.Count-1]!=failure)history.Add(failure);if(history.Count>200)history.RemoveAt(0);}}
    finally{monitorReadTask=null;monitorBusy=false;}
   };monitorTimer.Start();
  }
  void AddKnownMotherboardMonitorRows(){
   if(!String.Equals(info.Board,"ROG CROSSHAIR X870E EXTREME",StringComparison.OrdinalIgnoreCase))return;
   AddUnknownMonitorRow(monitor.Temperature,"dramTemperature","DRAM Temperature","°C");AddUnknownMonitorRow(monitor.Temperature,"vrmTemperature","VRM","°C");AddUnknownMonitorRow(monitor.Temperature,"chipset1Temperature","Chipset 1","°C");
   foreach(string name in new[]{"CPU OPT Fan","Chassis Fan 1","Chassis Fan 2","Radiator Fan 1","Radiator Fan 2","Water Pump+ 1","Water Pump+ 2"})AddUnknownMonitorRow(monitor.Fans,"boardFan:"+name,name,"RPM");
   foreach(string name in new[]{"CPU SOC Voltage","CPU VDDIO / MC Voltage","+12V","+5V","+3.3V","Misc Voltage","Chipset0 VDD Voltage"})AddUnknownMonitorRow(monitor.Voltage,"boardVoltage:"+name,name,"V");
   var dram=monitor.Temperature.Find(row=>row.Key=="dramTemperature");var board=monitor.Temperature.Find(row=>row.Key=="boardTemperature");if(dram!=null&&board!=null){monitor.Temperature.Remove(dram);monitor.Temperature.Insert(monitor.Temperature.IndexOf(board),dram);}
   MoveExtraMonitorRows(monitor.Fans,row=>row.Key.StartsWith("gpu:"));MoveExtraMonitorRows(monitor.Voltage,row=>row.Key=="dramVoltage"||row.Key.StartsWith("gpu:"));
  }
  static void MoveExtraMonitorRows(List<MonitoringValue> rows,Predicate<MonitoringValue> extra){var found=rows.FindAll(extra);rows.RemoveAll(extra);rows.AddRange(found);}
  static void AddUnknownMonitorRow(List<MonitoringValue> group,string key,string name,string unit){if(!group.Exists(row=>row.Key==key))group.Add(MonitoringSample.Unknown(key,name,unit));}
  async Task DisposeMonitoringAsync(){if(monitorTimer!=null)monitorTimer.Stop();var pending=monitorReadTask;if(pending!=null)try{await pending;}catch{}await Task.Run(()=>monitorReader.Dispose());}
  string MonitorValue(double? value,string suffix){return value.HasValue?value.Value.ToString("0",CultureInfo.InvariantCulture)+suffix:"—"+suffix;}
  void UpdateMonitorReadings(){if(page=="控制面板"||page=="总览"||page=="系统监控")UpdateDynamicMonitorCards();}
 }
}
