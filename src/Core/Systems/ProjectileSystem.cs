using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Laesst Geschosse fliegen und richtet beim Aufschlag Schaden an.
/// </summary>
/// <remarks>
/// Der Schaden faellt bewusst erst beim Einschlag an, nicht beim Abschuss. Sonst
/// stirbt das Ziel, bevor der Pfeil es erreicht — was den Fernkampf gegenueber dem
/// Nahkampf unfair macht und schlicht falsch aussieht.
/// </remarks>
public sealed class ProjectileSystem : ISimulationSystem
{
    /// <summary>Abstand, ab dem der Einschlag gilt.</summary>
    private const float HitRadius = 0.5f;

    public string Name => "Projectiles";

    public int ActiveCount { get; private set; }

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        for (int i = world.Projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = world.Projectiles[i];

            // Zielt weiterhin auf das Ziel, solange es lebt — sonst auf die
            // zuletzt bekannte Stelle, damit das Geschoss nicht in der Luft verharrt.
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
