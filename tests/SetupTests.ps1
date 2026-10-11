param([Parameter(Mandatory=$true)][string]$SetupExe, [Parameter(Mandatory=$true)][string]$BuildDir)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $BuildDir ('setup-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$extractDir = Join-Path $testDir 'extracted'
$verify = Start-Process -FilePath $SetupExe -ArgumentList @('--verify-payload', ('"' + $extractDir + '"')) -PassThru -WindowStyle Hidden
if (-not $verify.WaitForExit(60000)) { throw 'Payload extraction timeout.' }
if ($verify.ExitCode -ne 0) { if (Test-Path -LiteralPath "$extractDir.error.txt") { Get-Content -LiteralPath "$extractDir.error.txt" }; throw 'Payload verification failed.' }
$expected = @{
    'prerequisites\winfsp.msi' = '073A70E00F77423E34BED98B86E600DEF93393BA5822204FAC57A29324DB9F7A'
    'prerequisites\dotnet48.exe' = '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40'
}
foreach ($file in $expected.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $extractDir $file)).Hash -ne $expected[$file]) { throw "Embedded prerequisite mismatch: $file" } }
if (-not (Test-Path -LiteralPath (Join-Path $extractDir 'app\runtime\rclone.exe'))) { throw 'Missing embedded rclone.' }
Write-Output 'Single EXE payload extraction and all checksums passed without downloads or system installation.'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe = Join-Path $testDir 'InstallerCoreTests.exe'
& $compiler /nologo /target:exe /main:InstallerCoreTests "/out:$testExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:Microsoft.CSharp.dll (Join-Path $projectRoot 'installer\Setup.cs') (Join-Path $projectRoot 'src\Core.cs') (Join-Path $projectRoot 'src\Theme.cs') (Join-Path $PSScriptRoot 'InstallerTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer tests compilation failed.' }
& $testExe (Join-Path $testDir 'file-tests')
if ($LASTEXITCODE -ne 0) { throw 'Installer file tests failed.' }
