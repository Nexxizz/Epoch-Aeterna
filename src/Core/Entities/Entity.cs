using Godot;
using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Basis aller Objekte auf der Karte. Enthaelt ausschliesslich Simulationszustand —
/// keine Godot-Nodes, kein Rendering. Die Darstellung uebernimmt eine separate
/// EntityView, die dieser Entity nur ueber ihre <see cref="Id"/> folgt.
/// </summary>
public abstract class Entity
{
    public EntityId Id { get; internal set; }

    /// <summary>Besitzer-Spieler-ID. 0 = Gaia/neutral (Baeume, Felsen, Wild).</summary>
    public int OwnerId { get; init; }

    public string DefinitionId { get; init; } = string.Empty;

    /// <summary>Position auf der XZ-Ebene. Die Y-Hoehe liefert das Terrain, sie ist kein Simulationszustand.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Blickrichtung in Radiant um die Y-Achse.</summary>
    public float Rotation { get; set; }

    public float Health { get; set; }
    public float MaxHealth { get; set; } = 1f;
    public float VisionRange { get; set; } = 12f;

    public bool IsAlive => Health > 0f;
    public float HealthFraction => MaxHealth > 0f ? Health / MaxHealth : 0f;

    // --- Interpolation ---------------------------------------------------
    // Die Sim laeuft mit 20 Hz, gerendert wird mit 60+ FPS. Views blenden
    // zwischen dem Zustand vor und nach dem Tick, damit die Bewegung glatt wirkt.

    public Vector2 PreviousPosition { get; private set; }
    public float PreviousRotation { get; private set; }

    /// <summary>Wird zu Beginn jedes Ticks aufgerufen, bevor Systeme den Zustand aendern.</summary>
    public void CaptureInterpolationSnapshot()
    {
        PreviousPosition = Position;
        PreviousRotation = Rotation;
    }

    /// <summary>Setzt Vor- und Nachzustand gleich — bei Spawn und Teleport, sonst wird interpoliert.</summary>
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
