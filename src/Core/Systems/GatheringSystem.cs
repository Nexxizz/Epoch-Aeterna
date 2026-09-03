using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// The gathering cycle: walk there, harvest, carry to the drop-off point, return.
/// </summary>
/// <remarks>
/// The cycle continues on its own: once a deposit is exhausted the settler looks
/// for the next one of the same kind instead of standing idle. That is what an RTS
/// is expected to do, and it saves constant re-clicking.
/// </remarks>
public sealed class GatheringSystem : ISimulationSystem
{
    /// <summary>Distance at which work at the deposit or the drop-off point can begin.</summary>
    private const float WorkRange = 1.4f;

    public string Name => "Gathering";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.Order != UnitOrder.Gather) continue;

            switch (unit.GatherPhase)
            {
                case GatherPhase.ToNode: WalkToNode(world, unit); break;
                case GatherPhase.Harvesting: Harvest(world, unit, deltaSeconds); break;
                case GatherPhase.ToDropOff: WalkToDropOff(world, unit); break;
            }
        }
    }

    // --- Outbound --------------------------------------------------------

    private static void WalkToNode(SimulationWorld world, Unit unit)
    {
        ResourceNode? node = world.Entities.GetResourceNode(unit.GatherTarget);

        if (node is null || node.IsDepleted)
        {
            if (!RetargetNearestNode(world, unit)) unit.Stop();
            return;
        }

        float distance = unit.Position.DistanceTo(node.Position);

        if (distance <= WorkRange + unit.Radius)
        {
            unit.ClearPath();
            unit.GatherPhase = GatherPhase.Harvesting;
            node.ActiveGatherers++;
            return;
        }

        // Only request again when no path is running — otherwise every tick would
        // trigger a new search and eat the budget.
        if (!unit.HasPath && !unit.NeedsPath) unit.StartMoveTo(ApproachPoint(unit.Position, node.Position, WorkRange));
    }

    // --- Harvesting ------------------------------------------------------

    private static void Harvest(SimulationWorld world, Unit unit, float deltaSeconds)
    {
        ResourceNode? node = world.Entities.GetResourceNode(unit.GatherTarget);

        if (node is null || node.IsDepleted)
        {
            if (node is not null) node.ActiveGatherers = Mathf.Max(0, node.ActiveGatherers - 1);

            // Even half a load makes the trip back worthwhile.
            if (unit.CarriedAmount > 0f) BeginReturn(world, unit);
            else if (!RetargetNearestNode(world, unit)) unit.Stop();
            return;
        }

        unit.CarriedResource = node.Resource;
        unit.HarvestProgress += unit.GatherRatePerSecond * node.GatherRateFactor * deltaSeconds;

        // Take whole units from the deposit first — otherwise rounding would slowly
        // evaporate the stock.
        int whole = Mathf.FloorToInt(unit.HarvestProgress);
        if (whole > 0)
        {
            int taken = node.Extract(whole);
            unit.HarvestProgress -= whole;
            unit.CarriedAmount += taken;
        }

        if (node.IsDepleted)
        {
            node.ActiveGatherers = Mathf.Max(0, node.ActiveGatherers - 1);
            world.Entities.Remove(node.Id);
            BeginReturn(world, unit);
            return;
        }

        if (unit.IsCarryingFull)
        {
            node.ActiveGatherers = Mathf.Max(0, node.ActiveGatherers - 1);
            BeginReturn(world, unit);
        }
    }

    // --- Return trip -----------------------------------------------------

    private static void BeginReturn(SimulationWorld world, Unit unit)
    {
        Building? dropOff = FindNearestDropOff(world, unit);

        if (dropOff is null)
        {
            // No drop-off point left: the load is kept and the settler waits.
            unit.Stop();
            return;
        }

        unit.DropOffTarget = dropOff.Id;
        unit.GatherPhase = GatherPhase.ToDropOff;
        unit.StartMoveTo(ApproachPoint(unit.Position, dropOff.Position, dropOff.FootprintRadius + WorkRange));
    }

    private static void WalkToDropOff(SimulationWorld world, Unit unit)
    {
        Building? dropOff = world.Entities.GetBuilding(unit.DropOffTarget);

        if (dropOff is null || dropOff.IsUnderConstruction)
        {
            BeginReturn(world, unit);
            return;
        }

        float reach = dropOff.FootprintRadius + WorkRange + unit.Radius;

        if (unit.Position.DistanceTo(dropOff.Position) > reach)
        {
            if (!unit.HasPath && !unit.NeedsPath)
            {
                unit.StartMoveTo(ApproachPoint(unit.Position, dropOff.Position, dropOff.FootprintRadius + WorkRange));
            }
            return;
        }

        Deposit(world, unit);
    }

    private static void Deposit(SimulationWorld world, Unit unit)
    {
        int amount = Mathf.FloorToInt(unit.CarriedAmount);
        Player? player = world.GetPlayer(unit.OwnerId);

        if (amount > 0 && player is not null)
        {
            player.AddResource(unit.CarriedResource, amount);
            player.Stats.AddGathered(unit.CarriedResource, amount);
            world.Events.RaiseResourceDelivered(unit, unit.CarriedResource, amount);
        }

        unit.CarriedAmount = 0f;
        unit.ClearPath();

        // Back to work — to the old deposit, otherwise to the next of the same kind.
        ResourceNode? node = world.Entities.GetResourceNode(unit.GatherTarget);

        if (node is not null && !node.IsDepleted)
        {
            unit.GatherPhase = GatherPhase.ToNode;
            return;
        }

        if (!RetargetNearestNode(world, unit)) unit.Stop();
    }

    // --- Helpers ---------------------------------------------------------

    /// <summary>Finds the nearest deposit of the same kind. false when none is left.</summary>
    private static bool RetargetNearestNode(SimulationWorld world, Unit unit)
    {
        ResourceType wanted = unit.CarriedResource;

        ResourceNode? best = null;
        float bestDistance = float.MaxValue;

        foreach (ResourceNode candidate in world.Entities.ResourceNodes)
        {
            if (candidate.IsDepleted || candidate.Resource != wanted) continue;

            float distance = unit.Position.DistanceSquaredTo(candidate.Position);

            // Prefer deposits with room, so everything does not pile up around one tree.
            if (!candidate.HasFreeSlot) distance *= 4f;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        if (best is null) return false;

        unit.GatherTarget = best.Id;
        unit.GatherPhase = GatherPhase.ToNode;
        unit.ClearPath();
        return true;
    }

    private static Building? FindNearestDropOff(SimulationWorld world, Unit unit)
    {
        Building? best = null;
        float bestDistance = float.MaxValue;

        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId != unit.OwnerId || !building.IsDropOffPoint) continue;
            if (building.IsUnderConstruction) continue;

            float distance = unit.Position.DistanceSquaredTo(building.Position);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = building;
        }

        return best;
    }

    /// <summary>A point just short of the target — you walk up to the tree, not into it.</summary>
    private static Vector2 ApproachPoint(Vector2 from, Vector2 target, float standOff)
    {
        Vector2 delta = from - target;
        if (delta.LengthSquared() < 0.0001f) delta = Vector2.Right;
        return target + delta.Normalized() * standOff;
    }
}
