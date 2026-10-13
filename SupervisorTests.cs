using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RogControl;
class SupervisorTests {
 static int Main(){try{Test().GetAwaiter().GetResult();Console.WriteLine("PASS: correlation, native noise, worker crash, timeout, automatic restart, disposal prevents restart (6 checks)");return 0;}catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
 static async Task Test(){
  using(var worker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockWorker.exe"),500)){
   var reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok||reply.id!=1)throw new Exception("Wrong reply correlation or native output noise not isolated.");
   bool exited=false;try{await worker.Request(new Dictionary<string,object>{{"op","exit"}});}catch(InvalidOperationException){exited=true;}if(!exited)throw new Exception("Native exit not reported.");
   reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new Exception("Worker did not restart after exit.");
   bool timedOut=false;try{await worker.Request(new Dictionary<string,object>{{"op","hang"}});}catch(System.TimeoutException){timedOut=true;}if(!timedOut)throw new Exception("Hung SDK not timed out.");
   reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});if(!reply.ok)throw new Exception("Worker did not restart after timeout.");
   worker.Dispose();bool disposed=false;try{await worker.Request(new Dictionary<string,object>{{"op","scan"}});}catch(ObjectDisposedException){disposed=true;}if(!disposed||worker.Running)throw new Exception("Disposed worker unexpectedly restarted.");
  }
 }
}
