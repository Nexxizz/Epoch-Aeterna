using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// Shared base of every blueprint. A definition describes a *type*
/// (a spearman, say), not a concrete instance on the map.
/// </summary>
public abstract partial class EntityDefinition : Resource
{
    /// <summary>Unique, stable id. Used in save games and references — never change it afterwards.</summary>
    [Export] public string Id { get; set; } = string.Empty;

    [Export] public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export] public Texture2D? Icon { get; set; }

    /// <summary>Finished model. Stays empty until phase 5 — views fall back to placeholder geometry.</summary>
    [Export] public PackedScene? ModelScene { get; set; }

    [Export] public ResourceSet? Cost { get; set; }

    /// <summary>Build or training time in seconds.</summary>
    [Export] public float BuildTimeSeconds { get; set; } = 5f;

    [Export] public float MaxHealth { get; set; } = 100f;

    [Export] public float Armor { get; set; }

    /// <summary>Sight range in metres — feeds the fog of war.</summary>
    [Export] public float VisionRange { get; set; } = 12f;

    /// <summary>Age this becomes available in. 0 = from the start.</summary>
    [Export] public int RequiredAgeIndex { get; set; }

    /// <summary>Optional prerequisite: this building must exist. Empty means none.</summary>
    [Export] public string RequiredBuildingId { get; set; } = string.Empty;

    public override string ToString() => $"{GetType().Name}({Id})";
}
