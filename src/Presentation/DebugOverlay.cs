using System.Text;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Presentation;

/// <summary>
/// Vorlaeufige Zustandsanzeige, bis das echte HUD in Phase 6 entsteht.
/// </summary>
public sealed partial class DebugOverlay : CanvasLayer
{
    private readonly Label _label = new();
    private readonly StringBuilder _builder = new();

    private SimulationWorld? _world;
    private SimulationRunner? _runner;
    private SelectionController? _selection;
    private PathfindingSystem? _pathfinding;

    public override void _Ready()
    {
        Layer = 2;
        _label.Position = new Vector2(16, 12);
        _label.AddThemeFontSizeOverride("font_size", 15);
        _label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("outline_size", 5);
        AddChild(_label);
    }

    public void Attach(SimulationWorld world, SimulationRunner runner, SelectionController selection)
    {
        _world = world;
        _runner = runner;
        _selection = selection;

        foreach (ISimulationSystem system in world.Systems)
        {
            if (system is PathfindingSystem pathfinding) _pathfinding = pathfinding;
        }
    }

    public override void _Process(double delta)
    {
        if (_world is null || _runner is null) return;

        _builder.Clear();
        _builder.AppendLine("Epoch Aeterna — Phase 2 (Welt, Kamera, Steuerung)");

        _builder.Append("Tick ").Append(_world.CurrentTick)
                .Append("   Zeit ").Append(_world.ElapsedSeconds.ToString("0.0")).Append(" s")
                .Append("   FPS ").Append(Engine.GetFramesPerSecond())
                .Append(_runner.IsPaused ? "   [PAUSE]" : string.Empty)
                .AppendLine();

        _builder.Append("Entities ").Append(_world.Entities.Count)
                .Append("   Ausgewaehlt ").Append(_selection?.Selection.Count ?? 0)
                .Append("   Befehle ").Append(_world.Commands.TotalExecuted);

        if (_pathfinding is not null)
        {
            _builder.Append("   Wegsuchen/Tick ").Append(_pathfinding.LastProcessed)
                    .Append("   offen ").Append(_pathfinding.PendingRequests);
        }
        _builder.AppendLine();
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
        _builder.AppendLine("Linksklick waehlt   Ziehen waehlt mehrere   Doppelklick waehlt den Typ   Shift ergaenzt");
        _builder.AppendLine("Rechtsklick befiehlt   Shift+Rechtsklick haengt an   S stoppt   Strg+Zahl merkt, Zahl ruft ab");
        _builder.AppendLine("Pfeiltasten und Bildschirmrand schieben   Q/E drehen   Mausrad zoomt   Mittlere Maustaste zieht");
        _builder.AppendLine("Pos1 springt zur Basis   F1/F2 bilden aus   Leertaste pausiert   ESC beendet");

        _label.Text = _builder.ToString();
    }
}
