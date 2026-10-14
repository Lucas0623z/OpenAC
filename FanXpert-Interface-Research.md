# Desktop Fan Xpert interface status

The private, normally elevated COM read now **successfully enumerates8 motherboard fan headers** on the verified local board/service. OpenAC's shipped FanModeWorker and GUI transport remain **disconnected**: the private probe is not a production worker, and denied MainSDK access is unchanged. The successful read contains names, raw duty bytes and raw per-header profile selectors, with no RPM or confirmed global preset/applied-BIOS state. EURUX controller ports and notebook fans are separate devices and never substitute for motherboard headers.

This is an interface-fact report, not a vendor-supported third-party API guarantee. OpenAC remains MIT. No ASUS implementation/binary/type-library resource or Linux/G-Helper GPL function is included in this document or worker.

## Official product function versus public callable API

ASUS describes Fan Xpert in Armoury Crate for Intel600/AMD AM5 and newer boards, with individual fan curves, fixed RPM, temperature-source selection and four global presets. That product documentation contains no independent Windows fan getter ABI or third-party caller-access contract. [ASUS Fan Xpert FAQ, updated2025-08-01](https://www.asus.com/support/faq/1050426/)

No ASUS-published desktop Fan Xpert/ASUSManagement/SCI fan SDK reference was located in the official pages searched during this stage. This search result is not proof that no partner SDK exists. ASUS describes SCI-dependent MyASUS functions on several product categories; it does not publish a common fan IOCTL contract there. SCI cannot be inferred solely from a notebook product family. [ASUS MyASUS/SCI FAQ, updated2026-06-26](https://www.asus.com/support/faq/1045676/)

## Installed COM getter metadata

The installed, already-running `AsusFanControlService` version `2.0.05.06` has a registered 32-bit local COM server. Its reviewed executable had a valid ASUSTeK Authenticode signature and SHA256 `c2959edb65eb1104f1522fad15b55f4927415edd23ab552d9be74be656f7a977`. CLSID `{14083C53-B8E7-48E4-9320-811F3478C4A4}`; ProgID `AsusFanControlService.FanControlManager`. This exact local artifact is evidence, not a portable allowlist for arbitrary service versions/models.

The embedded type library exposes these ordinary property getters:

| Object | Exact facts | Status |
|---|---|---|
| Manager | Controls collection; FanCount and service/capability flags | Controls read; direct FanCount/flags untested |
| Controls collection | Count; _NewEnum; Item(Int32) | Count8 and enumeration read; Item index semantics unknown/untested |
| One control | Id, Name, DisplayName, DutyCycle(byte), Profiles, CurrentFanCurve, RPM-mode flag | First-stage identity/raw-duty/profile list read; CurrentFanCurve/RPM-mode getter untested |
| Profiles collection | Count, _NewEnum, Item(Int32), Current(UInt32) | Count5/list/raw Current read per header; selector meaning/applied state unverified |
| Profile | Id, Name, Type, FanCurve | Id/Name read; Type/FanCurve untested |
| Curve/point | Count/_NewEnum and Temperature(Int32), Speed(Int32) | Units/ranges not established for an independent setter |

These interfaces also contain setters and calibration-adjacent methods; discovery does not authorize calling them. The independent private probe accepts a fixed getter whitelist only, caps header/profile traversal, preserves duty as raw byte and profile Current as raw UInt32, and stops at the first error. There is no explicit per-header RPM property in this typelib. `GetAic2Rpm` names a different feature and cannot be displayed as CPU Fan RPM. No active **global** preset getter has been established.

The ordinary-permission attempt returned `UnauthorizedAccessException/0x80070005`, `activated:false`, `getterCalls:0`, exit1, with the existing service PID unchanged. Its result lacks phase detail: by itself it identifies failure before a usable COM object, not which of metadata preflight/activation rejected access. It was not retried. An independently authorized normal elevated read may test access, but is not an automatic fallback or a connected implementation.

The user then separately authorized one normal administrator/UAC read. The same frozen probe returned `activated:true`, `getterCalls:3`, `InvalidCastException/0x80004002`, exit1, no timeout; the service remained Running/PID6484. The trace reached Controls, Count and _NewEnum; no header attribute or profile was read. The first probe did not record Count or the returned managed enumerator type, so no actual header count can be reported. Three counts are getter attempts, not three completed hardware readings.

Microsoft documents that the CLR can adapt a COM enumerator into managed `IEnumerator`. The original probe's direct cast to a custom IEnumVARIANT declaration did not account for that case. Separate private V2 accepts managed IEnumerator first, otherwise the Framework's documented `System.Runtime.InteropServices.ComTypes.IEnumVARIANT` (IID `00020404-0000-0000-C000-000000000046`), with detailed phase/count/type diagnostics and the same bounds. The subsequent successful V2 read used the managed IEnumerator adapter, verifying that branch on this artifact. [Microsoft enumerator marshaling](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.custommarshalers.enumeratortoenumvariantmarshaler?view=netframework-4.8.1), [standard interface](https://learn.microsoft.com/en-us/windows/win32/api/oaidl/nn-oaidl-ienumvariant)

## Successful private read, not a connected app feature

The parent performed one normal administrator V2 read under the user's read-only authorization. At `2026-10-13T23:44:30.8217819Z`, result was success/exit0,147 getter attempts, no timeout or explicit setter. The existing service stayed Running/PID6484 before/after. Read data:

| Native header name | Raw DutyCycle(byte) | Raw Profiles.Current |
|---|---:|---:|
| CPU Fan | 122 | 4 |
| Chassis Fan1 | 191 | 4 |
| Chassis Fan2 | 191 | 4 |
| Radiator Fan1 | 191 | 4 |
| Radiator Fan2 | 191 | 4 |
| Water Pump+1 | 218 | 4 |
| Water Pump+2 | 218 | 4 |
| VRM HS Fan | 11 | 4 |

Each header exposed five profiles: native IDs0/1/2/3/4 with names Disable/Standard/Silent/Turbo/User. A Current value4 coexists with profile ID4/User, but the list alone does not prove whether Current denotes an index, ID, saved selector or actually applied choice. It is not a global motherboard preset or proof of current BIOS/Q-Fan settings. Duty is a reported raw byte; no percentage or RPM conversion has been established. Names of headers absent from this enumeration, such as CPU OPT, remain unknown rather than synthesized.

This snapshot is timestamped evidence from a private diagnostic helper. FanModeWorker remains disconnected, no recurring elevated worker or GUI read pipeline is implemented, and MainSDK type50 was not newly opened. No firmware/calibration/fan-control command was issued. COM object initialization internals were not independently measured; the zero-setter count describes the client's explicit calls.

## Curve getter metadata and units

Static inspection of the extracted, non-activated typelib additionally confirms:

| Interface/property | DISPID | Result | HelpString |
|---|---:|---|---|
| IFanControl.CurrentFanCurve | 13 | IFanCurve | property CurrentFanCurve |
| IFanProfile.FanCurve | 4 | IFanCurve | property FanCurve |
| IFanCurve.Count | 2 | Int32 | property Count |
| IFanCurve._NewEnum | -4 | IUnknown | property _NewEnum |
| IFanCurvePoint.Temperature | 1 | Int32 | property Temperature |
| IFanCurvePoint.Speed | 2 | Int32 | property Speed |

The relevant help contexts are0 and help-file fields empty. These strings contain no `%`, Celsius, degrees, RPM or PWM scale declaration. Names/Int32 fields identify readable raw points but do not prove Celsius or normalized percentage units, especially across RPM-mode and PWM-mode curves. DutyCycle and Profiles.Current help strings are also generic property names. No CurrentFanCurve/FanCurve/point getter was executed in the successful first-stage probe.

A reasonable next separately reviewed read would add only CurrentFanCurve→Count/_NewEnum→Temperature/Speed and the Boolean IsRpmMode getter. It would preserve raw signed integers, bound point traversal, and reject mutations/normalization/refresh helpers. Unit verification needs an explicit source conversion/type contract matched to the service/curve mode; plausible byte255→100% arithmetic alone cannot authorize that interpretation or a setter.

COM registration/signatures are separate from activation and call permissions. Absent per-AppID AccessPermission does not mean unrestricted access, especially when a server sets process security. Creating a COM object can execute vendor default initialization even when the client intends to read only; the singleton metadata does not prove absence of constructor effects. [Microsoft COM security](https://learn.microsoft.com/en-us/windows/win32/com/security-in-com), [CoCreateInstance](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-cocreateinstance)

## Other routes and why they are not connected

| Candidate | Evidence | Remaining limit |
|---|---|---|
| MainSDK type50 | Source-confirmed MBIF17, capability0, profile1, RPM12, point13, temperature16 and preset-set3; independent pure encoders/parser | Ordinary caller paths were denied; no retry/caller spoofing/session reuse; INIT4/calibration/stress excluded |
| ASUSManagement WMI | Installed class metadata includes GetFanPolicy, GetManualFanCurve and GetManualFanCurvePro | Prior ordinary instance read AccessDenied; desktop FanType/mode semantics and exact header mapping unproven; no method called |
| FanInfofromProtocol.dll | Exported getter names for count/name/capability/default-profile; Initialize/Release also exported | No complete independent C ABI/signatures or safe initialization requirements established; no DLL executed |
| fanInterface.dll | apiFanManager plus service-query exports; internal diagnostic getter names | Export name is not a callable class/vtable ABI; no guessed pointer/signature call |
| Installed Fan*.proto | Versioned fan/SIO/EC descriptor fields, names and default curves | Hardware-description schema, not RPC request/response or live snapshot; mode enums differ between versions |
| FanStore.xml | Saved board name and curve configuration | Not a current tachometer or confirmed applied profile; no cache-backed fake connection/polling |
| Mapping/pipe imports | Service imports CreateFileMappingW; binaries expose named synchronization/IPC identifiers | No public mapping layout, read permission or refresh semantics established; no mapping/pipe opened |
| Generic Win32_Fan | Microsoft documents DesiredSpeed as desired RPM | Requested RPM is not measured RPM; no guarantee ASUS publishes actual fan instances. [Microsoft](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-fan) |

The exact board `ROG CROSSHAIR X870E EXTREME` is present in upstream Linux **v7.1** nct6775-platform's ASUS motherboard interface list. That code uses ACPI `AsusMbSwInterface`/`WMBD` with register-read method IDs `RHWM` and `RSIO`. This establishes a board-specific kernel interface fact, not a Windows user-mode SCI fan SDK. OpenAC does not install a driver, port GPL driver bodies, evaluate raw ACPI, or perform Super-I/O/SMBus/register operations. [Pinned Linuxv7.1 source](https://github.com/torvalds/linux/blob/v7.1/drivers/hwmon/nct6775-platform.c)

Older Linux asus_wmi_sensors uses a different motherboard sensor GUID and lists older exact models; notebook asus-wmi status fan endpoints have different model/units assumptions. Neither provides evidence to treat X870E as those devices. [Linuxv6.17 motherboard sensor documentation](https://www.kernel.org/doc/html/v6.17/hwmon/asus_wmi_sensors.html), [notebook driver](https://github.com/torvalds/linux/blob/v6.17/drivers/platform/x86/asus-wmi.c)

## Concrete next integration boundary

This service's **normal, authorized administrator COM getter access** has now succeeded for the limited private read. Production integration still needs a reviewed read-only worker/elevation lifecycle and fresh exact identity/signature/capability checks. It must never initialize/configure/calibrate as a fallback. Pure checks cannot establish units, current-selector meaning or applied hardware state.

A future worker may expose `transportConnected:true` only after its own current read succeeds for this exact verified service/board; the old app must not inherit that flag from a private JSON capture. It still cannot supply CPU/chassis RPM without a separate explicit tachometer contract, or enable mode/curve writes without ranges/result/readback and a fresh capability guard. A new model needs its own confirmed identity/capability; this report does not claim all ASUS/TUF/ROG desktops are adapted.

Existing FanModeWorker remains pure/disconnected, with **84 pure checks**. The private minimal COM preparation has **36 fake collector checks**, plus **35 fake enumeration checks** for V2. No COM activation/native hardware getter occurs in these tests. Neither native probe is included in build/checks, and no GUI/build/allowlist is changed by this research stage.
