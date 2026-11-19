<#
.SYNOPSIS
    Dev uninstall: removes Melvor2Modded.exe and the modloader folder from the game (default: the Steam install).
    The game's mods folder is left alone.
#>
param(
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'
if (-not $GameDir) { $GameDir = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500').InstallLocation }
$GameDir = (Resolve-Path $GameDir).Path
$uninstaller = Join-Path $GameDir 'modloader\uninstall.exe'
if (-not (Test-Path $uninstaller)) { Write-Host 'The mod loader is not installed.'; return }

& $uninstaller --quiet | Out-Default
if ($LASTEXITCODE -ne 0) { throw 'Uninstall failed' }
# uninstall.exe removes itself, the launcher and the modloader folder shortly after it exits.
$deadline = (Get-Date).AddSeconds(15)
while ((Test-Path (Join-Path $GameDir 'modloader')) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
if (Test-Path (Join-Path $GameDir 'modloader')) { throw 'The modloader folder was not removed.' }
Write-Host "Removed the mod loader from $GameDir"
