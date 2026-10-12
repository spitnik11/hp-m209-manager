$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist'
if (-not (Test-Path (Join-Path $source 'HP M209 Manager.exe'))) { throw 'Build the application first with Build.ps1.' }
$destination = Join-Path $env:LOCALAPPDATA 'Programs\HP M209 Manager'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item (Join-Path $source '*') $destination -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'HP M209 Manager.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $destination 'HP M209 Manager.exe'
$shortcut.WorkingDirectory = $destination
$shortcut.Description = 'Manage, connect, and test the HP LaserJet M209dw'
$shortcut.Save()
Write-Host "Installed: $destination" -ForegroundColor Green
Write-Host "Desktop shortcut: $shortcutPath" -ForegroundColor Green
