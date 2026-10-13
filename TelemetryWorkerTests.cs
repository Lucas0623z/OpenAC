using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
using OpenAcTelemetry;

// Pure fake transport / executor tests. Never opens an installed DLL or a hardware handle.
internal static class TelemetryWorkerTests {
 static int Count,Calls;static string Last,Fault;static bool FailScan;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static string Sensor(string id,string name,int type,string unit){return "<sensor key='"+id+"'><name>"+name+"</name><default_name>"+name+"</default_name><type>"+type+"</type>"+(unit==null?"":"<unit>"+unit+"</unit>")+"</sensor>";}
 static string Cap(string sensors,string model="7299"){return "<root><capability><device_list><deviceid>"+model+"</deviceid></device_list><player key='hw_monitor_player'><sensor_list>"+sensors+"</sensor_list></player></capability></root>";}
 static string Row(string id,string name,string type,string value){return "<sensor><id>"+id+"</id>"+(name==null?"":"<default_name>"+name+"</default_name>")+(type==null?"":"<type>"+type+"</type>")+"<value>"+value+"</value></sensor>";}
 static Dictionary<string,object> Obj(params object[] pairs){return TelemetryRules.Obj(pairs);}
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);Count++;}
 static void Reject(Action action,string code,string name){try{action();throw new Exception("Accepted "+name);}catch(TelemetryError e){Check(e.Code==code,name);}}
 static Dictionary<string,object> First(List<object> values){return (Dictionary<string,object>)values[0];}
 static string Native(string xml){Calls++;Last=xml;var d=TelemetryRules.Parse(xml);string f=d.SelectSingleNode("/root/device_type/device/function").Attributes["key"].Value;Check(f=="0"||f=="36","fake transport read allowlist");if(f=="0"){if(FailScan)throw new Exception("fake disconnect");return Cap(Sensor("10","CPU Core",4,null)+Sensor("20","GPU",1,null)+Sensor("30","Pump",2,"rpm")+Sensor("40","GPU",6,null)+Sensor("40","GPU",6,null));}return Fault??("<root>"+Row("20","GPU","1","0")+Row("30","Pump","2","0")+"</root>");}
 static Dictionary<string,object> Reply(TelemetrySession s,object r){return Json.Deserialize<Dictionary<string,object>>(TelemetryWorker.Reply(s,Json.Serialize(r)));}
 static void NoIO(TelemetrySession s,object request,string name){int before=Calls;var r=Reply(s,request);Check(!(bool)r["ok"],name+" rejected");Check(Calls==before,name+" before I/O");}
 public static int Main(){try{
  foreach(string model in new[]{"7299","7124","7571"}){var c=TelemetryRules.Capabilities(Cap(Sensor("1","CPU",4,null),model),model);Check(c.Count==1&&c[0].Id=="sensor-0","exact model "+model);Check(TelemetryRules.Query(model,0,null).Contains("key=\""+model+"\""),"model encoded "+model);}
  Reject(()=>TelemetryRules.Capabilities(Cap(Sensor("1","CPU",4,null)),"9999"),"UNSUPPORTED_MODEL","unknown model");
  Reject(()=>TelemetryRules.Capabilities(Cap(Sensor("1","CPU",4,null),"7124"),"7299"),"IDENTITY_UNCONFIRMED","wrong native model");
  try{TelemetryRules.Parse("<!DOCTYPE root [<!ENTITY v SYSTEM 'file:///secret'>]><root>&v;</root>");throw new Exception("DTD accepted");}catch(System.Xml.XmlException){Check(true,"DTD/XXE rejected by XML parser");}
  return Continue();
 }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
 static int Continue(){try{
  foreach(string id in new[]{"-1","0","1.2","4294967296","not-id"})Reject(()=>TelemetryRules.Capabilities(Cap(Sensor(id,"CPU",4,null)),"7299"),"INVALID_NATIVE_RESPONSE","native ID "+id);
  Reject(()=>TelemetryRules.Capabilities(Cap(Sensor("1","CPU",-1,null)),"7299"),"INVALID_NATIVE_RESPONSE","negative category");
  Reject(()=>TelemetryRules.Capabilities(Cap(Sensor("1","",4,null)),"7299"),"INVALID_NATIVE_RESPONSE","empty sensor name");
  var huge="";for(int i=1;i<=257;i++)huge+=Sensor(i.ToString(),"CPU",4,null);Reject(()=>TelemetryRules.Capabilities(Cap(huge),"7299"),"INVALID_NATIVE_RESPONSE","capability bound");
  var caps=TelemetryRules.Capabilities(Cap(Sensor("1","Core #0",4,null)+Sensor("1","Graphics",4,null)+Sensor("2","GPU",1,null)+Sensor("3","GPU",6,null)+Sensor("3","GPU",6,null)),"7299");
  Check(!caps[0].Ambiguous&&!caps[1].Ambiguous,"same ID distinguishable by exact name");Check(caps[3].Ambiguous&&caps[4].Ambiguous,"duplicate GPU capability flagged");
  foreach(object bad in new object[]{null,"sensor-0",new string[0],new[]{"sensor-0","sensor-0"},new[]{"missing"},new object[]{1},new[]{"sensor-0","sensor-1","sensor-2","sensor-0","sensor-1","sensor-2"}})Reject(()=>TelemetryRules.Select(caps,bad),"INVALID_REQUEST","invalid selection");
  Reject(()=>TelemetryRules.Select(caps,new[]{"sensor-3"}),"AMBIGUOUS_SENSOR","duplicated GPU must not be selected");
  var selected=TelemetryRules.Select(caps,new[]{"sensor-0"});var body=TelemetryRules.Query("7299",36,selected);Check(body.Contains("<id>1</id><default_name>Core #0</default_name><type>4</type>"),"strict sensor tuple XML");Check(!body.Contains("<sn>")&&!body.Contains("token"),"no serial or trusted caller");
  foreach(int operation in new[]{1,2,15,25,26,27,30,37,101})Reject(()=>TelemetryRules.Query("7299",operation,null),"INVALID_REQUEST","unsupported query "+operation);
  var missing=First(TelemetryRules.Values("<root>"+Row("1",null,null,"4500")+"</root>",selected,caps));Check(missing["value"]==null&&(string)missing["error"]=="AMBIGUOUS_NATIVE_IDENTITY","ID-only reply does not merge CPU and GPU");
  foreach(string number in new[]{"NaN","Infinity","-Infinity","1e999","text",""}){var v=First(TelemetryRules.Values("<root>"+Row("1","Core #0","4",number)+"</root>",selected,caps));Check(!(bool)v["nativeValueReadbackConfirmed"]&&v["value"]==null,"finite parser "+number);}
  var raw=First(TelemetryRules.Values("<root>"+Row("1","Core #0","4","4500")+"</root>",selected,caps));Check(Convert.ToDouble(raw["rawNativeValue"])==4500&&(bool)raw["nativeValueReadbackConfirmed"],"raw finite identity returned");Check(raw["unit"]==null&&raw["value"]==null&&!(bool)raw["valueReadbackConfirmed"],"category does not imply units");
  var dup=First(TelemetryRules.Values("<root>"+Row("1","Core #0","4","4500")+Row("1","Core #0","4","4510")+"</root>",selected,caps));Check(!(bool)dup["nativeValueReadbackConfirmed"],"duplicate replies rejected");
  foreach(int type in new[]{1,4}){var c=TelemetryRules.Capabilities(Cap(Sensor("1","CPU",type,type==1?"C":"MHz")),"7299");var v=First(TelemetryRules.Values("<root>"+Row("1","CPU",type.ToString(),"0")+"</root>",c,c));Check(v["value"]==null&&(bool)v["placeholder"]&&(bool)v["nativeValueReadbackConfirmed"],"zero thermal/clock placeholder "+type);}
  foreach(var pair in new[]{new[]{"0","V","1.15"},new[]{"1","C","51.2"},new[]{"2","rpm","0"},new[]{"4","MHz","4900"}}){var c=TelemetryRules.Capabilities(Cap(Sensor("1","Known",int.Parse(pair[0]),pair[1])),"7299");var v=First(TelemetryRules.Values("<root>"+Row("1","Known",pair[0],pair[2])+"</root>",c,c));Check((bool)v["valueReadbackConfirmed"]&&!(bool)v["placeholder"],"explicit unit confirms value "+pair[1]);}
  foreach(var pair in new[]{new[]{"6","percent"},new[]{"4","rpm"},new[]{"1","V"}}){var c=TelemetryRules.Capabilities(Cap(Sensor("1","Mixed",int.Parse(pair[0]),pair[1])),"7299");Check(c[0].Unit==null,"unit/type mismatch "+pair[0]);}
  var escaped=TelemetryRules.Capabilities(Cap(Sensor("1","Core &amp; GPU",4,null)),"7299");Check(TelemetryRules.Query("7299",36,escaped).Contains("Core &amp; GPU"),"XML escaping");
  var s=new TelemetrySession(Native);Calls=0;NoIO(s,Obj("op","set","setting","power"),"set");NoIO(s,Obj("op","scan","deviceId","8888"),"unsupported caller SDK");NoIO(s,Obj("op","scan","xml","ignored"),"arbitrary XML");
  var status=Reply(s,Obj("id",1,"op","status"));Check((bool)status["ok"]&&Calls==0,"status never loads native");
  var scanned=Reply(s,Obj("op","scan"));Check((bool)scanned["ok"]&&Calls==1,"scan one capability read");var sd=(Dictionary<string,object>)scanned["data"];Check(!(bool)sd["physicalValidation"]&&!(bool)((Dictionary<string,object>)((IList)sd["sensors"])[0])["valueReadbackConfirmed"],"enumeration is not physical measurement");
  NoIO(s,Obj("op","read","deviceId","7299","sensorIds",new[]{"sensor-3"}),"ambiguous scanned GPU");NoIO(s,Obj("op","read","deviceId","7299","sensorIds",new[]{"sensor-1"},"method",2),"method override");NoIO(s,Obj("op","read","deviceId","7124","sensorIds",new[]{"sensor-1"}),"unscanned model");
  var read=Reply(s,Obj("op","read","deviceId","7299","sensorIds",new[]{"sensor-1","sensor-2"}));Check((bool)read["ok"]&&Calls==2&&Last.Contains("key=\"36\""),"read one bounded query");var rows=(IList)((Dictionary<string,object>)read["data"])["sensors"];Check((bool)((Dictionary<string,object>)rows[0])["placeholder"],"observed native zero temp");Check(Convert.ToDouble(((Dictionary<string,object>)rows[1])["value"])==0,"stopped fan zero is valid with unit");
  Fault="<root>"+Row("99","GPU","1","50")+"</root>";var wrong=Reply(s,Obj("op","read","deviceId","7299","sensorIds",new[]{"sensor-1"}));var wrongRow=(Dictionary<string,object>)((IList)((Dictionary<string,object>)wrong["data"])["sensors"])[0];Check(wrongRow["value"]==null&&!(bool)wrongRow["nativeValueReadbackConfirmed"],"wrong response ID cannot become selected measurement");Fault=null;
  FailScan=true;var failed=Reply(s,Obj("op","scan"));Check(!(bool)failed["ok"],"disconnect scan failure");int after=Calls;NoIO(s,Obj("op","read","deviceId","7299","sensorIds",new[]{"sensor-1"}),"stale sensor cache cleared");Check(Calls==after,"no stale query after disconnect");FailScan=false;
  var executor=new TelemetryWorker.Executor();bool restart;int threadId=0;executor.Run(()=>{threadId=Thread.CurrentThread.ManagedThreadId;Check(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA,"permanent native STA");return "a";},1000,1,out restart);Check(!restart,"healthy executor");executor.Run(()=>{Check(Thread.CurrentThread.ManagedThreadId==threadId,"same STA across requests");return "b";},1000,2,out restart);
  var gate=new ManualResetEvent(false);string timeout=executor.Run(()=>{gate.WaitOne();return "late";},10,42,out restart);var timed=Json.Deserialize<Dictionary<string,object>>(timeout);Check(restart&&(bool)timed["restartRequired"]&&Convert.ToInt32(timed["id"])==42,"timeout correlated response");Reject(()=>{bool next;executor.Run(()=>"bad",1000,43,out next);},"RESTART_REQUIRED","poisoned executor cannot retry");gate.Set();
  Check(!Reply(s,Obj("op","status","function",36)).ContainsKey("data"),"status fields closed");
  Console.WriteLine("PASS "+Count+" checks; installed DLL loads=0; native opens=0; hardware writes=0");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
