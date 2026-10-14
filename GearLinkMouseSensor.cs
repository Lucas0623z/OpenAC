// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independent implementation of exact p725 wire facts. No manufacturer source.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal static partial class GearLinkWorker {
 internal static bool MouseSensorModel(int pid){return pid==7546||pid==7548;}
 // The installed p725 desktop page has 24 choices including Off, not IDs 0..23.
 internal static readonly int[] MouseSensorPresetIds={255,28,27,26,24,23,25,20,21,22,18,19,12,13,11,10,7,8,14,0,1,15,16,17};
 internal static bool MouseSensorSetting(string setting){return new[]{"sensorFrameRate","mouseCalibrationMode","mouseCalibrationPreset","mouseCalibrationLOD","mouseTrackOnGlass"}.Contains(setting);}
 internal static int SensorInt(object value,int min,int max){if(!(value is int)||(int)value<min||(int)value>max)throw new ArgumentException("An in-range integer is required for this mouse sensor field.");return (int)value;}
 internal static void MouseSensorHeader(int pid,byte[] data,byte key,ushort index,int length){if(!MouseSensorModel(pid))throw new ArgumentException("Sensor protocol is restricted to exact HARPE II EXTREME EDITION20 PID 7546/7548.");Require(data,length);if(data[0]!=0x12||data[1]!=key||U16(data,2)!=index)throw new IOException("Mouse sensor response does not match the exact query header.");}
 internal static Dictionary<string,object> ParseMouseSensorSettings(int pid,byte[] data){MouseSensorHeader(pid,data,4,5,6);if(data[4]>4||data[5]>1)throw new IOException("Unknown sensor mode or frame-rate code.");return Obj("sensorMode",(int)data[4],"fpsCode",(int)data[5],"sensorFrameRate",data[5]==0?13000:20000);}
 internal static Dictionary<string,object> ParseMouseCalibrationInfo(int pid,byte[] data){MouseSensorHeader(pid,data,6,0,9);if(data[4]!=0&&data[4]!=255||data[5]>28&&data[5]!=255)throw new IOException("Unknown p725 calibration mode or material.");return Obj("mode",(int)data[4],"material",(int)data[5],"kValue",data.Skip(6).Take(3).Select(b=>(int)b).ToArray());}
 internal static Dictionary<string,object> ParseMouseCalibrationLod(int pid,byte[] data){MouseSensorHeader(pid,data,6,1,10);if(data[4]>2||data[5]>2||data[6]>11||data[7]>11||data[8]>1||data[9]>11)throw new IOException("Unknown native calibration LOD field.");return Obj("lodMode",(int)data[4],"normalModeLOD",(int)data[5],"asymmetricLiftValue",(int)data[6],"asymmetricDropValue",(int)data[7],"sensorTriggered",data[8]==1,"manualCalibrationLOD",(int)data[9],"manualLevelInitialized",data[9]>=1);}
 internal static bool ParseMouseGlassTracking(int pid,byte[] data){MouseSensorHeader(pid,data,6,2,5);if(data[4]>1)throw new IOException("Unknown glass-tracking switch.");return data[4]==1;}
 internal static void ReadMouseSensorFeatures(GearHid hid,byte report,int pid,Dictionary<string,object> snapshot,List<string> caps,List<object> errors){
  snapshot["mouseSensorSettings"]=null;snapshot["mouseCalibrationInfo"]=null;snapshot["mouseCalibrationLOD"]=null;snapshot["mouseTrackOnGlass"]=null;snapshot["sensorFrameRate"]=null;
  snapshot["manualCalibrationWorkflowSupported"]=false;
  if(!MouseSensorModel(pid))return;
  QueryField(hid,report,Packet(0x12,4,5),"mouseSensorSettings",errors,b=>{var value=ParseMouseSensorSettings(pid,b);snapshot["mouseSensorSettings"]=value;snapshot["sensorFrameRate"]=value["sensorFrameRate"];caps.Add("mouseSensorSettings");});
  QueryField(hid,report,Packet(0x12,6,0),"mouseCalibrationInfo",errors,b=>{snapshot["mouseCalibrationInfo"]=ParseMouseCalibrationInfo(pid,b);caps.Add("mouseCalibrationInfo");});
  QueryField(hid,report,Packet(0x12,6,1),"mouseCalibrationLOD",errors,b=>{snapshot["mouseCalibrationLOD"]=ParseMouseCalibrationLod(pid,b);caps.Add("mouseCalibrationLOD");});
  QueryField(hid,report,Packet(0x12,6,2),"mouseTrackOnGlass",errors,b=>{snapshot["mouseTrackOnGlass"]=ParseMouseGlassTracking(pid,b);caps.Add("mouseTrackOnGlass");});
 }
 internal static Dictionary<string,object> SensorMap(Dictionary<string,object> snapshot,string name){object value;return snapshot!=null&&snapshot.TryGetValue(name,out value)?value as Dictionary<string,object>:null;}
 internal static int[] SensorBytes(object value){var array=value as IEnumerable;if(array==null||value is string)throw new ArgumentException("Calibration K must contain exactly three bytes.");var bytes=new List<int>();foreach(object item in array){if(bytes.Count==3)throw new ArgumentException("Calibration K must contain exactly three bytes.");bytes.Add(SensorInt(item,0,255));}if(bytes.Count!=3)throw new ArgumentException("Calibration K must contain exactly three bytes.");return bytes.ToArray();}
 internal static Dictionary<string,object> RequireMouseSensorSettings(Dictionary<string,object> snapshot){var map=SensorMap(snapshot,"mouseSensorSettings");if(map==null)throw new ArgumentException("Sensor mode and frame rate have not been completely read.");SensorInt(map["sensorMode"],0,4);int code=SensorInt(map["fpsCode"],0,1),rate=SensorInt(map["sensorFrameRate"],13000,20000);if(rate!=(code==0?13000:20000))throw new ArgumentException("Sensor frame rate disagrees with its wire code.");return map;}
 internal static Dictionary<string,object> RequireMouseCalibrationInfo(Dictionary<string,object> snapshot){var map=SensorMap(snapshot,"mouseCalibrationInfo");if(map==null)throw new ArgumentException("Calibration mode, material and K have not been completely read.");int mode=SensorInt(map["mode"],0,255),material=SensorInt(map["material"],0,255);if(mode!=0&&mode!=255||material>28&&material!=255)throw new ArgumentException("Unknown exact-model calibration record.");SensorBytes(map["kValue"]);return map;}
 internal static Dictionary<string,object> RequireMouseCalibrationLod(Dictionary<string,object> snapshot){var map=SensorMap(snapshot,"mouseCalibrationLOD");if(map==null)throw new ArgumentException("The full native LOD record has not been read.");foreach(string key in new[]{"lodMode","normalModeLOD"})SensorInt(map[key],0,2);foreach(string key in new[]{"asymmetricLiftValue","asymmetricDropValue","manualCalibrationLOD"})SensorInt(map[key],0,11);if(!(map["sensorTriggered"] is bool))throw new ArgumentException("Sensor trigger state has not been read.");return map;}
 internal static void RequireMouseSensorIdentity(int pid,Dictionary<string,object> snapshot){object model;if(!MouseSensorModel(pid)||snapshot==null||!snapshot.TryGetValue("modelPid",out model)||!(model is int)||(int)model!=pid)throw new ArgumentException("The exact current p725 sensor model is not confirmed.");}
 internal static bool MouseSensorReady(Func<object> read){try{read();return true;}catch(Exception){return false;}}
 internal static void AddMouseSensorSetCapabilities(bool online,int pid,Dictionary<string,object> state,Dictionary<string,object> snapshot,List<string> caps){
  if(!MouseSensorModel(pid))return;
  state["mouseSensorSettingOptions"]=Obj("sensorFrameRates",new[]{13000,20000},"frameRateMeaning","sensorSamplingFramesPerSecond","presetSurfaces",MouseSensorPresetIds.Select(id=>(object)Obj("material",id,"name",MouseSensorPresetName(id))).ToArray(),"calibrationModes",new[]{0,255},"lodModes",new[]{0,1,2},"normalLodLevels",new[]{0,1,2},"nativeLodLevelMin",1,"nativeLodLevelMax",11,"automaticStartSupported",false);
  if(!online||!MouseSensorReady(()=>{RequireMouseSensorIdentity(pid,snapshot);return snapshot;}))return;
  object previous;var prior=state.TryGetValue("setCapabilities",out previous)?previous as IEnumerable:null;var setters=prior==null?new List<string>():prior.Cast<object>().OfType<string>().ToList();
  if(caps.Contains("mouseSensorSettings")&&MouseSensorReady(()=>RequireMouseSensorSettings(snapshot)))setters.Add("sensorFrameRate");
  if(caps.Contains("mouseCalibrationInfo")&&MouseSensorReady(()=>RequireMouseCalibrationInfo(snapshot))){setters.Add("mouseCalibrationMode");setters.Add("mouseCalibrationPreset");if((int)RequireMouseCalibrationInfo(snapshot)["mode"]==255&&caps.Contains("mouseCalibrationLOD")&&MouseSensorReady(()=>RequireMouseCalibrationLod(snapshot)))setters.Add("mouseCalibrationLOD");}
  object glass;if(caps.Contains("mouseTrackOnGlass")&&snapshot.TryGetValue("mouseTrackOnGlass",out glass)&&glass is bool)setters.Add("mouseTrackOnGlass");
  state["setCapabilities"]=setters.Distinct().ToArray();
 }
 // Functional preset bytes are independently recorded wire facts, grouped by
 // equal values. Manufacturer UI tables, images and JavaScript are not copied.
 internal static int[] MouseSensorPresetK(int material){
  if(!MouseSensorPresetIds.Contains(material))throw new ArgumentException("Preset material is not in the exact 24-choice p725 catalogue.");
  switch(material){case 255:return new[]{0,0,0};case 10:case 11:return new[]{26,50,32};case 20:return new[]{55,43,14};case 21:return new[]{24,53,35};case 23:return new[]{28,66,36};case 24:return new[]{30,52,25};case 25:return new[]{33,35,18};case 26:case 28:return new[]{30,61,18};case 27:return new[]{29,36,14};default:return new[]{28,52,22};}
 }
 internal static string MouseSensorPresetName(int material){switch(material){case 255:return "默认（无校准）";case 28:return "ROG Scabbard II XXL Edition 20";case 27:return "ROG Hone Control Ace L Vitality Edition";case 26:return "ROG Scabbard II XXL-KJP";case 24:return "TUF Gaming P1 Hatsune Miku Edition";case 23:return "ROG Scabbard II Arctic Grey XXL";case 25:return "ROG Sheath II XXL";case 20:return "ROG Moonstone Ace L · White";case 21:return "ROG Moonstone Ace L";case 22:return "ROG Hone Ace XXL";case 18:return "ROG Hone Ace Aim Lab Edition";case 19:return "ROG Scabbard II EVA Edition";case 12:return "ROG Scabbard II";case 13:return "ROG Sheath Gundam LTD";case 11:return "Balteus QI";case 10:return "Balteus";case 7:return "ROG Scabbard";case 8:return "ROG Strix Edge";case 14:return "ROG Strix Edge Call of Duty - Black Ops 4 Edition";case 0:return "ROG Whetstone";case 1:return "ROG Sheath";case 15:return "ROG Sheath BLK LTD";case 16:return "ROG Sheath PNK LTD";case 17:return "ROG Sheath Electro Punk";default:throw new ArgumentException("Unknown exact p725 preset material.");}}
 internal static Dictionary<string,object> ValidateMouseLodValue(object value){var record=ValueObject(value,"lodMode","normalModeLOD","asymmetricLiftValue","asymmetricDropValue","manualCalibrationLOD");int mode=SensorInt(record["lodMode"],0,2);SensorInt(record["normalModeLOD"],0,2);int lift=SensorInt(record["asymmetricLiftValue"],0,11),drop=SensorInt(record["asymmetricDropValue"],0,11),manual=SensorInt(record["manualCalibrationLOD"],0,11);if(mode==1&&(lift<1||drop<1||lift<drop))throw new ArgumentException("Asymmetric native levels must be 1..11 and lift must be at least drop.");if(mode==2&&manual<1)throw new ArgumentException("Manual native LOD level must be 1..11; zero means not initialized.");return record;}
 internal static byte[] BuildMouseSensorSetting(string setting,object value,Dictionary<string,object> snapshot,int pid){
  if(!MouseSensorSetting(setting))return null;RequireMouseSensorIdentity(pid,snapshot);
  if(setting=="sensorFrameRate"){var before=RequireMouseSensorSettings(snapshot);int rate=SensorInt(value,13000,20000);if(rate!=13000&&rate!=20000)throw new ArgumentException("Sensor frame rate must be 13000 or 20000; this is not game FPS.");return Packet(0x51,49,19,(byte)(int)before["sensorMode"],rate==13000?(byte)0:(byte)1);}
  if(setting=="mouseTrackOnGlass"){object prior;if(!snapshot.TryGetValue(setting,out prior)||!(prior is bool))throw new ArgumentException("Glass-tracking baseline is unknown.");return Packet(0x51,53,2,BooleanByte(value));}
  var calibration=RequireMouseCalibrationInfo(snapshot);
  if(setting=="mouseCalibrationMode"){int mode=SensorInt(value,0,255);if(mode!=0&&mode!=255)throw new ArgumentException("Calibration mode must be Preset0 or LOD255.");var k=SensorBytes(calibration["kValue"]);return Packet(0x51,53,0,(byte)mode,(byte)(int)calibration["material"],(byte)k[0],(byte)k[1],(byte)k[2]);}
  if(setting=="mouseCalibrationPreset"){int material=SensorInt(value,0,255);var k=MouseSensorPresetK(material);return Packet(0x51,53,0,0,(byte)material,(byte)k[0],(byte)k[1],(byte)k[2]);}
  if((int)calibration["mode"]!=255)throw new ArgumentException("Read back LOD calibration mode before changing LOD values.");RequireMouseCalibrationLod(snapshot);var input=ValidateMouseLodValue(value);return Packet(0x51,53,1,(byte)(int)input["lodMode"],(byte)(int)input["normalModeLOD"],(byte)(int)input["asymmetricLiftValue"],(byte)(int)input["asymmetricDropValue"],0,(byte)(int)input["manualCalibrationLOD"]);
 }
 internal static bool? MatchesMouseSensorSetting(string setting,object value,Dictionary<string,object> snapshot){
  if(!MouseSensorSetting(setting))return null;
  try{object model;if(!snapshot.TryGetValue("modelPid",out model)||!(model is int)||!MouseSensorModel((int)model))return false;
   if(setting=="sensorFrameRate")return (int)RequireMouseSensorSettings(snapshot)["sensorFrameRate"]==SensorInt(value,13000,20000);
   if(setting=="mouseTrackOnGlass"){object current;return value is bool&&snapshot.TryGetValue(setting,out current)&&current is bool&&(bool)current==(bool)value;}
   var info=RequireMouseCalibrationInfo(snapshot);
   if(setting=="mouseCalibrationMode")return (int)info["mode"]==SensorInt(value,0,255);
   if(setting=="mouseCalibrationPreset"){int material=SensorInt(value,0,255);return (int)info["mode"]==0&&(int)info["material"]==material&&SensorBytes(info["kValue"]).SequenceEqual(MouseSensorPresetK(material));}
   var input=ValidateMouseLodValue(value);var currentLod=RequireMouseCalibrationLod(snapshot);return (int)info["mode"]==255&&input.All(pair=>currentLod.ContainsKey(pair.Key)&&currentLod[pair.Key] is int&&(int)currentLod[pair.Key]==(int)pair.Value);
  }catch(Exception){return false;}
 }
 internal static bool IsMouseSensorQuery(int pid,byte[] packet){return MouseSensorModel(pid)&&packet!=null&&packet.Length==4&&packet[0]==0x12&&(packet[1]==4&&U16(packet,2)==5||packet[1]==6&&U16(packet,2)<=2);}
 internal static bool IsMouseSensorWrite(int pid,byte[] packet){
  if(!MouseSensorModel(pid)||packet==null||packet.Length<5||packet[0]!=0x51||packet[3]!=0)return false;
  if(packet[1]==49&&packet[2]==19)return packet.Length==6&&packet[4]<=4&&packet[5]<=1;
  if(packet[1]!=53)return false;
  if(packet[2]==0)return packet.Length==9&&(packet[4]==0||packet[4]==255)&&(packet[5]<=28||packet[5]==255);
  if(packet[2]==2)return packet.Length==5&&packet[4]<=1;
  if(packet[2]!=1||packet.Length!=10||packet[8]!=0)return false;
  try{ValidateMouseLodValue(Obj("lodMode",(int)packet[4],"normalModeLOD",(int)packet[5],"asymmetricLiftValue",(int)packet[6],"asymmetricDropValue",(int)packet[7],"manualCalibrationLOD",(int)packet[9]));return true;}catch(ArgumentException){return false;}
 }
}
