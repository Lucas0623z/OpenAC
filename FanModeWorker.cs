// OpenAC-owned Fan Xpert protocol preparation. No native transport is connected.
// Installed module/caps supply interface facts only; no ASUS implementation copied.
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace OpenAcFanMode {
 public sealed class FanModeError : Exception {public readonly string Code;public FanModeError(string code,string message):base(message){Code=code;}}
 public sealed class FanBoardFacts {
  public string BoardName {get;internal set;}
  public bool IdentityConfirmed {get;internal set;}
  public string NativeReturnCode {get;internal set;}
 }
 public sealed class FanHeaderFacts {
  public string Id,Name,NativeType;
  public bool FanStopSupported,DualModeSupported,WaterFan;
  public Dictionary<string,object> Snapshot(){return FanModeRules.Obj("id",Id,"name",Name,"nativeType",NativeType,"fanStopSupported",FanStopSupported,"dualModeSupported",DualModeSupported,"waterFan",WaterFan,"rpm",null,"currentPreset",null,"supportsWrite",false);}
 }
 public static class FanModeRules {
  public static Dictionary<string,object> Obj(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1];return d;}
  public static XmlDocument Parse(string xml){if(xml==null||xml.Length>1048576)throw new FanModeError("INVALID_RESPONSE","Response length is invalid");var d=new XmlDocument{XmlResolver=null};using(var r=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576}))d.Load(r);return d;}
  static string Text(XmlNode node,string path){var n=node==null?null:node.SelectSingleNode(path);return n==null?null:n.InnerText;}
  static bool ValidName(string text){if(String.IsNullOrWhiteSpace(text)||text.Length>256)return false;foreach(char c in text)if(Char.IsControl(c))return false;return true;}
  static bool ValidKey(string key){uint id;return key!=null&&key.Length<=10&&UInt32.TryParse(key,NumberStyles.None,CultureInfo.InvariantCulture,out id)&&id.ToString(CultureInfo.InvariantCulture)==key;}
  static XmlNode Response(string xml,int function){var d=Parse(xml);var profiles=d.SelectNodes("/root/profiles");var p=profiles.Count==1?profiles[0]:null;if(p==null||p.SelectNodes("functionid").Count!=1||Text(p,"functionid")!=function.ToString(CultureInfo.InvariantCulture))throw new FanModeError("FUNCTION_MISMATCH","Reply does not identify exactly the requested function");return p;}
  public static FanBoardFacts BoardInfo(string xml,string expectedWmiBoard){
   if(!ValidName(expectedWmiBoard))throw new FanModeError("IDENTITY_UNCONFIRMED","A separately confirmed exact motherboard identity is required");var p=Response(xml,17);string code=Text(p,"returncode"),name=Text(p,"boardname");
   if(p.SelectNodes("returncode").Count!=1||code!="16"&&code!="23"&&code!="24")throw new FanModeError("BOARD_UNSUPPORTED","Native MBIF did not report supported motherboard status");if(p.SelectNodes("boardname").Count!=1||!ValidName(name)||!String.Equals(name.Trim(),expectedWmiBoard.Trim(),StringComparison.OrdinalIgnoreCase))throw new FanModeError("IDENTITY_MISMATCH","Native board name differs from the independently read motherboard");return new FanBoardFacts{BoardName=name,IdentityConfirmed=true,NativeReturnCode=code};
  }
  static void Board(FanBoardFacts board){if(board==null||!board.IdentityConfirmed||!ValidName(board.BoardName))throw new FanModeError("IDENTITY_UNCONFIRMED","An exact verified MBIF response is required");}
  static bool Flag(XmlNode n,string key){string value=Text(n,key);if(value==null)return false;if(value!="0"&&value!="1")throw new FanModeError("INVALID_CAPABILITY","Unknown fan capability flag");return value=="1";}
  public static List<FanHeaderFacts> Headers(string xml,FanBoardFacts board){
   Board(board);var p=Response(xml,0);var nodes=p.SelectNodes("fan");if(nodes.Count>64)throw new FanModeError("INVALID_CAPABILITY","Too many motherboard fan headers");int count;if(!Int32.TryParse(Text(p,"fancount"),NumberStyles.None,CultureInfo.InvariantCulture,out count)||count!=nodes.Count)throw new FanModeError("INVALID_CAPABILITY","Native fan count does not match the returned headers");var result=new List<FanHeaderFacts>();var ids=new HashSet<string>(StringComparer.Ordinal);
   foreach(XmlNode n in nodes){string id=n.Attributes["key"]==null?null:n.Attributes["key"].Value,name=Text(n,"name"),type=Text(n,"fantype");if(!ValidKey(id)||!ids.Add(id)||!ValidName(name)||!ValidName(type))throw new FanModeError("INVALID_CAPABILITY","Fan header identity is invalid or repeated");result.Add(new FanHeaderFacts{Id=id,Name=name,NativeType=type,FanStopSupported=Flag(n,"isfanstopsupport"),DualModeSupported=Flag(n,"isdualmodesupport"),WaterFan=Flag(n,"iswaterfan")});}return result;
  }
  static void Header(FanHeaderFacts header){if(header==null||!ValidKey(header.Id)||!ValidName(header.Name))throw new FanModeError("INVALID_REQUEST","A source of actual fan header identity is required");}
  // Native fallback model 5000 belongs to this SDK interface, not a declaration of hardware model.
  static string Envelope(int function,string body){return "<root><device_type key=\"50\"><device key=\"5000\"><function key=\""+function+"\">"+body+"</function></device></device_type></root>";}
  public static string ReadQuery(int function,FanBoardFacts board,FanHeaderFacts header,string sensorIndex){
   if(function==17){if(board!=null||header!=null||sensorIndex!=null)throw new FanModeError("INVALID_REQUEST","MBIF has no query parameters");return Envelope(17,"");}
   Board(board);string body="";
   switch(function){
    case 0:if(header!=null||sensorIndex!=null)throw new FanModeError("INVALID_REQUEST","Capability has no header parameters");break;
    case 1:case 12:Header(header);if(sensorIndex!=null)throw new FanModeError("INVALID_REQUEST","Profile/RPM has no sensor parameter");body="<fan key=\""+header.Id+"\"/>";break;
    case 13:Header(header);if(!ValidKey(sensorIndex))throw new FanModeError("INVALID_REQUEST","A valid native sensor index is required");body="<fan key=\""+header.Id+"\"/><SensorIndex key=\""+sensorIndex+"\"/>";break;
    case 16:if(header!=null||!ValidKey(sensorIndex))throw new FanModeError("INVALID_REQUEST","Temperature requires only a native sensor index");body="<SensorIndex key=\""+sensorIndex+"\"/>";break;
    default:throw new FanModeError("INVALID_REQUEST","This function is not on the proven read wrapper allowlist");
   }return Envelope(function,body);
  }
  public static Dictionary<string,object> Rpm(string xml,FanBoardFacts board,FanHeaderFacts header){
   Board(board);Header(header);var p=Response(xml,12);var nodes=p.SelectNodes("fan");if(nodes.Count!=1||nodes[0].Attributes["key"]==null||nodes[0].Attributes["key"].Value!=header.Id)throw new FanModeError("IDENTITY_MISMATCH","RPM reply does not identify exactly the queried header");int rpm;if(!Int32.TryParse(Text(nodes[0],"rpm"),NumberStyles.None,CultureInfo.InvariantCulture,out rpm)||rpm<0||rpm>100000)throw new FanModeError("INVALID_RESPONSE","RPM is not a finite nonnegative integer");var value=header.Snapshot();value["rpm"]=rpm;value["unit"]="rpm";value["readbackConfirmed"]=true;value["rangeSource"]="parserSanityOnly";return value;
  }
  public static Dictionary<string,object> Profile(string xml,FanBoardFacts board,FanHeaderFacts header){
   Board(board);Header(header);var p=Response(xml,1);var nodes=p.SelectNodes("fan");if(nodes.Count!=1||nodes[0].Attributes["key"]==null||nodes[0].Attributes["key"].Value!=header.Id||Text(nodes[0],"name")!=header.Name)throw new FanModeError("IDENTITY_MISMATCH","Profile does not match the exact header");var fan=nodes[0];string mode=Text(fan,"rpmmode");if(mode!="0"&&mode!="1")throw new FanModeError("INVALID_RESPONSE","Unknown native RPM mode");var curve=new List<object>();var points=fan.SelectNodes("curvedata/point");if(points.Count>32)throw new FanModeError("INVALID_RESPONSE","Curve is too large");foreach(XmlNode n in points){double x,y;if(!Double.TryParse(Text(n,"x"),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!Double.TryParse(Text(n,"y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y)||Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y))throw new FanModeError("INVALID_RESPONSE","Curve contains invalid numbers");curve.Add(Obj("x",x,"y",y));}
   return Obj("id",header.Id,"name",header.Name,"nativeRpmMode",mode,"curveNativePoints",curve,"curveUnitsConfirmed",false,"currentPreset",null,"currentPresetReadbackConfirmed",false,"supportsWrite",false);
  }
  public static int PresetIndex(string preset){switch(preset){case "silent":return 3;case "standard":return 2;case "turbo":return 1;case "fullSpeed":return 0;default:throw new FanModeError("INVALID_REQUEST","Unknown Fan Xpert preset");}}
  // Pure draft encoder only. No native transmitter or exposed arbitrary-XML operation exists.
  public static string PresetDraft(string preset,FanBoardFacts board){Board(board);return Envelope(3,"<index>"+PresetIndex(preset)+"</index>");}
  public static void RequireWriteGate(bool legitimateTransport,bool freshBoardRead,bool freshFanCapabilities,bool explicitSessionWrite,bool modeReadbackAvailable){
   if(!legitimateTransport)throw new FanModeError("NATIVE_TRANSPORT_UNVERIFIED","No ordinary caller transport has been connected");if(!freshBoardRead||!freshFanCapabilities)throw new FanModeError("READBACK_REQUIRED","Fresh exact board and header capabilities are required");if(!explicitSessionWrite)throw new FanModeError("WRITE_DISABLED","Explicit session write authorization is required");if(!modeReadbackAvailable)throw new FanModeError("PRESET_READBACK_UNAVAILABLE","No current preset getter has been established");
  }
 }
 public static class FanModeWorker {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=65536};
  public static string HandleLine(string line){object id=null;try{if(line==null||line.Length>65536)throw new FanModeError("INVALID_REQUEST","Request is too large");var r=Json.Deserialize<Dictionary<string,object>>(line);if(r==null)throw new FanModeError("INVALID_REQUEST","An object is required");r.TryGetValue("id",out id);object opRaw;string op=r.TryGetValue("op",out opRaw)?opRaw as string:null;foreach(string key in r.Keys)if(key!="id"&&key!="op")throw new FanModeError("INVALID_REQUEST","No device/caller/function overrides are accepted");
   if(op=="status")return Json.Serialize(FanModeRules.Obj("id",id,"ok",true,"data",FanModeRules.Obj("family","motherboardFanXpert","sourcePrepared",true,"transportConnected",false,"supportsRead",false,"supportsWrite",false,"hardwareValidated",false,"currentPreset",null,"headers",new object[0],"nativeOpens",0,"hardwareSettingsWrites",0,"blocker","NATIVE_TRANSPORT_UNVERIFIED","sourceReadFunctions",new[]{0,1,12,13,16,17},"presetNames",new[]{"silent","standard","turbo","fullSpeed"})));
   if(op=="read"||op=="scan")throw new FanModeError("NATIVE_TRANSPORT_UNVERIFIED","Fan Xpert has no connected ordinary-caller transport; header readings remain unavailable");
   if(op=="set")throw new FanModeError("WRITE_DISABLED","Fan Xpert settings are disabled before any I/O");throw new FanModeError("INVALID_REQUEST","Only status and explicitly unavailable read/scan/set operations are recognized");
  }catch(Exception e){var error=e as FanModeError;return Json.Serialize(FanModeRules.Obj("id",id,"ok",false,"error",FanModeRules.Obj("code",error==null?"INVALID_REQUEST":error.Code,"message",error==null?"Invalid request":error.Message)));}}
  public static int Main(string[] args){if(args.Length!=0)return 2;var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};string line;while((line=Console.ReadLine())!=null)output.WriteLine(HandleLine(line));return 0;}
 }
}
