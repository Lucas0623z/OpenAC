// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored WPF geometry. No manufacturer artwork or UI source.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public sealed class KeyboardLightingObservation {
  public string DeviceId;
  public bool IdentityConfirmed,RecipeConfirmed,CanRequestStatic,StaticCurrent;
  public int? CurrentMode,Brightness;
  public int[] Color;
 }

 public static class KeyboardLightingUiFacts {
  internal static object Field(IDictionary<string,object> map,string name){object value;return map!=null&&map.TryGetValue(name,out value)?value:null;}
  internal static int? Number(IDictionary<string,object> map,string name,int min,int max){object value=Field(map,name);return value is int&&(int)value>=min&&(int)value<=max?(int?)value:null;}
  internal static bool Flag(IDictionary<string,object> map,string name,bool expected){object value=Field(map,name);return value is bool&&(bool)value==expected;}
  internal static bool Contains(IDictionary<string,object> map,string name,string value){var values=Field(map,name) as IEnumerable;return values!=null&&!(values is string)&&values.Cast<object>().Any(item=>item is string&&(string)item==value);}
  internal static int[] Rgb(object value){var values=value as IEnumerable;if(values==null||value is string)return null;var result=new List<int>();foreach(object item in values){if(!(item is int)||(int)item<0||(int)item>255||result.Count==3)return null;result.Add((int)item);}return result.Count==3?result.ToArray():null;}
  internal static bool Identity(string id,int pid){if(String.IsNullOrEmpty(id))return false;string suffix="-"+pid.ToString(CultureInfo.InvariantCulture);string baseId=id.EndsWith(suffix,StringComparison.Ordinal)?id.Substring(0,id.Length-suffix.Length):id;return baseId.Length==20&&baseId.StartsWith("hid-",StringComparison.Ordinal)&&baseId.Skip(4).All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}

  public static KeyboardLightingObservation Read(IDictionary<string,object> state) {
   var result=new KeyboardLightingObservation();var snapshot=Field(state,"snapshot") as IDictionary<string,object>;int? pid=Number(state,"pid",0,65535);
   result.DeviceId=Field(state,"deviceId") as string;
   result.IdentityConfirmed=pid.HasValue&&(pid==7586||pid==7588)&&Identity(result.DeviceId,pid.Value)&&Field(state,"kind") as string=="keyboard"&&Flag(state,"connected",true)&&Flag(state,"online",true)&&Flag(state,"readbackConfirmed",true)&&Number(snapshot,"modelPid",0,65535)==pid;
   if(!result.IdentityConfirmed||!Contains(state,"readCapabilities","lighting"))return result;
   var lighting=Field(snapshot,"lighting") as IDictionary<string,object>;if(lighting==null)return result;
   result.CurrentMode=Number(lighting,"currentMode",0,9);
   int? effect=Number(snapshot,"lightingEffectCode",0,9),profiles=Number(snapshot,"profileCount",1,255),brightness=Number(lighting,"brightness",0,100),speed=Number(lighting,"speed",0,255),flags=Number(lighting,"flags",0,17);
   var color=Rgb(Field(lighting,"color"));var secondary=Rgb(Field(lighting,"secondaryColor"));var background=Rgb(Field(lighting,"backgroundColor"));
   result.RecipeConfirmed=result.CurrentMode.HasValue&&effect==result.CurrentMode&&profiles.HasValue&&Number(lighting,"recipeMode",0,0)==0&&Number(lighting,"area",0,0)==0&&brightness.HasValue&&speed.HasValue&&flags.HasValue&&new[]{0,1,16,17}.Contains(flags.Value)&&color!=null&&secondary!=null&&background!=null;
   if(!result.RecipeConfirmed){result.CurrentMode=null;return result;}
   result.Brightness=brightness;result.Color=color;
   result.CanRequestStatic=Contains(state,"setCapabilities","staticLighting")&&Contains(state,"readCapabilities","lightingSoftwareMode")&&Contains(state,"readCapabilities","windowsLightingEnabled")&&Flag(snapshot,"lightingSoftwareMode",false)&&Flag(snapshot,"windowsLightingEnabled",false)&&Flag(snapshot,"uniformStaticLightingProtocolAvailable",true)&&Flag(snapshot,"uniformStaticLightingReady",true);
   // 25/0E is the saved static recipe. It is not the active parameters for any
   // dynamic mode, random color or another lighting owner.
   result.StaticCurrent=result.CurrentMode==0&&flags==0&&secondary.All(value=>value==0)&&background.All(value=>value==0)&&Flag(snapshot,"lightingSoftwareMode",false)&&Flag(snapshot,"windowsLightingEnabled",false)&&Contains(state,"readCapabilities","lightingSoftwareMode")&&Contains(state,"readCapabilities","windowsLightingEnabled");
   return result;
  }

  public static bool SameRecipe(KeyboardLightingObservation before,KeyboardLightingObservation current){return before!=null&&current!=null&&before.IdentityConfirmed&&current.IdentityConfirmed&&before.RecipeConfirmed&&current.RecipeConfirmed&&before.DeviceId==current.DeviceId&&before.CurrentMode==current.CurrentMode&&before.Brightness==current.Brightness&&before.Color!=null&&current.Color!=null&&before.Color.SequenceEqual(current.Color)&&before.CanRequestStatic==current.CanRequestStatic&&before.StaticCurrent==current.StaticCurrent;}
  public static bool SessionMatches(IDictionary<string,Dictionary<string,object>> states,Dictionary<string,object> state,KeyboardLightingObservation observed){Dictionary<string,object> active;return states!=null&&state!=null&&states.TryGetValue("keyboard",out active)&&Object.ReferenceEquals(active,state)&&SameRecipe(observed,Read(state));}
 }

 public sealed class HeadsetLightingObservation {
  public string DeviceId;
  public bool IdentityConfirmed,CanTogglePower,CanSetEffect,CanSetBrightness,CanSetColor,CanSetAura;
  public bool? Enabled,AuraEnabled;
  public int? Effect,Brightness;
  public int[] Color;
 }
 public static class HeadsetLightingUiFacts {
  public static HeadsetLightingObservation Read(IDictionary<string,object> state) {
   var result=new HeadsetLightingObservation();var snapshot=KeyboardLightingUiFacts.Field(state,"snapshot") as IDictionary<string,object>;result.DeviceId=KeyboardLightingUiFacts.Field(state,"deviceId") as string;
   result.IdentityConfirmed=KeyboardLightingUiFacts.Number(state,"pid",7489,7489)==7489&&result.DeviceId!=null&&result.DeviceId.Length==20&&KeyboardLightingUiFacts.Identity(result.DeviceId,7489)&&KeyboardLightingUiFacts.Field(state,"kind") as string=="headset"&&KeyboardLightingUiFacts.Flag(state,"connected",true)&&KeyboardLightingUiFacts.Flag(state,"online",true)&&KeyboardLightingUiFacts.Flag(state,"readbackConfirmed",true)&&KeyboardLightingUiFacts.Number(snapshot,"modelPid",7489,7489)==7489;
   if(!result.IdentityConfirmed)return result;
   object power=KeyboardLightingUiFacts.Field(snapshot,"lightingEnabled"),aura=KeyboardLightingUiFacts.Field(snapshot,"auraEnabled");
   if(power is bool&&KeyboardLightingUiFacts.Contains(state,"readCapabilities","lightingEnabled"))result.Enabled=(bool)power;
   if(aura is bool&&KeyboardLightingUiFacts.Contains(state,"readCapabilities","auraEnabled"))result.AuraEnabled=(bool)aura;
   if(KeyboardLightingUiFacts.Contains(state,"readCapabilities","lighting")){object mapping=KeyboardLightingUiFacts.Field(snapshot,"lightingModeMappingUnconfirmed");if(mapping==null||mapping is bool&&!(bool)mapping)result.Effect=KeyboardLightingUiFacts.Number(snapshot,"lightingEffectCode",1,5);result.Brightness=KeyboardLightingUiFacts.Number(snapshot,"brightnessRaw",0,100);result.Color=KeyboardLightingUiFacts.Rgb(KeyboardLightingUiFacts.Field(snapshot,"color"));}
   result.CanTogglePower=result.Enabled.HasValue&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","lightingEnabled");
   result.CanSetAura=result.Enabled==true&&result.AuraEnabled.HasValue&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","auraEnabled");
   bool complete=result.Enabled==true&&result.AuraEnabled==false&&result.Brightness.HasValue&&result.Color!=null;
   object mappingFlag=KeyboardLightingUiFacts.Field(snapshot,"lightingModeMappingUnconfirmed");bool observedZero=KeyboardLightingUiFacts.Contains(state,"readCapabilities","lighting")&&KeyboardLightingUiFacts.Number(snapshot,"lightingEffectCode",0,0)==0&&(mappingFlag==null||mappingFlag is bool);
   result.CanSetEffect=complete&&(result.Effect.HasValue||observedZero)&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","lightingEffect");
   bool recipe=complete&&result.Effect.HasValue;result.CanSetBrightness=recipe&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","brightness");result.CanSetColor=recipe&&result.Effect<=3&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","lightingColor");return result;
  }
  public static bool Same(HeadsetLightingObservation before,HeadsetLightingObservation after){return before!=null&&after!=null&&before.IdentityConfirmed&&after.IdentityConfirmed&&before.DeviceId==after.DeviceId&&before.Enabled==after.Enabled&&before.AuraEnabled==after.AuraEnabled&&before.Effect==after.Effect&&before.Brightness==after.Brightness&&(before.Color==null&&after.Color==null||before.Color!=null&&after.Color!=null&&before.Color.SequenceEqual(after.Color))&&before.CanTogglePower==after.CanTogglePower&&before.CanSetEffect==after.CanSetEffect&&before.CanSetBrightness==after.CanSetBrightness&&before.CanSetColor==after.CanSetColor&&before.CanSetAura==after.CanSetAura;}
  public static bool SessionMatches(IDictionary<string,Dictionary<string,object>> states,Dictionary<string,object> state,HeadsetLightingObservation observed){Dictionary<string,object> active;return states!=null&&state!=null&&states.TryGetValue("headset",out active)&&Object.ReferenceEquals(active,state)&&Same(observed,Read(state));}
 }

 public partial class MainWindow {
  static readonly string[] KeyboardLightingEffectTitles={"恒亮","呼吸","彩色循环","触发","彩虹","涟漪","星空","流沙","电流","雨滴"};
  static readonly string[] HeadsetLightingEffectTitles={"恒亮","呼吸","闪烁","彩色循环","彩虹"};

  bool KeyboardLightingCanApply(Dictionary<string,object> state,KeyboardLightingObservation observed) {
   return !render&&!closing&&!busy&&HardwareWritesEnabled&&observed!=null&&observed.CanRequestStatic&&KeyboardLightingUiFacts.SessionMatches(gearStates,state,observed)&&CanApplyGear(state,"staticLighting");
  }

  async Task ApplyKeyboardStaticFromUi(Dictionary<string,object> state,KeyboardLightingObservation observed,int[] color,int brightness) {
   if(!KeyboardLightingCanApply(state,observed)||color==null||color.Length!=3||color.Any(value=>value<0||value>255)||brightness<0||brightness>100)return;
   await SetGearSetting("keyboard",state,"staticLighting",new Dictionary<string,object>{{"color",color.ToArray()},{"brightness",brightness}});
  }

  void OriginalKeyboardLightingPage(StackPanel target,Dictionary<string,object> state) {
   var observed=KeyboardLightingUiFacts.Read(state);bool editable=KeyboardLightingCanApply(state,observed);bool valuesKnown=observed.StaticCurrent;
   // Body origin is y117 in the client layout. At narrower sizes the original
   // fixed parameter panel remains reachable without distorting its proportions.
   var surface=new Canvas{Width=1246,Height=605,Tag="KeyboardLighting.Surface"};
   var viewer=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left,Tag="KeyboardLighting.Viewport"};target.Children.Add(viewer);

   // Software lighting ownership is not a hardware on/off switch.
   var total=new CheckBox{Width=52,Height=20,IsThreeState=true,IsChecked=null,IsEnabled=false,Tag="KeyboardLighting.TotalPower",ToolTip="状态未知",Template=KeyboardLightingUnknownSwitch()};ToolTipService.SetShowOnDisabled(total,true);KeyboardLightingPlace(surface,total,35,30);
   var reset=KeyboardOutlineButton("重置",()=>{});reset.Width=133;reset.Height=33;reset.IsEnabled=false;reset.Tag="KeyboardLighting.Reset";reset.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(reset,true);KeyboardLightingPlace(surface,reset,1075,24);
   KeyboardLightingPlace(surface,KeyboardLightingHeading("基本效果",true),35,84);
   for(int index=0;index<KeyboardLightingEffectTitles.Length;index++) {
    int item=index;bool selected=index==0&&valuesKnown;var button=new Button{Width=132,Height=106,Padding=new Thickness(0),Margin=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=selected?accent:muted,IsEnabled=index==0&&editable,Tag="KeyboardLighting.Effect."+index,ToolTip=index==0?(editable?null:"设置暂不可用"):"此功能暂不可用",Template=KeyboardLightingEffectTemplate()};
    var tile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};tile.Children.Add(KeyboardLightingEffectIcon(index,selected?accent:muted));var label=Label(KeyboardLightingEffectTitles[index],15,selected?accent:muted);label.Margin=new Thickness(0,8,0,0);label.TextAlignment=TextAlignment.Center;tile.Children.Add(label);button.Content=tile;ToolTipService.SetShowOnDisabled(button,true);
    button.Click+=async(s,e)=>{if(item==0&&KeyboardLightingCanApply(state,observed))await ApplyKeyboardStaticFromUi(state,observed,observed.Color,observed.Brightness.Value);};KeyboardLightingPlace(surface,button,29+(index%5)*132,121+(index/5)*116);
   }
   surface.Children.Add(new Line{X1=809,Y1=113,X2=809,Y2=318,Stroke=muted,StrokeThickness=1,IsHitTestVisible=false});
   KeyboardLightingPlace(surface,KeyboardLightingHeading("高级效果",false),35,410);
   KeyboardLightingPlace(surface,KeyboardLightingCreatorIcon(),38,450);
   var creator=KeyboardOutlineButton("Aura Creator",()=>{});creator.Width=220;creator.Height=33;creator.IsEnabled=false;creator.Tag="KeyboardLighting.Creator";creator.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(creator,true);KeyboardLightingPlace(surface,creator,135,454);
   var note=Label("用 Aura Creator 进行自定义灯效并在兼容设备之间同步。",12,text);note.Margin=new Thickness(0);KeyboardLightingPlace(surface,note,135,498);

   var parameters=new Canvas{Width=377,Height=479};var frame=new Border{Width=377,Height=479,BorderBrush=muted,BorderThickness=new Thickness(0.7),CornerRadius=new CornerRadius(4),Background=Brushes.Transparent,Child=parameters,Tag="KeyboardLighting.Parameters"};KeyboardLightingPlace(surface,frame,831,80);
   var currentTitle=Label(valuesKnown?"恒亮":"—",15,text,FontWeights.SemiBold);currentTitle.Margin=new Thickness(0);currentTitle.Tag="KeyboardLighting.CurrentTitle";KeyboardLightingPlace(parameters,currentTitle,24,24);
   parameters.Children.Add(new Line{X1=24,Y1=60,X2=351,Y2=60,Stroke=border,StrokeThickness=1,IsHitTestVisible=false});
   var colorTitle=Label("颜色",15,text);colorTitle.Margin=new Thickness(0);KeyboardLightingPlace(parameters,colorTitle,24,74);
   var swatch=KeyboardOutlineButton(valuesKnown?"":"—",()=>OpenKeyboardLightingColorEditor(state,observed));swatch.Width=33;swatch.Height=33;swatch.Padding=new Thickness(4);swatch.Opacity=1;swatch.IsEnabled=valuesKnown&&editable;swatch.Tag="KeyboardLighting.Color";swatch.ToolTip=valuesKnown?"#"+String.Join("",observed.Color.Select(value=>value.ToString("X2",CultureInfo.InvariantCulture))):"状态未知";ToolTipService.SetShowOnDisabled(swatch,true);
   if(valuesKnown)swatch.Content=new Border{Width=23,Height=23,Background=new SolidColorBrush(Color.FromRgb((byte)observed.Color[0],(byte)observed.Color[1],(byte)observed.Color[2]))};KeyboardLightingPlace(parameters,swatch,24,106);
   var brightnessTitle=Label("LED 亮度",15,text);brightnessTitle.Margin=new Thickness(0);KeyboardLightingPlace(parameters,brightnessTitle,24,165);
   var infoIcon=new Grid{Width=19,Height=19,ToolTip="LED 亮度"};infoIcon.Children.Add(new Ellipse{Stroke=accent,StrokeThickness=0.7});var infoText=Label("i",12,accent);infoText.Margin=new Thickness(0);infoText.HorizontalAlignment=HorizontalAlignment.Center;infoText.VerticalAlignment=VerticalAlignment.Center;infoIcon.Children.Add(infoText);KeyboardLightingPlace(parameters,infoIcon,98,164);
   for(int mark=0;mark<5;mark++){var caption=Label((mark*25).ToString(CultureInfo.InvariantCulture)+"%",10,muted);caption.Margin=new Thickness(0);caption.Width=44;caption.TextAlignment=mark==0?TextAlignment.Left:mark==4?TextAlignment.Right:TextAlignment.Center;KeyboardLightingPlace(parameters,caption,24+mark*81.5-(mark==0?0:mark==4?44:22),203);var dot=new Ellipse{Width=5,Height=5,Fill=muted};KeyboardLightingPlace(parameters,dot,24+mark*81.5-2.5,224);}
   var slider=new Slider{Width=326,Height=24,Minimum=0,Maximum=100,Value=valuesKnown?observed.Brightness.Value:0,IsEnabled=valuesKnown&&editable,TickFrequency=1,IsSnapToTickEnabled=true,Tag=valuesKnown,ToolTip=valuesKnown?observed.Brightness.Value.ToString(CultureInfo.InvariantCulture)+"%":"状态未知",Template=KeyboardLightingSliderTemplate()};slider.Name="KeyboardLightingBrightness";KeyboardLightingPlace(parameters,slider,24,227);
   slider.ValueChanged+=(s,e)=>{if(valuesKnown)slider.ToolTip=((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture)+"%";};
   Func<Task> applyBrightness=async()=>{if(valuesKnown&&slider.IsEnabled&&KeyboardLightingCanApply(state,observed)&&(int)Math.Round(slider.Value)!=observed.Brightness.Value)await ApplyKeyboardStaticFromUi(state,observed,observed.Color,(int)Math.Round(slider.Value));};
   slider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler(async(s,e)=>await applyBrightness()));slider.PreviewMouseLeftButtonUp+=async(s,e)=>await applyBrightness();slider.KeyUp+=async(s,e)=>{if(e.Key==Key.Left||e.Key==Key.Right||e.Key==Key.Up||e.Key==Key.Down||e.Key==Key.PageUp||e.Key==Key.PageDown||e.Key==Key.Home||e.Key==Key.End)await applyBrightness();};
  }

  bool HeadsetLightingCanApply(Dictionary<string,object> state,HeadsetLightingObservation observed,string setting) {
   if(render||closing||busy||!HardwareWritesEnabled||observed==null||!HeadsetLightingUiFacts.SessionMatches(gearStates,state,observed)||!CanApplyGear(state,setting))return false;
   return setting=="lightingEnabled"?observed.CanTogglePower:setting=="lightingEffect"?observed.CanSetEffect:setting=="brightness"?observed.CanSetBrightness:setting=="lightingColor"?observed.CanSetColor:setting=="auraEnabled"&&observed.CanSetAura;
  }
  async Task ApplyHeadsetLightingFromUi(Dictionary<string,object> state,HeadsetLightingObservation observed,string setting,object value){if(!HeadsetLightingCanApply(state,observed,setting))return;await SetGearSetting("headset",state,setting,value);}

  void OriginalHeadsetLightingPage(StackPanel target,Dictionary<string,object> state) {
   var observed=HeadsetLightingUiFacts.Read(state);bool owned=observed.AuraEnabled==false;bool displayedEffect=owned&&observed.Effect.HasValue;bool hasColor=displayedEffect&&observed.Effect<=3;bool colorKnown=hasColor&&observed.Color!=null;bool brightnessKnown=owned&&observed.Brightness.HasValue;bool off=observed.Enabled==false;
   var surface=new Canvas{Width=1246,Height=605,Tag="HeadsetLighting.Surface"};var viewer=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left,Tag="HeadsetLighting.Viewport"};target.Children.Add(viewer);
   var total=new CheckBox{Width=52,Height=20,IsThreeState=!observed.Enabled.HasValue,IsChecked=observed.Enabled,IsEnabled=HeadsetLightingCanApply(state,observed,"lightingEnabled"),Template=GearLightingPowerSwitch(observed.Enabled),Tag="HeadsetLighting.TotalPower",ToolTip=observed.Enabled.HasValue?null:"状态未知"};ToolTipService.SetShowOnDisabled(total,true);KeyboardLightingPlace(surface,total,35,30);
   bool undo=false;RoutedEventHandler powerChanged=async(s,e)=>{if(undo)return;if(!total.IsChecked.HasValue||!HeadsetLightingCanApply(state,observed,"lightingEnabled")){undo=true;total.IsChecked=observed.Enabled;undo=false;return;}await ApplyHeadsetLightingFromUi(state,observed,"lightingEnabled",total.IsChecked.Value);};total.Checked+=powerChanged;total.Unchecked+=powerChanged;
   var reset=KeyboardOutlineButton("重置",()=>{});reset.Width=133;reset.Height=33;reset.IsEnabled=false;reset.Tag="HeadsetLighting.Reset";reset.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(reset,true);KeyboardLightingPlace(surface,reset,1075,24);
   var basicTitle=KeyboardLightingHeading("基本效果",true);basicTitle.Opacity=off?0.45:1;KeyboardLightingPlace(surface,basicTitle,35,84);
   for(int index=0;index<HeadsetLightingEffectTitles.Length;index++){int effect=index+1;bool selected=!off&&displayedEffect&&observed.Effect==effect;var button=new Button{Width=132,Height=106,Padding=new Thickness(0),Margin=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=selected?accent:muted,IsEnabled=HeadsetLightingCanApply(state,observed,"lightingEffect"),Opacity=off?0.45:1,Tag="HeadsetLighting.Effect."+effect,ToolTip="设置暂不可用",Template=KeyboardLightingEffectTemplate()};var tile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};tile.Children.Add(effect==3?HeadsetLightingBlinkIcon(selected?accent:muted):KeyboardLightingEffectIcon(effect==4?2:effect==5?4:index,selected?accent:muted));var label=Label(HeadsetLightingEffectTitles[index],15,selected?accent:muted);label.Margin=new Thickness(0,8,0,0);label.TextAlignment=TextAlignment.Center;tile.Children.Add(label);button.Content=tile;ToolTipService.SetShowOnDisabled(button,true);button.Click+=async(s,e)=>await ApplyHeadsetLightingFromUi(state,observed,"lightingEffect",effect);KeyboardLightingPlace(surface,button,29+index*132,121);}
   var auraButton=new Button{Width=132,Height=106,Padding=new Thickness(0),Margin=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),IsEnabled=HeadsetLightingCanApply(state,observed,"auraEnabled"),Opacity=off?0.45:1,Tag="HeadsetLighting.Aura",ToolTip=observed.AuraEnabled.HasValue?"设置暂不可用":"状态未知",Template=KeyboardLightingEffectTemplate()};var auraTile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};auraTile.Children.Add(HeadsetLightingAuraIcon(observed.AuraEnabled==true?accent:muted));var auraName=Label("Aura Sync",15,observed.AuraEnabled==true?accent:muted);auraName.Margin=new Thickness(0,8,0,0);auraName.TextAlignment=TextAlignment.Center;auraTile.Children.Add(auraName);auraButton.Content=auraTile;ToolTipService.SetShowOnDisabled(auraButton,true);auraButton.Click+=async(s,e)=>{if(observed.AuraEnabled.HasValue)await ApplyHeadsetLightingFromUi(state,observed,"auraEnabled",!observed.AuraEnabled.Value);};KeyboardLightingPlace(surface,auraButton,29,240);
   KeyboardLightingPlace(surface,KeyboardLightingHeading("高级效果",false),35,380);KeyboardLightingPlace(surface,KeyboardLightingCreatorIcon(),38,420);var creator=KeyboardOutlineButton("Aura Creator",()=>{});creator.Width=133;creator.Height=33;creator.IsEnabled=false;creator.Tag="HeadsetLighting.Creator";creator.ToolTip="此功能暂不可用";ToolTipService.SetShowOnDisabled(creator,true);KeyboardLightingPlace(surface,creator,135,424);var note=Label("用 Aura Creator 进行自定义灯效并在兼容设备之间同步。",12,text);note.Margin=new Thickness(0);KeyboardLightingPlace(surface,note,135,468);
   var parameters=new Canvas{Width=377,Height=479};var frame=new Border{Width=377,Height=479,BorderBrush=muted,BorderThickness=new Thickness(0.7),CornerRadius=new CornerRadius(4),Background=Brushes.Transparent,Child=parameters,Opacity=off?0.45:1,Tag="HeadsetLighting.Parameters"};KeyboardLightingPlace(surface,frame,831,80);var currentTitle=Label(observed.AuraEnabled==true?"Aura Sync":displayedEffect?HeadsetLightingEffectTitles[observed.Effect.Value-1]:"—",15,text,FontWeights.SemiBold);currentTitle.Margin=new Thickness(0);currentTitle.Tag="HeadsetLighting.CurrentTitle";KeyboardLightingPlace(parameters,currentTitle,24,24);
   if(hasColor){var colorTitle=Label("颜色",15,text);colorTitle.Margin=new Thickness(0);KeyboardLightingPlace(parameters,colorTitle,24,74);var swatch=KeyboardOutlineButton(colorKnown?"":"—",()=>OpenHeadsetLightingColorEditor(state,observed));swatch.Width=33;swatch.Height=33;swatch.Padding=new Thickness(4);swatch.Opacity=1;swatch.IsEnabled=colorKnown&&HeadsetLightingCanApply(state,observed,"lightingColor");swatch.Tag="HeadsetLighting.Color";swatch.ToolTip=colorKnown?"#"+String.Join("",observed.Color.Select(value=>value.ToString("X2",CultureInfo.InvariantCulture))):"状态未知";if(colorKnown)swatch.Content=new Border{Width=23,Height=23,Background=new SolidColorBrush(Color.FromRgb((byte)observed.Color[0],(byte)observed.Color[1],(byte)observed.Color[2]))};ToolTipService.SetShowOnDisabled(swatch,true);KeyboardLightingPlace(parameters,swatch,24,106);}
   double ledTop=hasColor?165:78;var ledTitle=Label("LED 亮度",15,text);ledTitle.Margin=new Thickness(0);KeyboardLightingPlace(parameters,ledTitle,24,ledTop);var infoIcon=new Grid{Width=19,Height=19,ToolTip="LED 亮度"};infoIcon.Children.Add(new Ellipse{Stroke=accent,StrokeThickness=0.7});var infoText=Label("i",12,accent);infoText.Margin=new Thickness(0);infoText.HorizontalAlignment=HorizontalAlignment.Center;infoText.VerticalAlignment=VerticalAlignment.Center;infoIcon.Children.Add(infoText);KeyboardLightingPlace(parameters,infoIcon,98,ledTop-1);var percent=Label(brightnessKnown?observed.Brightness.Value.ToString(CultureInfo.InvariantCulture)+"%":"—",15,muted);percent.Margin=new Thickness(0);percent.Width=60;percent.TextAlignment=TextAlignment.Right;percent.Tag="HeadsetLighting.BrightnessValue";KeyboardLightingPlace(parameters,percent,291,ledTop);
   var slider=new Slider{Width=326,Height=24,Minimum=0,Maximum=100,Value=brightnessKnown?observed.Brightness.Value:0,IsEnabled=brightnessKnown&&HeadsetLightingCanApply(state,observed,"brightness"),TickFrequency=5,IsSnapToTickEnabled=true,Tag=brightnessKnown,ToolTip=brightnessKnown?observed.Brightness.Value.ToString(CultureInfo.InvariantCulture)+"%":"状态未知",Template=KeyboardLightingSliderTemplate()};slider.Name="HeadsetLightingBrightness";KeyboardLightingPlace(parameters,slider,24,ledTop+32);slider.ValueChanged+=(s,e)=>{if(brightnessKnown){percent.Text=((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture)+"%";slider.ToolTip=percent.Text;}};Func<Task> applyBrightness=async()=>{int value=(int)Math.Round(slider.Value);if(slider.IsEnabled&&value%5==0&&value!=observed.Brightness.Value)await ApplyHeadsetLightingFromUi(state,observed,"brightness",value);};slider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler(async(s,e)=>await applyBrightness()));slider.PreviewMouseLeftButtonUp+=async(s,e)=>await applyBrightness();slider.KeyUp+=async(s,e)=>{if(e.Key==Key.Left||e.Key==Key.Right||e.Key==Key.Up||e.Key==Key.Down||e.Key==Key.PageUp||e.Key==Key.PageDown||e.Key==Key.Home||e.Key==Key.End)await applyBrightness();};
  }

  ControlTemplate GearLightingPowerSwitch(bool? value){if(!value.HasValue)return KeyboardLightingUnknownSwitch();var template=new ControlTemplate(typeof(CheckBox));var shell=new FrameworkElementFactory(typeof(Border));shell.SetValue(Border.BackgroundProperty,value.Value?text:Brushes.Transparent);shell.SetValue(Border.BorderBrushProperty,text);shell.SetValue(Border.BorderThicknessProperty,new Thickness(0.7));shell.SetValue(Border.CornerRadiusProperty,new CornerRadius(2));var grid=new FrameworkElementFactory(typeof(Grid));var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetValue(TextBlock.TextProperty,value.Value?"ON":"OFF");caption.SetValue(TextBlock.ForegroundProperty,value.Value?bg:text);caption.SetValue(TextBlock.FontSizeProperty,10.0);caption.SetValue(TextBlock.MarginProperty,value.Value?new Thickness(5,0,18,0):new Thickness(20,0,3,0));caption.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);grid.AppendChild(caption);var thumb=new FrameworkElementFactory(typeof(Border));thumb.SetValue(Border.WidthProperty,13.0);thumb.SetValue(Border.HeightProperty,13.0);thumb.SetValue(Border.BackgroundProperty,value.Value?bg:text);thumb.SetValue(Border.MarginProperty,value.Value?new Thickness(0,0,3,0):new Thickness(3,0,0,0));thumb.SetValue(Border.HorizontalAlignmentProperty,value.Value?HorizontalAlignment.Right:HorizontalAlignment.Left);grid.AppendChild(thumb);shell.AppendChild(grid);template.VisualTree=shell;return template;}
  Canvas HeadsetLightingBlinkIcon(Brush ink){var icon=new Canvas{Width=64,Height=64,IsHitTestVisible=false};icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(9,32),new Point(30,12),new Point(41,23),new Point(31,32),new Point(41,41),new Point(30,52)}),Stroke=ink,StrokeThickness=5,Fill=Brushes.Transparent});icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(35,32),new Point(48,19),new Point(61,32),new Point(48,45)}),Fill=ink});return icon;}
  Canvas HeadsetLightingAuraIcon(Brush ink){var icon=new Canvas{Width=64,Height=64,IsHitTestVisible=false};icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(30,8),new Point(9,47),new Point(26,47),new Point(40,22)}),Stroke=ink,StrokeThickness=4,Fill=Brushes.Transparent});icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(42,26),new Point(30,48),new Point(45,48),new Point(53,35)}),Fill=ink});return icon;}

  void KeyboardLightingPlace(Canvas surface,UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);surface.Children.Add(element);}
  UIElement KeyboardLightingHeading(string title,bool withMark){var row=new StackPanel{Orientation=Orientation.Horizontal};if(withMark){var icon=new Canvas{Width=36,Height=18};for(int index=0;index<3;index++)icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(index*8+2,3),new Point(index*8+7,3),new Point(index*8+15,15),new Point(index*8+10,15)}),Fill=Brush("#D94357")});row.Children.Add(icon);}var label=Label(title,16,text,FontWeights.SemiBold);label.Margin=new Thickness(withMark?4:0,0,0,0);row.Children.Add(label);return row;}

  ControlTemplate KeyboardLightingUnknownSwitch(){var template=new ControlTemplate(typeof(CheckBox));var shell=new FrameworkElementFactory(typeof(Border));shell.SetValue(Border.BackgroundProperty,muted);shell.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));var grid=new FrameworkElementFactory(typeof(Grid));var caption=new FrameworkElementFactory(typeof(TextBlock));caption.SetValue(TextBlock.TextProperty,"—");caption.SetValue(TextBlock.ForegroundProperty,bg);caption.SetValue(TextBlock.FontSizeProperty,10.0);caption.SetValue(TextBlock.MarginProperty,new Thickness(7,1,18,0));caption.SetValue(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center);grid.AppendChild(caption);var unknown=new FrameworkElementFactory(typeof(Border));unknown.SetValue(Border.WidthProperty,13.0);unknown.SetValue(Border.HeightProperty,13.0);unknown.SetValue(Border.BackgroundProperty,Brush("#585858"));unknown.SetValue(Border.MarginProperty,new Thickness(0,0,4,0));unknown.SetValue(Border.HorizontalAlignmentProperty,HorizontalAlignment.Right);grid.AppendChild(unknown);shell.AppendChild(grid);template.VisualTree=shell;return template;}
  ControlTemplate KeyboardLightingEffectTemplate(){var template=new ControlTemplate(typeof(Button));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(ContentPresenter.VerticalAlignmentProperty,VerticalAlignment.Top);template.VisualTree=content;return template;}
  ControlTemplate KeyboardLightingSliderTemplate(){var template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type Slider}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Grid><Border Height='3' Background='#515151' VerticalAlignment='Center'/><Track x:Name='PART_Track' Height='24' VerticalAlignment='Center'><Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='3' Background='#D7BF6C' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='10' Height='17'><Thumb.RenderTransform><TranslateTransform Y='7'/></Thumb.RenderTransform><Thumb.Template><ControlTemplate TargetType='Thumb'><Polygon Fill='#F1F1F1' Points='0,6 5,0 10,6 10,17 0,17'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='3' Background='#515151' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate>");var unknown=new Trigger{Property=FrameworkElement.TagProperty,Value=false};unknown.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed,"PART_Track"));template.Triggers.Add(unknown);return template;}

  Canvas KeyboardLightingEffectIcon(int effect,Brush ink) {
   var icon=new Canvas{Width=64,Height=64,IsHitTestVisible=false};
   Action<double,double,double> ring=(x,y,size)=>icon.Children.Add(new Ellipse{Width=size,Height=size,Stroke=ink,StrokeThickness=4,Margin=new Thickness(x,y,0,0)});
   Action<string> path=data=>icon.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(data),Stroke=ink,StrokeThickness=5,StrokeStartLineCap=PenLineCap.Square,StrokeEndLineCap=PenLineCap.Square});
   switch(effect) {
    case 0:ring(5,5,54);icon.Children.Add(new Ellipse{Width=13,Height=13,Fill=ink,Margin=new Thickness(25.5,25.5,0,0)});break;
    case 1:path("M 5,36 L 17,36 28,16 38,46 47,36 59,36");break;
    case 2:icon.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M 31,9 A 23,23 0 0 0 9,34 M 10,38 A 23,23 0 0 0 39,54 M 44,51 A 23,23 0 0 0 55,23"),Stroke=ink,StrokeThickness=6});icon.Children.Add(new Ellipse{Width=12,Height=12,Fill=ink,Margin=new Thickness(26,26,0,0)});break;
    case 3:var first=new Ellipse{Width=51,Height=30,Stroke=ink,StrokeThickness=4,RenderTransform=new RotateTransform(43,25.5,15),Margin=new Thickness(6,17,0,0)};var second=new Ellipse{Width=51,Height=30,Stroke=ink,StrokeThickness=4,RenderTransform=new RotateTransform(-43,25.5,15),Margin=new Thickness(6,17,0,0)};icon.Children.Add(first);icon.Children.Add(second);icon.Children.Add(new Ellipse{Width=8,Height=8,Fill=ink,Margin=new Thickness(28,28,0,0)});break;
    case 4:for(int arc=0;arc<4;arc++){int radius=25-arc*6;path(String.Format(CultureInfo.InvariantCulture,"M {0},44 A {1},{1} 0 0 1 {2},44",32-radius,radius,32+radius));}break;
    case 5:ring(10,10,44);ring(17,17,30);ring(24,24,16);icon.Children.Add(new Ellipse{Width=4,Height=4,Fill=ink,Margin=new Thickness(30,30,0,0)});break;
    case 6:for(int point=0;point<4;point++){double angle=point*Math.PI/2;double cx=32+20*Math.Cos(angle),cy=32+20*Math.Sin(angle);icon.Children.Add(new Polygon{Fill=ink,Points=new PointCollection(new[]{new Point(cx,cy-6),new Point(cx+6,cy),new Point(cx,cy+6),new Point(cx-6,cy)})});}icon.Children.Add(new Polygon{Fill=ink,Points=new PointCollection(new[]{new Point(32,21),new Point(43,32),new Point(32,43),new Point(21,32)})});break;
    case 7:for(int wave=0;wave<3;wave++)path(String.Format(CultureInfo.InvariantCulture,"M 8,{0} C 18,{0} 22,{1} 31,{1} C 43,{1} 43,{0} 56,{0}",18+wave*11,28+wave*11));break;
    case 8:path("M 5,43 L 15,43 23,13 30,48 37,28 44,47 49,39 59,39");break;
    case 9:for(int streak=0;streak<4;streak++){double x=15+streak*10;icon.Children.Add(new Line{X1=x,Y1=47-streak%2*6,X2=x+18,Y2=17-streak%2*6,Stroke=ink,StrokeThickness=5});}break;
   }
   return icon;
  }

  Canvas KeyboardLightingCreatorIcon(){var icon=new Canvas{Width=64,Height=64,IsHitTestVisible=false};icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(32,1),new Point(62,61),new Point(2,61)}),Stroke=Brush("#3D4149"),StrokeThickness=5,Fill=Brushes.Transparent});icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(32,9),new Point(32,40),new Point(10,57)}),Fill=new LinearGradientBrush(Colors.Cyan,Colors.Magenta,90)});icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(32,9),new Point(55,57),new Point(32,40)}),Fill=new LinearGradientBrush(Colors.Cyan,Colors.Yellow,90)});icon.Children.Add(new Polygon{Points=new PointCollection(new[]{new Point(10,57),new Point(32,40),new Point(55,57)}),Fill=new LinearGradientBrush(Colors.Magenta,Colors.Yellow,0)});return icon;}

  void OpenKeyboardLightingColorEditor(Dictionary<string,object> state,KeyboardLightingObservation observed) {
   if(!observed.StaticCurrent||!KeyboardLightingCanApply(state,observed))return;
   OpenGearLightingRgbEditor(observed.Color,chosen=>ApplyKeyboardLightingColor(state,observed,chosen));
  }
  void OpenHeadsetLightingColorEditor(Dictionary<string,object> state,HeadsetLightingObservation observed){if(!HeadsetLightingCanApply(state,observed,"lightingColor"))return;OpenGearLightingRgbEditor(observed.Color,chosen=>ApplyHeadsetLightingColor(state,observed,chosen));}
  void OpenGearLightingRgbEditor(int[] observedColor,Action<int[]> acceptColor) {
   if(render||closing||busy||!HardwareWritesEnabled||observedColor==null||observedColor.Length!=3)return;
   var editor=new Window{Title="颜色",Owner=this,Width=360,Height=252,MinWidth=360,MinHeight=252,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=bg,Foreground=text};var body=new StackPanel{Margin=new Thickness(22)};var values=new List<TextBox>();string[] channels={"R","G","B"};
   for(int channel=0;channel<3;channel++){var row=new Grid{Margin=new Thickness(0,0,0,12)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());var title=Label(channels[channel],14,text);title.Margin=new Thickness(0);title.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(title);var input=new TextBox{Text=observedColor[channel].ToString(CultureInfo.InvariantCulture),MaxLength=3,Foreground=text,Background=panel,BorderBrush=border,Padding=new Thickness(8,4,8,4),FontSize=14};Grid.SetColumn(input,1);row.Children.Add(input);values.Add(input);body.Children.Add(row);}
   int[] chosen=null;var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var cancel=KeyboardOutlineButton("取消",()=>editor.DialogResult=false);cancel.Width=84;cancel.Height=32;var accept=KeyboardOutlineButton("确定",()=>{var rgb=new int[3];for(int index=0;index<3;index++){if(!Int32.TryParse(values[index].Text,NumberStyles.None,CultureInfo.InvariantCulture,out rgb[index])||rgb[index]<0||rgb[index]>255){values[index].BorderBrush=accent;values[index].Focus();return;}}chosen=rgb;editor.DialogResult=true;});accept.Width=84;accept.Height=32;accept.Margin=new Thickness(12,0,0,0);buttons.Children.Add(cancel);buttons.Children.Add(accept);body.Children.Add(buttons);editor.Content=body;
   if(editor.ShowDialog()==true&&chosen!=null&&!closing)acceptColor(chosen);
  }
  async void ApplyKeyboardLightingColor(Dictionary<string,object> state,KeyboardLightingObservation observed,int[] chosen){await ApplyKeyboardStaticFromUi(state,observed,chosen,observed.Brightness.Value);}
  async void ApplyHeadsetLightingColor(Dictionary<string,object> state,HeadsetLightingObservation observed,int[] chosen){await ApplyHeadsetLightingFromUi(state,observed,"lightingColor",chosen);}
 }
}
