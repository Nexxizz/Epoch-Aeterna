using System.Text;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.UI;

/// <summary>
/// The wording of the HUD in one place.
/// </summary>
/// <remarks>
/// The definitions in <c>data/</c> carry English display names, the interface speaks
/// German. Everything the interface phrases itself lives here, so the two never drift
/// apart across half a dozen panels.
/// </remarks>
public static class HudText
{
    public static string Resource(ResourceType type) => type switch
    {
        ResourceType.Food => "Nahrung",
        ResourceType.Wood => "Holz",
        ResourceType.Stone => "Stein",
        ResourceType.Gold => "Gold",
        _ => type.ToString(),
    };

    /// <summary>"50 Nahrung • 30 Holz" — empty costs read as "Kostenlos".</summary>
    public static string Cost(ResourceSet? cost)
    {
        if (cost is null) return "Kostenlos";

        var text = new StringBuilder();
        foreach (ResourceType type in ResourceTypes.All)
        {
            int amount = cost[type];
            if (amount <= 0) continue;
            if (text.Length > 0) text.Append(" • ");
            text.Append(amount).Append(' ').Append(Resource(type));
        }
        return text.Length > 0 ? text.ToString() : "Kostenlos";
    }

    /// <summary>What the unit is doing right now, in one short line.</summary>
    public static string Order(Unit unit) => unit.Order switch
    {
        UnitOrder.Move => "Unterwegs",
        UnitOrder.Attack => "Im Kampf",
        UnitOrder.AttackMove => "Angriffsbewegung",
        UnitOrder.Build => "Baut",
        UnitOrder.Gather => unit.GatherPhase switch
        {
            GatherPhase.Harvesting => $"Sammelt {Resource(unit.CarriedResource)}",
            GatherPhase.ToDropOff => "Liefert ab",
            _ => "Auf dem Weg zur Ressource",
        },
        _ => "Wartet",
    };

    public static string StanceName(Stance stance) => stance switch
    {
        Stance.Defensive => "Defensiv",
        Stance.HoldPosition => "Stellung halten",
        _ => "Aggressiv",
    };
}
