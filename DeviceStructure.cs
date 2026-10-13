using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  string originalMotherboardTab="5\" LCD Display";
  string originalControllerTab="Fan PWM";
  const string OriginalLcdAssets=@"C:\Program Files (x86)\ASUS\ArmouryDevice\View\E7C8DA76-C9B9-4297-8681-DD878330AFE7\resources\src\pages\lcd\common\images\5\img_or_anim";

  void OriginalMotherboardPage() {
   var title=Label("ROG CROSSHAIR X870E EXTREME",24,text,FontWeights.SemiBold);title.Margin=new Thickness(0,0,0,24);content.Children.Add(title);HardwareControlHint(content);
   var tabs=new Grid();for(int i=0;i<4;i++)tabs.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   string[] names={"5\" LCD Display","可编程接针","磁盘信息","ASUS WiFi Q-Antenna"};
   for(int i=0;i<names.Length;i++) {
    string tab=names[i];var button=Button(tab,()=>{originalMotherboardTab=tab;ShowPage(page);});button.Margin=new Thickness(0,0,28,0);button.Padding=new Thickness(0,0,0,13);button.Background=Brush("#00000000");button.BorderBrush=Brush("#00000000");button.FontSize=14;
    var template=new ControlTemplate(typeof(Button));var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.PaddingProperty,new Thickness(0,0,0,13));frame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,2));frame.SetValue(Border.BorderBrushProperty,originalMotherboardTab==tab?accent:Brush("#00000000"));frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));template.VisualTree=frame;button.Template=template;Grid.SetColumn(button,i);tabs.Children.Add(button);
   }
   content.Children.Add(tabs);content.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,0,0,25)});
   if(originalMotherboardTab=="磁盘信息"){OriginalStoragePage();return;}
   if(originalMotherboardTab=="可编程接针"){OriginalBoardArgbPage(content);return;}
   if(originalMotherboardTab!="5\" LCD Display") {
    content.Children.Add(Paragraph("未接通",13,muted));return;
   }
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60,GridUnitType.Star)});
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   var powerRow=new Grid();powerRow.ColumnDefinitions.Add(new ColumnDefinition());powerRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   bool? power=OriginalStateFlag(snapshot,"powerS0");
   var toggle=new Border{Width=62,Height=29,CornerRadius=new CornerRadius(15),Background=power==true?accent:Brush("#303030"),HorizontalAlignment=HorizontalAlignment.Left,Child=new TextBlock{Text=power.HasValue?(power.Value?"ON":"OFF"):"—",Foreground=power==true?Brush("#151515"):muted,FontSize=12,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
   toggle.ToolTip="点击切换屏幕电源";toggle.IsEnabled=HardwareWritesEnabled&&power.HasValue&&!busy;toggle.Cursor=System.Windows.Input.Cursors.Hand;toggle.MouseLeftButtonUp+=async(s,e)=>{if(power.HasValue&&!busy)await SetOriginalNative("lcd","7299","powerS0",new Dictionary<string,object>{{"value",!power.Value}});};powerRow.Children.Add(toggle);
   var gear=Button("⚙",OpenLcdSettings);gear.IsEnabled=snapshot!=null&&!busy&&!render;gear.FontSize=18;gear.Padding=new Thickness(7,2,7,2);gear.Margin=new Thickness(0);gear.ToolTip="亮度、关机电源与待机设置";Grid.SetColumn(gear,1);powerRow.Children.Add(gear);left.Children.Add(powerRow);
   var display=new Border{Width=208,Height=370,Background=Brush("#090909"),BorderBrush=Brush("#454545"),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(4),Margin=new Thickness(0,22,0,15),HorizontalAlignment=HorizontalAlignment.Center};
   if(lcdSelectedPreset.HasValue&&!lcdMonitorMode)display.Child=OriginalInstalledImage(LcdPresetPath(lcdSelectedPreset.Value),204,366);else{var noPreview=Label(lcdMonitorMode?"硬件监控预览不可用":"未选择预设",12,muted);noPreview.HorizontalAlignment=HorizontalAlignment.Center;noPreview.VerticalAlignment=VerticalAlignment.Center;noPreview.Margin=new Thickness(0);display.Child=noPreview;}left.Children.Add(display);var draftCaption=Label("编辑预览 · 当前屏幕内容未读回",11,muted);draftCaption.TextAlignment=TextAlignment.Center;left.Children.Add(draftCaption);
   string dimensions=OriginalStateText(snapshot,"displayWidth","—")+" × "+OriginalStateText(snapshot,"displayHeight","—");
   var dimensionsLabel=Label(dimensions,11,muted);dimensionsLabel.TextAlignment=TextAlignment.Center;dimensionsLabel.Margin=new Thickness(0,0,0,13);left.Children.Add(dimensionsLabel);
   var read=Button(busy?"正在读取…":"读取屏幕状态",async()=>await ReadOriginalSnapshot("lcd","7299"));read.FontSize=11;read.Padding=new Thickness(10,5,10,5);read.HorizontalAlignment=HorizontalAlignment.Center;read.Margin=new Thickness(0,0,0,13);read.IsEnabled=!busy;left.Children.Add(read);
   var firmware=Label("固件  "+OriginalStateText(snapshot,"firmware","—")+"     亮度  未读回",11,muted);firmware.TextAlignment=TextAlignment.Center;firmware.Margin=new Thickness(0);left.Children.Add(firmware);if(!String.IsNullOrEmpty(nativeSettingsMessage))left.Children.Add(Paragraph(nativeSettingsMessage,11,muted));
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,21)};
   var applyContent=Button("应用",ApplyLcdDraft);applyContent.IsEnabled=lcdMonitorMode?LcdWritable(snapshot,"hardwareMonitorWritable"):lcdSelectedPreset.HasValue&&LcdWritable(snapshot,"presetWritable");var resetDraft=Button("默认",()=>{lcdSelectedPreset=0;lcdMonitorMode=false;ShowPage(page);});resetDraft.ToolTip="选择默认预设，点击应用后显示";foreach(var button in new[]{applyContent,resetDraft}){button.MinWidth=74;button.Padding=new Thickness(12,7,12,7);button.Margin=new Thickness(9,0,0,0);actions.Children.Add(button);}right.Children.Add(actions);
   var modes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,16)};
   var imagesMode=new RadioButton{Content="Image & Exclusive Animation",Foreground=text,FontSize=13,IsChecked=!lcdMonitorMode,Margin=new Thickness(0,0,25,0)};imagesMode.Checked+=(s,e)=>{lcdMonitorMode=false;ShowPage(page);};modes.Children.Add(imagesMode);var monitorMode=new RadioButton{Content="硬件监控",Foreground=text,FontSize=13,IsChecked=lcdMonitorMode};monitorMode.Checked+=(s,e)=>{lcdMonitorMode=true;ShowPage(page);};modes.Children.Add(monitorMode);right.Children.Add(modes);
   if(lcdMonitorMode){OriginalLcdMonitorEditor(right,snapshot);content.Children.Add(body);return;}
   var instructions=Paragraph("支持 JPEG 文件，大小不超过 64 MB。",11,muted);instructions.Margin=new Thickness(0,0,0,13);right.Children.Add(instructions);
   var upload=Button("上传文件",OpenLcdUpload);upload.IsEnabled=LcdWritable(snapshot,"imageUploadWritable");upload.Padding=new Thickness(15,7,15,7);upload.HorizontalAlignment=HorizontalAlignment.Left;upload.Margin=new Thickness(0,0,0,22);right.Children.Add(upload);
   var presets=new Grid();for(int i=0;i<7;i++)presets.ColumnDefinitions.Add(new ColumnDefinition());presets.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});presets.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   for(int i=0;i<8;i++) {
    string path=i<2?Path.Combine(OriginalLcdAssets,"bg_anim","x870_launch"+(i+1).ToString(CultureInfo.InvariantCulture)+".gif"):Path.Combine(OriginalLcdAssets,"bg_img","x870_"+(i-1).ToString("00",CultureInfo.InvariantCulture)+".png");
    // Thumbnails show the installed preset assets, never imply they are the
    // live screen contents. GIFs use WPF's first frame for the thumbnail.
    var image=OriginalInstalledImage(path,80,142);
    if(i==1){var preview=new Grid();preview.Children.Add(image);var caption=Label("安装预设",10,muted);caption.TextAlignment=TextAlignment.Center;caption.VerticalAlignment=VerticalAlignment.Bottom;caption.Margin=new Thickness(0,0,0,7);preview.Children.Add(caption);image=preview;}
    int presetId=i;var slot=new Border{Child=image,BorderBrush=lcdSelectedPreset==i?accent:border,BorderThickness=new Thickness(lcdSelectedPreset==i?2:1),Padding=new Thickness(3),Margin=new Thickness(i%7==0?0:4,0,i%7==6?0:4,13),Background=Brush("#141414"),ToolTip="选择预设 "+(i+1)+" · 点击应用后显示",Cursor=System.Windows.Input.Cursors.Hand};slot.MouseLeftButtonUp+=(s,e)=>{lcdSelectedPreset=presetId;ShowPage(page);};Grid.SetColumn(slot,i%7);Grid.SetRow(slot,i/7);presets.Children.Add(slot);
   }
   right.Children.Add(presets);content.Children.Add(body);
  }

  static bool? OriginalStateFlag(IDictionary<string,object> snapshot,string key) {
   object value;if(snapshot==null||!snapshot.TryGetValue(key,out value)||!(value is bool))return null;return (bool)value;
  }
  static string OriginalStateText(IDictionary<string,object> snapshot,string key,string missing) {
   object value;if(snapshot==null||!snapshot.TryGetValue(key,out value)||value==null)return missing;return Convert.ToString(value,CultureInfo.InvariantCulture);
  }

  void OriginalControllerPage() {
   var title=Label("ROG EURUX",24,text,FontWeights.SemiBold);title.Margin=new Thickness(0,0,0,24);content.Children.Add(title);HardwareControlHint(content);
   // Tab identities and authored fallback text are verified in the installed
   // View/7576/resources/src/_ref/caps.json functionList. No default RPM from
   // that capability file is treated as a current hardware reading.
   var tabs=new StackPanel{Orientation=Orientation.Horizontal};
   foreach(string name in new[]{"Fan PWM","Lighting Effect","FW Updated"}) {
    string tab=name;var button=Button(tab,()=>{originalControllerTab=tab;ShowPage(page);});button.Margin=new Thickness(0,0,30,0);button.Padding=new Thickness(0,0,0,13);button.FontSize=14;
    var template=new ControlTemplate(typeof(Button));var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.PaddingProperty,new Thickness(0,0,0,13));frame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,2));frame.SetValue(Border.BorderBrushProperty,originalControllerTab==tab?accent:Brush("#00000000"));frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));template.VisualTree=frame;button.Template=template;tabs.Children.Add(button);
   }
   content.Children.Add(tabs);content.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,0,0,20)});
   var reference=Paragraph("原版设备页面在本机返回“页面出错 (H-2004)”，布局参照尚未完成。",12,muted);reference.Margin=new Thickness(0,0,0,20);content.Children.Add(reference);
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("fan:7576",out snapshot);
   if(originalControllerTab=="Lighting Effect"){OriginalControllerLightingPage(content,snapshot);return;}
   if(originalControllerTab=="FW Updated"){
    content.Children.Add(Paragraph("当前控制器固件  "+OriginalStateText(snapshot,"firmware","—"),14,text));
    var readFirmware=Button("读取控制器固件",async()=>await ReadOriginalSnapshot("fan","7576"));readFirmware.IsEnabled=!busy&&!render;content.Children.Add(readFirmware);
    content.Children.Add(Paragraph("固件安装尚未实现。请通过官方更新中心确认适用版本。",12,muted));return;
   }
   var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,22)};
   var read=Button(busy?"正在读取…":"读取控制器状态",async()=>await ReadOriginalSnapshot("fan","7576"));read.IsEnabled=!busy;read.FontSize=12;read.Padding=new Thickness(13,7,13,7);actions.Children.Add(read);
   var state=Label(snapshot==null?"尚未读取":"本次读取快照",11,muted);state.Margin=new Thickness(7,0,0,0);state.VerticalAlignment=VerticalAlignment.Center;actions.Children.Add(state);content.Children.Add(actions);
   var rpmTitle=Label("端口转速",17,text,FontWeights.SemiBold);rpmTitle.Margin=new Thickness(0,0,0,13);content.Children.Add(rpmTitle);
   var ports=new Grid();for(int i=0;i<4;i++)ports.ColumnDefinitions.Add(new ColumnDefinition());
   for(int i=0;i<4;i++) {
    string id=(i+1).ToString(CultureInfo.InvariantCulture);var port=OriginalSnapshotItem(snapshot,"ports","portId",id);
    string value=OriginalStateFlag(port,"confirmed")==true?OriginalSnapshotNumber(port,"rpm",0,50000,"RPM"):"— RPM";
    var card=OriginalReadout("端口 "+id,value);card.Margin=new Thickness(i==0?0:7,0,i==3?0:7,0);Grid.SetColumn(card,i);ports.Children.Add(card);
   }
   content.Children.Add(ports);
   var temperatureTitle=Label("温度",17,text,FontWeights.SemiBold);temperatureTitle.Margin=new Thickness(0,25,0,13);content.Children.Add(temperatureTitle);
   var temperatures=new Grid();for(int i=0;i<5;i++)temperatures.ColumnDefinitions.Add(new ColumnDefinition());
   for(int i=0;i<5;i++) {
    string id=(i+1).ToString(CultureInfo.InvariantCulture);var source=OriginalSnapshotItem(snapshot,"temperatures","sourceId",id);
    string[] temperatureNames={"CPU","GPU","DRAM","主板","T Sensor"};var card=OriginalReadout(temperatureNames[i],OriginalSnapshotNumber(source,"temperature",-50,150,"°C"));card.Margin=new Thickness(i==0?0:7,0,i==4?0:7,0);Grid.SetColumn(card,i);temperatures.Children.Add(card);
   }
   content.Children.Add(temperatures);
   OriginalControllerSettings(content,snapshot);
   OriginalControllerTemperatureSourceEditor(content,snapshot);
   var note=Paragraph("风扇曲线可向在线端口发送 PWM 设置；灯效、固件更新与自动调校仍未接通。",11,muted);note.Margin=new Thickness(0,17,0,0);content.Children.Add(note);
  }

  Border OriginalReadout(string name,string value) {
   var stack=new StackPanel();var label=Label(name,12,muted);label.Margin=new Thickness(0,0,0,17);stack.Children.Add(label);
   var reading=Label(value,23,value.StartsWith("—",StringComparison.Ordinal)?muted:accent,FontWeights.SemiBold);reading.Margin=new Thickness(0);stack.Children.Add(reading);
   return OriginalPanel(stack,new Thickness(18),new Thickness(0));
  }

  static IDictionary<string,object> OriginalSnapshotItem(IDictionary<string,object> snapshot,string collection,string idKey,string id) {
   object values;if(snapshot==null||!snapshot.TryGetValue(collection,out values)||!(values is IEnumerable)||values is string)return null;
   foreach(object item in (IEnumerable)values){var entry=item as IDictionary<string,object>;object itemId;if(entry!=null&&entry.TryGetValue(idKey,out itemId)&&Convert.ToString(itemId,CultureInfo.InvariantCulture)==id)return entry;}
   return null;
  }
  static string OriginalSnapshotNumber(IDictionary<string,object> source,string key,double min,double max,string unit) {
   object value;double number;
   if(source==null||!source.TryGetValue(key,out value)||value==null||!Double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out number)||Double.IsNaN(number)||Double.IsInfinity(number)||number<min||number>max)return "— "+unit;
   return number.ToString("0.#",CultureInfo.InvariantCulture)+" "+unit;
  }
 }
}
