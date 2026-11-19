using System;
using System.Collections.Generic;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.GameMenus;
using Godot;

namespace Melvor2TimeSkip;

/// <summary>
/// Time Skip's normal selection window plus four direct, one-click shortcuts.
/// Direct shortcuts never open the window unless a request is rejected; they
/// invoke the same validated RequestOfflineHours pipeline as the full UI.
/// No Godot calls from the offline simulation worker.
/// </summary>
internal static class TimeSkipUi
{
    private static GameView? _view;
    private static Game? _game; // supplied by the game's public Bind(Game) argument
    private static CanvasLayer? _canvasLayer;
    private static Control? _overlay;
    private static Button? _launcher;
    private static readonly Dictionary<int, Button> QuickButtons = new();
    private static readonly Dictionary<int, Button> MenuPresets = new();
    private static Window? _window;
    private static Label? _selectedLabel;
    private static Label? _statusLabel;
    private static SpinBox? _customMinutes;
    private static Button? _runButton;
    private static int _selectedMinutes = 15;
    private static bool _longSkipConfirmed;

    internal static void Attach(GameView view, Game? boundGame = null)
    {
        if (!GodotObject.IsInstanceValid(view)) return;
        if (_view != null && !ReferenceEquals(_view, view)) Detach(_view);
        if (boundGame != null) _game = boundGame;
        if (!view.IsInsideTree()) return;
        _view = view;
        if (_canvasLayer != null && GodotObject.IsInstanceValid(_canvasLayer)) return;

        _canvasLayer = new CanvasLayer { Name = "Melvor2TimeSkipCanvas", Layer = 100 };
        view.GetTree().Root.AddChild(_canvasLayer);
        _overlay = new Control
        {
            Name = "Melvor2TimeSkipOverlay",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _canvasLayer.AddChild(_overlay);

        // Fixed to bottom right, matching the old Time Skip location. The four
        // adjacent duration buttons work with one click, without the popup.
        var bar = ModChrome.Container(true);
        bar.Name = "TimeSkipQuickBar";
        bar.AnchorLeft = 1f;
        bar.AnchorRight = 1f;
        bar.AnchorTop = 1f;
        bar.AnchorBottom = 1f;
        bar.OffsetLeft = -442f;
        bar.OffsetRight = -12f;
        bar.OffsetTop = -64f;
        bar.OffsetBottom = -10f;
        bar.MouseFilter = Control.MouseFilterEnum.Stop;
        _overlay.AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        bar.AddChild(row);
        AddQuick(row, "1m", 1);
        AddQuick(row, "5m", 5);
        AddQuick(row, "15m", 15);
        AddQuick(row, "1h", 60);
        _launcher = ModChrome.Button("Time Skip", "primary", true);
        _launcher.Name = "Melvor2TimeSkipButton";
        _launcher.CustomMinimumSize = new Vector2(107, 30);
        _launcher.TooltipText = "Open Time Skip for more durations, custom minutes and status information";
        _launcher.Pressed += Open;
        row.AddChild(_launcher);

        var timer = new Godot.Timer { Name = "TimeSkipQuickBarRefresh", WaitTime = 0.8, Autostart = true };
        timer.Timeout += RefreshQuickButtons;
        _canvasLayer.AddChild(timer);
        RefreshQuickButtons();
    }

    private static void AddQuick(HBoxContainer row, string label, int minutes)
    {
        var button = ModChrome.Button(label, "secondary", true);
        button.CustomMinimumSize = new Vector2(52, 30);
        button.TooltipText = $"Instantly simulate {TimeSkipRuntime.FormatDuration(minutes)}. No menu or confirmation; the game performs every offline tick.";
        button.Pressed += () => RunQuick(minutes);
        row.AddChild(button);
        QuickButtons[minutes] = button;
    }

    private static bool IsReady(int minutes)
    {
        try
        {
            return _view != null && GodotObject.IsInstanceValid(_view) && !_view.GetTree().Paused &&
                   TimeSkipRuntime.Available && !TimeSkipRuntime.Busy &&
                   _game?.IsSaveLoaded == true && _game.Flags.IsOffline == false &&
                   TimeSkipRuntime.GetLimitMinutes(_game) >= minutes;
        }
        catch { return false; }
    }

    private static void RefreshQuickButtons()
    {
        foreach (var pair in QuickButtons)
        {
            if (GodotObject.IsInstanceValid(pair.Value))
                pair.Value.Disabled = !IsReady(pair.Key);
        }
    }

    private static void RunQuick(int minutes)
    {
        try
        {
            if (!IsReady(minutes))
            {
                Open();
                Refresh("Time Skip is not currently available. Load a character and wait for offline processing to finish.");
                return;
            }
            if (!TimeSkipRuntime.TrySimulate(_game, minutes, out var message))
            {
                Open();
                Refresh(message);
            }
            else
            {
                _longSkipConfirmed = false;
                _window?.Hide();
                RefreshQuickButtons();
            }
        }
        catch (Exception ex)
        {
            TimeSkipMod.Log.Error("Time Skip quick button failed: " + ex);
            Open();
            Refresh("Quick skip failed. Check the game log for details.");
        }
    }

    internal static void Detach(GameView view)
    {
        if (!ReferenceEquals(_view, view)) return;
        if (_window != null && GodotObject.IsInstanceValid(_window))
        {
            _window.Hide();
            _window.QueueFree();
        }
        if (_canvasLayer != null && GodotObject.IsInstanceValid(_canvasLayer))
            _canvasLayer.QueueFree();
        QuickButtons.Clear();
        MenuPresets.Clear();
        _window = null;
        _canvasLayer = null;
        _overlay = null;
        _launcher = null;
        _selectedLabel = null;
        _statusLabel = null;
        _customMinutes = null;
        _runButton = null;
        _view = null;
        _game = null;
        _longSkipConfirmed = false;
    }

    private static void Open()
    {
        try
        {
            if (_view == null || !GodotObject.IsInstanceValid(_view)) return;
            if (_window == null || !GodotObject.IsInstanceValid(_window))
                CreateWindow(_view);
            Refresh();
            _window?.PopupCentered();
        }
        catch (Exception ex) { TimeSkipMod.Log.Error("Cannot open Time Skip window: " + ex); }
    }

    private static void CreateWindow(GameView view)
    {
        var window = new Window
        {
            Name = "Melvor2TimeSkipWindow",
            Title = "Melvor Idle 2 - Time Skip",
            Size = new Vector2I(770, 505),
            MinSize = new Vector2I(640, 445)
        };
        window.CloseRequested += () => window.Hide();
        view.GetTree().Root.AddChild(window);
        _window = window;

        var panel = ModChrome.FillWindow(window);
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);

        content.AddChild(ModChrome.Label("UTILITY  /  TIME CONTROL", "gold", 13));
        content.AddChild(ModChrome.Label("Time Skip", "normal", 24));
        content.AddChild(ModChrome.Label("On-demand offline simulation (not live speed-up)", "cyan", 15));
        content.AddChild(ModChrome.Label(
            "Uses the game's own ticks. Materials, stop goals, combat and passive skills follow normal game rules.",
            "normal", 14, true));

        var presetCard = ModChrome.Container(true);
        content.AddChild(presetCard);
        var presetSection = new VBoxContainer();
        presetSection.AddThemeConstantOverride("separation", 7);
        presetCard.AddChild(presetSection);
        presetSection.AddChild(ModChrome.Label("CHOOSE A DURATION", "gold", 13));
        var presets = new HBoxContainer();
        presets.AddThemeConstantOverride("separation", 7);
        presetSection.AddChild(presets);
        AddPreset(presets, "1 min", 1);
        AddPreset(presets, "5 min", 5);
        AddPreset(presets, "15 min", 15);
        AddPreset(presets, "30 min", 30);
        AddPreset(presets, "1 hour", 60);
        AddPreset(presets, "4 hours", 240);

        var customCard = ModChrome.Container(true);
        content.AddChild(customCard);
        var customRow = new HBoxContainer();
        customRow.AddThemeConstantOverride("separation", 9);
        customCard.AddChild(customRow);
        customRow.AddChild(ModChrome.Label("Custom minutes (1–1440)", "normal", 15));
        _customMinutes = new SpinBox
        {
            MinValue = 1,
            MaxValue = TimeSkipRuntime.HardMaxMinutes,
            Step = 1,
            Rounded = true,
            Value = _selectedMinutes,
            CustomMinimumSize = new Vector2(130, 0)
        };
        customRow.AddChild(_customMinutes);
        var chooseCustom = ModChrome.Button("Select custom", "secondary", true);
        chooseCustom.Pressed += () => Select((int)_customMinutes.Value);
        customRow.AddChild(chooseCustom);

        _selectedLabel = ModChrome.Label("", "cyan", 14, true);
        content.AddChild(_selectedLabel);
        content.AddChild(ModChrome.Label(
            "Long skips can take considerable real time and cannot be cancelled here. Crafting may stop if materials run out.",
            "muted", 13, true));

        var statusCard = ModChrome.Container(true);
        statusCard.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(statusCard);
        _statusLabel = ModChrome.Label("", "muted", 14, true);
        statusCard.AddChild(_statusLabel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 9);
        content.AddChild(actions);
        _runButton = ModChrome.Button("Simulate selected time", "primary");
        _runButton.Pressed += RunSelected;
        actions.AddChild(_runButton);
        var fill = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actions.AddChild(fill);
        var cancel = ModChrome.Button("Close");
        cancel.Pressed += () => window.Hide();
        actions.AddChild(cancel);
    }

    private static void AddPreset(HBoxContainer row, string label, int minutes)
    {
        var button = ModChrome.Button(label, "secondary", true);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.Pressed += () => Select(minutes);
        row.AddChild(button);
        MenuPresets[minutes] = button;
    }

    private static void Select(int minutes)
    {
        _selectedMinutes = Math.Clamp(minutes, 1, TimeSkipRuntime.HardMaxMinutes);
        if (_customMinutes != null && GodotObject.IsInstanceValid(_customMinutes))
            _customMinutes.Value = _selectedMinutes;
        _longSkipConfirmed = false;
        Refresh();
    }

    private static void Refresh(string? message = null)
    {
        if (_view == null || !GodotObject.IsInstanceValid(_view)) return;
        int gameLimit = _game == null ? 0 : TimeSkipRuntime.GetLimitMinutes(_game);
        bool enabled = IsReady(_selectedMinutes);

        foreach (var pair in MenuPresets)
            if (GodotObject.IsInstanceValid(pair.Value))
                ModChrome.StyleButton(pair.Value, pair.Key == _selectedMinutes ? "primary" : "secondary", true);
        if (_selectedLabel != null && GodotObject.IsInstanceValid(_selectedLabel))
            _selectedLabel.Text = $"Selected: {TimeSkipRuntime.FormatDuration(_selectedMinutes)}. " +
                $"Your current per-request limit: {TimeSkipRuntime.FormatDuration(gameLimit)}.";
        if (_statusLabel != null && GodotObject.IsInstanceValid(_statusLabel))
            _statusLabel.Text = message ?? TimeSkipRuntime.LastStatus;
        if (_runButton != null && GodotObject.IsInstanceValid(_runButton))
        {
            _runButton.Disabled = !enabled || _selectedMinutes > gameLimit;
            _runButton.Text = _longSkipConfirmed ? "CONFIRM long time skip" : "Simulate selected time";
        }
    }

    private static void RunSelected()
    {
        try
        {
            if (_view == null || !GodotObject.IsInstanceValid(_view)) return;
            if (_view.GetTree().Paused)
            {
                Refresh("The game is paused or is already running offline progress.");
                return;
            }
            if (_selectedMinutes > 240 && !_longSkipConfirmed)
            {
                _longSkipConfirmed = true;
                Refresh("Large time skip selected. Press CONFIRM long time skip to proceed.");
                return;
            }
            if (!TimeSkipRuntime.TrySimulate(_game, _selectedMinutes, out var response))
            {
                _longSkipConfirmed = false;
                Refresh(response);
                return;
            }
            _longSkipConfirmed = false;
            _window?.Hide();
            RefreshQuickButtons();
        }
        catch (Exception ex)
        {
            TimeSkipMod.Log.Error("Time Skip button failed: " + ex);
            Refresh("An unexpected error occurred. See the game's log.");
        }
    }
}
