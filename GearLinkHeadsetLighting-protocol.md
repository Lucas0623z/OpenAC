# DELTA II-KJP minimum static lighting scope

This independently authored MIT interface is limited to the exact normal ASUS PID7489 legacy RF control collection. It includes no ASUS implementation, DLL, asset, driver or service. PID6906 and other headsets do not inherit the adapter. The commands are source-prepared; new compound writes have not been physically exercised by this implementation's pure validation.

## Exact transport and packets

The descriptor must contain output/input report204 (CC), usagePageFF00, usage1, enough room for a complete nine-byte recipe plus reportID, and no OMNI/receiver route substitution. The worker's native enumeration already verifies vendor0B05. A fresh read and every compound write stay on one original HID handle/mutex; parent live worker session and hotplug generation checks remain required.

Bytes below exclude HID reportID and are zero-padded to the actual descriptor report length. Four command/key/index bytes must match. Unsupported/rejected/truncated getters remain unknown.

| Operation | Packet | Complete data requirement |
| --- | --- | --- |
| Awake GET | `12 00 00 01` | minimum5 bytes, Boolean4 |
| Recipe GET | `12 03 00 00` | minimum9 bytes: raw effect4, brightness5, RGB6..8 |
| LED GET | `12 13 00 00` | minimum5 bytes, Boolean4 |
| Aura/SW ownership GET | `12 33 00 00` | minimum5 bytes, Boolean4 |
| Explicit LED enable | `51 10 00 00 01` | fixed5-byte request; no automatic LED disable/rollback |
| Explicit Static1 recipe | `51 28 00 00 01 <brightness> <R> <G> <B>` | fixed9-byte request; brightness0..100, RGB0..255 |

Original selected modes are Static1, Breathing2, Strobing3, ColorCycle4 and Rainbow5. **Raw mode0 is unnamed**: the original frontend merely normalizes its selected option to1. This implementation preserves the raw observation0 and never claims it was Static or Off. Complete old raw bytes0..5 may be replaced by an explicit known Static1 request. Other raw values block this minimum contract.

Ownership GET33 is the same flag original UI notifications call “SW Mode Changed.” No independent mouse/keyboard SW or WDL query is borrowed. This scope requires Aura=false and never writes its ownership flag. It does not claim a dedicated Aura RGB stream or a separately verified Windows dynamic-lighting status.

The native receipt parser recognizes both header FF AA errors and, for exact PID7489/CC only, FF AA at payload bytes4/5 following the matching four-byte request header. An embedded error must not be mistaken for an ACK. Error frames from another report/request/PID do not reject the pending transaction.

## Operations and returned state

`lightingRead` with kind=headset returns the four-query minimum scope. `lightingSet`, and ordinary `set` with setting=staticLighting, route to this compound. Other ordinary headset reads/settings retain their separate existing adapter.

The setting value is exactly `{ "color": [R,G,B], "brightness": integer }`. Channels/brightness must be genuine integers in range; floats, strings, Boolean channels, missing/extra fields or a raw-effect override are rejected before the first getter. `experimental=true` is mandatory. `requireNoOp=true` allows only a completely observed already-set result and emits no setting output for a changed value.

Metadata uses `controlScope="headsetLightingOnly"`. The state includes exact deviceId/kind/pid/transport/report metadata, online/connected/readbackConfirmed/writeConfirmed, errors and read/set capability lists. The snapshot includes:

- modelPid7489; actually read lightingEffectCode, brightnessRaw, color, lightingEnabled and auraEnabled.
- lightingModeMappingUnconfirmed=true when the observed raw effect is0.
- headsetLightingProtocolPrepared=true and headsetLightingHardwareVerified=false, separating static-source preparation from physical verification.
- headsetLightingFieldsConfirmed=true only after complete recipe and both independent Boolean getters.
- uniformStaticLightingProtocolAvailable / uniformStaticLightingReady=true only for an online, complete exact current recipe and Aura=false. LED=false is allowed because the explicitly requested compound includes its enable step.

Only staticLighting is offered in setCapabilities. No default current effect/color/brightness/owner is inserted. A missing/malformed getter prevents the capability. An asleep or disconnected headset is unavailable even if its receiver is present. A handle/read exception gives an unknown snapshot with no current recipe or setters.

The GUI should validate this exact prepared/fields/capability/ownership schema, descriptor identity, original device object/session/generation and user permission. It must not infer capability merely from kind=headset or a friendly product name.

## Compound execution and confirmation

1. Validate schema and exact collection, then freshly read all four fields before constructing any SET.
2. Freeze the entire plan, requested value and original snapshot before its first output.
3. If LED=false, explicitly enable it. Freshly read all four GETs and require LED=true, Aura=false, the original device, and exactly preserved old effect/brightness/RGB. Unexpected recipe changes stop the plan; no later color command is sent.
4. If the confirmed old recipe differs from the request, write the complete defined Static1/RGB/brightness recipe. Freshly read all four GETs and require online/original identity, LED=true, Aura=false, effect1 and the exact requested brightness/RGB.
5. If everything was already observed as requested, return alreadySet without any SET. A stored desired recipe with LED=false needs only a verified enable step.

These steps are sequential and not atomic. A rejection, partial output, exception, device/owner change, unexpected readback or insufficient time stops further settings. There is no guessed restoration, automatic ownership takeover, pairing or firmware command.

Output completion, ACK and full current-value verification are separate. An ACK alone never confirms light output. A missing ACK may be superseded by complete independent current-value readback only when no rejection was reported. `accepted` means at least one output completed; `writeConfirmed` means all required submitted steps independently matched. `appliedState` is confirmed, partial when an earlier step confirmed, unconfirmed, or alreadySet. A thrown read/output does not reuse before bytes as current.

Results include lightingStepResults(setting/outputCompleted/acknowledged/rejected/readbackConfirmed/warning), confirmedLightingSteps and remainingLightingSteps. A successful enable followed by a failed recipe is partial; LEDs may remain enabled. Results explain that hardware may retain the completed step and no rollback was issued.

Each I/O transaction remains bounded to900ms. The compound uses a16-second budget and reserves5000ms before every next SET: one output plus four complete worst-case getter transactions and500ms margin. A next command is refused if its entire verification cannot fit. Parent request supervision remains18 seconds for a resolved single device; discovery/queue time also needs its own supervision.

## Interface provenance

The original selected capability/protocol table establishes CC/FF00/usage1, exact PID/model/transport, named mode list, LED key19/16, full config key3/40 and Aura key51. The original main establishes the raw0 frontend normalization and legacy full-recipe behavior. These are interface facts; no original functions are redistributed.

- [PID7489 manifest](https://gearlink.asus.com/view/7489/manifest.json): SHA256 A1595F4C166C26B59CF60C5D02B017B31A2700AAB3A34740CD651BD50177E9BF.
- [Selected capability/protocol bundle](https://gearlink.asus.com/view/7489/bundle-CAVv3YZt.js): SHA256 5AB06550BA3283A014C7BC1BE8216367183B5996E689410B3980B9843FAA9D42; zero-based UTF-16 offsets5923 lighting list,9936 LED status,10047 config,10251 Aura status.
- [Selected main](https://gearlink.asus.com/view/7489/main-1.00.35-1785308886.js): SHA256 725200B8BDEA446BA251D2803ECDD2837981DD77F718AE2C603A3740DC915F4A; offsets41410 frontend raw0 normalization,354756 LED getter,355282 LED setter,355691 recipe getter,356943 full recipe setter,359496 Aura getter. Its original Aura-exit path is outside this scope and is never triggered here.
- [Shared transport](https://gearlink.asus.com/view/7489/bundle-EsiwQnwJ.js): SHA256 38ECD01D0BBF821E269A956DB322032CF4D447AEDBFCC4B5AC4B98F785AEAC4C; offset17546 onward report204/four-byte matching and legacy embedded FF AA response facts.

## Pure checks

GearLinkHeadsetLightingTests uses fake GET/SET/clock delegates and the managed receipt parser:609 checks pass, real HID/native/GET/SET=0. Cases cover raw0/off bootstrap, all known originals, exact packets, all preserved/desired readback fields, invalid/missing/unknown fields, strict schema, ownership changes, partial output/rejection/exception/disconnect, no-op, immutable submitted values, missing ACK versus rejection, legacy embedded errors, exact PID/report/collection, and refusal to start another step beyond its bounded budget.

Related regression checks also pass with this source: effect replacement332, newly prepared peripheral families1072, existing scoped Aura100, peripheral lighting47, and worker self-tests149. None of these checks establishes physical verification or completion of all-headset compatibility.
