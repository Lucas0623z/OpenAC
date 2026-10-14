// SPDX-License-Identifier: MIT. Fixed synthetic records; no native transport.
using System;
using System.Collections.Generic;
using System.Linq;
public static class MouseProtocolIntegrationTests {
 static int checks;
 static void Check(bool value){if(!value)throw new Exception("Mouse integration case "+(checks+1));checks++;}
 static Dictionary<string,object> Snapshot(){return GearLinkWorker.Obj("modelPid",7548,"mouseSensorSettings",GearLinkWorker.Obj("sensorMode",2,"fpsCode",0,"sensorFrameRate",13000),"mouseCalibrationInfo",GearLinkWorker.Obj("mode",255,"material",20,"kValue",new[]{55,43,14}),"mouseCalibrationLOD",GearLinkWorker.Obj("lodMode",0,"normalModeLOD",1,"asymmetricLiftValue",0,"asymmetricDropValue",0,"manualCalibrationLOD",0,"sensorTriggered",false));}
 public static int Main(){try{
  Check(GearLinkWorker.CalibrationOperation("mouse","calibrationRead"));Check(GearLinkWorker.CalibrationOperation("mouse","calibrationSet"));foreach(string kind in new[]{"keyboard","headset","omni","unknown"})foreach(string op in new[]{"calibrationRead","calibrationSet"})Check(!GearLinkWorker.CalibrationOperation(kind,op));
  foreach(string op in new[]{"set","read","rawHid","sensorSet","calibrate","resume"})Check(!GearLinkWorker.CalibrationOperation("mouse",op));
  var before=Snapshot();var after=Snapshot();Check(GearLinkWorker.MatchesMouseSensorAfter("sensorFrameRate",13000,before,after));((Dictionary<string,object>)after["mouseSensorSettings"])["sensorMode"]=3;Check(!GearLinkWorker.MatchesMouseSensorAfter("sensorFrameRate",13000,before,after));
  after=Snapshot();Check(GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationMode",255,before,after));((Dictionary<string,object>)after["mouseCalibrationInfo"])["kValue"]=new[]{1,2,3};Check(!GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationMode",255,before,after));
  after=Snapshot();((Dictionary<string,object>)after["mouseCalibrationInfo"])["material"]=21;Check(!GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationMode",255,before,after));
  var request=GearLinkWorker.Obj("lodMode",2,"normalModeLOD",1,"asymmetricLiftValue",0,"asymmetricDropValue",0,"manualCalibrationLOD",5);after=Snapshot();var lod=(Dictionary<string,object>)after["mouseCalibrationLOD"];lod["lodMode"]=2;lod["manualCalibrationLOD"]=5;Check(GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationLOD",request,before,after));lod["normalModeLOD"]=2;Check(!GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationLOD",request,before,after));
  after=Snapshot();after["modelPid"]=7588;Check(!GearLinkWorker.MatchesMouseSensorAfter("mouseCalibrationMode",255,before,after));
  var bindings=GearLinkWorker.ParseMouseBindings(new byte[]{18,5,0,0,240,1,241,1,242,1,228,1,229,1,230,1,232,1,233,1});var state=GearLinkWorker.Obj("modelPid",7548,"buttonBindings",bindings);var button=GearLinkWorker.Obj("button","leftButton","target","KeyA","shift",false);Check(GearLinkWorker.BuildFeatureSetting("mouse","buttonBinding",button,state).SequenceEqual(new byte[]{81,33,0,0,240,1,4,0}));
  bindings.Remove("dpi1");bool rejected=false;try{GearLinkWorker.BuildFeatureSetting("mouse","buttonBinding",button,state);}catch(ArgumentException){rejected=true;}Check(rejected);Check(GearLinkWorker.MatchesFeatureSetting("buttonBinding",button,state)==false);
  Console.WriteLine(GearLinkWorker.Json.Serialize(GearLinkWorker.Obj("ok",true,"suite","mouse_protocol_integration","tests",checks,"hardwareAccess",false)));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
