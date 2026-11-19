# Reverts launcher, loader assemblies, and three mod folders from an installer backup.
# Run only with the game closed. Character saves are deliberately untouched.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BackupDir,
    [Parameter(Mandatory=$true)][string]$GameDir
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$BackupDir = (Resolve-Path -LiteralPath $BackupDir).Path
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
if (-not (Test-Path -LiteralPath (Join-Path $GameDir 'Melvor2.exe') -PathType Leaf)) {
    throw 'GameDir must contain Melvor2.exe.'
}
if (@(Get-Process -Name Melvor2,Melvor2Modded -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close Melvor Idle 2 before restoring.'
}
$inventoryFile = Join-Path $BackupDir 'INSTALL_BACKUP_STATE.json'
if (Test-Path -LiteralPath $inventoryFile -PathType Leaf) {
    # v0.2.4+ inventories distinguish files absent before installation
    # from files present before installation. These are the only valid targets.
    $allowed = @(
        'Melvor2Modded.exe',
        'modloader\MelvorModLoader.dll',
        'modloader\0Harmony.dll',
        'modloader\MelvorModLoader.xml',
        'mods\Multitasking',
        'mods\Melvor2RecipeQueue',
        'mods\Melvor2TimeSkip'
    )
    $entries = @(Get-Content -LiteralPath $inventoryFile -Raw | ConvertFrom-Json)
    if ($entries.Count -ne $allowed.Count) { throw 'Backup inventory is missing protected entries; refusing to restore.' }
    $seen = @{}
    foreach ($entry in $entries) {
        $target = [string]$entry.Target
        $source = Join-Path $BackupDir ([string]$entry.Backup)
        if ($target -notin $allowed -or $seen.ContainsKey($target)) {
            throw "Invalid or duplicate backup entry: $target"
        }
        $seen[$target] = $true
        if ([bool]$entry.Existed -and -not (Test-Path -LiteralPath $source)) {
            throw "Original backup item is missing; refusing to alter installation: $source"
        }
    }
    foreach ($entry in $entries) {
        $destination = Join-Path $GameDir ([string]$entry.Target)
        $source = Join-Path $BackupDir ([string]$entry.Backup)
        if (Test-Path -LiteralPath $destination) {
            Remove-Item -LiteralPath $destination -Recurse -Force
        }
        if ([bool]$entry.Existed) {
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Recurse -Force
        }
    }
    Write-Host "Restored launcher, loader components, and mod folders to pre-install state from: $BackupDir" -ForegroundColor Green
} else {
    # Legacy v0.2.1-v0.2.3 backups did not record missing files.
    Write-Warning 'Legacy backup without file inventory. Original components will be restored where backed up.'
    $exe = Join-Path $GameDir 'Melvor2Modded.exe'
    $old = Join-Path $BackupDir 'Melvor2Modded.exe'
    if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $exe -Force }
    elseif (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe -Force }
    foreach ($file in @('MelvorModLoader.dll','0Harmony.dll','MelvorModLoader.xml')) {
        $old = Join-Path $BackupDir $file
        $dest = Join-Path $GameDir "modloader\$file"
        if (Test-Path -LiteralPath $old) { Copy-Item -LiteralPath $old -Destination $dest -Force }
    }
    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        $oldDir = Join-Path $BackupDir $id
        $dest = Join-Path $GameDir "mods\$id"
        if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Force -Recurse }
        if (Test-Path -LiteralPath $oldDir) { Copy-Item -LiteralPath $oldDir -Destination $dest -Recurse }
    }
    Write-Host "Restored available loader and mod backups from: $BackupDir" -ForegroundColor Green
}
Write-Host 'Character saves were not touched. Any SaveBackup.zip remains in the backup directory.'
