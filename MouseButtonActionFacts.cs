// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independently authored action facts and pure packet validation.
// Standard keyboard meanings: https://www.usb.org/hid
// Shift behavior: https://www.asus.com/us/support/faq/1056607/
// P725's 1.00.15 view does not enable supportsFwCombineKey. Generic chord
// methods in a shared library alone do not establish model write support.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RogControl {
 public static class MouseButtonActionFacts {
  static readonly string[] Sources={"leftButton","rightButton","middleButton","sideKeyLeftBackward","sideKeyLeftForward","dpi1","scrollUp","scrollDown"};
  static readonly int[] SourceCodes={496,497,498,484,485,486,488,489};
  static readonly string[] MouseActions={"MouseClickLeft","MouseClickRight","MouseClickMiddle","MouseScrollUp","MouseScrollDown","MouseDPISwitch","MouseLeftBackward","MouseLeftForward","MouseTargetFocus","MouseDoubleClick"};
  static readonly string[] MediaActions={"MultimediaVolumeUp","MultimediaVolumeDown","MultimediaMute","MultimediaPlayPause","MultimediaPrevTrack","MultimediaNextTrack","MultimediaStop"};
  static readonly Dictionary<string,int> KnownCodes=CreateSimpleTargetMap();
  public static bool PreparedModel(int pid){return pid==7546||pid==7548;}
  public static bool FirmwareChordPreparedModel(int pid){return false;}
  public static string[] Targets(string category){
   if(category=="mouse")return (string[])MouseActions.Clone();if(category=="media")return (string[])MediaActions.Clone();if(category=="speedshift")return new[]{"ShiftMode"};
   if(category!="keyboard")return new string[0];var keys=new List<string>();for(char key='A';key<='Z';key++)keys.Add("Key"+key);for(int number=0;number<=9;number++)keys.Add("Digit"+number);for(int f=1;f<=12;f++)keys.Add("F"+f);keys.AddRange(new[]{"Enter","Escape","Backspace","Tab","Space","ArrowRight","ArrowLeft","ArrowDown","ArrowUp","LeftCtrl","LeftShift","LeftAlt","LeftWin","RightCtrl","RightShift","RightAlt","RightWin"});return keys.ToArray();
  }
  public static Dictionary<string,int> SimpleTargetMap(){return new Dictionary<string,int>(KnownCodes,StringComparer.Ordinal);}
  static Dictionary<string,int> CreateSimpleTargetMap(){
   var map=new Dictionary<string,int>(StringComparer.Ordinal);for(char key='A';key<='Z';key++)map.Add("Key"+key,4+key-'A');for(int number=0;number<=9;number++)map.Add("Digit"+number,number==0?39:29+number);for(int f=1;f<=12;f++)map.Add("F"+f,57+f);
   string[] names={"Enter","Escape","Backspace","Tab","Space","ArrowRight","ArrowLeft","ArrowDown","ArrowUp","LeftCtrl","LeftShift","LeftAlt","LeftWin","RightCtrl","RightShift","RightAlt","RightWin","MultimediaPlayPause","MultimediaStop","MultimediaPrevTrack","MultimediaNextTrack","MultimediaMute","MultimediaVolumeDown","MultimediaVolumeUp","MouseProfile","MouseDoubleClick","MouseLeftBackward","MouseLeftForward","MouseDPISwitch","MouseTargetFocus","MouseScrollUp","MouseScrollDown","MouseClickLeft","MouseClickRight","MouseClickMiddle","MouseDpiUp","MouseDpiDown","KeyDisabled","ShiftMode"};
   int[] codes={40,41,42,43,44,79,80,81,82,224,225,226,227,228,229,230,231,240,241,242,243,244,245,246,480,483,484,485,486,487,488,489,496,497,498,499,500,511,253};for(int i=0;i<names.Length;i++)map.Add(names[i],codes[i]);return map;
  }
  public static int? Code(string target){int value;return target!=null&&KnownCodes.TryGetValue(target,out value)?(int?)value:null;}
  public static string Category(string target){foreach(string category in new[]{"mouse","keyboard","media","speedshift"})if(Targets(category).Contains(target,StringComparer.Ordinal))return category;return target!=null&&(target.StartsWith("Mouse",StringComparison.Ordinal)||target=="KeyDisabled")&&Code(target).HasValue?"mouse":"unknown";}
  public static string Caption(string target){
   if(target!=null&&target.Length==4&&target.StartsWith("Key",StringComparison.Ordinal)&&target[3]>='A'&&target[3]<='Z')return target.Substring(3);if(target!=null&&target.Length==6&&target.StartsWith("Digit",StringComparison.Ordinal)&&target[5]>='0'&&target[5]<='9')return target.Substring(5);int f;if(target!=null&&target.StartsWith("F",StringComparison.Ordinal)&&Int32.TryParse(target.Substring(1),NumberStyles.None,CultureInfo.InvariantCulture,out f)&&f>=1&&f<=12&&target=="F"+f)return target;
   switch(target){case "MouseClickLeft":return "左键";case "MouseClickRight":return "右键";case "MouseClickMiddle":return "滚轮点击";case "MouseScrollUp":return "向上滚动";case "MouseScrollDown":return "向下滚动";case "MouseDPISwitch":return "DPI 切换";case "MouseLeftBackward":return "后退";case "MouseLeftForward":return "前进";case "MouseTargetFocus":return "狙击锁定";case "MouseDoubleClick":return "双击";case "MultimediaVolumeUp":return "提高音量";case "MultimediaVolumeDown":return "降低音量";case "MultimediaMute":return "关闭声音";case "MultimediaPlayPause":return "播放/暂停";case "MultimediaPrevTrack":return "上一首";case "MultimediaNextTrack":return "下一首";case "MultimediaStop":return "停止";case "ShiftMode":return "speed shift";case "Enter":return "Enter";case "Escape":return "Esc";case "Backspace":return "Backspace";case "Tab":return "Tab";case "Space":return "Space";case "ArrowRight":return "→";case "ArrowLeft":return "←";case "ArrowDown":return "↓";case "ArrowUp":return "↑";case "LeftCtrl":return "L-Ctrl";case "LeftShift":return "L-Shift";case "LeftAlt":return "L-Alt";case "LeftWin":return "L-Win";case "RightCtrl":return "R-Ctrl";case "RightShift":return "R-Shift";case "RightAlt":return "R-Alt";case "RightWin":return "R-Win";default:return "—";}
  }
  static Dictionary<string,object> Bank(IDictionary<string,object> snapshot,bool shift){object value;return snapshot!=null&&snapshot.TryGetValue(shift?"shiftButtonBindings":"buttonBindings",out value)?value as Dictionary<string,object>:null;}
  static int? RawCode(object value){var binding=value as IDictionary<string,object>;object raw;return binding!=null&&binding.TryGetValue("targetCode",out raw)&&raw is int&&(int)raw>=0&&(int)raw<=65535?(int?)raw:null;}
  public static bool CompleteBank(IDictionary<string,object> snapshot,bool shift){var bank=Bank(snapshot,shift);if(bank==null||bank.Count!=Sources.Length)return false;foreach(string source in Sources){object value;if(!bank.TryGetValue(source,out value)||!RawCode(value).HasValue)return false;}return true;}
  public static bool TargetAllowed(IDictionary<string,object> snapshot,string source,string target,bool shift){
   if(!Sources.Contains(source,StringComparer.Ordinal)||!Targets(Category(target)).Contains(target,StringComparer.Ordinal)||!CompleteBank(snapshot,shift))return false;
   if(target=="MouseTargetFocus"&&(source=="scrollUp"||source=="scrollDown"))return false;
   if(target!="ShiftMode")return true;if(shift||source=="leftButton"||source=="scrollUp"||source=="scrollDown")return false;
   var bank=Bank(snapshot,false);foreach(var item in bank)if(item.Key!=source&&RawCode(item.Value)==253)return false;return true;
  }
  public static bool TrySimplePacket(int modelPid,IDictionary<string,object> snapshot,string source,string target,bool shift,out byte[] packet,out bool alreadySet){
   packet=null;alreadySet=false;if(!PreparedModel(modelPid)||!TargetAllowed(snapshot,source,target,shift))return false;var bank=Bank(snapshot,shift);var current=bank[source] as IDictionary<string,object>;object observedName;int? before=RawCode(current),wanted=Code(target);if(current==null||!current.TryGetValue("target",out observedName)||!(observedName is string)||Code((string)observedName)!=before||!wanted.HasValue)return false;
   if(before==wanted){alreadySet=true;return true;}int index=Array.IndexOf(Sources,source),physical=SourceCodes[index],value=wanted.Value;packet=new byte[]{0x51,shift?(byte)0x23:(byte)0x21,0,0,(byte)physical,(byte)(physical>>8),(byte)value,(byte)(value>>8)};return true;
  }
  public static bool? MatchesSimple(IDictionary<string,object> snapshot,string source,string target,bool shift){if(!Sources.Contains(source,StringComparer.Ordinal)||!CompleteBank(snapshot,shift))return null;var bank=Bank(snapshot,shift);int? code=RawCode(bank[source]),wanted=Code(target);return code.HasValue&&wanted.HasValue?(bool?)(code==wanted):null;}
 }
}
