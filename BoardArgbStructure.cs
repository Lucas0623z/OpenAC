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
   var commands=Row();var read=Button(busy?"正在读取…":"读取主板通道",async()=>await ReadBoardArgbChannels());read.IsEnabled=!busy&&!render;commands.Children.Add(read);
   var reset=Button("重新检测",()=>{});reset.IsEnabled=false;reset.ToolTip="接针重新检测接口尚未接通。";ToolTipService.SetShowOnDisabled(reset,true);commands.Children.Add(reset);target.Children.Add(commands);
   target.Children.Add(Paragraph(boardArgbMessage,12,muted));
   var live=worker.Running&&boardArgbSnapshot!=null?GearArray(boardArgbSnapshot,"devices"):null;
   var headers=new List<IDictionary<string,object>>();string firmware="—";int onboardZones=0;
   if(live!=null)foreach(var rawDevice in live){var device=rawDevice as IDictionary<string,object>;if(device==null||OriginalStateText(device,"controllerFamily","")!="motherboard")continue;string id=OriginalStateText(device,"id","");if(!devices.Any(d=>d.id==id))continue;firmware=OriginalStateText(device,"firmware","—");var zones=GearArray(device,"zones");if(zones==null)continue;foreach(var rawZone in zones){var zone=rawZone as IDictionary<string,object>;if(zone==null)continue;if(OriginalStateFlag(zone,"addressable")==true)headers.Add(zone);else onboardZones++;}}
   var summary=new Grid{Margin=new Thickness(0,20,0,22)};summary.ColumnDefinitions.Add(new ColumnDefinition());summary.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});summary.Children.Add(Label("已设置灯珠总数",14,text));var total=Label("— / —",16,muted);total.Margin=new Thickness(0);Grid.SetColumn(total,1);summary.Children.Add(total);target.Children.Add(summary);
   target.Children.Add(Paragraph("当前数量、总数上限和 Gen 1 / Gen 2 模式无法读回，设置与重新检测暂不可用。",12,muted));
   var cards=new WrapPanel{Margin=new Thickness(0,12,0,14)};
   if(headers.Count==0)cards.Children.Add(BoardArgbHeaderCard("可编程接针",null));
   else for(int n=0;n<headers.Count;n++)cards.Children.Add(BoardArgbHeaderCard("可编程接针 "+(n+1),headers[n]));
   target.Children.Add(cards);
   if(headers.Count>0)target.Children.Add(Paragraph("已识别 "+headers.Count+" 个逻辑接针通道 · 主板灯区 "+onboardZones+" 个 · 固件 "+firmware+"。逻辑通道数不代表连接设备或灯珠数量。",11,muted));
   var aura=Button("打开 Aura 灯效",()=>{auraTab="Aura 灯效";ShowPage("灯效 & Aura Sync");});aura.Margin=new Thickness(0,12,0,0);target.Children.Add(aura);
  }

  UIElement BoardArgbHeaderCard(string name,IDictionary<string,object> zone) {
   var panel=new StackPanel();var heading=Label(name,16,text,FontWeights.SemiBold);heading.Margin=new Thickness(0,0,0,18);panel.Children.Add(heading);
   var generations=new Grid();generations.ColumnDefinitions.Add(new ColumnDefinition());generations.ColumnDefinitions.Add(new ColumnDefinition());
   for(int n=0;n<2;n++){var choice=Button(n==0?"Gen 1":"Gen 2",()=>{});choice.IsEnabled=false;choice.Margin=new Thickness(n==0?0:4,0,n==0?4:0,8);choice.ToolTip="当前模式未读回，切换尚未接通。";ToolTipService.SetShowOnDisabled(choice,true);Grid.SetColumn(choice,n);generations.Children.Add(choice);}panel.Children.Add(generations);
   var unknown=Label("当前模式：—",11,muted);unknown.Margin=new Thickness(0,0,0,18);panel.Children.Add(unknown);
   panel.Children.Add(Label("LED 灯珠数量",13,text));var count=FeatureText("—");count.IsEnabled=false;count.HorizontalContentAlignment=HorizontalAlignment.Center;count.Margin=new Thickness(0,7,0,16);count.ToolTip="此通道的物理灯珠数量尚未读回。";ToolTipService.SetShowOnDisabled(count,true);panel.Children.Add(count);
   var finish=Button("完成",()=>{});finish.IsEnabled=false;finish.HorizontalAlignment=HorizontalAlignment.Right;finish.Margin=new Thickness(0);finish.ToolTip="灯珠数量设置接口尚未接通。";ToolTipService.SetShowOnDisabled(finish,true);panel.Children.Add(finish);
   if(zone!=null){var channel=Label("逻辑通道 "+OriginalStateText(zone,"channel","—"),10,muted);channel.Margin=new Thickness(0,16,0,0);panel.Children.Add(channel);}
   return new Border{Width=270,Padding=new Thickness(20),Margin=new Thickness(0,0,20,20),BorderBrush=border,BorderThickness=new Thickness(1),Background=Brush("#151515"),Child=panel};
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
