using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Blueprint of a mobile unit.</summary>
[GlobalClass]
public partial class UnitDefinition : EntityDefinition
{
    /// <summary>Movement speed in metres per second.</summary>
    [Export] public float MoveSpeed { get; set; } = 3f;

    /// <summary>Turn rate in degrees per second.</summary>
    [Export] public float TurnSpeedDegrees { get; set; } = 540f;

    /// <summary>Radius used for collision and local avoidance, in metres.</summary>
    [Export] public float Radius { get; set; } = 0.4f;

    [Export] public int PopulationCost { get; set; } = 1;

    // --- Combat ---
    [Export] public float AttackDamage { get; set; }
    [Export] public float AttackRange { get; set; } = 1f;
    [Export] public float AttackCooldownSeconds { get; set; } = 1.5f;
    [Export] public DamageType DamageType { get; set; } = DamageType.Blunt;
    [Export] public ArmorClass ArmorClass { get; set; } = ArmorClass.Infantry;

    /// <summary>Ranged units launch a projectile with travel time instead of hitting instantly.</summary>
    [Export] public bool UsesProjectile { get; set; }

    [Export] public float ProjectileSpeed { get; set; } = 22f;

    /// <summary>Attacks whatever comes into sight on its own.</summary>
    [Export] public bool AutoEngages { get; set; } = true;

    // --- Work ---
    [Export] public bool CanGather { get; set; }
    [Export] public bool CanBuild { get; set; }
    [Export] public float GatherRatePerSecond { get; set; } = 0.5f;
    [Export] public int CarryCapacity { get; set; } = 10;
}
