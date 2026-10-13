using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  readonly List<Dictionary<string,object>> receiverStates=new List<Dictionary<string,object>>();
  string receiverMessage="尚未读取接收器。";
  void OriginalReceiverPage(){
   Header("ROG OMNI RECEIVER","");var actions=Row();var read=Button(busy?"正在读取…":"读取接收器",async()=>await ReadOriginalReceivers());read.IsEnabled=!busy&&!render;actions.Children.Add(read);content.Children.Add(actions);content.Children.Add(Paragraph(receiverMessage,12,muted));
   content.Children.Add(OriginalInstalledImage(OriginalProductImage("receiver"),270,160));
   foreach(var state in receiverStates){var snapshot=GearMap(state,"snapshot");int pid;Int32.TryParse(OriginalStateText(state,"pid","0"),out pid);string name=pid==6864?"ROG SPEEDNOVA 8K RECEIVER":pid==6862?"ROG OMNI RECEIVER":OriginalStateText(state,"product","ROG 接收器");var box=Box(name,"");box.Children.Add(Paragraph("固件："+OriginalStateText(snapshot,"firmware","未读回")+" · "+(GearOnline(state)?"接收器已响应":"未确认接收器响应"),13,text));
    var pairs=GearArray(snapshot,"pairedDevices");if(pairs!=null){box.Children.Add(Label("已配对设备记录",15,text));foreach(var raw in pairs){var pair=raw as Dictionary<string,object>;if(pair==null)continue;string kind=OriginalStateText(pair,"kind","");var row=Row();row.Children.Add(Paragraph(OriginalStateText(pair,"model","未知设备"),13,text));if(kind=="keyboard"||kind=="mouse")row.Children.Add(Button("打开设备设置",()=>OpenOriginalDevice(kind)));box.Children.Add(row);}}
    else box.Children.Add(Paragraph("配对记录未读回。",12,muted));box.Children.Add(Paragraph("配对记录不能证明外设已经在线；进入设备设置后读取连接状态。",11,muted));content.Children.Add(Card(box));
   }
   content.Children.Add(Paragraph("配对、更换接收器和固件刷写尚未接通。",12,muted));
  }
  async Task ReadOriginalReceivers(){if(busy||render||closing)return;busy=true;status.Text="正在读取接收器配对记录…";try{var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"},{"kind","omni"}});if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));receiverStates.Clear();var found=GearArray(reply.data,"devices");if(found!=null)receiverStates.AddRange(found.OfType<Dictionary<string,object>>());receiverMessage="已读取 "+receiverStates.Count+" 个接收器。";Log(receiverMessage);}catch(Exception ex){receiverStates.Clear();receiverMessage="读取失败："+ex.Message;Log(receiverMessage);}finally{busy=false;if(!closing)ShowPage(page);}}
 }
}
