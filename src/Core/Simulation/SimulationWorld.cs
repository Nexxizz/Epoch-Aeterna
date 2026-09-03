using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// The entire game state and how it advances. Contains no Godot nodes at all:
/// the world can be ticked headlessly, which is what makes tests, AI simulation and,
/// later, deterministic repeatability possible.
/// </summary>
public sealed class SimulationWorld
{
    /// <summary>Fixed simulation rate. Deliberately below the frame rate — the views interpolate.</summary>
    public const int TicksPerSecond = 20;

    public const float TickDelta = 1f / TicksPerSecond;

    public EntityRegistry Entities { get; } = new();

    /// <summary>Heights, walkability and occupancy of the map. Shared by sim and display.</summary>
    public NavGrid Nav { get; }

    public DefinitionDatabase Definitions { get; }
    public GameEvents Events { get; } = new();
    public CommandQueue Commands { get; } = new();

    /// <summary>Counter matrix. Loaded from the data, with a playable fallback.</summary>
    public CombatTable Combat { get; set; } = new();

    /// <summary>Projectiles in flight. Deliberately not entities — see <see cref="Projectile"/>.</summary>
    public List<Projectile> Projectiles { get; } = new();

    /// <summary>Seeded randomness — never use System.Random, or reproducibility breaks.</summary>
    public RandomNumberGenerator Random { get; } = new();

    public int CurrentTick { get; private set; }
    public float ElapsedSeconds => CurrentTick * TickDelta;

    /// <summary>Set once the match is decided. Null while it is still running.</summary>
    public Player? Winner { get; set; }

    public bool IsOver { get; set; }

    private readonly List<Player> _players = new();
    private readonly Dictionary<int, Player> _playersById = new();
    private readonly List<ISimulationSystem> _systems = new();

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<ISimulationSystem> Systems => _systems;

    public SimulationWorld(DefinitionDatabase definitions, NavGrid nav, ulong seed = 0)
    {
        Definitions = definitions;
        Nav = nav;
        Random.Seed = seed;

        // Forward registry events onto the public bus, so views only have to
        // subscribe in one place.
        Entities.EntityAdded += entity => Events.RaiseEntitySpawned(entity);
        Entities.EntityRemoved += OnEntityRemoved;
    }

    public void AddPlayer(Player player)
    {
        player.Vision ??= new VisionGrid(Nav.Width, Nav.Height);
        _players.Add(player);
        _playersById[player.Id] = player;
    }

    public Player? GetPlayer(int id) => _playersById.GetValueOrDefault(id);

    public void AddSystem(ISimulationSystem system) => _systems.Add(system);

    public T? GetSystem<T>() where T : class, ISimulationSystem
    {
        foreach (ISimulationSystem system in _systems)
        {
            if (system is T typed) return typed;
        }
        return null;
    }

    /// <summary>One simulation step. Always <see cref="TickDelta"/> long, regardless of frame rate.</summary>
    public void Tick()
    {
        if (IsOver) return;

        // 1. Capture the pre-tick state — the views blend between the two afterwards.
        Entities.CaptureInterpolationSnapshots();

        // 2. Apply the commands from the past frame.
        Commands.ExecutePending(this);

        // 3. System logic.
        foreach (ISimulationSystem system in _systems) system.Tick(this, TickDelta);

        // 4. Commit spawns and deaths, then refresh derived values.
        Entities.Flush();
        foreach (Player player in _players) player.RecalculatePopulation(Entities);

        CurrentTick++;
    }

    // --- Damage ----------------------------------------------------------

    /// <summary>
    /// Applies damage and kills the entity if that takes it to zero.
    /// </summary>
    /// <remarks>
    /// The only way to lower health. That gives exactly one place where armour,
    /// counter matrix, statistics and the death notification come together.
    /// </remarks>
    public void ApplyDamage(Entity target, float rawDamage, DamageType damageType, int attackerPlayerId)
    {
        if (!target.IsAlive) return;

        (float armor, ArmorClass armorClass) = ArmorOf(target);

        float multiplier = Combat.Get(damageType, armorClass);

        // Armour subtracts, the counter multiplies. At least 1 damage, so that heavy
        // armour does not amount to complete invulnerability.
        float damage = Mathf.Max(1f, (rawDamage - armor) * multiplier);

        target.Health -= damage;
        Events.RaiseEntityDamaged(target, damage);

        if (target.Health > 0f) return;

        target.Health = 0f;
        Kill(target, attackerPlayerId);
    }

    private static (float Armor, ArmorClass Class) ArmorOf(Entity target) => target switch
    {
        Unit unit => (unit.Armor, unit.ArmorClass),
        Building building => (building.Armor, building.ArmorClass),
        _ => (0f, ArmorClass.Building),
    };

    private void Kill(Entity target, int attackerPlayerId)
    {
        Player? attacker = GetPlayer(attackerPlayerId);
        Player? owner = GetPlayer(target.OwnerId);

        if (attacker is not null && attacker.Id != target.OwnerId) attacker.Stats.EnemiesKilled++;

        switch (target)
        {
            case Unit when owner is not null: owner.Stats.UnitsLost++; break;
            case Building when owner is not null: owner.Stats.BuildingsLost++; break;
        }

        Entities.Remove(target.Id);
    }

    /// <summary>Clears the tile block when a building or deposit disappears.</summary>
    private void OnEntityRemoved(Entity entity)
    {
        switch (entity)
        {
            case Building building:
                Nav.ApplyFootprint(building.Position, building.Footprint, blocked: false);
                break;

            case ResourceNode node when node.BlocksMovement:
                Vector2I cell = Nav.WorldToCell(node.Position);
                Nav.Unblock(cell.X, cell.Y, BlockFlags.Decoration);
                break;
        }

        Events.RaiseEntityRemoved(entity);
    }

    // --- Spawning --------------------------------------------------------

    public Unit? SpawnUnit(string definitionId, int ownerId, Vector2 position, float rotation = 0f)
    {
        UnitDefinition? definition = Definitions.GetUnit(definitionId);
        if (definition is null)
        {
            GD.PushError($"[Spawn] Unknown unit id '{definitionId}'.");
            return null;
        }

        var unit = new Unit
        {
            OwnerId = ownerId,
            DefinitionId = definitionId,
            Position = position,
            Rotation = rotation,
        };
        unit.ApplyDefinition(definition);
        return Entities.Add(unit);
    }

    public Building? SpawnBuilding(string definitionId, int ownerId, Vector2 position,
        bool underConstruction = false, float rotation = 0f)
    {
        BuildingDefinition? definition = Definitions.GetBuilding(definitionId);
        if (definition is null)
        {
            GD.PushError($"[Spawn] Unknown building id '{definitionId}'.");
            return null;
        }

        var building = new Building
        {
            OwnerId = ownerId,
            DefinitionId = definitionId,
            Position = position,
            Rotation = rotation,
        };
        building.ApplyDefinition(definition);
        if (underConstruction) building.BeginConstruction();

        // Block the footprint so pathfinding routes around the building immediately.
        Nav.ApplyFootprint(position, building.Footprint, blocked: true);

        return Entities.Add(building);
    }

    public ResourceNode? SpawnResourceNode(string definitionId, Vector2 position, float rotation = 0f)
    {
        var definition = Definitions.GetResourceNode(definitionId);
        if (definition is null)
        {
            GD.PushError($"[Spawn] Unknown resource deposit id '{definitionId}'.");
            return null;
        }

        var node = new ResourceNode
        {
            OwnerId = 0,
            DefinitionId = definitionId,
            Position = position,
            Rotation = rotation,
        };
        node.ApplyDefinition(definition);

        if (node.BlocksMovement)
        {
            Vector2I cell = Nav.WorldToCell(position);
            Nav.Block(cell.X, cell.Y, BlockFlags.Decoration);
        }

        return Entities.Add(node);
    }

    /// <summary>
    /// Commits buffered spawns immediately instead of waiting for the end of the tick.
    /// Intended for match setup, before the first tick.
    /// </summary>
    public void FlushSpawns()
    {
        Entities.Flush();
        foreach (Player player in _players) player.RecalculatePopulation(Entities);
    }
}
