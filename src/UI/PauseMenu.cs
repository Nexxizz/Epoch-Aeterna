using Godot;

namespace EpochAeterna.UI;

/// <summary>
/// The Escape menu during a match. Halts the simulation while it is open.
/// </summary>
/// <remarks>
/// It does not use <c>SceneTree.Paused</c>: the simulation already has an explicit
/// speed control in <c>SimulationRunner</c>, and pausing the whole tree would also
/// freeze the menu's own animations and input handling.
/// </remarks>
public sealed partial class PauseMenu : CanvasLayer
{
    public event System.Action? ResumeRequested;
    public event System.Action? OptionsRequested;
    public event System.Action? RestartRequested;
    public event System.Action? MainMenuRequested;
    public event System.Action? QuitRequested;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 20;
        Visible = false;

        var backdrop = new ColorRect { Color = new Color(0.03f, 0.04f, 0.06f, 0.72f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(backdrop);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(centre);

        var panel = new PanelContainer();
        HudTheme.ApplyPanel(panel, opaque: true);
        centre.AddChild(panel);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(column);

        column.AddChild(HudTheme.MakeLabel("PAUSE", 34, HudTheme.Accent, HorizontalAlignment.Center));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 12f) });

        MainMenu.AddButton(column, "Weiter", () => ResumeRequested?.Invoke());
        MainMenu.AddButton(column, "Optionen", () => OptionsRequested?.Invoke());
        MainMenu.AddButton(column, "Neues Spiel", () => RestartRequested?.Invoke());
        MainMenu.AddButton(column, "Hauptmenü", () => MainMenuRequested?.Invoke());
        MainMenu.AddButton(column, "Beenden", () => QuitRequested?.Invoke());
    }
}
