using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RogControl {
 public partial class MainWindow {
  const string OriginalDeviceAssets = @"C:\ProgramData\ASUS\ROG Live Service\DeviceContent";
  readonly Dictionary<string,BitmapSource> originalImageCache=new Dictionary<string,BitmapSource>(StringComparer.OrdinalIgnoreCase);

  // Armoury Crate's dashboard hierarchy is retained: system showcase and
  // devices on the left, system monitoring and fan modes on the right.
  void OriginalDashboard() {
   var title=Label("控制面板",30,text,FontWeights.SemiBold);title.Margin=new Thickness(0,16,0,20);content.Children.Add(title);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(44,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(56,GridUnitType.Star)});
   var left=new StackPanel();var right=new StackPanel{Margin=new Thickness(0,27,0,0)};Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   left.Children.Add(OriginalMachineShowcase());left.Children.Add(OriginalAiOverclock());left.Children.Add(OriginalDevicesShowcase());
   right.Children.Add(OriginalMonitoring());right.Children.Add(OriginalFanModes());content.Children.Add(body);
  }

  Border OriginalMachineShowcase() {
   var stack=new StackPanel();
   var picture=OriginalInstalledImage(OriginalProductImage("motherboard"),270,270);picture.Margin=new Thickness(0);stack.Children.Add(picture);
   var board=Label(OriginalDashboardValue(info.Board),18,text,FontWeights.SemiBold);board.TextAlignment=TextAlignment.Center;board.TextTrimming=TextTrimming.CharacterEllipsis;board.ToolTip=info.Board;board.Margin=new Thickness(0,0,0,4);stack.Children.Add(board);
   var cpu=Label(OriginalDashboardValue(info.Cpu),11,muted);cpu.TextAlignment=TextAlignment.Center;cpu.TextTrimming=TextTrimming.CharacterEllipsis;cpu.ToolTip=info.Cpu;cpu.Margin=new Thickness(0,0,0,4);stack.Children.Add(cpu);
   var memory=Label("DRAM ("+OriginalDashboardValue(info.Memory)+" & "+OriginalDashboardValue(info.MemorySpeed)+") | BIOS ver"+OriginalDashboardValue(info.Bios),11,muted);memory.TextAlignment=TextAlignment.Center;memory.TextTrimming=TextTrimming.CharacterEllipsis;memory.Margin=new Thickness(0);stack.Children.Add(memory);
   var card=OriginalPanel(stack,new Thickness(8),new Thickness(0,0,0,8));card.MaxWidth=390;card.MinHeight=343;card.HorizontalAlignment=HorizontalAlignment.Stretch;card.Background=Brush("#171717");card.BorderBrush=Brush("#2C2C2C");return card;
  }
  static string OriginalDashboardValue(string value){return String.IsNullOrWhiteSpace(value)||value=="读取中"||value=="未知"||value.Contains("暂不可用")?"—":value;}

  StackPanel OriginalAiOverclock() {
   var ai=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,48)};
   ai.Children.Add(new CheckBox{Foreground=muted,IsEnabled=false,IsThreeState=true,IsChecked=null,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,17,0),ToolTip="状态未读取"});
   var title=Label("开启 AI 超频",14,text);title.Margin=new Thickness(0);title.VerticalAlignment=VerticalAlignment.Center;ai.Children.Add(title);return ai;
  }

  void OriginalSpec(Grid grid,int row,string name,string value) {
   var label=Label(name,12,muted);label.Margin=new Thickness(0,3,17,3);Grid.SetRow(label,row);grid.Children.Add(label);
   var detail=Paragraph(value,12,text);detail.LineHeight=18;detail.Margin=new Thickness(0,3,0,3);Grid.SetRow(detail,row);Grid.SetColumn(detail,1);grid.Children.Add(detail);
  }

  Border OriginalDevicesShowcase() {
   var stack=new StackPanel{MaxWidth=410,HorizontalAlignment=HorizontalAlignment.Center};var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   heading.Children.Add(OriginalSectionHeading("设备"));
   var all=Button("显示所有",()=>ShowPage("设备"));all.Padding=new Thickness(0);all.FontSize=12;all.Margin=new Thickness(8,0,0,0);all.Background=Brushes.Transparent;all.BorderThickness=new Thickness(0);Grid.SetColumn(all,1);heading.Children.Add(all);stack.Children.Add(heading);stack.Children.Add(new Border{Height=1,Background=muted,Margin=new Thickness(0,8,0,17)});
   var cards=new Grid();for(int i=0;i<3;i++)cards.ColumnDefinitions.Add(new ColumnDefinition());
   string[] names={"ROG 夜魔 EXTREME 二十周年版","ROG HARPE II EXTREME EDITION 20","ROG 棱镜 2 - KJP"};
   string[] paths={OriginalProductImage("keyboard"),OriginalProductImage("mouse"),OriginalProductImage("headset")};
   for(int i=0;i<3;i++) {
    int index=i;var item=new StackPanel();var image=OriginalInstalledImage(paths[i],128,128);image.Margin=new Thickness(0,0,0,0);item.Children.Add(image);
    var name=Paragraph(names[i],12,muted);name.LineHeight=18;name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0);name.MinHeight=36;item.Children.Add(name);
    var button=OriginalTransparentProductButton(item,()=>ShowPage("设备"));button.Margin=new Thickness(i==0?0:4,0,i==2?0:4,0);Grid.SetColumn(button,index);cards.Children.Add(button);
   }
   stack.Children.Add(cards);stack.Children.Add(new Border{Height=1,Background=muted,Margin=new Thickness(0,34,0,0)});return new Border{Child=stack,Background=Brushes.Transparent};
  }

  Border OriginalMonitoring() {
   return OriginalMonitoringLayout(false);
  }

  Border OriginalFanModes() {
   var stack=new StackPanel();var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   heading.Children.Add(OriginalSectionHeading("风扇模式"));stack.Children.Add(heading);
   var modes=new Grid{Margin=new Thickness(0,46,0,0)};for(int i=0;i<5;i++)modes.ColumnDefinitions.Add(new ColumnDefinition());
   string[] names={"安静","标准","加速","全速","自定义"};
   for(int i=0;i<names.Length;i++) {
    var item=new StackPanel();
    item.Children.Add(OriginalFanModeGlyph(i));
    var name=Label(names[i],16,text);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0,9,0,0);item.Children.Add(name);
    var b=Button("",()=>{});b.Content=item;b.IsEnabled=false;b.Opacity=0.45;b.Margin=new Thickness(i==0?0:5,0,i==4?0:5,0);b.Background=Brush("#00000000");b.BorderThickness=new Thickness(0);
    var template=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(5,5,5,5));template.VisualTree=presenter;b.Template=template;Grid.SetColumn(b,i);modes.Children.Add(b);
   }
   stack.Children.Add(modes);return new Border{Child=stack,Background=Brush("#00000000")};
  }
  UIElement OriginalSectionHeading(string caption){var row=new StackPanel{Orientation=Orientation.Horizontal};var motif=new Canvas{Width=35,Height=16,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center};for(int i=0;i<3;i++)motif.Children.Add(new System.Windows.Shapes.Polygon{Fill=Brush("#F00024"),Points=new PointCollection{new Point(i*8,2),new Point(i*8+4,2),new Point(i*8+16,14),new Point(i*8+12,14)}});row.Children.Add(motif);var label=Label(caption,16,text);label.Margin=new Thickness(0);row.Children.Add(label);return row;}
  UIElement OriginalFanModeGlyph(int mode){var canvas=new Canvas{Width=37,Height=37,HorizontalAlignment=HorizontalAlignment.Center};string[] paths=mode==4?new[]{"M9,2 L9,35 M27,2 L27,35 M4,11 L14,11 M22,25 L32,25"}:mode==3?new[]{"M2,8 L35,8 M5,14 L32,14 M8,20 L29,20 M11,26 L26,26 M15,32 L22,32"}:new[]{"M2,13 L23,13 C31,13 31,4 25,4 C21,4 19,6 19,8","M2,20 L30,20 C36,20 36,29 30,29 C27,29 25,27 25,25","M5,27 L18,27"};foreach(string data in paths)canvas.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Stroke=text,StrokeThickness=1.4});return canvas;}
  Button OriginalTransparentProductButton(UIElement child,Action click){var button=new Button{Content=child,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Cursor=System.Windows.Input.Cursors.Hand};var template=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);template.VisualTree=presenter;button.Template=template;button.Click+=(s,e)=>click();return button;}

  Border OriginalPanel(UIElement child,Thickness padding,Thickness margin) {
   return new Border{Background=panel,BorderBrush=border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Padding=padding,Margin=margin,Child=child};
  }

  void OriginalDeviceGallery() {
   var title=Label("设备",30,text,FontWeights.SemiBold);title.Margin=new Thickness(14,0,0,56);content.Children.Add(title);
   var products=new WrapPanel{ItemWidth=210,ItemHeight=270,MaxWidth=1050,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(14,0,0,0)};
   string[] names={OriginalDashboardValue(info.Board),"ROG 夜魔 EXTREME 二十周年版","ROG HARPE II EXTREME EDITION 20","ROG 棱镜 2 - KJP","ROG OMNI RECEIVER","ROG EURUX","ASTRAL-RTX5090"};
   string[] targets={"motherboard","keyboard","mouse","headset","receiver","fan","gpu"};
   for(int i=0;i<targets.Length;i++) {
    string target=targets[i];var item=new StackPanel{VerticalAlignment=VerticalAlignment.Top};var image=OriginalInstalledImage(OriginalProductImage(target),210,210);image.Margin=new Thickness(0);item.Children.Add(image);
    var name=Paragraph(names[i],13,muted);name.Width=180;name.HorizontalAlignment=HorizontalAlignment.Center;name.LineHeight=20;name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0);item.Children.Add(name);
    products.Children.Add(OriginalTransparentProductButton(item,()=>OpenOriginalDevice(target)));
   }
   content.Children.Add(products);

  }

  string OriginalProductImage(string target) {
   // Product images keep their original canvas and aspect. The metadata
   // records official sources; public builds do not bundle ASUS artwork.
   string previewImageRoot=AppDomain.CurrentDomain.GetData("OpenAC.ProductImageRoot") as string;
   if(!String.IsNullOrWhiteSpace(previewImageRoot)&&new[]{"motherboard","keyboard","mouse","headset","fan","gpu","receiver"}.Contains(target,StringComparer.Ordinal)) {
    try{string previewImage=Path.Combine(Path.GetFullPath(previewImageRoot),target+".png");if(File.Exists(previewImage))return previewImage;}catch{}
   }
   string bundled=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","products",target+".png");
   if(File.Exists(bundled))return bundled;
   string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Packages\B9ECED6F.ArmouryCrate_qmba6cd70vzyy\LocalState\ProductPNG");
   string image=null,fallback=null;
   if(target=="motherboard"){image=Path.Combine(cache,"90MB1LB0-M0EAY0_crop.png");fallback=Path.Combine(OriginalDeviceAssets,"ROG CROSSHAIR X870E EXTREME","ALL MB.png");}
   else if(target=="keyboard"){image=Path.Combine(cache,"90MP04VF-BKUA20_crop.png");fallback=Path.Combine(OriginalDeviceAssets,"ROG AZOTH EXTREME EDITION 20","ROG Azoth Extreme Edition 20.png");}
   else if(target=="mouse"){image=Path.Combine(cache,"90MP04Q0-BMUA00_crop.png");fallback=Path.Combine(OriginalDeviceAssets,"ROG HARPE II EXTREME EDITION 20","ROG HARPE II EXTREME EDITION 20.png");}
   else if(target=="headset"){image=Path.Combine(cache,"90YH04C0-BHUA10_crop.png");fallback=Path.Combine(OriginalDeviceAssets,"ROG DELTA II-KJP","ROG DELTA II-KJP.png");}
   else if(target=="gpu"){image=Path.Combine(OriginalDeviceAssets,"ASTRAL-RTX5090","ASTRAL-RTX5090.png");}
   else {
    string package=OriginalInstalledPackage();
    if(package!=null&&target=="receiver")image=Path.Combine(package,@"DevicePlugin\Assets\Omni Receiver_1.png");
    if(package!=null&&target=="fan")image=Path.Combine(package,@"ArmouryCrate.Console.Uwp.CommonControl\Assets\ROG\06_Devices\ac_device default_img_fancontroller.png");
   }
   if(image!=null&&File.Exists(image))return image;return fallback??"";
  }

  string OriginalInstalledPackage() {
   string known=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),@"WindowsApps\B9ECED6F.ArmouryCrate_6.7.6.0_x64__qmba6cd70vzyy");
   if(Directory.Exists(known))return known;
   try{return Directory.GetDirectories(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps"),"B9ECED6F.ArmouryCrate_*_x64__qmba6cd70vzyy").OrderByDescending(p=>p,StringComparer.OrdinalIgnoreCase).FirstOrDefault();}catch{return null;}
  }

  FrameworkElement OriginalInstalledImage(string path,double height) {return OriginalInstalledImage(path,height,height);}

  FrameworkElement OriginalInstalledImage(string path,double width,double height) {
   // The slot owns both bounds. A height-only Image in a StackPanel can ask
   // for a very large width when a product photo is wide.
   var slot=new Border{Height=height,MaxWidth=width,Background=Brush("#00000000"),BorderThickness=new Thickness(0),ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Stretch};
   if(File.Exists(path)) {
    try {
     string cacheKey=path+"|"+width.ToString(CultureInfo.InvariantCulture)+"|"+height.ToString(CultureInfo.InvariantCulture);BitmapSource source;
     if(!originalImageCache.TryGetValue(cacheKey,out source)) {
      var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=(int)Math.Ceiling(width*2);bitmap.UriSource=new Uri(path,UriKind.Absolute);bitmap.EndInit();bitmap.Freeze();source=bitmap;
      originalImageCache[cacheKey]=source;
     }
     var image=new Image{Source=source,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch};
     RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);slot.Child=image;return slot;
    }catch{}
   }
   return slot;
  }

 }
}
