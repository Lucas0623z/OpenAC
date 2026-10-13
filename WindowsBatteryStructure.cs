using System;using System.Globalization;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Threading;
namespace RogControl {
 public partial class MainWindow {
  WindowsBatteryReader windowsBatteryReader=new WindowsBatteryReader();
  WindowsBatterySnapshot windowsBattery;
  DispatcherTimer windowsBatteryTimer;
  string windowsBatteryMessage="";
  DateTime? windowsBatteryWaitingCapturedUtc;
  static DateTime BatteryUtc(DateTime value){return value.Kind==DateTimeKind.Local?value.ToUniversalTime():DateTime.SpecifyKind(value,DateTimeKind.Utc);}
  static bool WindowsBatteryCurrent(DateTime capturedUtc,DateTime nowUtc){var age=BatteryUtc(nowUtc)-BatteryUtc(capturedUtc);return age<=TimeSpan.FromSeconds(30)&&age>=TimeSpan.FromSeconds(-5);}
  bool ExpireWindowsBattery(DateTime nowUtc){if(windowsBattery==null||WindowsBatteryCurrent(windowsBattery.CapturedUtc,nowUtc))return false;windowsBatteryWaitingCapturedUtc=BatteryUtc(windowsBattery.CapturedUtc);windowsBattery=null;windowsBatteryMessage="";return true;}
  void BeginWindowsBatteryPolling(){if(render)return;windowsBatteryTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};windowsBatteryTimer.Tick+=async(s,e)=>await WindowsBatteryPollTick(DateTime.UtcNow);windowsBatteryTimer.Start();}
  async Task WindowsBatteryPollTick(DateTime nowUtc){
   if(closing){if(windowsBatteryTimer!=null)windowsBatteryTimer.Stop();return;}
   // Invalidation precedes the read gates: an unrelated pending operation
   // must not leave an old battery snapshot looking current.
   if(ExpireWindowsBattery(nowUtc)&&page=="电池信息")ShowPage(page);
   if(!render&&IsLoaded&&page=="电池信息"&&!busy&&!pageReadRunning)await ReadWindowsBattery();
  }
  void OriginalWindowsBatteryPage(){
   Header("电池信息","");var commands=new WrapPanel{Margin=new Thickness(0,0,0,18)};var read=Button(busy?"正在读取…":"刷新",async()=>await ReadWindowsBattery());read.IsEnabled=!busy&&!closing&&!render;commands.Children.Add(read);commands.Children.Add(Button("返回工具",()=>ShowPage("工具")));content.Children.Add(commands);
   ExpireWindowsBattery(DateTime.UtcNow);
   if(windowsBattery==null){if(!String.IsNullOrEmpty(windowsBatteryMessage))content.Children.Add(Paragraph(windowsBatteryMessage,13,muted));else{var waiting=Paragraph("等待刷新",13,muted);if(windowsBatteryWaitingCapturedUtc.HasValue)waiting.ToolTip=windowsBatteryWaitingCapturedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'",CultureInfo.InvariantCulture);content.Children.Add(waiting);}return;}
   if(windowsBattery.HasSystemBattery==false){content.Children.Add(Paragraph("未检测到系统电池。",15,muted));return;}
   var overview=Box("系统电池","");BatteryMetric(overview,"电量",windowsBattery.ChargePercent.HasValue?windowsBattery.ChargePercent+" %":"—");BatteryMetric(overview,"电源",windowsBattery.AcOnline.HasValue?(windowsBattery.AcOnline.Value?"已接通":"使用电池"):"—");BatteryMetric(overview,"充电",windowsBattery.Charging.HasValue?(windowsBattery.Charging.Value?"充电中":"未充电"):"—");BatteryMetric(overview,"节电模式",windowsBattery.BatterySaver.HasValue?(windowsBattery.BatterySaver.Value?"开启":"关闭"):"—");content.Children.Add(Card(overview));
   foreach(var battery in windowsBattery.Batteries){var box=Box(battery.Name,"");BatteryMetric(box,"电量",BatteryNumber(battery.ChargePercent," %"));BatteryMetric(box,"健康度",BatteryNumber(battery.HealthPercent," %"));BatteryMetric(box,"设计容量",BatteryMilli(battery.DesignedCapacityMilliwattHours," Wh"));BatteryMetric(box,"满充容量",BatteryMilli(battery.FullChargedCapacityMilliwattHours," Wh"));BatteryMetric(box,"剩余容量",BatteryMilli(battery.RemainingCapacityMilliwattHours," Wh"));BatteryMetric(box,"电压",BatteryMilli(battery.VoltageMillivolts," V"));BatteryMetric(box,"充放电功率",BatteryNumber(battery.SignedPowerMilliwatts.HasValue?battery.SignedPowerMilliwatts.Value/1000.0:(double?)null," W"));BatteryMetric(box,"循环次数",battery.CycleCount.HasValue?battery.CycleCount.Value.ToString(CultureInfo.InvariantCulture):"—");if(battery.StateConflict)box.Children.Add(Paragraph("电池状态未确认。",12,muted));content.Children.Add(Card(box));}
   if(windowsBattery.Errors.Count>0){var partial=Paragraph("部分电池信息未读取。",12,muted);partial.ToolTip=String.Join("；",System.Linq.Enumerable.Select(windowsBattery.Errors,error=>error.Source+" · "+error.Category));content.Children.Add(partial);}
   if(!String.IsNullOrEmpty(windowsBatteryMessage))content.Children.Add(Paragraph(windowsBatteryMessage,13,muted));
  }
  static string BatteryNumber(double? value,string unit){return value.HasValue&&!Double.IsNaN(value.Value)&&!Double.IsInfinity(value.Value)?value.Value.ToString("0.##",CultureInfo.InvariantCulture)+unit:"—";}
  static string BatteryMilli(uint? value,string unit){return BatteryNumber(value.HasValue?value.Value/1000.0:(double?)null,unit);}
  void BatteryMetric(StackPanel parent,string caption,string value){var row=new Grid{Margin=new Thickness(0,7,0,7)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(180)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(Label(caption,13,muted));var detail=Label(value,14,text);Grid.SetColumn(detail,1);row.Children.Add(detail);parent.Children.Add(row);}
  async Task ReadWindowsBattery(){if(busy||closing||render)return;busy=true;ShowPage(page);try{var task=Task.Run(()=>windowsBatteryReader.ReadSnapshot());windowsSystemTask=task;var next=await task;if(!closing){windowsBattery=next;windowsBatteryWaitingCapturedUtc=null;windowsBatteryMessage="";}}catch(Exception ex){windowsBattery=null;windowsBatteryWaitingCapturedUtc=null;windowsBatteryMessage="读取失败："+ex.GetType().Name;}finally{windowsSystemTask=null;busy=false;if(!closing)ShowPage(page);}}
 }
}
