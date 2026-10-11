param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [Parameter(Mandatory=$true)][string]$Stage,
    [Parameter(Mandatory=$true)][string]$Target,
    [Parameter(Mandatory=$true)][string]$ExpectedHash
)
$ErrorActionPreference = 'Stop'
$updateLog = Join-Path (Split-Path -Parent $Stage) 'update.log'
$replaced = $false
$backup = "$Target.previous"
try {
    $old = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($old -and -not $old.WaitForExit(60000)) { throw 'Ứng dụng chưa thoát sau 60 giây.' }
    if ((Get-FileHash -LiteralPath $Stage -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'SHA256 thay đổi trước khi cài.' }
    $next = "$Target.new"
    Copy-Item -LiteralPath $Stage -Destination $next -Force
    if ((Get-FileHash -LiteralPath $next -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'SHA256 sai sau khi sao chép.' }
    # Same-volume atomic replace; retain old executable for rollback.
    [System.IO.File]::Replace($next, $Target, $backup, $true)
    $replaced = $true
    $probe = Start-Process -FilePath $Target -ArgumentList '--health-check' -PassThru -WindowStyle Hidden
    if (-not $probe.WaitForExit(15000)) { $probe.Kill(); throw 'Health check timeout.' }
    if ($probe.ExitCode -ne 0) { throw 'Health check failed.' }
    Start-Process -FilePath $Target -ArgumentList '--tray' -WindowStyle Hidden
    'Update completed.' | Set-Content -LiteralPath $updateLog -Encoding UTF8
} catch {
    $_ | Out-String | Set-Content -LiteralPath $updateLog -Encoding UTF8
    if ($replaced -and (Test-Path -LiteralPath $backup)) {
        [System.IO.File]::Replace($backup, $Target, "$Target.failed", $true)
    }
    Start-Process -FilePath $Target -ArgumentList '--tray' -WindowStyle Hidden
    exit 1
}
