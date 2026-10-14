// SPDX-License-Identifier: MIT
// Independently written exact-PID lighting contracts. Source facts are documented separately.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal static partial class GearLinkWorker
{
    internal sealed class LightingFamily
    {
        internal readonly int Pid; internal readonly string Family, Kind, Name, Transport;
        private readonly int[] areas;
        internal int[] Areas { get { return (int[])areas.Clone(); } }
        internal LightingFamily(int pid, string family, string kind, string name, string transport, params int[] zones)
        { Pid=pid;Family=family;Kind=kind;Name=name;Transport=transport;areas=(int[])zones.Clone(); }
    }
    private static readonly LightingFamily[] LightingFamilies = RogControl.GearLinkLightingModelFacts.All.Where(f=>f.LightingOnly).Select(f=>new LightingFamily(f.Pid,f.Family,f.Kind,f.Model,f.Transport,f.Areas)).ToArray();
    internal static LightingFamily LightingFamilyFor(int pid) { return LightingFamilies.FirstOrDefault(f=>f.Pid==pid); }
    internal static bool LightingOnlyFamily(int pid) { return LightingFamilyFor(pid)!=null; }
    internal static bool VerifiedLightingKeyboard(int pid) { var f=LightingFamilyFor(pid);return LegacyKeyboard(pid)||f!=null&&f.Kind=="keyboard"; }
    internal static string Model(int pid) { var f=LightingFamilyFor(pid);return f!=null?f.Name:Kind(pid)=="unknown"?"Unknown paired device":Model(Kind(pid)); }
    internal static bool LightingFamilyCollection(GearDescriptor d)
    {
        var f=LightingFamilyFor(d.Pid);if(f==null||d.Usage!=1||d.OutputReports==null||d.InputReports==null||d.OutputReports.Length!=1||d.InputLength<(f.Kind=="mouse"?20:19)||d.OutputLength<(f.Kind=="mouse"?13:19)||!d.InputReports.Contains(d.OutputReports[0]))return false;
        if(d.ReceiverPid!=0)return new[]{6862,6864,7508}.Contains((int)d.ReceiverPid)&&!String.IsNullOrEmpty(d.ReceiverId)&&d.Page>=0xFF00&&d.Page<=0xFF02&&f.Transport=="rf";
        return d.Page==(f.Kind=="mouse"?0xFF01:0xFF00);
    }
    internal static bool IsLightingFamilyQuery(int pid, byte[] p)
    {
        var f=LightingFamilyFor(pid);if(f==null||p==null||p.Length!=4||p[3]!=0)return false;
        if(f.Kind=="mouse")return p[0]==0x12&&(p[1]==0&&new byte[]{0,2,9,12}.Contains(p[2])||p[1]==3&&f.Areas.Contains((int)p[2]));
        return p[2]==0&&(p[0]==0x12&&new byte[]{0,3,22}.Contains(p[1])||p[0]==0x25&&p[1]==14||p[0]==0x27&&p[1]==0);
    }
    internal static bool IsLightingFamilyWrite(int pid, byte[] p)
    {
        var f=LightingFamilyFor(pid);if(f==null||p==null||p.Length<5||p[3]!=0)return false;
        if(p.Length==5&&p[2]==0&&p[4]<=1)return f.Kind=="mouse"?p[0]==0x51&&p[1]==48:p[0]==0x74&&p[1]==0;
        if(f.Kind=="mouse")return p.Length==12&&p[0]==0x51&&p[1]==40&&f.Areas.Contains((int)p[2])&&p[4]==0&&p[5]<=100&&p[9]<=1&&p[10]==0;
        return p.Length==18&&p[0]==0x51&&p[1]==44&&p[2]==0&&p[5]<=100&&p[6]==0&&p[7]==255&&p[8]==255&&p.Skip(12).All(b=>b==0);
    }
    internal static void ValidateLightingFamilyRequest(string op, string setting)
    {
        if(op=="read"||op=="lightingRead")return;
        if((op=="set"||op=="lightingSet")&&(setting=="staticLighting"||setting=="lightingSoftwareMode"))return;
        throw new ArgumentException("This exact model exposes only the independently prepared static lighting and software ownership scope.");
    }
    private static int FamilyInteger(object v,int min,int max)
    { if(!(v is int)||(int)v<min||(int)v>max)throw new ArgumentException("Lighting requires an integer within its documented range.");return (int)v; }
    private static int[] FamilyRgb(object v)
    {
        if(!(v is IEnumerable)||v is string||v is IDictionary)throw new ArgumentException("Lighting color requires exactly three integer bytes.");
        var values=new List<int>();foreach(object n in (IEnumerable)v){if(values.Count>=3)throw new ArgumentException("Lighting color has too many channels.");values.Add(FamilyInteger(n,0,255));}
        if(values.Count!=3)throw new ArgumentException("Lighting color requires three channels.");return values.ToArray();
    }
    internal static void ValidateLightingFamilyValue(string setting,object value)
    {
        if(setting=="lightingSoftwareMode"){if(!(value is bool))throw new ArgumentException("Software lighting ownership requires a Boolean.");return;}
        if(setting!="staticLighting")throw new ArgumentException("Unsupported minimal lighting setting.");
        var v=value as Dictionary<string,object>;if(v==null||v.Count!=2||!v.ContainsKey("color")||!v.ContainsKey("brightness"))throw new ArgumentException("Static lighting requires only color and brightness.");
        FamilyRgb(v["color"]);FamilyInteger(v["brightness"],0,100);
    }
    internal static Dictionary<string,object> ParseLightingFamilyRecipe(int pid,int area,byte[] b,Dictionary<string,object> snapshot)
    {
        var f=LightingFamilyFor(pid);if(f==null||!f.Areas.Contains(area))throw new ArgumentException("Unprepared lighting area.");
        if(f.Kind=="mouse"){
            Require(b,13);if(!new[]{0,1,2,3,4,5,6,240}.Contains((int)b[4])||b[5]>100||b[10]>1||b[11]>1)throw new IOException("Unknown mouse lighting recipe.");
            return Obj("area",area,"areaName",area==0?"Logo":area==1?"Scroll":"Base","currentMode",(int)b[4],"brightness",(int)b[5],"color",b.Skip(6).Take(3).Select(n=>(int)n).ToArray(),"direction",(int)b[10],"random",b[11]==1,"speed",(int)b[12],"source",f.Family);
        }
        Require(b,18);object mode;if(!snapshot.TryGetValue("lightingEffectCode",out mode)||!(mode is int)||(int)mode<0||(int)mode>9||b[5]>100||!new byte[]{0,1,16,17}.Contains(b[6])||b[7]!=255||b[8]!=255)throw new IOException("Unknown keyboard static recipe or active effect selector.");
        return Obj("area",0,"areaName","Main","currentMode",mode,"recipeMode",0,"speed",(int)b[4],"brightness",(int)b[5],"flags",(int)b[6],"direction",255,"width",255,"color",b.Skip(9).Take(3).Select(n=>(int)n).ToArray(),"secondaryColor",b.Skip(12).Take(3).Select(n=>(int)n).ToArray(),"backgroundColor",b.Skip(15).Take(3).Select(n=>(int)n).ToArray(),"source",f.Family);
    }
    private static void ReadLightingFamilyInfo(LightingFamily f,byte[] b,Dictionary<string,object> snapshot)
    {
        Require(b,f.Kind=="mouse"?19:15);int count=b[8],profile=b[10]+(f.Kind=="mouse"?1:0);
        if(count==0||profile>count||f.Kind=="mouse"&&profile<1)throw new IOException("Current lighting profile selector is not confirmed.");
        if(f.Kind=="keyboard"&&b[9]>9)throw new IOException("Unknown current keyboard effect selector.");
        snapshot["profileCount"]=count;snapshot["activeProfile"]=profile;
        snapshot["firmware"]=VersionNative4(b,f.Transport=="usb"?4:f.Kind=="mouse"?14:11);snapshot["dongleFirmware"]=f.Transport=="rf"?VersionNative4(b,4):null;
        if(f.Kind=="keyboard")snapshot["lightingEffectCode"]=(int)b[9];
    }
    private static void LightingFamilyField(Func<byte[],byte[]> query,byte[] p,string field,List<object> errors,Action<byte[]> parse)
    { try {parse(query(p));}catch(Exception e){errors.Add(Obj("field",field,"message",e.Message));} }
    internal static Dictionary<string,object> FailedLightingFamilyScope(GearDescriptor d,Exception error)
    {
        var state=d.Metadata();state["snapshot"]=Obj("modelPid",(int)d.Pid,"lightingAreas",new object[0],"uniformStaticLightingProtocolAvailable",false,"uniformStaticLightingReady",false,"lightingProtocolPrepared",true,"lightingHardwareVerified",false);
        state["online"]=false;state["connected"]=true;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;state["capabilities"]=new string[0];state["readCapabilities"]=new string[0];state["setCapabilities"]=new string[0];state["errors"]=new List<object>{Obj("field","connection","message",error.Message)};return state;
    }
    internal static Dictionary<string,object> ReadLightingFamilyScope(GearDescriptor d,Func<byte[],byte[]> query)
    {
        var f=LightingFamilyFor(d.Pid);if(f==null||!LightingFamilyCollection(d))throw new ArgumentException("Exact lighting model collection or routed report is not confirmed.");
        var state=d.Metadata();var snapshot=Obj("modelPid",(int)d.Pid,"lightingFamily",f.Family,"lightingAreas",new Dictionary<string,object>[0],"uniformStaticLightingProtocolAvailable",false,"uniformStaticLightingReady",false,"lightingProtocolPrepared",true,"lightingHardwareVerified",false);
        var errors=new List<object>();var caps=new List<string>();state["snapshot"]=snapshot;state["capabilities"]=caps;state["readCapabilities"]=caps;state["setCapabilities"]=new string[0];state["errors"]=errors;state["online"]=false;state["connected"]=true;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;state["controlScope"]="lightingOnly";
        try {
            byte[] alive=query(f.Kind=="mouse"?Packet(0x12,0,2):Packet(0x12,3,0));Require(alive,5);bool online;
            if(f.Kind=="mouse"){if(alive[4]>1)throw new IOException("Unknown mouse awake state.");online=alive[4]==1;}
            else {if(alive[4]!=0&&alive[4]!=1&&alive[4]!=255)throw new IOException("Connection mode is outside this exact USB/RF contract.");online=alive[4]!=(byte)255;if(online&&alive[4]!=(f.Transport=="usb"?0:1))throw new IOException("Connection mode does not match the normal product ID transport.");}
            state["readbackConfirmed"]=true;state["online"]=online;if(!online)return state;
            LightingFamilyField(query,Packet(0x12,0,0),"lightingBasicInfo",errors,b=>ReadLightingFamilyInfo(f,b,snapshot));
            var recipes=new List<Dictionary<string,object>>();foreach(int area in f.Areas)LightingFamilyField(query,f.Kind=="mouse"?Packet(0x12,3,(ushort)area):Packet(0x25,14,0),"lightingArea"+area,errors,b=>recipes.Add(ParseLightingFamilyRecipe(f.Pid,area,b,snapshot)));
            snapshot["lightingAreas"]=recipes.ToArray();var main=recipes.FirstOrDefault(r=>(int)r["area"]==f.Areas[0]);if(main!=null){snapshot["lighting"]=main;caps.Add("lighting");}
            LightingFamilyField(query,f.Kind=="mouse"?Packet(0x12,0,12):Packet(0x27,0,0),"lightingSoftwareMode",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown software lighting ownership.");snapshot["lightingSoftwareMode"]=b[4]==1;caps.Add("lightingSoftwareMode");});
            LightingFamilyField(query,f.Kind=="mouse"?Packet(0x12,0,9):Packet(0x12,22,0),"windowsLightingEnabled",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown Windows lighting ownership.");snapshot["windowsLightingEnabled"]=b[4]==1;caps.Add("windowsLightingEnabled");});
            bool prepared=LightingFamilyReady(f.Pid,snapshot,false);snapshot["uniformStaticLightingProtocolAvailable"]=prepared;snapshot["uniformStaticLightingReady"]=prepared&&!(bool)snapshot["lightingSoftwareMode"];
            if(prepared)state["setCapabilities"]=(bool)snapshot["lightingSoftwareMode"]?new[]{"lightingSoftwareMode"}:new[]{"lightingSoftwareMode","staticLighting"};
        }catch(Exception e){errors.Add(Obj("field","connection","message",e.Message));state["online"]=false;}
        return state;
    }
    private static Dictionary<string,object>[] FamilyAreas(Dictionary<string,object> s)
    {object raw;if(s==null||!s.TryGetValue("lightingAreas",out raw)||!(raw is IEnumerable)||raw is string)return new Dictionary<string,object>[0];return ((IEnumerable)raw).Cast<object>().Select(o=>o as Dictionary<string,object>).ToArray();}
    internal static bool LightingFamilyReady(int pid,Dictionary<string,object> s,bool requireHardwareOwner)
    {
        try{
            var f=LightingFamilyFor(pid);object v;if(f==null||s==null||!s.TryGetValue("modelPid",out v)||!(v is int)||(int)v!=pid||!s.TryGetValue("lightingSoftwareMode",out v)||!(v is bool)||requireHardwareOwner&&(bool)v)return false;
            if(!s.TryGetValue("windowsLightingEnabled",out v)||!(v is bool)||(bool)v)return false;
            int count=FamilyInteger(s["profileCount"],1,255),profile=FamilyInteger(s["activeProfile"],f.Kind=="mouse"?1:0,count);
            var areas=FamilyAreas(s);if(areas.Length!=f.Areas.Length||areas.Any(a=>a==null)||areas.Select(a=>FamilyInteger(a["area"],0,2)).Distinct().Count()!=areas.Length)return false;
            foreach(int area in f.Areas){var a=areas.SingleOrDefault(r=>(int)r["area"]==area);if(a==null||Text(a,"source")!=f.Family)return false;int mode=FamilyInteger(a["currentMode"],0,255);if(!(f.Kind=="mouse"?new[]{0,1,2,3,4,5,6,240}.Contains(mode):mode<=9))return false;FamilyInteger(a["brightness"],0,100);FamilyInteger(a["speed"],0,255);FamilyRgb(a["color"]);
                if(f.Kind=="mouse"){FamilyInteger(a["direction"],0,1);if(!(a["random"] is bool))return false;}
                else {if(FamilyInteger(a["recipeMode"],0,0)!=0||!new[]{0,1,16,17}.Contains(FamilyInteger(a["flags"],0,17))||FamilyInteger(a["direction"],255,255)!=255||FamilyInteger(a["width"],255,255)!=255)return false;FamilyRgb(a["secondaryColor"]);FamilyRgb(a["backgroundColor"]);}
            }return profile<=count;
        }catch{return false;}
    }
    internal sealed class LightingFamilyStep
    {
        internal readonly int Area;private readonly byte[] packet;internal byte[] Packet {get{return (byte[])packet.Clone();}}
        internal LightingFamilyStep(int area,byte[] bytes){Area=area;packet=(byte[])bytes.Clone();}
    }
    internal static LightingFamilyStep[] BuildLightingFamilyPlan(int pid,string setting,object value,Dictionary<string,object> before)
    {
        ValidateLightingFamilyValue(setting,value);var f=LightingFamilyFor(pid);if(f==null||!LightingFamilyReady(pid,before,setting=="staticLighting"))throw new ArgumentException("Fresh complete current profile, every lighting area, SW and WDL ownership are required.");
        if(setting=="lightingSoftwareMode")return new[]{new LightingFamilyStep(-1,f.Kind=="mouse"?Packet(0x51,48,0,(bool)value?(byte)1:(byte)0):Packet(0x74,0,0,(bool)value?(byte)1:(byte)0))};
        var v=(Dictionary<string,object>)value;int[] rgb=FamilyRgb(v["color"]);byte brightness=(byte)FamilyInteger(v["brightness"],0,100);var areas=FamilyAreas(before);var plan=new List<LightingFamilyStep>();
        foreach(int area in f.Areas){var a=areas.Single(r=>(int)r["area"]==area);byte speed=(byte)(int)a["speed"];byte[] packet=f.Kind=="mouse"?Packet(0x51,40,(ushort)area,0,brightness,(byte)rgb[0],(byte)rgb[1],(byte)rgb[2],(byte)(int)a["direction"],0,speed):Packet(0x51,44,0,speed,brightness,0,255,255,(byte)rgb[0],(byte)rgb[1],(byte)rgb[2],0,0,0,0,0,0);if(!IsLightingFamilyWrite(pid,packet))throw new ArgumentException("Prepared lighting packet violated its exact transport contract.");plan.Add(new LightingFamilyStep(area,packet));}
        return plan.ToArray();
    }
    private static bool FamilySameProfile(int pid,Dictionary<string,object> prior,Dictionary<string,object> now)
    {return LightingFamilyReady(pid,now,false)&&Equals(prior["profileCount"],now["profileCount"])&&Equals(prior["activeProfile"],now["activeProfile"]);}
    private static bool FamilySameRecipe(Dictionary<string,object> a,Dictionary<string,object> b)
    {return a.Count==b.Count&&a.All(pair=>b.ContainsKey(pair.Key)&&Json.Serialize(pair.Value)==Json.Serialize(b[pair.Key]));}
    internal static bool MatchesLightingFamilyAfter(int pid,string setting,object value,Dictionary<string,object> prior,Dictionary<string,object> now,IEnumerable<int> appliedAreas)
    {
        try{
            ValidateLightingFamilyValue(setting,value);if(!FamilySameProfile(pid,prior,now))return false;var f=LightingFamilyFor(pid);var previous=FamilyAreas(prior);var current=FamilyAreas(now);
            if(setting=="lightingSoftwareMode")return Equals(now["lightingSoftwareMode"],value)&&f.Areas.All(area=>FamilySameRecipe(previous.Single(a=>(int)a["area"]==area),current.Single(a=>(int)a["area"]==area)));
            if(!LightingFamilyReady(pid,now,true))return false;var done=new HashSet<int>(appliedAreas);if(done.Any(a=>!f.Areas.Contains(a)))return false;var request=(Dictionary<string,object>)value;int[] rgb=FamilyRgb(request["color"]);int brightness=(int)request["brightness"];
            foreach(int area in f.Areas){var old=previous.Single(previousArea=>(int)previousArea["area"]==area);var a=current.Single(r=>(int)r["area"]==area);if(!done.Contains(area)){if(!FamilySameRecipe(old,a))return false;continue;}if((int)a["currentMode"]!=0||(int)a["brightness"]!=brightness||!FamilyRgb(a["color"]).SequenceEqual(rgb)||!Equals(a["speed"],old["speed"]))return false;
                if(f.Kind=="mouse"){if(!Equals(a["direction"],old["direction"])||(bool)a["random"])return false;}
                else if((int)a["flags"]!=0||FamilyRgb(a["secondaryColor"]).Any(n=>n!=0)||FamilyRgb(a["backgroundColor"]).Any(n=>n!=0))return false;
            }return true;
        }catch{return false;}
    }
    internal static Dictionary<string,object> ApplyLightingFamily(GearDescriptor d,string setting,object value,bool noOp,Func<Dictionary<string,object>> read,Func<byte[],SettingReceipt> write,Func<int> elapsedMilliseconds=null)
    {
        var clock=Stopwatch.StartNew();if(elapsedMilliseconds==null)elapsedMilliseconds=()=>checked((int)Math.Min(Int32.MaxValue,clock.ElapsedMilliseconds));
        ValidateLightingFamilyValue(setting,value);if(!LightingFamilyCollection(d))throw new ArgumentException("Exact current lighting collection is unavailable.");
        var before=read();var prior=before["snapshot"] as Dictionary<string,object>;object online;if(!before.TryGetValue("online",out online)||!(online is bool)||!(bool)online||Text(before,"deviceId")!=d.Id)throw new ArgumentException("The original lighting device is not online.");
        var plan=BuildLightingFamilyPlan(d.Pid,setting,value,prior);int[] all=LightingFamilyFor(d.Pid).Areas;
        if(MatchesLightingFamilyAfter(d.Pid,setting,value,prior,prior,all)){before["writeAttempted"]=false;before["accepted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}
        if(noOp)throw new ArgumentException("Read-only validation rejected a changed lighting value.");
        // Freeze all plan bytes and source values before the first output. No automatic rollback.
        prior=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(prior));value=Json.DeserializeObject(Json.Serialize(value));var confirmed=new List<int>();var results=new List<object>();var after=before;bool anyOutput=false,attempted=false,failed=false;
        foreach(var step in plan){
            // The parent uses an 18-second request budget. Reserve the worst-case
            // bounded SET plus complete readback before starting another area.
            int reserve=(all.Length+5)*900+500;
            if(16000-elapsedMilliseconds()<reserve){results.Add(Obj("area",step.Area<0?null:(object)step.Area,"outputCompleted",false,"acknowledged",false,"rejected",false,"readbackConfirmed",false,"warning","The bounded lighting budget cannot safely start another complete output/readback transaction."));failed=true;break;}
            SettingReceipt receipt=null;string failure=null;attempted=true;
            try { receipt=write(step.Packet);anyOutput|=receipt.OutputCompleted;if(!receipt.OutputCompleted||receipt.Rejected)failure=receipt.Warning??"Lighting output was incomplete or rejected.";
                after=read();var current=after["snapshot"] as Dictionary<string,object>;var expected=confirmed.Concat(step.Area<0?new int[0]:new[]{step.Area});bool matches=Text(after,"deviceId")==d.Id&&after["online"] is bool&&(bool)after["online"]&&MatchesLightingFamilyAfter(d.Pid,setting,value,prior,current,expected);
                if(failure==null&&!matches)failure="Independent current profile, ownership and every-area readback did not match the submitted plan.";
                if(failure==null&&step.Area>=0)confirmed.Add(step.Area);
            }catch(Exception e){failure=e.Message;after=FailedLightingFamilyScope(d,e);}
            results.Add(Obj("area",step.Area<0?null:(object)step.Area,"outputCompleted",receipt!=null&&receipt.OutputCompleted,"acknowledged",receipt!=null&&receipt.Acknowledged,"rejected",receipt!=null&&receipt.Rejected,"readbackConfirmed",failure==null,"warning",failure??(receipt==null?null:receipt.Warning)));
            if(failure!=null){failed=true;break;}
        }
        after["setting"]=setting;after["requestedValue"]=value;after["writeAttempted"]=attempted;after["accepted"]=anyOutput;after["writeConfirmed"]=!failed;after["appliedState"]=!failed?"confirmed":confirmed.Count>0?"partial":"unconfirmed";after["lightingAreaResults"]=results.ToArray();after["confirmedAreas"]=confirmed.ToArray();after["remainingAreas"]=plan.Where(s=>s.Area>=0&&!confirmed.Contains(s.Area)).Select(s=>s.Area).ToArray();after["acceptanceEvidence"]="Output completion, acknowledgement, and independent all-area readback are separate. Multi-area changes are sequential and are not atomic.";return after;
    }
}
