using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 // Capability IDs are meaningful only in this exact read and worker session.
 // This data-only model never opens a device or carries IDs to another assembly.
 public sealed class LcdMonitorSensor {
  public string Id,NativeId,Name,DefaultName,Signature;public int NativeType;
 }
 public sealed class LcdMonitorRow {
  public int? Category {get;internal set;}public string SensorId {get;internal set;}internal string Signature;
 }
 public sealed class LcdMonitorDraft {
  Dictionary<string,object> boundSnapshot;object boundSession;Func<object> currentSession;string fingerprint;
  readonly List<LcdMonitorRow> rows=new List<LcdMonitorRow>();
  public IList<LcdMonitorRow> Rows {get{return rows.AsReadOnly();}}
  static object Field(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  static string Text(IDictionary<string,object> map,string key){return Field(map,key) as string;}
  static bool Int(object raw,out int value){value=0;return raw!=null&&!(raw is bool)&&Int32.TryParse(Convert.ToString(raw,CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture,out value);}
  static string Part(string value){return value.Length.ToString(CultureInfo.InvariantCulture)+":"+value;}
  static List<LcdMonitorSensor> Parse(Dictionary<string,object> snapshot) {
   var list=new List<LcdMonitorSensor>();var raw=Field(snapshot,"hardwareSensors") as IEnumerable;if(raw==null||raw is string)return list;
   var seen=new HashSet<string>(StringComparer.Ordinal);int count=0;
   foreach(object value in raw){if(++count>4096)return new List<LcdMonitorSensor>();var cap=value as IDictionary<string,object>;string id=Text(cap,"id"),native=Text(cap,"nativeId"),name=Text(cap,"name"),fallback=Text(cap,"defaultName");int type;long nativeNumber;
    if(String.IsNullOrEmpty(id)||id.Length>128||id.Any(Char.IsControl)||!seen.Add(id)||!Int(Field(cap,"nativeType"),out type)||String.IsNullOrEmpty(native)||!Int64.TryParse(native,NumberStyles.None,CultureInfo.InvariantCulture,out nativeNumber)||nativeNumber<0||name==null||fallback==null||name.Length>256||fallback.Length>256)return new List<LcdMonitorSensor>();
    // Installed motherboard caps.sortType exposes exactly these four categories.
    // Type5 utilization entries have no confirmed category here and are omitted.
    if(type!=0&&type!=1&&type!=2&&type!=4)continue;
    string signature=Part(native)+type.ToString(CultureInfo.InvariantCulture)+Part(fallback)+Part(name);list.Add(new LcdMonitorSensor{Id=id,NativeId=native,Name=String.IsNullOrWhiteSpace(name)?fallback:name,DefaultName=fallback,NativeType=type,Signature=signature});
   }return list;
  }
  static string Fingerprint(List<LcdMonitorSensor> sensors){return String.Join("",sensors.OrderBy(sensor=>sensor.Id,StringComparer.Ordinal).Select(sensor=>Part(sensor.Id)+Part(sensor.Signature)));}
  static bool Confirmed(Dictionary<string,object> snapshot){return Object.Equals(Field(snapshot,"readbackConfirmed"),true)&&Object.Equals(Field(snapshot,"deviceAlive"),true)&&Object.Equals(Field(snapshot,"hardwareMonitorWritable"),true)&&Text(snapshot,"deviceId")=="7299";}
  public void Clear(){boundSnapshot=null;boundSession=null;currentSession=null;fingerprint=null;rows.Clear();}
  public void Bind(Dictionary<string,object> snapshot,object session,Func<object> provider){Clear();if(!Confirmed(snapshot)||session==null||provider==null)return;var sensors=Parse(snapshot);if(sensors.Count==0)return;try{if(!Object.ReferenceEquals(provider(),session))return;}catch{return;}boundSnapshot=snapshot;boundSession=session;currentSession=provider;fingerprint=Fingerprint(sensors);}
  public bool IsCurrent(Dictionary<string,object> snapshot){if(!Object.ReferenceEquals(snapshot,boundSnapshot)||!Confirmed(snapshot)||boundSession==null||currentSession==null)return false;try{return Object.ReferenceEquals(currentSession(),boundSession)&&Fingerprint(Parse(snapshot))==fingerprint;}catch{return false;}}
  public void Resize(int count){if(count<1||count>5){rows.Clear();return;}while(rows.Count<count)rows.Add(new LcdMonitorRow());if(rows.Count>count)rows.RemoveRange(count,rows.Count-count);}
  public List<LcdMonitorSensor> Sensors(Dictionary<string,object> snapshot,int? type=null){return IsCurrent(snapshot)?Parse(snapshot).Where(sensor=>!type.HasValue||sensor.NativeType==type.Value).ToList():new List<LcdMonitorSensor>();}
  public void SetCategory(Dictionary<string,object> snapshot,int index,int? type){if(index<0||index>=rows.Count)return;rows[index].Category=type.HasValue&&Sensors(snapshot,type).Count>0?type:null;rows[index].SensorId=null;rows[index].Signature=null;}
  public bool Select(Dictionary<string,object> snapshot,int index,string id){if(index<0||index>=rows.Count||!rows[index].Category.HasValue)return false;var sensor=Sensors(snapshot,rows[index].Category).SingleOrDefault(choice=>choice.Id==id);rows[index].SensorId=sensor==null?null:sensor.Id;rows[index].Signature=sensor==null?null:sensor.Signature;return sensor!=null;}
  public bool TryBuild(Dictionary<string,object> snapshot,int background,int count,out string[] sensorIds,out object session) {
   sensorIds=null;session=null;if(background<8||background>11||count<1||count>(background==11?5:3)||rows.Count!=count||!IsCurrent(snapshot))return false;var caps=Sensors(snapshot);var selected=new List<string>();var ids=new HashSet<string>(StringComparer.Ordinal);var identities=new HashSet<string>(StringComparer.Ordinal);
   foreach(var row in rows){var cap=caps.SingleOrDefault(sensor=>sensor.Id==row.SensorId);if(cap==null||row.Category!=cap.NativeType||row.Signature!=cap.Signature||!ids.Add(cap.Id)||!identities.Add(Part(cap.NativeId)+cap.NativeType.ToString(CultureInfo.InvariantCulture)+Part(cap.DefaultName)))return false;selected.Add(cap.Id);}sensorIds=selected.ToArray();session=boundSession;return true;
  }
 }
 public partial class MainWindow {
  int? lcdSelectedPreset;
  bool lcdMonitorMode;
  int lcdMonitorBackground=8;
  int lcdMonitorInfoCount=1;
  readonly LcdMonitorDraft lcdMonitorDraft=new LcdMonitorDraft();
  readonly HashSet<string> lcdSelectedSensors=new HashSet<string>();
  string LcdPresetPath(int id){return id<2?Path.Combine(OriginalLcdAssets,"bg_anim","x870_launch"+(id+1).ToString(CultureInfo.InvariantCulture)+".gif"):Path.Combine(OriginalLcdAssets,"bg_img","x870_"+(id-1).ToString("00",CultureInfo.InvariantCulture)+".png");}
  bool LcdWritable(IDictionary<string,object> snapshot,string setting){return HardwareWritesEnabled&&OriginalStateFlag(snapshot,setting)==true&&!busy;}
  void ResetLcdMonitorSensorDraft(){lcdMonitorDraft.Clear();lcdSelectedSensors.Clear();}
  void BindLcdMonitorRead(Dictionary<string,object> snapshot,object session,Func<object> provider){lcdMonitorDraft.Bind(snapshot,session,provider);lcdSelectedSensors.Clear();}
  void SyncLcdMonitorLegacySensors(){lcdSelectedSensors.Clear();foreach(var row in lcdMonitorDraft.Rows)if(!String.IsNullOrEmpty(row.SensorId))lcdSelectedSensors.Add(row.SensorId);}
  bool LcdMonitorCanApply(Dictionary<string,object> snapshot){string[] sensors;object session;return LcdWritable(snapshot,"hardwareMonitorWritable")&&lcdMonitorDraft.TryBuild(snapshot,lcdMonitorBackground,lcdMonitorInfoCount,out sensors,out session);}
  async void ApplyLcdDraft(){if(render||busy||closing)return;Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);if(lcdMonitorMode){if(!LcdWritable(snapshot,"hardwareMonitorWritable"))return;string[] sensors;object session;if(!lcdMonitorDraft.TryBuild(snapshot,lcdMonitorBackground,lcdMonitorInfoCount,out sensors,out session)){Log("请选择完整且不同的传感器；读取会话失效时请重新读取。");return;}await SetOriginalNative("lcd","7299","hardwareMonitor",new Dictionary<string,object>{{"backgroundId",lcdMonitorBackground},{"sensorIds",sensors}},session);}else if(lcdSelectedPreset.HasValue&&LcdWritable(snapshot,"presetWritable"))await SetOriginalNative("lcd","7299","presetId",new Dictionary<string,object>{{"value",lcdSelectedPreset.Value}});}
  string LcdMonitorBackgroundPath(int background){return Path.Combine(Path.GetDirectoryName(OriginalLcdAssets),"hw_monitor","bg_anim",background==11?"hw_st_04.gif":"x870_hw_"+(background-7).ToString(CultureInfo.InvariantCulture)+".gif");}
  bool lcdMonitorLocalImages=true;
  FrameworkElement LcdMonitorBackgroundImage(int background,double width,double height){return lcdMonitorLocalImages?OriginalInstalledImage(LcdMonitorBackgroundPath(background),width,height):new Border{Width=width,Height=height,Background=Brush("#101010")};}
  void OriginalLcdMonitorEditor(StackPanel target,Dictionary<string,object> snapshot){
   if(lcdMonitorBackground<8||lcdMonitorBackground>11)lcdMonitorBackground=8;int maximum=lcdMonitorBackground==11?5:3;lcdMonitorInfoCount=Math.Max(1,Math.Min(maximum,lcdMonitorInfoCount));
   if(!lcdMonitorDraft.IsCurrent(snapshot))ResetLcdMonitorSensorDraft();lcdMonitorDraft.Resize(lcdMonitorInfoCount);SyncLcdMonitorLegacySensors();
   var editor=new StackPanel{MinWidth=688,Margin=new Thickness(0,20,0,0),Tag="lcdMonitor:editor"};target.Children.Add(editor);var backgrounds=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,24)};
   for(int background=8;background<=11;background++){int selected=background;var choice=Button("",()=>{if(busy||closing)return;lcdMonitorBackground=selected;lcdMonitorInfoCount=Math.Min(lcdMonitorInfoCount,selected==11?5:3);lcdMonitorDraft.Resize(lcdMonitorInfoCount);SyncLcdMonitorLegacySensors();ShowPage(page);});choice.Width=72;choice.Height=125;choice.Padding=new Thickness(0);choice.Margin=new Thickness(0,0,background==11?0:20,0);choice.Background=Brushes.Transparent;choice.BorderThickness=new Thickness(1.5);choice.BorderBrush=background==lcdMonitorBackground?Brush("#CB2C45"):muted;choice.Content=LcdMonitorBackgroundImage(background,69,122);choice.IsEnabled=!busy&&!closing;choice.ToolTip="背景 "+(background-7);choice.Tag="lcdMonitor:background:"+background;backgrounds.Children.Add(choice);}editor.Children.Add(backgrounds);
   var heading=Label("信息",15,text);heading.Margin=new Thickness(0,0,0,8);editor.Children.Add(heading);string[] names={"Single Info","Dual Info","Triple Info","Quad Info","Multi Info"};var count=MousePerformanceCombo(names.Take(maximum),lcdMonitorInfoCount-1,!busy&&!closing,320);count.Margin=new Thickness(0,0,0,14);count.HorizontalAlignment=HorizontalAlignment.Left;count.Tag="lcdMonitor:infoCount";count.SelectionChanged+=(sender,args)=>{if(busy||closing||count.SelectedIndex<0)return;lcdMonitorInfoCount=count.SelectedIndex+1;lcdMonitorDraft.Resize(lcdMonitorInfoCount);SyncLcdMonitorLegacySensors();ShowPage(page);};editor.Children.Add(count);
   var caps=lcdMonitorDraft.Sensors(snapshot);int[] types={0,1,2,4};string[] typeNames={"Voltage","Temperature","Fan Speed","Frequency"};
   for(int index=0;index<lcdMonitorInfoCount;index++){int rowIndex=index;var draft=lcdMonitorDraft.Rows[index];var row=new Grid{Height=34,Margin=new Thickness(0,0,0,6),Tag="lcdMonitor:row:"+index};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(264)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(6)});row.ColumnDefinitions.Add(new ColumnDefinition());
    var category=MousePerformanceCombo(new string[0],-1,caps.Count>0&&!busy&&!closing,264);category.Items.Add(new ComboBoxItem{Content="—",Tag=null});for(int item=0;item<types.Length;item++)if(caps.Any(sensor=>sensor.NativeType==types[item])){var option=new ComboBoxItem{Content=typeNames[item],Tag=types[item]};category.Items.Add(option);if(draft.Category==types[item])category.SelectedItem=option;}if(category.SelectedIndex<0)category.SelectedIndex=0;category.Tag="lcdMonitor:category:"+index;category.SelectionChanged+=(sender,args)=>{if(busy||closing)return;var option=category.SelectedItem as ComboBoxItem;lcdMonitorDraft.SetCategory(snapshot,rowIndex,option!=null&&option.Tag is int?(int?)option.Tag:null);SyncLcdMonitorLegacySensors();ShowPage(page);};row.Children.Add(category);
    var sensors=MousePerformanceCombo(new string[0],-1,draft.Category.HasValue&&caps.Count>0&&!busy&&!closing,320);sensors.Width=Double.NaN;sensors.MinWidth=0;sensors.Items.Add(new ComboBoxItem{Content="—",Tag=""});if(draft.Category.HasValue)foreach(var cap in caps.Where(sensor=>sensor.NativeType==draft.Category.Value)){var option=new ComboBoxItem{Content=String.IsNullOrWhiteSpace(cap.Name)?"—":cap.Name,Tag=cap.Id};sensors.Items.Add(option);if(draft.SensorId==cap.Id)sensors.SelectedItem=option;}if(sensors.SelectedIndex<0)sensors.SelectedIndex=0;sensors.Tag="lcdMonitor:sensor:"+index;sensors.SelectionChanged+=(sender,args)=>{if(busy||closing)return;var option=sensors.SelectedItem as ComboBoxItem;lcdMonitorDraft.Select(snapshot,rowIndex,option==null?null:Convert.ToString(option.Tag));SyncLcdMonitorLegacySensors();ShowPage(page);};Grid.SetColumn(sensors,2);row.Children.Add(sensors);editor.Children.Add(row);
   }editor.ToolTip="传感器选择仅属于当前读取会话。完整且不同的选择可点击应用；预览读数保持未知。";
  }
  FrameworkElement LcdMonitorPreview(Dictionary<string,object> snapshot){var preview=new Grid{Width=169,Height=301,ClipToBounds=true,Background=Brush("#090909"),Tag="lcdMonitor:preview"};preview.Children.Add(LcdMonitorBackgroundImage(lcdMonitorBackground,169,301));var overlay=new Canvas{Width=169,Height=301};int count=Math.Max(1,Math.Min(5,lcdMonitorInfoCount));var caps=lcdMonitorDraft.Sensors(snapshot);
   for(int index=0;index<count;index++){var row=index<lcdMonitorDraft.Rows.Count?lcdMonitorDraft.Rows[index]:null;var cap=row==null?null:caps.SingleOrDefault(sensor=>sensor.Id==row.SensorId);bool fourth=lcdMonitorBackground==11;double[] x={48,8,103,103,8},titleY={124,67,235,67,235},valueY={143,30,250,30,250};double titleTop=fourth?titleY[index]:count==1?125:30+index*(240.0/count);var title=Label(cap==null?"—":cap.Name,11,fourth&&index==0?text:Brush("#FF0050"),FontWeights.SemiBold);title.Width=fourth?(index==0?73:58):153;title.Margin=new Thickness(0);title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextAlignment=fourth&&index==0?TextAlignment.Center:TextAlignment.Left;title.ToolTip=cap==null?"—":cap.Name;title.Tag="lcdMonitor:previewName:"+index;Canvas.SetLeft(title,fourth?x[index]:8);Canvas.SetTop(title,titleTop);overlay.Children.Add(title);var value=Label("—",25,text,FontWeights.SemiBold);value.Width=fourth?(index==0?73:58):153;value.TextAlignment=fourth&&index==0?TextAlignment.Center:TextAlignment.Left;value.Margin=new Thickness(0);value.Tag="lcdMonitor:previewValue:"+index;Canvas.SetLeft(value,fourth?x[index]:8);Canvas.SetTop(value,fourth?valueY[index]:titleTop+16);overlay.Children.Add(value);}preview.Children.Add(overlay);preview.ToolTip="所选背景与传感器名称的本地预览；屏幕内容与数值未读取。";return preview;}
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
