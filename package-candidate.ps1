param(
 [ValidateSet('LocalPreview','PublicCandidate')][string]$Mode = 'PublicCandidate',
 [string]$OutputRoot = '',
 [switch]$BuildAndCheck
)
$ErrorActionPreference = 'Stop'
$sourceRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$projectRoot = $sourceRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $projectRoot 'outputs' }
$resolvedOutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$projectPrefix = $projectRoot.TrimEnd('\') + '\'
if (-not $resolvedOutputRoot.StartsWith($projectPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside this project.' }
$packageLabel = if ($Mode -eq 'PublicCandidate') { 'PublicPreview' } else { $Mode }
$packageRoot = [IO.Path]::GetFullPath((Join-Path $resolvedOutputRoot ('OpenAC-0.3-' + $packageLabel)))
$archivePath = $packageRoot + '.zip'
if (Test-Path -LiteralPath $packageRoot) { throw 'Package folder already exists; choose a fresh output directory. No existing files were changed.' }
if (Test-Path -LiteralPath $archivePath) { throw 'Archive already exists; choose a fresh output directory.' }
if ($BuildAndCheck) { & (Join-Path $sourceRoot 'build-all.ps1') -RunChecks }
$allowlist = Get-Content -LiteralPath (Join-Path $sourceRoot 'PUBLIC-FILES.json') -Raw | ConvertFrom-Json
if ($allowlist.schemaVersion -ne 1 -or $allowlist.recursiveWildcardsAllowed -ne $false) { throw 'Explicit candidate allowlist required.' }
if ($allowlist.projectLicenseSelection -ne 'MIT' -or -not (Test-Path -LiteralPath (Join-Path $sourceRoot 'LICENSE') -PathType Leaf)) { throw 'Reviewed project license must be present before preparing this release.' }
function Resolve-ListedFile([string]$relative) {
 if ([IO.Path]::IsPathRooted($relative) -or $relative -split '[\\/]' -contains '..' -or $relative.IndexOfAny([char[]]'*?') -ge 0) { throw ('Invalid allowlist path: ' + $relative) }
 $full = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
 if (-not $full.StartsWith(($sourceRoot.TrimEnd('\') + '\'),[StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw ('Missing or outside source file: ' + $relative) }
 return $full
}
function Copy-ListedFile([string]$relative,[string]$destination) {
 $origin = Resolve-ListedFile $relative
 $target = [IO.Path]::GetFullPath((Join-Path $packageRoot $destination))
 if (-not $target.StartsWith(($packageRoot + '\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Destination outside package.' }
 $parent = Split-Path $target -Parent
 if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
 Copy-Item -LiteralPath $origin -Destination $target
}
# Every source is checked before creating the package; no research directory
# enumeration, wildcard copy, recursive removal or vendor binary extraction.
$sourceFiles = @($allowlist.sourceFiles) + @($allowlist.buildFiles) + @($allowlist.testSourceFiles) + @($allowlist.documentationFiles)
$binaryFiles = @($allowlist.binaryFilesAfterFreshReviewedBuild)
foreach ($file in ($sourceFiles + $binaryFiles)) { $null = Resolve-ListedFile $file }
foreach ($mapping in $allowlist.metadataFileMappings) { $null = Resolve-ListedFile $mapping.source }
if ($Mode -eq 'LocalPreview') {
 foreach ($product in @('motherboard','keyboard','mouse','headset','fan','gpu')) { $null = Resolve-ListedFile ('assets\products\' + $product + '.png') }
 $null = Resolve-ListedFile 'assets\products\manifest.json'
 $null = Resolve-ListedFile 'assets\products\SOURCES.md'
}
if (-not (Test-Path -LiteralPath $resolvedOutputRoot)) { New-Item -ItemType Directory -Path $resolvedOutputRoot | Out-Null }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
foreach ($file in ($sourceFiles | Select-Object -Unique)) { Copy-ListedFile $file ('source\' + $file) }
foreach ($file in $binaryFiles) { Copy-ListedFile $file $file }
Copy-ListedFile 'README.md' 'README.md'
foreach ($mapping in $allowlist.metadataFileMappings) {
 Copy-ListedFile $mapping.source $mapping.destination
 Copy-ListedFile $mapping.source ('source\' + $mapping.source)
}
if ($Mode -eq 'LocalPreview') {
 foreach ($product in @('motherboard','keyboard','mouse','headset','fan','gpu')) { Copy-ListedFile ('assets\products\' + $product + '.png') ('assets\products\' + $product + '.png') }
 Copy-ListedFile 'assets\products\manifest.json' 'assets\products\local-preview-manifest.json'
 Copy-ListedFile 'assets\products\SOURCES.md' 'assets\products\local-preview-sources.md'
}
$entries = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
 $relative = $_.FullName.Substring($packageRoot.Length + 1).Replace('\','/')
 [ordered]@{path=$relative;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
if ($Mode -eq 'PublicCandidate' -and ($entries | Where-Object { $_.path -match '\.(png|jpe?g|gif|bmp|svg|dll|sys|node|asar)$' })) { throw 'Unexpected proprietary/media file in candidate.' }
$metadata = [ordered]@{schemaVersion=1;version='0.3.0-preview';mode=$Mode;status='preview';uploaded=$false;projectLicense='MIT';hardwareWriteValidation='partial';guiDeviceWritesDefault='disabled_session_opt_in';auraWritesDefault='disabled_separate_opt_in';signed=$false;vendorLibrariesBundled=$false;officialImagesBundled=($Mode -eq 'LocalPreview');files=$entries}
$metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $packageRoot 'PACKAGE-MANIFEST.json') -Encoding UTF8
$hashes = $entries | ForEach-Object { $_.sha256 + '  ' + $_.path }
$hashes | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding UTF8
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archivePath -CompressionLevel Optimal
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
($archiveHash + '  ' + [IO.Path]::GetFileName($archivePath)) | Set-Content -LiteralPath ($archivePath + '.sha256') -Encoding UTF8
Write-Output ('Prepared local review package: ' + $packageRoot)
Write-Output ('Archive: ' + $archivePath)
Write-Output ('SHA256: ' + $archiveHash)
