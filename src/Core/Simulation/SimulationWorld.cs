using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;

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
    public DefinitionDatabase Definitions { get; }
    public GameEvents Events { get; } = new();
    public CommandQueue Commands { get; } = new();

    /// <summary>Gesaater Zufall — nie System.Random verwenden, sonst bricht die Reproduzierbarkeit.</summary>
    public RandomNumberGenerator Random { get; } = new();

    public int CurrentTick { get; private set; }
    public float ElapsedSeconds => CurrentTick * TickDelta;

    private readonly List<Player> _players = new();
    private readonly Dictionary<int, Player> _playersById = new();
    private readonly List<ISimulationSystem> _systems = new();

    public IReadOnlyList<Player> Players => _players;
    public IReadOnlyList<ISimulationSystem> Systems => _systems;

    public SimulationWorld(DefinitionDatabase definitions, ulong seed = 0)
    {
        Definitions = definitions;
        Random.Seed = seed;

        // Registry-Ereignisse auf den oeffentlichen Bus durchreichen, damit Views
        // nur eine Stelle abonnieren muessen.
        Entities.EntityAdded += entity => Events.RaiseEntitySpawned(entity);
        Entities.EntityRemoved += entity => Events.RaiseEntityRemoved(entity);
    }

    public void AddPlayer(Player player)
    {
        _players.Add(player);
        _playersById[player.Id] = player;
    }

    public Player? GetPlayer(int id) => _playersById.GetValueOrDefault(id);

    public void AddSystem(ISimulationSystem system) => _systems.Add(system);

    /// <summary>Ein Simulationsschritt. Immer <see cref="TickDelta"/> lang, unabhaengig von der Bildrate.</summary>
    public void Tick()
    {
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

    public Building? SpawnBuilding(string definitionId, int ownerId, Vector2 position, float rotation = 0f)
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
        return Entities.Add(building);
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
