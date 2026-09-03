using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Laesst Einheiten ihrem Pfad folgen und dreht sie in Laufrichtung.
/// </summary>
public sealed class MovementSystem : ISimulationSystem
{
    /// <summary>Ab dieser Naehe gilt ein Zwischenpunkt als erreicht.</summary>
    private const float WaypointThreshold = 0.35f;

    /// <summary>Genauigkeit am Endziel — enger als bei Zwischenpunkten.</summary>
    private const float ArrivalThreshold = 0.18f;

    /// <summary>Innerhalb dieser Distanz zum Endziel wird abgebremst.</summary>
    private const float SlowdownDistance = 1.5f;

    private readonly NavGrid _grid;

    public string Name => "Movement";

    public MovementSystem(NavGrid grid) => _grid = grid;

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.Order != UnitOrder.Move || unit.NeedsPath) continue;

            if (!unit.HasPath)
            {
                FinishOrder(unit);
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
                FinishOrder(unit);
            }
            return;
        }

        Vector2 direction = toWaypoint / distance;

        float speed = unit.MoveSpeed;
        if (isFinalWaypoint && distance < SlowdownDistance) speed *= distance / SlowdownDistance;

        // Der Weg wurde einmal geprueft, aber Gebaeude koennen ihn seither versperren.
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

    /// <summary>Endziel erreicht — entweder das naechste Shift-Ziel starten oder anhalten.</summary>
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

    /// <summary>Dreht die Einheit begrenzt schnell in die Zielrichtung, statt sie umspringen zu lassen.</summary>
    private static void TurnTowards(Unit unit, Vector2 direction, float deltaSeconds)
    {
        // Godot-Konvention: -Z ist "vorne". Die Sim rechnet auf XZ, daher atan2(x, -y).
        float desired = Mathf.Atan2(direction.X, -direction.Y);
        float maxStep = unit.TurnSpeedRadians * deltaSeconds;

        float difference = Mathf.Wrap(desired - unit.Rotation, -Mathf.Pi, Mathf.Pi);
        unit.Rotation = Mathf.Abs(difference) <= maxStep
            ? desired
            : unit.Rotation + Mathf.Sign(difference) * maxStep;
    }
}
