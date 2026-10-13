# Third-party notices

Reviewed on 2026-10-13. This file records third-party attribution for the public source and binary distributions. Independently written project code is released under the standard MIT license in `LICENSE`, copyright 2026 OpenAC contributors. Third-party notices remain separate and must be retained.

## RIGStats — MIT code adapted in DirectAuraWorker

Project: [dvalfrid/rigstats](https://github.com/dvalfrid/rigstats).

Author and copyright holder: Daniel Valfridsson.

The native Aura adapter adapts report framing, firmware/configuration parsing, motherboard zone ordering, effect-mode mapping, effect/color reports, brightness scaling, and the Windows HID enumeration/ABI approach from these MIT-licensed files:

- [AuraUsb.cs at 2e9c9caa7512360b858dc22696b1f0aea1d653b9](https://github.com/dvalfrid/rigstats/blob/2e9c9caa7512360b858dc22696b1f0aea1d653b9/sensor-sidecar/Control/Lighting/AuraUsb.cs)
- [HidDevice.cs at 0d2033d6b99ecc83db842a70278de650d4d91b72](https://github.com/dvalfrid/rigstats/blob/0d2033d6b99ecc83db842a70278de650d4d91b72/sensor-sidecar/Control/Lighting/HidDevice.cs)
- [Upstream LICENSE at 34981a0e0d676e2122c5e24b38af7a3260e2f4f1](https://github.com/dvalfrid/rigstats/blob/34981a0e0d676e2122c5e24b38af7a3260e2f4f1/LICENSE)

Local modifications include .NET Framework adaptation, stricter input and packet validation, isolated line-JSON supervision, bounded native overlapped I/O, cancellation/lifetime handling, cross-worker HID ownership, and synthetic tests. The project-written bounded-I/O portion was also reused in GearLinkWorker.

Retain this notice and `DirectAuraWorker-LICENSE.txt` in source and binary distributions containing the adapted implementation. The complete upstream MIT notice follows.

MIT License

Copyright (c) 2026 Daniel Valfridsson

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## OpenRGB — protocol reference, no bundled implementation

[OpenRGB](https://github.com/CalcProgrammer1/OpenRGB) is GPL-2.0-or-later. The inspected controller files carry that SPDX identifier. The project does not distribute or link the OpenRGB application or library.

The recorded reference use is limited to controller protocol facts: vendor/product IDs, command values, response/configuration offsets and the effect mask. The current comparison found no direct copy of its controller functions or full detection table in the project. DirectAuraWorker's adapted C# implementation is attributed to the MIT RIGStats source above. This statement describes the examined source and recorded implementation process; it is not a guarantee about every possible historical action.

The reproducible comparison revision is [54f82e70042932a9520d676aab995f170863e7de](https://github.com/CalcProgrammer1/OpenRGB/tree/54f82e70042932a9520d676aab995f170863e7de). File URLs and content hashes are recorded in `PROVENANCE.md`. OpenRGB source is not included in the public package.

## Windows interfaces — reference-only SDK/API facts

Windows P/Invoke and COM declarations were written in C# from Microsoft API documentation and SDK interface definitions. Windows/.NET assemblies are runtime dependencies, not copied into the package.

Reference headers include `mmdeviceapi.h`, `endpointvolume.h` and `devpkey.h` from [microsoft/win32metadata at 76c04c2021ef4a831a6f1e06d9566002d746139b](https://github.com/microsoft/win32metadata/tree/76c04c2021ef4a831a6f1e06d9566002d746139b). No original header or generated interop assembly is included. That repository's [LICENSE](https://github.com/microsoft/win32metadata/blob/76c04c2021ef4a831a6f1e06d9566002d746139b/LICENSE) expressly preserves the original SDK-header licensing; its MIT notice must not be treated as blanket permission to redistribute those headers.

## ASUS/ROG — protocol references and external installed SDKs

ASUS official web resources and locally installed Armoury modules were examined for device identity, command numbers, byte/XML layouts, enumerated values, ranges and getter/setter behavior. The distributed adapters and WPF interface were written in C#. They do not include the original ASUS JavaScript/TypeScript functions, native implementations or source maps.

`SDKWorker` optionally loads the user's existing `ArmouryAIOSDK.dll` / `OmapboxSDK.dll` installation. These proprietary SDKs, their dependencies, firmware images and drivers are not supplied or covered by this project's license. Their installation and use remain subject to their own terms.

Official photographs, logos, fonts, icons, animations, LCD media presets, screenshots, original installers and reverse-analysis extracts are excluded from the public package. Product names and source URLs identify referenced devices; this project does not grant trademark or vendor-asset rights and is not an ASUS-endorsed release.

## Installer/build tools

The current installer is independently written C# / Windows Forms, built with the locally installed Windows .NET Framework compiler. Windows Script Host's CreateShortcut interface is used through project-written reflection code. No third-party installer implementation, compiler or runtime is distributed.

An attempted NSIS download was not executed and is excluded from source and installer payloads; NSIS is not a component of this distribution. If a tool or redistributable is added later, record its exact version, redistribution terms and required notices separately.

