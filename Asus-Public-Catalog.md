# OpenAC public ASUS BIOS and driver catalog

`AsusDriverCatalog.cs` is an independently written, read-only catalog client. It reads public ASUS model, operating-system, BIOS/firmware, and driver/tool metadata. It does not use Armoury Crate credentials, download packages, install drivers, or flash firmware. This catalog is separate from OpenAC's own GitHub release checker.

## API for the application

All public types are in `RogControl`.

```csharp
AsusCatalogIdentity AsusDriverCatalog.IdentifyLocal();
AsusCatalogResult AsusDriverCatalog.ReadLocal(int osId = 52);
Task<AsusCatalogResult> AsusDriverCatalog.ReadLocalAsync(int osId = 52);
AsusCatalogResult AsusDriverCatalog.Read(AsusCatalogIdentity identity, int osId = 52);
```

Local identification reads only ASUS manufacturer/model and SMBIOS BIOS-version fields. It prefers the exact `Win32_BaseBoard.Product`; it can fall back to exact `Win32_ComputerSystem.Model`. Placeholder values are rejected. It does not read serial numbers, UUIDs, user names, endpoints, or device-instance identifiers. An explicit identity is useful for catalog tests but does not establish physical hardware support.

The server must confirm the exact requested model in `CheckModelExists`, with a positive product identifier. Each BIOS/driver response must independently echo the same model. Matching is case-insensitive only; there is no fuzzy match, suffix removal, or alternate-model fallback. OS 52 is requested explicitly and must appear in that model's returned OS list; it is not silently assumed to exist.

`AsusCatalogResult` contains `Model`, `ModelSource`, `Platform`, `InstalledBios`, `ProductId`, `ExactModelConfirmed`, `OperatingSystems`, `OsId`, `OperatingSystem`, `CheckedAtUtc`, `BiosAndFirmware`, `DriversAndTools`, `SourceUrls`, `ResponseSha256`, and `Warnings`. `HardwareSettingsChanged` and `PackageDownloaded` are always false.

Each `AsusCatalogFile` contains `Category`, `Title`, `Version`, `ReleaseDate`, plain-text `Description`, `SizeText`, validated `DownloadUrl`, `ExpectedSha256`, `ReleaseFlag`, official generic `HardwareIds`, `CatalogSignaturePresent`, `DownloadVerified`, `IsSystemBios`, `InstalledVersion`, and `Comparison`.

- `ReleaseFlag` preserves ASUS `IsRelease` exactly. Select only `"1"` when identifying a formal BIOS release. Other flags are not automatically called stable releases.
- `ExpectedSha256` is the catalog's expected package hash. `DownloadVerified` remains false because no package is downloaded. An empty `Sign` is not a verified installer signature.
- A BIOS revision is compared only when both revisions are numeric, optionally with the exact model prefix such as `GA402RK.319`. Results are `newerAvailable`, `sameVersion`, `olderThanInstalled`, or `unknown`.
- Drivers and non-system firmware have `installedVersionUnknown`. Listing a driver does not establish that its adapter is present or that it should be installed. OpenAC does not infer installed driver versions from a similarly named package.
- Missing or unaccepted package URLs disable that entry's link and add a warning. Other catalog fields remain available.

## Public protocol

Requests use certificate-verified HTTPS without redirects or credentials, on `https://rog.asus.com/support/webapi/`:

1. `product/CheckModelExists?website=global&model=<exact model>&systemCode=rog`
2. `product/GetPDOS`, with the returned `pdid`, exact `model`, `m1id=0`, empty `cpu`/`mode`, and the same website/system code.
3. `ProductV2/GetPDBIOS`, with the same identity and empty `cpu`.
4. `ProductV2/GetPDDrivers`, additionally with the verified `osid`, empty `mode` and `active`.

The two catalog requests run independently after model and OS confirmation. Network reads share a 16-second budget, individual HTTPS reads are at most six seconds, and each response is limited to 4 MiB. No failed request is silently retried. The client accepts `Status: SUCCESS` only, then parses `Result.Obj`. Catalog entries are grouped by `Name`, with per-group `Files`. The current V2 responses also include `Result.Model`.

The ASUS website currently returns relative package paths under `/pub/ASUS/`. OpenAC resolves those to `https://dlcdnets.asus.com`. Absolute package links are limited to the explicitly listed ASUS download hosts and `/pub/ASUS/`, with no custom port, credentials, query, fragment, encoded path, or parent-directory segment. Links are displayed as metadata; this client never fetches their package bodies.

## Source and license provenance

G-Helper's update pages were consulted to identify public endpoint names and response fields, at commit [`e897b278bf809add90c904ba84aa2e451579dd0c`](https://github.com/seerge/g-helper/tree/e897b278bf809add90c904ba84aa2e451579dd0c). Relevant sources are [Updates.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/Updates.cs) and [UpdatesController.cs](https://github.com/seerge/g-helper/blob/e897b278bf809add90c904ba84aa2e451579dd0c/app/UpdatesController.cs). G-Helper is GPL-3.0. No G-Helper functions, class implementations, device tables, version-resolution algorithms, or source files are copied into this client or its tests.

The current ASUS website API contracts were independently checked against its own [support page](https://rog.asus.com/motherboards/rog-crosshair/rog-crosshair-x870e-extreme/helpdesk_bios/) and frontend [API module](https://rog.asus.com/_nuxt/3839d10131436.js), followed by direct official API responses. The frontend module names `ProductV2/GetPDBIOS` and `ProductV2/GetPDDrivers`. G-Helper's older `product/GetPDBIOS` request returned `FAIL` for the current desktop board; the implementation uses the website's observed V2 route. No ASUS JavaScript, images, downloaded packages, or raw response archives are distributed with the source.

## Verification on 2026-10-13

`AsusDriverCatalogTests.cs` passes 68 synthetic checks without WMI, network, or hardware access. Coverage includes exact model matching, placeholder rejection, OS schemas, model echoes, numeric BIOS comparisons, no invented installed-driver state, expected hashes versus verification, and package URL boundaries. Compile it with `AsusDriverCatalog.cs` and references to `System.Management.dll` and `System.Web.Extensions.dll`. The default mode and `--self-test` are pure. `--read-local` and `--read-model <exact model>` are explicitly network-reading diagnostics.

The implementation itself completed these official catalog reads:

| Exact model | Verification scope | BIOS/firmware entries | Driver/tool entries | Formal BIOS entry |
| --- | --- | ---: | ---: | --- |
| ROG CROSSHAIR X870E EXTREME | Local WMI model and BIOS, plus official catalog | 21 | 40 | 2503, 2026/09/22; installed BIOS 2402 |
| GA402RK | Remote official catalog only | 6 | 31 | 319, 2023/08/11 |
| RC71L | Remote official catalog only | 26 | 39 | 342, 2025/03/07 |

All three returned the exact model and Windows 11 64-bit as OS 52. Remote notebook/handheld checks do not establish physical control or installed-version compatibility. No package was downloaded, executed, or installed. Catalog contents can change, so the interface must show the actual check timestamp and current result rather than treating this table as a permanent latest-version list.
