using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Drives construction sites forward as long as settlers work on them.
/// </summary>
/// <remarks>
/// Several settlers build faster, but with diminishing returns: the second still
/// contributes a lot, the fifth barely anything. Without that damping it would
/// always be optimal to send every available settler to every site — and a
/// decision without a trade-off is not a decision.
/// </remarks>
public sealed class ConstructionSystem : ISimulationSystem
{
    private const float WorkRange = 1.6f;

    public string Name => "Construction";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        // Reset the counter: whoever helps reports in during this tick.
        foreach (Building building in world.Entities.Buildings) building.ActiveBuilders = 0;

        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.Order != UnitOrder.Build) continue;

            Building? site = world.Entities.GetBuilding(unit.BuildTarget);

            if (site is null || !site.IsUnderConstruction)
            {
                unit.Stop();
                continue;
            }

            float reach = site.FootprintRadius + WorkRange + unit.Radius;

            if (unit.Position.DistanceTo(site.Position) > reach)
            {
                if (!unit.HasPath && !unit.NeedsPath) unit.StartMoveTo(ApproachPoint(unit, site));
                continue;
            }

            unit.ClearPath();
            site.ActiveBuilders++;
        }

        AdvanceSites(world, deltaSeconds);
    }

    private static void AdvanceSites(SimulationWorld world, float deltaSeconds)
    {
        foreach (Building site in world.Entities.Buildings)
        {
            if (!site.IsUnderConstruction || site.ActiveBuilders == 0) continue;
            if (site.BuildTimeSeconds <= 0f) continue;

            int stageBefore = site.ConstructionStage;

            float rate = EffectiveBuilders(site.ActiveBuilders) / site.BuildTimeSeconds;
            site.ConstructionProgress = Mathf.Min(1f, site.ConstructionProgress + rate * deltaSeconds);

            // Health grows with progress — a fresh site is easy to destroy, an almost
            // finished building barely so.
            site.Health = Mathf.Max(site.Health, site.MaxHealth * Mathf.Max(0.05f, site.ConstructionProgress));

            if (site.ConstructionStage != stageBefore) world.Events.RaiseConstructionStageChanged(site);

            if (site.ConstructionProgress < 1f) continue;

            site.Health = site.MaxHealth;
            Player? owner = world.GetPlayer(site.OwnerId);
            if (owner is not null) owner.Stats.BuildingsCompleted++;
            world.Events.RaiseConstructionCompleted(site);

            ReleaseBuilders(world, site);
        }
    }

    /// <summary>
    /// Effective build output for n settlers. The first counts fully, every further
    /// one only with a diminishing share.
    /// </summary>
    private static float EffectiveBuilders(int count)
    {
        float total = 0f;
        for (int i = 0; i < count; i++) total += 1f / (1f + i * 0.6f);
        return total;
    }

    /// <summary>Finished — the settlers are available again.</summary>
    private static void ReleaseBuilders(SimulationWorld world, Building site)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.Order == UnitOrder.Build && unit.BuildTarget == site.Id) unit.Stop();
        }
    }

    private static Vector2 ApproachPoint(Unit unit, Building site)
    {
        Vector2 delta = unit.Position - site.Position;
        if (delta.LengthSquared() < 0.0001f) delta = Vector2.Right;
        return site.Position + delta.Normalized() * (site.FootprintRadius + WorkRange);
    }
}
