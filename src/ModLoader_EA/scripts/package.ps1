<#
.SYNOPSIS
    Builds dist\Melvor2Modded.exe: self-contained (no .NET needed), loader embedded. This file is the release
    download; players put it into the game folder. Prints the version.
#>
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'obj\launcher'
New-Item -ItemType Directory -Force $dist | Out-Null
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

dotnet publish (Join-Path $root 'src\Launcher\Launcher.csproj') -c Release -r win-x64 --self-contained true -o $publish --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Launcher publish failed' }
$launcher = Join-Path $publish 'Melvor2Modded.exe'
$version = (Get-Item $launcher).VersionInfo.ProductVersion -replace '\+.*', ''
Copy-Item $launcher (Join-Path $dist 'Melvor2Modded.exe') -Force

Get-ChildItem $dist -File | ForEach-Object { '{0,-40} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) } | Out-Host
"Mod Loader version $version"
