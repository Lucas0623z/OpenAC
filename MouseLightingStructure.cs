// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored WPF geometry and local drafts; no vendor artwork.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RogControl {
 public sealed class MouseLightingObservation {
  public string DeviceId;
  public bool IdentityConfirmed,RecipeConfirmed,CanRequestStatic,CanRequestEffects,StaticCurrent;
  public bool? SoftwareOwned,WindowsOwned,Random;
  public int? CurrentMode,Brightness,Direction,Speed;
  public int[] Color;
  public int[] WritableEffects=new int[0];
 }

 // Only user-editable data is persisted. Binding references never enter a preview snapshot.
 public sealed class MouseLightingDraft {
  public string DeviceId;
  public int? SelectedEffect,Brightness;
  public int[] Color;
  public bool Dirty;
 }

 public static class MouseLightingUiFacts {
  public static bool KnownEffect(int effect){return effect==0||effect==1||effect==2||effect==4||effect==6;}
  public static bool HasColor(int effect){return effect==0||effect==1||effect==4;}
  public static string EffectName(int effect){return effect==0?"恒亮":effect==1?"呼吸":effect==2?"彩色循环":effect==4?"触发":effect==6?"电池模式":"—";}
  static int[] EffectOptions(IDictionary<string,object> state) {
   var options=KeyboardLightingUiFacts.Field(state,"settingOptions") as IDictionary<string,object>;var values=KeyboardLightingUiFacts.Field(options,"mouseLightingEffects") as IEnumerable;if(values==null||values is string)return new int[0];var result=new List<int>();
   foreach(object value in values){var item=value as IDictionary<string,object>;int? mode=KeyboardLightingUiFacts.Number(item,"value",0,6);if(!mode.HasValue||!KnownEffect(mode.Value)||result.Contains(mode.Value)||!KeyboardLightingUiFacts.Flag(item,"color",HasColor(mode.Value))||!KeyboardLightingUiFacts.Flag(item,"brightness",true)||!KeyboardLightingUiFacts.Flag(item,"speed",false)||!KeyboardLightingUiFacts.Flag(item,"direction",false)||!KeyboardLightingUiFacts.Flag(item,"random",false))return new int[0];result.Add(mode.Value);}
   return result.Count==5&&result.OrderBy(mode=>mode).SequenceEqual(new[]{0,1,2,4,6})?result.ToArray():new int[0];
  }
  public static MouseLightingObservation Read(IDictionary<string,object> state) {
   var result=new MouseLightingObservation();var snapshot=KeyboardLightingUiFacts.Field(state,"snapshot") as IDictionary<string,object>;
   int? pid=KeyboardLightingUiFacts.Number(state,"pid",0,65535);result.DeviceId=KeyboardLightingUiFacts.Field(state,"deviceId") as string;
   result.IdentityConfirmed=pid.HasValue&&(pid==7546||pid==7548)&&KeyboardLightingUiFacts.Identity(result.DeviceId,pid.Value)&&KeyboardLightingUiFacts.Field(state,"kind") as string=="mouse"&&KeyboardLightingUiFacts.Flag(state,"connected",true)&&KeyboardLightingUiFacts.Flag(state,"online",true)&&KeyboardLightingUiFacts.Flag(state,"readbackConfirmed",true)&&KeyboardLightingUiFacts.Number(snapshot,"modelPid",0,65535)==pid;
   if(!result.IdentityConfirmed)return result;
   if(KeyboardLightingUiFacts.Contains(state,"readCapabilities","lightingSoftwareMode")&&KeyboardLightingUiFacts.Field(snapshot,"lightingSoftwareMode") is bool)result.SoftwareOwned=(bool)snapshot["lightingSoftwareMode"];
   if(KeyboardLightingUiFacts.Contains(state,"readCapabilities","windowsLightingEnabled")&&KeyboardLightingUiFacts.Field(snapshot,"windowsLightingEnabled") is bool)result.WindowsOwned=(bool)snapshot["windowsLightingEnabled"];
   if(!KeyboardLightingUiFacts.Contains(state,"readCapabilities","lighting"))return result;
   var lighting=KeyboardLightingUiFacts.Field(snapshot,"lighting") as IDictionary<string,object>;if(lighting==null)return result;
   int? mode=KeyboardLightingUiFacts.Number(lighting,"currentMode",0,240),brightness=KeyboardLightingUiFacts.Number(lighting,"brightness",0,100),direction=KeyboardLightingUiFacts.Number(lighting,"direction",0,1),speed=KeyboardLightingUiFacts.Number(lighting,"speed",0,255);
   int[] color=KeyboardLightingUiFacts.Rgb(KeyboardLightingUiFacts.Field(lighting,"color"));object random=KeyboardLightingUiFacts.Field(lighting,"random");
   result.RecipeConfirmed=mode.HasValue&&new[]{0,1,2,3,4,5,6,240}.Contains(mode.Value)&&KeyboardLightingUiFacts.Number(lighting,"area",0,0)==0&&brightness.HasValue&&direction.HasValue&&speed.HasValue&&color!=null&&random is bool;
   if(!result.RecipeConfirmed)return result;
   result.CurrentMode=mode;result.Brightness=brightness;result.Direction=direction;result.Speed=speed;result.Color=color;result.Random=(bool)random;
   result.StaticCurrent=mode==0&&result.Random==false&&result.SoftwareOwned==false&&result.WindowsOwned==false;
   result.CanRequestStatic=result.SoftwareOwned==false&&result.WindowsOwned==false&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","staticLighting")&&KeyboardLightingUiFacts.Flag(snapshot,"uniformStaticLightingProtocolAvailable",true)&&KeyboardLightingUiFacts.Flag(snapshot,"uniformStaticLightingReady",true);
   var effects=EffectOptions(state);result.CanRequestEffects=result.SoftwareOwned==false&&result.WindowsOwned==false&&KeyboardLightingUiFacts.Contains(state,"setCapabilities","mouseLighting")&&KeyboardLightingUiFacts.Flag(snapshot,"mouseLightingProtocolAvailable",true)&&effects.Length==5;if(result.CanRequestEffects)result.WritableEffects=effects;
   return result;
  }
  public static bool Same(MouseLightingObservation before,MouseLightingObservation after) {
   return before!=null&&after!=null&&before.IdentityConfirmed&&after.IdentityConfirmed&&before.RecipeConfirmed&&after.RecipeConfirmed&&before.DeviceId==after.DeviceId&&before.SoftwareOwned==after.SoftwareOwned&&before.WindowsOwned==after.WindowsOwned&&before.CurrentMode==after.CurrentMode&&before.Brightness==after.Brightness&&before.Direction==after.Direction&&before.Speed==after.Speed&&before.Random==after.Random&&before.Color!=null&&after.Color!=null&&before.Color.SequenceEqual(after.Color)&&before.CanRequestStatic==after.CanRequestStatic&&before.CanRequestEffects==after.CanRequestEffects&&before.WritableEffects.SequenceEqual(after.WritableEffects)&&before.StaticCurrent==after.StaticCurrent;
  }
  public static bool ValidValues(MouseLightingDraft draft) {
   return draft!=null&&draft.SelectedEffect.HasValue&&KnownEffect(draft.SelectedEffect.Value)&&draft.Brightness.HasValue&&draft.Brightness>=0&&draft.Brightness<=100&&(!HasColor(draft.SelectedEffect.Value)||KeyboardLightingUiFacts.Rgb(draft.Color)!=null);
  }
  public static MouseLightingDraft CopyDraft(MouseLightingDraft draft) {
   if(draft==null||String.IsNullOrEmpty(draft.DeviceId)||!(KeyboardLightingUiFacts.Identity(draft.DeviceId,7546)||KeyboardLightingUiFacts.Identity(draft.DeviceId,7548))||draft.SelectedEffect.HasValue&&!KnownEffect(draft.SelectedEffect.Value)||draft.Brightness.HasValue&&(draft.Brightness<0||draft.Brightness>100)||draft.Color!=null&&KeyboardLightingUiFacts.Rgb(draft.Color)==null||draft.Dirty&&!ValidValues(draft))return null;
   return new MouseLightingDraft{DeviceId=draft.DeviceId,SelectedEffect=draft.SelectedEffect,Brightness=draft.Brightness,Color=draft.Color==null?null:draft.Color.ToArray(),Dirty=draft.Dirty};
  }
  public static bool Changed(MouseLightingDraft draft,MouseLightingObservation before) {
   if(draft==null||before==null||!before.RecipeConfirmed)return false;
   if(before.CanRequestEffects)return draft.SelectedEffect.HasValue&&KnownEffect(draft.SelectedEffect.Value)&&(!ValidValues(draft)||draft.SelectedEffect!=before.CurrentMode||draft.Brightness!=before.Brightness||HasColor(draft.SelectedEffect.Value)&&before.Random==false&&!draft.Color.SequenceEqual(before.Color));
   return draft.SelectedEffect==0&&(!ValidValues(draft)||!before.StaticCurrent||draft.Brightness!=before.Brightness||!draft.Color.SequenceEqual(before.Color));
  }
  public static Dictionary<string,object> StaticValue(MouseLightingDraft draft) {
   if(!ValidValues(draft)||draft.SelectedEffect!=0)throw new ArgumentException("恒亮草稿无效。");
   // Direction and speed belong to the confirmed worker read, not this draft.
   return new Dictionary<string,object>{{"color",draft.Color.ToArray()},{"brightness",draft.Brightness.Value}};
  }
  public static Dictionary<string,object> EffectValue(MouseLightingDraft draft,MouseLightingObservation before) {
   if(!ValidValues(draft)||before==null||!before.CanRequestEffects||!before.WritableEffects.Contains(draft.SelectedEffect.Value))throw new ArgumentException("鼠标灯效草稿无效。");
   var value=new Dictionary<string,object>{{"effect",draft.SelectedEffect.Value},{"brightness",draft.Brightness.Value}};if(HasColor(draft.SelectedEffect.Value)&&before.Random==false)value["color"]=draft.Color.ToArray();return value;
  }
  public static bool TryColor(string value,out int[] rgb) {
   rgb=null;if(value==null||value.Length!=7||value[0]!='#'||value.Skip(1).Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F')))return false;
   rgb=new[]{Int32.Parse(value.Substring(1,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),Int32.Parse(value.Substring(3,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),Int32.Parse(value.Substring(5,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture)};return true;
  }
  public static string ColorText(int[] rgb){return KeyboardLightingUiFacts.Rgb(rgb)==null?"—":"#"+String.Join("",rgb.Select(v=>v.ToString("X2",CultureInfo.InvariantCulture)));}
  // The ownership bit identifies software control, not the software's name.
  public static string OwnershipText(MouseLightingObservation observed) {
   return observed!=null&&observed.WindowsOwned==true?"此设备目前由 Windows 动态灯光控制。":observed!=null&&observed.SoftwareOwned==true?"此设备目前由软件控制灯光。":"—";
  }
 }

 public sealed class MouseLightingBinding {
  readonly Dictionary<string,object> state;
  readonly object snapshot,lighting;
  readonly string capturedAt;
  readonly MouseLightingObservation observed;
  public MouseLightingBinding(Dictionary<string,object> source) {
   state=source;snapshot=KeyboardLightingUiFacts.Field(source,"snapshot");lighting=KeyboardLightingUiFacts.Field(snapshot as IDictionary<string,object>,"lighting");capturedAt=KeyboardLightingUiFacts.Field(source,"capturedAt") as string;observed=MouseLightingUiFacts.Read(source);
  }
  public MouseLightingObservation Observation {get{return observed;}}
  public bool IsCurrent(IDictionary<string,Dictionary<string,object>> states,Dictionary<string,object> source) {
   Dictionary<string,object> current;return source!=null&&states!=null&&states.TryGetValue("mouse",out current)&&Object.ReferenceEquals(current,source)&&Object.ReferenceEquals(state,source)&&snapshot!=null&&Object.ReferenceEquals(snapshot,KeyboardLightingUiFacts.Field(source,"snapshot"))&&Object.ReferenceEquals(lighting,KeyboardLightingUiFacts.Field(snapshot as IDictionary<string,object>,"lighting"))&&!String.IsNullOrWhiteSpace(capturedAt)&&capturedAt==KeyboardLightingUiFacts.Field(source,"capturedAt") as string&&MouseLightingUiFacts.Same(observed,MouseLightingUiFacts.Read(source));
  }
 }

 public partial class MainWindow {
  MouseLightingDraft mouseLightingDraft;
  MouseLightingBinding mouseLightingBinding;
  Dictionary<string,object> mouseLightingRestoreCache;
  bool mouseLightingRestored,mouseLightingApplying;
  static readonly string[] MouseLightingTitles={"恒亮","呼吸","彩色循环","触发","电池模式"};

  void RestoreMouseLightingDraft(MouseLightingDraft draft) {
   mouseLightingDraft=MouseLightingUiFacts.CopyDraft(draft);mouseLightingBinding=null;mouseLightingRestored=mouseLightingDraft!=null;gearStates.TryGetValue("mouse",out mouseLightingRestoreCache);
  }
  MouseLightingDraft MouseLightingValues(Dictionary<string,object> state) {
   var observed=MouseLightingUiFacts.Read(state);
   if(mouseLightingBinding!=null&&mouseLightingBinding.IsCurrent(gearStates,state)&&mouseLightingDraft!=null&&mouseLightingDraft.DeviceId==observed.DeviceId)return mouseLightingDraft;
   mouseLightingBinding=null;
   if(mouseLightingRestored&&Object.ReferenceEquals(mouseLightingRestoreCache,state))return mouseLightingDraft;
   if(!observed.IdentityConfirmed||!observed.RecipeConfirmed){if(!mouseLightingRestored)mouseLightingDraft=null;return mouseLightingDraft;}
   if(!mouseLightingRestored||mouseLightingDraft==null||mouseLightingDraft.DeviceId!=observed.DeviceId)mouseLightingDraft=new MouseLightingDraft{DeviceId=observed.DeviceId,SelectedEffect=observed.SoftwareOwned==false&&observed.WindowsOwned==false&&observed.CurrentMode.HasValue&&MouseLightingUiFacts.KnownEffect(observed.CurrentMode.Value)?observed.CurrentMode:null,Brightness=observed.Brightness,Color=observed.Color.ToArray()};
   mouseLightingRestored=false;mouseLightingRestoreCache=null;mouseLightingBinding=new MouseLightingBinding(state);mouseLightingDraft.Dirty=MouseLightingUiFacts.Changed(mouseLightingDraft,observed);return mouseLightingDraft;
  }
  bool MouseLightingHasPendingDraft(Dictionary<string,object> state) {
   return mouseLightingDraft!=null&&mouseLightingDraft.Dirty&&mouseLightingBinding!=null&&mouseLightingBinding.IsCurrent(gearStates,state)&&mouseLightingDraft.DeviceId==OriginalStateText(state,"deviceId","")&&(mouseLightingBinding.Observation.CanRequestStatic||mouseLightingBinding.Observation.CanRequestEffects);
  }
  bool MouseLightingCanEdit(Dictionary<string,object> state,MouseLightingDraft draft) {
   return !render&&!closing&&!busy&&!mouseLightingApplying&&draft!=null&&Object.ReferenceEquals(draft,mouseLightingDraft)&&mouseLightingBinding!=null&&mouseLightingBinding.IsCurrent(gearStates,state)&&draft.DeviceId==mouseLightingBinding.Observation.DeviceId&&(mouseLightingBinding.Observation.CanRequestStatic||mouseLightingBinding.Observation.CanRequestEffects);
  }
  bool MouseLightingCanSelect(Dictionary<string,object> state,MouseLightingDraft draft,int effect){return MouseLightingCanEdit(state,draft)&&(mouseLightingBinding.Observation.CanRequestEffects?mouseLightingBinding.Observation.WritableEffects.Contains(effect):effect==0&&mouseLightingBinding.Observation.CanRequestStatic);}
  bool MouseLightingMayApply(Dictionary<string,object> state,MouseLightingDraft draft) {
   return HardwareWritesEnabled&&draft!=null&&draft.SelectedEffect.HasValue&&MouseLightingCanSelect(state,draft,draft.SelectedEffect.Value)&&CanApplyGear(state,mouseLightingBinding.Observation.CanRequestEffects?"mouseLighting":"staticLighting")&&draft.Dirty&&MouseLightingUiFacts.ValidValues(draft);
  }
  async Task ApplyMouseLightingDraftAsync(Dictionary<string,object> state,MouseLightingDraft draft) {
   if(!MouseLightingMayApply(state,draft))return;
   bool effects=mouseLightingBinding.Observation.CanRequestEffects;var value=effects?MouseLightingUiFacts.EffectValue(draft,mouseLightingBinding.Observation):MouseLightingUiFacts.StaticValue(draft);
   mouseLightingApplying=true;try{await SetGearSetting("mouse",state,effects?"mouseLighting":"staticLighting",value);}finally{mouseLightingApplying=false;if(!closing)ShowPage(page);}
  }

  void OriginalMouseLightingPage(StackPanel target,Dictionary<string,object> state) {
   var observed=MouseLightingUiFacts.Read(state);var draft=MouseLightingValues(state);bool bound=mouseLightingBinding!=null&&mouseLightingBinding.IsCurrent(gearStates,state)&&draft!=null;int? selectedMode=bound?draft.SelectedEffect:null;bool selected=selectedMode.HasValue&&MouseLightingUiFacts.KnownEffect(selectedMode.Value);bool editable=selected&&MouseLightingCanSelect(state,draft,selectedMode.Value);
   var surface=new Canvas{Width=1246,Height=605,Tag="MouseLighting.Surface"};var viewer=new ScrollViewer{Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalContentAlignment=HorizontalAlignment.Left,Tag="MouseLighting.Viewport"};target.Children.Add(viewer);
   var reset=KeyboardOutlineButton("重置",()=>{});reset.Width=138;reset.Height=35;reset.IsEnabled=false;reset.Tag="MouseLighting.Reset";reset.ToolTip="重置协议尚未确认。";ToolTipService.SetShowOnDisabled(reset,true);KeyboardLightingPlace(surface,reset,1075,24);
   KeyboardLightingPlace(surface,KeyboardLightingHeading("基本效果",true),35,84);
   double[] centers={96,226,357,491,620};int[] effectCodes={0,1,2,4,6};
   for(int index=0;index<MouseLightingTitles.Length;index++) {
    int effect=effectCodes[index];bool current=selectedMode==effect;var ink=current?accent:muted;var tile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};tile.Children.Add(index==4?MouseLightingBatteryIcon(ink):KeyboardLightingEffectIcon(index,ink));var title=Label(MouseLightingTitles[index],15,ink);title.Margin=new Thickness(0,7,0,0);title.TextAlignment=TextAlignment.Center;tile.Children.Add(title);
    var button=new Button{Width=132,Height=106,Margin=new Thickness(0),Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=ink,Content=tile,Template=KeyboardLightingEffectTemplate(),IsEnabled=MouseLightingCanSelect(state,draft,effect),Tag="MouseLighting.Effect."+index,ToolTip=MouseLightingCanSelect(state,draft,effect)?"选择灯效草稿；点击应用后才写入。":"当前读取结果尚未开放此效果。"};ToolTipService.SetShowOnDisabled(button,true);
    button.Click+=(s,e)=>{if(!MouseLightingCanSelect(state,draft,effect))return;draft.SelectedEffect=effect;draft.Dirty=MouseLightingUiFacts.Changed(draft,mouseLightingBinding.Observation);ShowPage(page);};KeyboardLightingPlace(surface,button,centers[index]-66,121);
   }
   var auraTile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};auraTile.Children.Add(HeadsetLightingAuraIcon(muted));var auraLabel=Label("Aura Sync",15,muted);auraLabel.Margin=new Thickness(0,7,0,0);auraLabel.TextAlignment=TextAlignment.Center;auraTile.Children.Add(auraLabel);var aura=new Button{Width=132,Height=106,Margin=new Thickness(0),Padding=new Thickness(0),Content=auraTile,Template=KeyboardLightingEffectTemplate(),IsEnabled=false,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Tag="MouseLighting.Aura",ToolTip="同步接管接口尚未接入；软件控制状态不等于已确认 Aura Sync。"};ToolTipService.SetShowOnDisabled(aura,true);KeyboardLightingPlace(surface,aura,30,238);
   KeyboardLightingPlace(surface,KeyboardLightingHeading("高级效果",false),35,379);KeyboardLightingPlace(surface,KeyboardLightingCreatorIcon(),35,421);var creator=KeyboardOutlineButton("Aura Creator",()=>{});creator.Width=133;creator.Height=33;creator.IsEnabled=false;creator.Tag="MouseLighting.Creator";creator.ToolTip="Aura Creator 尚未接入。";ToolTipService.SetShowOnDisabled(creator,true);KeyboardLightingPlace(surface,creator,135,423);var note=Label("用 Aura Creator 自定义灯效，并同步兼容设备。",12,text);note.Margin=new Thickness(0);KeyboardLightingPlace(surface,note,135,468);

   var parameters=new Canvas{Width=376,Height=418};var frame=new Border{Width=376,Height=418,BorderBrush=muted,BorderThickness=new Thickness(0.7),CornerRadius=new CornerRadius(4),Background=Brushes.Transparent,Child=parameters,Tag="MouseLighting.Parameters"};KeyboardLightingPlace(surface,frame,832,82);
   var led=Label("LED 亮度",15,text);led.Margin=new Thickness(0);KeyboardLightingPlace(parameters,led,24,13);var info=Label("ⓘ",15,accent);info.Margin=new Thickness(0);info.ToolTip="显示设备读回的灯光参数；软件接管时不能由此页写入。";KeyboardLightingPlace(parameters,info,98,13);
   int? brightness=selected?draft.Brightness:observed.Brightness;bool brightnessKnown=brightness.HasValue&&brightness>=0&&brightness<=100;if(!brightnessKnown)brightness=null;var percent=Label(brightnessKnown?brightness.Value.ToString(CultureInfo.InvariantCulture)+"%":"—",14,text);percent.Margin=new Thickness(0);percent.Width=63;percent.TextAlignment=TextAlignment.Right;percent.Tag="MouseLighting.BrightnessValue";KeyboardLightingPlace(parameters,percent,289,13);
   var slider=new Slider{Width=328,Height=24,Minimum=0,Maximum=100,Value=brightness??0,TickFrequency=10,IsSnapToTickEnabled=true,SmallChange=10,LargeChange=10,IsEnabled=selected&&editable,Tag=brightnessKnown,Template=KeyboardLightingSliderTemplate(),Name="MouseLightingBrightness",ToolTip=percent.Text};KeyboardLightingPlace(parameters,slider,24,49);
   var apply=KeyboardOutlineButton("应用",async()=>await ApplyMouseLightingDraftAsync(state,draft));apply.Width=112;apply.Height=33;apply.IsEnabled=MouseLightingMayApply(state,draft);apply.Tag="MouseLighting.Apply";apply.ToolTip="应用已选择的灯效与可用参数；保留当前方向、速度及随机状态。";ToolTipService.SetShowOnDisabled(apply,true);KeyboardLightingPlace(parameters,apply,240,354);
   Action changed=()=>{draft.Dirty=MouseLightingUiFacts.Changed(draft,mouseLightingBinding.Observation);apply.IsEnabled=MouseLightingMayApply(state,draft);};
   slider.ValueChanged+=(s,e)=>{if(!selected||draft.SelectedEffect!=selectedMode||!MouseLightingCanSelect(state,draft,selectedMode.Value))return;draft.Brightness=(int)Math.Round(slider.Value);percent.Text=draft.Brightness.Value.ToString(CultureInfo.InvariantCulture)+"%";slider.ToolTip=percent.Text;changed();};
   bool externalOwner=observed.SoftwareOwned==true||observed.WindowsOwned==true;
   if(externalOwner) {
    KeyboardLightingPlace(parameters,HeadsetLightingAuraIcon(accent),156,130);var owner=Paragraph(MouseLightingUiFacts.OwnershipText(observed),14,text);owner.Margin=new Thickness(0);owner.Width=328;owner.TextAlignment=TextAlignment.Center;owner.Tag="MouseLighting.Ownership";KeyboardLightingPlace(parameters,owner,24,201);apply.Visibility=Visibility.Collapsed;
   } else {
    var current=Label(selected?MouseLightingUiFacts.EffectName(selectedMode.Value):"—",15,text,FontWeights.SemiBold);current.Margin=new Thickness(0);current.Tag="MouseLighting.CurrentTitle";current.ToolTip=observed.CurrentMode.HasValue?"设备读回的效果代码："+observed.CurrentMode.Value.ToString(CultureInfo.InvariantCulture):"状态未知";KeyboardLightingPlace(parameters,current,24,109);
    if(selected&&MouseLightingUiFacts.HasColor(selectedMode.Value)&&observed.Random==false) {
    var colorLabel=Label("颜色",15,text);colorLabel.Margin=new Thickness(0);KeyboardLightingPlace(parameters,colorLabel,24,151);
    bool colorKnown=selected&&draft.Color!=null;var chip=new Border{Width=33,Height=33,BorderBrush=muted,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Tag="MouseLighting.Color",ToolTip=colorKnown?MouseLightingUiFacts.ColorText(draft.Color):"—"};if(colorKnown)chip.Child=new Border{Margin=new Thickness(4),Background=new SolidColorBrush(Color.FromRgb((byte)draft.Color[0],(byte)draft.Color[1],(byte)draft.Color[2]))};else{var unknown=Label("—",13,muted);unknown.Margin=new Thickness(0);unknown.TextAlignment=TextAlignment.Center;unknown.VerticalAlignment=VerticalAlignment.Center;chip.Child=unknown;}KeyboardLightingPlace(parameters,chip,24,183);
    var colorInput=new TextBox{Text=colorKnown?MouseLightingUiFacts.ColorText(draft.Color):"—",Width=138,Height=33,MaxLength=7,Foreground=text,Background=panel,BorderBrush=border,Padding=new Thickness(8,5,8,4),FontSize=14,IsEnabled=selected&&editable,IsReadOnly=!selected||!editable,Tag="MouseLighting.ColorInput",ToolTip="输入 #RRGGBB；点击应用后才写入。"};KeyboardLightingPlace(parameters,colorInput,72,183);
    colorInput.TextChanged+=(s,e)=>{if(!selected||draft.SelectedEffect!=selectedMode||!MouseLightingCanSelect(state,draft,selectedMode.Value))return;int[] rgb;draft.Color=MouseLightingUiFacts.TryColor(colorInput.Text,out rgb)?rgb:null;colorInput.BorderBrush=rgb==null?accent:border;chip.ToolTip=rgb==null?"—":MouseLightingUiFacts.ColorText(rgb);if(rgb!=null)chip.Child=new Border{Margin=new Thickness(4),Background=new SolidColorBrush(Color.FromRgb((byte)rgb[0],(byte)rgb[1],(byte)rgb[2]))};else{var unknown=Label("—",13,muted);unknown.Margin=new Thickness(0);unknown.TextAlignment=TextAlignment.Center;unknown.VerticalAlignment=VerticalAlignment.Center;chip.Child=unknown;}changed();};
    } else if(selectedMode==6) {
     var batteryTitle=Label("电量",15,text);batteryTitle.Margin=new Thickness(0);KeyboardLightingPlace(parameters,batteryTitle,24,151);int? battery=KeyboardLightingUiFacts.Contains(state,"readCapabilities","battery")?KeyboardLightingUiFacts.Number(GearMap(state,"snapshot"),"batteryPercent",0,100):null;var batteryValue=Label(battery.HasValue?battery.Value.ToString(CultureInfo.InvariantCulture)+"%":"—",22,text);batteryValue.Margin=new Thickness(0);batteryValue.Tag="MouseLighting.BatteryValue";KeyboardLightingPlace(parameters,batteryValue,24,183);
    }
    var owner=Label(observed.SoftwareOwned==false&&observed.WindowsOwned==false?"":"—",14,muted);owner.Margin=new Thickness(0);owner.Tag="MouseLighting.Ownership";owner.ToolTip="灯光控制权须完整读回后才可应用。";KeyboardLightingPlace(parameters,owner,24,267);
   }
  }
  Canvas MouseLightingBatteryIcon(Brush ink) {
   var icon=new Canvas{Width=64,Height=64,IsHitTestVisible=false};icon.Children.Add(new Rectangle{Width=47,Height=33,Stroke=ink,StrokeThickness=4,Margin=new Thickness(6,16,0,0)});icon.Children.Add(new Rectangle{Width=7,Height=15,Fill=ink,Margin=new Thickness(55,25,0,0)});icon.Children.Add(new Polyline{Points=new PointCollection(new[]{new Point(35,8),new Point(20,34),new Point(33,34),new Point(23,57),new Point(44,29),new Point(31,29),new Point(35,8)}),Stroke=ink,StrokeThickness=4});return icon;
  }
 }
}
