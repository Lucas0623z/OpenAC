using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Globalization;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

// Owned adapter. Installed vendor DLLs are dependencies, never bundled or modified.
// One process per device kind keeps proprietary native state and crashes isolated.
internal static class SDKWorker
{
    const string Root = @"C:\Program Files (x86)\ASUS\ArmouryDevice";
    static string Kind;
    static StreamWriter Protocol;
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static readonly Dictionary<string,string> Serial = new Dictionary<string,string>();
    static readonly Dictionary<string,string> Names = new Dictionary<string,string>();
    static PostReturn Post;
    static FreeBuffer Free;
    static bool Initialized;
    static int StartStatus;
    // Test-only injection is internal; it is never reachable through JSON requests.
    internal static Func<string,string> TestTransport;
    static readonly Dictionary<string,FanBounds> FanLimits = new Dictionary<string,FanBounds>();
    static readonly HashSet<string> Alive = new HashSet<string>();
    static readonly HashSet<string> ActiveFanPorts = new HashSet<string>();
    static readonly Dictionary<string,SensorCapability> LcdSensors = new Dictionary<string,SensorCapability>();
    static readonly HashSet<int> FanEffects=new HashSet<int>();
    static readonly Dictionary<string,Dictionary<string,object>> FanSources=new Dictionary<string,Dictionary<string,object>>();
    static string FanFirmware;
    sealed class LedCapability {
        public string Area,Effect;public int NativeId,ColorCount;public bool Speed,Direction;
        public LedCapability(string area,string effect,int id,int count,bool speed,bool direction) { Area=area;Effect=effect;NativeId=id;ColorCount=count;Speed=speed;Direction=direction; }
        public Dictionary<string,object> Snapshot() { return Obj("area",Area,"effect",Effect,"nativeEffectId",NativeId,"colorCount",ColorCount,"colorCountRule",ColorCount<0?(Area=="outer"?"twoPerFan":"onePerFan"):"fixed","hasSpeed",Speed,"hasDirection",Direction,"connectedChain",Area=="connected","rangeSource","installedSourceAndHubInfo"); }
    }
    static List<LedCapability> LedCapabilities() {
        var list=new List<LedCapability>();
        foreach(string area in new[]{"center","outer"}) {
            list.Add(new LedCapability(area,"static",3,-1,false,false));list.Add(new LedCapability(area,"breathe",4,4,true,false));list.Add(new LedCapability(area,"rainbow",5,0,true,true));list.Add(new LedCapability(area,"track",10,2,true,true));
        }
        list.Add(new LedCapability("outer","paint",7,4,true,true));list.Add(new LedCapability("outer","oddEven",8,2,true,false));list.Add(new LedCapability("outer","pinball",9,2,true,true));
        string[] names={"storm","river","rainbowRiver","dualPulse","barberPole","spread","oddEven","pinball"};int[] normal={32,34,48,49,51,50,33,38},connected={66,67,64,69,70,71,72,65},colors={2,2,0,4,3,4,2,2};
        for(int n=0;n<names.Length;n++) { bool direction=n!=3&&n!=5&&n!=6;list.Add(new LedCapability("sync",names[n],normal[n],colors[n],true,direction));list.Add(new LedCapability("connected",names[n],connected[n],colors[n],true,direction)); }
        return list;
    }
    static List<object> AvailableLedCapabilities() { var list=new List<object>();foreach(var item in LedCapabilities()) if(FanEffects.Contains(item.NativeId))list.Add(item.Snapshot());return list; }
    static List<object> TemperatureSources() { var list=new List<object>();foreach(var item in FanSources.Values)list.Add(item);return list; }
    static string LedColors(Dictionary<string,object> request,int count) {
        object raw;if(!request.TryGetValue("colors",out raw)||!(raw is IList))throw new WorkerError("INVALID_REQUEST","colors must be an array of #RRGGBB strings");var colors=(IList)raw;
        if(colors.Count!=count)throw new WorkerError("INVALID_REQUEST","This effect requires exactly "+count+" colors");var converted=new List<string>();
        foreach(object entry in colors) {
            string color=entry as string;int numeric;
            if(color==null||color.Length!=7||color[0]!='#'||!Int32.TryParse(color.Substring(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out numeric))throw new WorkerError("INVALID_REQUEST","Invalid RGB color");
            foreach(char digit in color.Substring(1))if(!((digit>='0'&&digit<='9')||(digit>='a'&&digit<='f')||(digit>='A'&&digit<='F')))throw new WorkerError("INVALID_REQUEST","Invalid RGB color");
            converted.Add("0x"+color.Substring(3,2).ToUpperInvariant()+color.Substring(5,2).ToUpperInvariant()+color.Substring(1,2).ToUpperInvariant());
        }
        return String.Join(", ",converted.ToArray());
    }
    sealed class SensorCapability {
        public string Id,NativeId,Name,DefaultName; public int NativeType;
        public Dictionary<string,object> Snapshot() { return Obj("id",Id,"nativeId",NativeId,"name",Name,"defaultName",DefaultName,"nativeType",NativeType,"unit",null,"value",null,"rawNativeValue",null,"nativeValueReadbackConfirmed",false,"valueReadbackConfirmed",false,"placeholder",false); }
    }
    sealed class FanBounds {
        public int RpmMin,RpmMax,DutyMin,DutyMax;
        public Dictionary<string,object> Snapshot() { return Obj("rpmMin",RpmMin,"rpmMax",RpmMax,"percentageMin",DutyMin,"percentageMax",DutyMax,"rangeSource","sdkCapability","rangeReadbackConfirmed",true); }
    }
    sealed class WorkerError : Exception {
        public string Code; public object Details;
        public WorkerError(string code,string message,object details=null):base(message) { Code=code;Details=details; }
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SetDllDirectory(string path);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi, SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] static extern bool SetStdHandle(int id,IntPtr handle);
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    [DllImport("ole32.dll")] static extern int CoInitializeEx(IntPtr reserved,uint apartment);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int StartService(int port);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int PostReturn(IntPtr unused1,IntPtr input,int inputBytes,IntPtr unused2,IntPtr unused3,out IntPtr output,out int outputBytes,out int outputFlag);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int FreeBuffer(ref IntPtr output);
    static Dictionary<string,object> Obj(params object[] pairs) {
        var result=new Dictionary<string,object>();
        for(int i=0;i<pairs.Length;i+=2) result[(string)pairs[i]]=pairs[i+1];
        return result;
    }
    static string Str(Dictionary<string,object> obj,string key,string fallback) { object value; return obj.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):fallback; }
    static string Escape(string value) { return System.Security.SecurityElement.Escape(value)??""; }
    static string Query(string type,string model,string function,string body) {
        string serial="";
        string sn;
        if(Serial.TryGetValue(model,out sn)&&sn.Length>0) serial="<sn>"+Escape(sn)+"</sn>";
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><root><device_type key=\""+type+"\"><device key=\""+Escape(model)+"\">"+serial+"<function key=\""+function+"\">"+body+"</function></device></device_type></root>";
    }
    static string LoadData(string number) { return "<data_number>"+number+"</data_number><file_name>-1</file_name><profile_index>-1</profile_index>"; }
    static void Init() {
        if(Initialized) return;
        string relative;
        switch(Kind) {
            case "mouse": relative=@"MouseSDK\ArmouryMsSDK.dll"; break;
            case "keyboard": relative=@"KeyboardSDK\ArmouryKbSDK.dll"; break;
            case "fan": relative=@"OmapboxSDK\OmapboxSDK.dll"; break;
            case "lcd": relative=@"AIOFanSDK\ArmouryAIOSDK.dll"; break;
            default: throw new Exception("Unsupported kind");
        }
        string path=Path.Combine(Root,"dll",relative);
        string directory=Path.GetDirectoryName(path);
        Directory.SetCurrentDirectory(directory);
        if(!SetDllDirectory(directory)) throw new Exception("SetDllDirectory failed "+Marshal.GetLastWin32Error());
        // The original keyboard adapter uses STA COM. Mouse initialization uses no apartment.
        if(Kind!="mouse") CoInitializeEx(IntPtr.Zero,2);
        IntPtr lib=LoadLibrary(path);
        if(lib==IntPtr.Zero) throw new Exception("LoadLibrary failed "+Marshal.GetLastWin32Error());
        var start=(StartService)Marshal.GetDelegateForFunctionPointer(GetProcAddress(lib,"startService"),typeof(StartService));
        Post=(PostReturn)Marshal.GetDelegateForFunctionPointer(GetProcAddress(lib,"Post_EntrypointReturn"),typeof(PostReturn));
        Free=(FreeBuffer)Marshal.GetDelegateForFunctionPointer(GetProcAddress(lib,"freeBuffer"),typeof(FreeBuffer));
        StartStatus=start(0);
        if(StartStatus!=0) throw new Exception("SDK initialization returned "+StartStatus);
        Initialized=true;
    }
    static string RequestText(string xml) {
        if(TestTransport!=null) return TestTransport(xml);
        if(Kind=="lcd") {
            using(var mutex=new System.Threading.Mutex(false,"Local\\OpenAC.AioSdk")) {
                bool held=false;
                try {
                    try { held=mutex.WaitOne(0); } catch(System.Threading.AbandonedMutexException) { held=true; }
                    if(!held) throw new WorkerError("SDK_BUSY","Another OpenAC AIO query is active");
                    return RequestTextNative(xml);
                } finally { if(held)mutex.ReleaseMutex(); }
            }
        }
        return RequestTextNative(xml);
    }
    static string RequestTextNative(string xml) {
        Init();
        IntPtr input=Marshal.StringToHGlobalUni(xml),output=IntPtr.Zero;
        int bytes=0,flag=0;
        try {
            int result=Post(IntPtr.Zero,input,Encoding.Unicode.GetByteCount(xml),IntPtr.Zero,IntPtr.Zero,out output,out bytes,out flag);
            if(result!=0) throw new Exception("SDK returned "+result);
            if(output==IntPtr.Zero||bytes<2||bytes>4*1024*1024||bytes%2!=0) throw new Exception("SDK returned an invalid response buffer");
            return Marshal.PtrToStringUni(output,bytes/2).TrimEnd('\0');
        } finally {
            if(output!=IntPtr.Zero) Free(ref output);
            Marshal.FreeHGlobal(input);
        }
    }
    static XmlDocument ParseXml(string text) {
        var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null };
        var doc=new XmlDocument { XmlResolver=null };
        using(var reader=XmlReader.Create(new StringReader(text),settings)) doc.Load(reader);
        return doc;
    }
    static XmlDocument Request(string xml) { return ParseXml(RequestText(xml)); }
    static string Text(XmlNode node,string path) { XmlNode child=node==null?null:node.SelectSingleNode(path); return child==null?null:child.InnerText; }
    static bool? PowerValue(string text) {
        text=text.Trim();
        if(text=="0") return true;
        if(text=="1") return false;
        throw new Exception("SDK returned an unknown power state");
    }
    static Dictionary<string,object> Flat(XmlNode node) {
        var data=new Dictionary<string,object>();
        if(node!=null) foreach(XmlNode child in node.ChildNodes) if(child.NodeType==XmlNodeType.Element) data[child.Name]=child.InnerText;
        return data;
    }
    static XmlNode Data(XmlDocument doc) {
        XmlNode node=doc.SelectSingleNode("/root/device_type/device/data_name");
        if(node==null) node=doc.SelectSingleNode("/root/device_type/device/data_number");
        if(node==null) throw new Exception("Device data is absent in SDK response");
        return node;
    }
    static object Capabilities(string id) {
        try {
            string path=Path.Combine(Root,"View",id,@"resources\src\_ref\caps.json");
            var caps=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
            return Obj("device",Str(caps,"device",""),"performance",caps.ContainsKey("performance")?caps["performance"]:null,"power",caps.ContainsKey("power")?caps["power"]:null,"profileCount",caps.ContainsKey("profileCount")?caps["profileCount"]:null);
        } catch { return null; }
    }
    static int StrictInt(Dictionary<string,object> request,string key,int min,int max) {
        object raw;
        if(!request.TryGetValue(key,out raw)||!(raw is int || raw is long)) throw new WorkerError("INVALID_REQUEST",key+" must be a JSON integer");
        long value=Convert.ToInt64(raw,CultureInfo.InvariantCulture);
        if(value<min||value>max) throw new WorkerError("INVALID_REQUEST",key+" must be between "+min+" and "+max);
        return (int)value;
    }
    static bool StrictBool(Dictionary<string,object> request,string key) {
        object raw;
        if(!request.TryGetValue(key,out raw)||!(raw is bool)) throw new WorkerError("INVALID_REQUEST",key+" must be a JSON boolean");
        return (bool)raw;
    }
    static string StrictString(Dictionary<string,object> request,string key) {
        object raw;
        if(!request.TryGetValue(key,out raw)||!(raw is string)) throw new WorkerError("INVALID_REQUEST",key+" must be a JSON string");
        return (string)raw;
    }
    static void Fields(Dictionary<string,object> request,params string[] extra) {
        var allowed=new HashSet<string>(new[]{"id","op","kind","deviceId","setting"});
        foreach(string key in extra) allowed.Add(key);
        foreach(string key in request.Keys) if(!allowed.Contains(key)) throw new WorkerError("INVALID_REQUEST","Unknown field "+key);
    }
    static int NativeInt(XmlNode node,string path,int min,int max) {
        int value; string raw=Text(node,path);
        if(!int.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<min||value>max) throw new Exception("Invalid fan capability "+path);
        return value;
    }
    static FanBounds ReadFanBounds(string id,string port) {
        FanLimits.Remove(port);
        // The vendor's getFanTuningResult additionally sends 101/activate=0.
        // This adapter only sends the pure capability query 3, never calibration.
        var doc=Request(Query("32",id,"3","<port key=\""+port+"\"/>"));
        if(Text(doc,"/root/data/function/result")!="0") throw new Exception("Fan capability query did not acknowledge success");
        var node=doc.SelectSingleNode("/root/data/function/port[@key='"+port+"']");
        if(node==null) throw new Exception("Fan capability does not identify the requested port");
        var limits=new FanBounds { RpmMin=NativeInt(node,"rpm_min",0,100000),RpmMax=NativeInt(node,"rpm_max",0,100000),DutyMin=NativeInt(node,"duty_min",0,100),DutyMax=NativeInt(node,"duty_max",0,100) };
        if(limits.RpmMax<=limits.RpmMin||limits.DutyMax<limits.DutyMin) throw new Exception("Port has no valid calibrated fan range");
        FanLimits[port]=limits; return limits;
    }
    static bool KnownLcd(string id) { return id=="7124"||id=="7299"||id=="7571"; }
    static string LcdName(string id) { return id=="7124"?"ROG MAXIMUS Z890 EXTREME LCD":id=="7571"?"ROG CROSSHAIR X870E EXTREME GLACIAL LCD":"ROG CROSSHAIR X870E EXTREME LCD"; }
    static object LcdPresets(string id="7299") {
        var list=new List<object>();
        for(int n=0;n<8;n++) {
            string file=id=="7124"?(n<2?"ex_5_launch"+(n+1)+".gif":"ex_5_rog"+(n-1)+".png"):id=="7571"?(n<2?"x870e_glacial_launch_"+(n+1)+".gif":"x870e_glacial_img_"+(n-1)+".png"):(n<2?"x870_launch"+(n+1)+".gif":"x870_"+(n-1).ToString("00",CultureInfo.InvariantCulture)+".png");
            list.Add(Obj("presetId",n,"fileName",file,"preloadFileType",n<2?"gif":"jpg","type",n<2?2:0,"source",1,"index",n<2?n:n-2,"visualIdentityConfirmed",false));
        }
        return list;
    }
    static List<object> SensorSnapshots() {
        var list=new List<object>();foreach(var sensor in LcdSensors.Values) list.Add(sensor.Snapshot());return list;
    }
    static List<SensorCapability> SelectedSensors(Dictionary<string,object> request,int maximum) {
        object raw;if(!request.TryGetValue("sensorIds",out raw)||!(raw is IList)) throw new WorkerError("INVALID_REQUEST","sensorIds must be an array");
        var list=(IList)raw;if(list.Count<1||list.Count>maximum) throw new WorkerError("INVALID_REQUEST","Choose 1 to "+maximum+" sensors for this background");
        var selected=new List<SensorCapability>();var seen=new HashSet<string>();
        foreach(object item in list) {
            var key=item as string;SensorCapability sensor;
            if(key==null||!LcdSensors.TryGetValue(key,out sensor)||!seen.Add(key)) throw new WorkerError("INVALID_REQUEST","sensorIds must uniquely identify sensors from this worker's capability scan");
            selected.Add(sensor);
        }
        return selected;
    }
    static object ReadSensors(Dictionary<string,object> request) {
        if(Kind!="lcd") throw new WorkerError("INVALID_REQUEST","sensors is available only in the LCD worker");
        Fields(request,"sensorIds");string id=StrictString(request,"deviceId");if(!KnownLcd(id)) throw new WorkerError("INVALID_REQUEST","Unsupported LCD device");
        var selected=SelectedSensors(request,5);
        if(!Serial.ContainsKey(id)) throw new WorkerError("DEVICE_NOT_SCANNED","Scan before querying sensors");
        if(!Alive.Contains(id)) throw new WorkerError("READBACK_REQUIRED","Read and confirm the LCD device before querying sensors");
        var body=new StringBuilder("<settings><get_sensor_data>");
        for(int n=0;n<selected.Count;n++) { var sensor=selected[n];body.Append("<sensor key=\"").Append(n).Append("\"><id>").Append(Escape(sensor.NativeId)).Append("</id><default_name>").Append(Escape(sensor.DefaultName)).Append("</default_name><type>").Append(sensor.NativeType).Append("</type></sensor>"); }
        body.Append("</get_sensor_data></settings>");var doc=Request(Query("5",id,"36",body.ToString()));
        var values=new List<object>();
        foreach(var sensor in selected) {
            var snapshot=sensor.Snapshot();XmlNode match=null;bool ambiguous=false;
            foreach(XmlNode node in doc.SelectNodes("/root/sensor | /sensors/sensor")) {
                if(Text(node,"id")!=sensor.NativeId) continue;
                string returnedName=Text(node,"default_name"),returnedType=Text(node,"type");
                if(returnedName!=null&&returnedName!=sensor.DefaultName||returnedType!=null&&returnedType!=Convert.ToString(sensor.NativeType,CultureInfo.InvariantCulture)) continue;
                if(returnedName==null) foreach(var cap in LcdSensors.Values) if(cap.Id!=sensor.Id&&cap.NativeId==sensor.NativeId&&(returnedType==null||cap.NativeType==sensor.NativeType)) ambiguous=true;
                if(match!=null) ambiguous=true;match=node;
            }
            double value;string raw=Text(match,"value");
            if(!ambiguous&&double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!double.IsNaN(value)&&!double.IsInfinity(value)) {
                snapshot["rawNativeValue"]=value;snapshot["nativeValueReadbackConfirmed"]=true;
                if(value==0&&(sensor.NativeType==1||sensor.NativeType==4)) { snapshot["placeholder"]=true;snapshot["error"]="Zero temperature or frequency is unconfirmed; the local SDK returned all-zero samples"; }
                else { snapshot["value"]=value;snapshot["valueReadbackConfirmed"]=true; }
            }
            else snapshot["error"]=ambiguous?"Native sensor identity is ambiguous":"No finite value was returned for the requested capability";
            values.Add(snapshot);
        }
        return Obj("deviceId",id,"sensors",values,"source","sdkSensorQuery36");
    }
    static int Duration(Dictionary<string,object> request) {
        int value=request.ContainsKey("durationSeconds")?StrictInt(request,"durationSeconds",5,60):5;
        if(value!=5&&value!=10&&value!=20&&value!=30&&value!=60) throw new WorkerError("INVALID_REQUEST","durationSeconds must be 5, 10, 20, 30 or 60");
        return value;
    }
    static object MediaStatus(Dictionary<string,object> request) {
        if(Kind!="lcd") throw new WorkerError("INVALID_REQUEST","media is only available for the LCD worker");
        foreach(string key in request.Keys) if(key!="id"&&key!="op"&&key!="kind"&&key!="deviceId") throw new WorkerError("INVALID_REQUEST","Unknown field "+key);
        string id=StrictString(request,"deviceId");if(!KnownLcd(id)) throw new WorkerError("INVALID_REQUEST","Unsupported LCD device");
        if(!Serial.ContainsKey(id)) throw new WorkerError("DEVICE_NOT_SCANNED","Scan before querying media status");
        if(!Alive.Contains(id)) throw new WorkerError("READBACK_REQUIRED","Read and confirm the LCD device before querying media status");
        long free;string raw=RequestText(Query("5",id,"26","")).Trim().Trim('\0');
        if(!long.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out free)||free<0||free>Int32.MaxValue) throw new WorkerError("SDK_QUERY_FAILED","LCD free-space query did not return a valid KiB value");
        var profile=Request(Query("5",id,"1",""));
        var setting=profile.SelectSingleNode("/profiles/profile4/main/single/player[@key='multi_media_player']/setting");
        var groups=new List<object>();if(setting!=null) foreach(XmlNode group in setting.SelectNodes("group")) groups.Add(Flat(group));
        return Obj("deviceId",id,"freeSpaceKiB",free,"freeSpaceReadbackConfirmed",true,"spaceUnitSource","installedSdkSizeConversion","slotMin",0,"slotMax",31,"slotOccupancyReadable",false,"slots",null,"currentMediaGroups",groups,"profileReadbackConfirmed",setting!=null,"visualIdentityConfirmed",false,"userUploadFormats",new[]{"jpg"},"animationUploadSupported",false,"animationUploadReason","Current motherboard LCD source allows custom JPEG only; generic AIO GIF/video conversion is not this model's upload capability");
    }
    static string MediaBody(int type,int source,int index,int duration,string extra) {
        return "<profiles><profile4><main><single><player key=\"multi_media_player\"><setting><play_device>AIO2_PANEL</play_device><display_time>"+(duration*1000)+"</display_time><group key=\"0\"><type>"+type+"</type><source>"+source+"</source><index_format>"+index+"</index_format>"+extra+"</group></setting></player></single></main></profile4></profiles>";
    }
    static void VerifyMedia(Dictionary<string,object> result,string id,int type,int source,int index,int duration,string extra) {
        result["visualIdentityConfirmed"]=false;result["readbackScope"]="configuredProfile";
        try {
            var profile=Request(Query("5",id,"1",""));
            var setting=profile.SelectSingleNode("/profiles/profile4/main/single/player[@key='multi_media_player']/setting");
            var group=setting==null?null:setting.SelectSingleNode("group[@key='0']");
            bool match=group!=null&&setting.SelectNodes("group").Count==1&&Text(setting,"play_device")=="AIO2_PANEL"&&Text(setting,"display_time")==Convert.ToString(duration*1000,CultureInfo.InvariantCulture)&&Text(group,"type")==Convert.ToString(type,CultureInfo.InvariantCulture)&&Text(group,"source")==Convert.ToString(source,CultureInfo.InvariantCulture)&&Text(group,"index_format")==Convert.ToString(index,CultureInfo.InvariantCulture);
            if(match&&extra.Length>0) {
                var expected=ParseXml("<group>"+extra+"</group>");
                foreach(XmlNode expectedChild in expected.DocumentElement.ChildNodes) {
                    var actual=group.SelectSingleNode(expectedChild.Name);
                    if(actual==null||actual.OuterXml!=expectedChild.OuterXml) { match=false;break; }
                }
            }
            result["profileReadbackConfirmed"]=match;result["readbackConfirmed"]=match;
            if(!match) result["verificationWarning"]="Configured profile metadata did not match; displayed pixels have not been verified";
        } catch(Exception ex) { result["profileReadbackConfirmed"]=false;result["verificationWarning"]="Profile readback failed: "+ex.Message; }
    }
    static string LocalJpegPath(string input) {
        if(input.Length<4||input.Length>1024||!char.IsLetter(input[0])||input[1]!=':'||input[2]!='\\'||input.IndexOf(':',2)>=0||input.IndexOfAny(Path.GetInvalidPathChars())>=0) throw new WorkerError("INVALID_REQUEST","filePath must be an absolute local drive path");
        string full=Path.GetFullPath(input),extension=Path.GetExtension(full);
        if(!extension.Equals(".jpg",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".jpeg",StringComparison.OrdinalIgnoreCase)) throw new WorkerError("INVALID_REQUEST","Only local JPEG files are supported");
        // Reject remote drives and junction/symlink traversal; the source is always read-only.
        if(new DriveInfo(Path.GetPathRoot(full)).DriveType==DriveType.Network) throw new WorkerError("INVALID_REQUEST","Remote drives are unsupported");
        return full;
    }
    static byte[] NormalizeJpeg(string path) {
        try {
            for(string current=path;current!=null;current=Path.GetDirectoryName(current)) {
                if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new WorkerError("INVALID_REQUEST","Reparse-point image paths are unsupported");
            }
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(input.Length<4||input.Length>64L*1024*1024) throw new WorkerError("INVALID_REQUEST","JPEG must be nonempty and no larger than 64 MiB");
                if(input.ReadByte()!=255||input.ReadByte()!=216) throw new WorkerError("INVALID_REQUEST","File does not contain a JPEG image");input.Position=0;
                using(var image=Image.FromStream(input,false,true)) {
                    if(image.RawFormat.Guid!=ImageFormat.Jpeg.Guid||image.Width<1||image.Height<1||image.Width>16384||image.Height>16384||(long)image.Width*image.Height>64L*1024*1024) throw new WorkerError("INVALID_REQUEST","Invalid or oversized JPEG dimensions");
                    // Orientation is metadata from the read-only source, applied to its in-memory image.
                    if(Array.IndexOf(image.PropertyIdList,0x112)>=0) {
                        var prop=image.GetPropertyItem(0x112);int orientation=prop.Value.Length>0?prop.Value[0]:1;
                        RotateFlipType[] rotations={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};
                        if(orientation>=1&&orientation<=8) image.RotateFlip(rotations[orientation]);
                    }
                    using(var normalized=new Bitmap(720,1280,PixelFormat.Format24bppRgb)) {
                        using(var graphics=Graphics.FromImage(normalized)) {
                            graphics.Clear(Color.Black);graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                            double scale=Math.Min(720.0/image.Width,1280.0/image.Height);
                            int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
                            graphics.DrawImage(image,new Rectangle((720-width)/2,(1280-height)/2,width,height));
                        }
                        using(var output=new MemoryStream()) { normalized.Save(output,ImageFormat.Jpeg);return output.ToArray(); }
                    }
                }
            }
        } catch(WorkerError) { throw; }
        catch(Exception ex) { throw new WorkerError("INVALID_IMAGE","JPEG validation failed: "+ex.Message); }
    }
    static Dictionary<string,object> UploadImage(string id,string path,int slot,int duration) {
        byte[] jpeg=NormalizeJpeg(path);long free;long requiredKiB=((long)jpeg.Length+1023)/1024;
        string raw=RequestText(Query("5",id,"26","")).Trim();
        if(!long.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out free)||free<=0||free>Int32.MaxValue) throw new WorkerError("SPACE_UNCONFIRMED","SDK free-space query returned an unverified size; no upload was attempted",Obj("writeAttempted",false,"slot",slot));
        // Installed function 25 returns stat.st_size / 1024 for JPEG; the original
        // flow compares that with 26 unchanged. Round upward instead of truncating.
        if(free<requiredKiB) throw new WorkerError("INSUFFICIENT_SPACE","LCD reported insufficient free space; no upload was attempted",Obj("writeAttempted",false,"slot",slot,"freeSpace",free,"freeSpaceUnit","KiB","requiredKiB",requiredKiB,"normalizedBytes",jpeg.Length));
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RogControl","lcd-staging");
        for(string current=directory;current!=null;current=Path.GetDirectoryName(current)) if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new WorkerError("INVALID_REQUEST","Staging directory uses a reparse point");
        Directory.CreateDirectory(directory);
        for(string current=directory;current!=null;current=Path.GetDirectoryName(current)) if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new WorkerError("INVALID_REQUEST","Staging directory uses a reparse point");
        string staged=Path.Combine(directory,Guid.NewGuid().ToString("N")+".jpg");
        Dictionary<string,object> transfer=null;
        try {
            using(var file=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)) file.Write(jpeg,0,jpeg.Length);
            var requested=Obj("filePath",path,"slot",slot,"durationSeconds",duration);
            transfer=WriteResult(id,"uploadImage",requested,Query("5",id,"27","<settings><media_transfer><path>"+Escape(staged)+"</path><type>0</type><index>"+slot+"</index></media_transfer></settings>"));
            transfer["stage"]="transfer";transfer["slot"]=slot;transfer["overwritesExistingSlot"]=true;transfer["slotOccupancyConfirmed"]=false;transfer["normalizedWidth"]=720;transfer["normalizedHeight"]=1280;transfer["normalizedBytes"]=jpeg.Length;transfer["freeSpace"]=free;transfer["freeSpaceUnit"]="KiB";transfer["requiredKiB"]=requiredKiB;transfer["spaceUnitSource"]="installedSdkSizeConversion";transfer["freeSpaceReadbackConfirmed"]=true;transfer["originalModified"]=false;
            transfer["transferHardwareAcknowledged"]=transfer["hardwareAcknowledged"];
            // A return-only acknowledgement cannot prove a completed transfer. Never select it.
            if(!(bool)transfer["hardwareAcknowledged"]) { transfer["presentationAttempted"]=false;return transfer; }
            Dictionary<string,object> presentation;
            try { presentation=WriteResult(id,"uploadImage",requested,Query("5",id,"2",MediaBody(0,0,slot,duration,""))); }
            catch(WorkerError ex) { transfer["stage"]="presentation";transfer["presentationAttempted"]=true;transfer["presentationHardwareAcknowledged"]=false;transfer["presentationError"]=ex.Message;throw new WorkerError(ex.Code,"Image transfer acknowledged but presentation failed: "+ex.Message,transfer); }
            presentation["stage"]="completed";presentation["slot"]=slot;presentation["overwritesExistingSlot"]=true;presentation["slotOccupancyConfirmed"]=false;presentation["transferHardwareAcknowledged"]=true;presentation["presentationAttempted"]=true;presentation["presentationHardwareAcknowledged"]=presentation["hardwareAcknowledged"];presentation["normalizedWidth"]=720;presentation["normalizedHeight"]=1280;presentation["normalizedBytes"]=jpeg.Length;presentation["freeSpace"]=free;presentation["freeSpaceUnit"]="KiB";presentation["requiredKiB"]=requiredKiB;presentation["spaceUnitSource"]="installedSdkSizeConversion";presentation["freeSpaceReadbackConfirmed"]=true;presentation["originalModified"]=false;
            VerifyMedia(presentation,id,0,0,slot,duration,"");return presentation;
        } catch(WorkerError ex) {
            var details=ex.Details as Dictionary<string,object>;if(details!=null) { details["slot"]=slot;details["overwritesExistingSlot"]=true;details["slotOccupancyConfirmed"]=false; }
            throw;
        } finally { try { if(File.Exists(staged)) File.Delete(staged); } catch {} }
    }
    static Dictionary<string,object> WriteResult(string id,string setting,object requested,string xml) {
        string reply;
        try { reply=RequestText(xml).Trim(); }
        catch(Exception ex) { throw new WorkerError("SDK_WRITE_FAILED",ex.Message,Obj("writeAttempted",true,"hardwareAcknowledged",false,"readbackConfirmed",false,"appliedState","unknown")); }
        bool explicitAck=reply=="0";
        if(reply.StartsWith("<",StringComparison.Ordinal)) {
            try {
                var doc=ParseXml(reply); var nodes=doc.SelectNodes("//result");
                explicitAck=nodes.Count>0;
                foreach(XmlNode node in nodes) if(node.InnerText.Trim()!="0") throw new Exception("SDK write result "+node.InnerText.Trim());
                if(doc.SelectSingleNode("//error")!=null||doc.SelectSingleNode("//error_code")!=null) throw new Exception("SDK returned an error response");
            } catch(Exception ex) { throw new WorkerError("SDK_WRITE_FAILED",ex.Message,Obj("writeAttempted",true,"hardwareAcknowledged",false,"readbackConfirmed",false,"appliedState","unknown")); }
        } else if(reply.Length>0 && reply!="0") {
            throw new WorkerError("SDK_WRITE_FAILED","Unrecognized SDK write response",Obj("writeAttempted",true,"hardwareAcknowledged",false,"readbackConfirmed",false,"appliedState","unknown"));
        }
        return Obj("deviceId",id,"setting",setting,"requested",requested,"writeAttempted",true,"sdkAcknowledged",true,"hardwareAcknowledged",explicitAck,"acknowledgementSource",explicitAck?"sdkResponse":"nativeReturnOnly","readbackConfirmed",false,"appliedState",explicitAck?"acknowledged":"unconfirmed");
    }
    static Dictionary<string,object> Set(Dictionary<string,object> request) {
        // All caller-provided fields are validated before Scan/Init/Request/native I/O.
        string id=StrictString(request,"deviceId"),setting=StrictString(request,"setting");
        if(Kind=="fan" && id!="7576" || Kind=="lcd" && !KnownLcd(id)) throw new WorkerError("INVALID_REQUEST","Device ID is not supported by this isolated worker");
        string body=null,function=null,port=null; object requested=null;
        bool power=false; int brightness=0,mediaType=-1,mediaSource=-1,mediaIndex=-1,duration=5,uploadSlot=-1;
        string mediaExtra="",uploadPath=null;
        var nodes=new List<Dictionary<string,object>>();
        if(Kind=="fan") {
            if(setting!="temperatureSources") { port=StrictString(request,"portId");if(port!="1"&&port!="2"&&port!="3"&&port!="4") throw new WorkerError("INVALID_REQUEST","portId must be 1, 2, 3 or 4"); }
            if(setting=="temperatureSources") {
                Fields(request,"sourceIds");object raw;if(!request.TryGetValue("sourceIds",out raw)||!(raw is IList))throw new WorkerError("INVALID_REQUEST","sourceIds must be an array");var sources=(IList)raw;var seen=new HashSet<string>();var xml=new StringBuilder("<source_list>");
                if(sources.Count<1||sources.Count>5)throw new WorkerError("INVALID_REQUEST","Choose one to five available temperature sources");
                foreach(object value in sources) { string source=value as string;Dictionary<string,object> cap;if(source==null||!seen.Add(source)||!FanSources.TryGetValue(source,out cap)||!(bool)cap["available"])throw new WorkerError("INVALID_REQUEST","Every temperature source must be unique and confirmed available in the hub scan");xml.Append("<source key=\"").Append(Escape(source)).Append("\"/>"); }
                body=xml.Append("</source_list>").ToString();function="104";requested=Obj("sourceIds",request["sourceIds"]);
            } else if(setting=="fanCount") {
                Fields(request,"portId","value");int count=StrictInt(request,"value",0,14);body="<port key=\""+port+"\"><fan_count>"+count+"</fan_count></port>";function="202";requested=Obj("portId",port,"fanCount",count);
            } else if(setting=="disconnectLightingChain") {
                Fields(request,"portId");if(port!="1"||!FanEffects.Contains(3))throw new WorkerError("INVALID_REQUEST","Disconnect requires portId 1 and the hub-confirmed static effect");
                function="201";body="<doc>disconnect</doc><port key=\"1\"><group_color_set key=\"1\">#ffffff</group_color_set></port><effect_id>3</effect_id><speed>0</speed><direction>0</direction><brightness>0</brightness>";requested=Obj("portId","1","disconnectConnectedChain",true,"centerColor","#FFFFFF","brightness",100);
            } else if(setting=="lighting") {
                // Validate the complete source-derived request before any count/effect writes.
                Fields(request,"portId","area","effect","fanCount","fanCounts","brightness","speed","direction","colors");string area=StrictString(request,"area"),effect=StrictString(request,"effect");LedCapability cap=null;
                foreach(var item in LedCapabilities())if(item.Area==area&&item.Effect==effect&&FanEffects.Contains(item.NativeId)){cap=item;break;}
                if(cap==null)throw new WorkerError("UNSUPPORTED_SETTING","This effect/area is not confirmed by the installed source and hub effect list");
                int count=1;
                if(area=="connected") {
                    if(port!="1"||request.ContainsKey("fanCount"))throw new WorkerError("INVALID_REQUEST","Connected-chain effects use portId 1 and explicit fanCounts for all four ports");
                    object raw;if(!request.TryGetValue("fanCounts",out raw)||!(raw is IList))throw new WorkerError("INVALID_REQUEST","fanCounts must contain four JSON integers");var counts=(IList)raw;if(counts.Count!=4)throw new WorkerError("INVALID_REQUEST","fanCounts must contain exactly four port counts");int total=0;
                    foreach(object value in counts){var numeric=Obj("count",value);total+=StrictInt(numeric,"count",0,14);}if(total==0)throw new WorkerError("INVALID_REQUEST","Connected-chain effects require at least one configured fan");
                } else { if(request.ContainsKey("fanCounts"))throw new WorkerError("INVALID_REQUEST","fanCounts is only for connected-chain effects");count=StrictInt(request,"fanCount",1,14); }
                int bright=StrictInt(request,"brightness",0,100),speed=StrictInt(request,"speed",20,100);if(bright%25!=0||speed%20!=0)throw new WorkerError("INVALID_REQUEST","brightness must be 0/25/50/75/100; speed must be 20/40/60/80/100");
                string direction=StrictString(request,"direction");if(direction!="forward"&&direction!="backward")throw new WorkerError("INVALID_REQUEST","direction must be forward or backward");
                if(!cap.Speed&&speed!=60||!cap.Direction&&direction!="forward")throw new WorkerError("INVALID_REQUEST","This effect has no adjustable speed/direction; use speed 60 and forward for unavailable controls");
                int number=cap.ColorCount<0?count*(area=="outer"?2:1):cap.ColorCount;string colors=LedColors(request,number);
                int[] brightnessMap={8,3,2,1,0};body="<port key=\""+port+"\"><group_color_set key=\""+(area=="outer"?"2":"1")+"\">"+colors+"</group_color_set></port><effect_id>"+cap.NativeId+"</effect_id><speed>"+(3-speed/20)+"</speed><direction>"+(direction=="forward"?0:1)+"</direction><brightness>"+brightnessMap[bright/25]+"</brightness>";function="201";requested=Obj("portId",port,"area",area,"effect",effect,"brightness",bright,"speed",speed,"direction",direction,"colors",request["colors"],"fanCount",area=="connected"?null:(object)count,"fanCounts",area=="connected"?request["fanCounts"]:null);
            } else if(setting=="fixed") {
                Fields(request,"portId","rpm"); int rpm=StrictInt(request,"rpm",0,100000);
                requested=Obj("portId",port,"mode","fixed","rpm",rpm);
                body="<port key=\""+port+"\"><mode>2</mode><settings><rpm>"+rpm+"</rpm></settings></port>";
            } else if(setting=="smart") {
                Fields(request,"portId","nodes","preset");
                if(request.ContainsKey("nodes")&&request.ContainsKey("preset")) throw new WorkerError("INVALID_REQUEST","Choose nodes or preset, not both");
                if(request.ContainsKey("preset")) {
                    string preset=StrictString(request,"preset"); int[,] curve;
                    switch(preset) {
                        case "silent":curve=new int[,]{{0,0},{60,30},{80,40},{100,70}};break;
                        case "standard":curve=new int[,]{{0,30},{50,30},{70,50},{100,90}};break;
                        case "turbo":curve=new int[,]{{0,40},{40,40},{60,60},{90,100}};break;
                        case "fullSpeed":curve=new int[,]{{0,100},{50,100},{80,100},{100,100}};break;
                        default:throw new WorkerError("INVALID_REQUEST","Unknown fan preset");
                    }
                    for(int n=0;n<4;n++) nodes.Add(Obj("temperature",curve[n,0],"percentage",curve[n,1]));
                } else {
                    object raw; if(!request.TryGetValue("nodes",out raw)||!(raw is IList)) throw new WorkerError("INVALID_REQUEST","nodes must be an array");
                    var array=(IList)raw;
                    if(array.Count!=4) throw new WorkerError("INVALID_REQUEST","A curve must contain exactly four nodes, as in this controller's source presets");
                    foreach(object item in array) {
                        var node=item as Dictionary<string,object>;
                        if(node==null||node.Count!=2||!node.ContainsKey("temperature")||!node.ContainsKey("percentage")) throw new WorkerError("INVALID_REQUEST","Each curve node requires only temperature and percentage");
                        nodes.Add(Obj("temperature",StrictInt(node,"temperature",0,100),"percentage",StrictInt(node,"percentage",0,100)));
                    }
                }
                int lastTemp=-1,lastDuty=-1; var curveXml=new StringBuilder();
                for(int n=0;n<nodes.Count;n++) {
                    int temp=(int)nodes[n]["temperature"],duty=(int)nodes[n]["percentage"];
                    if(temp<=lastTemp||duty<lastDuty||(n==0&&temp!=0)) throw new WorkerError("INVALID_REQUEST","Curve temperatures must strictly increase from 0; percentages must not decrease");
                    lastTemp=temp;lastDuty=duty;
                    curveXml.Append("<item key=\"").Append(n+1).Append("\"><temperature>").Append(temp).Append("</temperature><duty>").Append(duty).Append("</duty></item>");
                }
                requested=Obj("portId",port,"mode","smart","nodes",nodes);
                body="<port key=\""+port+"\"><mode>1</mode><settings><duty_curve><item_count>4</item_count>"+curveXml+"</duty_curve><spin_up_level>2</spin_up_level><spin_down_level>2</spin_down_level></settings></port>";
            } else throw new WorkerError("UNSUPPORTED_SETTING","Supported fan settings are smart, fixed, fanCount, lighting, disconnectLightingChain and temperatureSources; port names are local UI aliases");
            if(setting=="smart"||setting=="fixed") {
            FanBounds limits;
            bool calibrated=FanLimits.TryGetValue(port,out limits);
            if(setting=="fixed") {
                if(!calibrated) throw new WorkerError("CAPABILITY_REQUIRED","Read a valid SDK RPM capability snapshot for this port before fixed-RPM writes");
                int rpm=(int)((Dictionary<string,object>)requested)["rpm"];
                if(rpm<limits.RpmMin||rpm>limits.RpmMax) throw new WorkerError("INVALID_REQUEST","rpm is outside this port's SDK capability range");
            } else {
                if(!calibrated&&!ActiveFanPorts.Contains(port)) throw new WorkerError("READBACK_REQUIRED","Read a positive current RPM for this port before using the source-defined PWM curve range");
                if(calibrated) foreach(var node in nodes) {
                    int duty=(int)node["percentage"];
                    if(duty<limits.DutyMin||duty>limits.DutyMax) throw new WorkerError("INVALID_REQUEST","Curve percentage is outside this port's SDK capability range");
                }
            }
            function="103";
            }
        } else if(Kind=="lcd") {
            switch(setting) {
                case "powerS0": case "powerS5":
                    Fields(request,"value");power=StrictBool(request,"value"); requested=power;function=setting=="powerS0"?"7":"21";
                    body="<settings><power>"+(power?"0":"1")+"</power><saving>1</saving></settings>";break;
                case "brightness":
                    Fields(request,"value");brightness=StrictInt(request,"value",10,100);
                    if(brightness%10!=0) throw new WorkerError("INVALID_REQUEST","brightness must be a multiple of 10");
                    requested=brightness;function="37";break;
                case "standby":
                    Fields(request,"enabled","brightness");power=StrictBool(request,"enabled");brightness=StrictInt(request,"brightness",10,100);
                    if(brightness%10!=0) throw new WorkerError("INVALID_REQUEST","brightness must be a multiple of 10");
                    requested=Obj("enabled",power,"brightness",brightness);function="24";
                    body="<settings><standby><enable>"+(power?"1":"0")+"</enable><brightness>"+(power?brightness:0)+"</brightness></standby></settings>";break;
                case "presetId":
                    Fields(request,"value","durationSeconds");int preset=StrictInt(request,"value",0,7);duration=Duration(request);requested=Obj("presetId",preset,"durationSeconds",duration);function="2";
                    mediaType=preset<2?2:0;mediaSource=1;mediaIndex=preset<2?preset:preset-2;body=MediaBody(mediaType,mediaSource,mediaIndex,duration,"");break;
                case "uploadImage":
                    Fields(request,"filePath","slot","durationSeconds");uploadSlot=StrictInt(request,"slot",0,31);duration=Duration(request);uploadPath=LocalJpegPath(StrictString(request,"filePath"));break;
                case "selectMedia":
                    Fields(request,"slot","durationSeconds");mediaIndex=StrictInt(request,"slot",0,31);duration=Duration(request);mediaType=0;mediaSource=0;function="2";
                    requested=Obj("slot",mediaIndex,"mediaFormat","jpg","durationSeconds",duration);body=MediaBody(mediaType,mediaSource,mediaIndex,duration,"");break;
                case "deleteMedia":
                    Fields(request,"slot","confirmSlotDeletion");int deleteSlot=StrictInt(request,"slot",0,31);
                    if(!StrictBool(request,"confirmSlotDeletion")) throw new WorkerError("INVALID_REQUEST","Deleting this exact user JPEG slot requires confirmSlotDeletion:true");
                    function="29";requested=Obj("slot",deleteSlot,"mediaFormat","jpg","confirmSlotDeletion",true);body="<settings><media_delete><type>0</type><index>"+deleteSlot+"</index></media_delete></settings>";break;
                case "hardwareMonitor":
                    Fields(request,"backgroundId","sensorIds","durationSeconds");int background=StrictInt(request,"backgroundId",8,11);duration=Duration(request);
                    var sensors=SelectedSensors(request,background==11?5:3);var sensorXml=new StringBuilder();
                    for(int n=0;n<sensors.Count;n++) { var sensor=sensors[n];sensorXml.Append("<sensor key=\"").Append(n).Append("\"><id>").Append(Escape(sensor.NativeId)).Append("</id><name>").Append(Escape(sensor.Name)).Append("</name><default_name>").Append(Escape(sensor.DefaultName)).Append("</default_name><type>").Append(sensor.NativeType).Append("</type></sensor>"); }
                    mediaType=5;mediaSource=1;mediaIndex=background-8;function="2";mediaExtra="<bg_type>1</bg_type><text_settings><title><font>1</font><color><red>255</red><green>0</green><blue>80</blue></color></title><value><font>0</font><color><red>255</red><green>255</green><blue>255</blue></color></value></text_settings><sensor_list>"+sensorXml+"</sensor_list>";
                    body=MediaBody(mediaType,mediaSource,mediaIndex,duration,mediaExtra);requested=Obj("backgroundId",background,"sensorIds",request["sensorIds"],"durationSeconds",duration);break;
                default:throw new WorkerError("UNSUPPORTED_SETTING","Supported LCD settings are powerS0, powerS5, brightness, standby, presetId, uploadImage, selectMedia, deleteMedia and hardwareMonitor");
            }
        } else throw new WorkerError("UNSUPPORTED_SETTING","Peripheral writes remain unsupported");
        if(!Serial.ContainsKey(id)) throw new WorkerError("DEVICE_NOT_SCANNED","Scan and confirm the device in this worker before writing");
        if(!Alive.Contains(id)) throw new WorkerError("READBACK_REQUIRED","Read and confirm this device in this worker before writing");
        if(uploadPath!=null) return UploadImage(id,uploadPath,uploadSlot,duration);
        Dictionary<string,object> countResult=null;
        if(Kind=="fan"&&setting=="lighting") {
            string area=(string)((Dictionary<string,object>)requested)["area"];
            if(area=="connected") {
                var counts=(IList)request["fanCounts"];var acknowledged=new List<object>();
                for(int n=0;n<4;n++) {
                    try { countResult=WriteResult(id,"fanCount",Obj("portId",Convert.ToString(n+1),"fanCount",counts[n]),Query("32",id,"202","<port key=\""+(n+1)+"\"><fan_count>"+counts[n]+"</fan_count></port>")); }
                    catch(WorkerError ex){throw new WorkerError(ex.Code,"Connected fan-count preparation failed; no lighting effect was attempted: "+ex.Message,Obj("writeAttempted",true,"countResults",acknowledged,"effectAttempted",false,"readbackConfirmed",false,"appliedState","unknown","affectsAllPorts",true));}
                    acknowledged.Add(countResult);if(!(bool)countResult["hardwareAcknowledged"])return Obj("deviceId",id,"setting",setting,"requested",requested,"writeAttempted",true,"hardwareAcknowledged",false,"countResults",acknowledged,"effectAttempted",false,"readbackConfirmed",false,"appliedState","unconfirmed","affectsAllPorts",true);
                }
                countResult=Obj("countResults",acknowledged,"hardwareAcknowledged",true);
            } else {
                int count=(int)((Dictionary<string,object>)requested)["fanCount"];
                countResult=WriteResult(id,"fanCount",Obj("portId",port,"fanCount",count),Query("32",id,"202","<port key=\""+port+"\"><fan_count>"+count+"</fan_count></port>"));
                if(!(bool)countResult["hardwareAcknowledged"]){countResult["effectAttempted"]=false;return countResult;}
            }
        }
        string getter=null; bool? previousPower=null;
        if(Kind=="lcd" && (setting=="powerS0"||setting=="powerS5")) {
            getter=setting=="powerS0"?"6":"20";
            previousPower=PowerValue(RequestText(Query("5",id,getter,"")));
            if(previousPower==power) return Obj("deviceId",id,"setting",setting,"requested",power,"value",power,"previousValue",previousPower,"writeAttempted",false,"sdkAcknowledged",false,"hardwareAcknowledged",false,"readbackConfirmed",true,"appliedState","alreadySet");
        } else if(Kind=="lcd" && setting=="brightness") {
            bool onS0=PowerValue(RequestText(Query("5",id,"6",""))).Value;
            bool onS5=PowerValue(RequestText(Query("5",id,"20",""))).Value;
            if(!onS0) throw new WorkerError("POWER_REQUIRED","Enable LCD powerS0 before adjusting brightness");
            // Installed motherboard mapping uses 1 for a powered-off S5 state.
            body="<settings><brightness><s0>"+brightness+"</s0><s5>"+(onS5?brightness:1)+"</s5></brightness></settings>";
        } else if(Kind=="lcd" && setting=="standby" && power && PowerValue(RequestText(Query("5",id,"6","")))!=true) {
            throw new WorkerError("POWER_REQUIRED","Enable LCD powerS0 before enabling standby");
        }
        Dictionary<string,object> result;
        try { result=WriteResult(id,setting,requested,Query(Kind=="fan"?"32":"5",id,function,body)); }
        catch(WorkerError ex) { if(countResult!=null){var details=ex.Details as Dictionary<string,object>;if(details!=null){details["countAcknowledgement"]=countResult;details["effectAttempted"]=true;details["affectsAllPorts"]=(string)((Dictionary<string,object>)requested)["area"]=="connected";}}throw; }
        if(Kind=="fan") {
            if(setting=="smart"||setting=="fixed") { result["rangeSource"]=FanLimits.ContainsKey(port)?"sdkCapability":"installedSource";result["rangeReadbackConfirmed"]=FanLimits.ContainsKey(port);result["controlUnit"]=setting=="smart"?"PWMPercent":"RPM"; }
            else { result["rangeSource"]="installedSourceAndHubInfo";result["settingsReadbackConfirmed"]=false; }
            if(countResult!=null){result["countAcknowledgement"]=countResult;result["effectAttempted"]=true;result["affectsAllPorts"]=(string)((Dictionary<string,object>)requested)["area"]=="connected";result["affectedPorts"]=result["affectsAllPorts"].Equals(true)?new[]{"1","2","3","4"}:new[]{port};}
            if(setting=="disconnectLightingChain"){result["affectsAllPorts"]=true;result["affectedPorts"]=new[]{"1","2","3","4"};result["directlyChanges"]=Obj("portId","1","area","center","color","#FFFFFF","brightness",100);}
        }
        if(getter!=null) {
            result["previousValue"]=previousPower;result["value"]=null;
            try {
                bool actual=PowerValue(RequestText(Query("5",id,getter,""))).Value;
                result["value"]=actual;result["readbackConfirmed"]=actual==power;
                result["appliedState"]=actual==power?"verified":"mismatch";
                if(actual!=power) result["verificationWarning"]="Power readback differs from the requested value; no retry or rollback was attempted";
            } catch(Exception ex) { result["verificationWarning"]="Write completed but readback failed: "+ex.Message; }
        } else if(Kind=="lcd" && setting=="deleteMedia") {
            result["slot"]=((Dictionary<string,object>)requested)["slot"];result["mediaFormat"]="jpg";result["deletionReadbackConfirmed"]=false;result["slotOccupancyReadable"]=false;result["appliedState"]=(bool)result["hardwareAcknowledged"]?"deletionAcknowledged":"unconfirmed";
            result["verificationWarning"]="Only the deletion command acknowledgement is available; the device has no proven uploaded-slot inventory getter";
        } else if(Kind=="lcd" && mediaType>=0) {
            VerifyMedia(result,id,mediaType,mediaSource,mediaIndex,duration,mediaExtra);
        }
        return result;
    }
    static Dictionary<string,object> Scan(string requestedLcd="7299") {
        if(Kind=="lcd"&&!KnownLcd(requestedLcd)) throw new WorkerError("INVALID_REQUEST","LCD model is not on the source-confirmed family list");
        Serial.Clear(); Names.Clear(); FanLimits.Clear(); Alive.Clear();ActiveFanPorts.Clear();LcdSensors.Clear();FanEffects.Clear();FanSources.Clear();FanFirmware=null;
        var list=new List<object>();
        if(Kind=="lcd") {
            var doc=Request(Query("5",requestedLcd,"0",""));
            if(doc.SelectSingleNode("/root/capability/device_list[deviceid='"+requestedLcd+"']")!=null) {
                Serial[requestedLcd]=""; Names[requestedLcd]=LcdName(requestedLcd);
                int n=0;
                foreach(XmlNode node in doc.SelectNodes("/root/capability/player[@key='hw_monitor_player']/sensor_list/sensor")) {
                    int type;string nativeId=node.Attributes["key"]==null?null:node.Attributes["key"].Value;string name=Text(node,"name"),defaultName=Text(node,"default_name");
                    long numericId;
                    if(!long.TryParse(nativeId,NumberStyles.None,CultureInfo.InvariantCulture,out numericId)||numericId<0||!int.TryParse(Text(node,"type"),out type)||name==null||defaultName==null) continue;
                    if(nativeId=="65280"||nativeId=="65281") type=5;
                    // Only category types the installed mainboard UI supports are selectable.
                    if(type!=0&&type!=1&&type!=2&&type!=4&&type!=5) continue;
                    string opaque="sensor-"+(n++);LcdSensors[opaque]=new SensorCapability {Id=opaque,NativeId=nativeId,Name=name,DefaultName=defaultName,NativeType=type};
                }
                list.Add(Obj("deviceId",requestedLcd,"name",Names[requestedLcd],"connection","USB","displayWidth",720,"displayHeight",1280,"supportsWrite",true,"supportedSettings",new[]{"powerS0","powerS5","brightness","standby","presetId","uploadImage","selectMedia","deleteMedia","hardwareMonitor"},"writeRequiresReadback",true,"presetAssets",LcdPresets(requestedLcd),"hardwareSensors",SensorSnapshots(),"hardwareMonitorWritable",LcdSensors.Count>0,"family","motherboardLcd51","sourcePrepared",true,"hardwareWriteValidated",false));
            }
        } else if(Kind=="fan") {
            var doc=Request(Query("32","7576","1",""));
            if(Text(doc,"/root/data/hal_found")=="1" && Text(doc,"/root/data/hub_found")=="1") {
                Serial["7576"]=""; Names["7576"]="ROG FAN CONTROLLER PLUS";
                FanFirmware=Text(doc,"/root/data/function/hub_info/firmware_version");
                foreach(XmlNode effect in doc.SelectNodes("/root/data/function/hub_info/effect_list/effect")) { int numeric;if(effect.Attributes["key"]!=null&&Int32.TryParse(effect.Attributes["key"].Value,NumberStyles.None,CultureInfo.InvariantCulture,out numeric))FanEffects.Add(numeric); }
                foreach(XmlNode source in doc.SelectNodes("/root/data/function/hub_info/temperature_source_list/source")) {string key=source.Attributes["key"]==null?null:source.Attributes["key"].Value;if(key!="1"&&key!="2"&&key!="3"&&key!="4"&&key!="5")continue;FanSources[key]=Obj("sourceId",key,"name",source.InnerText,"available",source.Attributes["avail"]!=null&&source.Attributes["avail"].Value=="1");}
                list.Add(Obj("deviceId","7576","name",Names["7576"],"firmware",FanFirmware,"connection","USB","supportsWrite",true,"supportedSettings",new[]{"smart","fixed","fanCount","lighting","disconnectLightingChain","temperatureSources"},"writeRequiresCapability",true,"effectCapabilities",AvailableLedCapabilities(),"temperatureSources",TemperatureSources()));
            }
        } else {
            var doc=Request(Query(Kind=="mouse"?"1":"2","-1",Kind=="mouse"?"10":"9",""));
            foreach(XmlNode node in doc.SelectNodes("/root/count")) {
                string id=Text(node,"pid"),sn=Text(node,"sn")??"",name=Text(node,"display_name")??Text(node,"model_name")??Kind;
                int numericId;
                if(!int.TryParse(id,out numericId)||numericId<1) continue;
                Serial[id]=sn; Names[id]=name;
                list.Add(Obj("deviceId",id,"name",name,"connection",Text(node,"device_mode")=="2"?"2.4GHz":"USB / Bluetooth","capabilities",Capabilities(id),"supportsWrite",false));
            }
        }
        return Obj("devices",list,"startStatus",StartStatus);
    }
    static Dictionary<string,object> Read(string id) {
        if(!Serial.ContainsKey(id)) Scan(Kind=="lcd"?id:"7299");
        if(!Serial.ContainsKey(id)) throw new Exception("Device is absent from SDK scan");
        Alive.Remove(id);
        var data=Obj("deviceId",id,"name",Names[id],"supportsWrite",false);
        var errors=new List<string>();
        if(Kind=="lcd") {
            string firmware=RequestText(Query("5",id,"5","")).Trim();
            bool confirmed=firmware.Length==4 && firmware!="0000";
            foreach(char digit in firmware) if(!((digit>='0'&&digit<='9')||(digit>='a'&&digit<='f')||(digit>='A'&&digit<='F'))) confirmed=false;
            data["firmware"]=confirmed?firmware:null;
            data["displayWidth"]=720; data["displayHeight"]=1280;
            data["readbackConfirmed"]=confirmed;
            data["deviceAlive"]=confirmed; data["placeholder"]=!confirmed;
            if(confirmed) Alive.Add(id);
            data["supportsWrite"]=confirmed;data["supportedSettings"]=new[]{"powerS0","powerS5","brightness","standby","presetId","uploadImage","selectMedia","deleteMedia","hardwareMonitor"};
            data["brightness"]=null; data["brightnessMin"]=10; data["brightnessMax"]=100;data["brightnessStep"]=10;
            data["brightnessReadable"]=false; data["brightnessWritable"]=confirmed;data["brightnessRangeSource"]="installedViewSlider";
            data["standbyEnabled"]=null; data["standbyReadable"]=false;data["standbyWritable"]=confirmed;
            data["presetWritable"]=confirmed;data["presetAssets"]=LcdPresets(id);data["contentPresets"]=LcdPresets(id);data["imageUploadWritable"]=confirmed;data["uploadSlotMin"]=0;data["uploadSlotMax"]=31;data["uploadSlotOccupancyReadable"]=false;
            data["family"]="motherboardLcd51";data["sourcePrepared"]=true;data["hardwareWriteValidated"]=false;
            data["mediaSelectWritable"]=confirmed;data["mediaDeleteWritable"]=confirmed;data["userUploadFormats"]=new[]{"jpg"};data["animationUploadSupported"]=false;data["animationUploadReason"]="The source-confirmed motherboard LCD51 family permits custom JPEG only";
            data["hardwareSensors"]=SensorSnapshots();data["hardwareMonitorWritable"]=confirmed&&LcdSensors.Count>0;data["hardwareMonitorBackgrounds"]=new[]{Obj("backgroundId",8,"index",0,"maxSensors",3),Obj("backgroundId",9,"index",1,"maxSensors",3),Obj("backgroundId",10,"index",2,"maxSensors",3),Obj("backgroundId",11,"index",3,"maxSensors",5)};
            data["powerS0"]=null; data["powerS5"]=null;
            // GET_PROFILE is the only verified profile getter. Its current response lacks
            // brightness/standby fields, so no stored UI defaults become hardware values.
            try {
                var profile=Request(Query("5",id,"1",""));
                data["profileReadbackConfirmed"]=profile.SelectSingleNode("/profiles")!=null;
                var group=profile.SelectSingleNode("/profiles/profile4/main/single/player[@key='multi_media_player']/setting/group[@key='0']");
                data["currentMediaProfile"]=group==null?null:Flat(group);
                data["mediaVisualIdentityConfirmed"]=false;
            } catch(Exception ex) { data["profileReadbackConfirmed"]=false; errors.Add("profile: "+ex.Message); }
            try { data["powerS0"]=PowerValue(RequestText(Query("5",id,"6",""))); }
            catch(Exception ex) { errors.Add("powerS0: "+ex.Message); }
            try { data["powerS5"]=PowerValue(RequestText(Query("5",id,"20",""))); }
            catch(Exception ex) { errors.Add("powerS5: "+ex.Message); }
        } else if(Kind=="mouse"||Kind=="keyboard") {
            string type=Kind=="mouse"?"1":"2",function=Kind=="mouse"?"6":"10";
            string[] numbers=Kind=="mouse"?new[]{"0","2","5"}:new[]{"1","16"};
            string[] labels=Kind=="mouse"?new[]{"deviceInfo","performance","power"}:new[]{"deviceInfo","performance"};
            for(int i=0;i<numbers.Length;i++) {
                try { data[labels[i]]=Flat(Data(Request(Query(type,id,function,LoadData(numbers[i]))))); }
                catch(Exception ex) { errors.Add(labels[i]+": "+ex.Message); }
            }
            // Fields stay nullable when hardware did not confirm the value.
            object rawInfo;
            var info=data.TryGetValue("deviceInfo",out rawInfo)?rawInfo as Dictionary<string,object>:null;
            string connection=info==null?"":Str(info,"connection_status","");
            string version=info==null?"":Str(info,"version","");
            bool alive=connection.Length>0 && connection!="255" && version.Length>0 && version!="0.00.00";
            data["readbackConfirmed"]=errors.Count==0 && alive;
            data["deviceAlive"]=alive;
            data["placeholder"]=!alive;
            if(!alive) {
                foreach(string label in labels) data[label]=null;
                errors.Add("Device readback is unconfirmed; placeholder firmware, power and performance values were suppressed");
            }
        } else {
            data["firmware"]=FanFirmware;data["firmwareSource"]="lastHubScan";data["firmwareReadbackConfirmed"]=!String.IsNullOrEmpty(FanFirmware);data["effectCapabilities"]=AvailableLedCapabilities();data["temperatureSources"]=TemperatureSources();data["currentLighting"]=null;data["currentTemperatureSources"]=null;data["fanCounts"]=null;data["lightingSettingsReadbackConfirmed"]=false;data["fanCountMin"]=0;data["fanCountMax"]=14;data["brightnessOptions"]=new[]{0,25,50,75,100};data["speedOptions"]=new[]{20,40,60,80,100};data["directionOptions"]=new[]{"forward","backward"};
            var ports=new List<object>();
            bool anyConfirmed=false;
            ActiveFanPorts.Clear();
            for(int port=1;port<=4;port++) {
                Dictionary<string,object> snapshot=Obj("portId",Convert.ToString(port),"rpm",null,"confirmed",false,"mode",null,"targetRpm",null,"nodes",null,"settingsReadbackConfirmed",false,"supportsWrite",false,"supportsSmartCurve",false,"supportsFixedRpm",false,"smartRange",null,"capability",null);
                try {
                    var doc=Request(Query("32",id,"4","<port key=\""+port+"\"/>"));
                    string result=Text(doc,"/root/data/function/result");
                    if(result!="0") throw new Exception("Fan RPM query result "+result);
                    string rpm=Text(doc,"/root/data/function/port[@key='"+port+"']/rpm");
                    int numericRpm;
                    if(!int.TryParse(rpm,NumberStyles.None,CultureInfo.InvariantCulture,out numericRpm)||numericRpm<0||numericRpm>100000) throw new Exception("Invalid fan RPM response");
                    snapshot["rpm"]=rpm;snapshot["confirmed"]=rpm!=null;
                    if(numericRpm>0) ActiveFanPorts.Add(Convert.ToString(port));
                    anyConfirmed=anyConfirmed||rpm!=null;
                } catch(Exception ex) { snapshot["error"]=ex.Message; }
                try { var limits=ReadFanBounds(id,Convert.ToString(port));snapshot["capability"]=limits.Snapshot();snapshot["supportsFixedRpm"]=true;snapshot["supportsSmartCurve"]=true;snapshot["smartRange"]=Obj("temperatureMin",0,"temperatureMax",100,"percentageMin",limits.DutyMin,"percentageMax",limits.DutyMax,"rangeSource","sdkCapability","rangeReadbackConfirmed",true); }
                catch(Exception ex) { snapshot["capabilityError"]=ex.Message; }
                if(!(bool)snapshot["supportsSmartCurve"]&&ActiveFanPorts.Contains(Convert.ToString(port))) {
                    snapshot["supportsSmartCurve"]=true;
                    snapshot["smartRange"]=Obj("temperatureMin",0,"temperatureMax",100,"percentageMin",0,"percentageMax",100,"rangeSource","installedSource","rangeReadbackConfirmed",false);
                }
                snapshot["supportsWrite"]=(bool)snapshot["supportsSmartCurve"]||(bool)snapshot["supportsFixedRpm"];
                ports.Add(snapshot);
            }
            data["ports"]=ports;
            data["readbackConfirmed"]=anyConfirmed; data["deviceAlive"]=anyConfirmed; data["placeholder"]=!anyConfirmed;
            if(anyConfirmed) Alive.Add(id);
            data["supportsLighting"]=anyConfirmed&&AvailableLedCapabilities().Count>0;data["supportsDisconnectLightingChain"]=anyConfirmed&&FanEffects.Contains(3);data["supportsFanCount"]=anyConfirmed;bool sourcesAvailable=false;foreach(var source in FanSources.Values)if((bool)source["available"])sourcesAvailable=true;data["supportsTemperatureSources"]=anyConfirmed&&sourcesAvailable;
            data["supportsSmartCurve"]=anyConfirmed&&(FanLimits.Count>0||ActiveFanPorts.Count>0);data["supportsFixedRpm"]=anyConfirmed&&FanLimits.Count>0;data["supportsWrite"]=(bool)data["supportsSmartCurve"]||(bool)data["supportsFixedRpm"]||(bool)data["supportsLighting"]||(bool)data["supportsTemperatureSources"];data["supportedSettings"]=new[]{"smart","fixed","fanCount","lighting","disconnectLightingChain","temperatureSources"};data["curveNodeCount"]=4;data["curveTemperatureMin"]=0;data["curveTemperatureMax"]=100;data["presets"]=new[]{"silent","standard","turbo","fullSpeed"};data["portNamingScope"]="localApplication";
            try {
                var doc=Request(Query("32",id,"5",""));
                var temps=new List<object>();
                foreach(XmlNode node in doc.SelectNodes("/root/data/function/temperatureData/value")) temps.Add(Obj("sourceId",node.Attributes["key"]==null?null:node.Attributes["key"].Value,"temperature",node.InnerText=="-1"?null:node.InnerText));
                data["temperatures"]=temps;
            } catch(Exception ex) { errors.Add("temperature: "+ex.Message); }
        }
        data["errors"]=errors;
        return data;
    }
    internal static void ConfigureSynthetic(string kind,Func<string,string> transport) {
        Kind=kind;TestTransport=transport;Serial.Clear();Names.Clear();FanLimits.Clear();Alive.Clear();ActiveFanPorts.Clear();LcdSensors.Clear();FanEffects.Clear();FanSources.Clear();FanFirmware=null;
    }
    internal static string HandleLine(string line) {
        object id=null;string op="";
        try {
            if(line.Length>65536) throw new WorkerError("INVALID_REQUEST","Request is too large");
            var request=Json.Deserialize<Dictionary<string,object>>(line);
            if(request==null) throw new WorkerError("INVALID_REQUEST","Request must be an object");
            object requestId; if(request.TryGetValue("id",out requestId)) id=requestId;
            op=Str(request,"op","");
            if(Str(request,"kind",Kind)!=Kind) throw new WorkerError("INVALID_REQUEST","Request kind differs from isolated worker kind");
            object result;
            switch(op) {
                case "scan":
                    foreach(string key in request.Keys) if(key!="id"&&key!="op"&&key!="kind"&&!(Kind=="lcd"&&key=="deviceId"))throw new WorkerError("INVALID_REQUEST","Unknown scan field "+key);
                    result=Scan(Kind=="lcd"&&request.ContainsKey("deviceId")?StrictString(request,"deviceId"):"7299"); break;
                case "read": result=Read(Str(request,"deviceId","")); break;
                case "set": result=Set(request);break;
                case "sensors": result=ReadSensors(request);break;
                case "media": result=MediaStatus(request);break;
                default: throw new WorkerError("INVALID_REQUEST","Operation is not on allowlist");
            }
            return Json.Serialize(Obj("id",id,"ok",true,"kind",Kind,"op",op,"data",result));
        } catch(Exception ex) {
            var typed=ex as WorkerError;
            return Json.Serialize(Obj("id",id,"ok",false,"kind",Kind,"op",op,"error",Obj("code",typed==null?"SDK_QUERY_FAILED":typed.Code,"message",ex.Message),"data",typed==null?null:typed.Details));
        }
    }
    public static int Main(string[] args) {
        if(args.Length!=1||(args[0]!="mouse"&&args[0]!="keyboard"&&args[0]!="fan"&&args[0]!="lcd")) return 2;
        Kind=args[0];
        Protocol=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)) { AutoFlush=true };
        // Native log output belongs on stderr, away from the JSON response pipe.
        SetStdHandle(-11,GetStdHandle(-12)); Console.SetOut(Console.Error);
        SetErrorMode(0x8007);
        string line;
        while((line=Console.ReadLine())!=null) {
            Protocol.WriteLine(HandleLine(line));
        }
        return 0;
    }
}
