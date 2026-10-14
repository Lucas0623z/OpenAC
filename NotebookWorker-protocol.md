# Notebook and handheld read-only worker

`NotebookWorker.cs` is an independently written, .NET Framework 4.8 Windows process. It uses `System.Management` for identity and installed-driver metadata, then a restricted ASUS System Control Interface status route. It does not link, launch, or redistribute G-Helper or an ASUS application/DLL. It must be supervised by the main application; native queries are never run on the GUI thread.

Compile with Framework64 `csc.exe`, `/platform:x64`, `/r:System.Web.Extensions.dll` and `/r:System.Management.dll`. It takes no command-line arguments and reads one JSON object per stdin line, with one JSON response per line on stdout. Maximum request length is 16384 characters.

## API

```json
{"id":1,"op":"scan"}
{"id":2,"op":"read"}
{"id":3,"op":"read","features":["cpuFan","gpuFan","cpuTemperature","chargeLimit"]}
```

`scan` reads only Windows identity and metadata. It never opens `ATKACPI`. `read` rereads identity every time, so a previously eligible snapshot cannot authorize a different/unknown machine. Default status selection is `cpuFan`, `gpuFan`, `performanceMode`, `chargeLimit`. Selection must contain 1–12 distinct allowlisted names; null, an empty array, duplicates, nonstrings, or any unknown name are rejected. Only `id`, `op` and (for read) `features` are accepted. Callers cannot supply manufacturer, model, path, endpoint, method, control status or XML.

Response envelope is `{id,ok,data,restartRequired}` or `{id,ok:false,error:{code,message},restartRequired}`. An ineligible identity returns `ok:true` with `eligibleForStatus:false`, a specific `supportReason`, empty states/capabilities, and `driverQueryPerformed:false`; this is a completed identity read, not a supported device-control result. JSON serials, UUIDs, hardware instance IDs and user names are not returned.

## Identity and driver gates

All gates must hold before an ASUS native handle is opened:

1. One confirmed `Win32_ComputerSystem` result, with exact normalized manufacturer `ASUS` or `ASUSTeK COMPUTER INC.`; a substring or reseller name does not match.
2. At least one `Win32_SystemEnclosure.ChassisTypes` entry, and **every** entry must be a mobile type: 8/9/10/11/14/30/31/32. An unknown type or any desktop enclosure blocks native I/O.
3. A boundary-delimited exact model code from the prepared table. Partial prefixes and contradictory prepared model codes do not match. This uses OS-reported identity, not cryptographic hardware attestation.
4. Windows signed-driver metadata must identify `ASUS System Control Interface`, `... V1` or `... V3`, with an ASUS provider and `IsSigned:true`. Metadata is checked using `Win32_PnPSignedDriver`; the worker neither installs the driver nor requests elevation. Actual handle-open/IOCTL permission errors are reported explicitly.

| Exact model code | Prepared family | Official identity source |
|---|---|---|
| RC71L | handheld | [ROG Ally (2023)](https://rog.asus.com/gaming-handhelds/rog-ally/rog-ally-2023/) |
| RC72LA | handheld | [ROG Ally X (2024)](https://rog.asus.com/gaming-handhelds/rog-ally/rog-ally-x-2024/) |
| RC73YA | handheld | [ASUS Armoury Crate SE introduction, product list](https://rog.asus.com/support/faq/1050220/) |
| RC73XA | handheld | [ASUS Armoury Crate SE introduction, product list](https://rog.asus.com/support/faq/1050220/) |
| GA402RJ | ROG notebook | [ASUS exact support model](https://www.asus.com/jp/supportonly/ga402rj/helpdesk_download/), [ASUS SCI V3 model list](https://www.asus.com/support/images/upload/file/20211216093201978_ASCIv3modelist211215.pdf) |
| GU605MI | ROG notebook | [ASUS G16 (2024) specs](https://rog.asus.com/ru/laptops/rog-zephyrus/rog-zephyrus-g16-2024/spec/) |
| FX507ZC4 | TUF notebook | [ASUS exact support model](https://www.asus.com/supportonly/fx507zc4/helpdesk_download/) |

This is a small source-prepared set. No portable ASUS machine was available for physical query validation. `hardwareReadValidated:false`, `physicalValidation:false` and `supportsWrite:false` remain explicit. All other ROG/TUF/ProArt/Vivobook/Zenbook/ExpertBook variants remain identity-only until their exact model and route are prepared. A prepared model does not imply every listed endpoint is present; each response must independently confirm presence.

## Restricted status interface

The only native control code is `0x0022240C`, with method `DSTS` (`0x53545344`) and a 16-byte little-endian input: method word, argument-size word 8, allowlisted endpoint word, zero control-status word. Windows opens the canonical device path `\\.\ATKACPI` with the access required by the installed interface. This handle access does not authorize setting operations: there is no DEVS, INIT, watchdog, event subscription, endpoint scan, or caller-controlled command encoder in this worker. Return status and returned-byte count must be valid; only 4–16 returned bytes are parsed.

| Feature | DSTS endpoint | Prepared response interpretation |
|---|---|---|
| cpuFan | 0x00110013 | Native 0–120 × 100 RPM |
| gpuFan | 0x00110014 | Native 0–120 × 100 RPM |
| middleFan | 0x00110031 | Same RPM interpretation, notebook only |
| cpuTemperature | 0x00120094 | Temperature in C |
| gpuTemperature | 0x00120097 | Temperature in C |
| chargeLimit | 0x00120057 | Native status value 0–100; current configured setting unconfirmed |
| chargerMode | 0x0012006C | 1 barrel / 2 USB |
| performanceMode | 0x00120075 | Native status value 0–4; current BIOS mode unconfirmed |
| gpuEco | 0x00090020 | Native status 0/1, notebook only |
| gpuMux | 0x00090016 | Native status 0/1, notebook only |
| keyboardBrightness | 0x00050021 | Low-byte level 0–3; notebook only |
| displayOverdrive | 0x00050019 | Native status 0/1, notebook only |

The source defines presence flag `0x00010000`; `0xFFFFFFFE`, `0xFFFFFFFF`, missing presence, or unknown high flags produce unknown values. Known presence/user/BIOS flags are separate from the value. `readbackConfirmed` confirms the native status word, while `valueReadbackConfirmed` confirms only the interpretation implemented here. Selected mode/charge/display/GPU endpoints use `valueSemantics:"nativeStatus"`, `queryValue` with **`value:null`**: the adapter does not infer the current configured setting from a capability/status result. Fan speed and temperatures have interpreted values only after validation. Temperature 0–150 is a conservative parser sanity bound, not a hardware setting range. Out-of-range responses keep the endpoint presence but do not claim usable read support. A missing endpoint produces unknown state and does not make other features disappear.

## Blocking and concurrency

WMI and synchronous ASUS driver calls run in a background thread within the isolated worker. The complete request has an **8-second** deadline. Timeout returns `QUERY_TIMEOUT` with `restartRequired:true`, then the worker flushes the reply and exits with code 4. It cannot be reused. A late identity result is checked against quarantine before opening a device, and every next status query checks quarantine. A native Win32 failure ends the batch immediately; it does not retry or continue probing other endpoints. The application should also enforce its process deadline and never respawn/poll a timed-out route automatically.

`Local\OpenAC.Notebook.Status` serializes actual native query batches between OpenAC processes. It is held only by the background query thread and released after the handle is disposed. It cannot coordinate with the ASUS application or G-Helper; users should avoid simultaneously changing the same notebook settings while assessing a snapshot. No service is stopped or modified.

## Explicit unsupported scope

Every setting operation is rejected before identity or native I/O, regardless of the GUI experimental-write switch. No notebook fan curve, calibration, power limit, charge-limit setter, GPU mode switch, undervolt, keyboard RGB, AniMe Matrix, Slash lighting, Ally gamepad remapping, hotkey subscription, display change, driver install or firmware update is implemented here. Those paths have model-specific requirements; importing G-Helper's advertised feature list would not validate OpenAC. Desktop Fan Xpert uses a different installed AiSuite SDK path and is not made functional by this notebook adapter.

## Provenance

Protocol study was fixed to [G-Helper e897b278bf809add90c904ba84aa2e451579dd0c](https://github.com/seerge/g-helper/tree/e897b278bf809add90c904ba84aa2e451579dd0c):

| Private reference | SHA-256 |
|---|---|
| `app/AsusACPI.cs` | C4D9273B43543ADA9E79F811293EF54961C88F48E81817C13E8A6E0609ADBAFE |
| `app/AppConfig.cs` | 363EDB8C3965FD685A28A9233212A19AAB62F0A11F66283FC8884CB6D5AA40FC |
| `LICENSE` (GPL-3.0) | 3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986 |

Selected endpoint IDs, DSTS packet fields and RPM units were studied as interface facts. G-Helper's constructor, state setter, fan-curve correction, fallback writes, model heuristics, GUI and whole data tables were not incorporated. This worker's identity gating, parser, allowlist selection, JSON protocol, deadline/quarantine and tests were independently written for OpenAC. It references no GPL application library or assembly. Private reference files remain outside the public repository in `work/reverse/ghelper`.

Presence masks and selected endpoint facts were cross-checked against [Linux asus-wmi.h at 24a19b953e2a3d0ea5a8c4951a0ab8f32720c907](https://github.com/torvalds/linux/blob/24a19b953e2a3d0ea5a8c4951a0ab8f32720c907/include/linux/platform_data/x86/asus-wmi.h), SHA-256 C5B861725B9BC842C8F55B3215E4C173A7B350E95BDD05B319EB823E49F26F99. The GPL header itself is private reference material, not redistributed or included by the build. Windows API declarations and WMI fields follow [DeviceIoControl](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-deviceiocontrol), [Win32_ComputerSystem](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-computersystem), [Win32_SystemEnclosure](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-systemenclosure), and [Win32_PnPSignedDriver](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/aa394354(v=vs.85)) API documentation. ASUS documents the installed [System Control Interface driver requirement](https://www.asus.com/support/faq/1047338/).

## Validation

The GUI exposes a manual **Tools → 笔记本与掌机 → 检测设备** entry. It requests identity first and reads only the fixed status features for an eligible model. Rendering, navigating, and the one-second expiry timer make no notebook query. Observations expire after 30 seconds; unknown query bits are not labelled as the configured BIOS mode, GPU mode, or charge limit. `NotebookGuiTests.cs` adds 766 fake-backend checks and 26 unshown layouts. GUI cancellation and timeout quarantine share a stable AppDomain request gate across preview assemblies. NotebookWorker is the seventh installed runtime worker; staging it does not imply mobile hardware verification.

`NotebookWorkerTests.cs` uses injected identities and fake native responses only. It exercises desktop/non-ASUS/unknown/ambiguous model rejection, all enclosure gates, driver gating, refreshed identity snapshots, exact DSTS fields, unsupported responses, bounds and flag parsing, mode-status uncertainty, write/arbitrary-command rejection, fail-fast driver errors, timeout quarantine and late-completion prevention. Initial suite: **117 checks passed**; WMI queries 0, ATKACPI opens 0, hardware writes 0. Runtime validation on this desktop should report an ineligible mobile gate and perform no ASUS IOCTL.
