$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$package = Get-AppxPackage -Name 'AD2F1837.HPPrinterControl' | Sort-Object Version -Descending | Select-Object -First 1
if (-not $package) { throw 'HP Smart is not installed.' }
$desktop = Join-Path $package.InstallLocation 'DesktopExtension'
$lib = Join-Path $root 'src\Connector\lib'
New-Item -ItemType Directory -Force $lib | Out-Null
Copy-Item (Join-Path $desktop '*.dll') $lib -Force

dotnet build (Join-Path $root 'src\Connector\HpBleProbe.csproj') -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet build (Join-Path $root 'src\Manager\HpPrinterManager.csproj') -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$dist = Join-Path $root 'dist'
$connector = Join-Path $dist 'connector'
New-Item -ItemType Directory -Force $connector | Out-Null
Copy-Item (Join-Path $root 'src\Manager\bin\Release\net48\HP M209 Manager.exe') $dist -Force
Copy-Item (Join-Path $root 'src\Connector\bin\Release\net48\*') $connector -Force
Write-Host "Built: $dist" -ForegroundColor Green
