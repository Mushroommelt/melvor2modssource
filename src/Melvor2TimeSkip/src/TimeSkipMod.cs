using System;
using System.Reflection;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.GameMenus;
using HarmonyLib;
using MelvorModLoader;

namespace Melvor2TimeSkip;

/// <summary>
/// Early Access integration, based on the shipped Game.RequestOfflineHours(hours)
/// and Main.GameState.RunOfflineProgress(milliseconds, fromSleepWake) methods.
/// All UI work is performed by Godot UI signals, never from the offline worker.
/// </summary>
public sealed class TimeSkipMod : IMod
{
    internal static ModLogger Log = new("Melvor2TimeSkip");

    public void OnLoad(ModContext context)
    {
        Log = context.Log;

        // This manual patch is intentionally fail-closed: without it a manual
        // offline request does not set GameFlags.IsOffline, and other mods may
        // attempt UI operations from the background simulation thread.
        try
        {
            var stateType = typeof(Main).GetNestedType("GameState",
                BindingFlags.Public | BindingFlags.NonPublic);
            if (stateType == null)
                throw new MissingMemberException("Main.GameState was not found");

            var runOffline = AccessTools.Method(stateType, "RunOfflineProgress",
                new[] { typeof(double), typeof(bool) });
            var gameProperty = AccessTools.Property(stateType, "Game");
            if (runOffline == null || gameProperty == null)
                throw new MissingMemberException("The Early Access offline entrypoint changed");

            var prefix = AccessTools.Method(typeof(TimeSkipRuntime),
                nameof(TimeSkipRuntime.BeforeRunOfflineProgress));
            if (prefix == null)
                throw new MissingMemberException("Time Skip's safety prefix is missing");

            context.Harmony.Patch(runOffline, prefix: new HarmonyMethod(prefix));
            TimeSkipRuntime.Configure(gameProperty);
            Log.Info("Time Skip 0.2.5-ea ready; gameplay UI will show a Time Skip button.");
        }
        catch (Exception ex)
        {
            TimeSkipRuntime.Disable();
            Log.Error("Time Skip disabled: cannot safely patch offline simulation. " + ex);
        }
    }
}

/// <summary>
/// Bind may precede scene-tree entry, so also install the overlay on Ready.
/// </summary>
[HarmonyPatch(typeof(GameView), nameof(GameView._Ready))]
internal static class GameViewReadyPatch
{
    private static void Postfix(GameView __instance)
    {
        try { TimeSkipUi.Attach(__instance); }
        catch (Exception ex) { TimeSkipMod.Log.Error("Cannot attach Time Skip UI on Ready: " + ex); }
    }
}

[HarmonyPatch(typeof(GameView), nameof(GameView.Bind))]
internal static class GameViewBindPatch
{
    private static void Postfix(GameView __instance, Game __0)
    {
        try { TimeSkipUi.Attach(__instance, __0); }
        catch (Exception ex) { TimeSkipMod.Log.Error("Cannot attach Time Skip button: " + ex); }
    }
}

[HarmonyPatch(typeof(GameView), nameof(GameView.Unbind))]
internal static class GameViewUnbindPatch
{
    private static void Postfix(GameView __instance)
    {
        try { TimeSkipUi.Detach(__instance); }
        catch (Exception ex) { TimeSkipMod.Log.Warning("Time Skip UI cleanup failed: " + ex); }
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.OnOfflineProgressEnd))]
internal static class OfflineSimulationEndedPatch
{
    private static void Postfix(Game __instance)
    {
        try { TimeSkipRuntime.AfterOfflineProgress(__instance); }
        catch (Exception ex) { TimeSkipMod.Log.Error("Time Skip completion handler error: " + ex); }
    }
}
