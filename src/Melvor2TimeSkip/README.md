> THIS IS THE EARLY ACCESS COMPATIBILITY PACK COPY (0.2.0-ea). Build/install via the top-level Install-All.cmd.

# Melvor Idle 2 - Time Skip v0.1.0 (Early Access source)

Time Skip adds a **Time Skip** button at the lower-right of the gameplay viewport. It uses the game's native `Game.RequestOfflineHours(double)` -> offline-progress tick replay to simulate a requested amount of time. It does **not** change the Windows clock, save timestamps, or Godot's engine tick rate.

This is a **source project**, not an install-ready mod. The package deliberately contains no third-party game DLLs, loader DLLs, or compiled mod DLL. Build it against your own installed Early Access game using .NET 10.

## Requirements

- Windows build of Melvor Idle 2 Early Access with the original `data_GamesByMalcs.Melvor2_windows_x86_64` folder.
- `Melvor2Modded.exe` mod loader 0.2.0 initialized in that game directory. Confirm these files exist:
  - `<GameDir>\modloader\MelvorModLoader.dll`
  - `<GameDir>\modloader\0Harmony.dll`
- .NET **10 SDK** (`dotnet --list-sdks` must include a version starting with `10.`).
- **Make a manual backup of your save first**, ideally including a copied save-string backup from the character selection screen. The upstream loader also recommends backing up `%APPDATA%\Games By Malcs\Melvor Idle 2`.

## Build and install

1. Extract this ZIP anywhere **outside** the game's `modloader\game` folder.
2. In Steam, right-click the Early Access game -> Manage -> Browse local files. Copy the full directory path (containing `Melvor2.exe` and `Melvor2Modded.exe`).
3. Open PowerShell in this extracted source directory and execute:

   ```powershell
   .\Build-And-Install.ps1 -GameDir 'C:\path\to\Melvor Idle 2'
   ```

   The script validates the .NET SDK and required game/loader DLLs, builds the C# library, creates `dist\Melvor2TimeSkip_Install_v0.1.0.zip`, and copies `mod.json` + `Melvor2TimeSkip.dll` into `<GameDir>\mods\Melvor2TimeSkip`.
4. Start **`Melvor2Modded.exe`** from the *original game folder*, not from `modloader\game`.
5. Load a character. A **Time Skip** button should appear near the bottom-right. Choose a preset or custom number of minutes, then click **Simulate selected time**. Times beyond 4 hours need one additional confirmation.

If you'd rather build a ZIP without installing directly, use `-SkipInstall`.

## Expected installation layout

```text
Melvor Idle 2/
  Melvor2.exe                  <- normal game
  Melvor2Modded.exe            <- run to load mods
  data_GamesByMalcs.Melvor2_windows_x86_64/
    GamesByMalcs.Melvor2.dll   <- original game assembly
  mods/
    Multitasking/
      mod.json
      MultitaskingMod.dll
    Melvor2RecipeQueue/
      mod.json
      Melvor2RecipeQueue.dll
    Melvor2TimeSkip/
      mod.json
      Melvor2TimeSkip.dll
  modloader/
    MelvorModLoader.dll
    0Harmony.dll
    game/                      <- auto-generated hard-linked copy
      data_GamesByMalcs.Melvor2_windows_x86_64/
        GamesByMalcs.Melvor2.dll <- patched COPY
```

The loader regenerates `modloader\game` on game changes. **Never install mods there.** Its source loads the mod manifests from `<GameDir>\mods\*/mod.json`.

## Behavior and limits

- Presets: 1, 5, 15, 30, 60 and 240 minutes; custom: 1-1440 minutes.
- The time request is clamped to the game's current `OfflineProgress.Component.MaxOfflineTime` and a conservative mod ceiling of 24 hours per request.
- An active save is required. Repeated requests are disabled while the previous simulation is running.
- The game's own offline-simulation UI, tick processing and summary remain in charge. A lengthy simulation **may take time in real life**; it cannot be cancelled by this mod after submission.
- Resources and recipes behave under normal game tick rules. The simulation continues even if an individual activity runs out of resources.
- A manual offline request does not set `Game.Flags.IsOffline` in the Early Access build examined. This mod patches `Main.GameState.RunOfflineProgress(double,bool)` to call `Game.OnOfflineProgressStart()` for **its own** requests. That prevents mods which rely on `IsOffline` from attempting normal UI operations during the background tick simulation. The game's existing `Game.OnOfflineProgressEnd(double)` resets the flag.
- No persistent settings are stored.

## Troubleshooting

- **No mod name in main menu**: confirm that the compiled DLL and `mod.json` are under `<GameDir>\mods\Melvor2TimeSkip` and run `Melvor2Modded.exe` (not the vanilla game EXE).
- **No Time Skip button**: load a character and examine `%APPDATA%\Games By Malcs\Melvor Idle 2\logs` for `[Melvor2TimeSkip]` or `[ModLoader]` errors.
- **Time Skip disabled**: the runtime patch failed; likely the game or loader API changed. Do not bypass this guard.
- **Compile error**: share the full PowerShell build output. The game and loader DLLs must be the exact versions installed on the same PC as the game.
- **Behavior differs with Recipe Queue/Multitasking**: test Time Skip by itself first, then add each other mod one at a time and compare logs.
- To remove it, delete the **`<GameDir>\mods\Melvor2TimeSkip`** folder.

## Verification status

Verified by examining the **user-supplied 17 November 2026 Early Access game assembly** and the loader 0.2.0 source:

- `Game.RequestOfflineHours(double)` emits `OfflineRequested` with hours converted to seconds.
- `Main.GameState.GameOfflineRequestedHandler` converts seconds to milliseconds and calls `RunOfflineProgress(double,bool)`.
- `RunOfflineProgress` initializes the offline progress counter, records a snapshot and launches background `RunOfflineTicks`.
- `Game.OnOfflineProgressStart` sets `Game.Flags.IsOffline = true`; `OnOfflineProgressEnd(double)` resets it and displays the summary.
- `GameView._Ready`, `Bind(Game)` and `Unbind()` exist in Early Access.
- The launcher 0.2.0 injection anchor `GodotPlugins.Game.Main.InitializeFromGameProject` -> `LookupScriptsInAssembly` still exists in the new game assembly.
- Godot API symbols for the controls/window were cross-checked against the supplied `GodotSharp.dll`.

**Not yet verified:** compilation with the Windows .NET 10 SDK; load-time Harmony patch success; appearance of the button; correctness of rewards/side effects across every skill; compatibility with Multitasking and Recipe Queue; safe behavior under game crashes or power failure. This environment has no .NET compiler or Windows game runtime. This is a first testing candidate, not a proven release.
