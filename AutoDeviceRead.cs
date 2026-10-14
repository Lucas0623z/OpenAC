using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RogControl {
 public partial class MainWindow {
  DispatcherTimer pageReadTimer;
  string pendingPageRead="";
  string lastPageReadViewKey="";bool pendingPageReadMayPreserveDraft;
  bool pageReadRunning;
  readonly Dictionary<string,DateTime> pageReadAttempts=new Dictionary<string,DateTime>();
  void BeginPageReads() {
   if(render)return;
   pageReadTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};
   pageReadTimer.Tick+=async(s,e)=>{
    if(!IsLoaded||closing||busy||pageReadRunning||String.IsNullOrEmpty(pendingPageRead))return;
    string target=pendingPageRead,key=PageReadKey(target);bool preserveDraft=pendingPageReadMayPreserveDraft;pendingPageRead="";if(key==null||preserveDraft&&PreserveVisiblePageReadDraft(target))return;
    DateTime attempted;if(pageReadAttempts.TryGetValue(key,out attempted)&&(DateTime.UtcNow-attempted).TotalSeconds<10)return;
    pageReadAttempts[key]=DateTime.UtcNow;pageReadRunning=true;
    try{await ReadVisiblePage(target);}catch(Exception ex){if(!closing)Log("读取失败："+ex.Message);}finally{pageReadAttempts[key]=DateTime.UtcNow;pageReadRunning=false;}
   };
   pageReadTimer.Start();
  }
  string PageReadKey(string target) {
   if(target=="device:motherboard")return target+":"+originalMotherboardTab;
   if(target=="Windows 电源"||target=="屏幕设置"||target=="电池信息")return target;
   return target=="设备"||target=="设备兼容"||target=="灯效 & Aura Sync"||target=="神光同步"||target=="device:keyboard"||target=="device:mouse"||target=="device:headset"||target=="device:fan"||target=="device:receiver"?target:null;
  }
  bool PreserveLcdMonitorReadDraft(string target){
   if(target!="device:motherboard"||originalMotherboardTab!="5\" LCD Display"||!lcdMonitorMode)return false;
   Dictionary<string,object> snapshot;if(!sdkSnapshots.TryGetValue("lcd:7299",out snapshot)||!lcdMonitorDraft.IsCurrent(snapshot))return false;
   foreach(var row in lcdMonitorDraft.Rows)if(row.Category.HasValue||!String.IsNullOrEmpty(row.SensorId))return true;return false;
  }
  bool PreserveVisiblePageReadDraft(string target) {
   if(PreserveLcdMonitorReadDraft(target))return true;if(target!="device:mouse")return false;
   Dictionary<string,object> current;object readSession;if(!gearStates.TryGetValue("mouse",out current)||!CurrentGearReadSession("mouse",current,out readSession))return false;
   string tab;gearTabs.TryGetValue("mouse",out tab);
   return tab=="按键"?MouseButtonsHasPendingDraft(current):tab=="灯光"?MouseLightingHasPendingDraft(current):tab=="Zone 模式"&&MouseZoneHasPendingDraft(current);
  }
  string PageReadViewKey(string target) {
   string key=PageReadKey(target)??target;if(target=="device:mouse"||target=="device:keyboard"||target=="device:headset"){
    string kind=target.Substring(7),tab;return key+":"+(gearTabs.TryGetValue(kind,out tab)?tab:kind=="headset"?"音效":"按键");
   }return key;
  }
  void QueuePageRead(string target){
   string key=PageReadKey(target),viewKey=PageReadViewKey(target);bool sameView=lastPageReadViewKey==viewKey;lastPageReadViewKey=viewKey;
   if(render||closing||key==null)return;
   // Editing re-renders must not clear IDs from a still-current read. Entering
   // another view, returning to LCD, or losing the worker permits a fresh read.
   if(sameView&&PreserveVisiblePageReadDraft(target)){if(pendingPageRead==target&&pendingPageReadMayPreserveDraft)pendingPageRead="";return;}
   bool entryAlreadyQueued=pendingPageRead==target&&!pendingPageReadMayPreserveDraft;
   pendingPageRead=target;pendingPageReadMayPreserveDraft=!entryAlreadyQueued&&sameView;
  }
  async Task ReadVisiblePage(string target) {
   if(target!=page||closing||render)return;
   switch(target) {
    case "设备":case "设备兼容":await ReadOriginalDiscovery();break;
    case "Windows 电源":await ReadWindowsPower();break;
    case "屏幕设置":await ReadWindowsDisplay();break;
    case "电池信息":await ReadWindowsBattery();break;
    case "灯效 & Aura Sync":case "神光同步":if(!controlling)await ScanAura();break;
    case "device:keyboard":await ReadGearDevice("keyboard");break;
    case "device:mouse":await ReadGearDevice("mouse");break;
    case "device:headset":await ReadGearDevice("headset");if(!closing&&page==target)await ReadOriginalWindowsAudio();break;
    case "device:fan":await ReadOriginalSnapshot("fan","7576");break;
    case "device:receiver":await ReadOriginalReceivers();break;
    case "device:motherboard":
     if(originalMotherboardTab=="磁盘信息")await ReadStorage();
     else if(originalMotherboardTab=="可编程接针")await ReadBoardArgbChannels();
     else if(originalMotherboardTab=="5\" LCD Display")await ReadOriginalSnapshot("lcd","7299");
     break;
   }
  }
 }
}
