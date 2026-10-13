using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RogControl {
 public sealed class MacroAction {
  public string Kind {get;set;}
  public int Key {get;set;}
  public int ScanCode {get;set;}
  public bool Extended {get;set;}
  public int Button {get;set;}
  public int X {get;set;}
  public int Y {get;set;}
  public int Value {get;set;}
  public int DelayMs {get;set;}
  public int BeforeMs {get;set;}
  public MacroAction Clone(){return (MacroAction)MemberwiseClone();}
  public string PressId {get{return Kind.StartsWith("key-",StringComparison.Ordinal)?"key:"+Key+":"+ScanCode+":"+Extended:"mouse:"+Button;}}
  public MacroAction Release(){var a=Clone();a.Kind=Kind=="key-down"?"key-up":"mouse-up";a.BeforeMs=0;return a;}
 }
 public sealed class MacroDocument {
  public int Version {get;set;}
  public string Id {get;set;}
  public string Name {get;set;}
  public List<MacroAction> Actions {get;set;}
  public MacroDocument(){Version=1;Id=Guid.NewGuid().ToString("N");Name="新宏";Actions=new List<MacroAction>();}
  public MacroDocument Clone(){return new MacroDocument{Version=Version,Id=Id,Name=Name,Actions=Actions.Select(a=>a.Clone()).ToList()};}
  public long DurationMs {get{return Actions.Sum(a=>(long)a.BeforeMs+(a.Kind=="delay"?a.DelayMs:0));}}
 }
 public static class MacroRules {
  public const int MaxActions=100,MaxDelayMs=60000,MaxTotalMs=600000;
  static readonly string[] Kinds={"key-down","key-up","mouse-down","mouse-up","mouse-wheel","mouse-move","delay"};
  public static void Validate(MacroDocument doc) {
   Guid id;if(doc==null||doc.Version!=1||!Guid.TryParse(doc.Id,out id))throw new InvalidDataException("不是支持的宏文件（格式版本 1）。");
   if(String.IsNullOrWhiteSpace(doc.Name)||doc.Name.Length>120)throw new InvalidDataException("宏名称需为 1–120 个字符。");
   if(doc.Actions==null||doc.Actions.Count>MaxActions)throw new InvalidDataException("一个宏最多包含 100 条动作。");
   foreach(var a in doc.Actions) {
    if(a==null||!Kinds.Contains(a.Kind))throw new InvalidDataException("包含不支持的动作类型。");
    if(a.BeforeMs<0||a.BeforeMs>MaxDelayMs||a.DelayMs<0||a.DelayMs>MaxDelayMs)throw new InvalidDataException("单次延时需为 0–60000 毫秒。");
    if(a.Kind.StartsWith("key-",StringComparison.Ordinal)&&(a.Key<1||a.Key>254||a.Key==0x78||a.ScanCode<0||a.ScanCode>255))throw new InvalidDataException("键盘动作无效，F9 为停止键，不能加入宏。");
    if(a.Kind.StartsWith("mouse-",StringComparison.Ordinal)&&(a.Button<0||a.Button>5))throw new InvalidDataException("鼠标按钮无效。");
    if((a.Kind=="mouse-down"||a.Kind=="mouse-up")&&a.Button==0)throw new InvalidDataException("请指定鼠标按钮。");
    if(a.Kind=="mouse-wheel"&&(a.Value==0||a.Value<-12000||a.Value>12000))throw new InvalidDataException("滚轮值需为 -12000 至 12000，且不能为 0。");
    if(a.Kind=="mouse-move"&&(a.X<-100000||a.X>100000||a.Y<-100000||a.Y>100000))throw new InvalidDataException("鼠标位置超出支持范围。");
   }
   if(doc.DurationMs>MaxTotalMs)throw new InvalidDataException("单个宏总延时最多 10 分钟。");
  }
 }
 public sealed class MacroEditor {
  readonly Stack<MacroDocument> undo=new Stack<MacroDocument>(),redo=new Stack<MacroDocument>();
  public MacroDocument Current {get;private set;}
  public bool CanUndo {get{return undo.Count>0;}}
  public bool CanRedo {get{return redo.Count>0;}}
  public MacroEditor(){Current=new MacroDocument();}
  public void Load(MacroDocument doc){MacroRules.Validate(doc);Current=doc.Clone();undo.Clear();redo.Clear();}
  public void Change(Action<MacroDocument> change){var next=Current.Clone();change(next);MacroRules.Validate(next);undo.Push(Current.Clone());redo.Clear();Current=next;}
  public void Undo(){if(!CanUndo)return;redo.Push(Current.Clone());Current=undo.Pop();}
  public void Redo(){if(!CanRedo)return;undo.Push(Current.Clone());Current=redo.Pop();}
 }
 public sealed class MacroRepository {
  public string DirectoryPath {get;private set;}
  public MacroRepository(string directory){DirectoryPath=directory;}
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1024*1024,RecursionLimit=32};}
  public static string Encode(MacroDocument doc){MacroRules.Validate(doc);return Json().Serialize(doc);}
  public static MacroDocument Decode(string json){if(json==null||json.Length>1024*1024)throw new InvalidDataException("宏文件过大。");MacroDocument doc;try{doc=Json().Deserialize<MacroDocument>(json);}catch(Exception e){throw new InvalidDataException("宏文件格式无法读取。",e);}MacroRules.Validate(doc);return doc;}
  public static MacroDocument Read(string path){if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("宏文件过大。");return Decode(File.ReadAllText(path,Encoding.UTF8));}
  public static void Write(string path,MacroDocument doc){string json=Encode(doc);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,json,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  public void Save(MacroDocument doc){MacroRules.Validate(doc);Directory.CreateDirectory(DirectoryPath);Write(Path.Combine(DirectoryPath,Guid.Parse(doc.Id).ToString("N")+".rogmacro.json"),doc);}
  public List<MacroDocument> List(out string warning){warning=null;var result=new List<MacroDocument>();if(!Directory.Exists(DirectoryPath))return result;foreach(string file in Directory.GetFiles(DirectoryPath,"*.rogmacro.json").Take(200)){try{result.Add(Read(file));}catch{warning="部分本地宏无法读取，已保留原文件。";}}return result.OrderBy(d=>d.Name,StringComparer.CurrentCultureIgnoreCase).ToList();}
 }
 public interface IMacroInput {
  void CheckReady();
  void Send(MacroAction action);
 }
 public static class MacroPlayback {
  public static async Task RunAsync(MacroDocument source,IMacroInput input,CancellationToken cancel,Action<int> progress=null) {
   MacroRules.Validate(source);MacroDocument doc=source.Clone();var held=new Dictionary<string,MacroAction>();Exception failure=null;var releaseErrors=new List<Exception>();
   try {
    cancel.ThrowIfCancellationRequested();input.CheckReady();
    for(int i=0;i<doc.Actions.Count;i++) {
     var a=doc.Actions[i];cancel.ThrowIfCancellationRequested();if(a.BeforeMs>0)await Task.Delay(a.BeforeMs,cancel);
     if(a.Kind=="delay"){if(a.DelayMs>0)await Task.Delay(a.DelayMs,cancel);}
     else {
      bool down=a.Kind=="key-down"||a.Kind=="mouse-down",up=a.Kind=="key-up"||a.Kind=="mouse-up";
      // Never release a physical key which this playback did not press.
      if(!up||held.ContainsKey(a.PressId)){input.Send(a);if(down)held[a.PressId]=a.Clone();if(up)held.Remove(a.PressId);}
     }
     if(progress!=null)progress(i+1);
    }
   }catch(Exception e){failure=e;}
   finally {
    foreach(var a in held.Values.Reverse()){try{input.Send(a.Release());}catch(Exception e){releaseErrors.Add(e);}}
   }
   if(releaseErrors.Count>0){if(failure!=null)releaseErrors.Insert(0,failure);throw new AggregateException("部分按键释放未被 Windows 接受。",releaseErrors);}
   if(failure!=null)throw failure;
  }
 }
 // Official API references: learn.microsoft.com/windows/win32/api/winuser/
 // nf-winuser-sendinput, ns-winuser-input, ns-winuser-keybdinput,
 // ns-winuser-mouseinput; winmsg/lowlevelkeyboardproc, lowlevelmouseproc.
 public sealed class WindowsMacroInput:IMacroInput {
  public void CheckReady(){for(int key=1;key<255;key++)if((MacroNative.GetAsyncKeyState(key)&0x8000)!=0)throw new InvalidOperationException("请先松开键盘和鼠标按钮，再执行宏。");}
  public void Send(MacroAction a) {
   MacroNative.INPUT input=new MacroNative.INPUT();
   if(a.Kind.StartsWith("key-",StringComparison.Ordinal)) {
    input.type=1;input.data.keyboard=new MacroNative.KEYBDINPUT{wVk=(ushort)(a.ScanCode>0?0:a.Key),wScan=(ushort)a.ScanCode,dwFlags=(uint)((a.ScanCode>0?8:0)|(a.Extended?1:0)|(a.Kind=="key-up"?2:0))};
   }else {
    uint flags=0,data=0;int x=0,y=0;
    if(a.Kind=="mouse-move") {
     int left=MacroNative.GetSystemMetrics(76),top=MacroNative.GetSystemMetrics(77),width=MacroNative.GetSystemMetrics(78),height=MacroNative.GetSystemMetrics(79);
     if(width<2||height<2)throw new InvalidOperationException("屏幕范围不可用。");
     if(a.X<left||a.X>=left+width||a.Y<top||a.Y>=top+height)throw new InvalidOperationException("宏鼠标位置不在当前显示器范围内。");
     x=(int)Math.Round((a.X-left)*65535.0/(width-1));y=(int)Math.Round((a.Y-top)*65535.0/(height-1));flags=0x8000|0x4000|1;
    }else if(a.Kind=="mouse-wheel"){flags=0x800;data=unchecked((uint)a.Value);}
    else if(a.Kind=="mouse-down"||a.Kind=="mouse-up") {
     bool up=a.Kind=="mouse-up";if(a.Button==1)flags=up?4U:2U;else if(a.Button==2)flags=up?16U:8U;else if(a.Button==3)flags=up?64U:32U;else{flags=up?256U:128U;data=(uint)(a.Button-3);}
    }else throw new InvalidOperationException("不能发送此动作。");
    input.type=0;input.data.mouse=new MacroNative.MOUSEINPUT{dx=x,dy=y,mouseData=data,dwFlags=flags};
   }
   if(MacroNative.SendInput(1,new[]{input},Marshal.SizeOf(typeof(MacroNative.INPUT)))!=1)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows 未接受宏输入；请检查前台窗口权限。");
  }
 }
 internal static class MacroNative {
  internal delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
  [StructLayout(LayoutKind.Sequential)]internal struct POINT{internal int x,y;}
  [StructLayout(LayoutKind.Sequential)]internal struct KBDLLHOOKSTRUCT{internal uint vkCode,scanCode,flags,time;internal UIntPtr extra;}
  [StructLayout(LayoutKind.Sequential)]internal struct MSLLHOOKSTRUCT{internal POINT pt;internal uint mouseData,flags,time;internal UIntPtr extra;}
  [StructLayout(LayoutKind.Sequential)]internal struct MSG{internal IntPtr hwnd;internal uint message;internal UIntPtr wParam;internal IntPtr lParam;internal uint time;internal POINT pt;internal uint privateData;}
  [StructLayout(LayoutKind.Sequential)]internal struct MOUSEINPUT{internal int dx,dy;internal uint mouseData,dwFlags,time;internal UIntPtr extra;}
  [StructLayout(LayoutKind.Sequential)]internal struct KEYBDINPUT{internal ushort wVk,wScan;internal uint dwFlags,time;internal UIntPtr extra;}
  [StructLayout(LayoutKind.Explicit)]internal struct INPUTUNION{[FieldOffset(0)]internal MOUSEINPUT mouse;[FieldOffset(0)]internal KEYBDINPUT keyboard;}
  [StructLayout(LayoutKind.Sequential)]internal struct INPUT{internal uint type;internal INPUTUNION data;}
  [DllImport("user32.dll",SetLastError=true)]internal static extern uint SendInput(uint count,INPUT[] inputs,int size);
  [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")]internal static extern int GetSystemMetrics(int index);
  [DllImport("user32.dll",SetLastError=true)]internal static extern IntPtr SetWindowsHookEx(int kind,HookProc callback,IntPtr module,uint thread);
  [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]internal static extern bool UnhookWindowsHookEx(IntPtr handle);
  [DllImport("user32.dll")]internal static extern IntPtr CallNextHookEx(IntPtr handle,int code,IntPtr message,IntPtr data);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]internal static extern IntPtr GetModuleHandle(string name);
  [DllImport("kernel32.dll")]internal static extern uint GetCurrentThreadId();
  [DllImport("kernel32.dll")]internal static extern uint GetCurrentProcessId();
  [DllImport("user32.dll")]internal static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]internal static extern IntPtr WindowFromPoint(POINT point);
  [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll",SetLastError=true)]internal static extern int GetMessage(out MSG message,IntPtr window,uint min,uint max);
  [DllImport("user32.dll")]internal static extern bool PeekMessage(out MSG message,IntPtr window,uint min,uint max,uint remove);
  [DllImport("user32.dll",SetLastError=true)]internal static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
 }
 // No hook is installed by construction. Start is called only from a manual
 // record/play button. Dedicated hook thread keeps UI work out of callbacks.
 public sealed class MacroHookSession:IDisposable {
  readonly Action<MacroAction> receive;readonly Action stop;readonly bool mouse;
  Thread thread;uint threadId;IntPtr keyboardHook,mouseHook;MacroNative.HookProc keyboardProc,mouseProc;int stopped;Exception startupError;
  public MacroHookSession(bool captureMouse,Action<MacroAction> onInput,Action onStop){mouse=captureMouse;receive=onInput;stop=onStop;}
  public void Start() {
   if(thread!=null)throw new InvalidOperationException("会话已启动。");var ready=new ManualResetEventSlim(false);
   thread=new Thread(()=>{
    try {
     threadId=MacroNative.GetCurrentThreadId();MacroNative.MSG message;MacroNative.PeekMessage(out message,IntPtr.Zero,0,0,0);
     keyboardProc=Keyboard;mouseProc=Mouse;keyboardHook=MacroNative.SetWindowsHookEx(13,keyboardProc,MacroNative.GetModuleHandle(null),0);
     if(keyboardHook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法启用宏停止键。");
     if(mouse){mouseHook=MacroNative.SetWindowsHookEx(14,mouseProc,MacroNative.GetModuleHandle(null),0);if(mouseHook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法启用鼠标录制。");}
     ready.Set();if(Volatile.Read(ref stopped)==0)while(MacroNative.GetMessage(out message,IntPtr.Zero,0,0)>0){}
    }catch(Exception e){startupError=e;ready.Set();}
    finally{RemoveHooks();}
   }){IsBackground=true,Name="ROG Control explicit macro session"};thread.Start();
   if(!ready.Wait(2000)){Dispose();throw new TimeoutException("宏输入会话启动超时。");}if(startupError!=null){Dispose();throw startupError;}
  }
  IntPtr Keyboard(int code,IntPtr message,IntPtr data) {
   if(code>=0&&Volatile.Read(ref stopped)==0)try {
    var e=(MacroNative.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(data,typeof(MacroNative.KBDLLHOOKSTRUCT));int m=message.ToInt32();bool down=m==0x100||m==0x104,up=m==0x101||m==0x105;
    if((e.flags&0x12)==0&&(down||up)) {
     if(e.vkCode==0x78){if(down)NotifyStop();return (IntPtr)1;}
     if(receive!=null&&!OurWindow(MacroNative.GetForegroundWindow()))receive(new MacroAction{Kind=down?"key-down":"key-up",Key=(int)e.vkCode,ScanCode=(int)e.scanCode,Extended=(e.flags&1)!=0});
    }
   }catch{NotifyStop();}
   return MacroNative.CallNextHookEx(IntPtr.Zero,code,message,data);
  }
  IntPtr Mouse(int code,IntPtr message,IntPtr data) {
   if(code>=0&&Volatile.Read(ref stopped)==0&&receive!=null)try {
    var e=(MacroNative.MSLLHOOKSTRUCT)Marshal.PtrToStructure(data,typeof(MacroNative.MSLLHOOKSTRUCT));if((e.flags&3)==0&&!OurWindow(MacroNative.WindowFromPoint(e.pt))) {
     int m=message.ToInt32(),button=0;string kind=null;
     if(m==0x200)kind="mouse-move";else if(m==0x20A)kind="mouse-wheel";
     else if(m==0x201||m==0x202){kind=m==0x201?"mouse-down":"mouse-up";button=1;}
     else if(m==0x204||m==0x205){kind=m==0x204?"mouse-down":"mouse-up";button=2;}
     else if(m==0x207||m==0x208){kind=m==0x207?"mouse-down":"mouse-up";button=3;}
     else if(m==0x20B||m==0x20C){kind=m==0x20B?"mouse-down":"mouse-up";button=3+(int)(e.mouseData>>16);}
     if(kind!=null)receive(new MacroAction{Kind=kind,Button=button,X=e.pt.x,Y=e.pt.y,Value=kind=="mouse-wheel"?(short)(e.mouseData>>16):0});
    }
   }catch{NotifyStop();}
   return MacroNative.CallNextHookEx(IntPtr.Zero,code,message,data);
  }
  static bool OurWindow(IntPtr window){uint process;MacroNative.GetWindowThreadProcessId(window,out process);return process==MacroNative.GetCurrentProcessId();}
  void NotifyStop(){try{if(stop!=null)stop();}catch{}}
  void RemoveHooks(){IntPtr keyboard=Interlocked.Exchange(ref keyboardHook,IntPtr.Zero),pointer=Interlocked.Exchange(ref mouseHook,IntPtr.Zero);if(keyboard!=IntPtr.Zero)MacroNative.UnhookWindowsHookEx(keyboard);if(pointer!=IntPtr.Zero)MacroNative.UnhookWindowsHookEx(pointer);}
  public void Dispose(){Interlocked.Exchange(ref stopped,1);RemoveHooks();if(threadId!=0)MacroNative.PostThreadMessage(threadId,0x12,UIntPtr.Zero,IntPtr.Zero);if(thread!=null&&Thread.CurrentThread!=thread)thread.Join(1000);}
 }
 // Pure capture buffer is also used by synthetic tests; it never installs hooks.
 public sealed class MacroCaptureBuffer {
  readonly object gate=new object();readonly bool positions;readonly int capacity;readonly long timeLimit;readonly List<MacroAction> actions=new List<MacroAction>();readonly HashSet<string> down=new HashSet<string>();long last,lastMove;bool ended;
  public MacroCaptureBuffer(bool recordPositions,int maxActions=100,long maxDurationMs=MacroRules.MaxTotalMs){if(maxActions<1||maxActions>100||maxDurationMs<1||maxDurationMs>MacroRules.MaxTotalMs)throw new ArgumentOutOfRangeException("录制剩余容量无效。");positions=recordPositions;capacity=maxActions;timeLimit=maxDurationMs;}
  public string StopReason {get;private set;}
  public int Count {get{lock(gate)return actions.Count;}}
  public bool Add(MacroAction action,long elapsedMs) {
   lock(gate) {
    if(ended)return false;if(action.Kind=="mouse-move"&&(!positions||elapsedMs-lastMove<50))return true;
    bool press=action.Kind=="key-down"||action.Kind=="mouse-down",release=action.Kind=="key-up"||action.Kind=="mouse-up";
    if(press&&!down.Add(action.PressId))return true;if(release&&!down.Remove(action.PressId))return true;
    long gap=Math.Max(0,elapsedMs-last);if(gap>MacroRules.MaxDelayMs||elapsedMs>timeLimit){ended=true;StopReason="达到录制时间上限，已停止。";return false;}
    var a=action.Clone();a.BeforeMs=(int)gap;actions.Add(a);last=elapsedMs;if(action.Kind=="mouse-move")lastMove=elapsedMs;
    if(actions.Count>=capacity){ended=true;StopReason="达到 100 条动作上限，已停止。";return false;}return true;
   }
  }
  public List<MacroAction> Snapshot(){lock(gate)return actions.Select(a=>a.Clone()).ToList();}
 }
}
