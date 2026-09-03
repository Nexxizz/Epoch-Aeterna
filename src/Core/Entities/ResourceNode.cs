using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// A resource deposit on the map. Belongs to nobody (OwnerId 0) and
/// disappears once it has been cleared out.
/// </summary>
public sealed class ResourceNode : Entity
{
    public ResourceType Resource { get; set; } = ResourceType.Wood;

    public int Remaining { get; set; }
    public int TotalAmount { get; set; }

    public int MaxGatherers { get; set; } = 4;
    public float GatherRateFactor { get; set; } = 1f;
    public bool BlocksMovement { get; set; } = true;

    /// <summary>How many settlers work here right now — limits the crowd around one tree.</summary>
    public int ActiveGatherers { get; set; }

    public bool IsDepleted => Remaining <= 0;

    public bool HasFreeSlot => ActiveGatherers < MaxGatherers;

    /// <summary>0 to 1 — the view uses it to shrink trees visibly as they are felled.</summary>
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

    /// <summary>Takes at most what is left and reports the actual yield.</summary>
    public int Extract(int wanted)
    {
        int taken = wanted < Remaining ? wanted : Remaining;
        Remaining -= taken;
        return taken;
    }
}
