[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $GameDir,
    [switch] $SkipInstall
)

$ErrorActionPreference = 'Stop'
$GameDir = [System.IO.Path]::GetFullPath($GameDir)
$ProjectDir = $PSScriptRoot
$DataDir = Join-Path $GameDir 'data_GamesByMalcs.Melvor2_windows_x86_64'
$Project = Join-Path $ProjectDir 'Melvor2TimeSkip.csproj'

foreach ($required in @(
    (Join-Path $DataDir 'GamesByMalcs.Melvor2.dll'),
    (Join-Path $DataDir 'GodotSharp.dll'),
    (Join-Path $GameDir 'modloader\MelvorModLoader.dll'),
    (Join-Path $GameDir 'modloader\0Harmony.dll')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing file: $required`nInstall/start the mod loader once, and check -GameDir."
    }
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match '(?m)^10\.')) {
    throw 'A .NET 10 SDK is required: https://dotnet.microsoft.com/download/dotnet/10.0'
}

$OutDir = Join-Path $ProjectDir 'dist\build'
New-Item -Path $OutDir -ItemType Directory -Force | Out-Null
Write-Host "Building against game files in: $GameDir"
& dotnet build $Project -c Release "-p:GameDir=$GameDir" "-p:OutputPath=$OutDir" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed. See compiler errors above.' }

$Manifest = Join-Path $ProjectDir 'mod.json'
$Dll = Join-Path $OutDir 'Melvor2TimeSkip.dll'
if (-not (Test-Path -LiteralPath $Dll -PathType Leaf)) {
    throw "Build did not produce $Dll"
}

$Package = Join-Path $ProjectDir 'dist\package\Melvor2TimeSkip'
New-Item -Path $Package -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $Manifest -Destination (Join-Path $Package 'mod.json') -Force
Copy-Item -LiteralPath $Dll -Destination (Join-Path $Package 'Melvor2TimeSkip.dll') -Force

if (-not $SkipInstall) {
    $ModDir = Join-Path $GameDir 'mods\Melvor2TimeSkip'
    New-Item -Path $ModDir -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $Manifest -Destination (Join-Path $ModDir 'mod.json') -Force
    Copy-Item -LiteralPath $Dll -Destination (Join-Path $ModDir 'Melvor2TimeSkip.dll') -Force
    Write-Host "Installed into $ModDir"
}

$Zip = Join-Path $ProjectDir 'dist\Melvor2TimeSkip_Install_v0.1.0.zip'
if (Test-Path -LiteralPath $Zip) { Remove-Item -LiteralPath $Zip -Force }
Compress-Archive -Path $Package -DestinationPath $Zip
Write-Host "Installable mod ZIP: $Zip"
Write-Host 'Start Melvor2Modded.exe and open any character to see the Time Skip button.'
