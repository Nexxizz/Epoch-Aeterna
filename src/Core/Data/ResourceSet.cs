using Godot;

namespace EpochAeterna.Core.Data;

/// <summary>
/// A bundle of resource amounts — usable as a cost, a yield or a starting stock.
/// </summary>
/// <remarks>
/// Its own Resource type so that costs show up in the Godot inspector with readable
/// field names instead of an anonymous int array.
/// </remarks>
[GlobalClass]
public partial class ResourceSet : Resource
{
    [Export] public int Food { get; set; }
    [Export] public int Wood { get; set; }
    [Export] public int Stone { get; set; }
    [Export] public int Gold { get; set; }

    /// <summary>Access by enum value, so systems don't have to branch per resource.</summary>
    public int this[ResourceType type]
    {
        get => type switch
        {
            ResourceType.Food => Food,
            ResourceType.Wood => Wood,
            ResourceType.Stone => Stone,
            ResourceType.Gold => Gold,
            _ => 0,
        };
        set
        {
            switch (type)
            {
                case ResourceType.Food: Food = value; break;
                case ResourceType.Wood: Wood = value; break;
                case ResourceType.Stone: Stone = value; break;
                case ResourceType.Gold: Gold = value; break;
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            foreach (ResourceType type in ResourceTypes.All)
            {
                if (this[type] != 0) return false;
            }
            return true;
        }
    }

    public ResourceSet Clone()
    {
        var copy = new ResourceSet();
        foreach (ResourceType type in ResourceTypes.All)
        {
            copy[type] = this[type];
        }
        return copy;
    }

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (ResourceType type in ResourceTypes.All)
        {
            if (this[type] != 0) parts.Add($"{this[type]} {ResourceTypes.DisplayName(type)}");
        }
        return parts.Count == 0 ? "free" : string.Join(", ", parts);
    }
}
