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
    private BuildPlacementController? _placement;
    private PathfindingSystem? _pathfinding;
    private ProjectileSystem? _projectiles;

    private int _localPlayerId = 1;

    public override void _Ready()
    {
        Layer = 2;
        _label.Position = new Vector2(16, 12);
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("outline_size", 5);
        AddChild(_label);
    }

    public void Attach(SimulationWorld world, SimulationRunner runner, SelectionController selection,
        BuildPlacementController placement, int localPlayerId)
    {
        _world = world;
        _runner = runner;
        _selection = selection;
        _placement = placement;
        _localPlayerId = localPlayerId;

        _pathfinding = world.GetSystem<PathfindingSystem>();
        _projectiles = world.GetSystem<ProjectileSystem>();
    }

    public override void _Process(double delta)
    {
        if (_world is null || _runner is null) return;

        _builder.Clear();
        _builder.AppendLine("Epoch Aeterna — Phase 3 (Wirtschaft, Bauen, Kampf, Zeitalter)");

        _builder.Append("Tick ").Append(_world.CurrentTick)
                .Append("   Zeit ").Append((_world.ElapsedSeconds / 60f).ToString("0.0")).Append(" min")
                .Append("   FPS ").Append(Engine.GetFramesPerSecond())
                .Append("   Tempo ").Append(_runner.TimeScale.ToString("0.0")).Append('x')
                .Append(_runner.IsPaused ? "   [PAUSE]" : string.Empty)
                .AppendLine();

        _builder.Append("Entities ").Append(_world.Entities.Count)
                .Append(" (Vorkommen ").Append(_world.Entities.ResourceNodes.Count).Append(')')
                .Append("   Ausgewaehlt ").Append(_selection?.Selection.Count ?? 0)
                .Append("   Geschosse ").Append(_projectiles?.ActiveCount ?? 0);

        if (_pathfinding is not null)
        {
            _builder.Append("   Wegsuchen ").Append(_pathfinding.LastProcessed)
                    .Append('/').Append(_pathfinding.PendingRequests).Append(" offen");
        }
        _builder.AppendLine();
        _builder.AppendLine();

        foreach (Player player in _world.Players)
        {
            _builder.Append(player.Name).Append(player.IsDefeated ? " (besiegt)" : string.Empty).Append("  |  ");

            foreach (ResourceType type in ResourceTypes.All)
            {
                _builder.Append(ResourceTypes.DisplayName(type)).Append(' ')
                        .Append(player.GetResource(type)).Append("   ");
            }

            _builder.Append("Bev ").Append(player.Population).Append('/').Append(player.PopulationCap)
                    .Append("   ")
                    .Append(_world.Definitions.GetAge(player.AgeIndex)?.DisplayName ?? "?")
                    .AppendLine();
        }

        _builder.AppendLine();

        if (_placement is { IsPlacing: true })
        {
            _builder.AppendLine(">> Bauplatz waehlen — Linksklick setzt, Shift setzt weitere, Rechtsklick bricht ab");
        }
        else
        {
            _builder.AppendLine("Rechtsklick: Boden = gehen, Vorkommen = sammeln, Baustelle = bauen, Gegner = angreifen");
            _builder.AppendLine("A Angriffsbewegung   S Stopp   H Halten   D Defensiv   Strg+Zahl merkt, Zahl ruft ab");
            _builder.AppendLine("F1 Siedler   F2 Spaeher   F3 Speerkaempfer   F4 Aufsteigen");
            _builder.AppendLine("B Haus   N Lagerhaus   M Kaserne   K Farm   T Turm   R Schiessstand   Entf Abriss");
        }

        _builder.AppendLine("Pfeiltasten/Rand schieben   Q/E drehen   Mausrad zoomt   Pos1 Basis   +/- Tempo   Leertaste Pause");

        _label.Text = _builder.ToString();
    }
}
