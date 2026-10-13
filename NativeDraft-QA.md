# Native draft submission safety

`FanLightingStructure.cs` now captures a separate `ControllerLightDraft` at the time of application. Both array fields are copied, and the worker request receives its own copied arrays. After the awaited request returns, only that submitted snapshot is saved to `controller-lighting.json`. The current editor's submission timestamp and acknowledgement are updated only when it still represents the same configuration; newer edits are never overwritten or labelled as the submitted values.

While a request is busy, fan lighting port/area/effect/count/brightness/speed/direction/colour and temperature-source controls are disabled. Their edit callbacks also ignore busy/render events. LCD background and sensor choices use the same guards. LCD request parameters already consist of scalar values and a copied sensor-ID array; there was no request-array alias to correct.

GUI compilation passed. `NativeDraftTests.cs` runs **12 synthetic checks** covering detached snapshots and request arrays, preservation of the submitted port and all values after later edits, metadata, safe null arrays, disabled controls and blocked stale events for fan lighting and LCD sensors. It constructs an unshown render-only window and never clicks Apply or invokes a device worker.

The test additionally creates `native-fan-busy.png` and `native-lcd-busy.png` at 1538 × 826 with explicit synthetic capability/sensor data. These static previews verify disabled editing states and the existing page hierarchy; they are not hardware readback evidence. No real hardware/audio write, input hook, macro playback or game launch is part of this QA.
