using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace RogControl {
 public partial class MainWindow {
  readonly MacroEditor macroEditor=new MacroEditor();
  MacroRepository macroRepository;
  List<MacroDocument> macroFiles=new List<MacroDocument>();
  ListBox macroActionList;StackPanel macroParameters;TextBlock macroCount,macroDuration,macroNotice;
  Button macroRecordButton,macroUndoButton,macroRedoButton,macroClearButton,macroInsertButton,macroRunButton,macroStopButton;
  ComboBox macroFileBox,macroKindBox,macroKeyBox,macroMouseBox;TextBox macroNameBox,macroBeforeBox,macroDelayBox,macroWheelBox,macroXBox,macroYBox;
  CheckBox macroRecordPositions;int macroSelection=-1;bool macroBuilding;
  string macroMessage="本地宏 · 尚未保存";bool macroPositions;
  MacroHookSession macroRecordingHook,macroPlaybackHook;MacroCaptureBuffer macroCapture;MacroDocument macroRecordingBase;Stopwatch macroClock;DispatcherTimer macroRecordingTimer;
  CancellationTokenSource macroPlayCancel;Task macroPlayTask;
  readonly List<FrameworkElement> macroEditingControls=new List<FrameworkElement>();
  public bool MacroFeaturesActive {get{return macroRecordingHook!=null||macroPlayTask!=null;}}

  void OriginalMacroPage() {
   if(macroRepository==null){macroRepository=new MacroRepository(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ROG Control","Macros"));LoadMacroFiles();}
   macroBuilding=true;macroEditingControls.Clear();
   content.Children.Add(Label("宏",25,text,FontWeights.SemiBold));
   var toolbar=new Grid{Margin=new Thickness(0,20,0,24)};toolbar.ColumnDefinitions.Add(new ColumnDefinition());toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var left=new StackPanel{Orientation=Orientation.Horizontal};macroRecordButton=Button(macroRecordingHook==null?"● 录制":"■ 停止录制",ToggleMacroRecording);macroInsertButton=Button("插入 ▾",OpenMacroInsertMenu);left.Children.Add(macroRecordButton);left.Children.Add(macroInsertButton);toolbar.Children.Add(left);
   var right=new StackPanel{Orientation=Orientation.Horizontal};right.Children.Add(Label("宏文件",13,muted));
   macroFileBox=MacroCombo(220);macroFileBox.DisplayMemberPath="Name";foreach(var doc in macroFiles)macroFileBox.Items.Add(doc);var selectedFile=macroFiles.FirstOrDefault(d=>d.Id==macroEditor.Current.Id);if(selectedFile==null){selectedFile=macroEditor.Current.Clone();macroFileBox.Items.Add(selectedFile);}macroFileBox.SelectedItem=selectedFile;macroFileBox.SelectionChanged+=(s,e)=>{if(macroBuilding||MacroFeaturesActive)return;var doc=macroFileBox.SelectedItem as MacroDocument;if(doc==null)return;macroEditor.Load(doc);macroSelection=-1;macroMessage="已载入宏："+doc.Name;ShowPage("宏");};right.Children.Add(macroFileBox);
   var fileMenu=Button("⋯",OpenMacroFileMenu);fileMenu.ToolTip="新建、导入、导出";right.Children.Add(fileMenu);
   macroUndoButton=Button("↶",()=>{macroEditor.Undo();RefreshMacroEditor();});macroUndoButton.ToolTip="撤销";macroRedoButton=Button("↷",()=>{macroEditor.Redo();RefreshMacroEditor();});macroRedoButton.ToolTip="重做";macroClearButton=Button("清除全部",()=>MacroChange(d=>d.Actions.Clear()));right.Children.Add(macroUndoButton);right.Children.Add(macroRedoButton);right.Children.Add(macroClearButton);Grid.SetColumn(right,1);toolbar.Children.Add(right);content.Children.Add(toolbar);
   macroEditingControls.Add(macroFileBox);macroEditingControls.Add(fileMenu);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(73,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(22)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(27,GridUnitType.Star)});
   var timeline=new Grid{Height=520};timeline.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});timeline.RowDefinitions.Add(new RowDefinition());timeline.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new Grid{Margin=new Thickness(14,10,14,14)};header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});header.ColumnDefinitions.Add(new ColumnDefinition());header.Children.Add(Label("时间",12,muted));macroCount=Label("",12,text);Grid.SetColumn(macroCount,1);header.Children.Add(macroCount);timeline.Children.Add(header);
   macroActionList=new ListBox{Background=Brushes.Transparent,Foreground=text,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};macroActionList.SelectionChanged+=(s,e)=>{if(macroBuilding)return;macroSelection=macroActionList.SelectedIndex;BuildMacroParameters();};Grid.SetRow(macroActionList,1);timeline.Children.Add(macroActionList);
   var lower=new Grid{Margin=new Thickness(14,15,14,10)};lower.ColumnDefinitions.Add(new ColumnDefinition());lower.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});macroDuration=Label("",12,muted);lower.Children.Add(macroDuration);var order=new StackPanel{Orientation=Orientation.Horizontal};foreach(string action in new[]{"上移","下移","删除"}){string command=action;var button=Button(command,()=>MoveMacroAction(command));order.Children.Add(button);macroEditingControls.Add(button);}Grid.SetColumn(order,1);lower.Children.Add(order);Grid.SetRow(lower,2);timeline.Children.Add(lower);
   body.Children.Add(new Border{BorderBrush=border,BorderThickness=new Thickness(1),Background=Brushes.Transparent,Child=timeline});
   var parameters=new Grid{Height=520};parameters.RowDefinitions.Add(new RowDefinition());parameters.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});macroParameters=new StackPanel{Margin=new Thickness(16,12,10,10)};parameters.Children.Add(new ScrollViewer{Content=macroParameters,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
   var saveRow=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};var save=Button("保存",SaveOriginalMacro,true);var cancel=Button("取消",BuildMacroParameters);cancel.ToolTip="取消尚未保存的参数编辑";saveRow.Children.Add(save);saveRow.Children.Add(cancel);macroEditingControls.Add(save);macroEditingControls.Add(cancel);Grid.SetRow(saveRow,1);parameters.Children.Add(saveRow);Grid.SetColumn(parameters,2);body.Children.Add(parameters);content.Children.Add(body);
   macroNotice=Paragraph(macroMessage,12,muted);macroNotice.Margin=new Thickness(0,15,0,0);content.Children.Add(macroNotice);
   var limitation=Paragraph("软件宏只在手动执行后发送至前台窗口，F9 可停止。硬件板载宏写入尚未接通。",11,muted);limitation.Margin=new Thickness(0,6,0,0);content.Children.Add(limitation);
   macroBuilding=false;RefreshMacroEditor();
  }
  ComboBox MacroCombo(double width){return new ComboBox{MinWidth=width,Foreground=Brush("#111111"),Background=Brush("#EEEEEE"),BorderBrush=border,Padding=new Thickness(8,6,8,6),Margin=new Thickness(0,0,8,0),FontSize=12};}
  TextBox MacroText(string value){return new TextBox{Text=value,Foreground=text,Background=panel,BorderBrush=border,Padding=new Thickness(8,6,8,6),Margin=new Thickness(0,0,0,12),FontSize=12};}
  void MacroField(string label,FrameworkElement field){macroParameters.Children.Add(Label(label,12,muted));macroParameters.Children.Add(field);field.IsEnabled=!MacroFeaturesActive;}
  List<MacroAction> DisplayedMacroActions(){return macroRecordingHook!=null&&macroCapture!=null?macroRecordingBase.Actions.Concat(macroCapture.Snapshot()).ToList():macroEditor.Current.Actions;}
  void RefreshMacroEditor() {
   if(macroActionList==null||page!="宏")return;macroBuilding=true;var actions=DisplayedMacroActions();macroActionList.Items.Clear();long time=0;
   for(int i=0;i<actions.Count;i++){var a=actions[i];time+=a.BeforeMs+(a.Kind=="delay"?a.DelayMs:0);var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});row.ColumnDefinitions.Add(new ColumnDefinition());var at=Label((time/1000.0).ToString("0.000",CultureInfo.InvariantCulture)+" s",12,muted);at.Margin=new Thickness(0);row.Children.Add(at);var action=Label(MacroActionLabel(a),13,text);action.Margin=new Thickness(0);Grid.SetColumn(action,1);row.Children.Add(action);macroActionList.Items.Add(new ListBoxItem{Content=row,Foreground=text,Padding=new Thickness(14,8,14,8),HorizontalContentAlignment=HorizontalAlignment.Stretch});}
   macroSelection=Math.Min(macroSelection,actions.Count-1);macroActionList.SelectedIndex=macroSelection;macroCount.Text="动作 ("+actions.Count+" / 100)";macroDuration.Text="总时长  "+(time/1000.0).ToString("0.000",CultureInfo.InvariantCulture)+" 秒";
   macroUndoButton.IsEnabled=!MacroFeaturesActive&&macroEditor.CanUndo;macroRedoButton.IsEnabled=!MacroFeaturesActive&&macroEditor.CanRedo;macroClearButton.IsEnabled=!MacroFeaturesActive&&actions.Count>0;macroInsertButton.IsEnabled=!MacroFeaturesActive&&actions.Count<100;macroRecordButton.IsEnabled=macroPlayTask==null;macroRecordButton.Content=macroRecordingHook==null?"● 录制":"■ 停止录制";macroActionList.IsEnabled=!MacroFeaturesActive;
   foreach(var control in macroEditingControls)control.IsEnabled=!MacroFeaturesActive;macroBuilding=false;BuildMacroParameters();UpdateMacroNotice();
  }
  static string MacroActionLabel(MacroAction a) {if(a.Kind=="delay")return "延时 "+a.DelayMs+" ms";if(a.Kind=="mouse-move")return "鼠标位置 "+a.X+", "+a.Y;if(a.Kind=="mouse-wheel")return "滚轮 "+a.Value;string state=a.Kind.EndsWith("down",StringComparison.Ordinal)?"按下":"释放";if(a.Kind.StartsWith("key-",StringComparison.Ordinal))return "键盘 "+KeyInterop.KeyFromVirtualKey(a.Key)+" · "+state;return new[]{"","左键","右键","中键","侧键 1","侧键 2"}[a.Button]+" · "+state;}
  void BuildMacroParameters() {
   if(macroParameters==null||page!="宏")return;macroParameters.Children.Clear();macroParameters.Children.Add(Label("参数",16,text,FontWeights.SemiBold));macroNameBox=MacroText(macroEditor.Current.Name);MacroField("宏名称",macroNameBox);
   var positions=new CheckBox{Content="录制鼠标移动",IsChecked=macroPositions,Foreground=text,Margin=new Thickness(0,0,0,14),IsEnabled=!MacroFeaturesActive};positions.Checked+=(s,e)=>macroPositions=true;positions.Unchecked+=(s,e)=>macroPositions=false;macroRecordPositions=positions;macroParameters.Children.Add(positions);
   MacroAction selected=macroSelection>=0&&macroSelection<macroEditor.Current.Actions.Count?macroEditor.Current.Actions[macroSelection]:null;
   macroKindBox=null;macroKeyBox=null;macroMouseBox=null;macroBeforeBox=null;macroDelayBox=null;macroWheelBox=null;macroXBox=null;macroYBox=null;
   if(selected==null){macroParameters.Children.Add(Paragraph("从左侧选择动作，或用“插入”添加键盘、鼠标或延时。",12,muted));}
   else {
    macroParameters.Children.Add(Label("动作 "+(macroSelection+1),13,text));macroBeforeBox=MacroText(selected.BeforeMs.ToString());MacroField("动作前延时（ms）",macroBeforeBox);
    if(selected.Kind.StartsWith("key-",StringComparison.Ordinal)) {
     macroKeyBox=MacroCombo(120);foreach(Key key in Enum.GetValues(typeof(Key)).Cast<Key>().Distinct()){int vk=KeyInterop.VirtualKeyFromKey(key);if(vk>0&&vk<255&&vk!=0x78)macroKeyBox.Items.Add(new MacroKeyChoice{Name=key.ToString(),Code=vk});}macroKeyBox.DisplayMemberPath="Name";macroKeyBox.SelectedItem=macroKeyBox.Items.Cast<MacroKeyChoice>().FirstOrDefault(k=>k.Code==selected.Key);MacroField("键盘按键",macroKeyBox);macroKindBox=MacroCombo(100);macroKindBox.Items.Add("按下");macroKindBox.Items.Add("释放");macroKindBox.SelectedIndex=selected.Kind=="key-down"?0:1;MacroField("动作",macroKindBox);
    }else if(selected.Kind=="mouse-down"||selected.Kind=="mouse-up") {
     macroMouseBox=MacroCombo(120);foreach(string button in new[]{"左键","右键","中键","侧键 1","侧键 2"})macroMouseBox.Items.Add(button);macroMouseBox.SelectedIndex=selected.Button-1;MacroField("鼠标按钮",macroMouseBox);macroKindBox=MacroCombo(100);macroKindBox.Items.Add("按下");macroKindBox.Items.Add("释放");macroKindBox.SelectedIndex=selected.Kind=="mouse-down"?0:1;MacroField("动作",macroKindBox);
    }else if(selected.Kind=="delay"){macroDelayBox=MacroText(selected.DelayMs.ToString());MacroField("延时（ms）",macroDelayBox);}
    else if(selected.Kind=="mouse-wheel"){macroWheelBox=MacroText(selected.Value.ToString());MacroField("滚轮值（120 为一格）",macroWheelBox);}
    else if(selected.Kind=="mouse-move"){macroXBox=MacroText(selected.X.ToString());macroYBox=MacroText(selected.Y.ToString());MacroField("X 坐标",macroXBox);MacroField("Y 坐标",macroYBox);}
   }
   var run=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};macroRunButton=Button("软件执行",BeginMacroPlayback);macroRunButton.IsEnabled=!MacroFeaturesActive&&macroEditor.Current.Actions.Count>0;macroRunButton.ToolTip="3 秒倒计时后执行列表内动作";macroStopButton=Button("停止",StopMacroFeatures);macroStopButton.IsEnabled=MacroFeaturesActive;run.Children.Add(macroRunButton);run.Children.Add(macroStopButton);macroParameters.Children.Add(run);
  }
  sealed class MacroKeyChoice {public string Name{get;set;}public int Code{get;set;}}
  void OpenMacroInsertMenu(){var menu=new ContextMenu();string[] labels={"键盘按下","键盘释放","鼠标按下","鼠标释放","滚轮","鼠标位置","延时"},kinds={"key-down","key-up","mouse-down","mouse-up","mouse-wheel","mouse-move","delay"};for(int i=0;i<kinds.Length;i++){string kind=kinds[i];var item=new MenuItem{Header=labels[i]};item.Click+=(s,e)=>InsertMacroAction(kind);menu.Items.Add(item);}macroInsertButton.ContextMenu=menu;menu.PlacementTarget=macroInsertButton;menu.IsOpen=true;}
  void InsertMacroAction(string kind){if(MacroFeaturesActive)return;int index=macroSelection<0?macroEditor.Current.Actions.Count:macroSelection+1;MacroChange(d=>d.Actions.Insert(index,new MacroAction{Kind=kind,Key=65,Button=1,Value=120,DelayMs=100}));macroSelection=Math.Min(index,macroEditor.Current.Actions.Count-1);RefreshMacroEditor();}
  void MacroChange(Action<MacroDocument> change){if(MacroFeaturesActive)return;try{macroEditor.Change(change);macroMessage="有未保存的修改";RefreshMacroEditor();}catch(Exception e){MacroError(e);}}
  void MoveMacroAction(string command){if(MacroFeaturesActive||macroSelection<0||macroSelection>=macroEditor.Current.Actions.Count)return;int from=macroSelection,to=command=="上移"?from-1:from+1;if(command=="删除"){MacroChange(d=>d.Actions.RemoveAt(from));return;}if(to<0||to>=macroEditor.Current.Actions.Count)return;MacroChange(d=>{var a=d.Actions[from];d.Actions.RemoveAt(from);d.Actions.Insert(to,a);});macroSelection=to;RefreshMacroEditor();}
  int MacroNumber(TextBox input,string label){int value;if(input==null||!Int32.TryParse(input.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value))throw new InvalidDataException(label+"必须是整数。");return value;}
  void SaveOriginalMacro() {
   if(MacroFeaturesActive)return;try {
    string name=macroNameBox.Text.Trim();MacroAction edited=null;int index=macroSelection;
    if(index>=0&&index<macroEditor.Current.Actions.Count){edited=macroEditor.Current.Actions[index].Clone();edited.BeforeMs=MacroNumber(macroBeforeBox,"动作前延时");if(macroKeyBox!=null){var key=macroKeyBox.SelectedItem as MacroKeyChoice;if(key==null)throw new InvalidDataException("请选择键盘按键。");if(edited.Key!=key.Code){edited.ScanCode=0;edited.Extended=false;}edited.Key=key.Code;edited.Kind=macroKindBox.SelectedIndex==0?"key-down":"key-up";}if(macroMouseBox!=null){edited.Button=macroMouseBox.SelectedIndex+1;edited.Kind=macroKindBox.SelectedIndex==0?"mouse-down":"mouse-up";}if(macroDelayBox!=null)edited.DelayMs=MacroNumber(macroDelayBox,"延时");if(macroWheelBox!=null)edited.Value=MacroNumber(macroWheelBox,"滚轮值");if(macroXBox!=null){edited.X=MacroNumber(macroXBox,"X 坐标");edited.Y=MacroNumber(macroYBox,"Y 坐标");}}
    var change=edited;macroEditor.Change(d=>{d.Name=name;if(change!=null)d.Actions[index]=change;});macroRepository.Save(macroEditor.Current);LoadMacroFiles();macroMessage="已保存本地宏："+name;ShowPage("宏");
   }catch(Exception e){MacroError(e);}
  }
  void OpenMacroFileMenu(){var menu=new ContextMenu();foreach(string label in new[]{"新建宏","导入本地宏文件","导出本地宏文件"}){string command=label;var item=new MenuItem{Header=label};item.Click+=(s,e)=>{if(command=="新建宏"){macroEditor.Load(new MacroDocument());macroSelection=-1;macroMessage="新宏 · 尚未保存";ShowPage("宏");}else if(command.StartsWith("导入",StringComparison.Ordinal))ImportOriginalMacro();else ExportOriginalMacro();};menu.Items.Add(item);}menu.PlacementTarget=macroFileBox;menu.IsOpen=true;}
  void LoadMacroFiles(){try{string warning;macroFiles=macroRepository.List(out warning);if(warning!=null)macroMessage=warning;}catch(Exception e){macroFiles=new List<MacroDocument>();macroMessage="本地宏目录暂不可用："+e.Message;}}
  void ImportOriginalMacro(){if(MacroFeaturesActive)return;var dialog=new Microsoft.Win32.OpenFileDialog{Filter="本版宏文件 (*.rogmacro.json)|*.rogmacro.json|JSON 文件 (*.json)|*.json",CheckFileExists=true};if(dialog.ShowDialog(this)!=true)return;try{var imported=MacroRepository.Read(dialog.FileName);imported.Id=Guid.NewGuid().ToString("N");macroEditor.Load(imported);macroSelection=-1;macroMessage="已导入，点击保存写入本地宏库。";ShowPage("宏");}catch(Exception e){MacroError(e);}}
  void ExportOriginalMacro(){if(MacroFeaturesActive)return;var dialog=new Microsoft.Win32.SaveFileDialog{Filter="本版宏文件 (*.rogmacro.json)|*.rogmacro.json",DefaultExt=".rogmacro.json",FileName="macro.rogmacro.json",AddExtension=true};if(dialog.ShowDialog(this)!=true)return;try{MacroRepository.Write(dialog.FileName,macroEditor.Current);macroMessage="已导出本地宏文件。";UpdateMacroNotice();}catch(Exception e){MacroError(e);}}
  void ToggleMacroRecording(){if(macroRecordingHook!=null){FinishMacroRecording("录制已停止。",null);return;}if(MacroFeaturesActive||render)return;try {
   var baseline=macroEditor.Current.Clone();if(baseline.Actions.Count>=100)throw new InvalidOperationException("已有 100 条动作，请先删除部分动作。");if(baseline.DurationMs>=MacroRules.MaxTotalMs)throw new InvalidOperationException("已达到总延时上限。");
   var capture=new MacroCaptureBuffer(macroPositions,100-baseline.Actions.Count,MacroRules.MaxTotalMs-baseline.DurationMs);var clock=Stopwatch.StartNew();MacroHookSession hook=null;int published=0;
   Action stop=()=>{hook.Dispose();Dispatcher.BeginInvoke(new Action(()=>FinishMacroRecording(capture.StopReason??"F9：录制已停止。",hook)));};
   hook=new MacroHookSession(true,a=>{bool keep=capture.Add(a,clock.ElapsedMilliseconds);int count=capture.Count;if(count!=published){published=count;Dispatcher.BeginInvoke(new Action(()=>{if(macroRecordingHook==hook){macroMessage="正在录制本程序外的输入 · F9 停止 · "+(baseline.Actions.Count+capture.Count)+" / 100";RefreshMacroEditor();}}));}if(!keep)stop();},stop);
   macroCapture=capture;macroClock=clock;macroRecordingBase=baseline;macroRecordingHook=hook;hook.Start();macroMessage="正在录制本程序外的输入 · F9 停止 · "+(macroPositions?"包含鼠标位置":"按钮与滚轮，鼠标位置不录制");
   macroRecordingTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};macroRecordingTimer.Tick+=(s,e)=>{if(macroRecordingHook==hook&&clock.ElapsedMilliseconds>=MacroRules.MaxTotalMs-baseline.DurationMs)FinishMacroRecording("达到 10 分钟总延时上限，录制已停止。",hook);};macroRecordingTimer.Start();RefreshMacroEditor();
  }catch(Exception e){if(macroRecordingHook!=null)macroRecordingHook.Dispose();macroRecordingHook=null;macroCapture=null;MacroError(e);RefreshMacroEditor();}}
  void FinishMacroRecording(string reason,MacroHookSession expected) {
   if(macroRecordingHook==null||(expected!=null&&macroRecordingHook!=expected))return;var hook=macroRecordingHook;hook.Dispose();macroRecordingHook=null;if(macroRecordingTimer!=null){macroRecordingTimer.Stop();macroRecordingTimer=null;}var added=macroCapture.Snapshot();macroCapture=null;macroClock=null;
   try{if(added.Count>0)macroEditor.Change(d=>d.Actions.AddRange(added));macroMessage=reason+" 已录制 "+added.Count+" 条，请保存。";}catch(Exception e){macroMessage="录制结果未能加入动作列表："+e.Message;}RefreshMacroEditor();
  }
  void BeginMacroPlayback(){if(MacroFeaturesActive||render)return;try{MacroRules.Validate(macroEditor.Current);if(macroEditor.Current.Actions.Count==0)return;macroPlayCancel=new CancellationTokenSource();macroPlayTask=RunOriginalMacroAsync(macroEditor.Current.Clone(),macroPlayCancel.Token);RefreshMacroEditor();}catch(Exception e){MacroError(e);}}
  async Task RunOriginalMacroAsync(MacroDocument doc,CancellationToken cancel) {
   await Task.Yield();
   try {
    cancel.ThrowIfCancellationRequested();
    macroPlaybackHook=new MacroHookSession(false,null,()=>{var source=macroPlayCancel;if(source!=null)source.Cancel();});macroPlaybackHook.Start();
    for(int left=3;left>0;left--){macroMessage=left+" 秒后向前台窗口执行宏 · F9 / 停止可取消";UpdateMacroNotice();await Task.Delay(1000,cancel);}
    macroMessage="正在软件执行 · F9 / 停止可终止";UpdateMacroNotice();await MacroPlayback.RunAsync(doc,new WindowsMacroInput(),cancel,done=>{macroMessage="正在软件执行 "+done+" / "+doc.Actions.Count+" · F9 停止";UpdateMacroNotice();});macroMessage="软件执行完成，已释放本次宏按下的键。";
   }catch(OperationCanceledException){macroMessage="软件执行已停止，已释放本次宏按下的键。";}catch(Exception e){macroMessage="软件执行未完成："+e.Message;}
   finally{if(macroPlaybackHook!=null){macroPlaybackHook.Dispose();macroPlaybackHook=null;}if(macroPlayCancel!=null){macroPlayCancel.Dispose();macroPlayCancel=null;}macroPlayTask=null;if(!closing)RefreshMacroEditor();}
  }
  void StopMacroFeatures(){if(macroRecordingHook!=null)FinishMacroRecording("录制已停止。",null);if(macroPlayCancel!=null)macroPlayCancel.Cancel();}
  public void OnMacroNavigation(string nextPage){if(nextPage!="宏")StopMacroFeatures();}
  public async Task StopMacroFeaturesAsync(){StopMacroFeatures();var task=macroPlayTask;if(task!=null)try{await task;}catch{}if(macroPlaybackHook!=null){macroPlaybackHook.Dispose();macroPlaybackHook=null;}}
  void UpdateMacroNotice(){if(macroNotice!=null&&page=="宏")macroNotice.Text=macroMessage;}
  void MacroError(Exception e){macroMessage=e.Message;UpdateMacroNotice();}
 }
}
