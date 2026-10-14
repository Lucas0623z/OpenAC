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
   var titleRow=new Grid{Height=34,Margin=new Thickness(15,0,0,23)};var title=Label("ROG CROSSHAIR X870E EXTREME",24,text);title.Margin=new Thickness(0);title.VerticalAlignment=VerticalAlignment.Center;titleRow.Children.Add(title);content.Children.Add(titleRow);
   var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(-22,0,-22,0)};
   string[] names={"5\" LCD Display","可编程接针","磁盘信息","ASUS WiFi Q-Antenna"};
   for(int i=0;i<names.Length;i++) {
    string tab=names[i];bool active=originalMotherboardTab==tab;var button=Button(tab,()=>{originalMotherboardTab=tab;ShowPage(page);});button.Width=183;button.Height=42;button.Margin=new Thickness(0);button.Padding=new Thickness(8,7,8,7);button.Background=active?Brush("#1D2021"):Brushes.Transparent;button.BorderBrush=tab=="磁盘信息"?Brush("#CB2C45"):Brush("#258CB4");button.BorderThickness=new Thickness(0,0,0,active?2:0);button.FontSize=15;tabs.Children.Add(button);
   }
   content.Children.Add(tabs);PinPageHeader(2,"motherboard:"+originalMotherboardTab);
   if(originalMotherboardTab=="磁盘信息"){OriginalStoragePage();return;}
   if(originalMotherboardTab=="可编程接针"){OriginalBoardArgbPage(content);return;}
   if(originalMotherboardTab!="5\" LCD Display") {
    OriginalMotherboardAntennaUnavailable();return;
   }
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);
   var body=new Grid{Margin=new Thickness(35,54,21,0)};body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(450)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(38)});body.ColumnDefinitions.Add(new ColumnDefinition());
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   var powerRow=new Grid{Margin=new Thickness(0,4,0,0)};powerRow.ColumnDefinitions.Add(new ColumnDefinition());powerRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   bool? power=OriginalStateFlag(snapshot,"powerS0");
   var toggle=new Border{Width=62,Height=29,CornerRadius=new CornerRadius(15),Background=power==true?accent:Brush("#303030"),HorizontalAlignment=HorizontalAlignment.Left,Child=new TextBlock{Text=power.HasValue?(power.Value?"ON":"OFF"):"—",Foreground=power==true?Brush("#151515"):muted,FontSize=12,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
   toggle.ToolTip="点击切换屏幕电源";toggle.IsEnabled=HardwareWritesEnabled&&power.HasValue&&!busy;toggle.Cursor=System.Windows.Input.Cursors.Hand;toggle.MouseLeftButtonUp+=async(s,e)=>{if(power.HasValue&&!busy)await SetOriginalNative("lcd","7299","powerS0",new Dictionary<string,object>{{"value",!power.Value}});};powerRow.Children.Add(toggle);
   var gear=Button("☷",OpenLcdSettings);gear.IsEnabled=!busy&&!closing&&!render;gear.FontSize=18;gear.Padding=new Thickness(7,2,7,2);gear.Margin=new Thickness(0,0,26,0);gear.Background=Brushes.Transparent;gear.BorderThickness=new Thickness(0);gear.ToolTip="设置";Grid.SetColumn(gear,1);powerRow.Children.Add(gear);left.Children.Add(powerRow);
   var display=new Border{Name="MotherboardLcdPreview",Width=169,Height=301,Background=Brush("#090909"),BorderThickness=new Thickness(0),Margin=new Thickness(0,28,26,15),HorizontalAlignment=HorizontalAlignment.Center,ToolTip="配置预览；设备屏幕画面未读取。"};int? previewPreset=lcdSelectedPreset??LcdConfiguredPreset(snapshot);if(lcdMonitorMode)display.Child=LcdMonitorPreview(snapshot);else if(previewPreset.HasValue)display.Child=OriginalInstalledImage(LcdPresetPath(previewPreset.Value),169,301);left.Children.Add(display);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,34,10)};
   var applyContent=Button("应用",ApplyLcdDraft);applyContent.IsEnabled=lcdMonitorMode?LcdMonitorCanApply(snapshot):lcdSelectedPreset.HasValue&&LcdWritable(snapshot,"presetWritable");var resetDraft=Button("默认",()=>{if(busy||closing||render)return;lcdSelectedPreset=0;lcdMonitorMode=false;ShowPage(page);});resetDraft.IsEnabled=!busy&&!closing&&!render;resetDraft.ToolTip="选择默认预设，点击应用后显示";foreach(var button in new[]{applyContent,resetDraft}){button.Width=132;button.Height=34;button.Padding=new Thickness(12,4,12,4);button.Margin=new Thickness(19,0,0,0);actions.Children.Add(button);}right.Children.Add(actions);
   var modes=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,36)};
   var imagesMode=OriginalAuraColourRadio("Image & Exclusive Animation",!lcdMonitorMode);imagesMode.FontSize=14;imagesMode.Height=20;imagesMode.Margin=new Thickness(0,0,16,0);imagesMode.IsEnabled=!busy&&!render;imagesMode.Checked+=(s,e)=>{if(busy||closing||render)return;lcdMonitorMode=false;ShowPage(page);};modes.Children.Add(imagesMode);var monitorMode=OriginalAuraColourRadio("硬件监控",lcdMonitorMode);monitorMode.FontSize=14;monitorMode.Height=20;monitorMode.Margin=new Thickness(0);monitorMode.IsEnabled=!busy&&!render;monitorMode.Checked+=(s,e)=>{if(busy||closing||render)return;lcdMonitorMode=true;ShowPage(page);};modes.Children.Add(monitorMode);right.Children.Add(modes);
   if(lcdMonitorMode){OriginalLcdMonitorEditor(right,snapshot);body.MinWidth=1176;content.Children.Add(new ScrollViewer{Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Tag="lcdMonitor:bodyViewport"});return;}
   var instructions=Paragraph("Only JPG formats allowed, with a storage limit of 64MB",11,muted);instructions.Margin=new Thickness(0,0,0,9);right.Children.Add(instructions);
   var upload=Button("上传文件",OpenLcdUpload);upload.IsEnabled=LcdWritable(snapshot,"imageUploadWritable");upload.Padding=new Thickness(15,7,15,7);upload.HorizontalAlignment=HorizontalAlignment.Left;upload.Margin=new Thickness(0,0,0,22);right.Children.Add(upload);
   var presets=new Grid();for(int i=0;i<7;i++)presets.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});presets.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});presets.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   for(int i=0;i<8;i++) {
    string path=i<2?Path.Combine(OriginalLcdAssets,"bg_anim","x870_launch"+(i+1).ToString(CultureInfo.InvariantCulture)+".gif"):Path.Combine(OriginalLcdAssets,"bg_img","x870_"+(i-1).ToString("00",CultureInfo.InvariantCulture)+".png");
    // Thumbnails show the installed preset assets, never imply they are the
    // live screen contents. GIFs use WPF's first frame for the thumbnail.
    var image=OriginalInstalledImage(path,67,119);
    int presetId=i;var slot=new Border{Width=72,Height=125,Child=image,BorderBrush=lcdSelectedPreset==i?accent:muted,BorderThickness=new Thickness(lcdSelectedPreset==i?2:1),Padding=new Thickness(1),Margin=new Thickness(0,0,i%7==6?0:20,18),Background=Brush("#141414"),ToolTip="预设 "+(i+1),IsEnabled=!busy&&!render,Cursor=System.Windows.Input.Cursors.Hand};slot.MouseLeftButtonUp+=(s,e)=>{if(busy||closing||render)return;lcdSelectedPreset=presetId;ShowPage(page);};Grid.SetColumn(slot,i%7);Grid.SetRow(slot,i/7);presets.Children.Add(slot);
   }
   right.Children.Add(KeyboardHorizontalChoices(presets,92,-1));content.Children.Add(body);
  }

  int? LcdConfiguredPreset(Dictionary<string,object> snapshot){if(OriginalStateFlag(snapshot,"profileReadbackConfirmed")!=true)return null;var group=GearMap(snapshot,"currentMediaProfile");if(OriginalStateText(group,"type","")!="2"||OriginalStateText(group,"source","")!="1")return null;return MouseInteger(group,"index_format",0,7);}
  void OriginalMotherboardAntennaUnavailable(){var body=new StackPanel{Name="MotherboardAntennaUnavailable",Width=540,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,178,0,0)};var icon=new Border{Width=90,Height=90,CornerRadius=new CornerRadius(45),BorderBrush=Brush("#2292AE"),BorderThickness=new Thickness(2),Child=new TextBlock{Text="i",FontSize=60,Foreground=Brush("#2292AE"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};body.Children.Add(icon);var caption=Label("请检查是否已安装无线网络驱动程序",14,text,FontWeights.SemiBold);caption.TextAlignment=TextAlignment.Center;caption.Margin=new Thickness(0,60,0,26);body.Children.Add(caption);var done=Button("确定",()=>ShowPage("设备"));done.Width=132;done.Height=34;done.HorizontalAlignment=HorizontalAlignment.Center;done.Padding=new Thickness(10,4,10,4);done.Margin=new Thickness(0);body.Children.Add(done);content.Children.Add(body);}

  static bool? OriginalStateFlag(IDictionary<string,object> snapshot,string key) {
   object value;if(snapshot==null||!snapshot.TryGetValue(key,out value)||!(value is bool))return null;return (bool)value;
  }
  static string OriginalStateText(IDictionary<string,object> snapshot,string key,string missing) {
   object value;if(snapshot==null||!snapshot.TryGetValue(key,out value)||value==null)return missing;return Convert.ToString(value,CultureInfo.InvariantCulture);
  }

  void OriginalControllerPage() {
   var title=Label("ROG EURUX",24,text,FontWeights.SemiBold);title.Margin=new Thickness(0,0,0,24);content.Children.Add(title);
   // Tab identities and authored fallback text are verified in the installed
   // View/7576/resources/src/_ref/caps.json functionList. No default RPM from
   // that capability file is treated as a current hardware reading.
   var tabs=new StackPanel{Orientation=Orientation.Horizontal};
   foreach(string name in new[]{"Fan PWM","Lighting Effect","FW Updated"}) {
    string tab=name;string label=name=="Fan PWM"?"风扇控制":name=="Lighting Effect"?"风扇灯效":"固件更新";var button=Button(label,()=>{originalControllerTab=tab;ShowPage(page);});button.Margin=new Thickness(0,0,30,0);button.Padding=new Thickness(0,0,0,13);button.FontSize=14;
    var template=new ControlTemplate(typeof(Button));var frame=new FrameworkElementFactory(typeof(Border));frame.SetValue(Border.PaddingProperty,new Thickness(0,0,0,13));frame.SetValue(Border.BorderThicknessProperty,new Thickness(0,0,0,2));frame.SetValue(Border.BorderBrushProperty,originalControllerTab==tab?accent:Brush("#00000000"));frame.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));template.VisualTree=frame;button.Template=template;tabs.Children.Add(button);
   }
   content.Children.Add(tabs);content.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,0,0,20)});
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("fan:7576",out snapshot);
   if(originalControllerTab=="Lighting Effect"){OriginalControllerLightingPage(content,snapshot);return;}
   if(originalControllerTab=="FW Updated"){
    content.Children.Add(Paragraph("当前控制器固件  "+OriginalStateText(snapshot,"firmware","—"),14,text));
    var update=Button("检查更新",()=>{});update.IsEnabled=false;content.Children.Add(update);return;
   }
   if(busy)content.Children.Add(Label("正在加载…",11,muted));
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
