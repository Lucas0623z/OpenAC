using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  int? lcdSelectedPreset;
  bool lcdMonitorMode;
  int lcdMonitorBackground=8;
  readonly HashSet<string> lcdSelectedSensors=new HashSet<string>();
  string LcdPresetPath(int id){return id<2?Path.Combine(OriginalLcdAssets,"bg_anim","x870_launch"+(id+1).ToString(CultureInfo.InvariantCulture)+".gif"):Path.Combine(OriginalLcdAssets,"bg_img","x870_"+(id-1).ToString("00",CultureInfo.InvariantCulture)+".png");}
  bool LcdWritable(IDictionary<string,object> snapshot,string setting){return HardwareWritesEnabled&&OriginalStateFlag(snapshot,setting)==true&&!busy;}
  async void ApplyLcdDraft(){Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);if(lcdMonitorMode){if(!LcdWritable(snapshot,"hardwareMonitorWritable"))return;int maximum=lcdMonitorBackground==11?5:3;if(lcdSelectedSensors.Count<1||lcdSelectedSensors.Count>maximum){Log("请为此背景选择 1 至 "+maximum+" 个传感器。");return;}await SetOriginalNative("lcd","7299","hardwareMonitor",new Dictionary<string,object>{{"backgroundId",lcdMonitorBackground},{"sensorIds",lcdSelectedSensors.ToArray()}});}else if(lcdSelectedPreset.HasValue&&LcdWritable(snapshot,"presetWritable"))await SetOriginalNative("lcd","7299","presetId",new Dictionary<string,object>{{"value",lcdSelectedPreset.Value}});}
  void OriginalLcdMonitorEditor(StackPanel target,Dictionary<string,object> snapshot){
   var choices=new WrapPanel{Margin=new Thickness(0,0,0,18)};for(int i=8;i<=11;i++){int id=i;var choice=Button("背景 "+(id-7),()=>{if(busy||render)return;lcdMonitorBackground=id;ShowPage(page);});choice.IsEnabled=!busy&&!render;choice.Foreground=id==lcdMonitorBackground?accent:text;choices.Children.Add(choice);}target.Children.Add(choices);
   var sensors=GearArray(snapshot,"hardwareSensors");if(sensors==null||sensors.Length==0){target.Children.Add(Paragraph("尚未读取到可用传感器。请先读取屏幕状态。",12,muted));return;}
   target.Children.Add(Paragraph("此背景最多显示 "+(lcdMonitorBackground==11?5:3)+" 项。选择后点击应用。",12,muted));var panelSensors=new WrapPanel();foreach(var raw in sensors){var sensor=raw as Dictionary<string,object>;if(sensor==null)continue;string id=OriginalStateText(sensor,"id","");if(id.Length==0)continue;var option=new CheckBox{Content=OriginalStateText(sensor,"name",OriginalStateText(sensor,"defaultName","传感器")),Foreground=text,IsEnabled=!busy&&!render,IsChecked=lcdSelectedSensors.Contains(id),Margin=new Thickness(0,10,22,10),MinWidth=150};option.Checked+=(s,e)=>{if(!busy&&!render)lcdSelectedSensors.Add(id);};option.Unchecked+=(s,e)=>{if(!busy&&!render)lcdSelectedSensors.Remove(id);};panelSensors.Children.Add(option);}target.Children.Add(panelSensors);
  }
  void OpenLcdUpload(){
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);if(!LcdWritable(snapshot,"imageUploadWritable"))return;
   var pick=new Microsoft.Win32.OpenFileDialog{Title="选择 LCD 图片",Filter="JPEG 图片 (*.jpg;*.jpeg)|*.jpg;*.jpeg",CheckFileExists=true};if(pick.ShowDialog(this)!=true)return;
   if(new FileInfo(pick.FileName).Length>64L*1024*1024){Log("图片不能超过 64 MB。");return;}
   string path=pick.FileName;var dialog=new Window{Title="上传 LCD 图片",Owner=this,Width=490,Height=580,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=bg,Foreground=text};var body=new StackPanel{Margin=new Thickness(24)};
   body.Children.Add(OriginalInstalledImage(path,150,267));body.Children.Add(Paragraph(Path.GetFileName(path),12,muted));body.Children.Add(Paragraph("图片将等比缩放到 720 × 1280，空白区域补黑边，原图保持不变。",12,muted));
   var slots=FeatureCombo(new[]{"请选择槽位"}.Concat(Enumerable.Range(0,32).Select(v=>"槽位 "+v)),"请选择槽位");body.Children.Add(FeatureField("保存位置",slots));body.Children.Add(Paragraph("设备不能读取槽位占用情况。上传会覆盖所选槽位已有的内容。",12,muted));
   var upload=Button("选择槽位后上传并显示",async()=>{if(slots.SelectedIndex<1||busy)return;int slot=slots.SelectedIndex-1;dialog.Close();await SetOriginalNative("lcd","7299","uploadImage",new Dictionary<string,object>{{"filePath",path},{"slot",slot}});},true);upload.IsEnabled=false;slots.SelectionChanged+=(s,e)=>{upload.IsEnabled=slots.SelectedIndex>0;upload.Content=slots.SelectedIndex>0?"覆盖槽位 "+(slots.SelectedIndex-1)+" 并显示":"选择槽位后上传并显示";};body.Children.Add(upload);dialog.Content=body;dialog.ShowDialog();
  }
 }
}
