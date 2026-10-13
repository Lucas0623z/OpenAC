# OpenAC peripheral static lighting

This is OpenAC's independent implementation of public wire-format observations. No ASUS JavaScript, G-Helper functions or GPL data table is included. The implementation is restricted to the exact AZOTH EXTREME EDITION20 keyboard (7586/7588) and HARPE II EXTREME EDITION20 mouse (7546/7548). A related device name or PID is not sufficient to enable control.

## Exact fields

Packets below exclude the HID report-ID byte. The current OMNI pairing response determines the routed control report ID; it must not be guessed from the collection name.

| Function | Keyboard m709 | Mouse p725 |
| --- | --- | --- |
| Current mode | BasicInfo `12/00/0`, payload offset 9, only with valid profile information | `12/03/0`, payload offset 4 |
| Static recipe / current RGB | `25/0E/0`: speed4, brightness5, flags6, RGB9..11, second RGB12..14, background15..17 | `12/03/0`: brightness5, RGB6..8, direction10, random11, speed12 |
| Software lighting flag | `27/00/0`, payload4 boolean | `12/00/12`, payload4 boolean |
| Windows lighting flag | `12/16/0`, payload4 boolean | `12/00/9`, payload4 boolean |
| Software flag setter | `74/00/0`, one boolean byte | `51/30/0`, one boolean byte |
| Static setter | `51/2C/0`, speed, brightness, flags0, FF, FF, RGB, six zeros | `51/28/0`, mode0, brightness, RGB, preserved direction, random0, preserved speed |

The mouse's exact official page advertises area 0, named Logo. A generic related implementation's Scrollwheel naming is not used to infer a second zone. The mouse response's unused offset9 is not shifted into the setter: the official response places direction at10, while the setter places direction at9.

The keyboard GET reads the saved static recipe; that alone does not prove static is the active mode. Same-value/no-output confirmation requires current mode0, exact brightness/RGB, nonrandom single-color flags, and zero secondary/background colors. Mouse confirmation requires current mode0, exact brightness/RGB, and random=false. Static writing also requires software mode=false and Windows lighting=false.

## Worker interface

`snapshot.lighting` contains the parsed recipe/current state. `lightingSoftwareMode` and `windowsLightingEnabled` are nullable when a query fails. `uniformStaticLightingProtocolAvailable` indicates a complete, exact-model recipe and ownership read; `uniformStaticLightingReady` additionally requires software mode=false. Unknown fields never become defaults.

`setCapabilities` exposes `lightingSoftwareMode` only after both ownership flags have been read, with Windows lighting disabled. `staticLighting` is exposed only after the software flag is disabled and lighting state is valid. Its value has exactly `color:[r,g,b]` and `brightness:0..100`. It preserves confirmed speed and mouse direction; it sends no profile-save, reset, topology, DPI, macro or firmware command. Requests still require the existing explicit experimental flag and the GUI's session permission.

Aura's separate **接管灯效** action reads a fresh baseline, changes only the confirmed software flag to false, then reads ownership and RGB capabilities again. The exact keyboard may show **尝试接管** from confirmed SW=true/WDL=false flags even when RGB GET was rejected: it cannot join synchronization unless the post-release recipe GET succeeds. A failed transaction attempts one exact original-flag restoration after a fresh, online ownership check; every restoration write requires the same session permission. A disconnected device, revoked permission or user stop remains explicitly unconfirmed. There are no blind retries, service stops, caller impersonation or automatic ownership writes during scanning.

**交还灯效** is a separate manual action available only after a successful takeover receipt in the current process. It restores only that receipt's original SW=true flag after fresh identity/ownership checks and a session permission check, then verifies the flag again. It does not restore prior colors or enter a guessed streaming protocol. Scanning may retain the receipt; stopping/closing never automatically returns the flag. Restarting the application does not invent an ownership receipt.

## Current physical evidence

Read-only queries confirmed the current mouse is online and returns effect2, brightness100, RGB(48,0,109), direction0, random=false and speed0. Its software flag was true and Windows lighting flag false. This is an ownership baseline, not successful synchronization.

The keyboard was online and returned software=true, Windows lighting=false. Its `25/0E/0` static-recipe query was explicitly rejected with an FF AA error frame. Its static RGB setter therefore remains unavailable. The GUI can offer a manual ownership attempt using the confirmed flags; no such physical attempt was performed in this stage. The same receiver also rejects several OLED/lever configuration queries. This response is not converted to default colors or an invented timeout fix.

No physical lighting, ownership or restoration writes were performed in this implementation stage. Physical visual behavior and readback after changing ownership still require a separate controlled validation. The pure lighting suite has 47 checks and opens zero HID interfaces; the coordination suite has 77 checks and performs zero worker requests.

## Public sources

- [Official keyboard protocol bundle, version 1.00.09](https://gearlink.asus.com/view/7588/chunk-1.00.09-7588-1784877011-dc4f14-DYj3IaUe.js): keyboard basic-info, saved effect GET/SET, software and Windows lighting flags.
- [Official mouse protocol bundle, version 1.00.15](https://gearlink.asus.com/view/7548/chunk-1.00.15-7548-1787817334-ff27b8-Cf7IleD9.js): effect GET/SET and ownership flags.
- [Official exact-model mouse page bundle](https://gearlink.asus.com/view/7548/main-1.00.15-7548-1787817334-ff27b8.js): supported lighting areas consist of Logo0.

Public assets can change; the referenced version paths identify the observed bundles. Copies used for private protocol inspection are excluded from distribution. The OpenAC C# parser, validation, request encoding and transaction implementation were independently written under this project's MIT license.
