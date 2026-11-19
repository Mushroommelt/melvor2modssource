using GamesByMalcs.Melvor2.Components.ActionUntilX;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Components.CraftingAction;
using HarmonyLib;
using System.Reflection;

namespace Melvor2RecipeQueue;

/// <summary>
/// Captures the user's chosen recipe and a SNAPSHOT of its stop configuration.
/// This avoids storing live mutable UI stop-option instances. All references
/// live only for the currently loaded character/session, not on disk.
/// </summary>
internal sealed class QueueStep
{
    internal required ActionUntilXComponent Until { get; init; }
    internal required IStopOptionProvider Provider { get; init; }
    internal required ActionComponent Action { get; init; }
    internal required object ActiveRecipe { get; init; }
    internal required Type StopOptionType { get; init; }
    internal required string Display { get; init; }
    internal required string StopDisplay { get; init; }
    internal int? CreationCount { get; init; }
    internal int? Quantity { get; init; }
    internal object? TargetItemOrCurrency { get; init; }
    internal bool TargetIsItem { get; init; }

    internal bool TryConfigureStop(out string message)
    {
        message = "";
        // Selecting a recipe generates fresh stop options for that recipe.
        // We must apply the captured values to one of those NEW options.
        var option = Provider.StopOptions.FirstOrDefault(o => o.GetType() == StopOptionType);
        if (option == null)
        {
            message = $"Stop option '{StopOptionType.Name}' is no longer offered for {Display}.";
            return false;
        }

        if (CreationCount is int count)
        {
            if (option is not BaseRecipeCreationsStopOption creation)
            {
                message = $"Stop option type changed for {Display}.";
                return false;
            }
            creation.Count = count;
        }
        else if (Quantity is int quantity)
        {
            if (option is not ItemCurrencyQuantityStopOption target)
            {
                message = $"Stop option type changed for {Display}.";
                return false;
            }
            var position = -1;
            if (TargetIsItem)
            {
                for (var i = 0; i < target.AvailableItems.Count; i++)
                    if (ReferenceEquals(target.AvailableItems[i], TargetItemOrCurrency)) { position = i; break; }
            }
            else
            {
                for (var i = 0; i < target.AvailableCurrencies.Count; i++)
                    if (ReferenceEquals(target.AvailableCurrencies[i], TargetItemOrCurrency))
                    {
                        position = i + target.AvailableItems.Count;
                        break;
                    }
            }
            if (position < 0)
            {
                message = $"The captured target item/currency is unavailable for {Display}.";
                return false;
            }
            target.SelectedItemIndex = position;
            target.Quantity = quantity;
        }
        else
        {
            message = $"Unsupported stop settings for {Display}.";
            return false;
        }

        if (!option.IsConfigurationValid)
        {
            message = $"Stop configuration is no longer valid for {Display} (inventory or requirements may have changed).";
            return false;
        }
        Provider.SelectedStopOption = option;
        return true;
    }
}

internal static class QueueController
{
    private static readonly object Gate = new();
    private static readonly List<QueueStep> Steps = new();
    private static readonly PropertyInfo? ActionProperty =
        AccessTools.Property(typeof(ActionUntilXComponent), "Action");

    private static bool _armed;
    private static bool _configuredStopReached;
    private static bool _transitioning;
    private static string _status = "Add recipes with their stop conditions, then start the queue.";

    internal static int Count { get { lock (Gate) return Steps.Count; } }
    internal static bool Armed { get { lock (Gate) return _armed; } }
    internal static string Status { get { lock (Gate) return _status; } }
    internal static QueueStep[] Snapshot() { lock (Gate) return Steps.ToArray(); }

    internal static void OnCharacterLoaded()
    {
        // The game calls OnSaveLoaded for each crafting component as it
        // initializes a character. Do not touch Godot UI during this callback.
        lock (Gate)
        {
            if (Steps.Count == 0 && !_armed) return;
            Steps.Clear();
            _armed = false;
            _configuredStopReached = false;
            _transitioning = false;
            _status = "Character save loaded. Session queue was cleared to prevent cross-save actions.";
        }
        RecipeQueueMod.Log.Info("Cleared session recipe queue on character load.");
    }

    internal static ActionComponent? GetAction(ActionUntilXComponent until) =>
        ActionProperty?.GetValue(until) as ActionComponent;

    internal static object? GetSelectedRecipe(ActionComponent action) =>
        action.GetType().GetProperty("SelectedRecipe", BindingFlags.Public | BindingFlags.Instance)?.GetValue(action);

    private static object? GetCreatingRecipe(ActionComponent action) =>
        action.GetType().GetProperty("CreatingRecipe", BindingFlags.Public | BindingFlags.Instance)?.GetValue(action);

    internal static void Add(IStopOptionProvider provider)
    {
        try
        {
            if (provider is not ActionUntilXComponent until)
            {
                SetStatus("The selected action does not support recipe stop conditions.");
                return;
            }
            var action = GetAction(until);
            var activeRecipe = action == null ? null : GetSelectedRecipe(action);
            if (action == null || activeRecipe == null || activeRecipe is not IActiveCraftingRecipe)
            {
                SetStatus("Select a crafting, cooking or herblore recipe before adding it.");
                return;
            }
            var option = provider.SelectedStopOption;
            if (option == null || !option.IsConfigurationValid)
            {
                SetStatus("Choose a valid stop condition and target before pressing '+ Queue'.");
                return;
            }

            int? count = null;
            int? quantity = null;
            object? target = null;
            bool targetIsItem = false;
            if (option is BaseRecipeCreationsStopOption creation)
                count = creation.Count;
            else if (option is ItemCurrencyQuantityStopOption cap)
            {
                quantity = cap.Quantity;
                targetIsItem = cap.IsItemSelected;
                target = targetIsItem ? (object)cap.SelectedItem : cap.SelectedCurrency;
            }
            else
            {
                SetStatus("Unsupported stop option. Nothing was queued.");
                return;
            }

            var display = RecipeDisplay(action, (IActiveCraftingRecipe)activeRecipe);
            var stop = option.DisplayName.Message +
                       (count is int c ? $" {c:N0}" : quantity is int q ? $" {q:N0} {ObjectDisplay(target)}" : "");
            var step = new QueueStep
            {
                Until = until, Provider = provider, Action = action,
                ActiveRecipe = activeRecipe, StopOptionType = option.GetType(),
                CreationCount = count, Quantity = quantity,
                TargetItemOrCurrency = target, TargetIsItem = targetIsItem,
                Display = display, StopDisplay = stop
            };

            int position;
            lock (Gate)
            {
                Steps.Add(step);
                position = Steps.Count;
                _status = $"Added #{position}: {display} — {stop}.";
            }
            RecipeQueueMod.Log.Info(_status);
            QueueUi.ShowAndRefresh();
        }
        catch (Exception ex)
        {
            RecipeQueueMod.Log.Error("Could not add recipe: " + ex);
            SetStatus("Unable to capture recipe/stop configuration (see game log).");
        }
    }

    private static string ObjectDisplay(object? o)
    {
        if (o == null) return "";
        var display = o.GetType().GetProperty("DisplayName")?.GetValue(o);
        var message = display?.GetType().GetProperty("Message")?.GetValue(display) as string;
        return !string.IsNullOrWhiteSpace(message) ? message : o.ToString() ?? "";
    }

    private static string RecipeDisplay(ActionComponent action, IActiveCraftingRecipe active)
    {
        var skill = action.Entity.DisplayName.Message;
        var recipe = ObjectDisplay(active.Recipe);
        return $"{skill}: {recipe}";
    }

    internal static void Start()
    {
        lock (Gate)
        {
            if (_armed) return;
            if (Steps.Count == 0)
            {
                _status = "Queue is empty. Add a recipe first.";
                QueueUi.Refresh();
                return;
            }
            _armed = true;
            _configuredStopReached = false;
            _transitioning = true;
        }
        var worked = StartFirst(out var message);
        lock (Gate)
        {
            _transitioning = false;
            if (!worked) _armed = false;
            _status = message;
        }
        RecipeQueueMod.Log.Info(message);
        QueueUi.RefreshAfterGameEvent();
    }

    /// <summary>Pausing never presses the game's own Stop button.</summary>
    internal static void Pause(string reason = "Queue paused. The current game action is unchanged.")
    {
        lock (Gate)
        {
            _armed = false;
            _configuredStopReached = false;
            _status = reason;
        }
        QueueUi.Refresh();
    }

    internal static void Clear()
    {
        lock (Gate)
        {
            Steps.Clear();
            _armed = false;
            _configuredStopReached = false;
            _status = "Queue cleared. Running crafting is unchanged.";
        }
        QueueUi.Refresh();
    }

    internal static void RemoveAt(int index)
    {
        lock (Gate)
        {
            if (index < 0 || index >= Steps.Count || (_armed && index == 0)) return;
            Steps.RemoveAt(index);
            _status = $"Removed step {index + 1}.";
        }
        QueueUi.Refresh();
    }

    internal static void Move(int index, int offset)
    {
        lock (Gate)
        {
            int dest = index + offset;
            int firstUnlocked = _armed ? 1 : 0;
            if (index < firstUnlocked || dest < firstUnlocked || index >= Steps.Count || dest >= Steps.Count)
                return;
            (Steps[index], Steps[dest]) = (Steps[dest], Steps[index]);
            _status = "Queue reordered.";
        }
        QueueUi.Refresh();
    }

    internal static void MarkConfiguredStopReached(ActionUntilXComponent until)
    {
        lock (Gate)
        {
            if (_armed && !_transitioning && Steps.Count > 0 &&
                ReferenceEquals(Steps[0].Until, until))
                _configuredStopReached = true;
        }
    }

    internal static void OnActionStopped(ActionComponent action)
    {
        bool advance = false;
        bool finished = false;
        lock (Gate)
        {
            if (!_armed || _transitioning || Steps.Count == 0 ||
                !ReferenceEquals(Steps[0].Action, action)) return;

            if (_configuredStopReached)
            {
                Steps.RemoveAt(0);
                _configuredStopReached = false;
                if (Steps.Count > 0)
                {
                    _transitioning = true;
                    advance = true;
                }
                else
                {
                    _armed = false;
                    _status = "Queue complete. All configured stop points were reached.";
                    finished = true;
                }
            }
            else
            {
                _armed = false;
                _status = "Queue paused: current action stopped before its configured stop point. " +
                          "Check materials or press Start Queue to retry.";
            }
        }

        if (advance)
        {
            var worked = StartFirst(out var message);
            lock (Gate)
            {
                _transitioning = false;
                if (!worked) _armed = false;
                _status = message;
            }
            RecipeQueueMod.Log.Info(message);
        }
        else if (finished) RecipeQueueMod.Log.Info("Recipe queue complete.");
        else RecipeQueueMod.Log.Warning("Recipe queue paused by an unexpected/manual stop.");
        QueueUi.RefreshAfterGameEvent();
    }

    private static bool StartFirst(out string message)
    {
        QueueStep step;
        lock (Gate)
        {
            if (Steps.Count == 0)
            {
                message = "No recipes remain.";
                return false;
            }
            step = Steps[0];
        }

        try
        {
            // Starting the queue with the exact recipe already running uses
            // its ACTUAL active stop. We don't restart/reset its progress.
            if (step.Action.IsStarted && ReferenceEquals(GetCreatingRecipe(step.Action), step.ActiveRecipe))
            {
                if (step.Until.StopGoal != null || step.Until.StopCondition != null)
                {
                    message = $"Following already-running recipe: {step.Display}.";
                    return true;
                }
                message = $"{step.Display} is running without a stop goal. " +
                          "Stop it in-game, then start the queue again.";
                return false;
            }

            var method = step.Action.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "SelectRecipe" &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType.IsInstanceOfType(step.ActiveRecipe));
            if (method == null)
            {
                message = $"Recipe selection API was not found for {step.Display}.";
                return false;
            }
            method.Invoke(step.Action, new[] { step.ActiveRecipe });

            if (!step.TryConfigureStop(out message)) return false;

            // This is the game's own 'start with selected stop option' method,
            // not an imitation. It creates the Condition/Goal and starts work.
            var start = step.Until.GetType().GetMethod(
                "StartCreating", BindingFlags.Public | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (start == null)
            {
                message = "Game changed: StartCreating() is missing.";
                return false;
            }
            start.Invoke(step.Until, null);

            if (!step.Action.IsStarted || !ReferenceEquals(GetCreatingRecipe(step.Action), step.ActiveRecipe))
            {
                message = $"Unable to start {step.Display}. It may be locked or missing ingredients. " +
                          "Queue paused instead of skipping it.";
                return false;
            }
            if (step.Until.StopGoal == null && step.Until.StopCondition == null)
            {
                // Fail closed: the mod must never leave a recipe running forever
                // when it couldn't arm the target.
                step.Action.QueueOrTryStop();
                message = $"Stop goal didn't activate for {step.Display}; crafting stopped and queue paused.";
                return false;
            }
            message = $"Started {step.Display}. Stop at: {step.StopDisplay}.";
            return true;
        }
        catch (TargetInvocationException e)
        {
            RecipeQueueMod.Log.Error("Game rejected recipe start: " + (e.InnerException ?? e));
            message = $"Unable to start {step.Display}: {e.InnerException?.Message ?? e.Message}. Queue paused.";
            return false;
        }
        catch (Exception e)
        {
            RecipeQueueMod.Log.Error("Queue start exception: " + e);
            message = $"Unable to start {step.Display}: {e.Message}. Queue paused.";
            return false;
        }
    }

    private static void SetStatus(string message)
    {
        lock (Gate) _status = message;
        RecipeQueueMod.Log.Info(message);
        QueueUi.ShowAndRefresh();
    }
}
