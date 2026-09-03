using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Game;

/// <summary>Startbedingungen eines Spielers.</summary>
public sealed class PlayerConfig
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Color Color { get; init; }
    public int TeamId { get; init; }
    public bool IsHuman { get; init; }
}

/// <summary>Alles, was eine Partie definiert. Spaeter aus dem Hauptmenue befuellt.</summary>
public sealed class MatchConfig
{
    public ulong Seed { get; init; } = 12345;
    public List<PlayerConfig> Players { get; init; } = new();
    public int StartingSettlers { get; init; } = 4;
    public int MapSize { get; init; } = 128;

    public ResourceSet StartingResources { get; init; } = new()
    {
        Food = 200,
        Wood = 200,
        Stone = 100,
        Gold = 100,
    };
}

/// <summary>Eine aufgebaute Partie: Simulation plus die Karte, aus der sie entstanden ist.</summary>
public sealed class Match
{
    public required SimulationWorld World { get; init; }
    public required GeneratedMap Map { get; init; }
}

/// <summary>
/// Baut aus einer <see cref="MatchConfig"/> eine spielfertige Partie.
/// </summary>
/// <remarks>
/// Bewusst getrennt vom Einstiegs-Node: Diese Methode braucht keinen SceneTree und
/// laesst sich daher in Tests und in der KI-Vorausberechnung genauso aufrufen.
/// </remarks>
public static class MatchSetup
{
    public const string TownCenterId = "bld_towncenter";
    public const string SettlerId = "unit_settler";

    public static Match Build(MatchConfig config, DefinitionDatabase definitions)
    {
        GeneratedMap map = MapGenerator.Generate(config.Seed, config.MapSize, config.MapSize);
        var world = new SimulationWorld(definitions, map.Grid, config.Seed);

        // Reihenfolge ist Absicht: erst Wege suchen, dann laufen, dann Ueberlappungen
        // aufloesen, zuletzt produzieren — frische Einheiten bewegen sich erst im Folgetick.
        world.AddSystem(new PathfindingSystem(map.Grid));
        world.AddSystem(new MovementSystem(map.Grid));
        world.AddSystem(new AvoidanceSystem(map.Grid));
        world.AddSystem(new ProductionSystem());

        for (int i = 0; i < config.Players.Count; i++)
        {
            PlayerConfig playerConfig = config.Players[i];

            var player = new Player
            {
                Id = playerConfig.Id,
                Name = playerConfig.Name,
                Color = playerConfig.Color,
                TeamId = playerConfig.TeamId,
                IsHuman = playerConfig.IsHuman,
            };
            player.GrantStartingResources(config.StartingResources);
            world.AddPlayer(player);

            Vector2 start = map.StartPositions[i % map.StartPositions.Count];
            SpawnStartingBase(world, playerConfig.Id, start, config.StartingSettlers);
        }

        // Startbestand sofort uebernehmen, damit Views und HUD schon vor dem
        // ersten Tick einen vollstaendigen Weltzustand sehen.
        world.FlushSpawns();

        return new Match { World = world, Map = map };
    }

    private static void SpawnStartingBase(SimulationWorld world, int ownerId, Vector2 position, int settlerCount)
    {
        Building? townCenter = world.SpawnBuilding(TownCenterId, ownerId, position);
        if (townCenter is null) return;

        // Siedler im Halbkreis vor dem Rathaus aufstellen.
        const float radius = 6f;
        for (int i = 0; i < settlerCount; i++)
        {
            float angle = Mathf.Pi * (i + 0.5f) / settlerCount;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            world.SpawnUnit(SettlerId, ownerId, position + offset);
        }
    }

    /// <summary>Zwei-Spieler-Aufstellung fuer die Testkarte.</summary>
    public static MatchConfig DefaultSkirmish() => new()
    {
        Players =
        {
            new PlayerConfig
            {
                Id = 1,
                Name = "Spieler",
                Color = new Color(0.20f, 0.45f, 0.85f),
                TeamId = 1,
                IsHuman = true,
            },
            new PlayerConfig
            {
                Id = 2,
                Name = "KI",
                Color = new Color(0.80f, 0.25f, 0.20f),
                TeamId = 2,
                IsHuman = false,
            },
        },
    };
}
