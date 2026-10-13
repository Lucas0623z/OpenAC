using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 // Consent is deliberately session-only. Loading a profile, importing settings,
 // or reading a device never grants permission to send configuration commands.
 public sealed class HardwareControlPolicy {
  public const string BlockedMessage="设备控制当前关闭。可在设置 → 一般设置中开启本次运行的实验性设备控制。";
  public bool Enabled { get; private set; }
  public void SetEnabled(bool enabled) { Enabled=enabled; }
  public void Demand() { if(!Enabled)throw new InvalidOperationException(BlockedMessage); }
 }
 public partial class MainWindow {
  readonly HardwareControlPolicy hardwareControlPolicy=new HardwareControlPolicy();
  bool HardwareWritesEnabled { get { return hardwareControlPolicy.Enabled&&!closing&&!render; } }
  bool PermitHardwareWrite() { if(HardwareWritesEnabled)return true;Log(HardwareControlPolicy.BlockedMessage);return false; }
  bool CanApplyGear(IDictionary<string,object> state,string setting) { return HardwareWritesEnabled&&GearCanSet(state,setting); }
  void HardwareControlHint(StackPanel target) { if(!HardwareWritesEnabled)target.Children.Add(Paragraph(HardwareControlPolicy.BlockedMessage,11,muted)); }
  void OriginalHardwareControlSettings() {
   var controls=Box("实验性设备控制","");
   var enable=new CheckBox { Content="允许本次运行应用设备设置",IsChecked=hardwareControlPolicy.Enabled,IsEnabled=!busy&&!closing&&!render,Foreground=text,Margin=new Thickness(0,6,0,12) };
   RoutedEventHandler changed=(s,e)=>{if(busy||closing||render)return;hardwareControlPolicy.SetEnabled(enable.IsChecked==true);ShowPage(page);};
   enable.Checked+=changed;enable.Unchecked+=changed;controls.Children.Add(enable);
   controls.Children.Add(Paragraph("用于键鼠、耳机、风扇控制器、LCD、Windows 音量和手动情境配置。每次启动默认关闭；读取、导出和编辑本地草稿仍可使用。",12,muted));
   controls.Children.Add(Paragraph("未完成实机验证的设置仍标为实验性。神光同步使用灯效页面上的独立试用开关。",12,muted));
   content.Children.Add(Card(controls));
  }
 }
}
