// SPDX-License-Identifier: MIT
// Hidden WPF layout checks use synthetic state only. No device request is sent.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RogControl;

public static class MotherboardGuiTests {
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static int checks;
 static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static object Get(MainWindow window,string name){return typeof(MainWindow).GetField(name,Hidden).GetValue(window);}
 static void Set(MainWindow window,string name,object value){typeof(MainWindow).GetField(name,Hidden).SetValue(window,value);}
 static object Call(MainWindow window,string name,params object[] values){return typeof(MainWindow).GetMethod(name,Hidden).Invoke(window,values);}
 static Dictionary<string,object> Obj(params object[] values){var map=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)map[(string)values[i]]=values[i+1];return map;}
 static IEnumerable<DependencyObject> Walk(DependencyObject node){if(node==null)yield break;yield return node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(node,i)))yield return child;}
 static FrameworkElement Render(MainWindow window,string page,int width,string suffix){
  Call(window,"ShowPage",page);var frame=(FrameworkElement)window.Content;frame.Measure(new Size(width,826));frame.Arrange(new Rect(0,0,width,826));frame.UpdateLayout();((ScrollViewer)Get(window,"pageScroll")).ScrollToTop();frame.UpdateLayout();var bitmap=new RenderTargetBitmap(width,826,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"device-views-"+width+"-"+suffix+".png")))png.Save(output);return frame;
 }
 static void WorkersStopped(MainWindow window){
  foreach(var field in typeof(MainWindow).GetFields(Hidden).Where(f=>f.FieldType==typeof(WorkerClient))){var worker=field.GetValue(window) as WorkerClient;Check(worker==null||typeof(WorkerClient).GetField("process",Hidden).GetValue(worker)==null,"No device worker started: "+field.Name);}
 }
 [STAThread] public static void Main(){
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new MainWindow(true);
  foreach(int width in new[]{1180,1538,2560})foreach(string tab in new[]{"5\" LCD Display","可编程接针","ASUS WiFi Q-Antenna"}){
   Set(window,"originalMotherboardTab",tab);var frame=Render(window,"device:motherboard",width,tab=="5\" LCD Display"?"lcd":tab=="可编程接针"?"argb":"antenna");var nodes=Walk(frame).ToArray();var scroll=(ScrollViewer)Get(window,"pageScroll");var pinned=(StackPanel)Get(window,"fixedPageHeader");Check(pinned.Children.Count==2,"Motherboard title and four tabs are pinned at "+width);Check(Math.Abs(scroll.TransformToAncestor(frame).Transform(new Point()).Y-117)<0.1,"Motherboard body has original reference origin at "+width);
   foreach(var label in Walk(scroll.Content as DependencyObject).OfType<TextBlock>().Where(t=>t.ActualWidth>0)){var point=label.TransformToAncestor(frame).Transform(new Point());Check(point.X>=248&&point.X+label.ActualWidth<=width-10,"Body text remains in its horizontal viewport at "+width+" / "+label.Text);}
   if(tab=="5\" LCD Display"){
    var preview=nodes.OfType<Border>().Single(b=>b.Name=="MotherboardLcdPreview");Check(preview.ActualWidth==169&&preview.ActualHeight==301,"LCD preview keeps its authored portrait proportion at "+width);Check(preview.Child==null,"Unknown configuration does not show a fictitious current preset at "+width);Check(nodes.OfType<Button>().Where(b=>Convert.ToString(b.Content)=="应用"||Convert.ToString(b.Content)=="上传文件").All(b=>!b.IsEnabled),"Unknown LCD values cannot enable writes at "+width);
   }else if(tab=="可编程接针"){
    var labels=nodes.OfType<TextBlock>().Select(t=>t.Text).ToArray();Check(new[]{"接头1","接头2","接头3"}.All(labels.Contains),"All authored physical connector positions remain at "+width);Check(nodes.OfType<TextBox>().Count(t=>t.Text=="—"&&!t.IsEnabled)==3,"All three unread LED counts are unknown and disabled at "+width);Check(nodes.OfType<Button>().Where(b=>Convert.ToString(b.Content)=="Gen1"||Convert.ToString(b.Content)=="Gen2"||Convert.ToString(b.Content)=="完成").All(b=>!b.IsEnabled),"Unread generation and LED actions are disabled at "+width);Check(!labels.Contains("480 / 500")&&!labels.Contains("120"),"Reference screenshot values are not fabricated at "+width);var title=nodes.OfType<TextBlock>().First(t=>t.Text=="ROG CROSSHAIR X870E EXTREME");double before=title.TransformToAncestor(frame).Transform(new Point()).Y;scroll.ScrollToBottom();frame.UpdateLayout();Check(scroll.VerticalOffset>0&&Math.Abs(title.TransformToAncestor(frame).Transform(new Point()).Y-before)<0.1,"Connector body scroll preserves the fixed header at "+width);
   }else{
    Check(nodes.OfType<TextBlock>().Any(t=>t.Text=="请检查是否已安装无线网络驱动程序"),"Unavailable antenna page matches the observed official state at "+width);Check(!nodes.OfType<Slider>().Any(),"Unavailable antenna page does not imply working antenna tuning at "+width);
   }
   WorkersStopped(window);
  }
  var snapshots=(Dictionary<string,Dictionary<string,object>>)Get(window,"sdkSnapshots");var profile=Obj("profileReadbackConfirmed",true,"currentMediaProfile",Obj("type","2","source","1","index_format","3"));snapshots["lcd:7299"]=profile;Check((int?)Call(window,"LcdConfiguredPreset",profile)==3,"Only a confirmed preset profile selects a configuration preview");profile["profileReadbackConfirmed"]=false;Check(Call(window,"LcdConfiguredPreset",profile)==null,"Unconfirmed profile is not a current preset");profile["profileReadbackConfirmed"]=true;((Dictionary<string,object>)profile["currentMediaProfile"])["index_format"]="8";Check(Call(window,"LcdConfiguredPreset",profile)==null,"Preset index outside installed range remains unknown");((Dictionary<string,object>)profile["currentMediaProfile"])["index_format"]="3";((Dictionary<string,object>)profile["currentMediaProfile"])["source"]="2";Check(Call(window,"LcdConfiguredPreset",profile)==null,"Uploaded media source is not a built-in preset");snapshots.Clear();
  var states=(Dictionary<string,Dictionary<string,object>>)Get(window,"gearStates");var tabs=(Dictionary<string,string>)Get(window,"gearTabs");
  foreach(string kind in new[]{"keyboard","mouse","headset"})foreach(int width in new[]{1180,1538}){
   tabs[kind]="固件更新";states[kind]=Obj("deviceId","synthetic-"+kind,"online",true,"snapshot",Obj("firmware","1.2.3","dongleFirmware","4.5.6"));var frame=Render(window,"device:"+kind,width,"firmware-"+kind);var nodes=Walk(frame).ToArray();Check(nodes.OfType<TextBlock>().Any(t=>t.Text=="设备固件版本：1.2.3")&&nodes.OfType<TextBlock>().Any(t=>t.Text=="接收器固件版本：4.5.6"),"Firmware page shows only observed device and receiver versions: "+kind);Check(nodes.OfType<Button>().Where(b=>Convert.ToString(b.Content)=="检查更新").All(b=>!b.IsEnabled),"Hidden render cannot start a firmware lookup: "+kind);Check(!nodes.OfType<Image>().Any(),"Firmware page uses authored drawing without product image: "+kind);WorkersStopped(window);
  }
  states["mouse"]=Obj("online",false,"snapshot",Obj("firmware","stale-version","dongleFirmware","stale-receiver"));var offline=Walk(Render(window,"device:mouse",1538,"firmware-offline")).OfType<TextBlock>().Select(t=>t.Text).ToArray();Check(offline.Contains("设备固件版本：—")&&offline.Contains("接收器固件版本：—")&&!offline.Any(t=>t.Contains("stale")),"Offline firmware never presents cached values as current");WorkersStopped(window);Console.WriteLine("Motherboard and firmware GUI: "+checks+" checks passed. Synthetic state and hidden rendering only; no device writes.");app.Shutdown();
 }
}
