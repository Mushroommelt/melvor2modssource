namespace MelvorModLoaderLauncher;

/// <summary>A Melvor Idle 2 folder and the files the launcher manages in it.</summary>
internal sealed record GameLayout(string GameDir)
{
    public const string GameExe = "Melvor2.exe";
    public const string LauncherExe = "Melvor2Modded.exe";

    /// <summary>Finds the game folder: <paramref name="dir"/> itself or its parent (for modloader/uninstall.exe).</summary>
    public static GameLayout? Find(string dir)
    {
        var parent = Path.GetDirectoryName(dir);
        return File.Exists(Path.Combine(dir, GameExe)) ? new GameLayout(dir)
            : parent != null && File.Exists(Path.Combine(parent, GameExe)) ? new GameLayout(parent)
            : null;
    }

    /// <summary>&lt;game&gt;/mods: one folder per mod, managed by the player.</summary>
    public string ModsDir => Path.Combine(GameDir, "mods");

    /// <summary>&lt;game&gt;/modloader: everything the launcher creates.</summary>
    public string LoaderDir => Path.Combine(GameDir, "modloader");

    public string LoaderAssembly => Path.Combine(LoaderDir, "MelvorModLoader.dll");

    public string UninstallExe => Path.Combine(LoaderDir, "uninstall.exe");

    /// <summary>&lt;game&gt;/modloader/game: the modded copy of the game the launcher starts.</summary>
    public string ModdedGameDir => Path.Combine(LoaderDir, "game");

    public string ModdedGameExe => Path.Combine(ModdedGameDir, GameExe);

    /// <summary>The Steam app id of the game, or null if it isn't a Steam installation (see <see cref="Steam"/>).</summary>
    public uint? SteamAppId { get; } = Steam.FindAppId(GameDir);

    /// <summary>The original game assembly, in the data_* folder next to the game exe.</summary>
    public string GameAssembly =>
        Directory.EnumerateDirectories(GameDir, "data_*")
            .OrderBy(dir => !Path.GetFileName(dir).Equals("data_GamesByMalcs.Melvor2_windows_x86_64", StringComparison.OrdinalIgnoreCase))
            .ThenBy(dir => dir, StringComparer.OrdinalIgnoreCase)
            .Select(dir => Path.Combine(dir, BootstrapPatcher.GameAssemblyFile))
            .FirstOrDefault(File.Exists)
        ?? throw new InvalidOperationException($"No data_* folder with {BootstrapPatcher.GameAssemblyFile} in {GameDir}.");
}
