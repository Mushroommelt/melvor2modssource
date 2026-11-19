# Provenance and versions

- `src/ModLoader_EA` derives from the MIT-licensed source code in the user-supplied `melvor2-modloader-0.2.0.zip`; attribution and license remain bundled. Our fork includes loader hardening for the 17 Nov 2026 Early Access game, version 0.2.1 custom.
- `bin/original-compatible/` contains the original, user-supplied compiled `Melvor2Modded.exe`, `MelvorModLoader.dll`, `0Harmony.dll`, and XML documentation. These are retained for an emergency build fallback.
- `mods/Multitasking` contains the user's **unchanged** v0.3.2 DLL and manifest from `Multitasking.zip`. It is not reverse-engineered into a replacement binary or misrepresented as newly compiled.
- `src/Melvor2RecipeQueue` and `src/Melvor2TimeSkip` derive from source prototypes created earlier in this conversation, with Early Access version labels and compatibility checks. No compile or runtime validation has been performed by the assistant.
- Early Access game assembly analysis was done on the supplied `data_GamesByMalcs.Melvor2_windows_x86_64(1).zip` from Nov 17, 2026. The game assembly isn't redistributed in this package.
- The original projects' current GitHub upstream releases (checked Nov 17, 2026) are [loader 0.2.0](https://github.com/nachtmarv/melvor2-modloader/releases/tag/v0.2.0) and [Multitasking 0.3.2](https://github.com/nachtmarv/melvor2-multitasking/releases/tag/v0.3.2). Custom builds in this bundle are not upstream releases.

## 0.2.2 package hotfix

After a Windows build reported `CS0122` for `BaseActionComponent.GameFlags`, Recipe Queue source was updated to 0.2.1-ea. The UI stop callback now checks the bound Godot thread ID without accessing protected game members. Custom loader fork (0.2.1), original Multitasking binary (0.3.2), and Time Skip source (0.2.0-ea) are unchanged. This revised package is not yet C# compiled or game-tested.

## 0.2.3 package hotfix

The second user-provided Windows compilation log revealed two CS0104 ambiguous-`Environment` references and one CS1501 invocation mismatch in Recipe Queue. Both are fixed by qualifying `System.Environment` and removing the stale argument. The installer now offers a Windows Forms textbox for pasting game-folder paths plus the standard OpenFileDialog with address bar; the successful selection is cached in `%LOCALAPPDATA%\Melvor2EA_ModPack\SelectedGameFolder.txt`. No game executable or assembly is included or changed by these source edits. All three mod behaviors remain untested in-game.

Early Access metadata follow-up: `GameView.get_Game()` is private; `GameView.Bind(Game)` is public. Time Skip v0.2.1-ea uses the public bind parameter rather than the inaccessible getter.

## v0.2.4 installer hotfix (17 Nov 2026)

The user-reported v0.2.3 Windows build **successfully compiled all four components** (loader, launcher, Recipe Queue, Time Skip) against the same expected Early Access game assembly SHA-256. Installation then failed at the missing `modloader\MelvorModLoader.dll` check. The failure was *after* copying the launcher and three mod folders and may have left those files partially deployed.

v0.2.4 does not alter C# source or the original Multitasking binary. It fixes installer sequencing by explicitly deploying both compiled loader dependencies, synchronously waiting for the GUI launcher prelaunch process, verifying installed files with SHA-256, maintaining a preinstall existence inventory for automatic rollback, and caching the selected game folder before the build. No runtime gameplay test has been performed; the updated PowerShell installer has been statically checked, not executed on Windows.

## v0.2.5 UI refresh (17 November 2026)

Based directly on the user-reported working v0.2.4 install. Upstream Multitasking v0.3.2 DLL and all gameplay code remain unchanged. ModLoader adds `MultitaskingActivityHud.cs` only when an id `Multitasking` loads; the HUD reads public `Game.ActionManager.ActiveActions`, `ActionComponent.CurrentDescription`, and `GameEntity.Icon`. RecipeQueue and TimeSkip only replace their Godot UI source and add duplicated style helper `ModChrome.cs` (independent of custom loader, so the fallback loader remains usable). The custom loader fork version increments to 0.2.5. The upstream authors are not responsible for these changes. The C# has been statically inspected against the supplied game/Godot DLLs but not built or runtime tested in this environment.

## v0.2.5.1 CS0104 hotfix (17 November 2026)

Windows build of v0.2.5 stopped at `MultitaskingActivityHud.cs(134,25)`: the unqualified `new Timer` was ambiguous due to .NET implicit usings. This fix uses `new Godot.Timer` in that file and proactively applies the identical fix to Time Skip's new quick-bar timer. Loader version is 0.2.5.1, and Time Skip manifest marks 0.2.5.1-ea. The original Multitasking DLL, queue controller, Time Skip runtime, launcher, installer, and rollback implementation remain untouched. Build and gameplay validation are still pending on Windows.
