using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public partial class MainWindow {
  string fanXpertChannel="CPU Fan";

  void OriginalFanXpertPage() {
   var heading=Label("FAN XPERT 4",26,text,FontWeights.Normal);heading.Margin=new Thickness(0,0,0,22);content.Children.Add(heading);
   var toolbar=new Grid { Margin=new Thickness(85,65,0,20) };
   toolbar.ColumnDefinitions.Add(new ColumnDefinition());toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var tools=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
   tools.Children.Add(FanPendingButton("ON","启用 Fan Xpert 4",54));
   tools.Children.Add(FanPendingButton("▣","保存风扇配置",34));
   tools.Children.Add(FanPendingButton("▤","载入风扇配置",34));
   var ai=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,10,0) };
   var aiLabel=Label("AI Cooling II",13,text);aiLabel.Margin=new Thickness(0,0,8,0);aiLabel.VerticalAlignment=VerticalAlignment.Center;ai.Children.Add(aiLabel);
   ai.Children.Add(FanPendingButton("OFF","AI Cooling II 开关",54));tools.Children.Add(ai);
   tools.Children.Add(FanPendingButton("↺","重置",34));
   foreach(string mode in new[]{"静音","标准","Turbo","全速"}) {
    var modeButton=FanPendingButton("",mode,40);modeButton.Content=FanRotor(24,muted);modeButton.Margin=new Thickness(3,0,3,0);tools.Children.Add(modeButton);
   }
   toolbar.Children.Add(tools);
   var tuning=FanPendingButton("风扇调校","风扇调校",96);tuning.Margin=new Thickness(12,0,0,0);Grid.SetColumn(tuning,1);toolbar.Children.Add(tuning);
   content.Children.Add(toolbar);

   var body=new Grid { Margin=new Thickness(85,80,0,0) };
   body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(200)});
   body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(400)});
   body.ColumnDefinitions.Add(new ColumnDefinition());
   var fanList=new StackPanel { Margin=new Thickness(0,0,16,0) };
   foreach(string name in new[]{"CPU Fan","Chassis Fan 1","Chassis Fan 2","Radiator Fan 1","Radiator Fan 2","Water Pump+ 1","Water Pump+ 2","VRM HS Fan"}) {
    string target=name;
    var select=FanFlatButton(name,()=>{fanXpertChannel=target;content.Children.Clear();OriginalFanXpertPage();});
    select.HorizontalContentAlignment=HorizontalAlignment.Left;select.Margin=new Thickness(0,0,0,5);select.Padding=new Thickness(12,14,8,14);
    select.FontSize=12;select.Foreground=target==fanXpertChannel?accent:text;
    select.BorderBrush=target==fanXpertChannel?accent:border;select.BorderThickness=new Thickness(target==fanXpertChannel?2:1,0,0,0);
    fanList.Children.Add(select);
   }
   body.Children.Add(fanList);

   var curve=new StackPanel { Width=360,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,18,0) };
   var modes=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,5,0,18) };
   modes.Children.Add(FanPendingRadio("智能模式"));modes.Children.Add(FanPendingRadio("固定转速模式"));curve.Children.Add(modes);
   var rpm=new Grid { Margin=new Thickness(0,0,0,18) };
   var rpmCaption=Label("Current RPM",13,muted);rpmCaption.Margin=new Thickness(0);rpm.Children.Add(rpmCaption);
   var rpmValue=Label("—",28,accent);rpmValue.HorizontalAlignment=HorizontalAlignment.Right;rpmValue.Margin=new Thickness(0,-5,0,0);rpm.Children.Add(rpmValue);curve.Children.Add(rpm);
   curve.Children.Add(FanCoordinateGrid(300,true));
   var state=Paragraph("当前转速、风扇曲线与控制状态尚未读取。",11,muted);state.Margin=new Thickness(0,8,0,0);curve.Children.Add(state);
   Grid.SetColumn(curve,1);body.Children.Add(curve);

   var details=new StackPanel { Width=450,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(18,0,0,0) };
   var speedOverview=new Grid { Margin=new Thickness(0,4,0,10) };
   speedOverview.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(205)});
   speedOverview.ColumnDefinitions.Add(new ColumnDefinition());
   var limits=new StackPanel { Margin=new Thickness(0,0,25,0) };
   limits.Children.Add(FanUnknownMetric("最大速度","— RPM"));
   limits.Children.Add(FanUnknownMetric("最小速度","— RPM"));
   limits.Children.Add(FanUnknownMetric("可控范围","—"));speedOverview.Children.Add(limits);
   var miniChart=(FrameworkElement)FanCoordinateGrid(110,false);miniChart.MinWidth=205;Grid.SetColumn(miniChart,1);speedOverview.Children.Add(miniChart);details.Children.Add(speedOverview);
   var tableLabel=Label("PWM (%) / RPM",11,muted);tableLabel.Margin=new Thickness(0,12,0,0);details.Children.Add(tableLabel);
   var speedTable=new Grid { Margin=new Thickness(0,10,0,14) };
   for(int i=0;i<11;i++)speedTable.ColumnDefinitions.Add(new ColumnDefinition());
   speedTable.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});speedTable.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   for(int i=0;i<11;i++){FanSpeedCell(speedTable,(i*10).ToString()+"%",0,i,true);FanSpeedCell(speedTable,"— RPM",1,i,false);}details.Children.Add(speedTable);
   var eq=new CheckBox { Content="EQ 模式",IsEnabled=false,Foreground=muted,Margin=new Thickness(0,0,0,18),ToolTip="尚未接入风扇控制，模式状态未读取。" };ToolTipService.SetShowOnDisabled(eq,true);details.Children.Add(eq);
   var delays=new Grid();delays.ColumnDefinitions.Add(new ColumnDefinition());delays.ColumnDefinitions.Add(new ColumnDefinition());
   var acceleration=(FrameworkElement)FanUnknownDelay("风扇加速时间");acceleration.Margin=new Thickness(0,0,18,0);delays.Children.Add(acceleration);
   var deceleration=(FrameworkElement)FanUnknownDelay("风扇减速时间");deceleration.Margin=new Thickness(18,0,0,0);Grid.SetColumn(deceleration,1);delays.Children.Add(deceleration);details.Children.Add(delays);
   Grid.SetColumn(details,2);body.Children.Add(details);content.Children.Add(body);
   var note=Paragraph("风扇设置尚未接通，调校和写入暂不可用。",11,muted);note.Margin=new Thickness(285,20,0,0);content.Children.Add(note);
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
   button.ToolTip=action+"尚未接通；控件与开关状态未读取。";ToolTipService.SetShowOnDisabled(button,true);return button;
  }

  RadioButton FanPendingRadio(string title) {
   var radio=new RadioButton { Content=title,IsEnabled=false,Foreground=muted,Margin=new Thickness(0,0,18,0),FontSize=13,ToolTip="当前模式未读取，设置写入尚未接通。" };
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
