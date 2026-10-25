param([int]$WaitProcessId = 0)
$ErrorActionPreference = 'Stop'
if ($WaitProcessId -gt 0) {
    $uninstaller = Get-Process -Id $WaitProcessId -ErrorAction SilentlyContinue
    if ($uninstaller -and -not $uninstaller.WaitForExit(15000)) { throw 'Trình gỡ chưa thoát.' }
}
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas'
$resolvedRoot = [System.IO.Path]::GetFullPath($installRoot)
if ($resolvedRoot -ne [System.IO.Path]::GetFullPath($PSScriptRoot)) { throw 'Chạy uninstall.ps1 từ thư mục ứng dụng đã cài.' }
if (Get-Process -Name GoogleNas,CyanNas,CyanNasServer,CyanNasClient -ErrorAction SilentlyContinue) { throw 'Chọn Thoát an toàn từ khay hệ thống trước khi gỡ.' }
Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name GoogleNas -ErrorAction SilentlyContinue
foreach ($suiteId in @('CyanNas','CyanNasServer','CyanNasClient')) { Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $suiteId -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GoogleNas' -ErrorAction SilentlyContinue
$menuRoot = [System.IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('Programs')) 'CYAN NAS'))
$menuParent = [System.IO.Path]::GetFullPath([Environment]::GetFolderPath('Programs')).TrimEnd('\') + '\'
if (-not $menuRoot.StartsWith($menuParent, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid Start Menu path.' }
if (Test-Path -LiteralPath $menuRoot) { Remove-Item -LiteralPath $menuRoot -Recurse -Force }
Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
Write-Output 'Đã gỡ ứng dụng. Dữ liệu, token và cache được giữ trong %LOCALAPPDATA%\GoogleNas, CyanNasServer và CyanNasClient. WinFsp không bị gỡ.'
