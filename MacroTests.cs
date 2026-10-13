using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RogControl;

// Synthetic only: no WindowsMacroInput, hooks, keyboard recording or SendInput.
public static class MacroTests {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static void Reject(Action action,string message){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}Check(failed,message);}
 static MacroAction Key(string kind,int key=65){return new MacroAction{Kind=kind,Key=key};}
 sealed class Fake:IMacroInput {
  public readonly List<MacroAction> Sent=new List<MacroAction>();public Action<MacroAction> OnSend;public bool NotReady;
  public void CheckReady(){if(NotReady)throw new InvalidOperationException("held physical key");}
  public void Send(MacroAction action){if(OnSend!=null)OnSend(action);Sent.Add(action.Clone());}
 }
 static async Task Run() {
  var editor=new MacroEditor();string initial=MacroRepository.Encode(editor.Current);editor.Change(d=>d.Actions.Add(Key("key-down")));editor.Change(d=>d.Name="测试宏");editor.Undo();Check(editor.Current.Name=="新宏"&&editor.Current.Actions.Count==1,"Undo lost actions");editor.Undo();Check(MacroRepository.Encode(editor.Current)==initial,"Undo did not restore initial snapshot");editor.Redo();Check(editor.Current.Actions.Count==1,"Redo failed");editor.Change(d=>d.Actions.Add(Key("key-up")));Check(!editor.CanRedo,"New edit retained stale redo history");
  var doc=editor.Current.Clone();doc.Name="往返测试";var parsed=MacroRepository.Decode(MacroRepository.Encode(doc));Check(parsed.Actions.Count==2&&parsed.Name==doc.Name,"JSON round trip failed");
  string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"macro-test-data",Guid.NewGuid().ToString("N"));var repo=new MacroRepository(directory);repo.Save(doc);doc.Name="原子替换";repo.Save(doc);string warning;var files=repo.List(out warning);Check(files.Count==1&&files[0].Name==doc.Name&&warning==null,"Atomic store save/replace failed");Check(Directory.GetFiles(directory).Length==1,"Store leaked temporary files");
  Reject(()=>{var invalid=doc.Clone();invalid.Actions=Enumerable.Range(0,101).Select(i=>Key("key-down")).ToList();MacroRules.Validate(invalid);},"101 actions accepted");
  Reject(()=>{var invalid=doc.Clone();invalid.Actions[0].Kind="run-shell";MacroRules.Validate(invalid);},"Unrecognized action accepted");
  Reject(()=>{var invalid=doc.Clone();invalid.Actions[0].Key=0x78;MacroRules.Validate(invalid);},"Reserved F9 accepted");
  Reject(()=>{var invalid=doc.Clone();invalid.Actions[0].BeforeMs=-1;MacroRules.Validate(invalid);},"Negative delay accepted");
  Reject(()=>{var invalid=doc.Clone();invalid.Actions=Enumerable.Range(0,11).Select(i=>new MacroAction{Kind="delay",DelayMs=60000}).ToList();MacroRules.Validate(invalid);},"Excessive total duration accepted");
  Reject(()=>MacroRepository.Decode("{broken json"),"Malformed import accepted");
  var limit=new MacroCaptureBuffer(false);for(int i=0;i<100;i++){bool result=limit.Add(Key(i%2==0?"key-down":"key-up"),i);Check(result==(i<99),"Capture limit stopped at wrong action");}Check(limit.Count==100&&!limit.Add(Key("key-down"),101),"Capture exceeded 100 actions");
  var remaining=new MacroCaptureBuffer(false,2);Check(remaining.Add(Key("key-down"),0)&&!remaining.Add(Key("key-up"),1)&&remaining.Count==2,"Remaining capacity was ignored");
  var noMove=new MacroCaptureBuffer(false);noMove.Add(new MacroAction{Kind="mouse-move",X=10,Y=10},100);Check(noMove.Count==0,"Default recorder retained mouse motion");
  var repeats=new MacroCaptureBuffer(false);repeats.Add(Key("key-down"),0);repeats.Add(Key("key-down"),5);repeats.Add(Key("key-up"),10);Check(repeats.Count==2,"Auto-repeat duplicated captured key down");
  var timeLimit=new MacroCaptureBuffer(false);Check(!timeLimit.Add(Key("key-down"),60001)&&timeLimit.Count==0,"Single delay limit was silently truncated");
  var fake=new Fake();await MacroPlayback.RunAsync(doc,fake,CancellationToken.None);Check(fake.Sent.Count==2&&fake.Sent.Last().Kind=="key-up","Normal playback did not release key");
  var held=new MacroDocument();held.Actions.Add(Key("key-down"));fake=new Fake();await MacroPlayback.RunAsync(held,fake,CancellationToken.None);Check(fake.Sent.Count==2&&fake.Sent.Last().Kind=="key-up","Unbalanced macro did not release in finally");
  var orphan=new MacroDocument();orphan.Actions.Add(Key("key-up"));fake=new Fake();await MacroPlayback.RunAsync(orphan,fake,CancellationToken.None);Check(fake.Sent.Count==0,"Orphan release could release a physical user key");
  using(var cancel=new CancellationTokenSource()){fake=new Fake();fake.OnSend=a=>{if(a.Kind=="key-down")cancel.Cancel();};var cancelled=new MacroDocument();cancelled.Actions.Add(Key("key-down"));cancelled.Actions.Add(new MacroAction{Kind="delay",DelayMs=5000});bool ended=false;try{await MacroPlayback.RunAsync(cancelled,fake,cancel.Token);}catch(OperationCanceledException){ended=true;}Check(ended&&fake.Sent.Count==2&&fake.Sent.Last().Kind=="key-up","Cancellation did not release held key");}
  var failure=new MacroDocument();failure.Actions.Add(Key("key-down",65));failure.Actions.Add(Key("key-down",66));fake=new Fake();fake.OnSend=a=>{if(a.Kind=="key-down"&&a.Key==66)throw new IOException("fake send failure");};bool failed=false;try{await MacroPlayback.RunAsync(failure,fake,CancellationToken.None);}catch(IOException){failed=true;}Check(failed&&fake.Sent.Count==2&&fake.Sent.Last().Key==65&&fake.Sent.Last().Kind=="key-up","Send failure did not release prior successful input");
  fake=new Fake{NotReady=true};failed=false;try{await MacroPlayback.RunAsync(held,fake,CancellationToken.None);}catch(InvalidOperationException){failed=true;}Check(failed&&fake.Sent.Count==0,"Not-ready playback sent input");
  int releases=0;fake=new Fake();fake.OnSend=a=>{if(a.Kind=="key-up"){releases++;throw new IOException("fake release failure");}};failed=false;try{await MacroPlayback.RunAsync(failure,fake,CancellationToken.None);}catch(AggregateException){failed=true;}Check(failed&&releases==2,"One release failure prevented remaining release attempts");
  Check(Marshal.SizeOf(typeof(MacroNative.INPUT))==(IntPtr.Size==8?40:28),"INPUT interop size does not match Windows ABI");
  Check(Marshal.OffsetOf(typeof(MacroNative.INPUT),"data").ToInt32()==(IntPtr.Size==8?8:4),"INPUT union alignment is invalid");
  Console.WriteLine("Macro synthetic checks passed: "+checks+". No input hooks or native input were used.");
 }
 public static int Main(){try{Run().GetAwaiter().GetResult();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
