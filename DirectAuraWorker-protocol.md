# Direct Aura USB worker

The x64 worker implements newline-delimited JSON on stdin/stdout. It talks only to the identified ASUS Aura motherboard HID collection, without loading Armoury Crate SDK/HAL components. A process supervisor must retain its own finite deadline because Windows HID enumeration and opening a handle have synchronous metadata calls.

## Requests and replies

- `scan`: closes previous handles, enumerates the allowlisted ASUS motherboard collection, sends firmware query `EC82` and configuration query `ECB0`. This performs no lighting write.
- `status`: returns cached device descriptors, control/effect state and warnings, with no device I/O.
- `apply`: requires device IDs from a successful scan; supports `static`, `breathe`, `cycle`, `off`, a `#RRGGBB` color and brightness. Every effect packet is validated before the first write. Firmware speed, strip LED count discovery, old-lighting restore and individual physical ARGB LED addressing remain unsupported.
- `release` / `shutdown`: close handles without a device command. The last hardware effect may remain active.

Replies include `id`, `ok`, `error`, `devices`, `controlled`, `effect`, `warnings`, `backend:"native-hid"` and `restartRequired`. These error replies do not claim that a timed-out effect packet was never delivered. A cancellation-completion race explicitly reports that a write may already have reached the device.

## Bounded device I/O

`NativeHid.Query` uses one monotonic 1200 ms budget for the query write and all input reports, including unrelated replies. An individual effect write has a 1000 ms budget. `AuraBoundedIo.Transfer` uses Windows `ReadFile`/`WriteFile` with a separately allocated `OVERLAPPED`, a manual-reset completion event and an unmanaged report buffer. It waits for completion with a finite `WaitForSingleObject` interval. The final 100 ms of each remaining budget is reserved to request `CancelIoEx` for that exact operation and check its terminal completion with `GetOverlappedResult(..., false)`.

`CancelIoEx` acknowledging a request is not proof of completion. If cancellation remains incomplete at the deadline, the buffer, `OVERLAPPED` and event are retained because the driver may still use them. The worker becomes poisoned, rejects any further native I/O, returns a JSON failure with `restartRequired:true`, flushes stdout, then exits with code 4. Process teardown owns cleanup. `Dispose` deliberately does not recycle that handle or release its ownership mutex before termination; poisoned `NativeHid` objects are kept in a static root so `SafeFileHandle`/mutex finalizers cannot close them between reply serialization and process exit. A supervisor can start a fresh worker for a later explicit action, but should not automatically repeat a failed hardware write.

The previous `FileStream.ReadAsync/WriteAsync` cancellation tokens are removed. They did not provide a reliable cancellation deadline for Windows HID handles. `HidD_FlushQueue` is also removed: it has no bounded cancellation contract and can consume replies needed by another application.

Every owned worker uses the same interface ownership name:

`Local\RogControl.Hid.<uppercase full SHA256 of UTF8(path.ToLowerInvariant())>`

Ownership is acquired with `WaitOne(0)` and held for the opened interface's lifetime. A second cooperating worker immediately receives a busy error. Armoury Crate does not use this mutex, so contention with it is reported through the bounded query failure rather than an unbounded wait. This mutex does not change global registration, services, driver files, firmware or other applications.

## Synthetic validation

Compile from the repository root with Windows PowerShell:

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 '/out:work\rog-control\DirectAuraWorker.exe' /r:System.Web.Extensions.dll 'work\rog-control\DirectAuraWorker.cs'
& 'work\rog-control\DirectAuraWorker.exe' --self-test
```

Result on 2026-10-13:

```text
Direct Aura protocol tests passed: 33; hardware writes performed: 0; HID devices opened: 0
```

The injected `IAuraIoApi` checks immediate and pending completion, report copies, completion-length validation, failures before an operation starts, aborted and delayed cancellation, completion races, incomplete operations preserving native memory, rejection after poisoning, depleted budgets and the cross-worker ownership key convention. No synthetic check opens a HID interface or sends a device report. `ResetForSyntheticTests` and the fake API are only used by `--self-test`; no JSON operation exposes them.

Root's supervised single read-only regression passed first scan, release, rescan, followed by fan and LCD queries. It did not hit the former 15 s scan hang. No actual lighting effect was applied during this change.
