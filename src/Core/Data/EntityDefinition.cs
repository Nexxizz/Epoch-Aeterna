using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// Gemeinsame Basis aller Bauplaene. Eine Definition beschreibt einen *Typ*
/// (z. B. "Speerkaempfer"), keine konkrete Instanz auf der Karte.
/// </summary>
public abstract partial class EntityDefinition : Resource
{
    /// <summary>Eindeutige, stabile ID. Wird in Savegames und Referenzen benutzt — nie nachtraeglich aendern.</summary>
    [Export] public string Id { get; set; } = string.Empty;

    [Export] public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export] public Texture2D? Icon { get; set; }

    /// <summary>Fertiges Modell. Bleibt bis Phase 5 leer — die Views nehmen dann Platzhalter-Geometrie.</summary>
    [Export] public PackedScene? ModelScene { get; set; }

    [Export] public ResourceSet? Cost { get; set; }

    /// <summary>Bau- bzw. Ausbildungsdauer in Sekunden.</summary>
    [Export] public float BuildTimeSeconds { get; set; } = 5f;

    [Export] public float MaxHealth { get; set; } = 100f;

    [Export] public float Armor { get; set; }

    /// <summary>Sichtweite in Metern — speist Fog of War (Phase 3.6).</summary>
    [Export] public float VisionRange { get; set; } = 12f;

    /// <summary>Ab welchem Zeitalter verfuegbar. 0 = von Anfang an.</summary>
    [Export] public int RequiredAgeIndex { get; set; }

    /// <summary>Optionale Voraussetzung: dieses Gebaeude muss existieren. Leer = keine.</summary>
    [Export] public string RequiredBuildingId { get; set; } = string.Empty;

    public override string ToString() => $"{GetType().Name}({Id})";
}
