# Installed SDK adapter

`SDKWorker.cs` is an independently written x86 .NET Framework adapter. It loads installed SDKs under `C:\Program Files (x86)\ASUS\ArmouryDevice\dll`. Vendor DLLs and extracted vendor source are not redistributed. Each worker owns one native SDK kind and remains isolated from the GUI.

## Build and synthetic validation

```powershell
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /platform:x86 /target:exe /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll /out:SDKWorker.exe SDKWorker.cs WorkerTextProtocol.cs
& 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' /nologo /platform:x86 /target:exe /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll /main:SDKWorkerTests /out:SDKWorkerTests.exe SDKWorker.cs WorkerTextProtocol.cs SDKWorkerTests.cs
& '.\SDKWorkerTests.exe'
```

Result on 2026-10-13:

```text
SDKWorker synthetic checks passed: 254; hardware writes performed: 0; native DLLs loaded: 0
```

The synthetic test executable injects an internal transport, which is not exposed by the JSON protocol. Tests cover exact source-derived XML, node/range/port validation before native I/O, fixed-RPM refusal without valid capability, source PWM curves on live positive-RPM ports, zero-RPM refusal without capability, no calibration functions during reads, unknown/XML input refusal, driver result failure, SDK-return-only uncertainty, power no-op, write/readback mismatch, failed readback after acknowledged write and request correlation. Media checks cover all eight GIF/JPEG mappings, duration and monitor validation, ambiguous sensor identities, absolute local JPEG/slot validation, 720×1280 letterboxing, unchanged original file hashes, free-space KiB rounding/boundaries, transfer→presentation→profile read, partial failures, and owned staging cleanup. Fan lighting checks cover native-capability gating, all brightness/speed map boundaries, strict RGB→GBR, colors per area/fan, unavailable controls, count preparation before effect, connected-chain scope/validation, partial errors, source selection and explicit chain disconnect. No writable hardware operation has been tested on the physical machine. Function 3 was subsequently read by the parent task on the physical machine and returned no valid calibrated range on all four ports; the fallback below preserves source-defined PWM support without inventing RPM ranges.

## Process and envelope

Launch `SDKWorker.exe fan` or `SDKWorker.exe lcd` with redirected stdin/stdout/stderr. Each request and response occupies one JSON line with a matching `id`. Keep the process alive for its kind: confirmed devices/capabilities belong to that worker's session. A scan clears old device and capability confirmations.

Success: `{"id":1,"ok":true,"kind":"fan","op":"read","data":{...}}`.

Failure: `{"id":1,"ok":false,"kind":"lcd","op":"set","error":{"code":"INVALID_REQUEST","message":"..."},"data":null}`.

Write failures also return `data:{writeAttempted:true,hardwareAcknowledged:false,readbackConfirmed:false,appliedState:"unknown"}`. Do not retry or restore automatically: an unsuccessful native call does not prove that no partial change occurred. Validation failures have no native I/O and no write-attempt metadata.

The public protocol accepts `scan`, `read`, LCD-only `sensors`/`media`, and allowlisted `set`. It never accepts arbitrary XML, DLL paths or function numbers. A read scans when needed; **a set never scans** and requires the exact device to have been scanned and a successful alive-device read in the current worker. Every setter rejects unknown fields. Numeric settings require JSON integers, switches require JSON booleans, and `deviceId`/`portId` require strings.

## LCD user-media slots (additional source-confirmed route)

After scan and a successful alive-device read:

```json
{"id":11,"op":"media","kind":"lcd","deviceId":"7299"}
{"id":12,"op":"set","kind":"lcd","deviceId":"7299","setting":"selectMedia","slot":0,"durationSeconds":10}
{"id":13,"op":"set","kind":"lcd","deviceId":"7299","setting":"deleteMedia","slot":0,"confirmSlotDeletion":true}
```

`media` sends only GET26 (free space) and GET1 (configured media profile). It reports verified `freeSpaceKiB`, current profile groups and their readback status. `slots:null,slotOccupancyReadable:false` is intentional: no uploaded-slot inventory getter exists in the inspected source. Slots are 0–31, independently of whether a chosen slot currently contains content. No cached local gallery is presented as hardware inventory.

`selectMedia` selects one existing custom JPEG slot through function2/profile4/multi_media_player with `type0,source0,index_format=slot`, then GET1 checks configuration metadata. It does not upload, invent a populated slot or confirm displayed pixels. Durations use the same 5/10/20/30/60-second source options as presets.

`deleteMedia` accepts only one exact user JPEG slot plus the JSON boolean `confirmSlotDeletion:true`. It sends function29/settings/media_delete/type0/index=slot. There is no delete-all99 route, local source-file deletion or automatic retry. Reply `deletionAcknowledged` means an explicit native response accepted the command; `deletionReadbackConfirmed:false` remains because neither inventory nor slot-emptiness readback is proven. A return-only result remains unconfirmed.

The installed motherboard caps for PID7299/lcdType51 explicitly restrict custom upload to JPEG with a 64MB storage limit. Its two built-in animations do not establish user GIF/video upload support. The generic AIO module includes GIF/MP4 conversion and AVI transfer, but those routes must be tied to a model's actual caps before declaration. This worker therefore exposes `userUploadFormats:["jpg"],animationUploadSupported:false` for7299. No unsupported format is silently mapped to JPEG or a native animation index.

Added synthetic validation: total290 checks passed, physical hardware writes0 and native DLL loads0. Covers only-space/profile reads, exact slot/type/duration fields, selection metadata readback, explicit deletion confirmation and range validation before I/O, no implicit delete-all, ACK/return-only/failure distinction, absent slot inventory, invalid free-space output and unscanned-session refusal. No physical slot was selected, uploaded or deleted by this change.

Native logs can emit extra lines despite stdout redirection. Accept only parseable objects with `id`, `ok`, `kind` and `op`; maintain request deadlines and terminate an unresponsive child. Native crashes cannot be caught in C#. Do not automatically retry a repeatedly crashing kind.

## Fan read contract

```json
{"id":1,"op":"scan","kind":"fan"}
{"id":2,"op":"read","kind":"fan","deviceId":"7576"}
```

Scan confirms `hal_found=1` and `hub_found=1` through type 32/PID 7576/function 1. Read sends function 4 RPM, pure function 3 capability for each port, and function 5 temperatures. **It never sends function 101**, which the original combined `getFanTuningResult` uses to stop calibration. No tuning/start/stop API is exposed.

Read data includes `ports`, `temperatures`, `readbackConfirmed`, `deviceAlive`, `placeholder`, `supportsWrite`, `supportsSmartCurve`, `supportsFixedRpm`, `supportedSettings`, `presets`, `curveNodeCount:4`, `curveTemperatureMin:0`, `curveTemperatureMax:100`, `portNamingScope:"localApplication"`, and `errors`.

It also returns source-confirmed hub metadata from the latest successful scan: `firmware,firmwareSource:"lastHubScan",firmwareReadbackConfirmed,effectCapabilities,temperatureSources`. `supportsLighting,supportsFanCount,supportsDisconnectLightingChain,supportsTemperatureSources` additionally require a successful alive-device read. Current unreadable settings remain `currentLighting:null,fanCounts:null,currentTemperatureSources:null,lightingSettingsReadbackConfirmed:false`. A saved GUI draft never becomes these native fields. Source ranges are `fanCountMin:0,fanCountMax:14`, brightness options0/25/50/75/100, speed options20/40/60/80/100, direction optionsforward/backward. Lighting capabilities are the intersection of installed current-model source mappings and actual hub function1 effect_list; an absent/empty hub list disables lighting even when installed caps contain defaults.

Each of ports 1–4 has:

```json
{"portId":"1","rpm":"1200","confirmed":true,"mode":null,"targetRpm":null,"nodes":null,"settingsReadbackConfirmed":false,"supportsWrite":true,"supportsSmartCurve":true,"supportsFixedRpm":true,"smartRange":{"temperatureMin":0,"temperatureMax":100,"percentageMin":0,"percentageMax":100,"rangeSource":"sdkCapability","rangeReadbackConfirmed":true},"capability":{"rpmMin":840,"rpmMax":2800,"percentageMin":0,"percentageMax":100,"rangeSource":"sdkCapability","rangeReadbackConfirmed":true}}
```

Unavailable RPM is null. Zero RPM is valid. An invalid/missing native capability is null with `capabilityError` and disables fixed-RPM writes. SDK capability requires an exact matching port, explicit result 0, nonnegative integer RPM bounds with maximum greater than minimum, and duty bounds within 0–100. Cached vendor UI calibration files are not substituted for SDK RPM capabilities. There is no verified current mode/target/curve getter; those fields always remain null, even after a successful write.

For a port with a successful, exactly identified **positive RPM read**, smart curves can use the original source/caps range even when RPM calibration is unavailable: `supportsSmartCurve:true,supportsFixedRpm:false,smartRange:{temperatureMin:0,temperatureMax:100,percentageMin:0,percentageMax:100,rangeSource:"installedSource",rangeReadbackConfirmed:false}`. This is PWM percent, not an inferred RPM conversion or measured calibration. A zero/unreadable-RPM port without valid native capability remains disabled. The GUI must use the per-port setting flags, not only the device-level `supportsWrite` flag.

Temperatures are Celsius records `{sourceId:string,temperature:string|null}`. Source IDs 1–5 are CPU, GPU, DRAM, motherboard and T Sensor; SDK `-1` becomes null.

Read-only verification on 2026-10-13 found FAN CONTROLLER PLUS firmware `90.36.32.00.0D`, port 1 roughly 1350–1380 RPM, port 2 1170 RPM, ports 3/4 zero; CPU/GPU around 43 °C and DRAM 54 °C. Those measurements vary. The parent task's later pure function 3 read found no valid calibrated fan ranges. Thus ports 1/2 can use the source-defined PWM curves; ports 3/4 and fixed RPM stay unsupported until actual port/capability evidence changes. These reads do not verify writable operations.

## Fan setters

Fixed mode uses actual RPM, not percentage:

```json
{"id":3,"op":"set","kind":"fan","deviceId":"7576","setting":"fixed","portId":"1","rpm":1000}
```

`rpm` must be inside that port's valid SDK capability snapshot from the latest read. The native mapping is type 32/function 103, port key 1–4, native mode **2**, settings/rpm. GUI mode ID 0 must not be passed through as native mode 0.

Smart mode accepts a source preset or exactly four custom nodes:

```json
{"id":4,"op":"set","kind":"fan","deviceId":"7576","setting":"smart","portId":"1","preset":"standard"}
{"id":5,"op":"set","kind":"fan","deviceId":"7576","setting":"smart","portId":"1","nodes":[{"temperature":0,"percentage":30},{"temperature":50,"percentage":30},{"temperature":70,"percentage":50},{"temperature":100,"percentage":90}]}
```

Temperature is an integer 0–100 °C; duty is an integer 0–100% and must also fit a valid native capability when available. Otherwise only a live positive-RPM port can use the source-defined 0–100% PWM range. Temperatures must strictly increase, the first must be zero, and percentages cannot decrease. These conservative validation rules avoid duplicate-temperature ambiguity; the original graph also adjusts neighboring nodes to avoid decreases. Do not silently clamp a user curve. `nodes` and `preset` are mutually exclusive.

| Preset | Four (°C, %) nodes |
|---|---|
| `silent` | (0,0), (60,30), (80,40), (100,70) |
| `standard` | (0,30), (50,30), (70,50), (100,90) |
| `turbo` | (0,40), (40,40), (60,60), (90,100) |
| `fullSpeed` | (0,100), (50,100), (80,100), (100,100) |

Smart native mode is 1, function 103, settings/duty_curve/item_count 4 with one-based item keys, temperature and duty. Spin-up/down levels are both 2, matching the installed adapter. There is no separately verified fan spin-up control or DC/PWM electrical mode API. Port naming is a local application alias, not a hardware setter.

Fan writes have no settings readback. A typical explicit-success response contains `writeAttempted:true,sdkAcknowledged:true,hardwareAcknowledged:true,acknowledgementSource:"sdkResponse",readbackConfirmed:false,appliedState:"acknowledged"` and the normalized `requested` configuration. Responses also carry `controlUnit:"PWMPercent"|"RPM"`, `rangeSource:"installedSource"|"sdkCapability"` and `rangeReadbackConfirmed` independently of settings readback. UI text should say the SDK acknowledged the setting and offer a later RPM read; an RPM change does not prove the whole curve was stored. The GUI owns local configuration persistence and should not replace hardware snapshot fields with its saved request.

## Fan controller lighting and temperature sources

```json
{"id":16,"op":"set","kind":"fan","deviceId":"7576","setting":"fanCount","portId":"1","value":2}
{"id":17,"op":"set","kind":"fan","deviceId":"7576","setting":"lighting","portId":"1","area":"outer","effect":"static","fanCount":1,"brightness":75,"speed":60,"direction":"forward","colors":["#112233","#AABBCC"]}
{"id":18,"op":"set","kind":"fan","deviceId":"7576","setting":"lighting","portId":"1","area":"connected","effect":"storm","fanCounts":[1,2,0,0],"brightness":50,"speed":80,"direction":"backward","colors":["#FF0080","#FFFFFF"]}
{"id":19,"op":"set","kind":"fan","deviceId":"7576","setting":"temperatureSources","sourceIds":["1","3"]}
{"id":20,"op":"set","kind":"fan","deviceId":"7576","setting":"disconnectLightingChain","portId":"1"}
```

All input is validated before any I/O. `fanCount` uses native202, port1–4, integer0–14. No current count getter is known. In a lighting request, normal areas require `fanCount:1..14`; connected area requires exactly four integer0–14 `fanCounts`, at least one nonzero, and portId1. The worker never guesses connection counts from RPM. Counts must describe the user's explicit draft, not claimed hardware measurements.

`lighting` first sends one202 for its normal port or one202 for each of the four connected ports, and requires every explicit hardware acknowledgement before sending201. A return-only count response stops before the effect. If any stage fails, earlier count results remain in error details, effectAttempted reports whether201 was reached, and no retry/rollback occurs. Successful responses include `countAcknowledgement,effectAttempted:true,affectsAllPorts,affectedPorts`. Connected chain affects all four ports via port1/group1; the GUI labels that scope before the user applies it.

Native201: port/group_color_set key1 for center/sync/connected, key2 for outer; effect_id/speed/direction/brightness are siblings. `colors` is always a JSON array of strict seven-character #RRGGBB strings; spaces/native hexadecimal/XML are rejected. Source converts RGB→GBR: #112233→0x223311. Colors are comma-space separated in source order. Black #000000 is supported as a color. There is no guessed hardware effect named off; brightness0 uses the verified native off mapping.

Brightness mapping:0→8,25→3,50→2,75→1,100→0. Speed:20→2,40→1,60→0,80→-1,100→-2. Direction:forward0/backward1. Effects without speed/direction must receive neutral speed60/forward; other values are rejected. The GUI disables those controls. All effects support brightness.

| Area | Effect | Native ID | Required colors | Speed | Direction |
|---|---|---|---|---|---|
| center | static |3|one per configured fan|no|no|
| outer | static |3|two per fan, top then bottom|no|no|
| center/outer | breathe |4|4|yes|no|
| center/outer | rainbow |5|0|yes|yes|
| center/outer | track |10|2 (foreground/background)|yes|yes|
| outer | paint |7|4|yes|yes|
| outer | oddEven |8|2|yes|no|
| outer | pinball |9|2|yes|yes|
| sync / connected | storm |32 /66|2|yes|yes|
| sync / connected | river |34 /67|2|yes|yes|
| sync / connected | rainbowRiver |48 /64|0|yes|yes|
| sync / connected | dualPulse |49 /69|4, outer pair then center pair|yes|no|
| sync / connected | barberPole |51 /70|3, two foregrounds then first background|yes|yes|
| sync / connected | spread |50 /71|4|yes|no|
| sync / connected | oddEven |33 /72|2|yes|no|
| sync / connected | pinball |38 /65|2|yes|yes|

There are27 source-defined area/effect combinations on this device, filtered by actual native IDs. `effectCapabilities` records `area,effect,nativeEffectId,colorCount,colorCountRule,hasSpeed,hasDirection,connectedChain,rangeSource`; static colorCount=-1 means the onePerFan/twoPerFan rule rather than a negative count.

The original wrapper implicitly injects a disconnected-chain201 at port1/group1, static3/white/native brightness0, before ordinary per-port effects. This has a real side effect: port1 center becomes white at100%. The owned worker instead exposes **explicit** `disconnectLightingChain` with that exact source command. It always reports `affectsAllPorts:true`, the directly changed center color/brightness, and false settings readback. It is never hidden inside a ordinary apply. The GUI offers a separately labeled button to end connected-chain mode before normal per-port use. Since current chain state cannot be read, the worker cannot claim that a normal request is independent of an existing chain.

`temperatureSources` uses native104 `source_list/source key` with one to five unique string IDs from the actual hub scan where available=true. On this machine1CPU Package/2GPU/3DRAM were confirmed available and4Motherboard/5T-Sensor unavailable. Native response acknowledgement does not read back selected sources. This is controller-level source selection, not a new per-port sensor setter or calibration command.

`FanLightingStructure.cs` provides `OriginalControllerLightingPage(StackPanel,IDictionary<string,object>)` and `OriginalControllerTemperatureSourceEditor` for the root GUI. It labels configuration as a draft, persists only submitted local profiles (`controller-lighting.json`, `controller-temperature-sources.json`), and keeps native fields nullable. Original visible tabs remain Fan PWM/Lighting Effect/FW Updated; no firmware flashing is exposed.

## LCD read contract

```json
{"id":6,"op":"scan","kind":"lcd"}
{"id":7,"op":"read","kind":"lcd","deviceId":"7299"}
```

Current supported model is ROG CROSSHAIR X870E EXTREME LCD, PID 7299/type 5, 720 × 1280. Firmware function 5 returned `0109`; GET_PROFILE function 1 returned profiles 5/warning and 4/media. Firmware `0000` or an invalid string does not confirm an alive device.

Read includes nullable booleans `powerS0` and `powerS5`: exact SDK text 0 means on, 1 means off, other values fail. Current getter functions are 6 and 20.

Brightness remains `null`, `brightnessReadable:false`, while its source-confirmed UI range is `brightnessMin:10,brightnessMax:100,brightnessStep:10,brightnessRangeSource:"installedViewSlider"`. `brightnessWritable` follows confirmed device availability. Standby remains `standbyEnabled:null,standbyReadable:false`, with `standbyWritable` following availability. Neither setting has a dedicated getter, and current GET_PROFILE lacks them. Stored Armoury defaults are never reported as hardware current values.

`profileReadbackConfirmed` only means GET_PROFILE parsed as a profile document. `currentMediaProfile` contains raw group metadata when available. `mediaVisualIdentityConfirmed:false` is always retained. `presetAssets` (also `contentPresets`) exposes eight source-confirmed selections with `presetId,fileName,preloadFileType,type,source,index,visualIdentityConfirmed:false`. The filenames identify the installed caps/preview selection; the adapter does not claim that displayed pixels have been visually verified. GIF and JPEG index spaces are distinct.

Read also includes `imageUploadWritable`, `uploadSlotMin:0,uploadSlotMax:31,uploadSlotOccupancyReadable:false`, `hardwareSensors`, `hardwareMonitorWritable`, and `hardwareMonitorBackgrounds`. Hardware sensors come exclusively from the successful current function 0 capability document; opaque `id` values belong to that process/scan and must not be persisted as permanent identities. Records preserve `nativeId,name,defaultName,nativeType`. Types are the original mainboard UI's supported voltage 0, temperature 1, fan speed 2, frequency 4, and special sensor type 5. Unsupported load type 6 is omitted. The source maps special IDs 65280/65281 back to native type 5. Duplicate native IDs are preserved as separate records with their exact names, rather than silently conflating CPU/GPU records.

A bounded read-only timing on 2026-10-13 found fresh-worker scan 3973 ms and read 22 ms (firmware 0109, 21 selectable sensor capabilities). An earlier parent run hit its 15-second LCD timeout once; a later parent run and this probe succeeded. This does not establish the cause of the delay. A 25-second hard deadline for whole LCD requests is reasonable; retain child termination and no automatic retry/write. Evidence: `work/reverse/lcd-worker-flow/worker-readonly-timing.txt`.

## LCD setters

The independently implemented monitor editor binds sensor IDs to the exact
confirmed capability snapshot and live `WorkerClient.SessionIdentity`. It
checks the capability fingerprint, complete ordered selections and distinct
sensor identities before applying. The optional expected-session request is
checked under the worker queue lock both before and after startup; an expired
binding cannot trigger an automatic restart and submit old IDs. Hot preview
replacement retains only mode, preset, background and information count, then
discards the cached LCD snapshot and selections until a fresh read. This GUI
binding does not add native commands or change the worker packet format.

While a bound monitor draft is being edited, its same-page redraws do not queue
another read that would clear the selections. Returning from another page or
tab, or losing the live binding, keeps the ordinary fresh-read path available.

```json
{"id":8,"op":"set","kind":"lcd","deviceId":"7299","setting":"powerS0","value":true}
{"id":9,"op":"set","kind":"lcd","deviceId":"7299","setting":"powerS5","value":false}
{"id":10,"op":"set","kind":"lcd","deviceId":"7299","setting":"brightness","value":50}
{"id":11,"op":"set","kind":"lcd","deviceId":"7299","setting":"standby","enabled":true,"brightness":30}
{"id":12,"op":"set","kind":"lcd","deviceId":"7299","setting":"presetId","value":3,"durationSeconds":5}
```

* Power S0 uses function 7; S5 uses 21. Settings/power is 0 for on or 1 for off; saving is 1. Read before writing and skip an already-equal state. Otherwise send one write, then read the matching getter. Data returns requested, previousValue, actual nullable value and verification status. A mismatch or post-write read failure remains a successful operation envelope with `readbackConfirmed:false` and `verificationWarning`, preserving acknowledgement information. No retry or rollback occurs. A verified no-op has `writeAttempted:false,hardwareAcknowledged:false,readbackConfirmed:true,appliedState:"alreadySet"`.
* Brightness is 10–100 in multiples of 10. Function 37 writes settings/brightness/s0 and s5. Both current power states are read first; S0 must be on. S0 receives the requested brightness. S5 receives the request when its power is on, otherwise native 1, matching the installed motherboard adapter's powered-off mapping. There is no hidden power change. Readback remains false and current brightness remains null.
* Standby uses function 24 with settings/standby/enable 1 or 0. Enabled brightness is 10–100 in multiples of 10; disabled native brightness is 0, matching the common source wrapper. The caller still supplies a validated brightness for a future enable. Enabling requires S0 currently on. No brightness/power/media settings are implicitly changed. There is no current standby getter, so readback remains false.
* `presetId` uses the actual motherboard `/mb/lcd` media route from module 283, not the older `onApplyImageOrAnimation` wrapper. Function 2 writes profiles/profile4/main/single/player key `multi_media_player`, play_device `AIO2_PANEL`, display_time in milliseconds, one group key 0, and source 1. The mapping below includes both animations and all six stills. `durationSeconds` defaults to 5 and must be one of the caps list 5/10/20/30/60. The obsolete ambiguous `presetIndex` setter is rejected. A post-write GET_PROFILE verifies exactly one group with matching play device, duration, type, source, index and any additional monitor metadata. It returns `readbackScope:"configuredProfile"`; `visualIdentityConfirmed` stays false. Only profile 4 is submitted; profile 5/warning is not modified.

| UI `presetId` | Installed preview asset | Native type | Native source | Native index |
|---|---|---|---|---|
| 0 | `x870_launch1.gif` | 2 (animation) | 1 | 0 |
| 1 | `x870_launch2.gif` | 2 (animation) | 1 | 1 |
| 2 | `x870_01.png` (caps file type JPG) | 0 (JPEG) | 1 | 0 |
| 3 | `x870_02.png` | 0 | 1 | 1 |
| 4 | `x870_03.png` | 0 | 1 | 2 |
| 5 | `x870_04.png` | 0 | 1 | 3 |
| 6 | `x870_05.png` | 0 | 1 | 4 |
| 7 | `x870_06.png` | 0 | 1 | 5 |

## LCD local JPEG upload

```json
{"id":13,"op":"set","kind":"lcd","deviceId":"7299","setting":"uploadImage","filePath":"D:\\Pictures\\example.jpg","slot":4,"durationSeconds":5}
```

The caller must explicitly choose slot 0–31 after being told that existing slot content may be overwritten. The worker never chooses a slot, guesses emptiness, implicitly deletes device media, or retries a failed transfer. `overwritesExistingSlot:true,slotOccupancyConfirmed:false` is returned even if the chosen slot may happen to be empty. GET_PROFILE is not an uploaded-slot inventory.

`filePath` must be an absolute local drive path to JPG/JPEG, with no alternate data stream, UNC/network drive or reparse-point traversal. The source is opened only with read access, must be at most 64 MiB and contain a decodable JPEG; dimensions are limited to 16384 each/64 million pixels. An independent in-memory conversion honors EXIF orientation, fits the complete image into 720×1280, preserves aspect ratio, and fills remaining pixels black. It generates a 24-bit JPEG. The original file is never written. No installed converter executable is launched.

After normalization, function 26 must return a positive integer confirmed free-space size; invalid, missing, zero, negative, oversized or insufficient values prevent every hardware write. The installed SDK function 25 JPEG branch computes `stat.st_size / 1024`; original JavaScript compares that unchanged to function 26. Therefore function 26 uses KiB. This adapter compares **ceil(normalizedBytes/1024)** to the reported size, avoiding the original truncation. Evidence offsets 0x100197c9–0x10019817 are saved in `work/reverse/lcd-worker-flow/sdk-function25-output-size-disassembly.txt`.

Only after sufficient space is confirmed does the worker create its own GUID-named file in `%LOCALAPPDATA%\RogControl\lcd-staging`. It sends function 27 once: `settings/media_transfer/path` is that normalized file, `type:0` JPEG, `index:slot`. A hardware acknowledgement is required before it selects the content. A return-only transfer reply leaves `presentationAttempted:false` and does not submit a profile. After acknowledged transfer it sends function 2 once to select type 0/source 0/index slot, then function 1 GET_PROFILE. The owned staging file is cleaned in every completed/error path; the original remains unchanged. A forcibly killed process may leave its own staging file; it never deletes unrelated files or other staged files.

Responses report `slot,overwritesExistingSlot,slotOccupancyConfirmed,normalizedWidth:720,normalizedHeight:1280,normalizedBytes,freeSpace,freeSpaceUnit:"KiB",requiredKiB,spaceUnitSource:"installedSdkSizeConversion",freeSpaceReadbackConfirmed,originalModified:false`, as well as `transferHardwareAcknowledged,presentationAttempted,presentationHardwareAcknowledged` where that stage occurred. `stage` is transfer, presentation on partial failure, or completed. If presentation fails after acknowledged transfer, the error keeps the successful transfer acknowledgement and the failed presentation stage. No rollback is guessed. `readbackConfirmed` confirms configured media profile metadata only; it does not confirm uploaded file bytes, slot occupancy, or displayed pixels.

## LCD hardware monitor

```json
{"id":14,"op":"set","kind":"lcd","deviceId":"7299","setting":"hardwareMonitor","backgroundId":8,"sensorIds":["sensor-2","sensor-19"],"durationSeconds":5}
{"id":15,"op":"sensors","kind":"lcd","deviceId":"7299","sensorIds":["sensor-2"]}
```

The example opaque sensor IDs must be replaced with IDs from this worker's current capability list. `hardwareMonitor` selects a source-defined preload background: 8/9/10 correspond to native index 0/1/2 and support up to three sensors, 11 is index 3 and supports up to five. At least one sensor is required; all IDs must be known and unique. It uses the current motherboard media group type 5/source 1, `bg_type:1` AVI, zero-based sensor keys and exact capability-native id/name/default_name/type. Source defaults are title font 1/RGB255,0,80 and value font 0/RGB255,255,255; arbitrary fonts/backgrounds are not accepted. GET_PROFILE compares these metadata separately from visual verification.

The read-only `sensors` op sends function 36 `settings/get_sensor_data/sensor` with zero-based keys and exact capability id/default_name/type. It does not change the display. Response entries carry `value` and `valueReadbackConfirmed`; missing/nonfinite values are null. If a native reply omits enough identity information to distinguish duplicate IDs, values remain null with an ambiguity error. The op has source/synthetic verification only; its physical response shape has not been tested in this subtask, so a shape mismatch yields unconfirmed values rather than guessed values. Firmware/profile/power and capability reads have physical verification as noted above.

## Acknowledgement semantics

The native ABI call must return 0 and produce a valid bounded response buffer. Raw `0` or an XML response with explicit result fields all equal to 0 yields `hardwareAcknowledged:true` (an SDK/device acknowledgement, not proof of the complete physical state). Nonzero results/error responses fail with unknown applied state. A well-formed response with no explicit result yields `sdkAcknowledged:true,hardwareAcknowledged:false,acknowledgementSource:"nativeReturnOnly",readbackConfirmed:false`. Never present native-return-only as hardware confirmation. The power/profile getters provide the separate readback status described above.

## Source evidence and unsupported operations

Evidence remains in the user's installation; no extracted implementation is packaged:

* `modules\FAN_AND_CONTROLLER\index.js`: functions 3/4/5, smart mode 1/fixed mode 2/function 103, spin-up/down 2, one-based curve item keys. The combined tuning getter also sends 101 and is intentionally not used.
* `View\7576\315-bundle-9e201.js`: graph axes 10–100, position conversion to integer 0–100, limits and nondecreasing neighboring nodes. `resources\src\_ref\default.json` supplies the four-node presets.
* `modules\aio\index.js`: power 7/21 and strict getter 6/20 mappings, brightness 37, standby 24, media profile 2, raw getter 1; module 283 motherboard `/mb/lcd` distinguishes JPG/dynamic animation/monitor types; generic `cropImage` maps 25→26→27, JPEG transfer type 0; `getLightingHardwareSensor` maps read-only 36.
* Mainboard `View\E7C8DA76-C9B9-4297-8681-DD878330AFE7\82-bundle-4b237.js`: brightness slider min 10/max 100/step 10. Its caps identify PID 7299, play device AIO2_PANEL, paired S0/S5 brightness and native media indices.

Unsupported here: firmware/bootloader updates, calibration, electrical DC/PWM mode changes, custom fan spin-up/down timing, SDK port renaming, GIF/video/custom monitor-background upload, automatic slot allocation, uploaded-slot inventory, arbitrary profiles/XML, and current brightness/standby/fan-curve readback. Displayed asset identity remains visually unverified. No services, DLL registration, installed files or firmware are changed by this adapter itself. Explicit caller `set` operations can change fan/LCD settings and JPEG media as described; read/scan never perform those writes.

## Source-prepared LCD51 family

LCD `scan` now accepts an optional exact `deviceId`: `7124` (ROG MAXIMUS Z890 EXTREME), `7299` (ROG CROSSHAIR X870E EXTREME, unchanged default), or `7571` (ROG CROSSHAIR X870E EXTREME GLACIAL). These three installed caps share LCD51, 720×1280, 32 JPEG user slots, `AIO2_PANEL`, the same 2 animation + 6 image native preset mapping, and the JPEG-only custom-upload restriction. Preset preview filenames are selected from each model's own caps. The capability getter must return the requested exact model ID; a different model is excluded. Unknown model IDs and extra scan fields are rejected before native I/O. Four-character firmware responses must contain hexadecimal digits and differ from `0000` before validating the device as alive.

`family:"motherboardLcd51",sourcePrepared:true,hardwareWriteValidated:false` separates source-confirmed preparation from physical write testing. Only the existing 7299 read path has been checked on this machine. Additional models have synthetic/source verification only; no writes or firmware checks were performed on them. Model caps firmware-limit strings are recorded as evidence and are not guessed into a new version-ordering policy.

Latest synthetic suite: **310 checks passed**, hardware writes 0, installed native DLL loads 0. New cases cover exact requested-model routing, mismatched capability exclusion, model-specific preset names, and malformed firmware preventing setters.

### Sensor zero placeholders and shared AIO mutex

A permitted local GET36 read returned zero for five selected clock/temperature sensors, without a validity flag or unit. `sensors` now keeps the exact finite number in `rawNativeValue` / `nativeValueReadbackConfirmed`, but zero type1/type4 remains `value:null,valueReadbackConfirmed:false,placeholder:true`. This is conservative uncertainty, not a claim that a zero sensor could never exist. Existing nonzero unitless values are native numbers only; `unit:null` does not establish MHz/C. The stricter new `TelemetryWorker` requires an explicit recognized capability unit before confirming a physical reading. Full evidence and interface are in [TelemetryWorker-protocol.md](TelemetryWorker-protocol.md).

LCD native requests, including ordinary loader initialization, now acquire `Local\OpenAC.AioSdk` without waiting. TelemetryWorker uses the same lock. Another owned AIO query returns `SDK_BUSY`; this does not coordinate with unrelated ASUS programs. Test injection bypasses the mutex/native loader entirely. Updated related suite: **313 checks passed**, actual hardware writes0/installed DLL loads0.

## Unstable mouse and keyboard SDKs

Experimental mouse/keyboard scan/read templates remain off by default and all their writes are rejected. Enumeration succeeded once each but subsequent attempts terminated with `0xC0000409`; MainSDK forwarding also crashed. Do not poll/retry these known unstable paths.

Mouse templates cover info/performance/power; keyboard info/polling. Placeholder info (`connection_status=255`, firmware `0.00.00`) reports `deviceAlive:false,readbackConfirmed:false,placeholder:true` and suppresses info/performance/power as null. Enumeration alone and a placeholder battery 0 do not confirm device state. Hardware serials stay internal.

## Native ABI

* `int __stdcall startService(int port)` called with 0; success is 0.
* `int __stdcall Post_EntrypointReturn(void* zero,void* utf16Input,int inputByteCount,void* zero,void* zero,void** output,int* outputByteCount,int* outputFlag)`; eight arguments, stack cleanup 32 bytes. Input/output are UTF-16LE; count is bytes.
* `freeBuffer(void** output)` uses the SDK allocator, stack cleanup 4 bytes.

Responses require a non-null buffer, even byte size between 2 and 4 MiB, and are freed with the SDK export. XML DTD/external resolution is prohibited. Dependencies resolve from the installed SDK directory using SetDllDirectory and an absolute DLL path. Installed ASUS services/drivers may still be required. The adapter does not spoof an ASUS process or bypass caller validation.
