using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public sealed class ControllerLightDraft {
  public string Port {get;set;} public string Area {get;set;} public string Effect {get;set;}
  public int[] FanCounts {get;set;} public int Brightness {get;set;} public int Speed {get;set;} public string Direction {get;set;} public string[] Colors {get;set;}
  public DateTime Submitted {get;set;} public string Acknowledgement {get;set;}
  public ControllerLightDraft Copy(){return new ControllerLightDraft{Port=Port,Area=Area,Effect=Effect,FanCounts=FanCounts==null?null:(int[])FanCounts.Clone(),Brightness=Brightness,Speed=Speed,Direction=Direction,Colors=Colors==null?null:(string[])Colors.Clone(),Submitted=Submitted,Acknowledgement=Acknowledgement};}
  public bool SameConfiguration(ControllerLightDraft other){return other!=null&&Port==other.Port&&Area==other.Area&&Effect==other.Effect&&Brightness==other.Brightness&&Speed==other.Speed&&Direction==other.Direction&&SameValues(FanCounts,other.FanCounts)&&SameValues(Colors,other.Colors);}
  static bool SameValues<T>(T[] left,T[] right){return left==null?right==null:right!=null&&left.SequenceEqual(right);}
 }
 public partial class MainWindow {
  ControllerLightDraft controllerLightDraft=LocalFeatureStore.Load("controller-lighting.json",new ControllerLightDraft{Port="1",Area="center",Effect="static",FanCounts=new[]{1,0,0,0},Brightness=100,Speed=60,Direction="forward",Colors=new[]{"#FF0080"}});
  readonly HashSet<string> controllerSourceDraft=new HashSet<string>(LocalFeatureStore.Load("controller-temperature-sources.json",new string[0])??new string[0]);
  string ControllerAreaName(string area){return area=="center"?"中心":area=="outer"?"外圈":area=="sync"?"中心与外圈同步":"跨端口联动";}
  string ControllerEffectName(string effect){var names=new Dictionary<string,string>{{"static","常亮"},{"breathe","呼吸"},{"rainbow","彩虹"},{"paint","彩色涂绘"},{"oddEven","交错流动"},{"pinball","弹球"},{"track","追踪"},{"storm","风暴"},{"river","河流"},{"rainbowRiver","彩虹河流"},{"dualPulse","双脉冲"},{"barberPole","螺旋"},{"spread","扩散"}};string name;return names.TryGetValue(effect,out name)?name:effect;}
  void OriginalControllerLightingPage(StackPanel target,IDictionary<string,object> snapshot) {
   if(controllerLightDraft==null)controllerLightDraft=new ControllerLightDraft();var draft=controllerLightDraft;
   if(draft.FanCounts==null||draft.FanCounts.Length!=4)draft.FanCounts=new[]{1,0,0,0};if(draft.Port!="1"&&draft.Port!="2"&&draft.Port!="3"&&draft.Port!="4")draft.Port="1";if(draft.Area!="center"&&draft.Area!="outer"&&draft.Area!="sync"&&draft.Area!="connected")draft.Area="center";
   var read=Button(busy?"正在读取…":"读取控制器状态",async()=>await ReadOriginalSnapshot("fan","7576"));read.IsEnabled=!busy&&!render;target.Children.Add(read);
   target.Children.Add(Paragraph("固件  "+OriginalStateText(snapshot,"firmware","—")+"  · 当前灯效和风扇数量无法读回。以下为编辑草稿。",12,muted));
   var ports=Row();for(int n=1;n<=4;n++){string port=n.ToString(CultureInfo.InvariantCulture);var choose=Button(FanAlias(port),()=>{if(busy||render)return;draft.Port=port;ShowPage(page);});choose.Foreground=draft.Port==port?accent:text;choose.IsEnabled=draft.Area!="connected"&&!busy&&!render;ports.Children.Add(choose);}target.Children.Add(ports);
   var areas=new[]{"center","outer","sync","connected"};var areaCombo=FeatureCombo(areas.Select(ControllerAreaName),ControllerAreaName(draft.Area));areaCombo.IsEnabled=!busy&&!render;areaCombo.SelectionChanged+=(s,e)=>{if(busy||render)return;int index=areaCombo.SelectedIndex;if(index<0)return;draft.Area=areas[index];draft.Effect="";ShowPage(page);};target.Children.Add(FeatureField("灯效区域",areaCombo));
   var caps=GearArray(snapshot,"effectCapabilities");var effects=new List<IDictionary<string,object>>();if(caps!=null)foreach(var raw in caps){var cap=raw as IDictionary<string,object>;if(cap!=null&&OriginalStateText(cap,"area","")==draft.Area)effects.Add(cap);}
   if(effects.Count==0){target.Children.Add(Paragraph("此区域尚未读取到支持的灯效。",12,muted));OriginalControllerTemperatureSourceEditor(target,snapshot);return;}
   var selected=effects.FirstOrDefault(cap=>OriginalStateText(cap,"effect","")==draft.Effect)??effects[0];draft.Effect=OriginalStateText(selected,"effect","");
   var effectCombo=FeatureCombo(effects.Select(cap=>ControllerEffectName(OriginalStateText(cap,"effect",""))),ControllerEffectName(draft.Effect));effectCombo.IsEnabled=!busy&&!render;effectCombo.SelectionChanged+=(s,e)=>{if(busy||render)return;int index=effectCombo.SelectedIndex;if(index<0||index>=effects.Count)return;draft.Effect=OriginalStateText(effects[index],"effect","");draft.Colors=null;ShowPage(page);};target.Children.Add(FeatureField("灯效",effectCombo));
   bool connected=draft.Area=="connected";
   if(connected)target.Children.Add(Paragraph("应用跨端口联动会影响全部 4 个端口。请填写各端口连接数量。",12,accent));
   else {target.Children.Add(Paragraph("如果当前使用跨端口联动，请先结束联动，再应用单独端口。",11,muted));var disconnect=Button("结束联动，端口 1 中心设为白色",async()=>await SetOriginalNative("fan","7576","disconnectLightingChain",new Dictionary<string,object>{{"portId","1"}}));disconnect.IsEnabled=HardwareWritesEnabled&&OriginalStateFlag(snapshot,"supportsDisconnectLightingChain")==true&&!busy;target.Children.Add(disconnect);}
   var countBoxes=new List<TextBox>();int active=int.Parse(draft.Port,CultureInfo.InvariantCulture)-1;
   for(int n=0;n<4;n++){int index=n;if(!connected&&index!=active)continue;var count=FeatureText(draft.FanCounts[index].ToString(CultureInfo.InvariantCulture));count.MaxLength=2;count.IsEnabled=!busy&&!render;countBoxes.Add(count);count.LostKeyboardFocus+=(s,e)=>{if(busy||render)return;int value;if(Int32.TryParse(count.Text,out value)&&value>=0&&value<=14&&value!=draft.FanCounts[index]){draft.FanCounts[index]=value;ShowPage(page);}};target.Children.Add(FeatureField(FanAlias((index+1).ToString())+" · 风扇数量 0–14",count));}
   var brightness=FeatureCombo(new[]{"0","25","50","75","100"},draft.Brightness.ToString(CultureInfo.InvariantCulture));brightness.IsEnabled=!busy&&!render;brightness.SelectionChanged+=(s,e)=>{if(busy||render)return;int value;if(Int32.TryParse(Convert.ToString(brightness.SelectedItem),out value))draft.Brightness=value;};target.Children.Add(FeatureField("亮度 %",brightness));
   bool speedAllowed=OriginalStateFlag(selected,"hasSpeed")==true,directionAllowed=OriginalStateFlag(selected,"hasDirection")==true;
   if(!speedAllowed)draft.Speed=60;if(!directionAllowed)draft.Direction="forward";
   var speed=FeatureCombo(new[]{"20","40","60","80","100"},draft.Speed.ToString(CultureInfo.InvariantCulture));speed.IsEnabled=speedAllowed&&!busy&&!render;speed.SelectionChanged+=(s,e)=>{if(busy||render)return;int value;if(Int32.TryParse(Convert.ToString(speed.SelectedItem),out value))draft.Speed=value;};target.Children.Add(FeatureField("速度",speed));
   var direction=FeatureCombo(new[]{"正向","反向"},draft.Direction=="backward"?"反向":"正向");direction.IsEnabled=directionAllowed&&!busy&&!render;direction.SelectionChanged+=(s,e)=>{if(!busy&&!render)draft.Direction=direction.SelectedIndex==1?"backward":"forward";};target.Children.Add(FeatureField("方向",direction));
   int fixedColors=Convert.ToInt32(selected["colorCount"],CultureInfo.InvariantCulture),countColors=fixedColors<0?Math.Max(1,Math.Min(14,draft.FanCounts[active]))*(draft.Area=="outer"?2:1):fixedColors;
   if(draft.Colors==null||draft.Colors.Length!=countColors)draft.Colors=Enumerable.Range(0,countColors).Select(i=>i%2==0?"#FF0080":"#FFFFFF").ToArray();
   var colorsPanel=new WrapPanel{Margin=new Thickness(0,10,0,12)};var colorBoxes=new List<TextBox>();
   for(int n=0;n<countColors;n++){int index=n;var box=FeatureText(draft.Colors[index]);box.Width=120;box.MaxLength=7;box.IsEnabled=!busy&&!render;box.TextChanged+=(s,e)=>{if(!busy&&!render&&draft.Colors!=null&&index<draft.Colors.Length)draft.Colors[index]=box.Text;};string name=fixedColors<0?("风扇 "+(draft.Area=="outer"?index/2+1:index+1)+(draft.Area=="outer"?(index%2==0?" 上":" 下"):"")):"颜色 "+(index+1);var colorField=FeatureField(name,box);colorField.Width=150;colorField.Margin=new Thickness(0,0,15,10);colorsPanel.Children.Add(colorField);colorBoxes.Add(box);}target.Children.Add(colorsPanel);
   if(countColors>0)target.Children.Add(Paragraph("颜色填写 #RRGGBB；#000000 表示黑色。",11,muted));
   target.Children.Add(Paragraph("应用会先提交连接数量，再提交灯效；两项均不能读回确认。",11,muted));
   var apply=Button(connected?"应用全部端口联动":"应用此端口灯效",async()=>{
    if(busy||render||closing)return;
    int[] counts=(int[])draft.FanCounts.Clone();for(int n=0;n<countBoxes.Count;n++){int value;if(!Int32.TryParse(countBoxes[n].Text,out value)||value<0||value>14){Log("连接数量必须为 0–14。");return;}counts[connected?n:active]=value;}
    if(!connected&&counts[active]<1||connected&&counts.Sum()==0){Log("请填写实际连接的风扇数量。");return;}
    int needed=fixedColors<0?counts[active]*(draft.Area=="outer"?2:1):fixedColors;if(colorBoxes.Count!=needed){draft.FanCounts=counts;ShowPage(page);Log("数量已修改，请检查对应颜色后应用。");return;}
    string[] colors=colorBoxes.Select(box=>box.Text.Trim()).ToArray();foreach(string color in colors)if(color.Length!=7||color[0]!='#'||color.Substring(1).Any(c=>!Uri.IsHexDigit(c))){Log("颜色需要使用 #RRGGBB 格式。");return;}
    draft.FanCounts=counts;draft.Colors=colors;var submitted=draft.Copy();
    var arguments=new Dictionary<string,object>{{"portId",connected?"1":submitted.Port},{"area",submitted.Area},{"effect",submitted.Effect},{"brightness",submitted.Brightness},{"speed",submitted.Speed},{"direction",submitted.Direction},{"colors",(string[])submitted.Colors.Clone()}};if(connected)arguments["fanCounts"]=(int[])submitted.FanCounts.Clone();else arguments["fanCount"]=submitted.FanCounts[active];
    var response=await SetOriginalNative("fan","7576","lighting",arguments);if(response!=null){submitted.Submitted=DateTime.Now;submitted.Acknowledgement=OriginalStateText(response,"appliedState","unconfirmed");if(Object.ReferenceEquals(controllerLightDraft,draft)&&draft.SameConfiguration(submitted)){draft.Submitted=submitted.Submitted;draft.Acknowledgement=submitted.Acknowledgement;}try{LocalFeatureStore.Save("controller-lighting.json",submitted);}catch(Exception ex){Log("命令已完成，本地配置保存失败："+ex.Message);}if(!closing)ShowPage(page);}
   },true);apply.IsEnabled=HardwareWritesEnabled&&OriginalStateFlag(snapshot,"supportsLighting")==true&&!busy;target.Children.Add(apply);
   if(draft.Submitted!=default(DateTime))target.Children.Add(Paragraph("上次提交的本地草稿  "+draft.Submitted.ToString("MM-dd HH:mm:ss")+" · "+draft.Acknowledgement,11,muted));if(!String.IsNullOrEmpty(nativeSettingsMessage))target.Children.Add(Paragraph(nativeSettingsMessage,12,muted));
   OriginalControllerTemperatureSourceEditor(target,snapshot);
  }
  void OriginalControllerTemperatureSourceEditor(StackPanel target,IDictionary<string,object> snapshot) {
   target.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,25,0,20)});target.Children.Add(Label("风扇温度来源",17,text,FontWeights.SemiBold));target.Children.Add(Paragraph("选择控制器使用的温度来源。当前选择无法读回，以下为本地草稿。",11,muted));
   var sources=GearArray(snapshot,"temperatureSources");if(sources==null||sources.Length==0){target.Children.Add(Paragraph("尚未读取到温度来源。",12,muted));return;}
   var choices=new WrapPanel();foreach(var raw in sources){var source=raw as IDictionary<string,object>;if(source==null)continue;string id=OriginalStateText(source,"sourceId","");bool available=OriginalStateFlag(source,"available")==true;var checkbox=new CheckBox{Content=OriginalStateText(source,"name",id)+(available?"":" · 不可用"),Foreground=available?text:muted,IsEnabled=available&&!busy&&!render,IsChecked=available&&controllerSourceDraft.Contains(id),Margin=new Thickness(0,10,25,10)};checkbox.Checked+=(s,e)=>{if(!busy&&!render)controllerSourceDraft.Add(id);};checkbox.Unchecked+=(s,e)=>{if(!busy&&!render)controllerSourceDraft.Remove(id);};choices.Children.Add(checkbox);}target.Children.Add(choices);
   var apply=Button("应用温度来源",async()=>{if(busy||render||closing)return;var available=sources.Select(raw=>raw as IDictionary<string,object>).Where(source=>source!=null&&OriginalStateFlag(source,"available")==true).Select(source=>OriginalStateText(source,"sourceId","")).Where(controllerSourceDraft.Contains).ToArray();if(available.Length==0){Log("请选择至少一个可用温度来源。");return;}var response=await SetOriginalNative("fan","7576","temperatureSources",new Dictionary<string,object>{{"sourceIds",available}});if(response!=null)try{LocalFeatureStore.Save("controller-temperature-sources.json",available);}catch(Exception ex){Log("本地温度来源保存失败："+ex.Message);}},false);apply.IsEnabled=HardwareWritesEnabled&&OriginalStateFlag(snapshot,"supportsTemperatureSources")==true&&!busy;target.Children.Add(apply);
  }
 }
}
