using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 // Consent is deliberately session-only. Loading a profile, importing settings,
 // or reading a device never grants permission to send configuration commands.
 public sealed class HardwareControlPolicy {
  public const string BlockedMessage="设备控制已关闭，请在更新库 → 设备控制中开启。";
  public bool Enabled { get; private set; }
  public void SetEnabled(bool enabled) { Enabled=enabled; }
  public void Demand() { if(!Enabled)throw new InvalidOperationException(BlockedMessage); }
 }
 public partial class MainWindow {
  readonly HardwareControlPolicy hardwareControlPolicy=new HardwareControlPolicy();
  const string PreviewControlGrantSlot="OpenAC.PreviewHardwareControlSession";
  bool HasPreviewControlRuntime {get{return !String.IsNullOrWhiteSpace(AppDomain.CurrentDomain.GetData("OpenAC.PreviewWorkerRoot") as string);}}
  void SetSessionHardwareControl(bool enabled){if(render||closing||busy)return;hardwareControlPolicy.SetEnabled(enabled);if(HasPreviewControlRuntime)AppDomain.CurrentDomain.SetData(PreviewControlGrantSlot,enabled);ShowPage(page);}
  void RestoreLivePreviewControlSession(bool previewReload){if(!render&&previewReload&&HasPreviewControlRuntime&&Object.Equals(AppDomain.CurrentDomain.GetData(PreviewControlGrantSlot),true))hardwareControlPolicy.SetEnabled(true);}
  bool HardwareWritesEnabled { get { return hardwareControlPolicy.Enabled&&!closing&&!render; } }
  bool PermitHardwareWrite() { if(HardwareWritesEnabled)return true;Log(HardwareControlPolicy.BlockedMessage);return false; }
  bool CanApplyGear(IDictionary<string,object> state,string setting) { return HardwareWritesEnabled&&!busy&&!AuraSyncBusy&&GearCanSet(state,setting); }
  void HardwareControlHint(StackPanel target) { if(!HardwareWritesEnabled)target.Children.Add(Paragraph(HardwareControlPolicy.BlockedMessage,11,muted)); }
  void OriginalHardwareControlSettings() {
   var controls=Box("实验性设备控制","");
   var enable=new CheckBox { Content="允许本次运行应用设备设置",IsChecked=hardwareControlPolicy.Enabled,IsEnabled=!busy&&!closing&&!render,Foreground=text,Margin=new Thickness(0,6,0,12) };
   RoutedEventHandler changed=(s,e)=>SetSessionHardwareControl(enable.IsChecked==true);
   enable.Checked+=changed;enable.Unchecked+=changed;controls.Children.Add(enable);
   controls.Children.Add(Paragraph("用于键鼠、耳机、风扇控制器、LCD、Windows 音量和手动情境配置。每次启动默认关闭；读取、导出和编辑本地草稿仍可使用。",12,muted));
   controls.Children.Add(Paragraph("未完成实机验证的设置仍标为实验性。Aura Sync 也使用本次运行的设备控制开关。",12,muted));
   content.Children.Add(Card(controls));
  }
 }
}
