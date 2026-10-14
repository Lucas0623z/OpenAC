// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored .NET Framework JSON-lines input framing.
using System;
using System.IO;
using System.Text;

namespace OpenAc {
 public static class WorkerTextProtocol {
  static readonly object inputGate=new object();
  static TextReader standardInput;

  // Do not change Console.InputEncoding: Framework Process.Start can emit that
  // writer's preamble before the supervisor writes its first UTF-8 JSON frame.
  public static string ReadLine() {
   lock(inputGate){if(standardInput==null)standardInput=OpenInput(Console.OpenStandardInput());return standardInput.ReadLine();}
  }

  // The returned reader owns raw. No I/O occurs until the reader is used.
  // Only a recognized stream-start preamble is removed; payload always uses
  // strict UTF-8, including after a Framework UTF-16 or UTF-32 preamble.
  public static TextReader OpenInput(Stream raw) {
   if(raw==null)throw new ArgumentNullException("raw");
   if(!raw.CanRead)throw new ArgumentException("Input stream must be readable.","raw");
   return new StreamReader(new PreambleInputStream(raw),new UTF8Encoding(false,true),false,1024);
  }

  sealed class PreambleInputStream : Stream {
   readonly Stream raw;
   readonly byte[] prefix=new byte[4];
   readonly Decoder validator=new UTF8Encoding(false,true).GetDecoder();
   readonly byte[] singleByte=new byte[1];
   char[] validationBuffer=new char[1026];
   int prefixLength,prefixPosition;
   bool initialized,disposed;
   public PreambleInputStream(Stream source){raw=source;}
   void Validate(byte[] bytes,int offset,int count,bool eof) {
    if(validationBuffer.Length<count+2)validationBuffer=new char[count+2];
    // Framework StreamReader does not reliably flush an incomplete decoder at
    // EOF. Validate the same payload bytes independently, including EOF flush.
    validator.GetChars(bytes,offset,count,validationBuffer,0,eof);
   }
   void InitializePrefix() {
    if(disposed)throw new ObjectDisposedException("Input stream");if(initialized)return;
    initialized=true;
    while(prefixLength<4){int value=raw.ReadByte();if(value<0)break;prefix[prefixLength++]=(byte)value;if(value==10)break;}
    // Match the four-byte forms before the shared FF FE two-byte form.
    if(prefixLength>=4&&(prefix[0]==255&&prefix[1]==254&&prefix[2]==0&&prefix[3]==0||prefix[0]==0&&prefix[1]==0&&prefix[2]==254&&prefix[3]==255))prefixPosition=4;
    else if(prefixLength>=3&&prefix[0]==239&&prefix[1]==187&&prefix[2]==191)prefixPosition=3;
    else if(prefixLength>=2&&(prefix[0]==255&&prefix[1]==254||prefix[0]==254&&prefix[1]==255))prefixPosition=2;
   }
   public override int Read(byte[] buffer,int offset,int count) {
    if(buffer==null)throw new ArgumentNullException("buffer");
    if(offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException("offset");
    if(disposed)throw new ObjectDisposedException("Input stream");if(count==0)return 0;
    InitializePrefix();int buffered=Math.Min(count,prefixLength-prefixPosition);
    if(buffered>0){Buffer.BlockCopy(prefix,prefixPosition,buffer,offset,buffered);prefixPosition+=buffered;Validate(buffer,offset,buffered,false);return buffered;}
    int read=raw.Read(buffer,offset,count);Validate(buffer,offset,read,read==0);return read;
   }
   public override int ReadByte(){InitializePrefix();int value=prefixPosition<prefixLength?prefix[prefixPosition++]:raw.ReadByte();if(value<0)Validate(singleByte,0,0,true);else{singleByte[0]=(byte)value;Validate(singleByte,0,1,false);}return value;}
   public override bool CanRead {get{return !disposed&&raw.CanRead;}}
   public override bool CanSeek {get{return false;}}
   public override bool CanWrite {get{return false;}}
   public override long Length {get{throw new NotSupportedException();}}
   public override long Position {get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
   public override void Flush(){if(disposed)throw new ObjectDisposedException("Input stream");}
   public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}
   public override void SetLength(long value){throw new NotSupportedException();}
   public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
   protected override void Dispose(bool disposing){if(!disposed){disposed=true;if(disposing)raw.Dispose();}base.Dispose(disposing);}
  }
 }
}
