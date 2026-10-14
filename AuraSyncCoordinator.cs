// OpenAC-owned RGB capability and ownership coordination. No vendor code,
// native I/O, retries, firmware commands or hardware-default recovery.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RogControl {
 public sealed class AuraSyncTarget {
  public string Id,DeviceId,Backend,Kind,Name,Transport,CapabilitySource,UnavailableReason;
  public bool Online,CanStaticRgb,CanReleaseSoftwareLighting,PhysicalWriteValidated;
  public DateTime ObservedUtc;
  public Dictionary<string,object> State;
  public object WorkerSession;
  public long ConnectionGeneration;
 }
 public sealed class AuraSyncCommand {
  public string TargetId,Backend,Kind,Setting;
  public Dictionary<string,object> Request;
  public object ExpectedSession;
  public long ConnectionGeneration;
  public bool IsAnimationFrame,IsOwnershipReturn;
 }
 public sealed class AuraSyncCommandReply {
  public bool Ok,Accepted,WriteAttempted,WriteConfirmed;
  public string Error;
 }
 public sealed class AuraSyncTargetResult {
  public string TargetId,Name,State,Error;
  public int CommandsCompleted;
  public bool WriteAttempted,Accepted,ReadbackConfirmed;
 }
 public sealed class AuraSyncResult {
  public string Effect,RequestedColor,OutputColor;
  public int Brightness;
  public DateTime CompletedUtc;
  public AuraSyncTargetResult[] Devices;
  public bool AllSubmitted,AllReadbackConfirmed,Cancelled;
 }
 public sealed class AuraOwnershipResult {
  public AuraSyncTarget Target;
  public string State,Error,RestorationState;
  public bool WriteAttempted,OwnershipConfirmed,RestorationConfirmed,CanReturnOriginalFlag;
  public List<string> Steps=new List<string>();
  public object WorkerSession;
  public long ConnectionGeneration;
 }
 public sealed class AuraSyncCoordinator {
  bool running;
  volatile bool stopRequested;
  public bool Running {get{return running;}}
  public void RequestStop(){stopRequested=true;}
  public static AuraSyncTarget Direct(Dictionary<string,object> state,string motherboardName,DateTime when) {
   string id=String(state,"id"),family=String(state,"controllerFamily");
   bool knownPid=family=="motherboard"?new[]{"18F3","1939","19AF","1AA6","1BED"}.Contains(String(state,"productId")):family=="addressable"&&new[]{"1867","1872","18A3","18A5"}.Contains(String(state,"productId"));
   bool ready=IdValid(id)&&String(state,"backend")=="native-hid"&&String(state,"vendorId")=="0B05"&&knownPid&&Strings(state,"effects").Contains("static")&&Integer(state,"zoneCount")>0&&Integer(state,"leds")>0;
   return new AuraSyncTarget{Id="direct:"+id,DeviceId=id,Backend="direct",Kind=family=="addressable"?"addressable":"motherboard",Name=family=="motherboard"?motherboardName:String(state,"name"),Transport="USB",ObservedUtc=when,State=state,Online=ready,CanStaticRgb=ready,PhysicalWriteValidated=Flag(state,"effectWriteValidated")==true,CapabilitySource="Native Aura firmware/configuration and verified static protocol",UnavailableReason=ready?null:"主板灯控未返回有效的灯区与静态颜色能力。"};
  }
  public static AuraSyncTarget Gear(Dictionary<string,object> state,DateTime when) {
   string kind=String(state,"kind"),id=String(state,"deviceId");bool online=Flag(state,"online")==true&&Flag(state,"readbackConfirmed")==true;var snapshot=Map(state,"snapshot");
   bool rgb=Bytes(Value(snapshot,"color"),3)!=null;int? mode=Integer(snapshot,"lightingEffectCode");var caps=Strings(state,"setCapabilities");
   bool ready=online&&IdValid(id)&&kind=="headset"&&Integer(state,"pid")==7489&&mode>=1&&mode<=3&&rgb&&caps.Contains("lightingEffect")&&caps.Contains("lightingColor")&&caps.Contains("brightness")&&Integer(snapshot,"brightnessRaw")>=0&&Integer(snapshot,"brightnessRaw")<=100&&Flag(snapshot,"auraEnabled")==false&&Flag(snapshot,"lightingEnabled").HasValue&&(Flag(snapshot,"lightingEnabled")==true||caps.Contains("lightingEnabled"));
   if(kind=="headset"&&String(state,"controlScope")=="headsetLightingOnly")ready=online&&IdValid(id)&&HeadsetStaticScopeValid(state,snapshot);
   bool canRelease=false;
   if(kind=="keyboard"||kind=="mouse") {
    var lighting=Map(snapshot,"lighting");int? pid=Integer(state,"pid"),current=Integer(lighting,"currentMode");var model=pid.HasValue?GearLinkLightingModelFacts.Lookup(pid.Value):null;bool exact=model!=null&&model.Kind==kind;
    bool validRecipe=kind=="keyboard"?current>=0&&current<=9&&Integer(lighting,"recipeMode")==0&&new int?[]{0,1,16,17}.Contains(Integer(lighting,"flags"))&&Bytes(Value(lighting,"secondaryColor"),3)!=null&&Bytes(Value(lighting,"backgroundColor"),3)!=null:current.HasValue&&new[]{0,1,2,3,4,5,6,240}.Contains(current.Value)&&Integer(lighting,"direction")>=0&&Integer(lighting,"direction")<=1&&Flag(lighting,"random").HasValue;
    bool readable=online&&IdValid(id)&&exact&&(!model.LightingOnly||LightingFamilySnapshotValid(model,snapshot))&&Flag(snapshot,"uniformStaticLightingProtocolAvailable")==true&&Bytes(Value(lighting,"color"),3)!=null&&Integer(lighting,"brightness")>=0&&Integer(lighting,"brightness")<=100&&Integer(lighting,"speed")>=0&&Integer(lighting,"speed")<=255&&Integer(lighting,"area")==model.PrimaryArea&&validRecipe&&Flag(snapshot,"windowsLightingEnabled")==false&&Flag(snapshot,"lightingSoftwareMode").HasValue;
    ready=readable&&Flag(snapshot,"uniformStaticLightingReady")==true&&caps.Contains("staticLighting")&&Flag(snapshot,"lightingSoftwareMode")==false;
    canRelease=online&&IdValid(id)&&exact&&Flag(snapshot,"windowsLightingEnabled")==false&&Flag(snapshot,"lightingSoftwareMode")==true&&caps.Contains("lightingSoftwareMode")&&(model.LightingOnly?LightingFamilySnapshotValid(model,snapshot):kind=="keyboard"||readable);
   }
   string reason=!online?"设备未在线或连接尚未确认。":kind!="headset"?"当前适配器未提供此型号的已验证 RGB 读写能力。":mode==0?"当前灯效代码 0 的颜色模式尚未确认。":Flag(snapshot,"auraEnabled")!=false?"专属 Aura 模式的直接颜色路由尚未确认。":"颜色、亮度或灯效模式未完整读回。";
   if(canRelease)reason=kind=="keyboard"&&Map(snapshot,"lighting")==null?"静态颜色查询被设备拒绝；可手动尝试退出软件灯控后重新读取，仍失败则恢复原标志。":"软件灯控模式仍开启；需手动接管并确认读回后才能同步。";
   else if(online&&(kind=="keyboard"||kind=="mouse"))reason=Map(snapshot,"lighting")==null?"设备未返回可确认的颜色配置。":Flag(snapshot,"windowsLightingEnabled")==true?"Windows 灯控仍开启，当前接口不会修改其归属。":"灯光配置或控制归属未完整读回。";
   return new AuraSyncTarget{Id="gear:"+kind+":"+id,DeviceId=id,Backend="gear",Kind=kind,Name=String(state,"model"),Transport=String(state,"transport"),ObservedUtc=when,State=state,Online=online,CanStaticRgb=ready,CanReleaseSoftwareLighting=canRelease,CapabilitySource="GearLink online response and per-setting getter capabilities",UnavailableReason=ready?null:reason,PhysicalWriteValidated=false};
  }
  static bool LightingFamilyRecipeValid(GearLinkLightingModelRecord model,Dictionary<string,object> recipe){
   int? mode=Integer(recipe,"currentMode"),brightness=Integer(recipe,"brightness"),speed=Integer(recipe,"speed");if(recipe==null||String(recipe,"source")!=model.Family||!mode.HasValue||brightness<0||brightness>100||!brightness.HasValue||speed<0||speed>255||!speed.HasValue||Bytes(Value(recipe,"color"),3)==null)return false;
   if(model.Kind=="mouse")return new[]{0,1,2,3,4,5,6,240}.Contains(mode.Value)&&Integer(recipe,"direction")>=0&&Integer(recipe,"direction")<=1&&Flag(recipe,"random").HasValue;
   return mode>=0&&mode<=9&&Integer(recipe,"recipeMode")==0&&new int?[]{0,1,16,17}.Contains(Integer(recipe,"flags"))&&Integer(recipe,"direction")==255&&Integer(recipe,"width")==255&&Bytes(Value(recipe,"secondaryColor"),3)!=null&&Bytes(Value(recipe,"backgroundColor"),3)!=null;
  }
  static bool LightingFamilyRecipeSame(GearLinkLightingModelRecord model,Dictionary<string,object> a,Dictionary<string,object> b){
   if(!LightingFamilyRecipeValid(model,a)||!LightingFamilyRecipeValid(model,b))return false;
   foreach(string key in model.Kind=="mouse"?new[]{"area","currentMode","brightness","speed","direction"}:new[]{"area","currentMode","recipeMode","brightness","speed","flags","direction","width"})if(Integer(a,key)!=Integer(b,key))return false;
   if(model.Kind=="mouse"&&Flag(a,"random")!=Flag(b,"random"))return false;
   foreach(string key in model.Kind=="mouse"?new[]{"color"}:new[]{"color","secondaryColor","backgroundColor"})if(!Bytes(Value(a,key),3).SequenceEqual(Bytes(Value(b,key),3)))return false;return true;
  }
  static bool LightingFamilySnapshotValid(GearLinkLightingModelRecord model,Dictionary<string,object> snapshot){
   if(model==null||!model.LightingOnly||snapshot==null||Integer(snapshot,"modelPid")!=model.Pid||String(snapshot,"lightingFamily")!=model.Family||Flag(snapshot,"lightingProtocolPrepared")!=true)return false;
   int? count=Integer(snapshot,"profileCount"),profile=Integer(snapshot,"activeProfile");if(!count.HasValue||count<1||count>255||!profile.HasValue||profile<(model.Kind=="mouse"?1:0)||profile>count)return false;
   var raw=Value(snapshot,"lightingAreas") as IEnumerable;if(raw==null||raw is string||raw is IDictionary)return false;var rows=raw.Cast<object>().Take(4).Select(v=>v as Dictionary<string,object>).ToArray();var areas=model.Areas;if(rows.Length!=areas.Length)return false;
   for(int i=0;i<areas.Length;i++)if(Integer(rows[i],"area")!=areas[i]||!LightingFamilyRecipeValid(model,rows[i]))return false;
   return LightingFamilyRecipeSame(model,Map(snapshot,"lighting"),rows[0]);
  }
  public static AuraSyncTarget Fan(Dictionary<string,object> state,DateTime when) {
   bool online=Flag(state,"readbackConfirmed")==true;
   // SDK lighting implicitly writes fan-count preparation. Current counts cannot
   // be queried, so a draft/default count must never be forwarded by Aura Sync.
   return new AuraSyncTarget{Id="sdk:fan:7576",DeviceId="7576",Backend="sdk",Kind="fan",Name="ROG EURUX Controller",Transport="USB",ObservedUtc=when,State=state,Online=online,CanStaticRgb=false,CapabilitySource="EURUX SDK read/capability response",UnavailableReason=online?"灯效写入会同时提交风扇数量，当前数量无法读回；同步不会覆盖连接配置。":"控制器状态未确认。",PhysicalWriteValidated=false};
  }
  static bool HeadsetStaticScopeValid(Dictionary<string,object> state,Dictionary<string,object> snapshot){
   return Integer(state,"pid")==7489&&String(state,"transport")=="rf"&&Integer(snapshot,"modelPid")==7489&&Flag(snapshot,"headsetLightingProtocolPrepared")==true&&Flag(snapshot,"headsetLightingFieldsConfirmed")==true&&Flag(snapshot,"uniformStaticLightingProtocolAvailable")==true&&Flag(snapshot,"uniformStaticLightingReady")==true&&Integer(snapshot,"lightingEffectCode")>=0&&Integer(snapshot,"lightingEffectCode")<=5&&Integer(snapshot,"brightnessRaw")>=0&&Integer(snapshot,"brightnessRaw")<=100&&Bytes(Value(snapshot,"color"),3)!=null&&Flag(snapshot,"lightingEnabled").HasValue&&Flag(snapshot,"auraEnabled")==false&&new[]{"lighting","lightingEnabled","auraEnabled"}.All(Strings(state,"readCapabilities").Contains)&&Strings(state,"setCapabilities").Contains("staticLighting");
  }
  public static AuraSyncTarget Identified(Dictionary<string,object> state,DateTime when) {
   string id=String(state,"deviceId");return new AuraSyncTarget{Id="identified:"+id,DeviceId=id,Backend="identification",Kind="unknown",Name=String(state,"name"),Transport=String(state,"transport"),ObservedUtc=when,State=state,Online=Flag(state,"present")==true,CanStaticRgb=false,CapabilitySource="Windows device identification only",UnavailableReason="仅识别设备，尚无此型号的同步颜色接口。"};
  }
  public static AuraSyncTarget Missing(string kind,string name,string reason,DateTime when) {return new AuraSyncTarget{Id="unavailable:"+kind,Kind=kind,Name=name,Backend="unavailable",UnavailableReason=reason,ObservedUtc=when};}
  public static string OutputColor(string effect,string color,int brightness) {
   if(effect!="static"&&effect!="off")throw new ArgumentException("Cross-device synchronization currently supports static RGB and black only; autonomous hardware effects are not phase synchronization.");
   if(color==null||!Regex.IsMatch(color,@"\A#[0-9a-fA-F]{6}\z")||brightness<0||brightness>100)throw new ArgumentException("Invalid RGB color or brightness.");
   if(effect=="off")return "#000000";int rgb=Int32.Parse(color.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
   return "#"+System.String.Concat(new[]{(rgb>>16)&255,(rgb>>8)&255,rgb&255}.Select(value=>((int)Math.Round(value*brightness/100.0)).ToString("X2")));
  }
  public static AuraSyncCommand[] BuildStaticCommands(AuraSyncTarget target,string outputColor) {
   OutputColor("static",outputColor,100);if(target==null||!target.CanStaticRgb||!target.Online||!IdValid(target.DeviceId))throw new InvalidOperationException("This target has no confirmed uniform static-RGB capability.");
   if(target.Backend=="direct") {
    var verified=Direct(target.State,target.Name,target.ObservedUtc);if(!verified.CanStaticRgb||verified.Id!=target.Id)throw new InvalidOperationException("Native capability identity changed.");
    return new[]{new AuraSyncCommand{TargetId=target.Id,Backend="direct",Kind=target.Kind,Setting="static",ExpectedSession=target.WorkerSession,ConnectionGeneration=target.ConnectionGeneration,Request=Object("op","apply","effect","static","color",outputColor,"brightness",100,"devices",new[]{target.DeviceId})}};
   }
   if(target.Backend=="gear") {
    var verified=Gear(target.State,target.ObservedUtc);if(!verified.CanStaticRgb||verified.Id!=target.Id)throw new InvalidOperationException("GearLink static capability identity changed.");
    var result=new List<AuraSyncCommand>();var snapshot=Map(target.State,"snapshot");
    int packed=Int32.Parse(outputColor.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);var uniformRgb=new[]{(packed>>16)&255,(packed>>8)&255,packed&255};
    if(target.Kind=="keyboard"||target.Kind=="mouse"||target.Kind=="headset"&&String(target.State,"controlScope")=="headsetLightingOnly") {
     result.Add(GearCommand(target,"staticLighting",Object("color",uniformRgb,"brightness",100)));return result.ToArray();
    }
    if(Flag(snapshot,"lightingEnabled")!=true)result.Add(GearCommand(target,"lightingEnabled",true));
    result.Add(GearCommand(target,"brightness",100));result.Add(GearCommand(target,"lightingEffect",1));
    int rgb=Int32.Parse(outputColor.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);result.Add(GearCommand(target,"lightingColor",new[]{(rgb>>16)&255,(rgb>>8)&255,rgb&255}));return result.ToArray();
   }
   throw new InvalidOperationException("No safe uniform RGB writer exists for this backend.");
  }
  static AuraSyncCommand GearCommand(AuraSyncTarget target,string setting,object value){bool scoped=(target.Kind=="mouse"||target.Kind=="keyboard")&&(setting=="staticLighting"||setting=="lightingSoftwareMode")||target.Kind=="headset"&&setting=="staticLighting"&&String(target.State,"controlScope")=="headsetLightingOnly";var request=Object("op",scoped?"lightingSet":"set","kind",target.Kind,"deviceId",target.DeviceId,"setting",setting,"value",value,"experimental",true);if(target.Kind=="headset"){request["requireLightingOwnership"]=true;if(setting=="lightingColor")request["requireUniformStaticLighting"]=true;}return new AuraSyncCommand{TargetId=target.Id,Backend="gear",Kind=target.Kind,Setting=setting,ExpectedSession=target.WorkerSession,ConnectionGeneration=target.ConnectionGeneration,Request=request};}
  public async Task<AuraOwnershipResult> TakeSoftwareLightingAsync(AuraSyncTarget target,Func<AuraSyncTarget,Task<AuraSyncTarget>> refresh,Func<AuraSyncCommand,Task<AuraSyncCommandReply>> send,Action demandPermission) {
   if(running)throw new InvalidOperationException("Aura synchronization is already busy.");if(target==null||refresh==null||send==null||demandPermission==null)throw new ArgumentNullException("Ownership callbacks and target are required.");
   if(!target.CanReleaseSoftwareLighting)throw new InvalidOperationException("No read-backed software-lighting release capability exists.");
   var result=new AuraOwnershipResult{Target=target,State="preflightFailed",RestorationState="notNeeded"};bool attempted=false;running=true;stopRequested=false;
   try {
    try {
    var before=await refresh(target);result.Target=before;result.Steps.Add("baselineRead");if(before==null||before.Id!=target.Id||!before.Online)throw new InvalidOperationException("Device identity or connection changed.");result.WorkerSession=before.WorkerSession;result.ConnectionGeneration=before.ConnectionGeneration;
    if(before.CanStaticRgb){result.State="alreadyAvailable";result.OwnershipConfirmed=true;return result;}
    if(!before.CanReleaseSoftwareLighting||Flag(Map(before.State,"snapshot"),"lightingSoftwareMode")!=true||Flag(Map(before.State,"snapshot"),"windowsLightingEnabled")!=false)throw new InvalidOperationException("Lighting ownership baseline is no longer valid.");
    if(stopRequested)throw new OperationCanceledException("Ownership was stopped.");demandPermission();attempted=true;result.WriteAttempted=true;result.Steps.Add("releaseRequested");
    var response=await send(GearCommand(before,"lightingSoftwareMode",false));if(stopRequested)throw new OperationCanceledException("Ownership was stopped.");
    var after=await refresh(before);result.Target=after;result.Steps.Add("releaseReadback");
    if(response==null||!response.Ok||!response.WriteConfirmed||after==null||after.Id!=before.Id||!after.Online||!after.CanStaticRgb)throw new InvalidOperationException(response!=null&&!System.String.IsNullOrEmpty(response.Error)?response.Error:"Software-lighting release or static capability was not confirmed.");
    result.State="ownershipConfirmed";result.OwnershipConfirmed=true;result.CanReturnOriginalFlag=true;return result;
   }catch(Exception ex) {
    result.State=stopRequested?"stopped":"failed";result.Error=ex.GetBaseException().Message;
   }
    if(!attempted)return result;
    if(stopRequested){result.RestorationState="notAttemptedStopped";return result;}
    try {
     var current=await refresh(target);result.Target=current;result.Steps.Add("restorationPreflight");
     var snapshot=current==null?null:Map(current.State,"snapshot");bool? sw=Flag(snapshot,"lightingSoftwareMode");
     if(current==null||current.Id!=target.Id||!current.Online||Flag(snapshot,"windowsLightingEnabled")!=false||!sw.HasValue)throw new InvalidOperationException("Connection or ownership baseline unavailable; restoration was not attempted.");
     if(sw==true){result.RestorationState="originalFlagReadback";result.RestorationConfirmed=true;return result;}
     if(!Strings(current.State,"setCapabilities").Contains("lightingSoftwareMode"))throw new InvalidOperationException("Exact original-flag restoration capability unavailable.");
     if(stopRequested){result.RestorationState="notAttemptedStopped";return result;}demandPermission();result.Steps.Add("restoreOriginalSoftwareFlag");
     var restoreCommand=GearCommand(current,"lightingSoftwareMode",true);restoreCommand.IsOwnershipReturn=true;var restored=await send(restoreCommand);var verified=await refresh(current);result.Target=verified;result.Steps.Add("restorationReadback");
     result.RestorationConfirmed=restored!=null&&restored.Ok&&restored.WriteConfirmed&&verified!=null&&verified.Id==target.Id&&verified.Online&&Flag(Map(verified.State,"snapshot"),"lightingSoftwareMode")==true&&Flag(Map(verified.State,"snapshot"),"windowsLightingEnabled")==false;
     result.RestorationState=result.RestorationConfirmed?"confirmed":"unconfirmed";
    }catch(Exception restoreError){result.RestorationState="unconfirmed";result.Error+=" Restoration: "+restoreError.GetBaseException().Message;}
    return result;
   }finally{running=false;}
  }
  public async Task<AuraOwnershipResult> ReturnSoftwareLightingAsync(AuraOwnershipResult original,Func<AuraSyncTarget,Task<AuraSyncTarget>> refresh,Func<AuraSyncCommand,Task<AuraSyncCommandReply>> send,Action demandPermission) {
   if(running)throw new InvalidOperationException("Aura synchronization is already busy.");if(original==null||!original.CanReturnOriginalFlag||!original.OwnershipConfirmed||original.Target==null)throw new InvalidOperationException("No successful owned takeover receipt is available.");if(refresh==null||send==null||demandPermission==null)throw new ArgumentNullException("Ownership callbacks required.");
   var result=new AuraOwnershipResult{Target=original.Target,State="returnFailed",RestorationState="unconfirmed",CanReturnOriginalFlag=true,WorkerSession=original.WorkerSession,ConnectionGeneration=original.ConnectionGeneration};running=true;stopRequested=false;
   try {
    var before=await refresh(original.Target);result.Target=before;result.Steps.Add("returnPreflight");var snapshot=before==null?null:Map(before.State,"snapshot");
    bool? sw=Flag(snapshot,"lightingSoftwareMode");if(before==null||before.Id!=original.Target.Id||!before.Online||Flag(snapshot,"windowsLightingEnabled")!=false||!sw.HasValue||!Strings(before.State,"setCapabilities").Contains("lightingSoftwareMode"))throw new InvalidOperationException("Fresh device identity or lighting ownership is unavailable.");
    if(sw==true){result.State="returned";result.RestorationState="originalFlagReadback";result.RestorationConfirmed=true;result.CanReturnOriginalFlag=false;return result;}
    if(stopRequested)throw new OperationCanceledException("Return was stopped.");demandPermission();result.WriteAttempted=true;result.Steps.Add("returnOriginalSoftwareFlag");var returnCommand=GearCommand(before,"lightingSoftwareMode",true);returnCommand.IsOwnershipReturn=true;var response=await send(returnCommand);if(stopRequested)throw new OperationCanceledException("Return was stopped.");
    var after=await refresh(before);result.Target=after;result.Steps.Add("returnReadback");
    if(response==null||!response.Ok||!response.WriteConfirmed||after==null||after.Id!=before.Id||!after.Online||Flag(Map(after.State,"snapshot"),"lightingSoftwareMode")!=true||Flag(Map(after.State,"snapshot"),"windowsLightingEnabled")!=false)throw new InvalidOperationException("Original lighting flag return is unconfirmed.");
    result.State="returned";result.RestorationState="confirmed";result.RestorationConfirmed=true;result.CanReturnOriginalFlag=false;return result;
   }catch(Exception ex){result.Error=ex.GetBaseException().Message;return result;}finally{running=false;}
  }
  public async Task<AuraSyncResult> ApplyAsync(IEnumerable<AuraSyncTarget> targets,IEnumerable<string> selected,string effect,string color,int brightness,Func<AuraSyncTarget,Task<AuraSyncTarget>> refresh,Func<AuraSyncCommand,Task<AuraSyncCommandReply>> send,Action demandPermission) {
   if(running)throw new InvalidOperationException("Aura synchronization is already busy.");
   if(refresh==null||send==null||demandPermission==null)throw new ArgumentNullException("Synchronization callbacks are required.");
   string output=OutputColor(effect,color,brightness);var all=targets.ToDictionary(item=>item.Id,StringComparer.Ordinal);var chosen=selected.Distinct(StringComparer.Ordinal).ToArray();
   if(chosen.Length==0||chosen.Any(id=>!all.ContainsKey(id)||!all[id].CanStaticRgb))throw new InvalidOperationException("Select only devices with a confirmed static RGB interface.");
   running=true;stopRequested=false;var records=new List<AuraSyncTargetResult>();var prepared=new List<KeyValuePair<AuraSyncTargetResult,AuraSyncCommand[]>>();
   try {
    // Fresh read and complete plan validation for every target precedes output.
    foreach(string id in chosen) {
     var record=new AuraSyncTargetResult{TargetId=id,Name=all[id].Name,State="notSubmitted"};records.Add(record);
     if(stopRequested){record.State="cancelled";continue;}
     try{var fresh=await refresh(all[id]);if(fresh==null||fresh.Id!=id)throw new InvalidOperationException("Device identity changed during read.");if(stopRequested){record.State="cancelled";continue;}prepared.Add(new KeyValuePair<AuraSyncTargetResult,AuraSyncCommand[]>(record,BuildStaticCommands(fresh,output)));}
     catch(Exception ex){record.State="preflightFailed";record.Error=ex.GetBaseException().Message;}
    }
    foreach(var target in prepared) {
     var record=target.Key;bool confirmed=true;
     foreach(var command in target.Value) {
      if(stopRequested){record.State="cancelled";confirmed=false;break;}
      try {
       // This gate is checked for each individual write, including mid-sequence
       // policy revocation. The coordinator never grants session permission.
       demandPermission();if(stopRequested){record.State="cancelled";confirmed=false;break;}
       var reply=await send(command);if(reply==null)throw new InvalidOperationException("Device returned no command response.");
       record.WriteAttempted|=reply.WriteAttempted;record.Accepted|=reply.Accepted;
       if(!reply.Ok)throw new InvalidOperationException(reply.Error??"Device request failed.");
       record.CommandsCompleted++;confirmed&=reply.WriteConfirmed;
       if(command.Backend!="direct"&&!reply.WriteConfirmed){record.State="unconfirmed";record.Error="Device did not confirm the requested setting; remaining writes for this target were stopped.";break;}
      }catch(Exception ex){record.State="failed";record.Error=ex.GetBaseException().Message;confirmed=false;break;}
     }
     record.ReadbackConfirmed=confirmed&&record.CommandsCompleted==target.Value.Length;
     if(record.State=="notSubmitted")record.State=record.ReadbackConfirmed?"readbackConfirmed":"submittedUnconfirmed";
    }
    return new AuraSyncResult{Effect=effect,RequestedColor=color,Brightness=brightness,OutputColor=output,CompletedUtc=DateTime.UtcNow,Devices=records.ToArray(),Cancelled=stopRequested,AllSubmitted=records.Count!=0&&records.All(item=>item.State=="readbackConfirmed"||item.State=="submittedUnconfirmed"),AllReadbackConfirmed=records.Count!=0&&records.All(item=>item.ReadbackConfirmed)};
   }finally{running=false;}
  }
  static bool IdValid(string value){return value!=null&&Regex.IsMatch(value,@"\A[A-Za-z0-9][A-Za-z0-9:_\-]{0,159}\z");}
  static object Value(IDictionary<string,object> row,string key){object value;return row!=null&&row.TryGetValue(key,out value)?value:null;}
  static string String(IDictionary<string,object> row,string key){return Value(row,key) as string??"";}
  static bool? Flag(IDictionary<string,object> row,string key){object value=Value(row,key);return value is bool?(bool?)value:null;}
  static int? Integer(IDictionary<string,object> row,string key){object value=Value(row,key);return value is int?(int?)value:null;}
  static Dictionary<string,object> Map(IDictionary<string,object> row,string key){return Value(row,key) as Dictionary<string,object>;}
  static string[] Strings(IDictionary<string,object> row,string key){var value=Value(row,key) as IEnumerable;if(value==null||value is string)return new string[0];return value.Cast<object>().OfType<string>().ToArray();}
  static int[] Bytes(object value,int count){var list=value as IEnumerable;if(list==null||value is string)return null;var raw=list.Cast<object>().ToArray();return raw.Length==count&&raw.All(item=>item is int&&(int)item>=0&&(int)item<=255)?raw.Cast<int>().ToArray():null;}
  static Dictionary<string,object> Object(params object[] pairs){var result=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)result[(string)pairs[i]]=pairs[i+1];return result;}
 }
}
