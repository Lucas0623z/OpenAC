using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public partial class MainWindow {
  string fanXpertChannel="CPU Fan";

  void OriginalFanXpertPage() {
   var heading=Label("FAN XPERT 4",26,text,FontWeights.SemiBold);heading.Margin=new Thickness(-22,-6,0,28);heading.HorizontalAlignment=HorizontalAlignment.Left;heading.RenderTransform=new ScaleTransform(1.16,1);content.Children.Add(heading);PinPageHeader(1,"fan-xpert");
   // The authored columns retain their positions at smaller widths through scrolling.
   // Neither the official screenshot nor the private COM probe supplies live UI values.
   var fanContent=new StackPanel{Width=1246};
   var horizontal=new ScrollViewer{Content=fanContent,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left};
   var toolbar=new Canvas{Width=1078,Height=70,Margin=new Thickness(85,67,0,0),HorizontalAlignment=HorizontalAlignment.Left};
   FanPlace(toolbar,FanLayoutSwitch("启用 Fan Xpert 4"),2,24);
   var save=FanPendingButton("","保存风扇配置",30);save.Height=30;save.Padding=new Thickness(0);save.Content=FanFileIcon(false);FanPlace(toolbar,save,77,0);
   var load=FanPendingButton("","载入风扇配置",30);load.Height=30;load.Padding=new Thickness(0);load.Content=FanFileIcon(true);FanPlace(toolbar,load,77,40);
   FanPlace(toolbar,FanLayoutSwitch("AI Cooling II 开关"),129,9);
   var aiLabel=Label("AI Cooling II",13,text);aiLabel.Margin=new Thickness(0);FanPlace(toolbar,aiLabel,189,9);
   var help=FanPendingButton("","AI Cooling II 说明",22);help.Height=22;help.BorderThickness=new Thickness(0);help.Padding=new Thickness(0);var helpIcon=new Grid{Width=22,Height=22};helpIcon.Children.Add(new Ellipse{Width=17,Height=17,Stroke=muted,StrokeThickness=1,Fill=Brushes.Transparent});var helpLabel=Label("i",11,muted);helpLabel.Margin=new Thickness(0);helpLabel.HorizontalAlignment=HorizontalAlignment.Center;helpLabel.VerticalAlignment=VerticalAlignment.Center;helpIcon.Children.Add(helpLabel);help.Content=helpIcon;FanPlace(toolbar,help,338,8);
   var reset=FanPendingButton("重置","重置",104);reset.Height=20;reset.Padding=new Thickness(0);FanPlace(toolbar,reset,371,8);
   string[] presets={"静音","标准","Turbo","全速"};
   for(int index=0;index<presets.Length;index++) {
    var preset=FanPendingButton("",presets[index],70);preset.Height=70;preset.Padding=new Thickness(0);preset.BorderThickness=new Thickness(0);
    var frame=new Grid{Width=70,Height=70};frame.Children.Add(FanBevel(70,70,false));frame.Children.Add(FanWindIcon(index,44));preset.Content=frame;FanPlace(toolbar,preset,505+90*index,0);
   }
   var tuning=FanPendingButton("","风扇调校",136);tuning.Height=32;tuning.Padding=new Thickness(0);tuning.BorderThickness=new Thickness(0);
   var tuningFrame=new Grid{Width=136,Height=32};tuningFrame.Children.Add(FanBevel(136,32,true));var tuningLabel=Label("风扇调校",14,text,FontWeights.SemiBold);tuningLabel.Margin=new Thickness(0);tuningLabel.HorizontalAlignment=HorizontalAlignment.Center;tuningLabel.VerticalAlignment=VerticalAlignment.Center;tuningFrame.Children.Add(tuningLabel);tuning.Content=tuningFrame;FanPlace(toolbar,tuning,941,18);fanContent.Children.Add(toolbar);
   fanContent.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,26,0,0)});

   var body=new Grid{Width=1075,Height=490,Margin=new Thickness(85,44,0,0),HorizontalAlignment=HorizontalAlignment.Left};
   foreach(double width in new[]{200.0,40.0,353.0,32.0,450.0})body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width)});
   var fanList=new StackPanel();var listLabel=Label("风扇列表",15,muted);listLabel.Margin=new Thickness(0,0,0,15);fanList.Children.Add(listLabel);
   var items=new StackPanel{Margin=new Thickness(10,0,5,0)};
   foreach(string name in new[]{"CPU Fan","Chassis Fan 1","Chassis Fan 2","Radiator Fan 1","Radiator Fan 2","Water Pump+ 1","Water Pump+ 2","VRM HS Fan"}) {
    string target=name;bool selectedChannel=target==fanXpertChannel;
    var select=FanFlatButton("",()=>{fanXpertChannel=target;content.Children.Clear();fixedPageHeader.Children.Clear();OriginalFanXpertPage();});
    select.Width=184;select.Height=36;select.Margin=new Thickness(0,0,0,4);select.Padding=new Thickness(12,0,8,0);select.HorizontalContentAlignment=HorizontalAlignment.Stretch;select.Background=panel;select.BorderThickness=new Thickness(selectedChannel?2:1);select.BorderBrush=selectedChannel?Brush("#00B4DC"):border;
    var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(FanRotor(18,selectedChannel?text:Brush("#707070")));
    var nameLabel=Label(name,13,selectedChannel?text:Brush("#707070"));nameLabel.Margin=new Thickness(12,0,0,0);nameLabel.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(nameLabel);select.Content=row;select.ToolTip="选择端口页面 · 当前读数未知";items.Children.Add(select);
   }
   fanList.Children.Add(items);fanList.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,44,0,0)});body.Children.Add(new Border{Height=400,VerticalAlignment=VerticalAlignment.Top,Child=fanList});body.Children.Add(new Border{Width=1,Height=366,Background=border,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,32,0,0)});
   var curve=new Canvas{Width=353,Height=490};var modes=new StackPanel{Orientation=Orientation.Horizontal};
   var smart=FanLayoutRadio("智能模式");smart.Width=146;smart.Margin=new Thickness(0,0,26,0);modes.Children.Add(smart);modes.Children.Add(FanLayoutRadio("固定转速模式"));FanPlace(curve,modes,0,0);
   var rpm=new Grid{Width=268,Height=24};rpm.ColumnDefinitions.Add(new ColumnDefinition());rpm.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var rpmLegend=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};rpmLegend.Children.Add(new Ellipse{Width=13,Height=13,Fill=accent,Margin=new Thickness(0,0,5,0)});var rpmCaption=Label("Current RPM",12,text);rpmCaption.Margin=new Thickness(0);rpmLegend.Children.Add(rpmCaption);rpm.Children.Add(rpmLegend);
   var rpmValue=Label("— rpm",12,text);rpmValue.Margin=new Thickness(0);rpmValue.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(rpmValue,1);rpm.Children.Add(rpmValue);FanPlace(curve,rpm,40,36);FanPlace(curve,FanXpertCoordinateGrid(),12,93);
   var sensor=new StackPanel{Orientation=Orientation.Horizontal};var sources=FanPendingButton("","温度来源",40);sources.Height=24;sources.Padding=new Thickness(0);sources.Content=FanSensorIcon();sensor.Children.Add(sources);sensor.Children.Add(new Ellipse{Width=12,Height=12,Fill=accent,Margin=new Thickness(12,0,8,0),VerticalAlignment=VerticalAlignment.Top});
   var sensorReading=new StackPanel();var cpu=Label("CPU",12,text);cpu.Margin=new Thickness(0,0,0,2);sensorReading.Children.Add(cpu);var temperature=Label("— °C",11,text);temperature.Margin=new Thickness(0);sensorReading.Children.Add(temperature);sensor.Children.Add(sensorReading);FanPlace(curve,sensor,0,430);Grid.SetColumn(curve,2);body.Children.Add(curve);
   var details=new Canvas{Width=450,Height=490};var mini=new Canvas{Width=110,Height=110,Background=Brush("#161616")};mini.Children.Add(new Line{X1=0,Y1=0,X2=0,Y2=110,Stroke=border,StrokeThickness=1});mini.Children.Add(new Line{X1=0,Y1=110,X2=110,Y2=110,Stroke=border,StrokeThickness=1});FanPlace(details,mini,0,43);
   var limits=new StackPanel{Width=160};limits.Children.Add(FanRangeBlock("最大速度:","— (RPM)"));limits.Children.Add(FanRangeBlock("最小速度:","— (RPM)"));limits.Children.Add(FanRangeBlock("可控范围:","—"));FanPlace(details,limits,140,43);FanPlace(details,FanLayoutSpeedTable(),0,204);
   var eq=new CheckBox{Content="EQ 模式",IsThreeState=true,IsChecked=null,IsEnabled=false,Foreground=muted,FontSize=14,ToolTip="EQ 状态未知 · 设置不可用"};eq.Template=FanUnknownCheckTemplate();ToolTipService.SetShowOnDisabled(eq,true);FanPlace(details,eq,2,338);
   FanPlace(details,FanLayoutDelay("风扇加速时间"),0,384);FanPlace(details,FanLayoutDelay("风扇减速时间"),242,384);Grid.SetColumn(details,4);body.Children.Add(details);fanContent.Children.Add(body);content.Children.Add(horizontal);
  }

  void FanPlace(Canvas parent,UIElement child,double x,double y){Canvas.SetLeft(child,x);Canvas.SetTop(child,y);parent.Children.Add(child);}

  FrameworkElement FanLayoutSwitch(string action) {
   var button=FanPendingButton("",action,53);button.Height=21;button.Padding=new Thickness(0);
   var box=new Grid{Width=49,Height=17};box.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(15)});box.ColumnDefinitions.Add(new ColumnDefinition());box.Children.Add(new Border{Width=13,Height=13,BorderBrush=muted,BorderThickness=new Thickness(1),Background=panel,VerticalAlignment=VerticalAlignment.Center});
   var value=Label("—",10,muted);value.Margin=new Thickness(0);value.HorizontalAlignment=HorizontalAlignment.Center;value.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(value,1);box.Children.Add(value);button.Content=box;return button;
  }

  RadioButton FanLayoutRadio(string title) {
   var radio=new RadioButton{Content=title,IsEnabled=false,Foreground=muted,FontSize=16,ToolTip="模式未知 · 设置不可用"};
   var template=new ControlTemplate(typeof(RadioButton));var frame=new FrameworkElementFactory(typeof(StackPanel));frame.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
   var ring=new FrameworkElementFactory(typeof(Ellipse));ring.SetValue(Ellipse.WidthProperty,20.0);ring.SetValue(Ellipse.HeightProperty,20.0);ring.SetValue(Ellipse.StrokeProperty,muted);ring.SetValue(Ellipse.StrokeThicknessProperty,1.0);ring.SetValue(Ellipse.FillProperty,Brushes.Transparent);ring.SetValue(Ellipse.MarginProperty,new Thickness(0,0,12,0));ring.SetValue(Ellipse.VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(ring);
   var caption=new FrameworkElementFactory(typeof(ContentPresenter));caption.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(caption);template.VisualTree=frame;radio.Template=template;ToolTipService.SetShowOnDisabled(radio,true);return radio;
  }

  ControlTemplate FanUnknownCheckTemplate() {
   var template=new ControlTemplate(typeof(CheckBox));var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
   var box=new FrameworkElementFactory(typeof(Border));box.SetValue(Border.WidthProperty,20.0);box.SetValue(Border.HeightProperty,20.0);box.SetValue(Border.BorderBrushProperty,muted);box.SetValue(Border.BorderThicknessProperty,new Thickness(1));box.SetValue(Border.BackgroundProperty,Brushes.Transparent);box.SetValue(Border.MarginProperty,new Thickness(0,0,18,0));
   var unknown=new FrameworkElementFactory(typeof(TextBlock));unknown.SetValue(TextBlock.TextProperty,"—");unknown.SetValue(TextBlock.FontSizeProperty,12.0);unknown.SetValue(TextBlock.ForegroundProperty,muted);unknown.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Center);unknown.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);box.AppendChild(unknown);row.AppendChild(box);var caption=new FrameworkElementFactory(typeof(ContentPresenter));caption.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(caption);template.VisualTree=row;return template;
  }

  UIElement FanRangeBlock(string caption,string value) {
   var block=new StackPanel{Margin=new Thickness(0,0,0,8)};var title=Label(caption,12,text);title.Margin=new Thickness(0,0,0,2);block.Children.Add(title);var reading=Label(value,11,muted);reading.Margin=new Thickness(0);block.Children.Add(reading);return block;
  }

  UIElement FanLayoutDelay(string title) {
   var section=new StackPanel{Width=216};section.Children.Add(FanUnknownMetric(title,"— 秒"));
   var limits=new Grid{Margin=new Thickness(0,9,0,3)};var slow=Label("缓慢",11,muted);slow.Margin=new Thickness(0);limits.Children.Add(slow);var immediate=Label("立即",11,muted);immediate.Margin=new Thickness(0);immediate.HorizontalAlignment=HorizontalAlignment.Right;limits.Children.Add(immediate);section.Children.Add(limits);
   // No thumb/value position is invented for an unknown delay.
   var slider=new Slider{Width=216,Height=18,IsEnabled=false,ToolTip="时间未知 · 设置不可用",Minimum=0,Maximum=1};
   var template=new ControlTemplate(typeof(Slider));var track=new FrameworkElementFactory(typeof(Border));track.SetValue(Border.HeightProperty,4.0);track.SetValue(Border.VerticalAlignmentProperty,VerticalAlignment.Center);track.SetValue(Border.BackgroundProperty,new LinearGradientBrush(Color.FromRgb(218,218,218),Color.FromRgb(78,157,151),0));template.VisualTree=track;slider.Template=template;ToolTipService.SetShowOnDisabled(slider,true);section.Children.Add(slider);return section;
  }

  FrameworkElement FanLayoutSpeedTable() {
   var table=new Grid{Width=412,Height=80};table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});for(int index=0;index<11;index++)table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});table.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});table.RowDefinitions.Add(new RowDefinition{Height=new GridLength(40)});
   var pwm=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(9,0,0,0)};pwm.Children.Add(new Path{Data=Geometry.Parse("M 9,0 L 1,9 L 6,9 L 3,17 L 13,6 L 8,6 Z"),Fill=muted,Width=12,Height=17,Stretch=Stretch.Uniform});var p=Label(" (%)",10,muted);p.Margin=new Thickness(3,0,0,0);p.VerticalAlignment=VerticalAlignment.Center;pwm.Children.Add(p);
   var rpm=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(7,0,0,0)};rpm.Children.Add(FanRotor(17,muted));var r=Label(" (rpm)",10,muted);r.Margin=new Thickness(2,0,0,0);r.VerticalAlignment=VerticalAlignment.Center;rpm.Children.Add(r);Grid.SetRow(rpm,1);table.Children.Add(pwm);table.Children.Add(rpm);
   for(int index=0;index<11;index++)for(int row=0;row<2;row++){var label=Label(row==0?(index*10).ToString():"—",10,row==0?muted:text);label.Margin=new Thickness(0);label.HorizontalAlignment=HorizontalAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;var cell=new Border{Child=label};Grid.SetColumn(cell,index+1);Grid.SetRow(cell,row);table.Children.Add(cell);}var divider=new Border{Height=1,Background=border,VerticalAlignment=VerticalAlignment.Bottom};Grid.SetColumnSpan(divider,12);table.Children.Add(divider);
   return new Border{Width=412,Height=80,BorderBrush=border,BorderThickness=new Thickness(1,1,0,1),Child=table};
  }

  FrameworkElement FanBevel(double width,double height,bool gradient) {
   var geometry=Geometry.Parse(gradient?"M 0,0 L "+(width-8)+",0 L "+width+",8 L "+width+","+height+" L 0,"+height+" Z":"M 0,0 L "+width+",0 L "+width+","+(height-10)+" L "+(width-10)+","+height+" L 0,"+height+" Z");
   return new Path{Data=geometry,Stroke=muted,StrokeThickness=1,Fill=gradient?(Brush)new LinearGradientBrush(Color.FromRgb(71,71,71),Color.FromRgb(21,21,21),90):panel};
  }

  FrameworkElement FanWindIcon(int preset,double size) {
   var drawing=new DrawingGroup();var pen=new Pen(text,2.2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
   if(preset==3){foreach(var data in new[]{"M 4,6 L 30,6","M 7,11 L 28,11","M 10,16 L 25,16","M 14,21 L 23,21","M 17,26 L 21,26"})drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(data)));}
   else {drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M 3,13 L 21,13 C 29,13 29,4 22,6")));drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M 5,18 L 16,18 C 22,18 22,25 17,24")));if(preset>0)drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M 2,9 L 12,9 C 18,9 18,3 14,3")));if(preset>1)drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse("M 7,23 L 27,23 C 32,23 32,29 27,29")));}
   return new Image{Source=new DrawingImage(drawing),Width=size,Height=size,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  }

  FrameworkElement FanFileIcon(bool folder) {
   var drawing=new DrawingGroup();var pen=new Pen(muted,1.5);drawing.Children.Add(new GeometryDrawing(null,pen,Geometry.Parse(folder?"M 2,6 L 10,6 L 13,9 L 25,9 L 23,24 L 2,24 Z M 5,12 L 28,12 L 23,24":"M 3,2 L 22,2 L 26,6 L 26,27 L 3,27 Z M 8,3 L 8,11 L 21,11 L 21,3 M 8,26 L 8,17 L 21,17 L 21,26 M 17,4 L 17,9")));return new Image{Source=new DrawingImage(drawing),Width=21,Height=21,Stretch=Stretch.Uniform};
  }

  FrameworkElement FanSensorIcon() {
   var drawing=new DrawingGroup();var pen=new Pen(muted,1.1);for(int index=0;index<3;index++){drawing.Children.Add(new GeometryDrawing(null,pen,new LineGeometry(new Point(2,4+index*5),new Point(5,4+index*5))));drawing.Children.Add(new GeometryDrawing(null,pen,new LineGeometry(new Point(9,4+index*5),new Point(23,4+index*5))));}return new Image{Source=new DrawingImage(drawing),Width=24,Height=16,Stretch=Stretch.Uniform};
  }

  FrameworkElement FanXpertCoordinateGrid() {
   var canvas=new Canvas{Width=325,Height=304,Background=Brushes.Transparent};double left=32,top=29,w=252,h=252;
   for(int index=0;index<=10;index++){double x=left+w*index/10,y=top+h*index/10;canvas.Children.Add(new Line{X1=x,Y1=top,X2=x,Y2=top+h,Stroke=border,StrokeThickness=0.8});canvas.Children.Add(new Line{X1=left,Y1=y,X2=left+w,Y2=y,Stroke=border,StrokeThickness=0.8});if(index>0){var tick=Label((index*10).ToString(),10,muted);tick.Margin=new Thickness(0);FanPlace(canvas,tick,x-8,top+h+4);}if(index<10){var tick=Label(((10-index)*10).ToString(),10,muted);tick.Margin=new Thickness(0);FanPlace(canvas,tick,6,y-7);}}
   canvas.Children.Add(new Line{X1=left,Y1=top+h,X2=left+w,Y2=top+h,Stroke=text,StrokeThickness=1.2});canvas.Children.Add(new Line{X1=left,Y1=top,X2=left,Y2=top+h,Stroke=text,StrokeThickness=1.2});var pwm=Label("PWM(%)",10,muted);pwm.Margin=new Thickness(0);FanPlace(canvas,pwm,0,0);var temp=Label(" (°C)",10,muted);temp.Margin=new Thickness(0);FanPlace(canvas,temp,left+w+10,top+h+4);
   // Axes alone are a layout, not a curve, live sample or calibrated range.
   return canvas;
  }

  Button FanFlatButton(string title,Action click) {
   var button=Button(title,click);
   button.Background=Brushes.Transparent;button.BorderBrush=border;button.Padding=new Thickness(10,9,10,9);
   var template=new ControlTemplate(typeof(Button));
   var frame=new FrameworkElementFactory(typeof(Border));
   frame.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   frame.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   frame.SetBinding(Border.BorderThicknessProperty,new System.Windows.Data.Binding("BorderThickness"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   frame.SetBinding(Border.PaddingProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
   presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);frame.AppendChild(presenter);template.VisualTree=frame;
   var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.58));template.Triggers.Add(disabled);button.Template=template;
   return button;
  }

  Button FanPendingButton(string title,string action,double width) {
   var button=FanFlatButton(title,()=>{});button.Width=width;button.Height=38;button.IsEnabled=false;
   button.ToolTip=action+" · 设置不可用";ToolTipService.SetShowOnDisabled(button,true);return button;
  }

  RadioButton FanPendingRadio(string title) {
   var radio=new RadioButton { Content=title,IsEnabled=false,Foreground=muted,Margin=new Thickness(0,0,18,0),FontSize=13,ToolTip="设置不可用" };
   ToolTipService.SetShowOnDisabled(radio,true);return radio;
  }

  UIElement FanUnknownMetric(string caption,string value) {
   var row=new Grid { Margin=new Thickness(0,0,0,9) };row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var label=Label(caption,12,muted);label.Margin=new Thickness(0);row.Children.Add(label);
   var reading=Label(value,12,text);reading.Margin=new Thickness(0);Grid.SetColumn(reading,1);row.Children.Add(reading);return row;
  }

  UIElement FanUnknownDelay(string title) {
   var section=new StackPanel { Margin=new Thickness(0,0,0,15) };
   section.Children.Add(FanUnknownMetric(title,"— 秒"));
   section.Children.Add(new Border{Height=3,Background=border,Margin=new Thickness(0,4,0,4)});
   return section;
  }

  void FanSpeedCell(Grid table,string value,int row,int column,bool heading) {
   var label=Label(value,heading?11:10,heading?muted:text);label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(2,7,2,7);
   var cell=new Border { Child=label,BorderBrush=border,BorderThickness=new Thickness(0,0,0,1) };Grid.SetRow(cell,row);Grid.SetColumn(cell,column);table.Children.Add(cell);
  }

  FrameworkElement FanRotor(double size,Brush ink) {
   var image=new DrawingImage();var drawing=new DrawingGroup();
   drawing.Children.Add(new GeometryDrawing(null,new Pen(ink,1.4),new EllipseGeometry(new Point(16,16),14,14)));
   drawing.Children.Add(new GeometryDrawing(ink,null,new EllipseGeometry(new Point(16,16),2.4,2.4)));
   for(int i=0;i<4;i++) {
    var blade=new GeometryGroup();blade.Children.Add(Geometry.Parse("M 15,13 C 8,8 10,3 16,4 C 22,5 22,9 18,14 Z"));
    blade.Transform=new RotateTransform(i*90,16,16);drawing.Children.Add(new GeometryDrawing(ink,null,blade));
   }
   image.Drawing=drawing;return new Image { Source=image,Width=size,Height=size,Stretch=Stretch.Uniform };
  }

  UIElement FanCoordinateGrid(double height,bool temperatureAxis) {
   var canvas=new Canvas { Height=height,Background=Brushes.Transparent,ClipToBounds=true };
   canvas.SizeChanged+=(sender,args)=>FanDrawGrid(canvas,args.NewSize.Width,height,temperatureAxis);
   return canvas;
  }

  void FanDrawGrid(Canvas canvas,double width,double height,bool temperatureAxis) {
   canvas.Children.Clear();if(width<50)return;
   double left=temperatureAxis?32:0,top=temperatureAxis?15:2,right=8,bottom=temperatureAxis?32:2;
   double w=width-left-right,h=height-top-bottom;
   for(int i=0;i<=5;i++) {
    double x=left+w*i/5,y=top+h*i/5;
    canvas.Children.Add(new Line{X1=x,Y1=top,X2=x,Y2=top+h,Stroke=border,StrokeThickness=1});
    canvas.Children.Add(new Line{X1=left,Y1=y,X2=left+w,Y2=y,Stroke=border,StrokeThickness=1});
    if(temperatureAxis) {
     var xtick=Label((i*20).ToString(),10,muted);xtick.Margin=new Thickness(0);Canvas.SetLeft(xtick,x-7);Canvas.SetTop(xtick,top+h+5);canvas.Children.Add(xtick);
     var ytick=Label(((5-i)*20).ToString(),10,muted);ytick.Margin=new Thickness(0);Canvas.SetLeft(ytick,0);Canvas.SetTop(ytick,y-6);canvas.Children.Add(ytick);
    }
   }
   if(temperatureAxis) {
    var pwm=Label("PWM (%)",10,muted);pwm.Margin=new Thickness(0);Canvas.SetLeft(pwm,left);Canvas.SetTop(pwm,0);canvas.Children.Add(pwm);
    var temp=Label("温度 °C",10,muted);temp.Margin=new Thickness(0);Canvas.SetLeft(temp,Math.Max(left,width-52));Canvas.SetTop(temp,height-13);canvas.Children.Add(temp);
   }
   // A grid is a plotting surface, not a hardware curve. No samples or preset are invented.
  }
 }
}
