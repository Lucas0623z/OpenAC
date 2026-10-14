// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored layout and validation. Manufacturer artwork is not bundled.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public sealed class MouseZoneDraft {
  public string DeviceId, CapturedAt;
  public bool? Enabled;
  public int? Dpi, DpiY, PollingRate, TxPower;
  public bool Dirty;
  public MouseZoneDraft Copy(){return new MouseZoneDraft{DeviceId=DeviceId,CapturedAt=CapturedAt,Enabled=Enabled,Dpi=Dpi,DpiY=DpiY,PollingRate=PollingRate,TxPower=TxPower,Dirty=Dirty};}
 }
 public sealed class MouseZoneObservation {
  public string DeviceId,CapturedAt;
  public bool IdentityConfirmed,Complete,CanSet;
  public bool? Enabled;
  public int? Dpi,DpiY,PollingRate,TxPower,GeneralDpi;
  internal Dictionary<string,object> Source;
 }
 public static class MouseZoneUiFacts {
  public static readonly int[] PollingRates={125,250,500,1000,2000,4000,8000};
  static object Field(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  static int? Number(IDictionary<string,object> map,string key,int min,int max){object v=Field(map,key);return v is int&&(int)v>=min&&(int)v<=max?(int?)v:null;}
  static bool Flag(IDictionary<string,object> map,string key,bool expected){object v=Field(map,key);return v is bool&&(bool)v==expected;}
  static bool Contains(IDictionary<string,object> map,string key,string name){var v=Field(map,key) as IEnumerable;return v!=null&&!(v is string)&&v.Cast<object>().Any(item=>item is string&&(string)item==name);}
  public static bool Timestamp(string text){DateTime value;return text!=null&&DateTime.TryParseExact(text,"HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out value);}
  public static bool Identity(string id,int pid){return (pid==7546||pid==7548)&&KeyboardLightingUiFacts.Identity(id,pid);}
  public static MouseZoneObservation Read(Dictionary<string,object> state){
   var result=new MouseZoneObservation{Source=state,DeviceId=Field(state,"deviceId") as string,CapturedAt=Field(state,"capturedAt") as string};
   var snapshot=Field(state,"snapshot") as IDictionary<string,object>;int? pid=Number(state,"pid",0,65535);
   result.IdentityConfirmed=pid.HasValue&&Identity(result.DeviceId,pid.Value)&&Field(state,"kind") as string=="mouse"&&Flag(state,"connected",true)&&Flag(state,"online",true)&&Flag(state,"readbackConfirmed",true)&&Number(snapshot,"modelPid",0,65535)==pid&&Timestamp(result.CapturedAt);
   if(!result.IdentityConfirmed||!Contains(state,"readCapabilities","zoneSettings"))return result;
   var zone=Field(snapshot,"zoneSettings") as IDictionary<string,object>;object enabled=Field(zone,"enabled");result.Enabled=enabled is bool?(bool?)enabled:null;
   result.Dpi=Number(zone,"dpi",100,65000);result.DpiY=Number(zone,"dpiY",100,65000);result.PollingRate=Number(zone,"pollingRate",125,8000);result.TxPower=Number(zone,"txPower",0,1);
   result.Complete=result.Enabled.HasValue&&result.Dpi.HasValue&&result.DpiY.HasValue&&result.PollingRate.HasValue&&PollingRates.Contains(result.PollingRate.Value)&&result.TxPower.HasValue;
   result.CanSet=result.Complete&&Contains(state,"setCapabilities","zoneSettings");
   if(!result.Complete){result.Enabled=null;result.Dpi=null;result.DpiY=null;result.PollingRate=null;result.TxPower=null;return result;}
   result.GeneralDpi=Number(snapshot,"currentDpi",100,65000);
   if(!result.GeneralDpi.HasValue&&Contains(state,"readCapabilities","splitDpi")&&Contains(state,"readCapabilities","activeDpiStage")){
    int? stage=Number(snapshot,"activeDpiStage",1,4);var values=Field(snapshot,"dpiStages") as IEnumerable;
    if(stage.HasValue&&values!=null&&!(values is string)){var array=values.Cast<object>().ToArray();if(array.Length==4&&array.All(v=>v is int&&(int)v>=100&&(int)v<=65000))result.GeneralDpi=(int)array[stage.Value-1];}
   }
   return result;
  }
  public static bool ValidDraft(MouseZoneDraft draft,bool requireComplete){
   if(draft==null||draft.DeviceId==null||draft.DeviceId.Length>64||draft.DeviceId.Length>0&&!Identity(draft.DeviceId,7546)&&!Identity(draft.DeviceId,7548)||draft.CapturedAt!=null&&!Timestamp(draft.CapturedAt))return false;
   if(draft.Dpi.HasValue&&(draft.Dpi<100||draft.Dpi>65000)||draft.DpiY.HasValue&&(draft.DpiY<100||draft.DpiY>65000)||draft.PollingRate.HasValue&&!PollingRates.Contains(draft.PollingRate.Value)||draft.TxPower.HasValue&&(draft.TxPower<0||draft.TxPower>1))return false;
   return !requireComplete||draft.DeviceId.Length>0&&Timestamp(draft.CapturedAt)&&draft.Enabled.HasValue&&draft.Dpi.HasValue&&draft.DpiY.HasValue&&draft.PollingRate.HasValue&&draft.TxPower.HasValue;
  }
  public static bool ValuesMatch(MouseZoneDraft draft,MouseZoneObservation observation){return draft!=null&&observation!=null&&observation.Complete&&draft.Enabled==observation.Enabled&&draft.Dpi==observation.Dpi&&draft.DpiY==observation.DpiY&&draft.PollingRate==observation.PollingRate&&draft.TxPower==observation.TxPower;}
  public static bool ObservationMatches(IDictionary<string,Dictionary<string,object>> states,Dictionary<string,object> state,MouseZoneObservation observation){
   Dictionary<string,object> active;if(states==null||state==null||observation==null||!states.TryGetValue("mouse",out active)||!Object.ReferenceEquals(active,state)||!Object.ReferenceEquals(observation.Source,state))return false;
   var current=Read(state);return observation.CanSet&&current.CanSet&&observation.DeviceId==current.DeviceId&&observation.CapturedAt==current.CapturedAt&&observation.Enabled==current.Enabled&&observation.Dpi==current.Dpi&&observation.DpiY==current.DpiY&&observation.PollingRate==current.PollingRate&&observation.TxPower==current.TxPower;
  }
  public static bool MayApply(IDictionary<string,Dictionary<string,object>> states,Dictionary<string,object> state,MouseZoneObservation observation,MouseZoneDraft draft,bool permission,bool busy){return permission&&!busy&&ObservationMatches(states,state,observation)&&ValidDraft(draft,true)&&draft.Dirty&&draft.DeviceId==observation.DeviceId&&draft.CapturedAt==observation.CapturedAt&&!ValuesMatch(draft,observation);}
  public static Dictionary<string,object> Command(MouseZoneDraft draft){if(!ValidDraft(draft,true))throw new ArgumentException("Zone 草稿必须包含完整有效的开关、X/Y DPI、轮询率及无线传输功率。");return new Dictionary<string,object>{{"enabled",draft.Enabled.Value},{"pollingRate",draft.PollingRate.Value},{"dpi",draft.Dpi.Value},{"dpiY",draft.DpiY.Value},{"txPower",draft.TxPower.Value}};}
 }
 public partial class MainWindow {
  MouseZoneDraft mouseZoneDraft;
  MouseZoneObservation mouseZoneObservation;
  Dictionary<string,object> mouseZoneExcludedReference;
  object mouseZoneWorkerSession;
  bool mouseCalibrationAutoExpanded,mouseCalibrationCustomExpanded;
  bool mouseZoneExpanded=true,mouseZoneYExpanded,mouseZoneApplying;
  void InvalidateMouseZoneObservation(){Dictionary<string,object> current;mouseZoneExcludedReference=gearStates.TryGetValue("mouse",out current)?current:null;mouseZoneObservation=null;mouseZoneWorkerSession=null;}
  void RestoreMouseZoneDraft(MouseZoneDraft draft){mouseZoneDraft=null;InvalidateMouseZoneObservation();if(MouseZoneUiFacts.ValidDraft(draft,draft!=null&&draft.Dirty))mouseZoneDraft=draft.Copy();}
  MouseZoneDraft MouseZoneValues(Dictionary<string,object> state){
   var observed=MouseZoneUiFacts.Read(state);string id=observed.DeviceId??"";
   if(mouseZoneDraft==null||mouseZoneDraft.DeviceId!=id){mouseZoneDraft=new MouseZoneDraft{DeviceId=id};mouseZoneObservation=null;mouseZoneWorkerSession=null;}
   if(observed.Complete&&!Object.ReferenceEquals(state,mouseZoneExcludedReference)){
    if(!Object.ReferenceEquals(mouseZoneObservation==null?null:mouseZoneObservation.Source,state)){mouseZoneObservation=observed;mouseZoneWorkerSession=gearWorker.SessionIdentity;}
    mouseZoneDraft.CapturedAt=observed.CapturedAt;
    if(!mouseZoneDraft.Dirty){mouseZoneDraft.Enabled=observed.Enabled;mouseZoneDraft.Dpi=observed.Dpi;mouseZoneDraft.DpiY=observed.DpiY;mouseZoneDraft.PollingRate=observed.PollingRate;mouseZoneDraft.TxPower=observed.TxPower;}
   }else if(!mouseZoneDraft.Dirty){mouseZoneDraft.Enabled=null;mouseZoneDraft.Dpi=null;mouseZoneDraft.DpiY=null;mouseZoneDraft.PollingRate=null;mouseZoneDraft.TxPower=null;mouseZoneDraft.CapturedAt=null;}
   return mouseZoneDraft;
  }
  bool MouseZoneSessionCurrent(Dictionary<string,object> state){return mouseZoneWorkerSession!=null&&Object.ReferenceEquals(mouseZoneWorkerSession,gearWorker.SessionIdentity)&&MouseZoneUiFacts.ObservationMatches(gearStates,state,mouseZoneObservation);}
  bool MouseZoneHasPendingDraft(Dictionary<string,object> state){return MouseZoneSessionCurrent(state)&&MouseZoneUiFacts.ValidDraft(mouseZoneDraft,false)&&mouseZoneDraft.Dirty&&mouseZoneDraft.DeviceId==mouseZoneObservation.DeviceId&&mouseZoneDraft.CapturedAt==mouseZoneObservation.CapturedAt;}
  bool MouseZoneCanEdit(Dictionary<string,object> state){return !render&&!closing&&!busy&&!mouseZoneApplying&&HardwareWritesEnabled&&MouseZoneSessionCurrent(state);}
  bool MouseZoneMayApply(Dictionary<string,object> state,MouseZoneDraft draft){return !render&&!closing&&!mouseZoneApplying&&MouseZoneSessionCurrent(state)&&MouseZoneUiFacts.MayApply(gearStates,state,mouseZoneObservation,draft,HardwareWritesEnabled,busy)&&CanApplyGear(state,"zoneSettings");}
  void MouseZoneEdited(Dictionary<string,object> state,MouseZoneDraft draft){if(!MouseZoneCanEdit(state)||!Object.ReferenceEquals(mouseZoneDraft,draft))return;draft.Dirty=!MouseZoneUiFacts.ValuesMatch(draft,mouseZoneObservation);}
  async Task ApplyMouseZoneDraftAsync(Dictionary<string,object> state,MouseZoneDraft draft){
   if(!MouseZoneMayApply(state,draft))return;var submitted=draft.Copy();Dictionary<string,object> command;try{command=MouseZoneUiFacts.Command(submitted);}catch(ArgumentException e){Log(e.Message);return;}
   mouseZoneApplying=true;try{ShowPage(page);await SetGearSetting("mouse",state,"zoneSettings",command);Dictionary<string,object> after;if(gearStates.TryGetValue("mouse",out after)&&!Object.ReferenceEquals(state,after)&&OriginalStateFlag(after,"writeConfirmed")==true&&MouseZoneUiFacts.ValuesMatch(submitted,MouseZoneUiFacts.Read(after))&&Object.ReferenceEquals(mouseZoneDraft,draft))draft.Dirty=false;}
   finally{mouseZoneApplying=false;if(!closing)ShowPage(page);}
  }
  void OriginalMouseCalibrationPage(StackPanel target,Dictionary<string,object> state){
   var fullWidth=new StackPanel{Margin=new Thickness(-22,0,-22,0)};target.Children.Add(fullWidth);var body=new StackPanel{Margin=new Thickness(56,24,56,0),Tag="MouseCalibration.Body"};fullWidth.Children.Add(body);
   var automatic=new StackPanel();automatic.Children.Add(MouseCalibrationHeading("自动校准",mouseCalibrationAutoExpanded,()=>{if(busy||closing)return;mouseCalibrationAutoExpanded=!mouseCalibrationAutoExpanded;ShowPage(page);}));
   if(mouseCalibrationAutoExpanded)automatic.Children.Add(MouseCalibrationSurfaces());body.Children.Add(MouseCalibrationPanel(automatic));
   var custom=new StackPanel();custom.Children.Add(MouseCalibrationHeading("自定义",mouseCalibrationCustomExpanded,()=>{if(busy||closing)return;mouseCalibrationCustomExpanded=!mouseCalibrationCustomExpanded;ShowPage(page);}));
   if(mouseCalibrationCustomExpanded)custom.Children.Add(MouseCalibrationCustom());body.Children.Add(MouseCalibrationPanel(custom));
   var glass=new Grid{Height=78,Margin=new Thickness(40,0,40,0)};glass.ColumnDefinitions.Add(new ColumnDefinition());glass.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});var name=Label("玻璃表面侦测  ⓘ",16,text,FontWeights.SemiBold);name.VerticalAlignment=VerticalAlignment.Center;name.Margin=new Thickness(0);glass.Children.Add(name);var choice=MousePerformanceCombo(new[]{"—"},0,false,200);choice.Tag="MouseCalibration.Glass";choice.VerticalAlignment=VerticalAlignment.Center;choice.ToolTip="设备状态未读取；校准接口尚未接通。";Grid.SetColumn(choice,1);glass.Children.Add(choice);body.Children.Add(MouseCalibrationPanel(glass));
  }
  Border MouseCalibrationPanel(UIElement child){return new Border{Background=Brush("#1C1C1C"),BorderBrush=Brush("#595959"),BorderThickness=new Thickness(0,0,0,2),Margin=new Thickness(0,0,0,12),Child=child};}
  Button MouseCalibrationHeading(string title,bool expanded,Action toggle){var button=MouseSectionHeading("",expanded,toggle);button.Height=78;var content=button.Content as Grid;var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(MouseCalibrationRadio());var label=Label(title,16,text,FontWeights.SemiBold);label.Margin=new Thickness(12,0,0,0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);content.Children.RemoveAt(0);content.Children.Insert(0,row);button.Tag="MouseCalibration.Expand."+title;button.ToolTip="仅展开查看；不会切换设备校准方式。";button.IsEnabled=!busy&&!closing;return button;}
  RadioButton MouseCalibrationRadio(){var template=new ControlTemplate(typeof(RadioButton));var ring=new FrameworkElementFactory(typeof(Ellipse));ring.SetValue(Ellipse.StrokeProperty,text);ring.SetValue(Ellipse.StrokeThicknessProperty,1.0);ring.SetValue(Ellipse.FillProperty,Brushes.Transparent);ring.SetValue(FrameworkElement.WidthProperty,19.0);ring.SetValue(FrameworkElement.HeightProperty,19.0);template.VisualTree=ring;return new RadioButton{IsChecked=false,IsEnabled=false,IsHitTestVisible=false,Width=20,Height=20,Template=template,VerticalAlignment=VerticalAlignment.Center,ToolTip="当前校准方式未读取"};}
  UIElement MouseCalibrationSurfaces(){
   var body=new StackPanel{Margin=new Thickness(36,0,36,16),Background=Brush("#101010"),Tag="MouseCalibration.Presets"};var title=Label("●  预设表面  ⓘ",14,text,FontWeights.SemiBold);title.Margin=new Thickness(28,24,0,8);body.Children.Add(title);var caption=Label("鼠标垫",12,muted);caption.Margin=new Thickness(56,0,0,16);body.Children.Add(caption);
   var grid=new UniformGrid{Columns=9,Rows=3,Margin=new Thickness(52,0,52,24)};
   // Slots are original geometry, not vendor thumbnails. No image is selected
   // or fabricated when the installed preset artwork is unavailable.
   for(int i=0;i<24;i++){var slot=new Border{Height=104,MaxWidth=90,Margin=new Thickness(3,0,3,16),Background=Brush("#242424"),BorderBrush=Brush("#414141"),BorderThickness=new Thickness(1),Tag="MouseCalibration.Preset."+i,ToolTip="预设表面 "+(i+1)+"：图像未安装，校准接口尚未接通。",IsEnabled=false};var placeholder=Label("—",16,muted);placeholder.Margin=new Thickness(0);placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;slot.Child=placeholder;grid.Children.Add(slot);}body.Children.Add(grid);return body;
  }
  UIElement MouseCalibrationCustom(){
   var body=new StackPanel{Margin=new Thickness(36,0,36,16),Background=Brush("#101010"),Tag="MouseCalibration.Custom"};var asymmetric=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(34,28,0,12)};asymmetric.Children.Add(MouseCalibrationRadio());var title=Label("非对称中断",16,muted,FontWeights.SemiBold);title.Margin=new Thickness(12,0,0,0);asymmetric.Children.Add(title);body.Children.Add(asymmetric);
   body.Children.Add(MouseCalibrationScale("抬起"));body.Children.Add(MouseCalibrationScale("放下"));
   var manual=new Grid{Margin=new Thickness(34,12,24,12)};manual.ColumnDefinitions.Add(new ColumnDefinition());manual.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(132)});var radio=new StackPanel{Orientation=Orientation.Horizontal};radio.Children.Add(MouseCalibrationRadio());var label=Label("手动校准",16,muted,FontWeights.SemiBold);label.Margin=new Thickness(12,0,0,0);radio.Children.Add(label);manual.Children.Add(radio);var start=Button("开始",()=>{});start.Height=34;start.Width=132;start.Margin=new Thickness(0);start.IsEnabled=false;start.Tag="MouseCalibration.Start";start.ToolTip="校准接口尚未接通。";Grid.SetColumn(start,1);manual.Children.Add(start);body.Children.Add(manual);body.Children.Add(MouseCalibrationScale("校准后拉抬高度"));return body;
  }
  UIElement MouseCalibrationScale(string title){var row=new Grid{Height=54,Margin=new Thickness(66,0,24,0)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(202)});var label=Label(title,14,muted);label.Margin=new Thickness(0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);var scale=new StackPanel();var limits=new Grid();limits.ColumnDefinitions.Add(new ColumnDefinition());limits.ColumnDefinitions.Add(new ColumnDefinition());var min=Label("1",10,muted);min.Margin=new Thickness(0);limits.Children.Add(min);var max=Label("11",10,muted);max.Margin=new Thickness(0);max.TextAlignment=TextAlignment.Right;Grid.SetColumn(max,1);limits.Children.Add(max);scale.Children.Add(limits);var slider=new Slider{Minimum=1,Maximum=11,Value=1,Height=24,IsEnabled=false,Tag=false,Template=MouseSliderTemplate(false),ToolTip="仅原版视觉标尺；当前值未知，尚无已核实的校准协议。"};scale.Children.Add(slider);Grid.SetColumn(scale,1);row.Children.Add(scale);return row;}
  void OriginalMouseZonePage(StackPanel target,Dictionary<string,object> state){
   var draft=MouseZoneValues(state);var observed=MouseZoneUiFacts.Read(state);bool edit=MouseZoneCanEdit(state),enabled=edit&&draft.Enabled==true;
   var fullWidth=new StackPanel{Margin=new Thickness(-22,0,-22,0)};target.Children.Add(fullWidth);var body=new StackPanel{Margin=new Thickness(24,24,24,0),Tag="MouseZone.Body"};fullWidth.Children.Add(body);var panel=new StackPanel();
   var heading=new Grid{Height=78,Margin=new Thickness(28,0,36,0)};heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var head=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var line=new StackPanel{Orientation=Orientation.Horizontal};var check=new CheckBox{IsThreeState=true,IsChecked=observed.Complete?draft.Enabled:null,IsEnabled=edit,Foreground=text,Content="Zone 模式",FontWeight=FontWeights.SemiBold,FontSize=16,Tag="MouseZone.Enabled"};line.Children.Add(check);head.Children.Add(line);var description=Paragraph("启用 Zone 模式可提升鼠标性能，获得更佳的游戏体验。",12,text);description.Margin=new Thickness(30,9,0,0);head.Children.Add(description);heading.Children.Add(head);var expand=Button(mouseZoneExpanded?"⌃":"⌄",()=>{if(busy||closing)return;mouseZoneExpanded=!mouseZoneExpanded;ShowPage(page);});expand.Width=32;expand.Height=40;expand.Margin=new Thickness(0);expand.Padding=new Thickness(0);expand.FontSize=21;expand.Background=Brushes.Transparent;expand.BorderThickness=new Thickness(0);expand.IsEnabled=!busy&&!closing;expand.Tag="MouseZone.Expand";Grid.SetColumn(expand,1);heading.Children.Add(expand);panel.Children.Add(heading);
   check.Template=MouseZoneCheckTemplate();check.IsThreeState=!observed.Complete;
   var apply=Button("应用",async()=>await ApplyMouseZoneDraftAsync(state,draft),true);apply.Width=112;apply.Height=34;apply.Tag="MouseZone.Apply";apply.IsEnabled=MouseZoneMayApply(state,draft);apply.ToolTip="手动应用完整的 Zone 设置；不会修改一般模式 DPI。";
   Action changed=()=>{MouseZoneEdited(state,draft);apply.IsEnabled=MouseZoneMayApply(state,draft);};
   RoutedEventHandler toggled=(s,e)=>{if(!MouseZoneCanEdit(state)||!check.IsChecked.HasValue)return;draft.Enabled=check.IsChecked.Value;changed();ShowPage(page);};check.Checked+=toggled;check.Unchecked+=toggled;
   if(mouseZoneExpanded){
    var details=new StackPanel{Margin=new Thickness(36,0,36,16),Background=Brush("#101010")};var sensitivity=Label("●  灵敏度",16,text,FontWeights.SemiBold);sensitivity.Margin=new Thickness(28,21,0,22);details.Children.Add(sensitivity);
    var dpiTitle=new Grid{Margin=new Thickness(84,8,24,18)};dpiTitle.ColumnDefinitions.Add(new ColumnDefinition());dpiTitle.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var dpiLabel=Label("DPI",16,text,FontWeights.SemiBold);dpiLabel.Margin=new Thickness(0);dpiTitle.Children.Add(dpiLabel);var copyRow=new StackPanel{Orientation=Orientation.Horizontal};var copyText=Label("复制一般模式 DPI",12,muted);copyText.Margin=new Thickness(0,0,12,0);copyText.VerticalAlignment=VerticalAlignment.Center;copyRow.Children.Add(copyText);var copy=Button("复制",()=>{if(!MouseZoneCanEdit(state)||draft.Enabled!=true||!observed.GeneralDpi.HasValue)return;draft.Dpi=observed.GeneralDpi;changed();ShowPage(page);});copy.Width=132;copy.Height=24;copy.Padding=new Thickness(8,0,8,0);copy.Margin=new Thickness(0);copy.IsEnabled=enabled&&observed.GeneralDpi.HasValue;copy.Tag="MouseZone.Copy";copy.ToolTip="复制已读回的一般模式 X 轴 DPI，保留 Zone Y 轴。";copyRow.Children.Add(copy);Grid.SetColumn(copyRow,1);dpiTitle.Children.Add(copyRow);details.Children.Add(dpiTitle);
    var dpiRow=new Grid{Margin=new Thickness(60,0,24,28),MinHeight=94};dpiRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(105)});dpiRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(128)});dpiRow.ColumnDefinitions.Add(new ColumnDefinition());var card=Label(observed.Complete&&draft.Dpi.HasValue?draft.Dpi.Value.ToString(CultureInfo.InvariantCulture):"—",14,text);card.VerticalAlignment=VerticalAlignment.Center;card.Margin=new Thickness(20,0,0,0);dpiRow.Children.Add(new Border{BorderBrush=border,BorderThickness=new Thickness(1,0,0,1),Margin=new Thickness(0,0,20,0),Child=card});var valueBox=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3,24,12,0)};var input=MouseDpiValueBox(valueBox,"DPI",observed.Complete?draft.Dpi:null,enabled);input.Tag="MouseZone.Dpi";Grid.SetColumn(valueBox,1);dpiRow.Children.Add(valueBox);var track=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,20,0,0)};var slider=MouseDpiSlider(track,"X",observed.Complete?draft.Dpi:null,enabled);slider.Tag=observed.Complete&&draft.Dpi.HasValue;slider.Name="MouseZoneDpiSlider";Grid.SetColumn(track,2);dpiRow.Children.Add(track);details.Children.Add(dpiRow);
    var numberGrid=input.Parent as Grid;if(numberGrid!=null&&numberGrid.ColumnDefinitions.Count==2)numberGrid.ColumnDefinitions[1].Width=new GridLength(32);
    bool updating=false;Action<int> editDpi=value=>{if(updating||!MouseZoneCanEdit(state)||draft.Enabled!=true||value<100||value>65000)return;updating=true;draft.Dpi=value;input.Text=value.ToString(CultureInfo.InvariantCulture);card.Text=input.Text;slider.Value=value;updating=false;changed();};slider.ValueChanged+=(s,e)=>editDpi((int)slider.Value);input.LostKeyboardFocus+=(s,e)=>{int value;if(Int32.TryParse(input.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value))editDpi(value);input.Text=draft.Dpi.HasValue?draft.Dpi.Value.ToString(CultureInfo.InvariantCulture):"—";};
    var yToggle=Button((mouseZoneYExpanded?"⌃ ":"⌄ ")+"Y 轴 DPI",()=>{if(busy||closing)return;mouseZoneYExpanded=!mouseZoneYExpanded;ShowPage(page);});yToggle.HorizontalAlignment=HorizontalAlignment.Left;yToggle.Background=Brushes.Transparent;yToggle.BorderThickness=new Thickness(0);yToggle.Margin=new Thickness(24,-14,0,8);yToggle.Padding=new Thickness(0);yToggle.FontSize=12;yToggle.Tag="MouseZone.YExpand";yToggle.IsEnabled=!busy&&!closing;details.Children.Add(yToggle);
    if(mouseZoneYExpanded){var yHost=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(84,0,24,20)};var yLabel=Label("Y",13,muted);yLabel.Margin=new Thickness(0,0,16,0);yLabel.VerticalAlignment=VerticalAlignment.Center;yHost.Children.Add(yLabel);var error=Label("请输入 100–65000，当前草稿无法应用",12,Brush("#E26F6F"));error.Margin=new Thickness(12,0,0,0);error.VerticalAlignment=VerticalAlignment.Center;error.Visibility=Visibility.Collapsed;var yInput=MouseZoneYInput(draft,observed.Complete,()=>MouseZoneCanEdit(state)&&draft.Enabled==true&&Object.ReferenceEquals(mouseZoneDraft,draft),changed,error);yHost.Children.Add(yInput);yHost.Children.Add(error);details.Children.Add(yHost);}
    var rates=MousePerformanceCombo(new[]{"—"}.Concat(MouseZoneUiFacts.PollingRates.Select(v=>v+" Hz")),observed.Complete&&draft.PollingRate.HasValue?Array.IndexOf(MouseZoneUiFacts.PollingRates,draft.PollingRate.Value)+1:0,enabled,200);rates.Tag="MouseZone.PollingRate";rates.SelectionChanged+=(s,e)=>{if(!MouseZoneCanEdit(state)||draft.Enabled!=true||rates.SelectedIndex<1)return;draft.PollingRate=MouseZoneUiFacts.PollingRates[rates.SelectedIndex-1];changed();};details.Children.Add(MouseZoneLargeRow("轮询率",rates));
    var fps=MousePerformanceCombo(new[]{"—"},0,false,200);fps.Tag="MouseZone.Fps";fps.ToolTip="FPS 帧率尚无已核实的读取或设置接口。";details.Children.Add(MouseZoneLargeRow("FPS 帧率  ⓘ",fps));
    var tx=MousePerformanceCombo(new[]{"—","动态侦测","全功率"},observed.Complete&&draft.TxPower.HasValue?(draft.TxPower==1?1:2):0,enabled,200);tx.Tag="MouseZone.TxPower";tx.SelectionChanged+=(s,e)=>{if(!MouseZoneCanEdit(state)||draft.Enabled!=true||tx.SelectedIndex<1)return;draft.TxPower=tx.SelectedIndex==1?1:0;changed();};details.Children.Add(MouseZoneLargeRow("无线传输功率  ⓘ",tx));panel.Children.Add(details);
   }
   var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(36,0,36,16)};footer.Children.Add(apply);panel.Children.Add(footer);body.Children.Add(MouseCalibrationPanel(panel));
  }
  UIElement MouseZoneLargeRow(string title,UIElement editor){var row=new Grid{Height=88,Margin=new Thickness(28,0,24,0)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var label=Label("●  "+title,16,text,FontWeights.SemiBold);label.Margin=new Thickness(0);label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);var element=editor as FrameworkElement;if(element!=null)element.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(editor,1);row.Children.Add(editor);return row;}
  TextBox MouseZoneYInput(MouseZoneDraft draft,bool observed,Func<bool> mayEdit,Action changed,TextBlock error){
   var input=FeatureText(observed&&draft.DpiY.HasValue?draft.DpiY.Value.ToString(CultureInfo.InvariantCulture):"—");input.Width=116;input.Height=28;input.MaxLength=6;input.IsEnabled=mayEdit();input.Tag="MouseZone.DpiY";Brush normalBorder=input.BorderBrush;
   if(observed&&draft.Dirty&&!draft.DpiY.HasValue){input.BorderBrush=Brush("#E26F6F");input.ToolTip="请输入 100–65000 的整数，当前草稿无法应用。";if(error!=null)error.Visibility=Visibility.Visible;}
   // Commit valid text immediately. Focus and preview replacement are not a
   // transaction boundary; incomplete text must never retain a writable old Y.
   input.TextChanged+=(sender,args)=>{if(!input.IsEnabled||!mayEdit())return;int value;bool valid=Int32.TryParse(input.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value)&&value>=100&&value<=65000;draft.DpiY=valid?(int?)value:null;input.BorderBrush=valid?normalBorder:Brush("#E26F6F");input.ToolTip=valid?null:"请输入 100–65000 的整数，当前草稿无法应用。";if(error!=null)error.Visibility=valid?Visibility.Collapsed:Visibility.Visible;changed();};return input;
  }
  ControlTemplate MouseZoneCheckTemplate(){
   var template=new ControlTemplate(typeof(CheckBox));var shell=new FrameworkElementFactory(typeof(Grid));var ring=new FrameworkElementFactory(typeof(Ellipse));ring.SetValue(FrameworkElement.WidthProperty,19.0);ring.SetValue(FrameworkElement.HeightProperty,19.0);ring.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);ring.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);ring.SetValue(Ellipse.StrokeProperty,text);ring.SetValue(Ellipse.StrokeThicknessProperty,1.0);ring.SetValue(Ellipse.FillProperty,Brushes.Transparent);shell.AppendChild(ring);
   var mark=new FrameworkElementFactory(typeof(Ellipse),"ZoneChecked");mark.SetValue(FrameworkElement.WidthProperty,9.0);mark.SetValue(FrameworkElement.HeightProperty,9.0);mark.SetValue(FrameworkElement.MarginProperty,new Thickness(5,0,0,0));mark.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);mark.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);mark.SetValue(Ellipse.FillProperty,accent);mark.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);shell.AppendChild(mark);
   var unknown=new FrameworkElementFactory(typeof(TextBlock),"ZoneUnknown");unknown.SetValue(TextBlock.TextProperty,"—");unknown.SetValue(FrameworkElement.WidthProperty,19.0);unknown.SetValue(TextBlock.TextAlignmentProperty,TextAlignment.Center);unknown.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);unknown.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);unknown.SetValue(TextBlock.ForegroundProperty,muted);unknown.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);shell.AppendChild(unknown);
   var caption=new FrameworkElementFactory(typeof(ContentPresenter));caption.SetValue(FrameworkElement.MarginProperty,new Thickness(30,0,0,0));caption.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);shell.AppendChild(caption);template.VisualTree=shell;var selected=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"ZoneChecked"));template.Triggers.Add(selected);var absent=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=null};absent.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"ZoneUnknown"));template.Triggers.Add(absent);return template;
  }
 }
}
