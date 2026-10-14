using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  readonly WorkerClient gearWorker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GearLinkWorker.exe"),18000);
  readonly Dictionary<string,Dictionary<string,object>> gearStates=new Dictionary<string,Dictionary<string,object>>();
  readonly Dictionary<string,object> gearReadSessions=new Dictionary<string,object>();
  readonly Dictionary<string,string> gearTabs=new Dictionary<string,string>();
  string gearMessage="";
  string headsetAudioTab="均衡器";
  void OriginalGearDevicePage(string kind) {
   Dictionary<string,object> state;gearStates.TryGetValue(kind,out state);var snapshot=GearMap(state,"snapshot");bool fullWidthGear=kind=="mouse"||kind=="keyboard"||kind=="headset";
   string title=kind=="headset"?"ROG 棱镜 2-KJP":kind=="mouse"?"ROG HARPE II EXTREME EDITION 20":"ROG 夜魔 EXTREME 二十周年版";
   if(fullWidthGear)OriginalGearProfileHeader(title,kind,state);else Header(title,"");
   string[] tabs=kind=="headset"?new[]{"音效","麦克风","设置","灯光","电量","奥创极速网页版","固件更新"}:kind=="mouse"?new[]{"按键","性能","灯光","校准","电量","Zone 模式","奥创极速网页版","固件更新"}:new[]{"按键","拨杆","OLED","灯光","电量","奥创极速网页版","性能","固件更新"};
   string current;if(!gearTabs.TryGetValue(kind,out current))current=tabs[0];if(current=="控制旋钮")current="拨杆";if(current=="Zone Mode")current="Zone 模式";Panel tabRow=fullWidthGear?(Panel)new StackPanel{Orientation=Orientation.Horizontal}:new WrapPanel{Margin=new Thickness(0,0,0,22)};
   foreach(string name in tabs){string target=name;var tab=Button(name,()=>{gearTabs[kind]=target;ShowPage(page);});tab.Foreground=target==current?accent:muted;tab.BorderThickness=new Thickness(0,0,0,target==current?2:0);tab.BorderBrush=accent;tab.Background=fullWidthGear&&target==current?Brush("#211E16"):System.Windows.Media.Brushes.Transparent;tab.Margin=new Thickness(0,0,fullWidthGear?0:14,0);if(fullWidthGear){tab.Width=183;tab.Height=42;tab.FontSize=15;tab.Padding=new Thickness(8,7,8,7);}tabRow.Children.Add(tab);}
   if(fullWidthGear)content.Children.Add(MouseTabStrip(tabRow,tabs,current));else content.Children.Add(tabRow);
   PinPageHeader(fullWidthGear?2:3,"device:"+kind+":"+current);
   string deviceStatus=state==null?(busy?"正在加载…":gearMessage.IndexOf("失败",StringComparison.Ordinal)>=0||gearMessage.IndexOf("未完成",StringComparison.Ordinal)>=0?"加载失败":""):GearOnline(state)?"":"设备未连接";
   if(deviceStatus.Length>0){status.Text=deviceStatus;status.ToolTip=gearMessage;}
   if(current=="性能"&&kind=="mouse"){OriginalMousePerformancePage(content,state);return;}
   if(current=="按键"&&kind=="mouse"){OriginalMouseButtonsPage(content,state);return;}
   if(current=="灯光"&&kind=="mouse"){OriginalMouseLightingPage(content,state);return;}
   if(current=="校准"&&kind=="mouse"){OriginalMouseCalibrationPage(content,state);return;}
   if(current=="Zone 模式"&&kind=="mouse"){OriginalMouseZonePage(content,state);return;}
   if(current=="按键"&&kind=="keyboard"){OriginalKeyboardKeysPage(content,state);return;}
   if(current=="拨杆"&&kind=="keyboard"){OriginalKeyboardLeverPage(content,state);return;}
   if(current=="OLED"&&kind=="keyboard"){OriginalKeyboardOledPage(content,state);return;}
   if(current=="电量"&&(kind=="keyboard"||kind=="mouse"||kind=="headset")){OriginalGearPowerPage(content,kind,state);return;}
   if(current=="性能"&&kind=="keyboard"){OriginalKeyboardPollingPage(content,state);return;}
   if(current=="灯光"&&kind=="keyboard"){OriginalKeyboardLightingPage(content,state);return;}
   if(current=="灯光"&&kind=="headset"){OriginalHeadsetLightingPage(content,state);return;}
   if(current=="音效"&&kind=="headset"){OriginalHeadsetAudioPage(content,state);return;}
   if(current=="麦克风"&&kind=="headset"){OriginalHeadsetMicrophonePage(content,state);return;}
   if(current=="设置"&&kind=="headset"){OriginalHeadsetSettingsPage(content,state);return;}
   if(current=="固件更新"){OriginalGearFirmware(content,kind,state);return;}
   var body=new Grid{Margin=new Thickness(0,26,0,0)};body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(70,GridUnitType.Star)});
   var left=new StackPanel();left.Children.Add(OriginalInstalledImage(OriginalProductImage(kind),270,290));
   var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);content.Children.Add(body);
   if(current=="奥创极速网页版"){right.Children.Add(Button("奥创极速网页版",()=>OpenOfficialUrl("https://gearlink.asus.com/"),true));return;}
   if(current=="固件更新"){OriginalGearFirmware(right,kind,state);return;}
   if(current=="电量") {
    right.Children.Add(Label("电池电量",18,text));right.Children.Add(Label(GearNumber(snapshot,"batteryPercent"," %"),34,accent));if(OriginalStateFlag(snapshot,"charging")==true)right.Children.Add(Label("正在充电",13,muted));
    AddGearMappedChoice(right,kind,state,"sleepMinutes","睡眠模式",kind=="headset"?new[]{"2","3","5","10","15","永不"}:new[]{"1","2","3","5","10","永不"},kind=="headset"?new[]{2,3,5,10,15,0}:new[]{1,2,3,5,10,0});
    AddGearMappedChoice(right,kind,state,"lowBatteryThreshold","低电量提示",new[]{"永不","10%","20%","30%","40%","50%"},new[]{0,10,20,30,40,50});if(kind=="headset")AddGearFlag(right,kind,state,"lowBatteryVoicePrompt","低电量语音提示");return;
   }
   if(current=="性能"&&kind=="keyboard") {AddGearChoice(right,kind,state,"pollingRate","轮询率",new[]{"1000","8000"});return;}
   if(current=="音效"&&kind=="headset") {
    right.Children.Add(Label("音效优化处理",18,text));var subtabs=new WrapPanel();foreach(string label in new[]{"均衡器","优化","音效增强"}){string next=label;var tab=Button(label,()=>{headsetAudioTab=next;ShowPage(page);});tab.Foreground=label==headsetAudioTab?accent:muted;tab.Background=System.Windows.Media.Brushes.Transparent;tab.BorderThickness=new Thickness(0);tab.Margin=new Thickness(0,16,28,20);subtabs.Children.Add(tab);}right.Children.Add(subtabs);if(headsetAudioTab!="均衡器"){right.Children.Add(Paragraph("功能暂不可用",13,muted));return;}
    AddGearFlag(right,kind,state,"eqEnabled","均衡器");var gains=GearArray(snapshot,"eqLevels");string[] bands={"125","250","500","1K","2K","4K","8K","16K"};var fields=new Grid{Margin=new Thickness(0,12,0,12)};for(int i=0;i<8;i++)fields.ColumnDefinitions.Add(new ColumnDefinition());var gainSliders=new List<Slider>();
    for(int i=0;i<8;i++){bool known=gains!=null&&i<gains.Length&&gains[i]!=null;var column=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};var value=Label(known?Convert.ToString(gains[i],CultureInfo.InvariantCulture):"—",12,muted);value.TextAlignment=TextAlignment.Center;column.Children.Add(value);var slider=new Slider{Orientation=Orientation.Vertical,Minimum=0,Maximum=100,TickFrequency=1,IsSnapToTickEnabled=true,Height=200,Width=36,Value=known?Convert.ToDouble(gains[i],CultureInfo.InvariantCulture):0,IsEnabled=known&&GearCanSet(state,"eqLevels"),Margin=new Thickness(0,4,0,18),ToolTip="0 至 100 · 中点 50"};slider.ValueChanged+=(s,e)=>value.Text=((int)slider.Value).ToString(CultureInfo.InvariantCulture);gainSliders.Add(slider);column.Children.Add(slider);column.Children.Add(Label(bands[i],12,text));Grid.SetColumn(column,i);fields.Children.Add(column);}right.Children.Add(fields);
    var apply=Button("应用",async()=>await SetGearSetting(kind,state,"eqLevels",gainSliders.Select(v=>(int)v.Value).ToArray()),true);apply.IsEnabled=CanApplyGear(state,"eqLevels");right.Children.Add(apply);return;
   }
   if(current=="麦克风"&&kind=="headset") {OriginalHeadsetWindowsAudio(right,true);AddGearFlag(right,kind,state,"noiseReductionEnabled","麦克风降噪");AddGearMappedChoice(right,kind,state,"noiseReductionRaw","降噪强度",new[]{"低","中","高"},new[]{0,1,2});return;}
   if(current=="设置"&&kind=="headset") {OriginalHeadsetWindowsAudio(right,false);AddGearFlag(right,kind,state,"sideToneEnabled","麦克风监听");AddGearNumber(right,kind,state,"sideToneLevel","监听音量",0,20);AddGearMappedChoice(right,kind,state,"voicePrompt","语音提示",new[]{"提示音","英文","中文"},new[]{0,1,2});AddGearChoice(right,kind,state,"latencyMs","延迟（毫秒）",new[]{"40","60","80"});return;}
   if(current=="灯光") {
    if(kind!="mouse")AddGearFlag(right,kind,state,"lightingEnabled","灯光");if(kind=="headset"){AddGearFlag(right,kind,state,"auraEnabled","神光同步");AddGearMappedChoice(right,kind,state,"lightingEffect","灯效",new[]{"恒亮","呼吸","闪烁","彩色循环","彩虹"},new[]{1,2,3,4,5},"lightingEffectCode");string[] levels=Enumerable.Range(0,21).Select(v=>(v*5).ToString(CultureInfo.InvariantCulture)).ToArray();AddGearChoice(right,kind,state,"brightness","亮度（%）",levels,"brightnessRaw");if(OriginalStateFlag(snapshot,"lightingModeMappingUnconfirmed")==true)right.Children.Add(Paragraph("灯效设置不可用",12,muted));}else{AddGearTextMappedChoice(right,kind,state,"lightingEffect","灯效",kind=="mouse"?new[]{"恒亮","呼吸","彩色循环"}:new[]{"恒亮","呼吸","彩色循环","彩虹"},kind=="mouse"?new[]{"static","breathe","cycle"}:new[]{"static","breathe","cycle","rainbow"});AddGearNumber(right,kind,state,"lightingBrightness","LED 亮度",0,100);}
    var rgb=GearArray(snapshot,kind=="headset"?"color":"lightingColor");string hex=rgb!=null&&rgb.Length==3?"#"+String.Join("",rgb.Select(v=>Convert.ToInt32(v,CultureInfo.InvariantCulture).ToString("X2",CultureInfo.InvariantCulture))):"";var input=FeatureText(hex);right.Children.Add(FeatureField("颜色",input));var apply=Button("应用",async()=>{if(!System.Text.RegularExpressions.Regex.IsMatch(input.Text,"^#[0-9a-fA-F]{6}$")){Log("请输入 #RRGGBB 格式的颜色。");return;}var values=new int[3];for(int i=0;i<3;i++)values[i]=Int32.Parse(input.Text.Substring(1+i*2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture);await SetGearSetting(kind,state,"lightingColor",values);});apply.IsEnabled=CanApplyGear(state,"lightingColor");right.Children.Add(apply);return;
   }
   if(current=="OLED"&&kind=="keyboard") {OriginalGearOled(right,state);return;}
   if(current=="拨杆"&&kind=="keyboard") {OriginalGearLever(right,state);return;}
   if(current=="Zone 模式") {OriginalGearZone(right,state);return;}
   if(current=="按键"){OriginalGearBindings(right,kind,state);return;}
   right.Children.Add(Paragraph("设置不可用",13,muted));
  }
  static Dictionary<string,object> GearMap(IDictionary<string,object> map,string key) {object value;return map!=null&&map.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
  static object[] GearArray(IDictionary<string,object> map,string key) {object value;if(map==null||!map.TryGetValue(key,out value)||value==null||value is string||!(value is IEnumerable))return null;return ((IEnumerable)value).Cast<object>().ToArray();}
  static bool GearOnline(IDictionary<string,object> state){return OriginalStateFlag(state,"online")==true;}
  static bool GearCanSet(IDictionary<string,object> state,string setting) {var list=GearArray(state,"setCapabilities");return GearOnline(state)&&list!=null&&list.Any(v=>Convert.ToString(v)==setting);}
  string GearNumber(IDictionary<string,object> map,string key,string unit){return OriginalSnapshotNumber(map,key,-1000,1000000,unit);}
  string GearFlag(IDictionary<string,object> map,string key){var flag=OriginalStateFlag(map,key);return !flag.HasValue?"未读回":flag.Value?"开":"关";}
  async Task ReadGearDevice(string kind) {
   if(busy||closing||render||DeviceHotplugPageReadsPaused)return;long discoveryGeneration=DeviceHotplugGeneration;busy=true;status.Text="正在读取设备 USB 设置…";
   try {var scan=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","enumerate"},{"kind",kind}});if(!scan.ok)throw new InvalidOperationException(SdkError(scan.error));var found=GearArray(scan.data,"devices");
    Dictionary<string,object> previous;gearStates.TryGetValue(kind,out previous);string previousId=OriginalStateText(previous,"deviceId","");
    var candidates=found==null?new Dictionary<string,object>[0]:found.OfType<Dictionary<string,object>>().OrderByDescending(d=>OriginalStateText(d,"deviceId","")==previousId).ThenByDescending(d=>OriginalStateText(d,"transport","")=="usb").Take(4).ToArray();
    if(candidates.Length==0)throw new InvalidOperationException("未找到此型号的 USB 控制接口。请使用有线或 2.4GHz 连接。");
    object readSession=gearWorker.SessionIdentity;if(readSession==null)throw new InvalidOperationException("设备读取会话已结束，请重新读取。");SdkReply reply=null;var budget=System.Diagnostics.Stopwatch.StartNew();
    foreach(var device in candidates){
     int remaining=18000-(int)budget.ElapsedMilliseconds;if(remaining<=0)throw new TimeoutException("设备读取超时。");
     reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",OriginalStateText(device,"deviceId","")}},readSession,remaining);if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));if(reply.data==null||!Object.ReferenceEquals(readSession,gearWorker.SessionIdentity))throw new InvalidOperationException("设备没有返回当前会话的有效数据。");if(GearOnline(reply.data))break;
    }
    if(DeviceHotplugGeneration!=discoveryGeneration){RequestDeviceHotplugRefresh();throw new InvalidOperationException("设备连接已改变，请重新读取。");}
    reply.data["capturedAt"]=DateTime.Now.ToString("HH:mm:ss");gearStates[kind]=reply.data;gearReadSessions[kind]=readSession;gearMessage=GearOnline(reply.data)?"":"USB 接收器已连接，设备当前未在线。";if(gearMessage.Length>0)Log(gearMessage);else status.Text="";
   }catch(Exception ex){gearStates.Remove(kind);gearReadSessions.Remove(kind);gearMessage=ex.Message;Log("读取失败："+ex.Message);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  bool CurrentGearReadSession(string kind,Dictionary<string,object> state,out object readSession) {
   readSession=null;Dictionary<string,object> current;object bound;
   if(!DeviceHotplugBindingsReady||String.IsNullOrEmpty(kind)||state==null||!gearStates.TryGetValue(kind,out current)||!Object.ReferenceEquals(current,state)||!gearReadSessions.TryGetValue(kind,out bound)||bound==null||!Object.ReferenceEquals(bound,gearWorker.SessionIdentity))return false;
   readSession=bound;return true;
  }
  async Task SetGearSetting(string kind,Dictionary<string,object> state,string setting,object value) {
   if(busy||closing||render)return;if(!PermitHardwareWrite())return;if(!GearCanSet(state,setting)){Log("该设置尚未通过此型号的接口核对。");return;}object readSession;if(!CurrentGearReadSession(kind,state,out readSession)){Log("设备读取会话已失效，请重新读取后应用。");return;}long settingGeneration=DeviceHotplugGeneration;busy=true;status.Text="正在应用设备设置…";
    try {var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","set"},{"kind",kind},{"deviceId",OriginalStateText(state,"deviceId","")},{"setting",setting},{"value",value},{"experimental",true}},readSession);if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));if(settingGeneration!=DeviceHotplugGeneration){RequestDeviceHotplugRefresh();throw new InvalidOperationException("设备连接已改变，设置结果未确认，请重新读取。");}if(reply.data!=null&&GearMap(reply.data,"snapshot")!=null&&Object.ReferenceEquals(readSession,gearWorker.SessionIdentity)){reply.data["capturedAt"]=DateTime.Now.ToString("HH:mm:ss");gearStates[kind]=reply.data;gearReadSessions[kind]=readSession;}else{gearStates.Remove(kind);gearReadSessions.Remove(kind);}Log(OriginalStateText(reply.data,"appliedState","")=="alreadySet"?"设备已是此设置，未重复发送。":OriginalStateFlag(reply.data,"writeConfirmed")==true?"设置已应用并读回确认。":"命令已发送；设备尚未确认设置读回。");}
   catch(Exception ex){gearStates.Remove(kind);gearReadSessions.Remove(kind);gearMessage="应用未完成，当前设备状态需要重新读取："+ex.Message;Log(gearMessage);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  void AddGearChoice(StackPanel target,string kind,Dictionary<string,object> state,string setting,string title,string[] options,string getter=null) {
   var snapshot=GearMap(state,"snapshot");var box=FeatureCombo(new[]{"—"}.Concat(options),OriginalStateText(snapshot,getter??setting,"—"));var row=new Grid{Margin=new Thickness(0,0,0,18)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(FeatureField(title,box));
   var apply=Button("应用",async()=>{string value=Convert.ToString(box.SelectedItem);if(box.SelectedIndex<=0||!options.Contains(value))return;int number;await SetGearSetting(kind,state,setting,Int32.TryParse(value,out number)?(object)number:value);});apply.VerticalAlignment=VerticalAlignment.Bottom;apply.IsEnabled=CanApplyGear(state,setting);Grid.SetColumn(apply,1);row.Children.Add(apply);target.Children.Add(row);
  }
  void AddGearMappedChoice(StackPanel target,string kind,Dictionary<string,object> state,string setting,string title,string[] labels,int[] values,string getter=null) {
   var snapshot=GearMap(state,"snapshot");int current;int index=Int32.TryParse(OriginalStateText(snapshot,getter??setting,""),out current)?Array.IndexOf(values,current):-1;var box=FeatureCombo(new[]{"—"}.Concat(labels),index<0?"—":labels[index]);var row=new Grid{Margin=new Thickness(0,0,0,18)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(FeatureField(title,box));var apply=Button("应用",async()=>{if(box.SelectedIndex>0)await SetGearSetting(kind,state,setting,values[box.SelectedIndex-1]);});apply.VerticalAlignment=VerticalAlignment.Bottom;apply.IsEnabled=CanApplyGear(state,setting);Grid.SetColumn(apply,1);row.Children.Add(apply);target.Children.Add(row);
  }
  void AddGearTextMappedChoice(StackPanel target,string kind,Dictionary<string,object> state,string setting,string title,string[] labels,string[] values) {
   var snapshot=GearMap(state,"snapshot");int index=Array.IndexOf(values,OriginalStateText(snapshot,setting,""));var box=FeatureCombo(new[]{"—"}.Concat(labels),index<0?"—":labels[index]);var row=new Grid{Margin=new Thickness(0,0,0,18)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(FeatureField(title,box));
   var apply=Button("应用",async()=>{if(box.SelectedIndex>0&&box.SelectedIndex<=values.Length)await SetGearSetting(kind,state,setting,values[box.SelectedIndex-1]);});apply.VerticalAlignment=VerticalAlignment.Bottom;apply.IsEnabled=CanApplyGear(state,setting);Grid.SetColumn(apply,1);row.Children.Add(apply);target.Children.Add(row);
  }
  void AddGearNumber(StackPanel target,string kind,Dictionary<string,object> state,string setting,string title,double min,double max,object explicitValue=null,Func<int,Task> action=null) {
   var snapshot=GearMap(state,"snapshot");var box=FeatureText(explicitValue!=null?Convert.ToString(explicitValue,CultureInfo.InvariantCulture):OriginalStateText(snapshot,setting,""));var row=new Grid{Margin=new Thickness(0,0,0,18)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(FeatureField(title,box));
   var apply=Button("应用",async()=>{int number;if(!Int32.TryParse(box.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out number)||number<min||number>max){Log("请输入 "+min+" 至 "+max+" 之间的整数。");return;}if(action!=null)await action(number);else await SetGearSetting(kind,state,setting,number);});apply.VerticalAlignment=VerticalAlignment.Bottom;apply.IsEnabled=CanApplyGear(state,action==null?setting:"dpiStage");Grid.SetColumn(apply,1);row.Children.Add(apply);target.Children.Add(row);
  }
  void AddGearFlag(StackPanel target,string kind,Dictionary<string,object> state,string setting,string title) {
   var snapshot=GearMap(state,"snapshot");var check=new CheckBox{Content=title,IsThreeState=true,IsChecked=OriginalStateFlag(snapshot,setting),Foreground=text,FontSize=14,Margin=new Thickness(0,10,16,10)};var row=Row();row.Margin=new Thickness(0,0,0,16);row.Children.Add(check);var apply=Button("应用",async()=>{if(check.IsChecked.HasValue)await SetGearSetting(kind,state,setting,check.IsChecked.Value);});apply.IsEnabled=CanApplyGear(state,setting);row.Children.Add(apply);target.Children.Add(row);
  }
  void ExportGearSettings(string kind) {Dictionary<string,object> state;if(!gearStates.TryGetValue(kind,out state)){Log("请先读取设备。");return;}var dialog=new Microsoft.Win32.SaveFileDialog{Filter="设备配置 (*.json)|*.json",FileName=kind+"-配置.json"};if(dialog.ShowDialog(this)!=true)return;try{File.WriteAllText(dialog.FileName,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(state),new System.Text.UTF8Encoding(false));Log("设备配置已导出。");}catch(Exception ex){Log(ex.Message);}}
  void OpenOfficialUrl(string url) {try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});}catch(Exception ex){Log("打开失败："+ex.Message);}}
 }
}
