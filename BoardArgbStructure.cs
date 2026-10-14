using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  Dictionary<string,object> boardArgbSnapshot;
  string boardArgbMessage="读取通道后可查看已识别的接针；灯珠数量与 Gen 代数尚未读回。";

  // Keep the installed addressable-header page's generation switch, total LED
  // count, LED-count field, Done action and rescan action. ECB0 reports only
  // logical zones, so none of these physical-strip values may be fabricated.
  void OriginalBoardArgbPage(StackPanel target) {
   var body=new StackPanel{Name="BoardArgbSurface",Margin=new Thickness(35,24,21,0)};target.Children.Add(body);
   var commands=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,34,25)};var info=Label("ⓘ",21,Brush("#B92842"));info.Margin=new Thickness(0,0,22,0);info.VerticalAlignment=VerticalAlignment.Center;info.ToolTip=boardArgbMessage;commands.Children.Add(info);var read=Button(busy?"正在扫描…":"重新扫描",async()=>await ReadBoardArgbChannels());read.Width=132;read.Height=34;read.Padding=new Thickness(10,4,10,4);read.Margin=new Thickness(0);read.IsEnabled=!busy&&!render;read.ToolTip="读取主板逻辑灯区；当前接头代数与灯珠数量未返回。";ToolTipService.SetShowOnDisabled(read,true);commands.Children.Add(read);body.Children.Add(commands);
   var guidance=Paragraph("当使用分线或控制盒时，单个 ARGB 第二代接头最多只能支持四个第二代 ARGB 设备。",13,muted);guidance.Margin=new Thickness(0,0,0,13);body.Children.Add(guidance);var generationNote=Paragraph("当切换至 ARGB Gen 2 模式时，仅能侦测到 ARGB Gen 2 设备。",13,muted);generationNote.Margin=new Thickness(0,0,0,86);body.Children.Add(generationNote);
   var live=worker.Running&&boardArgbSnapshot!=null?GearArray(boardArgbSnapshot,"devices"):null;
   var headers=new List<IDictionary<string,object>>();if(live!=null)foreach(var rawDevice in live){var device=rawDevice as IDictionary<string,object>;if(device==null||OriginalStateText(device,"controllerFamily","")!="motherboard")continue;string id=OriginalStateText(device,"id","");if(!devices.Any(d=>d.id==id))continue;var zones=GearArray(device,"zones");if(zones==null)continue;foreach(var rawZone in zones){var zone=rawZone as IDictionary<string,object>;if(zone!=null&&OriginalStateFlag(zone,"addressable")==true)headers.Add(zone);}}
   // This model has three authored physical connector positions. A logical
   // SDK zone is not proof of a connector's generation, LEDs or physical ID.
   string channels=headers.Count==0?"物理接头状态未读取。":"已读取 "+headers.Count+" 个逻辑灯区；无法映射物理接头。";for(int n=0;n<3;n++){var card=BoardArgbHeaderCard("接头"+(n+1),null);card.ToolTip=channels;body.Children.Add(card);}
  }

  FrameworkElement BoardArgbHeaderCard(string name,IDictionary<string,object> zone) {
   var panel=new StackPanel{Width=340,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,25)};var heading=Label(name,14,text,FontWeights.SemiBold);heading.Margin=new Thickness(0,0,0,5);panel.Children.Add(heading);
   var generations=new Grid();generations.ColumnDefinitions.Add(new ColumnDefinition());generations.ColumnDefinitions.Add(new ColumnDefinition());
   for(int n=0;n<2;n++){var choice=Button(n==0?"Gen1":"Gen2",()=>{});choice.Height=28;choice.IsEnabled=false;choice.Margin=new Thickness(0);choice.Padding=new Thickness(8,2,8,2);choice.Background=Brush("#191919");choice.BorderThickness=new Thickness(0);choice.ToolTip="当前代数未读取。";ToolTipService.SetShowOnDisabled(choice,true);Grid.SetColumn(choice,n);generations.Children.Add(choice);}panel.Children.Add(generations);
   var settings=new Grid{Width=260,Height=216,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,24,0,0),Background=Brush("#1A1A1A")};var values=new StackPanel{Margin=new Thickness(16,16,14,0)};var unknown=Label("—",14,muted);unknown.Margin=new Thickness(0,0,0,15);unknown.ToolTip="当前 Gen 模式未读取。";values.Children.Add(unknown);
   var countTitle=new Grid();countTitle.ColumnDefinitions.Add(new ColumnDefinition());countTitle.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var caption=Label("LED 数量",13,text);caption.Margin=new Thickness(0,0,0,4);countTitle.Children.Add(caption);var total=Label("— / —",13,muted);total.Margin=new Thickness(0);Grid.SetColumn(total,1);countTitle.Children.Add(total);values.Children.Add(countTitle);var count=FeatureText("—");count.Height=34;count.IsEnabled=false;count.IsReadOnly=true;count.HorizontalContentAlignment=HorizontalAlignment.Center;count.TextAlignment=TextAlignment.Center;count.Margin=new Thickness(0);count.ToolTip="此接头的灯珠数量与上限未读取。";ToolTipService.SetShowOnDisabled(count,true);values.Children.Add(count);settings.Children.Add(values);
   var finish=Button("完成",()=>{});finish.Width=132;finish.Height=34;finish.Padding=new Thickness(8,4,8,4);finish.IsEnabled=false;finish.HorizontalAlignment=HorizontalAlignment.Right;finish.VerticalAlignment=VerticalAlignment.Bottom;finish.Margin=new Thickness(0,0,22,24);finish.ToolTip="灯珠数量设置暂不可用。";ToolTipService.SetShowOnDisabled(finish,true);settings.Children.Add(finish);panel.Children.Add(settings);return panel;
  }

  async Task ReadBoardArgbChannels() {
   if(busy||closing||render)return;
   boardArgbSnapshot=null;boardArgbMessage="正在读取主板 USB 通道…";
   busy=true;
   try{
    var scan=await worker.Request(new Dictionary<string,object>{{"op","scan"}});
    if(!scan.ok)throw new InvalidOperationException(scan.error);devices=scan.devices??new List<AuraDevice>();
    if(devices.Count==0){boardArgbMessage="未找到主板灯控。";return;}
    var snapshot=await worker.Request<Dictionary<string,object>>(new Dictionary<string,object>{{"op","status"}});
    if(OriginalStateFlag(snapshot,"ok")!=true)throw new InvalidOperationException(OriginalStateText(snapshot,"error","通道读取失败。"));
    boardArgbSnapshot=snapshot;boardArgbMessage="已读取逻辑通道。实际灯珠数量与当前 Gen 模式尚未读回。";
   }catch(Exception ex){boardArgbSnapshot=null;boardArgbMessage="通道读取失败："+ex.Message;Log(boardArgbMessage);}
   finally{busy=false;if(!closing)ShowPage(page);}
  }
 }
}
