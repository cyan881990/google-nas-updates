param([switch]$Test, [switch]$OfflineInstaller)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Cần Windows 64-bit và .NET Framework 4.8.' }
$buildDir = Join-Path $projectRoot 'build'
$distDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Path $buildDir,$distDir -Force | Out-Null
if ($OfflineInstaller) { & (Join-Path $PSScriptRoot 'prepare-dependencies.ps1') }
if (Test-Path -LiteralPath (Join-Path $buildDir 'dependencies\rclone.exe')) {
    New-Item -ItemType Directory -Path (Join-Path $distDir 'runtime'),(Join-Path $distDir 'licenses') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $buildDir 'dependencies\rclone.exe') -Destination (Join-Path $distDir 'runtime\rclone.exe') -Force
    Copy-Item -LiteralPath (Join-Path $buildDir 'dependencies\rclone-LICENSE.txt'),(Join-Path $buildDir 'dependencies\WinFsp-LICENSE.txt'),(Join-Path $buildDir 'dependencies\WinFsp-source-v2.1.zip') -Destination (Join-Path $distDir 'licenses') -Force
}
$coreFile = Join-Path $projectRoot 'src\Core.cs'
$versionMatch = [regex]::Match((Get-Content -LiteralPath $coreFile -Raw), 'const string Version = "(\d+\.\d+\.\d+)"')
if (-not $versionMatch.Success) { throw 'Không tìm thấy version trong Core.cs.' }
$appVersion = $versionMatch.Groups[1].Value
$versionFile = Join-Path $buildDir 'Version.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("$appVersion.0")]
[assembly: AssemblyFileVersion("$appVersion.0")]
"@ | Set-Content -LiteralPath $versionFile -Encoding UTF8
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$outputExe = Join-Path $distDir 'GoogleNas.exe'
$iconGenerator = Join-Path $buildDir 'GenerateIcon.exe'
& $compiler /nologo /target:exe "/out:$iconGenerator" /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'GenerateIcon.cs') (Join-Path $projectRoot 'src\IconArtwork.cs')
if ($LASTEXITCODE -ne 0) { throw 'Icon generator compilation failed.' }
$appIcon = Join-Path $buildDir 'GoogleNas.ico'
& $iconGenerator $appIcon
if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$outputExe" "/win32icon:$appIcon" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')" "/resource:$(Join-Path $PSScriptRoot 'apply-update.ps1'),apply-update.ps1" "/resource:$(Join-Path $PSScriptRoot 'nas-admin.ps1'),nas-admin.ps1" $sourceFiles $versionFile
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$hash = (Get-FileHash -LiteralPath $outputExe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  GoogleNas.exe" | Set-Content -LiteralPath (Join-Path $distDir 'GoogleNas.exe.sha256') -Encoding ASCII
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.ps1') -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'uninstall.ps1') -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.cmd') -Destination $distDir -Force
if ($Test) {
    $testExe = Join-Path $buildDir 'CoreTests.exe'
    & $compiler /nologo /target:exe "/out:$testExe" /reference:System.Web.Extensions.dll $coreFile (Join-Path $projectRoot 'tests\CoreTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $parseFailed = $false
    Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1' | ForEach-Object {
        $parseTokens = $null; $parseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
        if ($parseErrors) { $parseErrors | Write-Output; $parseFailed = $true }
    }
    if ($parseFailed) { throw 'PowerShell syntax errors.' }
    $health = Start-Process -FilePath $outputExe -ArgumentList '--health-check' -PassThru -WindowStyle Hidden
    if (-not $health.WaitForExit(15000)) { throw 'Health check timeout.' }
    if ($health.ExitCode -ne 0) { throw 'Health check failed.' }
    Write-Output 'PowerShell syntax and executable health check passed.'
    & (Join-Path $projectRoot 'tests\UpdateTests.ps1') -BuildDir $buildDir
    $uiTestExe = Join-Path $buildDir 'UiTests.exe'
    & $compiler /nologo /target:exe /main:UiTests "/out:$uiTestExe" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $sourceFiles (Join-Path $projectRoot 'tests\UiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI tests compilation failed.' }
    & $uiTestExe (Join-Path $buildDir "ui-tests-$appVersion")
    if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
    $nativeUiTestExe = Join-Path $buildDir 'NativeUiTests.exe'
    & $compiler /nologo /target:exe /main:UiTests "/out:$nativeUiTestExe" "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $sourceFiles (Join-Path $projectRoot 'tests\UiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Native DPI tests compilation failed.' }
    & $nativeUiTestExe (Join-Path $buildDir "native-ui-$appVersion") --native-dpi
    if ($LASTEXITCODE -ne 0) { throw 'Native DPI tests failed.' }
    $nasTestExe = Join-Path $buildDir 'NasTests.exe'
    & $compiler /nologo /target:exe "/out:$nasTestExe" /reference:System.Security.dll /reference:System.Web.Extensions.dll $coreFile (Join-Path $projectRoot 'src\Nas.cs') (Join-Path $projectRoot 'tests\NasTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'NAS tests compilation failed.' }
    & $nasTestExe (Join-Path $buildDir ('nas-tests-'+[Guid]::NewGuid().ToString('N')))
    if ($LASTEXITCODE -ne 0) { throw 'NAS tests failed.' }
    & (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $projectRoot 'tests\NasAdminTests.ps1') -BuildDir $buildDir
    if ($LASTEXITCODE -ne 0) { throw 'NAS admin workflow tests failed.' }
}
$portableFiles = @('GoogleNas.exe','GoogleNas.exe.sha256','README.md','Install.cmd','install.ps1','uninstall.ps1','runtime','licenses') | ForEach-Object { Join-Path $distDir $_ } | Where-Object { Test-Path -LiteralPath $_ }
Compress-Archive -Path $portableFiles -DestinationPath (Join-Path $buildDir "GoogleNas-$appVersion-windows-x64.zip") -Force
Write-Output "Built $outputExe ($appVersion)"
if ($OfflineInstaller) { & (Join-Path $PSScriptRoot 'build-setup.ps1') -Test:$Test }
