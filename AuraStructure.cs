using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  string auraTab="Aura 灯效";bool experimentalLighting;
  static readonly string[] AuraNames={"恒亮","呼吸","闪烁","彩色循环","彩虹","星夜","音乐","智能","同步辨色","沉浸","黑暗 (关闭)","AI 神光同步"};
  static readonly string[] AuraKeys={"static","breathe","flash","cycle","rainbow","stars","music","smart","adaptive","immersive","off","ai"};
  static readonly string[] AuraSymbols={"◉","◌","☀","◷","≋","✦","♫","♙","▣","◈","⊘","✧"};
  void OriginalAuraPage() {
   Header("灯效 & Aura Sync","");
   content.Children.Add(OriginalTabs(new[]{"同步设备","Aura 灯效","AI 神光同步"},auraTab,chosen=>{auraTab=chosen;ShowPage(page);},accent,195));
   if(auraTab=="同步设备"){OriginalAuraDevices();return;}
   if(auraTab=="AI 神光同步"){content.Children.Add(Paragraph("AI 神光同步尚未接通。",13,muted));return;}
   var body=new Grid{Margin=new Thickness(0,30,0,0)};body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(375)});
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);body.Children.Add(left);body.Children.Add(right);
   left.Children.Add(OriginalAuraHeading("基本灯效"));
   var grid=new Grid{Margin=new Thickness(8,26,6,12)};for(int c=0;c<6;c++)grid.ColumnDefinitions.Add(new ColumnDefinition());for(int r=0;r<2;r++)grid.RowDefinitions.Add(new RowDefinition{Height=new GridLength(100)});
   for(int i=0;i<AuraKeys.Length;i++){
    string key=AuraKeys[i];bool supported=new[]{"static","breathe","cycle","off"}.Contains(key);
    var stack=new StackPanel();var symbol=Label(AuraSymbols[i],38,effect==key?accent:muted);symbol.FontFamily=new FontFamily("Segoe UI Symbol");symbol.TextAlignment=TextAlignment.Center;symbol.Margin=new Thickness(0,0,0,8);stack.Children.Add(symbol);var name=Label(AuraNames[i],12,effect==key?accent:text);name.TextAlignment=TextAlignment.Center;name.Margin=new Thickness(0);stack.Children.Add(name);
    var button=Button("",async()=>{effect=key;if(experimentalLighting)await ApplyAura();else ShowPage(page);});button.Content=stack;button.Background=Brushes.Transparent;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(2,5,2,5);button.Margin=new Thickness(1);button.IsEnabled=supported&&!busy;button.ToolTip=supported?"选择灯效；启用试用后，点击会应用到所选主板灯控。":"此灯效尚未接通";ToolTipService.SetShowOnDisabled(button,true);Grid.SetColumn(button,i%6);Grid.SetRow(button,i/6);grid.Children.Add(button);
   }
   left.Children.Add(grid);left.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,13,0,22)});left.Children.Add(OriginalAuraHeading("高级灯效"));
   var advanced=new Grid{Margin=new Thickness(12,22,8,10)};advanced.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});advanced.ColumnDefinitions.Add(new ColumnDefinition());advanced.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var logo=Label("△",43,text);advanced.Children.Add(logo);var profiles=new ComboBox{IsEnabled=false,Height=32,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,18,0)};profiles.Items.Add("无可用配置");profiles.SelectedIndex=0;Grid.SetColumn(profiles,1);advanced.Children.Add(profiles);var creator=Button("Aura Creator",()=>{});creator.IsEnabled=false;creator.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(creator,2);advanced.Children.Add(creator);left.Children.Add(advanced);left.Children.Add(Paragraph("使用 Aura Creator 创造多重设备组合灯效。此功能尚未接通。",12,muted));
   left.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,23,0,20)});left.Children.Add(OriginalAuraHeading("游戏灯效"));var games=Row();var gameList=Button("游戏列表",()=>{});gameList.IsEnabled=false;games.Children.Add(gameList);games.Children.Add(new CheckBox{Content="未接通",IsEnabled=false,Foreground=muted,VerticalAlignment=VerticalAlignment.Center});left.Children.Add(games);
   var reset=Button("恢复默认值",()=>{});reset.IsEnabled=false;reset.HorizontalAlignment=HorizontalAlignment.Right;reset.Margin=new Thickness(0,0,0,15);reset.ToolTip="未读取原厂默认值，无法确认恢复结果。";ToolTipService.SetShowOnDisabled(reset,true);right.Children.Add(reset);
   var settings=new StackPanel();settings.Children.Add(Label(AuraNames[Array.IndexOf(AuraKeys,effect)<0?0:Array.IndexOf(AuraKeys,effect)],20,text));settings.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,2,0,20)});settings.Children.Add(Label("色彩",15,text));
   settings.Children.Add(new RadioButton{Content="单色",IsChecked=true,Foreground=text,Margin=new Thickness(0,12,0,12),IsEnabled=false});
   var colors=new WrapPanel();foreach(string v in new[]{"#FF0000","#FFB800","#00BBFF","#FFFFFF"}){string chosen=v;var swatch=Button("",async()=>{color=chosen;if(experimentalLighting)await ApplyAura();else ShowPage(page);});swatch.Width=36;swatch.Height=34;swatch.Background=Brush(v);swatch.Margin=new Thickness(0,0,7,7);colors.Children.Add(swatch);}settings.Children.Add(colors);
   var hex=new TextBox{Text=color,Width=118,HorizontalAlignment=HorizontalAlignment.Left,Foreground=text,Background=bg,BorderBrush=border,Padding=new Thickness(6),Margin=new Thickness(0,2,0,16)};
   hex.TextChanged+=(s,e)=>color=hex.Text.Trim();hex.KeyDown+=async(s,e)=>{if(e.Key==Key.Enter&&experimentalLighting){e.Handled=true;await ApplyAura();}};settings.Children.Add(hex);
   settings.Children.Add(new RadioButton{Content="渐变",Foreground=muted,IsEnabled=false,Margin=new Thickness(0,2,0,13)});settings.Children.Add(Label("颜色配置",13,muted));var gradient=Row();foreach(string v in new[]{"#00BFFF","#FFAA00"})gradient.Children.Add(new Border{Width=34,Height=32,Background=Brush(v),Margin=new Thickness(0,0,16,0),Opacity=0.45});settings.Children.Add(gradient);settings.Children.Add(Label("方向",13,muted));var direction=new ComboBox{IsEnabled=false,Height=32,Margin=new Thickness(0,2,0,20)};direction.Items.Add("未接通");direction.SelectedIndex=0;settings.Children.Add(direction);
   var trial=new CheckBox{Content="试用灯效（写入尚未实机验证）",IsChecked=experimentalLighting,Foreground=muted,FontSize=11,Margin=new Thickness(0,12,0,10)};trial.Checked+=(s,e)=>experimentalLighting=true;trial.Unchecked+=(s,e)=>experimentalLighting=false;settings.Children.Add(trial);
   settings.Children.Add(Paragraph("启用后点击灯效或色块才会应用。\n退出后硬件会保留最后的灯效。",11,muted));right.Children.Add(new Border{BorderBrush=border,BorderThickness=new Thickness(1),Padding=new Thickness(22),MinHeight=475,Child=settings});content.Children.Add(body);
   content.Children.Add(Paragraph(auraMessage,11,muted));
  }
  UIElement OriginalTabs(string[] names,string active,Action<string> select,Brush ink,double width) {
   var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,16)};
   foreach(string value in names){string name=value;var stack=new StackPanel{Width=width};var button=Button(name,()=>select(name));button.Background=Brushes.Transparent;button.Foreground=name==active?ink:text;button.BorderThickness=new Thickness(0);button.Padding=new Thickness(6,13,6,15);button.Margin=new Thickness(0);button.FontSize=15;stack.Children.Add(button);stack.Children.Add(new Border{Height=2,Background=name==active?ink:border});tabs.Children.Add(stack);}return tabs;
  }
  UIElement OriginalAuraHeading(string heading){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(Label("╱╱╱",18,Brush("#B73038"),FontWeights.Bold));var label=Label(heading,18,text);label.Margin=new Thickness(11,0,0,0);row.Children.Add(label);return row;}
  void OriginalAuraDevices() {
   var commands=Row();commands.Children.Add(Button(busy?"正在读取…":"重新扫描",async()=>await ScanAura()));commands.Children.Add(Button("关闭本程序连接",async()=>await ReleaseAura()));content.Children.Add(commands);
   content.Children.Add(Paragraph(auraMessage,12,muted));
   foreach(var device in devices){var d=device;var box=new StackPanel();box.Children.Add(OriginalInstalledImage(OriginalProductImage("motherboard"),160));var check=new CheckBox{Content=info.Board,Foreground=text,IsChecked=selected.Contains(d.id),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,16,0,10)};check.Checked+=(s,e)=>selected.Add(d.id);check.Unchecked+=(s,e)=>selected.Remove(d.id);box.Children.Add(check);var detail=Paragraph(d.zoneCount+" 个逻辑 ARGB 通道 · 固件 "+d.firmware+"\n通道数不是实际灯珠数；外设同步尚未接通。",11,muted);detail.TextAlignment=TextAlignment.Center;box.Children.Add(detail);content.Children.Add(new Border{Width=320,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(20),BorderBrush=border,BorderThickness=new Thickness(1),Child=box});}
  }
 }
}
