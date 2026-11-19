# Melvor Idle 2 - Early Access compatibility bundle
# Builds a dedicated loader fork and both source mods, then installs all three mods.
# No game assets or saves are altered. Run with Windows PowerShell and .NET 10 SDK.
[CmdletBinding()]
param(
    [string]$GameDir,
    [switch]$UseOriginalLoader,
    [switch]$AllowUnverifiedBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = $PSScriptRoot
$expectedGameSha256 = '0A5A9FFF320AAAFF781816DEE0EEE938D61D14982BA7F4523DD8CA31D95F27A9'
$expectedMultitaskingSha256 = 'A46FD0A514C37C424953833E14B4C3493241BB93F86FE2E1C3FAB5638394EBFC'

function Step([string]$msg) {
    Write-Host ("`n=== {0} ===" -f $msg) -ForegroundColor Cyan
}
function Require([string]$file) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing required file: $file" }
}
# Remember the user's chosen installation path across future extracted mod-pack ZIPs.
$rememberedGamePathFile = Join-Path $env:LOCALAPPDATA 'Melvor2EA_ModPack\SelectedGameFolder.txt'

function ResolveGameFolder([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $candidate = [System.Environment]::ExpandEnvironmentVariables($value.Trim().Trim('"').Trim("'"))
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        if ([System.IO.Path]::GetFileName($candidate) -ne 'Melvor2.exe') { return $null }
        $candidate = Split-Path -LiteralPath $candidate -Parent
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { return $null }
    $full = (Resolve-Path -LiteralPath $candidate).Path
    if (-not (Test-Path -LiteralPath (Join-Path $full 'Melvor2.exe') -PathType Leaf)) { return $null }
    return $full
}

function FindSuggestedGameFolder {
    if (Test-Path -LiteralPath $rememberedGamePathFile -PathType Leaf) {
        $remembered = ResolveGameFolder (Get-Content -LiteralPath $rememberedGamePathFile -Raw)
        if ($remembered) { return $remembered }
    }
    $registryPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500'
    )
    foreach ($key in $registryPaths) {
        $record = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($null -ne $record -and $record.InstallLocation) {
            $found = ResolveGameFolder $record.InstallLocation
            if ($found) { return $found }
        }
    }
    return ''
}

function SelectGameFolder {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $window = New-Object System.Windows.Forms.Form
    $window.Text = 'Melvor Idle 2 - Select game folder'
    $window.ClientSize = [System.Drawing.Size]::new(780, 188)
    $window.MinimumSize = [System.Drawing.Size]::new(670, 230)
    $window.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
    $window.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
    $window.MaximizeBox = $false
    $window.MinimizeBox = $false
    $window.AutoScaleMode = [System.Windows.Forms.AutoScaleMode]::Font

    $prompt = New-Object System.Windows.Forms.Label
    $prompt.Text = 'Paste the Steam game folder path, or the full path to Melvor2.exe:'
    $prompt.AutoSize = $true
    $prompt.Location = [System.Drawing.Point]::new(16, 15)
    $window.Controls.Add($prompt)

    $pathBox = New-Object System.Windows.Forms.TextBox
    $pathBox.Location = [System.Drawing.Point]::new(16, 42)
    $pathBox.Size = [System.Drawing.Size]::new(628, 25)
    $pathBox.Anchor = [System.Windows.Forms.AnchorStyles]('Top,Left,Right')
    $pathBox.Text = FindSuggestedGameFolder
    $window.Controls.Add($pathBox)

    $browse = New-Object System.Windows.Forms.Button
    $browse.Text = 'Browse...'
    $browse.Location = [System.Drawing.Point]::new(655, 40)
    $browse.Size = [System.Drawing.Size]::new(108, 29)
    $browse.Anchor = [System.Windows.Forms.AnchorStyles]('Top,Right')
    $window.Controls.Add($browse)
    $browse.Add_Click(({
        # OpenFileDialog provides the modern Windows Explorer address bar.
        # Unlike FolderBrowserDialog, Ctrl+L / Ctrl+V can jump directly to a path.
        $filePicker = New-Object System.Windows.Forms.OpenFileDialog
        try {
            $filePicker.Title = 'Choose Melvor2.exe (paste game path in the address bar)'
            $filePicker.Filter = 'Melvor2.exe|Melvor2.exe|All files (*.*)|*.*'
            $filePicker.FileName = 'Melvor2.exe'
            $filePicker.CheckFileExists = $true
            $filePicker.CheckPathExists = $true
            $current = ResolveGameFolder $pathBox.Text
            if ($current) { $filePicker.InitialDirectory = $current }
            if ($filePicker.ShowDialog($window) -eq [System.Windows.Forms.DialogResult]::OK) {
                $pathBox.Text = [System.IO.Path]::GetDirectoryName($filePicker.FileName)
            }
        } finally { $filePicker.Dispose() }
    }).GetNewClosure())

    $tip = New-Object System.Windows.Forms.Label
    $tip.Text = 'Tip: Paste with Ctrl+V. In Browse, press Ctrl+L to paste an Explorer address.'
    $tip.AutoSize = $true
    $tip.Location = [System.Drawing.Point]::new(16, 82)
    $window.Controls.Add($tip)

    $accept = New-Object System.Windows.Forms.Button
    $accept.Text = 'Use this folder'
    $accept.Location = [System.Drawing.Point]::new(498, 130)
    $accept.Size = [System.Drawing.Size]::new(145, 31)
    $accept.Anchor = [System.Windows.Forms.AnchorStyles]('Bottom,Right')
    $window.Controls.Add($accept)
    $window.AcceptButton = $accept
    $accept.Add_Click(({
        $validated = ResolveGameFolder $pathBox.Text
        if (-not $validated) {
            [void][System.Windows.Forms.MessageBox]::Show(
                $window,
                'This must be the folder containing Melvor2.exe. Paste the Steam game folder address, or browse to Melvor2.exe.',
                'Invalid game folder',
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Warning)
            $pathBox.Focus()
            return
        }
        $window.Tag = $validated
        $window.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $window.Close()
    }).GetNewClosure())

    $cancel = New-Object System.Windows.Forms.Button
    $cancel.Text = 'Cancel'
    $cancel.Location = [System.Drawing.Point]::new(655, 130)
    $cancel.Size = [System.Drawing.Size]::new(108, 31)
    $cancel.Anchor = [System.Windows.Forms.AnchorStyles]('Bottom,Right')
    $cancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
    $window.CancelButton = $cancel
    $window.Controls.Add($cancel)

    try {
        $result = $window.ShowDialog()
        if ($result -ne [System.Windows.Forms.DialogResult]::OK -or -not $window.Tag) {
            throw 'No game folder selected. Nothing was installed.'
        }
        return [string]$window.Tag
    } finally { $window.Dispose() }
}

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = SelectGameFolder
} else {
    $normalized = ResolveGameFolder $GameDir
    if (-not $normalized) { throw "Invalid -GameDir: $GameDir. Supply the folder containing Melvor2.exe." }
    $GameDir = $normalized
}
Write-Host "Selected game folder: $GameDir"
# Remember a valid game location even if a later build or install step fails.
# This avoids asking for the same Steam path on every troubleshooting retry.
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $rememberedGamePathFile) | Out-Null
Set-Content -LiteralPath $rememberedGamePathFile -Value $GameDir -Encoding UTF8
if ($root.StartsWith($GameDir.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Extract this mod pack OUTSIDE the game folder (for example, Downloads or Desktop), then run the installer. Nothing was installed.'
}
$gameAssembly = Join-Path $GameDir 'data_GamesByMalcs.Melvor2_windows_x86_64\GamesByMalcs.Melvor2.dll'
$godotAssembly = Join-Path $GameDir 'data_GamesByMalcs.Melvor2_windows_x86_64\GodotSharp.dll'
$gameExe = Join-Path $GameDir 'Melvor2.exe'
$launcherExe = Join-Path $GameDir 'Melvor2Modded.exe'
Require $gameExe
Require $gameAssembly
Require $godotAssembly
Require (Join-Path $root 'mods\Multitasking\MultitaskingMod.dll')

Step 'Validate Early Access game and mod versions'
$foundSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $gameAssembly).Hash.ToUpperInvariant()
Write-Host "Game assembly SHA-256: $foundSha256"
if ($foundSha256 -ne $expectedGameSha256 -and -not $AllowUnverifiedBuild) {
    throw "Game build does not match the 17 Nov 2026 Early Access assembly used for this audit. A new update may have changed APIs. Nothing was installed. To override at your own risk, run Install-All.ps1 -AllowUnverifiedBuild."
}
$multiSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root 'mods\Multitasking\MultitaskingMod.dll')).Hash.ToUpperInvariant()
if ($multiSha256 -ne $expectedMultitaskingSha256) { throw 'Multitasking DLL checksum mismatch; package may be damaged.' }
if (@(Get-Process -Name Melvor2,Melvor2Modded -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close both the normal and modded game before installing. Nothing was changed.'
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 10 SDK is required. Install https://dotnet.microsoft.com/download/dotnet/10.0 and rerun Install-All.cmd.'
}
$sdks = & dotnet --list-sdks 2>$null
if ($LASTEXITCODE -ne 0 -or -not (@($sdks | Where-Object { $_ -match '^10\.' }).Count -gt 0)) {
    throw 'The .NET 10 SDK is required to compile the Early Access mods and custom loader. Install https://dotnet.microsoft.com/download/dotnet/10.0 and rerun Install-All.cmd.'
}
Write-Host 'Detected .NET 10 SDK.'

# Stage all builds away from the game directory. Do not partially install a broken DLL.
$stage = Join-Path $env:TEMP ('Melvor2EA_ModPack_' + [Guid]::NewGuid().ToString('N').Substring(0,10))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$backup = Join-Path $env:LOCALAPPDATA ('Melvor2EA_ModPack\Backups\' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Force -Path $backup | Out-Null
$stagedMods = Join-Path $stage 'mods'
New-Item -ItemType Directory -Force -Path $stagedMods | Out-Null
# The previous installer could stop after copying files but before loader extraction.
# Track when deployment starts so any partial installation can be rolled back.
$deploymentStarted = $false

try {
    Step 'Build the Early Access loader'
    $loaderExe = $null
    $loaderDll = $null
    $harmonyDll = $null
    if ($UseOriginalLoader) {
        $loaderExe = Join-Path $root 'bin\original-compatible\Melvor2Modded.exe'
        $loaderDll = Join-Path $root 'bin\original-compatible\MelvorModLoader.dll'
        $harmonyDll = Join-Path $root 'bin\original-compatible\0Harmony.dll'
        Write-Warning 'Using upstream loader 0.2.0 binary (verified static API targets, not in-game tested).'
    } else {
        $loaderProject = Join-Path $root 'src\ModLoader_EA\src\Launcher\Launcher.csproj'
        $outLauncher = Join-Path $stage 'launcher'
        & dotnet publish $loaderProject -c Release -r win-x64 --self-contained true -o $outLauncher '-p:RestoreIgnoreFailedSources=false' "-p:GameDir=$GameDir" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Updated loader publish failed. Nothing was installed. If NuGet cannot be reached, run again with -UseOriginalLoader.' }
        $loaderExe = Join-Path $outLauncher 'Melvor2Modded.exe'
        $loaderBin = Join-Path $root 'src\ModLoader_EA\src\ModLoader\bin\Release\net10.0'
        $loaderDll = Join-Path $loaderBin 'MelvorModLoader.dll'
        $harmonyDll = Join-Path $loaderBin '0Harmony.dll'
    }
    Require $loaderExe
    Require $loaderDll
    Require $harmonyDll

    Step 'Prepare compatible compile references'
    $refs = Join-Path $stage 'references'
    $refsGame = Join-Path $refs 'data_GamesByMalcs.Melvor2_windows_x86_64'
    $refsLoader = Join-Path $refs 'modloader'
    New-Item -ItemType Directory -Force -Path $refsGame,$refsLoader | Out-Null
    Copy-Item -LiteralPath $gameAssembly -Destination (Join-Path $refsGame 'GamesByMalcs.Melvor2.dll')
    Copy-Item -LiteralPath $godotAssembly -Destination (Join-Path $refsGame 'GodotSharp.dll')
    Copy-Item -LiteralPath $loaderDll -Destination (Join-Path $refsLoader 'MelvorModLoader.dll')
    Copy-Item -LiteralPath $harmonyDll -Destination (Join-Path $refsLoader '0Harmony.dll')

    Step 'Compile Recipe Queue'
    $queueProject = Join-Path $root 'src\Melvor2RecipeQueue\RecipeQueue.csproj'
    & dotnet build $queueProject -c Release "-p:GameDir=$refs" '-p:SkipDeploy=true' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Recipe Queue compilation failed. Nothing was installed.' }
    $queueDll = Join-Path $root 'src\Melvor2RecipeQueue\bin\Release\net10.0\Melvor2RecipeQueue.dll'
    Require $queueDll

    Step 'Compile Time Skip'
    $timeProject = Join-Path $root 'src\Melvor2TimeSkip\Melvor2TimeSkip.csproj'
    & dotnet build $timeProject -c Release "-p:GameDir=$refs" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Time Skip compilation failed. Nothing was installed.' }
    $timeDll = Join-Path $root 'src\Melvor2TimeSkip\bin\Release\net10.0\Melvor2TimeSkip.dll'
    Require $timeDll

    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $stagedMods $id) | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $root 'mods\Multitasking\MultitaskingMod.dll') -Destination (Join-Path $stagedMods 'Multitasking\MultitaskingMod.dll')
    Copy-Item -LiteralPath (Join-Path $root 'mods\Multitasking\mod.json') -Destination (Join-Path $stagedMods 'Multitasking\mod.json')
    foreach ($pair in @(@('Melvor2RecipeQueue',$queueDll), @('Melvor2TimeSkip',$timeDll))) {
        $id = $pair[0]
        Copy-Item -LiteralPath $pair[1] -Destination (Join-Path (Join-Path $stagedMods $id) ([IO.Path]::GetFileName($pair[1])))
        Copy-Item -LiteralPath (Join-Path $root "src\$id\mod.json") -Destination (Join-Path $stagedMods "$id\mod.json")
    }
    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        $manifest = Get-Content -LiteralPath (Join-Path $stagedMods "$id\mod.json") -Raw | ConvertFrom-Json
        if ($manifest.id -ne $id) { throw "Invalid id for mod $id." }
        Require (Join-Path $stagedMods "$id\$($manifest.assembly)")
    }

    Step 'Back up existing loader and mod folders'
    if (Test-Path -LiteralPath $launcherExe) { Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $backup 'Melvor2Modded.exe') }
    foreach ($file in @('MelvorModLoader.dll','0Harmony.dll','MelvorModLoader.xml')) {
        $oldFile = Join-Path $GameDir ("modloader\" + $file)
        if (Test-Path -LiteralPath $oldFile) { Copy-Item -LiteralPath $oldFile -Destination (Join-Path $backup $file) }
    }
    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        $oldDir = Join-Path $GameDir "mods\$id"
        if (Test-Path -LiteralPath $oldDir) { Copy-Item -LiteralPath $oldDir -Destination (Join-Path $backup $id) -Recurse }
    }
    # Record which destinations existed BEFORE the new install. The restore script
    # uses this to remove newly introduced files and reinstate preexisting ones.
    $backupEntries = @(
        [pscustomobject]@{ Target='Melvor2Modded.exe'; Backup='Melvor2Modded.exe'; Kind='file'; Existed=(Test-Path -LiteralPath $launcherExe -PathType Leaf) },
        [pscustomobject]@{ Target='modloader\MelvorModLoader.dll'; Backup='MelvorModLoader.dll'; Kind='file'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'modloader\MelvorModLoader.dll') -PathType Leaf) },
        [pscustomobject]@{ Target='modloader\0Harmony.dll'; Backup='0Harmony.dll'; Kind='file'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'modloader\0Harmony.dll') -PathType Leaf) },
        [pscustomobject]@{ Target='modloader\MelvorModLoader.xml'; Backup='MelvorModLoader.xml'; Kind='file'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'modloader\MelvorModLoader.xml') -PathType Leaf) },
        [pscustomobject]@{ Target='mods\Multitasking'; Backup='Multitasking'; Kind='directory'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'mods\Multitasking') -PathType Container) },
        [pscustomobject]@{ Target='mods\Melvor2RecipeQueue'; Backup='Melvor2RecipeQueue'; Kind='directory'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'mods\Melvor2RecipeQueue') -PathType Container) },
        [pscustomobject]@{ Target='mods\Melvor2TimeSkip'; Backup='Melvor2TimeSkip'; Kind='directory'; Existed=(Test-Path -LiteralPath (Join-Path $GameDir 'mods\Melvor2TimeSkip') -PathType Container) }
    )
    $backupEntries | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup 'INSTALL_BACKUP_STATE.json') -Encoding UTF8
    Write-Host "Existing mod and loader backups: $backup"
    # Make a second, independent backup of local character saves if they exist.
    $saveFolder = Join-Path $env:APPDATA 'Games By Malcs\Melvor Idle 2'
    if (Test-Path -LiteralPath $saveFolder -PathType Container) {
        Write-Host 'Backing up local save folder to SaveBackup.zip ...'
        Compress-Archive -LiteralPath $saveFolder -DestinationPath (Join-Path $backup 'SaveBackup.zip') -Force
    } else {
        Write-Warning 'Local save folder not found; export/backup your character manually before gameplay testing.'
    }

    Step 'Install loader and all three mods'
    $deploymentStarted = $true
    # Copy loader DLLs directly; never depend on a GUI launcher process
    # finishing its embedded-resource extraction before PowerShell continues.
    $loaderInstallDir = Join-Path $GameDir 'modloader'
    New-Item -ItemType Directory -Force -Path $loaderInstallDir | Out-Null
    Copy-Item -LiteralPath $loaderDll -Destination (Join-Path $loaderInstallDir 'MelvorModLoader.dll') -Force
    Copy-Item -LiteralPath $harmonyDll -Destination (Join-Path $loaderInstallDir '0Harmony.dll') -Force
    $loaderXml = Join-Path (Split-Path -Parent $loaderDll) 'MelvorModLoader.xml'
    if (Test-Path -LiteralPath $loaderXml -PathType Leaf) {
        Copy-Item -LiteralPath $loaderXml -Destination (Join-Path $loaderInstallDir 'MelvorModLoader.xml') -Force
    }
    Require (Join-Path $loaderInstallDir 'MelvorModLoader.dll')
    Require (Join-Path $loaderInstallDir '0Harmony.dll')
    Copy-Item -LiteralPath $loaderExe -Destination $launcherExe -Force
    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        $target = Join-Path $GameDir "mods\$id"
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $stagedMods $id) -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.Name) -Force
        }
    }

    Step 'Initialize the modded game copy (wait for launcher to exit)'
    # Melvor2Modded.exe is a WinExe. Running it with PowerShell's call operator (&)
    # does NOT reliably wait for GUI process completion. Start-Process -Wait does.
    $prelaunchErrors = Join-Path $backup 'Prelaunch-Launcher-Errors.txt'
    $prelaunch = Start-Process -FilePath $launcherExe -ArgumentList @('--no-launch','--quiet') `
        -WorkingDirectory $GameDir -PassThru -Wait -RedirectStandardError $prelaunchErrors -ErrorAction Stop
    if ($prelaunch.ExitCode -ne 0) {
        if (Test-Path -LiteralPath $prelaunchErrors) {
            Get-Content -LiteralPath $prelaunchErrors | ForEach-Object { Write-Warning $_ }
        }
        throw "Loader prelaunch initialization failed (exit code $($prelaunch.ExitCode)). Backup: $backup"
    }

    Step 'Verify loader, patches and installed mods'
    Require (Join-Path $loaderInstallDir 'MelvorModLoader.dll')
    Require (Join-Path $loaderInstallDir '0Harmony.dll')
    foreach ($name in @('MelvorModLoader.dll','0Harmony.dll')) {
        $source = if ($name -eq 'MelvorModLoader.dll') { $loaderDll } else { $harmonyDll }
        $destination = Join-Path $loaderInstallDir $name
        $expected = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        if ($actual -ne $expected) { throw "Installed loader component differs from the compiled version: $destination" }
    }
    $patchedGameAssembly = Join-Path $GameDir 'modloader\game\data_GamesByMalcs.Melvor2_windows_x86_64\GamesByMalcs.Melvor2.dll'
    Require $patchedGameAssembly
    Require (Join-Path $GameDir 'modloader\game\Melvor2.exe')
    $moddedStateFile = Join-Path $GameDir 'modloader\game\modloader.state'
    Require $moddedStateFile
    $moddedState = (Get-Content -LiteralPath $moddedStateFile -Raw).Trim()
    if (-not $moddedState.StartsWith($foundSha256 + ' ', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The modded game copy does not match the selected Early Access assembly. Refusing to claim installation success.'
    }
    if ((Get-FileHash -LiteralPath $patchedGameAssembly -Algorithm SHA256).Hash.ToUpperInvariant() -eq $foundSha256) {
        throw 'The modded game copy has an unpatched assembly. Prelaunch initialization is incomplete.'
    }
    foreach ($id in @('Multitasking','Melvor2RecipeQueue','Melvor2TimeSkip')) {
        $manifestFile = Join-Path $GameDir "mods\$id\mod.json"
        Require $manifestFile
        $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
        if ($manifest.id -ne $id) { throw "Incorrect installed mod ID: $id" }
        $installedDll = Join-Path (Split-Path -Parent $manifestFile) $manifest.assembly
        $sourceDll = Join-Path (Join-Path $stagedMods $id) $manifest.assembly
        Require $installedDll
        if ((Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $installedDll -Algorithm SHA256).Hash) {
            throw "Installed mod assembly differs from compiled/staged version: $id"
        }
    }
    if ((Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash.ToUpperInvariant() -ne $foundSha256) {
        throw 'UNEXPECTED: the original game assembly changed! Do not launch until the original game is restored.'
    }
    Step 'Prelaunch installation complete'
    Write-Host 'Installed components:' -ForegroundColor Green
    Write-Host ('  Loader:      ' + $(if ($UseOriginalLoader) { 'upstream 0.2.0' } else { 'custom 0.2.5 EA (Activity HUD)' }))
    Write-Host ('  Multitasking:   0.3.2 original DLL' + $(if ($UseOriginalLoader) { ' (HUD unavailable with original loader)' } else { ' + Activity HUD' }))
    Write-Host '  Recipe Queue:   0.2.5-ea (original mechanics, game-themed UI)'
    Write-Host '  Time Skip:      0.2.5-ea (new 1m/5m/15m/1h quick buttons)'
    Write-Host "Launch to test: $launcherExe" -ForegroundColor Green
    Write-Host "Backup directory: $backup"
    Write-Host 'In-game execution has NOT been tested by this script.'
    Write-Host 'The game path was remembered for future attempts and updates.'
} catch {
    $failure = $_
    if ($deploymentStarted) {
        Write-Warning "Installation failed after deployment started. Attempting automatic restore from: $backup"
        try {
            & (Join-Path $root 'Restore-Backup.ps1') -BackupDir $backup -GameDir $GameDir
            Write-Warning 'Pre-install launcher and mod files have been restored. Your saved games were not modified.'
        } catch {
            Write-Warning "Automatic restore failed: $_"
            Write-Warning "Backup remains available at: $backup"
        }
    }
    throw $failure
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
}
