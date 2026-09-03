using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Blueprint of a resource deposit: tree, quarry, gold vein, berry bush.</summary>
[GlobalClass]
public partial class ResourceNodeDefinition : EntityDefinition
{
    [Export] public ResourceType Resource { get; set; } = ResourceType.Wood;

    /// <summary>Total amount held before the node vanishes.</summary>
    [Export] public int TotalAmount { get; set; } = 120;

    /// <summary>How many settlers can work here at the same time.</summary>
    [Export] public int MaxGatherers { get; set; } = 4;

    /// <summary>Multiplier on the settler's gather rate — gold yields more grudgingly than wood.</summary>
    [Export] public float GatherRateFactor { get; set; } = 1f;

    /// <summary>Blocks its tile for pathfinding. Berries and farms do not.</summary>
    [Export] public bool BlocksMovement { get; set; } = true;
}
