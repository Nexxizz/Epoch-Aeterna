namespace EpochAeterna.Core.Data;

/// <summary>
/// Ressourcenarten des Spiels. Die Reihenfolge ist die Anzeigereihenfolge im HUD.
/// </summary>
/// <remarks>
/// Eine neue Ressource (z. B. Eisen ab der Eisenzeit) hinzuzufuegen erfordert genau
/// drei Aenderungen: einen Eintrag hier, ein Feld in <see cref="ResourceSet"/> samt
/// Zeile im dortigen Indexer, und <see cref="ResourceTypes.Count"/> waechst automatisch mit.
/// </remarks>
public enum ResourceType
{
    Food = 0,
    Wood = 1,
    Stone = 2,
    Gold = 3,
}

public static class ResourceTypes
{
    public static readonly ResourceType[] All =
    {
        ResourceType.Food,
        ResourceType.Wood,
        ResourceType.Stone,
        ResourceType.Gold,
    };

    public static int Count => All.Length;

    public static string DisplayName(ResourceType type) => type switch
    {
        ResourceType.Food => "Nahrung",
        ResourceType.Wood => "Holz",
        ResourceType.Stone => "Stein",
        ResourceType.Gold => "Gold",
        _ => type.ToString(),
    };
}
