using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Arbeitet die Ausbildungswarteschlangen der Gebaeude ab und setzt fertige Einheiten
/// auf die Karte.
/// </summary>
/// <remarks>
/// Phase 3.3 erweitert das um Fortschrittsanzeige im HUD und Sammelpunkt-Feinheiten;
/// die Mechanik selbst — Warteschlange, Bevoelkerungsblockade, Sammelpunkt — steht hier.
/// </remarks>
public sealed class ProductionSystem : ISimulationSystem
{
    public string Name => "Production";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            // Eine Baustelle bildet noch niemanden aus.
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

            // Volle Bevoelkerung haelt die Warteschlange an, statt sie zu leeren:
            // Der Spieler baut ein Haus, die Produktion laeuft von selbst weiter.
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

        // Zum Sammelpunkt schicken, sonst am Gebaeude stehen lassen.
        if (building.RallyPoint is { } rally) unit.OrderMoveTo(rally);

        Player? owner = world.GetPlayer(building.OwnerId);
        if (owner is not null) owner.Stats.UnitsTrained++;
        world.Events.RaiseProductionCompleted(building, unit);
    }
}
