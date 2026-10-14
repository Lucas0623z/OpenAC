using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public partial class MainWindow {
  string headsetSettingsTab="音量";
  bool headsetNoiseExpanded;

  // Independently authored WPF layout and line artwork. The reference screenshots
  // supply positions/ordinary labels, never device values or proprietary artwork.
  void OriginalHeadsetAudioPage(StackPanel target,Dictionary<string,object> state) {
   var surface=HeadsetSurface(target,620);
   var heading=new StackPanel{Orientation=Orientation.Horizontal};
   heading.Children.Add(new Path{Data=Geometry.Parse("M 0,1 L 8,14 M 10,1 L 18,14 M 20,1 L 28,14"),Width=30,Height=15,Stroke=Brush("#D94357"),StrokeThickness=3,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center});
   var title=HeadsetText("音效优化处理",18,text);title.Margin=new Thickness(26,0,0,0);heading.Children.Add(title);HeadsetPlace(surface,heading,35,30);
   HeadsetPlace(surface,HeadsetLineArtwork(),26,136);
   var preset=HeadsetCombo(new[]{"—"},0,false);preset.Width=596;HeadsetPlace(surface,preset,391,70);
   var reset=HeadsetButton("重置",()=>{});reset.Width=134;reset.IsEnabled=false;HeadsetPlace(surface,reset,1006,70);
   var subtabs=new StackPanel{Orientation=Orientation.Horizontal};
   foreach(string name in new[]{"均衡器","优化","音效增强"}) {
    string chosen=name;var tab=HeadsetButton(name,()=>{headsetAudioTab=chosen;ShowPage(page);});tab.Width=137;tab.Height=34;tab.BorderThickness=new Thickness(0,0,0,name==headsetAudioTab?1:0);tab.BorderBrush=text;tab.Foreground=name==headsetAudioTab?text:muted;subtabs.Children.Add(tab);
   }
   HeadsetPlace(surface,subtabs,391,133);
   if(headsetAudioTab=="均衡器")HeadsetEqualizer(surface,state);
   else if(headsetAudioTab=="优化") {
    HeadsetPlace(surface,HeadsetUnknownAudioRow("低音增强","增强低频音效",true),391,193);
    HeadsetPlace(surface,HeadsetUnknownAudioRow("清晰音质","调整语音清晰度",true),391,281);
    HeadsetPlace(surface,HeadsetUnknownAudioRow("压缩器","调整声音动态范围",true),391,369);
   } else {
    var panel=new Canvas{Width=761,Height=295,Background=Brush("#1C1C1C")};HeadsetPlace(panel,HeadsetText("虚拟环绕音效",17,text),41,25);HeadsetPlace(panel,MousePerformanceSwitch(null,false),603,30);HeadsetPlace(panel,HeadsetText("⌃",24,muted),699,20);
    var demo=new Canvas{Width=686,Height=202,Background=Brush("#101010")};
    demo.Children.Add(new Ellipse{Width=106,Height=106,Stroke=Brush("#8F8F8F"),StrokeThickness=1,Margin=new Thickness(289,48,0,0)});
    HeadsetPlace(demo,new Path{Data=Geometry.Parse("M 0,0 L 0,25 L 21,12.5 Z"),Fill=Brush("#858585"),Width=21,Height=25},334,88);
    HeadsetPlace(panel,demo,41,76);HeadsetPlace(surface,panel,391,193);
    var reverb=HeadsetUnknownAudioRow("混响","",false);HeadsetPlace(surface,reverb,391,501);
   }
  }

  void HeadsetEqualizer(Canvas surface,Dictionary<string,object> state) {
   var snapshot=GearOnline(state)?GearMap(state,"snapshot"):null;
   var frame=new Canvas{Width=761,Height=357,Background=Brush("#1C1C1C")};frame.Children.Add(new Rectangle{Width=761,Height=357,Stroke=muted,StrokeThickness=0.7,RadiusX=3,RadiusY=3});HeadsetPlace(surface,frame,391,193);
   var preset=HeadsetCombo(new[]{"—"},0,false);preset.Width=582;HeadsetPlace(frame,preset,52,33);
   bool? enabled=OriginalStateFlag(snapshot,"eqEnabled");var toggle=MousePerformanceSwitch(enabled,HeadsetEditable(state,"eqEnabled",enabled.HasValue));toggle.Tag="headset:eqEnabled";HeadsetPlace(frame,toggle,658,38);
   var enableApply=HeadsetApply(state,"eqEnabled",()=>toggle.IsChecked,()=>toggle.IsChecked.HasValue);enableApply.Height=28;HeadsetPlace(frame,enableApply,650,79);
   HeadsetPlace(frame,HeadsetText("低",12,muted),98,99);HeadsetPlace(frame,HeadsetText("中",12,muted),285,99);HeadsetPlace(frame,HeadsetText("高",12,muted),567,99);
   string[] bands={"125","250","500","1K","2K","4K","8K","16K"};var gains=GearArray(snapshot,"eqLevels");var sliders=new List<Slider>();bool allKnown=gains!=null&&gains.Length==8;
   for(int index=0;index<8;index++) {
    int? gain=HeadsetInt(gains!=null&&index<gains.Length?gains[index]:null,0,100);allKnown=allKnown&&gain.HasValue;
    var slider=HeadsetSlider(gain,0,100,34,170,true,HeadsetEditable(state,"eqLevels",gain.HasValue));slider.Tag="headset:eqGain:"+index;sliders.Add(slider);HeadsetPlace(frame,slider,47+index*90.3,131);
    var value=HeadsetText(gain.HasValue?gain.Value.ToString(CultureInfo.InvariantCulture):"—",11,muted);value.Width=46;value.TextAlignment=TextAlignment.Center;HeadsetPlace(frame,value,41+index*90.3,114);slider.ValueChanged+=(sender,args)=>{value.Text=((int)slider.Value).ToString(CultureInfo.InvariantCulture);};
    var band=HeadsetText(bands[index],12,text);band.Width=46;band.TextAlignment=TextAlignment.Center;HeadsetPlace(frame,band,41+index*90.3,320);
   }
   bool validSnapshot=allKnown;var apply=HeadsetApply(state,"eqLevels",()=>sliders.Select(slider=>(int)slider.Value).ToArray(),()=>validSnapshot);apply.Width=100;HeadsetPlace(surface,apply,1052,565);
  }

  void OriginalHeadsetMicrophonePage(StackPanel target,Dictionary<string,object> state) {
   var surface=HeadsetSurface(target,690);var reset=HeadsetButton("重置",()=>{});reset.Width=134;reset.IsEnabled=false;HeadsetPlace(surface,reset,1075,24);
   HeadsetPlace(surface,HeadsetFullUnknownRow("噪音抑制","过滤背景噪音",true),33,72);
   HeadsetPlace(surface,HeadsetFullUnknownRow("优质音频","增强语音清晰度",true),33,162);
   var snapshot=GearOnline(state)?GearMap(state,"snapshot"):null;bool? enabled=OriginalStateFlag(snapshot,"noiseReductionEnabled");
   var panel=new Canvas{Width=1176,Height=headsetNoiseExpanded?172:76,Background=Brush("#1C1C1C")};
   HeadsetPlace(panel,HeadsetText("回声消除 / 降噪",17,text),40,25);var toggle=MousePerformanceSwitch(enabled,HeadsetEditable(state,"noiseReductionEnabled",enabled.HasValue));toggle.Tag="headset:noiseReductionEnabled";HeadsetPlace(panel,toggle,1021,24);HeadsetPlace(panel,HeadsetApply(state,"noiseReductionEnabled",()=>toggle.IsChecked,()=>toggle.IsChecked.HasValue),908,22);
   var expand=HeadsetButton(headsetNoiseExpanded?"⌃":"⌄",()=>{headsetNoiseExpanded=!headsetNoiseExpanded;ShowPage(page);});expand.Width=32;expand.Height=32;expand.BorderThickness=new Thickness(0);expand.FontSize=24;expand.Tag="headset:noiseExpand";HeadsetPlace(panel,expand,1111,20);
   if(headsetNoiseExpanded) {
    var strength=new Canvas{Width=1104,Height=80,Background=Brush("#101010")};HeadsetPlace(strength,HeadsetText("降噪强度",15,text),24,27);HeadsetPlace(strength,HeadsetSettingCombo(state,"noiseReductionRaw",new[]{"低","中","高"},new[]{0,1,2},527),552,22);HeadsetPlace(panel,strength,40,77);
   }
   HeadsetPlace(surface,panel,33,252);
   int lower=headsetNoiseExpanded?454:358;HeadsetPlace(surface,HeadsetWindowsVolume("capture","麦克风音量（Windows）",true),33,lower);
  }

  void OriginalHeadsetSettingsPage(StackPanel target,Dictionary<string,object> state) {
   var surface=HeadsetSurface(target,headsetSettingsTab=="音量"?690:headsetSettingsTab=="高级功能"?460:680);var tabs=new StackPanel{Orientation=Orientation.Horizontal};
   foreach(string name in new[]{"音量","高级功能","快捷键说明"}) {
    string selected=name;var tab=HeadsetButton(name,()=>{headsetSettingsTab=selected;ShowPage(page);});tab.Width=170;tab.Height=26;tab.BorderThickness=new Thickness(0);tab.Background=name==headsetSettingsTab?text:Brushes.Transparent;tab.Foreground=name==headsetSettingsTab?Brush("#151515"):text;tabs.Children.Add(tab);
   }
   HeadsetPlace(surface,tabs,35,32);HeadsetPlace(surface,new Rectangle{Width=510,Height=1,Fill=text},35,65);
   if(headsetSettingsTab=="音量") {
    var playback=HeadsetWindowsVolume("render","音量",false);double extra=playback.Height-76;
    HeadsetPlace(surface,HeadsetVolumeHeading("播放","render"),33,99);HeadsetPlace(surface,playback,33,133);
    HeadsetPlace(surface,HeadsetFullUnknownRow("声道音量控制面板","",false),33,221+extra);
    HeadsetPlace(surface,HeadsetFullUnknownRow("声道音量","",false),33,310+extra);
    HeadsetPlace(surface,HeadsetFullUnknownRow("样式","",false),33,398+extra);
    HeadsetPlace(surface,HeadsetVolumeHeading("录音","capture"),33,547+extra);HeadsetPlace(surface,HeadsetWindowsVolume("capture","音量",false),33,580+extra);
   } else if(headsetSettingsTab=="高级功能") {
    var language=new Canvas{Width=1176,Height=76,Background=Brush("#1C1C1C")};HeadsetPlace(language,HeadsetText("语音提示语言",17,text),40,25);HeadsetPlace(language,HeadsetSettingCombo(state,"voicePrompt",new[]{"提示音","英文","中文"},new[]{0,1,2},200),936,22);HeadsetPlace(surface,language,33,89);
    HeadsetPlace(surface,HeadsetFullUnknownRow("连接优先顺序（仅支持 RF 2.4GHz 模式）","",false),33,177);
    var snapshot=GearOnline(state)?GearMap(state,"snapshot"):null;bool? enabled=OriginalStateFlag(snapshot,"sideToneEnabled");int? level=HeadsetInt(HeadsetValue(snapshot,"sideToneLevel"),0,20);
    var monitor=new Canvas{Width=1176,Height=76,Background=Brush("#1C1C1C")};HeadsetPlace(monitor,HeadsetText("监听",17,text),40,25);var slider=HeadsetSlider(level,0,20,200,24,false,HeadsetEditable(state,"sideToneLevel",level.HasValue));slider.Tag="headset:sideToneLevel";HeadsetPlace(monitor,slider,866,26);var toggle=MousePerformanceSwitch(enabled,HeadsetEditable(state,"sideToneEnabled",enabled.HasValue));toggle.Tag="headset:sideToneEnabled";HeadsetPlace(monitor,toggle,1084,25);
    HeadsetPlace(monitor,HeadsetApply(state,"sideToneLevel",()=>(int)slider.Value,()=>level.HasValue),777,21);HeadsetPlace(monitor,HeadsetApply(state,"sideToneEnabled",()=>toggle.IsChecked,()=>toggle.IsChecked.HasValue),674,21);HeadsetPlace(surface,monitor,33,266);
    var latency=new Canvas{Width=1176,Height=76,Background=Brush("#1C1C1C")};HeadsetPlace(latency,HeadsetText("延迟（毫秒）",17,text),40,25);HeadsetPlace(latency,HeadsetSettingCombo(state,"latencyMs",new[]{"40","60","80"},new[]{40,60,80},200),936,22);HeadsetPlace(surface,latency,33,354);
   } else {
    HeadsetPlace(surface,HeadsetLineArtwork(),352,95);
    var annotation=new StackPanel();annotation.Children.Add(HeadsetText("按键和开关",17,text));foreach(string label in new[]{"音量控制","电源","连接","麦克风"}){var entry=HeadsetText(label,14,muted);entry.Margin=new Thickness(0,20,0,0);annotation.Children.Add(entry);}HeadsetPlace(surface,annotation,72,412);
    var indicators=new StackPanel();indicators.Children.Add(HeadsetText("LED 指示灯",17,text));var note=HeadsetText("连接状态",14,muted);note.Margin=new Thickness(0,20,0,0);indicators.Children.Add(note);HeadsetPlace(surface,indicators,674,412);
   }
  }

  Canvas HeadsetSurface(StackPanel target,double height) {
   var surface=new Canvas{Width=1246,Height=height,Tag="headset:surface"};target.Children.Add(new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left});return surface;
  }
  void HeadsetPlace(Canvas canvas,UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);canvas.Children.Add(element);}
  TextBlock HeadsetText(string caption,double size,Brush color){var label=Label(caption,size,color);label.Margin=new Thickness(0);return label;}
  Button HeadsetButton(string caption,Action action){var button=KeyboardOutlineButton(caption,action);button.Height=34;button.FontSize=14;return button;}
  static object HeadsetValue(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  static int? HeadsetInt(object value,int min,int max){if(value==null||value is bool)return null;double number;if(!Double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out number)||Double.IsNaN(number)||Double.IsInfinity(number)||number!=Math.Truncate(number)||number<min||number>max)return null;return (int)number;}
  bool HeadsetEditable(Dictionary<string,object> state,string setting,bool observed){return observed&&GearCanSet(state,setting)&&!busy&&!closing;}
  Button HeadsetApply(Dictionary<string,object> state,string setting,Func<object> value,Func<bool> valid) {
   var button=HeadsetButton("应用",async()=>{if(render||busy||closing||!valid()||!CanApplyGear(state,setting))return;await SetGearSetting("headset",state,setting,value());});button.Width=76;button.Height=34;button.Tag="headset:apply:"+setting;button.IsEnabled=valid()&&CanApplyGear(state,setting);return button;
  }
  UIElement HeadsetSettingCombo(Dictionary<string,object> state,string setting,string[] labels,int[] values,double width) {
   var snapshot=GearOnline(state)?GearMap(state,"snapshot"):null;int? current=HeadsetInt(HeadsetValue(snapshot,setting),Int32.MinValue,Int32.MaxValue);int index=current.HasValue?Array.IndexOf(values,current.Value):-1;
   var box=HeadsetCombo(new[]{"—"}.Concat(labels).ToArray(),index+1,HeadsetEditable(state,setting,index>=0));box.Width=width;box.Tag="headset:"+setting;
   var row=new Canvas{Width=width,Height=34};HeadsetPlace(row,box,0,0);var apply=HeadsetApply(state,setting,()=>values[box.SelectedIndex-1],()=>index>=0&&box.SelectedIndex>0&&box.SelectedIndex<=values.Length);HeadsetPlace(row,apply,-96,0);return row;
  }
  ComboBox HeadsetCombo(string[] items,int selected,bool enabled) {
   var combo=MousePerformanceCombo(items,Math.Max(0,Math.Min(items.Length-1,selected)),enabled,200);combo.Width=Double.NaN;combo.Height=34;combo.Margin=new Thickness(0);return combo;
  }
  Slider HeadsetSlider(int? value,int min,int max,double width,double height,bool vertical,bool enabled) {
   var slider=new Slider{Minimum=min,Maximum=max,Value=value??min,Width=width,Height=height,Orientation=vertical?Orientation.Vertical:Orientation.Horizontal,TickFrequency=1,IsSnapToTickEnabled=true,IsEnabled=enabled&&value.HasValue,ToolTip=value.HasValue?value.Value.ToString(CultureInfo.InvariantCulture):"—",Tag=value.HasValue};
   if(!value.HasValue){var template=new ControlTemplate(typeof(Slider));var line=new FrameworkElementFactory(typeof(Border));line.SetValue(Border.BackgroundProperty,Brush("#666666"));line.SetValue(vertical?Border.WidthProperty:Border.HeightProperty,2.0);line.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);line.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);if(vertical)line.SetValue(Border.HeightProperty,height);else line.SetValue(Border.WidthProperty,width);template.VisualTree=line;slider.Template=template;}
   else if(!vertical)slider.Template=MouseSliderTemplate();
   else slider.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Slider}'><Track x:Name='PART_Track' Orientation='Vertical' Width='34'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='#AAAAAA' Width='2' HorizontalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='22' Height='7'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='#D7BF6C'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='#666666' Width='2' HorizontalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></ControlTemplate>");return slider;
  }
  Canvas HeadsetUnknownAudioRow(string title,string description,bool slider) {
   var row=new Canvas{Width=761,Height=76,Background=Brush("#1C1C1C")};HeadsetPlace(row,HeadsetText(title,17,text),41,description.Length==0?25:17);if(description.Length>0)HeadsetPlace(row,HeadsetText(description,12,muted),41,42);if(slider)HeadsetPlace(row,HeadsetSlider(null,0,100,200,24,false,false),444,26);HeadsetPlace(row,MousePerformanceSwitch(null,false),slider?667:603,25);if(!slider)HeadsetPlace(row,HeadsetText("⌄",24,muted),699,22);return row;
  }
  Canvas HeadsetFullUnknownRow(string title,string description,bool slider) {
   var row=new Canvas{Width=1176,Height=76,Background=Brush("#1C1C1C")};HeadsetPlace(row,HeadsetText(title,17,text),40,description.Length==0?25:17);if(description.Length>0)HeadsetPlace(row,HeadsetText(description,12,muted),40,42);if(slider){HeadsetPlace(row,HeadsetSlider(null,0,100,200,24,false,false),803,26);HeadsetPlace(row,MousePerformanceSwitch(null,false),1021,25);}else HeadsetPlace(row,HeadsetText("⌄",24,muted),1116,21);return row;
  }

  Canvas HeadsetLineArtwork() {
   // Generic headset schematic built from simple paths, not traced vendor vectors.
   var canvas=new Canvas{Width=280,Height=400,Tag="headset:ownArtwork"};
   string[] paths={"M 43,215 C 15,180 24,90 48,58 C 87,6 186,4 225,61 C 251,99 253,175 239,211","M 52,202 C 30,157 47,88 70,62 C 109,28 170,28 209,65 C 231,88 239,153 229,201","M 43,203 L 89,218 L 78,314 L 50,346 L 25,327 L 24,234 Z","M 231,202 L 191,218 L 201,312 L 229,347 L 256,327 L 254,234 Z","M 48,226 L 73,235 L 64,307 L 48,326 L 38,315 L 37,244 Z","M 229,226 L 209,237 L 218,307 L 232,326 L 244,315 L 243,243 Z","M 193,306 C 168,360 107,374 69,350 L 59,359 C 102,393 181,375 209,324","M 50,347 L 45,363 L 57,371 L 72,352"};
   foreach(string path in paths)canvas.Children.Add(new Path{Data=Geometry.Parse(path),Stroke=Brush("#939393"),StrokeThickness=1.2});return canvas;
  }

  Canvas HeadsetVolumeHeading(string title,string flow) {
   var heading=new Canvas{Width=1176,Height=30};HeadsetPlace(heading,HeadsetText(title,17,text),0,0);
   var read=HeadsetButton("读取音量",async()=>await ReadOriginalWindowsAudio());read.Width=100;read.Height=26;read.IsEnabled=!busy&&!closing&&!render;read.Tag="headset:readWindows:"+flow;HeadsetPlace(heading,read,1076,-2);return heading;
  }
  Canvas HeadsetWindowsVolume(string flow,string title,bool withRead) {
   var candidates=audioEndpoints.Where(endpoint=>OriginalStateText(endpoint,"flow","")==flow&&OriginalStateFlag(endpoint,"matchConfirmed")==true).ToList();string selected;audioEndpointSelection.TryGetValue(flow,out selected);
   var current=candidates.FirstOrDefault(endpoint=>OriginalStateText(endpoint,"endpointId","")==selected);if(current==null&&candidates.Count==1){current=candidates[0];selected=OriginalStateText(current,"endpointId","");audioEndpointSelection[flow]=selected;}
   int? volume=current==null?null:HeadsetVolumeValue(HeadsetValue(current,"volumePercent"));bool ready=current!=null&&!String.IsNullOrEmpty(selected)&&OriginalStateFlag(current,"active")==true&&OriginalStateFlag(current,"readbackConfirmed")==true&&volume.HasValue;bool? mutedValue=ready?OriginalStateFlag(current,"muted"):null;
   var row=new Canvas{Width=1176,Height=candidates.Count>1?120:76,Background=Brush("#1C1C1C")};HeadsetPlace(row,HeadsetText(title,17,text),40,17);HeadsetPlace(row,HeadsetText(ready?OriginalStateText(current,"name",""):"—",12,muted),40,42);
   var slider=HeadsetSlider(ready?volume:null,0,100,238,24,false,ready&&!busy&&!closing);slider.Tag="headset:windowsVolume:"+flow;HeadsetPlace(row,slider,897,26);
   var mute=MousePerformanceSwitch(mutedValue,ready&&mutedValue.HasValue&&!busy&&!closing);mute.Tag="headset:windowsMute:"+flow;HeadsetPlace(row,mute,790,25);string exactId=selected;
   var apply=HeadsetButton("应用",async()=>{if(render||busy||closing||!HardwareWritesEnabled||!ready)return;await SetOriginalWindowsAudioFromRead(current,flow,exactId,"volume",(int)slider.Value);});apply.Width=76;apply.Height=34;apply.IsEnabled=HardwareWritesEnabled&&ready&&!busy;apply.Tag="headset:applyWindowsVolume:"+flow;HeadsetPlace(row,apply,681,21);
   var muteApply=HeadsetButton("静音应用",async()=>{if(render||busy||closing||!HardwareWritesEnabled||!ready||!mute.IsChecked.HasValue)return;await SetOriginalWindowsAudioFromRead(current,flow,exactId,"mute",mute.IsChecked.Value);});muteApply.Width=90;muteApply.Height=34;muteApply.IsEnabled=HardwareWritesEnabled&&ready&&mutedValue.HasValue&&!busy;muteApply.Tag="headset:applyWindowsMute:"+flow;HeadsetPlace(row,muteApply,569,21);
   if(withRead){var read=HeadsetButton("读取音量",async()=>await ReadOriginalWindowsAudio());read.Width=100;read.IsEnabled=!busy&&!closing&&!render;HeadsetPlace(row,read,447,21);}
   if(candidates.Count>1){var choice=MousePerformanceCombo(new string[0],-1,!busy&&!closing,560);choice.Height=32;choice.Items.Add(new ComboBoxItem{Content="选择耳机音频端点",Tag=""});foreach(var endpoint in candidates){string id=OriginalStateText(endpoint,"endpointId","");var item=new ComboBoxItem{Content=OriginalStateText(endpoint,"name","")+" · "+(id.Length>8?id.Substring(id.Length-8):id),Tag=id};choice.Items.Add(item);if(id==selected)choice.SelectedItem=item;}if(choice.SelectedIndex<0)choice.SelectedIndex=0;choice.SelectionChanged+=(sender,args)=>{var item=choice.SelectedItem as ComboBoxItem;audioEndpointSelection[flow]=item==null?"":Convert.ToString(item.Tag);ShowPage(page);};HeadsetPlace(row,choice,40,82);}return row;
  }
  static int? HeadsetVolumeValue(object value){double number;if(value==null||value is bool||!Double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out number)||Double.IsNaN(number)||Double.IsInfinity(number)||number<0||number>100)return null;return (int)Math.Round(number,MidpointRounding.AwayFromZero);}
 }
}
