using GamesByMalcs.Melvor2.MainMenu;
using Godot;
using HarmonyLib;

namespace MelvorModLoader;

/// <summary>
/// Shows that the game runs modded: a panel in the main menu's bottom left with the loader version and a scrollable
/// list of the loaded mods (description as tooltip).
/// </summary>
internal static class MainMenuInfo
{
    private const float Margin = 16;
    private const float Width = 200;
    private const float MaxListHeight = 160;
    private static readonly Color s_accent = new(0.72f, 0.52f, 1f);
    private static readonly Color s_dim = new(0.7f, 0.7f, 0.7f);

    public static void Apply(Harmony harmony) =>
        harmony.Patch(AccessTools.Method(typeof(MainMenuView), nameof(MainMenuView._Ready)),
            postfix: new HarmonyMethod(typeof(MainMenuInfo), nameof(Postfix)));

    private static void Postfix(MainMenuView __instance)
    {
        try
        {
            var panel = CreatePanel();
            __instance.AddChild(panel);
            // Above the menu itself, below the overlays that follow it (pause menu, popups).
            if (__instance.GetNodeOrNull("MainMargins") is { } menu)
            {
                __instance.MoveChild(panel, menu.GetIndex() + 1);
            }
        }
        catch (Exception e)
        {
            Loader.Log.Error($"Main menu mod list failed: {e}");
        }
    }

    private static PanelContainer CreatePanel()
    {
        var panel = new PanelContainer { Name = "ModLoaderInfo" };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.85f),
            BorderColor = s_accent with { A = 0.6f },
            BorderWidthLeft = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });
        // Anchored to the bottom left corner, growing up and to the right with its content.
        panel.AnchorLeft = panel.AnchorRight = 0;
        panel.AnchorTop = panel.AnchorBottom = 1;
        panel.OffsetLeft = panel.OffsetRight = Margin;
        panel.OffsetTop = panel.OffsetBottom = -Margin;
        panel.GrowHorizontal = Control.GrowDirection.End;
        panel.GrowVertical = Control.GrowDirection.Begin;

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        panel.AddChild(content);

        var header = new Label { Text = $"ModLoader version {Loader.Version}" };
        header.AddThemeColorOverride("font_color", s_accent);
        if (ResourceLoader.Exists("res://Assets/Fonts/Default/Static/Default_Bold.ttf"))
        {
            header.AddThemeFontOverride("font", ResourceLoader.Load<Font>("res://Assets/Fonts/Default/Static/Default_Bold.ttf"));
        }
        content.AddChild(header);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(Width, 0),
        };
        content.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 2);
        scroll.AddChild(list);
        // A ScrollContainer doesn't size itself to its content: grow with the list up to MaxListHeight, then scroll.
        list.MinimumSizeChanged += () =>
            scroll.CustomMinimumSize = new Vector2(Width, Math.Min(list.GetCombinedMinimumSize().Y, MaxListHeight));

        if (Loader.Mods.Count == 0)
        {
            list.AddChild(CreateRow("No mods loaded", null));
        }
        foreach (var mod in Loader.Mods)
        {
            var manifest = mod.Manifest;
            var row = CreateRow(manifest.DisplayName + (manifest.Version is { } version ? $"  v{version}" : ""),
                manifest.Description);
            list.AddChild(row);
        }
        return panel;
    }

    private static Label CreateRow(string text, string? tooltip)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            TooltipText = tooltip ?? "",
            // Pass: shows the tooltip but still lets the wheel scroll the list.
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        if (tooltip == null)
        {
            label.AddThemeColorOverride("font_color", s_dim);
        }
        return label;
    }
}
