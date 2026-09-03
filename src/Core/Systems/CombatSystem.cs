using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Target selection, closing in and striking — for units as much as for defensive buildings.
/// </summary>
/// <remarks>
/// The stance decides how far a unit goes on its own: Aggressive pursues,
/// Defensive fights back and returns, Hold stands still. Without that distinction
/// troops chase scouts one by one and dissolve.
/// </remarks>
public sealed class CombatSystem : ISimulationSystem
{
    /// <summary>How far a defensive unit strays from its guard post, in metres.</summary>
    private const float DefensiveLeash = 8f;

    /// <summary>Extra reach so units do not oscillate at the edge of their range.</summary>
    private const float RangeTolerance = 0.35f;

    public string Name => "Combat";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        foreach (Unit unit in world.Entities.Units) TickUnit(world, unit, deltaSeconds);
        foreach (Building building in world.Entities.Buildings) TickBuilding(world, building, deltaSeconds);
    }

    // --- Units -----------------------------------------------------------

    private static void TickUnit(SimulationWorld world, Unit unit, float deltaSeconds)
    {
        unit.AttackCooldownLeft = Mathf.Max(0f, unit.AttackCooldownLeft - deltaSeconds);

        if (unit.AttackDamage <= 0f) return;

        Entity? target = world.Entities.Get(unit.AttackTarget);

        // Target gone or dead: search again or stop, depending on the order.
        if (target is null || !target.IsAlive || target.OwnerId == unit.OwnerId)
        {
            unit.AttackTarget = EntityId.None;

            if (unit.Order == UnitOrder.Attack)
            {
                if (!TryAcquire(world, unit)) ReturnToGuard(unit);
                return;
            }

            if (ShouldLookForTargets(unit)) TryAcquire(world, unit);
            return;
        }

        float reach = unit.AttackRange + unit.Radius + RadiusOf(target);
        float distance = unit.Position.DistanceTo(target.Position);

        if (distance > reach + RangeTolerance)
        {
            Chase(world, unit, target, reach);
            return;
        }

        // In range: stop and strike.
        unit.ClearPath();
        FaceTarget(unit, target.Position);

        if (unit.AttackCooldownLeft > 0f) return;

        unit.AttackCooldownLeft = unit.AttackCooldownSeconds;

        if (unit.UsesProjectile) LaunchProjectile(world, unit, target);
        else world.ApplyDamage(target, unit.AttackDamage, unit.DamageType, unit.OwnerId);
    }

    private static void Chase(SimulationWorld world, Unit unit, Entity target, float reach)
    {
        if (unit.Stance == Stance.HoldPosition)
        {
            unit.AttackTarget = EntityId.None;
            return;
        }

        // Defensive units do not pursue indefinitely.
        if (unit.Stance == Stance.Defensive && unit.Order != UnitOrder.Attack &&
            target.Position.DistanceTo(unit.GuardPosition) > DefensiveLeash)
        {
            unit.AttackTarget = EntityId.None;
            ReturnToGuard(unit);
            return;
        }

        if (unit.HasPath || unit.NeedsPath) return;

        Vector2 delta = unit.Position - target.Position;
        if (delta.LengthSquared() < 0.0001f) delta = Vector2.Right;

        unit.StartMoveTo(target.Position + delta.Normalized() * reach * 0.85f);
    }

    private static void ReturnToGuard(Unit unit)
    {
        if (unit.Order == UnitOrder.Attack) unit.Order = UnitOrder.Idle;
        if (unit.Order != UnitOrder.Idle) return;

        if (unit.Position.DistanceTo(unit.GuardPosition) > 1.5f) unit.StartMoveTo(unit.GuardPosition);
    }

    /// <summary>Only idle, attacking or attack-moving units look for targets themselves.</summary>
    private static bool ShouldLookForTargets(Unit unit) =>
        unit.AutoEngages &&
        unit.Stance != Stance.HoldPosition &&
        unit.Order is UnitOrder.Idle or UnitOrder.AttackMove or UnitOrder.Attack;

    /// <summary>Nearest enemy target within sight range.</summary>
    private static bool TryAcquire(SimulationWorld world, Unit unit)
    {
        float range = unit.Stance == Stance.HoldPosition ? unit.AttackRange : unit.VisionRange;

        Entity? best = FindNearestEnemy(world, unit.OwnerId, unit.Position, range);
        if (best is null) return false;

        unit.AttackTarget = best.Id;
        return true;
    }

    // --- Buildings -------------------------------------------------------

    private static void TickBuilding(SimulationWorld world, Building building, float deltaSeconds)
    {
        building.AttackCooldownLeft = Mathf.Max(0f, building.AttackCooldownLeft - deltaSeconds);

        if (building.AttackDamage <= 0f || building.IsUnderConstruction) return;

        Entity? target = world.Entities.Get(building.AttackTarget);
        float reach = building.AttackRange + building.FootprintRadius;

        if (target is null || !target.IsAlive ||
            building.Position.DistanceTo(target.Position) > reach)
        {
            target = FindNearestEnemy(world, building.OwnerId, building.Position, reach);
            building.AttackTarget = target?.Id ?? EntityId.None;
        }

        if (target is null || building.AttackCooldownLeft > 0f) return;

        building.AttackCooldownLeft = building.AttackCooldownSeconds;

        world.Projectiles.Add(new Projectile
        {
            OwnerPlayerId = building.OwnerId,
            TargetId = target.Id,
            Origin = building.Position,
            Position = building.Position,
            TargetPosition = target.Position,
            Damage = building.AttackDamage,
            DamageType = building.DamageType,
            Speed = building.ProjectileSpeed,
            OriginHeight = 5.0f,
            TotalDistance = building.Position.DistanceTo(target.Position),
        });
    }

    // --- Shared ----------------------------------------------------------

    private static void LaunchProjectile(SimulationWorld world, Unit unit, Entity target)
    {
        world.Projectiles.Add(new Projectile
        {
            OwnerPlayerId = unit.OwnerId,
            TargetId = target.Id,
            Origin = unit.Position,
            Position = unit.Position,
            TargetPosition = target.Position,
            Damage = unit.AttackDamage,
            DamageType = unit.DamageType,
            Speed = unit.ProjectileSpeed,
            TotalDistance = unit.Position.DistanceTo(target.Position),
        });
    }

    private static Entity? FindNearestEnemy(SimulationWorld world, int ownerId, Vector2 from, float range)
    {
        Player? owner = world.GetPlayer(ownerId);
        float rangeSquared = range * range;

        Entity? best = null;
        float bestDistance = float.MaxValue;

        foreach (Unit candidate in world.Entities.Units)
        {
            if (!IsEnemy(world, owner, candidate.OwnerId)) continue;

            float distance = from.DistanceSquaredTo(candidate.Position);
            if (distance > rangeSquared || distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        // Buildings only when no unit is in range — otherwise troops hammer on walls
        // while being shot at.
        if (best is not null) return best;

        foreach (Building candidate in world.Entities.Buildings)
        {
            if (!IsEnemy(world, owner, candidate.OwnerId)) continue;

            float distance = from.DistanceSquaredTo(candidate.Position);
            if (distance > rangeSquared || distance >= bestDistance) continue;

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    private static bool IsEnemy(SimulationWorld world, Player? owner, int otherId)
    {
        if (owner is null || otherId == 0 || otherId == owner.Id) return false;

        Player? other = world.GetPlayer(otherId);
        return other is not null && other.TeamId != owner.TeamId;
    }

    private static float RadiusOf(Entity entity) => entity switch
    {
        Building building => building.FootprintRadius,
        Unit unit => unit.Radius,
        _ => 0.5f,
    };

    private static void FaceTarget(Unit unit, Vector2 target)
    {
        unit.Rotation = Facing.LookAt(unit.Position, target);
    }
}
