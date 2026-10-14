// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Isolated, ordinary-caller x86 COM read worker. It never elevates or sets fans.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace OpenAcFanNative {
 public interface IFanNativeReader { Dictionary<string,object> Read(bool curves); }
 sealed class FanNativeReadFailure:Exception {
  public readonly string Stage;public readonly int GetterCalls;public readonly bool Activated;public readonly Dictionary<string,object> Abi;
  public FanNativeReadFailure(Exception inner,FanNativeTransport transport):base("A bounded native read failed.",inner){Stage=transport.Stage;GetterCalls=transport.NativeGetterCalls;Activated=transport.Activated;Abi=transport.LastAbiDiagnostics==null?null:new Dictionary<string,object>(transport.LastAbiDiagnostics);}
 }
 sealed class FanNativeComReader:IFanNativeReader {
  public Dictionary<string,object> Read(bool curves){using(var transport=new FanNativeTransport())try{object manager=transport.Open();var data=FanNativeRules.Collect(transport,manager,curves);transport.CheckSameService();data["boardName"]=transport.Identity.BoardName;data["serviceVersion"]=transport.Identity.FileVersion;data["source"]="AsusFanControlService.COM";data["capturedAtUtc"]=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);data["transportConnected"]=true;data["nativePropertyReadConfirmed"]=true;data["hardwareAppliedStateConfirmed"]=false;data["hardwareReadValidated"]=false;data["getterCalls"]=transport.NativeGetterCalls;data["unitsConfirmed"]=false;data["supportsWrite"]=false;return data;}catch(Exception e){throw new FanNativeReadFailure(e,transport);}}
 }
 public sealed class FanNativeProtocol {
  readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=32};readonly Func<IFanNativeReader> createReader;
  string lastReadAt;bool lastReadSucceeded;int readAttempts;
  public FanNativeProtocol(Func<IFanNativeReader> factory){if(factory==null)throw new ArgumentNullException("factory");createReader=factory;}
  public string Handle(string line){object id=null;try{
   if(line==null||line.Length>65536)throw FanNativeRules.Error("INVALID_REQUEST","Request length is invalid.");Dictionary<string,object> request;try{request=json.Deserialize<Dictionary<string,object>>(line);}catch(ArgumentException){throw FanNativeRules.Error("INVALID_REQUEST","Malformed JSON request.");}catch(InvalidOperationException){throw FanNativeRules.Error("INVALID_REQUEST","Malformed JSON request.");}if(request==null)throw FanNativeRules.Error("INVALID_REQUEST","An object request is required.");object raw;request.TryGetValue("id",out id);if(id!=null&&!(id is int)&&!(id is string)||id is string&&(((string)id).Length>128||((string)id).IndexOfAny(new[]{'\r','\n','\0'})>=0))throw FanNativeRules.Error("INVALID_REQUEST","A short string or Int32 request id is required.");string op=request.TryGetValue("op",out raw)?raw as string:null;if(op==null)throw FanNativeRules.Error("INVALID_REQUEST","A fixed operation is required.");foreach(string key in request.Keys)if(key!="id"&&key!="op"&&(key!="includeCurves"||op!="read"&&op!="scan"))throw FanNativeRules.Error("INVALID_REQUEST","No caller, native path, board or COM member override is accepted.");bool curves=false;if(request.TryGetValue("includeCurves",out raw)){if(!(raw is bool))throw FanNativeRules.Error("INVALID_REQUEST","includeCurves must be a Boolean.");curves=(bool)raw;}
   if(op=="status")return Ok(id,FanNativeRules.Obj("family","motherboardFanXpert","source","AsusFanControlService.COM","sourcePrepared",true,"supportsRead",true,"supportsWrite",false,"lastReadSucceeded",lastReadSucceeded,"lastReadAtUtc",lastReadAt,"readAttempts",readAttempts,"nativeActivationOnStatus",false,"requiresExactBoard",FanNativeRules.BoardName,"requiresInstalledServiceVersion",FanNativeRules.ServiceVersion,"requiresX86Worker",true,"automaticallyElevates",false,"hardwareSettingsWrites",0,"globalPreset",null,"rpmAvailable",false,"writeBlocker","NATIVE_UNITS_SELECTOR_AND_APPLIED_READBACK_UNVERIFIED"));
   if(op=="release"||op=="shutdown"){lastReadSucceeded=false;lastReadAt=null;return Ok(id,FanNativeRules.Obj("released",true,"hardwareSettingsWrites",0));}
   if(op=="set"||op=="apply")throw FanNativeRules.Error("WRITE_DISABLED","Units, selector meaning and applied write readback are not verified; all writes are rejected before I/O.");
   if(op!="read"&&op!="scan")throw FanNativeRules.Error("INVALID_REQUEST","Only status/read/scan/release/shutdown are available.");lastReadSucceeded=false;lastReadAt=null;readAttempts++;var reader=createReader();if(reader==null)throw FanNativeRules.Error("NATIVE_TRANSPORT_UNAVAILABLE","The fixed read transport is unavailable.");var data=reader.Read(curves);if(data==null)throw FanNativeRules.Error("INVALID_RESPONSE","A complete read snapshot is required.");object timestamp;if(!data.TryGetValue("capturedAtUtc",out timestamp)||!(timestamp is string))throw FanNativeRules.Error("INVALID_RESPONSE","A fresh read timestamp is required.");lastReadAt=(string)timestamp;lastReadSucceeded=true;return Ok(id,data);
  }catch(Exception e){var failure=e as FanNativeReadFailure;Exception actual=failure==null?e:e.InnerException;if(actual is TargetInvocationException&&actual.InnerException!=null)actual=actual.InnerException;var known=actual as FanNativeError;var windows=actual as Win32Exception;string code=known==null?(actual is UnauthorizedAccessException||actual.HResult==unchecked((int)0x80070005)||windows!=null&&windows.NativeErrorCode==5?"ACCESS_DENIED":"NATIVE_READ_FAILED"):known.Code;return json.Serialize(FanNativeRules.Obj("id",id,"ok",false,"error",FanNativeRules.Obj("code",code,"message",known==null?"The ordinary read failed; no retry, elevation or settings command was issued.":known.Message,"hresult","0x"+actual.HResult.ToString("X8",CultureInfo.InvariantCulture),"diagnostics",FanNativeRules.Obj("stage",failure==null?null:failure.Stage,"activated",failure==null?(bool?)null:failure.Activated,"getterCalls",failure==null?(int?)null:failure.GetterCalls,"exceptionType",actual.GetType().FullName,"nativeErrorCode",windows==null?(int?)null:windows.NativeErrorCode,"abi",failure==null?null:failure.Abi)),"hardwareSettingsWrites",0));}}
  string Ok(object id,object data){return json.Serialize(FanNativeRules.Obj("id",id,"ok",true,"data",data));}
 }
 public static class FanNativeWorker {
  [STAThread]public static int Main(string[] args){if(args.Length!=0)return 2;var handler=new FanNativeProtocol(()=>new FanNativeComReader());var writer=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};string line;while((line=OpenAc.WorkerTextProtocol.ReadLine())!=null){
   // The deadline includes preflight, activation, getter enumeration, disposal
   // and output. Parent supervision remains required in case process creation
   // or framing stalls. No COM cancellation/retry can bypass a hung vendor call.
   using(var deadline=new Timer(_=>Environment.Exit(4),null,10000,Timeout.Infinite)){writer.WriteLine(handler.Handle(line));}
   try{var request=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);object op;if(request!=null&&request.TryGetValue("op",out op)&&op as string=="shutdown")break;}catch{}
  }return 0;}
 }
}
