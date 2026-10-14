using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  sealed class KeyboardKeyShape {
   public string Source,Caption;
   public double X,Y,Width;
   public KeyboardKeyShape(string source,string caption,double x,double y,double width){Source=source;Caption=caption;X=x;Y=y;Width=width;}
  }

  // Independently authored standard ANSI 75% schematic. This contains no ASUS
  // position/command codes, vendor artwork/font or third-party application table.
  List<KeyboardKeyShape> KeyboardKeysSchematic() {
   var keys=new List<KeyboardKeyShape>();double unit=36;
   keys.Add(new KeyboardKeyShape("Escape","Esc",0,0,28));
   for(int number=1;number<=12;number++)keys.Add(new KeyboardKeyShape("F"+number,"F"+number,number*unit,0,28));
   keys.Add(new KeyboardKeyShape("Backquote","~\n`",0,47,28));
   string symbols="!@#$%^&*()";
   for(int index=0;index<10;index++){int digit=(index+1)%10;keys.Add(new KeyboardKeyShape("Digit"+digit,symbols[index]+"\n"+digit,(index+1)*unit,47,28));}
   keys.Add(new KeyboardKeyShape("Minus","_\n−",11*unit,47,28));keys.Add(new KeyboardKeyShape("Equal","+\n=",12*unit,47,28));keys.Add(new KeyboardKeyShape("Backspace","←",13*unit,47,64));keys.Add(new KeyboardKeyShape("Insert","Ins",548,47,28));
   keys.Add(new KeyboardKeyShape("Tab","⇥",0,83,46));
   string top="QWERTYUIOP";for(int index=0;index<top.Length;index++)keys.Add(new KeyboardKeyShape("Key"+top[index],top[index].ToString(),54+index*unit,83,28));
   keys.Add(new KeyboardKeyShape("BracketLeft","{\n[",414,83,28));keys.Add(new KeyboardKeyShape("BracketRight","}\n]",450,83,28));keys.Add(new KeyboardKeyShape("Backslash","|\n\\",486,83,46));keys.Add(new KeyboardKeyShape("Delete","Del",548,83,28));
   keys.Add(new KeyboardKeyShape("CapsLock","Caps",0,119,55));
   string middle="ASDFGHJKL";for(int index=0;index<middle.Length;index++)keys.Add(new KeyboardKeyShape("Key"+middle[index],middle[index].ToString(),63+index*unit,119,28));
   keys.Add(new KeyboardKeyShape("Semicolon",":\n;",387,119,28));keys.Add(new KeyboardKeyShape("Quote","\"\n'",423,119,28));keys.Add(new KeyboardKeyShape("Enter","↵",459,119,73));keys.Add(new KeyboardKeyShape("PageUp","PgUp",548,119,28));
   keys.Add(new KeyboardKeyShape("ShiftLeft","⇧",0,155,73));
   string lower="ZXCVBNM";for(int index=0;index<lower.Length;index++)keys.Add(new KeyboardKeyShape("Key"+lower[index],lower[index].ToString(),81+index*unit,155,28));
   keys.Add(new KeyboardKeyShape("Comma","<\n,",333,155,28));keys.Add(new KeyboardKeyShape("Period",">\n.",369,155,28));keys.Add(new KeyboardKeyShape("Slash","?\n/",405,155,28));keys.Add(new KeyboardKeyShape("ShiftRight","⇧",441,155,55));keys.Add(new KeyboardKeyShape("ArrowUp","↑",504,155,28));keys.Add(new KeyboardKeyShape("PageDown","PgDn",548,155,28));
   keys.Add(new KeyboardKeyShape("ControlLeft","Ctrl",0,191,46));keys.Add(new KeyboardKeyShape("MetaLeft","Win",54,191,37));keys.Add(new KeyboardKeyShape("AltLeft","Alt",99,191,37));keys.Add(new KeyboardKeyShape("Space","—",144,191,208));keys.Add(new KeyboardKeyShape("AltRight","Alt",360,191,28));keys.Add(new KeyboardKeyShape("Fn","Fn",396,191,28));keys.Add(new KeyboardKeyShape("ControlRight","Ctrl",432,191,28));keys.Add(new KeyboardKeyShape("ArrowLeft","←",468,191,28));keys.Add(new KeyboardKeyShape("ArrowDown","↓",504,191,28));keys.Add(new KeyboardKeyShape("ArrowRight","→",548,191,28));
   return keys;
  }

  void OriginalKeyboardKeysPage(StackPanel target,Dictionary<string,object> state) {
   var shapes=KeyboardKeysSchematic();var physical=new HashSet<string>(shapes.Select(key=>key.Source),StringComparer.Ordinal);
   var sources=new HashSet<string>(GearOptions(state,"keySources"),StringComparer.Ordinal);
   if(!physical.Contains(selectedKeyboardKey)||!sources.Contains(selectedKeyboardKey))selectedKeyboardKey="";
   // Coordinates are relative to the fixed device tabs' body origin (~y117).
   var surface=new Canvas{Width=1246,Height=605};
   var viewer=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left};target.Children.Add(viewer);

   var modes=new StackPanel{Orientation=Orientation.Horizontal};
   var regular=new Border{Width=134,Height=24,Background=text,CornerRadius=new CornerRadius(2)};
   var regularLabel=Label("常规绑定",14,Brush("#151515"));regularLabel.Margin=new Thickness(0);regularLabel.HorizontalAlignment=HorizontalAlignment.Center;regularLabel.VerticalAlignment=VerticalAlignment.Center;regular.Child=regularLabel;modes.Children.Add(regular);
   var instant=KeyboardOutlineButton("瞬时动态切换",()=>{});instant.Width=134;instant.Height=24;instant.Margin=new Thickness(16,0,0,0);instant.BorderThickness=new Thickness(0);instant.IsEnabled=false;instant.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(instant,true);modes.Children.Add(instant);KeyboardPlace(surface,modes,35,32);
   var reset=KeyboardOutlineButton("重置",()=>{});reset.Width=134;reset.Height=32;reset.IsEnabled=false;reset.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(reset,true);KeyboardPlace(surface,reset,1071,103);
   var frame=new Border{Width=423,Height=418,BorderBrush=muted,BorderThickness=new Thickness(0.7),CornerRadius=new CornerRadius(3),Background=Brushes.Transparent};KeyboardPlace(surface,frame,775,151);
   var diagram=new Canvas{Width=576,Height=220};KeyboardPlace(surface,diagram,73,181);
   var buttons=new Dictionary<string,Button>(StringComparer.Ordinal);
   Action refreshEditor=()=>{
    foreach(var entry in buttons){bool selectedKey=entry.Key==selectedKeyboardKey;entry.Value.BorderBrush=selectedKey?accent:muted;entry.Value.Background=selectedKey?Brush("#29261C"):Brushes.Transparent;}
    frame.Child=KeyboardBindingEditor(state,sources,physical);
   };
   foreach(var key in shapes) {
    string source=key.Source;bool known=sources.Contains(source);
    var keyButton=KeyboardOutlineButton(key.Caption,async()=>{
     if(!sources.Contains(source)||!physical.Contains(source)||busy||closing)return;
     selectedKeyboardKey=source;refreshEditor();
     // One explicit user selection requests only that key's binding. Page creation,
     // draft edits and render-only callbacks never start a worker or send writes.
     if(!render&&GearOnline(state))await ReadOriginalKeyBinding(state,source);
    });
    keyButton.Width=key.Width;keyButton.Height=28;keyButton.FontSize=key.Caption.Length>4?8:9;keyButton.Padding=new Thickness(0);keyButton.Margin=new Thickness(0);keyButton.IsEnabled=known&&!busy&&!closing;keyButton.Tag=source;
    keyButton.ToolTip=known?key.Caption.Replace("\n"," / "):"此按键暂不可用";ToolTipService.SetShowOnDisabled(keyButton,true);buttons.Add(source,keyButton);KeyboardPlace(diagram,keyButton,key.X,key.Y);
   }
   var help=KeyboardOutlineButton("",()=>{});help.Width=20;help.Height=20;help.Padding=new Thickness(0);help.BorderThickness=new Thickness(0);help.IsEnabled=false;help.ToolTip="选择键盘上的按键";var helpIcon=new Grid{Width=20,Height=20};helpIcon.Children.Add(new System.Windows.Shapes.Ellipse{Width=18,Height=18,Stroke=accent,StrokeThickness=1});var helpText=Label("?",13,accent);helpText.Margin=new Thickness(0);helpText.HorizontalAlignment=HorizontalAlignment.Center;helpText.VerticalAlignment=VerticalAlignment.Center;helpIcon.Children.Add(helpText);help.Content=helpIcon;ToolTipService.SetShowOnDisabled(help,true);KeyboardPlace(surface,help,80,154);
   var game=new StackPanel();var gameTitle=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,16)};var gameMark=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 2,2 L 8,12 M 11,2 L 17,12 M 20,2 L 26,12"),Stroke=Brush("#D94357"),StrokeThickness=3,Width=32,Height=14,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center};gameTitle.Children.Add(gameMark);var gameHeading=Label("游戏模式开启时:",15,text,FontWeights.SemiBold);gameHeading.Margin=new Thickness(10,0,0,0);gameTitle.Children.Add(gameHeading);game.Children.Add(gameTitle);
   foreach(string combination in new[]{"Alt + Tab","Alt + F4"}) {
    var check=new CheckBox{Content="停用 "+combination,IsThreeState=true,IsChecked=null,IsEnabled=false,Foreground=muted,FontSize=14,Margin=new Thickness(29,0,0,29),ToolTip="状态未知 · 此功能暂不可用"};check.Template=KeyboardUnknownGameCheck();ToolTipService.SetShowOnDisabled(check,true);game.Children.Add(check);
   }
   KeyboardPlace(surface,game,74,463);refreshEditor();
  }

  UIElement KeyboardBindingEditor(Dictionary<string,object> state,HashSet<string> sources,HashSet<string> physical) {
   string selectedKey=selectedKeyboardKey;
   if(selectedKey.Length==0||!physical.Contains(selectedKey)||!sources.Contains(selectedKey)) {
    var prompt=Label("请选择单键并开始编辑按键设置。",14,text,FontWeights.SemiBold);prompt.Margin=new Thickness(16);prompt.HorizontalAlignment=HorizontalAlignment.Center;prompt.VerticalAlignment=VerticalAlignment.Center;return prompt;
   }
   var editor=new StackPanel{Margin=new Thickness(30,28,30,24)};editor.Children.Add(Label("按键 · "+GearFriendlyKey(selectedKey),18,text));
   var current=GearMap(GearMap(state,"snapshot"),"keyBinding");var targets=GearOptions(state,"keyTargets").Where(value=>!String.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).ToArray();string observedSource=OriginalStateText(current,"source","");string observedTarget=OriginalStateText(current,"target","");
   bool observed=GearOnline(state)&&observedSource==selectedKey&&observedTarget.Length>0&&targets.Contains(observedTarget,StringComparer.Ordinal);
   if(!observed)editor.Children.Add(Label("按键映射尚未读回。",13,muted));
   var output=GearNamedChoices(targets,observed?observedTarget:"");output.IsEnabled=observed&&!busy&&!closing;output.Margin=new Thickness(0,6,0,20);editor.Children.Add(FeatureField("功能",output));
   var apply=KeyboardOutlineButton("应用",async()=>{
    string target=GearSelectedName(output);
    if(render||busy||closing||selectedKeyboardKey!=selectedKey||!observed||!sources.Contains(selectedKey)||!physical.Contains(selectedKey)||target.Length==0||!targets.Contains(target,StringComparer.Ordinal)||!CanApplyGear(state,"keyBinding"))return;
    await SetGearSetting("keyboard",state,"keyBinding",new Dictionary<string,object>{{"source",selectedKey},{"target",target}});
   });
   apply.Width=112;apply.Height=34;apply.HorizontalAlignment=HorizontalAlignment.Right;apply.IsEnabled=observed&&CanApplyGear(state,"keyBinding");apply.ToolTip=apply.IsEnabled?null:"设置暂不可用";ToolTipService.SetShowOnDisabled(apply,true);editor.Children.Add(apply);
   // Choosing a target changes only the local draft. Explicit Apply owns the setter.
   output.SelectionChanged+=(sender,args)=>{apply.IsEnabled=observed&&targets.Contains(GearSelectedName(output),StringComparer.Ordinal)&&CanApplyGear(state,"keyBinding");};return editor;
  }

  void KeyboardPlace(Canvas parent,UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);parent.Children.Add(element);}

  Button KeyboardOutlineButton(string caption,Action action) {
   var button=Button("",action);button.Background=Brushes.Transparent;button.BorderBrush=muted;button.BorderThickness=new Thickness(0.7);button.Padding=new Thickness(8,2,8,2);button.Margin=new Thickness(0);button.HorizontalContentAlignment=HorizontalAlignment.Center;
   var title=Label(caption,12,text);title.Margin=new Thickness(0);title.TextAlignment=TextAlignment.Center;title.VerticalAlignment=VerticalAlignment.Center;title.SetBinding(TextBlock.FontSizeProperty,new System.Windows.Data.Binding("FontSize"){Source=button});title.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});button.Content=title;
   var template=new ControlTemplate(typeof(Button));var borderElement=new FrameworkElementFactory(typeof(Border));borderElement.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));
   foreach(var property in new[]{new{Target=Border.BackgroundProperty,Name="Background"},new{Target=Border.BorderBrushProperty,Name="BorderBrush"},new{Target=Border.BorderThicknessProperty,Name="BorderThickness"},new{Target=Border.PaddingProperty,Name="Padding"}})borderElement.SetBinding(property.Target,new System.Windows.Data.Binding(property.Name){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   var contentPresenter=new FrameworkElementFactory(typeof(ContentPresenter));contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);borderElement.AppendChild(contentPresenter);template.VisualTree=borderElement;
   var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,0.45));template.Triggers.Add(disabled);button.Template=template;return button;
  }

  ControlTemplate KeyboardUnknownGameCheck() {
   var template=new ControlTemplate(typeof(CheckBox));var row=new FrameworkElementFactory(typeof(StackPanel));row.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
   var box=new FrameworkElementFactory(typeof(Border));box.SetValue(Border.WidthProperty,20.0);box.SetValue(Border.HeightProperty,20.0);box.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));box.SetValue(Border.BorderBrushProperty,muted);box.SetValue(Border.BorderThicknessProperty,new Thickness(1));box.SetValue(Border.MarginProperty,new Thickness(0,0,12,0));
   var unknown=new FrameworkElementFactory(typeof(TextBlock));unknown.SetValue(TextBlock.TextProperty,"—");unknown.SetValue(TextBlock.FontSizeProperty,12.0);unknown.SetValue(TextBlock.ForegroundProperty,muted);unknown.SetValue(TextBlock.HorizontalAlignmentProperty,HorizontalAlignment.Center);unknown.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);box.AppendChild(unknown);row.AppendChild(box);
   var caption=new FrameworkElementFactory(typeof(ContentPresenter));caption.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(caption);template.VisualTree=row;return template;
  }
 }
}
