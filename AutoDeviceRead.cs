using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RogControl {
 public partial class MainWindow {
  DispatcherTimer pageReadTimer;
  string pendingPageRead="";
  bool pageReadRunning;
  readonly Dictionary<string,DateTime> pageReadAttempts=new Dictionary<string,DateTime>();
  void BeginPageReads() {
   if(render)return;
   pageReadTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};
   pageReadTimer.Tick+=async(s,e)=>{
    if(!IsLoaded||closing||busy||pageReadRunning||String.IsNullOrEmpty(pendingPageRead))return;
    string target=pendingPageRead,key=PageReadKey(target);pendingPageRead="";if(key==null)return;
    DateTime attempted;if(pageReadAttempts.TryGetValue(key,out attempted)&&(DateTime.UtcNow-attempted).TotalSeconds<10)return;
    pageReadAttempts[key]=DateTime.UtcNow;pageReadRunning=true;
    try{await ReadVisiblePage(target);}catch(Exception ex){if(!closing)Log("读取失败："+ex.Message);}finally{pageReadAttempts[key]=DateTime.UtcNow;pageReadRunning=false;}
   };
   pageReadTimer.Start();
  }
  string PageReadKey(string target) {
   if(target=="device:motherboard")return target+":"+originalMotherboardTab;
   if(target=="Windows 电源"||target=="屏幕设置"||target=="电池信息")return target;
   return target=="设备"||target=="灯效 & Aura Sync"||target=="神光同步"||target=="device:keyboard"||target=="device:mouse"||target=="device:headset"||target=="device:fan"||target=="device:receiver"?target:null;
  }
  void QueuePageRead(string target){if(!render&&!closing&&PageReadKey(target)!=null)pendingPageRead=target;}
  async Task ReadVisiblePage(string target) {
   if(target!=page||closing||render)return;
   switch(target) {
    case "设备":await ReadOriginalDiscovery();break;
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
