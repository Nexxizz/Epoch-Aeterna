using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Ein Ressourcenvorkommen auf der Karte. Gehoert niemandem (OwnerId 0) und
/// verschwindet, wenn es leergeraeumt ist.
/// </summary>
public sealed class ResourceNode : Entity
{
    public ResourceType Resource { get; set; } = ResourceType.Wood;

    public int Remaining { get; set; }
    public int TotalAmount { get; set; }

    public int MaxGatherers { get; set; } = 4;
    public float GatherRateFactor { get; set; } = 1f;
    public bool BlocksMovement { get; set; } = true;

    /// <summary>Wie viele Siedler gerade hier arbeiten — begrenzt das Gedraenge am Baum.</summary>
    public int ActiveGatherers { get; set; }

    public bool IsDepleted => Remaining <= 0;

    public bool HasFreeSlot => ActiveGatherers < MaxGatherers;

    /// <summary>0 bis 1 — die Darstellung laesst Baeume damit sichtbar schrumpfen.</summary>
    public float RemainingFraction => TotalAmount > 0 ? Remaining / (float)TotalAmount : 0f;

    public void ApplyDefinition(ResourceNodeDefinition definition)
    {
        base.ApplyDefinition(definition);
        Resource = definition.Resource;
        TotalAmount = definition.TotalAmount;
        Remaining = definition.TotalAmount;
        MaxGatherers = definition.MaxGatherers;
        GatherRateFactor = definition.GatherRateFactor;
        BlocksMovement = definition.BlocksMovement;
    }

    /// <summary>Entnimmt hoechstens so viel, wie noch da ist, und meldet den tatsaechlichen Ertrag.</summary>
    public int Extract(int wanted)
    {
        int taken = wanted < Remaining ? wanted : Remaining;
        Remaining -= taken;
        return taken;
    }
}
