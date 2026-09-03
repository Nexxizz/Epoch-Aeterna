using Godot;
using EpochAeterna.Core.Data;

namespace EpochAeterna.Core.Entities;

/// <summary>A player — human or AI. Holds the account, the age and the population.</summary>
public sealed class Player
{
    /// <summary>Hard limit, regardless of how many houses stand.</summary>
    public const int HardPopulationCap = 200;

    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Color Color { get; init; }
    public int TeamId { get; init; }
    public bool IsHuman { get; init; }

    /// <summary>Current age as an index into the age chain. 0 = Stone Age.</summary>
    public int AgeIndex { get; private set; }

    public bool IsDefeated { get; set; }

    /// <summary>Match record — gathered, built, killed, lost.</summary>
    public MatchStats Stats { get; } = new();

    /// <summary>Map visibility from this player's point of view.</summary>
    public Map.VisionGrid? Vision { get; set; }

    private readonly int[] _resources = new int[ResourceTypes.Count];

    /// <summary>Population in use — the sum of the population cost of every living unit.</summary>
    public int Population { get; private set; }

    /// <summary>Cap provided by houses and the town centre, clamped to <see cref="HardPopulationCap"/>.</summary>
    public int PopulationCap { get; private set; }

    public int FreePopulation => PopulationCap - Population;

    // Events, so the HUD and the AI do not have to poll.
    public event System.Action<Player, ResourceType, int>? ResourceChanged;
    public event System.Action<Player>? PopulationChanged;
    public event System.Action<Player, int>? AgeAdvanced;

    public int GetResource(ResourceType type) => _resources[(int)type];

    public void AddResource(ResourceType type, int amount)
    {
        if (amount == 0) return;
        _resources[(int)type] = Mathf.Max(0, _resources[(int)type] + amount);
        ResourceChanged?.Invoke(this, type, _resources[(int)type]);
    }

    public void SetResource(ResourceType type, int amount)
    {
        int clamped = Mathf.Max(0, amount);
        if (_resources[(int)type] == clamped) return;
        _resources[(int)type] = clamped;
        ResourceChanged?.Invoke(this, type, clamped);
    }

    public void GrantStartingResources(ResourceSet? set)
    {
        if (set is null) return;
        foreach (ResourceType type in ResourceTypes.All) SetResource(type, set[type]);
    }

    public bool CanAfford(ResourceSet? cost)
    {
        if (cost is null) return true;
        foreach (ResourceType type in ResourceTypes.All)
        {
            if (_resources[(int)type] < cost[type]) return false;
        }
        return true;
    }

    /// <summary>Deducts the cost if it is fully covered. All or nothing.</summary>
    public bool TrySpend(ResourceSet? cost)
    {
        if (!CanAfford(cost)) return false;
        if (cost is null) return true;

        foreach (ResourceType type in ResourceTypes.All) AddResource(type, -cost[type]);
        return true;
    }

    public void Refund(ResourceSet? cost)
    {
        if (cost is null) return;
        foreach (ResourceType type in ResourceTypes.All) AddResource(type, cost[type]);
    }

    /// <summary>
    /// Recomputes population and cap from the actual entity population.
    /// Derived rather than tracked incrementally — that way the counter cannot drift.
    /// </summary>
    public void RecalculatePopulation(EntityRegistry registry)
    {
        int used = 0;
        foreach (Unit unit in registry.Units)
        {
            if (unit.OwnerId == Id) used += unit.PopulationCost;
        }

        int cap = 0;
        foreach (Building building in registry.Buildings)
        {
            // A construction site houses nobody yet.
            if (building.OwnerId == Id && !building.IsUnderConstruction) cap += building.PopulationProvided;
        }
        cap = Mathf.Min(cap, HardPopulationCap);

        if (used == Population && cap == PopulationCap) return;

        Population = used;
        PopulationCap = cap;
        PopulationChanged?.Invoke(this);
    }

    public bool HasPopulationSpace(int cost) => Population + cost <= PopulationCap;

    public void SetAge(int index)
    {
        if (index == AgeIndex) return;
        AgeIndex = index;
        AgeAdvanced?.Invoke(this, index);
    }

    public override string ToString() => $"P{Id} {Name}";
}
