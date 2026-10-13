using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using RogControl;
class ReadOnlyIntegrationTests {
 static int Main(){try{Test().GetAwaiter().GetResult();Console.WriteLine("PASS: motherboard HID query/release/rescan, fan readback, LCD readback, malformed setters rejected and typed error contract. No lighting/fan/firmware writes.");return 0;}catch(Exception ex){Console.WriteLine("FAIL: "+ex.Message);return 1;}}
 static Dictionary<string,object> Req(string op,string kind=null,string id=null){var r=new Dictionary<string,object>{{"op",op}};if(kind!=null)r["kind"]=kind;if(id!=null)r["deviceId"]=id;return r;}
 static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
 static async Task Test(){
  using(var worker=new WorkerClient()){
   Console.WriteLine("Read-only: motherboard USB scan / release / rescan");
   var scan=await worker.Request(Req("scan"));Require(scan.ok&&scan.backend=="native-hid"&&scan.devices!=null&&scan.devices.Count==1,"Motherboard scan failed.");
   Console.WriteLine("Read-only: motherboard first scan returned");
   Require(scan.devices[0].zoneCount==4&&!scan.devices[0].effectWriteValidated&&!scan.controlled,"Motherboard logical-zone/verification state incorrect.");
   string identity=scan.devices[0].id;var release=await worker.Request(Req("release"));Require(release.ok&&!release.controlled&&release.devices.Count==0,"Release retained an open controller.");
   Console.WriteLine("Read-only: motherboard release returned");
   scan=await worker.Request(Req("scan"));Require(scan.ok&&scan.devices[0].id==identity,"Read-only rescan failed or identity changed.");
  }
  using(var worker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SDKWorker.exe"),15000,"fan")){
   Console.WriteLine("Read-only: fan controller RPM / capability");
   var reply=await worker.Request<SdkReply>(Req("read","fan","7576"));Require(reply.ok&&reply.data!=null&&Object.Equals(reply.data["readbackConfirmed"],true),"Fan readback failed.");
   var ports=(IEnumerable)reply.data["ports"];int count=0;foreach(var item in ports){var port=(IDictionary<string,object>)item;double rpm;Require(Object.Equals(port["confirmed"],true)&&Double.TryParse(Convert.ToString(port["rpm"]),NumberStyles.Float,CultureInfo.InvariantCulture,out rpm),"Unconfirmed fan RPM.");count++;}Require(count==4,"Expected four fan controller ports.");
   var denied=await worker.Request<SdkReply>(Req("set","fan","7576"));Require(!denied.ok&&denied.error is IDictionary<string,object>,"Disabled writes/typed error contract failed.");
  }
  using(var worker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SDKWorker.exe"),25000,"lcd")){
   Console.WriteLine("Read-only: LCD identity / profile / power");
   var reply=await worker.Request<SdkReply>(Req("read","lcd","7299"));Require(reply.ok&&reply.data!=null&&Object.Equals(reply.data["readbackConfirmed"],true),"LCD readback failed.");
   Require(Convert.ToInt32(reply.data["displayWidth"])==720&&Convert.ToInt32(reply.data["displayHeight"])==1280,"LCD dimensions incorrect.");
   Require(reply.data["brightness"]==null&&Object.Equals(reply.data["brightnessReadable"],false)&&Object.Equals(reply.data["brightnessWritable"],true),"LCD brightness read/write capabilities were conflated.");
  }
 }
}
