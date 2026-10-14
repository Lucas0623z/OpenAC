// SPDX-License-Identifier: MIT
// Fixed SCI interface facts, independently implemented. No model heuristics or raw command API.
using System;
using System.Collections.Generic;
using System.Linq;

namespace RogControlNotebook {
 public sealed class NotebookControlSpec {
  public readonly string Name,Caption,Unit; public readonly uint Endpoint; public readonly int Minimum,Maximum; public readonly bool NotebookOnly,CanConfirm;
  internal NotebookControlSpec(string name,string caption,string unit,uint endpoint,int min,int max,bool notebookOnly,bool confirm){Name=name;Caption=caption;Unit=unit;Endpoint=endpoint;Minimum=min;Maximum=max;NotebookOnly=notebookOnly;CanConfirm=confirm;}
  public bool LegalValue(object value){return value is int&&(int)value>=Minimum&&(int)value<=Maximum;}
  public uint Parameter(int value){if(!LegalValue(value))throw new ArgumentOutOfRangeException("value");return Name=="keyboardBrightness"?(uint)(0x80|value):(uint)value;}
 }
 public sealed class NotebookControlStatus {
  public uint Raw;public bool Presence,Unsupported;public int? QueryValue,CurrentValue;public string Error;
 }
 public static class NotebookControlRules {
  static readonly NotebookControlSpec[] specs={
   new NotebookControlSpec("performanceMode","运行模式","code",0x00120075,0,2,false,false),
   new NotebookControlSpec("chargeLimit","充电上限","percent",0x00120057,40,100,false,false),
   new NotebookControlSpec("keyboardBrightness","键盘亮度","level",0x00050021,0,3,true,true),
   new NotebookControlSpec("displayOverdrive","面板 Overdrive","code",0x00050019,0,1,true,false),
   new NotebookControlSpec("gpuEco","独立显卡","code",0x00090020,0,1,true,false),
   new NotebookControlSpec("gpuMux","显卡 MUX","code",0x00090016,0,1,true,false)
  };
  public const uint OverdriveSupport=0x00050020,XgEnabled=0x00090019,ExternalDisplay=0x00090030;
  public static NotebookControlSpec[] Specs {get{return (NotebookControlSpec[])specs.Clone();}}
  public static NotebookControlSpec Find(string name){return specs.FirstOrDefault(s=>s.Name==name);}
  public static string Family(string code){switch(code){case "RC71L":case "RC72LA":case "RC73YA":case "RC73XA":return "handheld";case "FX507ZC4":case "FA507NV":return "tufNotebook";case "GA402RJ":case "GA403UI":case "GU605MI":case "GU605MZ":return "rogNotebook";default:return null;}}
  public static bool Token(string value){return value!=null&&value.Length==32&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
  public static bool IdentityToken(string value){return value!=null&&value.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
  public static bool SupportEndpoint(uint endpoint){return specs.Any(s=>s.Endpoint==endpoint)||endpoint==OverdriveSupport||endpoint==XgEnabled||endpoint==ExternalDisplay;}
  public static byte[] Packet(uint endpoint,uint parameter){var spec=specs.FirstOrDefault(s=>s.Endpoint==endpoint);if(spec==null||parameter>Int32.MaxValue)throw new ArgumentException("Control endpoint is not prepared.");int value=spec.Name=="keyboardBrightness"?(int)parameter-0x80:(int)parameter;if(!spec.LegalValue(value)||spec.Parameter(value)!=parameter)throw new ArgumentException("Control value is not prepared.");var bytes=new byte[16];Put(bytes,0,0x53564544);Put(bytes,4,8);Put(bytes,8,endpoint);Put(bytes,12,parameter);return bytes;}
  static void Put(byte[] data,int offset,uint value){for(int i=0;i<4;i++)data[offset+i]=(byte)(value>>(8*i));}
  public static NotebookControlStatus Decode(uint endpoint,byte[] bytes){
   var status=new NotebookControlStatus();if(!SupportEndpoint(endpoint)||bytes==null||bytes.Length<4||bytes.Length>16){status.Error="INVALID_STATUS";return status;}
   uint raw=(uint)bytes[0]|((uint)bytes[1]<<8)|((uint)bytes[2]<<16)|((uint)bytes[3]<<24);status.Raw=raw;
   if(raw==0xFFFFFFFE||raw==0xFFFFFFFF){status.Unsupported=true;status.Error="ENDPOINT_UNSUPPORTED";return status;}
   if((raw&0x10000)==0||(raw&0xFFF80000)!=0){status.Error="PRESENCE_UNCONFIRMED";return status;}status.Presence=true;int query=(int)(raw&0xFFFF);
   var spec=specs.FirstOrDefault(s=>s.Endpoint==endpoint);
   if(spec!=null&&spec.Name=="keyboardBrightness"){
    // Level excludes bit 7 (backlight on); upper byte is environment/maximum metadata.
    query=(int)(raw&0x7F);if(query>3||(raw&0x00020000)!=0||(raw&0xFF00)>0x0300){status.Error="BRIGHTNESS_UNKNOWN";return status;}status.CurrentValue=query;
   }else if(endpoint==ExternalDisplay){if(query>255){status.Error="DISPLAY_STATUS_UNKNOWN";return status;}}
   else{int maximum=spec==null?1:spec.Name=="performanceMode"?4:spec.Name=="chargeLimit"?100:spec.Maximum;if(query>maximum){status.Error="STATUS_RANGE_UNKNOWN";return status;}}
   status.QueryValue=query;return status;
  }
  static bool Known(Dictionary<uint,NotebookControlStatus> states,uint endpoint,int value){NotebookControlStatus state;return states.TryGetValue(endpoint,out state)&&state.Presence&&state.Error==null&&state.QueryValue==value;}
  static bool AbsentOrOff(Dictionary<uint,NotebookControlStatus> states,uint endpoint){NotebookControlStatus state;return states.TryGetValue(endpoint,out state)&&(state.Unsupported||state.Presence&&state.Error==null&&state.QueryValue==0);}
  public static int[] AllowedValues(NotebookControlSpec spec,string family,bool? acOnline,Dictionary<uint,NotebookControlStatus> states){
   NotebookControlStatus state;if(spec==null||FamilyCode(family)==false||spec.NotebookOnly&&family=="handheld"||!states.TryGetValue(spec.Endpoint,out state)||!state.Presence||state.Error!=null||!state.QueryValue.HasValue)return new int[0];
   if(spec.Name=="displayOverdrive"&&!Known(states,OverdriveSupport,1))return new int[0];
   var values=Enumerable.Range(spec.Minimum,spec.Maximum-spec.Minimum+1).ToList();
   if(spec.Name=="performanceMode"&&acOnline!=true)values.Remove(1);
   if(spec.Name=="gpuEco"&&(!Known(states,0x00090016,1)||!Known(states,ExternalDisplay,0)||!AbsentOrOff(states,XgEnabled)))values.Remove(1);
   if(spec.Name=="gpuMux"&&(!Known(states,0x00090020,0)||!Known(states,ExternalDisplay,0)||!AbsentOrOff(states,XgEnabled)))values.Remove(0);
   return values.ToArray();
  }
  static bool FamilyCode(string family){return family=="handheld"||family=="rogNotebook"||family=="tufNotebook";}
 }
}
