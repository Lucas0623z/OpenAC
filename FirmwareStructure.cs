using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  readonly Dictionary<string,Dictionary<string,object>> firmwareCatalogs=new Dictionary<string,Dictionary<string,object>>();
  void OriginalGearFirmware(StackPanel target,string kind,Dictionary<string,object> state){
   var snapshot=GearMap(state,"snapshot");target.Children.Add(Label("当前固件",18,text));target.Children.Add(Paragraph("设备："+OriginalStateText(snapshot,"firmware","未读回")+" · 接收器："+OriginalStateText(snapshot,"dongleFirmware","未读回"),14,accent));
   var check=Button(busy?"正在检查…":"检查官网固件版本",async()=>await CheckOriginalFirmware(kind));check.IsEnabled=!busy&&!render;target.Children.Add(check);Dictionary<string,object> catalog;
   if(firmwareCatalogs.TryGetValue(kind,out catalog)){target.Children.Add(Label("官网发布版本",18,text));string published=OriginalStateText(catalog,"deviceFWVersion","未提供"),installed=OriginalStateText(snapshot,"firmware","");target.Children.Add(Paragraph("设备："+published+" · 接收器："+OriginalStateText(catalog,"dongleFWVersion","未提供"),14,text));string comparison=installed.Length==0?"设备当前版本未读回，不能判断是否需要更新。":String.Equals(installed,published,StringComparison.OrdinalIgnoreCase)?"设备固件与官网发布版本相同。":"版本不同；是否适用需要由官方更新工具确认。";target.Children.Add(Paragraph(comparison,12,muted));target.Children.Add(Paragraph("获取时间（UTC）："+OriginalStateText(catalog,"fetchedAtUtc","—"),11,muted));}
   target.Children.Add(Paragraph("此页可检查官网版本，固件刷写尚未接通。",12,muted));target.Children.Add(Button("打开官方 GearLink",()=>OpenOfficialUrl("https://gearlink.asus.com/")));
  }
  async Task CheckOriginalFirmware(string kind){if(busy||closing||render)return;busy=true;status.Text="正在检查华硕官网固件清单…";try{var reply=await gearWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","firmware"},{"kind",kind}});if(!reply.ok||reply.data==null)throw new InvalidOperationException(SdkError(reply.error));firmwareCatalogs[kind]=reply.data;Log("官网固件版本已读取，没有下载或刷写固件。");}catch(Exception ex){firmwareCatalogs.Remove(kind);Log("官网检查失败："+ex.Message);}finally{busy=false;if(!closing)ShowPage(page);}}
 }
}
