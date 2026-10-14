using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace RogControl {
 public partial class MainWindow {
  readonly WorkerClient audioEndpointWorker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AudioEndpointWorker.exe"),10000);
  readonly List<Dictionary<string,object>> audioEndpoints=new List<Dictionary<string,object>>();
  readonly Dictionary<string,string> audioEndpointSelection=new Dictionary<string,string>();
  readonly Dictionary<string,SharedWriteReadBinding> audioReadBindings=new Dictionary<string,SharedWriteReadBinding>(StringComparer.Ordinal);
  string audioEndpointMessage="尚未读取 Windows 音频端点。";
  static string AudioReadKey(string flow,string id){return flow+":"+id;}
  static bool AudioStateReady(Dictionary<string,object> state,string flow,string id){return (flow=="render"||flow=="capture")&&OriginalStateText(state,"flow","")==flow&&OriginalStateText(state,"endpointId","")==id&&OriginalStateFlag(state,"active")==true&&OriginalStateFlag(state,"matchConfirmed")==true&&OriginalStateFlag(state,"readbackConfirmed")==true;}
  void RememberAudioRead(Dictionary<string,object> state,object session,long generation) {
   string flow=OriginalStateText(state,"flow",""),id=OriginalStateText(state,"endpointId",""),key=AudioReadKey(flow,id);audioReadBindings.Remove(key);
   if(closing||render||session==null||!Object.ReferenceEquals(session,audioEndpointWorker.SessionIdentity)||generation!=DeviceHotplugGeneration)throw new InvalidOperationException("音频读取连接已改变，请重新读取。");
   if(!AudioStateReady(state,flow,id))throw new InvalidOperationException("音频端点未确认，请重新读取。");
   state["capturedAt"]=DateTime.Now.ToString("o");audioEndpoints.RemoveAll(endpoint=>OriginalStateText(endpoint,"endpointId","")==id&&OriginalStateText(endpoint,"flow","")==flow);audioEndpoints.Add(state);
   audioReadBindings[key]=new SharedWriteReadBinding(state,session,generation,"endpointId",id,"flow",flow);
  }
  void DemandAudioWrite(SharedWriteReadBinding binding,string setting,object value,bool requireSelected) {
   SharedWriteReadBinding receipt;audioReadBindings.TryGetValue(AudioReadKey(binding.Scope,binding.Identity),out receipt);var current=audioEndpoints.FirstOrDefault(endpoint=>Object.ReferenceEquals(endpoint,binding.State));string selected;
   if(!Object.ReferenceEquals(binding,receipt)||requireSelected&&audioEndpointSelection.TryGetValue(binding.Scope,out selected)&&selected!=binding.Identity)throw new InvalidOperationException("音频读取目标已改变，请重新读取。");
   binding.Demand(current,audioEndpointWorker.SessionIdentity,DeviceHotplugGeneration,DeviceHotplugBindingsReady,closing,render,AuraSyncBusy,busy,hardwareControlPolicy.Demand,()=>AudioStateReady(current,binding.Scope,binding.Identity)&&(setting=="mute"?value is bool:setting=="volume"&&value is int&&(int)value>=0&&(int)value<=100),System.Threading.CancellationToken.None);
  }
  // Mount in the original headset Settings/Audio tab for render, and Microphone tab for capture.
  void OriginalHeadsetWindowsAudio(StackPanel target,bool capture) {
   string flow=capture?"capture":"render",heading=capture?"麦克风音量":"播放音量";
   var candidates=audioEndpoints.Where(d=>OriginalStateText(d,"flow","")==flow&&OriginalStateFlag(d,"matchConfirmed")==true).ToList();
   string selectedId;audioEndpointSelection.TryGetValue(flow,out selectedId);var current=candidates.FirstOrDefault(d=>OriginalStateText(d,"endpointId","")==selectedId);
   if(current==null&&candidates.Count==1){current=candidates[0];selectedId=OriginalStateText(current,"endpointId","");audioEndpointSelection[flow]=selectedId;}
   target.Children.Add(Label(heading,18,text));
   if(candidates.Count>1){var choice=new ComboBox{Foreground=System.Windows.Media.Brushes.Black,Background=System.Windows.Media.Brushes.White,Padding=new Thickness(8),Margin=new Thickness(0,12,14,12)};choice.Items.Add(new ComboBoxItem{Content="选择 DELTA II-KJP 音频端点",Tag=""});foreach(var endpoint in candidates){string id=OriginalStateText(endpoint,"endpointId","");var item=new ComboBoxItem{Content=OriginalStateText(endpoint,"name","ROG DELTA II-KJP")+" · "+(id.Length>8?id.Substring(id.Length-8):id),Tag=id};choice.Items.Add(item);if(id==selectedId)choice.SelectedItem=item;}if(choice.SelectedIndex<0)choice.SelectedIndex=0;choice.SelectionChanged+=(s,e)=>{var item=choice.SelectedItem as ComboBoxItem;audioEndpointSelection[flow]=item==null?"":Convert.ToString(item.Tag);ShowPage(page);};target.Children.Add(choice);}
   var row=Row();var read=Button(busy?"正在读取…":"读取 Windows 音频状态",async()=>await ReadOriginalWindowsAudio());read.IsEnabled=!busy&&!render&&!closing;row.Children.Add(read);target.Children.Add(row);
   bool ready=current!=null&&OriginalStateFlag(current,"active")==true&&OriginalStateFlag(current,"readbackConfirmed")==true;
   target.Children.Add(Paragraph(ready?OriginalStateText(current,"name","")+" · Windows 音频端点 · "+AudioCapturedTime(current):audioEndpointMessage,11,muted));
   var volume=FeatureText(ready?Math.Round(Convert.ToDouble(current["volumePercent"],CultureInfo.InvariantCulture),MidpointRounding.AwayFromZero).ToString("0",CultureInfo.InvariantCulture):"");volume.IsEnabled=ready&&!busy&&!render;volume.MaxLength=3;
   var volumeRow=new Grid{Margin=new Thickness(0,12,0,18)};volumeRow.ColumnDefinitions.Add(new ColumnDefinition());volumeRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});volumeRow.Children.Add(FeatureField(heading+"（0–100%）",volume));
   string exactId=selectedId;var apply=Button("应用",async()=>{int value;if(!Int32.TryParse(volume.Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out value)||value<0||value>100){Log("请输入 0 至 100 之间的整数音量。");return;}await SetOriginalWindowsAudioFromRead(current,flow,exactId,"volume",value);});apply.VerticalAlignment=VerticalAlignment.Bottom;apply.IsEnabled=HardwareWritesEnabled&&ready&&!busy;Grid.SetColumn(apply,1);volumeRow.Children.Add(apply);target.Children.Add(volumeRow);
   var mute=new CheckBox{Content=capture?"麦克风静音":"播放静音",IsThreeState=true,IsChecked=ready?OriginalStateFlag(current,"muted"):null,Foreground=text,Margin=new Thickness(0,8,16,8),IsEnabled=ready&&!busy&&!render};var muteRow=Row();muteRow.Margin=new Thickness(0,0,0,18);muteRow.Children.Add(mute);var muteApply=Button("应用",async()=>{if(mute.IsChecked.HasValue)await SetOriginalWindowsAudioFromRead(current,flow,exactId,"mute",mute.IsChecked.Value);});muteApply.IsEnabled=HardwareWritesEnabled&&ready&&!busy;muteRow.Children.Add(muteApply);target.Children.Add(muteRow);
   if(ready)target.Children.Add(Paragraph("Windows 当前音量："+Convert.ToDouble(current["volumePercent"],CultureInfo.InvariantCulture).ToString("0.##",CultureInfo.InvariantCulture)+"%",11,muted));
  }
  static string AudioCapturedTime(IDictionary<string,object> endpoint){object value;DateTime time;return endpoint!=null&&endpoint.TryGetValue("capturedAt",out value)&&DateTime.TryParse(Convert.ToString(value),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out time)?time.ToString("HH:mm:ss"):"未读回";}
  async Task ReadOriginalWindowsAudio() {
   if(busy||AuraSyncBusy||closing||render)return;busy=true;status.Text="正在读取 DELTA II-KJP 音频端点…";
   try{long generation=DeviceHotplugGeneration;audioReadBindings.Clear();var reply=await audioEndpointWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));var items=GearArray(reply.data,"endpoints");var found=items==null?new List<Dictionary<string,object>>():items.OfType<Dictionary<string,object>>().Where(d=>OriginalStateFlag(d,"matchConfirmed")==true).ToList();audioEndpoints.Clear();audioEndpoints.AddRange(found);object session=audioEndpointWorker.SessionIdentity;foreach(var endpoint in found)if(AudioStateReady(endpoint,OriginalStateText(endpoint,"flow",""),OriginalStateText(endpoint,"endpointId","")))RememberAudioRead(endpoint,session,generation);
    foreach(string flow in new[]{"render","capture"}){var list=found.Where(d=>OriginalStateText(d,"flow","")==flow).ToList();string selectedId;audioEndpointSelection.TryGetValue(flow,out selectedId);if(list.Count==1)audioEndpointSelection[flow]=OriginalStateText(list[0],"endpointId","");else if(!list.Any(d=>OriginalStateText(d,"endpointId","")==selectedId))audioEndpointSelection.Remove(flow);}
    audioEndpointMessage=found.Count==0?"未找到正在连接的 ROG DELTA II-KJP 播放或麦克风端点。请使用 2.4GHz 或 USB 音频连接。":"已读取 "+found.Count+" 个 DELTA II-KJP Windows 音频端点。";Log(audioEndpointMessage);
   }catch(Exception ex){audioReadBindings.Clear();audioEndpoints.Clear();audioEndpointSelection.Clear();audioEndpointMessage="读取失败："+ex.Message;Log(audioEndpointMessage);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  // Legacy ID-only callers cannot establish which read produced their draft.
  Task SetOriginalWindowsAudio(string flow,string endpointId,string setting,object value) {if(!busy&&!AuraSyncBusy&&!closing&&!render&&PermitHardwareWrite())Log("请重新读取并选择音频端点后应用。");return Task.FromResult(0);}
  async Task SetOriginalWindowsAudioFromRead(Dictionary<string,object> source,string flow,string endpointId,string setting,object value) {
   if(busy||AuraSyncBusy||closing||render)return;if(!PermitHardwareWrite())return;var current=audioEndpoints.FirstOrDefault(d=>OriginalStateText(d,"endpointId","")==endpointId&&OriginalStateText(d,"flow","")==flow&&OriginalStateFlag(d,"matchConfirmed")==true&&OriginalStateFlag(d,"readbackConfirmed")==true);SharedWriteReadBinding binding;if(source==null||!Object.ReferenceEquals(source,current)||!audioReadBindings.TryGetValue(AudioReadKey(flow,endpointId),out binding)){Log("请先读取并选择此耳机的音频端点。");return;}
   busy=true;status.Text="正在应用 DELTA II-KJP Windows 音量设置…";
   try{DemandAudioWrite(binding,setting,value,true);var reply=await audioEndpointWorker.Request<SdkReply>(new Dictionary<string,object>{{"op","set"},{"flow",flow},{"endpointId",endpointId},{"setting",setting},{"value",value},{"userInitiated",true}},binding.Session,10000,()=>DemandAudioWrite(binding,setting,value,true));if(!reply.ok)throw new InvalidOperationException(SdkError(reply.error));if(reply.data==null||!AudioStateReady(reply.data,flow,endpointId))throw new InvalidOperationException("音频端点读回不匹配，请重新读取。");RememberAudioRead(reply.data,binding.Session,binding.Generation);audioEndpointMessage=OriginalStateText(reply.data,"appliedState","")=="alreadySet"?"Windows 音频端点已是此设置。":OriginalStateFlag(reply.data,"writeConfirmed")==true?"Windows 音频端点设置已读回确认。":"设置已发送；Windows 读回尚未匹配。";Log(audioEndpointMessage);
   }catch(Exception ex){audioReadBindings.Remove(AudioReadKey(flow,endpointId));audioEndpoints.Remove(current);audioEndpointMessage="应用失败："+ex.Message;Log(audioEndpointMessage);}finally{busy=false;if(!closing)ShowPage(page);}
  }
  public void DisposeOriginalWindowsAudio(){audioReadBindings.Clear();audioEndpointWorker.Dispose();}
 }
}
