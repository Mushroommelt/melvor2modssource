<#
.SYNOPSIS
    Decompiles the game assembly (default: the Steam install) into a folder for browsing/grepping, by default
    decompiled\ in this repository (ignored by git).
    Requires: dotnet tool install -g ilspycmd --version 9.1.0.7988
#>
param(
    [string]$GameDir,
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
if (-not $GameDir) { $GameDir = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500').InstallLocation }
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot '..\decompiled' }
$dataDir = Join-Path (Resolve-Path $GameDir) 'data_GamesByMalcs.Melvor2_windows_x86_64'

if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
New-Item -ItemType Directory -Force $OutDir | Out-Null
ilspycmd -p -o $OutDir -r $dataDir (Join-Path $dataDir 'GamesByMalcs.Melvor2.dll')
