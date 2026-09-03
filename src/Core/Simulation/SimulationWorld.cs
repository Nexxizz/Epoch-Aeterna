using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Der gesamte Spielzustand und seine Fortschreibung. Enthaelt keinerlei Godot-Nodes:
/// Die Welt laesst sich headless ticken, was Tests, KI-Simulation und spaeter
/// deterministische Wiederholbarkeit ermoeglicht.
/// </summary>
public sealed class SimulationWorld
{
    /// <summary>Feste Simulationsrate. Bewusst niedriger als die Bildrate — die Views interpolieren.</summary>
    public const int TicksPerSecond = 20;

    public const float TickDelta = 1f / TicksPerSecond;

    public EntityRegistry Entities { get; } = new();

    /// <summary>Hoehen, Begehbarkeit und Belegung der Karte. Von Sim und Darstellung gemeinsam genutzt.</summary>
    public NavGrid Nav { get; }

    public DefinitionDatabase Definitions { get; }
    public GameEvents Events { get; } = new();
    public CommandQueue Commands { get; } = new();

    /// <summary>Konter-Matrix. Aus den Daten geladen, mit spielbarem Fallback.</summary>
    public CombatTable Combat { get; set; } = new();

    /// <summary>Fliegende Geschosse. Bewusst keine Entities — siehe <see cref="Projectile"/>.</summary>
    public List<Projectile> Projectiles { get; } = new();

    /// <summary>Gesaater Zufall — nie System.Random verwenden, sonst bricht die Reproduzierbarkeit.</summary>
    public RandomNumberGenerator Random { get; } = new();

    public int CurrentTick { get; private set; }
    public float ElapsedSeconds => CurrentTick * TickDelta;

    /// <summary>Gesetzt, sobald die Partie entschieden ist. Null, solange sie laeuft.</summary>
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

        // Registry-Ereignisse auf den oeffentlichen Bus durchreichen, damit Views
        // nur eine Stelle abonnieren muessen.
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

    /// <summary>Ein Simulationsschritt. Immer <see cref="TickDelta"/> lang, unabhaengig von der Bildrate.</summary>
    public void Tick()
    {
        if (IsOver) return;

        // 1. Zustand vor dem Tick sichern — die Views blenden spaeter dazwischen.
        Entities.CaptureInterpolationSnapshots();

        // 2. Befehle des vergangenen Frames anwenden.
        Commands.ExecutePending(this);

        // 3. Systemlogik.
        foreach (ISimulationSystem system in _systems) system.Tick(this, TickDelta);

        // 4. Spawns und Tode uebernehmen, dann abgeleitete Werte auffrischen.
        Entities.Flush();
        foreach (Player player in _players) player.RecalculatePopulation(Entities);

        CurrentTick++;
    }

    // --- Schaden ---------------------------------------------------------

    /// <summary>
    /// Bringt Schaden an und toetet die Entity, wenn sie dabei auf null faellt.
    /// </summary>
    /// <remarks>
    /// Einziger Weg, Lebenspunkte zu senken. Damit gibt es genau eine Stelle, an der
    /// Ruestung, Konter-Matrix, Statistik und Todesmeldung zusammenlaufen.
    /// </remarks>
    public void ApplyDamage(Entity target, float rawDamage, DamageType damageType, int attackerPlayerId)
    {
        if (!target.IsAlive) return;

        (float armor, ArmorClass armorClass) = ArmorOf(target);

        float multiplier = Combat.Get(damageType, armorClass);

        // Ruestung zieht ab, der Konter multipliziert. Mindestens 1 Schaden, damit
        // hohe Ruestung nicht zu voelliger Unverwundbarkeit fuehrt.
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

    /// <summary>Raeumt die Kachelsperre auf, wenn ein Gebaeude oder Vorkommen verschwindet.</summary>
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
            GD.PushError($"[Spawn] Unbekannte Einheiten-Id '{definitionId}'.");
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
            GD.PushError($"[Spawn] Unbekannte Gebaeude-Id '{definitionId}'.");
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

        // Grundflaeche sperren, damit die Wegfindung das Gebaeude sofort umgeht.
        Nav.ApplyFootprint(position, building.Footprint, blocked: true);

        return Entities.Add(building);
    }

    public ResourceNode? SpawnResourceNode(string definitionId, Vector2 position, float rotation = 0f)
    {
        var definition = Definitions.GetResourceNode(definitionId);
        if (definition is null)
        {
            GD.PushError($"[Spawn] Unbekannte Vorkommen-Id '{definitionId}'.");
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
    /// Uebernimmt gepufferte Spawns sofort, statt bis zum Tick-Ende zu warten.
    /// Nur fuer den Matchaufbau vor dem ersten Tick gedacht.
    /// </summary>
    public void FlushSpawns()
    {
        Entities.Flush();
        foreach (Player player in _players) player.RecalculatePopulation(Entities);
    }
}
