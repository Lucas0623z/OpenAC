using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RogControl {
 public sealed class ScenarioApplication {public string Name{get;set;}public string Executable{get;set;}public string SteamId{get;set;}}
 public sealed class ScenarioSetting {public string Setting{get;set;}public object Value{get;set;}}
 public sealed class ScenarioDeviceProfile {
  public string Kind{get;set;}public string DeviceId{get;set;}public string Name{get;set;}public DateTime CapturedAt{get;set;}public bool Enabled{get;set;}public bool IncludeLighting{get;set;}public List<ScenarioSetting> Settings{get;set;}
  public ScenarioDeviceProfile(){Enabled=true;IncludeLighting=true;Settings=new List<ScenarioSetting>();}
 }
 public sealed class ScenarioAudioProfile {
  public string Flow{get;set;}public string EndpointId{get;set;}public string Name{get;set;}public bool Enabled{get;set;}public DateTime CapturedAt{get;set;}public double ObservedVolumePercent{get;set;}public int Volume{get;set;}public bool Muted{get;set;}
  public ScenarioAudioProfile(){Enabled=true;}
 }
 public sealed class ScenarioProfile {
  public int Version{get;set;}public string Id{get;set;}public string Name{get;set;}public DateTime SavedAt{get;set;}public List<ScenarioApplication> Applications{get;set;}public List<ScenarioDeviceProfile> Devices{get;set;}public List<ScenarioAudioProfile> Audio{get;set;}
  public ScenarioProfile(){Version=1;Id=Guid.NewGuid().ToString("N");Name="新情境设置档";Applications=new List<ScenarioApplication>();Devices=new List<ScenarioDeviceProfile>();Audio=new List<ScenarioAudioProfile>();}
 }
 public sealed class ScenarioCommand {public string Source,Kind,DeviceId,Flow,Setting,Label;public object Value;}
 public sealed class ScenarioApplyResult {public string Label,Outcome,Message;public bool Confirmed{get{return Outcome=="confirmed"||Outcome=="alreadySet";}}}
 public static class ScenarioRules {
  public const int MaxSettings=100;
  public static readonly string[] HeadsetSettings={"eqLevels","eqEnabled","noiseReductionRaw","noiseReductionEnabled","sideToneLevel","sideToneEnabled","voicePrompt","latencyMs","sleepMinutes","lowBatteryThreshold","lowBatteryVoicePrompt","lightingEffect","lightingColor","brightness","lightingEnabled","auraEnabled"};
  public static readonly string[] MouseSettings={"pollingRate","sleepMinutes","angleSnapping","motionSync","activeDpiStage","dpiStage"};
  public static readonly string[] KeyboardSettings={"pollingRate","sleepMinutes","oledEnabled","oledBrightness","oledMode","oledKpsEnabled","oledPreset","leverMode"};
  static readonly string[] BooleanSettings={"eqEnabled","noiseReductionEnabled","sideToneEnabled","lowBatteryVoicePrompt","lightingEnabled","auraEnabled","angleSnapping","motionSync","oledEnabled","oledKpsEnabled"};
  public static bool Lighting(string setting){return setting=="brightness"||setting=="lightingEffect"||setting=="lightingColor"||setting=="lightingEnabled"||setting=="auraEnabled";}
  public static string Getter(string setting){return setting=="brightness"?"brightnessRaw":setting=="lightingEffect"?"lightingEffectCode":setting=="lightingColor"?"color":setting;}
  public static string[] Supported(string kind){return kind=="headset"?HeadsetSettings:kind=="mouse"?MouseSettings:kind=="keyboard"?KeyboardSettings:new string[0];}
  public static bool Boolean(string setting){return BooleanSettings.Contains(setting);}
  public static string Title(string setting){switch(setting){case "eqLevels":return "均衡器 8 个频段";case "eqEnabled":return "均衡器";case "noiseReductionRaw":return "降噪强度";case "noiseReductionEnabled":return "麦克风降噪";case "sideToneLevel":return "监听音量";case "sideToneEnabled":return "麦克风监听";case "voicePrompt":return "语音提示";case "latencyMs":return "延迟（ms）";case "sleepMinutes":return "休眠时间（分钟，0 为不休眠）";case "lowBatteryThreshold":return "低电量阈值（%）";case "lowBatteryVoicePrompt":return "低电量语音提示";case "lightingEffect":return "灯效编号（1–5）";case "lightingColor":return "颜色 RGB";case "brightness":return "灯光亮度（%）";case "lightingEnabled":return "灯光";case "auraEnabled":return "神光同步";case "pollingRate":return "轮询率（Hz）";case "angleSnapping":return "直线修正";case "motionSync":return "Motion Sync";case "activeDpiStage":return "当前 DPI 档位";case "dpiStage":return "DPI 档位";case "oledEnabled":return "OLED";case "oledBrightness":return "OLED 亮度（%）";case "oledMode":return "OLED 模式";case "oledKpsEnabled":return "OLED 按键速度显示";case "oledPreset":return "OLED 预设";case "leverMode":return "控制旋钮功能";default:return setting;}}
  static bool Text(string value,int max){return !String.IsNullOrWhiteSpace(value)&&value.Length<=max&&!value.Any(c=>Char.IsControl(c));}
  public static int Integer(object value,int min,int max){if(value==null||value is string||value is bool||!(value is int||value is long||value is short||value is byte||value is decimal||value is double||value is float))throw new InvalidDataException("设置值必须是数字整数。");double number=Convert.ToDouble(value,CultureInfo.InvariantCulture);if(Double.IsNaN(number)||Double.IsInfinity(number)||number!=Math.Truncate(number)||number<min||number>max)throw new InvalidDataException("设置值超出允许范围。");return (int)number;}
  static int Option(object value,params int[] options){int number=Integer(value,options.Min(),options.Max());if(!options.Contains(number))throw new InvalidDataException("此设置值不在设备支持的选项中。");return number;}
  public static object CanonicalValue(string kind,string setting,object value){
   if(!Supported(kind).Contains(setting))throw new InvalidDataException("配置档包含未支持的设备设置："+setting);
   if(Boolean(setting)){if(!(value is bool))throw new InvalidDataException("开关设置必须是布尔值。");return value;}
   if(setting=="eqLevels"||setting=="lightingColor"){var values=value as IEnumerable;if(values==null||value is string)throw new InvalidDataException("此设置需要数字数组。");var list=new List<int>();foreach(var entry in values){if(list.Count>=8)throw new InvalidDataException("数组长度无效。");list.Add(Integer(entry,0,setting=="eqLevels"?100:255));}if(list.Count!=(setting=="eqLevels"?8:3))throw new InvalidDataException("均衡器需要 8 个值，RGB 需要 3 个值。");return list.ToArray();}
   if(setting=="dpiStage"){var map=value as IDictionary<string,object>;if(map==null||map.Count!=2||!map.ContainsKey("index")||!map.ContainsKey("dpi"))throw new InvalidDataException("DPI 配置需要 index 和 dpi。");return new Dictionary<string,object>{{"index",Integer(map["index"],0,3)},{"dpi",Integer(map["dpi"],100,65000)}};}
   if(setting=="sleepMinutes")return kind=="headset"?Option(value,0,2,3,5,10,15):Option(value,0,1,2,3,5,10);
   if(setting=="pollingRate")return kind=="keyboard"?Option(value,1000,2000,4000,8000):Option(value,125,250,500,1000,2000,4000,8000);
   if(setting=="lowBatteryThreshold")return Option(value,0,10,20,30,40,50);
   if(setting=="brightness"){int number=Integer(value,0,100);if(number%5!=0)throw new InvalidDataException("灯光亮度需要使用 5% 步长。");return number;}
   if(setting=="latencyMs")return Option(value,40,60,80);
   if(setting=="lightingEffect"||setting=="activeDpiStage")return Integer(value,1,setting=="lightingEffect"?5:4);
   if(setting=="noiseReductionRaw"||setting=="voicePrompt")return Integer(value,0,2);
   if(setting=="sideToneLevel")return Integer(value,0,20);
   if(setting=="oledBrightness")return Integer(value,0,100);if(setting=="oledMode")return Integer(value,0,4);if(setting=="oledPreset")return Integer(value,0,5);if(setting=="leverMode")return Integer(value,0,7);
   throw new InvalidDataException("未支持的配置值。");
  }
  public static void Validate(ScenarioProfile profile){
   Guid id;if(profile==null||profile.Version!=1||!Guid.TryParse(profile.Id,out id)||!Text(profile.Name,120))throw new InvalidDataException("情境设置档格式或名称无效。");
   if(profile.Applications==null||profile.Applications.Count>32||profile.Devices==null||profile.Devices.Count>3||profile.Audio==null||profile.Audio.Count>2)throw new InvalidDataException("情境设置档条目数量无效。");
   var applications=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var app in profile.Applications){if(app==null||!Text(app.Name,120)||(!String.IsNullOrEmpty(app.Executable)&&!GameLibraryRules.AbsoluteExecutable(app.Executable))||(!String.IsNullOrEmpty(app.SteamId)&&!System.Text.RegularExpressions.Regex.IsMatch(app.SteamId,"^[0-9]{1,12}$"))||(String.IsNullOrEmpty(app.Executable)&&String.IsNullOrEmpty(app.SteamId))||!applications.Add((app.Executable??"")+":"+(app.SteamId??"")))throw new InvalidDataException("应用程序关联条目无效或重复。");}
   var kinds=new HashSet<string>();int count=0;
   foreach(var device in profile.Devices){if(device==null||!kinds.Add(device.Kind??"")||Supported(device.Kind).Length==0||!Text(device.DeviceId,128)||!Text(device.Name,120)||device.Settings==null||device.Settings.Count==0||device.Settings.Count>32)throw new InvalidDataException("设备配置条目无效或重复。");var keys=new HashSet<string>();
    foreach(var setting in device.Settings){if(setting==null||!Text(setting.Setting,40))throw new InvalidDataException("设置条目无效。");setting.Value=CanonicalValue(device.Kind,setting.Setting,setting.Value);string key=setting.Setting;if(key=="dpiStage")key+=":"+((IDictionary<string,object>)setting.Value)["index"];if(!keys.Add(key))throw new InvalidDataException("配置档重复定义同一设置。");count++;}
    var rate=device.Settings.FirstOrDefault(s=>s.Setting=="pollingRate");var sync=device.Settings.FirstOrDefault(s=>s.Setting=="motionSync");if(rate!=null&&sync!=null&&(int)rate.Value==8000&&(bool)sync.Value)throw new InvalidDataException("8000 Hz 轮询率不能与 Motion Sync 同时启用。");
    var effect=device.Settings.FirstOrDefault(s=>s.Setting=="lightingEffect");if(effect!=null&&(int)effect.Value>=4&&device.Settings.Any(s=>s.Setting=="lightingColor"))throw new InvalidDataException("循环/彩虹灯效不包含固定颜色，请移除颜色项。");
   }
   var flows=new HashSet<string>();var endpoints=new HashSet<string>(StringComparer.Ordinal);foreach(var audio in profile.Audio){if(audio==null||!flows.Add(audio.Flow??"")||!endpoints.Add(audio.EndpointId??"")||(audio.Flow!="render"&&audio.Flow!="capture")||!Text(audio.EndpointId,2048)||!Text(audio.Name,240)||audio.Volume<0||audio.Volume>100||Double.IsNaN(audio.ObservedVolumePercent)||Double.IsInfinity(audio.ObservedVolumePercent)||audio.ObservedVolumePercent<0||audio.ObservedVolumePercent>100)throw new InvalidDataException("Windows 音频端点配置无效。");count+=2;}
   if(count>MaxSettings)throw new InvalidDataException("情境设置档最多包含 100 个设置。");
  }
  public static List<ScenarioCommand> BuildPlan(ScenarioProfile source){var profile=ScenarioRepository.CloneRaw(source);Validate(profile);var plan=new List<ScenarioCommand>();foreach(var device in profile.Devices.Where(d=>d.Enabled)){
   var settings=device.Settings.Where(s=>device.IncludeLighting||!Lighting(s.Setting)).ToList();var sync=settings.FirstOrDefault(s=>s.Setting=="motionSync");if(sync!=null&&Object.Equals(sync.Value,false)){settings.Remove(sync);settings.Insert(0,sync);}if(sync!=null&&Object.Equals(sync.Value,true)){settings.Remove(sync);settings.Add(sync);}
   // Effect is set before RGB; every worker request still obtains a fresh current getter.
   settings=settings.OrderBy(s=>s.Setting=="lightingEffect"?-1:0).ToList();foreach(var setting in settings)plan.Add(new ScenarioCommand{Source="gear",Kind=device.Kind,DeviceId=device.DeviceId,Setting=setting.Setting,Value=setting.Value,Label=device.Name+" · "+Title(setting.Setting)});
  }foreach(var audio in profile.Audio.Where(a=>a.Enabled)){plan.Add(new ScenarioCommand{Source="audio",Flow=audio.Flow,DeviceId=audio.EndpointId,Setting="volume",Value=audio.Volume,Label=(audio.Flow=="capture"?"麦克风":"播放")+" · Windows 音量"});plan.Add(new ScenarioCommand{Source="audio",Flow=audio.Flow,DeviceId=audio.EndpointId,Setting="mute",Value=audio.Muted,Label=(audio.Flow=="capture"?"麦克风":"播放")+" · Windows 静音"});}return plan;}
  static object Get(IDictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)?value:null;}
  public static ScenarioDeviceProfile Capture(string kind,IDictionary<string,object> state,out string warning){warning=null;if(!Object.Equals(Get(state,"online"),true)||!Object.Equals(Get(state,"readbackConfirmed"),true))throw new InvalidDataException("设备未在线或设置未确认，无法捕获配置。");var snapshot=Get(state,"snapshot") as IDictionary<string,object>;var capabilities=Get(state,"setCapabilities") as IEnumerable;if(snapshot==null||capabilities==null)throw new InvalidDataException("设备没有可捕获的设置。");var allowed=new HashSet<string>(capabilities.Cast<object>().Select(v=>Convert.ToString(v)));var profile=new ScenarioDeviceProfile{Kind=kind,DeviceId=Convert.ToString(Get(state,"deviceId")),Name=Convert.ToString(Get(state,"model")),CapturedAt=DateTime.Now};if(String.IsNullOrWhiteSpace(profile.Name))profile.Name=kind;var skipped=new List<string>();
   bool unsafeMousePair=false;if(kind=="mouse"&&Object.Equals(Get(snapshot,"motionSync"),true)){try{unsafeMousePair=Integer(Get(snapshot,"pollingRate"),125,8000)==8000;}catch(InvalidDataException){}}
   if(unsafeMousePair&&(allowed.Contains("pollingRate")||allowed.Contains("motionSync")))skipped.Add("8000 Hz 轮询率与已开启的 Motion Sync 无法安全切换，已同时跳过这两项");
   foreach(string setting in Supported(kind).Where(allowed.Contains)){if(unsafeMousePair&&(setting=="pollingRate"||setting=="motionSync"))continue;try{if(setting=="dpiStage"){var x=Get(snapshot,"dpiStages") as IEnumerable;var y=Get(snapshot,"dpiStagesY") as IEnumerable;if(x==null||y==null)continue;var xs=x.Cast<object>().ToArray();var ys=y.Cast<object>().ToArray();if(xs.Length!=4||ys.Length!=4)continue;for(int i=0;i<4;i++){if(Integer(xs[i],100,65000)!=Integer(ys[i],100,65000)){skipped.Add("独立 X/Y DPI");continue;}profile.Settings.Add(new ScenarioSetting{Setting=setting,Value=CanonicalValue(kind,setting,new Dictionary<string,object>{{"index",i},{"dpi",xs[i]}})});}continue;}profile.Settings.Add(new ScenarioSetting{Setting=setting,Value=CanonicalValue(kind,setting,Get(snapshot,Getter(setting)))});}catch(InvalidDataException){skipped.Add(Title(setting));}}
   if(skipped.Count>0)warning="未捕获："+String.Join("、",skipped.Distinct());if(profile.Settings.Count==0)throw new InvalidDataException("没有已确认且支持切换的设置。"+(warning==null?"":" "+warning));Validate(new ScenarioProfile{Devices=new List<ScenarioDeviceProfile>{profile}});return profile;
  }
  public static ScenarioAudioProfile CaptureAudio(IDictionary<string,object> endpoint){if(!Object.Equals(Get(endpoint,"active"),true)||!Object.Equals(Get(endpoint,"matchConfirmed"),true)||!Object.Equals(Get(endpoint,"readbackConfirmed"),true)||!(Get(endpoint,"muted") is bool))throw new InvalidDataException("音频端点未确认，无法捕获。");object raw=Get(endpoint,"volumePercent");if(raw==null||raw is string||raw is bool)throw new InvalidDataException("音频音量未确认。");double volume=Convert.ToDouble(raw,CultureInfo.InvariantCulture);var profile=new ScenarioAudioProfile{Flow=Convert.ToString(Get(endpoint,"flow")),EndpointId=Convert.ToString(Get(endpoint,"endpointId")),Name=Convert.ToString(Get(endpoint,"name")),ObservedVolumePercent=volume,Volume=(int)Math.Round(volume,MidpointRounding.AwayFromZero),Muted=(bool)Get(endpoint,"muted"),CapturedAt=DateTime.Now};Validate(new ScenarioProfile{Audio=new List<ScenarioAudioProfile>{profile}});return profile;}
 }
 public sealed class ScenarioRepository {
  public string DirectoryPath{get;private set;}public ScenarioRepository(string path){DirectoryPath=path;}
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32};}
  public static ScenarioProfile CloneRaw(ScenarioProfile profile){if(profile==null)throw new InvalidDataException("配置档为空。");return Json().Deserialize<ScenarioProfile>(Json().Serialize(profile));}
  public static string Encode(ScenarioProfile source){var profile=CloneRaw(source);ScenarioRules.Validate(profile);return Json().Serialize(profile);}
  public static ScenarioProfile Decode(string text){if(text==null||text.Length>1048576)throw new InvalidDataException("文件过大。");ScenarioProfile profile;try{ValidateRaw(Json().Deserialize<Dictionary<string,object>>(text));profile=Json().Deserialize<ScenarioProfile>(text);}catch(Exception ex){throw new InvalidDataException("配置档无法读取。",ex);}ScenarioRules.Validate(profile);return profile;}
  static IDictionary<string,object> Fields(object value,params string[] fields){var map=value as IDictionary<string,object>;if(map==null||fields.Any(f=>!map.ContainsKey(f)))throw new InvalidDataException("配置档缺少必需字段。");return map;}
  static IEnumerable<object> Entries(object value){var values=value as IEnumerable;if(values==null||value is string||value is IDictionary<string,object>)throw new InvalidDataException("配置档列表字段无效。");return values.Cast<object>();}
  static void Flag(IDictionary<string,object> map,string name){if(!(map[name] is bool))throw new InvalidDataException("配置档开关字段必须是布尔值。");}
  static void ValidateRaw(IDictionary<string,object> raw){var root=Fields(raw,"Version","Id","Name","Applications","Devices","Audio");if(ScenarioRules.Integer(root["Version"],1,1)!=1)throw new InvalidDataException("配置档版本无效。");foreach(var app in Entries(root["Applications"]))Fields(app,"Name");foreach(var device in Entries(root["Devices"])){var d=Fields(device,"Kind","DeviceId","Name","Enabled","IncludeLighting","Settings");Flag(d,"Enabled");Flag(d,"IncludeLighting");foreach(var setting in Entries(d["Settings"]))Fields(setting,"Setting","Value");}foreach(var endpoint in Entries(root["Audio"])){var a=Fields(endpoint,"Flow","EndpointId","Name","Enabled","ObservedVolumePercent","Volume","Muted");Flag(a,"Enabled");Flag(a,"Muted");ScenarioRules.Integer(a["Volume"],0,100);if(a["ObservedVolumePercent"]==null||a["ObservedVolumePercent"] is bool||a["ObservedVolumePercent"] is string)throw new InvalidDataException("音量读数无效。");}}
  public static ScenarioProfile Read(string path){if(new FileInfo(path).Length>1048576)throw new InvalidDataException("文件过大。");return Decode(File.ReadAllText(path,Encoding.UTF8));}
  public static void Write(string path,ScenarioProfile profile){string data=Encode(profile),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,data,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  public void Save(ScenarioProfile profile){ScenarioRules.Validate(profile);Directory.CreateDirectory(DirectoryPath);Write(Path.Combine(DirectoryPath,Guid.Parse(profile.Id).ToString("N")+".rogscenario.json"),profile);}
  public List<ScenarioProfile> List(out string warning){warning=null;var list=new List<ScenarioProfile>();if(!Directory.Exists(DirectoryPath))return list;foreach(string path in Directory.GetFiles(DirectoryPath,"*.rogscenario.json").Take(200)){try{list.Add(Read(path));}catch{warning="部分本地设置档无法读取，已保留原文件。";}}return list.OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase).ToList();}
 }
 public static class ScenarioExecutor {
  public static async Task<List<ScenarioApplyResult>> RunAsync(ScenarioProfile profile,Func<List<ScenarioCommand>,CancellationToken,Task> preflight,Func<ScenarioCommand,Task<ScenarioApplyResult>> send,CancellationToken cancel,Action<ScenarioApplyResult> progress=null){
   // Every software value is validated before the first preflight/read or set delegate.
   var plan=ScenarioRules.BuildPlan(profile);if(plan.Count==0)throw new InvalidDataException("请选择至少一个已捕获的设备配置。");cancel.ThrowIfCancellationRequested();await preflight(plan,cancel);var results=new List<ScenarioApplyResult>();
   foreach(var command in plan){ScenarioApplyResult result;if(cancel.IsCancellationRequested)result=new ScenarioApplyResult{Label=command.Label,Outcome="cancelled",Message="已停止，未发送"};else try{result=await send(command);if(result==null)throw new InvalidOperationException("没有返回设备确认。");result.Label=command.Label;}catch(Exception ex){result=new ScenarioApplyResult{Label=command.Label,Outcome="failed",Message=ex.Message};}results.Add(result);if(progress!=null)progress(result);}
   return results;
  }
 }
}
