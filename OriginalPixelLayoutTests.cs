// SPDX-License-Identifier: MIT
// Pixel-size invariants with fake DPI matrices and unshown WPF roots only.
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RogControl;

public static class OriginalPixelLayoutTests {
 static int checks;
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool yes,string label){checks++;if(!yes)throw new Exception(label);}
 static void Near(double actual,double expected,string label){Check(Math.Abs(actual-expected)<=0.00001*Math.Max(1,Math.Abs(expected)),label+" actual="+actual+" expected="+expected);}
 static object Invoke(MainWindow window,string method,params object[] args){return typeof(MainWindow).GetMethod(method,Hidden).Invoke(window,args);}
 static bool Apply(MainWindow window,double sx,double sy){return (bool)Invoke(window,"ApplyOriginalPixelScale",sx,sy);}
 static ScaleTransform Transform(MainWindow window){return ((FrameworkElement)window.Content).LayoutTransform as ScaleTransform;}
 static void Set(MainWindow window,string field,object value){typeof(MainWindow).GetField(field,Hidden).SetValue(window,value);}
 static void NoWindow(MainWindow window,string label){Check(!window.IsVisible&&PresentationSource.FromVisual(window)==null,label+" leaves HWND absent");}
 static void PhysicalInvariant(MainWindow window,double width,double height,double sx,double sy,string label){
  Near(window.Width*sx,width,label+" width pixels");Near(window.Height*sy,height,label+" height pixels");Near(window.MinWidth*sx,1180,label+" original physical minimum width");Near(window.MinHeight*sy,760,label+" original physical minimum height");
  var transform=Transform(window);Check(transform!=null&&transform.IsFrozen,label+" absolute inverse is immutable");Near(transform.ScaleX*sx,1,label+" x content scale cancels system DPI");Near(transform.ScaleY*sy,1,label+" y content scale cancels system DPI");
  var root=(Grid)window.Content;Near(root.ColumnDefinitions[0].Width.Value*transform.ScaleX*sx,248,label+" sidebar stays 248 physical pixels");
  var captured=window.CaptureOriginalPixelSize();Near(captured.Width,width,label+" captured width is physical");Near(captured.Height,height,label+" captured height is physical");NoWindow(window,label);
 }
 static void StartupPhases(){
  // SourceInitialized precedes Loaded. Framework dimensions in this interval
  // are not evidence of a user resize, and an early preview capture must not
  // promote the old DIP dimensions into a new physical restoration target.
  foreach(double dpi in new[]{1.0,1.25,1.5,2.0})foreach(Size target in new[]{new Size(1538,860),new Size(2307,1290),new Size(2371,1217)}) {
   var window=new MainWindow(true);window.Left=217.5;window.Top=103.25;window.SetOriginalPixelTarget(target.Width,target.Height);
   Check((bool)Invoke(window,"BeginOriginalPixelStartup",dpi,dpi),"first own-source startup calibration begins");
   window.Width=target.Width;window.Height=target.Height; // Simulated first framework size synchronization.
   Invoke(window,"RememberOriginalPixelBounds");var early=window.CaptureOriginalPixelSize();Near(early.Width,target.Width,"startup capture cannot save an enlarged temporary width");Near(early.Height,target.Height,"startup capture cannot save an enlarged temporary height");
   Size restored;Check(OriginalPixelSizing.TryRestore(1,window.Width,window.Height,early.Width,early.Height,out restored),"early versioned snapshot remains restorable");Near(restored.Width,target.Width,"explicit physical size outranks temporary DIP field");
   Check((bool)Invoke(window,"CompleteOriginalPixelStartup",dpi,dpi),"Loaded-phase calibration completes");PhysicalInvariant(window,target.Width,target.Height,dpi,dpi,"settled custom startup "+dpi);Near(window.Left,217.5,"startup correction does not move left");Near(window.Top,103.25,"startup correction does not move top");
   window.Width=3107/dpi;window.Height=1373/dpi;var resized=window.CaptureOriginalPixelSize();Near(resized.Width,3107,"after-loaded user width is accepted");Near(resized.Height,1373,"after-loaded user height is accepted");
   Check((bool)Invoke(window,"CompleteOriginalPixelStartup",dpi,dpi),"repeated Loaded phase is harmless");Near(window.Width*dpi,3107,"repeated Loaded does not reset a later user resize");
   Check((bool)Invoke(window,"BeginOriginalPixelStartup",dpi,dpi),"repeated source notification is harmless");Near(window.Width*dpi,3107,"repeated startup begin does not reset custom user dimensions");NoWindow(window,"startup phase simulation");
  }
  var replaced=new MainWindow(true);replaced.SetOriginalPixelTarget(1538,860);Invoke(replaced,"BeginOriginalPixelStartup",1.5,1.5);Check(replaced.SetOriginalPixelTarget(2307,1290),"explicit custom restore is accepted while startup is pending");replaced.Width=1538;replaced.Height=860;Near(replaced.CaptureOriginalPixelSize().Width,2307,"pending guard retains explicit custom restoration, not the default");Check(!(bool)Invoke(replaced,"CompleteOriginalPixelStartup",0.0,1.5),"bad source scale cannot settle or change protected target");Near(replaced.CaptureOriginalPixelSize().Width,2307,"invalid phase completion cannot promote transient dimensions");Invoke(replaced,"CompleteOriginalPixelStartup",1.5,1.5);PhysicalInvariant(replaced,2307,1290,1.5,1.5,"replacement custom restore");
  // The previous implementation guarded SizeChanged but not Capture. This
  // directly simulates its DPI-to-Loaded interval with no native DPI event.
  var transition=new MainWindow(true);transition.SetOriginalPixelTarget(1538,860);Apply(transition,1.5,1.5);Set(transition,"originalPixelApplying",true);transition.Width=1538;transition.Height=860;Near(transition.CaptureOriginalPixelSize().Width,1538,"DPI transition capture retains its authoritative target");Near(transition.CaptureOriginalPixelSize().Height,860,"DPI transition height is not multiplied twice");Apply(transition,2,2);Set(transition,"originalPixelApplying",false);PhysicalInvariant(transition,1538,860,2,2,"DPI phase settles at next scale");
  var invalid=new MainWindow(true);invalid.SetOriginalPixelTarget(2307,1290);Check(!(bool)Invoke(invalid,"BeginOriginalPixelStartup",Double.NaN,1.5),"invalid startup source is rejected");Near(invalid.Width,2307,"failed startup source has no partial size mutation");Check(!(bool)typeof(MainWindow).GetField("originalPixelStartupPending",Hidden).GetValue(invalid),"failed startup begins no stuck guard");
  var closing=new MainWindow(true);closing.SetOriginalPixelTarget(2307,1290);Invoke(closing,"BeginOriginalPixelStartup",1.5,1.5);Invoke(closing,"OriginalPixelWindowClosed",null,EventArgs.Empty);double width=closing.Width;Check(!(bool)Invoke(closing,"CompleteOriginalPixelStartup",2.0,2.0),"closed window rejects deferred startup correction");Near(closing.Width,width,"closed window is not resized by a late phase");Check(typeof(MainWindow).GetField("originalPixelDeferred",Hidden).GetValue(closing)==null,"closed startup retains no dispatcher work");NoWindow(closing,"closed phase fixture");
 }
 [STAThread]public static int Main(){try{
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  foreach(double dpi in new[]{1.0,1.25,1.5,2.0}){
   var window=new MainWindow(true);double left=217.5,top=103.25;window.Left=left;window.Top=top;
   Invoke(window,"BeginOriginalPixelLayout");Check(!(bool)typeof(MainWindow).GetField("originalPixelStarted",Hidden).GetValue(window),"render-only does not subscribe native DPI or source events");
   Check(window.SetOriginalPixelTarget(1538,860),"default physical target accepted before source");Check(window.OriginalPixelLayoutReady&&Transform(window).ScaleX==1&&Transform(window).ScaleY==1,"render-only target applies an absolute 1x layout without source");Near(window.Width,1538,"unshown target does not divide initial logical property");
   Check(Apply(window,dpi,dpi),"fake own-source scale applied");PhysicalInvariant(window,1538,860,dpi,dpi,"default "+dpi);Near(window.Left,left,"left is not divided");Near(window.Top,top,"top is not divided");
   var first=Transform(window);for(int repeat=0;repeat<20;repeat++)Check(Apply(window,dpi,dpi),"repeat native source notification succeeds");PhysicalInvariant(window,1538,860,dpi,dpi,"repeated absolute application "+dpi);Check(!Object.ReferenceEquals(first,Transform(window)),"a fresh absolute transform replaces prior transform");
   // A user resize is captured from current logical bounds; it is not reset
   // to the original authored dimensions on the next preview restoration.
   window.Width=2371/dpi;window.Height=1217/dpi;var resized=window.CaptureOriginalPixelSize();Near(resized.Width,2371,"user width retained");Near(resized.Height,1217,"user height retained");
   for(int cycle=0;cycle<20;cycle++){
    Size pixels;Check(OriginalPixelSizing.TryRestore(window.OriginalPixelLayoutVersion,window.Width,window.Height,resized.Width,resized.Height,out pixels),"versioned warm reload accepts pixel snapshot");
    Check(window.SetOriginalPixelTarget(pixels.Width,pixels.Height),"warm target set");double next=new[]{1.0,1.25,1.5,2.0}[cycle%4];Check(Apply(window,next,next),"warm new monitor scale");PhysicalInvariant(window,2371,1217,next,next,"warm "+dpi+"/"+cycle);resized=window.CaptureOriginalPixelSize();
   }
  }
  var coldTarget=new Size(2639,1433);
  for(int cycle=0;cycle<20;cycle++){
   var replacement=new MainWindow(true);Size restored;Check(OriginalPixelSizing.TryRestore(1,999,999,coldTarget.Width,coldTarget.Height,out restored),"cold reload ignores old DIP fields when physical metadata exists");
   Check(replacement.SetOriginalPixelTarget(restored.Width,restored.Height),"cold target stored before source");double next=new[]{1.0,1.25,1.5,2.0}[cycle%4];Check(Apply(replacement,next,next),"cold fake source initialized");PhysicalInvariant(replacement,2639,1433,next,next,"cold "+cycle);coldTarget=replacement.CaptureOriginalPixelSize();
  }
  Size legacy;Check(OriginalPixelSizing.TryRestore(0,1538,860,0,0,out legacy),"unversioned dimensions migrate once as intended pixels");var migrated=new MainWindow(true);migrated.SetOriginalPixelTarget(legacy.Width,legacy.Height);Apply(migrated,1.5,1.5);PhysicalInvariant(migrated,1538,860,1.5,1.5,"legacy at 150 percent");Near(migrated.Width,1025.3333333333333,"legacy is not restored to an enlarged 2307-pixel width");
  var safe=new MainWindow(true);safe.SetOriginalPixelTarget(1538,860);Apply(safe,1.5,1.5);var last=Transform(safe);double previousWidth=safe.Width,previousHeight=safe.Height;
  foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity,Double.Epsilon,0.499,8.001,1000.0}){Check(!Apply(safe,invalid,1.5)&&!Apply(safe,1.5,invalid),"invalid DPI leaves layout untouched");Check(Object.ReferenceEquals(last,Transform(safe))&&safe.Width==previousWidth&&safe.Height==previousHeight,"failed DPI does not partially mutate size or transform");}
  foreach(double invalid in new[]{0.0,-1.0,Double.NaN,Double.PositiveInfinity,Double.MaxValue,65537}){Check(!safe.SetOriginalPixelTarget(invalid,860)&&!safe.SetOriginalPixelTarget(1538,invalid),"invalid saved pixel size rejected");Check(Object.ReferenceEquals(last,Transform(safe)),"invalid snapshot does not change live inverse");}
  Size unused;Check(!OriginalPixelSizing.TryRestore(2,1538,860,1538,860,out unused),"unknown future schema is not interpreted as legacy");Check(!OriginalPixelSizing.TryRestore(1,1538,860,0,0,out unused),"missing versioned physical dimensions are not secretly divided again");
  Check(safe.SetOriginalPixelTarget(15700,9200),"large user-defined size is not capped at design or monitor size");PhysicalInvariant(safe,15700,9200,1.5,1.5,"large custom size");
  Check(safe.SetOriginalPixelTarget(100,100),"existing physical minimum enforced");PhysicalInvariant(safe,1180,760,1.5,1.5,"minimum dimensions");
  var asymmetric=new MainWindow(true);asymmetric.SetOriginalPixelTarget(1538,860);Apply(asymmetric,1.25,1.5);PhysicalInvariant(asymmetric,1538,860,1.25,1.5,"independent x/y scale");
  var selected=OriginalPixelSizing.SelectLogicalBounds(false,new Size(3000,2000),new Rect(50,70,1024,574),new Size(1538,860));Near(selected.Width,1024,"maximized selects restore width instead of full screen width");Near(selected.Height,574,"maximized selects restore height instead of full screen height");
  selected=OriginalPixelSizing.SelectLogicalBounds(false,new Size(3000,2000),Rect.Empty,new Size(1024,574));Near(selected.Width,1024,"unshown maximized window uses last restore dimensions");
  selected=OriginalPixelSizing.SelectLogicalBounds(true,new Size(2000,1000),new Rect(50,70,1024,574),new Size(1538,860));Near(selected.Width,2000,"normal user resize overrides older restore bounds");
  var max=new MainWindow(true);max.SetOriginalPixelTarget(2038,1160);max.WindowState=WindowState.Maximized;Check(Apply(max,1.5,1.5)&&max.WindowState==WindowState.Maximized,"DPI normalization retains maximized state");var maximum=max.CaptureOriginalPixelSize();Near(maximum.Width,2038,"maximized capture preserves intended restore pixels");Near(maximum.Height,1160,"maximized restore height preserved");NoWindow(max,"maximized fixture");
  var verify=new MainWindow(true);Check(verify.SetOriginalPixelTarget(1400,840),"unshown preview verification restores a custom target");Near(verify.Width,1400,"unshown restored Width is actual WPF property, not only cached target");Near(verify.Height,840,"unshown restored Height is actual WPF property");Near(verify.CaptureOriginalPixelSize().Width,1400,"unshown capture agrees with verified Width");NoWindow(verify,"preview verification fixture");
  var preSource=new MainWindow(true);preSource.Width=1400;preSource.Height=840;Near(preSource.CaptureOriginalPixelSize().Width,1400,"unshown capture before any setter uses actual Width");Near(preSource.CaptureOriginalPixelSize().Height,840,"unshown capture before any setter uses actual Height");NoWindow(preSource,"pre-source capture fixture");
  var frame=(FrameworkElement)migrated.Content;frame.Measure(new Size(1538/1.5,826/1.5));frame.Arrange(new Rect(0,0,1538/1.5,826/1.5));frame.UpdateLayout();Near(frame.ActualWidth*Transform(migrated).ScaleX*1.5,1538,"headless layout receives full physical design width after inverse measure");
  Check(typeof(MainWindow).GetField("originalPixelDeferred",Hidden).GetValue(migrated)==null,"pure scale application creates no dispatcher/native DPI work");NoWindow(migrated,"final fixture");StartupPhases();app.Shutdown();Console.WriteLine("Original pixel layout: "+checks+" pure size invariants passed; 100/125/150/200 percent, 20 warm/cold reloads and startup phases; native source/window operations=0.");return 0;
 }catch(Exception error){Console.Error.WriteLine("Original pixel layout failed after "+checks+": "+(error.InnerException??error));return 1;}}
}
