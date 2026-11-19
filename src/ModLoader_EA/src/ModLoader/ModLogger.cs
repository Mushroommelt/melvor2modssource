using Godot;

namespace MelvorModLoader;

/// <summary>Writes "[prefix] message" to the game's console and log file (%APPDATA%\Games By Malcs\Melvor Idle 2\logs).</summary>
/// <remarks>
/// Deliberately avoids GD.PushError/PushWarning: mods load before Godot's ProjectSettings singleton exists, and
/// those calls dereference it, crashing the game with an access violation. GD.Print/PrintErr are safe.
/// </remarks>
public sealed class ModLogger(string prefix)
{
    private readonly string _prefix = $"[{prefix}] ";

    public void Info(string message) => GD.Print(_prefix + message);

    public void Warning(string message) => GD.PrintErr($"WARNING: {_prefix}{message}");

    public void Error(string message) => GD.PrintErr($"ERROR: {_prefix}{message}");
}
