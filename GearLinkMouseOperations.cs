// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

internal static partial class GearLinkWorker {
 // A fixed, minimal scope for the bounded manual-calibration transaction.
 // It accepts no caller-supplied HID packets and never enumerates all settings.
 internal static bool CalibrationOperation(string kind,string op){return kind=="mouse"&&(op=="calibrationRead"||op=="calibrationSet");}
 internal static Dictionary<string,object> ReadMouseCalibration(GearDescriptor descriptor){
  RequireMouseSensorIdentity(descriptor.Pid,Obj("modelPid",(int)descriptor.Pid));var state=descriptor.Metadata();var snapshot=Obj("modelPid",(int)descriptor.Pid);var errors=new List<object>();var caps=new List<string>();
  state["snapshot"]=snapshot;state["online"]=false;state["connected"]=true;state["readbackConfirmed"]=false;state["writeConfirmed"]=false;state["errors"]=errors;state["setCapabilities"]=new string[0];state["readCapabilities"]=caps;
  using(var hid=new GearHid(descriptor)){
   byte report=descriptor.SelectReport(null);byte[] alive=hid.Query(report,Packet(0x12,0,2));Require(alive,5);if(alive[4]>1)throw new IOException("Invalid current mouse connection state.");state["online"]=alive[4]==1;state["readbackConfirmed"]=true;if(alive[4]!=1)return state;
   QueryField(hid,report,Packet(0x12,6,0),"mouseCalibrationInfo",errors,b=>{snapshot["mouseCalibrationInfo"]=ParseMouseCalibrationInfo(descriptor.Pid,b);caps.Add("mouseCalibrationInfo");});
   QueryField(hid,report,Packet(0x12,6,1),"mouseCalibrationLOD",errors,b=>{snapshot["mouseCalibrationLOD"]=ParseMouseCalibrationLod(descriptor.Pid,b);caps.Add("mouseCalibrationLOD");});
  }
  AddMouseSensorSetCapabilities(true,descriptor.Pid,state,snapshot,caps);snapshot["manualCalibrationWorkflowSupported"]=caps.Contains("mouseCalibrationInfo")&&caps.Contains("mouseCalibrationLOD");return state;
 }
 internal static object HandleMouseCalibration(Dictionary<string,object> req,GearDescriptor target,string op){
  if(op=="calibrationRead")return ReadMouseCalibration(target);
  object flag;if(!req.TryGetValue("experimental",out flag)||!(flag is bool)||!(bool)flag)throw new ArgumentException("Device control must be explicitly enabled.");
  string setting=Text(req,"setting");if(setting!="mouseCalibrationMode"&&setting!="mouseCalibrationLOD")throw new ArgumentException("Only mode and LOD are available in the calibration transaction.");
  object value;if(!req.TryGetValue("value",out value))throw new ArgumentException("Setting value is required.");var before=ReadMouseCalibration(target);
  if(!(bool)before["online"]||!((string[])before["setCapabilities"]).Contains(setting))throw new ArgumentException("Current calibration state cannot be safely changed.");
  var beforeSnapshot=(Dictionary<string,object>)before["snapshot"];byte[] packet=BuildMouseSensorSetting(setting,value,beforeSnapshot,target.Pid);
  if(MatchesMouseSensorSetting(setting,value,beforeSnapshot)==true){before["writeAttempted"]=false;before["writeConfirmed"]=true;before["appliedState"]="alreadySet";return before;}
  object noOp;if(req.TryGetValue("requireNoOp",out noOp)&&noOp is bool&&(bool)noOp)throw new ArgumentException("Read-only no-op validation rejected a changed value.");
  SettingReceipt receipt;using(var hid=new GearHid(target))receipt=hid.WriteSetting(target.SelectReport(null),packet);
  var after=ReadMouseCalibration(target);var afterSnapshot=(Dictionary<string,object>)after["snapshot"];after["writeAttempted"]=true;after["accepted"]=receipt.OutputCompleted;after["settingAcknowledged"]=receipt.Acknowledged;after["settingRejected"]=receipt.Rejected;
  bool preserved=CalibrationInfoPreserved(beforeSnapshot,afterSnapshot);after["writeConfirmed"]=receipt.OutputCompleted&&!receipt.Rejected&&(bool)after["online"]&&preserved&&MatchesMouseSensorSetting(setting,value,afterSnapshot)==true;after["appliedState"]=(bool)after["writeConfirmed"]?"confirmed":"unconfirmed";return after;
 }
 internal static bool MatchesMouseSensorAfter(string setting,object value,Dictionary<string,object> before,Dictionary<string,object> after){
  if(MatchesMouseSensorSetting(setting,value,after)!=true)return false;
  try{if(setting=="sensorFrameRate")return (int)RequireMouseSensorSettings(before)["sensorMode"]==(int)RequireMouseSensorSettings(after)["sensorMode"];
   if(setting=="mouseCalibrationMode"||setting=="mouseCalibrationLOD")return CalibrationInfoPreserved(before,after);
   return true;}catch{return false;}
 }
 internal static bool CalibrationInfoPreserved(Dictionary<string,object> before,Dictionary<string,object> after){try{var a=RequireMouseCalibrationInfo(before);var b=RequireMouseCalibrationInfo(after);return (int)a["material"]==(int)b["material"]&&SensorBytes(a["kValue"]).SequenceEqual(SensorBytes(b["kValue"]));}catch{return false;}}
}
