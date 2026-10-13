$ErrorActionPreference = 'Stop'
$compilerRoot = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$sources = @('AssemblyInfo.cs','RogControl.cs','ShellStructure.cs','StructurePages.cs','AuraStructure.cs','SettingsStructure.cs','FanStructure.cs','DeviceStructure.cs','Monitoring.cs','GameLibrary.cs','DeviceGearLink.cs','StorageStructure.cs','NativeSettingsStructure.cs','MacroEngine.cs','MacroFeatures.cs','LcdStructure.cs','AudioEndpointStructure.cs','ReceiverStructure.cs','FirmwareStructure.cs','GearExtendedStructure.cs','FanLightingStructure.cs','ScenarioProfiles.cs','ScenarioStructure.cs','LocalUpdateCatalog.cs','LocalUpdateStructure.cs','BoardArgbStructure.cs','DiscoveryStructure.cs','ControlPolicy.cs')
$compileArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')),('/out:' + (Join-Path $PSScriptRoot 'OpenAC.exe')),'/r:System.Management.dll','/r:System.ServiceProcess.dll','/r:System.Web.Extensions.dll','/r:System.Xaml.dll',('/r:' + $compilerRoot + '\WPF\PresentationFramework.dll'),('/r:' + $compilerRoot + '\WPF\PresentationCore.dll'),('/r:' + $compilerRoot + '\WPF\WindowsBase.dll'))
$compileArgs += $sources | ForEach-Object { Join-Path $PSScriptRoot $_ }
& (Join-Path $compilerRoot 'csc.exe') @compileArgs
if ($LASTEXITCODE -ne 0) { throw 'OpenAC compilation failed.' }
Write-Output 'OpenAC compilation succeeded.'
