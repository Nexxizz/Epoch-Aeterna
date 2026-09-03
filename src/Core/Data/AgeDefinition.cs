using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// Ein Zeitalter. Die Kette wird ueber <see cref="Index"/> geordnet; das MVP nutzt
/// Index 0 (Steinzeit) und 1 (Kupferzeit), die Struktur erlaubt beliebig viele.
/// </summary>
[GlobalClass]
public partial class AgeDefinition : Resource
{
    [Export] public string Id { get; set; } = string.Empty;

    /// <summary>Position in der Zeitalterkette, beginnend bei 0.</summary>
    [Export] public int Index { get; set; }

    [Export] public string DisplayName { get; set; } = string.Empty;

    /// <summary>Kosten fuer den Aufstieg *in* dieses Zeitalter. Fuer Index 0 leer.</summary>
    [Export] public ResourceSet? AdvanceCost { get; set; }

    [Export] public float ResearchTimeSeconds { get; set; } = 30f;

    /// <summary>
    /// Wie viele verschiedene Gebaeudetypen des Vorgaengerzeitalters stehen muessen,
    /// bevor der Aufstieg freigeschaltet wird.
    /// </summary>
    [Export] public int RequiredDistinctBuildings { get; set; } = 2;
}
