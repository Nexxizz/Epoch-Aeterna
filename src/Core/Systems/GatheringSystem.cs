using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Der Sammelkreislauf: hinlaufen, abbauen, zur Abgabestelle bringen, zurueckkehren.
/// </summary>
/// <remarks>
/// Der Kreislauf laeuft von selbst weiter: Ist ein Vorkommen erschoepft, sucht der
/// Siedler das naechste gleichartige, statt untaetig stehen zu bleiben. Genau das
/// erwartet man in einem RTS, und es erspart staendiges Nachklicken.
/// </remarks>
public sealed class GatheringSystem : ISimulationSystem
{
    /// <summary>Abstand, ab dem am Vorkommen bzw. an der Abgabestelle gearbeitet werden kann.</summary>
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

    // --- Hinweg ----------------------------------------------------------

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

        // Nur nachfordern, wenn gerade kein Pfad laeuft — sonst wuerde jeder Tick
        // eine neue Suche ausloesen und das Budget auffressen.
        if (!unit.HasPath && !unit.NeedsPath) unit.StartMoveTo(ApproachPoint(unit.Position, node.Position, WorkRange));
    }

    // --- Abbau -----------------------------------------------------------

    private static void Harvest(SimulationWorld world, Unit unit, float deltaSeconds)
    {
        ResourceNode? node = world.Entities.GetResourceNode(unit.GatherTarget);

        if (node is null || node.IsDepleted)
        {
            if (node is not null) node.ActiveGatherers = Mathf.Max(0, node.ActiveGatherers - 1);

            // Mit halber Ladung lohnt der Rueckweg trotzdem.
            if (unit.CarriedAmount > 0f) BeginReturn(world, unit);
            else if (!RetargetNearestNode(world, unit)) unit.Stop();
            return;
        }

        unit.CarriedResource = node.Resource;
        unit.HarvestProgress += unit.GatherRatePerSecond * node.GatherRateFactor * deltaSeconds;

        // Erst ganze Einheiten dem Vorkommen entnehmen — sonst wuerde der Vorrat
        // durch Rundung langsam verpuffen.
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

    // --- Rueckweg --------------------------------------------------------

    private static void BeginReturn(SimulationWorld world, Unit unit)
    {
        Building? dropOff = FindNearestDropOff(world, unit);

        if (dropOff is null)
        {
            // Keine Abgabestelle mehr da: die Ladung bleibt erhalten, der Siedler wartet.
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

        // Zurueck an die Arbeit — zum alten Vorkommen, sonst zum naechsten gleichartigen.
        ResourceNode? node = world.Entities.GetResourceNode(unit.GatherTarget);

        if (node is not null && !node.IsDepleted)
        {
            unit.GatherPhase = GatherPhase.ToNode;
            return;
        }

        if (!RetargetNearestNode(world, unit)) unit.Stop();
    }

    // --- Hilfsmittel -----------------------------------------------------

    /// <summary>Sucht das naechste Vorkommen derselben Art. false, wenn keins mehr existiert.</summary>
    private static bool RetargetNearestNode(SimulationWorld world, Unit unit)
    {
        ResourceType wanted = unit.CarriedResource;

        ResourceNode? best = null;
        float bestDistance = float.MaxValue;

        foreach (ResourceNode candidate in world.Entities.ResourceNodes)
        {
            if (candidate.IsDepleted || candidate.Resource != wanted) continue;

            float distance = unit.Position.DistanceSquaredTo(candidate.Position);

            // Volle Vorkommen bevorzugen, damit sich nicht alles an einem Baum staut.
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

    /// <summary>Punkt kurz vor dem Ziel — man laeuft nicht in den Baum, sondern davor.</summary>
    private static Vector2 ApproachPoint(Vector2 from, Vector2 target, float standOff)
    {
        Vector2 delta = from - target;
        if (delta.LengthSquared() < 0.0001f) delta = Vector2.Right;
        return target + delta.Normalized() * standOff;
    }
}
