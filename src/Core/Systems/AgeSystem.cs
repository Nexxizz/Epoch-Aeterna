using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Laesst laufende Zeitalter-Forschungen ablaufen und stuft den Spieler danach hoch.
/// </summary>
/// <remarks>
/// Der Aufstieg schaltet nichts direkt frei — er erhoeht nur den Zeitalterindex des
/// Spielers. Ob eine Einheit oder ein Gebaeude verfuegbar ist, entscheidet deren
/// eigene <c>RequiredAgeIndex</c> in der .tres. Damit kostet ein neues Zeitalter
/// keinen einzigen Codeeingriff.
/// </remarks>
public sealed class AgeSystem : ISimulationSystem
{
    public string Name => "Age";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (!building.IsResearchingAge) continue;

            building.AgeResearchLeft -= deltaSeconds;
            if (building.AgeResearchLeft > 0f) continue;

            building.AgeResearchLeft = 0f;

            Player? player = world.GetPlayer(building.OwnerId);
            if (player is null) continue;

            int next = player.AgeIndex + 1;
            AgeDefinition? definition = world.Definitions.GetAge(next);
            if (definition is null) continue;

            player.SetAge(next);
            ApplyAgeBonus(world, player);

            world.Events.RaiseAgeAdvanced(player, definition);
        }
    }

    /// <summary>
    /// Pauschaler Zuwachs auf Lebenspunkte beim Aufstieg. Bewusst schlicht gehalten;
    /// echte Verbesserungen kommen mit dem Technologiebaum.
    /// </summary>
    private static void ApplyAgeBonus(SimulationWorld world, Player player)
    {
        const float bonus = 1.15f;

        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.OwnerId != player.Id) continue;

            float ratio = unit.HealthFraction;
            unit.MaxHealth *= bonus;
            unit.Health = unit.MaxHealth * ratio;
        }

        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId != player.Id) continue;

            float ratio = building.HealthFraction;
            building.MaxHealth *= bonus;
            building.Health = building.MaxHealth * ratio;
        }
    }
}
