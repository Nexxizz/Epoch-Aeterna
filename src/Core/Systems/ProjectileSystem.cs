using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Makes projectiles fly and applies damage on impact.
/// </summary>
/// <remarks>
/// The damage deliberately lands on impact, not on release. Otherwise the target
/// dies before the arrow reaches it — which makes ranged unfair against melee and
/// simply looks wrong.
/// </remarks>
public sealed class ProjectileSystem : ISimulationSystem
{
    /// <summary>Distance at which the impact counts.</summary>
    private const float HitRadius = 0.5f;

    public string Name => "Projectiles";

    public int ActiveCount { get; private set; }

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        for (int i = world.Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = world.Projectiles[i];

            // Keep aiming at the target while it lives — otherwise at the last known
            // position, so the projectile does not hang in mid-air.
            Entity? target = world.Entities.Get(projectile.TargetId);
            if (target is not null && target.IsAlive) projectile.TargetPosition = target.Position;

            Vector2 toTarget = projectile.TargetPosition - projectile.Position;
            float distance = toTarget.Length();
            float step = projectile.Speed * deltaSeconds;

            if (distance <= Mathf.Max(step, HitRadius))
            {
                projectile.Position = projectile.TargetPosition;
                projectile.HasLanded = true;

                if (target is not null && target.IsAlive)
                {
                    world.ApplyDamage(target, projectile.Damage, projectile.DamageType, projectile.OwnerPlayerId);
                }

                world.Events.RaiseProjectileLanded(projectile);
                world.Projectiles.RemoveAt(i);
                continue;
            }

            projectile.Position += toTarget / distance * step;

            float travelled = projectile.Origin.DistanceTo(projectile.Position);
            projectile.FlightProgress = projectile.TotalDistance > 0.01f
                ? Mathf.Clamp(travelled / projectile.TotalDistance, 0f, 1f)
                : 1f;
        }

        ActiveCount = world.Projectiles.Count;
    }
}
