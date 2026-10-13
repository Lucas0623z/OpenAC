using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  Dictionary<string,object> localUpdateCache;
  UIElement LocalUpdatePanel(){
   if(localUpdateCache==null)localUpdateCache=LocalUpdateCatalog.Read();var body=new StackPanel();
   var actions=Row();var refresh=Button("重新读取本机缓存",()=>{localUpdateCache=LocalUpdateCatalog.Read();ShowPage(page);});refresh.IsEnabled=!busy;actions.Children.Add(refresh);body.Children.Add(actions);
   body.Children.Add(Paragraph("下列信息来自本机官方缓存。缓存日期和服务器版本可能过期，不能用于判断最新；在线检查、下载与安装尚未接通。",12,muted));
   var dependencies=localUpdateCache["dependencies"] as CachedDeviceDependency[];if(dependencies!=null){
    int known=dependencies.Sum(d=>d.devicePageRequirements.Count(r=>r.minimumStatus=="meets_cached_minimum")),below=dependencies.Sum(d=>d.devicePageRequirements.Count(r=>r.minimumStatus=="below_cached_minimum")),unknown=dependencies.Sum(d=>d.devicePageRequirements.Count(r=>r.minimumStatus=="unknown"));
    body.Children.Add(Paragraph("缓存依赖："+known+" 项满足最低要求，"+below+" 项低于要求，"+unknown+" 项未知。满足缓存要求不代表组件运行正常。",12,text));
    foreach(var device in dependencies){var list=new StackPanel();list.Children.Add(Paragraph("依赖缓存修改时间（UTC）："+device.cacheModifiedUtc,11,muted));list.Children.Add(SettingsTableRow(new[]{"名称","缓存安装版本","缓存最低要求","状态"},new[]{3.8,1.4,1.4,1.6},true));foreach(var r in device.devicePageRequirements)list.Children.Add(SettingsTableRow(new[]{r.name,r.cachedInstalledVersion??"未知",r.minimumVersion??"未知",r.minimumStatus=="meets_cached_minimum"?"满足缓存要求":r.minimumStatus=="below_cached_minimum"?"低于缓存要求":"未知"},new[]{3.8,1.4,1.4,1.6},false));body.Children.Add(new Expander{Header=device.model+" · 缓存依赖",Content=list,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,12,0,8),IsExpanded=false});}
   }
   var catalog=localUpdateCache["catalog"] as Dictionary<string,object>;object catalogRows;
   if(catalog!=null&&catalog.TryGetValue("items",out catalogRows)&&catalogRows is object[]){var list=new StackPanel();object when;list.Children.Add(Paragraph("组件清单缓存修改时间（UTC）："+(catalog.TryGetValue("cacheModifiedUtc",out when)?Convert.ToString(when):"未知"),11,muted));list.Children.Add(SettingsTableRow(new[]{"设备 / 组件","Module","缓存安装版本","缓存服务器版本"},new[]{3.8,1.6,1.4,1.4},true));foreach(var rawGroup in (object[])catalogRows){var group=(Dictionary<string,object>)rawGroup;foreach(var rawItem in (object[])group["items"]){var item=(Dictionary<string,object>)rawItem;list.Children.Add(SettingsTableRow(new[]{Convert.ToString(group["displayName"]??group["name"]),Convert.ToString(item["module"]),Convert.ToString(item["cachedInstalledVersion"]??"未知"),Convert.ToString(item["cachedServerVersion"]??"未知")},new[]{3.8,1.6,1.4,1.4},false));}}body.Children.Add(new Expander{Header="本机官方缓存组件清单",Content=list,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,12,0,8),IsExpanded=false});}
   var diagnostics=localUpdateCache["diagnostics"] as object[];if(diagnostics!=null&&diagnostics.Length>0){var list=new StackPanel();foreach(var raw in diagnostics){var d=(Dictionary<string,object>)raw;string type=Convert.ToString(d["type"]);list.Children.Add(Paragraph(Convert.ToString(d["timeLocalUnspecified"])+" · "+(type=="msi_exit_code"?"RLS 安装退出码":"原版安装包执行失败码")+" "+Convert.ToString(d["code"])+" · "+Convert.ToString(d["source"]),12,text));}list.Children.Add(Paragraph("以上为限定安装日志中的历史记录。退出码 0 仅说明该次安装返回成功，不能证明之前的更新问题已修复。",11,muted));body.Children.Add(new Expander{Header="历史安装诊断",Content=list,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,12,0,8),IsExpanded=false});}
   var errors=localUpdateCache["errors"] as object[];if(errors!=null)foreach(var raw in errors){var e=(Dictionary<string,object>)raw;body.Children.Add(Paragraph(Convert.ToString(e["source"])+"："+Convert.ToString(e["message"]),12,muted));}
   return new Border{BorderBrush=border,BorderThickness=new Thickness(1),Background=panel,Padding=new Thickness(18,14,18,10),Margin=new Thickness(0,0,0,20),Child=new Expander{Header="本机官方缓存与更新诊断",Content=body,IsExpanded=false,Foreground=text,HorizontalContentAlignment=HorizontalAlignment.Stretch}};
  }
 }
}
