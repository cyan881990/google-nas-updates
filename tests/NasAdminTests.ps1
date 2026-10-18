param([Parameter(Mandatory=$true)][string]$BuildDir)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\nas-admin.ps1') -LibraryOnly
$fixture=Join-Path $env:TEMP ('GoogleNas-admin-tests-'+[Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory -Path $fixture -Force | Out-Null
# .NET Framework expands existing 8.3 TEMP paths on hosted Windows runners.
# Match the path normalization used by Assert-NasFolder and the SMB service.
$fixture=[IO.Path]::GetFullPath($fixture)
$originalAcl=(Get-Acl -LiteralPath $fixture).Sddl
$script:shares=@{}; $script:rules=@{}; $script:account=$null; $script:open=$false; $script:openDirectory=$false; $script:failFirewall=$false; $script:lastShare=$null; $script:lastRule=$null
Set-Content -LiteralPath (Join-Path $fixture 'open.txt') -Value 'fixture'
function Get-NasNetwork { return @{Address='100.100.0.1';Interface='Fixture Tailscale'} }
function Get-SmbShare { param($Name,$ErrorAction) if($script:shares.ContainsKey($Name)){return $script:shares[$Name]} }
function Get-LocalUser { param($Name,$ErrorAction) return $script:account }
function New-LocalUser { param($Name,$Password,$Description,[switch]$PasswordNeverExpires) $script:account=[pscustomobject]@{Name=$Name;Enabled=$true;Description=$Description;SID=(New-Object Security.Principal.SecurityIdentifier('S-1-5-21-123456789-123456789-123456789-1001'))};return $script:account }
function Set-LocalUser { param($SID,$Password) $script:passwordChanges++ }
function Remove-LocalUser { param($Name,$ErrorAction) $script:account=$null }
function Get-LocalGroup { param($SID) return [pscustomobject]@{Name='Fixture Users'} }
function Add-LocalGroupMember { param($Group,$Member) }
function Set-Service { param($Name,$StartupType) }
function Start-Service { param($Name) }
function New-SmbShare { param($Name,$Path,$Description,$EncryptData,$FolderEnumerationMode,$ReadAccess,$ChangeAccess)
    $script:lastShare=@{Name=$Name;Path=$Path;Description=$Description;EncryptData=$EncryptData;ReadAccess=$ReadAccess;ChangeAccess=$ChangeAccess}; $script:shares[$Name]=[pscustomobject]$script:lastShare
}
function Remove-SmbShare { param($Name,[switch]$Force,$ErrorAction) $script:shares.Remove($Name) }
function Get-NetFirewallRule { param($Name,$ErrorAction) if($script:rules.ContainsKey($Name)){return $script:rules[$Name]} }
function New-NetFirewallRule { param($Name,$DisplayName,$Group,$Direction,$Action,$Protocol,$LocalPort,$LocalAddress,$RemoteAddress,$InterfaceAlias,$Service,$Profile)
    if($script:failFirewall){throw 'fixture firewall failure'}
    $script:lastRule=@{LocalPort=$LocalPort;LocalAddress=$LocalAddress;RemoteAddress=$RemoteAddress;InterfaceAlias=$InterfaceAlias;Service=$Service;Action=$Action}; $script:rules[$Name]=[pscustomobject]$script:lastRule
}
function Remove-NetFirewallRule { param($Name,$ErrorAction) $script:rules.Remove($Name) }
function Get-SmbOpenFile { param($ErrorAction) if($script:open){return [pscustomobject]@{Path=(Join-Path $fixture 'open.txt');ClientComputerName='FixtureClient';ClientUserName='FixtureUser'}}; if($script:openDirectory){return [pscustomobject]@{Path=$fixture;ClientComputerName='FixtureExplorer';ClientUserName='FixtureUser'}} }
function Check($Condition,[string]$Message) { if(-not $Condition){throw $Message} }
function Reject([scriptblock]$Action,[string]$Message) { $rejected=$false;try{& $Action | Out-Null}catch{$rejected=$true};Check $rejected $Message }
$profile=[pscustomobject]@{Share='FixtureNas';Folder=$fixture;User='fixturenas';CreateUser=$true;ReadOnly=$false}
$data=[pscustomobject]@{Operation='Create';Profile=$profile;Password='FixturePass!12345';UserSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value}
try {
    Reject {Assert-NasShareName 'x;whoami'} 'Share injection accepted'
    Reject {Assert-NasUserName 'a\b'} 'User injection accepted'
    Reject {Assert-NasFolder ($env:SystemDrive+'\')} 'Windows root accepted'
    $result=Invoke-NasOperation $data
    Check ($result.path -eq '\\100.100.0.1\FixtureNas') 'Wrong NAS path'
    Check ($script:lastShare.EncryptData -and $script:lastShare.ChangeAccess -and -not $script:lastShare.ReadAccess) 'Write share permissions/encryption wrong'
    Check ($script:lastRule.LocalPort -eq 445 -and $script:lastRule.LocalAddress -eq '100.100.0.1' -and $script:lastRule.RemoteAddress -eq '100.64.0.0/10' -and $script:lastRule.InterfaceAlias -eq 'Fixture Tailscale') 'Firewall not scoped to Tailscale'
    $data.Operation='SetNasPassword';$script:open=$true;$script:passwordChanges=0
    Invoke-NasOperation $data | Out-Null
    Check ($script:passwordChanges -eq 1 -and $script:shares.Count -eq 1 -and $script:rules.Count -eq 1) 'Password change stopped share or changed firewall'
    foreach($weak in @('lowercase!12345','UPPERCASE!12345','NoNumber!Password','NoSymbolPass123','Short!12')) { $data.Password=$weak; Reject {Invoke-NasOperation $data} 'Weak password reset accepted' }; $data.Password='FixturePass!12345'
    $script:account.Description='Personal account';Reject {Invoke-NasOperation $data} 'Personal account password reset accepted';$script:account.Description='CYAN NAS remote file access'
    $script:account.Enabled=$false;Reject {Invoke-NasOperation $data} 'Disabled account password reset accepted';$script:account.Enabled=$true
    $requestSid=$data.UserSid;$data.UserSid=$script:account.SID.Value;Reject {Invoke-NasOperation $data} 'Operator Windows password reset accepted';$data.UserSid=$requestSid
    Check ($script:passwordChanges -eq 1) 'Rejected reset still modified password'
    $data.Operation='Remove';Reject {Invoke-NasOperation $data} 'Open file removal accepted';Check ($script:shares.Count -eq 1) 'Open-file guard removed share'
    $script:open=$false;$script:openDirectory=$true;Invoke-NasOperation $data | Out-Null;Check ($script:shares.Count -eq 0 -and $script:rules.Count -eq 0 -and $null -ne $script:account) 'Directory handle blocked removal or removal changed account';$script:openDirectory=$false
    $data.Operation='Create';$profile.CreateUser=$false;$profile.ReadOnly=$true;Invoke-NasOperation $data | Out-Null
    Check ($script:lastShare.ReadAccess -and -not $script:lastShare.ChangeAccess) 'Read-only share grants writes'
    $data.Operation='Remove';Invoke-NasOperation $data | Out-Null
    $restore=Get-Acl -LiteralPath $fixture;$restore.SetSecurityDescriptorSddlForm($originalAcl);Set-Acl -LiteralPath $fixture -AclObject $restore
    # Windows can canonicalize inherited descriptor flags during Set-Acl.
    # Rollback must restore the exact descriptor immediately before this operation.
    $rollbackAcl=(Get-Acl -LiteralPath $fixture).Sddl
    $script:account=$null;$profile.CreateUser=$true;$profile.ReadOnly=$false;$data.Operation='Create';$script:failFirewall=$true
    Reject {Invoke-NasOperation $data} 'Expected firewall failure'
    Check ($script:shares.Count -eq 0 -and $script:rules.Count -eq 0 -and $null -eq $script:account) 'Creation rollback leaked resources'
    $actualAcl=(Get-Acl -LiteralPath $fixture).Sddl
    Check ($actualAcl -eq $rollbackAcl) ("Creation rollback did not restore ACL. Expected: "+$rollbackAcl+" Actual: "+$actualAcl)
    $script:shares.FixtureNas=[pscustomobject]@{Description='OtherTool';Path=$fixture};$data.Operation='Remove'
    Reject {Invoke-NasOperation $data} 'Unrelated share removed';Check ($script:shares.Count -eq 1) 'Ownership guard failed'
    Write-Output 'NAS admin workflow: share permissions/encryption, Tailscale-only firewall, account/ACL rollback, open-file and ownership guards passed (network/account cmdlets mocked; fixture ACL real).'
} finally { $restore=Get-Acl -LiteralPath $fixture;$restore.SetSecurityDescriptorSddlForm($originalAcl);Set-Acl -LiteralPath $fixture -AclObject $restore }
