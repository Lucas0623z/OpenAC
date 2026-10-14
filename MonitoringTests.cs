// Synthetic telemetry-to-UI checks. No system queries, device workers or shown windows.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using RogControl;

internal static class MonitoringTests {
 static int count;
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static void Check(bool yes,string message){count++;if(!yes)throw new Exception(message);}
 static TelemetryReading Reading(double? value,string unit,string quality="sampled",int age=0){return new TelemetryReading{Value=value,Unit=unit,Quality=quality,Source="synthetic",CapturedAtUtc=DateTime.UtcNow.AddSeconds(-age)};}
 static NvidiaGpuTelemetry Gpu(int index,string id){return new NvidiaGpuTelemetry{Index=index,StableId=id,Name="Synthetic GPU",GraphicsClockMhz=Reading(2500+index,"MHz"),MemoryClockMhz=Reading(14001,"MHz"),TemperatureC=Reading(43.5+index,"°C"),UsagePercent=Reading(index,"%"),MemoryUsedPercent=Reading(27,"%"),FanSpeedPercent=Reading(31,"%"),VoltageV=Reading(null,"V")};}
 static WindowsTelemetrySample Fixture(){return new WindowsTelemetrySample{
  CapturedAtUtc=DateTime.UtcNow,CpuUsagePercent=Reading(18.2,"%"),CpuFrequencyMhz=Reading(4334.4,"MHz","osEstimate"),CpuPeakFrequencyMhz=Reading(5414,"MHz","osEstimate"),MemoryUsagePercent=Reading(27.4,"%"),MemoryConfiguredDataRateMTs=Reading(5600,"MT/s","configured"),BclkMhz=Reading(null,"MHz"),CpuTemperatureC=Reading(null,"°C"),MotherboardTemperatureC=Reading(null,"°C"),CpuCoreVoltageV=Reading(null,"V"),MemoryVoltageV=Reading(null,"V"),
  Gpus=new[]{Gpu(0,"synthetic0"),Gpu(1,"synthetic1")},LogicalProcessors=new[]{new LogicalProcessorTelemetry{Group=0,Number=0,FrequencyMhz=Reading(4400,"MHz","osEstimate")},new LogicalProcessorTelemetry{Group=1,Number=0,FrequencyMhz=Reading(4500,"MHz","osEstimate")}},Disks=new[]{new PhysicalDiskTelemetry{Index=2,ActivePercent=Reading(0,"%")}}
 };}
 static object Get(object owner,string field){return owner.GetType().GetField(field,Hidden).GetValue(owner);}
 static void Invoke(MainWindow window,string name,params object[] args){typeof(MainWindow).GetMethod(name,Hidden).Invoke(window,args);}
 static MonitoringValue Row(List<MonitoringValue> rows,string key){return rows.Single(row=>row.Key==key);}
 [STAThread] static void Main(){try{Run();Console.WriteLine("Monitoring: "+count+" synthetic checks passed; native reads 0, worker calls 0, shown windows 0, hardware writes 0.");}catch(Exception ex){Console.Error.WriteLine(ex.InnerException??ex);Environment.ExitCode=1;}}
 static void Run(){
  var raw=Fixture();var sample=MonitorReader.FromWindowsSample(raw);
  Check(sample.Frequency.Count==10,"Dynamic frequency rows include both GPUs and processor groups");
  Check(Row(sample.Frequency,"dramConfiguredRate").Unit=="MT/s","Configured DDR rate keeps transfer units");
  Check(Row(sample.Frequency,"cpuClock").Quality=="osEstimate","CPU estimate retains its quality");
  Check(Row(sample.Frequency,"cpuBclk").Value==null,"No fabricated BCLK");
  Check(Row(sample.Frequency,"cpu:1:0").Name=="CPU Logical Processor 1:0","Processor group identity is retained");
  Check(!sample.Frequency.Any(row=>row.Name.StartsWith("CPU Core ")),"Logical processors are not mislabeled physical cores");
  Check(Row(sample.Temperature,"gpu:synthetic0:temperature").Value==43.5&&Row(sample.Temperature,"gpu:synthetic1:temperature").Value==44.5,"GPU readings keep separate identities");
  Check(Row(sample.Temperature,"gpu:synthetic1:temperature").DeviceName=="Synthetic GPU","Driver model name remains accessible without changing the original row labels");
  Check(Row(sample.Temperature,"cpuPackageTemperature").Value==null,"CPU package is not inferred from another temperature");
  Check(sample.Fans.Count==3&&sample.Fans.Skip(1).All(row=>row.Unit=="%"),"GPU percentage is never advertised as RPM");
  Check(sample.Fans[0].Value==null,"CPU fan remains unknown");
  Check(Row(sample.Usage,"disk:2:activity").Value==0,"Valid zero disk activity is retained");
  Check(!sample.Usage.Any(row=>row.Key.StartsWith("volume:")),"Pure transformation never queries volumes");
  Check(sample.Voltage.All(row=>row.Value==null),"No fabricated voltage");
  raw.CpuFrequencyMhz=Reading(4000,"MHz","osEstimate",20);raw.Gpus[0].GraphicsClockMhz=Reading(2000,"MHz","driverReported",20);raw.CpuUsagePercent=Reading(50,"%","sampled",20);raw.MemoryConfiguredDataRateMTs=Reading(5600,"MT/s","configured",70);
  var aged=MonitorReader.FromWindowsSample(raw);Check(Row(aged.Frequency,"cpuClock").Value==null&&aged.CpuUsage==null,"Expired sampled values and compatibility fields are unknown");
  Check(Row(aged.Frequency,"gpu:synthetic0:graphicsClock").Value==null,"Expired GPU value is unknown");
  Check(Row(aged.Frequency,"dramConfiguredRate").Value==5600,"Configured rate has its separate cache lifetime");
  raw.MemoryConfiguredDataRateMTs=Reading(5600,"MT/s","configured",100);raw.CpuFrequencyMhz=Reading(Double.NaN,"MHz");
  aged=MonitorReader.FromWindowsSample(raw);Check(Row(aged.Frequency,"dramConfiguredRate").Value==null,"Expired configured rate is unknown");Check(Row(aged.Frequency,"cpuClock").Value==null,"NaN cannot become a reading");
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new MainWindow(true);
  typeof(MainWindow).GetField("monitor",Hidden).SetValue(window,sample);
  foreach(int width in new[]{1538,1180})foreach(string page in new[]{"控制面板","系统监控"}){
   Invoke(window,"ShowPage",page);var frame=(FrameworkElement)window.Content;frame.Measure(new Size(width,826));frame.Arrange(new Rect(0,0,width,826));frame.UpdateLayout();
   var cards=(IDictionary)Get(window,"dynamicMonitorCards");Check(cards.Count==5,"Every monitoring group exists at "+width+" / "+page);
   foreach(string group in new[]{"Frequency","Temperature","Usage","Fans","Voltage"}){var card=cards[group];var scroll=(ScrollViewer)Get(card,"Scroll");Check(scroll.ViewportHeight>0&&scroll.HorizontalScrollBarVisibility==ScrollBarVisibility.Disabled,"Bounded group viewport "+group);}
   var first=((IList)Get(cards["Frequency"],"Items"))[0];var value=(TextBlock)Get(first,"Value");Check(value.Text=="4334 MHz"&&Convert.ToString(value.ToolTip)=="Windows 估算","Original clock precision with accessible source quality");
   var row=sample.Frequency[0];row.Captured=DateTime.Now.AddSeconds(-30);Invoke(window,"UpdateDynamicMonitorCards");Check(value.Text=="— MHz","Expired preview snapshot cannot show a current value");row.Captured=DateTime.Now;Invoke(window,"UpdateDynamicMonitorCards");Check(value.Text=="4334 MHz","Current reading recovers in the same control");
   Check(Object.ReferenceEquals(first,((IList)Get(cards["Frequency"],"Items"))[0]),"Routine update retains row identity");
  }
  window.Close();app.Shutdown();
 }
}
