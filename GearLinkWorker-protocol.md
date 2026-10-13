# GearLink device adapter

Current OpenAC 0.3 status: both keyboard and mouse are awake and confirmed online. GearLink has 111 pure checks, 29 confirmed same-value no-write setter calls, and one separate spare-DPI modification attempt with unconfirmed immediate readback; independent read-only recovery confirmed all original settings. Full current evidence and three corrected read boundaries are in [Hardware-Validation.md](Hardware-Validation.md). Earlier offline observations and 33/80/93-check figures below are explicitly historical, not the current support state.

Owned x64 .NET Framework worker using Windows HID APIs. No ASUS DLLs or JavaScript are distributed or executed. Public official frontend was inspected in the research directory, and this worker was implemented separately. `build-gearlink-worker.ps1` builds and runs synthetic tests without touching hardware.

## JSON lines

Requests: `{id,op:"scan"|"read"|"set"|"firmware",kind:"headset"|"mouse"|"keyboard"|"omni",deviceId?,setting?,value?,experimental?:true,requireNoOp?:true}`.

Response: `{id,ok:true,kind,op,data:{...}}`; failures: `{id,ok:false,kind,op,error:{code,message}}`. `scan` returns `data.devices`; `read`/`set` return one device directly in `data`.

Device fields: `deviceId`, `model`, `kind`, `pid`, `receiverPid`, `receiverId`, `product`, `transport`, `route`, `connected`, `online`, `readbackConfirmed`, `writeConfirmed`, `capabilities`, `setCapabilities`, `snapshot`, `errors`. A stable ID hashes the HID control interface path; an OMNI routed ID includes the logical peripheral PID. No raw USB path or serial number is exposed.

`connected` means the interface/receiver exists. `online` requires a valid peripheral alive/connection response. `readbackConfirmed` means the connection query was answered, including a confirmed offline answer. Individual getter failures appear in `errors`; their values remain null or absent. `setCapabilities` contains only implemented setters with valid current getter data. The UI must require online and an explicit user action; requests must additionally include `experimental:true`.

A successful output alone returns `accepted:true,writeAttempted:true`, with `acceptanceEvidence` describing that this is HID output completion. `writeConfirmed:true` requires a subsequent getter that equals the requested value. `appliedState` is `confirmed` or `unconfirmed`. A spare mouse DPI change was attempted, but its immediate confirmation failed; original settings were independently confirmed afterward. Other physical setters remain unvalidated. Packet tests are synthetic; same-value calls do not prove modification. A failed confirmation must be displayed as unconfirmed. Never claim a bug is repaired based on these queries.

If the fresh before-getter equals the requested setting, the complete strict packet validator runs first, then the write is skipped: `accepted:false,writeAttempted:false,writeConfirmed:true,appliedState:"alreadySet"`. `setting` and `requestedValue` remain in the response. DPI no-op compares both X and Y. For read-only chain tests use `requireNoOp:true`; a changed value is rejected before HID output. Initial evidence included fourteen real headset no-op chains, a changed-value rejection, and 33 pure checks. The local evidence JSON is private and excluded from distributions. Current keyboard/mouse results are linked above.

Read transport uses native OVERLAPPED I/O with one absolute900ms budget covering output and all input reports for a query, including a100ms cancellation reserve. CancelIoEx targets that exact operation and GetOverlappedResult must confirm completion before native memory is freed. If cancellation remains incomplete, memory/handle/mutex are retained until process teardown: the worker returns error code `io_restart_required`, flushes its JSON result and exits4. Parent process should bound whole requests (including synchronous enumeration/open) and restart the isolated worker on stalls/disconnection. Owned Aura and GearLink workers share `Local\\RogControl.Hid.<64-character SHA256 uppercase hex of lowercased path UTF8>` mutexes and reject concurrent opens immediately. HID queues are not flushed. The worker never enters native ASUS SDKs. Maximum input JSON length is65536characters. No arbitrary packets, firmware flashing, pairing changes, reset commands, or arbitrary XML are exposed.

Control allowlist includes only verified USB/RF model PIDs7546/7548(mouse),7586/7588(keyboard),7489(headset). Guessed contiguous IDs7547/7587 were removed. Local official dependency caches list BLE7549/7589, while official GearLink supports settings overUSB/RF; Bluetooth is not exposed as a working control route.

## Initial local inventory before keyboard/mouse wake, 2026-10-13

- DELTA II-KJP PID7489 / 0x1D41, usage FF00/1, report CC. Online. Battery100, chargingtrue, headset/dongle firmware0.1.6.0, EQ disabled with all ten raw values50; eight visible125..16K bands are indices2..9. Sleep never, lighting disabled, brightness50, RGB82/0/111, noise reduction disabled/level1, voice prompt1, sidetone disabled/level10, latency60, Aura disabled. All current getters succeeded.
- OMNI RECEIVER PID6862 / 0x1ACE: paired AZOTH EXTREME EDITION20 PID7588, controlreport2/eventreport4. Keyboard connection query255 => offline. All settings null, setters empty.
- SPEEDNOVA8K RECEIVER PID6864 / 0x1AD0: paired HARPE II EXTREME EDITION20 PID7548, controlreport3/eventreport5. Mouse alive query0 => offline. All settings null, setters empty.

OMNI report assignment is dynamic: the report ID from the pairing table must choose the HID control collection, rather than assuming a usage page corresponds to mouse versus keyboard. Multiple matching physical collections are treated as ambiguous and no route is created. Current device model and route remain visible while a peripheral is asleep.

## Headset setters and snapshot fields

|Setting|JSON value|Snapshot getter|Allowed values / preservation|
|---|---|---|---|
|eqEnabled|boolean|eqEnabled|EQ switch|
|eqLevels|array of8integers|eqLevels|0..100,1step; preserves hidden raw bands0/1|
|sleepMinutes|integer|sleepMinutes|0never,2,3,5,10,15; preserves threshold+voice switch|
|lowBatteryThreshold|integer|lowBatteryThreshold|0,10,20,30,40,50; preserves sleep+voice|
|lowBatteryVoicePrompt|boolean|lowBatteryVoicePrompt|preserves sleep+threshold|
|brightness|integer|brightnessRaw|0..100 in5steps; preserves effect+RGB|
|lightingEffect|integer|lightingEffectCode|GearLink labels1恒亮,2呼吸,3闪烁,4彩色循环,5彩虹; disabled when current getter is unmapped0; preserves brightness+RGB|
|lightingColor|array of3integers|color|RGB0..255; available for current effect1..3; disabled at unmapped0; preserves effect+brightness|
|lightingEnabled|boolean|lightingEnabled|device lighting switch|
|noiseReductionEnabled|boolean|noiseReductionEnabled|microphone noise reduction switch|
|noiseReductionRaw|integer|noiseReductionRaw|0low,1medium,2high|
|voicePrompt|integer|voicePrompt|0prompt sound,1English,2Chinese|
|sideToneEnabled|boolean|sideToneEnabled|sidetone switch|
|sideToneLevel|integer|sideToneLevel|0..20|
|latencyMs|integer|latencyMs|40,60,80|
|auraEnabled|boolean|auraEnabled|hardware Aura participation|

`eqBandHz=[125,250,500,1000,2000,4000,8000,16000]`. GearLink's own graphic EQ uses raw slider levels0..100; it does not show or transform them into dB. `eqGains` remains null, so UI must use `eqLevels`. `eqRawGains` retains all ten values. Default stored config and UI initialization values are never substituted for hardware readback.

Lighting mode discrepancy: current hardware getter returns0, while GearLink caps list1..5 and its getter/setter send raw values directly without an offset conversion. The current raw0 meaning is unproven and differs from Armoury Crate's own enum. Therefore the adapter sets `lightingModeMappingUnconfirmed:true`, disables effect/color changes, and lets only brightness preserve the raw current effect unchanged. Do not label raw0 as a specific mode.

## Peripheral setters prepared from exact public protocol

At the initial capture, mouse and keyboard were offline and setCapabilities was empty. After wake, valid live getter data enables only the corresponding implemented capabilities. Several OLED/selected binding getters still time out and their setters remain unavailable; see the current validation record.

|Kind|Setting|Value|Preservation and constraints|
|---|---|---|---|
|mouse|dpiStage|`{index:0..3,dpi:100..65000}`|Both X/Y set equally; retains the stage's current RGB. Requires split-DPI+RGB getters. Firmware caps from p725 specify65000; no guessed default|
|mouse|activeDpiStage|1..4|Must be within current active count; readback current stage|
|mouse|pollingRate|125,250,500,1000,2000,4000,8000|Uses current RF/booster getter slot. Reject8000 while MotionSync is enabled|
|mouse|angleSnapping|boolean|Only this bit's setter|
|mouse|motionSync|boolean|Reject enabling at8000Hz|
|mouse|sleepMinutes|0never,1,2,3,5,10|Retains threshold,full-charge effect,motion wake,charging lighting|
|keyboard|pollingRate|1000,2000,4000,8000|Keyboard enum differs from mouse enum|
|keyboard|sleepMinutes|0never,1,2,3,5,10|Single-field setter|

## Wire format and readback

Windows buffer byte0 is report ID; following bytes are `[command,key,indexLE16,data...]`, padded to the descriptor's output report length. Responses match the report ID and first4payload bytes; pairing-table A0 matches2bytes because response index stores pair count. `FF AA` errors and legacy error payloads are rejected. Notifications are ignored during a query. No unsolicited event performs a setting.

Headset reportCC:

|Operation|Command/key/index(hex)|Response data after4byte header|
|---|---|---|
|alive|12/00/0100|status1online|
|firmware|12/00/0000|4headsetversion,4dongleversion (reversebyteorderhex)|
|power|12/07/0000|sleepmode,battery,threshold,voice switch|
|charging|12/08/0000|0/1|
|EQ|12/21/0000|switch,10rawlevels|
|EQ switch set|41/03/0000|boolean byte|
|EQ levels set|41/04/0000|all10preserved/updated levels|
|power set|51/37/0000|sleepmode,threshold,voice switch|
|lighting|12/03/0000|effect,brightness,R,G,B|
|lighting set|51/28/0000|effect,brightness,R,G,B|
|lighting switch|12/13/0000 =>51/10/0000|0/1|
|noise reduction|41/20/0000|switch,level|
|noise reduction switch set|41/02/0000|0/1|
|noise reduction level set|41/10/0000|0..2|
|voice|12/28/0000 =>41/0A/0000|0..2|
|sidetone switch|12/24/0000 =>41/11/0000|0/1|
|sidetone level|12/19/0000 =>61/11/0000|0..20|
|latency|12/52/0000 =>51/52/0000|40/60/80|
|Aura|12/33/0000 =>51/33/0000|0/1|

OMNI generic report1: A0/00/0000 reads pair table, response indexLE16 count then repeated4bytes `{pidLE16,controlReport,eventReport}`. A1/01/0000 reads receiver firmware. No pairing/set mode command is implemented.

Mouse routed report: alive12/00/0002; info12/00/0000; performance12/04/0000;splitDPI12/04/0002;DPIcolors12/04/0003;MotionSync12/04/0004;power12/07/0000. DPI integer values are encoded`dpi-1` with stepbase1 for p725. Split stage setter51/31/index12..15 has`XLE16,YLE16,RGB`; active stage51/31/0009;rate51/31/0004;angle51/31/0006;sync51/31/0012;power51/37/0000 has7data bytes`mode,0,threshold,0,fullEffect,motionWake,chargingLighting`.

Keyboard: connection12/03/0000 (0USB,1RF,2BT,255offline);info12/00/0000;power12/01/0000;pollingindex12/15/0000 (0..3=1000/2000/4000/8000);rate setter51/31/0000;idle setter51/38/0000.

## Coverage boundary and official sources

[Official GearLink FAQ](https://rog.asus.com/support/faq/1054795/) lists these three models, USB/RF configuration, and additional Companion requirements for the keyboard. Hardware control through HID is workable; Windows audio playback/recording settings and software audio effects remain a distinct Companion service path. Aura Sync orchestration, Windows Dynamic Lighting, full profiles/macros, OLED media upload, firmware flashing, and device updates are not fully implemented by this worker. Basic OLED/lever controls and manual firmware catalog reads are now prepared below.

Public source acquisition: [GearLink](https://gearlink.asus.com/en); `/view/7489/manifest.json` v1.00.35 with main/preload and bundles; `/view/7548/manifest.json` v1.00.15; `/view/7588/manifest.json` v1.00.09; `/view/6862/manifest.json` v1.00.24; `/view/6864/manifest.json` v1.00.12. Official local-service manifests list KB Companion3.1.0.10 and Audio Companion1.0.0.35. Public vendor sources stay in `work/reverse/gearlink` for analysis and are not shipped in output artifacts.

## Extended mouse and keyboard settings

`data.settingOptions` contains public protocol choices; these are option catalogs, not readback values. They may be shown while offline, but no setter is enabled unless `data.online=true` and the setter occurs in `data.setCapabilities`.

|Kind|Setting|Request value|Snapshot field|Confirmed protocol bounds|
|---|---|---|---|---|
|mouse|zoneSettings|`{enabled:bool,pollingRate:int,dpi:int,dpiY:int,txPower:int}`|zoneSettings with same fields|rate125/250/500/1000/2000/4000/8000; X/Y100..65000; txPower0FullPower/1EnvironmentAdaptive|
|mouse|buttonBinding|`{button:string,target:string,shift:bool}`|buttonBindings or shiftButtonBindings|8 physical sources in settingOptions.buttonSources; semantic target in buttonTargets|
|keyboard|oledEnabled|boolean|oledEnabled|display on/off|
|keyboard|oledBrightness|integer|oledBrightness|0..100, integer steps|
|keyboard|oledMode|integer|oledMode|0Animation,1Clock,2LaptopBattery,3HWInfo,4KPM|
|keyboard|oledModeEnabled|`{mode:0..4,enabled:bool}`|oledModeStates array of5booleans|selectable-mode switch|
|keyboard|oledKpsEnabled|boolean|oledKpsEnabled|dynamic animation switch|
|keyboard|oledPreset|integer0..5|oledPreset|getter may return6Custom/7BannerFont; setter only predefined0..5|
|keyboard|oledBarItem|`{item:string,enabled:bool}`|oledBarInfo dictionary|item in oledBarItems; preserves all other status bits including unknown reserved bits|
|keyboard|leverMode|integer0..7|leverMode|0MediaVolume,1MediaContent,2LightBrightnessPerKeyZone,3LightBrightnessOLED,4LightEffectSwitch,5FanControlMB,6FanControlAIO,7Customize|
|keyboard|leverModeEnabled|`{mode:0..7,enabled:bool}`|leverModeStates array of8booleans|mode switch; fan operations themselves are not implemented|
|keyboard|keyBinding|`{source:string,target:string}`|keyBinding object|selected source/target from settingOptions.keySources/keyTargets; no arbitrary numeric command|

Mouse binding readback is `{leftButton:{target:string|null,targetCode:int},...}` with sources `leftButton,rightButton,middleButton,sideKeyLeftBackward,sideKeyLeftForward,dpi1,scrollUp,scrollDown`. Unknown targets remain null plus the actual raw code. Normal and Shift maps are read independently. A map that failed its getter cannot be written. `buttonBinding` compares the selected map/source actual target code strictly, and `zoneSettings` compares all5fields for same-value detection.

Keyboard binding reads are selected, to avoid a slow all-key scan. Request:
```json
{"id":1,"op":"read","kind":"keyboard","deviceId":"...","setting":"keyBinding","value":{"source":"KeyA"}}
```
Successful snapshot adds `keyBinding={source:"KeyA",sourceCode:31,target:"KeyB"|null,targetCode:50,lever:false}` and `setCapabilities` adds `keyBinding`. The following setter includes both source and target. For setters the worker always reads the selected source fresh before building the packet; no UI cache enables an unsupported source. `LeverUp/LeverMid/LeverDown` use a distinct getter/setter and m709's custom-lever index7. Undefined targets are never replaced with the original source key. Backslash is excluded pending country/layout-specific validation. Other keys outside the current allowlist, FN layers, combined keys and macros remain unsupported.

Keyboard command/key/index (hex): OLED firmware23/00/0000; switch23/02=>69/00; brightness23/03=>68/00; bar23/04=>51/60; currentmode24/01=>6A/01; modestate24/02=>6A/00 `{mode,bool}`; KPS24/03=>6B/00; animation21/00=>61/00. Lever current22/01=>71/02; lever states25/0B=>71/00. For m709 getter states use offsets4,5,8,9,10,11,12,13, skipping magnetic-axis fields6/7; the public model subclass confirms this. Regular key GET25/A0/0000 sends sourceLE16 and reads targetLE16 at offset6, SET51/20/indexSource sends targetLE16. Lever GET25/0C/0000 sends `7,sourceLE16`, reads targetLE16 at offset4; SET71/01/0000 sends `7,sourceLE16,targetLE16`.

Mouse advanced Zone GET12/14/0001 reads `enabled,rateIndex,XMinus1LE16,YMinus1LE16,txPower`, SET51/44/0001 uses the same data. Normal binding GET12/05/0000 and Shift GET12/05/0007 return8LE16 target codes; SET51/21 or51/23 index0 sends `buttonCodeLE16,targetCodeLE16`. Physical mouse source codes are496/497/498/484/485/486/488/489; keyboard native positions differ (KeyA31 versus mouse keyboard-function KeyA4). The transport explicitly rejects 25/25,25/26,25/27 wheel setters from the read-only path and does not allow OLED image-upload62.

## Manual official firmware checks

Request `{"id":1,"op":"firmware","kind":"headset"}` (or mouse/keyboard). No deviceId, HID discovery or hardware access occurs. Success data contains `kind,model,pid,sourceUrl,fetchedAtUtc,lastModifiedUtc,etag,sha256,toolVersion,deviceFWVersion,dongleFWVersion,boosterFWVersion,toolUrl,signatureVerification,updateToolDownloaded:false,hardwareAccess:false`.

Only fixed HTTPS manifest paths on gearlink.asus.com are fetched. Windows-bundled curl is used because the legacy .NET HTTPS stack fails negotiation on this machine; certificate verification stays enabled, user curl configuration is disabled, redirects are rejected, download size is bounded to64KiB, network max time8s and child-process timeout9.5s. The update ZIP is never downloaded or run. SHA256 records observed manifest bytes; it is not an ASUS signature. The manifest has no published signed digest, so `signatureVerification="manifest_does_not_provide_signature"`. Tool URLs must remain on the exact official path and end in.zip.

|Model canonical RF PID|Official device FW|Official receiver FW|Official booster FW|Tool version|Manifest Last-Modified UTC observed2026-10-13|
|---|---|---|---|---|---|
|7548 HARPE II EXTREME EDITION20|1.00.15|9.00.10|not published|1.00.09|2026-06-24 03:02:50|
|7588 AZOTH EXTREME EDITION20|1.00.09|7.00.08|2.00.13|1.00.04|2026-09-16 10:03:32|
|7489 DELTA II-KJP|0.1.6.0|0.1.6.0|not published|0.1.1.2|2026-03-18 08:35:09|

Compare the device's actual firmware string to `deviceFWVersion`, and receiver firmware to `dongleFWVersion` only where a verified matching receiver is known. ToolVersion is not the hardware firmware. A matching string means matching the version published in that manifest at the check time; it does not prove there is no other release. Offline/failed hardware reads stay unknown. Do not label the whole peripheral up-to-date just because a receiver matches. Actual headset/dongle getters currently match0.1.6.0. At the initial offline capture mouse/keyboard firmware was unknown; subsequent online reads confirmed 1.00.15 / 1.00.09 respectively. OMNI6862/6864 returned7.00.08/9.00.10; these are3component hex/BCD version fields, unlike headset's4component version.

Historical local evidence (not distributed): `gearlink-firmware-catalog.json` and `gearlink-extended-evidence.json`. The last file contains80synthetic checks (15prior keyboard/mouse,18headset,47extended),3official catalog reads (~700ms each),3live route scans, and `hardwareWrites:0`. Earlier14headset same-value no-op evidence remains in `gearlink-readonly-evidence.json`.

Historical regression before keyboard/mouse wake: after native bounded-I/O migration, `gearlink-readonly-evidence.json` was refreshed:93synthetic checks (original80 plus13I/O lifecycle/mutex/PID allowlist checks),14live headset same-value no-op calls all returned `writeAttempted:false,accepted:false,writeConfirmed:true`, changed no-op-only value rejected before output. Headset scan721ms; repeated same-value reads721–736ms; OMNI217ms; mouse204ms and keyboard206ms bothoffline. Zero physical setting writes. Timeout cancellation lifecycle tests use injected fake operations and never open HID.

## DSP / Companion gap

Audio Companion has a separate official WebSocket broker range localhost8900..8999. Static protocol broker query is `{"class_id":10010000,"pid":7489}`; expected response `{status:200,payload:{class_id:10010000,pid:7489,port}}`. The device port then accepts GET20010000 with eptype0(playback) or1(recording), GET20020000(audioeffects), GET20021000(PEQenable), GET20021002(PEQnodes), GET20022000(EQsupportedprofiles), with matching class_id/pid. Companion effect fields include `reverb_idx/reverb_onoff/reverb_value`, `bassboost_onoff/bassboost_value`, `compressor_onoff/compressor_value`, `noisegate_onoff/noisegate_value`, `perfectvoice_onoff/perfectvoice_value`, `voiceclarity_onoff/voiceclarity_value`, `vr_onoff/vr_distance/vr_level/vr_angle_fl/fr/rl/rr` and PEQ fields. Hardware graphic EQ remains the distinct HID8displayband/10rawband route.

Current read-only TCP inventory found no listener in8900..8999. An existing ROGKBPlugin listener on8885 is a keyboard path; it is not evidence of Audio Companion availability. The adapter does not connect/reset/restart/install Companion automatically. Full software audio enhancements and PEQ cannot be claimed working without a confirmed Companion getter or an owned audio-processing path with verified APO integration. RESET10010001 and effects/system setters are not part of this worker. Profile persistence, shortcuts, OLED host sensor streams and fan actions also need an owned host component; changing a lever/display mode alone does not implement those external functions.
