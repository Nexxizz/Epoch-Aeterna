using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Bauplan einer beweglichen Einheit.</summary>
[GlobalClass]
public partial class UnitDefinition : EntityDefinition
{
    /// <summary>Bewegungstempo in Metern pro Sekunde.</summary>
    [Export] public float MoveSpeed { get; set; } = 3f;

    /// <summary>Drehgeschwindigkeit in Grad pro Sekunde.</summary>
    [Export] public float TurnSpeedDegrees { get; set; } = 540f;

    /// <summary>Radius fuer Kollision und Ausweichbewegung, in Metern.</summary>
    [Export] public float Radius { get; set; } = 0.4f;

    [Export] public int PopulationCost { get; set; } = 1;

    // --- Kampf ---
    [Export] public float AttackDamage { get; set; }
    [Export] public float AttackRange { get; set; } = 1f;
    [Export] public float AttackCooldownSeconds { get; set; } = 1.5f;
    [Export] public DamageType DamageType { get; set; } = DamageType.Blunt;
    [Export] public ArmorClass ArmorClass { get; set; } = ArmorClass.Infantry;

    /// <summary>Fernkaempfer verschiessen ein Geschoss mit Flugzeit statt sofort zu treffen.</summary>
    [Export] public bool UsesProjectile { get; set; }

    [Export] public float ProjectileSpeed { get; set; } = 22f;

    /// <summary>Greift von selbst an, was in Sichtweite kommt.</summary>
    [Export] public bool AutoEngages { get; set; } = true;

    // --- Arbeit (ausgewertet ab Phase 3.1/3.2) ---
    [Export] public bool CanGather { get; set; }
    [Export] public bool CanBuild { get; set; }
    [Export] public float GatherRatePerSecond { get; set; } = 0.5f;
    [Export] public int CarryCapacity { get; set; } = 10;
}
