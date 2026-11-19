using GamesByMalcs.Melvor2.Components.ActionUntilX;
using Godot;

namespace Melvor2RecipeQueue;

/// <summary>
/// Visual-only upgrade of v0.2.4's tested queue UI. QueueController and all
/// stop-condition interception are unchanged. Only use Godot on the UI thread.
/// </summary>
internal static class QueueUi
{
    private static readonly List<WeakReference<Button>> CounterButtons = new();
    private static int _uiThreadId;
    private static Window? _window;
    private static VBoxContainer? _rows;
    private static Label? _status;
    private static Label? _queueCount;
    private static Button? _run;
    private static StopOptionProviderView? _latestView;

    internal static void AddControls(StopOptionProviderView view, IStopOptionProvider provider)
    {
        if (provider is not ActionUntilXComponent) return;
        System.Threading.Volatile.Write(ref _uiThreadId, System.Environment.CurrentManagedThreadId);
        _latestView = view;
        // The stop view can be reused when navigating between recipes.
        foreach (var node in view.GetChildren())
            if (node.Name.ToString() == "RecipeQueueAddButton") { Refresh(); return; }

        var add = ModChrome.Button("+ Queue", "primary", true);
        add.Name = "RecipeQueueAddButton";
        add.TooltipText = "Add this recipe and a snapshot of its current stop target to the queue";
        add.Pressed += () =>
        {
            try
            {
                var current = typeof(StopOptionProviderView)
                    .GetProperty("OptionProvider", System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.Public |
                                                   System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(view) as IStopOptionProvider;
                if (current != null) QueueController.Add(current);
                else ShowError("No stop-condition provider is bound here.");
            }
            catch (Exception ex) { ShowError("Cannot queue this recipe: " + ex.Message); }
        };
        view.AddChild(add);

        var open = ModChrome.Button("Queue (0)", "secondary", true);
        open.Name = "RecipeQueueOpenButton";
        open.TooltipText = "Open Recipe Queue to start, pause, reorder or remove tasks";
        open.Pressed += () =>
        {
            try { ShowAndRefresh(); }
            catch (Exception ex) { RecipeQueueMod.Log.Error("Queue window error: " + ex); }
        };
        view.AddChild(open);
        CounterButtons.Add(new WeakReference<Button>(open));
        Refresh();
    }

    internal static void ShowError(string message)
    {
        RecipeQueueMod.Log.Warning(message);
        ShowAndRefresh(message);
    }

    internal static void ShowAndRefresh(string? overrideStatus = null)
    {
        if (_latestView == null || !GodotObject.IsInstanceValid(_latestView)) return;
        if (_window == null || !GodotObject.IsInstanceValid(_window))
            CreateWindow(_latestView);
        Refresh(overrideStatus);
        _window?.PopupCentered();
    }

    private static void CreateWindow(StopOptionProviderView view)
    {
        var window = new Window
        {
            Name = "MelvorRecipeQueueWindow",
            Title = "Melvor Idle 2 - Recipe Queue",
            Size = new Vector2I(930, 570),
            MinSize = new Vector2I(700, 430)
        };
        window.CloseRequested += () => window.Hide();
        view.GetTree().Root.AddChild(window);
        _window = window;

        var background = ModChrome.FillWindow(window);
        var content = new VBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 9);
        background.AddChild(content);

        content.AddChild(ModChrome.Label("CRAFTING  /  AUTOMATION", "gold", 13));
        var titleRow = new HBoxContainer();
        content.AddChild(titleRow);
        titleRow.AddChild(ModChrome.Label("Recipe Queue", "normal", 24));
        _queueCount = ModChrome.Label("0 STEPS", "cyan", 14);
        _queueCount.HorizontalAlignment = HorizontalAlignment.Right;
        titleRow.AddChild(_queueCount);

        // Retain both existing instructions, so this is a purely visual update.
        content.AddChild(ModChrome.Label("Each recipe runs until its own captured Stop Condition is met.", "normal", 15, true));
        content.AddChild(ModChrome.Label("Use '+ Queue' on each recipe, then select Start Queue.", "muted", 14, true));

        var listPanel = ModChrome.Container(true);
        listPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        listPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.AddChild(listPanel);
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        listPanel.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _rows.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(_rows);

        var statusPanel = ModChrome.Container(true);
        content.AddChild(statusPanel);
        _status = ModChrome.Label("", "muted", 14, true);
        statusPanel.AddChild(_status);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 9);
        content.AddChild(buttons);
        _run = ModChrome.Button("Start Queue", "primary");
        _run.Pressed += () =>
        {
            if (QueueController.Armed) QueueController.Pause();
            else QueueController.Start();
        };
        buttons.AddChild(_run);
        var clear = ModChrome.Button("Clear Queue", "danger");
        clear.Pressed += QueueController.Clear;
        buttons.AddChild(clear);
        var stretch = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        buttons.AddChild(stretch);
        var close = ModChrome.Button("Close");
        close.Pressed += () => window.Hide();
        buttons.AddChild(close);
    }

    internal static void RefreshAfterGameEvent()
    {
        // Time Skip can process game tick callbacks on a worker thread, even
        // when a generic offline flag was not set. Never update Godot there.
        if (System.Environment.CurrentManagedThreadId != System.Threading.Volatile.Read(ref _uiThreadId))
            return;
        Refresh();
    }

    internal static void Refresh(string? overrideStatus = null)
    {
        int count = QueueController.Count;
        for (int i = CounterButtons.Count - 1; i >= 0; i--)
        {
            if (!CounterButtons[i].TryGetTarget(out var button) || !GodotObject.IsInstanceValid(button))
                CounterButtons.RemoveAt(i);
            else
                button.Text = $"Queue ({count})";
        }
        if (_window == null || !GodotObject.IsInstanceValid(_window)) return;
        if (_queueCount != null && GodotObject.IsInstanceValid(_queueCount))
            _queueCount.Text = $"{count} STEP{(count == 1 ? "" : "S")}";
        if (_run != null && GodotObject.IsInstanceValid(_run))
        {
            _run.Text = QueueController.Armed ? "Pause Queue" : "Start Queue";
            _run.Disabled = !QueueController.Armed && count == 0;
            ModChrome.StyleButton(_run, QueueController.Armed ? "secondary" : "primary");
        }
        if (_status != null && GodotObject.IsInstanceValid(_status))
            _status.Text = overrideStatus ?? QueueController.Status;
        if (_rows == null || !GodotObject.IsInstanceValid(_rows)) return;

        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }
        var steps = QueueController.Snapshot();
        if (steps.Length == 0)
        {
            _rows.AddChild(ModChrome.Label("No queued recipes. Select a recipe and choose + Queue to begin.", "muted", 15, true));
            return;
        }
        for (int i = 0; i < steps.Length; i++)
        {
            int index = i;
            bool running = QueueController.Armed && index == 0;
            var card = ModChrome.Container(true, running);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _rows.AddChild(card);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            card.AddChild(row);

            var number = ModChrome.Label($"{index + 1:00}", "gold", 18);
            number.CustomMinimumSize = new Vector2(38, 0);
            number.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            row.AddChild(number);

            var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            details.AddThemeConstantOverride("separation", 2);
            row.AddChild(details);
            details.AddChild(ModChrome.Label((running ? "RUNNING  •  " : "") + steps[i].Display, "normal", 16, true));
            details.AddChild(ModChrome.Label("STOP  •  " + steps[i].StopDisplay, "gold", 13, true));

            var up = ModChrome.Button("Up", "secondary", true);
            up.CustomMinimumSize = new Vector2(60, 0);
            up.Disabled = index == 0 || (QueueController.Armed && index <= 1);
            up.Pressed += () => QueueController.Move(index, -1);
            row.AddChild(up);
            var down = ModChrome.Button("Down", "secondary", true);
            down.CustomMinimumSize = new Vector2(60, 0);
            down.Disabled = index == steps.Length - 1 || (QueueController.Armed && index == 0);
            down.Pressed += () => QueueController.Move(index, +1);
            row.AddChild(down);
            var remove = ModChrome.Button("Remove", "danger", true);
            remove.Disabled = QueueController.Armed && index == 0;
            remove.Pressed += () => QueueController.RemoveAt(index);
            row.AddChild(remove);
        }
    }
}
