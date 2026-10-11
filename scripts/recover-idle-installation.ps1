param(
    [Parameter(Mandatory=$true)][int]$AppProcessId,
    [Parameter(Mandatory=$true)][int]$MountProcessId,
    [Parameter(Mandatory=$true)][string]$SetupExe
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$projectRoot=Split-Path -Parent $PSScriptRoot
$newApp=Join-Path $projectRoot 'dist\GoogleNas.exe'
[Reflection.Assembly]::LoadFile($newApp) | Out-Null
if([GoogleNas.Core]::Version -ne '1.2.3'){throw 'Recovery requires the verified exit fix.'}
$expectedApp=Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas\GoogleNas.exe'
$expectedMount=Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas\runtime\rclone.exe'
$appHome=Join-Path $env:LOCALAPPDATA 'GoogleNas'
function Assert-Identity {
    $app=Get-CimInstance Win32_Process -Filter "ProcessId=$AppProcessId"
    $mount=Get-CimInstance Win32_Process -Filter "ProcessId=$MountProcessId"
    if(-not $app -or $app.ExecutablePath -ne $expectedApp){throw 'Old application identity changed.'}
    if(-not $mount -or $mount.ExecutablePath -ne $expectedMount -or $mount.ParentProcessId -ne $AppProcessId){throw 'Mount identity changed.'}
    if($mount.CommandLine -notlike ('*'+$appHome+'\rclone.conf*')){throw 'Mount config does not match this user.'}
}
function Assert-Idle {
    Assert-Identity
    [GoogleNas.Core]::CheckCacheIdle((Join-Path $appHome 'cache'))
    $logStream=New-Object IO.FileStream((Join-Path $appHome 'rclone.log'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $logReader=New-Object IO.StreamReader($logStream)
    try {$lines=$logReader.ReadToEnd() -split '\r?\n'} finally {$logReader.Dispose()}
    $cleanLine=@($lines | Where-Object {$_ -match 'vfs cache: cleaned:'})[-1]
    if(-not $cleanLine -or $cleanLine -notmatch 'in use 0, to upload 0, uploading 0,'){throw 'Cache cleaner does not confirm an idle mount.'}
    $stamp=[DateTime]::ParseExact($cleanLine.Substring(0,19),'yyyy/MM/dd HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture)
    $age=([DateTime]::Now-$stamp).TotalSeconds
    if($age -lt -5 -or $age -gt 90){throw 'Mount idle log is stale.'}
}
$actualHash=(Get-FileHash -LiteralPath $SetupExe -Algorithm SHA256).Hash
$expectedHash=(Get-Content -LiteralPath ($SetupExe+'.sha256') -Raw).Split(' ')[0].Trim()
if($actualHash -ne $expectedHash){throw 'Setup checksum mismatch.'}
Assert-Idle
Start-Sleep -Seconds 6
Assert-Idle
# Recovery for old builds whose VFS-reference bug makes their own exit impossible.
# Only the verified idle GUI/mount pair is stopped; never delete cache or config.
Stop-Process -Id $AppProcessId -Force
[GoogleNas.Core]::CheckCacheIdle((Join-Path $appHome 'cache'))
$mount=Get-CimInstance Win32_Process -Filter "ProcessId=$MountProcessId"
if($mount -and $mount.ExecutablePath -eq $expectedMount -and $mount.ParentProcessId -eq $AppProcessId){
    Stop-Process -Id $MountProcessId -Force
    (Get-Process -Id $MountProcessId -ErrorAction SilentlyContinue) | ForEach-Object {$_.WaitForExit(10000)|Out-Null}
}
& (Join-Path $PSScriptRoot 'complete-installed-setup.ps1') -SetupExe $SetupExe
Start-Process -FilePath $expectedApp -WindowStyle Hidden
Write-Output 'Idle old build recovered; CYAN NAS 1.2.3 installed and started. Cache and account preserved.'
