// SPDX-License-Identifier: MIT. Independently implemented exact PID7489 lighting scope.
// Only documented legacy GETs, LED enable and an explicitly requested Static1 recipe.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal static partial class GearLinkWorker
{
    internal static bool HeadsetLightingOperation(string kind,string op,string setting)
    { return kind=="headset"&&(op=="lightingRead"||op=="lightingSet"||op=="set"&&setting=="staticLighting"); }
    internal static bool HeadsetLightingCollection(GearDescriptor d)
    { return d!=null&&d.Pid==7489&&d.ReceiverPid==0&&String.IsNullOrEmpty(d.ReceiverId)&&d.Page==0xFF00&&d.Usage==1&&d.InputLength>=10&&d.OutputLength>=10&&d.InputReports!=null&&d.OutputReports!=null&&d.InputReports.Contains((byte)0xCC)&&d.OutputReports.Contains((byte)0xCC); }
    internal static bool IsHeadsetLightingQuery(byte[] p)
    {return p!=null&&p.Length==4&&p[0]==0x12&&(p[1]==0&&p[2]==0&&p[3]==1||new byte[]{3,19,51}.Contains(p[1])&&p[2]==0&&p[3]==0);}
    internal static bool IsHeadsetStaticLightingWrite(byte[] p)
    {return p!=null&&p.Length>=5&&p[0]==0x51&&p[2]==0&&p[3]==0&&(p.Length==5&&p[1]==16&&p[4]==1||p.Length==9&&p[1]==40&&p[4]==1&&p[5]<=100);}
    internal static bool HeadsetScopeComplete(Dictionary<string,object> s,bool requireHardwareOwner)
    {
        try{object v;if(s==null||!s.TryGetValue("modelPid",out v)||!(v is int)||(int)v!=7489||!s.TryGetValue("lightingEnabled",out v)||!(v is bool)||!s.TryGetValue("auraEnabled",out v)||!(v is bool)||requireHardwareOwner&&(bool)v)return false;
            if(!s.TryGetValue("lightingEffectCode",out v)||!(v is int)||(int)v<0||(int)v>5||!s.TryGetValue("brightnessRaw",out v)||!(v is int)||(int)v<0||(int)v>100)return false;HeadsetRecipeRgb(s["color"]);return true;
        }catch(ArgumentException){return false;}catch(KeyNotFoundException){return false;}
    }
    internal static Dictionary<string,object> EmptyHeadsetLightingState(GearDescriptor d,Exception error=null)
    {
        var state=d.Metadata();state["controlScope"]="headsetLightingOnly";state["connected"]=true;state["online"]=false;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;
        state["snapshot"]=Obj("modelPid",7489,"headsetLightingProtocolPrepared",true,"headsetLightingHardwareVerified",false,"headsetLightingFieldsConfirmed",false,"uniformStaticLightingProtocolAvailable",false,"uniformStaticLightingReady",false);
        state["capabilities"]=new List<string>();state["readCapabilities"]=state["capabilities"];state["setCapabilities"]=new string[0];state["errors"]=new List<object>();if(error!=null)((List<object>)state["errors"]).Add(Obj("field","connection","message",error.Message));return state;
    }
    internal static Dictionary<string,object> ReadHeadsetLightingScope(GearDescriptor d,Func<byte[],byte[]> query)
    {
        if(!HeadsetLightingCollection(d))throw new ArgumentException("Only the exact PID7489 legacy CC / FF00 usage1 collection is prepared.");
        var state=EmptyHeadsetLightingState(d);var snapshot=(Dictionary<string,object>)state["snapshot"];var caps=(List<string>)state["capabilities"];var errors=(List<object>)state["errors"];
        try{
            var alive=query(Packet(0x12,0,0x100));Require(alive,5);if(alive[4]>1)throw new IOException("Unknown headset awake status.");state["readbackConfirmed"]=true;state["online"]=alive[4]==1;if(!(bool)state["online"])return state;
            LightingFamilyField(query,Packet(0x12,3,0),"lighting",errors,b=>{Require(b,9);if(b[4]>5||b[5]>100)throw new IOException("Unknown headset recipe bytes.");snapshot["lightingEffectCode"]=(int)b[4];snapshot["brightnessRaw"]=(int)b[5];snapshot["color"]=b.Skip(6).Take(3).Select(v=>(int)v).ToArray();if(b[4]==0)snapshot["lightingModeMappingUnconfirmed"]=true;caps.Add("lighting");});
            LightingFamilyField(query,Packet(0x12,19,0),"lightingEnabled",errors,b=>{snapshot["lightingEnabled"]=ReadBoolean(b,4);caps.Add("lightingEnabled");});
            LightingFamilyField(query,Packet(0x12,51,0),"auraEnabled",errors,b=>{snapshot["auraEnabled"]=ReadBoolean(b,4);caps.Add("auraEnabled");});
            bool complete=HeadsetScopeComplete(snapshot,false)&&new[]{"lighting","lightingEnabled","auraEnabled"}.All(caps.Contains);bool ready=complete&&!(bool)snapshot["auraEnabled"];
            snapshot["headsetLightingFieldsConfirmed"]=complete;snapshot["uniformStaticLightingProtocolAvailable"]=ready;snapshot["uniformStaticLightingReady"]=ready;if(ready)state["setCapabilities"]=new[]{"staticLighting"};
        }catch(Exception e){state["online"]=false;errors.Add(Obj("field","connection","message",e.Message));}
        return state;
    }
    internal static Dictionary<string,object> ReadHeadsetLightingScope(GearDescriptor d)
    {
        if(!HeadsetLightingCollection(d))throw new ArgumentException("Exact headset lighting collection is not confirmed.");
        try{using(var hid=new GearHid(d)){return ReadHeadsetLightingScope(d,p=>hid.Query(0xCC,p));}}catch(Exception e){return EmptyHeadsetLightingState(d,e);}
    }
    internal sealed class HeadsetLightingStep
    {
        internal readonly string Setting;private readonly byte[] packet;internal byte[] Packet{get{return (byte[])packet.Clone();}}
        internal HeadsetLightingStep(string setting,byte[] bytes){Setting=setting;packet=(byte[])bytes.Clone();}
    }
    internal static HeadsetLightingStep[] BuildHeadsetStaticPlan(object value,Dictionary<string,object> before)
    {
        ValidateLightingFamilyValue("staticLighting",value);if(!HeadsetScopeComplete(before,true))throw new ArgumentException("A complete PID7489 recipe, LED switch and released Aura ownership must be read before static lighting.");
        var request=(Dictionary<string,object>)value;int[] rgb=HeadsetRecipeRgb(request["color"]);int brightness=(int)request["brightness"];var plan=new List<HeadsetLightingStep>();
        if(!(bool)before["lightingEnabled"])plan.Add(new HeadsetLightingStep("lightingEnabled",Packet(0x51,16,0,1)));
        if((int)before["lightingEffectCode"]!=1||(int)before["brightnessRaw"]!=brightness||!HeadsetRecipeRgb(before["color"]).SequenceEqual(rgb))plan.Add(new HeadsetLightingStep("staticLighting",Packet(0x51,40,0,1,(byte)brightness,(byte)rgb[0],(byte)rgb[1],(byte)rgb[2])));
        foreach(var step in plan)if(!IsHeadsetStaticLightingWrite(step.Packet))throw new ArgumentException("Headset lighting plan violated its fixed packet contract.");return plan.ToArray();
    }
    private static bool SameHeadsetScopeRecipe(Dictionary<string,object> before,Dictionary<string,object> current)
    {return (int)before["lightingEffectCode"]==(int)current["lightingEffectCode"]&&(int)before["brightnessRaw"]==(int)current["brightnessRaw"]&&HeadsetRecipeRgb(before["color"]).SequenceEqual(HeadsetRecipeRgb(current["color"]));}
    internal static bool MatchesHeadsetStaticLighting(object value,Dictionary<string,object> current)
    {
        try{ValidateLightingFamilyValue("staticLighting",value);if(!HeadsetScopeComplete(current,true)||!(bool)current["lightingEnabled"]||(int)current["lightingEffectCode"]!=1)return false;var v=(Dictionary<string,object>)value;return Equals(v["brightness"],current["brightnessRaw"])&&HeadsetRecipeRgb(v["color"]).SequenceEqual(HeadsetRecipeRgb(current["color"]));}catch(ArgumentException){return false;}
    }
    internal static Dictionary<string,object> ApplyHeadsetStaticLighting(GearDescriptor d,object value,bool noOp,Func<Dictionary<string,object>> read,Func<byte[],SettingReceipt> write,Func<int> elapsedMilliseconds=null)
    {
        ValidateLightingFamilyValue("staticLighting",value);if(!HeadsetLightingCollection(d))throw new ArgumentException("Exact original headset lighting report is unavailable.");var clock=Stopwatch.StartNew();if(elapsedMilliseconds==null)elapsedMilliseconds=()=>checked((int)Math.Min(Int32.MaxValue,clock.ElapsedMilliseconds));
        var before=read();var prior=before.ContainsKey("snapshot")?before["snapshot"] as Dictionary<string,object>:null;if(Text(before,"deviceId")!=d.Id||!before.ContainsKey("online")||!(before["online"] is bool)||!(bool)before["online"]||!before.ContainsKey("readbackConfirmed")||!(before["readbackConfirmed"] is bool)||!(bool)before["readbackConfirmed"])throw new ArgumentException("The exact headset is not currently online and confirmed.");
        var plan=BuildHeadsetStaticPlan(value,prior);if(plan.Length==0){before["setting"]="staticLighting";before["requestedValue"]=value;before["accepted"]=false;before["writeAttempted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}if(noOp)throw new ArgumentException("Read-only validation rejected changed static headset output.");
        prior=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(prior));value=Json.DeserializeObject(Json.Serialize(value));bool attempted=false,output=false,failed=false;var confirmed=new List<string>();var steps=new List<object>();var after=before;
        foreach(var step in plan){
            if(16000-elapsedMilliseconds()<5000){steps.Add(Obj("setting",step.Setting,"outputCompleted",false,"acknowledged",false,"rejected",false,"readbackConfirmed",false,"warning","Insufficient bounded budget for a complete headset output and four-query readback."));failed=true;break;}
            SettingReceipt receipt=null;string error=null;attempted=true;
            try{
                receipt=write(step.Packet);output|=receipt.OutputCompleted;if(!receipt.OutputCompleted||receipt.Rejected)error=receipt.Warning??"Headset output was incomplete or rejected.";
                after=read();var current=after.ContainsKey("snapshot")?after["snapshot"] as Dictionary<string,object>:null;
                bool ready=Text(after,"deviceId")==d.Id&&after.ContainsKey("online")&&after["online"] is bool&&(bool)after["online"]&&after.ContainsKey("readbackConfirmed")&&after["readbackConfirmed"] is bool&&(bool)after["readbackConfirmed"]&&HeadsetScopeComplete(current,true);
                bool match=ready&&(step.Setting=="lightingEnabled"?(bool)current["lightingEnabled"]&&SameHeadsetScopeRecipe(prior,current):MatchesHeadsetStaticLighting(value,current));
                if(error==null&&!match)error="Independent headset identity, LED/Aura status and complete recipe readback did not match the submitted step.";
                if(error==null)confirmed.Add(step.Setting);
            }catch(Exception e){error=e.Message;after=EmptyHeadsetLightingState(d,e);}
            steps.Add(Obj("setting",step.Setting,"outputCompleted",receipt!=null&&receipt.OutputCompleted,"acknowledged",receipt!=null&&receipt.Acknowledged,"rejected",receipt!=null&&receipt.Rejected,"readbackConfirmed",error==null,"warning",error??(receipt==null?null:receipt.Warning)));
            if(error!=null){failed=true;break;}
        }
        after["setting"]="staticLighting";after["requestedValue"]=value;after["accepted"]=output;after["writeAttempted"]=attempted;after["writeConfirmed"]=!failed&&MatchesHeadsetStaticLighting(value,(Dictionary<string,object>)after["snapshot"]);after["appliedState"]=(bool)after["writeConfirmed"]?"confirmed":confirmed.Count>0?"partial":"unconfirmed";after["lightingStepResults"]=steps.ToArray();after["confirmedLightingSteps"]=confirmed.ToArray();after["remainingLightingSteps"]=plan.Where(s=>!confirmed.Contains(s.Setting)).Select(s=>s.Setting).ToArray();after["acceptanceEvidence"]="LED enable and the Static1 recipe are sequential, not atomic. Output completion, ACK and independent full readback are separate. Aura ownership is never changed or guessed.";return after;
    }
    internal static object HandleHeadsetLightingScope(Dictionary<string,object> request,GearDescriptor d,string op)
    {
        if(op=="lightingRead")return ReadHeadsetLightingScope(d);object flag,value,noOpRaw;if(!request.TryGetValue("experimental",out flag)||!(flag is bool)||!(bool)flag)throw new ArgumentException("Explicit hardware control is required.");if(Text(request,"setting")!="staticLighting"||!request.TryGetValue("value",out value))throw new ArgumentException("This headset lighting scope exposes only an explicit staticLighting request.");ValidateLightingFamilyValue("staticLighting",value);bool noOp=false;if(request.TryGetValue("requireNoOp",out noOpRaw)){if(!(noOpRaw is bool))throw new ArgumentException("Read-only validation guard must be Boolean.");noOp=(bool)noOpRaw;}
        if(!HeadsetLightingCollection(d))throw new ArgumentException("Exact original headset lighting report is not confirmed.");using(var hid=new GearHid(d)){return ApplyHeadsetStaticLighting(d,value,noOp,()=>ReadHeadsetLightingScope(d,p=>hid.Query(0xCC,p)),p=>{if(!IsHeadsetStaticLightingWrite(p))throw new ArgumentException("Headset compound transport rejected an out-of-scope write.");return hid.WriteSetting(0xCC,p);});}
    }
}
