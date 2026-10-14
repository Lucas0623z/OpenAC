# Direct Aura USB worker

The x64 worker implements newline-delimited JSON on stdin/stdout. It talks only to the identified and allowlisted ASUS Aura USB HID families, without loading Armoury Crate SDK/HAL components. A process supervisor must retain its own finite deadline because Windows HID enumeration and opening a handle have synchronous metadata calls.

## Prepared controller families

The MIT RIGStats AuraUsb source at commit2e9c9caa7512360b858dc22696b1f0aea1d653b9 identifies five motherboard controller PIDs18F3/1939/19AF/1AA6/1BED and four older addressable controller PIDs1867/1872/18A3/18A5. Both require actual VID0B05, usage pageFF72 and adequate HID report lengths; an unknown device is not added merely because its name contains ASUS/ROG/TUF.

Motherboard family keeps the existing EC35 effect + EC36 mask/RGB packet flow. Addressable family uses EC3B/channel/0/mode/R/G/B, one report per reported logical header. Its header configuration count is read from the same verified table offset2; the motherboard-only onboard count at0x1B is ignored for this family. All packet preparation completes before the first hardware write. The worker conservatively refuses more than8 headers; that is an implementation boundary, not a claimed hardware LED limit.

Descriptors add `controllerFamily:motherboard|addressable`, `physicalStripLedCountReadable:false`, `generationReadable:false` and `generationWritable:false`. A logical addressable channel remains one logical unit, not a measured strip LED count. EC82/ECB0 does not establish Gen1/Gen2, connected-strip occupancy or the original motherboard ARGB count/rescan API. No generation/flash packet was added.

The four additional PIDs are protocol-prepared from the fixed MIT source and synthetic fixtures; no corresponding physical device or hardware effect has been tested on this machine. They do not imply universal notebook/handheld/GPU/DRAM support or full Aura Sync. The existing exact VID/PID/report checks and scan-local identity requirements remain in force.

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
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /platform:x64 '/out:work\rog-control\DirectAuraWorker.exe' /r:System.Web.Extensions.dll 'work\rog-control\DirectAuraWorker.cs' 'work\rog-control\WorkerTextProtocol.cs'
& 'work\rog-control\DirectAuraWorker.exe' --self-test
```

Result on 2026-10-13:

```text
Direct Aura protocol tests passed: 57; hardware writes performed: 0; HID devices opened: 0
```

The injected `IAuraIoApi` checks immediate and pending completion, report copies, completion-length validation, failures before an operation starts, aborted and delayed cancellation, completion races, incomplete operations preserving native memory, rejection after poisoning, depleted budgets and the cross-worker ownership key convention. No synthetic check opens a HID interface or sends a device report. `ResetForSyntheticTests` and the fake API are only used by `--self-test`; no JSON operation exposes them.

Root's supervised single read-only regression passed first scan, release, rescan, followed by fan and LCD queries. It did not hit the former 15 s scan hang. No actual lighting effect was applied during this change.

Additional family fixtures check all nine source PIDs, foreign-device PIDs, addressable zone ordering, mode/channel/RGB packet offsets, duplicate/invalid channels, unproven mode refusal and conservative table bounds. These tests never open HID devices or send reports.
