# Windows display refresh control

`WindowsDisplay.cs` is independently written MIT C#5/.NET Framework4.8 code using documented Microsoft User32 display interfaces. No G-Helper function, device table, algorithm or GPL assembly was copied or linked. This implementation concerns Windows refresh modes, not ASUS panel Overdrive/MiniLED/GameVisual, HDR, custom timing creation or GPU power control. It installs no driver and needs no ASUS SDK.

## GUI API

Constructing a controller is data-only. Run its synchronous native operations in a supervised background operation, never in a WPF render/event path:

```csharp
var displays = new WindowsDisplayController();
WindowsDisplaySnapshot snapshot = displays.Read();
WindowsDisplayScreen screen = snapshot.Screens[0];
// Only an explicit user action with the current session write switch enabled:
WindowsDisplayChangeResult change = displays.ApplyRefreshRate(screen, 120u, explicitSessionWrite);
// A separate explicit restore action, using the same controller:
WindowsDisplayChangeResult restore = displays.Restore(change.RestorePoint, explicitSessionWrite);
```

For GUI integration, use `new WindowsDisplayController(new WindowsDisplayNativeApi(sessionDemand))`. The optional Action defaults to null and its constructor performs no I/O. It runs immediately before **each** real CDS_TEST or dynamic native change, so a session policy disabled between the two calls blocks the second call. Callback rejection becomes `WRITE_POLICY_BLOCKED`; a rejection before dynamic change reports no actual write attempt. The native class remains the same concrete type, preserving the owned-process named-mutex protection. Read does not invoke the callback.

The GUI must obtain `screen` from this controller's current snapshot and list only its `SupportedReportedRefreshRatesHz`. A constructor/read/render/selection never changes mode or runs CDS_TEST. Apply/restore reject a false session-write flag before any native I/O. No automatic AC/battery switching, reset, timeout rollback, restore-on-close or retry exists. The GUI should keep the original mode and restore point while a change is active. A process/preview restart invalidates an in-memory restore point rather than silently replaying it.

`Read()` returns every identified active CCD target, including unsupported/unmatched targets as unavailable records, plus inactive/mirror-filtered GDI context. Each screen includes an opaque ID, logical Windows DeviceName, monitor-friendly name, adapter description, primary flag, current mode and explicit capability/error. Physical interface paths stay internal; they are not serialized in the public screen object.

## Enumeration and precision

The adapter uses Unicode EnumDisplayDevicesW with initialized840-byte DISPLAY_DEVICEW. Attached desktop adapters exclude mirror drivers. Monitor enumeration requests the documented per-monitor interface name. QueryDisplayConfig with QDC_ONLY_ACTIVE_PATHS and DisplayConfigGetDeviceInfo correlate actual targets and their GDI sources, friendly names, rotation and target rational refresh timing. Active paths with targetAvailable false remain unavailable. More than one target per GDI source is a shared mode group; independent single-screen changes are disabled. Missing topology also disables writes instead of guessing clone status.

DEVMODEW is the complete **220-byte** public structure, including the16-byte union display variant, form name, ICM/reserved/panning tail. dmSize is initialized220 and dmDriverExtra0 before every EnumDisplaySettingsExW call. Current settings use ENUM_CURRENT_SETTINGS0xFFFFFFFF. Enumerated modes start at0 and stop on false; flags0 retain monitor-compatible modes and the current orientation. No EDS_RAWMODE or rotated-mode expansion is requested. Mode/private-data size, required dmFields, resolution/depth/frequency and orientation must be confirmed. Unknown current modes do not produce a selectable rate list. EnumDisplaySettingsEx's false cannot distinguish every driver error from end-of-list, so a selected rate is still checked with CDS_TEST.

Matching candidates retain the exact current width, height,32-bit depth, orientation and display flags. Driver-supported rates are deduplicated/sorted; missing/default0/1 and invalid structures are excluded. Enumeration has bounds64 adapters/16 monitor records per adapter/4096 modes; reaching a bound disables affected operations. Duplicate physical identities and targets not associated with a confirmed GDI source cannot authorize a change.

CCD rotation is clockwise; DEVMODE DMDO is counter-clockwise. The adapter explicitly converts that difference and rejects inconsistent orientation sources. When a candidate lacks an orientation field, the documented flags0 current-orientation filtering is used; it does not expand to another orientation.

DEVMODE dmDisplayFrequency is an **integer Windows mode value**. A reported59 is not relabelled59.94. `FractionalSelectionSupported:false` states that this setter cannot choose an arbitrary rational refresh rate. Current `RefreshNumerator/RefreshDenominator/TargetRefreshRateHz` are separately reported when CCD supplies them. This is active-target timing metadata, not a physical panel measurement. For example the actual PG32UCDM returned integer240 but target240016/1000 =240.016Hz. The candidates remain integer240 and other real enumerated entries.

## Manual change and explicit recovery

Apply validates an owned snapshot, freshly enumerates identity/topology/current mode, rejects stale or ambiguous identity/mode, and verifies the requested integer is still a matching supported candidate. It copies the current full DEVMODE and changes only dmDisplayFrequency, preserving position, resolution, depth, orientation and other current fields. It calls CDS_TEST for that exact device, requires return0, rereads the identity/current mode and candidate capability, then calls one dynamic change with flags0. The device name is always the specific source returned by enumeration, never null/default/global.

No CDS_UPDATEREGISTRY, CDS_GLOBAL, CDS_NORESET, CDS_SET_PRIMARY, unsafe-mode, null-mode reset or multi-monitor commit is used. Only the explicit requested display source is sent to ChangeDisplaySettingsExW. `Local\OpenAC.Display.Change` serializes owned native changes across OpenAC processes; it is acquired without waiting and is not used for fake tests. It cannot prevent external Windows/vendor software from changing a display between checks, so post-change readback remains mandatory.

The result separates TestStatus, ApplyStatus, WriteAttempted and ReadbackConfirmed. A0 driver response with mismatched mode is not confirmed. Driver/readback exceptions after an attempted change preserve `OriginalMode` and do not automatically retry or restore. `ExactTimingMatchesRequestedInteger` separately compares the current rational timing with the selected integer; it can be false while the Windows integer mode readback is confirmed. That difference must not be presented as exact fractional control.

A restore point is issued **only after a successful apply and confirmed readback of the exact requested integer rate**, with reliable identity/geometry and a still-valid manual capability. It stores the original native mode and that confirmed post-change fingerprint, bound to this controller/physical display. An exception, failed API, unchanged readback or third party selecting another rate yields no restore point; only original-mode evidence is retained, so OpenAC cannot claim another application's changed state as its own recovery state. Restore requires a new explicit session-authorized action, the same display and recorded post-change state, still-compatible geometry and an enumerated original integer rate. It follows the same test/change/readback sequence. `OriginalTargetTimingRestored` separately compares old/new rational timing if both are known; restoring the nominal integer does not guarantee identical fractional pixel timing. Original modes are never restored globally or used to overwrite a changed resolution/rotation/topology. No stale restore point can override a later user change.

## Verification

**87 synthetic checks passed on both x64 and x86**. Fake APIs cover ABI sizes/offsets, all active targets, compatible rates, private/unknown modes, orientation conversion, stale/hotplug identity, clones, default-off and foreign-snapshot gates, CDS_TEST failures, capability change during test, actual-call failures, unconfirmed readback, external post-change rates, failed API with changed state, original-mode retention, explicit/stale restore, integer/rational distinctions and native per-call policy rejection before P/Invoke. Native display reads0/real display tests0/real display writes0 in these suites.

One separately permitted **read-only** native enumeration completed exit0, no warnings and no CDS_TEST/change calls:

| Screen | Current mode | Real matching integer candidates |
|---|---|---|
| PG27AQWP-G / DISPLAY1 | 2560×1440,32bpp,540Hz; target540/1 | 59,60,120,240,270,540 |
| PG32UCDM / primary DISPLAY2 | 3840×2160,32bpp,240Hz; target240016/1000 | 23,24,25,29,30,50,59,60,100,119,120,240 |

Private evidence: `work/validation/windows-display-readonly.json`; error output was empty. This verifies local enumeration, not a real mode change. All setter/restore tests used fake API implementations. No window was activated, moved or closed. The tests' `--read-only` argument performs real enumeration and must not be used as an automatic pure build test; the default invocation uses fake APIs only.

GUI integration additionally passed **51 fake checks** at1180/1538 widths in an unshown render-only MainWindow. They confirm integer-mode/rational-timing labels remain distinct, candidate selection matches its actual draft, unsupported current rates fall back to the first supported candidate, selection/render callbacks never transmit changes, missing modes/results clear stale current snapshots, and an independently confirmed restore point remains reachable and blocks preview replacement until explicit keep/restore. The one mutation used to create the receipt was entirely in the injected fake API. Real display GET/TEST/write0 and shown windows0 for this GUI suite.

A private layout audit covered47 existing page/tab states plus system monitoring, Windows power and display settings at both widths: **100 renders**, zero horizontal-overflow states and zero untrimmed text-clipping states. Long pages retained vertical scrolling; monitoring groups kept separate bounded viewports; product photos remained within their containers. This tests layout reachability and bounds using synthetic state/local preview assets, not exact pixel identity with every original ASUS screen. Private artifacts remain outside the public source package.

Compile the library without extra package dependencies. Tests need System.Web.Extensions.dll for the optional read-only JSON evidence path:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library /out:..\..\validation\WindowsDisplay.check.dll WindowsDisplay.cs
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /r:System.Web.Extensions.dll /out:..\..\validation\WindowsDisplayTests.exe WindowsDisplay.cs WindowsDisplayTests.cs
& ..\..\validation\WindowsDisplayTests.exe
```

## Primary sources

Official Microsoft references checked2026-10-13:

- [EnumDisplayDevicesW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw) and [DISPLAY_DEVICEW](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-display_devicew).
- [EnumDisplaySettingsExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw), [DEVMODEW](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew) and [ChangeDisplaySettingsExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw).
- [GetDisplayConfigBufferSizes](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdisplayconfigbuffersizes), [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig) and [DisplayConfigGetDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo).
- [DISPLAYCONFIG_TARGET_DEVICE_NAME](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name), [DISPLAYCONFIG_PATH_TARGET_INFO](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info) and [DISPLAYCONFIG_ROTATION](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_rotation).

No G-Helper source or vendor binary is part of these files. GUI/build/allowlist integration is handled separately by the root task.
