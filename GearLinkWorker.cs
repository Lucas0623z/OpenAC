// Owned adapter: public GearLink protocol observations, no ASUS JavaScript or DLLs included.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

internal static partial class GearLinkWorker
{
    internal static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
    internal static Dictionary<string, object> Obj(params object[] fields) { var o = new Dictionary<string, object>(); for (int i=0;i<fields.Length;i+=2) o[(string)fields[i]]=fields[i+1]; return o; }
    internal static string Text(Dictionary<string,object> r, string key) { object v; return r.TryGetValue(key,out v) && v != null ? Convert.ToString(v) : ""; }
    internal static string Kind(int pid) { return pid == 7546 || pid == 7548 ? "mouse" : pid == 7586 || pid == 7588 ? "keyboard" : pid == 7489 ? "headset" : pid == 6862 || pid == 6864 || pid == 7508 ? "omni" : "unknown"; }
    internal static bool LegacyKeyboard(int pid) { return pid==7586||pid==7588; }
    internal static int ResponsePrefixLength(int pid,byte[] packet) { Require(packet,4);return packet[0]==0xA0||LegacyKeyboard(pid)?2:4; }
    internal static bool ResponseMatches(int pid,byte report,byte[] packet,byte[] input,int received)
    {
        int match=ResponsePrefixLength(pid,packet);if(input==null||received<match+1||received>input.Length||input[0]!=report)return false;
        for(int i=0;i<match;i++)if(input[i+1]!=packet[i])return false;return true;
    }
    internal static bool DeviceErrorResponse(byte report,byte[] input,int received) { return input!=null&&received>=3&&received<=input.Length&&input[0]==report&&input[1]==255&&input[2]==170; }
    internal sealed class SettingReceipt { internal bool OutputCompleted,Acknowledged,Rejected;internal string Warning; }
    internal static SettingReceipt SendSettingAndAwaitResponse(int pid,byte report,byte[] packet,int outputLength,int inputLength,Func<bool,byte[],int,int> transfer)
    {
        if(packet==null||packet.Length<4||outputLength<packet.Length+1||inputLength<5)throw new IOException("Unexpected setting report length.");
        var receipt=new SettingReceipt();var output=new byte[outputLength];output[0]=report;Array.Copy(packet,0,output,1,packet.Length);var clock=Stopwatch.StartNew();
        try {
            int sent=transfer(true,output,900);if(sent!=output.Length){receipt.Warning="Setting output returned a partial report; its hardware effect is unknown.";return receipt;}receipt.OutputCompleted=true;
            for(int reads=0;reads<64;reads++) {int remaining=900-(int)clock.ElapsedMilliseconds;if(remaining<=GearBoundedIo.CancelReserveMs)throw new TimeoutException("Setting acknowledgement exceeded its bounded transaction budget.");var input=new byte[inputLength];int got=transfer(false,input,remaining);if(got==0)throw new IOException("Device disconnected while awaiting setting acknowledgement.");if(DeviceErrorResponse(report,input,got)){receipt.Rejected=true;receipt.Warning="Device rejected the setting with an FF AA error frame.";return receipt;}if(ResponseMatches(pid,report,packet,input,got)){receipt.Acknowledged=true;return receipt;}}
            receipt.Warning="No matching acknowledgement in the bounded response stream.";
        } catch(TimeoutException e) {receipt.Warning="Setting output completed but acknowledgement was not confirmed. "+e.Message;}catch(IOException e){receipt.Warning="Setting acknowledgement was not confirmed. "+e.Message;}
        return receipt;
    }
    internal static string Model(string kind) { return kind=="mouse"?"ROG HARPE II EXTREME EDITION20":kind=="keyboard"?"ROG AZOTH EXTREME EDITION20":kind=="headset"?"ROG DELTA II-KJP":"ROG OMNI / Polling Rate Booster"; }
    internal static string Transport(int pid) { return pid==7546||pid==7586?"usb":pid==7548||pid==7588||pid==7489?"rf":"unknown"; }
    private static void Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false); Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Contains("--self-test")) { PeripheralSelfTest(); SelfTest(); ExtendedSelfTest(); ReadBoundarySelfTest(); GearIoSelfTest.Run(); return; }
        if (args.Contains("--inventory")) { Console.WriteLine(Json.Serialize(GearHid.Enumerate().Select(d=>d.Metadata()).ToArray())); return; }
        string line; while ((line=OpenAc.WorkerTextProtocol.ReadLine())!=null) {
            object id=null; string kind="", op="";
            try { if (line.Length>65536) throw new ArgumentException("Request too large."); var req=Json.Deserialize<Dictionary<string,object>>(line); req.TryGetValue("id",out id); kind=Text(req,"kind");op=Text(req,"op");
                object result=Handle(req,kind,op);if(GearBoundedIo.RestartRequired)throw new IOException("A pending HID operation did not finish after cancellation. The isolated device worker must be restarted.");
                Console.WriteLine(Json.Serialize(Obj("id",id,"ok",true,"kind",kind,"op",op,"data",result)));
            } catch(Exception e) { Console.WriteLine(Json.Serialize(Obj("id",id,"ok",false,"kind",kind,"op",op,"error",Obj("code",GearBoundedIo.RestartRequired?"io_restart_required":e is TimeoutException?"timeout":e is ArgumentException?"invalid_request":"device_error","message",e.Message)))); }
            if(GearBoundedIo.RestartRequired){Console.Out.Flush();Environment.Exit(4);}
        }
    }
    internal static object Handle(Dictionary<string,object> req,string kind,string op)
    {
        if (kind!="mouse"&&kind!="keyboard"&&kind!="headset"&&kind!="omni") throw new ArgumentException("Unsupported device kind.");
        if(op=="firmware") return FirmwareCatalog.Read(kind);
        var descriptors=Discover().Where(d=>Kind(d.Pid)==kind && d.Usage==1 && d.Page>=0xFF00 && d.Page<=0xFF02 && d.OutputLength>4 && (kind!="omni"||d.Page==0xFF02)).ToList();
        if(op=="scan") return Obj("devices",descriptors.Select(d=>Read(d)).ToArray());
        var target=descriptors.FirstOrDefault(d=>d.Id==Text(req,"deviceId"));
        if(target==null) throw new ArgumentException("Device is disconnected or the device ID is unknown. Scan again.");
        if(op=="read") return Read(target,req);
        if(op=="set") {
            object flag; if (!req.TryGetValue("experimental",out flag)||!(flag is bool)||(bool)flag!=true) throw new ArgumentException("Experimental device control must be explicitly enabled.");
            var before=Read(target,req);if(!(bool)before["online"])throw new ArgumentException("Peripheral is offline. Wake it before changing settings.");
            string setting=Text(req,"setting");object value;if(!req.TryGetValue("value",out value))throw new ArgumentException("Setting value is required.");
            if(!((string[])before["setCapabilities"]).Contains(setting))throw new ArgumentException("This setting cannot be safely changed with the current readback state.");
            byte[] packet=kind=="headset"?BuildHeadsetSetting(setting,value,(Dictionary<string,object>)before["snapshot"]):BuildPeripheralSetting(kind,setting,value,(Dictionary<string,object>)before["snapshot"]);
            if(MatchesSetting(setting,value,(Dictionary<string,object>)before["snapshot"])) {before["setting"]=setting;before["requestedValue"]=value;before["accepted"]=false;before["writeAttempted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}
            object noOp; if(req.TryGetValue("requireNoOp",out noOp)&&noOp is bool&&(bool)noOp)throw new ArgumentException("Read-only no-op validation rejected a changed value; no setting was written.");
            SettingReceipt receipt;using(var hid=new GearHid(target))receipt=hid.WriteSetting(target.SelectReport(kind=="headset"?(byte?)0xCC:null),packet);
            var after=Read(target,req);after["setting"]=setting;after["requestedValue"]=value;after["accepted"]=true;after["writeAttempted"]=true;
            bool match=MatchesSetting(setting,value,(Dictionary<string,object>)after["snapshot"]);
            after["accepted"]=receipt.OutputCompleted;after["settingAcknowledged"]=receipt.Acknowledged;after["settingRejected"]=receipt.Rejected;if(receipt.Warning!=null)((List<object>)after["errors"]).Add(Obj("field","settingAcknowledgement","message",receipt.Warning));
            after["writeConfirmed"]=receipt.OutputCompleted&&!receipt.Rejected&&(bool)after["online"]&&match;after["acceptanceEvidence"]="HID output completion, matching acknowledgement, and current-value readback are separate results.";
            after["appliedState"]=(bool)after["writeConfirmed"]?"confirmed":"unconfirmed";
            return after;
        }
        throw new ArgumentException("Unsupported operation.");
    }
    internal static List<GearDescriptor> Discover()
    {
        var all=GearHid.Enumerate();var routed=new List<GearDescriptor>();
        foreach(var hub in all.Where(d=>Kind(d.Pid)=="omni"&&d.Page==0xFF02&&d.Usage==1&&d.OutputReports.Contains((byte)1))) {
            try { using(var hid=new GearHid(hub)) {byte[] b=hid.Query(1,Packet(0xA0,0,0));Require(b,4);int count=U16(b,2);if(count>14)throw new IOException("Invalid paired device count.");Require(b,4+count*4);
                for(int i=0;i<count;i++){int off=4+i*4,pid=U16(b,off),report=b[off+2],eventReport=b[off+3];string kind=Kind(pid);if(kind!="mouse"&&kind!="keyboard")continue;
                    // OMNI assigns report IDs to paired routes; the collection's usage page
                    // must not be assumed to identify the routed peripheral type.
                    var matches=all.Where(d=>d.Pid==hub.Pid&&d.Page>=0xFF00&&d.Page<=0xFF02&&d.Usage==1&&d.OutputReports.Contains((byte)report)).ToList();
                    // Without a unique matching physical interface, no command is routed.
                    if(matches.Count!=1)continue;var control=matches[0];routed.Add(new GearDescriptor{Path=control.Path,Id=control.Id+"-"+pid,Product=control.Product,Pid=(ushort)pid,ReceiverPid=hub.Pid,ReceiverId=hub.Id,Page=control.Page,Usage=control.Usage,InputLength=control.InputLength,OutputLength=control.OutputLength,FeatureLength=control.FeatureLength,InputReports=control.InputReports,OutputReports=new byte[]{(byte)report},EventReport=eventReport});
                }
            }} catch(Exception) { /* A failed pairing query never invents a route. */ }
        }
        all.AddRange(routed);return all;
    }
    internal static Dictionary<string,object> Read(GearDescriptor d,Dictionary<string,object> request=null)
    {
        string kind=Kind(d.Pid); var state=d.Metadata();
        var snapshot=Obj("modelPid",(int)d.Pid,"batteryPercent",null,"charging",null,"sleepMinutes",null,"sleepNever",null,"eqEnabled",null,"eqGains",null,"eqRawGains",null,"pollingRate",null,"dpiStages",null,"activeDpiStage",null,"angleSnapping",null,"motionSync",null,"firmware",null,"dongleFirmware",null);
        var errors=new List<object>(); var capabilities=new List<string>(); bool online=false, confirmed=false;
        state["snapshot"]=snapshot;state["capabilities"]=capabilities;state["readCapabilities"]=capabilities;state["setCapabilities"]=new string[0];state["settingOptions"]=FeatureOptions(kind,d.Pid);state["errors"]=errors;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;state["connected"]=true;state["online"]=false;
        if(kind=="omni") {
            try{using(var hid=new GearHid(d)){byte[] b=hid.Query(1,Packet(0xA0,0,0));Require(b,4);int count=U16(b,2);if(count>14)throw new IOException("Invalid paired device count.");Require(b,4+count*4);var pairs=new List<object>();for(int i=0;i<count;i++){int o=4+i*4,pid=U16(b,o);pairs.Add(Obj("pid",pid,"kind",Kind(pid),"model",Kind(pid)=="unknown"?"Unknown paired device":Model(Kind(pid)),"controlReportId",(int)b[o+2],"eventReportId",(int)b[o+3]));}snapshot["pairedDevices"]=pairs;state["online"]=true;state["readbackConfirmed"]=true;capabilities.Add("pairedDevices");QueryField(hid,1,Packet(0xA1,1,0),"firmware",errors,x=>{Require(x,8);snapshot["firmware"]=VersionNative4(x,4);});}}
            catch(Exception e){errors.Add(Obj("field","pairing","message",e.Message));}return state;
        }
        try {
            using(var hid=new GearHid(d)) {
                byte report=d.SelectReport(kind=="headset"?(byte?)0xCC:null);
                byte[] alive=hid.Query(report,kind=="headset"?Packet(0x12,0,0x100):kind=="keyboard"?Packet(0x12,3,0):Packet(0x12,0,2));
                if(alive.Length<5 || (kind=="keyboard" ? alive[4]!=0&&alive[4]!=1&&alive[4]!=2&&alive[4]!=255 : alive[4]>1)) throw new IOException("Device returned an invalid connection status; no settings are trusted.");
                confirmed=true;online=kind=="keyboard"?alive[4]!=255:alive[4]==1;
                if(!online) {state["connectionMessage"]="Receiver is present; device is asleep, disconnected, or paired to a different receiver. Wake it and select RF 2.4 GHz.";return state;}
                QueryField(hid,report,Packet(0x12,0,0),"firmware",errors, b=>{
                    if(kind=="headset") { Require(b,12);snapshot["firmware"]=VersionReverse(b,4);snapshot["dongleFirmware"]=VersionReverse(b,8); }
                    else ApplyPeripheralInfo(kind,d.Pid,b,snapshot);
                    if(snapshot["firmware"]!=null)capabilities.Add("firmware");else errors.Add(Obj("field","firmware","message","Device firmware information is not ready; its zero value is not a confirmed version."));
                    if(kind=="mouse"&&snapshot["activeDpiStage"] is int)capabilities.Add("activeDpiStage");
                    if(kind=="mouse"&&snapshot["currentDpi"]==null)errors.Add(Obj("field","currentDpi","message","The device info DPI value is outside the verified range; no current DPI is confirmed."));
                });
                if(kind=="headset") {
                    QueryField(hid,report,Packet(0x12,7,0),"power",errors,b=>{Require(b,8);if(b[5]>100)throw new IOException("Battery value is outside 0..100.");snapshot["batteryPercent"]=(int)b[5];snapshot["sleepModeCode"]=(int)b[4];int[]sleep={2,3,5,10,15,0};snapshot["sleepMinutes"]=b[4]<sleep.Length?(object)sleep[b[4]]:null;snapshot["sleepNever"]=b[4]==5;snapshot["lowBatteryThreshold"]=(int)b[6];snapshot["lowBatteryVoicePrompt"]=b[7]==1;capabilities.AddRange(new[]{"battery","sleepMinutes"});});
                    QueryField(hid,report,Packet(0x12,8,0),"charging",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown charging state.");snapshot["charging"]=b[4]==1;capabilities.Add("charging");});
                    QueryField(hid,report,Packet(0x12,33,0),"equalizer",errors,b=>{Require(b,15);if(b[4]>1||b.Skip(5).Take(10).Any(v=>v>100))throw new IOException("Unknown equalizer data.");snapshot["eqEnabled"]=b[4]==1;snapshot["eqRawGains"]=b.Skip(5).Take(10).Select(v=>(int)v).ToArray();snapshot["eqLevels"]=b.Skip(7).Take(8).Select(v=>(int)v).ToArray();snapshot["eqBandHz"]=new int[]{125,250,500,1000,2000,4000,8000,16000};capabilities.AddRange(new[]{"eqEnabled","eqLevels","eqRawGains"});});
                    QueryField(hid,report,Packet(0x12,3,0),"lighting",errors,b=>{Require(b,9);snapshot["lightingEffectCode"]=(int)b[4];snapshot["brightnessRaw"]=(int)b[5];snapshot["color"]=b.Skip(6).Take(3).Select(v=>(int)v).ToArray();capabilities.Add("lighting");});
                    QueryField(hid,report,Packet(0x12,19,0),"lightingEnabled",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown lighting switch.");snapshot["lightingEnabled"]=b[4]==1;capabilities.Add("lightingEnabled");});
                    QueryField(hid,report,Packet(0x41,32,0),"microphone",errors,b=>{Require(b,6);if(b[4]>1||b[5]>2)throw new IOException("Unknown noise reduction state.");snapshot["noiseReductionEnabled"]=b[4]==1;snapshot["noiseReductionRaw"]=(int)b[5];capabilities.Add("noiseReduction");});
                    QueryField(hid,report,Packet(0x12,40,0),"voicePrompt",errors,b=>{Require(b,5);if(b[4]>2)throw new IOException("Unknown voice prompt language.");snapshot["voicePrompt"]=(int)b[4];capabilities.Add("voicePrompt");});
                    QueryField(hid,report,Packet(0x12,36,0),"sideToneEnabled",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown sidetone switch.");snapshot["sideToneEnabled"]=b[4]==1;capabilities.Add("sideToneEnabled");});
                    QueryField(hid,report,Packet(0x12,25,0),"sideToneLevel",errors,b=>{Require(b,5);if(b[4]>20)throw new IOException("Unknown sidetone level.");snapshot["sideToneLevel"]=(int)b[4];capabilities.Add("sideToneLevel");});
                    QueryField(hid,report,Packet(0x12,82,0),"latencyMs",errors,b=>{Require(b,5);if(!new int[]{40,60,80}.Contains(b[4]))throw new IOException("Unknown headset latency mode.");snapshot["latencyMs"]=(int)b[4];capabilities.Add("latencyMs");});
                    QueryField(hid,report,Packet(0x12,51,0),"auraEnabled",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown Aura switch.");snapshot["auraEnabled"]=b[4]==1;capabilities.Add("auraEnabled");});
                } else if(kind=="mouse") {
                    QueryField(hid,report,Packet(0x12,4,0),"performance",errors,b=>{Require(b,19);if(b[16]>1)throw new IOException("Unknown angle snapping switch.");snapshot["dpiStages"]=new int[]{U16(b,4)+1,U16(b,6)+1,U16(b,8)+1,U16(b,10)+1}; int packed=U16(b,12),index=d.ReceiverPid==6864||d.ReceiverPid==7508?(packed>>4)&15:packed&15;int[]rates={125,250,500,1000,2000,4000,8000};snapshot["pollingRate"]=index<rates.Length?(object)rates[index]:null;snapshot["pollingRateCode"]=index;snapshot["pollingRateUsbRfCode"]=packed&15;snapshot["pollingRateBoosterCode"]=(packed>>4)&15;snapshot["angleSnapping"]=b[16]==1;snapshot["buttonResponseMs"]=(U16(b,14)+1)*4;snapshot["angleTuning"]=(int)(sbyte)b[18];capabilities.AddRange(new[]{"dpiStages","pollingRate","angleSnapping"});});
                    QueryField(hid,report,Packet(0x12,4,2),"splitDpi",errors,b=>{Require(b,20);snapshot["dpiStages"]=Enumerable.Range(0,4).Select(i=>U16(b,4+i*4)+1).ToArray();snapshot["dpiStagesY"]=Enumerable.Range(0,4).Select(i=>U16(b,6+i*4)+1).ToArray();capabilities.Add("splitDpi");});
                    QueryField(hid,report,Packet(0x12,4,3),"dpiColors",errors,b=>{Require(b,16);snapshot["dpiColors"]=Enumerable.Range(0,4).Select(i=>b.Skip(4+i*3).Take(3).Select(v=>(int)v).ToArray()).ToArray();capabilities.Add("dpiColors");});
                    QueryField(hid,report,Packet(0x12,4,4),"motionSync",errors,b=>{Require(b,5);if(b[4]>1)throw new IOException("Unknown motion sync switch.");snapshot["motionSync"]=b[4]==1;capabilities.Add("motionSync");});
                    QueryField(hid,report,Packet(0x12,7,0),"power",errors,b=>{Require(b,14);if(b[4]>100)throw new IOException("Invalid battery value.");snapshot["batteryPercent"]=(int)b[4];snapshot["charging"]=b[9]==1;snapshot["sleepModeCode"]=(int)b[5];int[]sleep={1,2,3,5,10};snapshot["sleepMinutes"]=b[5]<sleep.Length?(object)sleep[b[5]]:b[5]==255?(object)0:null;snapshot["sleepNever"]=b[5]==255;snapshot["lowBatteryThreshold"]=(int)b[6];snapshot["fullChargingEffectCode"]=(int)b[11];snapshot["wakeOnMotionCode"]=(int)b[12];snapshot["chargingLightingCode"]=(int)b[13];capabilities.AddRange(new[]{"battery","charging","sleepMinutes"});});
                } else if(kind=="keyboard") {
                    QueryField(hid,report,Packet(0x12,1,0),"power",errors,b=>{Require(b,10);if(b[5]>100)throw new IOException("Invalid keyboard battery.");snapshot["batteryPercent"]=(int)b[5];snapshot["charging"]=b[8]==1;int[]sleep={1,2,3,5,10};snapshot["sleepModeCode"]=(int)b[6];snapshot["sleepMinutes"]=b[6]<sleep.Length?(object)sleep[b[6]]:b[6]==255?(object)0:null;snapshot["sleepNever"]=b[6]==255;snapshot["powerSavingModeCode"]=(int)b[7];snapshot["lowBatteryThreshold"]=(int)b[9];capabilities.AddRange(new[]{"battery","charging","sleepMinutes"});});
                    QueryField(hid,report,Packet(0x12,21,0),"pollingRate",errors,b=>{Require(b,5);int[]rates={1000,2000,4000,8000};if(b[4]>=rates.Length)throw new IOException("Unknown keyboard polling rate.");snapshot["pollingRateCode"]=(int)b[4];snapshot["pollingRateRaw"]=rates[b[4]];if(!KeyboardPollingRates(d.Pid).Contains(rates[b[4]]))throw new IOException("Polling rate is outside the verified options for this keyboard model.");snapshot["pollingRate"]=rates[b[4]];capabilities.Add("pollingRate");});
                }
                if(kind=="mouse"||kind=="keyboard")ReadFeatures(hid,d,report,kind,request,snapshot,capabilities,errors);
            }
        } catch(Exception e) {errors.Add(Obj("field","connection","message",e.Message));state["connectionMessage"]="Device state could not be read. Settings remain disabled.";}
        finally {state["online"]=online;state["readbackConfirmed"]=confirmed;}
        if(kind=="headset"&&online){var setters=new List<string>();if(capabilities.Contains("eqEnabled"))setters.Add("eqEnabled");if(capabilities.Contains("eqLevels"))setters.Add("eqLevels");if(capabilities.Contains("sleepMinutes")&&snapshot["sleepMinutes"]!=null&&new int[]{0,10,20,30,40,50}.Contains((int)snapshot["lowBatteryThreshold"]))setters.AddRange(new[]{"sleepMinutes","lowBatteryThreshold","lowBatteryVoicePrompt"});if(capabilities.Contains("lighting")&&(int)snapshot["brightnessRaw"]<=100&&(int)snapshot["lightingEffectCode"]<=5){setters.Add("brightness");int effect=(int)snapshot["lightingEffectCode"];if(effect>=1&&effect<=5)setters.Add("lightingEffect");if(effect>=1&&effect<4)setters.Add("lightingColor");else if(effect==0)snapshot["lightingModeMappingUnconfirmed"]=true;}if(capabilities.Contains("noiseReduction"))setters.AddRange(new[]{"noiseReductionEnabled","noiseReductionRaw"});foreach(string setter in new[]{"lightingEnabled","voicePrompt","sideToneEnabled","sideToneLevel","latencyMs","auraEnabled"})if(capabilities.Contains(setter))setters.Add(setter);state["setCapabilities"]=setters.ToArray();}
        if((kind=="mouse"||kind=="keyboard")&&online){var setters=new List<string>();if(capabilities.Contains("pollingRate")&&snapshot["pollingRate"]!=null)setters.Add("pollingRate");if(capabilities.Contains("sleepMinutes")&&snapshot["sleepMinutes"]!=null)setters.Add("sleepMinutes");if(kind=="mouse"){if(capabilities.Contains("angleSnapping"))setters.Add("angleSnapping");if(capabilities.Contains("motionSync")&&snapshot["pollingRate"]!=null)setters.Add("motionSync");if(capabilities.Contains("splitDpi")&&capabilities.Contains("dpiColors"))setters.Add("dpiStage");object stage,count;if(snapshot.TryGetValue("activeDpiStage",out stage)&&snapshot.TryGetValue("dpiStageCount",out count)&&stage is int&&count is int&&(int)stage>=1&&(int)stage<=4&&(int)count>=1&&(int)count<=4)setters.Add("activeDpiStage");}state["setCapabilities"]=setters.ToArray();}
        AddFeatureSetCapabilities(kind,online,state,snapshot,capabilities);
        return state;
    }
    internal static bool MatchesSetting(string setting,object value,Dictionary<string,object> snapshot)
    {
        bool? featureMatch=MatchesFeatureSetting(setting,value,snapshot);if(featureMatch.HasValue)return featureMatch.Value;
        if(setting=="dpiStage"){var request=value as Dictionary<string,object>;object x,y;if(request==null||!snapshot.TryGetValue("dpiStages",out x)||!snapshot.TryGetValue("dpiStagesY",out y)||!(x is int[])||!(y is int[]))return false;int index=Integer(request["index"],0,3),dpi=Integer(request["dpi"],100,65000);return ((int[])x).Length==4&&((int[])y).Length==4&&((int[])x)[index]==dpi&&((int[])y)[index]==dpi;}
        string key=setting=="brightness"?"brightnessRaw":setting=="lightingEffect"?"lightingEffectCode":setting=="lightingColor"?"color":setting;object actual;return snapshot.TryGetValue(key,out actual)&&actual!=null&&Json.Serialize(actual)==Json.Serialize(value);
    }
    internal static byte[] BuildPeripheralSetting(string kind,string setting,object value,Dictionary<string,object> before)
    {
        byte[] feature=BuildFeatureSetting(kind,setting,value,before);if(feature!=null)return feature;
        if(setting=="pollingRate"){int rate=Integer(value,125,8000);int[]rates=kind=="keyboard"?new int[]{1000,2000,4000,8000}:new int[]{125,250,500,1000,2000,4000,8000};int code=Array.IndexOf(rates,rate);if(code<0)throw new ArgumentException("Unsupported polling rate.");if(kind=="keyboard"){object model;if(!before.TryGetValue("modelPid",out model)||!(model is int)||!KeyboardPollingRates((int)model).Contains(rate))throw new ArgumentException("Polling rate is not a verified option for the current keyboard model.");}object sync;if(kind=="mouse"&&rate==8000&&before.TryGetValue("motionSync",out sync)&&sync is bool&&(bool)sync)throw new ArgumentException("Disable Motion Sync before selecting 8000 Hz.");return Packet(0x51,49,kind=="keyboard"?(ushort)0:(ushort)4,(byte)code,0);}
        if(setting=="sleepMinutes"){int minutes=Integer(value,0,10);int[]sleep={1,2,3,5,10};int code=minutes==0?255:Array.IndexOf(sleep,minutes);if(code<0)throw new ArgumentException("Allowed sleep minutes: 0, 1, 2, 3, 5, 10.");if(kind=="keyboard")return Packet(0x51,56,0,(byte)code);return Packet(0x51,55,0,(byte)code,0,(byte)(int)before["lowBatteryThreshold"],0,(byte)(int)before["fullChargingEffectCode"],(byte)(int)before["wakeOnMotionCode"],(byte)(int)before["chargingLightingCode"]);}
        if(kind!="mouse")throw new ArgumentException("Unsupported keyboard setting.");
        if(setting=="angleSnapping"||setting=="motionSync"){if(!(value is bool))throw new ArgumentException("Switch must be boolean.");if(setting=="motionSync"&&(bool)value&&(int)before["pollingRate"]==8000)throw new ArgumentException("Motion Sync is unavailable at 8000 Hz.");return Packet(0x51,49,setting=="angleSnapping"?(ushort)6:(ushort)18,(byte)((bool)value?1:0));}
        if(setting=="activeDpiStage"){int stage=Integer(value,1,4);if(stage>(int)before["dpiStageCount"])throw new ArgumentException("DPI stage is inactive in the current profile.");return Packet(0x51,49,9,(byte)stage,0);}
        if(setting=="dpiStage"){var request=value as Dictionary<string,object>;if(request==null||!request.ContainsKey("index")||!request.ContainsKey("dpi"))throw new ArgumentException("dpiStage requires index and dpi.");int index=Integer(request["index"],0,3),dpi=Integer(request["dpi"],100,65000),encoded=dpi-1;var colors=(int[][])before["dpiColors"];if(colors.Length!=4||colors[index].Length!=3||colors[index].Any(v=>v<0||v>255))throw new ArgumentException("Previous DPI color is invalid.");return Packet(0x51,49,(ushort)(12+index),(byte)encoded,(byte)(encoded>>8),(byte)encoded,(byte)(encoded>>8),(byte)colors[index][0],(byte)colors[index][1],(byte)colors[index][2]);}
        throw new ArgumentException("Unsupported mouse setting.");
    }
    internal static byte[] BuildHeadsetSetting(string setting,object value,Dictionary<string,object> before)
    {
        if(setting=="eqEnabled"||setting=="noiseReductionEnabled"||setting=="lightingEnabled"||setting=="sideToneEnabled"||setting=="auraEnabled") {if(!(value is bool))throw new ArgumentException("Switch setting must be boolean.");byte command=setting=="eqEnabled"||setting=="noiseReductionEnabled"||setting=="sideToneEnabled"?(byte)0x41:(byte)0x51;byte key=setting=="eqEnabled"?(byte)3:setting=="noiseReductionEnabled"?(byte)2:setting=="lightingEnabled"?(byte)16:setting=="sideToneEnabled"?(byte)17:(byte)51;return Packet(command,key,0,(byte)((bool)value?1:0));}
        if(setting=="eqLevels") {var values=value as IEnumerable;if(values==null||value is string)throw new ArgumentException("eqLevels must contain eight integer levels.");var list=new List<int>();foreach(object v in values)list.Add(Integer(v,0,100));if(list.Count!=8)throw new ArgumentException("eqLevels must contain eight integer levels.");var prior=(int[])before["eqRawGains"];if(prior.Length!=10||prior.Any(v=>v<0||v>100))throw new ArgumentException("Previous EQ data is invalid.");return Packet(0x41,4,0,prior.Take(2).Concat(list).Select(v=>(byte)v).ToArray());}
        if(setting=="sleepMinutes"||setting=="lowBatteryThreshold"||setting=="lowBatteryVoicePrompt"){int code=(int)before["sleepModeCode"],threshold=(int)before["lowBatteryThreshold"];bool voice=(bool)before["lowBatteryVoicePrompt"];if(setting=="sleepMinutes"){int minutes=Integer(value,0,15);int[] modes={2,3,5,10,15,0};code=Array.IndexOf(modes,minutes);if(code<0)throw new ArgumentException("Allowed sleep minutes: 0 (never), 2, 3, 5, 10, 15.");}else if(setting=="lowBatteryThreshold"){threshold=Integer(value,0,50);if(threshold%10!=0)throw new ArgumentException("Low battery threshold must use increments of ten.");}else {if(!(value is bool))throw new ArgumentException("Voice prompt switch must be boolean.");voice=(bool)value;}return Packet(0x51,55,0,(byte)code,(byte)threshold,(byte)(voice?1:0));}
        if(setting=="brightness"||setting=="lightingEffect"||setting=="lightingColor"){int level=(int)before["brightnessRaw"],effect=(int)before["lightingEffectCode"];var rgb=(int[])before["color"];if(rgb.Length!=3||rgb.Any(v=>v<0||v>255))throw new ArgumentException("Previous RGB data is invalid.");if(setting=="brightness"){level=Integer(value,0,100);if(level%5!=0)throw new ArgumentException("Brightness must use increments of five.");}else if(setting=="lightingEffect")effect=Integer(value,1,5);else{var values=value as IEnumerable;if(values==null||value is string)throw new ArgumentException("Color must contain three integer bytes.");var list=new List<int>();foreach(object v in values)list.Add(Integer(v,0,255));if(list.Count!=3)throw new ArgumentException("Color must contain three integer bytes.");rgb=list.ToArray();}return Packet(0x51,40,0,new byte[]{(byte)effect,(byte)level,(byte)rgb[0],(byte)rgb[1],(byte)rgb[2]});}
        if(setting=="noiseReductionRaw")return Packet(0x41,16,0,(byte)Integer(value,0,2));
        if(setting=="voicePrompt")return Packet(0x41,10,0,(byte)Integer(value,0,2));
        if(setting=="sideToneLevel")return Packet(0x61,17,0,(byte)Integer(value,0,20));
        if(setting=="latencyMs"){int latency=Integer(value,40,80);if(!new int[]{40,60,80}.Contains(latency))throw new ArgumentException("Latency must be 40, 60, or 80 ms.");return Packet(0x51,82,0,(byte)latency);}
        throw new ArgumentException("Unsupported setting.");
    }
    internal static int Integer(object v,int min,int max){if(v is bool||v is string||v==null)throw new ArgumentException("An integer value is required.");double number=Convert.ToDouble(v);if(Double.IsNaN(number)||Double.IsInfinity(number)||number!=Math.Truncate(number)||number<min||number>max)throw new ArgumentException("Setting is outside its allowed range.");return (int)number;}
    internal static byte[] Packet(byte command,byte key,ushort index,params byte[] data) {var b=new byte[4+data.Length];b[0]=command;b[1]=key;b[2]=(byte)index;b[3]=(byte)(index>>8);Array.Copy(data,0,b,4,data.Length);return b;}
    private static void QueryField(GearHid h,byte report,byte[] packet,string field,List<object> errors,Action<byte[]> parse) {try{parse(h.Query(report,packet));}catch(Exception e){errors.Add(Obj("field",field,"message",e.Message));}}
    internal static void Require(byte[] b,int length) {if(b.Length<length)throw new IOException("Response is too short.");}
    internal static ushort U16(byte[] b,int offset){Require(b,offset+2);return (ushort)(b[offset]|b[offset+1]<<8);}
    internal static string VersionReverse(byte[] b,int offset) {Require(b,offset+4);return String.Join(".",b.Skip(offset).Take(4).Reverse().Select(v=>v.ToString("x")));}
    internal static string Version3(byte[] b,int offset){Require(b,offset+3);return b[offset+2].ToString("x")+"."+b[offset+1].ToString("x2")+"."+b[offset].ToString("x2");}
    internal static string VersionNative4(byte[] b,int offset){Require(b,offset+4);if(b.Skip(offset).Take(4).All(v=>v==0))return null;return U16(b,offset+2).ToString("x")+"."+b[offset+1].ToString("x2")+"."+b[offset].ToString("x2");}
    internal static void ApplyPeripheralInfo(string kind,int pid,byte[] b,Dictionary<string,object> snapshot)
    {
        if(kind=="mouse") {
            Require(b,19);snapshot["firmware"]=VersionNative4(b,pid==7546?4:14);snapshot["dongleFirmware"]=pid==7546?null:VersionNative4(b,4);
            int count=b[18],stage=b[11]+1,dpi=U16(b,12);snapshot["dpiStageCountRaw"]=count;snapshot["activeDpiStageRaw"]=stage;snapshot["currentDpiRaw"]=dpi;
            snapshot["dpiStageCount"]=count>=1&&count<=4?(object)count:null;snapshot["activeDpiStage"]=count>=1&&count<=4&&stage<=count?(object)stage:null;
            snapshot["currentDpi"]=dpi>=100&&dpi<=65000?(object)dpi:null;snapshot["currentDpiUnconfirmed"]=snapshot["currentDpi"]==null;
        } else if(kind=="keyboard") {
            Require(b,15);snapshot["firmware"]=VersionNative4(b,pid==7586?4:11);snapshot["dongleFirmware"]=pid==7586?null:VersionNative4(b,4);
            int count=b[8],profile=b[10];snapshot["profileCountRaw"]=count;snapshot["activeProfileRaw"]=profile;
            snapshot["profileCount"]=count>0?(object)count:null;snapshot["activeProfile"]=count>0&&profile<=count?(object)profile:null;snapshot["profileInformationUnconfirmed"]=count==0||profile>count;
            snapshot["lightingEffectCode"]=(int)b[9];
        } else throw new ArgumentException("Unsupported peripheral information kind.");
    }
    internal static void ReadBoundarySelfTest()
    {
        int tests=0;Action<bool> check=v=>{if(!v)throw new Exception("Synthetic read-boundary regression failed at case "+(tests+1)+".");tests++;};
        var mouse=new byte[19];mouse[4]=10;mouse[5]=0;mouse[6]=9;mouse[11]=2;mouse[12]=255;mouse[13]=255;mouse[14]=0x15;mouse[16]=1;mouse[18]=4;
        var state=Obj();ApplyPeripheralInfo("mouse",7548,mouse,state);check(state["currentDpi"]==null&&(int)state["currentDpiRaw"]==65535&&(bool)state["currentDpiUnconfirmed"]);check((int)state["activeDpiStage"]==3&&(int)state["dpiStageCount"]==4);check((string)state["firmware"]=="1.00.15"&&(string)state["dongleFirmware"]=="9.00.0a");
        mouse[12]=100;mouse[13]=0;ApplyPeripheralInfo("mouse",7548,mouse,state);check((int)state["currentDpi"]==100&&!(bool)state["currentDpiUnconfirmed"]);mouse[12]=232;mouse[13]=253;ApplyPeripheralInfo("mouse",7548,mouse,state);check((int)state["currentDpi"]==65000);mouse[12]=99;mouse[13]=0;ApplyPeripheralInfo("mouse",7548,mouse,state);check(state["currentDpi"]==null);
        mouse[18]=0;ApplyPeripheralInfo("mouse",7548,mouse,state);check(state["dpiStageCount"]==null&&state["activeDpiStage"]==null);mouse[18]=2;ApplyPeripheralInfo("mouse",7548,mouse,state);check(state["activeDpiStage"]==null);mouse[18]=5;ApplyPeripheralInfo("mouse",7548,mouse,state);check(state["dpiStageCount"]==null&&state["activeDpiStage"]==null);
        var keyboard=new byte[15];keyboard[4]=8;keyboard[6]=7;state=Obj();ApplyPeripheralInfo("keyboard",7588,keyboard,state);check(state["firmware"]==null&&state["profileCount"]==null&&state["activeProfile"]==null&&(bool)state["profileInformationUnconfirmed"]);check((string)state["dongleFirmware"]=="7.00.08");keyboard[8]=6;keyboard[10]=6;keyboard[11]=9;keyboard[13]=1;ApplyPeripheralInfo("keyboard",7588,keyboard,state);check((string)state["firmware"]=="1.00.09"&&(int)state["profileCount"]==6&&(int)state["activeProfile"]==6&&!(bool)state["profileInformationUnconfirmed"]);keyboard[10]=7;ApplyPeripheralInfo("keyboard",7588,keyboard,state);check(state["activeProfile"]==null&&(bool)state["profileInformationUnconfirmed"]);
        check(VersionNative4(new byte[4],0)==null);check(VersionNative4(new byte[]{9,0,1,0},0)=="1.00.09");check(VersionNative4(new byte[]{9,0,1,2},0)=="201.00.09");
        var device=Obj("setCapabilities",new string[]{"pollingRate","sleepMinutes"});var caps=new List<string>{"oledMode","oledPreset","leverMode"};AddFeatureSetCapabilities("keyboard",true,device,Obj(),caps);var setters=(string[])device["setCapabilities"];check(setters.Contains("oledMode")&&setters.Contains("oledPreset")&&setters.Contains("leverMode"));check(new string[]{"oledEnabled","oledBrightness","oledBarItem","oledModeEnabled","oledKpsEnabled","leverModeEnabled"}.All(s=>!setters.Contains(s)));
        Console.WriteLine(Json.Serialize(Obj("ok",true,"hardwareAccess",false,"suite","read_boundaries","tests",tests)));
    }
    internal static void PeripheralSelfTest()
    {
        int tests=0;Action<bool> check=v=>{if(!v)throw new Exception("Synthetic peripheral packet test failed.");tests++;};
        var before=Obj("modelPid",7588,"dpiColors",new int[][]{new int[]{1,2,3},new int[]{10,20,30},new int[]{40,50,60},new int[]{70,80,90}},"dpiStageCount",4,"motionSync",false,"pollingRate",1000,"lowBatteryThreshold",20,"fullChargingEffectCode",1,"wakeOnMotionCode",1,"chargingLightingCode",255);
        check(BuildPeripheralSetting("mouse","dpiStage",Obj("index",1,"dpi",1600),before).SequenceEqual(new byte[]{81,49,13,0,63,6,63,6,10,20,30}));
        check(BuildPeripheralSetting("mouse","pollingRate",8000,before).SequenceEqual(new byte[]{81,49,4,0,6,0}));
        check(BuildPeripheralSetting("keyboard","pollingRate",8000,before).SequenceEqual(new byte[]{81,49,0,0,3,0}));
        check(BuildPeripheralSetting("mouse","sleepMinutes",0,before).SequenceEqual(new byte[]{81,55,0,0,255,0,20,0,1,1,255}));
        check(BuildPeripheralSetting("keyboard","sleepMinutes",5,before).SequenceEqual(new byte[]{81,56,0,0,3}));
        check(BuildPeripheralSetting("mouse","activeDpiStage",4,before).SequenceEqual(new byte[]{81,49,9,0,4,0}));
        check(BuildPeripheralSetting("mouse","motionSync",true,before).SequenceEqual(new byte[]{81,49,18,0,1}));
        before["pollingRate"]=8000;bool blocked=false;try{BuildPeripheralSetting("mouse","motionSync",true,before);}catch(ArgumentException){blocked=true;}check(blocked);
        foreach(int bad in new int[]{99,65001}){blocked=false;try{BuildPeripheralSetting("mouse","dpiStage",Obj("index",0,"dpi",bad),before);}catch(ArgumentException){blocked=true;}check(blocked);}
        before["dpiStages"]=new int[]{400,800,1600,3200};before["dpiStagesY"]=new int[]{400,800,1600,3200};check(MatchesSetting("dpiStage",Obj("index",2,"dpi",1600),before));((int[])before["dpiStagesY"])[2]=1601;check(!MatchesSetting("dpiStage",Obj("index",2,"dpi",1600),before));check(!MatchesSetting("eqEnabled",false,before));before["eqEnabled"]=false;check(MatchesSetting("eqEnabled",false,before));check(!MatchesSetting("eqEnabled",true,before));
        Console.WriteLine(Json.Serialize(Obj("ok",true,"hardwareAccess",false,"suite","peripheral_packets","tests",tests)));
    }
    internal static void SelfTest(){int tests=0;Action<bool> check=v=>{if(!v)throw new Exception("Synthetic protocol test failed.");tests++;};check(Packet(0x12,0,0x100).SequenceEqual(new byte[]{18,0,0,1}));check(Packet(0x12,4,0).SequenceEqual(new byte[]{18,4,0,0}));var previous=Obj("eqRawGains",new int[]{33,44,50,50,50,50,50,50,50,50},"sleepModeCode",2,"lowBatteryThreshold",20,"lowBatteryVoicePrompt",true,"lightingEffectCode",2,"brightnessRaw",50,"color",new int[]{82,0,111});check(BuildHeadsetSetting("eqLevels",new int[]{0,10,20,30,40,50,60,100},previous).SequenceEqual(new byte[]{65,4,0,0,33,44,0,10,20,30,40,50,60,100}));check(BuildHeadsetSetting("eqEnabled",true,previous).SequenceEqual(new byte[]{65,3,0,0,1}));check(BuildHeadsetSetting("sleepMinutes",0,previous).SequenceEqual(new byte[]{81,55,0,0,5,20,1}));check(BuildHeadsetSetting("brightness",50,previous).SequenceEqual(new byte[]{81,40,0,0,2,50,82,0,111}));check(BuildHeadsetSetting("lowBatteryThreshold",30,previous).SequenceEqual(new byte[]{81,55,0,0,2,30,1}));check(BuildHeadsetSetting("lightingColor",new int[]{10,20,30},previous).SequenceEqual(new byte[]{81,40,0,0,2,50,10,20,30}));check(BuildHeadsetSetting("lightingEffect",5,previous).SequenceEqual(new byte[]{81,40,0,0,5,50,82,0,111}));check(BuildHeadsetSetting("sideToneLevel",20,previous).SequenceEqual(new byte[]{97,17,0,0,20}));check(BuildHeadsetSetting("noiseReductionRaw",2,previous).SequenceEqual(new byte[]{65,16,0,0,2}));check(BuildHeadsetSetting("latencyMs",60,previous).SequenceEqual(new byte[]{81,82,0,0,60}));foreach(object bad in new object[]{-1,101,50.5,"50",true}){bool rejected=false;try{BuildHeadsetSetting("brightness",bad,previous);}catch(ArgumentException){rejected=true;}check(rejected);}check(!Version3(new byte[]{8,0,7},0).StartsWith("0."));Console.WriteLine(Json.Serialize(Obj("ok",true,"hardwareAccess",false,"tests",tests))); }
}

internal sealed class GearDescriptor
{
    internal string Path,Id,Product,ReceiverId;internal ushort Pid,ReceiverPid,Page,Usage;internal int InputLength,OutputLength,FeatureLength,EventReport;internal byte[] InputReports,OutputReports;
    internal byte SelectReport(byte? expected) { if(expected.HasValue) {if(!OutputReports.Contains(expected.Value))throw new IOException("Expected report ID is absent from descriptor.");return expected.Value;} if(OutputReports.Length!=1)throw new IOException("Control report ID is ambiguous.");return OutputReports[0]; }
    internal Dictionary<string,object> Metadata(){string k=GearLinkWorker.Kind(Pid);return GearLinkWorker.Obj("deviceId",Id,"model",GearLinkWorker.Model(k),"kind",k,"pid",(int)Pid,"receiverPid",ReceiverPid==0?null:(object)(int)ReceiverPid,"receiverId",ReceiverId,"product",Product,"transport",GearLinkWorker.Transport(Pid),"route",ReceiverPid==0?"direct":"omni","usagePage",(int)Page,"usage",(int)Usage,"inputLength",InputLength,"outputLength",OutputLength,"inputReports",InputReports.Select(v=>(int)v).ToArray(),"outputReports",OutputReports.Select(v=>(int)v).ToArray());}
}

internal sealed class GearHid : IDisposable
{
    private static readonly List<GearHid> RetainedPoisoned = new List<GearHid>();
    private SafeFileHandle handle;private Mutex ownership;private bool ownsMutex;private readonly GearDescriptor descriptor;
    internal GearHid(GearDescriptor d){descriptor=d;if(GearBoundedIo.RestartRequired)throw new IOException("The HID worker requires a restart.");ownership=new Mutex(false,MutexName(d.Path));try{try{ownsMutex=ownership.WaitOne(0);}catch(AbandonedMutexException){ownsMutex=true;}if(!ownsMutex)throw new IOException("This HID interface is already in use by another ROG Control worker.");handle=CreateFile(d.Path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);if(handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Cannot open device control interface.");}catch{Dispose();throw;}}
    internal byte[] Query(byte report,byte[] packet)
    {
        if(packet[0]!=0x12&&packet[0]!=0x7D&&!GearLinkWorker.IsFeatureQuery(GearLinkWorker.Kind(descriptor.Pid),packet)&&!(packet[0]==0x41&&packet[1]==32&&packet[2]==0&&packet[3]==0)&&!(packet[0]==0xA0&&packet[1]==0&&packet[2]==0&&packet[3]==0)&&!(packet[0]==0xA1&&packet[1]==1&&packet[2]==0&&packet[3]==0))throw new ArgumentException("Read-only transport rejected a nonquery command.");
        if(descriptor.OutputLength<packet.Length+1||descriptor.InputLength<5)throw new IOException("Unexpected report length.");
        // Do not flush a shared HID queue: an original ASUS app may be waiting on it.
        byte[] output=new byte[descriptor.OutputLength];output[0]=report;Array.Copy(packet,0,output,1,packet.Length);Stopwatch deadline=Stopwatch.StartNew();var observedHeaders=new List<string>();bool trace=Environment.GetEnvironmentVariable("OPENAC_PROTOCOL_TRACE")=="1";
        try {
                int sent=Transfer(true,output,Remaining(deadline,900));if(sent!=output.Length)throw new IOException("Device query output returned a partial report.");
                while(true) {byte[] b=new byte[descriptor.InputLength];int got=Transfer(false,b,Remaining(deadline,900));if(got==0)throw new IOException("Device disconnected.");if(trace&&got>=3&&observedHeaders.Count<8)observedHeaders.Add(b[0].ToString("X2")+":"+b[1].ToString("X2")+":"+b[2].ToString("X2"));if(b[0]!=report)continue;
                    if(GearLinkWorker.DeviceErrorResponse(report,b,got))throw new IOException("Device rejected this query with an FF AA error frame."+(trace?" Error frame: "+BitConverter.ToString(b.Take(Math.Min(got,9)).ToArray())+".":""));
                    if(GearLinkWorker.ResponseMatches(descriptor.Pid,report,packet,b,got))return b.Skip(1).Take(got-1).ToArray();
                }
        }catch(TimeoutException e){throw new TimeoutException("Device query exceeded its bounded 900 ms budget. "+e.Message+(trace?" Control response header triples (report:command:key): "+(observedHeaders.Count==0?"none":String.Join(",",observedHeaders))+".":""),e);}
    }
    internal GearLinkWorker.SettingReceipt WriteSetting(byte report,byte[] packet)
    {
        string kind=GearLinkWorker.Kind(descriptor.Pid);bool allow=kind=="headset"&&report==0xCC&&packet.Length>=5&&packet[2]==0&&packet[3]==0&&(packet[0]==0x41&&new byte[]{2,3,4,10,16,17}.Contains(packet[1])||packet[0]==0x51&&new byte[]{16,40,51,55,82}.Contains(packet[1])||packet[0]==0x61&&packet[1]==17);
        if((kind=="mouse"||kind=="keyboard")&&packet.Length>=5&&packet[0]==0x51&&packet[3]==0&&descriptor.OutputReports.Contains(report)){allow=kind=="mouse"?(packet[1]==49&&new byte[]{4,6,9,12,13,14,15,18}.Contains(packet[2])||packet[1]==55&&packet[2]==0):(packet[1]==49||packet[1]==56)&&packet[2]==0;}
        if(descriptor.OutputReports.Contains(report)&&GearLinkWorker.IsFeatureWrite(kind,packet))allow=true;
        if(!allow)throw new ArgumentException("Setting transport rejected a nonallowlisted command.");
        if(descriptor.OutputLength<packet.Length+1)throw new IOException("Unexpected output report length.");
        return GearLinkWorker.SendSettingAndAwaitResponse(descriptor.Pid,report,packet,descriptor.OutputLength,descriptor.InputLength,(write,buffer,budget)=>Transfer(write,buffer,budget));
    }
    private static int Remaining(Stopwatch elapsed,int budget){int remaining=budget-(int)Math.Min(Int32.MaxValue,elapsed.ElapsedMilliseconds);if(remaining<=GearBoundedIo.CancelReserveMs)throw new TimeoutException("The HID transaction deadline expired.");return remaining;}
    private int Transfer(bool write,byte[] buffer,int timeoutMs){if(handle==null||handle.IsClosed||handle.IsInvalid)throw new ObjectDisposedException("GearHid");bool retained=false;try{handle.DangerousAddRef(ref retained);return GearBoundedIo.Transfer(handle.DangerousGetHandle(),write,buffer,timeoutMs,null);}finally{if(retained)handle.DangerousRelease();}}
    internal static string MutexName(string path){using(var hash=SHA256.Create())return "Local\\RogControl.Hid."+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Replace("-","");}
    public void Dispose(){if(GearBoundedIo.RestartRequired){if(!RetainedPoisoned.Contains(this))RetainedPoisoned.Add(this);return;}if(handle!=null){handle.Dispose();handle=null;}if(ownership!=null){if(ownsMutex){ownership.ReleaseMutex();ownsMutex=false;}ownership.Dispose();ownership=null;}}
    internal static List<GearDescriptor> Enumerate()
    {
        Guid guid;HidD_GetHidGuid(out guid);IntPtr set=SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,0x12);if(set==new IntPtr(-1))throw new Win32Exception(Marshal.GetLastWin32Error());var list=new List<GearDescriptor>();
        try {var iface=new DeviceInterface{Size=Marshal.SizeOf(typeof(DeviceInterface))};for(uint i=0;SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref iface);i++){
            uint needed;SetupDiGetDeviceInterfaceDetail(set,ref iface,IntPtr.Zero,0,out needed,IntPtr.Zero);if(needed==0||needed>65536)continue;IntPtr detail=Marshal.AllocHGlobal((int)needed);
            try{Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);if(!SetupDiGetDeviceInterfaceDetail(set,ref iface,detail,needed,out needed,IntPtr.Zero))continue;string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                using(var h=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)){if(h.IsInvalid)continue;var at=new Attributes{Size=Marshal.SizeOf(typeof(Attributes))};if(!HidD_GetAttributes(h,ref at)||at.VendorId!=0x0B05||GearLinkWorker.Kind(at.ProductId)=="unknown")continue;IntPtr prep;if(!HidD_GetPreparsedData(h,out prep))continue;
                    try{Caps c;if(HidP_GetCaps(prep,out c)!=0x00110000)continue;var d=new GearDescriptor{Path=path,Pid=at.ProductId,Page=c.UsagePage,Usage=c.Usage,InputLength=c.InputLength,OutputLength=c.OutputLength,FeatureLength=c.FeatureLength,InputReports=Reports(prep,0,c.InputValues,c.InputButtons),OutputReports=Reports(prep,1,c.OutputValues,c.OutputButtons)};
                        using(var hash=SHA256.Create()){d.Id="hid-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()))).Replace("-","").Substring(0,16).ToLowerInvariant();}
                        byte[] name=new byte[512];d.Product=HidD_GetProductString(h,name,name.Length)?Encoding.Unicode.GetString(name).TrimEnd('\0'):"";list.Add(d);
                    }finally{HidD_FreePreparsedData(prep);}
                }
            }finally{Marshal.FreeHGlobal(detail);}
        }}finally{SetupDiDestroyDeviceInfoList(set);}return list;
    }
    private static byte[] Reports(IntPtr prep,int type,ushort values,ushort buttons){var result=new HashSet<byte>();for(int mode=0;mode<2;mode++){ushort count=mode==0?values:buttons;if(count==0)continue;IntPtr caps=Marshal.AllocHGlobal(count*72);try{int status=mode==0?HidP_GetValueCaps(type,caps,ref count,prep):HidP_GetButtonCaps(type,caps,ref count,prep);if(status==0x00110000)for(int i=0;i<count;i++)result.Add(Marshal.ReadByte(caps,i*72+2));}finally{Marshal.FreeHGlobal(caps);}}return result.OrderBy(v=>v).ToArray();}
    [StructLayout(LayoutKind.Sequential)]private struct DeviceInterface{public int Size;public Guid Guid;public int Flags;public IntPtr Reserved;}
    [StructLayout(LayoutKind.Sequential)]private struct Attributes{public int Size;public ushort VendorId,ProductId,Version;}
    [StructLayout(LayoutKind.Sequential)]private struct Caps{public ushort Usage,UsagePage,InputLength,OutputLength,FeatureLength;[MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] Reserved;public ushort LinkNodes,InputButtons,InputValues,InputIndices,OutputButtons,OutputValues,OutputIndices,FeatureButtons,FeatureValues,FeatureIndices;}
    [DllImport("hid.dll")]private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll",SetLastError=true)]private static extern bool HidD_GetAttributes(SafeFileHandle h,ref Attributes attributes);
    [DllImport("hid.dll",SetLastError=true)]private static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr data);
    [DllImport("hid.dll")]private static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")]private static extern int HidP_GetCaps(IntPtr data,out Caps caps);
    [DllImport("hid.dll")]private static extern int HidP_GetValueCaps(int type,IntPtr caps,ref ushort length,IntPtr data);
    [DllImport("hid.dll")]private static extern int HidP_GetButtonCaps(int type,IntPtr caps,ref ushort length,IntPtr data);
    [DllImport("hid.dll",SetLastError=true)]private static extern bool HidD_GetProductString(SafeFileHandle h,byte[] buffer,int length);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr SetupDiGetClassDevs(ref Guid guid,IntPtr enumerator,IntPtr hwnd,uint flags);
    [DllImport("setupapi.dll",SetLastError=true)]private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set,IntPtr device,ref Guid guid,uint index,ref DeviceInterface iface);
    [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set,ref DeviceInterface iface,IntPtr detail,uint size,out uint needed,IntPtr device);
    [DllImport("setupapi.dll")]private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
}

internal interface IGearIoApi
{
    IntPtr CreateEvent();
    void CloseEvent(IntPtr eventHandle);
    int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped);
    uint Wait(IntPtr eventHandle, uint milliseconds);
    bool Cancel(IntPtr handle, IntPtr overlapped, out int error);
    int Finish(IntPtr handle, IntPtr overlapped, out uint transferred);
}

internal static class GearBoundedIo
{
    internal const int CancelReserveMs = 100;
    internal static bool RestartRequired { get; private set; }
    private const int Pending = 997, Incomplete = 996, Aborted = 995;
    private const uint Signaled = 0;
    private static readonly IGearIoApi Windows = new WindowsGearIoApi();

    [StructLayout(LayoutKind.Sequential)]
    private struct Overlapped
    {
        internal IntPtr Internal, InternalHigh;
        internal uint Offset, OffsetHigh;
        internal IntPtr Event;
    }

    internal static int Transfer(IntPtr handle, bool write, byte[] buffer, int timeoutMs, IGearIoApi injected)
    {
        if (RestartRequired) throw new IOException("The HID worker requires a restart after an unconfirmed I/O cancellation.");
        if (buffer == null || buffer.Length == 0) throw new ArgumentException("A nonempty HID report is required.");
        if (timeoutMs <= CancelReserveMs || timeoutMs > 60000) throw new ArgumentOutOfRangeException("timeoutMs", "The bounded HID deadline must include its 100 ms cancellation reserve.");
        IGearIoApi api = injected ?? Windows;
        Stopwatch deadline = Stopwatch.StartNew();
        IntPtr eventHandle = api.CreateEvent();
        if (eventHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot create the HID completion event.");
        IntPtr nativeBuffer = IntPtr.Zero, overlapped = IntPtr.Zero;
        bool started = false, terminal = true;
        try
        {
            nativeBuffer = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, nativeBuffer, buffer.Length);
            overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlapped)));
            Marshal.StructureToPtr(new Overlapped { Event = eventHandle }, overlapped, false);
            int error = api.Start(handle, write, nativeBuffer, buffer.Length, overlapped);
            if (error != 0 && error != Pending) throw new Win32Exception(error, "Cannot start the GearLink HID " + (write ? "write" : "read") + ".");
            started = true;
            terminal = false;
            uint transferred;
            if (error == 0)
            {
                error = api.Finish(handle, overlapped, out transferred);
                if (error == 0)
                {
                    terminal = true;
                    return Complete(write, buffer, nativeBuffer, transferred);
                }
                if (error != Incomplete)
                {
                    terminal = true;
                    throw new Win32Exception(error, "The GearLink HID transfer failed.");
                }
            }
            int workBudget = timeoutMs - (int)deadline.ElapsedMilliseconds - CancelReserveMs;
            uint wait = workBudget > 0 ? api.Wait(eventHandle, (uint)workBudget) : 258;
            if (wait == Signaled)
            {
                error = api.Finish(handle, overlapped, out transferred);
                if (error == 0)
                {
                    terminal = true;
                    return Complete(write, buffer, nativeBuffer, transferred);
                }
                if (error != Incomplete)
                {
                    terminal = true;
                    throw new Win32Exception(error, "The GearLink HID transfer failed.");
                }
            }
            // Cancel only this exact operation; never cancel another report or
            // assume CancelIoEx returning true means the driver finished with it.
            int cancelError;
            api.Cancel(handle, overlapped, out cancelError);
            error = api.Finish(handle, overlapped, out transferred);
            if (error == Incomplete)
            {
                int remaining = timeoutMs - (int)deadline.ElapsedMilliseconds;
                if (remaining > 0) api.Wait(eventHandle, (uint)Math.Min(CancelReserveMs, remaining));
                error = api.Finish(handle, overlapped, out transferred);
            }
            if (error != Incomplete)
            {
                terminal = true;
                string ending = error == 0 ? "The operation completed while cancellation was being requested; a write may already have reached the device."
                    : error == Aborted ? "The pending operation was cancelled." : "The pending operation completed with error " + error + ".";
                throw new TimeoutException("GearLink HID " + (write ? "write" : "read") + " exceeded its bounded deadline. " + ending);
            }
            RestartRequired = true;
            throw new TimeoutException("GearLink HID " + (write ? "write" : "read") + " exceeded its bounded deadline and its cancellation did not complete. The worker will stop and must be restarted.");
        }
        finally
        {
            if (terminal || !started)
            {
                if (overlapped != IntPtr.Zero) Marshal.FreeHGlobal(overlapped);
                if (nativeBuffer != IntPtr.Zero) Marshal.FreeHGlobal(nativeBuffer);
                api.CloseEvent(eventHandle);
            }
            else
            {
                // Kernel I/O may still reference all three native allocations.
                // Intentionally retain them until child process teardown. Freeing
                // or reusing this memory would be a use-after-free in the driver.
                RestartRequired = true;
            }
        }
    }

    private static int Complete(bool write, byte[] buffer, IntPtr nativeBuffer, uint transferred)
    {
        if (transferred > buffer.Length) throw new IOException("The GearLink HID driver returned an invalid transfer length.");
        if (!write && transferred > 0) Marshal.Copy(nativeBuffer, buffer, 0, (int)transferred);
        return (int)transferred;
    }

    // Synthetic tests own their fake pending operations; they use this only
    // after fake completion. A JSON request cannot clear the poisoned state.
    internal static void ResetForSyntheticTests() { RestartRequired = false; }

    private sealed class WindowsGearIoApi : IGearIoApi
    {
        public IntPtr CreateEvent() { return CreateEventW(IntPtr.Zero, true, false, null); }
        public void CloseEvent(IntPtr eventHandle) { CloseHandle(eventHandle); }
        public int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped)
        {
            uint transferred;
            bool ok = write ? WriteFile(handle, buffer, (uint)length, out transferred, overlapped)
                : ReadFile(handle, buffer, (uint)length, out transferred, overlapped);
            return ok ? 0 : Marshal.GetLastWin32Error();
        }
        public uint Wait(IntPtr eventHandle, uint milliseconds) { return WaitForSingleObject(eventHandle, milliseconds); }
        public bool Cancel(IntPtr handle, IntPtr overlapped, out int error)
        {
            bool ok = CancelIoEx(handle, overlapped);
            error = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }
        public int Finish(IntPtr handle, IntPtr overlapped, out uint transferred)
        {
            bool ok = GetOverlappedResult(handle, overlapped, out transferred, false);
            return ok ? 0 : Marshal.GetLastWin32Error();
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateEventW(IntPtr security, [MarshalAs(UnmanagedType.Bool)] bool manualReset, [MarshalAs(UnmanagedType.Bool)] bool initialState, string name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadFile(IntPtr handle, IntPtr buffer, uint count, out uint transferred, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteFile(IntPtr handle, IntPtr buffer, uint count, out uint transferred, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOverlappedResult(IntPtr handle, IntPtr overlapped, out uint transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CancelIoEx(IntPtr handle, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    }
}

internal static class GearIoSelfTest
{
    private sealed class Fake : IGearIoApi
    {
        internal int StartError=997,Calls,CloseCount,CancelCount;internal uint WaitResult=0,Transferred=3;internal int[] Finishes=new int[]{0};internal byte[] Bytes=new byte[]{10,20,30};internal IntPtr Buffer,Overlap;internal readonly List<uint> Waits=new List<uint>();private int finishIndex;
        public IntPtr CreateEvent(){return new IntPtr(123);}
        public void CloseEvent(IntPtr h){CloseCount++;}
        public int Start(IntPtr h,bool write,IntPtr b,int length,IntPtr overlap){Calls++;Buffer=b;Overlap=overlap;if(StartError==0||StartError==997)Marshal.Copy(Bytes,0,b,Math.Min(Bytes.Length,length));return StartError;}
        public uint Wait(IntPtr h,uint ms){Waits.Add(ms);return WaitResult;}
        public bool Cancel(IntPtr h,IntPtr o,out int error){CancelCount++;error=0;return true;}
        public int Finish(IntPtr h,IntPtr o,out uint got){got=Transferred;int code=Finishes[Math.Min(finishIndex,Finishes.Length-1)];finishIndex++;return code;}
        internal void CompleteAndFreeFakePending(){Marshal.FreeHGlobal(Buffer);Marshal.FreeHGlobal(Overlap);CloseEvent(new IntPtr(123));GearBoundedIo.ResetForSyntheticTests();}
    }
    internal static void Run()
    {
        int tests=0;Action<bool> check=v=>{if(!v)throw new Exception("Synthetic bounded HID I/O test failed.");tests++;};byte[] b=new byte[8];var f=new Fake{StartError=0};check(GearBoundedIo.Transfer(new IntPtr(1),false,b,900,f)==3&&b.Take(3).SequenceEqual(f.Bytes)&&f.CloseCount==1&&f.CancelCount==0);
        f=new Fake();b=new byte[8];check(GearBoundedIo.Transfer(new IntPtr(1),false,b,900,f)==3&&f.Waits.Count==1&&f.Waits[0]<=800&&f.CloseCount==1);
        f=new Fake{WaitResult=258,Finishes=new int[]{995}};bool timeout=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(TimeoutException){timeout=true;}check(timeout&&f.CancelCount==1&&f.CloseCount==1&&!GearBoundedIo.RestartRequired);
        f=new Fake{WaitResult=258,Finishes=new int[]{996,995}};timeout=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(TimeoutException){timeout=true;}check(timeout&&f.CancelCount==1&&f.Waits.Count==2&&f.Waits[1]<=100&&f.CloseCount==1&&!GearBoundedIo.RestartRequired);
        f=new Fake{WaitResult=258,Finishes=new int[]{0}};timeout=false;try{GearBoundedIo.Transfer(new IntPtr(1),true,new byte[8],900,f);}catch(TimeoutException){timeout=true;}check(timeout&&f.CloseCount==1&&!GearBoundedIo.RestartRequired);
        f=new Fake{WaitResult=0xFFFFFFFF,Finishes=new int[]{995}};timeout=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(TimeoutException){timeout=true;}check(timeout&&f.CancelCount==1&&f.CloseCount==1);
        f=new Fake{WaitResult=258,Finishes=new int[]{996,996}};timeout=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(TimeoutException){timeout=true;}check(timeout&&GearBoundedIo.RestartRequired&&f.CloseCount==0&&f.CancelCount==1);bool blocked=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(IOException){blocked=true;}check(blocked&&f.Calls==1);f.CompleteAndFreeFakePending();check(!GearBoundedIo.RestartRequired&&f.CloseCount==1);
        f=new Fake{StartError=5};blocked=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(Win32Exception){blocked=true;}check(blocked&&f.CloseCount==1&&f.CancelCount==0);
        f=new Fake{StartError=0,Transferred=9};blocked=false;try{GearBoundedIo.Transfer(new IntPtr(1),false,new byte[8],900,f);}catch(IOException){blocked=true;}check(blocked&&f.CloseCount==1);
        check(GearHid.MutexName("\\?\\HID#ABC")==GearHid.MutexName("\\?\\hid#abc")&&GearHid.MutexName("a").Length=="Local\\RogControl.Hid.".Length+64);
        check(GearLinkWorker.Kind(7547)=="unknown"&&GearLinkWorker.Kind(7587)=="unknown"&&GearLinkWorker.Kind(7546)=="mouse"&&GearLinkWorker.Kind(7588)=="keyboard");
        Console.WriteLine(GearLinkWorker.Json.Serialize(GearLinkWorker.Obj("ok",true,"hardwareAccess",false,"suite","bounded_hid_io","tests",tests)));
    }
}

internal static class GearBoundedIoTests
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] report = new byte[65];
        FakeIo fake = new FakeIo { StartResult = 0 };
        int got = GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake);
        check(got == 65 && report[0] == 0xEC && report[1] == 0x30, "Immediate native read copies only completed data");
        check(fake.Closes == 1 && fake.Waits.Count == 0 && fake.Cancels == 0, "Immediate completion frees its event without waiting or cancelling");

        fake = new FakeIo();
        got = GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake);
        check(got == 65 && fake.Closes == 1 && fake.Waits.Count == 1 && fake.Cancels == 0, "Pending read waits for completion then releases native storage");
        check(fake.Waits[0] > 0 && fake.Waits[0] <= 1100, "Working deadline reserves cancellation time");

        report[4] = 0xAC;
        fake = new FakeIo { StartResult = 0 };
        got = GearBoundedIo.Transfer(new IntPtr(7), true, report, 1000, fake);
        check(got == 65 && fake.Output[4] == 0xAC, "Native write uses a stable unmanaged copy of the validated report");

        fake = new FakeIo { StartResult = 5 };
        bool rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (Win32Exception) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Finishes == 0 && fake.Cancels == 0, "Failure to start an operation releases allocation safely");

        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Cancels == 1 && fake.Closes == 1 && !GearBoundedIo.RestartRequired, "Timed out read verifies aborted completion before freeing its buffer");
        check(fake.Waits.Count == 1 && fake.Finishes == 1, "Already completed cancellation needs no second wait");

        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.WaitResults.Enqueue(0);
        fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Waits.Count == 2 && !GearBoundedIo.RestartRequired, "Delayed cancellation waits for terminal completion");
        check(fake.Waits[1] <= GearBoundedIo.CancelReserveMs, "Cancellation cleanup has a bounded reserve");

        fake = new FakeIo { CancelError = 1168 }; fake.WaitResults.Enqueue(258); fake.FinishResults.Enqueue(0);
        string error = null;
        try { GearBoundedIo.Transfer(new IntPtr(7), true, report, 1000, fake); } catch (TimeoutException ex) { error = ex.Message; }
        check(error != null && error.Contains("may already have reached") && fake.Closes == 1, "Write completing during cancellation is reported as an uncertain hardware outcome");
        check(!GearBoundedIo.RestartRequired, "A cancellation completion race leaves no pending I/O");

        fake = new FakeIo(); fake.FinishResults.Enqueue(5);
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (Win32Exception) { rejected = true; }
        check(rejected && fake.Closes == 1 && fake.Cancels == 0, "A terminal device error releases native storage");

        fake = new FakeIo { Transferred = 100 };
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (IOException) { rejected = true; }
        check(rejected && fake.Closes == 1, "Invalid driver length cannot copy past the validated report");

        fake = new FakeIo(); fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(995);
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && fake.Cancels == 1 && fake.Closes == 1, "A signaled event with incomplete I/O is cancelled rather than freeing a pending buffer");

        // This fake models a driver that ignores cancellation. There is no real
        // device I/O, so the test can safely release its retained memory itself.
        fake = new FakeIo(); fake.WaitResults.Enqueue(258); fake.WaitResults.Enqueue(258);
        fake.FinishResults.Enqueue(996); fake.FinishResults.Enqueue(996);
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, fake); } catch (TimeoutException) { rejected = true; }
        check(rejected && GearBoundedIo.RestartRequired && fake.Closes == 0, "Unconfirmed cancellation preserves buffer/OVERLAPPED/event and poisons the worker");
        FakeIo forbidden = new FakeIo();
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 1200, forbidden); } catch (IOException) { rejected = true; }
        check(rejected && forbidden.Creates == 0, "A poisoned worker rejects every subsequent native operation");
        fake.FreeSyntheticRetainedMemory();
        GearBoundedIo.ResetForSyntheticTests();

        fake = new FakeIo();
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, report, 100, fake); } catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected && fake.Creates == 0, "An exhausted deadline is rejected before event creation or hardware I/O");
        rejected = false;
        try { GearBoundedIo.Transfer(new IntPtr(7), false, new byte[0], 1200, fake); } catch (ArgumentException) { rejected = true; }
        check(rejected && fake.Creates == 0, "Empty reports are rejected before any I/O");
        check(!GearBoundedIo.RestartRequired, "Synthetic pending state cannot leak into later tests");
    }

    private sealed class FakeIo : IGearIoApi
    {
        internal int StartResult = 997, CancelError, Creates, Closes, Cancels, Finishes;
        internal uint Transferred = 65;
        internal byte[] Output;
        internal readonly Queue<uint> WaitResults = new Queue<uint>();
        internal readonly Queue<int> FinishResults = new Queue<int>();
        internal readonly List<uint> Waits = new List<uint>();
        private IntPtr memory, operation;
        public IntPtr CreateEvent() { Creates++; return new IntPtr(123); }
        public void CloseEvent(IntPtr eventHandle) { Closes++; }
        public int Start(IntPtr handle, bool write, IntPtr buffer, int length, IntPtr overlapped)
        {
            memory = buffer; operation = overlapped;
            if (write) { Output = new byte[length]; Marshal.Copy(buffer, Output, 0, length); }
            else { byte[] reply = new byte[length]; reply[0] = 0xEC; reply[1] = 0x30; Marshal.Copy(reply, 0, buffer, length); }
            return StartResult;
        }
        public uint Wait(IntPtr eventHandle, uint milliseconds)
        {
            Waits.Add(milliseconds); return WaitResults.Count == 0 ? 0 : WaitResults.Dequeue();
        }
        public bool Cancel(IntPtr handle, IntPtr overlapped, out int error) { Cancels++; error = CancelError; return error == 0; }
        public int Finish(IntPtr handle, IntPtr overlapped, out uint transferred)
        {
            Finishes++; transferred = Transferred; return FinishResults.Count == 0 ? 0 : FinishResults.Dequeue();
        }
        internal void FreeSyntheticRetainedMemory()
        {
            if (Closes != 0) throw new Exception("Cannot release a fake allocation twice.");
            Marshal.FreeHGlobal(operation); Marshal.FreeHGlobal(memory); Closes++;
        }
    }
}
