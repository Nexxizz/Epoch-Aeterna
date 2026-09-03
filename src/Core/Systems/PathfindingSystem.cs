using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Arbeitet offene Wegeanfragen ab — mit festem Budget pro Tick.
/// </summary>
/// <remarks>
/// Das Budget ist der Grund, warum dieses System existiert, statt die Suche direkt
/// im Befehl auszufuehren: Wenn fuenfzig Einheiten gleichzeitig einen Befehl bekommen,
/// wuerde das den Tick sprengen. Stattdessen laufen sie in den ersten Ticks
/// gestaffelt los, was optisch ohnehin natuerlicher wirkt.
/// </remarks>
public sealed class PathfindingSystem : ISimulationSystem
{
    /// <summary>Wegesuchen pro Tick. Bei 20 Hz also bis zu 160 Pfade pro Sekunde.</summary>
    public int RequestsPerTick { get; set; } = 8;

    private readonly AStarPathfinder _pathfinder;
    private readonly List<Vector2> _scratch = new();

    /// <summary>Anfragen des letzten Ticks, fuers Profiling-Overlay.</summary>
    public int LastProcessed { get; private set; }

    public int PendingRequests { get; private set; }

    public string Name => "Pathfinding";

    public PathfindingSystem(NavGrid grid) => _pathfinder = new AStarPathfinder(grid);

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        int processed = 0;
        int pending = 0;

        foreach (Unit unit in world.Entities.Units)
        {
            if (!unit.NeedsPath) continue;

            if (processed >= RequestsPerTick)
            {
                pending++;
                continue;
            }

            processed++;

            if (_pathfinder.TryFindPath(unit.Position, unit.MoveTarget, unit.Clearance, _scratch))
            {
                unit.SetPath(_scratch);
            }
            else
            {
                // Unerreichbar: Befehl fallen lassen, statt die Einheit endlos
                // neu suchen zu lassen.
                unit.Stop();
            }
        }

        LastProcessed = processed;
        PendingRequests = pending;
    }
}
