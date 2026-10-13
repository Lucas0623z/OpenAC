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
   var title=Label("控制面板",25,text,FontWeights.SemiBold);title.Margin=new Thickness(0,0,0,18);content.Children.Add(title);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(42,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(58,GridUnitType.Star)});
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   left.Children.Add(OriginalMachineShowcase());left.Children.Add(OriginalAiOverclock());left.Children.Add(OriginalDevicesShowcase());
   right.Children.Add(OriginalMonitoring());right.Children.Add(OriginalFanModes());content.Children.Add(body);
  }

  Border OriginalMachineShowcase() {
   var stack=new StackPanel();
   var picture=OriginalInstalledImage(OriginalProductImage("motherboard"),280,168);
   picture.Margin=new Thickness(0,0,0,12);stack.Children.Add(picture);
   var board=Paragraph(info.Board,18,text);board.FontWeight=FontWeights.SemiBold;board.TextAlignment=TextAlignment.Center;board.Margin=new Thickness(0,0,0,13);stack.Children.Add(board);
   var specs=new Grid();specs.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});specs.ColumnDefinitions.Add(new ColumnDefinition());
   for(int i=0;i<3;i++)specs.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   OriginalSpec(specs,0,"CPU",info.Cpu);OriginalSpec(specs,1,"DRAM",info.Memory+" | "+info.MemorySpeed+" MT/s");OriginalSpec(specs,2,"BIOS",info.Bios);stack.Children.Add(specs);
   return OriginalPanel(stack,new Thickness(22,17,22,17),new Thickness(0,0,0,13));
  }

  StackPanel OriginalAiOverclock() {
   var ai=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,23)};
   var title=Label("AI 超频",14,text);title.Margin=new Thickness(0);title.VerticalAlignment=VerticalAlignment.Center;ai.Children.Add(title);
   ai.Children.Add(new CheckBox{Foreground=muted,IsEnabled=false,IsChecked=false,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(17,0,0,0)});return ai;
  }

  void OriginalSpec(Grid grid,int row,string name,string value) {
   var label=Label(name,12,muted);label.Margin=new Thickness(0,3,17,3);Grid.SetRow(label,row);grid.Children.Add(label);
   var detail=Paragraph(value,12,text);detail.LineHeight=18;detail.Margin=new Thickness(0,3,0,3);Grid.SetRow(detail,row);Grid.SetColumn(detail,1);grid.Children.Add(detail);
  }

  Border OriginalDevicesShowcase() {
   var stack=new StackPanel();var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var label=Label("设备 (6)",17,text,FontWeights.SemiBold);label.Margin=new Thickness(0);heading.Children.Add(label);
   var all=Button("显示所有",()=>ShowPage("设备"));all.Padding=new Thickness(10,5,10,5);all.FontSize=11;all.Margin=new Thickness(8,0,0,0);all.Background=Brush("#191919");all.BorderBrush=border;Grid.SetColumn(all,1);heading.Children.Add(all);stack.Children.Add(heading);
   var cards=new Grid{Margin=new Thickness(0,15,0,0)};for(int i=0;i<3;i++)cards.ColumnDefinitions.Add(new ColumnDefinition());
   string[] names={"ROG AZOTH EXTREME EDITION 20","ROG HARPE II EXTREME EDITION 20","ROG DELTA II-KJP"};
   string[] paths={OriginalProductImage("keyboard"),OriginalProductImage("mouse"),OriginalProductImage("headset")};
   string[] kinds={"键盘","鼠标","耳机"};
   for(int i=0;i<3;i++) {
    int index=i;var item=new StackPanel();var image=OriginalInstalledImage(paths[i],140,70);image.Margin=new Thickness(0,2,0,8);item.Children.Add(image);
    var name=Paragraph(names[i],10,text);name.LineHeight=15;name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0,0,0,7);name.MinHeight=30;item.Children.Add(name);
    var kind=Label(kinds[i],10,muted);kind.TextAlignment=TextAlignment.Center;kind.Margin=new Thickness(0);item.Children.Add(kind);
    var button=Button("",()=>ShowPage("设备"));button.Content=item;button.Padding=new Thickness(9);button.Margin=new Thickness(i==0?0:5,0,i==2?0:5,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Background=Brush("#202020");button.BorderBrush=Brush("#343434");Grid.SetColumn(button,index);cards.Children.Add(button);
   }
   stack.Children.Add(cards);return new Border{Child=stack,Background=Brush("#00000000")};
  }

  Border OriginalMonitoring() {
   return OriginalMonitoringLayout(false);
  }

  Border OriginalFanModes() {
   var stack=new StackPanel();var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition());heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var title=Label("风扇模式",17,text,FontWeights.SemiBold);title.Margin=new Thickness(0);heading.Children.Add(title);stack.Children.Add(heading);
   var modes=new Grid{Margin=new Thickness(0,17,0,0)};for(int i=0;i<5;i++)modes.ColumnDefinitions.Add(new ColumnDefinition());
   string[] names={"安静","标准","加速","全速","自定义"};
   string[] iconNames={"OSD_Silent.png","OSD_Standard.png","OSD_Turbo.png","OSD_FullSpeed.png"};
   for(int i=0;i<names.Length;i++) {
    var item=new StackPanel();
    if(i<4)item.Children.Add(OriginalInstalledImage(Path.Combine(@"C:\Program Files (x86)\ASUS\ArmouryDevice\dll\SwAgent\FanOSD",iconNames[i]),37));
    else{var gear=Label("⚙",31,text);gear.Margin=new Thickness(0);gear.Height=37;gear.TextAlignment=TextAlignment.Center;item.Children.Add(gear);}
    var name=Label(names[i],12,text);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0,9,0,0);item.Children.Add(name);
    var b=Button("",()=>{});b.Content=item;b.IsEnabled=false;b.Opacity=0.45;b.Margin=new Thickness(i==0?0:5,0,i==4?0:5,0);b.Background=Brush("#00000000");b.BorderThickness=new Thickness(0);
    var template=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(5,5,5,5));template.VisualTree=presenter;b.Template=template;Grid.SetColumn(b,i);modes.Children.Add(b);
   }
   stack.Children.Add(modes);return new Border{Child=stack,Background=Brush("#00000000")};
  }

  Border OriginalPanel(UIElement child,Thickness padding,Thickness margin) {
   return new Border{Background=panel,BorderBrush=border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Padding=padding,Margin=margin,Child=child};
  }

  void OriginalDeviceGallery() {
   var title=Label("设备",25,text,FontWeights.SemiBold);title.Margin=new Thickness(0,0,0,38);content.Children.Add(title);
   var products=new Grid();for(int i=0;i<5;i++)products.ColumnDefinitions.Add(new ColumnDefinition());
   products.RowDefinitions.Add(new RowDefinition{Height=new GridLength(280)});products.RowDefinitions.Add(new RowDefinition{Height=new GridLength(280)});
   string[] names={info.Board,"ROG 夜魔 EXTREME 二十周年版","ROG HARPE II EXTREME EDITION 20","ROG 棱镜 2-KJP","ROG OMNI RECEIVER","ROG EURUX","ASTRAL-RTX5090"};
   string[] targets={"motherboard","keyboard","mouse","headset","receiver","fan","gpu"};
   for(int i=0;i<targets.Length;i++) {
    string target=targets[i];var item=new StackPanel{VerticalAlignment=VerticalAlignment.Top};var image=OriginalInstalledImage(OriginalProductImage(target),210,176);image.Margin=new Thickness(0,0,0,20);item.Children.Add(image);
    var name=Paragraph(names[i],13,text);name.LineHeight=20;name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0);item.Children.Add(name);
    var button=new Button{Content=item,Background=Brush("#00000000"),BorderThickness=new Thickness(0),Padding=new Thickness(14,14,14,8),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Cursor=System.Windows.Input.Cursors.Hand,Margin=new Thickness(4,0,4,0)};
    // A local template keeps gallery products transparent even when global
    // button styles have a bordered panel template.
    var template=new ControlTemplate(typeof(Button));var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.MarginProperty,new Thickness(14,14,14,8));template.VisualTree=presenter;button.Template=template;button.Click+=(s,e)=>OpenOriginalDevice(target);
    Grid.SetColumn(button,i<5?i:i-5);Grid.SetRow(button,i<5?0:1);products.Children.Add(button);
   }
   content.Children.Add(products);
   OriginalDiscoveryGallery(content);
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
