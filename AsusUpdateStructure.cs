using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  AsusCatalogResult asusCatalog;
  bool asusCatalogChecking;
  int asusCatalogOsId=DefaultAsusCatalogOsId();
  string asusCatalogMessage="";

  void OriginalToolsPage() {
   Header("工具","");
   var entry=Button("驱动程序更新",()=>ShowPage("驱动程序更新"));
   entry.HorizontalAlignment=HorizontalAlignment.Left;entry.MinWidth=180;content.Children.Add(entry);
   var power=Button("Windows 电源",()=>ShowPage("Windows 电源"));power.HorizontalAlignment=HorizontalAlignment.Left;power.MinWidth=180;content.Children.Add(power);
   var display=Button("屏幕设置",()=>ShowPage("屏幕设置"));display.HorizontalAlignment=HorizontalAlignment.Left;display.MinWidth=180;content.Children.Add(display);
   var battery=Button("电池信息",()=>ShowPage("电池信息"));battery.HorizontalAlignment=HorizontalAlignment.Left;battery.MinWidth=180;content.Children.Add(battery);
  }

  static int DefaultAsusCatalogOsId() {
   var version=Environment.OSVersion.Version;
   return version.Major==10?(version.Build>=22000?52:45):0;
  }

  void OriginalAsusDriverUpdatePage() {
   Header("驱动程序更新","");
   string model=asusCatalog==null?AsusDisplayValue(info.Board):AsusDisplayValue(asusCatalog.Model);
   string bios=asusCatalog==null?AsusDisplayValue(info.Bios):AsusDisplayValue(asusCatalog.InstalledBios);
   var identity=Paragraph(model,19,text);identity.FontWeight=FontWeights.SemiBold;identity.Margin=new Thickness(0,0,0,8);content.Children.Add(identity);
   content.Children.Add(Paragraph("当前 BIOS："+bios,13,muted));

   var commands=new WrapPanel{Margin=new Thickness(0,8,0,18),VerticalAlignment=VerticalAlignment.Center};
   var check=Button(asusCatalogChecking?"正在检查…":"检查官网目录",async()=>await CheckAsusDriverCatalog(),true);
   check.IsEnabled=!asusCatalogChecking&&!busy&&!closing&&!render&&asusCatalogOsId>0;check.Margin=new Thickness(0,0,14,8);commands.Children.Add(check);
   if(asusCatalog!=null&&asusCatalog.OperatingSystems!=null&&asusCatalog.OperatingSystems.Length!=0) {
    var selector=new ComboBox{MinWidth=190,MaxWidth=320,Margin=new Thickness(0,0,0,8),VerticalContentAlignment=VerticalAlignment.Center,IsEnabled=!asusCatalogChecking&&!closing&&!render};
    foreach(var os in asusCatalog.OperatingSystems) {
     var item=new ComboBoxItem{Content=os.Name,Tag=os.Id};selector.Items.Add(item);if(os.Id==asusCatalogOsId)selector.SelectedItem=item;
    }
    selector.SelectionChanged+=(s,e)=>{
     var selectedOs=selector.SelectedItem as ComboBoxItem;if(selectedOs==null||!(selectedOs.Tag is int))return;
     int next=(int)selectedOs.Tag;if(next==asusCatalogOsId||asusCatalogChecking||closing||render)return;
     asusCatalogOsId=next;asusCatalogMessage="已选择 "+Convert.ToString(selectedOs.Content)+"，请重新检查目录。";ShowPage(page);
    };
    commands.Children.Add(selector);
   }else {
    var os=Paragraph(asusCatalogOsId==52?"Windows 11 64-bit":asusCatalogOsId==45?"Windows 10 64-bit":"当前 Windows 版本未识别",13,muted);
    os.Margin=new Thickness(0,10,0,8);commands.Children.Add(os);
   }
   content.Children.Add(commands);
   if(!String.IsNullOrWhiteSpace(asusCatalogMessage))content.Children.Add(Paragraph(asusCatalogMessage,13,muted));
   if(asusCatalog==null) {
    content.Children.Add(Paragraph(asusCatalogChecking?"正在读取此型号的官方 BIOS 与驱动目录。":"检查后显示此型号的官方 BIOS、固件和驱动程序。",13,muted));return;
   }
   string when=AsusCheckedTime(asusCatalog.CheckedAtUtc);
   content.Children.Add(Paragraph("检查时间："+when+" · "+asusCatalog.OperatingSystem,11,muted));
   if(asusCatalog.OsId!=asusCatalogOsId)content.Children.Add(Paragraph("下列内容仍为上次检查的 "+asusCatalog.OperatingSystem+" 目录。",12,muted));

   OriginalAsusBiosCatalog(asusCatalog.BiosAndFirmware??new AsusCatalogFile[0]);
   OriginalAsusDriverCatalog(asusCatalog.DriversAndTools??new AsusCatalogFile[0]);
   content.Children.Add(Paragraph("官方下载链接将在浏览器中打开。",11,muted));
  }

  async Task CheckAsusDriverCatalog() {
   if(asusCatalogChecking||busy||closing||render||asusCatalogOsId<=0)return;
   int requestedOs=asusCatalogOsId;asusCatalogChecking=true;asusCatalogMessage="";ShowPage(page);
   try {
    AsusCatalogResult result=await AsusDriverCatalog.ReadLocalAsync(requestedOs);if(closing)return;
    asusCatalog=result;asusCatalogOsId=result.OsId;
    asusCatalogMessage="已读取官网目录。";
   }catch(Exception ex) {
    if(closing)return;asusCatalog=null;asusCatalogMessage="检查失败："+ex.GetBaseException().Message;
   }finally {
    asusCatalogChecking=false;if(!closing&&page=="驱动程序更新")ShowPage(page);
   }
  }

  void OriginalAsusBiosCatalog(AsusCatalogFile[] files) {
   var section=new StackPanel{Margin=new Thickness(0,22,0,0)};
   section.Children.Add(Label("BIOS 与固件",18,text,FontWeights.SemiBold));
   AsusCatalogFile latest=LatestFormalAsusBios(files);var biosFiles=files.Where(file=>file.IsSystemBios).ToArray();
   if(latest!=null) {
    string state=AsusComparisonLabel(latest);
    section.Children.Add(Paragraph("官网正式 BIOS："+latest.Version+(state=="—"?"":" · "+state),14,latest.Comparison=="newerAvailable"?accent:text));
    section.Children.Add(AsusCatalogTable(new[]{latest}));
    if(!String.IsNullOrWhiteSpace(latest.Description))section.Children.Add(AsusCatalogDisclosure("版本说明",Paragraph(latest.Description,12,muted)));
   }else if(biosFiles.Length!=0) {
    section.Children.Add(Paragraph("官网 BIOS 版本尚不能确定先后顺序。",12,muted));
   }
   var history=biosFiles.Where(file=>!Object.ReferenceEquals(file,latest)).ToArray();
   if(history.Length!=0)section.Children.Add(AsusCatalogDisclosure("BIOS 历史与其他版本（"+history.Length+"）",AsusCatalogTable(history)));
   var firmware=files.Where(file=>!file.IsSystemBios).ToArray();
   if(firmware.Length!=0){var title=Label("设备固件",14,text,FontWeights.SemiBold);title.Margin=new Thickness(0,20,0,8);section.Children.Add(title);section.Children.Add(AsusCatalogTable(firmware));}
   if(files.Length==0)section.Children.Add(Paragraph("此型号暂无 BIOS 或固件目录。",12,muted));
   content.Children.Add(section);
  }

  void OriginalAsusDriverCatalog(AsusCatalogFile[] files) {
   var section=new StackPanel{Margin=new Thickness(0,28,0,0)};section.Children.Add(Label("驱动程序与工具",18,text,FontWeights.SemiBold));
   section.Children.Add(Paragraph("本机驱动版本未比较，以官网列出的适用型号和系统为准。",12,muted));
   foreach(var group in files.GroupBy(file=>file.Category)) {
    var heading=Label(group.Key,14,text,FontWeights.SemiBold);heading.Margin=new Thickness(0,18,0,10);section.Children.Add(heading);
    // ASUS can offer several hardware variants or package versions in one group.
    // Preserve those entries rather than selecting a guessed installed adapter.
    var entries=group.ToArray();section.Children.Add(AsusCatalogTable(new[]{entries[0]}));
    if(entries.Length>1)section.Children.Add(AsusCatalogDisclosure("此类别的其他官方条目（"+(entries.Length-1)+"）",AsusCatalogTable(entries.Skip(1).ToArray())));
   }
   if(files.Length==0)section.Children.Add(Paragraph("此系统暂无驱动程序目录。",12,muted));content.Children.Add(section);
  }

  UIElement AsusCatalogTable(AsusCatalogFile[] entries) {
   var table=new StackPanel();
   table.Children.Add(SettingsTableRow(new[]{"名称","官网版本","日期","本机版本",""},new[]{3.4,1.0,1.3,1.0,1.1},true));
   foreach(var file in entries) {
    var row=new Grid{Margin=new Thickness(14,0,14,0)};double[] widths={3.4,1.0,1.3,1.0,1.1};
    foreach(double width in widths)row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});
    string[] values={file.Title,AsusDisplayValue(file.Version),AsusDisplayValue(file.ReleaseDate),file.IsSystemBios?AsusDisplayValue(file.InstalledVersion):"—"};
    for(int i=0;i<values.Length;i++) {
     var label=Paragraph(values[i],i==0?13:12,text);label.Margin=new Thickness(0,14,12,14);label.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(label,i);row.Children.Add(label);
    }
    var download=Button("官方下载",()=>OpenAsusCatalogDownload(file.DownloadUrl));download.Padding=new Thickness(8,7,8,7);download.FontSize=12;download.Margin=new Thickness(0,12,0,12);download.VerticalAlignment=VerticalAlignment.Center;
    download.IsEnabled=!closing&&!render&&AsusDownloadLinkAccepted(file.DownloadUrl);Grid.SetColumn(download,4);row.Children.Add(download);
    table.Children.Add(new Border{BorderBrush=border,BorderThickness=new Thickness(0,0,0,1),Child=row});
   }
   return new Border{Background=panel,BorderBrush=border,BorderThickness=new Thickness(1),Child=table};
  }

  UIElement AsusCatalogDisclosure(string title,UIElement body) {
   return new Expander{Header=title,Content=body,IsExpanded=false,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,10,0,4)};
  }
  static AsusCatalogFile LatestFormalAsusBios(IEnumerable<AsusCatalogFile> entries) {
   var candidates=entries.Where(file=>file.IsSystemBios&&file.ReleaseFlag=="1").ToArray();
   if(candidates.Length==0)return null;
   uint parsed;if(candidates.Any(file=>String.IsNullOrEmpty(file.Version)||file.Version.Length>8||!file.Version.All(Char.IsDigit)||!UInt32.TryParse(file.Version,out parsed)))return null;
   return candidates.OrderByDescending(file=>UInt32.Parse(file.Version,CultureInfo.InvariantCulture)).First();
  }
  static string AsusComparisonLabel(AsusCatalogFile file) {
   if(!file.IsSystemBios||file.ReleaseFlag!="1")return "—";
   return file.Comparison=="newerAvailable"?"有可用新版本":file.Comparison=="sameVersion"?"与本机版本相同":file.Comparison=="olderThanInstalled"?"早于本机版本":"本机版本未比较";
  }
  static string AsusDisplayValue(string value) {return String.IsNullOrWhiteSpace(value)||value=="读取中"||value=="未读取"?"—":value;}
  static string AsusCheckedTime(string value) {DateTime parsed;return DateTime.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed)?parsed.ToLocalTime().ToString("yyyy/MM/dd HH:mm"):"—";}
  static bool AsusDownloadLinkAccepted(string value) {try{AsusDriverCatalog.DownloadUri(value);return true;}catch{return false;}}
  void OpenAsusCatalogDownload(string value) {
   if(closing||render)return;
   try{OpenOfficialUrl(AsusDriverCatalog.DownloadUri(value).AbsoluteUri);}catch(Exception ex){Log("官网链接不可用："+ex.Message);}
  }
 }
}
