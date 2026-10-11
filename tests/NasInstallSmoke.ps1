param([Parameter(Mandatory=$true)][string]$ApplicationExe,[Parameter(Mandatory=$true)][string]$ResultFile)
$ErrorActionPreference='Stop'
try {
    [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $ApplicationExe).Path) | Out-Null
    $nas=New-Object GoogleNas.NasService((Join-Path $env:LOCALAPPDATA 'GoogleNas'))
    $profile=New-Object GoogleNas.NasProfile
    $result=$nas.Admin('Install',$profile,'').GetAwaiter().GetResult()
    $status=$nas.Status().GetAwaiter().GetResult()
    @{ok=$true;installed=$nas.Installed;state=$status.State;details=$result['details']} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultFile -Encoding UTF8
} catch {
    @{ok=$false;error=$_.Exception.ToString()} | ConvertTo-Json | Set-Content -LiteralPath $ResultFile -Encoding UTF8
    exit 1
}
