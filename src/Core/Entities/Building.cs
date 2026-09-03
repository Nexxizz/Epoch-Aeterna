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

/// <summary>Gebaeude — im Bau oder fertig.</summary>
public sealed class Building : Entity
{
    public const int MaxQueueLength = 5;

    /// <summary>Sichtbare Baustufen: Fundament, Rohbau, fertig.</summary>
    public const int ConstructionStages = 3;

    public Vector2I Footprint { get; set; } = new(2, 2);
    public int PopulationProvided { get; set; }
    public bool IsDropOffPoint { get; set; }
    public bool CanAdvanceAge { get; set; }
    public bool IsFarm { get; set; }

    // --- Verteidigung ----------------------------------------------------

    public float AttackDamage { get; set; }
    public float AttackRange { get; set; }
    public float AttackCooldownSeconds { get; set; } = 2f;
    public float AttackCooldownLeft { get; set; }
    public DamageType DamageType { get; set; } = DamageType.Ranged;
    public ArmorClass ArmorClass { get; set; } = ArmorClass.Building;
    public float ProjectileSpeed { get; set; } = 26f;
    public float Armor { get; set; }
    public EntityId AttackTarget { get; set; } = EntityId.None;

    // --- Bau -------------------------------------------------------------

    /// <summary>0 bis 1. Unter 1 ist das Gebaeude eine Baustelle und tut nichts.</summary>
    public float ConstructionProgress { get; set; } = 1f;

    public bool IsUnderConstruction => ConstructionProgress < 1f;

    /// <summary>Gesamte Bauarbeit in Sekunden, wenn genau ein Siedler daran arbeitet.</summary>
    public float BuildTimeSeconds { get; set; } = 20f;

    /// <summary>Siedler, die in diesem Tick Hand angelegt haben. Wird jeden Tick neu gezaehlt.</summary>
    public int ActiveBuilders { get; set; }

    /// <summary>0, 1 oder 2 — welches Baustufen-Modell gezeigt wird.</summary>
    public int ConstructionStage => IsUnderConstruction
        ? Mathf.Clamp((int)(ConstructionProgress * ConstructionStages), 0, ConstructionStages - 1)
        : ConstructionStages - 1;

    // --- Produktion ------------------------------------------------------

    /// <summary>Wohin frisch ausgebildete Einheiten laufen. Null = direkt neben dem Gebaeude bleiben.</summary>
    public Vector2? RallyPoint { get; set; }

    public List<ProductionOrder> Queue { get; } = new();

    public ProductionOrder? CurrentOrder => Queue.Count > 0 ? Queue[0] : null;
    public bool QueueIsFull => Queue.Count >= MaxQueueLength;

    /// <summary>Laeuft der Zeitalteraufstieg hier gerade? Sekunden bis zur Fertigstellung.</summary>
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

    /// <summary>Startet als Baustelle: kaum Lebenspunkte, kein Nutzen, bis sie fertig ist.</summary>
    public void BeginConstruction()
    {
        ConstructionProgress = 0f;
        Health = Mathf.Max(1f, MaxHealth * 0.05f);
    }

    /// <summary>Halber Radius der Grundflaeche in Metern — fuer Reichweiten und Andocken.</summary>
    public float FootprintRadius =>
        Mathf.Max(Footprint.X, Footprint.Y) * 0.5f * Pathfinding.NavGrid.CellSize;

    /// <summary>Punkt, an dem eine fertige Einheit erscheint: knapp ausserhalb der Grundflaeche.</summary>
    public Vector2 SpawnPoint() => Position + new Vector2(0f, FootprintRadius + 1.5f);
}
