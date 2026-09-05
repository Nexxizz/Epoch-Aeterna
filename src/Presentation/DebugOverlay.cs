using System.Text;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Presentation;

/// <summary>
/// Diagnostic readout: tick, frame rate, entity and path counts.
/// </summary>
/// <remarks>
/// Since the HUD of phase 6 shows resources, population and age properly, this
/// overlay is a development tool rather than an interface. It stays hidden until
/// the diagnosis key switches it on.
/// </remarks>
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
        Visible = false;

        // Below the resource bar, which owns the top edge.
        _label.Position = new Vector2(16, 64);
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        _label.AddThemeConstantOverride("outline_size", 5);
        AddChild(_label);
    }

    /// <summary>Switches the readout on and off — bound to the diagnosis key.</summary>
    public void Toggle() => Visible = !Visible;

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
        if (!Visible || _world is null || _runner is null) return;

        _builder.Clear();
        _builder.AppendLine("Epoch Aeterna — diagnostics");

        _builder.Append("Tick ").Append(_world.CurrentTick)
                .Append("   Time ").Append((_world.ElapsedSeconds / 60f).ToString("0.0")).Append(" min")
                .Append("   FPS ").Append(Engine.GetFramesPerSecond())
                .Append("   Speed ").Append(_runner.TimeScale.ToString("0.0")).Append('x')
                .Append(_runner.IsPaused ? "   [PAUSED]" : string.Empty)
                .AppendLine();

        _builder.Append("Entities ").Append(_world.Entities.Count)
                .Append(" (Resources ").Append(_world.Entities.ResourceNodes.Count).Append(')')
                .Append("   Selected ").Append(_selection?.Selection.Count ?? 0)
                .Append("   Projectiles ").Append(_projectiles?.ActiveCount ?? 0);

        if (_pathfinding is not null)
        {
            _builder.Append("   Path requests ").Append(_pathfinding.LastProcessed)
                    .Append('/').Append(_pathfinding.PendingRequests).Append(" pending");
        }
        _builder.AppendLine();
        _builder.AppendLine();

        foreach (Player player in _world.Players)
        {
            _builder.Append(player.Name).Append(player.IsDefeated ? " (defeated)" : string.Empty).Append("  |  ");

            foreach (ResourceType type in ResourceTypes.All)
            {
                _builder.Append(ResourceTypes.DisplayName(type)).Append(' ')
                        .Append(player.GetResource(type)).Append("   ");
            }

            _builder.Append("Pop ").Append(player.Population).Append('/').Append(player.PopulationCap)
                    .Append("   ")
                    .Append(_world.Definitions.GetAge(player.AgeIndex)?.DisplayName ?? "?")
                    .AppendLine();
        }

        _builder.AppendLine();

        if (_placement is { IsPlacing: true })
        {
            _builder.AppendLine(">> Choose a building site — left-click to place, Shift to place more, right-click to cancel");
        }
        else
        {
            _builder.AppendLine("Right-click: ground = move, resource = gather, construction site = build, enemy = attack");
            _builder.AppendLine("A Attack Move   S Stop   H Hold   D Defensive   Ctrl+number assign, number recall");
            _builder.AppendLine("F1 Settler   F2 Scout   F3 Spearman   F4 Advance Age");
            _builder.AppendLine("B House   N Storehouse   M Barracks   K Farm   T Watchtower   R Archery Range   Delete Demolish");
        }

        _builder.AppendLine("Arrow keys/edge pan   Q/E rotate   Mouse wheel zoom   Home base   +/- speed   Space pause");

        _label.Text = _builder.ToString();
    }
}
