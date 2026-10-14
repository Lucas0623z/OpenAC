// SPDX-License-Identifier: MIT
// Synthetic byte streams only: no processes, native windows or hardware.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using OpenAc;

public static class WorkerTextProtocolTests {
 static int checks;
 static readonly UTF8Encoding StrictUtf8=new UTF8Encoding(false,true);
 static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static byte[] Bytes(string value){return StrictUtf8.GetBytes(value);}
 static byte[] Concat(byte[] prefix,byte[] payload){return prefix.Concat(payload).ToArray();}
 static List<string> Lines(byte[] bytes){using(var raw=new MemoryStream(bytes))using(var reader=WorkerTextProtocol.OpenInput(raw)){var result=new List<string>();string line;while((line=reader.ReadLine())!=null)result.Add(line);return result;}}
 static bool Invalid(byte[] bytes){try{Lines(bytes);return false;}catch(DecoderFallbackException){return true;}}

 sealed class CountedStream : Stream {
  readonly byte[] bytes;int position;
  public int ByteCalls,ReadCalls;public bool Closed;public int FailAt=-1;
  public CountedStream(byte[] value){bytes=value;}
  public override int ReadByte(){ByteCalls++;if(position==FailAt)throw new IOException("Synthetic read boundary.");return position<bytes.Length?bytes[position++]:-1;}
  public override int Read(byte[] buffer,int offset,int count){ReadCalls++;if(position==FailAt)throw new IOException("Synthetic read boundary.");if(count==0||position>=bytes.Length)return 0;buffer[offset]=bytes[position++];return 1;}
  public override bool CanRead{get{return !Closed;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
  public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
  public override void Flush(){}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
  protected override void Dispose(bool disposing){Closed=true;base.Dispose(disposing);}
 }

 [STAThread] public static int Main() {
  try {
   string[] expected={"{\"id\":1,\"text\":\"键鼠灯光\"}","{\"id\":2,\"text\":\"主板温度\"}","{\"id\":3,\"text\":\"😀\"}"};
   byte[] payload=Bytes(String.Join("\n",expected)+"\n");
   var prefixes=new[]{new byte[0],new byte[]{239,187,191},new byte[]{255,254},new byte[]{254,255},new byte[]{255,254,0,0},new byte[]{0,0,254,255}};
   foreach(byte[] prefix in prefixes) {
    var lines=Lines(Concat(prefix,payload));Check(lines.SequenceEqual(expected),"all payload stays UTF-8 after preamble "+BitConverter.ToString(prefix));
    var serializer=new JavaScriptSerializer();Check(lines.Select(line=>serializer.Deserialize<Dictionary<string,object>>(line)).Select(row=>Convert.ToInt32(row["id"])).SequenceEqual(new[]{1,2,3}),"three JSON frames remain valid and correlated");
    using(var raw=new CountedStream(Concat(prefix,payload)))using(var reader=WorkerTextProtocol.OpenInput(raw)){Check(raw.ByteCalls==0&&raw.ReadCalls==0,"OpenInput construction is inert");Check(reader.ReadLine()==expected[0]&&raw.ByteCalls<=4,"stream head buffers at most four bytes");Check(reader.ReadLine()==expected[1]&&reader.ReadLine()==expected[2]&&reader.ReadLine()==null,"one-byte reads preserve subsequent lines and EOF");}
    Check(Lines(prefix).Count==0,"preamble-only stream is empty");
   }
   string literal="{\"text\":\"before\uFEFFafter\"}";foreach(byte[] prefix in prefixes)Check(Lines(Concat(prefix,Bytes(literal+"\n"))).Single()==literal,"literal BOM inside payload is retained");
   string later="\uFEFF{\"id\":2}";var laterLines=Lines(Bytes("{\"id\":1}\n"+later+"\n"));Check(laterLines.Count==2&&laterLines[1]==later,"second-line BOM is not filtered");bool jsonRejected=false;try{new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(laterLines[1]);}catch(ArgumentException){jsonRejected=true;}Check(jsonRejected,"preserved second-line BOM is rejected by JSON parser");
   var doubled=Lines(Concat(new byte[]{239,187,191,239,187,191},Bytes("{}\n")));Check(doubled.Single()=="\uFEFF{}","only one leading preamble is removed");
   Check(Lines(new byte[0]).Count==0,"empty EOF");foreach(string shortLine in new[]{"a","ab","abc","a\n","\n","{}\n"})Check(Lines(Bytes(shortLine)).SequenceEqual(new[]{shortLine.TrimEnd('\n')}),"short one-to-three-byte stream is not dropped");
   foreach(byte[] prefix in prefixes)foreach(string shortLine in new[]{"a","ab","abc","\n"})Check(Lines(Concat(prefix,Bytes(shortLine))).Single()==shortLine.TrimEnd('\n'),"prefix remainder is retained for short payload");
   foreach(byte[] partial in new[]{new byte[]{239},new byte[]{239,187},new byte[]{255},new byte[]{254},new byte[]{0,0,254}})Check(Invalid(partial),"truncated unrecognized BOM is strict invalid UTF-8");
   Check(Lines(new byte[]{255,254,0}).Single()=="\0","short valid UTF-16 preamble drops two bytes only; payload NUL retained");
   foreach(byte[] invalid in new[]{new byte[]{128},new byte[]{192,128},new byte[]{226,130},new byte[]{237,160,128},new byte[]{244,144,128,128},new byte[]{255,10}})foreach(byte[] prefix in prefixes)Check(Invalid(Concat(prefix,invalid)),"invalid UTF-8 is never replaced or auto-decoded as another encoding");
   foreach(byte[] prefix in prefixes)Check(Invalid(Concat(prefix,Bytes("good\n").Concat(new byte[]{255}).ToArray())),"strict UTF-8 also covers later payload");
   using(var raw=new CountedStream(Bytes("\nmore"))){raw.FailAt=1;using(var reader=WorkerTextProtocol.OpenInput(raw))Check(reader.ReadLine()==""&&raw.ByteCalls==1&&raw.ReadCalls==0,"LF ends prefix lookahead without blocking for more bytes");}
   using(var raw=new CountedStream(Bytes("a\nmore"))){raw.FailAt=2;using(var reader=WorkerTextProtocol.OpenInput(raw))Check(reader.ReadLine()=="a"&&raw.ByteCalls==2&&raw.ReadCalls==0,"short line ends prefix lookahead at LF");}
   var ownership=new CountedStream(Bytes("{}\n"));var ownedReader=WorkerTextProtocol.OpenInput(ownership);ownedReader.Dispose();Check(ownership.Closed&&ownership.ByteCalls==0&&ownership.ReadCalls==0,"reader owns its stream and disposal performs no read");
   bool nullRejected=false;try{WorkerTextProtocol.OpenInput(null);}catch(ArgumentNullException){nullRejected=true;}Check(nullRejected,"null input rejected");bool unreadableRejected=false;using(var unreadable=new MemoryStream()){unreadable.Dispose();try{WorkerTextProtocol.OpenInput(unreadable);}catch(ArgumentException){unreadableRejected=true;}}Check(unreadableRejected,"unreadable input rejected");
   var originalOut=Console.Out;var originalError=Console.Error;var capturedOut=new StringWriter();var capturedError=new StringWriter();try{Console.SetOut(capturedOut);Console.SetError(capturedError);Check(Lines(Concat(prefixes[1],payload)).SequenceEqual(expected),"input helper has no diagnostic output");}finally{Console.SetOut(originalOut);Console.SetError(originalError);}Check(capturedOut.ToString()==""&&capturedError.ToString()=="","reader emits no unsolicited stdout/stderr");
   Console.WriteLine(new JavaScriptSerializer().Serialize(new{ok=true,checks=checks,processes=0,hardwareAccess=false}));return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
 }
}
