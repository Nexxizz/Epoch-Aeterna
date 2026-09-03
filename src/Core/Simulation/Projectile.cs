using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// A projectile in flight — an arrow or a sling stone.
/// </summary>
/// <remarks>
/// Deliberately not an entity: projectiles live for a second or two and would burden
/// the registry and the view manager with constant registering and unregistering. They
/// sit in a plain list instead, which the display reads every frame.
/// </remarks>
public sealed class Projectile
{
    public required int OwnerPlayerId { get; init; }
    public required EntityId TargetId { get; init; }
    public required Vector2 Origin { get; init; }
    public required float Damage { get; init; }
    public required DamageType DamageType { get; init; }
    public required float Speed { get; init; }

    public Vector2 Position { get; set; }

    /// <summary>Last known target position — the projectile flies on even if the target dies.</summary>
    public Vector2 TargetPosition { get; set; }

    public bool HasLanded { get; set; }

    /// <summary>0 to 1 along the trajectory — the display derives the arc from it.</summary>
    public float FlightProgress { get; set; }

    /// <summary>Total distance travelled, so the arc stays independent of the target.</summary>
    public float TotalDistance { get; set; }
}
