$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas'
$sourceExe = Join-Path $PSScriptRoot 'GoogleNas.exe'
$checksumFile = Join-Path $PSScriptRoot 'GoogleNas.exe.sha256'
if (-not (Test-Path -LiteralPath $sourceExe)) { throw 'Giải nén toàn bộ gói cài đặt trước.' }
if (-not (Test-Path -LiteralPath $checksumFile)) { throw 'Thiếu file SHA256.' }
$expected = ((Get-Content -LiteralPath $checksumFile -Raw).Trim() -split '\s+')[0]
if ($expected -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $sourceExe -Algorithm SHA256).Hash -ne $expected) { throw 'Checksum bộ cài không khớp.' }
$currentSession = (Get-Process -Id $PID).SessionId
while ($true) {
    $running = @(Get-Process -Name GoogleNas -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -eq $currentSession })
    if ($running.Count -eq 0) { break }
    Write-Host ''
    Write-Host 'CYAN NAS đang chạy. Cần thoát ứng dụng trước khi cài.' -ForegroundColor Yellow
    foreach ($appProcess in $running) {
        Write-Host ("  PID {0}: {1}" -f $appProcess.Id, $appProcess.Path)
    }
    Write-Host '1. Bấm mũi tên ^ ở góc phải thanh taskbar để mở các biểu tượng ẩn.'
    Write-Host '2. Nhấp chuột phải biểu tượng CYAN NAS, chọn Thoát an toàn.'
    Write-Host '   Bấm X trên cửa sổ chỉ ẩn ứng dụng, chưa thoát.'
    Write-Host '   Nếu còn upload, đóng file đang mở và chờ upload xong rồi thử lại.'
    $answer = Read-Host 'Sau khi thoát, bấm Enter để tiếp tục cài; nhập Q để hủy'
    if ($answer -match '^(?i:q)$') {
        Write-Host 'Đã hủy cài đặt. Ứng dụng đang chạy vẫn được giữ nguyên.'
        exit 0
    }
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
$targetExe = Join-Path $installRoot 'GoogleNas.exe'
if ([System.IO.Path]::GetFullPath($sourceExe) -ne [System.IO.Path]::GetFullPath($targetExe)) { Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $installRoot -Force
$startMenuRoot = Join-Path ([Environment]::GetFolderPath('Programs')) 'CYAN NAS'
New-Item -ItemType Directory -Path $startMenuRoot -Force | Out-Null
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenuRoot 'CYAN NAS.lnk'))
$shortcut.TargetPath = $targetExe
$shortcut.WorkingDirectory = $installRoot
$shortcut.Save()
Write-Output "Đã cài tại $installRoot. Bật khởi chạy cùng Windows trong ứng dụng."
Start-Process -FilePath $targetExe
