using System;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// One-way reporting channel from the simulation to everything outside: views, HUD, VFX, audio, AI.
/// </summary>
/// <remarks>
/// The direction is strictly sim to outside world. Anything that wants to change the
/// simulation sends a command through the <see cref="CommandQueue"/>. That keeps the sim
/// self-contained and, later, deterministically reproducible.
/// </remarks>
public sealed class GameEvents
{
    public event Action<Entity>? EntitySpawned;
    public event Action<Entity>? EntityRemoved;
    public event Action<Entity, float>? EntityDamaged;

    public event Action<Building, string>? ProductionQueued;
    public event Action<Building, Unit>? ProductionCompleted;

    /// <summary>A construction site reached a new visible stage.</summary>
    public event Action<Building>? ConstructionStageChanged;

    public event Action<Building>? ConstructionCompleted;

    /// <summary>A settler delivered — the basis for the rising number text.</summary>
    public event Action<Unit, ResourceType, int>? ResourceDelivered;

    public event Action<Player, AgeDefinition>? AgeAdvanced;
    public event Action<Player>? PlayerDefeated;
    public event Action<Player?>? MatchEnded;

    public event Action<Projectile>? ProjectileLanded;

    internal void RaiseEntitySpawned(Entity entity) => EntitySpawned?.Invoke(entity);

    internal void RaiseEntityRemoved(Entity entity) => EntityRemoved?.Invoke(entity);

    internal void RaiseEntityDamaged(Entity entity, float amount) => EntityDamaged?.Invoke(entity, amount);

    internal void RaiseProductionQueued(Building building, string unitId) =>
        ProductionQueued?.Invoke(building, unitId);

    internal void RaiseProductionCompleted(Building building, Unit unit) =>
        ProductionCompleted?.Invoke(building, unit);

    internal void RaiseConstructionStageChanged(Building building) =>
        ConstructionStageChanged?.Invoke(building);

    internal void RaiseConstructionCompleted(Building building) =>
        ConstructionCompleted?.Invoke(building);

    internal void RaiseResourceDelivered(Unit unit, ResourceType type, int amount) =>
        ResourceDelivered?.Invoke(unit, type, amount);

    internal void RaiseAgeAdvanced(Player player, AgeDefinition age) => AgeAdvanced?.Invoke(player, age);

    internal void RaisePlayerDefeated(Player player) => PlayerDefeated?.Invoke(player);

    internal void RaiseMatchEnded(Player? winner) => MatchEnded?.Invoke(winner);

    internal void RaiseProjectileLanded(Projectile projectile) => ProjectileLanded?.Invoke(projectile);
}
