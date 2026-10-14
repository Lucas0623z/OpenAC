// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored fixed COM property-get contract. No vendor code/resources.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenAcFanNative {
 public enum FanNativeRole { Manager, Controls, Control, Profiles, Profile, Curve, Point }
 public sealed class FanNativeError:Exception { public readonly string Code;public FanNativeError(string code,string message):base(message){Code=code;} }
 public sealed class FanNativeProperty {
  public readonly FanNativeRole Role; public readonly string Name;public readonly int DispatchId,VariantType;public readonly FanNativeRole? ResultRole;
  internal FanNativeProperty(FanNativeRole role,string name,int id,int variant,FanNativeRole? child=null){Role=role;Name=name;DispatchId=id;VariantType=variant;ResultRole=child;}
 }
 public interface IFanNativeReadSource {
  object Get(object target,FanNativeRole role,string name);
  IList<object> Items(object target,FanNativeRole collectionRole,FanNativeRole elementRole,int count);
 }
 public sealed class FanNativeIdentity {
  public string BoardName,Manufacturer,ServicePath,FileVersion,Hash,RegisteredServer,RegisteredAppId,RegisteredTypeLib,RegisteredTypeLibVersion,RegisteredService,ServiceState,ServiceCommand,ProcessImage;
  public bool SignatureValid,ReparsePath;public string SignerName;public int ProcessId,PointerSize;
 }
 public static class FanNativeRules {
  public const string BoardName="ROG CROSSHAIR X870E EXTREME";
  public const string ServicePath=@"C:\Program Files (x86)\ASUS\AsusFanControlService\2.05.06\AsusFanControlService.exe";
  public const string ServiceVersion="2.0.05.06";
  public const string ServiceHash="c2959edb65eb1104f1522fad15b55f4927415edd23ab552d9be74be656f7a977";
  public const string ClassId="14083C53-B8E7-48E4-9320-811F3478C4A4",AppId="{BEBCB884-9C21-4117-B30B-CED03A72B50F}",TypeLib="{DF5522FB-119C-428D-A62E-8BFE4A2FBC9B}",ServiceName="AsusFanControlService";
  public const int HeaderLimit=32,ProfileLimit=16,PointLimit=16;
  static readonly Guid[] InterfaceIds={new Guid("bb327645-e3ec-4b7d-90ee-67fcb949e584"),new Guid("05f22b4c-27d5-4488-a7ee-b712fe447426"),new Guid("d20e7b8f-c878-4538-b624-9bc08fe7d54f"),new Guid("8caf0dbe-d1e1-42a4-b0ec-1b365cf50982"),new Guid("ddc814d3-118a-4bc7-9e0d-8c96599e922c"),new Guid("e6bb07b4-f360-45af-814e-eb0d12d55bec"),new Guid("c3c24163-fc1b-431f-978d-44a9fbac34ec")};
  static readonly FanNativeProperty[] Properties={
   new FanNativeProperty(FanNativeRole.Manager,"Controls",2,26,FanNativeRole.Controls),
   new FanNativeProperty(FanNativeRole.Controls,"Count",2,3),new FanNativeProperty(FanNativeRole.Controls,"_NewEnum",-4,13),
   new FanNativeProperty(FanNativeRole.Control,"Id",1,19),new FanNativeProperty(FanNativeRole.Control,"Name",2,8),new FanNativeProperty(FanNativeRole.Control,"DisplayName",14,8),new FanNativeProperty(FanNativeRole.Control,"DutyCycle",3,17),new FanNativeProperty(FanNativeRole.Control,"Profiles",4,26,FanNativeRole.Profiles),new FanNativeProperty(FanNativeRole.Control,"CurrentFanCurve",13,26,FanNativeRole.Curve),new FanNativeProperty(FanNativeRole.Control,"IsRpmMode",19,11),
   new FanNativeProperty(FanNativeRole.Profiles,"Count",2,3),new FanNativeProperty(FanNativeRole.Profiles,"_NewEnum",-4,13),new FanNativeProperty(FanNativeRole.Profiles,"Current",4,19),
   new FanNativeProperty(FanNativeRole.Profile,"Id",3,19),new FanNativeProperty(FanNativeRole.Profile,"Name",1,8),
   new FanNativeProperty(FanNativeRole.Curve,"Count",2,3),new FanNativeProperty(FanNativeRole.Curve,"_NewEnum",-4,13),
   new FanNativeProperty(FanNativeRole.Point,"Temperature",1,3),new FanNativeProperty(FanNativeRole.Point,"Speed",2,3)
  };
  public static Guid InterfaceId(FanNativeRole role){if((int)role<0||(int)role>=InterfaceIds.Length)throw Error("ABI_MISMATCH","Unexpected COM object role.");return InterfaceIds[(int)role];}
  public static Dictionary<string,object> AbiDiagnostics(FanNativeRole role,Guid? actualGuid,string typeKind,int? functionCount,uint? typeInfoCount){return Obj("expectedRole",role.ToString(),"expectedTypeGuid",InterfaceId(role).ToString("D"),"actualTypeGuid",actualGuid.HasValue?actualGuid.Value.ToString("D"):null,"actualTypeKind",typeKind,"actualFunctionCount",functionCount,"actualTypeInfoCount",typeInfoCount);}
  public static FanNativeProperty Property(FanNativeRole role,string name){var p=Properties.SingleOrDefault(x=>x.Role==role&&x.Name==name);if(p==null)throw Error("GETTER_NOT_ALLOWED","Only fixed ordinary getters are allowed.");return p;}
  public static FanNativeError Error(string code,string message){return new FanNativeError(code,message);}
  public static Dictionary<string,object> Obj(params object[] pairs){var d=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)d.Add((string)pairs[i],pairs[i+1]);return d;}
  public static void ValidateIdentity(FanNativeIdentity x){
   if(x==null)throw Error("IDENTITY_REQUIRED","Fresh identity is required.");if(x.PointerSize!=4)throw Error("X86_REQUIRED","Fan COM worker must be a separate x86 process.");
   if(!String.Equals(x.BoardName,BoardName,StringComparison.OrdinalIgnoreCase)||!(String.Equals(x.Manufacturer,"ASUSTeK COMPUTER INC.",StringComparison.OrdinalIgnoreCase)||String.Equals(x.Manufacturer,"ASUS",StringComparison.OrdinalIgnoreCase)))throw Error("EXACT_BOARD_MISMATCH","This backend is limited to the verified motherboard.");
   if(x.ReparsePath||!PathEquals(x.ServicePath,ServicePath)||!String.Equals(x.FileVersion,ServiceVersion,StringComparison.Ordinal)||x.Hash!=ServiceHash)throw Error("SERVICE_BINARY_CHANGED","The installed service must match the reviewed artifact.");
   if(!x.SignatureValid||x.SignerName!="ASUSTeK COMPUTER INC.")throw Error("SIGNATURE_INVALID","A valid matching installed ASUS signature is required.");
   string command="\""+ServicePath+"\"";if(!String.Equals(x.RegisteredServer,command,StringComparison.OrdinalIgnoreCase)||!String.Equals(x.RegisteredAppId,AppId,StringComparison.OrdinalIgnoreCase)||!String.Equals(x.RegisteredTypeLib,TypeLib,StringComparison.OrdinalIgnoreCase)||x.RegisteredTypeLibVersion!="1.0"||x.RegisteredService!=ServiceName)throw Error("COM_REGISTRATION_CHANGED","COM registration does not match the reviewed local service.");
   if(x.ServiceState!="Running"||x.ProcessId<=0||!String.Equals(x.ServiceCommand,command,StringComparison.OrdinalIgnoreCase)||!PathEquals(x.ProcessImage,ServicePath))throw Error("EXACT_SERVICE_NOT_RUNNING","The exact signed service must already be running; explicit service-start commands are excluded.");
  }
  static bool PathEquals(string actual,string expected){return String.Equals(actual,expected,StringComparison.OrdinalIgnoreCase);}
  public static int Count(object v,int max){if(!(v is int)||(int)v<0||(int)v>max)throw Error("COLLECTION_COUNT_INVALID","Native collection count is invalid.");return (int)v;}
  public static uint NativeId(object v){if(!(v is uint))throw Error("PROPERTY_TYPE_INVALID","A native UInt32 was required.");return (uint)v;}
  public static string Text(object v){string s=v as string;if(String.IsNullOrWhiteSpace(s)||s.Length>256||s.Any(Char.IsControl))throw Error("PROPERTY_TEXT_INVALID","Native text is invalid.");return s;}
  public static int Signed(object v){if(!(v is int))throw Error("PROPERTY_TYPE_INVALID","A native signed Int32 was required.");return (int)v;}
  static IList<object> Items(IFanNativeReadSource source,object collection,FanNativeRole role,FanNativeRole element,int maximum){if(collection==null)throw Error("COM_OBJECT_REQUIRED","A collection was missing.");int count=Count(source.Get(collection,role,"Count"),maximum);var rows=source.Items(collection,role,element,count);if(rows==null||rows.Count!=count||rows.Any(x=>x==null))throw Error("ENUMERATION_COUNT_MISMATCH","The enumerated objects do not match the declared count.");return rows;}
  public static Dictionary<string,object> Curve(IFanNativeReadSource source,object curve){var rows=Items(source,curve,FanNativeRole.Curve,FanNativeRole.Point,PointLimit);var points=new List<object>();foreach(object p in rows)points.Add(Obj("temperatureRaw",Signed(source.Get(p,FanNativeRole.Point,"Temperature")),"speedRaw",Signed(source.Get(p,FanNativeRole.Point,"Speed"))));return Obj("points",points,"count",points.Count,"temperatureUnit",null,"speedUnit",null,"unitsConfirmed",false,"rangeSource","nativeUnscaledInt32");}
  public static Dictionary<string,object> Collect(IFanNativeReadSource source,object manager,bool includeCurves){
   if(source==null||manager==null)throw Error("COM_OBJECT_REQUIRED","A source and manager are required.");var controls=source.Get(manager,FanNativeRole.Manager,"Controls");var rows=Items(source,controls,FanNativeRole.Controls,FanNativeRole.Control,HeaderLimit);var result=new List<object>();var headerIds=new HashSet<uint>();
   foreach(object f in rows){uint id=NativeId(source.Get(f,FanNativeRole.Control,"Id"));if(!headerIds.Add(id))throw Error("FAN_ID_DUPLICATE","Native header identifiers must be unique.");string name=Text(source.Get(f,FanNativeRole.Control,"Name")),display=Text(source.Get(f,FanNativeRole.Control,"DisplayName"));object duty=source.Get(f,FanNativeRole.Control,"DutyCycle");if(!(duty is byte))throw Error("PROPERTY_TYPE_INVALID","DutyCycle must be the native byte; no conversion is authorized.");
    object profiles=source.Get(f,FanNativeRole.Control,"Profiles");uint current=NativeId(source.Get(profiles,FanNativeRole.Profiles,"Current"));var profileRows=Items(source,profiles,FanNativeRole.Profiles,FanNativeRole.Profile,ProfileLimit);var entries=new List<object>();var ids=new HashSet<uint>();foreach(object p in profileRows){uint pid=NativeId(source.Get(p,FanNativeRole.Profile,"Id"));if(!ids.Add(pid))throw Error("PROFILE_ID_DUPLICATE","Native profile identifiers must be unique per header.");entries.Add(Obj("id",pid,"name",Text(source.Get(p,FanNativeRole.Profile,"Name"))));}
    object curve=null,rpmMode=null;if(includeCurves){object rawMode=source.Get(f,FanNativeRole.Control,"IsRpmMode");if(!(rawMode is bool))throw Error("PROPERTY_TYPE_INVALID","IsRpmMode must be a native Boolean.");rpmMode=rawMode;curve=Curve(source,source.Get(f,FanNativeRole.Control,"CurrentFanCurve"));}
    result.Add(Obj("id",id,"name",name,"displayName",display,"dutyCycleRaw",(byte)duty,"dutyUnitConfirmed",false,"profiles",entries,"currentProfileRaw",current,"currentSelectorSemanticsConfirmed",false,"currentCurve",curve,"isRpmMode",rpmMode,"rpm",null,"supportsWrite",false));
   }
   return Obj("headers",result,"count",result.Count,"curvesRequested",includeCurves,"rpmAvailable",false,"globalPreset",null,"globalPresetReadbackConfirmed",false,"hardwareSettingsWrites",0,"explicitSetterCalls",0);
  }
  public static List<object> EnumerateManaged(System.Collections.IEnumerator e,int expected,Action<object> own){if(e==null)throw Error("ENUMERATOR_NULL","Native collection enumeration is missing.");if(expected<0||expected>HeaderLimit)throw Error("COLLECTION_COUNT_INVALID","Enumeration count is invalid.");var rows=new List<object>();for(int i=0;i<=expected;i++){if(!e.MoveNext()){if(rows.Count!=expected)throw Error("ENUMERATION_COUNT_MISMATCH","Enumeration ended before the declared count.");return rows;}if(i==expected||e.Current==null)throw Error("ENUMERATION_COUNT_MISMATCH","Enumeration exceeds the declared count or returns null.");var item=e.Current;if(own!=null)own(item);rows.Add(item);}throw Error("ENUMERATION_COUNT_MISMATCH","Enumeration did not terminate.");}
 }
}
