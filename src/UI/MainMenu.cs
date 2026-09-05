using Godot;

namespace EpochAeterna.UI;

/// <summary>
/// The screen the game starts on: start a match, options, quit.
/// </summary>
/// <remarks>
/// Deliberately not a scene of its own. The game only ever has one 3D world, and
/// swapping scenes just to show three buttons would mean tearing down and rebuilding
/// the environment, the sun and the graphics settings every time.
/// </remarks>
public sealed partial class MainMenu : CanvasLayer
{
    public event System.Action? StartRequested;
    public event System.Action? OptionsRequested;
    public event System.Action? QuitRequested;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 20;

        var backdrop = new ColorRect { Color = new Color(0.04f, 0.05f, 0.07f, 0.94f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(backdrop);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(centre);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 10);
        centre.AddChild(column);

        Label title = HudTheme.MakeLabel("EPOCH AETERNA", 64, HudTheme.Accent, HorizontalAlignment.Center);
        column.AddChild(title);

        Label subtitle = HudTheme.MakeLabel(
            "Ressourcen sammeln • Basis bauen • Zeitalter aufsteigen • Armee führen",
            16, HudTheme.TextDim, HorizontalAlignment.Center);
        column.AddChild(subtitle);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 36f) });

        AddButton(column, "Spiel starten", () => StartRequested?.Invoke());
        AddButton(column, "Optionen", () => OptionsRequested?.Invoke());
        AddButton(column, "Beenden", () => QuitRequested?.Invoke());
    }

    /// <summary>Buttons wide enough to read as a menu rather than as a toolbar.</summary>
    internal static Button AddButton(BoxContainer parent, string text, System.Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(320f, 46f) };
        button.AddThemeFontSizeOverride("font_size", 18);
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }
}
