// SPDX-License-Identifier: MIT
// App-local pixel layout. No system DPI setting, native input or other-window API.
// WPF DPI facts: https://learn.microsoft.com/en-us/windows/win32/hidpi/declaring-managed-apps-dpi-aware
// Window DPI event: https://learn.microsoft.com/en-us/dotnet/api/system.windows.window?view=netframework-4.8.1
// Loaded follows initial layout: https://learn.microsoft.com/en-us/dotnet/framework/wpf/advanced/object-lifetime-events
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace RogControl {
 public static class OriginalPixelSizing {
  public const int Version=1;
  public const double DesignWidth=1538,DesignHeight=860,MinimumWidth=1180,MinimumHeight=760;
  public static bool Positive(double value){return !Double.IsNaN(value)&&!Double.IsInfinity(value)&&value>0;}
  // Cached state may not request an extreme HWND size or inverse transform.
  // Normal custom window sizes remain independent of the authored design.
  public static bool PixelsValid(double value){return Positive(value)&&value<=65536;}
  public static bool ScaleValid(double value){return Positive(value)&&value>=0.5&&value<=8;}
  public static bool TryLogical(Size pixels,double scaleX,double scaleY,out Size logical){
   logical=new Size();if(!PixelsValid(pixels.Width)||!PixelsValid(pixels.Height)||!ScaleValid(scaleX)||!ScaleValid(scaleY))return false;
   double width=pixels.Width/scaleX,height=pixels.Height/scaleY;if(!Positive(width)||!Positive(height))return false;logical=new Size(width,height);return true;
  }
  public static bool TryPixels(Size logical,double scaleX,double scaleY,out Size pixels){
   pixels=new Size();if(!Positive(logical.Width)||!Positive(logical.Height)||!ScaleValid(scaleX)||!ScaleValid(scaleY))return false;
   double width=logical.Width*scaleX,height=logical.Height*scaleY;if(!PixelsValid(width)||!PixelsValid(height))return false;pixels=new Size(width,height);return true;
  }
  // Version 0 dimensions were authored as design pixels despite being stored
  // in DIP fields. A versioned snapshot supplies explicit physical dimensions.
  public static bool TryRestore(int version,double width,double height,double widthPixels,double heightPixels,out Size pixels){
   pixels=new Size();if(version!=0&&version!=Version)return false;double w=version==0?width:widthPixels,h=version==0?height:heightPixels;
   if(!PixelsValid(w)||!PixelsValid(h))return false;pixels=new Size(Math.Max(MinimumWidth,w),Math.Max(MinimumHeight,h));return true;
  }
  public static Size SelectLogicalBounds(bool normal,Size normalSize,Rect restoreBounds,Size fallback){
   if(!normal&&!restoreBounds.IsEmpty&&Positive(restoreBounds.Width)&&Positive(restoreBounds.Height))return restoreBounds.Size;
   if(normal&&Positive(normalSize.Width)&&Positive(normalSize.Height))return normalSize;return fallback;
  }
 }

 public partial class MainWindow {
  double originalPixelScaleX=1,originalPixelScaleY=1;
  Size originalPixelTarget=new Size(OriginalPixelSizing.DesignWidth,OriginalPixelSizing.DesignHeight);
  Size originalPixelLastLogical=new Size(OriginalPixelSizing.DesignWidth,OriginalPixelSizing.DesignHeight);
  bool originalPixelStarted,originalPixelReady,originalPixelApplying,originalPixelClosed;
  bool originalPixelStartupPending,originalPixelStartupSettled;
  DispatcherOperation originalPixelDeferred;
  int originalPixelDeferredGeneration;
  public double OriginalPixelScaleX {get{return originalPixelScaleX;}}
  public double OriginalPixelScaleY {get{return originalPixelScaleY;}}
  public int OriginalPixelLayoutVersion {get{return OriginalPixelSizing.Version;}}
  public bool OriginalPixelLayoutReady {get{return originalPixelReady;}}
  public double OriginalPixelWidthPixels {get{return CaptureOriginalPixelSize().Width;}}
  public double OriginalPixelHeightPixels {get{return CaptureOriginalPixelSize().Height;}}

  public Size CaptureOriginalPixelSize(){
   // A startup/DPI transition may temporarily expose the framework's old DIP
   // dimensions. Both persistence and bounds recording must use the same guard.
   if(OriginalPixelBoundsGuarded)return originalPixelTarget;
   if(!originalPixelReady){if(!render)return originalPixelTarget;Size unshown=OriginalPixelSizing.SelectLogicalBounds(WindowState==WindowState.Normal,new Size(Width,Height),RestoreBounds,originalPixelLastLogical),unshownPixels;return OriginalPixelSizing.TryPixels(unshown,1,1,out unshownPixels)?unshownPixels:originalPixelTarget;}
   Size logical=OriginalPixelSizing.SelectLogicalBounds(WindowState==WindowState.Normal,new Size(Width,Height),RestoreBounds,originalPixelLastLogical),pixels;
   return OriginalPixelSizing.TryPixels(logical,originalPixelScaleX,originalPixelScaleY,out pixels)?pixels:originalPixelTarget;
  }
  public bool SetOriginalPixelTarget(double widthPixels,double heightPixels){
   Size target;if(!OriginalPixelSizing.TryRestore(OriginalPixelSizing.Version,0,0,widthPixels,heightPixels,out target))return false;
   originalPixelTarget=target;
   // The actual source DPI does not exist until the window has its source.
   // Before that point, the target is retained without another division.
   if(!originalPixelReady)return !render||ApplyOriginalPixelScale(1,1);
   return ApplyOriginalPixelScale(originalPixelScaleX,originalPixelScaleY);
  }
  void BeginOriginalPixelLayout(){
   if(render||originalPixelStarted)return;originalPixelStarted=true;
   SourceInitialized+=OriginalPixelSourceInitialized;Loaded+=OriginalPixelWindowLoaded;DpiChanged+=OriginalPixelDpiChanged;SizeChanged+=OriginalPixelBoundsChanged;StateChanged+=OriginalPixelWindowStateChanged;Closed+=OriginalPixelWindowClosed;
   double sx,sy;if(TryOriginalPixelSourceScale(out sx,out sy)&&BeginOriginalPixelStartup(sx,sy)&&IsLoaded)QueueOriginalPixelStartupSettle();
  }
  bool TryOriginalPixelSourceScale(out double sx,out double sy){
   sx=sy=1;var source=PresentationSource.FromVisual(this);if(source==null||source.CompositionTarget==null)return false;var matrix=source.CompositionTarget.TransformToDevice;
   if(!OriginalPixelSizing.ScaleValid(matrix.M11)||!OriginalPixelSizing.ScaleValid(matrix.M22)||Math.Abs(matrix.M12)>0.000001||Math.Abs(matrix.M21)>0.000001)return false;
   sx=matrix.M11;sy=matrix.M22;return true;
  }
  // These phase helpers only change this window's WPF properties. They also
  // permit unshown tests to simulate early framework dimensions without HWNDs.
  bool BeginOriginalPixelStartup(double sx,double sy){
   if(closing||originalPixelClosed)return false;if(originalPixelStartupSettled)return true;
   bool pending=originalPixelStartupPending,applying=originalPixelApplying;originalPixelStartupPending=true;originalPixelApplying=true;
   if(ApplyOriginalPixelScale(sx,sy))return true;originalPixelStartupPending=pending;originalPixelApplying=applying;return false;
  }
  bool CompleteOriginalPixelStartup(double sx,double sy){
   if(closing||originalPixelClosed)return false;if(!originalPixelStartupPending)return true;
   if(!ApplyOriginalPixelScale(sx,sy))return false;originalPixelStartupPending=false;originalPixelStartupSettled=true;originalPixelApplying=false;return true;
  }
  void OriginalPixelSourceInitialized(object sender,EventArgs args){
   double sx,sy;if(!closing&&TryOriginalPixelSourceScale(out sx,out sy)&&BeginOriginalPixelStartup(sx,sy)&&IsLoaded)QueueOriginalPixelStartupSettle();
  }
  void OriginalPixelWindowLoaded(object sender,RoutedEventArgs args){
   if(closing||originalPixelClosed||originalPixelStartupSettled)return;
   // An HwndSource created before Show may not yet expose this RootVisual at
   // SourceInitialized. Loaded supplies a second, app-local source opportunity.
   double sx,sy;if(TryOriginalPixelSourceScale(out sx,out sy)&&BeginOriginalPixelStartup(sx,sy))QueueOriginalPixelStartupSettle();
  }
  void QueueOriginalPixelStartupSettle(){
   if(!originalPixelStartupPending||closing||originalPixelClosed)return;
   if(originalPixelDeferred!=null&&originalPixelDeferred.Status==DispatcherOperationStatus.Pending)originalPixelDeferred.Abort();int generation=++originalPixelDeferredGeneration;originalPixelApplying=true;
   originalPixelDeferred=Dispatcher.BeginInvoke(new Action(()=>{
    try{if(closing||originalPixelClosed)return;double sx,sy;if(TryOriginalPixelSourceScale(out sx,out sy))CompleteOriginalPixelStartup(sx,sy);}finally{if(generation==originalPixelDeferredGeneration){originalPixelApplying=originalPixelStartupPending;originalPixelDeferred=null;}}
   }),DispatcherPriority.Loaded);
  }
  void OriginalPixelDpiChanged(object sender,DpiChangedEventArgs args){
   if(closing||originalPixelClosed)return;
   // SizeChanged records bounds only while the source DPI still matches our
   // last applied DPI. Thus a framework-suggested transition rectangle cannot
   // turn into a new user resize target before this event is processed.
   Size pixels;if(originalPixelReady&&!originalPixelStartupPending&&OriginalPixelSizing.TryPixels(originalPixelLastLogical,originalPixelScaleX,originalPixelScaleY,out pixels))originalPixelTarget=pixels;
   originalPixelApplying=true;double sx,sy;if(TryOriginalPixelSourceScale(out sx,out sy))ApplyOriginalPixelScale(sx,sy);
   // Reapply once after WPF has finished its own source-DPI transition. Every
   // application is absolute; no prior root transform is multiplied again.
   if(originalPixelDeferred!=null&&originalPixelDeferred.Status==DispatcherOperationStatus.Pending)originalPixelDeferred.Abort();
   int generation=++originalPixelDeferredGeneration;originalPixelApplying=true;originalPixelDeferred=Dispatcher.BeginInvoke(new Action(()=>{
    try{if(closing||originalPixelClosed)return;double nextX,nextY;if(TryOriginalPixelSourceScale(out nextX,out nextY)){if(originalPixelStartupPending&&IsLoaded)CompleteOriginalPixelStartup(nextX,nextY);else ApplyOriginalPixelScale(nextX,nextY);}}finally{if(generation==originalPixelDeferredGeneration){originalPixelApplying=originalPixelStartupPending;originalPixelDeferred=null;}}
   }),DispatcherPriority.Loaded);
  }
  void OriginalPixelBoundsChanged(object sender,SizeChangedEventArgs args){RememberOriginalPixelBounds();}
  void OriginalPixelWindowStateChanged(object sender,EventArgs args){RememberOriginalPixelBounds();}
  bool OriginalPixelBoundsGuarded {get{return originalPixelApplying||originalPixelStartupPending||originalPixelDeferred!=null&&(originalPixelDeferred.Status==DispatcherOperationStatus.Pending||originalPixelDeferred.Status==DispatcherOperationStatus.Executing);}}
  void RememberOriginalPixelBounds(){
   if(!originalPixelReady||OriginalPixelBoundsGuarded||closing||originalPixelClosed)return;double sx,sy;
   if(!TryOriginalPixelSourceScale(out sx,out sy)||Math.Abs(sx-originalPixelScaleX)>0.000001||Math.Abs(sy-originalPixelScaleY)>0.000001)return;
   Size logical=OriginalPixelSizing.SelectLogicalBounds(WindowState==WindowState.Normal,new Size(Width,Height),RestoreBounds,originalPixelLastLogical),pixels;
   if(OriginalPixelSizing.TryPixels(logical,originalPixelScaleX,originalPixelScaleY,out pixels)){originalPixelLastLogical=logical;originalPixelTarget=pixels;}
  }
  bool ApplyOriginalPixelScale(double sx,double sy){
   var root=Content as FrameworkElement;Size logical,minimum;
   if(root==null||!OriginalPixelSizing.TryLogical(originalPixelTarget,sx,sy,out logical)||!OriginalPixelSizing.TryLogical(new Size(OriginalPixelSizing.MinimumWidth,OriginalPixelSizing.MinimumHeight),sx,sy,out minimum))return false;
   bool applying=originalPixelApplying;originalPixelApplying=true;
   try{
    originalPixelScaleX=sx;originalPixelScaleY=sy;originalPixelReady=true;
    var inverse=new ScaleTransform(1/sx,1/sy);inverse.Freeze();root.LayoutTransform=inverse;
    // Width/Height also establish the restore size while maximized. The
    // WindowState and its desktop position are never changed here.
    MinWidth=minimum.Width;MinHeight=minimum.Height;Width=logical.Width;Height=logical.Height;originalPixelLastLogical=logical;
    return true;
   }finally{originalPixelApplying=applying;}
  }
  void OriginalPixelWindowClosed(object sender,EventArgs args){
   originalPixelClosed=true;originalPixelStartupPending=false;originalPixelApplying=false;originalPixelDeferredGeneration++;if(originalPixelDeferred!=null&&originalPixelDeferred.Status==DispatcherOperationStatus.Pending)originalPixelDeferred.Abort();originalPixelDeferred=null;
   SourceInitialized-=OriginalPixelSourceInitialized;Loaded-=OriginalPixelWindowLoaded;DpiChanged-=OriginalPixelDpiChanged;SizeChanged-=OriginalPixelBoundsChanged;StateChanged-=OriginalPixelWindowStateChanged;Closed-=OriginalPixelWindowClosed;
  }
 }
}
