using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Game;

/// <summary>A player's starting conditions.</summary>
public sealed class PlayerConfig
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required Color Color { get; init; }
    public int TeamId { get; init; }
    public bool IsHuman { get; init; }
}

/// <summary>Everything that defines a match. Filled from the main menu later on.</summary>
public sealed class MatchConfig
{
    public ulong Seed { get; init; } = 12345;
    public List<PlayerConfig> Players { get; init; } = new();
    public int StartingSettlers { get; init; } = 4;
    public int MapSize { get; init; } = 128;

    /// <summary>For tests and debugging: switch off the fog of war.</summary>
    public bool FogOfWar { get; init; } = true;

    public ResourceSet StartingResources { get; init; } = new()
    {
        Food = 250,
        Wood = 250,
        Stone = 150,
        Gold = 100,
    };
}

/// <summary>An assembled match: the simulation plus the map it grew from.</summary>
public sealed class Match
{
    public required SimulationWorld World { get; init; }
    public required GeneratedMap Map { get; init; }
}

/// <summary>
/// Turns a <see cref="MatchConfig"/> into a match ready to play.
/// </summary>
/// <remarks>
/// Deliberately separate from the entry node: this method needs no SceneTree and can
/// therefore be called from tests and from AI look-ahead in exactly the same way.
/// </remarks>
public static class MatchSetup
{
    public const string TownCenterId = "bld_towncenter";
    public const string SettlerId = "unit_settler";

    public static Match Build(MatchConfig config, DefinitionDatabase definitions)
    {
        GeneratedMap map = MapGenerator.Generate(config.Seed, config.MapSize, config.MapSize);
        var world = new SimulationWorld(definitions, map.Grid, config.Seed)
        {
            Combat = definitions.Combat,
        };

        RegisterSystems(world, map, config);
        SpawnResourceNodes(world, map);

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

        // Commit the starting population immediately, so views and HUD see a complete
        // world state before the first tick.
        world.FlushSpawns();

        // Set the initial visibility, otherwise the match starts in the dark.
        world.GetSystem<VisionSystem>()?.Tick(world, SimulationWorld.TickDelta);

        return new Match { World = world, Map = map };
    }

    /// <summary>
    /// The order of the systems. It is part of the game rules, not an afterthought:
    /// first decide and move, then fight, then evaluate.
    /// </summary>
    private static void RegisterSystems(SimulationWorld world, GeneratedMap map, MatchConfig config)
    {
        world.AddSystem(new GatheringSystem());
        world.AddSystem(new ConstructionSystem());
        world.AddSystem(new CombatSystem());
        world.AddSystem(new PathfindingSystem(map.Grid));
        world.AddSystem(new MovementSystem(map.Grid));
        world.AddSystem(new AvoidanceSystem(map.Grid));
        world.AddSystem(new ProjectileSystem());
        world.AddSystem(new ProductionSystem());
        world.AddSystem(new AgeSystem());
        world.AddSystem(new VisionSystem { Enabled = config.FogOfWar });
        world.AddSystem(new VictorySystem());
    }

    private static void SpawnResourceNodes(SimulationWorld world, GeneratedMap map)
    {
        foreach (ResourceSpot spot in map.ResourceSpots)
        {
            world.SpawnResourceNode(spot.DefinitionId, spot.Position, spot.Rotation);
        }
    }

    private static void SpawnStartingBase(SimulationWorld world, int ownerId, Vector2 position, int settlerCount)
    {
        Building? townCenter = world.SpawnBuilding(TownCenterId, ownerId, position);
        if (townCenter is null) return;

        // Place the settlers in a semicircle in front of the town centre.
        const float radius = 7f;
        for (int i = 0; i < settlerCount; i++)
        {
            float angle = Mathf.Pi * (i + 0.5f) / settlerCount;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            world.SpawnUnit(SettlerId, ownerId, position + offset);
        }
    }

    /// <summary>Two-player setup for the test map.</summary>
    public static MatchConfig DefaultSkirmish() => new()
    {
        Players =
        {
            new PlayerConfig
            {
                Id = 1,
                Name = "Player",
                Color = new Color(0.20f, 0.45f, 0.85f),
                TeamId = 1,
                IsHuman = true,
            },
            new PlayerConfig
            {
                Id = 2,
                Name = "Computer",
                Color = new Color(0.80f, 0.25f, 0.20f),
                TeamId = 2,
                IsHuman = false,
            },
        },
    };
}
