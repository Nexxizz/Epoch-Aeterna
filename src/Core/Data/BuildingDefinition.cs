using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Blueprint of a building.</summary>
[GlobalClass]
public partial class BuildingDefinition : EntityDefinition
{
    /// <summary>Footprint in tiles (X = width, Y = depth).</summary>
    [Export] public Vector2I Footprint { get; set; } = new(2, 2);

    /// <summary>
    /// Optional construction models ordered from foundation to nearly finished.
    /// Missing entries fall back to the procedural placeholder.
    /// </summary>
    [Export] public PackedScene[] ConstructionStageScenes { get; set; } = System.Array.Empty<PackedScene>();

    /// <summary>Raises the owner's population cap by this much.</summary>
    [Export] public int PopulationProvided { get; set; }

    /// <summary>Settlers can drop off resources here.</summary>
    [Export] public bool IsDropOffPoint { get; set; }

    /// <summary>Ids of the units trainable here.</summary>
    [Export] public string[] TrainableUnitIds { get; set; } = System.Array.Empty<string>();

    /// <summary>Allows advancing an age. In the MVP only the town centre.</summary>
    [Export] public bool CanAdvanceAge { get; set; }

    // --- Defence ---
    [Export] public float AttackDamage { get; set; }
    [Export] public float AttackRange { get; set; }
    [Export] public float AttackCooldownSeconds { get; set; } = 2f;
    [Export] public DamageType DamageType { get; set; } = DamageType.Ranged;
    [Export] public ArmorClass ArmorClass { get; set; } = ArmorClass.Building;
    [Export] public float ProjectileSpeed { get; set; } = 26f;

    /// <summary>Share of the cost a demolition returns.</summary>
    [Export] public float DemolishRefundFraction { get; set; } = 0.5f;

    /// <summary>Renewable food source: the farm yields food without disappearing.</summary>
    [Export] public bool IsFarm { get; set; }

    [Export] public int FarmFoodAmount { get; set; } = 250;
}
