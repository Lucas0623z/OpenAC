using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;

namespace RogControl {
 public sealed class UiPreferenceState {
  public string StartupPage { get; set; }
  public string LastPage { get; set; }
  public string Theme { get; set; }
  public UiPreferenceState(){StartupPage="last";LastPage="控制面板";Theme="dark";}
 }
 public static class UiPreferenceStore {
  public const int MaximumBytes=65536;
  static readonly string[] pages={"控制面板","系统监控","设备","灯效 & Aura Sync","情境设置档","宏","GameFirst","游戏库","Fan Xpert","工具","驱动程序更新","Windows 电源","屏幕设置","电池信息","更新库","device:motherboard","device:keyboard","device:mouse","device:headset","device:receiver","device:fan","device:gpu"};
  public static bool IsKnownPage(string page){return pages.Contains(page,StringComparer.Ordinal);}
  public static UiPreferenceState Normalize(UiPreferenceState value){
   var normalized=new UiPreferenceState();if(value==null)return normalized;
   normalized.StartupPage=value.StartupPage=="dashboard"?"dashboard":"last";
   normalized.Theme=value.Theme=="light"?"light":"dark";
   normalized.LastPage=value.LastPage=="设置"?"更新库":IsKnownPage(value.LastPage)?value.LastPage:"控制面板";
   return normalized;
  }
  public static string StartupTarget(UiPreferenceState state){var value=Normalize(state);return value.StartupPage=="dashboard"?"控制面板":value.LastPage;}
  public static UiPreferenceState Load(string path){
   try{var file=new FileInfo(path);if(!file.Exists||file.Length>MaximumBytes)return new UiPreferenceState();return Normalize(new JavaScriptSerializer().Deserialize<UiPreferenceState>(File.ReadAllText(path,Encoding.UTF8)));}catch{return new UiPreferenceState();}
  }
  public static void Save(string path,UiPreferenceState value){
   string absolute=Path.GetFullPath(path);string folder=Path.GetDirectoryName(absolute);Directory.CreateDirectory(folder);
   string temporary=Path.Combine(folder,"ui-preferences-"+Guid.NewGuid().ToString("N")+".tmp");
   try{File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Normalize(value)),new UTF8Encoding(false));if(File.Exists(absolute))File.Replace(temporary,absolute,null);else File.Move(temporary,absolute);}
   finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
 public partial class MainWindow {
  UiPreferenceState uiPreferences;
  string UiPreferencePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAC","ui-preferences.json"); } }
  void LoadUiPreferences(){if(uiPreferences==null)uiPreferences=render?new UiPreferenceState():UiPreferenceStore.Load(UiPreferencePath);ApplyUiPreferenceTheme();}
  string ResolveUiStartupPage(){if(uiPreferences==null)LoadUiPreferences();return UiPreferenceStore.StartupTarget(uiPreferences);}
  void RememberUiPage(string value){
   if(render||closing||!UiPreferenceStore.IsKnownPage(value))return;if(uiPreferences==null)LoadUiPreferences();if(uiPreferences.LastPage==value)return;
   var previous=uiPreferences;var next=UiPreferenceStore.Normalize(previous);next.LastPage=value;
   try{UiPreferenceStore.Save(UiPreferencePath,next);uiPreferences=next;}catch(Exception ex){history.Add("界面偏好保存失败："+ex.Message);}
  }
  void EnsureUiPreferenceBrushes(){foreach(Brush value in new[]{bg,panel,border,text,muted,accent}){var solid=value as SolidColorBrush;if(solid==null||solid.IsFrozen)throw new InvalidOperationException("主题暂不可用。");}}
  void ApplyUiPreferenceTheme(){
   if(uiPreferences==null)return;EnsureUiPreferenceBrushes();bool light=uiPreferences.Theme=="light";
   // Startup only: WPF may freeze shared brushes after templates are built.
   // The installed module confirms the base backgrounds, text and border
   // colors below. This is an owned limited palette, not vendor artwork.
   SetUiBrush(bg,light?"#F7F7F7":"#101010");SetUiBrush(panel,light?"#33FFFFFF":"#1C1C1C");
   SetUiBrush(border,light?"#CCCCCC":"#3C3C3C");SetUiBrush(text,light?"#1A1A1A":"#F1F1F1");
   SetUiBrush(muted,light?"#666666":"#AAAAAA");SetUiBrush(accent,light?"#FF3346":"#D7BF6C");
   Background=bg;Foreground=text;
  }
  static void SetUiBrush(Brush value,string color){((SolidColorBrush)value).Color=(Color)ColorConverter.ConvertFromString(color);}
 }
}
