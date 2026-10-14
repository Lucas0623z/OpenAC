// SPDX-License-Identifier: MIT. Manual bounded read only; no fan SET in this UI.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace RogControl {
 public partial class MainWindow {
  readonly WorkerClient fanNativeWorker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FanNativeWorker.exe"),12000);
  Task fanNativeReadTask;Dictionary<string,object> fanNativeSnapshot;string fanNativeReadError="";
  public bool FanNativeCanPreviewReload {get{return fanNativeReadTask==null||fanNativeReadTask.IsCompleted;}}
  static string[] FanNativeNames(Dictionary<string,object> data){
   object raw;if(data==null||!data.TryGetValue("nativePropertyReadConfirmed",out raw)||!Object.Equals(raw,true)||!data.TryGetValue("supportsWrite",out raw)||!Object.Equals(raw,false)||!data.TryGetValue("source",out raw)||!Object.Equals(raw,"AsusFanControlService.COM")||!data.TryGetValue("headers",out raw)||raw is string||raw is IDictionary||!(raw is IEnumerable))throw new InvalidDataException("风扇读取结果无效。");
   var rows=((IEnumerable)raw).Cast<object>().ToArray();object count;if(rows.Length>32||!data.TryGetValue("count",out count)||!(count is int)||(int)count!=rows.Length)throw new InvalidDataException("风扇列表不完整。");
   var names=new List<string>();var identities=new HashSet<long>();
   foreach(var item in rows){var row=item as Dictionary<string,object>;object id,name,writable,rpm;if(row==null||!row.TryGetValue("id",out id)||!(id is int)&&!(id is long)&&!(id is uint)||Convert.ToInt64(id,CultureInfo.InvariantCulture)<0||Convert.ToInt64(id,CultureInfo.InvariantCulture)>UInt32.MaxValue||!identities.Add(Convert.ToInt64(id,CultureInfo.InvariantCulture))||!row.TryGetValue("displayName",out name)||!(name is string)||String.IsNullOrWhiteSpace((string)name)||((string)name).Length>256||((string)name).Any(Char.IsControl)||!row.TryGetValue("supportsWrite",out writable)||!Object.Equals(writable,false)||!row.TryGetValue("rpm",out rpm)||rpm!=null)throw new InvalidDataException("风扇身份或读数无效。");names.Add((string)name);}
   if(names.Distinct(StringComparer.Ordinal).Count()!=names.Count)throw new InvalidDataException("风扇名称重复。");return names.ToArray();
  }
  string[] CurrentFanNativeNames(){if(fanNativeSnapshot==null)return new[]{"CPU Fan","Chassis Fan 1","Chassis Fan 2","Radiator Fan 1","Radiator Fan 2","Water Pump+ 1","Water Pump+ 2","VRM HS Fan"};return FanNativeNames(fanNativeSnapshot);}
  string FanNativeReadDescription {get{return String.IsNullOrEmpty(fanNativeReadError)?fanNativeSnapshot==null?"读取风扇列表。":"列表已读取；当前接口未确认转速和曲线单位。":fanNativeReadError;}}
  async Task ReadFanNative(){if(render||closing||busy||AuraSyncBusy)return;busy=true;fanNativeSnapshot=null;fanNativeReadError="";ShowPage(page);try{
   var task=fanNativeWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"includeCurves",false}},null,12000,()=>{if(render||closing)throw new OperationCanceledException();});fanNativeReadTask=task;var reply=await task;
   if(closing)return;if(reply==null||!reply.ok){var error=reply==null?null:reply.error as Dictionary<string,object>;object code;if(error!=null&&error.TryGetValue("code",out code)&&Object.Equals(code,"ACCESS_DENIED"))throw new InvalidOperationException("需要管理员权限读取 ASUS 风扇服务。");throw new InvalidOperationException("风扇服务读取未完成。");}
   FanNativeNames(reply.data);fanNativeSnapshot=reply.data;var names=CurrentFanNativeNames();if(names.Length>0&&!names.Contains(fanXpertChannel))fanXpertChannel=names[0];
  }catch(Exception error){fanNativeSnapshot=null;fanNativeReadError=error.Message;}finally{fanNativeReadTask=null;busy=false;if(!closing)ShowPage(page);}}
  async Task DisposeFanNativeAsync(){fanNativeWorker.Dispose();var task=fanNativeReadTask;if(task!=null)try{await task;}catch(Exception){}fanNativeSnapshot=null;}
 }
}
