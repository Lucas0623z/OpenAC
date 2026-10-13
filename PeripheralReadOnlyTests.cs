using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using RogControl;

class PeripheralReadOnlyTests {
 static int Main(){try{Run().GetAwaiter().GetResult();return 0;}catch(Exception ex){Console.WriteLine("FAIL: "+ex.Message);return 1;}}
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static async Task Run(){
  var evidence=new Dictionary<string,object>();
  using(var worker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"GearLinkWorker.exe"),18000)){
   foreach(string kind in new[]{"headset","keyboard","mouse"}){
    var scan=await worker.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"},{"kind",kind}});Require(scan.ok&&scan.data!=null,kind+" scan failed.");
    evidence[kind+"Scan"]=scan.data;
    var devices=(IEnumerable)scan.data["devices"];var target=devices.Cast<object>().OfType<IDictionary<string,object>>().FirstOrDefault(d=>Object.Equals(d["online"],true));
    if(target==null){Console.WriteLine("Read-only: "+kind+" is offline; hardware settings cannot be verified.");continue;}
    string id=Convert.ToString(target["deviceId"]);
    var reply=await worker.Request<SdkReply>(new Dictionary<string,object>{{"op","read"},{"kind",kind},{"deviceId",id}});Require(reply.ok&&reply.data!=null&&Object.Equals(reply.data["readbackConfirmed"],true),kind+" read failed.");evidence[kind+"Read"]=reply.data;
    if(kind!="headset")continue;
    var observed=(IDictionary<string,object>)reply.data["snapshot"];var supported=((IEnumerable)reply.data["setCapabilities"]).Cast<object>().Select(Convert.ToString).ToArray();int checkedCount=0;
    foreach(string setting in supported){
     string getter=setting=="brightness"?"brightnessRaw":setting;object value;Require(observed.TryGetValue(getter,out value)&&value!=null,"No confirmed no-op value for "+setting);
     var noop=await worker.Request<SdkReply>(new Dictionary<string,object>{{"op","set"},{"kind",kind},{"deviceId",id},{"setting",setting},{"value",value},{"experimental",true},{"requireNoOp",true}});
     Require(noop.ok&&noop.data!=null&&Object.Equals(noop.data["writeAttempted"],false)&&Convert.ToString(noop.data["appliedState"])=="alreadySet","No-op guard failed for "+setting);checkedCount++;
    }
    var denied=await worker.Request<SdkReply>(new Dictionary<string,object>{{"op","set"},{"kind",kind},{"deviceId",id},{"setting","eqEnabled"},{"value",!Convert.ToBoolean(observed["eqEnabled"])},{"experimental",true},{"requireNoOp",true}});
    Require(!denied.ok,"Changed setting escaped the mandatory no-op guard.");evidence["headsetNoOpSettings"]=checkedCount;evidence["changedValueRejected"]=true;Console.WriteLine("Read-only: "+checkedCount+" headset mandatory no-op guards passed; changed value rejected.");
   }
   foreach(string kind in new[]{"headset","keyboard","mouse"}){
    var catalog=await worker.Request<SdkReply>(new Dictionary<string,object>{{"op","firmware"},{"kind",kind}});Require(catalog.ok&&catalog.data!=null&&Object.Equals(catalog.data["hardwareAccess"],false)&&Object.Equals(catalog.data["updateToolDownloaded"],false),"Official firmware directory failed: "+kind);evidence[kind+"Firmware"]=catalog.data;Console.WriteLine("Read-only: official "+kind+" firmware directory returned; no tool download.");
   }
  }
  using(var audio=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AudioEndpointWorker.exe"),10000)){
   var reply=await audio.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});Require(reply.ok&&reply.data!=null,"Windows audio read failed.");evidence["audio"]=reply.data;Console.WriteLine("Read-only: Windows audio endpoints returned; no audio changes.");
  }
  using(var storage=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"StorageWorker.exe"),12000)){
   var reply=await storage.Request<SdkReply>(new Dictionary<string,object>{{"op","scan"}});Require(reply.ok&&reply.data!=null,"Storage read failed.");evidence["storage"]=reply.data;Console.WriteLine("Read-only: storage identity and health returned.");
  }
  evidence["capturedAtUtc"]=DateTime.UtcNow.ToString("o");evidence["hardwareWrites"]=0;File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"peripheral-final-readonly.json"),new JavaScriptSerializer().Serialize(evidence));
  Console.WriteLine("PASS: read-only peripheral/audio/storage/catalog checks. No setting, audio, firmware or macro changes.");
 }
}
