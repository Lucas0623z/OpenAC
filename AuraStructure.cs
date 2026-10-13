using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  string auraTab="Aura 灯效";bool experimentalLighting {get{return HardwareWritesEnabled;}}
  static readonly string[] AuraNames={"恒亮","呼吸","闪烁","彩色循环","彩虹","星夜","音乐","智能","同步辨色","沉浸","黑暗 (关闭)","AI 神光同步"};
  static readonly string[] AuraKeys={"static","breathe","flash","cycle","rainbow","stars","music","smart","adaptive","immersive","off","ai"};
  static readonly string[] AuraSymbols={"◉","◌","☀","◷","≋","✦","♫","♙","▣","◈","⊘","✧"};
  void OriginalAuraPage() {
   Header("灯效 & Aura Sync","");
   content.Children.Add(OriginalTabs(new[]{"同步设备","Aura 灯效","AI 神光同步"},auraTab,chosen=>{auraTab=chosen;ShowPage(page);},accent,195));
   if(auraTab=="同步设备"){OriginalAuraDevices();return;}
   if(auraTab=="AI 神光同步"){content.Children.Add(Paragraph("AI 神光同步尚未接通。",13,muted));return;}
   var body=new Grid{Margin=new Thickness(0,30,0,0)};body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(375)});
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   left.Children.Add(OriginalAuraHeading("基本灯效"));
   var grid=new Grid{Margin=new Thickness(8,26,6,12)};for(int c=0;c<6;c++)grid.ColumnDefinitions.Add(new ColumnDefinition());for(int r=0;r<2;r++)grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(100)});
   for(int i=0;i<AuraKeys.Length;i++){
    string key=AuraKeys[i];bool supported=new[]{"static","off"}.Contains(key);
    var stack=new StackPanel();var symbol=Label(AuraSymbols[i],38,effect==key?accent:muted);symbol.FontFamily=new FontFamily("Segoe UI Symbol");symbol.TextAlignment=TextAlignment.Center;symbol.Margin=new Thickness(0,0,0,8);stack.Children.Add(symbol);var name=Label(AuraNames[i],12,effect==key?accent:text);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0);stack.Children.Add(name);
    var button=Button("",()=>{effect=key;ShowPage(page);});button.Content=stack;button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(2,5,2,5);button.Margin=new Thickness(1);button.IsEnabled=supported&&!busy&&!auraSyncBusy;button.ToolTip=supported?"编辑同步灯效，点击应用后发送。":"此灯效尚无跨设备的统一帧接口，不能保证同步相位。";ToolTipService.SetShowOnDisabled(button,true);Grid.SetColumn(button,i%6);Grid.SetRow(button,i/6);grid.Children.Add(button);
   }
   left.Children.Add(grid);left.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,13,0,22)});left.Children.Add(OriginalAuraHeading("高级灯效"));
   var advanced=new Grid{Margin=new Thickness(12,22,8,10)};advanced.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});advanced.ColumnDefinitions.Add(new ColumnDefinition());advanced.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var logo=Label("△",43,text);advanced.Children.Add(logo);var profiles=new ComboBox{IsEnabled=false,Height=32,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,18,0)};profiles.Items.Add("无可用配置");profiles.SelectedIndex=0;Grid.SetColumn(profiles,1);advanced.Children.Add(profiles);var creator=Button("Aura Creator",()=>{});creator.IsEnabled=false;creator.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(creator,2);advanced.Children.Add(creator);left.Children.Add(advanced);left.Children.Add(Paragraph("使用 Aura Creator 创造多重设备组合灯效。此功能尚未接通。",12,muted));
   left.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,23,0,20)});left.Children.Add(OriginalAuraHeading("游戏灯效"));var games=Row();var gameList=Button("游戏列表",()=>{});gameList.IsEnabled=false;games.Children.Add(gameList);games.Children.Add(new CheckBox{Content="未接通",IsEnabled=false,Foreground=muted,VerticalAlignment=VerticalAlignment.Center});left.Children.Add(games);
   var reset=Button("恢复默认值",()=>{});reset.IsEnabled=false;reset.HorizontalAlignment=HorizontalAlignment.Right;reset.Margin=new Thickness(0,0,0,15);reset.ToolTip="未读取原厂默认值，无法确认恢复结果。";ToolTipService.SetShowOnDisabled(reset,true);right.Children.Add(reset);
   var settings=new StackPanel();settings.Children.Add(Label(AuraNames[Array.IndexOf(AuraKeys,effect)<0?0:Array.IndexOf(AuraKeys,effect)],20,text));settings.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,2,0,20)});settings.Children.Add(Label("色彩",15,text));
   settings.Children.Add(new RadioButton{Content="单色",IsChecked=true,Foreground=text,Margin=new Thickness(0,12,0,12),IsEnabled=false});
   var colors=new WrapPanel();foreach(string v in new[]{"#FF0000","#FFB800","#00BBFF","#FFFFFF"}){string chosen=v;var swatch=Button("",()=>{color=chosen;ShowPage(page);});swatch.Width=36;swatch.Height=34;swatch.Background=Brush(v);swatch.Margin=new Thickness(0,0,7,7);colors.Children.Add(swatch);}settings.Children.Add(colors);
   var hex=new TextBox{Text=color,Width=118,HorizontalAlignment=HorizontalAlignment.Left,Foreground=text,Background=bg,BorderBrush=border,Padding=new Thickness(6),Margin=new Thickness(0,2,0,16)};
   hex.TextChanged+=(s,e)=>color=hex.Text.Trim();hex.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){e.Handled=true;ShowPage(page);}};settings.Children.Add(hex);
   settings.Children.Add(new RadioButton{Content="渐变",Foreground=muted,IsEnabled=false,Margin=new Thickness(0,2,0,13)});settings.Children.Add(Label("颜色配置",13,muted));var gradient=Row();foreach(string v in new[]{"#00BFFF","#FFAA00"})gradient.Children.Add(new Border{Width=34,Height=32,Background=Brush(v),Margin=new Thickness(0,0,16,0),Opacity=0.45});settings.Children.Add(gradient);settings.Children.Add(Label("方向",13,muted));var direction=new ComboBox{IsEnabled=false,Height=32,Margin=new Thickness(0,2,0,20)};direction.Items.Add("未接通");direction.SelectedIndex=0;settings.Children.Add(direction);
   var apply=Button("应用同步",async()=>await ApplyAllAuraTargetsAsync(),true);apply.IsEnabled=HardwareWritesEnabled&&!busy&&!auraSyncBusy&&auraSyncSelected.Count>0;settings.Children.Add(apply);settings.Children.Add(Paragraph(HardwareWritesEnabled?"统一静态颜色；实体效果尚未验证。":HardwareControlPolicy.BlockedMessage,11,muted));right.Children.Add(new Border{BorderBrush=border,BorderThickness=new Thickness(1),Padding=new Thickness(22),MinHeight=475,Child=settings});content.Children.Add(body);if(!String.IsNullOrWhiteSpace(auraSyncMessage))content.Children.Add(Paragraph(auraSyncMessage,12,muted));
  }
  UIElement OriginalTabs(string[] names,string active,Action<string> select,Brush ink,double width) {
   var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,16)};
   foreach(string value in names){string name=value;var stack=new StackPanel{Width=width};var button=Button(name,()=>select(name));button.Background=Brushes.Transparent;button.Foreground=name==active?ink:text;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(6,13,6,15);button.Margin=new Thickness(0);button.FontSize=15;stack.Children.Add(button);stack.Children.Add(new Border{Height=2,Background=name==active?ink:border});tabs.Children.Add(stack);}return tabs;
  }
  UIElement OriginalAuraHeading(string heading){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(Label("╱╱╱",18,Brush("#B73038"),FontWeights.Bold));var label=Label(heading,18,text);label.Margin=new Thickness(11,0,0,0);row.Children.Add(label);return row;}
  void OriginalAuraDevices() {
   var commands=new WrapPanel{Margin=new Thickness(0,12,0,18)};
   var scan=Button(auraSyncBusy?"正在读取…":"重新扫描",async()=>await ScanAllAuraTargetsAsync());scan.IsEnabled=!busy&&!auraSyncBusy&&!closing&&!render;commands.Children.Add(scan);
   var all=Button("选择可同步设备",()=>{auraSyncSelected.Clear();foreach(var target in auraSyncTargets.Where(target=>target.CanStaticRgb&&target.Online))auraSyncSelected.Add(target.Id);ShowPage(page);});all.IsEnabled=!busy&&!auraSyncBusy;commands.Children.Add(all);
   var stop=Button("停止同步",async()=>await StopAllAuraTargetsAsync());stop.IsEnabled=!closing&&!render&&(!busy||auraSyncBusy);commands.Children.Add(stop);content.Children.Add(commands);
   if(auraSyncTargets.Count==0)content.Children.Add(Paragraph(auraSyncBusy?"正在读取设备灯效能力…":"尚未扫描同步设备。",12,muted));
   var cards=new WrapPanel();foreach(var target in auraSyncTargets) {
    var item=target;var box=new StackPanel();string imageKind=new[]{"motherboard","mouse","keyboard","headset","fan","receiver"}.Contains(item.Kind)?item.Kind:"";
    if(imageKind.Length!=0)box.Children.Add(OriginalInstalledImage(OriginalProductImage(imageKind),125));
    var check=new CheckBox{Content=Paragraph(item.Name,13,text),Foreground=text,IsChecked=auraSyncSelected.Contains(item.Id),IsEnabled=item.CanStaticRgb&&item.Online&&!busy&&!auraSyncBusy,Margin=new Thickness(0,12,0,10)};
    check.Checked+=(s,e)=>auraSyncSelected.Add(item.Id);check.Unchecked+=(s,e)=>auraSyncSelected.Remove(item.Id);box.Children.Add(check);
    var stateLabel=Paragraph(item.CanStaticRgb?"静态 RGB · "+(item.Transport??""):item.CanReleaseSoftwareLighting?"待接管":item.Online?"未接通":"未在线",11,item.CanStaticRgb?accent:muted);stateLabel.ToolTip=item.UnavailableReason;box.Children.Add(stateLabel);
    if(item.CanReleaseSoftwareLighting){var take=Button(item.Kind=="keyboard"?"尝试接管":"接管灯效",async()=>await TakeAuraSoftwareLightingAsync(item));take.IsEnabled=HardwareWritesEnabled&&!busy&&!auraSyncBusy&&!closing&&!render;take.ToolTip="退出已确认的软件灯控模式，读回后才能选择同步；仍无法读回时恢复原标志。扫描不会改变灯光。";ToolTipService.SetShowOnDisabled(take,true);box.Children.Add(take);}
    AuraOwnershipResult ownership;if(auraOwnershipResults.TryGetValue(item.Id,out ownership)){var state=Paragraph(ownership.State=="returned"?"原灯控标志已交还":ownership.OwnershipConfirmed?"接管已读回":ownership.RestorationConfirmed?"接管失败 · 原标志已恢复":"接管失败 · 恢复未确认",11,ownership.OwnershipConfirmed||ownership.RestorationConfirmed?green:muted);state.ToolTip=ownership.Error;box.Children.Add(state);if(ownership.CanReturnOriginalFlag){var give=Button("交还灯效",async()=>await ReturnAuraSoftwareLightingAsync(item.Id));give.IsEnabled=HardwareWritesEnabled&&!busy&&!auraSyncBusy&&!closing&&!render;give.ToolTip="只交还本次已成功接管前的原软件灯控标志；不恢复之前的颜色。";box.Children.Add(give);}}
    var result=auraSyncResult==null?null:auraSyncResult.Devices.FirstOrDefault(value=>value.TargetId==item.Id);if(result!=null)box.Children.Add(Paragraph(AuraSyncResultLabel(result),11,result.ReadbackConfirmed?green:muted));
    cards.Children.Add(new Border{Width=270,Margin=new Thickness(0,0,16,16),Padding=new Thickness(18),BorderBrush=border,BorderThickness=new Thickness(1),Child=box});
   }content.Children.Add(cards);if(!String.IsNullOrWhiteSpace(auraSyncMessage))content.Children.Add(Paragraph(auraSyncMessage,12,muted));
  }
 }
}

namespace RogControl {
 public sealed class AuraSyncNativeReply {
  public int id;public bool ok,controlled;public object error;public System.Collections.Generic.List<System.Collections.Generic.Dictionary<string,object>> devices;public string[] warnings;
 }
 public partial class MainWindow {
  readonly AuraSyncCoordinator auraSyncCoordinator=new AuraSyncCoordinator();
  readonly List<AuraSyncTarget> auraSyncTargets=new List<AuraSyncTarget>();
  readonly HashSet<string> auraSyncSelected=new HashSet<string>(StringComparer.Ordinal);
  AuraSyncResult auraSyncResult;
  bool auraSyncBusy,auraSyncConnectionsActive,auraSyncStopRequested;
  readonly Dictionary<string,AuraOwnershipResult> auraOwnershipResults=new Dictionary<string,AuraOwnershipResult>(StringComparer.Ordinal);
  string auraSyncMessage="";
  public bool AuraSyncBusy {get{return auraSyncBusy||auraSyncCoordinator.Running;}}
  public bool AuraSyncConnectionsActive {get{return auraSyncConnectionsActive;}}

  async System.Threading.Tasks.Task ScanAllAuraTargetsAsync() {
   if(busy||auraSyncBusy||closing||render)return;busy=true;auraSyncBusy=true;auraSyncStopRequested=false;auraSyncMessage="正在读取各设备的灯效能力…";auraSyncTargets.Clear();auraSyncResult=null;ShowPage(page);
   try {
    try {
     var native=await worker.Request<AuraSyncNativeReply>(new Dictionary<string,object>{{"op","scan"}});
     if(!native.ok)throw new InvalidOperationException(SdkError(native.error));
     foreach(var device in native.devices??new List<Dictionary<string,object>>())auraSyncTargets.Add(AuraSyncCoordinator.Direct(device,info.Board,DateTime.UtcNow));
     if((native.devices??new List<Dictionary<string,object>>()).Count==0)auraSyncTargets.Add(AuraSyncCoordinator.Missing("motherboard",info.Board,"未读到主板 RGB 控制接口。",DateTime.UtcNow));
    }catch(Exception ex){auraSyncTargets.Add(AuraSyncCoordinator.Missing("motherboard",info.Board,"主板灯控读取失败："+ex.GetBaseException().Message,DateTime.UtcNow));}
    foreach(string kind in new[]{"keyboard","mouse","headset"}) {
     if(auraSyncStopRequested||closing)break;
     auraSyncMessage="正在读取 "+AuraSyncKindName(kind)+" 灯效能力…";if(page=="灯效 & Aura Sync")ShowPage(page);
     try {
      var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"},{"kind",kind}});
      if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));var list=GearArray(reply.data,"devices");
      if(list==null)throw new InvalidOperationException("设备接口未返回完整列表。");
      if(list.Length==0)auraSyncTargets.Add(AuraSyncCoordinator.Missing(kind,AuraSyncKindName(kind),"未找到此型号的有线或 2.4GHz 控制接口。",DateTime.UtcNow));
      foreach(var raw in list){var state=raw as Dictionary<string,object>;if(state==null)throw new InvalidOperationException("设备能力数据无效。");auraSyncTargets.Add(AuraSyncCoordinator.Gear(state,DateTime.UtcNow));}
     }catch(Exception ex){auraSyncTargets.Add(AuraSyncCoordinator.Missing(kind,AuraSyncKindName(kind),"读取失败："+ex.GetBaseException().Message,DateTime.UtcNow));}
    }
    if(!auraSyncStopRequested&&!closing)try {
     var scan=await Sdk("fan").Request<SdkReply>(new Dictionary<string,object>{{"op","scan"},{"kind","fan"}});if(!scan.ok)throw new InvalidOperationException(SdkError(scan.error));
     var reply=await Sdk("fan").Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind","fan"},{"deviceId","7576"}});if(!reply.ok||reply.data==null)throw new InvalidOperationException(SdkError(reply.error));auraSyncTargets.Add(AuraSyncCoordinator.Fan(reply.data,DateTime.UtcNow));
    }catch(Exception ex){auraSyncTargets.Add(AuraSyncCoordinator.Missing("fan","ROG EURUX Controller","读取失败："+ex.GetBaseException().Message,DateTime.UtcNow));}
    if(!auraSyncStopRequested&&!closing)try {
     var reply=await discoveryWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok||OriginalStateFlag(reply.data,"identificationOnly")!=true)throw new InvalidOperationException(SdkError(reply.error));
     var list=GearArray(reply.data,"devices");if(list==null)throw new InvalidOperationException("Windows 设备列表缺失。");
     foreach(var raw in list){var identity=raw as Dictionary<string,object>;if(DiscoveryEntryValid(identity)&&!AuraSyncIdentityRepresented(identity))auraSyncTargets.Add(AuraSyncCoordinator.Identified(identity,DateTime.UtcNow));}
    }catch(Exception ex){auraSyncMessage="其他设备识别未完成："+ex.GetBaseException().Message;}
    auraSyncSelected.RemoveWhere(id=>!auraSyncTargets.Any(target=>target.Id==id&&target.CanStaticRgb&&target.Online));
    if(auraSyncSelected.Count==0&&!auraSyncStopRequested)foreach(var target in auraSyncTargets.Where(target=>target.CanStaticRgb&&target.Online))auraSyncSelected.Add(target.Id);
    if(auraSyncStopRequested){foreach(var target in auraSyncTargets){target.Online=false;target.CanStaticRgb=false;target.UnavailableReason="连接已释放，请重新扫描。";}auraSyncSelected.Clear();auraSyncMessage="读取已停止。";}
    else auraSyncMessage="可同步 "+auraSyncTargets.Count(target=>target.CanStaticRgb)+" 项 · 已识别 "+auraSyncTargets.Count+" 项";
   }finally{auraSyncBusy=false;busy=false;if(!closing)ShowPage(page);}
  }
  bool AuraSyncIdentityRepresented(IDictionary<string,object> identity) {
   string pid=DiscoveryText(identity,"pid"),name=DiscoveryText(identity,"name");
   if(!DiscoverySignatureKnown(pid,name,String.Equals(info.Board,"ROG CROSSHAIR X870E EXTREME",StringComparison.OrdinalIgnoreCase)))return false;
   if(pid=="1D98")return auraSyncTargets.Any(target=>target.Kind=="fan"&&target.Backend=="sdk");
   if(pid=="1D41")return auraSyncTargets.Any(target=>target.Kind=="headset"&&target.Backend=="gear");
   if(pid=="1DA2"||pid=="1DA4"||pid=="1D54")return auraSyncTargets.Any(target=>target.Kind=="keyboard"&&target.Backend=="gear");
   if(pid=="1D7A"||pid=="1D7C")return auraSyncTargets.Any(target=>target.Kind=="mouse"&&target.Backend=="gear");
   if(pid=="1AA6")return auraSyncTargets.Any(target=>target.Backend=="direct"&&OriginalStateText(target.State,"productId","")==pid);
   return false;
  }
  async System.Threading.Tasks.Task<AuraSyncTarget> RefreshAuraSyncTarget(AuraSyncTarget target) {
   if(auraSyncStopRequested||closing)throw new OperationCanceledException("同步已停止。");
   if(target.Backend=="direct") {
    var reply=await worker.Request<AuraSyncNativeReply>(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));
    var found=(reply.devices??new List<Dictionary<string,object>>()).FirstOrDefault(item=>OriginalStateText(item,"id","")==target.DeviceId);if(found==null)throw new InvalidOperationException("此主板灯控已断开。");return AuraSyncCoordinator.Direct(found,info.Board,DateTime.UtcNow);
   }
   if(target.Backend=="gear") {
    var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",target.Kind},{"deviceId",target.DeviceId}});if(!reply.ok||reply.data==null)throw new InvalidOperationException(SdkError(reply.error));return AuraSyncCoordinator.Gear(reply.data,DateTime.UtcNow);
   }
   throw new InvalidOperationException("此设备没有已验证的静态颜色写入接口。");
  }
  async System.Threading.Tasks.Task<AuraSyncCommandReply> SendAuraSyncCommand(AuraSyncCommand command) {
   // Recheck permission at the transport boundary, even when caller validated it.
   hardwareControlPolicy.Demand();if(closing||render||auraSyncStopRequested)throw new OperationCanceledException("同步已停止。");
   if(command.Backend=="direct") {
    var reply=await worker.Request<AuraSyncNativeReply>(new Dictionary<string,object>(command.Request));return new AuraSyncCommandReply{Ok=reply.ok,Accepted=reply.ok,WriteAttempted=true,WriteConfirmed=false,Error=reply.ok?null:SdkError(reply.error)};
   }
   if(command.Backend=="gear") {
    var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>(command.Request));if(!reply.ok)return new AuraSyncCommandReply{Ok=false,WriteAttempted=true,Error=SdkError(reply.error)};
    if(reply.data!=null)gearStates[command.Kind]=reply.data;
    return new AuraSyncCommandReply{Ok=true,Accepted=OriginalStateFlag(reply.data,"accepted")==true,WriteAttempted=OriginalStateFlag(reply.data,"writeAttempted")==true,WriteConfirmed=OriginalStateFlag(reply.data,"writeConfirmed")==true};
   }
   throw new InvalidOperationException("此同步后端尚未接通。");
  }
  async System.Threading.Tasks.Task ApplyAllAuraTargetsAsync() {
   if(busy||auraSyncBusy||closing||render)return;if(!PermitHardwareWrite())return;
   busy=true;auraSyncBusy=true;auraSyncStopRequested=false;auraSyncMessage="正在核对所选设备并应用静态颜色…";ShowPage(page);
   try {
    auraSyncResult=await auraSyncCoordinator.ApplyAsync(auraSyncTargets,auraSyncSelected,effect,color,brightness,RefreshAuraSyncTarget,SendAuraSyncCommand,()=>{hardwareControlPolicy.Demand();if(closing||render||auraSyncStopRequested)throw new OperationCanceledException("同步已停止。");});
    auraSyncConnectionsActive=!auraSyncStopRequested&&(auraSyncConnectionsActive||auraSyncResult.Devices.Any(result=>result.WriteAttempted));controlling=auraSyncConnectionsActive;
    int confirmed=auraSyncResult.Devices.Count(result=>result.ReadbackConfirmed),unconfirmed=auraSyncResult.Devices.Count(result=>result.State=="submittedUnconfirmed"||result.State=="unconfirmed"),failed=auraSyncResult.Devices.Count(result=>result.State=="failed"||result.State=="preflightFailed");
    auraSyncMessage="所选设备："+confirmed+" 项配置读回确认，"+unconfirmed+" 项未确认，"+failed+" 项失败。实体灯光效果需要观察确认。";Log(auraSyncMessage);
   }catch(Exception ex){auraSyncMessage="同步未完成："+ex.GetBaseException().Message;Log(auraSyncMessage);}
   finally{auraSyncBusy=false;busy=false;if(!closing)ShowPage(page);}
  }
  async System.Threading.Tasks.Task TakeAuraSoftwareLightingAsync(AuraSyncTarget target) {
   if(busy||auraSyncBusy||closing||render)return;if(!PermitHardwareWrite())return;busy=true;auraSyncBusy=true;auraSyncStopRequested=false;auraSyncMessage="正在接管灯效…";ShowPage(page);
   try {
    var result=await auraSyncCoordinator.TakeSoftwareLightingAsync(target,RefreshAuraSyncTarget,SendAuraSyncCommand,()=>{hardwareControlPolicy.Demand();if(closing||render||auraSyncStopRequested)throw new OperationCanceledException("接管已停止。");});auraOwnershipResults[target.Id]=result;
    if(result.Target!=null&&result.Target.Id==target.Id){int index=auraSyncTargets.FindIndex(value=>value.Id==target.Id);if(index>=0)auraSyncTargets[index]=result.Target;if(result.OwnershipConfirmed&&result.Target.CanStaticRgb)auraSyncSelected.Add(target.Id);else auraSyncSelected.Remove(target.Id);}
    if(!result.OwnershipConfirmed&&result.Target!=null){result.Target.CanStaticRgb=false;result.Target.UnavailableReason=result.Error+"；原标志恢复："+result.RestorationState;}
    auraSyncConnectionsActive=!auraSyncStopRequested&&(auraSyncConnectionsActive||result.WriteAttempted);controlling=auraSyncConnectionsActive;
    auraSyncMessage=result.OwnershipConfirmed?"灯效接管已读回，可以选择同步。":result.RestorationConfirmed?"接管未完成，原灯控标志已恢复。":"接管未完成，恢复状态未确认。";Log(auraSyncMessage+(String.IsNullOrEmpty(result.Error)?"":" "+result.Error));
   }catch(Exception ex){auraSyncMessage="接管未完成："+ex.GetBaseException().Message;Log(auraSyncMessage);}finally{auraSyncBusy=false;busy=false;if(!closing)ShowPage(page);}
  }
  System.Threading.Tasks.Task StopAllAuraTargetsAsync() {
   auraSyncStopRequested=true;auraSyncCoordinator.RequestStop();worker.Kill();gearWorker.Kill();discoveryWorker.Kill();WorkerClient sdk;if(sdkWorkers.TryGetValue("fan",out sdk))sdk.Kill();
   auraSyncConnectionsActive=false;controlling=false;auraSyncSelected.Clear();foreach(var target in auraSyncTargets){target.Online=false;target.CanStaticRgb=false;target.CanReleaseSoftwareLighting=false;target.UnavailableReason="连接已释放，请重新扫描。";}
   auraSyncMessage="同步连接已释放；设备保留最后一次颜色，不会自动恢复先前设置。";if(!closing)ShowPage(page);return System.Threading.Tasks.Task.FromResult(0);
  }
  async System.Threading.Tasks.Task ReturnAuraSoftwareLightingAsync(string id) {
   if(busy||auraSyncBusy||closing||render)return;if(!PermitHardwareWrite())return;AuraOwnershipResult original;if(!auraOwnershipResults.TryGetValue(id,out original)||!original.CanReturnOriginalFlag)return;busy=true;auraSyncBusy=true;auraSyncStopRequested=false;auraSyncMessage="正在交还灯效…";ShowPage(page);
   try {
    var result=await auraSyncCoordinator.ReturnSoftwareLightingAsync(original,RefreshAuraSyncTarget,SendAuraSyncCommand,()=>{hardwareControlPolicy.Demand();if(closing||render||auraSyncStopRequested)throw new OperationCanceledException("交还已停止。");});
    if(result.RestorationConfirmed)auraOwnershipResults[id]=result;else{original.Error=result.Error;original.RestorationState=result.RestorationState;}
    if(result.Target!=null&&result.Target.Id==id){int index=auraSyncTargets.FindIndex(value=>value.Id==id);if(index>=0)auraSyncTargets[index]=result.Target;}auraSyncSelected.Remove(id);
    if(!result.RestorationConfirmed&&result.Target!=null){result.Target.CanStaticRgb=false;result.Target.UnavailableReason=result.Error;}
    auraSyncConnectionsActive=!auraSyncStopRequested&&(auraSyncConnectionsActive||result.WriteAttempted);controlling=auraSyncConnectionsActive;
    auraSyncMessage=result.RestorationConfirmed?"原软件灯控标志已交还并读回。":"交还未确认，请查看设备状态。";Log(auraSyncMessage+(String.IsNullOrEmpty(result.Error)?"":" "+result.Error));
   }catch(Exception ex){auraSyncMessage="交还未完成："+ex.GetBaseException().Message;Log(auraSyncMessage);}finally{auraSyncBusy=false;busy=false;if(!closing)ShowPage(page);}
  }
  static string AuraSyncKindName(string kind){return kind=="keyboard"?"ROG AZOTH EXTREME EDITION 20":kind=="mouse"?"ROG HARPE II EXTREME EDITION 20":kind=="headset"?"ROG DELTA II-KJP":kind;}
  static string AuraSyncResultLabel(AuraSyncTargetResult result){return result.State=="readbackConfirmed"?"颜色配置已读回；实体效果待确认。":result.State=="submittedUnconfirmed"?"请求已提交，灯光读回未确认。":result.State=="cancelled"?"已停止。":String.IsNullOrEmpty(result.Error)?"未完成。":result.Error;}
 }
}
