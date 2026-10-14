# Exact-board Fan Xpert COM read worker

`FanNativeRules.cs`, `FanNativeTransport.cs`, `FanNativeProcessImage.cs`, `FanNativeWorker.cs` and `FanNativeTests.cs` are independent MIT source. No ASUS binary, JavaScript implementation, type-library resource, cache or driver is distributed. This worker adds an actual ordinary COM getter path. Root's ordinary-permission read after the limited process-query change was explicitly denied at preflight-process-image (Win32 code5), with zero activation/getters. A separately authorized bounded administrator extended read activated COM and reached the first CurrentFanCurve object after23 getters, then rejected ABI_MISMATCH at validate-interface:Curve. No complete snapshot or write confirmation is established by that partial read.

Build a separate **x86** Framework executable with `/main:OpenAcFanNative.FanNativeWorker`, `WorkerTextProtocol.cs`, `System.Management.dll` and `System.Web.Extensions.dll`. The application remains x64. The worker takes no command-line arguments and reads UTF-8 JSON lines. Building it or running pure tests does not activate COM.

```json
{"id":1,"op":"status"}
{"id":2,"op":"read","includeCurves":false}
{"id":3,"op":"read","includeCurves":true}
{"id":4,"op":"release"}
{"id":5,"op":"shutdown"}
```

`status`, `release` and `shutdown` perform no native read or activation. `scan` aliases `read`. Only `id`, `op` and a Boolean `includeCurves` on read/scan are accepted. There is no caller-selectable path, board, COM class, IID, member, function, XML or native command. `set`/`apply` reject `WRITE_DISABLED` before constructing a transport. A read failure is one attempt with no retry, privilege escalation, service restart, vendor initialization, calibration or alternate transport.

Each request has a 10-second process watchdog covering preflight, COM activation, collection, disposal and output. The parent must also supervise process start, input/output and exit with a finite independent deadline (12–15 seconds is suitable for this worker). A stuck getter or release terminates the child; it is not retried. The parent must discard any incomplete or failed response. Elevated operation, if separately authorized, is the responsibility of the normal Windows parent launch workflow; this worker never displays UAC or changes permissions.

## Identity and ABI gates

Fresh preflight requires the exact independently read ASUS board `ROG CROSSHAIR X870E EXTREME`, x86 worker, fixed installed `AsusFanControlService` version `2.0.05.06` and SHA256 `c2959edb65eb1104f1522fad15b55f4927415edd23ab552d9be74be656f7a977`. The full fixed service path and every parent must have no reparse point. A read lock prevents modification or replacement of that binary during the read.

Authenticode must validate using Windows trust policy with no UI, cache-only revocation-chain checking excluding the root, and the exact ASUSTeK signer name. Missing cached revocation information or other trust failure rejects the read; there is no trust downgrade or network fetch. This is an offline Windows trust decision plus a pinned artifact, not a promise of globally fresh revocation information. [Microsoft Windows trust structure](https://learn.microsoft.com/windows/win32/api/wintrust/ns-wintrust-wintrust_data).

The fixed 32-bit CLSID, AppID, TypeLib/version, LocalServer32 and LocalService registration must match the reviewed installed server. The exact service must already be Running with nonzero PID and matching actual process image. Image verification uses `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` and `QueryFullProcessImageNameW(flags=0)` rather than process-module enumeration or VM-read access. The exact returned image still must match the pinned service binary; failure never skips that gate. After collection the worker rechecks the same service PID/state/image and rejects a replaced service. No explicit service-start operation exists. Normal COM activation can have vendor constructor effects; a service stopping between preflight and activation can also make COM attempt normal activation. This worker cannot guarantee that a third-party constructor or that race has no internal side effects. [Microsoft process-image query](https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew).

Failure diagnostics distinguish architecture, artifact path/hash/version, signature trust/signer, registration, board, service-state and process-image phases, followed by fixed COM ABI/getter phases. They carry exception type and Win32 NativeErrorCode when present, alongside activated/getter-call counts and HRESULT. Interface-validation failures additionally carry expected role/IID and observed type GUID/kind/function/typeinfo counts; recording a different GUID never accepts it or expands the ABI gate. Native exception messages, returned process paths and other private data are not serialized. A Win32 code5 is ACCESS_DENIED even when its Framework HRESULT is the generic0x80004005; neither outcome triggers elevation.

Every returned object's `ITypeInfo` GUID/typekind must match its fixed native role. Before each fixed property GET, the worker verifies its exact DISPID, name, PROPERTYGET kind, zero argument count and return variant type. Interface-pointer returns must additionally resolve to the exact referenced child interface IID; returned COM objects are checked again. No generic invocation, property PUT, index-based Item access, normalization, refresh or low-level register helper is exposed. [Microsoft ITypeInfo](https://learn.microsoft.com/windows/win32/api/oaidl/nn-oaidl-itypeinfo).

## Read data

Basic read follows the previously successful private V2 getter sequence: Controls → Count/_NewEnum → Id/Name/DisplayName/DutyCycle/Profiles → Count/Current/_NewEnum → profile Id/Name. Managed `IEnumerator` is accepted first; the standard Framework `IEnumVARIANT` is the only alternate adapter. Counts are exact and bounded (32 headers, 16 profiles); extra, missing, duplicate, null or wrongly typed entries reject the entire snapshot.

`includeCurves:true` additionally GETs each header's `IsRpmMode` Boolean and `CurrentFanCurve`, then bounded Count/_NewEnum and each point's signed Int32 Temperature/Speed. At most 16 points are traversed; this is a resource bound, not a supported electrical curve limit. The new curve getters were verified statically from the installed type library, not by the earlier V2 live read, and require separate bounded real validation.

Successful data includes `source`, exact `boardName`, `serviceVersion`, UTC `capturedAtUtc`, logical `getterCalls` and `headers`. `nativePropertyReadConfirmed:true` means the fresh service getters completed; `hardwareAppliedStateConfirmed:false` and `hardwareReadValidated:false` prevent treating that as verified EC/BIOS applied state. Each header carries its returned native uint `id`, names, raw byte `dutyCycleRaw`, raw UInt32 `currentProfileRaw`, profile Id/Name pairs, optional `currentCurve` points (`temperatureRaw`, `speedRaw`) and optional `isRpmMode`.

All COM duty/curve units remain unconfirmed. `temperatureUnit`, `speedUnit`, `rpm` and global preset are null. `dutyUnitConfirmed`, `currentSelectorSemanticsConfirmed`, `unitsConfirmed`, `supportsWrite`, and `globalPresetReadbackConfirmed` are false. No raw byte is called percent or RPM; no curve integer is called °C; IsRpmMode is not a tachometer. Status records only the last completed read timestamp/outcome and attempts; it does not present a cached snapshot as a current getter.

## Remaining write contract

COM's observed per-header profile list Disable0/Standard1/Silent2/Turbo3/User4 differs from MainSDK global preset fullSpeed0/turbo1/standard2/silent3. `Profiles.Current` equalling a listed ID does not prove ID/index selector semantics or applied BIOS state. ApplyIndex's Int32 signature does not establish that mapping. ApplyFanCurve's UInt32 result is not proof that zero means success.

The original MainSDK View has a documented-in-source display conversion from XML GET_PROFILE curve x/y to °C and normalized PWM, and an inverse UI conversion. That fact applies to the MainSDK data route; no source bridge to COM CurrentFanCurve/Temperature/Speed was found. Similar saved XML numbers are insufficient to authorize a COM setter. Until native units/ranges, selector behavior, result semantics, applied readback and exact before-state restoration are established, writes remain rejected.

The next finite validation is one basic read to reproduce V2, followed separately by one extended curve read. Preserve unscaled integers, returned header/profile identities, IsRpmMode and timestamps; compare against the original application's displayed data without issuing any setter. This can resolve native getter/ABI behavior, but one coincident numeric comparison alone does not prove a safe complete write contract.

`FanNativeTests.cs` currently has **179 synthetic checks**. They cover identity gates, denied getters, exact primitive types, counts/duplicates, raw signed/zero data, optional curves, bounded enumeration, protocol rejection before transport construction, and clearing completed-read evidence after access denial. Additional injected process-image tests verify the exact limited access mask/flags, no retry or privilege fallback, handle cleanup, malformed-length rejection and diagnostic privacy/error classification. ABI diagnostics tests preserve mismatch/unknown evidence without changing the accepted interface. No native COM, WMI, process query, signature verification or hardware I/O is executed by these tests.
