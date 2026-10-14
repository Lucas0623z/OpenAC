param([switch]$RunChecks,[switch]$HardwareReadOnlyChecks)
$ErrorActionPreference = 'Stop'
$compiler64 = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$compiler32 = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
& (Join-Path $PSScriptRoot 'build.ps1')
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'DirectAuraWorker.exe')) (Join-Path $PSScriptRoot 'DirectAuraWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Direct Aura adapter build failed.' }
& $compiler32 /nologo /target:exe /platform:x86 /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll ('/out:' + (Join-Path $PSScriptRoot 'SDKWorker.exe')) (Join-Path $PSScriptRoot 'SDKWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installed SDK adapter build failed.' }
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'GearLinkWorker.exe')) (Join-Path $PSScriptRoot 'GearLinkWorker.cs') (Join-Path $PSScriptRoot 'GearLinkFeatures.cs') (Join-Path $PSScriptRoot 'FirmwareCatalog.cs')
if ($LASTEXITCODE -ne 0) { throw 'Direct peripheral adapter build failed.' }
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll /r:System.Management.dll /r:System.Numerics.dll ('/out:' + (Join-Path $PSScriptRoot 'StorageWorker.exe')) (Join-Path $PSScriptRoot 'StorageWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Storage adapter build failed.' }
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'AudioEndpointWorker.exe')) (Join-Path $PSScriptRoot 'AudioEndpointWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Windows audio adapter build failed.' }
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'DiscoveryWorker.exe')) (Join-Path $PSScriptRoot 'DiscoveryWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Read-only device discovery build failed.' }
& $compiler64 /nologo /target:exe /platform:x64 /optimize+ /r:System.Web.Extensions.dll /r:System.Management.dll ('/out:' + (Join-Path $PSScriptRoot 'NotebookWorker.exe')) (Join-Path $PSScriptRoot 'NotebookWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Notebook status adapter build failed.' }
& $compiler32 /nologo /target:exe /platform:x86 /optimize+ /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'TelemetryWorker.exe')) (Join-Path $PSScriptRoot 'TelemetryWorker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installed AIO telemetry adapter build failed.' }
if ($RunChecks -or $HardwareReadOnlyChecks) {
 & $compiler64 /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $PSScriptRoot 'MockWorker.exe')) (Join-Path $PSScriptRoot 'MockWorker.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Mock worker build failed.' }
 foreach ($testName in @('SupervisorTests','ReadOnlyIntegrationTests')) {
  & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) ('/out:' + (Join-Path $PSScriptRoot ($testName + '.exe'))) (Join-Path $PSScriptRoot ($testName + '.cs'))
  if ($LASTEXITCODE -ne 0) { throw ('Test build failed: ' + $testName) }
 }
 & (Join-Path $PSScriptRoot 'SupervisorTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Worker supervisor checks failed.' }
 & (Join-Path $PSScriptRoot 'DirectAuraWorker.exe') --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Direct Aura protocol checks failed.' }
 & (Join-Path $PSScriptRoot 'GearLinkWorker.exe') --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Direct peripheral protocol checks failed.' }
 & (Join-Path $PSScriptRoot 'StorageWorker.exe') --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Storage protocol checks failed.' }
 & (Join-Path $PSScriptRoot 'AudioEndpointWorker.exe') --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Windows audio protocol checks failed.' }
 & (Join-Path $PSScriptRoot 'DiscoveryWorker.exe') --self-test
 if ($LASTEXITCODE -ne 0) { throw 'Read-only discovery checks failed.' }
 & $compiler32 /nologo /target:exe /platform:x86 /main:SDKWorkerTests /r:System.Web.Extensions.dll /r:System.Drawing.dll ('/out:' + (Join-Path $PSScriptRoot 'SDKWorkerTests.exe')) (Join-Path $PSScriptRoot 'SDKWorker.cs') (Join-Path $PSScriptRoot 'SDKWorkerTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Installed SDK checks build failed.' }
 & (Join-Path $PSScriptRoot 'SDKWorkerTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Installed SDK setter checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'MacroTests.exe')) (Join-Path $PSScriptRoot 'MacroEngine.cs') (Join-Path $PSScriptRoot 'MacroTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Macro checks build failed.' }
 & (Join-Path $PSScriptRoot 'MacroTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Macro checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) ('/out:' + (Join-Path $PSScriptRoot 'GameLibraryTests.exe')) (Join-Path $PSScriptRoot 'GameLibraryTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Game library checks build failed.' }
 & (Join-Path $PSScriptRoot 'GameLibraryTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Game library checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) ('/out:' + (Join-Path $PSScriptRoot 'ScenarioTests.exe')) (Join-Path $PSScriptRoot 'ScenarioTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Scenario checks build failed.' }
 & (Join-Path $PSScriptRoot 'ScenarioTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Scenario checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'NativeDraftTests.exe')) (Join-Path $PSScriptRoot 'NativeDraftTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Native draft checks build failed.' }
 & (Join-Path $PSScriptRoot 'NativeDraftTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Native draft checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'ControlPolicyTests.exe')) (Join-Path $PSScriptRoot 'ControlPolicyTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Session control policy checks build failed.' }
 & (Join-Path $PSScriptRoot 'ControlPolicyTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Session control policy checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'DiscoveryGuiTests.exe')) (Join-Path $PSScriptRoot 'DiscoveryGuiTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Discovery GUI checks build failed.' }
 & (Join-Path $PSScriptRoot 'DiscoveryGuiTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Discovery GUI checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) ('/out:' + (Join-Path $PSScriptRoot 'LocalUpdateCatalogTests.exe')) (Join-Path $PSScriptRoot 'LocalUpdateCatalogTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Local update catalog checks build failed.' }
 & (Join-Path $PSScriptRoot 'LocalUpdateCatalogTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Local update catalog checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) ('/out:' + (Join-Path $PSScriptRoot 'GitHubUpdateCatalogTests.exe')) (Join-Path $PSScriptRoot 'GitHubUpdateCatalogTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'GitHub update checks build failed.' }
 & (Join-Path $PSScriptRoot 'GitHubUpdateCatalogTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'GitHub update checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /main:NotebookWorkerTests /r:System.Web.Extensions.dll /r:System.Management.dll ('/out:' + (Join-Path $PSScriptRoot 'NotebookWorkerTests.exe')) (Join-Path $PSScriptRoot 'NotebookWorker.cs') (Join-Path $PSScriptRoot 'NotebookWorkerTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Notebook status checks build failed.' }
 & (Join-Path $PSScriptRoot 'NotebookWorkerTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Notebook status checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll /r:System.Management.dll ('/out:' + (Join-Path $PSScriptRoot 'AsusDriverCatalogTests.exe')) (Join-Path $PSScriptRoot 'AsusDriverCatalog.cs') (Join-Path $PSScriptRoot 'AsusDriverCatalogTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'ASUS public catalog checks build failed.' }
 & (Join-Path $PSScriptRoot 'AsusDriverCatalogTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'ASUS public catalog checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll /r:System.Management.dll ('/out:' + (Join-Path $PSScriptRoot 'WindowsTelemetryTests.exe')) (Join-Path $PSScriptRoot 'WindowsTelemetry.cs') (Join-Path $PSScriptRoot 'WindowsTelemetryTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows telemetry checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsTelemetryTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows telemetry checks failed.' }
 & $compiler32 /nologo /target:exe /platform:x86 /main:TelemetryWorkerTests /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'TelemetryWorkerTests.exe')) (Join-Path $PSScriptRoot 'TelemetryWorker.cs') (Join-Path $PSScriptRoot 'TelemetryWorkerTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Installed telemetry checks build failed.' }
 & (Join-Path $PSScriptRoot 'TelemetryWorkerTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Installed telemetry checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'MonitoringTests.exe')) (Join-Path $PSScriptRoot 'MonitoringTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Monitoring integration checks build failed.' }
 & (Join-Path $PSScriptRoot 'MonitoringTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Monitoring integration checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $PSScriptRoot 'AuraSyncCoordinatorTests.exe')) (Join-Path $PSScriptRoot 'AuraSyncCoordinator.cs') (Join-Path $PSScriptRoot 'AuraSyncCoordinatorTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Aura synchronization checks build failed.' }
 & (Join-Path $PSScriptRoot 'AuraSyncCoordinatorTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Aura synchronization checks failed.' }
 & $compiler32 /nologo /target:exe /platform:x86 /main:FanModeWorkerTests /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'FanModeWorkerTests.exe')) (Join-Path $PSScriptRoot 'FanModeWorker.cs') (Join-Path $PSScriptRoot 'FanModeWorkerTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Desktop fan protocol checks build failed.' }
 & (Join-Path $PSScriptRoot 'FanModeWorkerTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Desktop fan protocol checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'WindowsPowerTests.exe')) (Join-Path $PSScriptRoot 'WindowsPower.cs') (Join-Path $PSScriptRoot 'WindowsPowerTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows power checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsPowerTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows power checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'WindowsToolsTests.exe')) (Join-Path $PSScriptRoot 'WindowsToolsTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows tools UI checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsToolsTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows tools UI checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/out:' + (Join-Path $PSScriptRoot 'WindowsDisplayTests.exe')) (Join-Path $PSScriptRoot 'WindowsDisplay.cs') (Join-Path $PSScriptRoot 'WindowsDisplayTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows display checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsDisplayTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows display checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll /main:PeripheralLightingTests ('/out:' + (Join-Path $PSScriptRoot 'PeripheralLightingTests.exe')) (Join-Path $PSScriptRoot 'GearLinkWorker.cs') (Join-Path $PSScriptRoot 'GearLinkFeatures.cs') (Join-Path $PSScriptRoot 'FirmwareCatalog.cs') (Join-Path $PSScriptRoot 'PeripheralLightingTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Peripheral lighting checks build failed.' }
 & (Join-Path $PSScriptRoot 'PeripheralLightingTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Peripheral lighting checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'WindowsDisplayGuiTests.exe')) (Join-Path $PSScriptRoot 'WindowsDisplayGuiTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows display UI checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsDisplayGuiTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows display UI checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll /r:System.Management.dll ('/out:' + (Join-Path $PSScriptRoot 'WindowsBatteryTests.exe')) (Join-Path $PSScriptRoot 'WindowsBattery.cs') (Join-Path $PSScriptRoot 'WindowsBatteryTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows battery checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsBatteryTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows battery checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'WindowsBatteryGuiTests.exe')) (Join-Path $PSScriptRoot 'WindowsBatteryGuiTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Windows battery UI checks build failed.' }
 & (Join-Path $PSScriptRoot 'WindowsBatteryGuiTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Windows battery UI checks failed.' }
 & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot 'NotebookGuiTests.exe')) (Join-Path $PSScriptRoot 'NotebookGuiTests.cs')
 if ($LASTEXITCODE -ne 0) { throw 'Notebook GUI checks build failed.' }
 & (Join-Path $PSScriptRoot 'NotebookGuiTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Notebook GUI checks failed.' }
 foreach ($taskGuiTest in @('MousePerformanceGuiTests','OriginalPixelLayoutTests','GearPowerGuiTests','KeyboardViewsGuiTests','KeyboardLightingGuiTests','HeadsetAudioGuiTests','MotherboardGuiTests','LcdSettingsGuiTests','LcdMonitorGuiTests','GameLibraryGuiTests','ReceiverGuiTests','ScenarioGuiTests','GameFirstGuiTests','MacroGuiTests')) {
  & $compiler64 /nologo /target:exe /platform:x64 /r:System.Web.Extensions.dll ('/r:' + (Join-Path $PSScriptRoot 'OpenAC.exe')) /r:System.Xaml.dll ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationFramework.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\PresentationCore.dll') ('/r:' + (Split-Path $compiler64) + '\WPF\WindowsBase.dll') ('/out:' + (Join-Path $PSScriptRoot ($taskGuiTest + '.exe'))) (Join-Path $PSScriptRoot ($taskGuiTest + '.cs'))
  if ($LASTEXITCODE -ne 0) { throw ($taskGuiTest + ' build failed.') }
  & (Join-Path $PSScriptRoot ($taskGuiTest + '.exe'))
  if ($LASTEXITCODE -ne 0) { throw ($taskGuiTest + ' failed.') }
 }
}
if ($HardwareReadOnlyChecks) {
 & (Join-Path $PSScriptRoot 'ReadOnlyIntegrationTests.exe')
 if ($LASTEXITCODE -ne 0) { throw 'Read-only integration checks failed.' }
}
