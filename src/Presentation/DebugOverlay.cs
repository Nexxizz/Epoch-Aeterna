using System.Text;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Vorlaeufige Zustandsanzeige, bis das echte HUD in Phase 6 entsteht.
/// Zeigt, dass Tick, Ressourcenkonto, Bevoelkerung und Produktion zusammenspielen.
/// </summary>
public sealed partial class DebugOverlay : CanvasLayer
{
    private readonly Label _label = new();
    private readonly StringBuilder _builder = new();

    private SimulationWorld? _world;
    private SimulationRunner? _runner;

    public override void _Ready()
    {
        _label.Position = new Vector2(16, 12);
        _label.AddThemeFontSizeOverride("font_size", 15);
        _label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("outline_size", 5);
        AddChild(_label);
    }

    public void Attach(SimulationWorld world, SimulationRunner runner)
    {
        _world = world;
        _runner = runner;
    }

    public override void _Process(double delta)
    {
        if (_world is null || _runner is null) return;

        _builder.Clear();
        _builder.AppendLine("Epoch Aeterna — Phase 1 (Architektur-Grundgeruest)");
        _builder.Append("Tick ").Append(_world.CurrentTick)
                .Append("   Zeit ").Append(_world.ElapsedSeconds.ToString("0.0")).Append(" s")
                .Append("   FPS ").Append(Engine.GetFramesPerSecond())
                .Append("   Alpha ").Append(_runner.InterpolationAlpha.ToString("0.00"))
                .Append(_runner.IsPaused ? "   [PAUSE]" : string.Empty)
                .AppendLine();
        _builder.Append("Entities ").Append(_world.Entities.Count)
                .Append("   Befehle gesamt ").Append(_world.Commands.TotalExecuted)
                .AppendLine();
        _builder.AppendLine();

        foreach (Player player in _world.Players)
        {
            _builder.Append(player.Name).Append("  |  ");
            foreach (ResourceType type in ResourceTypes.All)
            {
                _builder.Append(ResourceTypes.DisplayName(type)).Append(' ')
                        .Append(player.GetResource(type)).Append("   ");
            }
            _builder.Append("Bev ").Append(player.Population).Append('/').Append(player.PopulationCap)
                    .Append("   Zeitalter ")
                    .Append(_world.Definitions.GetAge(player.AgeIndex)?.DisplayName ?? "?")
                    .AppendLine();
        }

        _builder.AppendLine();
        _builder.AppendLine("1 = Siedler ausbilden    2 = Spaeher ausbilden    Leertaste = Pause    ESC = Beenden");

        _label.Text = _builder.ToString();
    }
}
