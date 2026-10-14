using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Web.Script.Serialization;
using RogControlNotebook;

// Pure injected identities and transports only. Native WMI and ATKACPI are never accessed.
internal static class NotebookWorkerTests {
 static int count,identityCalls,opens,reads,disposals;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 static NotebookIdentity current;
 static uint rawStatus;
 static bool failRead;
 sealed class Fake:INotebookTransport {
  public byte[] ReadStatus(uint endpoint) { reads++;var packet=NotebookRules.StatusPacket(endpoint);Assert(packet.Length==16&&packet[0]==0x44&&packet[1]==0x53&&packet[2]==0x54&&packet[3]==0x53&&packet[4]==8&&BitConverter.ToUInt32(packet,8)==endpoint&&BitConverter.ToUInt32(packet,12)==0,"only DSTS header/zero status");if(failRead)throw new Win32Exception(5);return Word(rawStatus); }
  public void Dispose(){disposals++;}
 }
 static void Assert(bool condition,string label){if(!condition)throw new Exception("FAIL "+label);count++;}
 static byte[] Word(uint value){return BitConverter.GetBytes(value);}
 static Dictionary<string,object> Obj(params object[] pairs){return NotebookRules.Obj(pairs);}
 static Dictionary<string,object> Reply(NotebookService service,object request){return Json.Deserialize<Dictionary<string,object>>(service.HandleLine(Json.Serialize(request)));}
 static Dictionary<string,object> Data(Dictionary<string,object> reply){return (Dictionary<string,object>)reply["data"];}
 static Dictionary<string,object> Error(Dictionary<string,object> reply){return (Dictionary<string,object>)reply["error"];}
 static bool Ok(Dictionary<string,object> reply){return (bool)reply["ok"];}
 static NotebookIdentity Identity(string model,int chassis){return new NotebookIdentity {Manufacturer="ASUSTeK COMPUTER INC.",Model=model,ChassisTypes=new[]{chassis},IdentityReadConfirmed=true,InterfaceDriverConfirmed=true};}
 static NotebookService Ready(){current=Identity("ROG Zephyrus G16 GU605MI_GU605MI",10);identityCalls=opens=reads=disposals=0;rawStatus=0x00010021;failRead=false;return new NotebookService(delegate{identityCalls++;return current;},delegate{opens++;return new Fake();},1000);}
 static void Rejected(NotebookService service,object request,string label){identityCalls=opens=reads=0;var reply=Reply(service,request);Assert(!Ok(reply),label+" rejected");Assert(identityCalls==0&&opens==0&&reads==0,label+" before any I/O");}
 static Dictionary<string,object> State(string feature,uint raw){return NotebookRules.Decode(NotebookRules.Select(null,new[]{feature})[0],Word(raw));}
 public static int Main(){try{
  foreach(var model in NotebookRules.Models){var gate=NotebookRules.Gate(Identity("ASUS "+model.Code+"_"+model.Code,model.Family=="handheld"?11:10));Assert(gate.Eligible&&gate.Model.Code==model.Code,"exact prepared model "+model.Code);}
  foreach(string manufacturer in new[]{"ASUSTeK COMPUTER INC.","ASUSTeK COMPUTER INC","ASUS"})Assert(NotebookRules.IsAsus(manufacturer),"known manufacturer "+manufacturer);
  foreach(string manufacturer in new[]{"Other ASUS Company","ASUS reseller", "ASUStekFake", "Dell Inc.", "",null})Assert(!NotebookRules.IsAsus(manufacturer),"reject ambiguous manufacturer");
  foreach(int chassis in new[]{3,4,5,6,7,13,15,16,17,23,35,0})Assert(!NotebookRules.Gate(Identity("GU605MI",chassis)).Eligible,"desktop/unknown chassis "+chassis);
  var conflict=Identity("GU605MI",10);conflict.ChassisTypes=new[]{10,3};Assert(!NotebookRules.Gate(conflict).Eligible,"conflicting mobile/desktop enclosure");
  var noChassis=Identity("GU605MI",10);noChassis.ChassisTypes=new int[0];Assert(!NotebookRules.Gate(noChassis).Eligible,"empty enclosure blocked");
  foreach(string model in new[]{"GU605MIX","XGU605MI","GU605","RC72","unknown","GU605MI RC71L"})Assert(!NotebookRules.Gate(Identity(model,10)).Eligible,"no guessed or ambiguous model "+model);
  var unconfirmed=Identity("GU605MI",10);unconfirmed.IdentityReadConfirmed=false;Assert(!NotebookRules.Gate(unconfirmed).Eligible,"unconfirmed WMI identity");
  var unsigned=Identity("GU605MI",10);unsigned.InterfaceDriverConfirmed=false;Assert(!NotebookRules.Gate(unsigned).Eligible,"unconfirmed ASUS driver");
  var service=Ready();var scan=Reply(service,Obj("id","scan-1","op","scan"));Assert(Ok(scan)&&identityCalls==1&&opens==0&&reads==0,"scan reads identity only");Assert((string)scan["id"]=="scan-1"&&!(bool)Data(scan)["supportsWrite"]&&!(bool)Data(scan)["hardwareReadValidated"],"identity is not physical status validation");
  var read=Reply(service,Obj("id",2,"op","read","features",new[]{"cpuFan","gpuFan"}));Assert(Ok(read)&&reads==2&&opens==1&&disposals==1,"limited two-feature read and dispose");var states=(IList)Data(read)["states"];Assert(states.Count==2&&(int)((Dictionary<string,object>)states[0])["value"]==3300,"RPM conversion is 100 per native unit");
  Rejected(service,Obj("id",3,"op","set","setting","chargeLimit","value",80),"writes without explicit permission and a fresh control capture");
  foreach(string op in new[]{"init","watchdog","devs","calibrate","firmware","subscribe","shutdown",""})Rejected(service,Obj("op",op),"nonread op "+op);
  Rejected(service,Obj("op","read","deviceId",0x00120057),"caller endpoint override");
  Rejected(service,Obj("op","read","methodId",0x53564544),"caller method override");
  Rejected(service,Obj("op","read","status",1),"caller control-status override");
  Rejected(service,Obj("op","read","model","GU605MI"),"caller identity override");
  Rejected(service,Obj("op","scan","features",new[]{"cpuFan"}),"scan fields");
  foreach(object features in new object[]{new string[0],new[]{"unknown"},new[]{"cpuFan","cpuFan"},new object[]{1},"cpuFan",null})Rejected(service,Obj("op","read","features",features),"invalid feature selection");
  current=Identity("ROG CROSSHAIR X870E EXTREME",3);opens=reads=0;var desktop=Reply(service,Obj("op","read"));Assert(Ok(desktop)&&!(bool)Data(desktop)["eligibleForStatus"]&&opens==0&&reads==0&&((IList)Data(desktop)["states"]).Count==0,"desktop never opens ATKACPI and refreshes snapshot");
  current=Identity("Unknown ASUS Notebook",10);var unknown=Reply(service,Obj("op","read"));Assert(Ok(unknown)&&opens==0&&(string)Data(unknown)["supportReason"]=="MODEL_NOT_PREPARED","unknown mobile remains identity-only");
  current=Identity("RC73YA",11);opens=reads=0;var handheld=Reply(service,Obj("op","read","features",new[]{"cpuFan","chargeLimit"}));Assert(Ok(handheld)&&reads==2,"source-prepared handheld read path");opens=reads=0;var notebookOnly=Reply(service,Obj("op","read","features",new[]{"keyboardBrightness"}));Assert(!Ok(notebookOnly)&&opens==0&&reads==0,"handheld cannot query notebook keyboard endpoint");
  foreach(uint raw in new uint[]{0,33,0xFFFFFFFE,0xFFFFFFFF,0x80010021}){var state=State("cpuFan",raw);Assert(!(bool)state["readbackConfirmed"]&&state["value"]==null,"unsupported/unknown fan word "+raw);}
  foreach(uint raw in new uint[]{0x10000,0x10078}){var state=State("cpuFan",raw);Assert((bool)state["valueReadbackConfirmed"]&&(int)state["value"]==(int)(raw&0xFFFF)*100,"valid RPM lower/upper");}
  var highFan=State("cpuFan",0x10079);Assert((bool)highFan["presenceConfirmed"]&&!(bool)highFan["valueReadbackConfirmed"]&&highFan["value"]==null,"invalid RPM leaves value unknown");
  var userFlag=State("cpuFan",0x30021);Assert((int)userFlag["value"]==3300,"status flag not added to RPM");
  var temp=State("cpuTemperature",0x10032);Assert((int)temp["value"]==50&&(bool)temp["valueReadbackConfirmed"],"temperature status decode");
  Assert(State("cpuTemperature",0x100FF)["value"]==null,"invalid temperature is unknown");
  var limit=State("chargeLimit",0x10050);Assert((int)limit["queryValue"]==80&&limit["value"]==null&&!(bool)limit["valueReadbackConfirmed"],"charge status not asserted current charge setting");
  var mode=State("performanceMode",0x10001);Assert((int)mode["queryValue"]==1&&mode["value"]==null,"performance status not asserted current BIOS mode");
  var keyboard=State("keyboardBrightness",0x10302);Assert((int)keyboard["value"]==2,"brightness low byte independent of maximum byte");
  Assert((int)State("keyboardBrightness",0x10380)["value"]==0,"brightness on bit is not added to a confirmed zero level");
  Assert((int)State("keyboardBrightness",0x10382)["value"]==2,"brightness on bit is not added to current level");
  Assert(State("keyboardBrightness",0x30382)["value"]==null,"ambiguous keyboard status bit stays unknown");
  foreach(byte[] bytes in new[]{new byte[0],new byte[3],new byte[17]})Assert(!(bool)NotebookRules.Decode(NotebookRules.Select(null,new[]{"cpuFan"})[0],bytes)["readbackConfirmed"],"invalid response size");
  Assert(!(bool)NotebookRules.Decode(NotebookRules.Select(null,new[]{"cpuFan"})[0],null)["readbackConfirmed"],"null driver response");
  bool forbidden=false;try{NotebookRules.StatusPacket(0x00110024);}catch(NotebookError){forbidden=true;}Assert(forbidden,"fan curve and arbitrary endpoints never encoded");
  service=Ready();failRead=true;var denied=Reply(service,Obj("op","read"));Assert(!Ok(denied)&&reads==1&&disposals==1&&(string)Error(denied)["code"]=="INTERFACE_QUERY_FAILED","driver error fails batch without retries or subsequent endpoints");
  var leave=new ManualResetEvent(false);var finished=new ManualResetEvent(false);int timedOpens=0;
  var timed=new NotebookService(delegate{leave.WaitOne();finished.Set();return Identity("GU605MI",10);},delegate{timedOpens++;return new Fake();},20);
  var timeout=Reply(timed,Obj("id","deadline","op","read"));Assert(!Ok(timeout)&&(string)Error(timeout)["code"]=="QUERY_TIMEOUT"&&(bool)timeout["restartRequired"],"timeout is explicit worker restart");leave.Set();Assert(finished.WaitOne(1000),"pure blocked identity released");Assert(timed.WaitForSyntheticCompletion(1000)&&timedOpens==0,"completed late callback cannot open a native interface after timeout");Assert(!Ok(Reply(timed,Obj("op","scan"))),"poisoned worker cannot be reused");leave.Dispose();finished.Dispose();
  service=Ready();Assert(!Ok(Json.Deserialize<Dictionary<string,object>>(service.HandleLine(new string('x',16385)))),"bounded JSON request");Assert(!Ok(Json.Deserialize<Dictionary<string,object>>(service.HandleLine("null"))),"null JSON object");
  Console.WriteLine("NotebookWorker pure checks passed: "+count+"; WMI queries: 0; ATKACPI opens: 0; hardware writes: 0");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
