# OpenAC peripheral protocol progress

This note records the current protocol implementation stage. It does not claim full Armoury Crate parity or successful physical validation of every setting.

## Legacy AZOTH EXTREME command framing

The official GearLink m709 implementation uses a two-byte command/key response prefix for control requests. The owned worker now applies that exact framing only to the verified keyboard PIDs 7586 (USB) and 7588 (RF). Report ID must also match, and other device families retain their own response-prefix checks.

For this legacy keyboard family, a normal key-binding query is command `0x25`, key `0x0D`, with the source code in request bytes 4–5. The returned target code is in response bytes 4–5. The modern `0x25/0xA0` binding route and its later offsets are a distinct implementation and are not used for m709. Lever custom bindings use `0x25/0x0C` with the verified custom-mode index. Unknown binding targets remain unknown.

The current RF device's OLED enable/brightness/bar/mode-state/KPS, lever-mode-state, and selected key/lever-binding queries return the receiver's explicit same-report `FF AA` error frame. They previously appeared as full query timeouts. The worker now reports rejection immediately. This improves diagnosis and bounded read time; it does not make these settings readable or enable their setters. The two-byte framing change alone did not resolve the device rejection. USB data-mode access was not physically present during this stage.

## Model-scoped keyboard polling options

The official m709 USB and RF UI metadata exposes only 1000 and 8000 Hz. The packet enum retains its original slots 0 and 3. OpenAC now emits those options as `data.settingOptions.pollingRates` and validates keyboard writes using the actual `modelPid`. Unsupported or unknown current polling values do not become confirmed settings. The intermediate 2000/4000 Hz enum slots are not inferred as valid for this model.

## Settings response handling

The official mouse controller awaits a matching response after a settings output report. The owned worker now completes output and response handling within one bounded transaction before follow-up getters. It sends the settings packet once, accepts only the correct model-specific report/prefix/index response, ignores unrelated notifications, and does not resend after timeout or disconnection.

The response protocol keeps separate facts:

- `accepted` means the output report completed.
- `settingAcknowledged` means a matching device response arrived.
- `settingRejected` means an explicit device error response arrived.
- `writeConfirmed` requires a subsequent online getter that matches the requested value and no negative acknowledgement. ACK alone is insufficient.

Same-value requests still use the existing strict validation and no-output path. This new settings ACK behavior has synthetic coverage but has not been physically tested by changing a setting. The earlier authorized non-current DPI-stage test's immediate readback was unconfirmed; a fresh independent read subsequently confirmed the original four stages, current stage, and colors restored. The new response handling is a protocol-backed correction candidate for that immediate-readback problem, not a new claim of successful physical writes.

## Evidence and checks

The GearLink worker currently passes 149 pure checks across packet construction, query boundaries, extended features, and bounded HID I/O. Added checks cover legacy binding offsets, report/prefix matching, explicit error responses, model-scoped polling choices, output exactly once, settings ACK notification filtering, timeout without resend, partial output, disconnection, and negative responses. Default self-tests open no HID handle and perform no hardware writes.

Protocol facts were checked against ASUS's own versioned GearLink frontend. The m709 USB manifest is [7586 manifest](https://gearlink.asus.com/view/7586/manifest.json), version 1.00.09, bundle `7586-1784877011-4dba94`; the RF manifest is [7588 manifest](https://gearlink.asus.com/view/7588/manifest.json), version 1.00.09, bundle `7588-1784877011-dc4f14`. The separate USB [UI bundle](https://gearlink.asus.com/view/7586/main-1.00.09-7586-1784877011-4dba94.js) confirms 1000/8000 Hz. The RF [protocol bundle](https://gearlink.asus.com/view/7588/chunk-1.00.09-7588-1784877011-dc4f14-DYj3IaUe.js) contains the legacy framing/binding implementation and the shared mouse response queue. ASUS source files remain private research inputs; OpenAC contains independently written C# and synthetic fixtures based on these protocol facts.
