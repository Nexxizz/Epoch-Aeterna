using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Bewegt Einheiten auf ihr Ziel zu und dreht sie in Laufrichtung.
/// </summary>
/// <remarks>
/// Bewusst noch geradlinig, ohne Wegfindung und ohne Kollision — Phase 2.4 ersetzt
/// die Zielbestimmung durch A* mit Pfadglaettung und lokaler Ausweichbewegung.
/// Die Schnittstelle nach aussen (Order + MoveTarget) bleibt dabei unveraendert.
/// </remarks>
public sealed class MovementSystem : ISimulationSystem
{
    /// <summary>Ab dieser Naehe zum Ziel gilt die Bewegung als beendet.</summary>
    private const float ArrivalThreshold = 0.15f;

    /// <summary>Innerhalb dieser Distanz wird linear abgebremst, damit Einheiten nicht ueberschiessen.</summary>
    private const float SlowdownDistance = 1.2f;

    public string Name => "Movement";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.Order != UnitOrder.Move) continue;

            Vector2 toTarget = unit.MoveTarget - unit.Position;
            float distance = toTarget.Length();

            if (distance <= ArrivalThreshold)
            {
                unit.Position = unit.MoveTarget;
                unit.Stop();
                continue;
            }

            Vector2 direction = toTarget / distance;

            float speed = unit.MoveSpeed;
            if (distance < SlowdownDistance) speed *= distance / SlowdownDistance;

            float step = speed * deltaSeconds;
            unit.Position += direction * Mathf.Min(step, distance);

            TurnTowards(unit, direction, deltaSeconds);
        }
    }

    /// <summary>Dreht die Einheit begrenzt schnell in die Zielrichtung, statt sie umspringen zu lassen.</summary>
    private static void TurnTowards(Unit unit, Vector2 direction, float deltaSeconds)
    {
        // Godot-Konvention: -Z ist "vorne". Die Sim rechnet auf XZ, daher atan2(x, -y).
        float desired = Mathf.Atan2(direction.X, -direction.Y);
        float maxStep = unit.TurnSpeedRadians * deltaSeconds;
        unit.Rotation = RotateToward(unit.Rotation, desired, maxStep);
    }

    private static float RotateToward(float current, float target, float maxDelta)
    {
        float difference = Mathf.Wrap(target - current, -Mathf.Pi, Mathf.Pi);
        if (Mathf.Abs(difference) <= maxDelta) return target;
        return current + Mathf.Sign(difference) * maxDelta;
    }
}
