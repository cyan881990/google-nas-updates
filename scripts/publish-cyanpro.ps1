param([string]$Version,[switch]$UpdateProduct,[switch]$Release)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($env:CYAN_DEV_TOKEN)){throw 'Set CYAN_DEV_TOKEN in the developer environment or CI secrets; never put it in source or the installer.'}
$projectRoot=Split-Path -Parent $PSScriptRoot
if(!$Version){$Version=[regex]::Match([IO.File]::ReadAllText((Join-Path $projectRoot 'src/Core.cs')),'const string Version = "([0-9.]+)"').Groups[1].Value}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
$headers=@{'X-Dev-Token'=$env:CYAN_DEV_TOKEN}
$url="https://github.com/cyan881990/google-nas-updates/releases/download/v$Version/CyanNasSetup.exe"
function Request($method,$path,$body){
 $json=$body|ConvertTo-Json -Depth 10 -Compress
 Invoke-RestMethod -Uri "https://cyanpro.app/api/v1/dev/$path" -Method $method -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($json))
}
if($UpdateProduct){
 $body=@{name='CYAN NAS';tagline='Biến ổ cứng thành NAS. Kết nối NAS và cloud như ổ đĩa Windows.';tagline_en='Share local disks as NAS. Mount NAS and cloud storage as Windows drives.';description_format='html';description=[IO.File]::ReadAllText((Join-Path $projectRoot 'design/cyanpro-description.html'));guide=[IO.File]::ReadAllText((Join-Path $projectRoot 'design/cyanpro-guide.md'));platforms=@('Windows');download_url=$url;downloads=@{windows=$url}}
 $result=Request 'PATCH' 'product' $body
 if(!$result.ok){throw 'CYANPRO rejected product update'}
 Write-Output 'CYANPRO product content updated.'
}
if($Release){
 # Publish only after GitHub has the complete, accessible installer and checksum.
 $null=Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing
 $null=Invoke-WebRequest -Uri "$url.sha256" -Method Head -UseBasicParsing
 $existing=Invoke-RestMethod 'https://cyanpro.app/api/v1/dev/releases' -Headers $headers
 $matched=@($existing.releases|Where-Object version -eq $Version)
 if($matched.Count){if($matched[0].download_url -ne $url){throw 'Existing CYANPRO version has a different URL; inspect before publishing.'};Write-Output 'CYANPRO version already registered with the expected link.';return}
 $notes=if(Test-Path (Join-Path $projectRoot 'CHANGELOG-latest.md')){[IO.File]::ReadAllText((Join-Path $projectRoot 'CHANGELOG-latest.md'))}else{"CYAN NAS $Version"}
 $result=Request 'POST' 'releases' @{version=$Version;notes=$notes;download_url=$url;downloads=@{windows=$url};is_mandatory=$false}
 if(!$result.ok){throw 'CYANPRO rejected release'}
 Write-Output "CYANPRO release $Version registered."
}
