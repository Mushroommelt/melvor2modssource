using HarmonyLib;

namespace MelvorModLoader;

/// <summary>
/// Optional mod entry point. The loader applies a mod's [HarmonyPatch] classes automatically; implement this (with a
/// public parameterless constructor) only when a mod needs to run code at startup.
/// </summary>
public interface IMod
{
    /// <summary>Called once at game startup, after the mod's Harmony patches are applied and before any scene loads.</summary>
    void OnLoad(ModContext context);
}

/// <summary>What the loader knows about a loaded mod.</summary>
public sealed class ModContext(ModManifest manifest, string directory, Harmony harmony)
{
    public ModManifest Manifest { get; } = manifest;

    /// <summary>The mod's folder, e.g. for its own data files.</summary>
    public string Directory { get; } = directory;

    /// <summary>The Harmony instance (id = manifest id) the mod's patches were applied with.</summary>
    public Harmony Harmony { get; } = harmony;

    public ModLogger Log { get; } = new(manifest.Id);
}
