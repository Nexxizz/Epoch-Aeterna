using Godot;

namespace EpochAeterna.Game;

/// <summary>
/// Einstiegspunkt des Spiels. In Phase 0 nur ein Smoke-Test, der belegt, dass
/// die C#-Assembly geladen und der Forward+-Renderer initialisiert wird.
/// Ab Phase 1 haengt hier das Bootstrapping von SimulationWorld und Match auf.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        GD.Print("=== Epoch Aeterna ===");
        GD.Print($"Godot-Version   : {Engine.GetVersionInfo()["string"]}");
        GD.Print($"Renderer        : {ProjectSettings.GetSetting("rendering/renderer/rendering_method")}");
        GD.Print($".NET-Assembly   : {typeof(Main).Assembly.GetName().Name}");
        GD.Print("Bootstrap OK - Phase 0 steht.");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            GetTree().Quit();
        }
    }
}
