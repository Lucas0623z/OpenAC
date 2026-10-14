using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  sealed class MonitorCardView {
   public string Group;
   public StackPanel Rows;
   public ScrollViewer Scroll;
   public bool Expanded;
   public readonly List<MonitorRowView> Items=new List<MonitorRowView>();
  }
  sealed class MonitorRowView {
   public string Key,Group;
   public Grid Root;
   public TextBlock Name,Value;
   public Border Track,Fill;
   public double Fraction;
  }
  readonly Dictionary<string,MonitorCardView> dynamicMonitorCards=new Dictionary<string,MonitorCardView>(StringComparer.Ordinal);

  void OriginalSystemMonitoringPage() {
   content.Children.Add(OriginalMonitoringLayout(true));
  }

  Border OriginalMonitoringLayout(bool expanded) {
   dynamicMonitorCards.Clear();
   var stack=new StackPanel();
   var heading=Label("系统监控",expanded?20:16,text,FontWeights.Normal);
   heading.Margin=new Thickness(0,0,0,expanded?18:10);
   stack.Children.Add(expanded?(UIElement)heading:OriginalSectionHeading("系统监控"));
   if(!expanded){var section=(FrameworkElement)stack.Children[0];section.Margin=new Thickness(0,0,0,10);section.Cursor=System.Windows.Input.Cursors.Hand;section.MouseLeftButtonUp+=(s,e)=>ShowPage("系统监控");}
   var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition());
   var frequency=CreateMonitorCard("Frequency","频率",expanded);frequency.Margin=new Thickness(0,0,expanded?25:7,0);top.Children.Add(frequency);
   var temperature=CreateMonitorCard("Temperature","温度",expanded);temperature.Margin=new Thickness(expanded?25:7,0,0,0);Grid.SetColumn(temperature,1);top.Children.Add(temperature);
   stack.Children.Add(top);
   var bottom=new Grid{Margin=new Thickness(0,expanded?42:16,0,0)};for(int i=0;i<3;i++)bottom.ColumnDefinitions.Add(new ColumnDefinition());
   var usage=CreateMonitorCard("Usage","使用率",expanded);usage.Margin=new Thickness(0,0,expanded?18:6,0);bottom.Children.Add(usage);
   var fans=CreateMonitorCard("Fans","风扇",expanded);fans.Margin=new Thickness(expanded?18:6,0,expanded?18:6,0);Grid.SetColumn(fans,1);bottom.Children.Add(fans);
   var voltage=CreateMonitorCard("Voltage","电压",expanded);voltage.Margin=new Thickness(expanded?18:6,0,0,0);Grid.SetColumn(voltage,2);bottom.Children.Add(voltage);
   stack.Children.Add(bottom);UpdateDynamicMonitorCards();
   return new Border{Child=stack,Background=Brush("#00000000"),Margin=new Thickness(0,0,0,expanded?16:25)};
  }

  Border CreateMonitorCard(string group,string title,bool expanded) {
   var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());
   var heading=Label(title,expanded?30:28,text,FontWeights.Normal);heading.Margin=new Thickness(0,0,0,expanded?9:2);layout.Children.Add(heading);
   var rows=new StackPanel();
   var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Padding=new Thickness(0,0,3,0),Focusable=true};
   scroll.Resources.Add(typeof(ScrollBar),MonitorScrollBarStyle());
   Grid.SetRow(scroll,1);layout.Children.Add(scroll);
   var view=new MonitorCardView{Group=group,Rows=rows,Scroll=scroll,Expanded=expanded};dynamicMonitorCards[group]=view;
   // Each sensor group has its own viewport, so adding a GPU or CPU core
   // never increases the dashboard height or moves the system showcase.
   return new Border{Child=layout,Height=expanded?(group=="Frequency"||group=="Temperature"?330:300):(group=="Frequency"||group=="Temperature"?215:205),Padding=new Thickness(expanded?6:0,0,expanded?6:0,0),Background=Brush("#00000000")};
  }

  static Style MonitorScrollBarStyle() {
   // This small original template avoids the light operating-system scroll
   // gutter inside dark sensor lists, without using vendor theme resources.
   var style=new Style(typeof(ScrollBar));style.Setters.Add(new Setter(FrameworkElement.WidthProperty,9.0));
   var template=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
    <Grid Background='Transparent' Margin='3,0,0,0'>
     <Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' ViewportSize='{TemplateBinding ViewportSize}'>
      <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False' Opacity='0'/></Track.DecreaseRepeatButton>
      <Track.Thumb><Thumb Background='#565656' MinHeight='18'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{TemplateBinding Background}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
      <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False' Opacity='0'/></Track.IncreaseRepeatButton>
     </Track>
    </Grid>
   </ControlTemplate>");
   style.Setters.Add(new Setter(Control.TemplateProperty,template));return style;
  }

  IList<MonitoringValue> MonitorGroupItems(string group) {
   switch(group) {
    case "Frequency":return monitor.Frequency;
    case "Temperature":return monitor.Temperature;
    case "Usage":return monitor.Usage;
    case "Fans":return monitor.Fans;
    case "Voltage":return monitor.Voltage;
    default:return null;
   }
  }

  void UpdateDynamicMonitorCards() {
   if(page!="控制面板"&&page!="总览"&&page!="系统监控")return;
   foreach(var card in dynamicMonitorCards.Values) {
    var readings=MonitorGroupItems(card.Group);var present=new List<MonitoringValue>();
    if(readings!=null)foreach(var reading in readings)if(reading!=null&&!String.IsNullOrWhiteSpace(reading.Name))present.Add(reading);
    bool rebuild=present.Count!=card.Items.Count;
    if(!rebuild)for(int i=0;i<present.Count;i++)if(card.Items[i].Key!=(present[i].Key??present[i].Name)){rebuild=true;break;}
    if(rebuild) {
     double offset=card.Scroll.VerticalOffset;card.Rows.Children.Clear();card.Items.Clear();
     foreach(var reading in present) {
      var row=CreateMonitorRow(card.Expanded);row.Group=card.Group;row.Key=reading.Key??reading.Name;card.Items.Add(row);card.Rows.Children.Add(row.Root);
     }
     card.Scroll.ScrollToVerticalOffset(offset);
    }
    for(int i=0;i<present.Count;i++)UpdateMonitorRow(card.Items[i],present[i]);
   }
  }

  MonitorRowView CreateMonitorRow(bool expanded) {
   var row=new MonitorRowView();var grid=new Grid{Height=expanded?38:30};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   row.Root=grid;
   row.Name=Label("",expanded?16:12,muted,FontWeights.Normal);row.Name.Margin=new Thickness(0,0,5,0);row.Name.VerticalAlignment=VerticalAlignment.Center;row.Name.TextTrimming=TextTrimming.CharacterEllipsis;grid.Children.Add(row.Name);
   row.Value=Label("",expanded?16:12,muted,FontWeights.Normal);row.Value.Margin=new Thickness(5,0,0,0);row.Value.VerticalAlignment=VerticalAlignment.Center;row.Value.TextAlignment=TextAlignment.Right;Grid.SetColumn(row.Value,1);grid.Children.Add(row.Value);
   row.Fill=new Border{Background=accent,HorizontalAlignment=HorizontalAlignment.Left};
   row.Track=new Border{Height=2,Background=Brush("#666666"),VerticalAlignment=VerticalAlignment.Bottom,Child=row.Fill,Visibility=Visibility.Collapsed,ClipToBounds=true};Grid.SetColumnSpan(row.Track,2);grid.Children.Add(row.Track);
   row.Track.SizeChanged+=(s,e)=>row.Fill.Width=Math.Max(0,row.Track.ActualWidth*row.Fraction);
   return row;
  }

  void UpdateMonitorRow(MonitorRowView row,MonitoringValue reading) {
   TimeSpan age=DateTime.UtcNow-reading.Captured.ToUniversalTime();
   bool confirmed=reading.Confirmed&&MonitorFinite(reading.Value)&&age.TotalSeconds>=-5&&age.TotalSeconds<=(reading.Quality=="configured"?90:15);
   row.Name.Text=reading.Name;row.Name.ToolTip=String.IsNullOrWhiteSpace(reading.DeviceName)?reading.Name:reading.Name+" · "+reading.DeviceName;
   string suffix=String.IsNullOrWhiteSpace(reading.Unit)?"":" "+reading.Unit.Trim();
   string format=reading.Name=="CPU BCLK"?"0.###########":reading.Unit=="V"?"0.###":reading.Unit=="MHz"||reading.Unit=="MT/s"||reading.Unit=="RPM"?"0":"0.##";
   row.Value.Text=(confirmed?reading.Value.Value.ToString(format,CultureInfo.InvariantCulture):"—")+suffix;
   row.Value.ToolTip=reading.Quality=="osEstimate"?"Windows 估算":reading.Quality=="configured"?"配置速率":reading.Source;
   row.Value.Foreground=confirmed&&reading.Unit=="%"&&reading.Value.Value>=100?Brush("#FF1515"):muted;
   bool bounded=confirmed&&!String.IsNullOrWhiteSpace(reading.Source)&&MonitorFinite(reading.Min)&&MonitorFinite(reading.Max)&&reading.Max.Value>reading.Min.Value;
   bool progressSlot=(row.Group=="Frequency"&&(reading.Name=="CPU"||reading.Name=="CPU BCLK"))||(row.Group=="Temperature"&&(reading.Name=="CPU"||reading.Name=="CPU Package"));
   row.Track.Visibility=progressSlot?Visibility.Visible:Visibility.Collapsed;row.Fill.Visibility=bounded&&progressSlot?Visibility.Visible:Visibility.Collapsed;
   row.Fraction=bounded?Math.Max(0,Math.Min(1,(reading.Value.Value-reading.Min.Value)/(reading.Max.Value-reading.Min.Value))):0;
   row.Fill.Width=Math.Max(0,row.Track.ActualWidth*row.Fraction);
  }

  static bool MonitorFinite(double? value) {return value.HasValue&&!Double.IsNaN(value.Value)&&!Double.IsInfinity(value.Value);}
 }
}
