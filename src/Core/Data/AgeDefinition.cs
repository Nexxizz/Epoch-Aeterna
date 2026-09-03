using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// One age. The chain is ordered by <see cref="Index"/>; the MVP uses index 0
/// (Stone Age) and 1 (Copper Age), but the structure allows any number.
/// </summary>
[GlobalClass]
public partial class AgeDefinition : Resource
{
    [Export] public string Id { get; set; } = string.Empty;

    /// <summary>Position in the age chain, starting at 0.</summary>
    [Export] public int Index { get; set; }

    [Export] public string DisplayName { get; set; } = string.Empty;

    /// <summary>Cost of advancing *into* this age. Empty for index 0.</summary>
    [Export] public ResourceSet? AdvanceCost { get; set; }

    [Export] public float ResearchTimeSeconds { get; set; } = 30f;

    /// <summary>
    /// How many distinct building types must stand before advancing unlocks.
    /// </summary>
    [Export] public int RequiredDistinctBuildings { get; set; } = 2;
}
