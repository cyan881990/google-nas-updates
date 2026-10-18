param([string]$Request, [switch]$LibraryOnly)
$ErrorActionPreference = 'Stop'
function Assert-NasShareName([string]$Name) {
    if ($Name -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,47}$') { throw 'Invalid NAS share name.' }
}
function Assert-NasUserName([string]$Name) {
    if ($Name -notmatch '^[A-Za-z][A-Za-z0-9_-]{2,19}$') { throw 'Invalid NAS local account name.' }
}
function Assert-NasPassword([string]$Password) {
    if ($Password.Length -lt 12 -or $Password -cnotmatch '[A-Z]' -or $Password -cnotmatch '[a-z]' -or $Password -notmatch '[0-9]' -or $Password -notmatch '[^A-Za-z0-9]') { throw 'Mật khẩu NAS cần ít nhất 12 ký tự, có chữ hoa, chữ thường, số và ký hiệu.' }
}
function Assert-NasFolder([string]$Folder) {
    $full = [IO.Path]::GetFullPath($Folder)
    $item = Get-Item -LiteralPath $full -Force
    if (-not $item.PSIsContainer) { throw 'Select an existing folder or drive.' }
    $root = [IO.Path]::GetPathRoot($full)
    $drive = New-Object IO.DriveInfo($root)
    if ($drive.DriveType -notin @([IO.DriveType]::Fixed,[IO.DriveType]::Removable) -or $drive.DriveFormat -notin @('NTFS','ReFS')) { throw 'NAS requires a local NTFS/ReFS drive. FAT/exFAT and mapped network drives are unsupported.' }
    if ($full.TrimEnd('\') -eq $env:SystemDrive) { throw 'Do not share the Windows system drive root.' }
    foreach ($reserved in @($env:WINDIR,$env:ProgramFiles,${env:ProgramFiles(x86)},(Join-Path $env:LOCALAPPDATA 'GoogleNas'),(Join-Path $env:LOCALAPPDATA 'Programs\GoogleNas'))) {
        if ($reserved -and ($full -eq $reserved -or $full.StartsWith($reserved.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase))) { throw 'Select a data folder, not a system/application folder.' }
    }
    $ancestor = $item
    while ($ancestor) {
        if (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Junctions, symbolic links and cloud placeholders are not supported as NAS roots.' }
        $ancestor = $ancestor.Parent
    }
    return $full
}
function Get-NasRuleName([string]$Name,[string]$Sid) {
    $hash = [Security.Cryptography.SHA256]::Create()
    try { $bytes = $hash.ComputeHash([Text.Encoding]::UTF8.GetBytes($Sid+':'+$Name.ToLowerInvariant())); return 'GoogleNas-Tailscale-'+([BitConverter]::ToString($bytes).Replace('-','').Substring(0,24)) } finally { $hash.Dispose() }
}
function Get-NasNetwork {
    $cli = Join-Path $env:ProgramFiles 'Tailscale\tailscale.exe'
    if (-not (Test-Path -LiteralPath $cli)) { throw 'Install Tailscale first.' }
    $status = & $cli status --json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $status.BackendState -ne 'Running') { throw 'Sign in to Tailscale before creating NAS.' }
    $address = @($status.TailscaleIPs | Where-Object { $_ -match '^100\.(6[4-9]|[7-9][0-9]|1[01][0-9]|12[0-7])\.[0-9]{1,3}\.[0-9]{1,3}$' }) | Select-Object -First 1
    if (-not $address) { throw 'Tailscale IPv4 is unavailable.' }
    $interface = Get-NetIPAddress -AddressFamily IPv4 -IPAddress $address | Select-Object -First 1
    if (-not $interface) { throw 'Tailscale network adapter is not ready.' }
    return @{ Address=$address; Interface=$interface.InterfaceAlias; Cli=$cli }
}
function Install-NasTailscale {
    $cli = Join-Path $env:ProgramFiles 'Tailscale\tailscale.exe'
    if (Test-Path -LiteralPath $cli) { return @{ message='Tailscale is already installed.' } }
    $directory = Join-Path $env:LOCALAPPDATA 'GoogleNas\downloads'; New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $package = Join-Path $directory 'tailscale-1.104.1-amd64.msi'
    $expected = '6DA8BCDB0D45A214CAE0E8A88C2987AD48DEC134BF04DFFB9C05F1354C02B0B4'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (-not (Test-Path -LiteralPath $package) -or (Get-FileHash -LiteralPath $package).Hash -ne $expected) { Invoke-WebRequest -UseBasicParsing -Uri 'https://pkgs.tailscale.com/stable/tailscale-setup-1.104.1-amd64.msi' -OutFile $package -TimeoutSec 120 }
    if ((Get-FileHash -LiteralPath $package).Hash -ne $expected) { throw 'Tailscale installer checksum mismatch.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $package
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|, )O=Tailscale Inc\.(?:,|$)') { throw 'Tailscale installer signature is not valid.' }
    $log = Join-Path $directory 'tailscale-install.log'
    # MSI switches must remain unquoted.
    $installer = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList @('/i',('"'+$package+'"'),'/qn','/norestart','TS_NOLAUNCH=1','/l*v',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if (-not $installer.WaitForExit(180000)) { throw "Windows Installer is still running (PID $($installer.Id)); do not start a second install. Log: $log" }
    if ($installer.ExitCode -notin @(0,3010,1641)) { throw "Tailscale install failed ($($installer.ExitCode)). Log: $log" }
    if ($installer.ExitCode -in @(3010,1641)) { return @{ message='Tailscale installed. Restart Windows before connecting.'; restart=$true } }
    if (-not (Test-Path -LiteralPath $cli)) { throw 'Tailscale executable is missing after install.' }
    return @{ message='Tailscale installed. Sign in to connect.' }
}
function Invoke-NasOperation($Data) {
    $profile = $Data.Profile; $owner = 'GoogleNas:'+ $Data.UserSid
    switch ($Data.Operation) {
        'Install' { return Install-NasTailscale }
        'Unattended' { $network=Get-NasNetwork; & $network.Cli set --unattended=true; if ($LASTEXITCODE -ne 0) { throw 'Unable to enable unattended mode.' }; return @{ message='Tailscale can run after Windows sign-out.' } }
        'Inspect' {
            Assert-NasShareName $profile.Share
            $share = Get-SmbShare -Name $profile.Share -ErrorAction SilentlyContinue
            if (-not $share -or $share.Description -ne $owner) { return @{ enabled=$false; message='No managed NAS share with this name.' } }
            return @{ enabled=$true; folder=$share.Path; message='NAS share is enabled.' }
        }
        'Create' {
            Assert-NasShareName $profile.Share; Assert-NasUserName $profile.User
            $folder=Assert-NasFolder $profile.Folder; $network=Get-NasNetwork
            if (Get-SmbShare -Name $profile.Share -ErrorAction SilentlyContinue) { throw 'This share name already exists. Stop the managed NAS or choose another name.' }
            $account=Get-LocalUser -Name $profile.User -ErrorAction SilentlyContinue
            if ($profile.CreateUser -and $account) { throw 'Account already exists. Uncheck Create account to use its current password.' }
            if (-not $profile.CreateUser -and (-not $account -or -not $account.Enabled)) { throw 'Select an existing enabled local Windows account.' }
            if ($profile.CreateUser) { Assert-NasPassword $Data.Password }
            $oldAcl=Get-Acl -LiteralPath $folder; $oldSddl=$oldAcl.Sddl
            $createdAccount=$false; $createdShare=$false; $createdRule=$false; $aclChanged=$false
            $rule=Get-NasRuleName $profile.Share $Data.UserSid
            if (Get-NetFirewallRule -Name $rule -ErrorAction SilentlyContinue) { throw 'A managed firewall rule already exists. Stop this NAS before creating it again.' }
            try {
                if ($profile.CreateUser) {
                    $secure=ConvertTo-SecureString $Data.Password -AsPlainText -Force
                    $account=New-LocalUser -Name $profile.User -Password $secure -Description 'CYAN NAS remote file access' -PasswordNeverExpires
                    $createdAccount=$true
                    $users=Get-LocalGroup -SID 'S-1-5-32-545'; Add-LocalGroupMember -Group $users -Member $account
                }
                $rights=if ($profile.ReadOnly) { [Security.AccessControl.FileSystemRights]::ReadAndExecute } else { [Security.AccessControl.FileSystemRights]::Modify }
                $grant=New-Object Security.AccessControl.FileSystemAccessRule($account.SID,$rights,([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit),[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow)
                $oldAcl.AddAccessRule($grant); Set-Acl -LiteralPath $folder -AclObject $oldAcl; $aclChanged=$true
                Set-Service -Name LanmanServer -StartupType Automatic; Start-Service LanmanServer
                $parameters=@{ Name=$profile.Share; Path=$folder; Description=$owner; EncryptData=$true; FolderEnumerationMode='AccessBased' }
                $qualified=$env:COMPUTERNAME+'\'+$profile.User
                if ($profile.ReadOnly) { $parameters.ReadAccess=$qualified } else { $parameters.ChangeAccess=$qualified }
                New-SmbShare @parameters | Out-Null; $createdShare=$true
                New-NetFirewallRule -Name $rule -DisplayName ('CYAN NAS - '+$profile.Share) -Group 'CYAN NAS' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 445 -LocalAddress $network.Address -RemoteAddress '100.64.0.0/10' -InterfaceAlias $network.Interface -Service LanmanServer -Profile Any | Out-Null; $createdRule=$true
                return @{ message='NAS created.'; address=$network.Address; path=('\\'+$network.Address+'\'+$profile.Share); account=$qualified; readOnly=$profile.ReadOnly }
            } catch {
                $failure=$_
                if ($createdRule) { Remove-NetFirewallRule -Name $rule -ErrorAction Continue }
                if ($createdShare) { Remove-SmbShare -Name $profile.Share -Force -ErrorAction Continue }
                if ($aclChanged) { $restore=Get-Acl -LiteralPath $folder; $restore.SetSecurityDescriptorSddlForm($oldSddl); Set-Acl -LiteralPath $folder -AclObject $restore }
                if ($createdAccount) { Remove-LocalUser -Name $profile.User -ErrorAction Continue }
                throw $failure
            }
        }
        'SetNasPassword' {
            Assert-NasShareName $profile.Share; Assert-NasUserName $profile.User; Assert-NasPassword $Data.Password
            $account=Get-LocalUser -Name $profile.User -ErrorAction SilentlyContinue
            if (-not $account -or -not $account.Enabled) { throw 'Chọn tài khoản NAS đã tồn tại và đang được bật trên máy này.' }
            if ($account.SID.Value -eq $Data.UserSid -or $account.Description -notin @('CYAN NAS remote file access','Google NAS remote file access')) { throw 'Chỉ đặt lại mật khẩu tài khoản NAS riêng do tool tạo. Tool không đặt lại mật khẩu tài khoản Windows cá nhân.' }
            $share=Get-SmbShare -Name $profile.Share -ErrorAction SilentlyContinue
            if ($share -and $share.Description -ne $owner) { throw 'NAS này không thuộc tài khoản đang quản lý; chưa thay đổi mật khẩu.' }
            $secure=ConvertTo-SecureString $Data.Password -AsPlainText -Force
            Set-LocalUser -SID $account.SID -Password $secure
            return @{ message='Đã đặt mật khẩu Windows mới cho tài khoản NAS. Chia sẻ và phiên đang mở được giữ nguyên.'; account=($env:COMPUTERNAME+'\'+$profile.User) }
        }
        'Remove' {
            Assert-NasShareName $profile.Share
            $share=Get-SmbShare -Name $profile.Share -ErrorAction SilentlyContinue
            $ruleName=Get-NasRuleName $profile.Share $Data.UserSid
            if (-not $share) { if(Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue){Remove-NetFirewallRule -Name $ruleName}; return @{ message='NAS share is already stopped.' } }
            if ($share.Description -ne $owner) { throw 'This share was not created by CYAN NAS for your account.' }
            $root=$share.Path.TrimEnd('\')+'\'
            $blocking=@(Get-SmbOpenFile -ErrorAction Stop | Where-Object {
                $_.Path -and ($_.Path.TrimEnd('\') -eq $share.Path.TrimEnd('\') -or $_.Path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase))
            } | Where-Object {
                # SMB also reports Explorer's directory handles. Those contain no file writes.
                # Unknown/deleted/inaccessible paths still block instead of guessing they are safe.
                try { $item=Get-Item -LiteralPath $_.Path -Force -ErrorAction Stop }
                catch { throw ('Khong xac dinh duoc file SMB: '+$_.Exception.Message) }
                -not $item.PSIsContainer
            })
            if ($blocking.Count -gt 0) {
                $details=($blocking | Select-Object -First 5 | ForEach-Object { $_.Path+' (may: '+$_.ClientComputerName+', tai khoan: '+$_.ClientUserName+')' }) -join "`n"
                throw ("NAS con file dang duoc may khac giu. Dong file tren may do truoc khi dung chia se:`n"+$details)
            }
            Remove-SmbShare -Name $profile.Share -Force
            if(Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue){Remove-NetFirewallRule -Name $ruleName}
            return @{ message='NAS sharing stopped. Local account and folder permissions are preserved.' }
        }
        default { throw 'Unknown NAS operation.' }
    }
}
if ($LibraryOnly) { return }
try {
    Add-Type -AssemblyName System.Security
    $expectedRoot=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'GoogleNas\admin-requests')).TrimEnd('\')+'\'
    $requestPath=[IO.Path]::GetFullPath($Request)
    if (-not $requestPath.StartsWith($expectedRoot,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($requestPath) -ne '.request') { throw 'Invalid NAS request location.' }
    $bytes=[Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($requestPath),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
    try { $data=[Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json } finally { [Array]::Clear($bytes,0,$bytes.Length) }
    if ($data.UserSid -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) { throw 'Run the approval under the same Windows account.' }
    $details=Invoke-NasOperation $data
    @{ ok=$true; details=$details } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath ($requestPath+'.result') -Encoding UTF8
} catch {
    if ($requestPath -and $requestPath.StartsWith($expectedRoot,[StringComparison]::OrdinalIgnoreCase)) { @{ ok=$false; error=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath ($requestPath+'.result') -Encoding UTF8 }
    exit 1
}
