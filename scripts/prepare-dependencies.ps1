$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = Split-Path -Parent $PSScriptRoot
$dependencyDir = Join-Path $projectRoot 'build\dependencies'
New-Item -ItemType Directory -Path $dependencyDir -Force | Out-Null
function Fetch-Verified([string]$name, [string]$url, [string]$expected) {
    $destination = Join-Path $dependencyDir $name
    if (-not (Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $expected) { Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $destination }
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $expected) { throw "SHA256 failed: $name" }
}
Fetch-Verified 'rclone.zip' 'https://downloads.rclone.org/v1.75.2/rclone-v1.75.2-windows-amd64.zip' '8A9DB2BAD1DC79A65B3348FD857069598E2AAB7CF4D118B77B1DF23055AFF818'
Fetch-Verified 'winfsp.msi' 'https://github.com/winfsp/winfsp/releases/download/v2.1/winfsp-2.1.25156.msi' '073A70E00F77423E34BED98B86E600DEF93393BA5822204FAC57A29324DB9F7A'
Fetch-Verified 'dotnet48.exe' 'https://go.microsoft.com/fwlink/?linkid=2088631' '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40'
Fetch-Verified 'WinFsp-source-v2.1.zip' 'https://github.com/winfsp/winfsp/archive/refs/tags/v2.1.zip' '7B51F3C64FC5596EAB315C8812F8B13E96D6830DEEF66E30DF2019DAFD3E0DD4'
foreach ($license in @(@('WinFsp-LICENSE.txt','https://raw.githubusercontent.com/winfsp/winfsp/v2.1/License.txt'),@('rclone-LICENSE.txt','https://raw.githubusercontent.com/rclone/rclone/v1.75.2/COPYING'))) {
    $destination = Join-Path $dependencyDir $license[0]
    if (-not (Test-Path -LiteralPath $destination)) { Invoke-WebRequest -UseBasicParsing -Uri $license[1] -OutFile $destination }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$rcloneExe = Join-Path $dependencyDir 'rclone.exe'
if (Test-Path -LiteralPath $rcloneExe) { Remove-Item -LiteralPath $rcloneExe }
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $dependencyDir 'rclone.zip'))
try { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('rclone-v1.75.2-windows-amd64/rclone.exe'), $rcloneExe) } finally { $zip.Dispose() }
Write-Output 'Offline dependencies verified: rclone 1.75.2, WinFsp 2.1, .NET Framework 4.8.'

