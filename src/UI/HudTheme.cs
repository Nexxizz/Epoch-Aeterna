using Godot;

namespace EpochAeterna.UI;

/// <summary>
/// The shared look of the HUD: colours, panel frames and the layout grid.
/// </summary>
/// <remarks>
/// Everything is built from code rather than from a <c>.theme</c> resource. The HUD
/// is assembled in C# anyway, and a single place holding the numbers is easier to
/// change than a resource that has to be opened in the editor.
///
/// The layout constants describe the 1600x900 base resolution. Panels are anchored
/// to the screen edges, so a wider window stretches the gaps rather than the panels.
/// </remarks>
public static class HudTheme
{
    public static readonly Color Panel = new(0.08f, 0.09f, 0.11f, 0.90f);
    public static readonly Color PanelBorder = new(0.44f, 0.37f, 0.24f, 0.95f);
    public static readonly Color Text = new(0.93f, 0.92f, 0.88f);
    public static readonly Color TextDim = new(0.70f, 0.69f, 0.64f);
    public static readonly Color Accent = new(0.92f, 0.80f, 0.38f);
    public static readonly Color Warning = new(0.96f, 0.66f, 0.28f);
    public static readonly Color Danger = new(0.95f, 0.40f, 0.34f);
    public static readonly Color Good = new(0.54f, 0.86f, 0.48f);

    // --- Layout ----------------------------------------------------------

    public const float Margin = 8f;
    public const float TopBarHeight = 44f;

    /// <summary>Height of the panel row along the bottom edge.</summary>
    public const float BottomHeight = 188f;

    public const float MinimapSize = 188f;
    public const float SelectionWidth = 520f;
    public const float CommandWidth = 160f;

    /// <summary>Width of the contextual build and training panel on the right.</summary>
    public const float ActionWidth = 692f;

    /// <summary>Left edge of the selection panel, measured from the left screen edge.</summary>
    public const float SelectionLeft = Margin + MinimapSize + Margin;

    /// <summary>Right edge of the action panel, measured from the right screen edge.</summary>
    public const float ActionRight = -(Margin + CommandWidth + Margin);

    // --- Building blocks -------------------------------------------------

    /// <summary>
    /// The standard frame: dark, slightly transparent, with a warm border.
    /// </summary>
    /// <param name="opaque">
    /// Menus block the game behind them and are drawn solid; HUD panels stay
    /// translucent so the map remains readable underneath.
    /// </param>
    public static StyleBoxFlat PanelStyle(bool opaque = false)
    {
        var style = new StyleBoxFlat
        {
            BgColor = opaque ? new Color(Panel, 1f) : Panel,
            BorderColor = PanelBorder,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.SetContentMarginAll(10f);
        return style;
    }

    public static void ApplyPanel(PanelContainer panel, bool opaque = false) =>
        panel.AddThemeStyleboxOverride("panel", PanelStyle(opaque));

    public static Label MakeLabel(string text, int fontSize = 13, Color? color = null,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var label = new Label { Text = text, HorizontalAlignment = alignment };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? Text);
        return label;
    }

    /// <summary>Section heading — small, wide-spaced, in the accent colour.</summary>
    public static Label MakeTitle(string text, HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        Label label = MakeLabel(text.ToUpperInvariant(), 13, Accent, alignment);
        label.AddThemeConstantOverride("line_spacing", 0);
        return label;
    }

    /// <summary>A bar without a percentage — used for health, construction and training.</summary>
    public static ProgressBar MakeBar(Color fill, float height = 12f)
    {
        var background = new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.05f, 0.85f) };
        background.SetCornerRadiusAll(2);

        var foreground = new StyleBoxFlat { BgColor = fill };
        foreground.SetCornerRadiusAll(2);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0f, height),
        };
        bar.AddThemeStyleboxOverride("background", background);
        bar.AddThemeStyleboxOverride("fill", foreground);
        return bar;
    }

    /// <summary>Green while healthy, amber below half, red below a quarter.</summary>
    public static Color HealthColor(float fraction) =>
        fraction > 0.5f ? Good : fraction > 0.25f ? Warning : Danger;

    /// <summary>Anchors a control to the bottom edge with a fixed height.</summary>
    public static void AnchorBottom(Control control, float left, float right, float height,
        float bottomOffset = Margin)
    {
        control.AnchorTop = 1f;
        control.AnchorBottom = 1f;
        control.OffsetTop = -(height + bottomOffset);
        control.OffsetBottom = -bottomOffset;
        control.OffsetLeft = left;
        control.OffsetRight = right;
    }
}
