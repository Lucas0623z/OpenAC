using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Xml;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;

// Synthetic transport only: no installed DLL, service, USB or firmware access.
internal static class SDKWorkerTests
{
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    static readonly List<string> Calls=new List<string>();
    static int Count;
    static bool S0=true,S5=true,IgnorePowerWrite;
    static string Ack="<root><data><function><result>0</result></function></data></root>";
    static string Fault="";
    static string BadCapability="";
    static bool FailPowerReadAfterWrite;
    static int PowerWrites;
    static string Rpm="1200";
    static string FreeSpace="10485760",CurrentProfile,LcdDetectedId="7299",LcdFirmware="0109";
    static string LastStaged;
    static int LastWidth,LastHeight;
    static Color LastTop,LastCenter;
    static bool IgnoreProfileWrite;
    static bool OmitFanEffects;
    static bool ZeroSensorValues;
    static Dictionary<string,object> Object(params object[] pairs) { var d=new Dictionary<string,object>();for(int n=0;n<pairs.Length;n+=2)d[(string)pairs[n]]=pairs[n+1];return d; }
    static void Assert(bool condition,string label) { if(!condition)throw new Exception("FAIL "+label);Count++; }
    static Dictionary<string,object> Reply(object request) { return Json.Deserialize<Dictionary<string,object>>(SDKWorker.HandleLine(Json.Serialize(request))); }
    static Dictionary<string,object> Data(Dictionary<string,object> response) { return (Dictionary<string,object>)response["data"]; }
    static bool IsOk(Dictionary<string,object> response) { return (bool)response["ok"]; }
    static Dictionary<string,object> Set(string kind,string setting,params object[] extras) {
        var d=Object("id",123,"op","set","kind",kind,"deviceId",kind=="lcd"?"7299":"7576","setting",setting);
        for(int n=0;n<extras.Length;n+=2)d[(string)extras[n]]=extras[n+1];return d;
    }
    static object Node(int t,int p) { return Object("temperature",t,"percentage",p); }
    static string Native(string xml) {
        Calls.Add(xml);var doc=new XmlDocument();doc.LoadXml(xml);
        var function=doc.SelectSingleNode("/root/device_type/device/function");string f=function.Attributes["key"].Value;
        string type=doc.SelectSingleNode("/root/device_type").Attributes["key"].Value;
        if(Fault==f)throw new Exception("synthetic failure");
        if(type=="32") {
            if(f=="1") { string effects="";if(!OmitFanEffects)foreach(int effect in new[]{3,4,5,7,8,9,10,32,34,48,49,51,50,33,38,66,67,64,69,70,71,72,65})effects+="<effect key='"+effect+"'>source effect</effect>";return "<root><data><hal_found>1</hal_found><hub_found>1</hub_found><function><hub_info><firmware_version>90.36.32.00.0D</firmware_version><effect_list>"+effects+"</effect_list><temperature_source_list><source key='1' avail='1'>CPU Package</source><source key='2' avail='1'>GPU</source><source key='3' avail='1'>DRAM</source><source key='4' avail='0'>Motherboard</source><source key='5' avail='0'>T-Sensor</source></temperature_source_list></hub_info></function></data></root>"; }
            string p=function.SelectSingleNode("port")==null?"":function.SelectSingleNode("port").Attributes["key"].Value;
            if(f=="4")return "<root><data><function><result>0</result><port key='"+p+"'><rpm>"+Rpm+"</rpm></port></function></data></root>";
            if(f=="3")return BadCapability.Length>0?BadCapability:"<root><data><function><result>0</result><port key='"+p+"'><rpm_min>840</rpm_min><rpm_max>2800</rpm_max><duty_min>0</duty_min><duty_max>100</duty_max></port></function></data></root>";
            if(f=="5")return "<root><data><function><temperatureData><value key='1'>43</value></temperatureData></function></data></root>";
            if(f=="103"||f=="104"||f=="201"||f=="202")return Ack;
            throw new Exception("Unexpected synthetic fan function "+f);
        }
        if(f=="0")return "<root><capability><device_list><deviceid>"+LcdDetectedId+"</deviceid></device_list><player key='hw_monitor_player'><sensor_list><sensor key='61440'><name>Core #0</name><default_name>Core #0</default_name><type>4</type></sensor><sensor key='61440'><name>Graphics</name><default_name>Graphics</default_name><type>4</type></sensor><sensor key='67117057'><name>GPU</name><default_name>GPU</default_name><type>1</type></sensor><sensor key='57345'><name>Memory Load</name><default_name>Memory Load</default_name><type>6</type></sensor></sensor_list></player></capability></root>";
        if(f=="5")return LcdFirmware;
        if(f=="6") { if(FailPowerReadAfterWrite&&PowerWrites>0)throw new Exception("synthetic verification failure");return S0?"0":"1"; }
        if(f=="20")return S5?"0":"1";
        if(f=="1")return CurrentProfile;
        if(f=="7"||f=="21") {
            PowerWrites++;
            if(!IgnorePowerWrite) { bool value=function.SelectSingleNode("settings/power").InnerText=="0";if(f=="7")S0=value;else S5=value; }
            return Ack;
        }
        if(f=="2") { if(!IgnoreProfileWrite)CurrentProfile=function.SelectSingleNode("profiles").OuterXml;return Ack; }
        if(f=="26")return FreeSpace;
        if(f=="27") { LastStaged=function.SelectSingleNode("settings/media_transfer/path").InnerText;using(var image=new Bitmap(LastStaged)){LastWidth=image.Width;LastHeight=image.Height;LastTop=image.GetPixel(360,0);LastCenter=image.GetPixel(360,640);}return Ack; }
        if(f=="36")return "<root><sensor><id>61440</id><value>4500</value></sensor><sensor><id>67117057</id><default_name>GPU</default_name><type>1</type><value>"+(ZeroSensorValues?"0":"48.5")+"</value></sensor></root>";
        if(f=="24"||f=="37"||f=="29")return Ack;
        throw new Exception("Unexpected synthetic LCD function "+f);
    }
    static void Configure(string kind) { Calls.Clear();S0=true;S5=true;IgnorePowerWrite=false;Fault="";BadCapability="";FailPowerReadAfterWrite=false;PowerWrites=0;Rpm="1200";FreeSpace="10485760";LcdDetectedId="7299";LcdFirmware="0109";LastStaged=null;IgnoreProfileWrite=false;OmitFanEffects=false;CurrentProfile="<profiles><profile4><main><single><player key='multi_media_player'><setting><play_device>AIO2_PANEL</play_device><display_time>5000</display_time><group key='0'><type>2</type><source>1</source><index_format>1</index_format></group></setting></player></single></main></profile4></profiles>";Ack="<root><data><function><result>0</result></function></data></root>";SDKWorker.ConfigureSynthetic(kind,Native); }
    static Dictionary<string,object> Light(string area,string effect,int count,int brightness,int speed,string direction,string[] colors) { return Set("fan","lighting","portId","1","area",area,"effect",effect,"fanCount",count,"brightness",brightness,"speed",speed,"direction",direction,"colors",colors); }
    static string Function(string xml) { var doc=new XmlDocument();doc.LoadXml(xml);return doc.SelectSingleNode("/root/device_type/device/function").Attributes["key"].Value; }
    static string Hash(string path) { using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(stream)); }
    static void Ready(string kind) { Configure(kind);Assert(IsOk(Reply(Object("id",1,"op","scan","kind",kind))),kind+" scan");Assert(IsOk(Reply(Object("id",2,"op","read","kind",kind,"deviceId",kind=="lcd"?"7299":"7576"))),kind+" read");if(kind=="fan"){Assert(Calls.Count==10,"fan scan plus nine pure reads");foreach(string xml in Calls) { var doc=new XmlDocument();doc.LoadXml(xml);int f=int.Parse(doc.SelectSingleNode("/root/device_type/device/function").Attributes["key"].Value);Assert(f>=1&&f<=5,"read excludes calibration and setters"); }}Calls.Clear(); }
    static void RejectNoIO(Dictionary<string,object> request,string label) { Calls.Clear();var reply=Reply(request);Assert(!IsOk(reply),label+" rejection");Assert(Calls.Count==0,label+" validates before I/O"); }
    public static int Main() {
        try {
            Ready("fan");
            RejectNoIO(Set("fan","fixed","portId","0","rpm",1000),"invalid port");
            RejectNoIO(Set("fan","fixed","portId","1","rpm",839),"below capability");
            RejectNoIO(Set("fan","fixed","portId","1","rpm",2801),"above capability");
            RejectNoIO(Set("fan","fixed","portId","1","rpm","1000"),"string RPM");
            RejectNoIO(Set("fan","fixed","portId","1","rpm",1000.5),"fraction RPM");
            RejectNoIO(Set("fan","fixed","portId","1","rpm",1000,"xml","x"),"arbitrary XML");
            RejectNoIO(Set("fan","rename","portId","1","value","new"),"hardware rename");
            var fixedReply=Reply(Set("fan","fixed","portId","1","rpm",1000));
            Assert(IsOk(fixedReply)&&Calls.Count==1,"fixed one write");
            Assert(Calls[0].Contains("<mode>2</mode>")&&Calls[0].Contains("<rpm>1000</rpm>"),"fixed exact XML");
            Assert((bool)Data(fixedReply)["hardwareAcknowledged"]&&!(bool)Data(fixedReply)["readbackConfirmed"],"fixed acknowledgement only");
            RejectNoIO(Set("fan","smart","portId","1","nodes",new[]{Node(0,30),Node(50,30),Node(50,50),Node(100,90)}),"duplicate temperature");
            RejectNoIO(Set("fan","smart","portId","1","nodes",new[]{Node(0,30),Node(50,20),Node(70,50),Node(100,90)}),"decreasing duty");
            RejectNoIO(Set("fan","smart","portId","1","nodes",new[]{Node(0,30),Node(50,30),Node(70,50),Node(101,90)}),"temperature range");
            RejectNoIO(Set("fan","smart","portId","1","nodes",new[]{Node(0,30),Node(50,30),Node(70,50)}),"node count");
            RejectNoIO(Set("fan","smart","portId","1","preset","unknown"),"unknown preset");
            RejectNoIO(Set("fan","smart","portId","1","preset","standard","nodes",new object[0]),"conflicting curves");
            foreach(string preset in new[]{"silent","standard","turbo","fullSpeed"}) {
                Calls.Clear();var reply=Reply(Set("fan","smart","portId","1","preset",preset));
                Assert(IsOk(reply)&&Calls.Count==1,"preset "+preset);
                Assert(Calls[0].Contains("<mode>1</mode>")&&Calls[0].Contains("<item_count>4</item_count>")&&Calls[0].Contains("<spin_up_level>2</spin_up_level>"),"smart exact XML "+preset);
            }
            Ack="<root><data><function><result>1</result></function></data></root>";Calls.Clear();
            var failed=Reply(Set("fan","fixed","portId","1","rpm",1000));Assert(!IsOk(failed)&&Calls.Count==1,"nonzero result no retry");
            Assert((bool)Data(failed)["writeAttempted"]&&!(bool)Data(failed)["hardwareAcknowledged"],"failed write uncertainty");
            Ack="<root/>";var ambiguous=Reply(Set("fan","fixed","portId","1","rpm",1000));Assert(IsOk(ambiguous)&&!(bool)Data(ambiguous)["hardwareAcknowledged"]&&(bool)Data(ambiguous)["sdkAcknowledged"],"native return only distinguished");
            BadCapability="<root><data><function><result>0</result><port key='1'><rpm_min>0</rpm_min><rpm_max>0</rpm_max><duty_min>0</duty_min><duty_max>100</duty_max></port></function></data></root>";
            var badCaps=Reply(Object("op","read","kind","fan","deviceId","7576"));Assert(IsOk(badCaps)&&(bool)Data(badCaps)["supportsSmartCurve"]&&!(bool)Data(badCaps)["supportsFixedRpm"],"invalid RPM capability preserves source PWM curve support");RejectNoIO(Set("fan","fixed","portId","1","rpm",1000),"invalid RPM capability cannot fixed write");
            Calls.Clear();var fallbackSmart=Reply(Set("fan","smart","portId","1","preset","standard"));Assert(IsOk(fallbackSmart)&&Calls.Count==1&&(string)Data(fallbackSmart)["rangeSource"]=="installedSource"&&!(bool)Data(fallbackSmart)["rangeReadbackConfirmed"],"source PWM range on live positive RPM port");
            Rpm="0";Reply(Object("op","read","kind","fan","deviceId","7576"));RejectNoIO(Set("fan","smart","portId","1","preset","standard"),"zero RPM port without capability");
            Configure("fan");RejectNoIO(Set("fan","fixed","portId","1","rpm",1000),"missing capability");
            Ready("fan");
            var fanSnapshot=Data(Reply(Object("op","read","kind","fan","deviceId","7576")));Assert((string)fanSnapshot["firmware"]=="90.36.32.00.0D"&&(string)fanSnapshot["firmwareSource"]=="lastHubScan"&&(bool)fanSnapshot["supportsLighting"]&&(bool)fanSnapshot["supportsTemperatureSources"],"hub capability metadata preserved in fan read");Assert(fanSnapshot["currentLighting"]==null&&fanSnapshot["fanCounts"]==null&&fanSnapshot["currentTemperatureSources"]==null,"unreadable fan settings stay null");
            RejectNoIO(Set("fan","fanCount","portId","1","value",15),"fan count upper range");RejectNoIO(Set("fan","fanCount","portId","1","value",-1),"fan count negative");Calls.Clear();var countReply=Reply(Set("fan","fanCount","portId","4","value",0));Assert(IsOk(countReply)&&Calls.Count==1&&Function(Calls[0])=="202"&&Calls[0].Contains("<port key=\"4\"><fan_count>0</fan_count></port>")&&!(bool)Data(countReply)["readbackConfirmed"],"source fan count zero can explicitly clear a port");
            RejectNoIO(Set("fan","temperatureSources","sourceIds",new string[0]),"empty source selection");RejectNoIO(Set("fan","temperatureSources","sourceIds",new[]{"4"}),"unavailable temperature source");RejectNoIO(Set("fan","temperatureSources","sourceIds",new[]{"1","1"}),"duplicate temperature source");RejectNoIO(Set("fan","temperatureSources","sourceIds",new[]{"7"}),"unknown temperature source");Calls.Clear();var sourcesReply=Reply(Set("fan","temperatureSources","sourceIds",new[]{"1","3"}));Assert(IsOk(sourcesReply)&&Calls.Count==1&&Function(Calls[0])=="104"&&Calls[0].Contains("<source_list><source key=\"1\"/><source key=\"3\"/></source_list>")&&!(bool)Data(sourcesReply)["readbackConfirmed"],"source list exact XML acknowledgement only");
            RejectNoIO(Light("center","paint",1,100,60,"forward",new[]{"#FFFFFF"}),"effect not available in area");RejectNoIO(Light("center","static",0,100,60,"forward",new string[0]),"zero count effect");RejectNoIO(Light("outer","static",15,100,60,"forward",new string[0]),"effect fan count maximum");RejectNoIO(Light("center","static",1,30,60,"forward",new[]{"#FFFFFF"}),"unsupported LED brightness");RejectNoIO(Light("center","breathe",1,100,30,"forward",new string[4]),"unsupported LED speed");RejectNoIO(Light("center","static",1,100,80,"forward",new[]{"#FFFFFF"}),"static cannot adjust speed");RejectNoIO(Light("center","breathe",1,100,60,"backward",new[]{"#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF"}),"breath cannot reverse");RejectNoIO(Light("outer","static",2,100,60,"forward",new[]{"#FFFFFF"}),"static outer two colors per fan");RejectNoIO(Light("center","static",1,100,60,"forward",new[]{"# FF00"}),"hex whitespace invalid");RejectNoIO(Light("center","static",1,100,60,"forward",new[]{"0x112233"}),"raw native color rejected");
            Calls.Clear();var rgb=Reply(Light("outer","static",1,75,60,"forward",new[]{"#112233","#aAbBcC"}));Assert(IsOk(rgb)&&Calls.Count==2&&Function(Calls[0])=="202"&&Function(Calls[1])=="201","fan count precedes effect exactly once");Assert(Calls[1].Contains("<group_color_set key=\"2\">0x223311, 0xBBCCAA</group_color_set>")&&Calls[1].Contains("<brightness>1</brightness>")&&Calls[1].Contains("<speed>0</speed>"),"RGB to GBR and outer mapping exact");Assert(!(bool)Data(rgb)["readbackConfirmed"]&&(bool)Data(rgb)["hardwareAcknowledged"]&&!(bool)Data(rgb)["affectsAllPorts"],"LED acknowledgement never current state");
            int[] levels={0,25,50,75,100},nativeLevels={8,3,2,1,0},speeds={20,40,60,80,100},nativeSpeeds={2,1,0,-1,-2};for(int n=0;n<5;n++){Calls.Clear();var mapped=Reply(Light("center","rainbow",1,levels[n],speeds[n],"backward",new string[0]));Assert(IsOk(mapped)&&Calls[1].Contains("<brightness>"+nativeLevels[n]+"</brightness>")&&Calls[1].Contains("<speed>"+nativeSpeeds[n]+"</speed>")&&Calls[1].Contains("<direction>1</direction>"),"native LED map boundary "+n);}
            var chain=Set("fan","lighting","portId","1","area","connected","effect","storm","fanCounts",new[]{1,2,0,0},"brightness",50,"speed",80,"direction","backward","colors",new[]{"#112233","#445566"});Calls.Clear();var connected=Reply(chain);Assert(IsOk(connected)&&Calls.Count==5&&Function(Calls[4])=="201"&&Calls[4].Contains("<effect_id>66</effect_id>")&&Calls[4].Contains("<port key=\"1\"><group_color_set key=\"1\">0x223311, 0x556644</group_color_set>"),"connected chain explicit four counts and effect ID");Assert((bool)Data(connected)["affectsAllPorts"]&&((System.Collections.IList)Data(connected)["affectedPorts"]).Count==4,"connected chain reports full scope");
            chain["portId"]="2";RejectNoIO(chain,"connected native primary port");chain["portId"]="1";chain["fanCounts"]=new[]{1,2,0,15};RejectNoIO(chain,"connected count range before any write");chain["fanCounts"]=new[]{0,0,0,0};RejectNoIO(chain,"connected all empty");chain["fanCounts"]=new[]{1,2,0,0};chain["fanCount"]=1;RejectNoIO(chain,"connected conflicting count fields");chain.Remove("fanCount");
            Fault="201";Calls.Clear();var effectFailed=Reply(Light("center","static",1,100,60,"forward",new[]{"#FFFFFF"}));Assert(!IsOk(effectFailed)&&Calls.Count==2&&Data(effectFailed).ContainsKey("countAcknowledgement")&&(bool)Data(effectFailed)["effectAttempted"],"effect error retains earlier count acknowledgement no retry");Fault="";Ack="<root/>";Calls.Clear();var countUncertain=Reply(Light("center","static",1,100,60,"forward",new[]{"#FFFFFF"}));Assert(IsOk(countUncertain)&&Calls.Count==1&&!(bool)Data(countUncertain)["effectAttempted"],"uncertain count stops effect");Ack="<root><result>0</result></root>";
            Calls.Clear();var disconnect=Reply(Set("fan","disconnectLightingChain","portId","1"));Assert(IsOk(disconnect)&&Calls.Count==1&&Calls[0].Contains("<doc>disconnect</doc>")&&Calls[0].Contains("<group_color_set key=\"1\">#ffffff</group_color_set>")&&(bool)Data(disconnect)["affectsAllPorts"],"explicit chain disconnect source white center scope");RejectNoIO(Set("fan","disconnectLightingChain","portId","2"),"disconnect only primary port");
            OmitFanEffects=true;Reply(Object("op","scan","kind","fan"));var noEffects=Data(Reply(Object("op","read","kind","fan","deviceId","7576")));Assert(!(bool)noEffects["supportsLighting"],"empty native effect list disables LEDs");RejectNoIO(Light("center","static",1,100,60,"forward",new[]{"#FFFFFF"}),"missing real LED capability");
            Ready("lcd");
            foreach(int b in new[]{0,9,101,55})RejectNoIO(Set("lcd","brightness","value",b),"brightness "+b);
            RejectNoIO(Set("lcd","brightness","value",true),"boolean brightness");
            RejectNoIO(Set("lcd","powerS0","value",1),"integer power");
            RejectNoIO(Set("lcd","presetId","value",8),"preset boundary");
            RejectNoIO(Set("lcd","presetId","value",-1),"preset negative");
            RejectNoIO(Set("lcd","presetIndex","value",3),"obsolete ambiguous presetIndex");
            RejectNoIO(Set("lcd","presetId","value",3,"durationSeconds",7),"invalid slideshow duration");
            RejectNoIO(Set("lcd","firmware","value",1),"firmware blocked");
            var noOp=Reply(Set("lcd","powerS0","value",true));Assert(IsOk(noOp)&&Calls.Count==1&&!(bool)Data(noOp)["writeAttempted"]&&(bool)Data(noOp)["readbackConfirmed"],"power no-op");
            Calls.Clear();var powerReply=Reply(Set("lcd","powerS0","value",false));
            Assert(IsOk(powerReply)&&Calls.Count==3,"power read write read");Assert((bool)Data(powerReply)["readbackConfirmed"]&&!(bool)Data(powerReply)["value"],"power verified");
            Assert(Calls[1].Contains("<power>1</power><saving>1</saving>"),"off exact XML");
            Calls.Clear();var offBrightness=Reply(Set("lcd","brightness","value",50));Assert(!IsOk(offBrightness)&&Calls.Count==2,"off LCD refuses brightness write");
            S0=true;S5=false;Calls.Clear();var brightnessReply=Reply(Set("lcd","brightness","value",50));
            Assert(IsOk(brightnessReply)&&Calls.Count==3,"brightness power reads then one write");
            Assert(Calls[2].Contains("<s0>50</s0><s5>1</s5>"),"brightness off S5 mapping");Assert(!(bool)Data(brightnessReply)["readbackConfirmed"],"brightness never invents readback");
            Calls.Clear();var standbyReply=Reply(Set("lcd","standby","enabled",true,"brightness",30));Assert(IsOk(standbyReply)&&Calls.Count==2&&Calls[1].Contains("<enable>1</enable><brightness>30</brightness>"),"standby exact XML");
            Calls.Clear();var standbyOff=Reply(Set("lcd","standby","enabled",false,"brightness",30));Assert(IsOk(standbyOff)&&Calls.Count==1&&Calls[0].Contains("<enable>0</enable><brightness>0</brightness>"),"standby disable zero");
            for(int preset=0;preset<8;preset++) {
                Calls.Clear();var presetReply=Reply(Set("lcd","presetId","value",preset));Assert(IsOk(presetReply)&&Calls.Count==2,"preset write profile read "+preset);
                Assert(Calls[0].Contains("<type>"+(preset<2?2:0)+"</type>")&&Calls[0].Contains("<index_format>"+(preset<2?preset:preset-2)+"</index_format>")&&Calls[0].Contains("<source>1</source>"),"exact GIF/JPEG preset mapping "+preset);
                Assert((bool)Data(presetReply)["profileReadbackConfirmed"]&&!(bool)Data(presetReply)["visualIdentityConfirmed"],"profile confirmed without pixels claim "+preset);
            }
            IgnoreProfileWrite=true;Calls.Clear();var wrongProfile=Reply(Set("lcd","presetId","value",0));Assert(IsOk(wrongProfile)&&!(bool)Data(wrongProfile)["readbackConfirmed"]&&(bool)Data(wrongProfile)["hardwareAcknowledged"],"media acknowledgement survives mismatched metadata");IgnoreProfileWrite=false;
            RejectNoIO(Set("lcd","hardwareMonitor","backgroundId",7,"sensorIds",new[]{"sensor-2"}),"monitor background range");
            RejectNoIO(Set("lcd","hardwareMonitor","backgroundId",8,"sensorIds",new string[0]),"empty monitor");
            RejectNoIO(Set("lcd","hardwareMonitor","backgroundId",8,"sensorIds",new[]{"sensor-2","sensor-2"}),"duplicate monitor sensors");
            RejectNoIO(Set("lcd","hardwareMonitor","backgroundId",8,"sensorIds",new[]{"sensor-unknown"}),"unknown monitor sensor");
            Calls.Clear();var monitor=Reply(Set("lcd","hardwareMonitor","backgroundId",8,"sensorIds",new[]{"sensor-0","sensor-2"},"durationSeconds",10));
            Assert(IsOk(monitor)&&Calls.Count==2&&(bool)Data(monitor)["readbackConfirmed"],"monitor writes current motherboard route and confirms metadata");
            Assert(Calls[0].Contains("<type>5</type><source>1</source><index_format>0</index_format><bg_type>1</bg_type>")&&Calls[0].Contains("<display_time>10000</display_time>")&&Calls[0].Contains("<name>Core #0</name>")&&Calls[0].Contains("<font>1</font>"),"monitor exact media and sensor XML");
            Calls.Clear();var sensorReply=Reply(Object("op","sensors","kind","lcd","deviceId","7299","sensorIds",new[]{"sensor-0","sensor-2"}));
            var values=(System.Collections.IList)Data(sensorReply)["sensors"];var first=(Dictionary<string,object>)values[0];var gpu=(Dictionary<string,object>)values[1];
            Assert(IsOk(sensorReply)&&Calls.Count==1&&Function(Calls[0])=="36","sensor query is a single read");
            Assert(first["value"]==null&&!(bool)first["valueReadbackConfirmed"],"duplicate native sensor IDs do not fabricate values");
            Assert(Convert.ToDouble(gpu["value"])==48.5&&(bool)gpu["valueReadbackConfirmed"],"unique exact sensor finite value");
            ZeroSensorValues=true;Calls.Clear();var zeroResponse=Reply(Object("op","sensors","kind","lcd","deviceId","7299","sensorIds",new[]{"sensor-2"}));
            var zero=(Dictionary<string,object>)((System.Collections.IList)Data(zeroResponse)["sensors"])[0];
            Assert(IsOk(zeroResponse)&&Calls.Count==1&&Function(Calls[0])=="36","zero sample stays a single pure query");
            Assert(zero["value"]==null&&!(bool)zero["valueReadbackConfirmed"]&&(bool)zero["placeholder"],"zero SDK temperature is not presented as live telemetry");
            Assert(Convert.ToDouble(zero["rawNativeValue"])==0&&(bool)zero["nativeValueReadbackConfirmed"]&&zero["unit"]==null,"raw response is retained without inventing units");ZeroSensorValues=false;
            string fixtureDir=Path.Combine(Path.GetTempPath(),"RogControl-synthetic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixtureDir);
            try {
                string source=Path.Combine(fixtureDir,"source.jpg"),notJpeg=Path.Combine(fixtureDir,"invalid.jpg");
                using(var fixture=new Bitmap(800,400)){using(var graphics=Graphics.FromImage(fixture))graphics.Clear(Color.Red);fixture.Save(source,ImageFormat.Jpeg);}File.WriteAllText(notJpeg,"not an image");string originalHash=Hash(source);
                RejectNoIO(Set("lcd","uploadImage","filePath",source,"slot",32),"upload slot upper boundary");
                RejectNoIO(Set("lcd","uploadImage","filePath",source,"slot",-1),"upload slot lower boundary");
                RejectNoIO(Set("lcd","uploadImage","filePath","source.jpg","slot",0),"relative upload path");
                RejectNoIO(Set("lcd","uploadImage","filePath",@"\\host\share\image.jpg","slot",0),"UNC upload path");
                RejectNoIO(Set("lcd","uploadImage","filePath",source+":stream","slot",0),"alternate data stream path");
                RejectNoIO(Set("lcd","uploadImage","filePath",notJpeg,"slot",0),"non-JPEG upload");
                RejectNoIO(Set("lcd","uploadImage","filePath",source,"slot",0,"rawXml","x"),"upload unknown field");
                foreach(string space in new[]{"","-1","0","not a size","2147483648","1","1.5"}) {
                    FreeSpace=space;Calls.Clear();var rejected=Reply(Set("lcd","uploadImage","filePath",source,"slot",0));
                    Assert(!IsOk(rejected)&&Calls.Count==1&&Function(Calls[0])=="26","space validation prevents transfer "+space);
                }
                FreeSpace="10485760";Calls.Clear();var uploaded=Reply(Set("lcd","uploadImage","filePath",source,"slot",31));
                Assert(IsOk(uploaded)&&Calls.Count==4&&Function(Calls[0])=="26"&&Function(Calls[1])=="27"&&Function(Calls[2])=="2"&&Function(Calls[3])=="1","complete upload query flow no native converter");
                Assert(Calls[1].Contains("<type>0</type><index>31</index>")&&Calls[2].Contains("<type>0</type><source>0</source><index_format>31</index_format>"),"custom JPEG transfer and selected profile index");
                Assert(LastWidth==720&&LastHeight==1280&&LastTop.R<8&&LastTop.G<8&&LastTop.B<8&&LastCenter.R>240&&LastCenter.G<10&&LastCenter.B<10,"normalize full image with black letterbox");
                Assert(Hash(source)==originalHash&&File.Exists(source)&&!File.Exists(LastStaged),"original retained and only owned staging file cleaned");
                Assert((bool)Data(uploaded)["overwritesExistingSlot"]&&!(bool)Data(uploaded)["slotOccupancyConfirmed"]&&(bool)Data(uploaded)["transferHardwareAcknowledged"]&&(bool)Data(uploaded)["readbackConfirmed"]&&!(bool)Data(uploaded)["visualIdentityConfirmed"],"upload explicit overwrite and truthful acknowledgement metadata");
                long required=Convert.ToInt64(Data(uploaded)["requiredKiB"]);Assert(required==(Convert.ToInt64(Data(uploaded)["normalizedBytes"])+1023)/1024&&(string)Data(uploaded)["freeSpaceUnit"]=="KiB","verified native size units with conservative round-up");
                FreeSpace=Convert.ToString(required);Calls.Clear();var exactSpace=Reply(Set("lcd","uploadImage","filePath",source,"slot",0));Assert(IsOk(exactSpace)&&Calls.Count==4,"exact KiB capacity accepts slot zero");
                FreeSpace=Convert.ToString(required-1);Calls.Clear();var notEnough=Reply(Set("lcd","uploadImage","filePath",source,"slot",0));Assert(!IsOk(notEnough)&&Calls.Count==1,"one KiB below required prevents upload");FreeSpace="10485760";
                Ack="<root/>";Calls.Clear();var uncertain=Reply(Set("lcd","uploadImage","filePath",source,"slot",1));Assert(IsOk(uncertain)&&Calls.Count==2&&!(bool)Data(uncertain)["hardwareAcknowledged"]&&!(bool)Data(uncertain)["presentationAttempted"]&&!File.Exists(LastStaged),"unconfirmed transfer never selected no retry");Ack="<root><result>0</result></root>";
                Fault="27";Calls.Clear();var transferFailed=Reply(Set("lcd","uploadImage","filePath",source,"slot",2));Assert(!IsOk(transferFailed)&&Calls.Count==2&&(bool)Data(transferFailed)["overwritesExistingSlot"],"failed transfer stops and preserves slot uncertainty");Fault="2";
                Calls.Clear();var presentFailed=Reply(Set("lcd","uploadImage","filePath",source,"slot",3));Assert(!IsOk(presentFailed)&&Calls.Count==3&&(bool)Data(presentFailed)["hardwareAcknowledged"]&&!(bool)Data(presentFailed)["presentationHardwareAcknowledged"]&&!File.Exists(LastStaged),"partial transfer success preserved when presentation fails");Fault="";
                Fault="1";Calls.Clear();var readFailed=Reply(Set("lcd","uploadImage","filePath",source,"slot",3));Assert(IsOk(readFailed)&&Calls.Count==4&&(bool)Data(readFailed)["transferHardwareAcknowledged"]&&(bool)Data(readFailed)["presentationHardwareAcknowledged"]&&!(bool)Data(readFailed)["readbackConfirmed"],"transfer and presentation acknowledgements survive failed profile read");Fault="";
                Assert(Hash(source)==originalHash,"all failure paths preserve original bytes");
            } finally {
                string safeFixture=Path.GetFullPath(fixtureDir),safeTemp=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!safeFixture.StartsWith(safeTemp,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(safeFixture).StartsWith("RogControl-synthetic-",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup path");
                Directory.Delete(safeFixture,true);
            }
            IgnorePowerWrite=true;Calls.Clear();var mismatch=Reply(Set("lcd","powerS0","value",false));Assert(IsOk(mismatch)&&Calls.Count==3&&!(bool)Data(mismatch)["readbackConfirmed"]&&(string)Data(mismatch)["appliedState"]=="mismatch","mismatch preserved no retry");
            Fault="7";Calls.Clear();var nativeFail=Reply(Set("lcd","powerS0","value",false));Assert(!IsOk(nativeFail)&&Calls.Count==2&&(bool)Data(nativeFail)["writeAttempted"],"native write failure metadata");
            Ready("lcd");FailPowerReadAfterWrite=true;Calls.Clear();var failedReadback=Reply(Set("lcd","powerS0","value",false));Assert(IsOk(failedReadback)&&Calls.Count==3&&(bool)Data(failedReadback)["hardwareAcknowledged"]&&!(bool)Data(failedReadback)["readbackConfirmed"]&&Data(failedReadback)["value"]==null,"write acknowledgement survives failed readback");
            Ready("lcd");Calls.Clear();var media=Reply(Object("id",91,"op","media","kind","lcd","deviceId","7299"));
            Assert(IsOk(media)&&Calls.Count==2&&Function(Calls[0])=="26"&&Function(Calls[1])=="1","media status uses only space/profile getters");
            Assert((bool)Data(media)["freeSpaceReadbackConfirmed"]&&Convert.ToInt64(Data(media)["freeSpaceKiB"])==10485760&&!((bool)Data(media)["slotOccupancyReadable"])&&Data(media)["slots"]==null,"space verified without invented slot inventory");
            Assert(!((bool)Data(media)["animationUploadSupported"])&&((System.Collections.IList)Data(media)["userUploadFormats"]).Count==1,"current motherboard custom media only JPEG by source");
            RejectNoIO(Object("id",92,"op","media","kind","lcd","deviceId","7299","slot",0),"media query unknown field");
            Calls.Clear();var selectedSlot=Reply(Set("lcd","selectMedia","slot",31,"durationSeconds",10));
            Assert(IsOk(selectedSlot)&&Calls.Count==2&&Function(Calls[0])=="2"&&Function(Calls[1])=="1","select existing slot writes profile then reads metadata");
            Assert(Calls[0].Contains("<type>0</type><source>0</source><index_format>31</index_format>")&&(bool)Data(selectedSlot)["profileReadbackConfirmed"]&&!((bool)Data(selectedSlot)["visualIdentityConfirmed"]),"select JPEG slot exact fields and separate pixel uncertainty");
            RejectNoIO(Set("lcd","selectMedia","slot",32),"select slot boundary");
            RejectNoIO(Set("lcd","selectMedia","slot",0,"mediaType","gif"),"unsupported custom animation selector");
            RejectNoIO(Set("lcd","deleteMedia","slot",0),"slot deletion confirmation missing");
            RejectNoIO(Set("lcd","deleteMedia","slot",0,"confirmSlotDeletion",false),"slot deletion confirmation false");
            RejectNoIO(Set("lcd","deleteMedia","slot",0,"confirmSlotDeletion","true"),"slot deletion confirmation type");
            RejectNoIO(Set("lcd","deleteMedia","slot",-1,"confirmSlotDeletion",true),"slot deletion lower boundary");
            RejectNoIO(Set("lcd","deleteMedia","slot",32,"confirmSlotDeletion",true),"slot deletion upper boundary");
            RejectNoIO(Set("lcd","deleteMedia","slot",0,"confirmSlotDeletion",true,"all",true),"no implicit delete-all route");
            Calls.Clear();var deleted=Reply(Set("lcd","deleteMedia","slot",0,"confirmSlotDeletion",true));
            Assert(IsOk(deleted)&&Calls.Count==1&&Function(Calls[0])=="29"&&Calls[0].Contains("<media_delete><type>0</type><index>0</index>"),"single-slot delete exact native source XML");
            Assert((bool)Data(deleted)["hardwareAcknowledged"]&&!((bool)Data(deleted)["deletionReadbackConfirmed"])&&!((bool)Data(deleted)["slotOccupancyReadable"])&&(string)Data(deleted)["appliedState"]=="deletionAcknowledged","deletion ACK never claims empty slot readback");
            Ack="<root/>";Calls.Clear();var deleteUncertain=Reply(Set("lcd","deleteMedia","slot",31,"confirmSlotDeletion",true));Assert(IsOk(deleteUncertain)&&Calls.Count==1&&!((bool)Data(deleteUncertain)["hardwareAcknowledged"]),"return-only delete uncertainty no retries");Ack="<root><result>0</result></root>";
            Fault="29";Calls.Clear();var deleteFailed=Reply(Set("lcd","deleteMedia","slot",1,"confirmSlotDeletion",true));Assert(!IsOk(deleteFailed)&&Calls.Count==1&&(bool)Data(deleteFailed)["writeAttempted"],"failed deletion keeps write-attempt uncertainty");Fault="";
            foreach(string space in new[]{"-1","not a size","2147483648"}){FreeSpace=space;Calls.Clear();var badSpace=Reply(Object("id",93,"op","media","kind","lcd","deviceId","7299"));Assert(!IsOk(badSpace)&&Calls.Count==1&&Function(Calls[0])=="26","invalid media capacity stops before profile "+space);}FreeSpace="10485760";
            Configure("lcd");RejectNoIO(Object("id",94,"op","media","kind","lcd","deviceId","7299"),"unscanned media query");RejectNoIO(Set("lcd","deleteMedia","slot",0,"confirmSlotDeletion",true),"unscanned media deletion");
            Configure("lcd");RejectNoIO(Set("lcd","brightness","value",50),"unscanned LCD");
            var badKind=Set("lcd","brightness","value",50);badKind["kind"]="fan";RejectNoIO(badKind,"isolated kind");
            foreach(string lcdId in new[]{"7124","7299","7571"}) {
                Configure("lcd");LcdDetectedId=lcdId;
                var familyScan=Reply(Object("id",95,"op","scan","kind","lcd","deviceId",lcdId));
                Assert(IsOk(familyScan)&&Calls.Count==1&&Calls[0].Contains("<device key=\""+lcdId+"\""),"family scan exact requested model "+lcdId);
                var devices=(System.Collections.IList)Data(familyScan)["devices"];
                Assert(devices.Count==1&&(string)((Dictionary<string,object>)devices[0])["deviceId"]==lcdId,"family exact capability identity "+lcdId);
                var familyRead=Reply(Object("id",96,"op","read","kind","lcd","deviceId",lcdId));
                Assert(IsOk(familyRead)&&(bool)Data(familyRead)["sourcePrepared"]&&!(bool)Data(familyRead)["hardwareWriteValidated"],"family source versus physical validation "+lcdId);
                var assets=(System.Collections.IList)Data(familyRead)["presetAssets"];
                string expected=lcdId=="7124"?"ex_5_launch1.gif":lcdId=="7571"?"x870e_glacial_launch_1.gif":"x870_launch1.gif";
                Assert(assets.Count==8&&(string)((Dictionary<string,object>)assets[0])["fileName"]==expected,"family source-specific preset filename "+lcdId);
            }
            Configure("lcd");var wrongModel=Reply(Object("id",97,"op","scan","kind","lcd","deviceId","7124"));
            Assert(IsOk(wrongModel)&&((System.Collections.IList)Data(wrongModel)["devices"]).Count==0,"mismatched native identity excluded");
            RejectNoIO(Object("id",98,"op","scan","kind","lcd","deviceId","9999"),"unknown LCD family");
            RejectNoIO(Object("id",98,"op","scan","kind","lcd","deviceId","7299","xml","arbitrary"),"scan unknown field");
            Configure("lcd");LcdFirmware="oops";Reply(Object("id",99,"op","scan","kind","lcd"));var invalidFw=Reply(Object("id",100,"op","read","kind","lcd","deviceId","7299"));
            Assert(IsOk(invalidFw)&&!(bool)Data(invalidFw)["deviceAlive"]&&!(bool)Data(invalidFw)["supportsWrite"]&&Data(invalidFw)["firmware"]==null,"malformed firmware cannot validate LCD alive");
            RejectNoIO(Set("lcd","powerS0","value",true),"malformed firmware setter gated");
            var oversized=SDKWorker.HandleLine(new string('x',65537));Assert(!(bool)Json.Deserialize<Dictionary<string,object>>(oversized)["ok"],"request size");
            var nullReply=Json.Deserialize<Dictionary<string,object>>(SDKWorker.HandleLine("null"));Assert(!IsOk(nullReply),"null object");
            var correlated=Reply(Object("id","test-42","op","unknown","kind","lcd"));Assert((string)correlated["id"]=="test-42"&&(string)correlated["kind"]=="lcd"&&(string)correlated["op"]=="unknown","protocol correlation");
            Console.WriteLine("SDKWorker synthetic checks passed: "+Count+"; hardware writes performed: 0; native DLLs loaded: 0");return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex.ToString());return 1; }
    }
}
