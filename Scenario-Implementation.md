# Local scenario profiles

The new `ScenarioProfiles.cs` and `ScenarioStructure.cs` implement local software scenario profiles. They preserve the referenced structure: profile name at the top, application association at left (30%), device-profile selection and lighting inclusion at right (70%), Save / Apply and Save at the bottom. Device values are edited in a local-profile dialog, keeping the original main-page hierarchy. There is no claim that an original vendor hardware profile slot was written.

## Integration

- Add the two source files to the GUI compiler list.
- Route `情境设置档` to `OriginalScenarioPage()`.
- Call `OnScenarioNavigation(value)` at the start of `ShowPage`. Leaving this page requests cancellation of subsequent scenario commands.
- In Closing, before disposing shared GearLink / audio workers, `await StopScenarioFeaturesAsync()`. The helper cancels subsequent commands and awaits the in-flight operation; it does not call Close.
- Existing GearLinkWorker and AudioEndpointWorker are reused. No background process or hook is started by viewing, loading or saving a profile.

## Capture and edit

Read and Capture explicitly performs a fresh read through the existing device reader, then captures only settings present in the verified `setCapabilities` whitelist. Offline/unconfirmed devices cannot be captured. Missing/invalid values are skipped with a visible capture warning; no default keyboard or mouse setting is generated.

The headset supports 16 known fields when getter mappings are confirmed. This local machine's current mapping permits **14**: EQ (8 confirmed public bands) and its switch; noise reduction strength/switch; sidetone level/switch; voice prompt; latency; sleep duration; low-battery threshold/voice prompt; brightness; lighting switch; Aura switch. `brightness` reads `brightnessRaw`. Effect reads `lightingEffectCode` and fixed RGB reads `color`; the latter two are omitted when the firmware's mode mapping is unconfirmed. The “Include lighting” checkbox filters brightness/effect/RGB/lighting/Aura commands when applying.

Keyboard capture currently covers verified polling/sleep plus scalar OLED and lever settings. Mouse capture covers polling, sleep, angle snapping, Motion Sync, active DPI stage and four verified symmetric DPI values. Independent unequal X/Y DPI is skipped rather than silently flattened. Other button bindings / compound keyboard modes are outside this first local-profile schema.

When the confirmed mouse snapshot reports 8000 Hz together with Motion Sync enabled, capture omits **both** polling and Motion Sync and explicitly explains that this stored combination cannot be safely switched by the current adapter. Keeping just one would still fail the actual-state preflight. The decision checks the actual snapshot even if only one of the two appears in `setCapabilities`. Other confirmed settings, including DPI and sleep, remain capturable; the input snapshot and capability list are untouched. Legal 8000 Hz / disabled Motion Sync and lower-rate / enabled Motion Sync combinations retain their normal capture behavior. If no supported settings remain, capture fails with the reason instead of creating an empty device profile.

Editor fields validate switches, supported numeric choices, EQ's 8 integer levels, RGB bytes and DPI limits. The user can uncheck a setting to remove it from this local profile, for example removing fixed RGB when selecting a cycling effect. Editing and saving change only the local file. Windows audio includes the exact endpoint ID and render/capture direction. Its profile targets whole-percent volume; the original fractional observed volume is retained and displayed separately (for example 45.10% observed / 45% target).

## Validation and manual application

All values, enabled and disabled entries, application associations, duplicate kinds/settings/endpoint IDs, field lengths, version and cross-field constraints are checked for the whole document before the first read/set delegate. Import additionally checks required raw JSON fields and strict switch/volume types, preventing typed deserialization from turning omitted volume into 0 or rounding an invalid fractional target.

Apply and Save first saves the validated intended software profile. It then reads every exact target and verifies online state, identity, supported setting capability and relevant runtime constraints before sending the first setting. Mouse Motion Sync must be off for 8000 Hz; DPI stage must be active in the current hardware profile. Every command still uses the current worker's strict setter, which reads the device again and verifies its resulting getter.

Each command is independently reported as confirmed, already set (no repeated write), accepted but unconfirmed, failed, or cancelled/not sent. A failure does not trigger a rollback or restore earlier values. Cancellation stops subsequent requests and lets the current bounded request finish. Results remain available in the profile page's result dialog. A completed setter without matching getter is never counted as confirmed.

Application associations are metadata for this local profile. **Automatic application switching is not implemented or enabled; selecting an application never launches it.** No startup playback, global input recording or application monitor was introduced.

## Storage and verification

Local files: `%LOCALAPPDATA%\ROG Control\ScenarioProfiles\<guid>.rogscenario.json`, version 1, up to 1 MiB per file. Export/import use this application's format. Import creates a new local profile ID and does not automatically save or apply it. Atomic save uses a unique temporary file and preserves malformed existing profile files during enumeration.

Validation performed: **74 pure checks** using fake delegates. The latest capture regression compiled the updated rules and tests directly, without starting the GUI or any worker; it adds 14 checks for the unsupported mouse pair, unchanged input state/capabilities, preservation of DPI/sleep, legal combinations, single-capability cases and rejection when no settings remain. Previous GUI compilation and visually inspected static renders `scenario-empty.png` / `scenario-headset.png` cover the existing layout. At 1538 × 826, the captured 14-setting headset and two audio endpoints fit with Save / Apply and Save visible at the bottom. Additional endpoint-choice controls can scroll within the existing page when several exact matching endpoints are available. The other checks cover capability-based 14-field capture, accurate getter aliases, offline rejection, all value ranges/types, JSON required fields, last-setting rejection before any read/write, zero writes after failed preflight, partial failures and cancellation, exact audio binding, mouse dependency ordering, atomic save and malformed-file preservation. The tests and static renderer perform no worker calls, real capture, hardware/audio writes, game launch, macros, hooks or SendInput.

Native profile application has not been tested on real hardware.
