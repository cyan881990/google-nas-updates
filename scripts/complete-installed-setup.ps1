# Run with Windows PowerShell 5.1: the installer targets .NET Framework.
param([Parameter(Mandatory=$true)][string]$SetupExe)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $SetupExe).Path) | Out-Null
$installProgress = [Action[string,int]] { param($message,$percent) Write-Host "$percent% $message" }
[GoogleNasInstaller.SetupEngine]::Install($installProgress,$true)
if ([GoogleNasInstaller.SetupEngine]::ShortcutWarning) { throw [GoogleNasInstaller.SetupEngine]::ShortcutWarning }
$installedExe = Join-Path ([GoogleNasInstaller.SetupEngine]::Target) 'GoogleNas.exe'
$installedCheck = Start-Process -FilePath $installedExe -ArgumentList '--health-check' -PassThru -WindowStyle Hidden
if (-not $installedCheck.WaitForExit(15000) -or $installedCheck.ExitCode -ne 0) { throw 'Installed executable health check failed' }
$uninstallKey = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleNas'
$startupKey = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$shortcutMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'CYAN NAS'
foreach ($name in @('CYAN NAS.lnk','Uninstall CYAN NAS.lnk')) {
    if (-not (Test-Path -LiteralPath (Join-Path $shortcutMenu $name))) { throw "Missing shortcut: $name" }
}
if ($uninstallKey.DisplayVersion -ne [GoogleNas.Core]::Version -or -not $startupKey.GoogleNas) { throw 'Registration verification failed' }
Write-Output "Installed version: $($uninstallKey.DisplayVersion); startup registered; two Start Menu shortcuts; health check passed."
Write-Output "Restart required: $([GoogleNasInstaller.SetupEngine]::Reboot)"
