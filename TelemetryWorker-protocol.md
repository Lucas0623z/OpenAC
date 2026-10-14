# Installed AIO telemetry reader

This independently written x86 worker loads the installed `ArmouryAIOSDK.dll`. It does not distribute ASUS binaries, execute ASUS JavaScript, reuse authentication, impersonate an ASUS caller, install a driver, or issue hardware settings writes. Its only native XML function numbers are capability **0** and sensor **36**, device type **5**. The ordinary existing ABI loader `startService(0)` is required; this is not an SDK function INIT/calibration/stress request.

Source-prepared exact LCD models are 7299, 7124 and 7571, matching `SDKWorker-protocol.md`. The capability response must identify exactly the requested model. This preparation does not establish physical validation on every model. A successful capability read does not establish a working physical sensor provider.

## Line JSON

Requests and replies are one UTF-8 JSON object per line. Native stdout is redirected to stderr. There are no unsolicited stdout messages. `id` is echoed, including the timeout response. Errors contain `{code,message}`; raw native exception text is not exposed.

```json
{"id":1,"op":"scan","deviceId":"7299"}
{"id":2,"op":"read","deviceId":"7299","sensorIds":["sensor-1","sensor-2"]}
{"id":3,"op":"status"}
```

`scan` performs one GET0 and replaces the capability snapshot, clearing the old snapshot before native I/O. A failed refresh therefore cannot retain disconnected sensor identities. `deviceId` defaults to 7299 and otherwise must be an exact allowlisted string. `read` requires this worker's successful scan and 1–5 unique opaque IDs. Invalid/unknown/duplicate/ambiguous IDs and extra fields are rejected before native I/O. `status` only reports cached model/count and `hardwareSettingsWrites:0`; it never opens the SDK.

No `set`, caller/process override, raw XML, method number, arbitrary DLL path, INIT, calibration, stress or voltage-control operation exists.

## Identity and readings

Each sensor records native ID, native type, name and default name. Native IDs alone are insufficient: the actual capability contains ID 61440 for both Core #0 and Graphics, and identical ID/name/type tuples for multiple GPUs. Indistinguishable tuples have `selectable:false`; the worker does not assign them to a guessed GPU index. The value response must uniquely match the selected capability. Missing name/type may be accepted only when the remaining identity is unique across the entire capability snapshot.

The known source categories are type 0 voltage, 1 temperature, 2 fan speed, 4 frequency, and 6 other native data. **A category does not establish units.** In particular, type 6 includes different memory/load concepts. `unit` stays null unless the native capability explicitly supplies a recognized unit consistent with its category. Only a finite value with exact identity and a verified unit reaches `value` / `valueReadbackConfirmed:true`; otherwise the response keeps the finite number as `rawNativeValue` / `nativeValueReadbackConfirmed:true` with a concrete error. These raw numbers are not suitable for a unit-labelled dashboard.

Zero temperature/frequency has `value:null,placeholder:true` and `ZERO_TEMPERATURE_OR_FREQUENCY_UNCONFIRMED`. This conservative handling follows the actual powered local system's all-zero query, which supplies no validity flag. It does not claim every zero in every possible environment is impossible. Zero fan RPM with an explicit RPM unit remains a valid stopped-fan value. No board temperature, voltage rail, DRAM temperature or motherboard fan label is synthesized from a generic AIO sensor or EURUX controller port.

## Native isolation

One permanent STA owns all native state. Each request has a **25-second** process-local deadline. A blocked loader/query yields `restartRequired:true`, code `SDK_TIMEOUT`, then process exit **4**. The blocked background native thread is not aborted or reused; the supervisor must terminate/restart the process. There is no retry. A supervisor should retain a slightly longer outer process deadline, and should not continuously respawn a failed provider.

`Local\OpenAC.AioSdk` is acquired without waiting around loader/query/free operations. SDKWorker's LCD transport uses the same mutex. Concurrent OpenAC AIO requests return `SDK_BUSY` rather than queueing native calls. This coordinates owned workers, not unrelated vendor processes. It does not assert ASUS services are reentrant. Native output is bounded to 4 MiB UTF-16LE and freed by the SDK export; XML has no DTD/external resolution.

## Actual read-only evidence (2026-10-13)

One permitted bounded x86 probe called GET0 then one GET36 on five unique sensors: Core #1 (61456/type4), Core #2 (61472/type4), GPU (67117057/type1), Memory (61441/type4), Memory (8195/type1). It completed in about four seconds, loader status0/process exit0. Every returned value was **0**; no unit or GPU-device identity was returned. Thus this local provider currently cannot supply confirmed live frequency or temperature through this route. No subsequent native probe or settings write was run for this implementation.

Private evidence: `work/reverse/telemetry/sensor36.out.txt` and the independent probe source. The capability itself has no CPU Package/Motherboard/VRM/Chipset temperature, CPU/CPU OPT/Chassis/Radiator/Water Pump motherboard fan, VCore/SOC/VDDIO or +12/+5/+3.3V rail entry.

Read-only WMI metadata exposed `root\wmi:ASUSManagement`, but querying its ordinary instance returned **Access Denied**. No WMI method, low-level SMBus/ASIO operation or permission bypass was attempted. Static installed MB_Home/FAN/AiSuite DLL strings identified `Global\cpuzlock`, `Global\ASUS_ISABUS_MUTEX` and ASUS SIO mutexes; these are locks, not an established telemetry mapping. No public stable readable shared-memory schema was located. Installed components importing file mapping APIs alone do not prove a sensor IPC interface.

Windows WMI/performance counters, native memory/time APIs and documented NVIDIA telemetry are handled independently by `WindowsTelemetry.cs`. These cannot be represented as verified motherboard voltages or header RPMs unless their actual source identifies those measurements.

## Pure verification

Compile worker using Framework x86 csc with `System.Web.Extensions.dll`; compile tests from both sources with `/main:TelemetryWorkerTests`. Neither pure suite loads an installed DLL or opens a native device. Result: **91 checks passed**, installed DLL loads0/native opens0/hardware writes0. SDKWorker's related zero-placeholder regression result: **313 checks passed**, installed native DLL loads0/hardware writes0.

Example commands (output goes outside the public source):

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /target:exe /r:System.Web.Extensions.dll /out:..\..\validation\TelemetryWorker.exe TelemetryWorker.cs WorkerTextProtocol.cs
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" /nologo /platform:x86 /target:exe /r:System.Web.Extensions.dll /main:TelemetryWorkerTests /out:..\..\validation\TelemetryWorkerTests.exe TelemetryWorker.cs WorkerTextProtocol.cs TelemetryWorkerTests.cs
& ..\..\validation\TelemetryWorkerTests.exe
```

The examples compile and run synthetic tests only. Running the compiled worker's `scan/read` performs installed-native I/O and is deliberately excluded from the test instructions.
