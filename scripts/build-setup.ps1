param([switch]$Test, [switch]$Suite, [string]$OutputName = 'GoogleNasSetup.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $projectRoot 'build'
$dependencyDir = Join-Path $buildDir 'dependencies'
$payloadDir = Join-Path $buildDir ('payload-' + [Guid]::NewGuid().ToString('N'))
$appDir = Join-Path $payloadDir 'app'
$prerequisitesDir = Join-Path $payloadDir 'prerequisites'
New-Item -ItemType Directory -Path $appDir,$prerequisitesDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\GoogleNas.exe'),(Join-Path $PSScriptRoot 'uninstall.ps1'),(Join-Path $projectRoot 'README.md') -Destination $appDir
if ($Suite -and (Test-Path -LiteralPath (Join-Path $buildDir 'GoogleNasLauncher.exe'))) { Copy-Item -LiteralPath (Join-Path $buildDir 'GoogleNasLauncher.exe') -Destination (Join-Path $appDir 'GoogleNas.exe') -Force }
if ($Suite) { Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\CyanNasServer.exe'),(Join-Path $projectRoot 'dist\CyanNasClient.exe') -Destination $appDir }
New-Item -ItemType Directory -Path (Join-Path $appDir 'runtime'),(Join-Path $appDir 'licenses') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $dependencyDir 'rclone.exe') -Destination (Join-Path $appDir 'runtime\rclone.exe')
Copy-Item -LiteralPath (Join-Path $dependencyDir 'WinFsp-LICENSE.txt'),(Join-Path $dependencyDir 'rclone-LICENSE.txt'),(Join-Path $dependencyDir 'WinFsp-source-v2.1.zip') -Destination (Join-Path $appDir 'licenses')
Copy-Item -LiteralPath (Join-Path $dependencyDir 'winfsp.msi'),(Join-Path $dependencyDir 'dotnet48.exe') -Destination $prerequisitesDir
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setupSources = @((Join-Path $projectRoot 'installer\Setup.cs'),(Join-Path $projectRoot 'src\Core.cs'),(Join-Path $projectRoot 'src\Theme.cs'),(Join-Path $projectRoot 'src\IconArtwork.cs'),(Join-Path $buildDir 'Version.cs'))
$compileOptions = @('/nologo','/target:winexe','/platform:x64','/optimize+',"/win32icon:$(Join-Path $buildDir 'GoogleNas.ico')",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:Microsoft.CSharp.dll',"/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')")
if ($Suite) { $compileOptions += '/define:CYAN_SUITE' }
& $compiler $compileOptions "/out:$(Join-Path $appDir 'Uninstall.exe')" $setupSources
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
$manifest = [ordered]@{}
Get-ChildItem -LiteralPath $payloadDir -File -Recurse | ForEach-Object { $relative = $_.FullName.Substring($payloadDir.Length + 1).Replace('\','/'); $manifest[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payloadDir 'payload.json') -Encoding UTF8
$payloadZip = Join-Path $buildDir 'setup-payload.zip'
Compress-Archive -Path (Join-Path $payloadDir '*') -DestinationPath $payloadZip -CompressionLevel Optimal -Force
if ($OutputName -notmatch '^GoogleNasSetup(?:-[0-9.]+)?\.exe$') { throw 'Invalid setup output name.' }
$setupExe = Join-Path (Join-Path $projectRoot 'dist') $OutputName
& $compiler $compileOptions "/out:$setupExe" "/resource:$payloadZip,payload.zip" $setupSources
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed.' }
$hash = (Get-FileHash -LiteralPath $setupExe -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $OutputName" | Set-Content -LiteralPath "$setupExe.sha256" -Encoding ASCII
if ($Test) { & (Join-Path $projectRoot 'tests\SetupTests.ps1') -SetupExe $setupExe -BuildDir $buildDir }
Write-Output "Offline installer built: $setupExe"
$brandedSetup = Join-Path (Join-Path $projectRoot 'dist') 'CyanNasSetup.exe'
Copy-Item -LiteralPath $setupExe -Destination $brandedSetup -Force
"$hash  CyanNasSetup.exe" | Set-Content -LiteralPath "$brandedSetup.sha256" -Encoding ASCII
Write-Output "CYAN NAS installer: $brandedSetup"

if ($Suite) {
    # Older updaters accept only GoogleNas.exe. Give them the complete suite installer,
    # launched from TEMP so it can safely replace the old executable after it exits.
    $bootstrap=Join-Path $projectRoot 'dist\GoogleNas.exe'
    $bootstrapOptions=@($compileOptions | Where-Object { $_ -notlike '/define:*' }) + '/define:CYAN_SUITE,LEGACY_BOOTSTRAP'
    & $compiler $bootstrapOptions "/out:$bootstrap" "/resource:$payloadZip,payload.zip" $setupSources
    if($LASTEXITCODE -ne 0){throw 'Legacy suite bootstrap build failed.'}
    $bootstrapHash=(Get-FileHash -LiteralPath $bootstrap -Algorithm SHA256).Hash.ToLowerInvariant()
    "$bootstrapHash  GoogleNas.exe" | Set-Content -LiteralPath "$bootstrap.sha256" -Encoding ASCII
    $probe=Start-Process -FilePath $bootstrap -ArgumentList '--health-check' -PassThru -WindowStyle Hidden
    if(!$probe.WaitForExit(15000) -or $probe.ExitCode -ne 0){throw 'Legacy bootstrap health check failed.'}
    Write-Output 'Legacy updater bootstrap includes the complete two-tool installer.'
}
