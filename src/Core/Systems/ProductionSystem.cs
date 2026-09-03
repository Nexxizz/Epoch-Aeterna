using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Works through the training queues of buildings and places finished units
/// on the map.
/// </summary>
/// <remarks>
/// Phase 3.3 adds the progress display in the HUD and rally point refinements;
/// the mechanics themselves — queue, population stall, rally point — live here.
/// </remarks>
public sealed class ProductionSystem : ISimulationSystem
{
    public string Name => "Production";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            // A construction site trains nobody yet.
            if (building.IsUnderConstruction) continue;

            ProductionOrder? order = building.CurrentOrder;
            if (order is null) continue;

            Player? player = world.GetPlayer(building.OwnerId);
            if (player is null) continue;

            UnitDefinition? definition = world.Definitions.GetUnit(order.UnitDefinitionId);
            if (definition is null)
            {
                GD.PushError($"[Production] Discarding unknown unit id '{order.UnitDefinitionId}'.");
                building.Queue.RemoveAt(0);
                continue;
            }

            // A full population stalls the queue instead of clearing it:
            // the player builds a house and production carries on by itself.
            if (order.IsComplete && !player.HasPopulationSpace(definition.PopulationCost)) continue;

            if (!order.IsComplete)
            {
                order.ElapsedSeconds += deltaSeconds;
                if (!order.IsComplete) continue;
                if (!player.HasPopulationSpace(definition.PopulationCost)) continue;
            }

            building.Queue.RemoveAt(0);
            Complete(world, building, definition);
        }
    }

    private static void Complete(SimulationWorld world, Building building, UnitDefinition definition)
    {
        Vector2 spawn = building.SpawnPoint();

        Unit? unit = world.SpawnUnit(definition.Id, building.OwnerId, spawn);
        if (unit is null) return;

        // Send to the rally point, otherwise leave them beside the building.
        if (building.RallyPoint is { } rally) unit.OrderMoveTo(rally);

        Player? owner = world.GetPlayer(building.OwnerId);
        if (owner is not null) owner.Stats.UnitsTrained++;
        world.Events.RaiseProductionCompleted(building, unit);
    }
}
