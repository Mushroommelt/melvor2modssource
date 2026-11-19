# Melvor Idle 2 Mod Loader

A mod loader for Melvor Idle 2 by Games By Malcs. Start the game with
**Melvor2Modded.exe** to play with mods; the game's own `Melvor2.exe` keeps starting the normal, unmodded game. Your
game files are never modified.

> [!IMPORTANT]
> **Only tested with the Melvor Idle 2 alpha** (Steam playtest, v0.11.5-alpha.3, Windows). Melvor Idle 2 is in early
> development; a game update can break the loader or individual mods at any time. Back up your saves
> (`%APPDATA%\Games By Malcs\Melvor Idle 2`) before playing with mods. This is an unofficial fan project, not
> affiliated with or supported by Games By Malcs.

> [!NOTE]
> **Made with [Claude Code](https://claude.com/claude-code).** The code and documentation in this repository were
> written with Anthropic's AI coding assistant, directed, reviewed and tested in-game by a human.

Mods for it: [Multitasking](https://github.com/nachtmarv/melvor2-multitasking) (train all skills at the same time).

## Install

1. Download **[Melvor2Modded.exe](https://github.com/nachtmarv/melvor2-modloader/releases/latest/download/Melvor2Modded.exe)**
   from the [latest release](https://github.com/nachtmarv/melvor2-modloader/releases/latest).
2. Put it into the game folder, next to `Melvor2.exe`. On Steam: right-click Melvor Idle 2 in your library >
   *Manage* > *Browse local files*.
3. Start Steam (if it isn't running yet), then start **Melvor2Modded.exe** (the purple icon).

   Windows may warn that the exe is from an unknown publisher (it isn't code-signed): click *More info* > *Run anyway*.
4. The first start creates a `mods` folder in the game folder. Put mods there, one folder per mod, and start
   Melvor2Modded.exe again.

The bottom left of the main menu shows the loader version and the loaded mods; hover a mod for its description.

```
Melvor Idle 2 Playtest/
  Melvor2.exe         <- the normal game
  Melvor2Modded.exe   <- the game with mods
  mods/
    Multitasking/
      mod.json
      MultitaskingMod.dll
  modloader/          <- created and managed by Melvor2Modded.exe
    uninstall.exe
```

To remove a mod, delete its folder.

### Steam

Melvor2Modded.exe starts the game as your Steam game (playtime, Steam Cloud saves) but Steam must already be running
and logged in: the game syncs your Steam Cloud saves when it starts, and right after Steam starts they aren't available
yet, so the game could overwrite your cloud saves with the ones on this PC. If Steam isn't running, the launcher
doesn't start the game and offers to start Steam; start the game again once Steam has finished starting.

Optionally, to start the modded game with Steam's *Play* button: right-click the game > *Properties* > *Launch
Options* and enter

```
"<game folder>\Melvor2Modded.exe" %command%
```

for example `"C:\Program Files (x86)\Steam\steamapps\common\Melvor Idle 2 Playtest\Melvor2Modded.exe" %command%`.
Remove the launch option to play without mods. (This way is less tested than starting the exe directly.)

### Updates

- **Game updates** need nothing from you: the launcher notices them and updates its copy of the game on the next start.
  A game update can still break a mod; check for a new version of the mod then.
- **Loader updates:** download the new Melvor2Modded.exe and replace the old one.

### Uninstall

Run `modloader\uninstall.exe` in the game folder (and remove the Steam launch option if you set one). It removes
Melvor2Modded.exe and the `modloader` folder. Your `mods` folder and your saves are kept.

### How it works

Melvor2Modded.exe builds a copy of the game in `modloader\game` out of hard links to the original files (they take no
extra disk space), with one difference: its `GamesByMalcs.Melvor2.dll` contains a small extra startup step that loads
the mod loader. It starts the game from there. On every start it checks whether the game was updated and refreshes the
copy if needed. Saves are shared with the normal game.

## Making your own mod

Melvor Idle 2 is a [Godot](https://godotengine.org/) game written in C#. A mod is a .NET class library plus a
`mod.json`; it changes the game with [Harmony](https://harmony.pardeike.net/) patches (code that runs before, after or
instead of a game method).

### What you need

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Melvor Idle 2 with the loader installed (start Melvor2Modded.exe once). Your mod compiles against the game's
  `GamesByMalcs.Melvor2.dll` and the loader's `modloader\MelvorModLoader.dll`.
- A .NET decompiler to read the game's code and find what to patch, e.g. [ILSpy](https://github.com/icsharpcode/ILSpy)
  (open `data_GamesByMalcs.Melvor2_windows_x86_64\GamesByMalcs.Melvor2.dll`).

### 1. Create the project

Create a folder `MyMod` with three files.

**MyMod.csproj**:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDependencyFile>false</GenerateDependencyFile>

    <!-- The game folder; defaults to the Steam install. Override with: dotnet build -p:GameDir="D:\Games\Melvor Idle 2" -->
    <GameDir Condition="'$(GameDir)' == ''">$(Registry:HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3794500@InstallLocation)</GameDir>
    <GameDataDir>$(GameDir)\data_GamesByMalcs.Melvor2_windows_x86_64</GameDataDir>
  </PropertyGroup>

  <!-- Compile-time only: the game and the mod loader provide these at runtime, so don't copy them. -->
  <ItemGroup>
    <Reference Include="GamesByMalcs.Melvor2" HintPath="$(GameDataDir)\GamesByMalcs.Melvor2.dll" Private="false" />
    <Reference Include="GodotSharp" HintPath="$(GameDataDir)\GodotSharp.dll" Private="false" />
    <Reference Include="MelvorModLoader" HintPath="$(GameDir)\modloader\MelvorModLoader.dll" Private="false" />
    <Reference Include="0Harmony" HintPath="$(GameDir)\modloader\0Harmony.dll" Private="false" />
  </ItemGroup>

  <ItemGroup>
    <None Include="mod.json" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>

  <!-- After every build, copy the mod into the game's mods folder. -->
  <Target Name="DeployMod" AfterTargets="Build">
    <Copy SourceFiles="$(OutDir)mod.json;$(OutDir)$(AssemblyName).dll" DestinationFolder="$(GameDir)\mods\$(AssemblyName)" />
  </Target>

</Project>
```

**mod.json**, the mod's description for the loader:

```json
{
  "id": "MyMod",
  "name": "My Mod",
  "version": "1.0.0",
  "author": "you",
  "description": "What it does (shown as a tooltip in the main menu).",
  "assembly": "MyMod.dll"
}
```

`id` must be unique (mods load in id order) and `assembly` is your DLL. The other fields are optional.

**MyMod.cs**:

```csharp
using GamesByMalcs.Melvor2.MainMenu;
using HarmonyLib;
using MelvorModLoader;

namespace MyMod;

// Optional: runs once when the game starts, after the patches below are applied.
public class MyMod : IMod
{
    internal static ModLogger Log = new("MyMod");

    public void OnLoad(ModContext context)
    {
        Log = context.Log;
        Log.Info($"Loaded from {context.Directory}");
    }
}

// Every [HarmonyPatch] class is applied automatically.
[HarmonyPatch(typeof(MainMenuView), nameof(MainMenuView._Ready))]
internal static class MainMenuReadyPatch
{
    private static void Postfix() => MyMod.Log.Info("The main menu opened");
}
```

### 2. Build and test

Run `dotnet build` in the folder. It builds the mod and copies it to `<game>\mods\MyMod`. Start Melvor2Modded.exe:
"My Mod" appears in the main menu's mod list, and the game's log
(`%APPDATA%\Games By Malcs\Melvor Idle 2\logs`) contains:

```
[ModLoader] Loaded My Mod v1.0.0: patched 1 method(s)
[MyMod] Loaded from ...\mods\MyMod
[MyMod] The main menu opened
```

### 3. Share it

Zip the mod's folder (`MyMod\mod.json` + `MyMod\MyMod.dll`) so players can extract it into their `mods` folder. A
complete real-world mod (several patches, UI built in code, a packaging script and GitHub releases) is
[Multitasking](https://github.com/nachtmarv/melvor2-multitasking).

### API

- **Patches:** the loader applies every `[HarmonyPatch]` class in your assembly (Harmony id = your mod id), each one
  separately, so one patch broken by a game update doesn't disable the rest. Failures are logged.
- **`IMod`** (optional): implement it in a public class with a parameterless constructor to run code at startup.
  `OnLoad(ModContext context)` gives you `context.Manifest` (your mod.json), `context.Directory` (your folder, e.g.
  for data files), `context.Harmony` (your Harmony instance, for patches applied manually) and `context.Log`. If
  `OnLoad` throws, your patches are removed again.
- **`ModLogger`**: `Info`, `Warning`, `Error` write `[YourMod] ...` to the game's console and log.
- **`Loader`**: `GameDirectory`, `ModsDirectory`, `LoaderDirectory` and `Mods` (every loaded mod's `ModContext`).
- **Libraries** your mod ships next to its DLL are found automatically. They are looked up in `modloader\` first, then
  in every mod folder; if two mods ship different versions of the same library, whichever is found first is used.

### Pitfalls

- **Don't call `GD.PushError` / `GD.PushWarning` while the mod loads** (in `OnLoad` or static constructors): mods load
  before the game's first scene, before Godot is fully set up, and these calls crash the game. Use `ModLogger`.
- **Don't patch methods of generic classes** such as `PageNavigationButton<T>.Update`: .NET shares their compiled code
  and re-compiles it later, which silently drops the patch. Patch a non-generic override or a caller instead.
- **Classes in your mod aren't registered with Godot**, so you can't attach your own scripts to nodes or use them in
  scenes. Build UI in code from plain Godot nodes or the game's existing classes.
- Offline progress runs the game's ticks on a background thread, after the UI has unsubscribed from game events. UI
  code should follow the game's views: subscribe to game events in `HandleEvents`, unsubscribe in `UnhandleEvents`.
- Game updates rename and change things. Rebuild your mod against each new game version and check the log for patch
  failures. To make a mod survive updates: look up private members (`AccessTools`) in a Harmony `Prepare()` method
  and return false if one is missing (the patch is then skipped), and catch exceptions in patch code so that a broken
  feature switches itself off instead of breaking the game. Multitasking's `Feature` class does both.

## Building the loader from source

Requires the .NET 10 SDK and the game (by default the Steam install, found through the registry; override with
`-p:GameDir=...` / `-GameDir ...`).

- `scripts/install.ps1` builds the launcher into the game folder and sets up `modloader/` (needs the .NET 10 runtime
  to run, unlike the release exe).
- `scripts/uninstall.ps1` removes it again.
- `scripts/package.ps1` builds the release exe `dist/Melvor2Modded.exe` (self-contained, no .NET needed).
- `scripts/release.ps1` publishes a GitHub release (needs the [GitHub CLI](https://cli.github.com/)): it builds the
  exe, tags the commit `v<version>` and uploads the exe. The version is `ModLoaderVersion` in `Directory.Build.props`.
- `scripts/decompile.ps1` decompiles the game into `decompiled/` (needs `ilspycmd`).

`src/Launcher` is `Melvor2Modded.exe` (game copy, bootstrap patch, Steam handling); `src/ModLoader` is
`MelvorModLoader.dll`, which runs inside the game and loads the mods.

## License

[MIT](LICENSE). Melvor Idle 2 and its assets belong to Games By Malcs; this repository contains none of the game's
files.
