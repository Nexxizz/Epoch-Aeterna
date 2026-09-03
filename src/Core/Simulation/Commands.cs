using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Simulation;

/// <summary>Sends units to a point. The route is found by the PathfindingSystem.</summary>
public sealed class MoveCommand : ICommand
{
    public required int PlayerId { get; init; }
    public required EntityId[] Units { get; init; }
    public required Vector2 Target { get; init; }

    /// <summary>Shift-click: append to the running order instead of replacing it.</summary>
    public bool Queued { get; init; }

    /// <summary>Angriffsbewegung: unterwegs alles bekaempfen, was in Reichweite kommt.</summary>
    public bool Attacking { get; init; }

    public void Execute(SimulationWorld world)
    {
        // Formation: units are spread in a ring around the target so they do not
        // all crowd onto the same point.
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

    /// <summary>Spiral slot layout: index 0 in the centre, then in growing rings.</summary>
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

/// <summary>Cancels the current activity.</summary>
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

/// <summary>Sets the stance: how independently a unit reacts to enemies.</summary>
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

/// <summary>Sends settlers to a resource deposit.</summary>
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
/// Places a construction site and sends the selected settlers to it.
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

        // The site only enters the registry on flush — the settlers get the assignment
        // now regardless, since they have to walk there first anyway.
        foreach (EntityId id in Builders)
        {
            Unit? unit = world.Entities.GetUnit(id);
            if (unit is not null && unit.OwnerId == PlayerId && unit.CanBuild) unit.OrderBuild(site.Id);
        }
    }
}

/// <summary>Sends settlers to an existing construction site.</summary>
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

/// <summary>Demolishes one of your own buildings and refunds part of the cost.</summary>
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
            // Proportional to build progress: a half-finished site gives back less.
            float fraction = definition.DemolishRefundFraction * building.ConstructionProgress;

            foreach (ResourceType type in ResourceTypes.All)
            {
                player.AddResource(type, Mathf.FloorToInt(definition.Cost[type] * fraction));
            }
        }

        world.Entities.Remove(building.Id);
    }
}

/// <summary>Appends a unit to a building's training queue.</summary>
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

        // The population cap is only checked on completion — that way a full population
        // stalls the queue instead of silently swallowing the order.
        building.Queue.Add(new ProductionOrder
        {
            UnitDefinitionId = UnitDefinitionId,
            TotalSeconds = unitDef.BuildTimeSeconds,
        });

        world.Events.RaiseProductionQueued(building, UnitDefinitionId);
    }
}

/// <summary>Takes an entry off the queue and refunds its cost.</summary>
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

/// <summary>Sets a production building's rally point.</summary>
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

/// <summary>Starts the advance into the next age.</summary>
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

    /// <summary>Counts distinct finished building types — the prerequisite for advancing.</summary>
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

/// <summary>Checks whether a building may stand somewhere. Used by both the command and the preview.</summary>
public static class BuildPlacement
{
    /// <summary>Largest permitted height difference under the footprint, in metres.</summary>
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
