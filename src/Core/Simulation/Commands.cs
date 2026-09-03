using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Simulation;

/// <summary>Schickt Einheiten zu einem Punkt. Der Weg wird vom PathfindingSystem gesucht.</summary>
public sealed class MoveCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required Vector2 Target { get; init; }

    /// <summary>Shift-Klick: an den laufenden Befehl anhaengen, statt ihn zu ersetzen.</summary>
    public bool Queued { get; init; }

    /// <summary>Angriffsbewegung: unterwegs alles bekaempfen, was in Reichweite kommt.</summary>
    public bool Attacking { get; init; }

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
            else if (Attacking) unit.OrderAttackMoveTo(target);
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

/// <summary>Setzt die Haltung: wie selbstaendig auf Feinde reagiert wird.</summary>
public sealed class SetStanceCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required Stance Stance { get; init; }

    public void Execute(SimulationWorld world)
    {
        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is null || unit.OwnerId != PlayerId) continue;

            unit.Stance = Stance;
            unit.GuardPosition = unit.Position;
        }
    }
}

/// <summary>Schickt Siedler an ein Ressourcenvorkommen.</summary>
public sealed class GatherCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required EntityId Node { get; init; }

    public void Execute(SimulationWorld world)
    {
        ResourceNode? node = world.Entities.GetResourceNode(Node);
        if (node is null || node.IsDepleted) return;

        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is null || unit.OwnerId != PlayerId || !unit.CanGather) continue;

            unit.OrderGather(Node);
        }
    }
}

/// <summary>Greift ein bestimmtes Ziel an.</summary>
public sealed class AttackCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required EntityId Target { get; init; }

    public void Execute(SimulationWorld world)
    {
        Entity? target = world.Entities.Get(Target);
        if (target is null || target.OwnerId == PlayerId) return;

        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is null || unit.OwnerId != PlayerId || unit.AttackDamage <= 0f) continue;

            unit.OrderAttack(Target);
        }
    }
}

/// <summary>
/// Setzt eine Baustelle und schickt die ausgewaehlten Siedler hin.
/// </summary>
public sealed class PlaceBuildingCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required string BuildingDefinitionId { get; init; }
    public required Vector2 Position { get; init; }
    public required EntityId[] Builders { get; init; }

    public void Execute(SimulationWorld world)
    {
        Player? player = world.GetPlayer(PlayerId);
        BuildingDefinition? definition = world.Definitions.GetBuilding(BuildingDefinitionId);
        if (player is null || definition is null) return;

        if (!BuildPlacement.IsValid(world, definition, Position, player)) return;
        if (!player.TrySpend(definition.Cost)) return;

        Building? site = world.SpawnBuilding(BuildingDefinitionId, PlayerId, Position, underConstruction: true);
        if (site is null)
        {
            player.Refund(definition.Cost);
            return;
        }

        // Die Baustelle existiert erst nach dem Flush in der Registry — die Siedler
        // bekommen den Auftrag trotzdem schon jetzt, sie laufen ohnehin erst hin.
        foreach (EntityId id in Builders)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is not null && unit.OwnerId == PlayerId && unit.CanBuild) unit.OrderBuild(site.Id);
        }
    }
}

/// <summary>Schickt Siedler an eine bestehende Baustelle.</summary>
public sealed class RepairOrBuildCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required EntityId Site { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? site = world.Entities.GetBuilding(Site);
        if (site is null || site.OwnerId != PlayerId || !site.IsUnderConstruction) return;

        foreach (EntityId id in Units)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is not null && unit.OwnerId == PlayerId && unit.CanBuild) unit.OrderBuild(Site);
        }
    }
}

/// <summary>Reisst ein eigenes Gebaeude ab und erstattet einen Teil der Kosten.</summary>
public sealed class DemolishCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId Building { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? building = world.Entities.GetBuilding(Building);
        if (building is null || building.OwnerId != PlayerId) return;

        BuildingDefinition? definition = world.Definitions.GetBuilding(building.DefinitionId);
        Player? player = world.GetPlayer(PlayerId);

        if (definition?.Cost is not null && player is not null)
        {
            // Anteilig zum Baufortschritt: eine halbfertige Baustelle gibt weniger zurueck.
            float fraction = definition.DemolishRefundFraction * building.ConstructionProgress;

            foreach (ResourceType type in ResourceTypes.All)
            {
                player.AddResource(type, Mathf.FloorToInt(definition.Cost[type] * fraction));
            }
        }

        world.Entities.Remove(building.Id);
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
        if (building is null || building.OwnerId != PlayerId || building.IsUnderConstruction) return;

        Player? player = world.GetPlayer(PlayerId);
        if (player is null) return;

        BuildingDefinition? buildingDef = world.Definitions.GetBuilding(building.DefinitionId);
        UnitDefinition? unitDef = world.Definitions.GetUnit(UnitDefinitionId);
        if (buildingDef is null || unitDef is null) return;

        if (System.Array.IndexOf(buildingDef.TrainableUnitIds, UnitDefinitionId) < 0) return;

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

/// <summary>Startet den Aufstieg ins naechste Zeitalter.</summary>
public sealed class AdvanceAgeCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId Building { get; init; }

    public void Execute(SimulationWorld world)
    {
        Building? building = world.Entities.GetBuilding(Building);
        Player? player = world.GetPlayer(PlayerId);

        if (building is null || player is null) return;
        if (building.OwnerId != PlayerId || !building.CanAdvanceAge) return;
        if (building.IsUnderConstruction || building.IsResearchingAge) return;

        AgeDefinition? next = world.Definitions.GetAge(player.AgeIndex + 1);
        if (next is null) return;

        if (CountDistinctBuildings(world, PlayerId) < next.RequiredDistinctBuildings) return;
        if (!player.TrySpend(next.AdvanceCost)) return;

        building.AgeResearchLeft = next.ResearchTimeSeconds;
    }

    /// <summary>Zaehlt verschiedene fertige Gebaeudetypen — die Voraussetzung fuer den Aufstieg.</summary>
    private static int CountDistinctBuildings(SimulationWorld world, int playerId)
    {
        var seen = new System.Collections.Generic.HashSet<string>();

        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == playerId && !building.IsUnderConstruction) seen.Add(building.DefinitionId);
        }
        return seen.Count;
    }
}

/// <summary>Prueft, ob ein Gebaeude an einer Stelle stehen darf. Von Befehl und Vorschau genutzt.</summary>
public static class BuildPlacement
{
    /// <summary>Groesster zulaessiger Hoehenunterschied unter der Grundflaeche, in Metern.</summary>
    private const float MaxSlope = 1.6f;

    public static bool IsValid(SimulationWorld world, BuildingDefinition definition, Vector2 position, Player player)
    {
        if (player.AgeIndex < definition.RequiredAgeIndex) return false;

        if (!string.IsNullOrEmpty(definition.RequiredBuildingId) &&
            !HasBuilding(world, player.Id, definition.RequiredBuildingId))
        {
            return false;
        }

        NavGrid grid = world.Nav;
        Vector2I origin = grid.WorldToCell(position);
        int halfX = definition.Footprint.X / 2;
        int halfY = definition.Footprint.Y / 2;

        for (int y = origin.Y - halfY; y < origin.Y - halfY + definition.Footprint.Y; y++)
        {
            for (int x = origin.X - halfX; x < origin.X - halfX + definition.Footprint.X; x++)
            {
                if (!grid.InBounds(x, y)) return false;
                if (!grid.IsWalkable(x, y)) return false;
                if (grid.CellSlope(x, y) > MaxSlope) return false;
            }
        }

        return true;
    }

    private static bool HasBuilding(SimulationWorld world, int playerId, string definitionId)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == playerId &&
                building.DefinitionId == definitionId &&
                !building.IsUnderConstruction)
            {
                return true;
            }
        }
        return false;
    }
}
