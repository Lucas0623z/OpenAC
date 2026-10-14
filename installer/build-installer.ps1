param(
    [Parameter(Mandatory=$true)][string]$PayloadDirectory,
    [string]$OutputDirectory,
    [string]$Version='0.3.0',
    [switch]$RunChecks
)
$ErrorActionPreference='Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Windows .NET Framework 4.x compiler is unavailable.' }
$parsedVersion = $null
if (-not [Version]::TryParse($Version,[ref]$parsedVersion) -or $parsedVersion.Build -lt 0 -or $Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?$') { throw 'Use a numeric version such as 0.3.0.' }
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$payloadRoot = (Resolve-Path -LiteralPath $PayloadDirectory).ProviderPath.TrimEnd('\')
if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) { throw 'PayloadDirectory must be a directory.' }
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot ('artifacts\OpenAC-'+$Version+'-preview') }
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
if (-not $outputRoot.StartsWith($projectRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Build output must be inside the OpenAC source project directory.' }
if (Test-Path -LiteralPath $outputRoot) { throw 'Output directory already exists. Choose a fresh directory; this script never overwrites or deletes builds.' }
function Assert-NoReparse([string]$path) {
    $cursor=[IO.Path]::GetFullPath($path)
    while ($cursor) {
        if (Test-Path -LiteralPath $cursor) { $item=Get-Item -LiteralPath $cursor -Force; if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw ('Reparse point rejected: '+$cursor) } }
        $parent=[IO.Path]::GetDirectoryName($cursor)
        if ($parent -eq $cursor) { break }
        $cursor=$parent
    }
}
Assert-NoReparse $payloadRoot
Assert-NoReparse $outputRoot
$runtime=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RuntimeFiles.json') -Raw|ConvertFrom-Json
if ($runtime.schemaVersion -ne 1 -or $runtime.files.Count -ne 19 -or @($runtime.files|Select-Object -Unique).Count -ne 19) { throw 'Unexpected installer runtime allowlist.' }
$records=@()
[long]$totalBytes=0
foreach ($relative in $runtime.files) {
    if ($relative -notmatch '^(?:[A-Za-z0-9][A-Za-z0-9._-]*|assets/products/(?:manifest.json|SOURCES.md))$' -or $relative -match '\\|:|\.\./') { throw ('Invalid runtime path: '+$relative) }
    $source=[IO.Path]::GetFullPath((Join-Path $payloadRoot $relative.Replace('/','\')))
    if (-not $source.StartsWith($payloadRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime path escaped payload directory.' }
    Assert-NoReparse $source
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw ('Required runtime file missing: '+$relative) }
    $length=(Get-Item -LiteralPath $source).Length
    if ($length -gt 32MB) { throw ('Runtime file too large: '+$relative) }
    $totalBytes+=$length
    $records+= [ordered]@{path=$relative;bytes=$length;sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()}
}
if ($totalBytes -gt 32MB) { throw 'Runtime payload exceeds 32 MiB.' }
$license=Get-Content -LiteralPath (Join-Path $payloadRoot 'LICENSE') -Raw
if ($license -notmatch 'MIT License') { throw 'The reviewed MIT LICENSE is missing.' }
foreach ($notice in @('THIRD-PARTY-NOTICES.md','DirectAuraWorker-LICENSE.txt')) { if ((Get-Item -LiteralPath (Join-Path $payloadRoot $notice)).Length -eq 0) { throw ('Required notice is empty: '+$notice) } }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$utf8=New-Object Text.UTF8Encoding($false)
$metadata=Join-Path $outputRoot 'InstallerPayload.json'
[IO.File]::WriteAllText($metadata,([ordered]@{schemaVersion=1;product='org.openac.community.preview';version=$Version;files=$records}|ConvertTo-Json -Depth 6),$utf8)
Add-Type -AssemblyName System.IO.Compression
$archive=Join-Path $outputRoot 'InstallerPayload.zip'
$stream=[IO.File]::Open($archive,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {
    $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
    try {
        foreach ($record in $records) {
            $source=Join-Path $payloadRoot $record.path.Replace('/','\')
            Assert-NoReparse $source
            if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $record.sha256 -or (Get-Item -LiteralPath $source).Length -ne $record.bytes) { throw ('Source changed while building: '+$record.path) }
            $entry=$zip.CreateEntry($record.path,[IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime=[DateTimeOffset]::Parse('2026-01-01T00:00:00+00:00')
            $input=[IO.File]::OpenRead($source)
            try { $target=$entry.Open(); try { $input.CopyTo($target) } finally { $target.Dispose() } } finally { $input.Dispose() }
        }
    } finally { $zip.Dispose() }
} finally { $stream.Dispose() }
$setup=Join-Path $outputRoot ('OpenAC-'+$Version+'-preview-Setup.exe')
$references=@('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /main:RogControlInstaller.SetupProgram ('/out:'+$setup) ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) ('/resource:'+$metadata+',InstallerPayload.json') ('/resource:'+$archive+',InstallerPayload.zip') $references (Join-Path $PSScriptRoot 'InstallerCore.cs') (Join-Path $PSScriptRoot 'Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'OpenAC installer compilation failed.' }
if ((Get-Item -LiteralPath $setup).Length+$totalBytes -gt 32MB) { throw 'Installed runtime plus uninstaller exceeds 32 MiB.' }
if ($RunChecks) {
    $test=Join-Path $outputRoot 'InstallerTests.exe'
    & $compiler /nologo /target:exe /platform:x64 /optimize+ /main:InstallerTests ('/out:'+$test) $references (Join-Path $PSScriptRoot 'InstallerCore.cs') (Join-Path $PSScriptRoot 'InstallerTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Installer pure-test compilation failed.' }
    & $test
    if ($LASTEXITCODE -ne 0) { throw 'Installer pure tests failed.' }
}
$verify=New-Object Diagnostics.Process
$verify.StartInfo=New-Object Diagnostics.ProcessStartInfo($setup,'--verify')
$verify.StartInfo.UseShellExecute=$false
$verify.StartInfo.CreateNoWindow=$true
$verify.StartInfo.RedirectStandardOutput=$true
$verify.StartInfo.RedirectStandardError=$true
$verify.StartInfo.StandardOutputEncoding=New-Object Text.UTF8Encoding($false)
$verify.StartInfo.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
try {
    $verify.Start()|Out-Null
    if (-not $verify.WaitForExit(15000)) { $verify.Kill(); throw 'Installer verification timed out.' }
    $verificationText=$verify.StandardOutput.ReadToEnd()
    $verification=$verificationText|ConvertFrom-Json
    if ($verify.ExitCode -ne 0 -or $verification.ok -ne $true -or $verification.product -ne 'org.openac.community.preview' -or $verification.version -ne $Version -or $verification.files -ne 19 -or $verification.installed -ne $false -or $verification.hardwareExecuted -ne $false) { throw ('Installer verification failed: '+$verificationText+$verify.StandardError.ReadToEnd()) }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'VERIFY.json'),$verificationText,$utf8)
} finally { $verify.Dispose() }
$hash=(Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputRoot 'SHA256SUMS.txt'),($hash+'  '+[IO.Path]::GetFileName($setup)+[Environment]::NewLine),$utf8)
[IO.File]::WriteAllText((Join-Path $outputRoot 'BUILD-INFO.json'),([ordered]@{schemaVersion=1;product='OpenAC';version=$Version;installer=[IO.Path]::GetFileName($setup);sha256=$hash;payloadBytes=$totalBytes;payloadFiles=$records;verification='embedded_payload_hashes_only_no_install';unsigned=$true;installed=$false}|ConvertTo-Json -Depth 7),$utf8)
Write-Output ('Verified unsigned installer: '+$setup)
Write-Output ('SHA256: '+$hash)

