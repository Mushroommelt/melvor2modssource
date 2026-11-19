using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;

namespace MelvorModLoader;

/// <summary>
/// Entry point, called by the bootstrap the launcher (Melvor2Modded.exe) injects into GodotPlugins.Game.Main
/// (right after Godot's script bridge is initialized, before any game scene or script runs).
///
/// Loads every <c>mods/*/mod.json</c> mod in id order into the game's AssemblyLoadContext, applies its Harmony
/// patches (each patch class separately, so one broken by a game update doesn't disable the rest) and calls its
/// <see cref="IMod"/> entry points. A failing mod is logged and skipped; it never stops the game from starting.
/// </summary>
public static class Loader
{
    private static readonly List<ModContext> s_mods = [];
    private static bool s_initialized;

    /// <summary>The game folder (where Melvor2.exe is).</summary>
    public static string GameDirectory { get; private set; } = "";

    /// <summary>&lt;game&gt;/modloader: the loader and shared libraries (0Harmony).</summary>
    public static string LoaderDirectory { get; private set; } = "";

    /// <summary>&lt;game&gt;/mods: one folder per mod.</summary>
    public static string ModsDirectory { get; private set; } = "";

    public static IReadOnlyList<ModContext> Mods => s_mods;

    internal static ModLogger Log { get; } = new("ModLoader");

    internal static string Version => typeof(Loader).Assembly.GetName().Version?.ToString(3) ?? "?";

    public static void Init()
    {
        if (s_initialized)
        {
            return;
        }
        s_initialized = true;

        LoaderDirectory = Path.GetDirectoryName(typeof(Loader).Assembly.Location)!;
        GameDirectory = Path.GetDirectoryName(LoaderDirectory)!;
        ModsDirectory = Path.Combine(GameDirectory, "mods");

        var loadContext = AssemblyLoadContext.GetLoadContext(typeof(Loader).Assembly) ?? AssemblyLoadContext.Default;
        loadContext.Resolving += ResolveFromLoaderAndMods;

        try
        {
            Directory.CreateDirectory(ModsDirectory);
            LoadMods(loadContext);
        }
        catch (Exception e)
        {
            Log.Error($"Loading mods failed: {e}");
        }
    }

    // Kept separate so HarmonyLib is only touched after the resolver is installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadMods(AssemblyLoadContext loadContext)
    {
        Log.Info($"v{Version} (Early Access fork), mods folder: {ModsDirectory}");
        var manifests = new List<(ModManifest Manifest, string Directory)>();
        foreach (var directory in Directory.EnumerateDirectories(ModsDirectory))
        {
            var manifestPath = Path.Combine(directory, ModManifest.FileName);
            if (!File.Exists(manifestPath))
            {
                continue;
            }
            try
            {
                manifests.Add((ModManifest.Read(manifestPath), directory));
            }
            catch (Exception e)
            {
                Log.Error($"Skipping {Path.GetFileName(directory)}: invalid {ModManifest.FileName}: {e.Message}");
            }
        }

        foreach (var group in manifests
            .GroupBy(m => m.Manifest.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                Log.Error($"Mod id '{group.Key}' is used by several folders, skipping all of them: " +
                    string.Join(", ", group.Select(m => Path.GetFileName(m.Directory))));
                continue;
            }
            var (manifest, directory) = group.Single();
            try
            {
                LoadMod(loadContext, manifest, directory);
            }
            catch (Exception e) when (e is IOException or BadImageFormatException)
            {
                Log.Error($"Failed to load {manifest.DisplayName}: {e.Message}");
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load {manifest.DisplayName}: {e}");
            }
        }
        Log.Info($"{s_mods.Count} mod(s) loaded");

        try
        {
            MainMenuInfo.Apply(new Harmony("MelvorModLoader"));
        }
        catch (Exception e)
        {
            Log.Error($"Main menu mod list unavailable (game update?): {e.GetBaseException().Message}");
        }

        // The original Multitasking DLL remains untouched. The loader can
        // render its optional activity HUD without changing any game mechanics.
        if (s_mods.Any(m => m.Manifest.Id.Equals("Multitasking", StringComparison.OrdinalIgnoreCase)))
        {
            try { MultitaskingActivityHud.Apply(new Harmony("MelvorModLoader.MultitaskingHud")); }
            catch (Exception e) { Log.Warning("Multitasking HUD unavailable: " + e.GetBaseException().Message); }
        }
    }

    private static void LoadMod(AssemblyLoadContext loadContext, ModManifest manifest, string directory)
    {
        var assembly = loadContext.LoadFromAssemblyPath(Path.Combine(directory, manifest.Assembly));
        var context = new ModContext(manifest, directory, new Harmony(manifest.Id));
        var types = AccessTools.GetTypesFromAssembly(assembly);

        var failed = 0;
        foreach (var type in types)
        {
            try
            {
                context.Harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                failed++;
                context.Log.Error($"Patch {type.FullName} failed (game update?): {e.GetBaseException().Message}");
            }
        }

        try
        {
            foreach (var type in types.Where(t => typeof(IMod).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }))
            {
                ((IMod)Activator.CreateInstance(type)!).OnLoad(context);
            }
        }
        catch
        {
            // A mod that failed to start must not stay half active.
            context.Harmony.UnpatchAll(context.Harmony.Id);
            throw;
        }

        s_mods.Add(context);
        Log.Info($"Loaded {manifest.DisplayName} v{manifest.Version ?? "?"}: " +
            $"patched {context.Harmony.GetPatchedMethods().Count()} method(s)" + (failed > 0 ? $", {failed} patch(es) failed" : ""));
    }

    // Mod dependencies are not on the game's probing path: look in the loader folder (shared 0Harmony), then all mod folders.
    private static Assembly? ResolveFromLoaderAndMods(AssemblyLoadContext context, AssemblyName name)
    {
        var fileName = name.Name + ".dll";
        var directories = Directory.Exists(ModsDirectory)
            ? Directory.EnumerateDirectories(ModsDirectory).Prepend(LoaderDirectory)
            : [LoaderDirectory];
        foreach (var directory in directories)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }
        }
        return null;
    }
}
