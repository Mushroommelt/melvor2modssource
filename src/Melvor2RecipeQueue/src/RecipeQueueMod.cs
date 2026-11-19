using GamesByMalcs.Melvor2.Components.ActionUntilX;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Conditions;
using GamesByMalcs.Melvor2.Core;
using HarmonyLib;
using MelvorModLoader;
using System.Reflection;

namespace Melvor2RecipeQueue;

public sealed class RecipeQueueMod : IMod
{
    internal static ModLogger Log = new("Melvor2RecipeQueue");

    public void OnLoad(ModContext context)
    {
        Log = context.Log;
        Log.Info("Recipe Queue 0.2.5-ea initialized. Buttons appear beside crafting stop options.");
    }
}

/// <summary>
/// The stop selector is a non-generic Godot view. One patch covers crafting,
/// cooking and herblore without patching the shared generic crafting base.
/// </summary>
[HarmonyPatch(typeof(StopOptionProviderView), nameof(StopOptionProviderView.Bind))]
internal static class StopSelectorBindPatch
{
    private static void Postfix(StopOptionProviderView __instance, IStopOptionProvider __0)
    {
        try { QueueUi.AddControls(__instance, __0); }
        catch (Exception ex) { RecipeQueueMod.Log.Error("Unable to add recipe queue controls: " + ex); }
    }
}

/// <summary>
/// Mark the configured STOP CONDITION as satisfied BEFORE the game initiates
/// its action stop. The mark is consumed only after TryStop succeeds.
/// </summary>
[HarmonyPatch]
internal static class ConfiguredConditionReachedPatch
{
    private static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(ActionUntilXComponent), "StopConditionMetHandler");

    private static void Prefix(ActionUntilXComponent __instance, ConditionMetEventArgs __1)
    {
        try
        {
            if (__1.IsMet && __instance.StopCondition != null)
                QueueController.MarkConfiguredStopReached(__instance);
        }
        catch (Exception ex) { RecipeQueueMod.Log.Error("Condition hook failed: " + ex); }
    }
}

/// <summary>As above, but for stop GOALS (such as 'Recipe Creations').</summary>
[HarmonyPatch]
internal static class ConfiguredGoalReachedPatch
{
    private static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(ActionUntilXComponent), "StopGoalIsCompleteChangedHandler");

    private static void Prefix(ActionUntilXComponent __instance, ValueChangedEventArgs<bool> __1)
    {
        try
        {
            if (__1.NewValue && __instance.StopGoal != null)
                QueueController.MarkConfiguredStopReached(__instance);
        }
        catch (Exception ex) { RecipeQueueMod.Log.Error("Goal hook failed: " + ex); }
    }
}

/// <summary>
/// This *non-generic* patch runs after the Stop event is fired and all of its
/// listeners (including ActiveActionManager) have returned. Advancing from an
/// earlier stop event would be too soon: the old action could still be active.
/// </summary>
[HarmonyPatch(typeof(ActionComponent), nameof(ActionComponent.TryStop))]
internal static class FinishedStoppingPatch
{
    private static void Postfix(ActionComponent __instance, bool __result)
    {
        if (!__result) return;
        try { QueueController.OnActionStopped(__instance); }
        catch (Exception ex) { RecipeQueueMod.Log.Error("Queue transition failed: " + ex); }
    }
}

/// <summary>
/// Do not carry references to one character's crafting components into a
/// different character. Game save loading recreates the component graph.
/// </summary>
[HarmonyPatch]
internal static class CharacterSaveLoadedPatch
{
    private static MethodBase? TargetMethod() =>
        AccessTools.Method(typeof(ActionUntilXComponent), "OnSaveLoaded");

    private static void Postfix()
    {
        try { QueueController.OnCharacterLoaded(); }
        catch (Exception ex) { RecipeQueueMod.Log.Error("Queue save-reset hook failed: " + ex); }
    }
}
