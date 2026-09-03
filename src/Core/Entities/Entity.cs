using Godot;
using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Base of every object on the map. Holds simulation state only —
/// no Godot nodes, no rendering. Display is handled by a separate
/// EntityView that follows this entity by its <see cref="Id"/> alone.
/// </summary>
public abstract class Entity
{
    public EntityId Id { get; internal set; }

    /// <summary>Owning player id. 0 = neutral (trees, rocks, wildlife).</summary>
    public int OwnerId { get; init; }

    public string DefinitionId { get; init; } = string.Empty;

    /// <summary>Position on the XZ plane. The Y height comes from the terrain and is not simulation state.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Facing in radians around the Y axis.</summary>
    public float Rotation { get; set; }

    public float Health { get; set; }
    public float MaxHealth { get; set; } = 1f;
    public float VisionRange { get; set; } = 12f;

    public bool IsAlive => Health > 0f;
    public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;

    // --- Interpolation ---------------------------------------------------
    // The sim runs at 20 Hz while rendering runs at 60+ FPS. Views blend
    // between the state before and after the tick so movement reads as smooth.

    public Vector2 PreviousPosition { get; private set; }
    public float PreviousRotation { get; private set; }

    /// <summary>Called at the start of every tick, before systems change the state.</summary>
    public void CaptureInterpolationSnapshot()
    {
        PreviousPosition = Position;
        PreviousRotation = Rotation;
    }

    /// <summary>Makes before and after identical — on spawn and teleport, where interpolation would be wrong.</summary>
    public void ResetInterpolation()
    {
        PreviousPosition = Position;
        PreviousRotation = Rotation;
    }

    public void ApplyDefinition(EntityDefinition definition)
    {
        MaxHealth = definition.MaxHealth;
        Health = definition.MaxHealth;
        VisionRange = definition.VisionRange;
    }

    public override string ToString() => $"{GetType().Name} {Id} ({DefinitionId}, P{OwnerId})";
}
