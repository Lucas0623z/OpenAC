param([switch]$VerifyOnly,[string]$HostName='OpenAC.PreviewHost.exe')
$ErrorActionPreference = 'Stop'
$previewFolder = Join-Path $PSScriptRoot '.preview'
New-Item -ItemType Directory -Path $previewFolder -Force | Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath (Join-Path $previewFolder 'payload.exe')
$compilerRoot = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if ($HostName -notmatch '^OpenAC\.PreviewHost(?:\.[A-Za-z0-9]+)?\.exe$') { throw 'Invalid preview host name.' }
$previewHost = Join-Path $PSScriptRoot $HostName
$compileArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:' + $previewHost),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),'/r:System.Web.Extensions.dll','/r:System.Xaml.dll',('/r:' + $compilerRoot + '\WPF\PresentationFramework.dll'),('/r:' + $compilerRoot + '\WPF\PresentationCore.dll'),('/r:' + $compilerRoot + '\WPF\WindowsBase.dll'),(Join-Path $PSScriptRoot 'LivePreviewHost.cs'))
& (Join-Path $compilerRoot 'csc.exe') @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'Live preview host compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'OpenAC.exe.config') -Destination ($previewHost + '.config') -Force
if ($VerifyOnly) {
 $process = Start-Process -FilePath $previewHost -ArgumentList '--verify-state' -WindowStyle Hidden -PassThru -Wait
 if ($process.ExitCode -ne 0) { throw 'Live preview state verification failed; inspect .preview/verify-state-error.txt.' }
 Get-Content -LiteralPath (Join-Path $previewFolder 'verify-state.json')
} else {
 $process = Start-Process -FilePath $previewHost -WorkingDirectory $PSScriptRoot -PassThru
 Write-Output ('Live preview started. PID ' + $process.Id)
}
