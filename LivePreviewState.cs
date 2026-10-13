using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 // A data-only boundary between successive local preview assemblies. Never
 // carries worker handles, control permission, or running hardware actions.
 public sealed class PreviewSessionState {
  public int Schema=1;
  public string Page,SettingsTab,AuraTab,MotherboardTab,ControllerTab,HeadsetAudioTab;
  public double Left,Top,Width,Height,ScrollOffset;
  public bool Maximized;
  public MachineInfo Info;
  public MonitoringSample Monitor;
  public List<AuraDevice> Devices;
  public Dictionary<string,Dictionary<string,object>> SdkSnapshots,GearStates;
  public Dictionary<string,string> GearTabs;
  public string[] Selected;
  public string Effect,Color;public int Brightness;public double Speed;
  public MacroDocument MacroDraft;
  public int MacroSelection;
  public ScenarioProfile ScenarioDraft;
  public string[] History;
  public OpenAcUpdateResult UpdateResult;
  public string UpdateMessage;
  public Dictionary<string,DateTime> PageReadAttempts;
  public AsusCatalogResult AsusCatalog;
  public int AsusCatalogOsId;
  public string AsusCatalogMessage;
  public List<AuraSyncTarget> AuraSyncTargets;
  public string[] AuraSyncSelected;
  public AuraSyncResult AuraSyncResult;
  public Dictionary<string,AuraOwnershipResult> AuraOwnershipResults;
  public string AuraSyncMessage;
  public Guid WindowsPowerDraftScheme;
  public uint? WindowsPowerDraftAc,WindowsPowerDraftDc;
  public Dictionary<string,uint> WindowsDisplayDraft;
 }
 public partial class MainWindow {
  System.Windows.Threading.DispatcherTimer previewStateTimer;
  void BeginPreviewSnapshots() {
   if(render||String.IsNullOrEmpty(AppDomain.CurrentDomain.GetData("OpenAC.PreviewWorkerRoot") as string))return;
   previewStateTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
   previewStateTimer.Tick+=(s,e)=>{
    if(closing){previewStateTimer.Stop();return;}if(!IsLoaded||!CanPreviewReload)return;
    try{string directory=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview");string path=System.IO.Path.Combine(directory,"resume.json"),temporary=path+".new";System.IO.File.WriteAllText(temporary,CapturePreviewState(),new System.Text.UTF8Encoding(false));if(System.IO.File.Exists(path))System.IO.File.Replace(temporary,path,null);else System.IO.File.Move(temporary,path);}catch{}
   };previewStateTimer.Start();
  }
  public bool CanPreviewReload {get{var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;return !closing&&!busy&&!monitorBusy&&!controlling&&!AuraSyncBusy&&!AuraSyncConnectionsActive&&!windowsSystemWriting&&windowsPowerUndo==null&&windowsDisplayUndo==null&&(windowsSystemTask==null||windowsSystemTask.IsCompleted)&&!pageReadRunning&&!openAcUpdateChecking&&!asusCatalogChecking&&!MacroFeaturesActive&&!ScenarioFeaturesActive&&OwnedWindows.Count==0&&(handle==IntPtr.Zero||PreviewWindowEnabled(handle));}}
  [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="IsWindowEnabled")][return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]static extern bool PreviewWindowEnabled(IntPtr handle);
  public string PreviewPage {get{return page;}}
  public bool PreviewHardwareWritesEnabled {get{return HardwareWritesEnabled||experimentalLighting;}}
  public string CapturePreviewState() {
   var bounds=WindowState==WindowState.Normal?new Rect(Left,Top,Width,Height):RestoreBounds;
   var scroll=FindPreviewScroll(Content as DependencyObject);
   var state=new PreviewSessionState {
    Page=page,SettingsTab=settingsTab,AuraTab=auraTab,MotherboardTab=originalMotherboardTab,
    ControllerTab=originalControllerTab,HeadsetAudioTab=headsetAudioTab,
    Left=bounds.Left,Top=bounds.Top,Width=bounds.Width,Height=bounds.Height,
    Maximized=WindowState==WindowState.Maximized,ScrollOffset=scroll==null?0:scroll.VerticalOffset,
    Info=info,Monitor=monitor,Devices=devices,SdkSnapshots=sdkSnapshots,GearStates=gearStates,GearTabs=gearTabs,
    Selected=new List<string>(selected).ToArray(),Effect=effect,Color=color,Brightness=brightness,Speed=speed,
    MacroDraft=macroEditor.Current,MacroSelection=macroSelection,ScenarioDraft=scenarioDraft,
    History=history.ToArray(),UpdateResult=openAcUpdate,UpdateMessage=openAcUpdateMessage,PageReadAttempts=pageReadAttempts,
    AsusCatalog=asusCatalog,AsusCatalogOsId=asusCatalogOsId,AsusCatalogMessage=asusCatalogMessage,
    AuraSyncTargets=auraSyncTargets,AuraSyncSelected=new List<string>(auraSyncSelected).ToArray(),AuraSyncResult=auraSyncResult,AuraOwnershipResults=auraOwnershipResults,AuraSyncMessage=auraSyncMessage,
    WindowsPowerDraftScheme=windowsPowerDraftScheme,WindowsPowerDraftAc=windowsPowerDraftAc,WindowsPowerDraftDc=windowsPowerDraftDc,WindowsDisplayDraft=windowsDisplayDraft
   };
   return new JavaScriptSerializer{MaxJsonLength=4194304}.Serialize(state);
  }
  public void RestorePreviewState(string json) {
   if(String.IsNullOrEmpty(json)||json.Length>4194304)throw new ArgumentException("预览状态无效。");
   var state=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<PreviewSessionState>(json);
   if(state==null||state.Schema!=1)throw new ArgumentException("预览状态版本无效。");
   if(state.Info!=null)info=state.Info;if(state.Monitor!=null)monitor=state.Monitor;
   if(state.Devices!=null)devices=state.Devices;
   CopyPreviewMap(sdkSnapshots,state.SdkSnapshots);CopyPreviewMap(gearStates,state.GearStates);CopyPreviewMap(gearTabs,state.GearTabs);
   settingsTab=state.SettingsTab??settingsTab;auraTab=state.AuraTab??auraTab;
   originalMotherboardTab=state.MotherboardTab??originalMotherboardTab;
   originalControllerTab=state.ControllerTab??originalControllerTab;headsetAudioTab=state.HeadsetAudioTab??headsetAudioTab;
   selected.Clear();foreach(string id in state.Selected??new string[0])selected.Add(id);
   effect=state.Effect??effect;color=state.Color??color;brightness=state.Brightness;speed=state.Speed;
   if(state.MacroDraft!=null)macroEditor.Load(state.MacroDraft);macroSelection=state.MacroSelection;
   if(state.ScenarioDraft!=null)scenarioDraft=state.ScenarioDraft;
   history.Clear();history.AddRange(state.History??new string[0]);
   openAcUpdate=GitHubUpdateCatalog.NormalizeCachedResult(state.UpdateResult);openAcUpdateMessage=openAcUpdate==null?"":state.UpdateMessage??"";
   CopyPreviewMap(pageReadAttempts,state.PageReadAttempts);
   asusCatalog=state.AsusCatalog;if(state.AsusCatalogOsId>0&&state.AsusCatalogOsId<=1000)asusCatalogOsId=state.AsusCatalogOsId;asusCatalogMessage=state.AsusCatalogMessage??"";
   auraSyncTargets.Clear();if(state.AuraSyncTargets!=null)auraSyncTargets.AddRange(state.AuraSyncTargets);
   auraSyncSelected.Clear();foreach(string id in state.AuraSyncSelected??new string[0])if(auraSyncTargets.Exists(target=>target!=null&&target.Id==id&&target.CanStaticRgb&&target.Online))auraSyncSelected.Add(id);
   auraSyncResult=state.AuraSyncResult;auraSyncMessage=state.AuraSyncMessage??"";
   auraOwnershipResults.Clear();if(state.AuraOwnershipResults!=null)foreach(var pair in state.AuraOwnershipResults)if(pair.Value!=null&&pair.Value.Target!=null&&pair.Value.Target.Id==pair.Key)auraOwnershipResults[pair.Key]=pair.Value;
   windowsPowerDraftScheme=state.WindowsPowerDraftScheme;windowsPowerDraftAc=state.WindowsPowerDraftAc;windowsPowerDraftDc=state.WindowsPowerDraftDc;windowsPowerRestoredDraft=state.Page=="Windows 电源";
   CopyPreviewMap(windowsDisplayDraft,state.WindowsDisplayDraft);
   if(FinitePreviewNumber(state.Width)&&FinitePreviewNumber(state.Height)){Width=Math.Max(MinWidth,state.Width);Height=Math.Max(MinHeight,state.Height);}
   if(FinitePreviewNumber(state.Left)&&FinitePreviewNumber(state.Top)){WindowStartupLocation=WindowStartupLocation.Manual;Left=state.Left;Top=state.Top;}
   if(state.Maximized)WindowState=WindowState.Maximized;
   // Permission stays disabled in the new instance; a refresh never reapplies
   // settings or resumes a macro, animation, or scenario.
   ShowPage(state.Page??"控制面板");
   Action restoreScroll=()=>{var scroll=FindPreviewScroll(Content as DependencyObject);if(scroll!=null&&FinitePreviewNumber(state.ScrollOffset))scroll.ScrollToVerticalOffset(state.ScrollOffset);};
   if(IsLoaded)Dispatcher.BeginInvoke(restoreScroll,System.Windows.Threading.DispatcherPriority.Loaded);else Loaded+=(s,e)=>restoreScroll();
  }
  static bool FinitePreviewNumber(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value);}
  static void CopyPreviewMap<T>(Dictionary<string,T> target,Dictionary<string,T> source){target.Clear();if(source!=null)foreach(var pair in source)target[pair.Key]=pair.Value;}
  static ScrollViewer FindPreviewScroll(DependencyObject root){if(root==null)return null;var scroll=root as ScrollViewer;if(scroll!=null)return scroll;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindPreviewScroll(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;}
 }
}
