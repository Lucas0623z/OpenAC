using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls;

namespace RogControl {
 public sealed class MonitoringSample {
  public double? CpuUsage;public uint? MemoryUsage;public double? GpuUsage,GpuClock,GpuTemperature;
  public DateTime Captured=DateTime.Now;
 }
 public sealed class MonitorReader {
  ulong previousIdle,previousKernel,previousUser;bool sampled;
  [StructLayout(LayoutKind.Sequential)]struct ClockTime{public uint Low,High;public ulong Value{get{return ((ulong)High<<32)|Low;}}}
  [StructLayout(LayoutKind.Sequential)]struct MemoryState{public uint Length,Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended;}
  [DllImport("kernel32.dll")]static extern bool GetSystemTimes(out ClockTime idle,out ClockTime kernel,out ClockTime user);
  [DllImport("kernel32.dll",CharSet=CharSet.Auto,SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryState state);
  public MonitoringSample Read(){
   var sample=new MonitoringSample();ClockTime idle,kernel,user;
   if(GetSystemTimes(out idle,out kernel,out user)){
    if(sampled&&idle.Value>=previousIdle&&kernel.Value>=previousKernel&&user.Value>=previousUser){ulong total=(kernel.Value-previousKernel)+(user.Value-previousUser),unused=idle.Value-previousIdle;if(total>0&&unused<=total)sample.CpuUsage=100.0*(total-unused)/total;}
    previousIdle=idle.Value;previousKernel=kernel.Value;previousUser=user.Value;sampled=true;
   }
   var memory=new MemoryState{Length=(uint)Marshal.SizeOf(typeof(MemoryState))};if(GlobalMemoryStatusEx(ref memory)&&memory.Load<=100)sample.MemoryUsage=memory.Load;
   try{using(var process=new Process()){
    process.StartInfo=new ProcessStartInfo("nvidia-smi.exe","--query-gpu=clocks.current.graphics,temperature.gpu,utilization.gpu --format=csv,noheader,nounits"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    process.Start();if(!process.WaitForExit(3000)){process.Kill();return sample;}
    if(process.ExitCode==0){string line=process.StandardOutput.ReadToEnd().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0];string[] cells=line.Split(',');if(cells.Length==3){sample.GpuClock=Reading(cells[0],0,15000);sample.GpuTemperature=Reading(cells[1],-20,150);sample.GpuUsage=Reading(cells[2],0,100);}}
   }}catch{}
   sample.Captured=DateTime.Now;return sample;
  }
  static double? Reading(string text,double min,double max){double value;return Double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!Double.IsNaN(value)&&!Double.IsInfinity(value)&&value>=min&&value<=max?(double?)value:null;}
 }
 public partial class MainWindow {
  MonitoringSample monitor=new MonitoringSample();readonly MonitorReader monitorReader=new MonitorReader();bool monitorBusy;DispatcherTimer monitorTimer;
  readonly Dictionary<string,TextBlock> liveMonitorLabels=new Dictionary<string,TextBlock>();
  void BeginMonitoring(){
   if(render)return;monitorTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
   monitorTimer.Tick+=async(s,e)=>{if(closing||monitorBusy||page!="控制面板"&&page!="总览")return;monitorBusy=true;try{monitor=await Task.Run(()=>monitorReader.Read());if(!closing&&(page=="控制面板"||page=="总览"))UpdateMonitorReadings();}finally{monitorBusy=false;}};monitorTimer.Start();
  }
  string MonitorValue(double? value,string suffix){return value.HasValue?value.Value.ToString("0",CultureInfo.InvariantCulture)+suffix:"—"+suffix;}
  void TrackMonitorReading(string heading,string device,TextBlock label){
   string key=null;
   if(heading=="频率"&&device=="GPU")key="gpuClock";
   else if(heading=="温度"&&device=="GPU")key="gpuTemperature";
   else if(heading=="使用率")key=device=="CPU"?"cpuUsage":device=="GPU"?"gpuUsage":device=="DRAM"?"memoryUsage":null;
   if(key!=null)liveMonitorLabels[key]=label;
  }
  void UpdateMonitorReadings(){
   foreach(var entry in liveMonitorLabels){string value=null;
    switch(entry.Key){case "gpuClock":value=MonitorValue(monitor.GpuClock," MHz");break;case "gpuTemperature":value=MonitorValue(monitor.GpuTemperature," °C");break;case "cpuUsage":value=MonitorValue(monitor.CpuUsage," %");break;case "gpuUsage":value=MonitorValue(monitor.GpuUsage," %");break;case "memoryUsage":value=monitor.MemoryUsage.HasValue?monitor.MemoryUsage.Value+" %":"— %";break;case "state:频率":value=OriginalMonitorState(monitor.GpuClock.HasValue);break;case "state:温度":value=OriginalMonitorState(monitor.GpuTemperature.HasValue);break;case "state:使用率":value=OriginalMonitorState(monitor.CpuUsage.HasValue||monitor.GpuUsage.HasValue||monitor.MemoryUsage.HasValue);break;}
    if(value!=null){entry.Value.Text=value;if(!entry.Key.StartsWith("state:",StringComparison.Ordinal))entry.Value.Foreground=value.StartsWith("—",StringComparison.Ordinal)?muted:accent;}
   }
  }
 }
}
