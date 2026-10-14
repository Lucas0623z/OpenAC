// Copyright (c) OpenAC contributors. Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading;

namespace RogControl {
 // A process-local read receipt. It is deliberately absent from saved profiles
 // and preview state: restoring data cannot restore permission or a connection.
 public sealed class SharedWriteReadBinding {
  public readonly Dictionary<string,object> State;
  public readonly object Session;
  public readonly long Generation;
  public readonly string Identity,Scope,Stamp;
  readonly string identityField,scopeField;
  static string Text(IDictionary<string,object> state,string key){object raw;return state!=null&&state.TryGetValue(key,out raw)?raw as string:null;}
  public SharedWriteReadBinding(Dictionary<string,object> state,object session,long generation,string idField,string identity,string scopeKey,string scope) {
   if(state==null||session==null||String.IsNullOrWhiteSpace(identity)||String.IsNullOrWhiteSpace(scope)||Text(state,idField)!=identity||scopeKey!=null&&Text(state,scopeKey)!=scope||String.IsNullOrWhiteSpace(Text(state,"capturedAt")))throw new InvalidOperationException("读取连接未确认，请重新读取。");
   State=state;Session=session;Generation=generation;Identity=identity;Scope=scope;Stamp=Text(state,"capturedAt");identityField=idField;scopeField=scopeKey;
  }
  public bool Matches(Dictionary<string,object> state,object session,long generation) {
   return Object.ReferenceEquals(State,state)&&Object.ReferenceEquals(Session,session)&&Generation==generation&&Text(state,identityField)==Identity&&(scopeField==null||Text(state,scopeField)==Scope)&&Text(state,"capturedAt")==Stamp;
  }
  public void Demand(Dictionary<string,object> current,object currentSession,long generation,bool topologyReady,bool closing,bool render,bool auraBusy,bool operationActive,Action permission,Func<bool> supported,CancellationToken cancellation) {
   cancellation.ThrowIfCancellationRequested();if(permission==null)throw new InvalidOperationException("设备控制未授权。");permission();
   if(closing||render||auraBusy||!operationActive||!topologyReady||!Matches(current,currentSession,generation)||supported==null||!supported())throw new InvalidOperationException("设备控制或读取连接已改变，没有发送设置。");
  }
 }
}
