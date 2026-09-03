using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Core.Simulation;

/// <summary>Schickt Einheiten zu einem Punkt. Der Weg wird vom PathfindingSystem gesucht.</summary>
public sealed class MoveCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required Vector2 Target { get; init; }

    /// <summary>Shift-Klick: an den laufenden Befehl anhaengen, statt ihn zu ersetzen.</summary>
    public bool Queued { get; init; }

    public void Execute(SimulationWorld world)
    {
        // Formation: Einheiten werden ringfoermig um das Ziel verteilt, damit sie
        // sich nicht alle auf denselben Punkt draengen.
        int count = Units.Length;
        int placed = 0;

        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is null || unit.OwnerId != PlayerId) continue;

            Vector2 target = count == 1 ? Target : Target + FormationOffset(placed, unit.Radius);

            if (Queued) unit.QueueMoveTo(target);
            else unit.OrderMoveTo(target);

            placed++;
        }
    }

    /// <summary>Spiralfoermige Platzverteilung: Index 0 in der Mitte, danach in wachsenden Ringen.</summary>
    private static Vector2 FormationOffset(int index, float radius)
    {
        if (index == 0) return Vector2.Zero;

        float spacing = Mathf.Max(radius * 2.4f, 1f);
        int ring = 1;
        int consumed = 0;

        while (true)
        {
            int slots = ring * 6;
            if (index - consumed <= slots) break;
            consumed += slots;
            ring++;
        }

        int slotsInRing = ring * 6;
        int slot = index - consumed - 1;
        float angle = Mathf.Tau * slot / slotsInRing;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (ring * spacing);
    }
}

/// <summary>Bricht die aktuelle Taetigkeit ab.</summary>
public sealed class StopCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }

    public void Execute(SimulationWorld world)
    {
        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is not null && unit.OwnerId == PlayerId) unit.Stop();
        }
    }
}

/// <summary>Haengt eine Einheit an die Ausbildungswarteschlange eines Gebaeudes.</summary>
public sealed class TrainUnitCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId Building { get; init; }
    public required string UnitDefinitionId { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? building = world.Entities.GetBuilding(Building);
        if (building is null || building.OwnerId != PlayerId) return;

        Player? player = world.GetPlayer(PlayerId);
        if (player is null) return;

        BuildingDefinition? buildingDef = world.Definitions.GetBuilding(building.DefinitionId);
        UnitDefinition? unitDef = world.Definitions.GetUnit(UnitDefinitionId);
        if (buildingDef is null || unitDef is null) return;

        if (System.Array.IndexOf(buildingDef.TrainableUnitIds, UnitDefinitionId) < 0)
        {
            GD.PushWarning($"[Train] {building.DefinitionId} kann '{UnitDefinitionId}' nicht ausbilden.");
            return;
        }

        if (building.QueueIsFull) return;
        if (player.AgeIndex < unitDef.RequiredAgeIndex) return;
        if (!player.TrySpend(unitDef.Cost)) return;

        // Bevoelkerungslimit wird erst bei Fertigstellung geprueft — so blockiert eine
        // volle Bevoelkerung die Warteschlange, statt den Befehl stumm zu verschlucken.
        building.Queue.Add(new ProductionOrder
        {
            UnitDefinitionId = UnitDefinitionId,
            TotalSeconds = unitDef.BuildTimeSeconds,
        });

        world.Events.RaiseProductionQueued(building, UnitDefinitionId);
    }
}

/// <summary>Nimmt einen Posten aus der Warteschlange und erstattet die Kosten.</summary>
public sealed class CancelTrainingCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId Building { get; init; }
    public required int QueueIndex { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? building = world.Entities.GetBuilding(Building);
        if (building is null || building.OwnerId != PlayerId) return;
        if (QueueIndex < 0 || QueueIndex >= building.Queue.Count) return;

        ProductionOrder order = building.Queue[QueueIndex];
        building.Queue.RemoveAt(QueueIndex);

        UnitDefinition? unitDef = world.Definitions.GetUnit(order.UnitDefinitionId);
        world.GetPlayer(PlayerId)?.Refund(unitDef?.Cost);
    }
}

/// <summary>Setzt den Sammelpunkt eines Produktionsgebaeudes.</summary>
public sealed class SetRallyPointCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId Building { get; init; }
    public required Vector2 Target { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? building = world.Entities.GetBuilding(Building);
        if (building is not null && building.OwnerId == PlayerId) building.RallyPoint = Target;
    }
}
