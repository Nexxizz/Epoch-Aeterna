namespace EpochAeterna.Core.Data;

/// <summary>
/// The game's resource types. The order here is the display order in the HUD.
/// </summary>
/// <remarks>
/// Adding a resource (iron from the Iron Age, say) takes exactly three changes:
/// an entry here, a field in <see cref="ResourceSet"/> plus a line in its indexer,
/// and <see cref="ResourceTypes.Count"/> grows on its own.
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
        ResourceType.Food => "Food",
        ResourceType.Wood => "Wood",
        ResourceType.Stone => "Stone",
        ResourceType.Gold => "Gold",
        _ => type.ToString(),
    };
}
