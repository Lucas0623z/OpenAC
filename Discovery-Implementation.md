# Present ASUS USB/HID identification

`DiscoveryWorker.cs` is an independent x64 .NET Framework worker. It uses Windows SetupAPI and Configuration Manager read APIs, without ASUS SDKs, HALs, services, driver downloads, plugins, HID handle opens or device commands. Its JSONL contract accepts only `{ "id": <nonnegative integer>, "op": "scan" }`; other operations and extra fields are rejected before enumeration.

## Integration

- Build `DiscoveryWorker.cs` as the separate x64 executable `DiscoveryWorker.exe`, reference `System.Web.Extensions.dll`.
- Include `DiscoveryStructure.cs` in the GUI source list.
- Call `OriginalDiscoveryGallery(content)` after the original product gallery.
- In the default navigation route, handle `device:discovered:` with `OriginalDiscoveredDevicePage(value.Substring("device:discovered:".Length))` before the generic `device:` fallback.
- Call `DisposeOriginalDiscovery()` during Closing alongside the other worker disposals.
- Run `DiscoveryWorker.exe --self-test` for **34** synthetic checks. The GUI WorkerClient supervises requests at **20 seconds**; the worker's native enumeration loop additionally checks a 15-second budget and a 4096-node ceiling. Failed enumeration publishes no incomplete device list.
- `DiscoveryGuiTests.cs` compiles against the GUI plus the WPF reference assemblies and runs **26** pure GUI checks and static renders. It never shows a window, sends a worker request or clicks a device command. No further product source file is required for those tests.

## Identity boundaries

Enumeration uses `DIGCF_PRESENT | DIGCF_ALLCLASSES` and reads instance IDs, hardware IDs, names, container IDs and present parents. Only strict USB/HID `VID_0B05&PID_XXXX` identities qualify. PCI devices, a name containing ASUS/ROG, a cached pairing record, or malformed/extra-length IDs do not qualify.

An explicitly parsed instance VID is authoritative. A contradictory VID or PID in any parsed hardware-ID evidence excludes that node, rather than allowing an ASUS-looking secondary ID to override a foreign instance identity. A contradictory USB parent cannot be used as an ASUS physical root.

Each USB/HID function is traced to its nearest non-interface USB parent. That USB instance is the preferred identity: its PID and name are displayed, and multiple HID collections/interface functions are combined. This prevents receiver child functions from becoming extra physical keyboard/mouse devices. Different USB parent instances remain separate even when a container property is duplicated. When a USB parent is unavailable, a valid nonempty Windows ContainerId groups functions; transport is left unknown and conflicting PIDs remain unspecified. The local-machine container flag and shared reserved container ID are excluded from this fallback, so unrelated onboard functions cannot be merged into an invented physical device. If neither identity exists, the entry is explicitly a function node with unconfirmed physical identity rather than a claimed physical device.

If an intermediate interface node is missing, an orphan HID can join an already observed USB root only when its valid container belongs to exactly one root. A container shared by several USB roots leaves the orphan unresolved; it does not merge those roots or invent another confirmed physical device.

Names are actual bus-reported strings, then actual friendly name/device description as fallbacks; missing names stay missing. The detail page explains their sources as device-provided name, Windows display name or Windows device description. Neither a name nor a PID proves a complete commercial model/SKU or control support.

The returned stable device ID is a hash of the identity. Raw USB instance IDs/serial strings and raw ContainerId GUIDs are not included in discovery responses; container IDs remain internal to grouping. Stable hashes are identifiers, not a claim of anonymous data. Every entry has `controlSupported=false`; identification cannot enable any setter.

The original seven gallery positions remain. Suppression from the extra new-device cards requires an exact current PID/name signature, rather than the entire old adapter PID range. Generic Aura/LCD descriptors additionally require the current WMI motherboard to match this machine's X870E EXTREME. A differently named same-PID variant stays visible as identification-only. A default-collapsed **All hardware in this scan (N)** list retains every individual identity, including multiple same-model/same-PID devices; each has its own read-only detail entrance. Known-signature detail pages say that the page provides identity information without controls. Unknown signatures explicitly state that their control is unimplemented. These signature rules only avoid duplicate cards; they do not grant or extend device compatibility.

Bluetooth remote vendor identity and live connection have not been verified by this implementation. Bluetooth pairing records are excluded; no ASUS VID or model is inferred from a Bluetooth name. Discovery therefore does not promise complete Bluetooth coverage, hotplug monitoring or automatic control of future ROG models.

Connection results are timestamped snapshots, not continuous presence claims. After plugging, unplugging or changing connection, the user clicks **Scan connected devices** again. A fresh successful scan replaces the complete previous list; removed identities disappear from new-device cards and their detail page states that they are no longer in the current result. On a failed scan, the old list is cleared and the failure is shown. No startup scan or automatic device command was introduced. The fixed original product gallery remains separate from physical-presence results and may retain an offline product entrance.

## Verification

**34 worker checks** cover strict IDs, malformed inputs, absent/PCI/Bluetooth exclusion, receiver physical-parent grouping, stable IDs, distinct USB roots, unique-container orphan joining, ambiguous orphan identity, bidirectional VID/PID conflict exclusion, consistent hardware-ID fallback, local-machine/shared-container rejection, ambiguous PIDs, unknown physical identity, fresh unplug removal, name sanitization, omission of raw container/instance identity, and rejection of every non-scan request before native access. Another agent independently repeated **16** pure regression checks against these rules.

**26 GUI checks** cover exact signatures, future same-PID variants, generic motherboard descriptor boundaries, friendly name-source labels, invalid/unverified/absent metadata rejection, identification-only flags, default-folded all-hardware list, multiple equal-PID/equal-name identities, unknown entrances, absence of Apply controls in either known or unknown identity pages, and removal of stale presence claims. Static synthetic renders `discovery-gallery.png` (1538 × 1200, showing the complete extended gallery) and `discovery-identity.png` (1538 × 826) were visually inspected. The displayed sample is explicitly marked synthetic; those renders do not prove real hardware connection or control.

On this machine, a bounded read-only scan combined **62** ASUS USB/HID nodes into **9** USB-parent identities. It identified the existing Aura controller, motherboard LCD, DELTA II-KJP receiver, EURUX controller, HARPE II Extreme Edition 20 receiver and OMNI receiver, plus actual bus names `ROG RYUO IV Standard` (0B05:1C76), `Airoha Headset Gaming` (0B05:1D44), and `USB Audio` (0B05:1B7C). The last three only have metadata entrances; their control protocols are not implemented. A USB audio identity may be another function of a headset, and its actual descriptor does not prove a separate commercial product/model.

References: [SetupDiGetClassDevsW / present enumeration](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw), [ContainerId semantics](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-containerid), [CM_Get_Parent](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_get_parent), and [Microsoft Windows SDK property keys](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/shared/devpkey.h).
