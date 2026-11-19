using System.Diagnostics;
using System.Reflection;
using MelvorModLoaderLauncher;

// Melvor2Modded.exe [--no-launch] [--quiet] [game arguments...]
//   Starts Melvor Idle 2 with mods. Lives in the game folder. The original game files are never modified: the game is
//   started from <game>/modloader/game, a hard-linked copy with the mod loader bootstrap patched in, which is updated
//   automatically (e.g. after game updates). Starting Melvor2.exe directly still starts the unmodded game.
//   Steam: the copy is started as the Steam game (see Steam.cs), only while Steam is running (otherwise the player is
//   asked to start Steam first, so the game's cloud saves are available when it syncs them). Optionally the
//   game's launch options can be set to  "<game folder>\Melvor2Modded.exe" %command%  so Steam's Play button starts
//   the modded game (the Melvor2.exe path Steam puts first is recognized, the remaining arguments are passed on).
//
// modloader/uninstall.exe (or Melvor2Modded.exe --uninstall) [--quiet]
//   Removes Melvor2Modded.exe and the modloader folder. The mods folder and saves are kept.
//
//   --no-launch  only set up / update the modloader folder (used by the dev scripts)
//   --quiet      no message boxes; errors go to stderr only

string[] ownFlags = ["--uninstall", "--no-launch", "--quiet"];
var gameArgs = args.Where(a => !ownFlags.Contains(a)).ToList();
var quiet = args.Contains("--quiet");
var exePath = Environment.ProcessPath!;
var uninstall = args.Contains("--uninstall")
    || Path.GetFileNameWithoutExtension(exePath).Equals("uninstall", StringComparison.OrdinalIgnoreCase);

try
{
    var gameDir = Path.GetDirectoryName(exePath)!;
    if (gameArgs.Count > 0 && Path.GetFileName(gameArgs[0]).Equals(GameLayout.GameExe, StringComparison.OrdinalIgnoreCase)
        && File.Exists(gameArgs[0]))
    {
        gameDir = Path.GetDirectoryName(Path.GetFullPath(gameArgs[0]))!;
        gameArgs.RemoveAt(0);
    }
    var layout = GameLayout.Find(gameDir)
        ?? throw new InvalidOperationException($"{GameLayout.GameExe} not found. Put {GameLayout.LauncherExe} into the Melvor Idle 2 folder.");
    if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameLayout.GameExe)).Length > 0)
    {
        throw new InvalidOperationException("Melvor Idle 2 is already running.");
    }

    if (uninstall)
    {
        Uninstall(layout);
        return 0;
    }
    Install(layout);
    return args.Contains("--no-launch") ? 0 : Launch(layout, gameArgs);
}
catch (Exception e) when (e is InvalidOperationException or IOException or UnauthorizedAccessException)
{
    Show(e.Message, NativeMethods.MB_ICONERROR);
    return 1;
}

void Install(GameLayout layout)
{
    Directory.CreateDirectory(layout.LoaderDir);
    foreach (var (name, content) in LoaderFiles())
    {
        Files.WriteIfChanged(Path.Combine(layout.LoaderDir, name), content);
    }
    if (!string.Equals(exePath, layout.UninstallExe, StringComparison.OrdinalIgnoreCase))
    {
        // The uninstaller is this exe, under another name.
        Files.Mirror(exePath, layout.UninstallExe);
    }
    ModdedGame.Update(layout);
    Directory.CreateDirectory(layout.ModsDir);
}

int Launch(GameLayout layout, List<string> arguments)
{
    var start = new ProcessStartInfo(layout.ModdedGameExe) { WorkingDirectory = layout.ModdedGameDir, UseShellExecute = false };
    arguments.ForEach(start.ArgumentList.Add);
    start.Environment[BootstrapPatcher.LoaderPathVariable] = layout.LoaderAssembly;
    if (layout.SteamAppId is { } appId)
    {
        if (!Steam.IsReady())
        {
            AskToStartSteam();
            return 0;
        }
        Steam.SetAppIdVariables(start, appId);
    }
    using var game = Process.Start(start)!;
    // Stay alive while the game runs: Steam (and anything else that started us) sees the game as running.
    game.WaitForExit();
    return game.ExitCode;
}

// The game needs Steam, but started right after Steam it sees no cloud saves yet and replaces them with this PC's saves
// (see Steam). So the game is never started together with Steam: the player starts it again once Steam is ready.
void AskToStartSteam()
{
    if (quiet)
    {
        throw new InvalidOperationException("Steam is not running. Start Steam, wait until it has finished starting " +
            "(the game syncs its Steam Cloud saves on start), then start the game again.");
    }
    if (TaskDialog.Show("Melvor Idle 2 Modded", "Let's get Steam going first",
            "Your saves sync with Steam Cloud when the game starts, so Steam needs to be up and running before you " +
            "play. Otherwise your cloud saves could get overwritten.\n\n" +
            "Start Steam, give it a few seconds, then launch the game again.",
            "Start Steam\nThen launch the game again once Steam is ready.",
            "Not now") == 0)
    {
        Steam.Start();
    }
}

void Uninstall(GameLayout layout)
{
    if (!quiet && NativeMethods.MessageBox(0,
            $"Remove the mod loader from {layout.GameDir}?\n\nYour mods folder and saves are kept.",
            "Melvor Idle 2 Mod Loader", NativeMethods.MB_YESNO | NativeMethods.MB_ICONQUESTION) != NativeMethods.IDYES)
    {
        return;
    }
    // Hard links: deleting them leaves the original game files alone.
    if (Directory.Exists(layout.ModdedGameDir))
    {
        Directory.Delete(layout.ModdedGameDir, recursive: true);
    }
    foreach (var (name, _) in LoaderFiles())
    {
        File.Delete(Path.Combine(layout.LoaderDir, name));
    }
    Show($"The mod loader was removed. Your mods folder ({layout.ModsDir}) and saves were kept.", NativeMethods.MB_ICONINFORMATION);

    // A running exe can't delete itself: a hidden cmd removes the launcher and the rest of the modloader folder
    // (including uninstall.exe) once we have exited.
    Process.Start(new ProcessStartInfo("cmd.exe")
    {
        Arguments = $"/c ping -n 3 127.0.0.1 >nul & del /f /q \"{Path.Combine(layout.GameDir, GameLayout.LauncherExe)}\" & rmdir /s /q \"{layout.LoaderDir}\"",
        CreateNoWindow = true,
        UseShellExecute = false,
    });
}

// The loader, Harmony and the loader's API docs, embedded at build time (see Launcher.csproj).
static IEnumerable<(string Name, byte[] Content)> LoaderFiles()
{
    const string prefix = "modloader/";
    var assembly = Assembly.GetExecutingAssembly();
    foreach (var resource in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix)))
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        yield return (resource[prefix.Length..], buffer.ToArray());
    }
}

void Show(string message, uint icon)
{
    Console.Error.WriteLine(message);
    if (!quiet)
    {
        NativeMethods.MessageBox(0, message, "Melvor Idle 2 Mod Loader", NativeMethods.MB_OK | icon);
    }
}
