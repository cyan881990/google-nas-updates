param(
    [Parameter(Mandatory=$true)][int]$ExpectedProcessId,
    [Parameter(Mandatory=$true)][string]$SetupExe,
    [Parameter(Mandatory=$true)][string]$ExpectedUserSid,
    [Parameter(Mandatory=$true)][string]$ResultFile
)
$ErrorActionPreference = 'Stop'
try {
    if ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value -ne $ExpectedUserSid) { throw 'Windows account mismatch.' }
    $expectedApp = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas\GoogleNas.exe'))
    $runningApp = Get-Process -Id $ExpectedProcessId -ErrorAction Stop
    if ($runningApp.ProcessName -ne 'GoogleNas' -or $runningApp.MainModule.FileName -ne $expectedApp) { throw 'Application identity mismatch.' }
    if (@(Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'rclone.exe' }).Count -ne 0) { throw 'An rclone process is active; exit CYAN NAS safely before updating.' }
    # Only stop the verified GUI when no mount/sync process exists.
    Stop-Process -Id $ExpectedProcessId -Force
    $runningApp.WaitForExit(5000) | Out-Null
    & (Join-Path $PSScriptRoot 'complete-installed-setup.ps1') -SetupExe $SetupExe
    @{ ok=$true; version='1.1.3' } | ConvertTo-Json | Set-Content -LiteralPath $ResultFile -Encoding UTF8
} catch {
    @{ ok=$false; error=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath $ResultFile -Encoding UTF8
    exit 1
}
