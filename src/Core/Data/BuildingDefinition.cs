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

    // --- Verteidigung (ausgewertet ab Phase 3.4) ---
    [Export] public float AttackDamage { get; set; }
    [Export] public float AttackRange { get; set; }
    [Export] public float AttackCooldownSeconds { get; set; } = 2f;
}
