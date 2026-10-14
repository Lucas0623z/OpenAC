// Copyright (c) OpenAC contributors. SPDX-License-Identifier: MIT
// Independent implementation of documented limited, read-only process identity.
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenAcFanNative {
 public interface IFanNativeProcessImageApi {
  IntPtr Open(uint access,bool inherit,uint pid);
  bool Query(IntPtr handle,uint flags,StringBuilder name,ref uint characters);
  int LastError{get;}
  void Close(IntPtr handle);
 }
 public static class FanNativeProcessImage {
  public const uint QueryLimitedInformation=0x1000;
  public static string Read(int pid){return Read(pid,new WindowsProcessImageApi());}
  // Injectable only for pure tests; runtime receives the fixed service PID and
  // cannot choose another access mask, flags, handle or process via JSON.
  public static string Read(int pid,IFanNativeProcessImageApi api){if(pid<=0||api==null)throw FanNativeRules.Error("EXACT_SERVICE_NOT_RUNNING","A valid service PID and read source are required.");IntPtr handle=api.Open(QueryLimitedInformation,false,(uint)pid);if(handle==IntPtr.Zero||handle==new IntPtr(-1))throw new Win32Exception(api.LastError,"The service process image could not be queried with limited read access.");try{var name=new StringBuilder(32768);uint size=(uint)name.Capacity;if(!api.Query(handle,0,name,ref size))throw new Win32Exception(api.LastError,"The service process image could not be read.");if(size==0||size>32767||size!=name.Length||name.ToString().IndexOf('\0')>=0)throw FanNativeRules.Error("PROCESS_IMAGE_INVALID","The native process image response is malformed.");return name.ToString();}finally{api.Close(handle);}}
  sealed class WindowsProcessImageApi:IFanNativeProcessImageApi {
   int error;public int LastError{get{return error;}}
   [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)]static extern IntPtr OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint pid);
   [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool QueryFullProcessImageNameW(IntPtr process,uint flags,StringBuilder path,ref uint size);
   [DllImport("kernel32.dll",ExactSpelling=true,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool CloseHandle(IntPtr handle);
   public IntPtr Open(uint access,bool inherit,uint pid){IntPtr result=OpenProcess(access,inherit,pid);error=Marshal.GetLastWin32Error();return result;}
   public bool Query(IntPtr handle,uint flags,StringBuilder name,ref uint characters){bool result=QueryFullProcessImageNameW(handle,flags,name,ref characters);error=Marshal.GetLastWin32Error();return result;}
   public void Close(IntPtr handle){CloseHandle(handle);}
  }
 }
}
