// OpenAC-owned Windows/NVIDIA read-only telemetry. No kernel driver installation,
// SMU access, settings, overclocking, voltage controls or vendor HAL initialization.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RogControl {
 public sealed class TelemetryReading {
  public double? Value;
  public string Unit,Source,Quality,Reason;
  public DateTime CapturedAtUtc;
  public int? SampleIntervalMs;
  public bool IsStale(DateTime nowUtc,TimeSpan maximumAge) {return Value.HasValue&&(nowUtc-CapturedAtUtc>maximumAge||CapturedAtUtc>nowUtc.AddSeconds(5));}
 }
 public sealed class LogicalProcessorTelemetry {
  public int Group,Number;
  public string Name;
  public TelemetryReading FrequencyMhz,UsagePercent,PerformancePercent,ReportedFrequencyMhz;
 }
 public sealed class MemoryModuleTelemetry {
  public int Index,SmBiosType;
  public ulong? CapacityBytes;
  public TelemetryReading ConfiguredDataRateMTs;
 }
 public sealed class PhysicalDiskTelemetry {
  public int Index;
  public string Name;
  public TelemetryReading ActivePercent,ReadBytesPerSecond,WriteBytesPerSecond;
 }
 public sealed class NvidiaGpuTelemetry {
  // Index is valid for the current driver session, not a persistent device ID.
  public int Index;
  public string Name,StableId,IdentitySource,DriverVersion;
  public TelemetryReading GraphicsClockMhz,MemoryClockMhz,TemperatureC,UsagePercent,MemoryControllerUsagePercent,MemoryTotalMiB,MemoryUsedMiB,MemoryUsedPercent,FanSpeedPercent,VoltageV;
 }
 public sealed class WindowsTelemetrySample {
  public DateTime CapturedAtUtc;
  public TelemetryReading CpuUsagePercent,CpuFrequencyMhz,CpuPeakFrequencyMhz,MemoryUsagePercent,MemoryConfiguredDataRateMTs,BusiestDiskActivePercent;
  public TelemetryReading CpuTemperatureC,MotherboardTemperatureC,CpuCoreVoltageV,MemoryVoltageV,BclkMhz;
  public LogicalProcessorTelemetry[] LogicalProcessors=new LogicalProcessorTelemetry[0];
  public MemoryModuleTelemetry[] MemoryModules=new MemoryModuleTelemetry[0];
  public PhysicalDiskTelemetry[] Disks=new PhysicalDiskTelemetry[0];
  public NvidiaGpuTelemetry[] Gpus=new NvidiaGpuTelemetry[0];
  public string[] Warnings=new string[0];
 }
 public sealed class WindowsTelemetryReader:IDisposable {
  readonly object sync=new object();
  IntPtr query;
  readonly Dictionary<string,IntPtr> counters=new Dictionary<string,IntPtr>();
  bool attemptedPdh,disposed,haveCpuBaseline;
  ulong idleBefore,kernelBefore,userBefore;
  DateTime cpuBeforeUtc,pdhBeforeUtc;
  MemoryModuleTelemetry[] memoryModules=new MemoryModuleTelemetry[0];
  DateTime memoryReadUtc;
  string memoryError;
  static readonly Dictionary<string,string> CounterPaths=new Dictionary<string,string>{
   {"cpuFrequency",@"\Processor Information(*)\Processor Frequency"},
   {"cpuUsage",@"\Processor Information(*)\% Processor Time"},
   {"cpuPerformance",@"\Processor Information(*)\% Processor Performance"},
   {"diskIdle",@"\PhysicalDisk(*)\% Idle Time"},
   {"diskRead",@"\PhysicalDisk(*)\Disk Read Bytes/sec"},
   {"diskWrite",@"\PhysicalDisk(*)\Disk Write Bytes/sec"}
  };
  // Construction is data-only, so a render-only MainWindow never probes devices.
  public WindowsTelemetryReader(){}
  public WindowsTelemetrySample Read() {lock(sync){
   if(disposed)throw new ObjectDisposedException("WindowsTelemetryReader");
   var warning=new List<string>();DateTime now=DateTime.UtcNow;var sample=EmptySample(now);
   ReadCpuUsage(sample,now);ReadMemoryUsage(sample,now);
   InitializePdh(warning);
   var values=new Dictionary<string,Dictionary<string,double>>();
   int? interval=pdhBeforeUtc==default(DateTime)?(int?)null:Interval(pdhBeforeUtc,now);
   if(query!=IntPtr.Zero) {
    uint code=PdhCollectQueryData(query);
    if(code==0) {
     foreach(var counter in counters)try{values[counter.Key]=CounterArray(counter.Value);}catch(Exception ex){warning.Add("PDH "+counter.Key+": "+ex.Message);}
     pdhBeforeUtc=now;
    }else warning.Add("Windows performance counters could not be sampled (0x"+code.ToString("X8")+").");
   }
   sample.LogicalProcessors=BuildCpu(Map(values,"cpuFrequency"),Map(values,"cpuUsage"),Map(values,"cpuPerformance"),now,interval);
   if(GetActiveProcessorGroupCount()>1&&sample.LogicalProcessors.Length==GetActiveProcessorCount(0xffff)&&sample.LogicalProcessors.All(core=>core.UsagePercent.Value.HasValue))
    sample.CpuUsagePercent=Known(sample.LogicalProcessors.Average(core=>core.UsagePercent.Value.Value),"%","PDH % Processor Time mean across every active processor group","sampled",now,interval,null);
   if(sample.LogicalProcessors.Length==0)try{sample.LogicalProcessors=PowerInformation(now);}catch(Exception ex){warning.Add("Windows processor frequency: "+ex.Message);}
   var frequencies=sample.LogicalProcessors.Select(core=>core.FrequencyMhz).Where(item=>item.Value.HasValue).ToArray();
   if(frequencies.Length!=0) {
    string quality=frequencies.All(item=>item.Quality=="osEstimate")?"osEstimate":"osReported";
    sample.CpuFrequencyMhz=Known(frequencies.Average(item=>item.Value.Value),"MHz","Mean of sampled logical-processor frequency values",quality,now,interval,"Windows estimate/report; not a directly measured BCLK or CPU sensor.");
    sample.CpuPeakFrequencyMhz=Known(frequencies.Max(item=>item.Value.Value),"MHz","Maximum of sampled logical-processor frequency values",quality,now,interval,"Windows estimate/report; not a directly measured BCLK or CPU sensor.");
   }
   sample.Disks=BuildDisks(Map(values,"diskIdle"),Map(values,"diskRead"),Map(values,"diskWrite"),now,interval);
   var active=sample.Disks.Select(disk=>disk.ActivePercent).Where(item=>item.Value.HasValue).ToArray();
   if(active.Length!=0)sample.BusiestDiskActivePercent=Known(active.Max(item=>item.Value.Value),"%","Maximum physical-disk active time (100 minus PDH % Idle Time)","sampled",now,interval,null);
   if((now-memoryReadUtc).TotalSeconds>=60)ReadMemoryModules(now);
   sample.MemoryModules=memoryModules;sample.MemoryConfiguredDataRateMTs=CommonMemoryRate(memoryModules,now);
   if(memoryError!=null)warning.Add(memoryError);
   try{sample.Gpus=ReadNvidia();}catch(Exception ex){warning.Add("NVIDIA telemetry: "+ex.Message);}
   sample.Warnings=warning.Distinct().ToArray();sample.CapturedAtUtc=DateTime.UtcNow;return sample;
  }}
  static WindowsTelemetrySample EmptySample(DateTime now) {return new WindowsTelemetrySample{
   CapturedAtUtc=now,CpuUsagePercent=Missing("%","GetSystemTimes","A second valid CPU time sample is required.",now),
   CpuFrequencyMhz=Missing("MHz","Windows processor counters","No confirmed Windows processor frequency is available.",now),
   CpuPeakFrequencyMhz=Missing("MHz","Windows processor counters","No confirmed Windows processor frequency is available.",now),
   MemoryUsagePercent=Missing("%","GlobalMemoryStatusEx","Windows memory usage is unavailable.",now),
   MemoryConfiguredDataRateMTs=Missing("MT/s","SMBIOS Type 17 via Win32_PhysicalMemory","Configured DDR rate is unavailable.",now),
   BusiestDiskActivePercent=Missing("%","PDH PhysicalDisk % Idle Time","A second valid disk sample is required.",now),
   CpuTemperatureC=Missing("°C","Windows standard interfaces","Windows does not expose a verified CPU package temperature through this reader.",now),
   MotherboardTemperatureC=Missing("°C","Windows standard interfaces","No identified motherboard temperature sensor is connected.",now),
   CpuCoreVoltageV=Missing("V","Windows standard interfaces","No measured CPU VCore source is connected; VID is not used as VCore.",now),
   MemoryVoltageV=Missing("V","Windows standard interfaces","SMBIOS configured voltage is not a measured DRAM voltage.",now),
   BclkMhz=Missing("MHz","Windows standard interfaces","No verified BCLK source is connected; no nominal 100 MHz is assumed.",now)
  };}
  void ReadCpuUsage(WindowsTelemetrySample sample,DateTime now) {
   if(GetActiveProcessorGroupCount()>1){sample.CpuUsagePercent=Missing("%","GetSystemTimes","GetSystemTimes only reports the calling processor group; a complete PDH sample is required for this system.",now);return;}
   FileTime idle,kernel,user;if(!GetSystemTimes(out idle,out kernel,out user))return;
   if(haveCpuBaseline)sample.CpuUsagePercent=CpuUsage(idleBefore,kernelBefore,userBefore,idle.Value,kernel.Value,user.Value,now,Interval(cpuBeforeUtc,now));
   idleBefore=idle.Value;kernelBefore=kernel.Value;userBefore=user.Value;cpuBeforeUtc=now;haveCpuBaseline=true;
  }
  internal static TelemetryReading CpuUsage(ulong oldIdle,ulong oldKernel,ulong oldUser,ulong idle,ulong kernel,ulong user,DateTime now,int? interval) {
   if(idle<oldIdle||kernel<oldKernel||user<oldUser)return Missing("%","GetSystemTimes","CPU time counters reset.",now);
   ulong dk=kernel-oldKernel,du=user-oldUser,di=idle-oldIdle;
   if(UInt64.MaxValue-dk<du||dk+du==0||di>dk+du)return Missing("%","GetSystemTimes","CPU time interval is invalid.",now);
   return Known(100.0*(1.0-(double)di/(dk+du)),"%","GetSystemTimes idle/kernel/user delta","sampled",now,interval,null);
  }
  static void ReadMemoryUsage(WindowsTelemetrySample sample,DateTime now) {
   var memory=new MemoryStatus{Length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};if(GlobalMemoryStatusEx(ref memory)&&memory.MemoryLoad<=100)sample.MemoryUsagePercent=Known(memory.MemoryLoad,"%","GlobalMemoryStatusEx.dwMemoryLoad","osReported",now,null,null);
  }
  void InitializePdh(List<string> warning) {
   if(attemptedPdh)return;attemptedPdh=true;uint status=PdhOpenQueryW(null,UIntPtr.Zero,out query);
   if(status!=0){query=IntPtr.Zero;warning.Add("Windows PDH query is unavailable (0x"+status.ToString("X8")+").");return;}
   foreach(var path in CounterPaths){IntPtr counter;uint code=PdhAddEnglishCounterW(query,path.Value,UIntPtr.Zero,out counter);if(code==0)counters[path.Key]=counter;else warning.Add("Windows counter unavailable: "+path.Key+" (0x"+code.ToString("X8")+").");}
  }
  Dictionary<string,double> CounterArray(IntPtr counter) {
   uint bytes=0,count=0;const uint format=0x200|0x8000;uint code=PdhGetFormattedCounterArrayW(counter,format,ref bytes,ref count,IntPtr.Zero);
   if(code==0&&count==0)return new Dictionary<string,double>();
   if(code!=0x800007D2||bytes==0||bytes>1048576)throw new InvalidDataException("Counter array is unavailable (0x"+code.ToString("X8")+").");
   IntPtr buffer=Marshal.AllocHGlobal((int)bytes);try {
    uint capacity=bytes;code=PdhGetFormattedCounterArrayW(counter,format,ref bytes,ref count,buffer);int itemSize=Marshal.SizeOf(typeof(CounterItem));
    if(code!=0||bytes>capacity||count>4096||(ulong)count*(uint)itemSize>capacity)throw new InvalidDataException("Invalid formatted-counter array.");
    var result=new Dictionary<string,double>(StringComparer.Ordinal);
    for(int i=0;i<count;i++){var item=(CounterItem)Marshal.PtrToStructure(IntPtr.Add(buffer,i*itemSize),typeof(CounterItem));
     string name=CounterName(item.Name,buffer,capacity);if((item.Value.Status==0||item.Value.Status==1)&&Finite(item.Value.DoubleValue)&&!result.ContainsKey(name))result.Add(name,item.Value.DoubleValue);
    }return result;
   }finally{Marshal.FreeHGlobal(buffer);}
  }
  static string CounterName(IntPtr name,IntPtr buffer,uint bytes) {
   long offset=name.ToInt64()-buffer.ToInt64();if(offset<0||offset>=bytes||offset%2!=0)throw new InvalidDataException("Counter name is outside its buffer.");
   var text=new StringBuilder();for(int i=0;i<256&&offset+i*2+2<=bytes;i++){char c=(char)Marshal.ReadInt16(name,i*2);if(c==0)return text.ToString();text.Append(c);}throw new InvalidDataException("Invalid counter instance name.");
  }
  static Dictionary<string,double> Map(Dictionary<string,Dictionary<string,double>> values,string key){Dictionary<string,double> value;return values.TryGetValue(key,out value)?value:new Dictionary<string,double>();}
  internal static LogicalProcessorTelemetry[] BuildCpu(Dictionary<string,double> frequency,Dictionary<string,double> usage,Dictionary<string,double> performance,DateTime now,int? interval) {
   var list=new List<LogicalProcessorTelemetry>();foreach(string key in frequency.Keys.Union(usage.Keys).Union(performance.Keys)) {
    var match=Regex.Match(key,@"\A([0-9]{1,3}),([0-9]{1,3})\z");if(!match.Success)continue;int group=Int32.Parse(match.Groups[1].Value),number=Int32.Parse(match.Groups[2].Value);double value;
    var nominal=frequency.TryGetValue(key,out value)?Number(value,1,100000,"MHz","PDH Processor Information / Processor Frequency","osReported",now,null,"Windows-reported reference frequency, not a live hardware clock measurement."):Missing("MHz","PDH Processor Frequency","Reference frequency is unavailable.",now);
    var perf=performance.TryGetValue(key,out value)&&interval.HasValue&&interval>=1000?Number(value,0,2000,"%","PDH Processor Information / % Processor Performance","sampled",now,interval,"Performance relative to the reference frequency; may exceed 100% during boost."):Missing("%","PDH % Processor Performance","A second sample at least one second apart is required.",now);
    var effective=nominal.Value.HasValue&&perf.Value.HasValue?Number(nominal.Value.Value*perf.Value.Value/100.0,0,100000,"MHz","PDH Processor Frequency × % Processor Performance / 100","osEstimate",now,interval,"Interval estimate for one logical processor; not directly measured instantaneous core MHz."):nominal;
    var busy=usage.TryGetValue(key,out value)&&interval.HasValue&&interval>=1000?Number(value,0,100,"%","PDH Processor Information / % Processor Time","sampled",now,interval,null):Missing("%","PDH % Processor Time","A second sample at least one second apart is required.",now);
    list.Add(new LogicalProcessorTelemetry{Group=group,Number=number,Name="逻辑处理器 "+group+":"+number,FrequencyMhz=effective,ReportedFrequencyMhz=nominal,UsagePercent=busy,PerformancePercent=perf});
   }return list.OrderBy(core=>core.Group).ThenBy(core=>core.Number).ToArray();
  }
  static LogicalProcessorTelemetry[] PowerInformation(DateTime now) {
   if(GetActiveProcessorGroupCount()!=1)throw new InvalidOperationException("The fallback frequency API is limited to one processor group.");
   uint count=GetActiveProcessorCount(0xffff);if(count==0||count>256)throw new InvalidDataException("Unexpected logical-processor count.");
   int bytes=checked((int)count*Marshal.SizeOf(typeof(ProcessorPower)));IntPtr buffer=Marshal.AllocHGlobal(bytes);try {
    uint code=CallNtPowerInformation(11,IntPtr.Zero,0,buffer,(uint)bytes);if(code!=0)throw new InvalidOperationException("ProcessorInformation query failed (0x"+code.ToString("X8")+").");var result=new List<LogicalProcessorTelemetry>();
    for(int i=0;i<count;i++){var power=(ProcessorPower)Marshal.PtrToStructure(IntPtr.Add(buffer,i*24),typeof(ProcessorPower));if(power.Number>=count||result.Any(core=>core.Number==power.Number))throw new InvalidDataException("Invalid processor-information number.");
     var mhz=Number(power.CurrentMhz,1,100000,"MHz","CallNtPowerInformation.ProcessorInformation.CurrentMhz","osReported",now,null,"Maximum specified MHz multiplied by current throttle; may equal nominal speed on modern CPUs.");
     result.Add(new LogicalProcessorTelemetry{Group=0,Number=(int)power.Number,Name="逻辑处理器 0:"+power.Number,FrequencyMhz=mhz,ReportedFrequencyMhz=mhz,UsagePercent=Missing("%","Windows processor counters","Per-processor usage is unavailable.",now),PerformancePercent=Missing("%","Windows processor counters","Performance ratio is unavailable.",now)});
    }return result.ToArray();
   }finally{Marshal.FreeHGlobal(buffer);}
  }
  internal static PhysicalDiskTelemetry[] BuildDisks(Dictionary<string,double> idle,Dictionary<string,double> read,Dictionary<string,double> write,DateTime now,int? interval) {
   var result=new List<PhysicalDiskTelemetry>();foreach(string key in idle.Keys.Union(read.Keys).Union(write.Keys)) {
    var match=Regex.Match(key,@"\A([0-9]{1,4})(?:\s|$)");if(!match.Success)continue;int index=Int32.Parse(match.Groups[1].Value);if(result.Any(item=>item.Index==index))continue;double value;
    var active=idle.TryGetValue(key,out value)&&interval.HasValue&&interval>=1000?DiskActive(value,now,interval):Missing("%","PDH PhysicalDisk / % Idle Time","A second sample at least one second apart is required.",now);
    result.Add(new PhysicalDiskTelemetry{Index=index,Name="磁盘 "+index,ActivePercent=active,
     ReadBytesPerSecond=read.TryGetValue(key,out value)&&interval.HasValue&&interval>=1000?Number(value,0,1e15,"B/s","PDH PhysicalDisk / Disk Read Bytes/sec","sampled",now,interval,null):Missing("B/s","PDH PhysicalDisk","A second read-rate sample is required.",now),
     WriteBytesPerSecond=write.TryGetValue(key,out value)&&interval.HasValue&&interval>=1000?Number(value,0,1e15,"B/s","PDH PhysicalDisk / Disk Write Bytes/sec","sampled",now,interval,null):Missing("B/s","PDH PhysicalDisk","A second write-rate sample is required.",now)});
   }return result.OrderBy(item=>item.Index).ToArray();
  }
  internal static TelemetryReading DiskActive(double idle,DateTime now,int? interval) {return Finite(idle)&&idle>=0&&idle<=100?Known(100-idle,"%","100 minus PDH PhysicalDisk / % Idle Time","sampled",now,interval,null):Missing("%","PDH PhysicalDisk / % Idle Time","Idle percentage is outside its valid range.",now);}
  void ReadMemoryModules(DateTime now) {
   memoryReadUtc=now;memoryError=null;var list=new List<MemoryModuleTelemetry>();
   try{using(var query=new ManagementObjectSearcher("root\\CIMV2","SELECT ConfiguredClockSpeed,SMBIOSMemoryType,Capacity FROM Win32_PhysicalMemory",new EnumerationOptions{Timeout=TimeSpan.FromSeconds(2),ReturnImmediately=false}))using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){if(list.Count>=256)throw new InvalidDataException("Unexpected memory-module count.");
    int type=Convert.ToInt32(row["SMBIOSMemoryType"],CultureInfo.InvariantCulture);double speed=Convert.ToDouble(row["ConfiguredClockSpeed"],CultureInfo.InvariantCulture);ulong capacity;ulong? size=UInt64.TryParse(Convert.ToString(row["Capacity"],CultureInfo.InvariantCulture),out capacity)&&capacity>0?(ulong?)capacity:null;
    list.Add(new MemoryModuleTelemetry{Index=list.Count,SmBiosType=type,CapacityBytes=size,ConfiguredDataRateMTs=MemoryRate(type,speed,now)});
   }memoryModules=list.ToArray();}catch(Exception ex){memoryModules=new MemoryModuleTelemetry[0];memoryError="Configured memory rate: "+ex.Message;}
  }
  internal static TelemetryReading MemoryRate(int type,double rate,DateTime now) {
   if(!new[]{18,19,20,24,26,27,28,29,30,34,35}.Contains(type))return Missing("MT/s","SMBIOS Type 17 / Win32_PhysicalMemory.ConfiguredClockSpeed","Memory technology is not confirmed as DDR/LPDDR.",now);
   if(rate==65535)return Missing("MT/s","SMBIOS Type 17 / Win32_PhysicalMemory.ConfiguredClockSpeed","SMBIOS 0xFFFF is an extended-speed marker, not a confirmed memory rate.",now);
   return Number(rate,1,100000,"MT/s","SMBIOS Type 17 / Win32_PhysicalMemory.ConfiguredClockSpeed","configured",now,null,"Configured effective DDR data rate, not a sampled DRAM clock. It is not divided into invented live MHz.");
  }
  internal static TelemetryReading CommonMemoryRate(MemoryModuleTelemetry[] modules,DateTime now) {
   if(modules==null||modules.Length==0||modules.Any(item=>item.ConfiguredDataRateMTs==null||!item.ConfiguredDataRateMTs.Value.HasValue))return Missing("MT/s","SMBIOS Type 17","Not every memory module has a confirmed configured DDR rate.",now);
   double first=modules[0].ConfiguredDataRateMTs.Value.Value;if(modules.Any(item=>item.ConfiguredDataRateMTs.Value!=first))return Missing("MT/s","SMBIOS Type 17","Memory modules report different configured rates; see individual modules.",now);
   return Known(first,"MT/s","SMBIOS Type 17 configured rate shared by all memory modules","configured",modules.Min(item=>item.ConfiguredDataRateMTs.CapturedAtUtc),null,"Configuration metadata, not a live DRAM frequency sensor.");
  }
  static NvidiaGpuTelemetry[] ReadNvidia() {
   string executable=Path.Combine(Environment.SystemDirectory,"nvidia-smi.exe");if(!File.Exists(executable))executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"NVIDIA Corporation","NVSMI","nvidia-smi.exe");
   if(!File.Exists(executable))return new NvidiaGpuTelemetry[0];
   const string arguments="--query-gpu=index,name,uuid,driver_version,clocks.current.graphics,clocks.current.memory,temperature.gpu,utilization.gpu,utilization.memory,memory.total,memory.used,fan.speed --format=csv,noheader,nounits";
   var start=new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};string output;
   using(var process=Process.Start(start)){var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(3000)){try{process.Kill();}catch{}throw new TimeoutException("NVIDIA read-only query timed out.");}output=stdout.GetAwaiter().GetResult();stderr.GetAwaiter().GetResult();if(process.ExitCode!=0)throw new InvalidOperationException("NVIDIA read-only query failed (code "+process.ExitCode+").");}
   return ParseNvidiaCsv(output,DateTime.UtcNow);
  }
  internal static NvidiaGpuTelemetry[] ParseNvidiaCsv(string csv,DateTime now) {
   if(csv==null||csv.Length>65536)throw new InvalidDataException("Unexpected NVIDIA response size.");var result=new List<NvidiaGpuTelemetry>();
   foreach(string line in csv.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
    string[] cells=CsvCells(line);int index;if(cells.Length!=12||!Int32.TryParse(cells[0],NumberStyles.None,CultureInfo.InvariantCulture,out index)||index<0||index>255||result.Any(item=>item.Index==index)||cells[1].Length>160||!Regex.IsMatch(cells[2],@"\AGPU-[0-9a-fA-F\-]{16,80}\z")||cells[3].Length>80||cells[3].Any(Char.IsControl))throw new InvalidDataException("Unexpected NVIDIA GPU row.");
    string stable;using(var hash=SHA256.Create())stable="nvidia-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(cells[2].ToUpperInvariant()))).Replace("-","").ToLowerInvariant();
    if(result.Any(item=>item.StableId==stable))throw new InvalidDataException("Duplicate NVIDIA hardware identity.");
    Func<int,double,double,string,string,TelemetryReading> field=(at,min,max,unit,name)=>Scalar(cells[at],min,max,unit,"NVIDIA nvidia-smi / "+name,"driverReported",now);
    var gpu=new NvidiaGpuTelemetry{Index=index,Name=cells[1],StableId=stable,IdentitySource="NVIDIA hardware UUID SHA256 (raw UUID omitted)",DriverVersion=cells[3],GraphicsClockMhz=field(4,0,100000,"MHz","clocks.current.graphics"),MemoryClockMhz=field(5,0,100000,"MHz","clocks.current.memory"),TemperatureC=field(6,-40,180,"°C","temperature.gpu"),UsagePercent=field(7,0,100,"%","utilization.gpu"),MemoryControllerUsagePercent=field(8,0,100,"%","utilization.memory"),MemoryTotalMiB=field(9,1,1e9,"MiB","memory.total"),MemoryUsedMiB=field(10,0,1e9,"MiB","memory.used"),FanSpeedPercent=field(11,0,200,"%","fan.speed"),VoltageV=Missing("V","NVIDIA public telemetry","Current NVIDIA SMI removed graphics-voltage reporting. Public NVAPI P-state voltages describe settings, not measured VCore.",now)};
    gpu.MemoryUsedPercent=MemoryUsedPercent(gpu.MemoryUsedMiB,gpu.MemoryTotalMiB,now);result.Add(gpu);
   }return result.OrderBy(item=>item.Index).ToArray();
  }
  internal static TelemetryReading MemoryUsedPercent(TelemetryReading used,TelemetryReading total,DateTime now) {return used!=null&&total!=null&&used.Value.HasValue&&total.Value.HasValue&&total.Value>0&&used.Value>=0&&used.Value<=total.Value?Known(used.Value.Value/total.Value.Value*100,"%","NVIDIA memory.used / memory.total × 100","calculated",now,null,"VRAM allocation ratio, distinct from memory-controller utilization."):Missing("%","NVIDIA memory.used / memory.total","A valid used and total VRAM pair is unavailable.",now);}
  internal static string[] CsvCells(string line) {
   var result=new List<string>();var current=new StringBuilder();bool quoted=false,closed=false;
   for(int i=0;i<line.Length;i++){char c=line[i];if(quoted){if(c=='"'){if(i+1<line.Length&&line[i+1]=='"'){current.Append('"');i++;}else{quoted=false;closed=true;}}else current.Append(c);}
    else if(c==','){result.Add(current.ToString().Trim());current.Clear();closed=false;}
    else if(c=='"'){if(current.ToString().Trim().Length!=0||closed)throw new InvalidDataException("Invalid CSV quoting.");current.Clear();quoted=true;}
    else{if(closed&&!Char.IsWhiteSpace(c))throw new InvalidDataException("Invalid CSV suffix.");current.Append(c);}
   }if(quoted)throw new InvalidDataException("Unterminated CSV field.");result.Add(current.ToString().Trim());return result.ToArray();
  }
  internal static TelemetryReading Scalar(string supplied,double min,double max,string unit,string source,string quality,DateTime now) {
   double value;return Double.TryParse(supplied,NumberStyles.Float,CultureInfo.InvariantCulture,out value)?Number(value,min,max,unit,source,quality,now,null,null):Missing(unit,source,"The provider returned no supported numeric reading.",now);
  }
  internal static TelemetryReading Number(double value,double min,double max,string unit,string source,string quality,DateTime now,int? interval,string reason){return Finite(value)&&value>=min&&value<=max?Known(value,unit,source,quality,now,interval,reason):Missing(unit,source,"Reading is outside the accepted range.",now);}
  static bool Finite(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
  static int? Interval(DateTime before,DateTime now){double milliseconds=(now-before).TotalMilliseconds;return milliseconds>=0&&milliseconds<=Int32.MaxValue?(int?)milliseconds:null;}
  static TelemetryReading Known(double value,string unit,string source,string quality,DateTime now,int? interval,string reason){return new TelemetryReading{Value=value,Unit=unit,Source=source,Quality=quality,Reason=reason,CapturedAtUtc=now,SampleIntervalMs=interval};}
  static TelemetryReading Missing(string unit,string source,string reason,DateTime now){return new TelemetryReading{Value=null,Unit=unit,Source=source,Quality="unavailable",Reason=reason,CapturedAtUtc=now};}
  public void Dispose(){lock(sync){if(disposed)return;disposed=true;if(query!=IntPtr.Zero){PdhCloseQuery(query);query=IntPtr.Zero;}counters.Clear();}}
  [StructLayout(LayoutKind.Sequential)]struct FileTime{public uint Low,High;public ulong Value{get{return ((ulong)High<<32)|Low;}}}
  [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint Length,MemoryLoad;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;}
  [StructLayout(LayoutKind.Sequential)]struct ProcessorPower{public uint Number,MaxMhz,CurrentMhz,MhzLimit,MaxIdleState,CurrentIdleState;}
  [StructLayout(LayoutKind.Explicit,Size=16)]struct CounterValue{[FieldOffset(0)]public uint Status;[FieldOffset(8)]public double DoubleValue;}
  [StructLayout(LayoutKind.Sequential)]struct CounterItem{public IntPtr Name;public CounterValue Value;}
  [DllImport("kernel32.dll")]static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
  [DllImport("kernel32.dll",CharSet=CharSet.Auto,SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus state);
  [DllImport("kernel32.dll")]static extern uint GetActiveProcessorCount(ushort group);
  [DllImport("kernel32.dll")]static extern ushort GetActiveProcessorGroupCount();
  [DllImport("powrprof.dll")]static extern uint CallNtPowerInformation(int information,IntPtr input,uint inputLength,IntPtr output,uint outputLength);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhOpenQueryW(string source,UIntPtr data,out IntPtr query);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhAddEnglishCounterW(IntPtr query,string path,UIntPtr data,out IntPtr counter);
  [DllImport("pdh.dll")]static extern uint PdhCollectQueryData(IntPtr query);
  [DllImport("pdh.dll",CharSet=CharSet.Unicode)]static extern uint PdhGetFormattedCounterArrayW(IntPtr counter,uint format,ref uint bytes,ref uint count,IntPtr buffer);
  [DllImport("pdh.dll")]static extern uint PdhCloseQuery(IntPtr query);
 }
}
