using System;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  OpenAcUpdateResult openAcUpdate;
  bool openAcUpdateChecking;
  string openAcUpdateMessage="";
  void OriginalOpenAcUpdateCenter() {
   var header=new Grid{Margin=new Thickness(0,0,0,24)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var title=Label("OpenAC",20,text,FontWeights.SemiBold);title.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(title);
   var check=Button(openAcUpdateChecking?"正在检查…":"检查更新",async()=>await CheckOpenAcUpdate());check.IsEnabled=!openAcUpdateChecking&&!render&&!closing;check.Margin=new Thickness(0);Grid.SetColumn(check,1);header.Children.Add(check);content.Children.Add(header);
   var body=new StackPanel();body.Children.Add(SettingsTableRow(new[]{"名称","当前版本","最新版本"},new[]{2.0,1.3,1.3},true));
   body.Children.Add(SettingsTableRow(new[]{"OpenAC",GitHubUpdateCatalog.InstalledVersion,openAcUpdate==null?"—":openAcUpdate.Latest.Version},new[]{2.0,1.3,1.3},false));
   content.Children.Add(new Border{Background=panel,BorderBrush=border,BorderThickness=new Thickness(1),Child=body});
   if(!String.IsNullOrEmpty(openAcUpdateMessage)){var message=Paragraph(openAcUpdateMessage,13,openAcUpdate!=null&&!openAcUpdate.UpdateAvailable?green:muted);message.Margin=new Thickness(0,20,0,16);content.Children.Add(message);}
   if(openAcUpdate!=null&&openAcUpdate.UpdateAvailable) {
    var actions=Row();if(!String.IsNullOrEmpty(openAcUpdate.Latest.InstallerUrl))actions.Children.Add(Button("下载更新",()=>OpenOpenAcReleaseLink(true),true));
    actions.Children.Add(Button("查看更新",()=>OpenOpenAcReleaseLink(false)));content.Children.Add(actions);
   }
  }
  void OpenOpenAcReleaseLink(bool installer){try{OpenOfficialUrl(GitHubUpdateCatalog.ValidatedReleaseLink(openAcUpdate,installer));}catch(Exception ex){Log(ex.Message);}}
  async System.Threading.Tasks.Task CheckOpenAcUpdate() {
   if(openAcUpdateChecking||closing||render)return;openAcUpdateChecking=true;openAcUpdateMessage="";ShowPage(page);
   try {
    var result=await GitHubUpdateCatalog.CheckAsync();if(closing)return;openAcUpdate=result;
    openAcUpdateMessage=result.UpdateAvailable?"发现新版本 "+result.Latest.Version+(String.IsNullOrEmpty(result.Latest.InstallerUrl)?"，安装包尚未发布。":"。"):"已是最新版本。";
   }catch(Exception ex){if(closing)return;openAcUpdate=null;openAcUpdateMessage=ex.Message;}
   finally{openAcUpdateChecking=false;if(!closing)ShowPage(page);}
  }
 }
}
