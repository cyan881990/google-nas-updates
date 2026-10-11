param(
    [Parameter(Mandatory=$true)][int]$SetupProcessId,
    [Parameter(Mandatory=$true)][int]$InstallerProcessId,
    [Parameter(Mandatory=$true)][string]$ExpectedSetup,
    [Parameter(Mandatory=$true)][string]$ResultFile
)
$ErrorActionPreference = 'Stop'
try {
    $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$SetupProcessId"
    $child = Get-CimInstance Win32_Process -Filter "ProcessId=$InstallerProcessId"
    if (-not $parent -and -not $child) { @{ok=$true; message='Old installer already exited.'} | ConvertTo-Json | Set-Content -LiteralPath $ResultFile; exit 0 }
    if (-not $parent -or $parent.Name -ne 'GoogleNasSetup.exe' -or $parent.ExecutablePath -ne $ExpectedSetup) { throw 'Parent process does not match the identified stalled setup.' }
    if (-not $child -or $child.Name -ne 'msiexec.exe' -or $child.ParentProcessId -ne $SetupProcessId) { throw 'Child process does not match the identified stalled msiexec.' }
    if ($child.CommandLine -notmatch '"/i"' -or $child.CommandLine -notmatch '"/qn"') { throw 'MSI command line is not the malformed command; no process was stopped.' }
    # Only the malformed MSI help process and its waiting setup are stopped.
    Stop-Process -Id $InstallerProcessId -Force
    Stop-Process -Id $SetupProcessId -Force
    @{ok=$true; message='Stopped the two identified malformed installer processes.'} | ConvertTo-Json | Set-Content -LiteralPath $ResultFile
} catch {
    @{ok=$false; message=$_.Exception.Message} | ConvertTo-Json | Set-Content -LiteralPath $ResultFile
    exit 1
}
