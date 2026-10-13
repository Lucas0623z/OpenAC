// OpenAC-owned Windows display adapter. Microsoft ABI facts only; no G-Helper code.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RogControl {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)]
 public struct DisplayNativeDevice {
  public uint cb;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceString;
  public uint StateFlags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceID;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceKey;
 }
 // The 16-byte display variant occupies the complete printer/display union.
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)]
 public struct DisplayNativeMode {
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmDeviceName;
  public ushort dmSpecVersion,dmDriverVersion,dmSize,dmDriverExtra;
  public uint dmFields;
  public int dmPositionX,dmPositionY;
  public uint dmDisplayOrientation,dmDisplayFixedOutput;
  public short dmColor,dmDuplex,dmYResolution,dmTTOption,dmCollate;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string dmFormName;
  public ushort dmLogPixels;
  public uint dmBitsPerPel,dmPelsWidth,dmPelsHeight,dmDisplayFlags,dmDisplayFrequency;
  public uint dmICMMethod,dmICMIntent,dmMediaType,dmDitherType,dmReserved1,dmReserved2,dmPanningWidth,dmPanningHeight;
 }
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayLuid { public uint Low;public int High; }
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayRational { public uint Numerator,Denominator; }
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayPathSource {public DisplayLuid Adapter;public uint Id,ModeIndex,StatusFlags;}
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayPathTarget {public DisplayLuid Adapter;public uint Id,ModeIndex;public int OutputTechnology;public uint Rotation,Scaling;public DisplayRational RefreshRate;public uint ScanLineOrdering;public int TargetAvailable;public uint StatusFlags;}
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayConfigNativePath {public DisplayPathSource Source;public DisplayPathTarget Target;public uint Flags;}
 [StructLayout(LayoutKind.Sequential,Pack=4)] public struct DisplayConfigHeader {public uint Type,Size;public DisplayLuid Adapter;public uint Id;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)] public struct DisplayConfigSourceName {public DisplayConfigHeader Header;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string GdiName;}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode,Pack=4)] public struct DisplayConfigTargetName {public DisplayConfigHeader Header;public uint Flags;public int OutputTechnology;public ushort EdidManufacturerId,EdidProductCode;public uint ConnectorInstance;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string FriendlyName;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string MonitorPath;}

 public sealed class DisplayPathInfo {
  public string SourceDeviceName,MonitorDevicePath,FriendlyName,Error;
  public uint Rotation,RefreshNumerator,RefreshDenominator,ScanLineOrdering;
  public bool Active,TargetAvailable;
 }
 public interface IWindowsDisplayApi {
  bool EnumDevice(string parent,uint index,uint flags,out DisplayNativeDevice device);
  bool EnumMode(string device,uint index,out DisplayNativeMode mode);
  DisplayPathInfo[] ActivePaths();
  int ChangeMode(string device,ref DisplayNativeMode mode,uint flags);
 }
 public sealed class WindowsDisplayNativeApi : IWindowsDisplayApi {
  readonly Action BeforeChange;
  public WindowsDisplayNativeApi(Action beforeChange=null){BeforeChange=beforeChange;}
  [DllImport("user32.dll",EntryPoint="EnumDisplayDevicesW",CharSet=CharSet.Unicode,ExactSpelling=true)] [return:MarshalAs(UnmanagedType.Bool)]static extern bool Devices(string parent,uint index,ref DisplayNativeDevice device,uint flags);
  [DllImport("user32.dll",EntryPoint="EnumDisplaySettingsExW",CharSet=CharSet.Unicode,ExactSpelling=true)] [return:MarshalAs(UnmanagedType.Bool)]static extern bool Settings(string device,uint index,ref DisplayNativeMode mode,uint flags);
  [DllImport("user32.dll",EntryPoint="ChangeDisplaySettingsExW",CharSet=CharSet.Unicode,ExactSpelling=true)]static extern int Change(string device,ref DisplayNativeMode mode,IntPtr window,uint flags,IntPtr parameters);
  [DllImport("user32.dll")]static extern int GetDisplayConfigBufferSizes(uint flags,out uint paths,out uint modes);
  [DllImport("user32.dll")]static extern int QueryDisplayConfig(uint flags,ref uint paths,IntPtr pathBuffer,ref uint modes,IntPtr modeBuffer,IntPtr topology);
  [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int SourceName(ref DisplayConfigSourceName name);
  [DllImport("user32.dll",EntryPoint="DisplayConfigGetDeviceInfo")]static extern int TargetName(ref DisplayConfigTargetName name);
  public bool EnumDevice(string parent,uint index,uint flags,out DisplayNativeDevice device){device=new DisplayNativeDevice{cb=(uint)Marshal.SizeOf(typeof(DisplayNativeDevice))};return Devices(parent,index,ref device,flags);}
  public bool EnumMode(string device,uint index,out DisplayNativeMode mode){mode=new DisplayNativeMode{dmSize=(ushort)Marshal.SizeOf(typeof(DisplayNativeMode)),dmDriverExtra=0};return Settings(device,index,ref mode,0);}
  public int ChangeMode(string device,ref DisplayNativeMode mode,uint flags){if(!WindowsDisplayRules.ValidDeviceName(device)||(flags!=0&&flags!=WindowsDisplayRules.CdsTest))throw new ArgumentException("Only a specific enumerated device and TEST/dynamic change are allowed");if(BeforeChange!=null){try{BeforeChange();}catch(Exception e){throw new WindowsDisplayError("WRITE_POLICY_BLOCKED",e.Message);}}return Change(device,ref mode,IntPtr.Zero,flags,IntPtr.Zero);}
  public DisplayPathInfo[] ActivePaths(){
   uint count,modeCount;int result=GetDisplayConfigBufferSizes(2,out count,out modeCount);if(result!=0)throw new WindowsDisplayError("DISPLAY_CONFIG_FAILED","Active display topology size query failed: "+result);
   if(count>64||modeCount>512)throw new WindowsDisplayError("DISPLAY_CONFIG_TOO_LARGE","Active display topology exceeds the bounded reader");if(count==0)return new DisplayPathInfo[0];IntPtr paths=Marshal.AllocHGlobal(checked((int)count*72)),modes=Marshal.AllocHGlobal(checked((int)Math.Max(modeCount,1)*64));
   try {uint capacity=count,modeCapacity=modeCount;result=QueryDisplayConfig(2,ref count,paths,ref modeCount,modes,IntPtr.Zero);if(result!=0||count>capacity||modeCount>modeCapacity)throw new WindowsDisplayError("DISPLAY_CONFIG_FAILED","Active topology query failed or changed during enumeration: "+result);var list=new List<DisplayPathInfo>();
    for(int i=0;i<count;i++){var path=(DisplayConfigNativePath)Marshal.PtrToStructure(IntPtr.Add(paths,i*72),typeof(DisplayConfigNativePath));if((path.Flags&1)==0)continue;var source=new DisplayConfigSourceName{Header=new DisplayConfigHeader{Type=1,Size=84,Adapter=path.Source.Adapter,Id=path.Source.Id}};var target=new DisplayConfigTargetName{Header=new DisplayConfigHeader{Type=2,Size=420,Adapter=path.Target.Adapter,Id=path.Target.Id}};int s=SourceName(ref source),t=TargetName(ref target);list.Add(new DisplayPathInfo{SourceDeviceName=s==0?source.GdiName:null,MonitorDevicePath=t==0?target.MonitorPath:null,FriendlyName=t==0?target.FriendlyName:null,Rotation=path.Target.Rotation,RefreshNumerator=path.Target.RefreshRate.Numerator,RefreshDenominator=path.Target.RefreshRate.Denominator,ScanLineOrdering=path.Target.ScanLineOrdering,Active=true,TargetAvailable=path.Target.TargetAvailable!=0,Error=s==0&&t==0?null:"Display target/source name unavailable"});}return list.ToArray();
   }finally{Marshal.FreeHGlobal(paths);Marshal.FreeHGlobal(modes);}
  }
 }
 public sealed class WindowsDisplayError : Exception {public string Code {get;private set;}public WindowsDisplayError(string code,string message):base(message){Code=code;}}
 public sealed class WindowsDisplayMode {
  public uint Width {get;private set;}public uint Height {get;private set;}public uint BitsPerPixel {get;private set;}public uint? Orientation {get;private set;}public uint ReportedRefreshRateHz {get;private set;}public uint DisplayFlags {get;private set;}public int PositionX {get;private set;}public int PositionY {get;private set;}public bool PositionKnown {get;private set;}
  public uint? RefreshNumerator {get;private set;}public uint? RefreshDenominator {get;private set;}public double? TargetRefreshRateHz {get;private set;}public string Precision {get;private set;}public bool Valid {get;private set;}public string Error {get;private set;}
  internal DisplayNativeMode Native;
  internal WindowsDisplayMode(DisplayNativeMode native,DisplayPathInfo path){Native=native;Width=native.dmPelsWidth;Height=native.dmPelsHeight;BitsPerPixel=native.dmBitsPerPel;ReportedRefreshRateHz=native.dmDisplayFrequency;DisplayFlags=native.dmDisplayFlags;PositionX=native.dmPositionX;PositionY=native.dmPositionY;PositionKnown=(native.dmFields&WindowsDisplayRules.DmPosition)!=0;Precision="integerWindowsMode";
   if((native.dmFields&WindowsDisplayRules.DmOrientation)!=0&&native.dmDisplayOrientation<=3)Orientation=native.dmDisplayOrientation;
   // CCD describes clockwise rotation; DEVMODE's DMDO values are counter-clockwise.
   if(path!=null&&path.TargetAvailable&&path.Rotation>=1&&path.Rotation<=4){uint rotation=(4-(path.Rotation-1))%4;if(Orientation.HasValue&&Orientation.Value!=rotation)Error="Rotation differs between Windows mode and active target";else Orientation=rotation;}
   if(path!=null&&path.TargetAvailable&&path.RefreshNumerator>0&&path.RefreshDenominator>0){RefreshNumerator=path.RefreshNumerator;RefreshDenominator=path.RefreshDenominator;TargetRefreshRateHz=(double)path.RefreshNumerator/path.RefreshDenominator;Precision="integerWindowsModeAndRationalTargetTiming";}
   if(native.dmSize!=220||native.dmDriverExtra!=0)Error="Unsupported public/private DEVMODE size";
   else if((native.dmFields&WindowsDisplayRules.RequiredModeFields)!=WindowsDisplayRules.RequiredModeFields||Width==0||Height==0||Width>65535||Height>65535||BitsPerPixel!=32||ReportedRefreshRateHz<=1||ReportedRefreshRateHz>1000)Error="Current mode lacks confirmed resolution, 32-bit depth or explicit frequency";
   else if(!Orientation.HasValue)Error="Current orientation is unconfirmed";Valid=Error==null;
  }
 }
 public sealed class WindowsDisplayScreen {
  public string Id {get;private set;}public string DeviceName {get;private set;}public string Name {get;private set;}public string AdapterName {get;private set;}public bool Primary {get;private set;}public bool IdentityConfirmed {get;private set;}public bool SharedModeGroup {get;private set;}public bool ManualRefreshSupported {get;private set;}public string Error {get;private set;}public string CurrentModeFingerprint {get;private set;}public WindowsDisplayMode CurrentMode {get;private set;}public ReadOnlyCollection<uint> SupportedReportedRefreshRatesHz {get;private set;}public DateTime CapturedAtUtc {get;private set;}public bool FractionalSelectionSupported {get{return false;}}
  internal Guid Owner;internal string Identity,PhysicalIdentity;internal bool Complete;
  internal void Disable(string error){Error=Error??error;ManualRefreshSupported=false;}
  internal WindowsDisplayScreen(Guid owner,DisplayNativeDevice adapter,DisplayNativeDevice monitor,DisplayPathInfo path,DisplayNativeMode? native,IEnumerable<DisplayNativeMode> modes,int groupSize,bool complete,string error){
   Owner=owner;Complete=complete;DeviceName=adapter.DeviceName;AdapterName=adapter.DeviceString;Name=path!=null&&!String.IsNullOrWhiteSpace(path.FriendlyName)?path.FriendlyName:monitor.DeviceString;Primary=(adapter.StateFlags&4)!=0;CapturedAtUtc=DateTime.UtcNow;SharedModeGroup=groupSize!=1;
   string physical=path!=null?path.MonitorDevicePath:monitor.DeviceID;PhysicalIdentity=physical;IdentityConfirmed=WindowsDisplayRules.ValidDeviceName(DeviceName)&&!String.IsNullOrEmpty(physical)&&physical.Length<=512&&(path==null||path.Active&&path.TargetAvailable&&String.IsNullOrEmpty(path.Error));Identity=DeviceName+"\n"+physical;Id=WindowsDisplayRules.Hash(Identity.ToLowerInvariant());
   CurrentMode=native.HasValue?new WindowsDisplayMode(native.Value,path):null;var rates=new SortedSet<uint>();if(CurrentMode!=null&&CurrentMode.Valid)foreach(var mode in modes)if(WindowsDisplayRules.Compatible(CurrentMode,mode))rates.Add(mode.dmDisplayFrequency);SupportedReportedRefreshRatesHz=new ReadOnlyCollection<uint>(rates.ToList());CurrentModeFingerprint=CurrentMode==null?null:WindowsDisplayRules.Fingerprint(CurrentMode);Error=error;
   if(!IdentityConfirmed)Error=Error??"Physical display identity is unconfirmed";else if(SharedModeGroup)Error=Error??"Several active targets share this Windows mode group";else if(CurrentMode==null||!CurrentMode.Valid)Error=Error??(CurrentMode==null?"Current mode unavailable":CurrentMode.Error);else if(!complete)Error=Error??"Enumeration was incomplete";else if(rates.Count==0)Error=Error??"No matching monitor-supported refresh modes were enumerated";ManualRefreshSupported=Error==null;
  }
 }
 public sealed class WindowsDisplaySnapshot {public ReadOnlyCollection<WindowsDisplayScreen> Screens {get;internal set;}public ReadOnlyCollection<string> Warnings {get;internal set;}public DateTime CapturedAtUtc {get;internal set;}}
 public sealed class WindowsDisplayRestorePoint {
  public WindowsDisplayMode OriginalMode {get;internal set;}public string ScreenId {get;internal set;}
  internal Guid Owner;internal string ExpectedAfterFingerprint,Identity;internal DisplayNativeMode Original;
 }
 public sealed class WindowsDisplayChangeResult {
  public bool WriteAttempted,TestPassed,ReadbackConfirmed,AlreadyCurrent;public bool? ExactTimingMatchesRequestedInteger;
  public int? TestStatus,ApplyStatus;public string ErrorCode,Error;public bool? OriginalTargetTimingRestored;
  public WindowsDisplayMode OriginalMode;public WindowsDisplayScreen ObservedScreen;public WindowsDisplayRestorePoint RestorePoint;
 }
 public static class WindowsDisplayRules {
  public const uint EnumCurrent=0xFFFFFFFF,CdsTest=2,DmPosition=0x20,DmOrientation=0x80,DmBits=0x40000,DmWidth=0x80000,DmHeight=0x100000,DmFlags=0x200000,DmFrequency=0x400000;
  public const uint RequiredModeFields=DmBits|DmWidth|DmHeight|DmFrequency;
  public static bool ValidDeviceName(string name){if(name==null||!name.StartsWith(@"\\.\DISPLAY",StringComparison.OrdinalIgnoreCase)||name.Length>32||name.Length<=11)return false;for(int i=11;i<name.Length;i++)if(name[i]<'0'||name[i]>'9')return false;return true;}
  public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value??""))).Replace("-","").ToLowerInvariant();}
  public static string Fingerprint(WindowsDisplayMode m){var n=m.Native;return Hash(String.Join("|",new object[]{n.dmSpecVersion,n.dmDriverVersion,n.dmSize,n.dmDriverExtra,n.dmFields,n.dmPositionX,n.dmPositionY,n.dmDisplayOrientation,n.dmDisplayFixedOutput,n.dmBitsPerPel,n.dmPelsWidth,n.dmPelsHeight,n.dmDisplayFlags,n.dmDisplayFrequency,n.dmPanningWidth,n.dmPanningHeight,m.Orientation,m.RefreshNumerator,m.RefreshDenominator}));}
  public static bool Compatible(WindowsDisplayMode current,DisplayNativeMode mode){
   if(!current.Valid||mode.dmSize!=220||mode.dmDriverExtra!=0||(mode.dmFields&RequiredModeFields)!=RequiredModeFields||mode.dmPelsWidth!=current.Width||mode.dmPelsHeight!=current.Height||mode.dmBitsPerPel!=current.BitsPerPixel||mode.dmDisplayFrequency<=1||mode.dmDisplayFrequency>1000||mode.dmDisplayFlags!=current.DisplayFlags)return false;
   if((mode.dmFields&DmOrientation)!=0)return mode.dmDisplayOrientation<=3&&mode.dmDisplayOrientation==current.Orientation.Value;
   // EnumDisplaySettingsEx flags0 explicitly filters to the current orientation.
   return mode.dmDisplayOrientation==0;
  }
  public static bool SameGeometry(WindowsDisplayMode a,WindowsDisplayMode b){return a!=null&&b!=null&&a.Valid&&b.Valid&&a.Width==b.Width&&a.Height==b.Height&&a.BitsPerPixel==b.BitsPerPixel&&a.Orientation==b.Orientation&&a.DisplayFlags==b.DisplayFlags&&a.PositionKnown==b.PositionKnown&&(!a.PositionKnown||a.PositionX==b.PositionX&&a.PositionY==b.PositionY);}
 }
 public sealed class WindowsDisplayController {
  readonly IWindowsDisplayApi Api;readonly Guid Owner=Guid.NewGuid();readonly object Sync=new object();
  sealed class ChangeLock : IDisposable {
   Mutex Mutex;bool Held;
   public ChangeLock(bool native){if(!native)return;Mutex=new Mutex(false,"Local\\OpenAC.Display.Change");try{try{Held=Mutex.WaitOne(0);}catch(AbandonedMutexException){Held=true;}if(!Held)throw new WindowsDisplayError("DISPLAY_BUSY","Another OpenAC display change is active");}catch{Mutex.Dispose();Mutex=null;throw;}}
   public void Dispose(){if(Mutex==null)return;try{if(Held)Mutex.ReleaseMutex();}finally{Mutex.Dispose();}}
  }
  public WindowsDisplayController():this(new WindowsDisplayNativeApi()){}
  public WindowsDisplayController(IWindowsDisplayApi api){if(api==null)throw new ArgumentNullException("api");Api=api;}
  public WindowsDisplaySnapshot Read(){lock(Sync)return ReadCore();}
  WindowsDisplaySnapshot ReadCore(){
   var screens=new List<WindowsDisplayScreen>();var warnings=new List<string>();var seenPaths=new HashSet<DisplayPathInfo>();DisplayPathInfo[] paths=null;try{paths=Api.ActivePaths();}catch(Exception e){warnings.Add("Active topology unavailable: "+e.Message);}bool adaptersComplete=false;
   for(uint a=0;a<64;a++){DisplayNativeDevice adapter;if(!Api.EnumDevice(null,a,0,out adapter)){adaptersComplete=true;break;}if((adapter.StateFlags&1)==0||(adapter.StateFlags&8)!=0)continue;if(adapter.cb!=840||!WindowsDisplayRules.ValidDeviceName(adapter.DeviceName)){warnings.Add("Invalid active display device structure/name");continue;}
    var monitors=new List<DisplayNativeDevice>();bool monitorComplete=false;for(uint i=0;i<16;i++){DisplayNativeDevice monitor;if(!Api.EnumDevice(adapter.DeviceName,i,1,out monitor)){monitorComplete=true;break;}if(monitor.cb==840&&(monitor.StateFlags&1)!=0)monitors.Add(monitor);}
    DisplayNativeMode current;DisplayNativeMode? actual=Api.EnumMode(adapter.DeviceName,WindowsDisplayRules.EnumCurrent,out current)?(DisplayNativeMode?)current:null;var modes=new List<DisplayNativeMode>();bool modeComplete=false;for(uint i=0;i<4096;i++){DisplayNativeMode mode;if(!Api.EnumMode(adapter.DeviceName,i,out mode)){modeComplete=true;break;}modes.Add(mode);}
    var targets=paths==null?new DisplayPathInfo[0]:paths.Where(p=>p!=null&&p.Active&&String.Equals(p.SourceDeviceName,adapter.DeviceName,StringComparison.OrdinalIgnoreCase)).ToArray();int group=targets.Length>0?targets.Length:monitors.Count;bool complete=monitorComplete&&modeComplete;
    if(targets.Length>0){foreach(var path in targets){seenPaths.Add(path);var monitor=monitors.FirstOrDefault(m=>String.Equals(m.DeviceID,path.MonitorDevicePath,StringComparison.OrdinalIgnoreCase));screens.Add(new WindowsDisplayScreen(Owner,adapter,monitor,path,actual,modes,group,complete,null));}}
    else if(monitors.Count>0){foreach(var monitor in monitors)screens.Add(new WindowsDisplayScreen(Owner,adapter,monitor,null,actual,modes,group,complete,paths!=null?"Active source has no matched target path":"Active display topology is unconfirmed"));}
    else screens.Add(new WindowsDisplayScreen(Owner,adapter,new DisplayNativeDevice(),null,actual,modes,0,false,"No physical monitor identity was returned"));
   }
   if(paths!=null)foreach(var path in paths)if(path!=null&&path.Active&&!seenPaths.Contains(path))screens.Add(new WindowsDisplayScreen(Owner,new DisplayNativeDevice{cb=840,DeviceName=path.SourceDeviceName},new DisplayNativeDevice(),path,null,new DisplayNativeMode[0],0,false,"Active target has no confirmed GDI source mode"));
   if(!adaptersComplete){warnings.Add("Adapter enumeration reached its bound");foreach(var s in screens)s.Disable("Adapter enumeration was incomplete");}var duplicateIds=screens.GroupBy(s=>s.Id).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();if(duplicateIds.Length>0){warnings.Add("Duplicate display identities; changes will be rejected");foreach(var s in screens)if(duplicateIds.Contains(s.Id))s.Disable("Duplicate display identity");}var physicalDuplicates=screens.Where(s=>!String.IsNullOrEmpty(s.PhysicalIdentity)).GroupBy(s=>s.PhysicalIdentity,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray();foreach(var s in screens)if(physicalDuplicates.Contains(s.PhysicalIdentity,StringComparer.OrdinalIgnoreCase))s.Disable("Physical target appears on more than one active source");return new WindowsDisplaySnapshot{Screens=new ReadOnlyCollection<WindowsDisplayScreen>(screens),Warnings=new ReadOnlyCollection<string>(warnings),CapturedAtUtc=DateTime.UtcNow};
  }
  WindowsDisplayScreen Fresh(WindowsDisplayScreen expected){if(expected==null||expected.Owner!=Owner)throw new WindowsDisplayError("FOREIGN_SNAPSHOT","Use a screen snapshot created by this controller");var all=ReadCore();var matches=all.Screens.Where(s=>s.Id==expected.Id).ToArray();if(matches.Length!=1)throw new WindowsDisplayError("DISPLAY_IDENTITY_CHANGED","The exact display is absent or ambiguous");var fresh=matches[0];if(!fresh.ManualRefreshSupported)throw new WindowsDisplayError("DISPLAY_MODE_UNAVAILABLE",fresh.Error??"Current display mode cannot be changed");if(fresh.Identity!=expected.Identity||fresh.CurrentModeFingerprint!=expected.CurrentModeFingerprint)throw new WindowsDisplayError("STALE_DISPLAY_MODE","Display identity/current mode changed; refresh the snapshot");return fresh;}
  public WindowsDisplayChangeResult ApplyRefreshRate(WindowsDisplayScreen expected,uint reportedHz,bool explicitSessionWrite){lock(Sync){if(!explicitSessionWrite)throw new WindowsDisplayError("WRITE_DISABLED","An explicit current-session display change is required");if(reportedHz<=1||reportedHz>1000)throw new WindowsDisplayError("INVALID_RATE","Choose a reported integer refresh rate");using(var changeLock=new ChangeLock(Api is WindowsDisplayNativeApi)){var fresh=Fresh(expected);if(!fresh.SupportedReportedRefreshRatesHz.Contains(reportedHz))throw new WindowsDisplayError("UNSUPPORTED_RATE","Rate was not enumerated for the exact current resolution, depth and orientation");return ChangeCore(fresh,reportedHz);}}}
  WindowsDisplayChangeResult ChangeCore(WindowsDisplayScreen fresh,uint hz){
   var result=new WindowsDisplayChangeResult{OriginalMode=fresh.CurrentMode};if(fresh.CurrentMode.ReportedRefreshRateHz==hz){result.AlreadyCurrent=true;result.ReadbackConfirmed=true;result.ObservedScreen=fresh;return result;}
   var target=fresh.CurrentMode.Native;target.dmDisplayFrequency=hz;target.dmFields|=WindowsDisplayRules.DmFrequency;int test=Api.ChangeMode(fresh.DeviceName,ref target,WindowsDisplayRules.CdsTest);result.TestStatus=test;if(test!=0){result.ErrorCode="CDS_TEST_FAILED";result.Error="Windows rejected the requested mode test";return result;}result.TestPassed=true;
   // A device/topology/mode change during TEST invalidates the operation before apply.
   fresh=Fresh(fresh);if(!fresh.SupportedReportedRefreshRatesHz.Contains(hz))throw new WindowsDisplayError("UNSUPPORTED_RATE","Monitor-supported modes changed during TEST");target=fresh.CurrentMode.Native;target.dmDisplayFrequency=hz;target.dmFields|=WindowsDisplayRules.DmFrequency;result.WriteAttempted=true;try{result.ApplyStatus=Api.ChangeMode(fresh.DeviceName,ref target,0);}catch(WindowsDisplayError e){if(e.Code=="WRITE_POLICY_BLOCKED"){result.WriteAttempted=false;result.ErrorCode=e.Code;result.Error=e.Message;return result;}result.ErrorCode="DISPLAY_APPLY_EXCEPTION";result.Error="The display driver change could not be completed";}catch(Exception){result.ErrorCode="DISPLAY_APPLY_EXCEPTION";result.Error="The display driver change could not be completed";}
   WindowsDisplayScreen[] observed;try{observed=ReadCore().Screens.Where(s=>s.Id==fresh.Id).ToArray();}catch(Exception){result.ErrorCode="MODE_READBACK_FAILED";result.Error="Post-change display readback failed; the original mode is retained";return result;}if(observed.Length==1){result.ObservedScreen=observed[0];var after=observed[0].CurrentMode;result.ReadbackConfirmed=result.ApplyStatus==0&&observed[0].IdentityConfirmed&&!observed[0].SharedModeGroup&&WindowsDisplayRules.SameGeometry(fresh.CurrentMode,after)&&after.ReportedRefreshRateHz==hz;
    if(after!=null&&after.RefreshNumerator.HasValue&&after.RefreshDenominator.HasValue)result.ExactTimingMatchesRequestedInteger=(ulong)after.RefreshNumerator.Value==(ulong)hz*after.RefreshDenominator.Value;
    if(result.ReadbackConfirmed&&observed[0].ManualRefreshSupported)result.RestorePoint=new WindowsDisplayRestorePoint{Owner=Owner,ScreenId=fresh.Id,Identity=fresh.Identity,Original=fresh.CurrentMode.Native,OriginalMode=fresh.CurrentMode,ExpectedAfterFingerprint=observed[0].CurrentModeFingerprint};
   }if(!result.ReadbackConfirmed){result.ErrorCode=result.ApplyStatus==0?"MODE_READBACK_UNCONFIRMED":"DISPLAY_APPLY_FAILED";result.Error="Display change/readback was not confirmed; original mode is retained for explicit recovery";}return result;
  }
  public WindowsDisplayChangeResult Restore(WindowsDisplayRestorePoint point,bool explicitSessionWrite){lock(Sync){if(!explicitSessionWrite)throw new WindowsDisplayError("WRITE_DISABLED","Restore must be explicitly requested in this session");if(point==null||point.Owner!=Owner)throw new WindowsDisplayError("FOREIGN_RESTORE_POINT","Restore point was not created by this controller");using(var changeLock=new ChangeLock(Api is WindowsDisplayNativeApi)){var matches=ReadCore().Screens.Where(s=>s.Id==point.ScreenId).ToArray();if(matches.Length!=1||matches[0].Identity!=point.Identity||matches[0].CurrentModeFingerprint!=point.ExpectedAfterFingerprint)throw new WindowsDisplayError("STALE_RESTORE_POINT","Current identity/mode differs from the recorded post-change mode");var current=matches[0];if(!current.ManualRefreshSupported||!WindowsDisplayRules.SameGeometry(current.CurrentMode,point.OriginalMode)||!current.SupportedReportedRefreshRatesHz.Contains(point.Original.dmDisplayFrequency))throw new WindowsDisplayError("RESTORE_MODE_UNAVAILABLE","Original refresh mode is no longer valid for this exact screen");var result=ChangeCore(current,point.Original.dmDisplayFrequency);var after=result.ObservedScreen==null?null:result.ObservedScreen.CurrentMode;if(after!=null&&point.OriginalMode.RefreshNumerator.HasValue&&point.OriginalMode.RefreshDenominator.HasValue&&after.RefreshNumerator.HasValue&&after.RefreshDenominator.HasValue)result.OriginalTargetTimingRestored=(ulong)point.OriginalMode.RefreshNumerator.Value*after.RefreshDenominator.Value==(ulong)after.RefreshNumerator.Value*point.OriginalMode.RefreshDenominator.Value;return result;}}}
 }
}
