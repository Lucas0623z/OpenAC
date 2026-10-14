// SPDX-License-Identifier: MIT
// Geometry and unknown-state checks only. No network or hardware query occurs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using RogControl;

public static class GameFirstGuiTests {
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static readonly string[] Tabs={"概览","网络监控","系统分析器","Network Doctor","设置"};
 static int checks;
 static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static object Get(MainWindow window,string name){return typeof(MainWindow).GetField(name,Hidden).GetValue(window);}
 static void Set(MainWindow window,string name,object value){typeof(MainWindow).GetField(name,Hidden).SetValue(window,value);}
 static object Call(MainWindow window,string name,params object[] values){return typeof(MainWindow).GetMethod(name,Hidden).Invoke(window,values);}
 static IEnumerable<DependencyObject> Walk(DependencyObject node){if(node==null)yield break;yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(node,i)))yield return child;}
 static string Tag(FrameworkElement element){return Convert.ToString(element.Tag);}
 static FrameworkElement Render(MainWindow window,int width,string tab){
  Set(window,"gameFirstTab",tab);Call(window,"ShowPage","GameFirst");var frame=(FrameworkElement)window.Content;frame.Measure(new Size(width,826));frame.Arrange(new Rect(0,0,width,826));frame.UpdateLayout();((ScrollViewer)Get(window,"pageScroll")).ScrollToTop();frame.UpdateLayout();var bitmap=new RenderTargetBitmap(width,826,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"gamefirst-"+width+"-"+Array.IndexOf(Tabs,tab)+".png")))png.Save(output);return frame;
 }
 static void WorkersStopped(MainWindow window){foreach(var field in typeof(MainWindow).GetFields(Hidden).Where(f=>f.FieldType==typeof(WorkerClient))){var worker=field.GetValue(window) as WorkerClient;Check(worker==null||typeof(WorkerClient).GetField("process",Hidden).GetValue(worker)==null,"UI never starts a worker: "+field.Name);}}
 [STAThread] public static void Main(){
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new MainWindow(true);
  foreach(int width in new[]{1180,1538,2560})foreach(string tab in Tabs){
   var frame=Render(window,width,tab);var nodes=Walk(frame).ToArray();var scroll=(ScrollViewer)Get(window,"pageScroll");var body=nodes.OfType<Canvas>().Single(c=>Tag(c)=="gamefirst:surface:"+tab);var viewport=nodes.OfType<ScrollViewer>().Single(s=>Tag(s)=="gamefirst:body-viewport:scroll");
   Check(((StackPanel)Get(window,"fixedPageHeader")).Children.Count==3,"GameFirst brand, title and five tabs have one pinned header at "+width);Check(Math.Abs(scroll.TransformToAncestor(frame).Transform(new Point()).Y-163)<0.1,"GameFirst reference body starts at 163 at "+width);Check(body.Width==1192,"Fixed design canvas keeps chart geometry at "+width);Check(Math.Abs(body.TransformToAncestor(frame).Transform(new Point()).X-270)<0.1,"Reference canvas is left anchored at every window width: "+width);Check(!nodes.OfType<Image>().Any(),"No copied product/background/logo asset at "+width);
   var tabButtons=nodes.OfType<Button>().Where(b=>Tag(b).StartsWith("gamefirst:tab:",StringComparison.Ordinal)).ToArray();Check(tabButtons.Length==5&&tabButtons.All(b=>b.IsEnabled),"All five main tabs are local navigation at "+width);Check(nodes.OfType<Button>().Where(b=>Tag(b).StartsWith("gamefirst:subtab:",StringComparison.Ordinal)).All(b=>!b.IsEnabled),"Uninspected subtabs stay disabled at "+width);
   foreach(var toggle in nodes.OfType<CheckBox>()){Check(!toggle.IsEnabled&&!toggle.IsChecked.HasValue,"Unknown controls never invent ON/OFF at "+width);Check(Walk(toggle).OfType<TextBlock>().Any(t=>t.Text=="—"),"Unknown switches show a dash at "+width);}
   Check(nodes.OfType<RadioButton>().All(r=>!r.IsEnabled&&r.IsChecked!=true),"Unknown network profile/group controls never invent a selected value at "+width);Check(nodes.OfType<ItemsControl>().Where(c=>Tag(c).EndsWith("applications",StringComparison.Ordinal)).All(c=>c.Items.Count==0),"App lists remain empty without adapter data at "+width);
   foreach(var label in Walk(body).OfType<TextBlock>().Where(t=>t.ActualWidth>0)){var point=label.TransformToAncestor(body).Transform(new Point());Check(point.X>=0&&point.X+label.ActualWidth<=1192,"Text remains within authored canvas: "+tab+" / "+label.Text);}
   var labels=Walk(body).OfType<TextBlock>().Select(t=>t.Text).ToArray();Check(!labels.Contains("99")&&!labels.Contains("RiotClient")&&!labels.Contains("Ethernet 2")&&!labels.Any(t=>t.Contains("Microsoft Edge")||t.Contains("Weixin")),"Reference readings and apps are not copied at "+width);
   if(tab=="概览"){var gauges=nodes.OfType<Canvas>().Where(c=>Tag(c).StartsWith("gamefirst:gauge:",StringComparison.Ordinal)).ToArray();Check(gauges.Length==4&&gauges.All(g=>Walk(g).OfType<TextBlock>().Any(t=>t.Text=="—")),"Four overview gauges have unknown readings at "+width);Check(nodes.OfType<ComboBox>().All(c=>!c.IsEnabled&&Convert.ToString(c.SelectedItem)=="—"),"Unread network mode is unknown at "+width);}
   if(tab=="网络监控")Check(nodes.OfType<Grid>().Any(g=>Tag(g)=="gamefirst:network-distribution"&&Walk(g).OfType<TextBlock>().Any(t=>t.Text=="—")),"Unread traffic distribution has an unknown marker at "+width);
   if(tab=="系统分析器"){var plots=nodes.OfType<Canvas>().Where(c=>Tag(c).StartsWith("gamefirst:plot:",StringComparison.Ordinal)).ToArray();Check(plots.Length==4,"Four resource plots retain their original positions at "+width);foreach(var plot in plots){Check(!Walk(plot).OfType<Polyline>().Any(),"Unknown history has no fabricated flat-zero or trend series at "+width);Check(Walk(plot).OfType<TextBlock>().Any(t=>t.Text=="—"),"Unknown resource chart is explicit at "+width);}scroll.ScrollToBottom();frame.UpdateLayout();Check(scroll.VerticalOffset>0,"Lower resource charts are reachable by vertical scroll at "+width);}
   if(tab=="Network Doctor")Check(nodes.OfType<Button>().Where(b=>Convert.ToString(b.Content)=="网络和性能诊断").All(b=>!b.IsEnabled),"No real network diagnosis starts at "+width);
   if(tab=="设置")Check(nodes.OfType<CheckBox>().Count()==6,"Six first settings-subtab switches remain at "+width);
   if(width==1180){Check(viewport.ScrollableWidth>0,"Narrow body uses a local horizontal viewport");viewport.ScrollToRightEnd();frame.UpdateLayout();Check(viewport.HorizontalOffset>0,"Right-hand content remains reachable in the narrow viewport");}else Check(viewport.ScrollableWidth==0,"Wide body preserves the fixed reference canvas without horizontal clipping");WorkersStopped(window);
  }
  var last=Render(window,1538,"概览");var networkTab=Walk(last).OfType<Button>().Single(b=>Tag(b)=="gamefirst:tab:网络监控");networkTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check((string)Get(window,"gameFirstTab")=="网络监控","Main tab click changes only local presentation state");WorkersStopped(window);Set(window,"gameFirstTab","invalid");Call(window,"ShowPage","GameFirst");Check((string)Get(window,"gameFirstTab")=="概览","Invalid restored tab falls back to the observed first page");WorkersStopped(window);Console.WriteLine("GameFirst GUI: "+checks+" checks passed. Hidden layout and unknown state only; no external calls.");app.Shutdown();
 }
}
