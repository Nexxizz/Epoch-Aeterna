using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Makes units follow their path and turns them to face the direction of travel.
/// </summary>
/// <remarks>
/// Responsible for *every* unit with a path, not only for plain movement orders:
/// a settler on the way to a tree and a warrior on the way to an enemy walk the same
/// way. What happens on arrival is decided by whichever system owns the task —
/// this one simply stops at the destination.
/// </remarks>
public sealed class MovementSystem : ISimulationSystem
{
    /// <summary>Within this distance a waypoint counts as reached.</summary>
    private const float WaypointThreshold = 0.35f;

    /// <summary>Precision at the final target — tighter than for intermediate waypoints.</summary>
    private const float ArrivalThreshold = 0.18f;

    /// <summary>Within this distance of the final target the unit decelerates.</summary>
    private const float SlowdownDistance = 1.5f;

    private readonly NavGrid _grid;

    public string Name => "Movement";

    public MovementSystem(NavGrid grid) => _grid = grid;

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.NeedsPath) continue;

            if (!unit.HasPath)
            {
                // Only a plain movement order ends here. Gathering, building and fighting
                // have their own follow-up steps and are driven by their own systems.
                if (unit.Order is UnitOrder.Move or UnitOrder.AttackMove) FinishOrder(unit);
                continue;
            }

            AdvanceAlongPath(unit, deltaSeconds);
        }
    }

    private void AdvanceAlongPath(Unit unit, float deltaSeconds)
    {
        Vector2 waypoint = unit.CurrentWaypoint;
        bool isFinalWaypoint = unit.PathIndex == unit.Path.Count - 1;

        Vector2 toWaypoint = waypoint - unit.Position;
        float distance = toWaypoint.Length();

        float threshold = isFinalWaypoint ? ArrivalThreshold : WaypointThreshold;
        if (distance <= threshold)
        {
            unit.PathIndex++;
            if (!unit.HasPath)
            {
                unit.Position = waypoint;
                if (unit.Order is UnitOrder.Move or UnitOrder.AttackMove) FinishOrder(unit);
            }
            return;
        }

        Vector2 direction = toWaypoint / distance;

        float speed = unit.MoveSpeed;
        if (isFinalWaypoint && distance < SlowdownDistance) speed *= distance / SlowdownDistance;

        // The route was checked once, but buildings may have blocked it since.
        Vector2 next = unit.Position + direction * Mathf.Min(speed * deltaSeconds, distance);
        Vector2I cell = _grid.WorldToCell(next);

        if (!_grid.IsPassable(cell.X, cell.Y, unit.Clearance))
        {
            RequestNewPath(unit);
            return;
        }

        unit.Position = next;
        TurnTowards(unit, direction, deltaSeconds);
    }

    /// <summary>Destination reached — either start the next Shift target or stop.</summary>
    private static void FinishOrder(Unit unit)
    {
        if (unit.AdvanceToQueuedTarget()) return;
        unit.Stop();
    }

    private static void RequestNewPath(Unit unit)
    {
        unit.Path.Clear();
        unit.PathIndex = 0;
        unit.NeedsPath = true;
    }

    /// <summary>Turns the unit towards its heading at a limited rate, rather than snapping.</summary>
    private static void TurnTowards(Unit unit, Vector2 direction, float deltaSeconds)
    {
        // Godot convention: -Z is "forward". The sim works on XZ, hence atan2(x, -y).
        float desired = Mathf.Atan2(direction.X, -direction.Y);
        float maxStep = unit.TurnSpeedRadians * deltaSeconds;

        float difference = Mathf.Wrap(desired - unit.Rotation, -Mathf.Pi, Mathf.Pi);
        unit.Rotation = Mathf.Abs(difference) <= maxStep
            ? desired
            : unit.Rotation + Mathf.Sign(difference) * maxStep;
    }
}
