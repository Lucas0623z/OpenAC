# Desktop Fan Xpert protocol preparation

`FanModeWorker.cs` is independently written MIT code. It is a **pure protocol/parser preparation with a disconnected runtime**, not a working Fan Xpert hardware backend. It has no P/Invoke, SDK loader, HTTP broker, registry, WMI, cache reader or other native transport. It cannot initialize, calibrate, stress or set a fan. Its runtime status has `sourcePrepared:true,transportConnected:false,supportsRead:false,supportsWrite:false,hardwareValidated:false`. It returns no guessed headers and no current preset.

This differs from SDKWorker's EURUX type32 controller. A EURUX port RPM or its PWM curve must never be labelled as a CPU/Chassis/Radiator/Water Pump motherboard header. It also differs from NotebookWorker's mobile SCI fan endpoints.

## Source-confirmed interface facts

Installed source: `C:\Program Files (x86)\ASUS\ArmouryDevice\modules\fanXpert\index.js`; model caps/defaults: `View\2dfe216d-3481-4684-ad4d-2566bd7cfe4f\resources\src\_ref`. The module uses device type **50**, source fallback interface model **5000**. That interface key does not prove the actual motherboard identity. No ASUS source file or binary is redistributed.

| Function | Proven wrapper | Parameters / response facts |
|---|---|---|
| 17 | getMbif | No parameters; returncode16/23/24 means supported in the original wrapper; boardname must separately match exact Windows motherboard identity |
| 0 | getCapability | No fan parameters; fancount/fan key/name/fantype, multisource and flags identify actual motherboard headers |
| 1 | getFanProfile | `<fan key="nativeHeaderId"/>`; profile contains header/name, rpmmode, curvedata, other range/response fields |
| 12 | getFanRPM | `<fan key="nativeHeaderId"/>`; response fan key and rpm |
| 13 | getCurrentPointFromSensor | fan key plus exact-case `<SensorIndex key="nativeSensorIndex"/>`; currentpoint yellow_x/y and blue_x/y |
| 16 | getTemperatureFromSensor | exact-case SensorIndex; sensor key and original spelling `tempature` |
| 3 | setFanMode | `<index>N</index>`; source caps confirm silent3, standard2, turbo1, fullSpeed0 |

The enum also names function14 GET_CURRENT_POINT_FROM_EC. Its complete wrapper/response flow was not established in this stage, so it is **excluded** from the encoder allowlist. Source naming alone does not justify guessing additional parameters.

Read encoders use only those exact functions. MBIF is encoded first without identity parameters. Other builders require separately parsed exact MBIF board facts; header reads require capability-derived key/name. No input can choose device type, interface model, caller, DLL path, arbitrary XML or raw function number outside the proven list. The pure builders do not establish a transport.

`FanModeRules.BoardInfo` rejects unsupported returncodes, empty names, wrong function identity and mismatched independently read WMI board name. `Headers` requires matching native count and unique exact header IDs, bounded lists, nonempty name/type and known boolean flags. A zero-header capability is preserved as empty. There is no fixed CPU/header order or static assumption that header0 is CPU.

`Rpm` requires exactly one reply for the queried header, exactly one rpm field and a nonnegative integer RPM. Its upper bound100000 is a parser sanity limit, **not a confirmed electrical/calibrated fan range** and cannot authorize a fixed-RPM setter. `Profile` preserves finite native curve x/y and the original RPM-mode code, rejects repeated name/mode/curve containers/x/y and malformed or duplicate supplied point keys, and does not convert it into an active global preset. All XML disallows DTD/external resolution and is limited to1MiB.

The independently researched original MainSDK View maps GET1 `rpmmode=0` to smart control and `1` to fixed RPM. Its smart curve display uses x as °C and y as duty/255 before visual clamp/rounding; the inverse UI encoder rounds temperature and duty percentage back to those native XML values. `Profile` now carries separately scoped **source display contract** metadata (`sourceRoute:MainSDKType50`, `sourceCurveDisplayContractConfirmed:true`, `sourceCurveTemperatureUnit:°C`, smart-mode `sourceCurveSpeedScale:duty255`). Raw points are never clamped or scaled by the parser, `curveUnitsConfirmed` remains false for independently unvalidated hardware, and no COM curve or RPM-mode speed unit is inferred. The source conversion belongs to the installed View data route, not a promise that similar raw COM integers share units.

`Temperature` purely parses a function16 reply with exactly one sensor, matching the exact queried SensorIndex and one field using the original spelling `tempature`. It rejects malformed/nonfinite values and uses a labelled parser sanity range, not a physical operating or write range. Its °C **source display contract** is scoped to the original MainSDK GET16 View path; it neither grants a transport nor supplies a sensor name/CPU identity without real capability mapping. No direct COM manager temperature property inherits that unit.

`PresetDraft` only builds a reviewable source-confirmed command; it does not transmit it. There is **no proven getter for the active global silent/standard/turbo/fullSpeed preset** in this stage. A fan's `rpmmode` is not that preset. The strict write-basis guard requires legitimate transport, fresh exact board/header capability, explicit session authorization and an established mode readback path. All of these are absent from the disconnected runtime, whose set requests are rejected before any I/O. No successful draft encoding is advertised as a successful hardware write.

## Why the runtime stays disconnected

The original page initialization includes INIT_SDK4 and additional refresh/configuration commands; alert initialization may also enter AI_COOLING_PROFILE22 with stress3. Replaying the entire page flow would violate this read-only stage. The prior ordinary MainSDK service paths were rejected for this independently identified caller; they were not retried. No trusted process name, session, authentication material or vendor handle was impersonated/reused.

Installed AiSuite `output.xml` was observed as a single recent GET13 reply with fan key0/currentpoint54,126,-1,-1. It had no board identity, header name, units or complete capability/profile snapshot. It cannot supply confirmed CPU fan/temperature/PWM/RPM or current preset and is neither connected to GUI nor polled by this worker. A later investigation identified the already-running AsusFanControlService's COM property-get ABI. Ordinary activation was denied; an authorized administrator V1 read hit an enumeration cast error. A separately prepared V2 subsequently completed an authorized private administrator read: eight exact native headers, raw-duty bytes and five Id/Name profiles per header,147 getter attempts, no explicit setter, no timeout, existing service PID unchanged. This proves the limited private COM read on this artifact, **not** that this worker/GUI is connected or MainSDK type50 now accessible. Duty units, raw Profiles.Current semantics, applied BIOS state, global preset and per-header RPM remain unverified/unknown. See [Fan Xpert interface research](FanXpert-Interface-Research.md) for the snapshot and curve getter metadata (generic help strings do not supply physical units). This worker still has no COM transport or public shared-memory schema. No guessed native signature or low-level SMBus/ASIO operation was invoked.

A future legitimate transport must first establish its documented ABI/caller access and pure-read initialization requirements, then verify MBIF17 → capability0 → each explicit profile1/RPM12. A failure or permission rejection must stop this sequence, not trigger INIT/calibration/stress. Preset writes require a new reviewed result/readback contract; this file does not supply an imaginary GetPreset API.

## Runtime JSON

```json
{"id":1,"op":"status"}
{"id":2,"op":"scan"}
{"id":3,"op":"read"}
{"id":4,"op":"set"}
```

Only id/op fields are recognized. Status returns honest disconnected metadata, empty headers, currentPreset null and zero native opens/hardware writes. Scan/read return `NATIVE_TRANSPORT_UNVERIFIED`. Set returns `WRITE_DISABLED`. Unknown/override fields are invalid before any I/O. This runtime is optional research plumbing; users do not need an additional disabled page or cached-data view.

A separate [FanNative COM worker](FanNative-protocol.md) now prepares a real ordinary-caller, exact-board/x86 getter path with independent identity/ABI checks and a finite watchdog. It does not modify this legacy MainSDK runtime, solve its denied caller access, or supply a write contract. Raw COM curve units and selector semantics remain unknown; SET is rejected in both workers.

## Pure checks

Compile with Framework csc, `WorkerTextProtocol.cs` and `System.Web.Extensions.dll`; tests also add `FanModeWorkerTests.cs` with `/main:FanModeWorkerTests`. Result: **84 checks passed**, native transports0/cache reads0/hardware writes0. Tests cover exact board identity/status, unique native headers/count, zero stopped RPM, mismatch/invalid values, profile/preset distinction, exact XML and preset indexes, forbidden INIT/calibration/stress/extra commands, write-basis gates and disconnected runtime truthfulness. No Fan Xpert native call was made in this stage.

`FanNativeMainSdkTests.cs` adds **35 pure checks** for GET16 temperature identity and duplicate fields, malformed/nonfinite data, source-route-scoped units, preserved curve x/y, smart/fixed-RPM mode metadata, duplicate curve fields/point keys, and duplicate RPM rejection. It does not connect a native transport.
