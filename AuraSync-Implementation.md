# OpenAC cross-device Aura coordination

The owned `AuraSyncCoordinator.cs` separates device discovery, capability validation, static RGB plans, permission checks and per-device results. `AuraStructure.cs` renders real capability snapshots, including unavailable devices. Editing a color/effect or selecting a device never applies it; output requires the explicit Apply action and the session hardware-control gate.

Current common modes are static RGB and black. The requested brightness is applied by scaling the shared RGB bytes, with each participating writer using 100% hardware brightness. Independently running hardware breathe/rainbow/cycle effects are not advertised as a shared phase. No software animation scheduler exists yet.

Each selected target is freshly read and its complete plan validated before the first output request. Session permission is demanded before every command and checked again at the UI transport boundary. An unconfirmed GearLink setting stops the remaining commands for that device. A timeout is not retried. Other selected devices retain separate success, failure or unconfirmed results; the operation is not described as an atomic hardware transaction.

Native Aura command completion does not confirm visible light output: its result remains `submittedUnconfirmed`. GearLink can confirm the configuration through current-value getters; that still does not prove physical LEDs were observed. Same-value GearLink requests preserve the existing no-output path and do not count as physical-write validation.

## Integration hooks

The MainWindow partial methods are `ScanAllAuraTargetsAsync()`, `ApplyAllAuraTargetsAsync()` and `StopAllAuraTargetsAsync()`. Root calls formerly entering the motherboard-only scan/apply/release should use these methods. `AuraSyncBusy` and `AuraSyncConnectionsActive` are available for preview/closing guards. Stop requests cancellation and closes the affected workers without sending restoration commands. Devices can retain their last color. A stop must also be called on closing before ordinary worker disposal. The previous direct-worker status timer must not overwrite the cross-device connection state.

State fields `auraSyncTargets`, `auraSyncSelected`, `auraSyncResult` and `auraSyncMessage` can be preserved as data across preview reload. No running operation, hardware permission or live worker handle should be resumed. A restored selection still requires a fresh preflight before output.

## Current capability boundaries

- Native motherboard/addressable targets require valid firmware/configuration, positive LED/zone counts, a source-verified family/PID and the returned static-effect capability. Physical LED counts/generation are not invented, and no strip-topology commands are added.
- GearLink headset static participation requires confirmed online state, exact supported PID, known effect/color/brightness getters and corresponding setters. Unmapped current effect code 0 remains unavailable. The adapter does not enable a proprietary Aura participation flag and call that synchronized color output.
- Exact m709/p725 keyboard/mouse RGB parsing and static packet encoding now exist in the owned GearLink adapter. Confirmed software ownership is released only by the separate manual takeover action; scanning and applying color do not silently take ownership. The actual mouse has a readable RGB baseline but currently uses software mode. The keyboard rejects its static recipe getter, so it remains unavailable. See [peripheral protocol and physical evidence](Peripheral-Lighting-Protocol.md).
- EURUX currently has source-confirmed lighting effects, but the existing SDK lighting setter first submits fan count. Current counts cannot be read. Aura Sync therefore does not forward local/default count drafts or touch PWM/curves/port topology.
- Additional Windows-discovered devices are identification-only unless a separate verified control adapter exists. Known receivers and LCD/other hardware do not become color targets by having an ASUS name.

The UI reports supported subsets and explicit reasons for excluded devices. It must not claim all physical devices are synchronized because all available checkboxes are selected.

## Verification and provenance

The coordinator has 77 synthetic checks covering exact model capabilities, raw0 exclusion, no topology changes, shared RGB scaling, no phase claims, all-target preflight, permission revocation, per-device confirmation, no retry, cancellation, same-value no-output results, rejected-keyboard-recipe ownership attempts and explicit takeover/recovery/manual-return transactions. They perform zero worker requests and zero hardware writes. The separate peripheral lighting suite has 47 pure checks. The GUI sources compile with the new coordinator included. This stage has not performed a physical lighting or ownership write.

The coordinator and GUI integration are independently written under the project's existing MIT license. They invoke the already owned DirectAura/GearLink/SDK interfaces and incorporate no G-Helper/OpenRGB functions or proprietary ASUS source. Existing backend provenance and limitations remain applicable; conditional support in this coordinator does not expand those adapters' verified model lists.
