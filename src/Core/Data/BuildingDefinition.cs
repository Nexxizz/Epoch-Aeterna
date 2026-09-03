using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>Bauplan eines Gebaeudes.</summary>
[GlobalClass]
public partial class BuildingDefinition : EntityDefinition
{
    /// <summary>Grundflaeche in Kacheln (X = Breite, Y = Tiefe).</summary>
    [Export] public Vector2I Footprint { get; set; } = new(2, 2);

    /// <summary>Erhoeht das Bevoelkerungslimit des Besitzers um diesen Wert.</summary>
    [Export] public int PopulationProvided { get; set; }

    /// <summary>Siedler koennen hier Ressourcen abliefern.</summary>
    [Export] public bool IsDropOffPoint { get; set; }

    /// <summary>IDs der hier ausbildbaren Einheiten.</summary>
    [Export] public string[] TrainableUnitIds { get; set; } = System.Array.Empty<string>();

    /// <summary>Erlaubt den Zeitalteraufstieg (Phase 3.5). Im MVP nur das Rathaus.</summary>
    [Export] public bool CanAdvanceAge { get; set; }

    // --- Verteidigung ---
    [Export] public float AttackDamage { get; set; }
    [Export] public float AttackRange { get; set; }
    [Export] public float AttackCooldownSeconds { get; set; } = 2f;
    [Export] public DamageType DamageType { get; set; } = DamageType.Ranged;
    [Export] public ArmorClass ArmorClass { get; set; } = ArmorClass.Building;
    [Export] public float ProjectileSpeed { get; set; } = 26f;

    /// <summary>
    /// Anteil der Kosten, den ein Abriss zurueckgibt.
    /// </summary>
    [Export] public float DemolishRefundFraction { get; set; } = 0.5f;

    /// <summary>Erneuerbare Nahrungsquelle: die Farm gibt Nahrung, ohne zu verschwinden.</summary>
    [Export] public bool IsFarm { get; set; }

    [Export] public int FarmFoodAmount { get; set; } = 250;
}
