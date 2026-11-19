using System;
using System.Reflection;
using GamesByMalcs.Melvor2;

namespace Melvor2TimeSkip;

/// <summary>
/// Invokes the actual game's offline replay, with an acknowledgement handshake.
/// No changes to the OS clock, game save timestamps or game tick length.
/// </summary>
internal static class TimeSkipRuntime
{
    internal const int HardMaxMinutes = 1440; // One-day ceiling per request.

    private static PropertyInfo? _stateGameProperty;
    private static bool _requestPending;
    private static bool _acknowledged;
    private static bool _busy;
    private static Game? _requestedGame;
    private static string _lastStatus = "Ready. Select an amount of game time to simulate.";

    internal static bool Available => _stateGameProperty != null;
    internal static bool Busy => _busy;
    internal static string LastStatus => _lastStatus;

    internal static void Configure(PropertyInfo stateGameProperty)
    {
        _stateGameProperty = stateGameProperty;
        _lastStatus = "Ready. The game will process every simulated tick.";
    }

    internal static void Disable()
    {
        _stateGameProperty = null;
        _lastStatus = "Disabled: the offline simulation safety patch did not install.";
    }

    /// <summary>
    /// MaxOfflineTime is milliseconds. Clamp to both the game cap and a
    /// conservative 24-hour limit. Display the result before execution.
    /// </summary>
    internal static int GetLimitMinutes(Game game)
    {
        try
        {
            double ms = game.OfflineProgress.Component.MaxOfflineTime;
            if (!double.IsFinite(ms) || ms <= 0) return 0;
            return (int)Math.Min(HardMaxMinutes, Math.Floor(ms / 60_000.0));
        }
        catch (Exception ex)
        {
            TimeSkipMod.Log.Warning("Cannot determine offline-time limit: " + ex.Message);
            return 0;
        }
    }

    internal static bool TrySimulate(Game? game, int minutes, out string reason)
    {
        if (!Available) { reason = "Mod disabled: offline safety patch missing."; return false; }
        if (_busy) { reason = "A Time Skip is already running."; return false; }
        if (game == null || !game.IsSaveLoaded)
        {
            reason = "Load a saved character before simulating time.";
            return false;
        }
        if (game.Flags.IsOffline)
        {
            reason = "The game is already processing offline progress.";
            return false;
        }
        int limit = GetLimitMinutes(game);
        if (limit <= 0)
        {
            reason = "The game does not currently allow offline simulation.";
            return false;
        }
        if (minutes < 1 || minutes > limit)
        {
            reason = $"Choose between 1 and {limit} minutes for this character.";
            return false;
        }

        _requestedGame = game;
        _busy = true;
        _requestPending = true;
        _acknowledged = false;

        try
        {
            // Public game method: hours -> seconds -> OfflineRequested event.
            // Main.GameState handles the event and runs its existing simulator.
            game.RequestOfflineHours(minutes / 60.0);

            if (!_acknowledged)
            {
                _busy = false;
                _requestedGame = null;
                _lastStatus = "The game did not accept the offline request. No skip was confirmed.";
                reason = _lastStatus;
                return false;
            }

            _lastStatus = $"Simulating {FormatDuration(minutes)}. Wait for the game's progress window.";
            reason = _lastStatus;
            TimeSkipMod.Log.Info("Offline replay requested: " + FormatDuration(minutes));
            return true;
        }
        catch (Exception ex)
        {
            // If the original game callback already accepted the request,
            // let its completion handler release Busy instead of guessing.
            if (!_acknowledged)
            {
                _busy = false;
                _requestedGame = null;
            }
            _lastStatus = "Could not start simulation; inspect the game log.";
            reason = _lastStatus;
            TimeSkipMod.Log.Error("RequestOfflineHours failed: " + ex);
            return false;
        }
        finally
        {
            _requestPending = false;
        }
    }

    /// <summary>
    /// Prefix for Main.GameState.RunOfflineProgress, installed manually by
    /// TimeSkipMod.OnLoad. Executes on the main thread before Task.Run.
    /// The game does not mark RequestOfflineHours as offline by itself;
    /// setting that flag prevents other mods' UI callbacks on worker ticks.
    /// </summary>
    public static void BeforeRunOfflineProgress(object __instance)
    {
        if (!_requestPending || _requestedGame == null || _stateGameProperty == null)
            return;
        try
        {
            var currentGame = _stateGameProperty.GetValue(__instance) as Game;
            if (!ReferenceEquals(currentGame, _requestedGame)) return;

            // Called only for our own accepted request, not real sleep/load.
            if (!currentGame.Flags.IsOffline)
                currentGame.OnOfflineProgressStart();
            _acknowledged = true;
        }
        catch (Exception ex)
        {
            TimeSkipMod.Log.Error("Could not mark manual offline replay as offline: " + ex);
        }
    }

    internal static void AfterOfflineProgress(Game game)
    {
        if (!ReferenceEquals(game, _requestedGame)) return;
        _busy = false;
        _requestedGame = null;
        _lastStatus = "Time Skip completed. Review the game's offline summary and save your character.";
        TimeSkipMod.Log.Info("Time Skip completed.");
    }

    internal static string FormatDuration(int minutes)
    {
        if (minutes < 60) return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
        int hours = minutes / 60;
        int remaining = minutes % 60;
        string head = $"{hours} hour{(hours == 1 ? "" : "s")}";
        return remaining == 0 ? head : head + $" {remaining} minute{(remaining == 1 ? "" : "s")}";
    }
}
