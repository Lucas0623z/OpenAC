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
   Header("更新库","");
   OriginalOpenAcUpdateCenter();
   OriginalAdvancedHardwareControl();
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
   if(info.Components.Count==0)components.Children.Add(Paragraph("暂无数据",12,muted));
   content.Children.Add(SettingsGroup("设备与元件","全部更新",components));
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

  void OriginalAdvancedHardwareControl() {
   var controls=new CheckBox{Content="启用设备控制",Foreground=text,IsChecked=hardwareControlPolicy.Enabled,IsEnabled=!busy&&!closing&&!render,ToolTip="开启后可以手动应用已适配的设备设置；开关本身不发送设备命令。仅本次运行有效。",Margin=new Thickness(0,8,0,12)};
   RoutedEventHandler change=(s,e)=>SetSessionHardwareControl(controls.IsChecked==true);controls.Checked+=change;controls.Unchecked+=change;
   content.Children.Add(new Expander{Header="设备控制",Content=controls,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,IsExpanded=!hardwareControlPolicy.Enabled});
  }
 }
}
