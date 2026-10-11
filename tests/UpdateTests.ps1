param([Parameter(Mandatory=$true)][string]$BuildDir)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testRoot = Join-Path $BuildDir ('updater-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($variant in @('old','good','bad')) {
    $healthExit = if ($variant -eq 'bad') { 1 } else { 0 }
    $fixture = Join-Path $testRoot "$variant.cs"
    "class Fixture { static int Main(string[] args) { if (args.Length > 0 && args[0] == `"--health-check`") return $healthExit; return 0; } }" | Set-Content -LiteralPath $fixture -Encoding UTF8
    & $compiler /nologo /target:winexe "/out:$(Join-Path $testRoot "$variant.exe")" $fixture
    if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
}
$shellPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$helper = Join-Path $projectRoot 'scripts\apply-update.ps1'
foreach ($scenario in @('success','rollback','tampered')) {
    $scenarioRoot = Join-Path $testRoot $scenario
    New-Item -ItemType Directory -Path $scenarioRoot -Force | Out-Null
    $target = Join-Path $scenarioRoot 'GoogleNas.exe'
    $stage = Join-Path $scenarioRoot 'staged.exe'
    Copy-Item -LiteralPath (Join-Path $testRoot 'old.exe') -Destination $target
    $oldHash = (Get-FileHash -LiteralPath $target).Hash
    $variant = if ($scenario -eq 'rollback') { 'bad' } else { 'good' }
    Copy-Item -LiteralPath (Join-Path $testRoot "$variant.exe") -Destination $stage
    $stageHash = (Get-FileHash -LiteralPath $stage).Hash
    $expectedHash = if ($scenario -eq 'tampered') { '0' * 64 } else { $stageHash }
    & $shellPath -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $helper -ProcessId 2147483647 -Stage $stage -Target $target -ExpectedHash $expectedHash
    $helperExit = $LASTEXITCODE
    $resultHash = (Get-FileHash -LiteralPath $target).Hash
    if ($scenario -eq 'success') {
        if ($helperExit -ne 0 -or $resultHash -ne $stageHash -or (Get-FileHash -LiteralPath "$target.previous").Hash -ne $oldHash) { throw 'Successful update or retained backup failed.' }
    } else {
        if ($helperExit -eq 0 -or $resultHash -ne $oldHash) { throw "$scenario did not preserve the old executable." }
    }
    Write-Output "Updater integration: $scenario passed."
}
