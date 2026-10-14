// Default: synthetic transformations only. --read performs explicitly requested
// Windows/NVIDIA read-only sampling; no settings or driver installation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using RogControl;

internal static class WindowsTelemetryTests {
 static int checks;
 static DateTime now=new DateTime(2026,10,13,12,0,0,DateTimeKind.Utc);
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,message);}
 static Dictionary<string,double> Counters(params object[] pairs){var data=new Dictionary<string,double>();for(int i=0;i<pairs.Length;i+=2)data.Add((string)pairs[i],Convert.ToDouble(pairs[i+1]));return data;}
 static string GpuRow(int index,string uuid,string values){return index+", Synthetic NVIDIA GPU, "+uuid+", 600.00, "+values;}
 static TelemetryReading Value(double value){return new TelemetryReading{Value=value,CapturedAtUtc=now};}
 static int Main(string[] args){try {
  if(args.Length==1&&args[0]=="--read") {using(var reader=new WindowsTelemetryReader()){var first=reader.Read();Thread.Sleep(1100);var second=reader.Read();Console.WriteLine(new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(new{first,second}));}return 0;}
  if(args.Length!=0&&!(args.Length==1&&args[0]=="--self-test"))throw new ArgumentException("Unsupported test option.");
  Run();Console.WriteLine("Windows telemetry: "+checks+" pure checks passed; native reads 0, WMI 0, NVIDIA subprocesses 0, hardware writes 0.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex.GetBaseException().Message);return 1;}}
 static void Run(){
  Check(WindowsTelemetryReader.CpuUsage(0,0,0,50,100,0,now,1000).Value==50,"CPU idle/kernel delta");
  Check(WindowsTelemetryReader.CpuUsage(0,0,0,100,100,0,now,1000).Value==0,"Idle CPU is zero busy");
  Check(WindowsTelemetryReader.CpuUsage(0,0,0,0,100,0,now,1000).Value==100,"Busy CPU is 100 percent");
  Check(WindowsTelemetryReader.CpuUsage(10,10,10,9,11,11,now,1000).Value==null,"Counter rollback unknown");
  Check(WindowsTelemetryReader.CpuUsage(10,10,10,10,10,10,now,1000).Value==null,"Zero time interval unknown");
  Check(WindowsTelemetryReader.CpuUsage(0,0,0,200,100,0,now,1000).Value==null,"Invalid idle time unknown");
  Check(WindowsTelemetryReader.CpuUsage(0,0,0,1,UInt64.MaxValue,1,now,1000).Value==null,"Counter addition overflow unknown");
  var cpu=WindowsTelemetryReader.BuildCpu(Counters("0,0",4000,"0,1",4000,"0,_Total",4000,"_Total",4000),Counters("0,0",25,"0,1",50),Counters("0,0",125,"0,1",75),now,1000);
  Check(cpu.Length==2&&cpu[0].Group==0&&cpu[0].Number==0,"Logical processor names parsed, totals excluded");
  Check(cpu[0].FrequencyMhz.Value==5000&&cpu[1].FrequencyMhz.Value==3000,"Boost ratio produces explicit interval estimate");
  Check(cpu[0].FrequencyMhz.Quality=="osEstimate"&&cpu[0].FrequencyMhz.SampleIntervalMs==1000,"Estimate and sampling interval preserved");
  Check(cpu[0].ReportedFrequencyMhz.Value==4000&&cpu[0].ReportedFrequencyMhz.Quality=="osReported","Reference frequency stays distinct");
  Check(cpu[0].UsagePercent.Value==25&&cpu[1].UsagePercent.Value==50,"Per-logical-processor usage");
  var first=WindowsTelemetryReader.BuildCpu(Counters("0,0",4000),Counters("0,0",25),Counters("0,0",125),now,null)[0];
  Check(first.UsagePercent.Value==null&&first.PerformancePercent.Value==null,"No invented first-rate sample");
  Check(first.FrequencyMhz.Quality=="osReported"&&first.FrequencyMhz.Value==4000,"Warmup frequency does not claim measured boost");
  Check(WindowsTelemetryReader.BuildCpu(Counters("0,0",4000),Counters("0,0",25),Counters("0,0",125),now,500)[0].PerformancePercent.Value==null,"Too-short performance interval remains unknown");
  var groups=WindowsTelemetryReader.BuildCpu(Counters("1,3",4500,"0,12",4500,"0,2",4500),Counters(),Counters(),now,null);
  Check(groups[0].Number==2&&groups[1].Number==12&&groups[2].Group==1,"Multi-group numeric order");
  Check(WindowsTelemetryReader.BuildCpu(Counters("0,0",Double.NaN),Counters("0,0",101),Counters("0,0",Double.PositiveInfinity),now,1000)[0].FrequencyMhz.Value==null,"Invalid frequency/performance unknown");
  Check(WindowsTelemetryReader.BuildCpu(Counters("0,0",4000),Counters("0,0",101),Counters(),now,1000)[0].UsagePercent.Value==null,"Usage outside percent range unknown");
  Check(WindowsTelemetryReader.DiskActive(75,now,1000).Value==25,"Disk uses inverse idle time");
  Check(WindowsTelemetryReader.DiskActive(0,now,1000).Value==100,"Fully active disk");
  Check(WindowsTelemetryReader.DiskActive(100,now,1000).Value==0,"Fully idle disk");
  foreach(double invalid in new[]{-1,101,Double.NaN,Double.PositiveInfinity})Check(WindowsTelemetryReader.DiskActive(invalid,now,1000).Value==null,"Invalid idle not capped or invented");
  var disks=WindowsTelemetryReader.BuildDisks(Counters("0 C: D:",90,"1 E:",50,"_Total",140),Counters("0 C: D:",10000),Counters("0 C: D:",20000),now,1000);
  Check(disks.Length==2&&disks[0].Index==0&&disks[1].ActivePercent.Value==50,"Physical disks retained, total excluded");
  Check(disks[0].Name=="磁盘 0"&&!disks[0].Name.Contains("C:"),"Disk labels do not expose volume mappings");
  Check(disks[0].ReadBytesPerSecond.Value==10000&&disks[0].WriteBytesPerSecond.Unit=="B/s","Disk transfer rates retain units");
  Check(WindowsTelemetryReader.BuildDisks(Counters("0 C:",90),Counters(),Counters(),now,null)[0].ActivePercent.Value==null,"First disk sample unknown");
  var ddr=WindowsTelemetryReader.MemoryRate(34,5600,now);
  Check(ddr.Value==5600&&ddr.Unit=="MT/s"&&ddr.Quality=="configured","DDR5 configured effective rate");
  Check(ddr.Value!=2800,"No invented live DRAM MHz");
  Check(WindowsTelemetryReader.MemoryRate(26,3200,now).Value==3200,"DDR4 configured rate");
  Check(WindowsTelemetryReader.MemoryRate(0,5600,now).Value==null,"Unknown memory technology unknown");
  Check(WindowsTelemetryReader.MemoryRate(34,0,now).Value==null,"SMBIOS zero is unknown");
  Check(WindowsTelemetryReader.MemoryRate(34,65535,now).Value==null,"SMBIOS extended-speed marker is not actual rate");
  var modules=new[]{new MemoryModuleTelemetry{ConfiguredDataRateMTs=ddr},new MemoryModuleTelemetry{ConfiguredDataRateMTs=WindowsTelemetryReader.MemoryRate(34,5600,now)}};
  Check(WindowsTelemetryReader.CommonMemoryRate(modules,now).Value==5600,"Common DIMM rate");
  modules[1].ConfiguredDataRateMTs=WindowsTelemetryReader.MemoryRate(34,5200,now);Check(WindowsTelemetryReader.CommonMemoryRate(modules,now).Value==null,"Different DIMM rates not averaged");
  Check(WindowsTelemetryReader.CommonMemoryRate(new MemoryModuleTelemetry[0],now).Value==null,"Missing memory metadata unknown");
  modules[1].ConfiguredDataRateMTs=null;Check(WindowsTelemetryReader.CommonMemoryRate(modules,now).Value==null,"Partial memory metadata unknown");
  Check(WindowsTelemetryReader.CsvCells("0, \"Synthetic, quoted GPU\", 1")[1]=="Synthetic, quoted GPU","CSV quoted comma");
  Check(WindowsTelemetryReader.CsvCells("\"a\"\"b\", 2")[0]=="a\"b","CSV escaped quote");
  Reject(()=>WindowsTelemetryReader.CsvCells("\"missing"),"Malformed quote rejected");
  Reject(()=>WindowsTelemetryReader.CsvCells("\"closed\"extra"),"CSV trailing garbage rejected");
  string uuidA="GPU-00000000-0000-0000-0000-000000000001",uuidB="GPU-00000000-0000-0000-0000-000000000002";
  var gpu=WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(0,uuidA,"1000, 14001, 45, 20, 60, 16000, 4000, 31"),now)[0];
  Check(gpu.Index==0&&gpu.Name=="Synthetic NVIDIA GPU"&&gpu.DriverVersion=="600.00","GPU identity and driver fields");
  Check(gpu.StableId.StartsWith("nvidia-")&&gpu.StableId.Length==71&&!gpu.StableId.Contains(uuidA),"Stable hardware identity hashes raw UUID");
  Check(gpu.GraphicsClockMhz.Value==1000&&gpu.MemoryClockMhz.Value==14001,"Driver clock domains remain unscaled");
  Check(gpu.TemperatureC.Value==45&&gpu.TemperatureC.Unit=="°C","GPU temperature units");
  Check(gpu.MemoryUsedPercent.Value==25&&gpu.MemoryControllerUsagePercent.Value==60,"Allocation separate from memory controller use");
  Check(gpu.FanSpeedPercent.Value==31&&gpu.FanSpeedPercent.Unit=="%","GPU fan percentage not invented RPM");
  Check(gpu.VoltageV.Value==null&&gpu.VoltageV.Reason.Contains("settings"),"P-state settings do not become measured voltage");
  var multiple=WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(1,uuidB,"1200, 2000, N/A, 50, 10, 8000, 1000, N/A")+"\n"+GpuRow(0,uuidA,"1000, 14001, 45, 20, 60, 16000, 4000, 31"),now);
  Check(multiple.Length==2&&multiple[0].Index==0&&multiple[1].Index==1,"Multiple GPUs ordered without merging");
  Check(multiple[1].TemperatureC.Value==null&&multiple[1].FanSpeedPercent.Value==null,"N/A fields unknown independently");
  Check(WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(7,uuidA,"1000, 14001, 45, 20, 60, 16000, 4000, 31"),now)[0].StableId==gpu.StableId,"GPU index change does not change stable ID");
  Reject(()=>WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(0,uuidA,"1,2,3,4,5,6,7,8")+"\n"+GpuRow(0,uuidB,"1,2,3,4,5,6,7,8"),now),"Duplicate GPU indices rejected");
  Reject(()=>WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(0,uuidA,"1,2,3,4,5,6,7,8")+"\n"+GpuRow(1,uuidA,"1,2,3,4,5,6,7,8"),now),"Duplicate hardware identities rejected");
  Reject(()=>WindowsTelemetryReader.ParseNvidiaCsv("0, GPU, 1",now),"Truncated GPU row rejected");
  Reject(()=>WindowsTelemetryReader.ParseNvidiaCsv(GpuRow(0,"not-a-uuid","1,2,3,4,5,6,7,8"),now),"Unconfirmed GPU identity rejected");
  Reject(()=>WindowsTelemetryReader.ParseNvidiaCsv(new string('x',65537),now),"Unbounded response rejected");
  Check(WindowsTelemetryReader.MemoryUsedPercent(Value(2),Value(1),now).Value==null,"Used VRAM cannot exceed total");
  Check(WindowsTelemetryReader.MemoryUsedPercent(Value(2),Value(0),now).Value==null,"Zero total VRAM unknown");
  foreach(string invalid in new[]{"N/A","[Not Supported]","NaN","Infinity","-1","101"})Check(WindowsTelemetryReader.Scalar(invalid,0,100,"%","synthetic","sampled",now).Value==null,"Unsupported/invalid scalar remains unknown");
  var reading=Value(50);Check(!reading.IsStale(now.AddSeconds(1),TimeSpan.FromSeconds(3)),"Recent reading not stale");Check(reading.IsStale(now.AddSeconds(4),TimeSpan.FromSeconds(3)),"Stale reading detectable");Check(reading.IsStale(now.AddSeconds(-10),TimeSpan.FromSeconds(3)),"Future clock mismatch detectable");
  using(var reader=new WindowsTelemetryReader()){Check((IntPtr)typeof(WindowsTelemetryReader).GetField("query",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(reader)==IntPtr.Zero,"Constructor performs no native initialization");reader.Dispose();bool disposed=false;try{reader.Read();}catch(ObjectDisposedException){disposed=true;}Check(disposed,"Disposed reader cannot reopen native handles");}
 }
}
