using System.Reflection;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.GameMenus;
using Godot;
using HarmonyLib;

namespace MelvorModLoader;

/// <summary>
/// Non-invasive HUD for the original, unchanged Multitasking v0.3.2 mod.
/// This class ONLY reads Game.ActionManager.ActiveActions and displays the
/// native game textures. It never starts, stops, or patches skill mechanics.
/// Available on any in-game skill/page because it lives in a root CanvasLayer.
/// </summary>
internal static class MultitaskingActivityHud
{
    private sealed record Activity(string Skill, string Detail, string Id,
        Texture2D? SkillIcon, Texture2D? ActivityIcon);

    private static GameView? _view;
    private static Game? _game;
    private static CanvasLayer? _layer;
    private static Control? _overlay;
    private static PanelContainer? _panel;
    private static HBoxContainer? _header;
    private static Label? _title;
    private static Label? _hint;
    private static ScrollContainer? _scroll;
    private static GridContainer? _grid;
    private static Button? _toggle;
    private static bool _collapsed;
    private static bool _dragging;
    private static bool _wasDragged;
    private static Vector2 _lastDraggedPosition;
    private static string _lastSignature = "";

    internal static void Apply(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(GameView), nameof(GameView._Ready)),
            postfix: new HarmonyMethod(typeof(MultitaskingActivityHud), nameof(ReadyPostfix)));
        harmony.Patch(AccessTools.Method(typeof(GameView), nameof(GameView.Bind), new[] { typeof(Game) }),
            postfix: new HarmonyMethod(typeof(MultitaskingActivityHud), nameof(BindPostfix)));
        harmony.Patch(AccessTools.Method(typeof(GameView), nameof(GameView.Unbind)),
            postfix: new HarmonyMethod(typeof(MultitaskingActivityHud), nameof(UnbindPostfix)));
        Loader.Log.Info("Multitasking Activity HUD enabled (original multitasking DLL unchanged).");
    }

    private static void ReadyPostfix(GameView __instance)
    {
        try { Attach(__instance); }
        catch (Exception e) { Loader.Log.Warning("Multitasking HUD Ready hook: " + e.Message); }
    }

    private static void BindPostfix(GameView __instance, Game __0)
    {
        try { Attach(__instance, __0); }
        catch (Exception e) { Loader.Log.Warning("Multitasking HUD Bind hook: " + e.Message); }
    }

    private static void UnbindPostfix(GameView __instance)
    {
        try { Detach(__instance); }
        catch (Exception e) { Loader.Log.Warning("Multitasking HUD Unbind hook: " + e.Message); }
    }

    private static void Attach(GameView view, Game? game = null)
    {
        if (!GodotObject.IsInstanceValid(view)) return;
        if (_view != null && !ReferenceEquals(_view, view)) Detach(_view);
        if (game != null) _game = game;
        if (!view.IsInsideTree()) return;
        _view = view;
        if (_layer != null && GodotObject.IsInstanceValid(_layer)) return;

        _layer = new CanvasLayer { Name = "MelvorMultitaskingHudLayer", Layer = 105 };
        view.GetTree().Root.AddChild(_layer);
        _overlay = new Control
        {
            Name = "MelvorMultitaskingHudOverlay",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _layer.AddChild(_overlay);
        _panel = ModChrome.Container(true);
        _panel.Name = "MultitaskingActiveActions";
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        _panel.Size = new Vector2(484, 185);
        _overlay.AddChild(_panel);

        var layout = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(layout);

        _header = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = "Drag this header to reposition the Multitasking HUD"
        };
        _header.AddThemeConstantOverride("separation", 8);
        _header.GuiInput += HeaderInput;
        layout.AddChild(_header);
        var accent = ModChrome.Label("●", "cyan", 15);
        accent.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        accent.MouseFilter = Control.MouseFilterEnum.Ignore;
        _header.AddChild(accent);
        _title = ModChrome.Label("MULTITASKING", "gold", 15);
        _title.MouseFilter = Control.MouseFilterEnum.Ignore;
        _header.AddChild(_title);
        _toggle = ModChrome.Button("Hide", "secondary", true);
        _toggle.TooltipText = "Collapse or expand the activity tiles";
        _toggle.Pressed += Toggle;
        _header.AddChild(_toggle);

        _hint = ModChrome.Label("Skill and current activity icons • Drag header to move", "muted", 12, true);
        layout.AddChild(_hint);

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(452, 96)
        };
        layout.AddChild(_scroll);
        _grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 7);
        _grid.AddThemeConstantOverride("v_separation", 7);
        _scroll.AddChild(_grid);

        if (_wasDragged) ClampAndPlace(_lastDraggedPosition);
        else PositionDefault();
        var timer = new Godot.Timer { Name = "MultitaskingHudRefresh", WaitTime = 0.75, Autostart = true };
        timer.Timeout += Refresh;
        _layer.AddChild(timer);
        Refresh();
    }

    private static void Detach(GameView view)
    {
        if (!ReferenceEquals(_view, view)) return;
        if (_layer != null && GodotObject.IsInstanceValid(_layer)) _layer.QueueFree();
        _layer = null;
        _overlay = null;
        _panel = null;
        _header = null;
        _title = null;
        _hint = null;
        _scroll = null;
        _grid = null;
        _toggle = null;
        _view = null;
        _game = null;
        _dragging = false;
        _lastSignature = "";
    }

    private static void HeaderInput(InputEvent input)
    {
        if (_header == null || _panel == null || _overlay == null) return;
        if (input is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
        {
            _dragging = mouse.Pressed;
            _header.AcceptEvent();
        }
        else if (input is InputEventMouseMotion motion && _dragging)
        {
            _wasDragged = true;
            ClampAndPlace(_panel.Position + motion.Relative);
            _lastDraggedPosition = _panel.Position;
            _header.AcceptEvent();
        }
    }

    private static void PositionDefault()
    {
        if (_panel == null || _view == null) return;
        var size = _view.GetViewport().GetVisibleRect().Size;
        ClampAndPlace(new Vector2(size.X - _panel.Size.X - 16f, 76f));
    }

    private static void ClampAndPlace(Vector2 position)
    {
        if (_panel == null || _view == null) return;
        var viewport = _view.GetViewport().GetVisibleRect().Size;
        _panel.Position = new Vector2(
            Mathf.Clamp(position.X, 6, Mathf.Max(6, viewport.X - _panel.Size.X - 6)),
            Mathf.Clamp(position.Y, 6, Mathf.Max(6, viewport.Y - _panel.Size.Y - 6)));
    }

    private static void Toggle()
    {
        _collapsed = !_collapsed;
        if (_scroll != null) _scroll.Visible = !_collapsed;
        if (_hint != null) _hint.Visible = !_collapsed;
        if (_toggle != null) _toggle.Text = _collapsed ? "Show" : "Hide";
        UpdatePanelSize(_grid?.GetChildCount() ?? 0);
    }

    private static void UpdatePanelSize(int count)
    {
        if (_panel == null || _scroll == null) return;
        int rows = Math.Max(1, (count + 1) / 2);
        int scrollHeight = Math.Min(315, rows * 66 + 4);
        _scroll.CustomMinimumSize = new Vector2(452, scrollHeight);
        _panel.Size = new Vector2(484, _collapsed ? 53f : 83f + scrollHeight);
        if (_wasDragged) ClampAndPlace(_lastDraggedPosition);
        else PositionDefault();
    }

    private static void Refresh()
    {
        try
        {
            if (_view == null || !GodotObject.IsInstanceValid(_view) || _game == null ||
                _grid == null || !GodotObject.IsInstanceValid(_grid)) return;
            if (!_game.IsSaveLoaded || _game.Flags.IsOffline || _view.GetTree().Paused) return;

            var activities = new List<Activity>();
            foreach (var action in _game.ActionManager.ActiveActions)
            {
                if (!action.IsStarted) continue;
                var skill = action.Entity.DisplayName.Message;
                var desc = action.CurrentDescription;
                var detail = DescriptionName(desc);
                if (string.IsNullOrWhiteSpace(detail)) detail = "Working";
                var activityIcon = desc?.Icon;
                var skillIcon = action.Entity.Icon;
                activities.Add(new Activity(skill, detail, action.Entity.Id,
                    GodotObject.IsInstanceValid(skillIcon) ? skillIcon : null,
                    GodotObject.IsInstanceValid(activityIcon) ? activityIcon : null));
            }
            var signature = string.Join("||", activities.Select(a => a.Id + "|" + a.Detail + "|" +
                    (a.ActivityIcon == null ? "-" : a.ActivityIcon.GetInstanceId().ToString())));
            if (signature == _lastSignature) return;
            _lastSignature = signature;
            if (_title != null) _title.Text = $"MULTITASKING  •  {activities.Count} ACTIVE";

            foreach (var child in _grid.GetChildren())
            {
                _grid.RemoveChild(child);
                child.QueueFree();
            }
            if (activities.Count == 0)
            {
                var nothing = ModChrome.Label("No skills running", "muted", 14);
                _grid.AddChild(nothing);
            }
            else
            {
                foreach (var activity in activities)
                    AddTile(activity);
            }
            UpdatePanelSize(activities.Count);
        }
        catch (Exception e)
        {
            // This HUD is optional and cannot be allowed to interfere with
            // the game's progress tick or the already-tested Multitasking mod.
            Loader.Log.Warning("Multitasking activity HUD refresh skipped: " + e.Message);
        }
    }

    private static string DescriptionName(object? description)
    {
        if (description == null) return "";
        try
        {
            var localized = description.GetType()
                .GetProperty("DisplayName", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(description);
            if (localized == null) return "";
            return localized.GetType().GetProperty("Message", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(localized) as string ?? "";
        }
        catch { return ""; }
    }

    private static void AddTile(Activity activity)
    {
        if (_grid == null) return;
        var tile = ModChrome.Container(true);
        tile.CustomMinimumSize = new Vector2(215, 55);
        tile.TooltipText = activity.Skill + " — " + activity.Detail;
        _grid.AddChild(tile);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 5);
        tile.AddChild(h);
        AddIcon(h, activity.SkillIcon, 29);
        if (activity.ActivityIcon != null && !ReferenceEquals(activity.ActivityIcon, activity.SkillIcon))
            AddIcon(h, activity.ActivityIcon, 25);
        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 1);
        h.AddChild(names);
        var skill = ModChrome.Label(activity.Skill, "gold", 13);
        skill.MouseFilter = Control.MouseFilterEnum.Ignore;
        skill.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        names.AddChild(skill);
        var detail = ModChrome.Label(activity.Detail, "normal", 12);
        detail.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        detail.MouseFilter = Control.MouseFilterEnum.Ignore;
        names.AddChild(detail);
    }

    private static void AddIcon(HBoxContainer parent, Texture2D? texture, int size)
    {
        if (texture == null) return;
        var icon = new TextureRect
        {
            Texture = texture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(icon);
    }
}
