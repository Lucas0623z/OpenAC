# Windows screen brightness

Independent MIT implementation. No G-Helper functions, vendor SDK, driver installation, WMI authentication change, or elevated caller is used. The source implements Microsoft's fixed monitor-brightness interfaces.

Microsoft defines `Active`, `CurrentBrightness`, `Level`, `Levels`, and the `InstanceName` key in [WmiMonitorBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightness). Current brightness must be one of the returned supported levels. [WmiMonitorBrightnessMethods](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorbrightnessmethods) supplies the exact instance key and active state for the setter. [WmiSetBrightness](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmisetbrightness-method-in-class-wmimonitorbrightnessmethods) accepts a timeout in seconds and a brightness percentage; return code zero means method success.

These facts do not establish that every desktop monitor, laptop panel, or ASUS model exposes this provider. OpenAC does not infer a 0–100 slider or associate a WMI instance with a GDI display number. The supported values are only the actual `Level` array. Unsupported or ambiguous instances remain unavailable. The optional name query uses only `Active`, `InstanceName`, `UserFriendlyName`, and `UserFriendlyNameLength` from [WmiMonitorID](https://learn.microsoft.com/en-us/windows/win32/wmicoreprov/wmimonitorid). It does not query serial numbers, product codes, or manufacture data; absent or invalid friendly names use a generic screen number.

## Runtime boundary

`WindowsBrightness.cs` contains the pure service, validation, and fixed protocol model. `WindowsBrightnessWorker.cs` is the only file containing `System.Management` calls. It runs as the current user with fixed local namespace `\\.\root\WMI`; clients cannot select namespace, class, method, WMI path, or credentials. Its only hardware-writing method is `WmiSetBrightness` with `Timeout=0` and a supported byte brightness. It does not invoke policy restore, ambient-light control, calibration, or another method.

The GUI constructs the worker lazily and uses `WorkerClient.Request` with a 10-second total queue/request budget. A timeout terminates the owned worker process. WMI's native call may run in an OS provider process, so killing the client is **not** proof that a submitted setting was cancelled. A setter timeout, disposal while a setter is pending, a transport failure after the final send boundary, or a returned `METHOD_EXCEPTION`/`INVALID_METHOD_RESULT` disables further brightness writes for the current app session using an AppDomain marker that survives preview reloads. Explicit reads remain available and never clear that marker. No automatic retry or restore follows an uncertain result.

The worker's five-second WMI method option can return an exception before the outer ten-second deadline while provider work continues. Its sanitized exception reply therefore triggers the same sticky restriction even though the worker replied promptly. An invalid method result means invocation was attempted but no valid completion result was obtained. These categories express **unknown completion/effect**, not proof that the method is still running. By contrast, a completed nonzero method result or a zero-result/readback mismatch is not automatically classified as an ongoing method call; it is still displayed as unconfirmed and never falsely applied.

The WMI enumeration and method options also request short timeouts as a secondary limit. They are not presented as proof of cancellation; the real GUI isolation boundary is the supervised process. Microsoft documents the synchronous [ManagementObject.InvokeMethod](https://learn.microsoft.com/en-us/dotnet/api/system.management.managementobject.invokemethod?view=netframework-4.8.1) API used inside that process.

## JSON lines

The worker uses `WorkerTextProtocol.ReadLine()` and emits one JSON response per request. Error output contains a fixed category, never native exception text or a raw monitor instance path.

Read request:

```json
{"id":1,"op":"read"}
```

Success response data:

```json
{
  "schema":1,
  "sessionToken":"32 lowercase hex characters",
  "readToken":"32 lowercase hex characters",
  "capturedAtUtc":"UTC round-trip timestamp",
  "expiresAfterMilliseconds":30000,
  "monitors":[{
    "id":"brightness- plus 64 lowercase hex characters",
    "name":"System-reported friendly name or generic screen number",
    "currentBrightness":50,
    "levels":[0,25,50,75,100],
    "supportsWrite":true,
    "fingerprint":"64 lowercase hex characters",
    "unavailableReason":null
  }]
}
```

The levels in this example are illustrative. Each real instance must report its own exact levels. Identifiers and fingerprints use an unexported, per-worker salt. Raw instance keys remain internal. IDs are not a physical display association or a credential that can survive a restarted worker. Read tokens are replaced by every new read.

Set request:

```json
{"id":2,"op":"set","targetId":"brightness-...","brightness":75,"sessionToken":"...","readToken":"...","currentFingerprint":"...","experimental":true}
```

Before any setter, the service checks strict input types, exact opaque target/session/read token, the original fingerprint, and a 30-second monotonic lease. The current brightness, capabilities, active state, and exactly one active method instance must match an independent fresh read. Expiry is rechecked after that read. Only an enumerated supported brightness can be sent. A same-value request performs the fresh getters and sends no setter. Each transaction consumes the old lease, including a no-op or attempted write.

Set result data reports `alreadyCurrent`, `writeAttempted`, `methodReturnCode`, `readbackConfirmed`, `originalBrightness`, `observed`, and `errorCode`. The original value is evidence only. A changed value is confirmed only when the method returns zero and an independent subsequent read finds the same instance and levels with the requested brightness. Nonzero return, exception, missing instance, changed capabilities, or readback mismatch remains unconfirmed. There is no restore-point API.

## GUI integration

Add `WindowsBrightness.cs` and `WindowsBrightnessStructure.cs` to the GUI compilation. Build the worker from `WindowsBrightness.cs`, `WindowsBrightnessWorker.cs`, and `WorkerTextProtocol.cs`, referencing `System.Management.dll` and `System.Web.Extensions.dll`.

The root window calls `InitializeWindowsBrightness()` during setup, `AddWindowsBrightnessControls()` on the screen-settings page, and awaits `DisposeWindowsBrightnessAsync()` while closing. Include `WindowsBrightnessCanPreviewReload` in the preview gate. The section's read button is named **读取亮度**, avoiding the existing display-page refresh button.

`IWindowsBrightnessGuiBackend` permits fake injection. The GUI client binds captures to its own owner, the original worker session, the original monitor object, and expiry. Every SET uses the supervisor's terminal `beforeSend` callback to recheck policy, closing, original snapshot, session, expiry, and quarantine after waiting for the worker gate. Selecting a supported level only edits the local draft. Rendering never invokes WMI, starts a worker, opens a window, or applies a setting.

`windowsBrightnessTask` also occupies the existing `windowsSystemTask` lifecycle slot. A one-second presentation-only expiry timer checks the visible, idle screen-settings page. It retires the capture/draft and redraws unknown state after the 30-second lease; it never initiates GET or SET. Rendering also validates expiry. The timer is not started in render-only mode and is detached/stopped on closing or disposal. Closing disposes the isolated transport and joins the tracked request. Brightness captures, tokens, worker sessions, pending requests, and executable permissions must not be restored from preview state.

## Verification

`WindowsBrightnessTests.cs` compiles with `WindowsBrightness.cs` and uses only synthetic raw-provider objects. `WindowsBrightnessGuiTests.cs` references the GUI assembly and uses fake transport plus unshown WPF layouts. Neither test runs WMI, displays a native window, starts a worker, or changes brightness. No real panel read/write validation has been performed by the author in this task.
