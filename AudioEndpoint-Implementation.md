# DELTA II-KJP Windows audio endpoint control

`AudioEndpointWorker.cs` is a separate x64 .NET Framework 4 console process. It uses the Windows MMDevice and EndpointVolume COM APIs. It does not load an ASUS DLL or send a HID audio-setting packet. Compile with `System.Web.Extensions.dll`; package `AudioEndpointWorker.exe` beside `OpenAC.exe`.

`AudioEndpointStructure.cs` provides the original headset-page fields for playback/capture volume and mute. Root integration calls `OriginalHeadsetWindowsAudio(right,false)` for playback and `OriginalHeadsetWindowsAudio(right,true)` for microphone, and calls `DisposeOriginalWindowsAudio()` on closing. The existing bounded WorkerClient has a 10-second timeout.

Opening the UI installs no audio callback and does not read or set audio. A user presses Read to enumerate active endpoints. The full normalized `PKEY_DeviceInterface_FriendlyName` must identify **ROG DELTA II-KJP** (optional USB / 2.4 GHz / Bluetooth suffix). A renamed endpoint-friendly name alone cannot qualify another adapter. Each flow has an exact opaque endpoint ID. One matching endpoint per flow is selected automatically; multiple matching endpoints require a user selection. No default endpoint API is invoked, and this adapter cannot change the default endpoint.

## JSONL protocol

- `{ "id": 1, "op": "scan" }`: returns `data.endpoints`, restricted to this exact model, with `flow` `render` or `capture`.
- `{ "id": 2, "op": "read", "flow": "render", "endpointId": "<exact ID from scan>" }`: rechecks the active state, adapter identity and direction before getting the current state.
- `{ "id": 3, "op": "set", "flow": "capture", "endpointId": "<exact ID>", "setting": "volume", "value": 45, "userInitiated": true }`: integer volume 0–100. `mute` accepts Boolean only. The UI sends this only from the corresponding Apply button.

Unknown operations/settings, relative/no endpoint IDs, wrong flows, missing explicit application flag, wrong value types and out-of-range volume are rejected. `set` obtains the exact endpoint with `GetDevice`, rechecks the identity and flow, then reads the old value. An already matching value returns `appliedState: alreadySet` with no setter call. A setter's successful HRESULT means acceptance; `writeConfirmed` requires a fresh matching getter. The volume confirmation tolerance is 0.51 percentage points, accommodating endpoint quantization for a whole-percent UI. Every response includes `source: Windows MMDevice / IAudioEndpointVolume`; `capturedAt` labels an observed snapshot.

## Validation performed

- 23 pure rule/ABI checks passed via `--self-test`, with no COM activation or audio change.
- Read-only live enumeration and JSONL scan correctly identified the installed DELTA II-KJP (2.4 GHz) headphones and microphone. Playback: **84%**, unmuted, 2 channels. Capture: **45.10%**, unmuted, 1 channel. Both reported hardware support bits 3 (volume and mute).
- Razer, Realtek and NVIDIA audio endpoints were listed only in the diagnostic inventory and excluded from control. Their volume interfaces were not activated.
- Native setter methods were implemented against the documented COM contract; **no actual volume or mute setter was tested**. Software values may differ from a physical microphone switch or another per-application audio control.
- `AudioEndpointRender.cs` renders static synthetic-state headset Settings/Microphone pages with `MainWindow(true)`; it starts no worker and invokes no setter. PNG previews: `audio-playback.png`, `audio-microphone.png`.

Official API references:

- https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmastervolumelevelscalar
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-getmastervolumelevelscalar
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nf-endpointvolume-iaudioendpointvolume-setmute
- https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/mmdeviceapi.h
- https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/endpointvolume.h

## Other review findings

Game-library loading now filters null/malformed entries and normalizes categories, sources and ID duplication. Import validates target IDs, Steam IDs, absolute `.exe` paths, field lengths and the merged 2000-entry limit; it saves the candidate before replacing the current in-memory list. Independent discovery manifests no longer abort a whole library when one file fails. Titles are capped at 120 characters. 27 pure game-library checks passed without discovery or launching a game.

StorageWorker's existing query/parser self-test passed all 4 checks without hardware access; review found no concrete new parser defect. Macro's existing 127 synthetic checks passed, including cancellation, failure and input-release paths, without hooks or native input. The integrated Closing handler cancels the first close, awaits `StopMacroFeaturesAsync`, then closes after playback's release `finally` and hook disposal. The repaired native fan-profile loader now validates null/invalid points before rendering.
