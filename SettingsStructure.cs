using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  string settingsTab="更新中心";

  void OriginalSettingsPage() {
   Header("设置","");
   var tabs=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,24) };
   foreach(string label in new[]{"一般设置","更新中心","关于"}) {
    string target=label;
    var tab=Button(label,()=>{settingsTab=target;content.Children.Clear();OriginalSettingsPage();});
    tab.Background=Brushes.Transparent;tab.BorderThickness=new Thickness(0);
    tab.Foreground=target==settingsTab?accent:muted;
    tab.FontSize=16;tab.FontWeight=target==settingsTab?FontWeights.SemiBold:FontWeights.Normal;
    tab.Padding=new Thickness(4,10,4,12);tab.Margin=new Thickness(0,0,36,0);
    var underline=new StackPanel();underline.Children.Add(tab);
    underline.Children.Add(new Border { Height=2,Background=target==settingsTab?accent:Brushes.Transparent });
    tabs.Children.Add(underline);
   }
   content.Children.Add(tabs);
   if(settingsTab=="一般设置") OriginalGeneralSettings();
   else if(settingsTab=="关于") OriginalAboutSettings();
   else OriginalUpdateCenter();
  }

  Button PendingUpdateButton(string title) {
   var button=Button(title,()=>{});
   button.IsEnabled=false;
   button.ToolTip="更新服务器与安装流程尚未接通，当前仅显示本机安装版本。";
   ToolTipService.SetShowOnDisabled(button,true);
   return button;
  }

  string InstalledArmouryVersion() {
   if(info.ArmouryVersion!="未读取")return info.ArmouryVersion;
   try {
    string[] paths=Directory.GetDirectories(@"C:\Program Files\WindowsApps","B9ECED6F.ArmouryCrate_*_x64__qmba6cd70vzyy");
    Version newest=null;
    foreach(string path in paths) {
     string[] parts=Path.GetFileName(path).Split('_');Version parsed;
     if(parts.Length>1&&Version.TryParse(parts[1],out parsed)&&(newest==null||parsed>newest)) newest=parsed;
    }
    return newest==null?"未读取":newest.ToString();
   } catch {return "未读取";}
  }

  void OriginalUpdateCenter() {
   var commands=new Grid { Margin=new Thickness(0,0,0,18) };
   var check=PendingUpdateButton("检查更新");check.HorizontalAlignment=HorizontalAlignment.Right;check.Margin=new Thickness(0);
   commands.Children.Add(check);content.Children.Add(commands);

   var core=new StackPanel();
   core.Children.Add(SettingsInstallStatus());
   core.Children.Add(SettingsTableRow(new[]{"名称","版本"},new[]{3.0,1.0},true));
   core.Children.Add(SettingsTableRow(new[]{"Armoury Crate",InstalledArmouryVersion()},new[]{3.0,1.0},false));
   content.Children.Add(SettingsGroup("UWP 应用程序与核心服务","更新",core));

   var components=new StackPanel();
   components.Children.Add(SettingsInstallStatus());
   components.Children.Add(SettingsTableRow(new[]{"名称","Module","版本","日期"},new[]{3.8,1.2,1.7,1.1},true));
   foreach(var component in info.Components) {
    // Uninstall registration supplies name/version only. Module/date stay unknown.
    components.Children.Add(SettingsTableRow(new[]{component.Name,"—",String.IsNullOrWhiteSpace(component.Version)?"—":component.Version,"—"},new[]{3.8,1.2,1.7,1.1},false));
   }
   if(info.Components.Count==0)components.Children.Add(Paragraph("本机组件版本尚未读取，请在一般设置中刷新状态。",12,muted));
   content.Children.Add(SettingsGroup("设备与元件","全部更新",components));
   content.Children.Add(LocalUpdatePanel());
   content.Children.Add(Paragraph("当前显示本机安装版本；更新服务器状态尚未读取。",12,muted));
  }

  UIElement SettingsInstallStatus() {
   var label=Label("本机已安装",12,muted);label.Margin=new Thickness(14,10,14,10);
   return new Border { Background=Brushes.Transparent,BorderBrush=border,BorderThickness=new Thickness(0,0,0,1),Child=label };
  }

  UIElement SettingsGroup(string title,string action,UIElement body) {
   var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var heading=Label(title,16,text,FontWeights.SemiBold);heading.Margin=new Thickness(0);heading.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(heading);
   var button=PendingUpdateButton(action);button.Margin=new Thickness(16,0,0,0);Grid.SetColumn(button,1);header.Children.Add(button);
   var expander=new Expander { Header=header,Content=body,IsExpanded=true,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,18) };
   header.MinWidth=600;
   return new Border { BorderBrush=border,BorderThickness=new Thickness(1),Background=panel,Padding=new Thickness(18,14,18,0),Margin=new Thickness(0,0,0,20),Child=expander };
  }

  UIElement SettingsTableRow(string[] values,double[] widths,bool heading) {
   var row=new Grid { Margin=new Thickness(14,0,14,0) };
   for(int i=0;i<widths.Length;i++)row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(widths[i],GridUnitType.Star)});
   for(int i=0;i<values.Length;i++) {
    var cell=Label(values[i],heading?12:13,heading?muted:text,heading?FontWeights.SemiBold:FontWeights.Normal);
    cell.Margin=new Thickness(0,heading?12:16,12,heading?12:16);cell.TextWrapping=TextWrapping.Wrap;cell.VerticalAlignment=VerticalAlignment.Center;
    Grid.SetColumn(cell,i);row.Children.Add(cell);
   }
   return new Border { BorderBrush=border,BorderThickness=new Thickness(0,0,0,1),Child=row };
  }

  void OriginalGeneralSettings() {
   OriginalHardwareControlSettings();
   var services=Box("服务状态","");
   foreach(var service in info.Services)services.Children.Add(Label(service.Name+"    "+service.Status,13,service.Status=="Running"?green:muted));
   if(info.Services.Count==0)services.Children.Add(Paragraph("本机服务状态尚未读取。",12,muted));
   var actions=Row();var refresh=Button("刷新状态",async()=>await RefreshInventory());refresh.IsEnabled=!busy;actions.Children.Add(refresh);
   var restart=Button("重启 AURA 服务",async()=>await RestartLighting());restart.IsEnabled=!busy;actions.Children.Add(restart);services.Children.Add(actions);
   services.Children.Add(Paragraph("重启时灯效会短暂中断，Windows 将请求管理员授权。",12,muted));
   content.Children.Add(Card(services));
  }

  void OriginalAboutSettings() {
   var about=Box("OpenAC · 研究版 0.3","本机 Armoury Crate 版本："+InstalledArmouryVersion());
   about.Children.Add(Paragraph("保留原版导航。已实现耳机设置、风扇控制器曲线与灯效、LCD 预设与图片上传、磁盘健康信息、本地宏、游戏库和手动情境配置。设备原图按比例显示。硬件写入仍需要实机验证，未确认状态会明确标出。",13,text));
   about.Children.Add(Paragraph("完整神光同步、主板 Fan Xpert、音效增强、自动情境切换、官方组件安装与固件刷写等功能尚未完成。键鼠已在线完成部分实机验证，OLED 部分读取仍未确认。此版本尚不能完整替代原应用。",13,muted));
   var actions=Row();actions.Children.Add(Button("导出诊断报告",ExportReport));about.Children.Add(actions);content.Children.Add(Card(about));
  }
 }
}
