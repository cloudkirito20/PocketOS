param([Parameter(Mandatory=$true)][string]$Archive,[Parameter(Mandatory=$true)][string]$InstallDirectory,[int]$WaitPid=0)
$ErrorActionPreference='Stop'
try {
  $Archive=[IO.Path]::GetFullPath($Archive)
  $InstallDirectory=[IO.Path]::GetFullPath($InstallDirectory)
  if (!(Test-Path -LiteralPath $Archive -PathType Leaf)) { throw 'Update ZIP not found' }
  $temp=Join-Path ([IO.Path]::GetTempPath()) ('PocketOSUpdate_'+[guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $temp -Force | Out-Null
  try {
    Expand-Archive -LiteralPath $Archive -DestinationPath $temp -Force
    if (!(Test-Path -LiteralPath (Join-Path $temp 'PocketOSManager.exe'))) { throw 'Invalid update: PocketOSManager.exe missing at ZIP root' }
    if (!(Test-Path -LiteralPath (Join-Path $temp 'PokemonBridge.exe'))) { throw 'Invalid update: PokemonBridge.exe missing at ZIP root' }
    if ($WaitPid -gt 0) { try { Wait-Process -Id $WaitPid -Timeout 45 -ErrorAction Stop } catch { throw 'Manager did not close; update cancelled' } }
    Get-Process -Name PokemonBridge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 450
    New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
    foreach ($name in @('PocketOSManager.exe','PokemonBridge.exe','Install_Update.ps1')) {
      $src=Join-Path $temp $name
      if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $InstallDirectory $name) -Force }
    }
    Start-Process -FilePath (Join-Path $InstallDirectory 'PocketOSManager.exe') -WorkingDirectory $InstallDirectory
  } finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
} catch {
  Add-Type -AssemblyName System.Windows.Forms
  [Windows.Forms.MessageBox]::Show($_.Exception.Message,'PocketOS Update Failed') | Out-Null
  exit 1
}
