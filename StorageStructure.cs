using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  readonly WorkerClient storageWorker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"StorageWorker.exe"),12000);
  object[] storageDisks;int storageDiskIndex;string storageMessage="尚未读取";
  void OriginalStoragePage(){
   var frame=new StackPanel{Margin=new Thickness(75,10,70,0)};content.Children.Add(frame);var row=Row();var read=Button(busy?"正在读取…":"读取磁盘状态",async()=>await ReadStorage());read.IsEnabled=!busy&&!render;row.Children.Add(read);row.Children.Add(Label(storageMessage,12,muted));frame.Children.Add(row);
   var choice=new ComboBox{Background=Brush("#F1F1F1"),Foreground=Brush("#151515"),MinWidth=330,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,14,0,18),Padding=new Thickness(10,7,10,7)};
   if(storageDisks!=null)foreach(object disk in storageDisks)choice.Items.Add(OriginalStateText(disk as IDictionary<string,object>,"model","未知磁盘"));choice.SelectedIndex=storageDisks!=null&&storageDisks.Length>storageDiskIndex?storageDiskIndex:-1;choice.SelectionChanged+=(s,e)=>{if(choice.SelectedIndex<0||choice.SelectedIndex==storageDiskIndex)return;storageDiskIndex=choice.SelectedIndex;ShowPage(page);};frame.Children.Add(choice);
   var entry=storageDisks!=null&&storageDiskIndex<storageDisks.Length?storageDisks[storageDiskIndex] as Dictionary<string,object>:null;var smart=GearMap(entry,"smart");
   var table=new Grid();table.ColumnDefinitions.Add(new ColumnDefinition());table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(25)});table.ColumnDefinitions.Add(new ColumnDefinition());
   var left=new StackPanel();var right=new StackPanel();Grid.SetColumn(right,2);table.Children.Add(left);table.Children.Add(right);frame.Children.Add(table);
   string[] labels={"温度","主机读取量总数","主机写入量总数","通电次数","通电时间"};string[] keys={"temperature","hostReadGB","hostWrittenGB","powerCycles","powerOnHours"};string[] units={" °C"," GB"," GB"," 次"," Hours"};for(int i=0;i<labels.Length;i++)left.Children.Add(SettingsTableRow(new[]{labels[i],OriginalStateText(smart,keys[i],"—")+units[i]},new[]{2.0,1.0},false));
   right.Children.Add(SettingsTableRow(new[]{"固件",OriginalStateText(entry,"firmware","—")},new[]{1.0,1.0},false));right.Children.Add(SettingsTableRow(new[]{"接口",OriginalStateText(entry,"interface","—")},new[]{1.0,1.0},false));right.Children.Add(SettingsTableRow(new[]{"传输模式","—"},new[]{1.0,1.0},false));right.Children.Add(SettingsTableRow(new[]{"标准",smart==null?"—":"NVM Express SMART / Health"},new[]{1.0,1.0},false));
   frame.Children.Add(new Border{Height=1,Background=border,Margin=new Thickness(0,25,0,12)});frame.Children.Add(Label("S.M.A.R.T",13,muted));string[] names={"01  Critical Warning","03  Available Spare","05  Percentage Used","0B  Power Cycles","0D  Unsafe Shutdowns","0F  Number of Error Information Log Entries"};string[] fields={"criticalWarning","availableSpare","percentageUsed","powerCycles","unsafeShutdowns","errorLogEntries"};for(int i=0;i<names.Length;i++)frame.Children.Add(SettingsTableRow(new[]{names[i],OriginalStateText(smart,fields[i],"—")+(i==1||i==2?" %":"")},new[]{4.0,1.0},false));
   if(entry!=null&&smart==null)frame.Children.Add(Paragraph("磁盘身份信息已读回；此权限或接口未返回 SMART 数据。",12,muted));
  }
  async Task ReadStorage(){if(busy||closing||render)return;busy=true;status.Text="正在读取磁盘状态…";try{var reply=await storageWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));storageDisks=GearArray(reply.data,"disks");storageDiskIndex=0;storageMessage=DateTime.Now.ToString("HH:mm:ss")+" · "+(storageDisks==null?0:storageDisks.Length)+" 个磁盘";Log("磁盘状态已读取。");}catch(Exception ex){storageDisks=null;storageMessage=ex.Message;Log("磁盘读取失败："+ex.Message);}finally{busy=false;if(!closing)ShowPage(page);}}
 }
}
