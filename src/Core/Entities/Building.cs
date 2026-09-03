using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>Ein Posten in der Ausbildungswarteschlange eines Gebaeudes.</summary>
public sealed class ProductionOrder
{
    public required string UnitDefinitionId { get; init; }
    public required float TotalSeconds { get; init; }
    public float ElapsedSeconds { get; set; }

    public float Progress => TotalSeconds > 0f ? Mathf.Clamp(ElapsedSeconds / TotalSeconds, 0f, 1f) : 1f;
    public bool IsComplete => ElapsedSeconds >= TotalSeconds;
}

/// <summary>Gebaeude. Im MVP immer fertig gebaut — Baustellen kommen in Phase 3.2 dazu.</summary>
public sealed class Building : Entity
{
    public const int MaxQueueLength = 5;

    public Vector2I Footprint { get; set; } = new(2, 2);
    public int PopulationProvided { get; set; }
    public bool IsDropOffPoint { get; set; }
    public bool CanAdvanceAge { get; set; }

    /// <summary>Wohin frisch ausgebildete Einheiten laufen. Null = direkt neben dem Gebaeude bleiben.</summary>
    public Vector2? RallyPoint { get; set; }

    public List<ProductionOrder> Queue { get; } = new();

    public ProductionOrder? CurrentOrder => Queue.Count > 0 ? Queue[0] : null;
    public bool QueueIsFull => Queue.Count >= MaxQueueLength;

    public void ApplyDefinition(BuildingDefinition definition)
    {
        base.ApplyDefinition(definition);
        Footprint = definition.Footprint;
        PopulationProvided = definition.PopulationProvided;
        IsDropOffPoint = definition.IsDropOffPoint;
        CanAdvanceAge = definition.CanAdvanceAge;
    }

    /// <summary>Punkt, an dem eine fertige Einheit erscheint: knapp ausserhalb der Grundflaeche.</summary>
    public Vector2 SpawnPoint()
    {
        float offset = Mathf.Max(Footprint.X, Footprint.Y) * 0.5f + 1.5f;
        return Position + new Vector2(0f, offset);
    }
}
