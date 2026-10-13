// Owned feature adapter. Facts are derived from public GearLink commands for p725 / m709.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class GearLinkWorker
{
    internal static readonly string[] MouseButtons={"leftButton","rightButton","middleButton","sideKeyLeftBackward","sideKeyLeftForward","dpi1","scrollUp","scrollDown"};
    internal static readonly int[] MouseButtonCodes={496,497,498,484,485,486,488,489};
    internal static readonly string[] OledBarItems={"CapsLock","NumLock","ConnectionStatus","BatteryStatus","RapidTrigger","MacModePCMode","CurrentProfile","SpeedTap","ScrollLock"};
    internal static readonly Dictionary<string,int> KeyboardKeys=CreateKeyboardKeys();
    internal static readonly Dictionary<string,int> MouseTargets=CreateMouseTargets();
    internal static Dictionary<string,int> CreateKeyboardKeys()
    {
        // ASUS keyboard position codes differ from standard USB HID keyboard usage codes.
        var result=new Dictionary<string,int>(StringComparer.Ordinal);
        string[] names={"Backquote","Digit1","Digit2","Digit3","Digit4","Digit5","Digit6","Digit7","Digit8","Digit9","Digit0","Minus","Equal","Backspace","Tab","KeyQ","KeyW","KeyE","KeyR","KeyT","KeyY","KeyU","KeyI","KeyO","KeyP","BracketLeft","BracketRight","CapsLock","KeyA","KeyS","KeyD","KeyF","KeyG","KeyH","KeyJ","KeyK","KeyL","Semicolon","Quote","Enter","ShiftLeft","KeyZ","KeyX","KeyC","KeyV","KeyB","KeyN","KeyM","Comma","Period","Slash","ShiftRight","ControlLeft","MetaLeft","AltLeft","Space","AltRight","ControlRight","Insert","Delete","ArrowLeft","Home","End","ArrowUp","ArrowDown","PageUp","PageDown","ArrowRight","Escape","AV_NextTrack","AV_PrevTrack","AV_Stop","AV_PlayPause","AV_Mute","AV_VolUp","AV_VolDown","LeverUp","LeverMid","LeverDown","Disabled","Default"};
        int[] codes={1,2,3,4,5,6,7,8,9,10,11,12,13,15,16,17,18,19,20,21,22,23,24,25,26,27,28,30,31,32,33,34,35,36,37,38,39,40,41,43,44,46,47,48,49,50,51,52,53,54,55,57,58,59,60,61,62,64,75,76,79,80,81,83,84,85,86,89,110,180,181,182,183,184,187,188,164,165,166,0,255};
        for(int i=0;i<names.Length;i++)result.Add(names[i],codes[i]);
        for(int i=1;i<=12;i++)result.Add("F"+i,111+i);
        return result;
    }
    internal static Dictionary<string,int> CreateMouseTargets()
    {
        var result=new Dictionary<string,int>(StringComparer.Ordinal);
        for(int i=0;i<26;i++)result.Add("Key"+(char)('A'+i),4+i);
        for(int i=1;i<=9;i++)result.Add("Digit"+i,29+i);result.Add("Digit0",39);
        string[] names={"Enter","Escape","Backspace","Tab","Space","ArrowRight","ArrowLeft","ArrowDown","ArrowUp","LeftCtrl","LeftShift","LeftAlt","LeftWin","RightCtrl","RightShift","RightAlt","RightWin","MultimediaPlayPause","MultimediaStop","MultimediaPrevTrack","MultimediaNextTrack","MultimediaMute","MultimediaVolumeDown","MultimediaVolumeUp","MouseProfile","MouseDoubleClick","MouseLeftBackward","MouseLeftForward","MouseDPISwitch","MouseTargetFocus","MouseScrollUp","MouseScrollDown","MouseClickLeft","MouseClickRight","MouseClickMiddle","MouseDpiUp","MouseDpiDown","KeyDisabled","ShiftMode"};
        int[] codes={40,41,42,43,44,79,80,81,82,224,225,226,227,228,229,230,231,240,241,242,243,244,245,246,480,483,484,485,486,487,488,489,496,497,498,499,500,511,253};
        for(int i=0;i<names.Length;i++)result.Add(names[i],codes[i]);
        for(int i=1;i<=12;i++)result.Add("F"+i,57+i);
        return result;
    }
    internal static int[] KeyboardPollingRates(int modelPid) { return modelPid==7586||modelPid==7588?new int[]{1000,8000}:new int[0]; }
    internal static object FeatureOptions(string kind,int modelPid=0)
    {
        if(kind=="mouse")return Obj("buttonSources",MouseButtons,"buttonTargets",MouseTargets.Keys.ToArray(),"zonePollingRates",new int[]{125,250,500,1000,2000,4000,8000},"zoneTxPower",new object[]{Obj("value",0,"name","FullPower"),Obj("value",1,"name","EnvironmentAdaptive")});
        if(kind=="keyboard")return Obj("pollingRates",KeyboardPollingRates(modelPid),"keySources",KeyboardKeys.Keys.Where(k=>!k.StartsWith("AV_")&&k!="Disabled"&&k!="Default").ToArray(),"keyTargets",KeyboardKeys.Keys.Where(k=>!k.StartsWith("Lever")).ToArray(),"oledModes",new string[]{"Animation","Clock","LaptopBattery","HWInfo","KPM"},"leverModes",new string[]{"MediaVolume","MediaContent","LightBrightnessPerKeyZone","LightBrightnessOLED","LightEffectSwitch","FanControlMB","FanControlAIO","Customize"},"oledBarItems",OledBarItems);
        return Obj();
    }
    internal static void ReadFeatures(GearHid hid,GearDescriptor descriptor,byte report,string kind,Dictionary<string,object> request,Dictionary<string,object> snapshot,List<string> caps,List<object> errors)
    {
        if(kind=="mouse") {
            QueryField(hid,report,Packet(0x12,20,1),"zoneSettings",errors,b=>{snapshot["zoneSettings"]=ParseZone(b);caps.Add("zoneSettings");});
            QueryField(hid,report,Packet(0x12,5,0),"buttonBindings",errors,b=>{snapshot["buttonBindings"]=ParseMouseBindings(b);caps.Add("buttonBindings");});
            QueryField(hid,report,Packet(0x12,5,7),"shiftButtonBindings",errors,b=>{snapshot["shiftButtonBindings"]=ParseMouseBindings(b);caps.Add("shiftButtonBindings");});
            return;
        }
        QueryField(hid,report,Packet(0x23,0,0),"oledFirmware",errors,b=>{Require(b,8);string version=VersionNative4(b,4);if(version==null)throw new IOException("OLED firmware information is not ready.");snapshot["oledFirmware"]=version;caps.Add("oledFirmware");});
        QueryField(hid,report,Packet(0x23,2,0),"oledEnabled",errors,b=>{snapshot["oledEnabled"]=ReadBoolean(b,4);caps.Add("oledEnabled");});
        QueryField(hid,report,Packet(0x23,3,0),"oledBrightness",errors,b=>{Require(b,5);if(b[4]>100)throw new IOException("Unknown OLED brightness.");snapshot["oledBrightness"]=(int)b[4];caps.Add("oledBrightness");});
        QueryField(hid,report,Packet(0x23,4,0),"oledBarInfo",errors,b=>{Require(b,6);snapshot["oledBarRaw"]=new int[]{b[4],b[5]};var items=new Dictionary<string,object>();for(int i=0;i<OledBarItems.Length;i++)items.Add(OledBarItems[i],(b[4+i/8]&(1<<(i%8)))!=0);snapshot["oledBarInfo"]=items;caps.Add("oledBarInfo");});
        QueryField(hid,report,Packet(0x24,1,0),"oledMode",errors,b=>{Require(b,5);if(b[4]>4)throw new IOException("OLED mode is outside the verified model modes.");snapshot["oledMode"]=(int)b[4];caps.Add("oledMode");});
        QueryField(hid,report,Packet(0x24,2,0),"oledModeStates",errors,b=>{snapshot["oledModeStates"]=ReadBooleans(b,new int[]{4,5,6,7,8});caps.Add("oledModeStates");});
        QueryField(hid,report,Packet(0x24,3,0),"oledKpsEnabled",errors,b=>{snapshot["oledKpsEnabled"]=ReadBoolean(b,4);caps.Add("oledKpsEnabled");});
        QueryField(hid,report,Packet(0x21,0,0),"oledPreset",errors,b=>{Require(b,5);if(b[4]>7)throw new IOException("Unknown OLED animation index.");snapshot["oledPreset"]=(int)b[4];caps.Add("oledPreset");});
        QueryField(hid,report,Packet(0x22,1,0),"leverMode",errors,b=>{Require(b,5);if(b[4]>7)throw new IOException("Unknown lever mode.");snapshot["leverMode"]=(int)b[4];caps.Add("leverMode");});
        // m709's own model subclass skips the two magnetic-axis fields at offsets 6/7.
        QueryField(hid,report,Packet(0x25,11,0),"leverModeStates",errors,b=>{snapshot["leverModeStates"]=ReadBooleans(b,new int[]{4,5,8,9,10,11,12,13});caps.Add("leverModeStates");});
        // Read only the user-selected binding. Never scan every key or invent default bindings.
        if(request!=null&&Text(request,"setting")=="keyBinding") {
            var value=request.ContainsKey("value")?request["value"] as Dictionary<string,object>:null;
            if(value==null||!value.ContainsKey("source"))throw new ArgumentException("keyBinding read requires value.source.");
            string source=value["source"] as string;int code=KeyCode(KeyboardKeys,source);if(source=="Disabled"||source=="Default"||source.StartsWith("AV_"))throw new ArgumentException("Unsupported physical key source.");
            if(!LegacyKeyboard(descriptor.Pid))throw new ArgumentException("This keyboard binding protocol has not been verified for the model.");
            bool lever=source.StartsWith("Lever");byte[] packet=lever?Packet(0x25,12,0,7,(byte)code,(byte)(code>>8)):Packet(0x25,13,0,(byte)code,(byte)(code>>8));
            QueryField(hid,report,packet,"keyBinding",errors,b=>{snapshot["keyBinding"]=ParseKeyboardBinding(source,b);caps.Add("keyBinding");});
        }
    }
    internal static Dictionary<string,object> ParseZone(byte[] b)
    {
        Require(b,11);int[] rates={125,250,500,1000,2000,4000,8000};if(b[4]>1||b[5]>=rates.Length||b[10]>1)throw new IOException("Unknown Zone mode data.");int x=U16(b,6)+1,y=U16(b,8)+1;if(x<100||x>65000||y<100||y>65000)throw new IOException("Zone DPI is outside the verified model range.");
        return Obj("enabled",b[4]==1,"pollingRate",rates[b[5]],"dpi",x,"dpiY",y,"txPower",(int)b[10]);
    }
    internal static Dictionary<string,object> ParseMouseBindings(byte[] b)
    {
        Require(b,20);var result=new Dictionary<string,object>();for(int i=0;i<MouseButtons.Length;i++){int code=U16(b,4+i*2);result.Add(MouseButtons[i],Obj("target",NameForCode(MouseTargets,code),"targetCode",code));}return result;
    }
    internal static bool ReadBoolean(byte[] b,int offset){Require(b,offset+1);if(b[offset]>1)throw new IOException("Unknown switch value.");return b[offset]==1;}
    internal static bool[] ReadBooleans(byte[] b,int[] offsets){return offsets.Select(i=>ReadBoolean(b,i)).ToArray();}
    internal static string NameForCode(Dictionary<string,int> map,int code){foreach(var pair in map)if(pair.Value==code)return pair.Key;return null;}
    internal static Dictionary<string,object> ParseKeyboardBinding(string source,byte[] response){Require(response,6);int code=KeyCode(KeyboardKeys,source),target=U16(response,4);return Obj("source",source,"sourceCode",code,"target",NameForCode(KeyboardKeys,target),"targetCode",target,"lever",source.StartsWith("Lever"));}
    internal static int KeyCode(Dictionary<string,int> map,string name){int code;if(name==null||!map.TryGetValue(name,out code))throw new ArgumentException("Unknown key or function name.");return code;}
    internal static void AddFeatureSetCapabilities(string kind,bool online,Dictionary<string,object> state,Dictionary<string,object> snapshot,List<string> caps)
    {
        object pid;int model=state.TryGetValue("pid",out pid)&&pid is int?(int)pid:0;state["settingOptions"]=FeatureOptions(kind,model);if(!online)return;var setters=new List<string>((string[])state["setCapabilities"]);
        if(kind=="mouse"){if(caps.Contains("zoneSettings"))setters.Add("zoneSettings");if(caps.Contains("buttonBindings")||caps.Contains("shiftButtonBindings"))setters.Add("buttonBinding");}
        if(kind=="keyboard") {foreach(string name in new string[]{"oledEnabled","oledBrightness","oledMode","oledKpsEnabled","oledPreset","leverMode","keyBinding"})if(caps.Contains(name))setters.Add(name);if(caps.Contains("oledModeStates"))setters.Add("oledModeEnabled");if(caps.Contains("leverModeStates"))setters.Add("leverModeEnabled");if(caps.Contains("oledBarInfo"))setters.Add("oledBarItem");}
        state["setCapabilities"]=setters.Distinct().ToArray();
    }
    internal static Dictionary<string,object> ValueObject(object value,params string[] keys)
    {
        var result=value as Dictionary<string,object>;if(result==null||result.Count!=keys.Length||keys.Any(k=>!result.ContainsKey(k)))throw new ArgumentException("Setting requires exactly: "+String.Join(", ",keys)+".");return result;
    }
    internal static byte BooleanByte(object value){if(!(value is bool))throw new ArgumentException("Switch must be boolean.");return (bool)value?(byte)1:(byte)0;}
    internal static byte[] BuildFeatureSetting(string kind,string setting,object value,Dictionary<string,object> before)
    {
        if(kind=="mouse"&&setting=="zoneSettings") {
            ValueObject(value,"enabled","pollingRate","dpi","dpiY","txPower");var v=(Dictionary<string,object>)value;var baseline=before.ContainsKey("zoneSettings")?before["zoneSettings"] as Dictionary<string,object>:null;if(baseline==null)throw new ArgumentException("Current Zone mode is unknown.");
            int[] rates={125,250,500,1000,2000,4000,8000};int rate=Integer(v["pollingRate"],125,8000),index=Array.IndexOf(rates,rate);if(index<0)throw new ArgumentException("Unsupported Zone polling rate.");int x=Integer(v["dpi"],100,65000)-1,y=Integer(v["dpiY"],100,65000)-1,power=Integer(v["txPower"],0,1);
            return Packet(0x51,68,1,BooleanByte(v["enabled"]),(byte)index,(byte)x,(byte)(x>>8),(byte)y,(byte)(y>>8),(byte)power);
        }
        if(kind=="mouse"&&setting=="buttonBinding") {
            var v=ValueObject(value,"button","target","shift");string source=v["button"] as string;int index=Array.IndexOf(MouseButtons,source);if(index<0)throw new ArgumentException("Unknown physical mouse button.");bool shift=BooleanByte(v["shift"])==1;int code=KeyCode(MouseTargets,v["target"] as string);string field=shift?"shiftButtonBindings":"buttonBindings";var mappings=before.ContainsKey(field)?before[field] as Dictionary<string,object>:null;if(mappings==null||!mappings.ContainsKey(source))throw new ArgumentException("Current button mapping is unknown.");int button=MouseButtonCodes[index];return Packet(0x51,shift?(byte)35:(byte)33,0,(byte)button,(byte)(button>>8),(byte)code,(byte)(code>>8));
        }
        if(kind!="keyboard")return null;
        if(setting=="oledEnabled"||setting=="oledKpsEnabled")return Packet(setting=="oledEnabled"?(byte)0x69:(byte)0x6B,0,0,BooleanByte(value));
        if(setting=="oledBrightness")return Packet(0x68,0,0,(byte)Integer(value,0,100));
        if(setting=="oledMode")return Packet(0x6A,1,0,(byte)Integer(value,0,4));
        if(setting=="oledPreset")return Packet(0x61,0,0,(byte)Integer(value,0,5));
        if(setting=="leverMode")return Packet(0x71,2,0,(byte)Integer(value,0,7));
        if(setting=="oledModeEnabled"||setting=="leverModeEnabled") {var v=ValueObject(value,"mode","enabled");int mode=Integer(v["mode"],0,setting=="oledModeEnabled"?4:7);return Packet(setting=="oledModeEnabled"?(byte)0x6A:(byte)0x71,0,0,(byte)mode,BooleanByte(v["enabled"]));}
        if(setting=="oledBarItem") {var v=ValueObject(value,"item","enabled");int index=Array.IndexOf(OledBarItems,v["item"] as string);if(index<0)throw new ArgumentException("Unknown OLED status item.");var raw=before.ContainsKey("oledBarRaw")?before["oledBarRaw"] as int[]:null;if(raw==null||raw.Length!=2||raw.Any(b=>b<0||b>255))throw new ArgumentException("Current OLED status data is unknown.");int[] result=(int[])raw.Clone();int bit=1<<(index%8);result[index/8]=BooleanByte(v["enabled"])==1?result[index/8]|bit:result[index/8]&~bit;return Packet(0x51,96,0,(byte)result[0],(byte)result[1]);}
        if(setting=="keyBinding") {var v=ValueObject(value,"source","target");string source=v["source"] as string;int a=KeyCode(KeyboardKeys,source),b=KeyCode(KeyboardKeys,v["target"] as string);if(source=="Disabled"||source=="Default"||source.StartsWith("AV_")||(v["target"] as string).StartsWith("Lever"))throw new ArgumentException("Unsupported source or target key.");var prior=before.ContainsKey("keyBinding")?before["keyBinding"] as Dictionary<string,object>:null;if(prior==null||Text(prior,"source")!=source)throw new ArgumentException("The selected key has no verified readback.");return source.StartsWith("Lever")?Packet(0x71,1,0,7,(byte)a,(byte)(a>>8),(byte)b,(byte)(b>>8)):Packet(0x51,32,(ushort)a,(byte)b,(byte)(b>>8));}
        return null;
    }
    internal static bool IsFeatureQuery(string kind,byte[] packet)
    {
        if(kind!="keyboard"||packet.Length<4||packet[2]!=0||packet[3]!=0)return false;
        return packet[0]==0x21&&packet[1]==0||packet[0]==0x22&&packet[1]==1||packet[0]==0x23&&packet[1]<=4||packet[0]==0x24&&packet[1]>=1&&packet[1]<=3||packet[0]==0x25&&(packet[1]==11||packet[1]==12||packet[1]==13);
    }
    internal static bool IsFeatureWrite(string kind,byte[] packet)
    {
        if(packet.Length<5||packet[3]!=0)return false;
        if(kind=="mouse")return packet[0]==0x51&&(packet[1]==68&&packet[2]==1&&packet.Length==11||(packet[1]==33||packet[1]==35)&&packet[2]==0&&packet.Length==8&&MouseButtonCodes.Contains(U16(packet,4))&&MouseTargets.Values.Contains(U16(packet,6)));
        if(kind!="keyboard")return false;
        if(packet[0]==0x51&&packet[1]==32&&packet.Length==6)return KeyboardKeys.Values.Contains(packet[2])&&KeyboardKeys.Values.Contains(U16(packet,4));
        if(packet[2]!=0)return false;
        return packet[0]==0x51&&packet[1]==96&&packet.Length==6||new byte[]{0x61,0x68,0x69,0x6B}.Contains(packet[0])&&packet[1]==0&&packet.Length==5||packet[0]==0x6A&&(packet[1]==0&&packet.Length==6||packet[1]==1&&packet.Length==5)||packet[0]==0x71&&(packet[1]==0&&packet.Length==6||packet[1]==2&&packet.Length==5||packet[1]==1&&packet.Length==9&&packet[4]==7&&new int[]{164,165,166}.Contains(U16(packet,5))&&KeyboardKeys.Values.Contains(U16(packet,7)));
    }
    internal static bool? MatchesFeatureSetting(string setting,object value,Dictionary<string,object> snapshot)
    {
        object prior;
        if(setting=="zoneSettings"){if(!snapshot.TryGetValue(setting,out prior)||!(prior is Dictionary<string,object>)||!(value is Dictionary<string,object>))return false;var v=(Dictionary<string,object>)value;var p=(Dictionary<string,object>)prior;return new string[]{"enabled","pollingRate","dpi","dpiY","txPower"}.All(k=>p.ContainsKey(k)&&v.ContainsKey(k)&&Json.Serialize(p[k])==Json.Serialize(v[k]));}
        if(setting=="buttonBinding"){var v=value as Dictionary<string,object>;if(v==null)return false;string field=(bool)v["shift"]?"shiftButtonBindings":"buttonBindings";if(!snapshot.TryGetValue(field,out prior)||!(prior is Dictionary<string,object>))return false;var p=(Dictionary<string,object>)prior;object binding;if(!p.TryGetValue(v["button"] as string,out binding)||!(binding is Dictionary<string,object>))return false;return (int)((Dictionary<string,object>)binding)["targetCode"]==KeyCode(MouseTargets,v["target"] as string);}
        if(setting=="oledModeEnabled"||setting=="leverModeEnabled"){var v=value as Dictionary<string,object>;if(v==null||!snapshot.TryGetValue(setting=="oledModeEnabled"?"oledModeStates":"leverModeStates",out prior)||!(prior is bool[]))return false;var modes=(bool[])prior;int mode=Convert.ToInt32(v["mode"]);return mode>=0&&mode<modes.Length&&modes[mode]==(bool)v["enabled"];}
        if(setting=="oledBarItem"){var v=value as Dictionary<string,object>;if(v==null||!snapshot.TryGetValue("oledBarInfo",out prior)||!(prior is Dictionary<string,object>))return false;var items=(Dictionary<string,object>)prior;object current;return items.TryGetValue(v["item"] as string,out current)&&current is bool&&(bool)current==(bool)v["enabled"];}
        if(setting=="keyBinding"){var v=value as Dictionary<string,object>;if(v==null||!snapshot.TryGetValue(setting,out prior)||!(prior is Dictionary<string,object>))return false;var p=(Dictionary<string,object>)prior;return Text(p,"source")==v["source"] as string&&(int)p["targetCode"]==KeyCode(KeyboardKeys,v["target"] as string);}
        return null;
    }
    internal static void ExtendedSelfTest()
    {
        int tests=0;Action<bool> check=v=>{if(!v)throw new Exception("Extended synthetic protocol check failed.");tests++;};
        var before=Obj("zoneSettings",Obj("enabled",false,"pollingRate",1000,"dpi",1600,"dpiY",800,"txPower",1),"buttonBindings",ParseMouseBindings(new byte[]{18,5,0,0,240,1,241,1,242,1,228,1,229,1,230,1,232,1,233,1}),"shiftButtonBindings",ParseMouseBindings(new byte[]{18,5,7,0,240,1,241,1,242,1,228,1,229,1,230,1,232,1,233,1}),"oledBarRaw",new int[]{61,128},"oledModeStates",new bool[]{true,false,false,false,true},"leverModeStates",new bool[]{true,true,true,true,true,false,false,true},"keyBinding",Obj("source","KeyA","targetCode",31));
        var zone=Obj("enabled",true,"pollingRate",8000,"dpi",1600,"dpiY",800,"txPower",1);
        check(BuildFeatureSetting("mouse","zoneSettings",zone,before).SequenceEqual(new byte[]{81,68,1,0,1,6,63,6,31,3,1}));
        var parsed=ParseZone(new byte[]{18,20,1,0,1,6,63,6,31,3,1});before["zoneSettings"]=parsed;check(MatchesFeatureSetting("zoneSettings",zone,before)==true);zone["dpiY"]=801;check(MatchesFeatureSetting("zoneSettings",zone,before)==false);
        check(BuildFeatureSetting("mouse","buttonBinding",Obj("button","leftButton","target","KeyA","shift",false),before).SequenceEqual(new byte[]{81,33,0,0,240,1,4,0}));
        check(BuildFeatureSetting("mouse","buttonBinding",Obj("button","scrollDown","target","MultimediaVolumeUp","shift",true),before).SequenceEqual(new byte[]{81,35,0,0,233,1,246,0}));
        check(MatchesFeatureSetting("buttonBinding",Obj("button","leftButton","target","MouseClickLeft","shift",false),before)==true);
        check(BuildFeatureSetting("keyboard","oledEnabled",false,before).SequenceEqual(new byte[]{105,0,0,0,0}));
        check(BuildFeatureSetting("keyboard","oledBrightness",100,before).SequenceEqual(new byte[]{104,0,0,0,100}));
        check(BuildFeatureSetting("keyboard","oledMode",4,before).SequenceEqual(new byte[]{106,1,0,0,4}));
        check(BuildFeatureSetting("keyboard","oledModeEnabled",Obj("mode",3,"enabled",true),before).SequenceEqual(new byte[]{106,0,0,0,3,1}));
        check(BuildFeatureSetting("keyboard","oledKpsEnabled",true,before).SequenceEqual(new byte[]{107,0,0,0,1}));
        check(BuildFeatureSetting("keyboard","oledPreset",5,before).SequenceEqual(new byte[]{97,0,0,0,5}));
        check(BuildFeatureSetting("keyboard","oledBarItem",Obj("item","ScrollLock","enabled",true),before).SequenceEqual(new byte[]{81,96,0,0,61,129}));
        check(BuildFeatureSetting("keyboard","leverMode",7,before).SequenceEqual(new byte[]{113,2,0,0,7}));
        check(BuildFeatureSetting("keyboard","leverModeEnabled",Obj("mode",3,"enabled",false),before).SequenceEqual(new byte[]{113,0,0,0,3,0}));
        check(MatchesFeatureSetting("oledModeEnabled",Obj("mode",4,"enabled",true),before)==true);
        check(MatchesFeatureSetting("leverModeEnabled",Obj("mode",3,"enabled",false),before)==false);
        check(ReadBooleans(new byte[]{37,11,0,0,1,0,55,66,1,0,1,0,1,0},new int[]{4,5,8,9,10,11,12,13}).SequenceEqual(new bool[]{true,false,true,false,true,false,true,false}));
        check(BuildFeatureSetting("keyboard","keyBinding",Obj("source","KeyA","target","KeyB"),before).SequenceEqual(new byte[]{81,32,31,0,50,0}));
        check(MatchesFeatureSetting("keyBinding",Obj("source","KeyA","target","KeyA"),before)==true);
        before["keyBinding"]=Obj("source","LeverUp","targetCode",187);
        check(BuildFeatureSetting("keyboard","keyBinding",Obj("source","LeverUp","target","AV_VolUp"),before).SequenceEqual(new byte[]{113,1,0,0,7,164,0,187,0}));
        check(NameForCode(MouseTargets,4080)==null);check(NameForCode(KeyboardKeys,999)==null);
        foreach(var bad in new object[]{-1,101,50.1,"50",true}){bool rejected=false;try{BuildFeatureSetting("keyboard","oledBrightness",bad,before);}catch(ArgumentException){rejected=true;}check(rejected);}
        bool blocked=false;try{BuildFeatureSetting("keyboard","oledPreset",6,before);}catch(ArgumentException){blocked=true;}check(blocked);
        blocked=false;try{BuildFeatureSetting("mouse","buttonBinding",Obj("button","rawCode","target","KeyA","shift",false),before);}catch(ArgumentException){blocked=true;}check(blocked);
        blocked=false;try{ParseZone(new byte[]{18,20,1,0,255,6,63,6,31,3,1});}catch(IOException){blocked=true;}check(blocked);
        check(KeyboardKeys["KeyA"]!=MouseTargets["KeyA"]);check(!KeyboardKeys.ContainsKey("Backslash"));
        check(IsFeatureQuery("keyboard",Packet(0x25,13,0,31,0)));check(!IsFeatureQuery("keyboard",Packet(0x25,37,0,0,31,0)));check(!IsFeatureQuery("mouse",Packet(0x23,3,0)));
        var legacyRequest=Packet(0x23,3,0);var legacyReply=new byte[]{2,0x23,3,1,0,60};check(ResponseMatches(7588,2,legacyRequest,legacyReply,legacyReply.Length));check(ResponseMatches(7586,2,legacyRequest,legacyReply,legacyReply.Length));check(!ResponseMatches(7548,2,legacyRequest,legacyReply,legacyReply.Length));check(!ResponseMatches(7588,3,legacyRequest,legacyReply,legacyReply.Length));check(!ResponseMatches(7588,2,legacyRequest,new byte[]{2,0x23,4,1,0,60},6));check(!ResponseMatches(7588,2,legacyRequest,legacyReply,2));check(!ResponseMatches(7588,2,legacyRequest,legacyReply,legacyReply.Length+1));check(ResponseMatches(6862,1,Packet(0xA0,0,0),new byte[]{1,0xA0,0,1,0},5));
        var binding=ParseKeyboardBinding("KeyA",new byte[]{0x25,13,1,0,50,0});check((string)binding["target"]=="KeyB"&&(int)binding["targetCode"]==50&&(int)binding["sourceCode"]==31);check((string)ParseKeyboardBinding("KeyA",new byte[]{0x25,13,0,0,255,0})["target"]=="Default");check(ParseKeyboardBinding("KeyA",new byte[]{0x25,13,0,0,231,3})["target"]==null);check(!IsFeatureQuery("keyboard",Packet(0x25,160,0,31,0)));
        check(DeviceErrorResponse(2,new byte[]{2,255,170,0,2,3},6));check(!DeviceErrorResponse(3,new byte[]{2,255,170,0,2,3},6));check(!DeviceErrorResponse(2,new byte[]{2,255,170},2));check(!DeviceErrorResponse(2,new byte[]{2,0x23,3,0,0,50},6));
        check(KeyboardPollingRates(7588).SequenceEqual(new[]{1000,8000}));check(KeyboardPollingRates(7586).SequenceEqual(new[]{1000,8000}));check(KeyboardPollingRates(7495).Length==0);
        foreach(int model in new[]{7586,7588})foreach(int rate in new[]{2000,4000}){bool rejected=false;try{BuildPeripheralSetting("keyboard","pollingRate",rate,Obj("modelPid",model));}catch(ArgumentException){rejected=true;}check(rejected);}
        check(BuildPeripheralSetting("keyboard","pollingRate",1000,Obj("modelPid",7588)).SequenceEqual(new byte[]{81,49,0,0,0,0}));check(BuildPeripheralSetting("keyboard","pollingRate",8000,Obj("modelPid",7586)).SequenceEqual(new byte[]{81,49,0,0,3,0}));bool unknownModel=false;try{BuildPeripheralSetting("keyboard","pollingRate",8000,Obj());}catch(ArgumentException){unknownModel=true;}check(unknownModel);check(((int[])((Dictionary<string,object>)FeatureOptions("keyboard",7588))["pollingRates"]).SequenceEqual(new[]{1000,8000}));
        var settingPacket=Packet(0x51,49,15,1,2,1,2,3,4,5);int outputCalls=0,inputCalls=0;var replies=new Queue<byte[]>(new[]{new byte[]{3,0x12,0,2,0,1},new byte[]{3,0x51,49,14,0,1},new byte[]{3,0x51,49,15,0,1}});var receipt=SendSettingAndAwaitResponse(7548,3,settingPacket,64,64,(write,buffer,budget)=>{if(write){outputCalls++;check(buffer[0]==3&&buffer.Skip(1).Take(settingPacket.Length).SequenceEqual(settingPacket));return buffer.Length;}inputCalls++;check(budget<=900&&budget>GearBoundedIo.CancelReserveMs);var reply=replies.Dequeue();Array.Copy(reply,buffer,reply.Length);return reply.Length;});check(outputCalls==1&&inputCalls==3&&receipt.OutputCompleted&&receipt.Acknowledged&&!receipt.Rejected);
        outputCalls=0;receipt=SendSettingAndAwaitResponse(7548,3,settingPacket,64,64,(write,buffer,budget)=>{if(write){outputCalls++;return buffer.Length;}throw new TimeoutException("fake timeout");});check(outputCalls==1&&receipt.OutputCompleted&&!receipt.Acknowledged&&receipt.Warning!=null);
        outputCalls=0;receipt=SendSettingAndAwaitResponse(7548,3,settingPacket,64,64,(write,buffer,budget)=>{if(write){outputCalls++;return buffer.Length;}Array.Copy(new byte[]{3,255,170},buffer,3);return 3;});check(outputCalls==1&&receipt.OutputCompleted&&!receipt.Acknowledged&&receipt.Rejected);
        inputCalls=0;receipt=SendSettingAndAwaitResponse(7548,3,settingPacket,64,64,(write,buffer,budget)=>{if(write)return buffer.Length-1;inputCalls++;return 0;});check(!receipt.OutputCompleted&&!receipt.Acknowledged&&inputCalls==0&&receipt.Warning!=null);
        receipt=SendSettingAndAwaitResponse(7588,2,Packet(0x68,0,0,50),64,64,(write,buffer,budget)=>{if(write)return buffer.Length;Array.Copy(new byte[]{2,0x68,0,1,0,50},buffer,6);return 6;});check(receipt.OutputCompleted&&receipt.Acknowledged);
        receipt=SendSettingAndAwaitResponse(7548,3,settingPacket,64,64,(write,buffer,budget)=>{if(write)return buffer.Length;return 0;});check(receipt.OutputCompleted&&!receipt.Acknowledged&&receipt.Warning!=null);
        check(ResponseMatches(7548,3,Packet(0x12,4,2),new byte[]{3,0x12,4,2,0,255,170},7)&&!DeviceErrorResponse(3,new byte[]{3,0x12,4,2,0,255,170},7));
        check(IsFeatureWrite("keyboard",BuildFeatureSetting("keyboard","oledBrightness",50,before)));check(!IsFeatureWrite("headset",BuildFeatureSetting("keyboard","oledBrightness",50,before)));check(!IsFeatureWrite("keyboard",Packet(0x62,0,0,1)));
        check(VersionNative4(new byte[]{16,0,9,0},0)=="9.00.10");check(VersionNative4(new byte[]{9,0,1,0},0)=="1.00.09");check(VersionNative4(new byte[]{9,0,1,1},0)=="101.00.09");
        var origin=new Uri("https://gearlink.asus.com/firmware-update-tools/7489/windows/manifest.json");check(FirmwareCatalog.ToolUri(origin,7489,"./Firmware Tool.zip").Host=="gearlink.asus.com");
        foreach(string path in new string[]{"https://example.com/tool.zip","./../../7489/tool.zip","./tool.exe","./\\example.com\\tool.zip"}){blocked=false;try{FirmwareCatalog.ToolUri(origin,7489,path);}catch(IOException){blocked=true;}check(blocked);}
        Console.WriteLine(Json.Serialize(Obj("ok",true,"hardwareAccess",false,"suite","extended_features","tests",tests)));
    }
}
