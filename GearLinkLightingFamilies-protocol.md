# Exact GearLink lighting families

This independently written MIT implementation adds **only static RGB lighting and explicit software-lighting ownership** for 18 normal USB/RF PIDs from nine additional official families. It does not implement their sensor, buttons, DPI, calibration, Zone, power, OLED, macros, profiles, firmware, pairing or receiver administration. Sharing a lighting packet does not establish those other protocols. No vendor JavaScript, DLL, image or GPL implementation is included.

The existing p725 and m709 full adapters remain separate. Their four PIDs also appear in the shared pure facts registry so the GUI has one exact lighting identity contract. `LegacyKeyboard` still contains only 7586/7588. Ambiguous catalog entries 7547, 7587, 6976 and 7206 are excluded; 7587 is a bootloader identity, not a normal control device. Bluetooth or an unknown product category does not expand the allowlist.

## Shared facts and supported lighting scope

`GearLinkLightingModelFacts.cs` is compiled into both the GUI and isolated worker, with no native APIs. `Lookup`, `Supports`, `IsLightingOnly`, `AreasForPid`, `KindForPid`, `ModelForPid`, `TransportForPid` and `All` return immutable records or cloned arrays. Records expose `Pid`, `Family`, `Kind`, `Model`, `Transport`, `LightingOnly`, `Areas` and `PrimaryArea`.

| Family | Model | USB / RF PID (decimal) | Exact areas |
| --- | --- | --- | --- |
| p713 | ROG HARPE ACE AIM LAB EDITION | 6802 / 6804 | Scroll1 |
| p714 | ROG KERIS II ACE | 6934 / 6936 | Logo0 |
| p716 | ROG HARPE ACE MINI | 7011 / 7013 | Scroll1 |
| p718 | ROG HARPE ACE EXTREME | 7015 / 7017 | Scroll1 |
| p722 | ROG KERIS II ORIGIN | 7180 / 7182 | Logo0, Scroll1, Base2 |
| p723 | ROG HARPE II ACE | 7273 / 7275 | Logo0 |
| m701 | ROG AZOTH | 6787 / 6789 | Main0 static effect |
| m702 | ROG AZOTH EXTREME | 6975 / 6978 | Main0 static effect |
| m704 | ROG AZOTH X | 7204 / 7205 | Main0 static effect |
| p725 (existing adapter) | ROG HARPE II EXTREME EDITION20 | 7546 / 7548 | Logo0 |
| m709 (existing adapter) | ROG AZOTH EXTREME EDITION20 | 7586 / 7588 | Main0 static effect |

Source-confirmed normal direct control collections are usage1 / FF01 for mice and usage1 / FF00 for keyboards. Their actual report ID comes from the HID descriptor, not an enum default. A unique verified OMNI route may use another FF00..FF02 collection, but must have one actual mapped output report present in its input reports, a known receiver PID/identity, and an exact RF peripheral PID. Minimum descriptor input sizes must accommodate the complete device-info/recipe response; output sizes must accommodate the complete setting. Ambiguous reports, wrong pages, bootloaders and an unproven wired identity routed as RF are rejected.

## Packet facts

These bytes exclude the descriptor-selected HID report ID. Index is a 16-bit little-endian value; the independently generated output is zero-padded to the real descriptor report length. New families require all four command/key/index reply bytes to match. Their official lighting methods use the protocol base's default `matchBytes=4`; this implementation does not inherit m709's separately validated two-byte legacy replies.

Mouse GET allowlist:

- `12 00 02 00`: awake Boolean, complete response at least5 bytes; a receiver's presence does not prove the mouse is awake.
- `12 00 00 00`: complete device info at least19 bytes; profile count at8, active profile at10+1, USB firmware at4 or RF peripheral firmware at14. The minimal scope does not expose its DPI fields as current controls.
- `12 03 <area> 00`: complete effect recipe at least13 bytes; mode4, brightness5, RGB6..8, direction10, random11, speed12. Area must belong to that exact model.
- `12 00 0C 00`: software-lighting owner Boolean4.
- `12 00 09 00`: actual Windows-lighting owner Boolean4. This is not a support flag.

Mouse SET allowlist:

- `51 28 <area> 00 00 <brightness> <R> <G> <B> <direction> 00 <speed>`: static effect0, random0. Direction and speed are preserved from that area's complete fresh recipe. Brightness is0..100, RGB0..255.
- `51 30 00 00 <bool>`: explicit software ownership. Windows-lighting ownership is never silently disabled.

Keyboard GET allowlist:

- `12 03 00 00`: connection0 USB /1 RF /255 unavailable; Bluetooth2 or a connection not matching the exact normal PID transport is rejected.
- `12 00 00 00`: complete info at least15 bytes; profile count8, active effect9, active profile10, USB firmware4 or RF peripheral firmware11. Current effect must be0..9 and current profile within the confirmed count. Profile0 is accepted as the documented original selector value, not converted to1.
- `25 0E 00 00`: stored static recipe0, complete response at least18 bytes: speed4, brightness5, flags6, direction7, width8, firstRGB9..11, secondRGB12..14, backgroundRGB15..17. Single/random flags must be one of0/1/16/17; direction/width must be255. Reading this stored recipe alone does not prove the active effect is static.
- `27 00 00 00`: software owner Boolean4.
- `12 16 00 00`: actual Windows-lighting enabled Boolean4. `12 07` is a different supported-capability query and is not substituted for current ownership.

Keyboard SET allowlist:

- `51 2C 00 00 <speed> <brightness> 00 FF FF <R> <G> <B> 00 00 00 00 00 00`: preserved stored speed, single-color non-random static0, zero secondary/background triples. Independent device-info readback must also report the current effect0.
- `74 00 00 00 <bool>`: explicit software owner.

All new-family queries are length4 and settings have exact lengths5/12/18. The final `GearHid.Query` and `WriteSetting` entry points apply this fixed per-PID contract, so an encoder error cannot open old mouse/keyboard commands. Sensor/calibration/DPI/keys/power/profile/firmware commands are rejected before a HID transaction. The shared router may enumerate handles and query a receiver's pairing table to resolve identity; a refused device operation does not issue a peripheral setting or sensor getter.

## Read result and GUI contract

`read`, `scan` and `lightingRead` for the additional PIDs use the same minimum scope. Metadata has an exact model/transport and `controlScope="lightingOnly"`. Its state contains real `online`, `connected`, `readbackConfirmed`, `writeConfirmed`, `errors`, `capabilities`, `readCapabilities`, `setCapabilities` and `snapshot`. A failed handle/read returns offline with no invented recipe or setters.

The snapshot uses:

- `modelPid` integer, `lightingFamily` exact family, `profileCount`1..255, `activeProfile` mouse1..count or keyboard0..count, independently read firmware strings where nonzero.
- `lightingAreas`: an array of the actually read area recipes in the exact facts-table order. Missing/invalid areas are omitted; no default recipe is inserted.
- `lighting`: the actually read primary recipe for existing consumers. A Scroll-only model's primary area is1. This field alone never authorizes uniform multi-area writes.
- Each mouse recipe: `area`, `areaName`, `currentMode`, `brightness`, `color` three integers, `direction`, `random` Boolean, `speed`, `source` exact family. Accepted modes are0..6 or240.
- Each keyboard recipe adds `recipeMode=0`, `flags`, `direction=255`, `width=255`, `secondaryColor`, `backgroundColor`; `currentMode` comes from separately read device info. Accepted active effects0..9.
- `lightingSoftwareMode` and `windowsLightingEnabled` exist only after complete Boolean getters. Unsupported or malformed getters remain unknown.
- `uniformStaticLightingProtocolAvailable` requires every exact area, current profile/effect metadata, known SW ownership and WDL=false. `uniformStaticLightingReady` additionally requires SW=false. A complete recipe with SW=true permits only explicit software ownership; WDL=true permits neither setting.
- `lightingProtocolPrepared=true`, `lightingHardwareVerified=false` explicitly distinguish protocol-source preparation from live validation of a newly supported physical model.

Static value schema is exactly `{ "color": [R,G,B], "brightness": integer }`; extra fields, strings, Boolean channels, floats and non-integer/out-of-range numbers are rejected before the first getter. SW value must be a Boolean. An explicit experimental-control request is still required by the worker. GUI permissions, original object/device identity, worker session and hotplug generation remain the parent's responsibility.

`lightingSet` and ordinary `set` on these PIDs route only those two settings. A user starts a fresh scoped read, then the entire immutable per-area packet plan is validated before its first SET. The worker holds one original HID handle/mutex through submission and readback. No WDL toggling, automatic ownership takeover, rollback, firmware operation or all-area3 shortcut is issued.

## Sequential readback, partial failures and bounds

The p722 plan contains one SET per Logo/Scroll/Base area. Each step's independent full readback checks the same device identity/PID, profile/count, WDL and SW owner, every already changed area, and every still unchanged recipe. Static matches require mode0, requested RGB/brightness, random=false, preserved per-area speed/direction; keyboard matches also require single flag0, zero secondary/background and current effect0. SW changes must preserve every recipe and match the independent owner getter.

Output completion, acknowledgement and current readback are separate. A full output without a matching ACK can be confirmed only by that independent complete readback; an FF AA rejection, partial output, disconnect, malformed/mismatched reply, changed profile/owner/unwritten area or exception stops later settings. `appliedState` is `confirmed`, `partial` when preceding areas have independently confirmed, or `unconfirmed`; an exact all-area no-op is `alreadySet`. No atomicity is claimed and no unproven rollback occurs.

Results include `lightingAreaResults` entries with area/outputCompleted/acknowledged/rejected/readbackConfirmed/warning, `confirmedAreas`, `remainingAreas`, `writeAttempted`, `accepted`, `writeConfirmed`, `setting`, `requestedValue` and an acceptance-evidence explanation. `accepted` means at least one output completed, not that the entire plan succeeded. An exceptional readback produces an unknown snapshot instead of reusing before values as current.

Each native transaction remains bounded to900ms. Within the family's16-second submission budget, each next SET must have enough remaining time for its complete worst-case output plus all-area readback, with500ms reserve. If there is insufficient budget, the next command is not sent and the partial/unconfirmed result is returned. This keeps repeated full readbacks within the parent18-second request bound for a resolved device. Parent discovery/receiver resolution has its separate bounded supervision; a multi-device GUI should enumerate then read individual devices with progress/cancellation instead of accumulating all reads in one request.

## Primary interface evidence

No downloaded script was executed. This table identifies the independently inspected official protocol chunks; each representative main bundle supplied the exact selected areas, and its manifest/preload supplied family/model selection. The private research anchor file records all44 manifest/preload/main/protocol URLs, hashes and method character offsets. These public notes publish interface facts and source provenance only, not vendor code.

| Family | Official protocol source | SHA256 |
| --- | --- | --- |
| p713 | [6802 protocol](https://gearlink.asus.com/view/6802/chunk-1.00.06-6802-1780363498-ba0f72-BY--oeMX.js) | 9934ac8c0726f40108195c8a51bbcf79ac77b15ce926a6064d394cb9c3357edc |
| p714 | [6934 protocol](https://gearlink.asus.com/view/6934/chunk-1.00.04-6934-1781677222-8ec2ef-CEOYheup.js) | 73b16e87d2faa0b8321b7d27f59bcb85b2b3ca94e9fb6dab47d33071a5a7d5c2 |
| p716 | [7011 protocol](https://gearlink.asus.com/view/7011/chunk-1.00.09-7011-1780557037-387f91-BgvbCCS5.js) | d57ad511b9cdc2e7e60ffce511ecec0621b71eccc19ab702cfd78551dfaa7dcd |
| p718 | [7015 protocol](https://gearlink.asus.com/view/7015/chunk-1.00.05-7015-1775709631-724ce7-00i2AxAE.js) | 04391a32a8e9193678ac284357ffcca70e534445548976a7aa6615b4b840d489 |
| p722 | [7180 protocol](https://gearlink.asus.com/view/7180/chunk-1.00.26-7180-1779174923-78e6a2-ZjLbjiXy.js) | ef3d2d99610d38d7acb9f40a4d683196b789f973c2f324f94d5ace81724814e8 |
| p723 | [7273 protocol](https://gearlink.asus.com/view/7273/chunk-1.00.54-7273-1780540246-a76ff2-DEK-kehd.js) | a615b3f5af117d69fa3262f9a99022db5aecdc5e8f712858658f617ddde94ec1 |
| m701 | [6787 protocol](https://gearlink.asus.com/view/6787/chunk-1.00.22-6787-1784711756-8d8a9d-Cuj2MYi1.js) | 4d3a7de589948159b3458ab5eaf991bbca6e897d59eb638ca6d281d84b764c7c |
| m702 | [6975 protocol](https://gearlink.asus.com/view/6975/chunk-1.00.07-6975-1783491805-28e8bf-DfQzACUZ.js) | 84af365ddc3c2b9e61d0f572596fe619b0955de9f5a215071a8ff3170ae204e0 |
| m704 | [7204 protocol](https://gearlink.asus.com/view/7204/chunk-1.00.36-7204-1788853886-139979-BQd-SFAq.js) | a81645a57e35e97c4f68e3df3ed6646e74351a0070984a88ff28a94af6556355 |

## Pure validation

`GearLinkLightingFamilyTests.cs` compiles with the worker partial sources, shared model facts, `WorkerTextProtocol`, `MouseButtonActionFacts` and `FirmwareCatalog`; select `/main:GearLinkLightingFamilyTests`. It uses fake delegates and uninitialized managed objects solely to test rejecting final transport boundaries before a native handle exists. It never constructs a live HID transport, scans, records input or executes vendor JavaScript.

The family suite currently passes1072 pure checks. Coverage includes every normal PID, golden packets, exact areas/reports, immutable source facts and submitted packets, no inherited legacy APIs, strict integer/RGB/unknown-field schemas, malformed ownership/connection/profile/current mode, missing areas, arbitrary catalog/bootloader PIDs, exact reply matching, complete readback, sequential partial/rejected/disconnected/changed recipes, no-op protection and bounded refusal to start another area. Existing scoped Aura100, peripheral lighting47, mouse sensor173 and worker self-tests149 also pass with the added sources. These tests do not establish new-model hardware verification.
