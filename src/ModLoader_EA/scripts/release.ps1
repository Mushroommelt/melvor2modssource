<#
.SYNOPSIS
    Publishes a GitHub release: builds dist\Melvor2Modded.exe, tags the current commit v<version>, pushes the tag and
    creates the release with the exe attached. Needs the GitHub CLI (gh), logged in. Commit and push first.
    Bump <ModLoaderVersion> in Directory.Build.props before releasing a new version.
.PARAMETER NotesFile
    Markdown file with the release notes. Without it, GitHub lists the commits since the previous release.
#>
param(
    [string]$NotesFile,
    [switch]$Draft
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
    if (git status --porcelain) { throw 'The working tree has uncommitted changes; commit them first.' }

    $output = & (Join-Path $PSScriptRoot 'package.ps1')
    $version = ($output | Select-Object -Last 1) -replace '^Mod Loader version ', ''
    $tag = "v$version"
    if ((gh release list --json tagName --jq ".[].tagName") -contains $tag) { throw "Release $tag already exists; bump ModLoaderVersion first." }

    git push origin HEAD
    if (-not (git tag --list $tag)) { git tag -a $tag -m "Mod Loader $version" }
    git push origin $tag
    if ($LASTEXITCODE -ne 0) { throw 'Pushing the tag failed' }

    $ghArgs = @('release', 'create', $tag, (Join-Path $root 'dist\Melvor2Modded.exe'), '--title', "Mod Loader $version")
    if ($NotesFile) { $ghArgs += @('--notes-file', $NotesFile) } else { $ghArgs += '--generate-notes' }
    if ($Draft) { $ghArgs += '--draft' }
    gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw 'Creating the release failed' }
}
finally {
    Pop-Location
}
