using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Works through open path requests — with a fixed budget per tick.
/// </summary>
/// <remarks>
/// The budget is the reason this system exists instead of running the search
/// directly in the command: if fifty units receive an order at once, that would
/// blow the tick. Instead they set off staggered over the first few ticks,
/// gestaffelt los, was optisch ohnehin natuerlicher wirkt.
/// </remarks>
public sealed class PathfindingSystem : ISimulationSystem
{
    /// <summary>Path searches per tick. At 20 Hz that is up to 160 paths per second.</summary>
    public int RequestsPerTick { get; set; } = 8;

    private readonly AStarPathfinder _pathfinder;
    private readonly List<Vector2> _scratch = new();

    /// <summary>Requests from the last tick, for the profiling overlay.</summary>
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
                // Unreachable: drop the order rather than letting the unit search forever
                // search forever.
                unit.Stop();
            }
        }

        LastProcessed = processed;
        PendingRequests = pending;
    }
}
