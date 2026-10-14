using System;using System.Collections;using System.Collections.Generic;using System.Web.Script.Serialization;using OpenAcFanMode;
// Synthetic XML only. No installed DLLs, cache, Windows identity or hardware access.
internal static class FanModeWorkerTests {
 static int Count;static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 const string Name="ROG CROSSHAIR X870E EXTREME";
 static void Check(bool b,string name){if(!b)throw new Exception("FAIL "+name);Count++;}
 static void Reject(Action a,string code,string name){try{a();throw new Exception("Accepted "+name);}catch(FanModeError e){Check(e.Code==code,name);}}
 static string Wrap(int f,string body){return "<root><profiles><functionid>"+f+"</functionid>"+body+"</profiles></root>";}
 static FanBoardFacts Board(){return FanModeRules.BoardInfo(Wrap(17,"<returncode>24</returncode><boardname>"+Name+"</boardname>"),Name);}
 static string Header(string id,string name,string extra=""){return "<fan key='"+id+"'><name>"+name+"</name><fantype>0</fantype>"+extra+"</fan>";}
 static Dictionary<string,object> Reply(string op){return Json.Deserialize<Dictionary<string,object>>(FanModeWorker.HandleLine("{\"id\":5,\"op\":\""+op+"\"}"));}
 public static int Main(){try{
  foreach(string code in new[]{"16","23","24"})Check(FanModeRules.BoardInfo(Wrap(17,"<returncode>"+code+"</returncode><boardname>"+Name+"</boardname>"),Name).IdentityConfirmed,"supported MBIF "+code);
  foreach(string code in new[]{"0","1","15","25","-1",""})Reject(()=>FanModeRules.BoardInfo(Wrap(17,"<returncode>"+code+"</returncode><boardname>"+Name+"</boardname>"),Name),"BOARD_UNSUPPORTED","unsupported MBIF "+code);
  Reject(()=>FanModeRules.BoardInfo(Wrap(17,"<returncode>24</returncode><boardname>Other board</boardname>"),Name),"IDENTITY_MISMATCH","exact WMI model required");
  Reject(()=>FanModeRules.BoardInfo(Wrap(17,"<returncode>24</returncode><boardname>"+Name+"</boardname>"),null),"IDENTITY_UNCONFIRMED","no caller manufactured identity");
  Reject(()=>FanModeRules.BoardInfo(Wrap(0,""),Name),"FUNCTION_MISMATCH","wrong board function");
  Reject(()=>FanModeRules.BoardInfo("<root><profiles><functionid>17</functionid><functionid>0</functionid></profiles></root>",Name),"FUNCTION_MISMATCH","duplicate function identity");
  Reject(()=>FanModeRules.BoardInfo(Wrap(17,"<returncode>24</returncode><boardname>"+Name+"</boardname><boardname>Other board</boardname>"),Name),"IDENTITY_MISMATCH","duplicate board identity");
  try{FanModeRules.Parse("<!DOCTYPE root [<!ENTITY e SYSTEM 'file:///secret'>]><root>&e;</root>");throw new Exception("DTD accepted");}catch(System.Xml.XmlException){Check(true,"XXE prohibited");}
  var board=Board();var cap=Wrap(0,"<fancount>2</fancount>"+Header("0","CPU Fan","<isfanstopsupport>1</isfanstopsupport><isdualmodesupport>0</isdualmodesupport>")+Header("4","Water Pump+","<iswaterfan>1</iswaterfan>"));var headers=FanModeRules.Headers(cap,board);
  Check(headers.Count==2&&headers[0].Id=="0"&&headers[0].Name=="CPU Fan","native header identities retained");Check(headers[0].FanStopSupported&&headers[1].WaterFan,"actual native flags");
  Reject(()=>FanModeRules.Headers(cap,null),"IDENTITY_UNCONFIRMED","capability needs board");
  Reject(()=>FanModeRules.Headers(Wrap(0,"<fancount>1</fancount>"+Header("0","CPU Fan")+Header("4","Water Pump+")),board),"INVALID_CAPABILITY","fan count mismatch");
  Reject(()=>FanModeRules.Headers(Wrap(0,"<fancount>2</fancount>"+Header("0","CPU Fan")+Header("0","Other")),board),"INVALID_CAPABILITY","duplicate native port");
  foreach(string id in new[]{"-1","1.2","bad","4294967296","00"})Reject(()=>FanModeRules.Headers(Wrap(0,"<fancount>1</fancount>"+Header(id,"CPU Fan")),board),"INVALID_CAPABILITY","bad header "+id);
  Reject(()=>FanModeRules.Headers(Wrap(0,"<fancount>1</fancount>"+Header("0","CPU Fan","<isfanstopsupport>yes</isfanstopsupport>")),board),"INVALID_CAPABILITY","unknown flag");
  Check(FanModeRules.Headers(Wrap(0,"<fancount>0</fancount>"),board).Count==0,"empty capability not synthetic fans");
  Check(FanModeRules.ReadQuery(17,null,null,null).Contains("key=\"50\""),"type50 only");Check(FanModeRules.ReadQuery(0,board,null,null).Contains("key=\"5000\""),"fixed SDK interface model");
  foreach(int f in new[]{1,12})Check(FanModeRules.ReadQuery(f,board,headers[0],null).Contains("<fan key=\"0\"/>"),"header read "+f);
  Check(FanModeRules.ReadQuery(13,board,headers[0],"7").Contains("<fan key=\"0\"/><SensorIndex key=\"7\"/>"),"point native key exact case");Check(FanModeRules.ReadQuery(16,board,null,"7").Contains("<SensorIndex key=\"7\"/>"),"temperature native key exact case");
  foreach(int f in new[]{2,3,4,5,6,8,9,10,11,14,15,18,19,20,21,22,36,101})Reject(()=>FanModeRules.ReadQuery(f,board,headers[0],null),"INVALID_REQUEST","not a proven pure getter "+f);
  Reject(()=>FanModeRules.ReadQuery(12,null,headers[0],null),"IDENTITY_UNCONFIRMED","read before identity");Reject(()=>FanModeRules.ReadQuery(12,board,null,null),"INVALID_REQUEST","read before headercap");Reject(()=>FanModeRules.ReadQuery(13,board,headers[0],"a'"),"INVALID_REQUEST","no XML injection");
  foreach(int rpm in new[]{0,1200,100000}){var value=FanModeRules.Rpm(Wrap(12,"<fan key='0'><rpm>"+rpm+"</rpm></fan>"),board,headers[0]);Check((int)value["rpm"]==rpm&&(bool)value["readbackConfirmed"]&&!(bool)value["supportsWrite"],"RPM pure decode "+rpm);}
  foreach(string rpm in new[]{"-1","1.5","NaN","100001"})Reject(()=>FanModeRules.Rpm(Wrap(12,"<fan key='0'><rpm>"+rpm+"</rpm></fan>"),board,headers[0]),"INVALID_RESPONSE","invalid RPM "+rpm);
  Reject(()=>FanModeRules.Rpm(Wrap(12,"<fan key='4'><rpm>1200</rpm></fan>"),board,headers[0]),"IDENTITY_MISMATCH","RPM reply wrong header");
  var profile=FanModeRules.Profile(Wrap(1,"<fan key='0'><name>CPU Fan</name><rpmmode>0</rpmmode><curvedata><point><x>40</x><y>30</y></point></curvedata></fan>"),board,headers[0]);Check((string)profile["nativeRpmMode"]=="0"&&((IList)profile["curveNativePoints"]).Count==1,"profile native configuration");Check(profile["currentPreset"]==null&&!(bool)profile["currentPresetReadbackConfirmed"]&&!(bool)profile["curveUnitsConfirmed"],"profile does not invent preset or units");
  Reject(()=>FanModeRules.Profile(Wrap(1,"<fan key='0'><name>CPU Fan</name><rpmmode>2</rpmmode></fan>"),board,headers[0]),"INVALID_RESPONSE","unknown RPM mode");
  string[] names={"silent","standard","turbo","fullSpeed"};for(int i=0;i<names.Length;i++)Check(FanModeRules.PresetDraft(names[i],board).Contains("<index>"+(3-i)+"</index>")&&FanModeRules.PresetDraft(names[i],board).Contains("<function key=\"3\">"),"exact installed preset "+names[i]);Reject(()=>FanModeRules.PresetIndex("performance"),"INVALID_REQUEST","not laptop performance enum");
  Reject(()=>FanModeRules.RequireWriteGate(false,true,true,true,true),"NATIVE_TRANSPORT_UNVERIFIED","no legal transport");Reject(()=>FanModeRules.RequireWriteGate(true,false,true,true,true),"READBACK_REQUIRED","no fresh board");Reject(()=>FanModeRules.RequireWriteGate(true,true,false,true,true),"READBACK_REQUIRED","no fresh headers");Reject(()=>FanModeRules.RequireWriteGate(true,true,true,false,true),"WRITE_DISABLED","default-off session");Reject(()=>FanModeRules.RequireWriteGate(true,true,true,true,false),"PRESET_READBACK_UNAVAILABLE","no fictional getter");
  var status=Reply("status");var data=(Dictionary<string,object>)status["data"];Check((bool)status["ok"]&&!(bool)data["transportConnected"]&&!(bool)data["supportsRead"]&&!(bool)data["supportsWrite"],"runtime truthfully unavailable");Check(((IList)data["headers"]).Count==0&&data["currentPreset"]==null,"runtime never invents CPU/chassis fans");
  foreach(string op in new[]{"read","scan","set","init","calibration","stress"})Check(!(bool)Reply(op)["ok"],"runtime disabled "+op);
  Check(!(bool)Json.Deserialize<Dictionary<string,object>>(FanModeWorker.HandleLine("{\"op\":\"status\",\"caller\":\"ASUS\"}"))["ok"],"caller override rejected");
  Console.WriteLine("PASS "+Count+" checks; native transports=0; cache reads=0; hardware writes=0");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
