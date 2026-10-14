using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  UIElement BuildOriginalNavigation() {
   var shell=new DockPanel{Background=panel};
   var top=Label("☰",20,text);top.Margin=new Thickness(16,12,0,10);top.Height=32;DockPanel.SetDock(top,Dock.Top);shell.Children.Add(top);
   var bottom=new StackPanel();bottom.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(12,0,12,10)});
   AddOriginalNav(bottom,"更新库","⚙");bottom.Margin=new Thickness(0,0,0,10);DockPanel.SetDock(bottom,Dock.Bottom);shell.Children.Add(bottom);
   var list=new StackPanel();AddOriginalNav(list,"控制面板","◴");AddOriginalNav(list,"设备","▣");
   AddOriginalGroup(list,"游乐场");AddOriginalNav(list,"灯效 & Aura Sync","⬡");
   AddOriginalGroup(list,"助理");AddOriginalNav(list,"情境设置档","☷");AddOriginalNav(list,"宏","▧");AddOriginalNav(list,"GameFirst","ϟ");AddOriginalNav(list,"游戏库","▣");AddOriginalNav(list,"Fan Xpert","▨");AddOriginalNav(list,"工具","⚒");
   shell.Children.Add(new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   return new Border{Child=shell,BorderBrush=border,BorderThickness=new Thickness(0,0,1,0)};
  }
  void AddOriginalGroup(StackPanel parent,string heading) {var label=Label(heading,13,accent);label.Margin=new Thickness(10,14,0,7);parent.Children.Add(label);}
  void AddOriginalNav(StackPanel parent,string name,string symbol) {
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var icon=Label(symbol,21,text);icon.Margin=new Thickness(0);row.Children.Add(icon);
   var label=Label(name,14,text);label.VerticalAlignment=VerticalAlignment.Center;label.Margin=new Thickness(0);Grid.SetColumn(label,1);row.Children.Add(label);
   var button=Button("",()=>ShowPage(name));button.Content=row;button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;button.BorderThickness=new Thickness(3,0,0,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(12,10,8,10);button.Margin=new Thickness(0);button.Height=43;
   navigation[name]=button;parent.Children.Add(button);
  }
  void UpdateOriginalNavigation() {
   string current=page=="系统监控"?"控制面板":page.StartsWith("device:",StringComparison.Ordinal)?"设备":page=="设置"?"更新库":page=="驱动程序更新"||page=="Windows 电源"||page=="屏幕设置"||page=="电池信息"||page=="笔记本与掌机"||page=="设备兼容"?"工具":page;
   foreach(var entry in navigation){bool active=entry.Key==current;entry.Value.Background=active?(uiPreferences!=null&&uiPreferences.Theme=="light"?panel:Brush("#302E25")):Brushes.Transparent;entry.Value.BorderBrush=active?accent:Brushes.Transparent;}
  }
  void OpenOriginalDevice(string target){ShowPage("device:"+target);}
  void OriginalPendingPage(string name) {Header(name,"");content.Children.Add(Paragraph("功能暂不可用",13,muted));}
  void OriginalUnknownDevice(string kind) {
   string name=kind=="keyboard"?"ROG 夜魔 EXTREME 二十周年版":kind=="mouse"?"ROG HARPE II EXTREME EDITION 20":kind=="headset"?"ROG 棱镜 2-KJP":kind=="receiver"?"ROG OMNI RECEIVER":"ASTRAL-RTX5090";
   Header(name,"");content.Children.Add(OriginalInstalledImage(OriginalProductImage(kind),230));
   content.Children.Add(Paragraph("设置不可用",13,muted));
  }
 }
}
