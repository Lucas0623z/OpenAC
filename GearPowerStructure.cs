using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public sealed class GearPowerFieldDraft { public string Kind,DeviceId,Setting; public int Baseline,Value; }
 public sealed class GearPowerVoiceDraft { public string DeviceId; public bool Baseline,Value; }
 public partial class MainWindow {
  readonly Dictionary<string,GearPowerFieldDraft> gearPowerDrafts=new Dictionary<string,GearPowerFieldDraft>();
  GearPowerVoiceDraft gearPowerVoiceDraft;bool gearPowerVoiceExpanded;
  static int[] GearPowerValues(string kind,string setting) {
   if(setting=="sleepMinutes")return kind=="headset"?new[]{2,3,5,10,15,0}:kind=="mouse"||kind=="keyboard"?new[]{1,2,3,5,10,0}:new int[0];
   if(setting=="lowBatteryThreshold")return kind=="headset"||kind=="mouse"||kind=="keyboard"?new[]{0,10,20,30,40,50}:new int[0];
   if(setting=="pollingRate"&&kind=="keyboard")return new[]{1000,8000};return new int[0];
  }
  static int? GearPowerObserved(Dictionary<string,object> state,string setting) {
   if(!GearOnline(state))return null;object value;var snapshot=GearMap(state,"snapshot");if(snapshot==null||!snapshot.TryGetValue(setting,out value)||!(value is int))return null;return (int)value;
  }
  int[] GearPowerAllowed(string kind,string setting,Dictionary<string,object> state) {
   int[] values=GearPowerValues(kind,setting);if(setting!="pollingRate")return values;
   return values.Where(v=>GearOptions(state,"pollingRates").Contains(v.ToString(CultureInfo.InvariantCulture))).ToArray();
  }
  GearPowerFieldDraft GearPowerDraft(string kind,string setting,Dictionary<string,object> state) {
   string key=kind+":"+setting,id=OriginalStateText(state,"deviceId","");int? observed=GearPowerObserved(state,setting);var options=GearPowerAllowed(kind,setting,state);GearPowerFieldDraft draft;
   if(id.Length==0||!observed.HasValue||!options.Contains(observed.Value)){gearPowerDrafts.Remove(key);return null;}
   if(!gearPowerDrafts.TryGetValue(key,out draft)||draft.DeviceId!=id||draft.Kind!=kind||draft.Setting!=setting||!options.Contains(draft.Value)||observed.Value!=draft.Baseline&&observed.Value!=draft.Value)draft=new GearPowerFieldDraft{Kind=kind,DeviceId=id,Setting=setting,Baseline=observed.Value,Value=observed.Value};
   if(observed.Value==draft.Value)draft.Baseline=observed.Value;gearPowerDrafts[key]=draft;return draft;
  }
  void RestoreGearPowerDrafts(Dictionary<string,GearPowerFieldDraft> restored) {
   gearPowerDrafts.Clear();if(restored==null||restored.Count>7)return;
   foreach(var item in restored){var draft=item.Value;Dictionary<string,object> state;if(draft==null||item.Key!=draft.Kind+":"+draft.Setting||!gearStates.TryGetValue(draft.Kind??"",out state)||draft.DeviceId!=OriginalStateText(state,"deviceId","")||String.IsNullOrEmpty(draft.DeviceId))continue;int? observed=GearPowerObserved(state,draft.Setting);int[] allowed=GearPowerAllowed(draft.Kind,draft.Setting,state);if(!observed.HasValue||observed.Value!=draft.Baseline||!allowed.Contains(draft.Baseline)||!allowed.Contains(draft.Value)||!GearCanSet(state,draft.Setting))continue;gearPowerDrafts[item.Key]=new GearPowerFieldDraft{Kind=draft.Kind,DeviceId=draft.DeviceId,Setting=draft.Setting,Baseline=draft.Baseline,Value=draft.Value};}
  }
  bool GearPowerMayApply(GearPowerFieldDraft draft) {
   if(draft==null||draft.Value==draft.Baseline||busy||closing||render)return false;Dictionary<string,object> current;return gearStates.TryGetValue(draft.Kind,out current)&&draft.DeviceId==OriginalStateText(current,"deviceId","")&&GearPowerObserved(current,draft.Setting)==draft.Baseline&&GearPowerAllowed(draft.Kind,draft.Setting,current).Contains(draft.Value)&&CanApplyGear(current,draft.Setting);
  }
  void RestoreGearPowerVoiceDraft(GearPowerVoiceDraft restored) {
   gearPowerVoiceDraft=null;Dictionary<string,object> state;if(restored==null||!gearStates.TryGetValue("headset",out state)||!GearOnline(state)||String.IsNullOrEmpty(restored.DeviceId)||restored.DeviceId!=OriginalStateText(state,"deviceId","")||OriginalStateFlag(GearMap(state,"snapshot"),"lowBatteryVoicePrompt")!=restored.Baseline||!GearCanSet(state,"lowBatteryVoicePrompt"))return;gearPowerVoiceDraft=new GearPowerVoiceDraft{DeviceId=restored.DeviceId,Baseline=restored.Baseline,Value=restored.Value};
  }
  UIElement GearPowerVoiceOptions(Dictionary<string,object> state) {
   var body=new StackPanel();var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.Children.Add(GearPowerRow("当电池电量达到以下百分比时发出灯光警示:","",GearPowerChoice("headset",state,"lowBatteryThreshold")));var toggle=Button(gearPowerVoiceExpanded?"⌃":"⌄",()=>{gearPowerVoiceExpanded=!gearPowerVoiceExpanded;ShowPage(page);});toggle.Width=26;toggle.Height=34;toggle.Padding=new Thickness(0);toggle.Margin=new Thickness(0,0,16,12);toggle.Background=Brushes.Transparent;toggle.BorderThickness=new Thickness(0);toggle.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(toggle,1);header.Children.Add(toggle);body.Children.Add(header);if(!gearPowerVoiceExpanded)return body;
   string id=OriginalStateText(state,"deviceId","");bool? observed=GearOnline(state)?OriginalStateFlag(GearMap(state,"snapshot"),"lowBatteryVoicePrompt"):null;if(!observed.HasValue||id.Length==0)gearPowerVoiceDraft=null;else if(gearPowerVoiceDraft==null||gearPowerVoiceDraft.DeviceId!=id||observed.Value!=gearPowerVoiceDraft.Baseline&&observed.Value!=gearPowerVoiceDraft.Value)gearPowerVoiceDraft=new GearPowerVoiceDraft{DeviceId=id,Baseline=observed.Value,Value=observed.Value};else if(observed.Value==gearPowerVoiceDraft.Value)gearPowerVoiceDraft.Baseline=observed.Value;
   var draft=gearPowerVoiceDraft;var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(74,8,40,22)};var check=new CheckBox{Content="关闭低电量语音提示。",Foreground=text,FontSize=14,IsThreeState=!observed.HasValue,IsChecked=draft==null?(bool?)null:!draft.Value,IsEnabled=draft!=null&&GearCanSet(state,"lowBatteryVoicePrompt")&&!busy&&!closing&&!render,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(check);Func<bool> canApply=()=>{Dictionary<string,object> current;return draft!=null&&draft.Value!=draft.Baseline&&!closing&&!render&&gearStates.TryGetValue("headset",out current)&&draft.DeviceId==OriginalStateText(current,"deviceId","")&&OriginalStateFlag(GearMap(current,"snapshot"),"lowBatteryVoicePrompt")==draft.Baseline&&CanApplyGear(current,"lowBatteryVoicePrompt");};var apply=Button("应用",async()=>{if(!canApply())return;await SetGearSetting("headset",gearStates["headset"],"lowBatteryVoicePrompt",draft.Value);});apply.Width=76;apply.Height=34;apply.Margin=new Thickness(24,0,0,0);apply.Padding=new Thickness(8,4,8,4);apply.IsEnabled=canApply();apply.Visibility=draft!=null&&draft.Value!=draft.Baseline?Visibility.Visible:Visibility.Collapsed;row.Children.Add(apply);RoutedEventHandler edit=(s,e)=>{if(draft==null||!check.IsEnabled||busy||closing||render||!check.IsChecked.HasValue)return;draft.Value=!check.IsChecked.Value;apply.Visibility=draft.Value!=draft.Baseline?Visibility.Visible:Visibility.Collapsed;apply.IsEnabled=canApply();};check.Checked+=edit;check.Unchecked+=edit;body.Children.Add(new Border{Background=Brush("#1A1A1A"),Margin=new Thickness(0,-12,0,12),Child=row});return body;
  }
  UIElement GearPowerChoice(string kind,Dictionary<string,object> state,string setting) {
   var draft=GearPowerDraft(kind,setting,state);int[] values=GearPowerAllowed(kind,setting,state);var labels=values.Select(v=>setting=="sleepMinutes"?v==0?"永不":v+" 分钟":setting=="lowBatteryThreshold"?v==0?"永不":v+"%":v.ToString(CultureInfo.InvariantCulture)).ToArray();
   var holder=new Grid{VerticalAlignment=VerticalAlignment.Center};holder.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});holder.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var combo=MousePerformanceCombo(new[]{"—"}.Concat(labels),draft==null?0:Array.IndexOf(values,draft.Value)+1,draft!=null&&GearCanSet(state,setting)&&!busy&&!closing&&!render,200);combo.Margin=new Thickness(0);holder.Children.Add(combo);
   var apply=Button("应用",async()=>{if(!GearPowerMayApply(draft))return;Dictionary<string,object> current;gearStates.TryGetValue(kind,out current);await SetGearSetting(kind,current,setting,draft.Value);});apply.Width=76;apply.Height=34;apply.Padding=new Thickness(8,4,8,4);apply.Margin=new Thickness(12,0,0,0);apply.IsEnabled=GearPowerMayApply(draft);apply.Visibility=draft!=null&&draft.Baseline!=draft.Value?Visibility.Visible:Visibility.Collapsed;Grid.SetColumn(apply,1);holder.Children.Add(apply);
   combo.SelectionChanged+=(s,e)=>{if(draft==null||!combo.IsEnabled||busy||closing||render||combo.SelectedIndex<=0)return;draft.Value=values[combo.SelectedIndex-1];apply.Visibility=draft.Baseline!=draft.Value?Visibility.Visible:Visibility.Collapsed;apply.IsEnabled=GearPowerMayApply(draft);};return holder;
  }
  Border GearPowerRow(string heading,string note,UIElement choice) {
   var row=new Grid{Margin=new Thickness(40,0,40,0)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var labels=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,24,0)};var title=Label(heading,15,text,FontWeights.SemiBold);title.Margin=new Thickness(0);labels.Children.Add(title);if(!String.IsNullOrEmpty(note)){var detail=Paragraph(note,11,muted);detail.Margin=new Thickness(0,4,0,0);labels.Children.Add(detail);}row.Children.Add(labels);Grid.SetColumn(choice,1);row.Children.Add(choice);return new Border{MinHeight=76,Background=Brush("#1A1A1A"),Margin=new Thickness(0,0,0,12),Child=row};
  }
  void OriginalGearPowerPage(StackPanel target,string kind,Dictionary<string,object> state) {
   var snapshot=GearOnline(state)?GearMap(state,"snapshot"):null;var body=new StackPanel{Margin=new Thickness(0,24,8,0)};target.Children.Add(body);var heading=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(31,0,0,43)};heading.Children.Add(Label("≋",22,accent));heading.Children.Add(Label("无线节省电选项:",16,text,FontWeights.SemiBold));body.Children.Add(heading);
   var charge=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(31,0,0,44)};charge.Children.Add(GearBatteryIcon(MouseInteger(snapshot,"batteryPercent",0,100)));int? battery=MouseInteger(snapshot,"batteryPercent",0,100);var value=Label(battery.HasValue?battery.Value+"%":"—",21,text,FontWeights.SemiBold);value.Margin=new Thickness(15,0,0,0);value.VerticalAlignment=VerticalAlignment.Center;charge.Children.Add(value);if(OriginalStateFlag(snapshot,"charging")==true){var charging=Label("正在充电",13,muted);charging.Margin=new Thickness(18,0,0,0);charging.VerticalAlignment=VerticalAlignment.Center;charge.Children.Add(charging);}body.Children.Add(charge);
   var notify=Button("现在开启",()=>{});notify.Width=158;notify.Height=34;notify.Padding=new Thickness(8,4,8,4);notify.Margin=new Thickness(0);notify.IsEnabled=false;notify.ToolTip="电量通知暂不可用";ToolTipService.SetShowOnDisabled(notify,true);body.Children.Add(GearPowerRow("电量通知","点击按钮以开启低电量通知和横幅，并在任务栏通知区域显示电量信息。",notify));
   if(kind=="headset")body.Children.Add(GearPowerVoiceOptions(state));else body.Children.Add(GearPowerRow("当电池电量达到以下百分比时发出灯光警示:","",GearPowerChoice(kind,state,"lowBatteryThreshold")));
   body.Children.Add(GearPowerRow("当空闲以下时间后切换为睡眠模式:","",GearPowerChoice(kind,state,"sleepMinutes")));
   if(kind=="keyboard"){var controls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};controls.Children.Add(MousePerformanceSwitch(null,false));var expand=Label("⌄",22,muted);expand.Margin=new Thickness(30,0,0,0);controls.Children.Add(expand);body.Children.Add(GearPowerRow("省电模式","",controls));}
  }
  UIElement GearBatteryIcon(int? percent) {
   var shell=new Grid{Width=44,Height=25};shell.ColumnDefinitions.Add(new ColumnDefinition());shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(3)});var cells=new Grid{Margin=new Thickness(3)};for(int i=0;i<5;i++){cells.ColumnDefinitions.Add(new ColumnDefinition());var cell=new Border{Background=percent.HasValue&&percent.Value>i*20?text:Brushes.Transparent,BorderBrush=muted,BorderThickness=percent.HasValue?new Thickness(0):new Thickness(0.5),Margin=new Thickness(1,0,1,0)};Grid.SetColumn(cell,i);cells.Children.Add(cell);}shell.Children.Add(new Border{BorderBrush=text,BorderThickness=new Thickness(1),Child=cells});var contact=new Border{Height=9,Background=text,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(contact,1);shell.Children.Add(contact);return shell;
  }
  void OriginalKeyboardPollingPage(StackPanel target,Dictionary<string,object> state) {
   var body=new StackPanel{Margin=new Thickness(31,24,42,0)};target.Children.Add(body);body.Children.Add(GearPowerRow("轮询率","将两个设备配对至一颗接收器上并且同时处于连接状态时，轮询率的上限为 1000 Hz。",GearPowerChoice("keyboard",state,"pollingRate")));
  }
 }
}
