using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RogControl {
 public partial class MainWindow {
  readonly WorkerClient storageWorker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"StorageWorker.exe"),12000);
  object[] storageDisks;int storageDiskIndex;string storageMessage="尚未读取";
  void OriginalStoragePage(){
   // Identity comes from the existing Windows worker. This page does not query
   // storage during construction or infer firmware/protocol versions from SMART.
   var frame=new StackPanel{MaxWidth=920,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,40,0,24),Tag="storage:frame"};content.Children.Add(frame);
   var selector=new Grid{Height=34,Margin=new Thickness(0,0,0,14)};selector.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(512)});selector.ColumnDefinitions.Add(new ColumnDefinition());
   var disks=storageDisks??new object[0];var entry=storageDiskIndex>=0&&storageDiskIndex<disks.Length?disks[storageDiskIndex] as Dictionary<string,object>:null;
   var choice=MousePerformanceCombo(new string[0],-1,disks.Length>0&&!busy&&!closing,512);choice.Tag="storage:disk";choice.Items.Add(new ComboBoxItem{Content="—",Tag=-1});for(int index=0;index<disks.Length;index++)choice.Items.Add(new ComboBoxItem{Content=StorageIdentity(disks[index] as IDictionary<string,object>,"model"),Tag=index});choice.SelectedIndex=entry!=null?storageDiskIndex+1:0;
   choice.SelectionChanged+=(sender,args)=>{if(busy||closing)return;var selected=choice.SelectedItem as ComboBoxItem;if(selected==null||!(selected.Tag is int))return;int index=(int)selected.Tag;if(index<0||index>=disks.Length||index==storageDiskIndex)return;storageDiskIndex=index;ShowPage(page);};selector.Children.Add(choice);
   var read=KeyboardOutlineButton("⟳",async()=>await ReadStorage());read.Width=34;read.Height=34;read.FontSize=20;read.Margin=new Thickness(14,0,0,0);read.HorizontalAlignment=HorizontalAlignment.Left;read.IsEnabled=!busy&&!closing&&!render;read.ToolTip="刷新磁盘状态 · "+storageMessage;read.Tag="storage:refresh";Grid.SetColumn(read,1);selector.Children.Add(read);frame.Children.Add(selector);
   var smart=GearMap(entry,"smart");bool confirmed=OriginalStateFlag(smart,"readbackConfirmed")==true;
   var table=new Grid{Height=196};table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(452,GridUnitType.Star)});table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(32)});table.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(436,GridUnitType.Star)});
   for(int row=0;row<5;row++){table.RowDefinitions.Add(new RowDefinition{Height=new GridLength(36)});if(row<4)table.RowDefinitions.Add(new RowDefinition{Height=new GridLength(4)});}
   string[] labels={"温度","主机读取量总数","主机写入量总数","通电次数","通电时间"};string[] keys={"temperature","hostReadGB","hostWrittenGB","powerCycles","powerOnHours"};string[] units={" °C"," GB"," GB"," 计数"," Hours"};
   string[] identities={"固件","接口","传输模式","盘符","标准"};string[] values={StorageIdentity(entry,"firmware"),StorageIdentity(entry,"interface"),"—","—","—"};
   for(int row=0;row<5;row++) {
    string value=confirmed?StorageSmartValue(smart,keys[row],units[row]):"—";var left=StorageSummaryRow(labels[row],value,true);left.Tag="storage:value:"+keys[row];Grid.SetRow(left,row*2);table.Children.Add(left);
    var right=StorageSummaryRow(identities[row],values[row],false);right.Tag="storage:identity:"+identities[row];Grid.SetRow(right,row*2);Grid.SetColumn(right,2);table.Children.Add(right);
   }
   frame.Children.Add(table);var heading=Label("S.M.A.R.T",14,muted);heading.Margin=new Thickness(0,50,0,0);heading.Height=20;frame.Children.Add(heading);
   string[] names={"01   Critical Warning","03   Available Spare","05   Percentage Used","0B   Power Cycles","0D   Unsafe Shutdowns","0F   Number of Error Information Log Entries"};string[] fields={"criticalWarning","availableSpare","percentageUsed","powerCycles","unsafeShutdowns","errorLogEntries"};string[] smartUnits={""," %"," %"," count"," count",""};
   for(int row=0;row<6;row++){string value=confirmed?StorageSmartValue(smart,fields[row],smartUnits[row]):"—";var line=StorageSmartRow(names[row],value,row%2==1);line.Tag="storage:smart:"+fields[row];if(row==0)line.ToolTip="NVMe Critical Warning 原始位标志";frame.Children.Add(line);}
  }
  static string StorageIdentity(IDictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)&&value is string&&!String.IsNullOrWhiteSpace((string)value)?((string)value).Trim():"—";}
  static string StorageSmartValue(IDictionary<string,object> smart,string key,string unit) {
   object raw;if(smart==null||!smart.TryGetValue(key,out raw)||raw==null||raw is bool)return "—";string value=Convert.ToString(raw,CultureInfo.InvariantCulture).Trim();
   if(key=="temperature"){int number;return Int32.TryParse(value,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out number)&&number>=-73&&number<=177?number.ToString(CultureInfo.InvariantCulture)+unit:"—";}
   if(!Regex.IsMatch(value,"^[0-9]{1,39}$",RegexOptions.CultureInvariant))return "—";value=value.TrimStart('0');if(value.Length==0)value="0";
   if(key=="criticalWarning"||key=="availableSpare"||key=="percentageUsed"){int number;if(!Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out number)||number>255||(key=="availableSpare"&&number>100))return "—";}
   else if(value.Length==39&&String.CompareOrdinal(value,"340282366920938463463374607431768211455")>0)return "—";
   return value+unit;
  }
  Grid StorageSummaryRow(string label,string value,bool shaded) {
   var row=new Grid{Height=36};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(shaded?2.0:206.0,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(shaded?1.0:230.0,GridUnitType.Star)});
   if(shaded){var fill=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 0,0 L 438,0 L 452,14 L 452,36 L 0,36 Z"),Fill=Brush("#1C1C1C"),Stretch=Stretch.Fill};Grid.SetColumnSpan(fill,2);row.Children.Add(fill);}
   var name=Label(label,14,muted);name.Margin=new Thickness(shaded?16:0,0,6,0);name.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(name);var data=Label(value,14,text,shaded?FontWeights.SemiBold:FontWeights.Normal);data.Margin=new Thickness(shaded?6:0,0,4,0);data.VerticalAlignment=VerticalAlignment.Center;data.TextTrimming=TextTrimming.CharacterEllipsis;data.ToolTip=value;Grid.SetColumn(data,1);row.Children.Add(data);return row;
  }
  Grid StorageSmartRow(string label,string value,bool shaded) {
   var row=new Grid{Height=40,Background=shaded?Brush("#1C1C1C"):Brushes.Transparent};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});
   if(shaded){var outline=new Border{BorderBrush=Brush("#555555"),BorderThickness=new Thickness(0.6,0.6,0,0),IsHitTestVisible=false};Grid.SetColumnSpan(outline,2);row.Children.Add(outline);}
   var name=Label(label,14,text);name.Margin=new Thickness(16,0,8,0);name.VerticalAlignment=VerticalAlignment.Center;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=label;row.Children.Add(name);var data=Label(value,14,text);data.Margin=new Thickness(8,0,46,0);data.VerticalAlignment=VerticalAlignment.Center;data.TextAlignment=TextAlignment.Right;data.TextTrimming=TextTrimming.CharacterEllipsis;data.ToolTip=value;Grid.SetColumn(data,1);row.Children.Add(data);return row;
  }
  async Task ReadStorage(){if(busy||closing||render)return;busy=true;status.Text="正在读取磁盘状态…";try{var reply=await storageWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));storageDisks=GearArray(reply.data,"disks");storageDiskIndex=0;storageMessage=DateTime.Now.ToString("HH:mm:ss")+" · "+(storageDisks==null?0:storageDisks.Length)+" 个磁盘";Log("磁盘状态已读取。");}catch(Exception ex){storageDisks=null;storageMessage=ex.Message;Log("磁盘读取失败："+ex.Message);}finally{busy=false;if(!closing)ShowPage(page);}}
 }
}
