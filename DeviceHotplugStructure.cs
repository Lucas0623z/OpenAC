// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independent Windows notification lifecycle and read-only refresh scheduling.
// ABI facts: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerdevicenotificationw
// https://learn.microsoft.com/en-us/windows/win32/api/dbt/ns-dbt-dev_broadcast_deviceinterface_w
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace RogControl {
 public sealed class DeviceHotplugQueue {
  long generation,lastChange,lastStart,activeTicket;bool pending,running,started,stopped;
  public long Generation {get{return generation;}}
  public bool Pending {get{return pending;}}
  public bool Running {get{return running;}}
  public bool Dirty {get{return pending||running;}}
  public bool Stopped {get{return stopped;}}
  public void MarkChanged(long now){if(stopped)return;if(now<0)throw new ArgumentOutOfRangeException("now");generation++;lastChange=now;pending=true;}
  public bool TryBegin(long now,bool idle,out long ticket) {
   ticket=0;if(stopped||running||!pending||!idle||now<lastChange||now-lastChange<750||started&&(now<lastStart||now-lastStart<5000))return false;
   pending=false;running=true;started=true;lastStart=now;activeTicket=generation;ticket=activeTicket;return true;
  }
  public bool IsCurrent(long ticket){return !stopped&&running&&ticket==activeTicket&&generation==ticket;}
  public void Complete(long ticket,bool success,long now) {
   if(!running||ticket!=activeTicket)return;running=false;
   if(stopped){pending=false;return;}if(generation!=ticket){pending=true;return;}if(!success){pending=true;if(now>lastChange)lastChange=now;}
  }
  public void Stop(){if(stopped)return;stopped=true;generation++;pending=false;}
 }

 public sealed class DeviceHotplugCoordinator {
  readonly DeviceHotplugQueue queue;readonly Func<long> clock;readonly Func<bool> idle;readonly Action invalidate;readonly Func<long,Task<bool>> refresh;
  public DeviceHotplugCoordinator(DeviceHotplugQueue state,Func<long> now,Func<bool> available,Action invalidateBindings,Func<long,Task<bool>> readOnlyRefresh){if(state==null||now==null||available==null||invalidateBindings==null||readOnlyRefresh==null)throw new ArgumentNullException();queue=state;clock=now;idle=available;invalidate=invalidateBindings;refresh=readOnlyRefresh;}
  public string LastError {get;private set;}
  public static bool RelevantMessage(int message,long eventCode){return message==0x0219&&(eventCode==7||eventCode==0x8000||eventCode==0x8003||eventCode==0x8004);}
  public void RequestRefresh(){if(queue.Stopped)return;queue.MarkChanged(clock());try{invalidate();}catch(Exception error){LastError=error.GetType().Name;}}
  public bool Notify(int message,long eventCode){if(queue.Stopped||!RelevantMessage(message,eventCode))return false;RequestRefresh();return true;}
  public async Task TickAsync(){long ticket;if(!queue.TryBegin(clock(),idle(),out ticket))return;bool success=false;try{success=await refresh(ticket);}catch(Exception error){LastError=error.GetType().Name;}finally{queue.Complete(ticket,success,clock());}}
 }

 public interface IDeviceHotplugNotifications {IntPtr Register(IntPtr window,Guid interfaceClass);bool Unregister(IntPtr notification);}
 [StructLayout(LayoutKind.Sequential,Pack=4,CharSet=CharSet.Unicode)]
 public struct DeviceHotplugInterfaceFilter {public int Size,DeviceType,Reserved;public Guid ClassGuid;public short NameFirst;}
 public sealed class WindowsDeviceHotplugNotifications : IDeviceHotplugNotifications {
  [DllImport("user32.dll",EntryPoint="RegisterDeviceNotificationW",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr RegisterNative(IntPtr recipient,IntPtr filter,uint flags);
  [DllImport("user32.dll",EntryPoint="UnregisterDeviceNotification",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool UnregisterNative(IntPtr notification);
  public IntPtr Register(IntPtr window,Guid interfaceClass){if(window==IntPtr.Zero)return IntPtr.Zero;var filter=new DeviceHotplugInterfaceFilter{Size=Marshal.SizeOf(typeof(DeviceHotplugInterfaceFilter)),DeviceType=5,ClassGuid=interfaceClass};IntPtr memory=Marshal.AllocHGlobal(filter.Size);try{Marshal.StructureToPtr(filter,memory,false);return RegisterNative(window,memory,0);}finally{Marshal.FreeHGlobal(memory);}}
  public bool Unregister(IntPtr notification){return notification!=IntPtr.Zero&&UnregisterNative(notification);}
 }
 public sealed class DeviceHotplugRegistration : IDisposable {
  public static readonly Guid HidInterface=new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
  public static readonly Guid UsbInterface=new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
  readonly IDeviceHotplugNotifications api;readonly List<IntPtr> notifications=new List<IntPtr>();bool attached,disposed;
  public DeviceHotplugRegistration(IDeviceHotplugNotifications notificationsApi){if(notificationsApi==null)throw new ArgumentNullException("notificationsApi");api=notificationsApi;}
  public int Count {get{return notifications.Count;}}
  public bool Attach(IntPtr window){if(disposed||attached||window==IntPtr.Zero)return false;attached=true;foreach(Guid kind in new[]{HidInterface,UsbInterface}){try{IntPtr value=api.Register(window,kind);if(value!=IntPtr.Zero&&!notifications.Contains(value))notifications.Add(value);}catch(Exception){/* Top-level DEVNODES_CHANGED remains usable without interface registration. */}}return notifications.Count>0;}
  public void Dispose(){if(disposed)return;disposed=true;foreach(IntPtr value in notifications){try{api.Unregister(value);}catch(Exception){}}notifications.Clear();}
 }

 public static class DeviceHotplugReadFacts {
  static object Field(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  public static bool GearIdentity(string kind,IDictionary<string,object> state){object raw=Field(state,"pid");if(!(raw is int)||Field(state,"kind") as string!=kind)return false;int pid=(int)raw;bool exact=kind=="mouse"&&(pid==7546||pid==7548)||kind=="keyboard"&&(pid==7586||pid==7588)||kind=="headset"&&pid==7489;return exact&&KeyboardLightingUiFacts.Identity(Field(state,"deviceId") as string,pid);}
  public static bool GearReadback(string kind,string expectedId,IDictionary<string,object> state){var snapshot=Field(state,"snapshot") as IDictionary<string,object>;return GearIdentity(kind,state)&&Field(state,"deviceId") as string==expectedId&&Field(state,"connected") is bool&&(bool)state["connected"]&&Field(state,"readbackConfirmed") is bool&&(bool)state["readbackConfirmed"]&&Field(state,"online") is bool&&Field(snapshot,"modelPid") is int&&Object.Equals(snapshot["modelPid"],state["pid"]);}
 }

 public partial class MainWindow {
  readonly DeviceHotplugQueue deviceHotplugQueue=new DeviceHotplugQueue();
  readonly Stopwatch deviceHotplugClock=Stopwatch.StartNew();
  IDeviceHotplugNotifications deviceHotplugNotifications=new WindowsDeviceHotplugNotifications();
  DeviceHotplugCoordinator deviceHotplugCoordinator;DeviceHotplugRegistration deviceHotplugRegistration;HwndSource deviceHotplugSource;DispatcherTimer deviceHotplugTimer;Task deviceHotplugRefreshTask;bool deviceHotplugStarted,deviceHotplugStopped;
  public long DeviceHotplugGeneration {get{return deviceHotplugQueue.Generation;}}
  public bool DeviceHotplugPageReadsPaused {get{return deviceHotplugQueue.Dirty;}}
  public bool DeviceHotplugBindingsReady {get{return !deviceHotplugQueue.Dirty;}}
  public bool DeviceHotplugCanPreviewReload {get{return deviceHotplugRefreshTask==null||deviceHotplugRefreshTask.IsCompleted;}}
  void BeginDeviceHotplugMonitoring() {
   if(render||closing||deviceHotplugStarted||deviceHotplugStopped)return;deviceHotplugStarted=true;
   deviceHotplugCoordinator=new DeviceHotplugCoordinator(deviceHotplugQueue,()=>deviceHotplugClock.ElapsedMilliseconds,DeviceHotplugIdle,()=>gearReadSessions.Clear(),RefreshDeviceHotplugReadOnly);
   SourceInitialized+=DeviceHotplugSourceInitialized;Loaded+=DeviceHotplugLoaded;Closed+=DeviceHotplugClosed;AttachDeviceHotplugSource();if(IsLoaded)StartDeviceHotplugTimer();
  }
  void DeviceHotplugSourceInitialized(object sender,EventArgs args){AttachDeviceHotplugSource();}
  void DeviceHotplugLoaded(object sender,RoutedEventArgs args){AttachDeviceHotplugSource();StartDeviceHotplugTimer();}
  void DeviceHotplugClosed(object sender,EventArgs args){StopDeviceHotplugMonitoring();}
  void AttachDeviceHotplugSource(){if(!deviceHotplugStarted||deviceHotplugStopped||closing||render||deviceHotplugSource!=null)return;var source=PresentationSource.FromVisual(this) as HwndSource;if(source==null||source.Handle==IntPtr.Zero)return;deviceHotplugSource=source;source.AddHook(DeviceHotplugMessage);deviceHotplugRegistration=new DeviceHotplugRegistration(deviceHotplugNotifications);deviceHotplugRegistration.Attach(source.Handle);}
  void StartDeviceHotplugTimer(){if(render||closing||deviceHotplugStopped||!deviceHotplugStarted||!IsLoaded||deviceHotplugTimer!=null)return;deviceHotplugTimer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(250)};deviceHotplugTimer.Tick+=DeviceHotplugTick;deviceHotplugTimer.Start();}
  IntPtr DeviceHotplugMessage(IntPtr window,int message,IntPtr eventCode,IntPtr data,ref bool handled){if(!render&&!deviceHotplugStopped&&deviceHotplugCoordinator!=null)deviceHotplugCoordinator.Notify(message,eventCode.ToInt64());return IntPtr.Zero;}
  // Do not dereference lParam, copy instance paths, perform I/O, veto removal,
  // rebuild pages or change permissions from the Windows notification hook.
  void RequestDeviceHotplugRefresh(){if(!render&&!closing&&!deviceHotplugStopped&&deviceHotplugCoordinator!=null)deviceHotplugCoordinator.RequestRefresh();}
  async void DeviceHotplugTick(object sender,EventArgs args){if(render||closing||deviceHotplugStopped||deviceHotplugCoordinator==null||deviceHotplugRefreshTask!=null&&!deviceHotplugRefreshTask.IsCompleted)return;var pending=deviceHotplugCoordinator.TickAsync();deviceHotplugRefreshTask=pending;try{await pending;}finally{if(Object.ReferenceEquals(deviceHotplugRefreshTask,pending))deviceHotplugRefreshTask=null;}}
  bool DeviceHotplugIdle(){return IsLoaded&&!render&&!closing&&!deviceHotplugStopped&&!busy&&!pageReadRunning&&!mouseCalibrationWorkflowRunning&&!mouseCalibrationApplying&&!mousePerformanceApplying&&!mouseButtonsApplying&&!mouseLightingApplying&&!mouseZoneApplying&&!keyboardViewsApplying&&!controlling&&!AuraSyncBusy&&!windowsSystemWriting&&(windowsSystemTask==null||windowsSystemTask.IsCompleted)&&NotebookCanPreviewReload&&!MacroFeaturesActive&&!ScenarioFeaturesActive&&OwnedWindows.Count==0;}
  bool DeviceHotplugMayCommit(long generation,string view){return deviceHotplugQueue.IsCurrent(generation)&&DeviceHotplugIdle()&&PageReadViewKey(page)==view;}
  async Task<bool> RefreshDeviceHotplugReadOnly(long generation) {
   string currentPage=page,view=PageReadViewKey(page),kind=currentPage=="device:mouse"?"mouse":currentPage=="device:keyboard"?"keyboard":currentPage=="device:headset"?"headset":null;
   if(!DeviceHotplugMayCommit(generation,view))return false;
   List<Dictionary<string,object>> found=null;string at="";
   try {
    var reply=await discoveryWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});if(!DeviceHotplugMayCommit(generation,view))return false;
    if(!reply.ok||reply.data==null||OriginalStateFlag(reply.data,"identificationOnly")!=true)throw new InvalidDataException("Invalid discovery response.");var raw=GearArray(reply.data,"devices");if(raw==null||raw.Length>4096)throw new InvalidDataException("Invalid discovery list.");found=raw.Select(item=>item as Dictionary<string,object>).ToList();if(found.Any(item=>!DiscoveryEntryValid(item)))throw new InvalidDataException("Unconfirmed discovery identity.");found=found.GroupBy(item=>DiscoveryText(item,"deviceId"),StringComparer.Ordinal).Select(group=>group.First()).ToList();DateTime captured;string textAt=OriginalStateText(reply.data,"capturedAt","");at=DateTime.TryParse(textAt,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out captured)?captured.ToString("MM-dd HH:mm:ss"):"本次扫描";
   }catch(Exception){found=null;at="";if(!DeviceHotplugMayCommit(generation,view))return false;}
   Dictionary<string,object> gear=null;object session=null;
   if(kind!=null) {
    try {
     var enumerate=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","enumerate"},{"kind",kind}});if(!DeviceHotplugMayCommit(generation,view))return false;if(!enumerate.ok)throw new InvalidDataException("Gear enumeration failed.");var rows=GearArray(enumerate.data,"devices");if(rows==null||rows.Length>4096)throw new InvalidDataException("Invalid gear enumeration.");Dictionary<string,object> previous;gearStates.TryGetValue(kind,out previous);string previousId=OriginalStateText(previous,"deviceId","");
     var candidates=rows.OfType<Dictionary<string,object>>().Where(item=>DeviceHotplugReadFacts.GearIdentity(kind,item)).OrderByDescending(item=>OriginalStateText(item,"deviceId","")==previousId).ThenByDescending(item=>OriginalStateText(item,"transport","")=="usb").Take(4).ToArray();session=gearWorker.SessionIdentity;if(session==null)throw new InvalidDataException("Gear session ended.");var clock=Stopwatch.StartNew();
     foreach(var candidate in candidates){if(!DeviceHotplugMayCommit(generation,view))return false;int remaining=18000-(int)clock.ElapsedMilliseconds;if(remaining<=0)throw new TimeoutException("Gear refresh deadline.");string id=OriginalStateText(candidate,"deviceId","");var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",id}},session,remaining);if(!DeviceHotplugMayCommit(generation,view))return false;if(!Object.ReferenceEquals(session,gearWorker.SessionIdentity))throw new InvalidDataException("Gear session changed.");if(reply.ok&&reply.data!=null&&DeviceHotplugReadFacts.GearReadback(kind,id,reply.data)){gear=reply.data;if(GearOnline(gear))break;}}
    }catch(Exception){if(!DeviceHotplugMayCommit(generation,view))return false;gear=null;session=null;}
   }
   if(!DeviceHotplugMayCommit(generation,view))return false;
   discoveredHardware.Clear();if(found!=null)discoveredHardware.AddRange(found);discoveryCompleted=found!=null;discoveryCapturedAt=found==null?"":at;discoveryMessage=found==null?"设备识别暂不可用，请手动刷新。":"扫描完成";
   if(kind!=null){gearReadSessions.Remove(kind);if(gear!=null&&session!=null&&Object.ReferenceEquals(session,gearWorker.SessionIdentity)){gear["capturedAt"]=DateTime.Now.ToString("HH:mm:ss");gearStates[kind]=gear;gearReadSessions[kind]=session;gearMessage=GearOnline(gear)?"":"设备当前未在线。";}else{gearStates.Remove(kind);gearMessage="设备状态需要重新读取。";}}
   string key=PageReadKey(currentPage);if(key!=null)pageReadAttempts[key]=DateTime.UtcNow;
   // Defer page rebuilding until after the coordinator drops its running gate.
   // This avoids a self-created page read or accidental session enable.
   QueueDeviceHotplugRender(currentPage,kind);
   return true;
  }
  void QueueDeviceHotplugRender(string currentPage,string kind){Dispatcher.BeginInvoke(new Action(()=>{if(!closing&&!render&&!deviceHotplugStopped&&!deviceHotplugQueue.Dirty&&page==currentPage&&(currentPage=="设备兼容"||currentPage.StartsWith("device:discovered:",StringComparison.Ordinal)||kind!=null))ShowPage(page);}),DispatcherPriority.Background);}
  void StopDeviceHotplugMonitoring(){if(deviceHotplugStopped)return;deviceHotplugStopped=true;deviceHotplugQueue.Stop();if(deviceHotplugTimer!=null){deviceHotplugTimer.Stop();deviceHotplugTimer.Tick-=DeviceHotplugTick;deviceHotplugTimer=null;}SourceInitialized-=DeviceHotplugSourceInitialized;Loaded-=DeviceHotplugLoaded;Closed-=DeviceHotplugClosed;if(deviceHotplugSource!=null){try{deviceHotplugSource.RemoveHook(DeviceHotplugMessage);}catch(Exception){}deviceHotplugSource=null;}if(deviceHotplugRegistration!=null){deviceHotplugRegistration.Dispose();deviceHotplugRegistration=null;}}
  async Task WaitDeviceHotplugAsync(){StopDeviceHotplugMonitoring();var pending=deviceHotplugRefreshTask;if(pending!=null)try{await pending;}catch(Exception){}}
 }
}
