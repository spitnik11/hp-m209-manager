$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'src\Manager\MainForm.cs') -Raw
foreach ($required in @('Connect / reconnect','Print test page','Open printer settings','Refresh status','Key Content','RedirectStandardInput')) {
    if (-not $source.Contains($required)) { throw "Missing required behavior: $required" }
}
if ($source -match 'File\.WriteAllText\([^\r\n]*key') { throw 'Possible Wi-Fi key persistence detected.' }
Write-Host 'Source checks passed.' -ForegroundColor Green
