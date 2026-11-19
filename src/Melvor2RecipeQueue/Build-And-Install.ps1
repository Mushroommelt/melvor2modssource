# Run from PowerShell: .\Build-And-Install.ps1 -GameDir 'C:\...\Melvor Idle 2 Playtest'
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 10 SDK not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0'
}
$GameDir = (Resolve-Path -LiteralPath $GameDir -ErrorAction Stop).Path
$mustExist = @(
    (Join-Path $GameDir 'Melvor2.exe'),
    (Join-Path $GameDir 'data_GamesByMalcs.Melvor2_windows_x86_64\GamesByMalcs.Melvor2.dll'),
    (Join-Path $GameDir 'modloader\MelvorModLoader.dll'),
    (Join-Path $GameDir 'modloader\0Harmony.dll')
)
foreach ($path in $mustExist) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file missing: $path. Start Melvor2Modded.exe once to initialize the loader."
    }
}
Push-Location $PSScriptRoot
try {
    & dotnet build '.\RecipeQueue.csproj' -c Release "-p:GameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE). Nothing was installed." }
    Write-Host "Installed to: $(Join-Path $GameDir 'mods\Melvor2RecipeQueue')"
    Write-Host 'Launch Melvor2Modded.exe; navigate to a crafting recipe and expand Manage Stop Conditions.'
}
finally { Pop-Location }
