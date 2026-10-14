using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RogControl {
 public sealed class FanPoint {public int temperature {get;set;} public int percentage {get;set;} }
 public sealed class LocalFanProfile {public string Port {get;set;} public string Mode {get;set;} public int Rpm {get;set;} public FanPoint[] Nodes {get;set;} public DateTime Submitted {get;set;} public string Acknowledgement {get;set;} }
 public partial class MainWindow {
  readonly Dictionary<string,LocalFanProfile> localFanProfiles=LocalFeatureStore.Load("fan-profiles.json",new Dictionary<string,LocalFanProfile>())??new Dictionary<string,LocalFanProfile>();
  readonly Dictionary<string,string> fanAliases=LocalFeatureStore.Load("fan-names.json",new Dictionary<string,string>())??new Dictionary<string,string>();
  string fanControlPort="1";string fanControlMode="smart";FanPoint[] fanDraft;int fixedRpmDraft;string nativeSettingsMessage="";
  readonly Dictionary<string,SharedWriteReadBinding> sdkReadBindings=new Dictionary<string,SharedWriteReadBinding>(StringComparer.Ordinal);
  void RememberSdkRead(string kind,string id,Dictionary<string,object> snapshot,WorkerClient client,long generation) {
   string key=kind+":"+id;sdkReadBindings.Remove(key);
   if(closing||render||generation!=DeviceHotplugGeneration||client.SessionIdentity==null)throw new InvalidOperationException("设备读取连接已改变，请重新读取。");
   snapshot["capturedAt"]=DateTime.Now.ToString("o");sdkSnapshots[key]=snapshot;
   if(OriginalStateText(snapshot,"deviceId","")==id&&OriginalStateFlag(snapshot,"readbackConfirmed")==true&&OriginalStateFlag(snapshot,"deviceAlive")==true)
    sdkReadBindings[key]=new SharedWriteReadBinding(snapshot,client.SessionIdentity,generation,"deviceId",id,null,kind);
   if(kind=="lcd"&&id=="7299")BindLcdMonitorRead(snapshot,client.SessionIdentity,()=>client.SessionIdentity);
  }
  void ForgetSdkRead(string kind,string id){sdkReadBindings.Remove(kind+":"+id);sdkSnapshots.Remove(kind+":"+id);if(kind=="lcd"&&id=="7299")ResetLcdMonitorSensorDraft();}
  bool SdkSettingSupported(string kind,Dictionary<string,object> state,string setting,Dictionary<string,object> request) {
   if(OriginalStateFlag(state,"readbackConfirmed")!=true||OriginalStateFlag(state,"deviceAlive")!=true||OriginalStateFlag(state,"supportsWrite")!=true)return false;
   var settings=GearArray(state,"supportedSettings");if(settings==null||!settings.Any(value=>value is string&&(string)value==setting))return false;
   if(kind=="lcd") {
    switch(setting) {
     case "powerS0":case "powerS5":return OriginalStateFlag(state,setting).HasValue&&OriginalStateFlag(request,"value").HasValue;
     case "brightness":int? brightness=MouseInteger(request,"value",10,100);return OriginalStateFlag(state,"brightnessWritable")==true&&brightness.HasValue&&brightness.Value%10==0;
     case "standby":return OriginalStateFlag(state,"standbyWritable")==true;
     case "presetId":return OriginalStateFlag(state,"presetWritable")==true;
     case "uploadImage":return OriginalStateFlag(state,"imageUploadWritable")==true;
     case "selectMedia":return OriginalStateFlag(state,"mediaSelectWritable")==true;
     case "deleteMedia":return OriginalStateFlag(state,"mediaDeleteWritable")==true;
     case "hardwareMonitor":string[] sensors;object session;var ids=GearArray(request,"sensorIds");int? background=MouseInteger(request,"backgroundId",8,11);return OriginalStateFlag(state,"hardwareMonitorWritable")==true&&background.HasValue&&background.Value==lcdMonitorBackground&&lcdMonitorDraft.TryBuild(state,background.Value,lcdMonitorInfoCount,out sensors,out session)&&ids!=null&&ids.All(value=>value is string)&&sensors.SequenceEqual(ids.Cast<string>());
     default:return false;
    }
   }
   if(kind!="fan")return false;
   string flag=setting=="smart"?"supportsSmartCurve":setting=="fixed"?"supportsFixedRpm":setting=="fanCount"?"supportsFanCount":setting=="lighting"?"supportsLighting":setting=="disconnectLightingChain"?"supportsDisconnectLightingChain":setting=="temperatureSources"?"supportsTemperatureSources":null;
   if(flag==null||OriginalStateFlag(state,flag)!=true)return false;
   if(setting=="smart"||setting=="fixed") {var ports=GearArray(state,"ports");string port=OriginalStateText(request,"portId","");var candidates=ports==null?new Dictionary<string,object>[0]:ports.OfType<Dictionary<string,object>>().Where(item=>OriginalStateText(item,"portId","")==port).ToArray();return candidates.Length==1&&OriginalStateFlag(candidates[0],"supportsWrite")==true&&OriginalStateFlag(candidates[0],flag)==true;}
   return true;
  }
  void DemandSdkWrite(string kind,string id,string setting,Dictionary<string,object> request,SharedWriteReadBinding binding,WorkerClient client) {
   Dictionary<string,object> current;SharedWriteReadBinding receipt;sdkSnapshots.TryGetValue(kind+":"+id,out current);sdkReadBindings.TryGetValue(kind+":"+id,out receipt);
   if(!Object.ReferenceEquals(binding,receipt))throw new InvalidOperationException("设备读取已更新，请重新选择设置。");
   binding.Demand(current,client.SessionIdentity,DeviceHotplugGeneration,DeviceHotplugBindingsReady,closing,render,AuraSyncBusy,busy,hardwareControlPolicy.Demand,()=>SdkSettingSupported(kind,current,setting,request),System.Threading.CancellationToken.None);
  }
  static bool ValidFanPoints(FanPoint[] points){if(points==null||points.Length!=4)return false;for(int i=0;i<4;i++){var p=points[i];if(p==null||p.temperature<0||p.temperature>100||p.percentage<0||p.percentage>100||i==0&&p.temperature!=0||i>0&&(p.temperature<=points[i-1].temperature||p.percentage<points[i-1].percentage))return false;}return true;}
  static FanPoint[] ParseFanPoints(IList<TextBox> temperatures,IList<TextBox> duties){var points=new FanPoint[4];for(int i=0;i<4;i++){int temp,duty;if(!Int32.TryParse(temperatures[i].Text,out temp)||!Int32.TryParse(duties[i].Text,out duty))return null;points[i]=new FanPoint{temperature=temp,percentage=duty};}return ValidFanPoints(points)?points:null;}
  void SaveLocalFanState(){try{LocalFeatureStore.Save("fan-profiles.json",localFanProfiles);}catch(Exception ex){Log("设备命令已完成，本地配置保存失败："+ex.Message);}}
  async Task<Dictionary<string,object>> SetOriginalNative(string kind,string id,string setting,Dictionary<string,object> arguments,object expectedWorkerSession=null) {
   if(busy||AuraSyncBusy||closing||render)return null;if(!PermitHardwareWrite())return null;busy=true;status.Text="正在应用设备设置…";ShowPage(page);
   Dictionary<string,object> result=null;
   try {var client=Sdk(kind);SharedWriteReadBinding binding;if(!sdkReadBindings.TryGetValue(kind+":"+id,out binding)||expectedWorkerSession!=null&&!Object.ReferenceEquals(expectedWorkerSession,binding.Session))throw new InvalidOperationException("请重新读取原设备后应用。");var json=new System.Web.Script.Serialization.JavaScriptSerializer();var request=json.Deserialize<Dictionary<string,object>>(json.Serialize(arguments));request["op"]="set";request["kind"]=kind;request["deviceId"]=id;request["setting"]=setting;DemandSdkWrite(kind,id,setting,request,binding,client);var reply=await client.Request<SdkReply>(request,binding.Session,kind=="lcd"?25000:15000,()=>DemandSdkWrite(kind,id,setting,request,binding,client));if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));if(closing||binding.Generation!=DeviceHotplugGeneration||!Object.ReferenceEquals(binding.Session,client.SessionIdentity))throw new InvalidOperationException("设备连接已改变，设置结果未确认。");if(reply.data==null||OriginalStateText(reply.data,"deviceId","")!=id||OriginalStateText(reply.data,"setting","")!=setting)throw new InvalidOperationException("设备设置读回目标不匹配。");result=reply.data;
    string state=OriginalStateText(result,"appliedState","unconfirmed");nativeSettingsMessage=state=="verified"?"设置已应用并读回确认。":state=="alreadySet"?"当前设置已经相同，没有发送写入。":state=="mismatch"?"设备读回值与请求不同，请检查设备状态。":OriginalStateFlag(result,"hardwareAcknowledged")==true?"设备已接受命令；当前设置无法读回确认。":"接口调用已完成，设备尚未确认命令生效。";
    if(OriginalStateText(result,"readbackScope","")=="configuredProfile"&&OriginalStateFlag(result,"profileReadbackConfirmed")==true)nativeSettingsMessage="显示配置已读回确认；请查看实体屏幕确认图像。";
    object warning;if(result!=null&&result.TryGetValue("verificationWarning",out warning))nativeSettingsMessage+=" "+Convert.ToString(warning);
    // Keep observed settings separate from the last requested software profile.
    var read=await client.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",id}},binding.Session,kind=="lcd"?25000:15000,()=>{if(closing||render||binding.Generation!=DeviceHotplugGeneration)throw new OperationCanceledException();});if(read.ok&&read.data!=null&&OriginalStateText(read.data,"deviceId","")==id&&OriginalStateFlag(read.data,"readbackConfirmed")==true)RememberSdkRead(kind,id,read.data,client,binding.Generation);else ForgetSdkRead(kind,id);Log(nativeSettingsMessage);
   }catch(Exception ex){ForgetSdkRead(kind,id);nativeSettingsMessage="应用未完成，当前设备状态需要重新读取："+ex.Message;Log(nativeSettingsMessage);}finally{busy=false;if(!closing)ShowPage(page);}return result;
  }
  void OpenLcdSettings() {
   if(busy||closing||render)return;
   Dictionary<string,object> snapshot;sdkSnapshots.TryGetValue("lcd:7299",out snapshot);
   var dialog=new Window{Title="设置",Owner=this,Width=485,Height=677,MinWidth=240,MinHeight=320,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Brush("#111111"),Foreground=text,FontFamily=FontFamily,ResizeMode=ResizeMode.NoResize};
   bool active=true,applying=false;Action rebuild=null;
   Action cancel=()=>{if(active&&!applying)dialog.Close();};
   Action<string,Dictionary<string,object>> apply=async(setting,arguments)=>{
    if(!active||applying||!LcdSettingsMayApply(snapshot,setting))return;
    applying=true;dialog.IsEnabled=false;
    try{await SetOriginalNative("lcd","7299",setting,arguments);sdkSnapshots.TryGetValue("lcd:7299",out snapshot);}
    finally{applying=false;if(active&&!closing){dialog.IsEnabled=true;rebuild();}}
   };
   rebuild=()=>{var renderedSnapshot=snapshot;dialog.Content=BuildLcdSettingsBody(renderedSnapshot,setting=>LcdSettingsMayApply(renderedSnapshot,setting),apply,cancel,true);ApplyLcdSettingsDialogScale(dialog);if(dialog.IsLoaded)CenterLcdSettingsDialog(dialog);};
   dialog.SourceInitialized+=(s,e)=>ApplyLcdSettingsDialogScale(dialog);
   dialog.DpiChanged+=(s,e)=>{ApplyLcdSettingsDialogScale(dialog);dialog.Dispatcher.BeginInvoke(new Action(()=>{if(active){ApplyLcdSettingsDialogScale(dialog);CenterLcdSettingsDialog(dialog);}}),DispatcherPriority.Loaded);};
   dialog.Loaded+=(s,e)=>{ApplyLcdSettingsDialogScale(dialog);dialog.Dispatcher.BeginInvoke(new Action(()=>{if(active)CenterLcdSettingsDialog(dialog);}),DispatcherPriority.Loaded);};
   dialog.Closed+=(s,e)=>active=false;
   dialog.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){cancel();e.Handled=true;}};
   rebuild();dialog.ShowDialog();
  }
  // Only a callback for one existing setting is emitted; there is no aggregate save.
  bool LcdSettingsMayApply(Dictionary<string,object> snapshot,string setting){Dictionary<string,object> current;return !busy&&!closing&&!render&&HardwareWritesEnabled&&sdkSnapshots.TryGetValue("lcd:7299",out current)&&Object.ReferenceEquals(current,snapshot)&&LcdSettingsSupports(snapshot,setting);}
  static bool LcdSettingsObserved(IDictionary<string,object> snapshot){object id;return snapshot!=null&&snapshot.TryGetValue("deviceId",out id)&&id is string&&(string)id=="7299"&&OriginalStateFlag(snapshot,"readbackConfirmed")==true&&OriginalStateFlag(snapshot,"deviceAlive")==true&&OriginalStateFlag(snapshot,"placeholder")==false;}
  static int? LcdSettingsNumber(IDictionary<string,object> snapshot,string name,int min,int max){object raw;if(snapshot==null||!snapshot.TryGetValue(name,out raw)||!(raw is int||raw is long))return null;long value=Convert.ToInt64(raw,CultureInfo.InvariantCulture);return value>=min&&value<=max?(int?)value:null;}
  static bool LcdSettingsSupports(IDictionary<string,object> snapshot,string setting){
   if(!LcdSettingsObserved(snapshot)||OriginalStateFlag(snapshot,"supportsWrite")!=true)return false;
   object raw;var options=snapshot.TryGetValue("supportedSettings",out raw)?raw as System.Collections.IEnumerable:null;bool found=false;if(options!=null)foreach(object item in options)if(item is string&&(string)item==setting)found=true;if(!found)return false;
   if(setting=="powerS5")return true;
   if(setting=="brightness"||setting=="standby")return OriginalStateFlag(snapshot,setting=="brightness"?"brightnessWritable":"standbyWritable")==true&&LcdSettingsNumber(snapshot,"brightnessMin",10,10)==10&&LcdSettingsNumber(snapshot,"brightnessMax",100,100)==100&&LcdSettingsNumber(snapshot,"brightnessStep",10,10)==10;
   return false;
  }
  static string LcdSettingsFirmware(IDictionary<string,object> snapshot){object raw;string value=snapshot!=null&&snapshot.TryGetValue("firmware",out raw)?raw as string:null;return LcdSettingsObserved(snapshot)&&value!=null&&value.Length==4&&value!="0000"&&value.All(c=>Uri.IsHexDigit(c))?value:"—";}
  void ApplyLcdSettingsDialogScale(Window dialog){
   var source=PresentationSource.FromVisual(dialog);if(source==null||source.CompositionTarget==null)return;var matrix=source.CompositionTarget.TransformToDevice;double sx=matrix.M11,sy=matrix.M22;Size size;if(!TryLcdSettingsDialogBounds(SystemParameters.WorkArea.Size,sx,sy,out size))return;
   var root=dialog.Content as FrameworkElement;if(root==null)return;var transform=new ScaleTransform(1/sx,1/sy);transform.Freeze();root.LayoutTransform=transform;
   // WorkArea is in device-independent units; only this owned popup is sized.
   dialog.MinWidth=240/sx;dialog.MinHeight=320/sy;dialog.Width=size.Width;dialog.Height=size.Height;
  }
  static bool TryLcdSettingsDialogBounds(Size available,double sx,double sy,out Size size){size=new Size();if(!OriginalPixelSizing.ScaleValid(sx)||!OriginalPixelSizing.ScaleValid(sy)||!OriginalPixelSizing.PixelsValid(available.Width)||!OriginalPixelSizing.PixelsValid(available.Height))return false;size=new Size(Math.Min(485/sx,Math.Max(240/sx,available.Width-16)),Math.Min(677/sy,Math.Max(320/sy,available.Height-16)));return true;}
  void CenterLcdSettingsDialog(Window dialog){
   if(closing||!IsLoaded||!dialog.IsLoaded)return;var root=Content as Grid;var source=PresentationSource.FromVisual(dialog);if(root==null||root.ActualWidth<=0||root.ActualHeight<=0||source==null||source.CompositionTarget==null)return;
   var matrix=source.CompositionTarget.TransformToDevice;if(!OriginalPixelSizing.ScaleValid(matrix.M11)||!OriginalPixelSizing.ScaleValid(matrix.M22))return;
   double sidebar=root.ColumnDefinitions.Count==0?0:root.ColumnDefinitions[0].ActualWidth;
   // Only our own visuals are used. Center after the final physical size is
   // laid out, within the page canvas to the right of the navigation column.
   Point desired=root.PointToScreen(new Point((root.ActualWidth+sidebar)/2,root.ActualHeight/2)),current=dialog.PointToScreen(new Point(dialog.ActualWidth/2,dialog.ActualHeight/2));
   double left=dialog.Left+(desired.X-current.X)/matrix.M11,top=dialog.Top+(desired.Y-current.Y)/matrix.M22;if(FinitePreviewNumber(left)&&FinitePreviewNumber(top)){dialog.Left=left;dialog.Top=top;}
  }
  FrameworkElement BuildLcdSettingsBody(IDictionary<string,object> snapshot,Func<string,bool> mayApply,Action<string,Dictionary<string,object>> apply,Action cancel,bool installedAssets){
   bool observed=LcdSettingsObserved(snapshot);int? bright=observed&&OriginalStateFlag(snapshot,"brightnessReadable")==true?LcdSettingsNumber(snapshot,"brightness",10,100):null;
   var surface=new Grid{Tag="LcdSettings.Surface",Background=Brush("#111111"),Width=483,Height=675};surface.RowDefinitions.Add(new RowDefinition{Height=new GridLength(44)});surface.RowDefinitions.Add(new RowDefinition{Height=new GridLength(551)});surface.RowDefinitions.Add(new RowDefinition{Height=new GridLength(80)});
   var title=LcdSettingsLabel("设置",16,text);title.Margin=new Thickness(32,9,0,0);surface.Children.Add(title);
   var body=new StackPanel{Width=414,HorizontalAlignment=HorizontalAlignment.Left};var canvas=new Canvas{Tag="LcdSettings.OriginalBody",Width=414,Height=523};body.Children.Add(canvas);
   LcdSettingsPlace(canvas,LcdSettingsLabel("亮度",16,text),0,6);
   var value=LcdSettingsLabel(bright.HasValue?bright.Value+"%":"—",14,bright.HasValue?text:muted);value.Tag="LcdSettings.BrightnessValue";value.Width=70;value.TextAlignment=TextAlignment.Right;LcdSettingsPlace(canvas,value,344,6);
   var brightness=new Slider{Tag="LcdSettings.Brightness",Minimum=10,Maximum=100,Value=bright??10,TickFrequency=10,IsSnapToTickEnabled=true,Height=27,Width=414,IsEnabled=false,Template=LcdSettingsSliderTemplate(bright.HasValue),ToolTip=bright.HasValue?"已读取亮度":"当前亮度未知；可在附加设置中手动应用亮度。"};LcdSettingsPlace(canvas,brightness,0,32);
   LcdSettingsPlace(canvas,LcdSettingsLabel("当系统处于睡眠、休眠或关机状态",16,text),0,77);
   var sleeping=LcdSettingsSwitch(null);sleeping.Tag="LcdSettings.SleepPower";sleeping.ToolTip="睡眠与休眠电源状态未知；关机（S5）可在附加设置中独立应用。";LcdSettingsPlace(canvas,sleeping,362,77);
   LcdSettingsPlace(canvas,LcdSettingsLabel("预设桌布",12,muted),0,136);
   for(int i=0;i<2;i++){var slot=new Border{Tag="LcdSettings.Wallpaper."+i,Width=64,Height=112,BorderBrush=muted,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Background=Brush("#151515"),IsEnabled=false,ToolTip="尚未接通睡眠、休眠或关机桌布设置。"};if(installedAssets)slot.Child=OriginalInstalledImage(System.IO.Path.Combine(OriginalLcdAssets,"bg_anim","x870_launch"+(i+1).ToString(CultureInfo.InvariantCulture)+".gif"),62,110);LcdSettingsPlace(canvas,slot,i*92,155);}
   LcdSettingsPlace(canvas,LcdSettingsLabel("温度警告",16,text),0,353);var alert=LcdSettingsSwitch(null);alert.Tag="LcdSettings.TemperatureAlert";alert.ToolTip="当前温度警告状态未知，设置接口尚未接通。";LcdSettingsPlace(canvas,alert,89,355);
   LcdSettingsPlace(canvas,LcdSettingsLabel("温度",16,text),0,385);var unknown=LcdSettingsLabel("—",14,muted);unknown.Tag="LcdSettings.TemperatureValue";unknown.Width=50;unknown.TextAlignment=TextAlignment.Right;LcdSettingsPlace(canvas,unknown,364,385);
   for(int i=0;i<6;i++){var tick=LcdSettingsLabel((75+i*5)+" °C",11,muted);tick.Tag="LcdSettings.TemperatureTick."+i;tick.Width=52;tick.TextAlignment=i==0?TextAlignment.Left:i==5?TextAlignment.Right:TextAlignment.Center;LcdSettingsPlace(canvas,tick,i*(414-52)/5.0,414);var dot=new Ellipse{Width=5,Height=5,Fill=muted};LcdSettingsPlace(canvas,dot,1+i*409/5.0,439);}
   var threshold=new Slider{Tag="LcdSettings.Temperature",Minimum=75,Maximum=100,Value=75,TickFrequency=5,IsSnapToTickEnabled=true,Height=27,Width=414,IsEnabled=false,Template=LcdSettingsSliderTemplate(false),ToolTip="温度阈值未知"};LcdSettingsPlace(canvas,threshold,0,445);
   LcdSettingsPlace(canvas,LcdSettingsLabel("固件更新",16,text),-5,469);var version=LcdSettingsLabel("当前版本：  "+LcdSettingsFirmware(snapshot),16,text);version.Tag="LcdSettings.Firmware";version.Width=414;version.TextTrimming=TextTrimming.CharacterEllipsis;LcdSettingsPlace(canvas,version,-5,497);
   var extra=new Expander{Header="附加设置",Tag="LcdSettings.Additional",Foreground=muted,FontSize=12,Margin=new Thickness(0),IsExpanded=false};var commands=new StackPanel{Margin=new Thickness(0,8,0,10)};extra.Content=commands;body.Children.Add(extra);
   var levels=MacroCombo(126);foreach(string level in new[]{"请选择亮度","10","20","30","40","50","60","70","80","90","100"})levels.Items.Add(level);levels.SelectedIndex=0;levels.Margin=new Thickness(0);levels.Tag="LcdSettings.ManualBrightness";levels.Width=126;levels.Height=28;levels.IsEnabled=mayApply("brightness")||mayApply("standby");levels.ToolTip="选择要应用的亮度；此值不会冒充当前设备亮度。";
   var brightApply=LcdSettingsApplyButton("应用亮度","LcdSettings.ApplyBrightness");brightApply.IsEnabled=false;brightApply.ToolTip="只应用所选亮度；关机电源是否开启由设备读回决定。";
   var brightRow=new Grid{Margin=new Thickness(0,0,0,12)};brightRow.ColumnDefinitions.Add(new ColumnDefinition());brightRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(126)});brightRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(118)});brightRow.Children.Add(LcdSettingsLabel("亮度",13,text));Grid.SetColumn(levels,1);brightRow.Children.Add(levels);Grid.SetColumn(brightApply,2);brightRow.Children.Add(brightApply);commands.Children.Add(brightRow);
   var s5=new CheckBox{Tag="LcdSettings.ManualS5",Content="关机后屏幕开启（S5）",IsThreeState=true,IsChecked=observed?OriginalStateFlag(snapshot,"powerS5"):null,Foreground=text,FontSize=13,IsEnabled=mayApply("powerS5"),VerticalAlignment=VerticalAlignment.Center};
   var powerApply=LcdSettingsApplyButton("应用电源设置","LcdSettings.ApplyS5");powerApply.IsEnabled=s5.IsChecked.HasValue&&mayApply("powerS5");powerApply.ToolTip="只应用关机（S5）电源；不会改变未知睡眠和休眠设置。";LcdSettingsCommandRow(commands,s5,powerApply);
   var standby=new CheckBox{Tag="LcdSettings.ManualStandby",Content="启用待机调暗",IsThreeState=true,IsChecked=observed&&OriginalStateFlag(snapshot,"standbyReadable")==true?OriginalStateFlag(snapshot,"standbyEnabled"):null,Foreground=text,FontSize=13,IsEnabled=mayApply("standby"),VerticalAlignment=VerticalAlignment.Center};
   var standbyApply=LcdSettingsApplyButton("应用待机设置","LcdSettings.ApplyStandby");standbyApply.IsEnabled=false;standbyApply.ToolTip="只应用待机调暗与所选亮度；当前待机状态无法读取。";LcdSettingsCommandRow(commands,standby,standbyApply);
   Func<int?> selected=()=>{int parsed;return Int32.TryParse(Convert.ToString(levels.SelectedItem,CultureInfo.InvariantCulture),out parsed)&&parsed>=10&&parsed<=100&&parsed%10==0?(int?)parsed:null;};
   Action changed=()=>{brightApply.IsEnabled=selected().HasValue&&mayApply("brightness");powerApply.IsEnabled=s5.IsChecked.HasValue&&mayApply("powerS5");standbyApply.IsEnabled=standby.IsChecked.HasValue&&selected().HasValue&&mayApply("standby");};levels.SelectionChanged+=(s,e)=>changed();s5.Checked+=(s,e)=>changed();s5.Unchecked+=(s,e)=>changed();s5.Indeterminate+=(s,e)=>changed();standby.Checked+=(s,e)=>changed();standby.Unchecked+=(s,e)=>changed();standby.Indeterminate+=(s,e)=>changed();
   brightApply.Click+=(s,e)=>{int? chosen=selected();if(chosen.HasValue&&mayApply("brightness"))apply("brightness",new Dictionary<string,object>{{"value",chosen.Value}});};
   powerApply.Click+=(s,e)=>{if(s5.IsChecked.HasValue&&mayApply("powerS5"))apply("powerS5",new Dictionary<string,object>{{"value",s5.IsChecked.Value}});};
   standbyApply.Click+=(s,e)=>{int? chosen=selected();if(standby.IsChecked.HasValue&&chosen.HasValue&&mayApply("standby"))apply("standby",new Dictionary<string,object>{{"enabled",standby.IsChecked.Value},{"brightness",chosen.Value}});};
   var viewport=new ScrollViewer{Tag="LcdSettings.Viewport",Content=body,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(35,0,31,0)};Grid.SetRow(viewport,1);surface.Children.Add(viewport);
   var footer=new Canvas{Height=80};var save=LcdSettingsApplyButton("保存","LcdSettings.Save");save.Width=132;save.Height=32;save.Margin=new Thickness(0);save.IsEnabled=false;save.ToolTip="整体保存尚未接通；已支持的设置请在附加设置中分别应用。";ToolTipService.SetShowOnDisabled(save,true);LcdSettingsPlace(footer,save,168,6);var cancelButton=LcdSettingsApplyButton("取消","LcdSettings.Cancel");cancelButton.Width=132;cancelButton.Height=32;cancelButton.Margin=new Thickness(0);cancelButton.Click+=(s,e)=>cancel();LcdSettingsPlace(footer,cancelButton,320,6);
   if(!String.IsNullOrWhiteSpace(nativeSettingsMessage)){var message=LcdSettingsLabel(nativeSettingsMessage,11,muted);message.Tag="LcdSettings.Result";message.Width=414;message.TextTrimming=TextTrimming.CharacterEllipsis;message.ToolTip=nativeSettingsMessage;LcdSettingsPlace(footer,message,35,48);}Grid.SetRow(footer,2);surface.Children.Add(footer);
   var frame=new Border{Tag="LcdSettings.Frame",Width=485,Height=677,BorderBrush=Brush("#737373"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Child=surface};return new ScrollViewer{Tag="LcdSettings.FrameViewport",Content=frame,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=Brush("#111111")};
  }
  static void LcdSettingsPlace(Canvas canvas,FrameworkElement item,double left,double top){Canvas.SetLeft(item,left);Canvas.SetTop(item,top);canvas.Children.Add(item);}
  TextBlock LcdSettingsLabel(string caption,double size,Brush ink){return new TextBlock{Text=caption,FontSize=size,Foreground=ink,Margin=new Thickness(0)};}
  Button LcdSettingsApplyButton(string caption,string tag){return new Button{Content=caption,Tag=tag,Style=Resources[typeof(Button)] as Style,Width=108,Height=28,Margin=new Thickness(10,0,0,0),Padding=new Thickness(6,2,6,2),FontSize=13,Background=Brush("#202020"),Foreground=text,BorderBrush=muted,BorderThickness=new Thickness(1)};}
  void LcdSettingsCommandRow(StackPanel target,CheckBox field,Button command){var row=new Grid{Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(118)});row.Children.Add(field);Grid.SetColumn(command,1);row.Children.Add(command);target.Children.Add(row);}
  Border LcdSettingsSwitch(bool? value){var row=new Grid{Width=52,Height=20,Background=value.HasValue?Brush("#E4E4E4"):Brush("#555555")};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(15)});row.Children.Add(new TextBlock{Text=value.HasValue?(value.Value?"ON":"OFF"):"—",FontSize=11,Foreground=value.HasValue?Brush("#171717"):muted,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});var marker=new Border{Width=12,Height=14,Background=Brush("#151515"),Margin=new Thickness(0,0,3,0)};Grid.SetColumn(marker,1);row.Children.Add(marker);return new Border{Width=52,Height=20,CornerRadius=new CornerRadius(3),Background=row.Background,Child=row,IsEnabled=false};}
  ControlTemplate LcdSettingsSliderTemplate(bool known){string source="<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Slider}'><Grid><Border Height='4' Background='#555555' VerticalAlignment='Center'/><Track x:Name='PART_Track' Height='27'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='#D02C46' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='11' Height='17'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Polygon Fill='#EEEEEE' Points='0,5 5.5,0 11,5 11,17 0,17'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Height='4' Background='#555555' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>";if(!known)source=source.Replace("<Track x:Name='PART_Track' Height='27'>","<Track x:Name='PART_Track' Height='27' Visibility='Collapsed'>");return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(source);}
  string FanAlias(string port){string name;return fanAliases.TryGetValue(port,out name)&&!String.IsNullOrWhiteSpace(name)?name:"端口 "+port;}
  void OriginalControllerSettings(StackPanel target,IDictionary<string,object> snapshot) {
   var ports=new WrapPanel{Margin=new Thickness(0,22,0,15)};for(int i=1;i<=4;i++){string id=i.ToString();var button=Button(FanAlias(id),()=>{fanControlPort=id;fanDraft=null;ShowPage(page);});button.Foreground=id==fanControlPort?accent:text;ports.Children.Add(button);}target.Children.Add(ports);
   var port=OriginalSnapshotItem(snapshot,"ports","portId",fanControlPort);var limits=GearMap(port,"capability");bool smart=OriginalStateFlag(port,"supportsSmartCurve")==true, fixedAllowed=OriginalStateFlag(port,"supportsFixedRpm")==true;
   target.Children.Add(Label("Current RPM    "+OriginalSnapshotNumber(port,"rpm",0,50000,"RPM"),17,accent));
   var local=localFanProfiles.ContainsKey(fanControlPort)?localFanProfiles[fanControlPort]:null;
   if(fanDraft==null){fanDraft=local!=null&&ValidFanPoints(local.Nodes)?local.Nodes.Select(p=>new FanPoint{temperature=p.temperature,percentage=p.percentage}).ToArray():new[]{new FanPoint{temperature=0,percentage=30},new FanPoint{temperature=50,percentage=30},new FanPoint{temperature=70,percentage=50},new FanPoint{temperature=100,percentage=90}};fixedRpmDraft=local==null?0:local.Rpm;}
   var modes=Row();var smartRadio=new RadioButton{Content="智能模式",Foreground=text,IsChecked=fanControlMode=="smart",Margin=new Thickness(0,0,20,0)};smartRadio.Checked+=(s,e)=>{fanControlMode="smart";ShowPage(page);};var fixedRadio=new RadioButton{Content="固定转速模式",Foreground=text,IsChecked=fanControlMode=="fixed",Margin=new Thickness(0,0,20,0)};fixedRadio.Checked+=(s,e)=>{fanControlMode="fixed";ShowPage(page);};modes.Children.Add(smartRadio);modes.Children.Add(fixedRadio);target.Children.Add(modes);
   var body=new Grid{Margin=new Thickness(0,18,0,0)};body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});body.ColumnDefinitions.Add(new ColumnDefinition());var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);target.Children.Add(body);
   left.Children.Add(FanDraftChart(fanDraft));left.Children.Add(Paragraph("图中显示编辑草稿。设备没有当前曲线读回接口。",11,muted));
   var alias=FeatureText(FanAlias(fanControlPort));right.Children.Add(FeatureField("端口名称",alias));right.Children.Add(Button("保存名称",()=>{if(String.IsNullOrWhiteSpace(alias.Text))return;fanAliases[fanControlPort]=alias.Text.Trim().Substring(0,Math.Min(60,alias.Text.Trim().Length));try{LocalFeatureStore.Save("fan-names.json",fanAliases);}catch(Exception ex){Log("名称保存失败："+ex.Message);}ShowPage(page);}));
   if(fanControlMode=="smart") {
    var presets=Row();string[] names={"静音","标准","Turbo","全速"};string[] codes={"silent","standard","turbo","fullSpeed"};for(int i=0;i<4;i++){string preset=codes[i];presets.Children.Add(Button(names[i],()=>{fanDraft=FanPreset(preset);ShowPage(page);}));}right.Children.Add(presets);
    var editor=new Grid{Margin=new Thickness(0,18,0,12)};for(int i=0;i<3;i++)editor.ColumnDefinitions.Add(new ColumnDefinition());var temps=new List<TextBox>();var duties=new List<TextBox>();editor.RowDefinitions.Add(new RowDefinition());editor.Children.Add(Label("节点",11,muted));var ht=Label("温度 °C",11,muted);Grid.SetColumn(ht,1);editor.Children.Add(ht);var hd=Label("PWM %",11,muted);Grid.SetColumn(hd,2);editor.Children.Add(hd);
    for(int i=0;i<4;i++){editor.RowDefinitions.Add(new RowDefinition());var name=Label((i+1).ToString(),12,muted);Grid.SetRow(name,i+1);editor.Children.Add(name);TextBox temperature=FeatureText(fanDraft[i].temperature.ToString()),duty=FeatureText(fanDraft[i].percentage.ToString());temps.Add(temperature);duties.Add(duty);Grid.SetRow(temperature,i+1);Grid.SetColumn(temperature,1);Grid.SetRow(duty,i+1);Grid.SetColumn(duty,2);editor.Children.Add(temperature);editor.Children.Add(duty);}right.Children.Add(editor);
    var preview=Button("预览曲线",()=>{var points=ParseFanPoints(temps,duties);if(points==null){Log("曲线需要 4 个节点：温度从 0 起严格递增，PWM 在 0–100% 内且不递减。");return;}fanDraft=points;ShowPage(page);});preview.ToolTip="仅更新编辑预览";right.Children.Add(preview);
    var apply=Button("应用并保存",async()=>{var points=ParseFanPoints(temps,duties);if(points==null){Log("曲线需要 4 个节点：温度从 0 起严格递增，PWM 在 0–100% 内且不递减。");return;}string submittedPort=fanControlPort;fanDraft=points;var response=await SetOriginalNative("fan","7576","smart",new Dictionary<string,object>{{"portId",submittedPort},{"nodes",points.Select(p=>new Dictionary<string,object>{{"temperature",p.temperature},{"percentage",p.percentage}}).ToArray()}});if(response!=null){localFanProfiles[submittedPort]=new LocalFanProfile{Port=submittedPort,Mode="smart",Nodes=points,Submitted=DateTime.Now,Acknowledgement=OriginalStateText(response,"appliedState","unconfirmed")};SaveLocalFanState();ShowPage(page);}},true);apply.IsEnabled=HardwareWritesEnabled&&smart&&!busy;right.Children.Add(apply);
   }else{
    right.Children.Add(Paragraph("有效范围："+OriginalStateText(limits,"rpmMin","—")+"–"+OriginalStateText(limits,"rpmMax","—")+" RPM",12,muted));var rpm=FeatureText(fixedRpmDraft>0?fixedRpmDraft.ToString():"");right.Children.Add(FeatureField("固定转速 RPM",rpm));var apply=Button("应用固定转速",async()=>{int value;if(!Int32.TryParse(rpm.Text,out value)){Log("请输入有效转速。");return;}string submittedPort=fanControlPort;fixedRpmDraft=value;var response=await SetOriginalNative("fan","7576","fixed",new Dictionary<string,object>{{"portId",submittedPort},{"rpm",value}});if(response!=null){localFanProfiles[submittedPort]=new LocalFanProfile{Port=submittedPort,Mode="fixed",Rpm=value,Submitted=DateTime.Now,Acknowledgement=OriginalStateText(response,"appliedState","unconfirmed")};SaveLocalFanState();ShowPage(page);}},true);apply.IsEnabled=HardwareWritesEnabled&&fixedAllowed&&!busy;right.Children.Add(apply);if(!fixedAllowed)right.Children.Add(Paragraph("此端口尚未返回校准后的转速范围，固定转速不可用。",11,muted));
   }
   if(local!=null)target.Children.Add(Paragraph("上次提交的本地配置："+local.Submitted.ToString("MM-dd HH:mm:ss")+" · "+local.Mode+" · "+local.Acknowledgement,11,muted));if(!String.IsNullOrEmpty(nativeSettingsMessage))target.Children.Add(Paragraph(nativeSettingsMessage,12,muted));
  }
  FanPoint[] FanPreset(string name){int[,] values=name=="silent"?new int[,]{{0,0},{60,30},{80,40},{100,70}}:name=="turbo"?new int[,]{{0,40},{40,40},{60,60},{90,100}}:name=="fullSpeed"?new int[,]{{0,100},{50,100},{80,100},{100,100}}:new int[,]{{0,30},{50,30},{70,50},{100,90}};return Enumerable.Range(0,4).Select(i=>new FanPoint{temperature=values[i,0],percentage=values[i,1]}).ToArray();}
  UIElement FanDraftChart(FanPoint[] points){var canvas=new Canvas{Height=280,MinWidth=250,ClipToBounds=true};canvas.SizeChanged+=(s,e)=>{FanDrawGrid(canvas,e.NewSize.Width,280,true);if(points==null)return;double w=e.NewSize.Width-40,h=233;var line=new Polyline{Stroke=accent,StrokeThickness=2};foreach(var point in points){double x=32+w*point.temperature/100.0,y=15+h*(1-point.percentage/100.0);line.Points.Add(new Point(x,y));var dot=new Ellipse{Width=8,Height=8,Fill=accent};Canvas.SetLeft(dot,x-4);Canvas.SetTop(dot,y-4);canvas.Children.Add(dot);}canvas.Children.Add(line);};return canvas;}
 }
}
