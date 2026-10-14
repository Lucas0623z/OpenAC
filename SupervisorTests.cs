using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RogControl;
class SupervisorTests {
 static int checks;
 static void Check(bool valid,string message){checks++;if(!valid)throw new Exception(message);}
 static async Task<bool> SessionRejected(WorkerClient worker,object session,Dictionary<string,object> request){try{await worker.Request<Reply>(request,session);return false;}catch(InvalidOperationException){return true;}}
 static int Main(){try{Test().GetAwaiter().GetResult();Console.WriteLine("PASS: correlation, native noise, worker crash, timeout, restart, bounded-session rejection and disposal ("+checks+" checks)");return 0;}catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
 static async Task Test(){
  using(var worker=new WorkerClient(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MockWorker.exe"),500)){
   Check(worker.SessionIdentity==null&&!worker.Running,"Unstarted client has no native session.");
   var denied=new Dictionary<string,object>{{"op","scan"}};Check(await SessionRejected(worker,new object(),denied)&&!worker.Running&&!denied.ContainsKey("id"),"Bounded request cannot start an absent session.");
   var reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});Check(reply.ok&&reply.id==1,"Wrong reply correlation or native output noise not isolated.");
   object first=worker.SessionIdentity;Check(first!=null,"Successful mock startup creates a session identity.");
   reply=await worker.Request<Reply>(new Dictionary<string,object>{{"op","scan"}},first);Check(reply.ok&&Object.ReferenceEquals(first,worker.SessionIdentity),"Valid bounded request remains in the same process session.");
   bool exited=false;try{await worker.Request(new Dictionary<string,object>{{"op","exit"}});}catch(InvalidOperationException){exited=true;}Check(exited,"Native exit not reported.");
   Check(worker.SessionIdentity==null,"Exited process cannot supply a live session identity.");
   denied=new Dictionary<string,object>{{"op","scan"}};Check(await SessionRejected(worker,first,denied)&&!worker.Running&&!denied.ContainsKey("id"),"Expired identity cannot silently restart a process or transmit a request.");
   reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});Check(reply.ok,"Worker did not restart after exit.");
   object second=worker.SessionIdentity;Check(second!=null&&!Object.ReferenceEquals(first,second),"A restart creates a distinct session identity.");
   denied=new Dictionary<string,object>{{"op","scan"}};Check(await SessionRejected(worker,first,denied)&&Object.ReferenceEquals(second,worker.SessionIdentity)&&!denied.ContainsKey("id"),"Old identity is rejected in a newly running session without touching its request.");
   reply=await worker.Request<Reply>(new Dictionary<string,object>{{"op","scan"}},second);Check(reply.ok,"New identity accepts a valid bounded mock request.");
   bool timedOut=false;try{await worker.Request(new Dictionary<string,object>{{"op","hang"}});}catch(System.TimeoutException){timedOut=true;}Check(timedOut,"Hung SDK not timed out.");
   Check(worker.SessionIdentity==null,"Timeout clears the process session.");
   denied=new Dictionary<string,object>{{"op","scan"}};Check(await SessionRejected(worker,second,denied)&&!worker.Running&&!denied.ContainsKey("id"),"Timeout-expired identity does not restart a worker.");
   reply=await worker.Request(new Dictionary<string,object>{{"op","scan"}});Check(reply.ok,"Worker did not restart after timeout.");
   object third=worker.SessionIdentity;Check(third!=null&&!Object.ReferenceEquals(second,third),"Timeout recovery creates a fresh session.");
   worker.Dispose();bool disposed=false;try{await worker.Request(new Dictionary<string,object>{{"op","scan"}});}catch(ObjectDisposedException){disposed=true;}Check(disposed&&!worker.Running&&worker.SessionIdentity==null,"Disposed worker unexpectedly restarted or retained its session.");
  }
 }
}
