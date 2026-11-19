using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MelvorModLoaderLauncher;

/// <summary>
/// Lets the modded copy run as the Steam game. On startup the Steam build calls SteamAPI.RestartAppIfNecessary: if the
/// game wasn't started by Steam, it quits and has Steam start the original (unmodded) exe instead. Steam marks the
/// games it starts with environment variables, and a steam_appid.txt next to the game does the same; we provide both.
///
/// The app id comes from Steam's own record of the installation (steamapps/appmanifest_&lt;id&gt;.acf), never from the
/// game's code, so it doesn't depend on how the game is written and is always the id Steam checks against.
///
/// Unlike a start through Steam, Steam doesn't sync the game's cloud saves before starting it. Once Steam has been
/// running for a moment its cloud state is loaded and the game syncs its saves itself; right after Steam starts, the
/// game sees no cloud saves and replaces them with the local ones (see Program.AskToStartSteam).
/// </summary>
internal static partial class Steam
{
    public const string AppIdFile = "steam_appid.txt";

    private const string SteamKey = @"HKEY_CURRENT_USER\Software\Valve\Steam";
    private const string ActiveProcessKey = SteamKey + @"\ActiveProcess";

    // The variables Steam sets for the games it starts.
    private static readonly string[] s_appIdVariables = ["SteamAppId", "SteamGameId", "SteamOverlayGameId"];

    /// <summary>The app id Steam installed <paramref name="gameDir"/> as, or null if it isn't in a Steam library.</summary>
    public static uint? FindAppId(string gameDir)
    {
        // Steam installs apps into <library>/steamapps/common/<installdir> and describes each one in
        // <library>/steamapps/appmanifest_<appid>.acf.
        gameDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDir));
        var common = Path.GetDirectoryName(gameDir);
        var steamApps = Path.GetDirectoryName(common);
        if (common == null || steamApps == null || !Path.GetFileName(common).Equals("common", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var installDir = Path.GetFileName(gameDir);
        foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
        {
            var text = File.ReadAllText(manifest);
            if (InstallDirPattern().Match(text) is { Success: true } dir
                && dir.Groups[1].Value.Equals(installDir, StringComparison.OrdinalIgnoreCase)
                && AppIdPattern().Match(text) is { Success: true } id
                && uint.TryParse(id.Groups[1].Value, out var appId))
            {
                return appId;
            }
        }
        return null;
    }

    /// <summary>Marks a game started with <paramref name="start"/> as started by Steam.</summary>
    public static void SetAppIdVariables(ProcessStartInfo start, uint appId)
    {
        foreach (var name in s_appIdVariables)
        {
            start.Environment[name] = appId.ToString();
        }
    }

    /// <summary>Whether Steam is running with a user logged in: the game can't initialize Steam before that.</summary>
    public static bool IsReady() => IsRunning() && Registry.GetValue(ActiveProcessKey, "ActiveUser", 0) is int user && user != 0;

    /// <summary>Starts Steam in the background (in the tray, without opening the library window) if it isn't running.</summary>
    public static void Start()
    {
        if (IsRunning())
        {
            return;
        }
        var exe = Registry.GetValue(SteamKey, "SteamExe", null) as string;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            throw new InvalidOperationException("Steam is not running and could not be found. Start Steam and try again.");
        }
        Process.Start(new ProcessStartInfo(exe, "-silent") { UseShellExecute = false });
    }

    // Steam keeps the running client's process id and the logged in user in the registry (0 when not running / logged out).
    private static bool IsRunning()
    {
        if (Registry.GetValue(ActiveProcessKey, "pid", 0) is not int pid || pid == 0)
        {
            return false;
        }
        // The value outlives a crashed Steam, and the id may have been reused since.
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [GeneratedRegex("""^\s*"installdir"\s+"([^"]*)"\s*$""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirPattern();

    [GeneratedRegex("""^\s*"appid"\s+"(\d+)"\s*$""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex AppIdPattern();
}
