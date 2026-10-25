$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source=@(Get-ChildItem (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$license=Join-Path $projectRoot 'build\CyanProTests.exe'
& $compiler /nologo /target:exe /main:CyanProTests "/out:$license" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $source (Join-Path $projectRoot 'tests\CyanProTests.cs')
if($LASTEXITCODE -ne 0){throw 'CYANPRO tests compilation failed'}
& $license (Join-Path $projectRoot ('build\cyanpro-tests-'+[Guid]::NewGuid().ToString('N'))) (Join-Path $projectRoot 'dist\CyanNasSetup.exe')
if($LASTEXITCODE -ne 0){throw 'CYANPRO tests failed'}
$exe=Join-Path $projectRoot 'build\SuiteTests.exe'
& $compiler /nologo /target:exe /main:SuiteTests "/out:$exe" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $source (Join-Path $projectRoot 'tests\SuiteTests.cs')
if($LASTEXITCODE -ne 0){throw 'Suite tests compilation failed'}
& $exe (Join-Path $projectRoot ('build\suite-tests-'+[Guid]::NewGuid().ToString('N'))) (Join-Path $projectRoot 'build\dependencies\rclone.exe')
if($LASTEXITCODE -ne 0){throw 'Suite tests failed'}

$ui=Join-Path $projectRoot 'build\SuiteUiTests.exe'
& $compiler /nologo /target:exe /main:SuiteUiTests "/out:$ui" /reference:System.Security.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32manifest:$(Join-Path $projectRoot 'src\app.manifest')" $source (Join-Path $projectRoot 'tests\SuiteUiTests.cs')
if($LASTEXITCODE -ne 0){throw 'Suite UI tests compilation failed'}
& $ui (Join-Path $projectRoot 'build\suite-ui-tests')
if($LASTEXITCODE -ne 0){throw 'Suite UI tests failed'}

$installerTest=Join-Path $projectRoot 'build\SuiteInstallerTests.exe'
& $compiler /nologo /target:exe /define:CYAN_SUITE /main:SuiteInstallerTests "/out:$installerTest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll (Join-Path $projectRoot 'installer\Setup.cs') (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\Theme.cs') (Join-Path $projectRoot 'src\IconArtwork.cs') (Join-Path $projectRoot 'tests\SuiteInstallerTests.cs')
if($LASTEXITCODE -ne 0){throw 'Suite installer tests compilation failed'}
& $installerTest (Join-Path $projectRoot ('build\suite-shortcuts-'+[Guid]::NewGuid().ToString('N')))
if($LASTEXITCODE -ne 0){throw 'Suite installer tests failed'}
