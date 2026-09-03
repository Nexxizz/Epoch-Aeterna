using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>One entry in a building's training queue.</summary>
public sealed class ProductionOrder
{
    public required string UnitDefinitionId { get; init; }
    public required float TotalSeconds { get; init; }
    public float ElapsedSeconds { get; set; }

    public float Progress => TotalSeconds > 0f ? Mathf.Clamp(ElapsedSeconds / TotalSeconds, 0f, 1f) : 1f;
    public bool IsComplete => ElapsedSeconds >= TotalSeconds;
}

/// <summary>A building — under construction or finished.</summary>
public sealed class Building : Entity
{
    public const int MaxQueueLength = 5;

    /// <summary>Visible construction stages: foundation, shell, finished.</summary>
    public const int ConstructionStages = 3;

    public Vector2I Footprint { get; set; } = new(2, 2);
    public int PopulationProvided { get; set; }
    public bool IsDropOffPoint { get; set; }
    public bool CanAdvanceAge { get; set; }
    public bool IsFarm { get; set; }

    // --- Defence ---------------------------------------------------------

    public float AttackDamage { get; set; }
    public float AttackRange { get; set; }
    public float AttackCooldownSeconds { get; set; } = 2f;
    public float AttackCooldownLeft { get; set; }
    public DamageType DamageType { get; set; } = DamageType.Ranged;
    public ArmorClass ArmorClass { get; set; } = ArmorClass.Building;
    public float ProjectileSpeed { get; set; } = 26f;
    public float Armor { get; set; }
    public EntityId AttackTarget { get; set; } = EntityId.None;

    // --- Construction ----------------------------------------------------

    /// <summary>0 to 1. Below 1 the building is a construction site and does nothing.</summary>
    public float ConstructionProgress { get; set; } = 1f;

    public bool IsUnderConstruction => ConstructionProgress < 1f;

    /// <summary>Total build work in seconds when exactly one settler works on it.</summary>
    public float BuildTimeSeconds { get; set; } = 20f;

    /// <summary>Settlers who worked here this tick. Recounted every tick.</summary>
    public int ActiveBuilders { get; set; }

    /// <summary>0, 1 or 2 — which construction stage model is shown.</summary>
    public int ConstructionStage => IsUnderConstruction
        ? Mathf.Clamp((int)(ConstructionProgress * ConstructionStages), 0, ConstructionStages - 1)
        : ConstructionStages - 1;

    // --- Production ------------------------------------------------------

    /// <summary>Where freshly trained units walk. Null means they stay beside the building.</summary>
    public Vector2? RallyPoint { get; set; }

    public List<ProductionOrder> Queue { get; } = new();

    public ProductionOrder? CurrentOrder => Queue.Count > 0 ? Queue[0] : null;
    public bool QueueIsFull => Queue.Count >= MaxQueueLength;

    /// <summary>Is an age advance running here? Seconds until it completes.</summary>
    public float AgeResearchLeft { get; set; }

    public bool IsResearchingAge => AgeResearchLeft > 0f;

    public void ApplyDefinition(BuildingDefinition definition)
    {
        base.ApplyDefinition(definition);
        Footprint = definition.Footprint;
        PopulationProvided = definition.PopulationProvided;
        IsDropOffPoint = definition.IsDropOffPoint;
        CanAdvanceAge = definition.CanAdvanceAge;
        IsFarm = definition.IsFarm;
        BuildTimeSeconds = definition.BuildTimeSeconds;

        AttackDamage = definition.AttackDamage;
        AttackRange = definition.AttackRange;
        AttackCooldownSeconds = definition.AttackCooldownSeconds;
        DamageType = definition.DamageType;
        ArmorClass = definition.ArmorClass;
        ProjectileSpeed = definition.ProjectileSpeed;
        Armor = definition.Armor;
    }

    /// <summary>Starts as a construction site: barely any health, no use, until finished.</summary>
    public void BeginConstruction()
    {
        ConstructionProgress = 0f;
        Health = Mathf.Max(1f, MaxHealth * 0.05f);
    }

    /// <summary>Half the footprint radius in metres — used for ranges and for docking.</summary>
    public float FootprintRadius =>
        Mathf.Max(Footprint.X, Footprint.Y) * 0.5f * Pathfinding.NavGrid.CellSize;

    /// <summary>Where a finished unit appears: just outside the footprint.</summary>
    public Vector2 SpawnPoint() => Position + new Vector2(0f, FootprintRadius + 1.5f);
}
