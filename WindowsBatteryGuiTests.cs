// SPDX-License-Identifier: MIT
// Unshown WPF tests with a fake battery provider; no Windows native reads or writes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RogControl;

public static class WindowsBatteryGuiTests {
 const string Identity="FAKE_GUI_BATTERY_PRIVATE_INSTANCE";
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks,renders;
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static void Set(MainWindow window,string field,object value){typeof(MainWindow).GetField(field,Hidden).SetValue(window,value);}
 static object Get(MainWindow window,string field){return typeof(MainWindow).GetField(field,Hidden).GetValue(window);}
 static object Invoke(MainWindow window,string method,params object[] values){return typeof(MainWindow).GetMethod(method,Hidden).Invoke(window,values);}
 static void Timestamp(WindowsBatterySnapshot value,DateTime captured){typeof(WindowsBatterySnapshot).GetProperty("CapturedUtc").SetValue(value,captured,null);}
 static bool Current(DateTime captured,DateTime now){return (bool)typeof(MainWindow).GetMethod("WindowsBatteryCurrent",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{captured,now});}
 static IEnumerable<T> Logical<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Logical<T>(child))yield return item;}
 static StackPanel Content(MainWindow window){return (StackPanel)Get(window,"content");}
 static string[] Texts(MainWindow window){return Logical<TextBlock>(Content(window)).Select(t=>t.Text).ToArray();}
 static WindowsBatteryInputRow Row(Dictionary<string,object> values){values["Active"]=true;values["Tag"]=1u;return new WindowsBatteryInputRow(Identity,values);}
 sealed class Fake:IWindowsBatteryBackend {
  public int SystemReads,Queries;
  public Exception SystemError;
  public WindowsBatteryPowerStatus System=new WindowsBatteryPowerStatus{AcLineStatus=1,BatteryFlag=8,BatteryLifePercent=75,SystemStatusFlag=0,BatteryLifeTime=UInt32.MaxValue,BatteryFullLifeTime=UInt32.MaxValue};
  public readonly Dictionary<WindowsBatteryDataKind,IList<WindowsBatteryInputRow>> Data=new Dictionary<WindowsBatteryDataKind,IList<WindowsBatteryInputRow>>();
  public readonly Dictionary<WindowsBatteryDataKind,Exception> Errors=new Dictionary<WindowsBatteryDataKind,Exception>();
  public WindowsBatteryPowerStatus ReadSystemStatus(){SystemReads++;if(SystemError!=null)throw SystemError;return System;}
  public IList<WindowsBatteryInputRow> Query(WindowsBatteryDataKind kind,TimeSpan timeout,int maximumRows){Queries++;Exception error;if(Errors.TryGetValue(kind,out error))throw error;IList<WindowsBatteryInputRow> data;return Data.TryGetValue(kind,out data)?data:new WindowsBatteryInputRow[0];}
 }
 static Fake Provider(uint? capabilities){
  var fake=new Fake();
  fake.Data[WindowsBatteryDataKind.Status]=new[]{Row(new Dictionary<string,object>{{"RemainingCapacity",39000u},{"Voltage",11400u},{"Charging",true},{"Discharging",false},{"ChargeRate",3750u},{"PowerOnline",true}})};
  var data=new Dictionary<string,object>{{"DesignedCapacity",50000u}};if(capabilities.HasValue)data["Capabilities"]=capabilities.Value;
  fake.Data[WindowsBatteryDataKind.StaticData]=new[]{Row(data)};
  fake.Data[WindowsBatteryDataKind.FullChargedCapacity]=new[]{Row(new Dictionary<string,object>{{"FullChargedCapacity",52000u}})};
  fake.Data[WindowsBatteryDataKind.CycleCount]=new[]{Row(new Dictionary<string,object>{{"CycleCount",9u}})};
  return fake;
 }
 static WindowsBatterySnapshot Use(MainWindow window,Fake fake){
  var reader=new WindowsBatteryReader(fake);Set(window,"windowsBatteryReader",reader);var snapshot=reader.ReadSnapshot();
  Set(window,"windowsBattery",snapshot);Set(window,"windowsBatteryWaitingCapturedUtc",null);Set(window,"windowsBatteryMessage","");return snapshot;
 }
 static void Layout(MainWindow window,int width,string scenario,string output){
  Invoke(window,"ShowPage","电池信息");var frame=(FrameworkElement)window.Content;int height=width==1180?726:826;
  frame.Measure(new Size(width,height));frame.Arrange(new Rect(0,0,width,height));frame.UpdateLayout();
  foreach(var block in Logical<TextBlock>(Content(window))){
   if(block.ActualWidth<=0||block.ActualHeight<=0)continue;
   var bounds=block.TransformToAncestor(frame).TransformBounds(new Rect(0,0,block.ActualWidth,block.ActualHeight));
   Check(bounds.Left>=-1&&bounds.Right<=width+1,scenario+" text fits horizontal window at "+width+": "+block.Text);
   if(block.TextWrapping==TextWrapping.NoWrap&&block.TextTrimming==TextTrimming.None)
    Check(block.DesiredSize.Width-block.Margin.Left-block.Margin.Right<=block.ActualWidth+1,scenario+" text is not clipped at "+width+": "+block.Text);
  }
  var read=Logical<Button>(Content(window)).Single(b=>Convert.ToString(b.Content)=="刷新"||Convert.ToString(b.Content)=="正在读取…");
  Check(!read.IsEnabled,"render-only refresh remains disabled at "+width+"/"+scenario);
  Check(Logical<Button>(Content(window)).All(b=>!Convert.ToString(b.Content).Contains("应用")),"battery page has no apply controls at "+width+"/"+scenario);
  var scroll=Logical<ScrollViewer>(frame).Single(s=>Object.ReferenceEquals(s.Content,Content(window)));
  Check(scroll.HorizontalScrollBarVisibility!=ScrollBarVisibility.Auto&&scroll.ScrollableWidth<1,"no horizontal scrolling needed at "+width+"/"+scenario);
  if(scroll.ScrollableHeight>0){scroll.ScrollToBottom();frame.UpdateLayout();Check(scroll.VerticalOffset>=scroll.ScrollableHeight-1,"all battery rows remain vertically reachable at "+width+"/"+scenario);scroll.ScrollToTop();frame.UpdateLayout();}
  Check(PresentationSource.FromVisual(window)==null&&!window.IsVisible,"window remains unshown at "+width+"/"+scenario);
  if(output!=null){var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,scenario+"-"+width+".png")))encoder.Save(stream);}
  renders++;
 }
 static void BothWidths(MainWindow window,string scenario,string output){foreach(int width in new[]{1538,1180})Layout(window,width,scenario,output);}
 static void Waiting(MainWindow window,string label){
  Check(Texts(window).Contains("等待刷新"),label+" displays short waiting state");
  Check(!Texts(window).Contains("75 %")&&!Texts(window).Contains("52 Wh")&&!Texts(window).Contains("未检测到系统电池。"),label+" does not retain earlier battery claims");
  Check(Get(window,"windowsBattery")==null,label+" clears cached current snapshot");
  Check(!Texts(window).Any(t=>t=="系统电池"||t=="系统电池 1"),label+" removes earlier value cards");
  var waiting=Logical<TextBlock>(Content(window)).Single(t=>t.Text=="等待刷新");Check(Convert.ToString(waiting.ToolTip).EndsWith(" UTC",StringComparison.Ordinal),label+" keeps capture time only in tooltip");
 }
 [STAThread]public static int Main(string[] args){try{
  string output=null;if(args.Length>0){if(args.Length!=2||args[0]!="--render")throw new ArgumentException("Expected optional --render <directory>.");output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);}
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new MainWindow(true);
  Check(Get(window,"windowsBatteryTimer")==null,"render-only construction does not start polling");Check(PresentationSource.FromVisual(window)==null,"constructor does not create native window handle");
  var now=new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc);
  Check(Current(now.AddSeconds(-30),now),"exact 30-second age remains current");Check(!Current(now.AddSeconds(-30).AddTicks(-1),now),"age over 30 seconds expires");
  Check(Current(now.AddSeconds(5),now),"exact five-second future tolerance remains current");Check(!Current(now.AddSeconds(5).AddTicks(1),now),"future over five seconds expires");
  Check(Current(DateTime.SpecifyKind(now,DateTimeKind.Unspecified),now),"unspecified snapshot timestamp is treated as UTC");Check(Current(now.ToLocalTime(),now),"local timestamp is normalized before age comparison");Check(!Current(DateTime.MinValue,now),"missing timestamp cannot be current");
  var normal=Provider(WindowsBatteryReader.SystemBatteryFlag);Use(window,normal);BothWidths(window,"normal",output);
  Check(Texts(window).Contains("75 %")&&Texts(window).Contains("104 %"),"normal charge and measured health above 100 remain visible");
  Check(Texts(window).Contains("50 Wh")&&Texts(window).Contains("52 Wh")&&Texts(window).Contains("39 Wh")&&Texts(window).Contains("11.4 V")&&Texts(window).Contains("3.75 W"),"physical units use mWh/mV/mW conversion");
  Check(!Texts(window).Any(t=>t.Contains(Identity)),"raw battery identity is not shown");
  int reads=normal.SystemReads,queries=normal.Queries;
  ((Task)Invoke(window,"ReadWindowsBattery")).GetAwaiter().GetResult();((Task)Invoke(window,"WindowsBatteryPollTick",DateTime.UtcNow)).GetAwaiter().GetResult();
  Check(normal.SystemReads==reads&&normal.Queries==queries,"render-only callbacks never invoke provider");
  var relative=Provider(WindowsBatteryReader.SystemBatteryFlag|WindowsBatteryReader.RelativeCapacityFlag);Use(window,relative);BothWidths(window,"relative-units",output);
  Check(!Texts(window).Any(t=>t.EndsWith(" Wh",StringComparison.Ordinal)||t.EndsWith(" W",StringComparison.Ordinal))&&Texts(window).Contains("11.4 V"),"relative capacities and rates stay unknown while independent voltage remains valid");
  Check(Texts(window).Count(t=>t=="—")>=6,"relative-unit record has missing-value indicators");
  var unitsUnknown=Provider(null);Use(window,unitsUnknown);BothWidths(window,"unknown-units",output);Check(!Texts(window).Any(t=>t.EndsWith(" Wh",StringComparison.Ordinal)||t.EndsWith(" W",StringComparison.Ordinal)),"missing capabilities never receive physical energy or power units");
  var absent=new Fake();absent.System.BatteryFlag=128;Use(window,absent);BothWidths(window,"no-battery",output);
  Check(Texts(window).Contains("未检测到系统电池。")&&!Texts(window).Contains("75 %")&&absent.Queries==0,"known absent system battery is truthful and does not query WMI");
  var unknown=new Fake();unknown.System.BatteryFlag=255;unknown.System.BatteryLifePercent=255;Use(window,unknown);BothWidths(window,"unknown-battery",output);
  Check(!Texts(window).Contains("未检测到系统电池。")&&!Texts(window).Contains("0 %")&&Texts(window).Count(t=>t=="—")>=2,"unknown presence and charge are not replaced with absence or zero");
  var old=Provider(WindowsBatteryReader.SystemBatteryFlag);var oldSnapshot=Use(window,old);Timestamp(oldSnapshot,DateTime.UtcNow.AddSeconds(-31));BothWidths(window,"expired",output);Waiting(window,"old snapshot");
  var future=Provider(WindowsBatteryReader.SystemBatteryFlag);var futureSnapshot=Use(window,future);Timestamp(futureSnapshot,DateTime.UtcNow.AddSeconds(6));BothWidths(window,"future",output);Waiting(window,"future snapshot");
  var absentSnapshot=Use(window,absent);Timestamp(absentSnapshot,DateTime.UtcNow.AddSeconds(-31));BothWidths(window,"expired-no-battery",output);Waiting(window,"old no-battery observation");
  foreach(string gate in new[]{"busy","pageReadRunning"}){
   var blocked=Provider(WindowsBatteryReader.SystemBatteryFlag);var snapshot=Use(window,blocked);Invoke(window,"ShowPage","电池信息");Check(Texts(window).Contains("75 %"),gate+" starts with fresh visible data");
   var tick=DateTime.UtcNow;Timestamp(snapshot,tick.AddSeconds(-31));Set(window,gate,true);int beforeReads=blocked.SystemReads,beforeQueries=blocked.Queries;
   ((Task)Invoke(window,"WindowsBatteryPollTick",tick)).GetAwaiter().GetResult();Waiting(window,gate+" tick");
   Check(blocked.SystemReads==beforeReads&&blocked.Queries==beforeQueries,gate+" expiry precedes read gating without reading provider");Set(window,gate,false);BothWidths(window,"blocked-"+gate,output);
  }
  var offPage=Use(window,Provider(WindowsBatteryReader.SystemBatteryFlag));Invoke(window,"ShowPage","工具");Timestamp(offPage,DateTime.UtcNow.AddSeconds(-31));
  ((Task)Invoke(window,"WindowsBatteryPollTick",DateTime.UtcNow)).GetAwaiter().GetResult();Check(Get(window,"windowsBattery")==null&&Convert.ToString(Get(window,"page"))=="工具","off-page expiry clears cache without changing selected page");BothWidths(window,"expired-return",output);Waiting(window,"return to expired page");
  var errors=Provider(WindowsBatteryReader.SystemBatteryFlag);errors.Errors[WindowsBatteryDataKind.FullChargedCapacity]=new TimeoutException(Identity);Use(window,errors);BothWidths(window,"provider-error",output);
  Check(Texts(window).Contains("部分电池信息未读取。")&&Texts(window).Contains("75 %")&&Texts(window).Contains("50 Wh")&&!Texts(window).Contains("52 Wh"),"partial provider failure preserves confirmed independent fields only");
  var notice=Logical<TextBlock>(Content(window)).Single(t=>t.Text=="部分电池信息未读取。");Check(Convert.ToString(notice.ToolTip)=="BatteryFullChargedCapacity · Timeout","error tooltip shows only sanitized source and category");Check(!Convert.ToString(notice.ToolTip).Contains(Identity),"raw exception message is not shown");
  var systemError=new Fake{SystemError=new InvalidOperationException(Identity)};Use(window,systemError);BothWidths(window,"system-provider-error",output);
  Check(Texts(window).Contains("部分电池信息未读取。")&&!Texts(window).Contains("75 %")&&!Texts(window).Contains("未检测到系统电池。"),"failed system status does not invent battery claims");
  Set(window,"windowsBattery",null);Set(window,"windowsBatteryMessage","读取失败：TimeoutException");BothWidths(window,"read-error",output);Check(Texts(window).Contains("读取失败：TimeoutException")&&!Texts(window).Contains("75 %"),"failed read retains a short necessary error without old values");
  Set(window,"windowsBatteryMessage","");Set(window,"windowsBatteryWaitingCapturedUtc",null);BothWidths(window,"initial",output);Check(Texts(window).Contains("等待刷新")&&Logical<TextBlock>(Content(window)).Single(t=>t.Text=="等待刷新").ToolTip==null,"initial page waits without an invented capture time");
  Check(!window.IsVisible&&PresentationSource.FromVisual(window)==null,"all checks finish without showing a native window");
  app.Shutdown();Console.WriteLine("Windows battery GUI: "+checks+" checks, "+renders+" unshown layouts passed; native GET/WMI/write=0; shown windows=0.");return 0;
 }catch(Exception ex){Console.Error.WriteLine("Windows battery GUI checks failed after "+checks+": "+(ex.InnerException??ex));return 1;}}
}
