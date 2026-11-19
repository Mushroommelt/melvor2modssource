using System.Security.Cryptography;

namespace MelvorModLoaderLauncher;

/// <summary>
/// Keeps &lt;game&gt;/modloader/game up to date: a copy of the game made of hard links to the original files (no extra
/// disk space), except for the game assembly, which is a patched copy (<see cref="BootstrapPatcher"/>). Starting the
/// game from there loads mods; the original game folder is never modified and keeps starting the unmodded game.
///
/// Runs on every launch and only touches what changed, so game updates are picked up automatically.
/// </summary>
internal static class ModdedGame
{
    // Remembers which original assembly (and bootstrap version) the patched copy was made from.
    private const string StateFile = "modloader.state";

    public static void Update(GameLayout layout)
    {
        var assembly = Path.GetRelativePath(layout.GameDir, layout.GameAssembly);
        var managed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { assembly, StateFile };

        Directory.CreateDirectory(layout.ModdedGameDir);
        if (layout.SteamAppId is { } appId)
        {
            managed.Add(Steam.AppIdFile);
            WriteSteamAppId(Path.Combine(layout.ModdedGameDir, Steam.AppIdFile), appId);
        }
        foreach (var source in GameFiles(layout))
        {
            var relative = Path.GetRelativePath(layout.GameDir, source);
            if (managed.Add(relative))
            {
                Files.Mirror(source, Path.Combine(layout.ModdedGameDir, relative));
            }
        }
        RemoveStale(layout.ModdedGameDir, managed);
        UpdateAssembly(layout, Path.Combine(layout.ModdedGameDir, assembly));
    }

    // Everything in the game folder except what belongs to the mod loader.
    private static IEnumerable<string> GameFiles(GameLayout layout)
    {
        foreach (var entry in new DirectoryInfo(layout.GameDir).EnumerateFileSystemInfos())
        {
            if (entry is FileInfo file && !file.Name.Equals(GameLayout.LauncherExe, StringComparison.OrdinalIgnoreCase))
            {
                yield return file.FullName;
            }
            else if (entry is DirectoryInfo dir && !IsSamePath(dir.FullName, layout.LoaderDir) && !IsSamePath(dir.FullName, layout.ModsDir))
            {
                foreach (var nested in dir.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    yield return nested.FullName;
                }
            }
        }
    }

    private static bool IsSamePath(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    // Files the game no longer has (after an update), and folders left empty by that.
    private static void RemoveStale(string dir, HashSet<string> keep)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList())
        {
            if (!keep.Contains(Path.GetRelativePath(dir, file)))
            {
                File.Delete(file);
            }
        }
        foreach (var sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList())
        {
            if (!Directory.EnumerateFileSystemEntries(sub).Any())
            {
                Directory.Delete(sub);
            }
        }
    }

    // Marks the copy as started by Steam (see Steam). Replaced, not overwritten: it may be a hard link to a file the
    // game shipped itself.
    private static void WriteSteamAppId(string path, uint appId)
    {
        var content = appId.ToString();
        if (!File.Exists(path) || File.ReadAllText(path) != content)
        {
            File.Delete(path);
            File.WriteAllText(path, content);
        }
    }

    private static void UpdateAssembly(GameLayout layout, string patched)
    {
        var original = layout.GameAssembly;
        var statePath = Path.Combine(layout.ModdedGameDir, StateFile);
        var state = $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original)))} bootstrap v{BootstrapPatcher.BootstrapVersion}";
        if (File.Exists(patched) && File.Exists(statePath) && File.ReadAllText(statePath) == state
            && BootstrapPatcher.IsPatched(patched))
        {
            return;
        }

        if (BootstrapPatcher.IsPatched(original))
        {
            RestoreLegacyBackup(layout);
            UpdateAssembly(layout, patched);
            return;
        }
        BootstrapPatcher.Patch(original, patched);
        File.WriteAllText(statePath, state);
    }

    // Mod loader setups before 0.2.0 patched the game's own assembly and kept the original in modloader/backup.
    private static void RestoreLegacyBackup(GameLayout layout)
    {
        var backupDir = Path.Combine(layout.LoaderDir, "backup");
        var backup = Path.Combine(backupDir, Path.GetRelativePath(layout.GameDir, layout.GameAssembly));
        if (!File.Exists(backup) || BootstrapPatcher.IsPatched(backup))
        {
            throw new InvalidOperationException(
                "The game files were modified by an older version of the mod loader and no original backup was found. " +
                "Restore the original game files (Steam: Properties > Installed Files > Verify integrity) and start again.");
        }
        File.Copy(backup, layout.GameAssembly, overwrite: true);
        Directory.Delete(backupDir, recursive: true);
    }
}
