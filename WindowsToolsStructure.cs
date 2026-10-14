using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  sealed class SessionPowerBackend:IWindowsPowerBackend {
   readonly WindowsPowerNativeBackend native=new WindowsPowerNativeBackend();readonly Action demand;
   public SessionPowerBackend(Action check){demand=check;}
   public IList<WindowsPowerScheme> EnumerateSchemes(){return native.EnumerateSchemes();}
   public Guid GetActiveScheme(){return native.GetActiveScheme();}
   public uint ReadBoost(Guid scheme,bool ac){return native.ReadBoost(scheme,ac);}
   public void WriteBoost(Guid scheme,bool ac,uint value){demand();native.WriteBoost(scheme,ac,value);}
   public void SetActiveScheme(Guid scheme){demand();native.SetActiveScheme(scheme);}
  }
  WindowsPowerService windowsPowerService;
  WindowsPowerSnapshot windowsPower;
  WindowsPowerRestorePoint windowsPowerUndo;
  Task windowsSystemTask;
  bool windowsSystemWriting;
  string windowsPowerMessage="";
  Guid windowsPowerDraftScheme;
  uint? windowsPowerDraftAc,windowsPowerDraftDc;
  bool windowsPowerRestoredDraft;
  static readonly string[] WindowsBoostNames={"禁用","启用","积极","高效启用","高效积极","保证频率以上积极","保证频率以上高效积极"};
  void InitializeWindowsTools(){InitializeWindowsDisplay();InitializeWindowsBrightness();windowsPowerService=new WindowsPowerService(new SessionPowerBackend(()=>{hardwareControlPolicy.Demand();if(render||closing&&!windowsSystemWriting)throw new InvalidOperationException("设置操作已结束。");}));}
  async Task WaitWindowsToolsAsync(){var task=windowsSystemTask;if(task!=null)try{await task;}catch{}}
  void OriginalWindowsPowerPage(){
   Header("Windows 电源","");
   var commands=new WrapPanel{Margin=new Thickness(0,0,0,18)};var read=Button(busy?"正在读取…":"刷新",async()=>await ReadWindowsPower());read.IsEnabled=!busy&&!render&&!closing;commands.Children.Add(read);commands.Children.Add(Button("返回工具",()=>ShowPage("工具")));content.Children.Add(commands);
   if(windowsPower==null){WindowsPowerRestoreActions();if(!String.IsNullOrEmpty(windowsPowerMessage))content.Children.Add(Paragraph(windowsPowerMessage,13,muted));return;}
   var active=windowsPower.Schemes.FirstOrDefault(s=>s.Id==windowsPower.ActiveScheme);content.Children.Add(Paragraph("当前电源计划："+(active==null?"—":active.Name),17,text));
   var scheme=new ComboBox{MinWidth=260,MaxWidth=520,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,12),IsEnabled=!busy};
   foreach(var plan in windowsPower.Schemes){var item=new ComboBoxItem{Content=plan.Name,Tag=plan.Id};scheme.Items.Add(item);if(plan.Id==windowsPowerDraftScheme)scheme.SelectedItem=item;}
   if(scheme.SelectedIndex<0&&scheme.Items.Count>0)scheme.SelectedIndex=0;
   scheme.SelectionChanged+=(s,e)=>{var item=scheme.SelectedItem as ComboBoxItem;if(!busy&&item!=null)windowsPowerDraftScheme=(Guid)item.Tag;};content.Children.Add(scheme);
   var applyScheme=Button("应用电源计划",async()=>await ChangeWindowsPower(true),true);applyScheme.IsEnabled=HardwareWritesEnabled&&!busy&&windowsPower.Consistent;content.Children.Add(applyScheme);
   var boost=Box("CPU Boost","");boost.Children.Add(WindowsBoostSelector("接通电源",true,windowsPower.Boost.AcBoost,windowsPower.Boost.AcError));boost.Children.Add(WindowsBoostSelector("使用电池",false,windowsPower.Boost.DcBoost,windowsPower.Boost.DcError));
   var applyBoost=Button("应用 CPU Boost",async()=>await ChangeWindowsPower(false),true);applyBoost.IsEnabled=HardwareWritesEnabled&&!busy&&windowsPower.Consistent&&(windowsPower.Boost.AcBoost.HasValue||windowsPower.Boost.DcBoost.HasValue);boost.Children.Add(applyBoost);content.Children.Add(Card(boost));
   WindowsPowerRestoreActions();
   if(!String.IsNullOrEmpty(windowsPowerMessage))content.Children.Add(Paragraph(windowsPowerMessage,13,muted));
  }
  void WindowsPowerRestoreActions(){if(windowsPowerUndo!=null){var undo=Row();var restore=Button("恢复修改前的设置",async()=>await RestoreWindowsPower());restore.IsEnabled=HardwareWritesEnabled&&!busy;undo.Children.Add(restore);var keep=Button("保留当前设置",()=>{windowsPowerUndo=null;windowsPowerMessage="已保留当前设置。";ShowPage(page);});keep.IsEnabled=!busy;undo.Children.Add(keep);content.Children.Add(undo);}}
  UIElement WindowsBoostSelector(string label,bool ac,uint? current,string error){
   var row=new Grid{Margin=new Thickness(0,8,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(160)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(Label(label,13,text));
   var select=new ComboBox{Width=250,HorizontalAlignment=HorizontalAlignment.Left,IsEnabled=!busy&&current.HasValue,ToolTip=error};
   ToolTipService.SetShowOnDisabled(select,true);
   if(!current.HasValue){select.Items.Add("未读取");select.SelectedIndex=0;}else{for(uint mode=0;mode<WindowsBoostNames.Length;mode++)select.Items.Add(new ComboBoxItem{Content=WindowsBoostNames[mode],Tag=mode});uint? draft=ac?windowsPowerDraftAc:windowsPowerDraftDc;select.SelectedIndex=(int)(draft??current.Value);select.SelectionChanged+=(s,e)=>{if(busy)return;var item=select.SelectedItem as ComboBoxItem;if(item==null)return;if(ac)windowsPowerDraftAc=(uint)item.Tag;else windowsPowerDraftDc=(uint)item.Tag;};}
   Grid.SetColumn(select,1);row.Children.Add(select);return row;
  }
  void SetWindowsPowerRead(WindowsPowerSnapshot snapshot,bool keepDraft=false){windowsPower=snapshot;if(!keepDraft||!snapshot.Schemes.Any(s=>s.Id==windowsPowerDraftScheme))windowsPowerDraftScheme=snapshot.ActiveScheme;if(!keepDraft||!snapshot.Boost.AcBoost.HasValue||!windowsPowerDraftAc.HasValue||windowsPowerDraftAc.Value>6)windowsPowerDraftAc=snapshot.Boost.AcBoost;if(!keepDraft||!snapshot.Boost.DcBoost.HasValue||!windowsPowerDraftDc.HasValue||windowsPowerDraftDc.Value>6)windowsPowerDraftDc=snapshot.Boost.DcBoost;}
  async Task ReadWindowsPower(){
   if(busy||closing||render)return;busy=true;ShowPage(page);
   try{var task=Task.Run(()=>windowsPowerService.ReadSnapshot());windowsSystemTask=task;var snapshot=await task;if(!closing){SetWindowsPowerRead(snapshot,windowsPowerRestoredDraft);windowsPowerRestoredDraft=false;windowsPowerMessage=snapshot.Consistent?"":"电源计划已改变，请重新读取。";}}
   catch(Exception ex){windowsPower=null;windowsPowerMessage="读取失败："+ex.Message;}
   finally{windowsSystemTask=null;busy=false;if(!closing)ShowPage(page);}
  }
  async Task ChangeWindowsPower(bool scheme){
   if(busy||closing||render||windowsPower==null)return;if(!PermitHardwareWrite())return;
   if(windowsPowerUndo!=null){windowsPowerMessage="请先保留当前设置或恢复上一次修改。";ShowPage(page);return;}
   Guid expected=windowsPower.ActiveScheme,target=windowsPowerDraftScheme;uint? ac=windowsPowerDraftAc,dc=windowsPowerDraftDc;
   busy=true;windowsSystemWriting=true;ShowPage(page);
   try{var task=Task.Run(()=>scheme?windowsPowerService.ChangeActiveScheme(target,expected):windowsPowerService.ChangeCpuBoost(expected,ac,dc));windowsSystemTask=task;WindowsPowerChanged(await task);}
   catch(Exception ex){windowsPowerMessage="应用失败："+ex.Message;}
   finally{windowsSystemWriting=false;windowsSystemTask=null;busy=false;if(!closing)ShowPage(page);}
  }
  async Task RestoreWindowsPower(){
   if(busy||closing||render||windowsPowerUndo==null)return;if(!PermitHardwareWrite())return;
   var point=windowsPowerUndo;busy=true;windowsSystemWriting=true;ShowPage(page);
   try{var task=Task.Run(()=>windowsPowerService.Restore(point));windowsSystemTask=task;var result=await task;WindowsPowerChanged(result);if(result.Succeeded){windowsPowerUndo=null;windowsPowerMessage="修改前的设置已恢复并读回。";}}
   catch(Exception ex){windowsPowerMessage="恢复失败："+ex.Message;}
   finally{windowsSystemWriting=false;windowsSystemTask=null;busy=false;if(!closing)ShowPage(page);}
  }
  void WindowsPowerChanged(WindowsPowerChangeResult result){
   if(result.After!=null)SetWindowsPowerRead(result.After);else windowsPower=null;
   if(result.RestorePoint!=null)windowsPowerUndo=result.RestorePoint;
   if(result.Succeeded){windowsPowerMessage=result.Changed?"设置已应用并读回。":"设置相同，无需修改。";}
   else{windowsPowerMessage="应用未完成："+result.Error;if(result.RollbackAttempted)windowsPowerMessage+=result.RollbackSucceeded?" 修改前的设置已恢复。":" 恢复失败："+result.RollbackError;}
  }
 }
}
