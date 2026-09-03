using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Runs down age research in progress and promotes the player once it completes.
/// </summary>
/// <remarks>
/// Advancing unlocks nothing directly — it only raises the player's age index.
/// Whether a unit or a building is available is decided by its own
/// <c>RequiredAgeIndex</c> in the .tres. A new age therefore costs
/// not a single change to the code.
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
    /// A flat health increase on advancing. Deliberately kept plain;
    /// real upgrades come with the technology tree.
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
