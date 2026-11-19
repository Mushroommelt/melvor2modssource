# Collect relevant log files for a troubleshooting upload after testing.
[CmdletBinding()]
param([string]$OutputDir = [IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Melvor2_Mod_Test_Logs'))
$ErrorActionPreference = 'Stop'
$source = Join-Path $env:APPDATA 'Games By Malcs\Melvor Idle 2\logs'
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "No game log directory at $source" }
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Get-ChildItem -LiteralPath $source -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 5 | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $OutputDir $_.Name) -Force
}
Write-Host "Logs copied to $OutputDir. Review them for personal data before sharing."
