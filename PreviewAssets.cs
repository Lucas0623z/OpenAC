using System;
using System.IO;
using System.Web.Script.Serialization;

namespace RogControl {
 public partial class MainWindow {
  void LoadPreviewAssets(){
   if(String.IsNullOrEmpty(AppDomain.CurrentDomain.GetData("OpenAC.PreviewWorkerRoot") as string))return;
   try{
    string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,".preview","assets.json");
    if(!File.Exists(path)||new FileInfo(path).Length>65536)return;
    var config=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(path));string images;
    if(config==null||!config.TryGetValue("productImageRoot",out images)||String.IsNullOrWhiteSpace(images)||!Path.IsPathRooted(images))return;
    images=Path.GetFullPath(images);if(Directory.Exists(images))AppDomain.CurrentDomain.SetData("OpenAC.ProductImageRoot",images);
   }catch{}
  }
 }
}
