using Godot;

namespace Melvor2TimeSkip;

/// <summary>
/// Small self-contained visual language derived from the game's charcoal panels,
/// gold section headings and turquoise action accents. Only built-in Godot nodes.
/// Embedded in each assembly so the mods still work with the original loader.
/// </summary>
internal static class ModChrome
{
    internal static readonly Color Gold = new(0.91f, 0.72f, 0.39f);
    internal static readonly Color Cyan = new(0.26f, 0.78f, 0.82f);
    internal static readonly Color Text = new(0.95f, 0.96f, 0.94f);
    internal static readonly Color Muted = new(0.67f, 0.73f, 0.73f);
    private static readonly Color Base = new(0.055f, 0.079f, 0.091f, 0.97f);
    private static readonly Color Surface = new(0.084f, 0.135f, 0.157f, 0.96f);
    private static readonly Color Card = new(0.099f, 0.182f, 0.209f, 0.94f);

    internal static StyleBoxFlat MakePanel(bool card = false, bool emphasized = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = emphasized ? Card : card ? Surface : Base,
            BorderColor = emphasized ? Gold : new Color(0.19f, 0.31f, 0.33f, 0.96f),
            BorderWidthLeft = emphasized ? 3 : 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = card ? 11 : 17,
            ContentMarginRight = card ? 11 : 17,
            ContentMarginTop = card ? 9 : 15,
            ContentMarginBottom = card ? 9 : 15
        };
        return style;
    }

    internal static PanelContainer Container(bool card = false, bool emphasized = false)
    {
        var result = new PanelContainer();
        result.AddThemeStyleboxOverride("panel", MakePanel(card, emphasized));
        return result;
    }

    internal static PanelContainer FillWindow(Window window)
    {
        var panel = Container();
        panel.AnchorLeft = 0f;
        panel.AnchorTop = 0f;
        panel.AnchorRight = 1f;
        panel.AnchorBottom = 1f;
        window.AddChild(panel);
        return panel;
    }

    internal static Label Label(string text, string tone = "normal", int size = 15, bool wrap = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        label.AddThemeColorOverride("font_color", tone == "gold" ? Gold : tone == "cyan" ? Cyan : tone == "muted" ? Muted : Text);
        label.AddThemeFontSizeOverride("font_size", size);
        if (size >= 18 || tone == "gold")
        {
            const string boldPath = "res://Assets/Fonts/Default/Static/Default_Bold.ttf";
            if (ResourceLoader.Exists(boldPath))
                label.AddThemeFontOverride("font", ResourceLoader.Load<Font>(boldPath));
        }
        return label;
    }

    internal static Button Button(string text, string kind = "secondary", bool compact = false)
    {
        var button = new Button { Text = text };
        StyleButton(button, kind, compact);
        return button;
    }

    internal static void StyleButton(Button button, string kind = "secondary", bool compact = false)
    {
        var primary = kind == "primary";
        var warning = kind == "danger";
        Color bg = primary ? new Color(0.065f, 0.37f, 0.43f) : warning ? new Color(0.35f, 0.105f, 0.105f) : new Color(0.13f, 0.225f, 0.26f);
        Color border = primary ? Cyan : warning ? new Color(0.72f, 0.30f, 0.25f) : new Color(0.28f, 0.40f, 0.41f);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(bg, border, compact));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(bg.Lightened(0.14f), Gold, compact));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(bg.Darkened(0.18f), Cyan, compact));
        button.AddThemeStyleboxOverride("focus", ButtonStyle(new Color(0f, 0f, 0f, 0f), Gold, compact));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.12f, 0.14f, 0.15f, 0.62f), new Color(0.22f, 0.25f, 0.26f), compact));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Text);
        button.AddThemeColorOverride("font_pressed_color", Text);
        button.AddThemeColorOverride("font_disabled_color", Muted.Darkened(0.23f));
        button.AddThemeFontSizeOverride("font_size", compact ? 13 : 15);
    }

    private static StyleBoxFlat ButtonStyle(Color fill, Color stroke, bool compact)
    {
        int vertical = compact ? 4 : 7;
        return new StyleBoxFlat
        {
            BgColor = fill,
            BorderColor = stroke,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = compact ? 8 : 12,
            ContentMarginRight = compact ? 8 : 12,
            ContentMarginTop = vertical,
            ContentMarginBottom = vertical
        };
    }
}
