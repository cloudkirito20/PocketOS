param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string]$ManagerVersion='3.2.0',[string]$BridgeVersion='3.0.0',[string]$Esp32Version='2.9.0')
$ErrorActionPreference='Stop'
$components=@{}
$zip=Join-Path $ReleaseDirectory 'PocketOS_Update.zip'
if (!(Test-Path $zip)) { throw 'Windows update ZIP missing' }
$components['windows']=@{asset='PocketOS_Update.zip';sha256=(Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()}
$bin=Join-Path $ReleaseDirectory 'PocketOS_ESP32.bin'
if (Test-Path $bin) {$components['esp32']=@{asset='PocketOS_ESP32.bin';sha256=(Get-FileHash -Algorithm SHA256 $bin).Hash.ToLowerInvariant()}}
@{managerVersion=$ManagerVersion;bridgeVersion=$BridgeVersion;esp32Version=$Esp32Version;components=$components} | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $ReleaseDirectory 'pocketos-manifest.json') -Encoding UTF8
Write-Host 'GitHub manifest created. Publish the ZIP, optional BIN, and manifest as release assets.'
