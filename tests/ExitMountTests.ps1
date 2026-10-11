param([Parameter(Mandatory=$true)][string]$AppExe,[Parameter(Mandatory=$true)][string]$RcloneExe)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $AppExe).Path) | Out-Null
$fixture=Join-Path $env:TEMP ('GoogleNas-exit-mount-'+[Guid]::NewGuid().ToString('N'))
$remote=Join-Path $fixture 'remote'
New-Item -ItemType Directory -Path $remote -Force | Out-Null
$service=New-Object GoogleNas.Services((Join-Path $fixture 'home'))
$listener=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0)
$listener.Start(); $port=$listener.LocalEndpoint.Port; $listener.Stop()
$password=[Guid]::NewGuid().ToString('N')
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
[GoogleNas.Services].GetField('port',$flags).SetValue($service,$port)
[GoogleNas.Services].GetField('password',$flags).SetValue($service,$password)
$letter=@('Z','Y','X','W','V','U','T','S','R','Q','P','O','N','M','L','K','J','I','H' | Where-Object { -not (Test-Path ($_+':\')) })[0]
if(-not $letter){throw 'No free drive letter for isolated test.'}
$info=New-Object Diagnostics.ProcessStartInfo
$info.FileName=$RcloneExe
$info.Arguments=[GoogleNas.Core]::Args([string[]]@('mount',$remote,($letter+':'),'--vfs-cache-mode','full','--cache-dir',$service.Cache,'--vfs-write-back','1s','--network-mode','--log-file',([IO.Path]::Combine($fixture,'rclone.log')),'--log-level','INFO','--rc','--rc-addr',('127.0.0.1:'+ $port)))
$info.UseShellExecute=$false; $info.CreateNoWindow=$true
$info.EnvironmentVariables['RCLONE_RC_USER']='googlenas'; $info.EnvironmentVariables['RCLONE_RC_PASS']=$password
$service.Mount=[Diagnostics.Process]::Start($info)
$writer=$null
try {
    $stats=$null
    for($attempt=0;$attempt -lt 40;$attempt++){
        try {$stats=$service.Rc('vfs/stats').GetAwaiter().GetResult(); break} catch {Start-Sleep -Milliseconds 250}
    }
    if(-not $stats){throw 'Fixture VFS did not start.'}
    if($stats['inUse'] -lt 1 -or -not [GoogleNas.Core]::SafeToStop($stats)){throw 'Mounted idle reference regression.'}
    [GoogleNas.Core]::CheckCacheIdle($service.Cache)
    $service.Stop().GetAwaiter().GetResult()
    if($service.Running){throw 'Safe exit did not stop mount.'}
    Write-Output 'Real rclone VFS: idle inUse=1 allows exit; Stop gracefully exited rclone through authenticated RC.'
} finally {
    if($writer){$writer.Dispose()}
    if($service.Running){$service.Mount.Kill();$service.Mount.WaitForExit(5000)|Out-Null}
    if(-not ([IO.Path]::GetFullPath($fixture).StartsWith([IO.Path]::GetFullPath($env:TEMP)+[IO.Path]::DirectorySeparatorChar))){throw 'Fixture cleanup boundary failure.'}
    Remove-Item -LiteralPath $fixture -Recurse -Force
}
