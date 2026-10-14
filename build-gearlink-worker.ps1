$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskSources = @('GearLinkWorker.cs', 'GearLinkFeatures.cs', 'GearLinkMouseSensor.cs', 'GearLinkMouseLighting.cs', 'GearLinkMouseOperations.cs', 'MouseButtonActionFacts.cs', 'FirmwareCatalog.cs', 'WorkerTextProtocol.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$taskOutput = Join-Path $PSScriptRoot 'GearLinkWorker.exe'
& $taskCompiler /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll "/out:$taskOutput" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'GearLink worker compilation failed.' }
& $taskOutput --self-test
if ($LASTEXITCODE -ne 0) { throw 'GearLink worker synthetic checks failed.' }
