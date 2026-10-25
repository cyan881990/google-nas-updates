param([switch]$Test)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'build.ps1') -Test:$Test -OfflineInstaller
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\GoogleNas.exe') -Destination (Join-Path $projectRoot 'build\GoogleNasLauncher.exe') -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@(Get-ChildItem (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$iconGenerator=Join-Path $projectRoot 'build\GenerateSuiteIcon.exe'
& $compiler /nologo /target:exe "/out:$iconGenerator" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'GenerateSuiteIcon.cs') (Join-Path $projectRoot 'src\SuiteGlyph.cs') (Join-Path $projectRoot 'src\Theme.cs') (Join-Path $projectRoot 'src\IconArtwork.cs')
if($LASTEXITCODE -ne 0){throw 'Suite icon generation compilation failed'}
$suiteIcon=Join-Path $projectRoot 'build\CyanNas.ico'
& $iconGenerator $suiteIcon Client
if($LASTEXITCODE -ne 0){throw 'Unified icon generation failed'}
$exe=Join-Path $projectRoot 'dist\CyanNas.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /define:CYAN_UNIFIED "/out:$exe" "/win32icon:$suiteIcon" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')" "/resource:$(Join-Path $PSScriptRoot 'apply-update.ps1'),apply-update.ps1" "/resource:$(Join-Path $PSScriptRoot 'nas-admin.ps1'),nas-admin.ps1" $source (Join-Path $projectRoot 'build\Version.cs')
if($LASTEXITCODE -ne 0){throw 'Unified compilation failed'}
$hash=(Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  CyanNas.exe" | Set-Content "$exe.sha256" -Encoding ASCII
$health=Start-Process -FilePath $exe -ArgumentList '--health-check' -PassThru -WindowStyle Hidden
if(!$health.WaitForExit(15000) -or $health.ExitCode -ne 0){throw 'Unified health check failed'}
foreach($edition in @('Server','Client')){
 $alias=Join-Path $projectRoot "dist\CyanNas$edition.exe"
 $symbol=if($edition -eq 'Server'){'SERVER_LINK'}else{'CLIENT_LINK'}
 & $compiler /nologo /target:winexe /platform:x64 /optimize+ "/define:$symbol" "/out:$alias" "/win32icon:$suiteIcon" /reference:System.Windows.Forms.dll (Join-Path $projectRoot 'installer\RoleLauncher.cs') (Join-Path $projectRoot 'build\Version.cs')
 if($LASTEXITCODE -ne 0){throw 'Compatibility launcher failed'}
 $hash=(Get-FileHash $alias -Algorithm SHA256).Hash.ToLowerInvariant()
 "$hash  CyanNas$edition.exe" | Set-Content "$alias.sha256" -Encoding ASCII
}
if($Test){ & (Join-Path $PSScriptRoot 'test-suite.ps1') }
& (Join-Path $PSScriptRoot 'build-setup.ps1') -Suite -Test:$Test
Write-Output 'One CYAN NAS application contains SERVER and CLIENT tabs.'
