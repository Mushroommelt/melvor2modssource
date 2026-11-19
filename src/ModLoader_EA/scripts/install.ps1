<#
.SYNOPSIS
    Dev install: publishes the launcher (framework-dependent, needs the .NET 10 runtime) as <game>\Melvor2Modded.exe
    and lets it set up <game>\modloader. The game defaults to the Steam install. The game files themselves are never
    modified. Start the modded game with <game>\Melvor2Modded.exe. Re-runnable.
#>
param(
    [string]$GameDir,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
if (-not $GameDir) { $GameDir = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500').InstallLocation }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$GameDir = (Resolve-Path $GameDir).Path
$out = Join-Path $root "src\Launcher\bin\$Configuration\publish-dev"

dotnet publish (Join-Path $root 'src\Launcher\Launcher.csproj') -c $Configuration -r win-x64 --self-contained false -p:GameDir="$GameDir" -o $out --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$launcher = Join-Path $GameDir 'Melvor2Modded.exe'
Copy-Item (Join-Path $out 'Melvor2Modded.exe') $launcher -Force
& $launcher --no-launch --quiet | Out-Default
if ($LASTEXITCODE -ne 0) { throw 'Mod loader setup failed' }
Write-Host "Installed the mod loader into $GameDir; start the game with $launcher"
