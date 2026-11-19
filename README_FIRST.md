# Melvor Idle 2 — Early Access UI Refresh Pack v0.2.5.1

## Install on your PC

1. Export/back up your current save; close the game.
2. Extract the whole ZIP to a new folder **outside the Steam game folder**.
3. Double-click `Install-All.cmd`. It reuses your remembered game folder and still supports pasting a path or browsing to `Melvor2.exe`.
4. The script checks the exact game assembly, requires **.NET 10 SDK**, compiles the custom loader and both editable mods, then makes a timestamped backup of the working v0.2.4 components and your local saves.
5. Only after all builds succeed does it install files, refresh the modded game copy, and verify hashes.
6. Launch your original game folder's `Melvor2Modded.exe`.

To bypass the folder picker:

```powershell
powershell.exe -STA -NoProfile -ExecutionPolicy Bypass -File .\Install-All.ps1 -GameDir 'A:\Steam\steamapps\common\Melvor Idle 2'
```

The installer **never** writes into the original `Melvor2.exe` or original game DLL. It keeps separate backups under `%LOCALAPPDATA%\Melvor2EA_ModPack\Backups` and can roll back failed deployments. `Restore-Backup.ps1` can restore a selected old backup. The source code and audit scripts are included.
