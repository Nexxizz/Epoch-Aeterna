using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
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

    /// <summary>Position des Start-Rathauses auf der XZ-Ebene.</summary>
    public required Vector2 StartPosition { get; init; }
}

/// <summary>Alles, was eine Partie definiert. Spaeter aus dem Hauptmenue befuellt.</summary>
public sealed class MatchConfig
{
    public ulong Seed { get; init; } = 12345;
    public List<PlayerConfig> Players { get; init; } = new();
    public int StartingSettlers { get; init; } = 4;

    public ResourceSet StartingResources { get; init; } = new()
    {
        Food = 200,
        Wood = 200,
        Stone = 100,
        Gold = 100,
    };
}

/// <summary>
/// Baut aus einer <see cref="MatchConfig"/> eine spielfertige <see cref="SimulationWorld"/>.
/// </summary>
/// <remarks>
/// Bewusst getrennt vom Einstiegs-Node: Diese Methode braucht keinen SceneTree und
/// laesst sich daher in Tests und in der KI-Vorausberechnung genauso aufrufen.
/// </remarks>
public static class MatchSetup
{
    public const string TownCenterId = "bld_towncenter";
    public const string SettlerId = "unit_settler";

    public static SimulationWorld Build(MatchConfig config, DefinitionDatabase definitions)
    {
        var world = new SimulationWorld(definitions, config.Seed);

        // Reihenfolge ist Absicht: Produktion setzt Einheiten, die im selben Tick
        // noch nicht bewegt werden sollen — Movement laeuft daher zuerst.
        world.AddSystem(new MovementSystem());
        world.AddSystem(new ProductionSystem());

        foreach (PlayerConfig playerConfig in config.Players)
        {
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

            SpawnStartingBase(world, playerConfig, config.StartingSettlers);
        }

        // Startbestand sofort uebernehmen, damit Views und HUD schon vor dem
        // ersten Tick einen vollstaendigen Weltzustand sehen.
        world.FlushSpawns();
        return world;
    }

    private static void SpawnStartingBase(SimulationWorld world, PlayerConfig config, int settlerCount)
    {
        Building? townCenter = world.SpawnBuilding(TownCenterId, config.Id, config.StartPosition);
        if (townCenter is null) return;

        // Siedler im Halbkreis vor dem Rathaus aufstellen.
        const float radius = 5f;
        for (int i = 0; i < settlerCount; i++)
        {
            float angle = Mathf.Pi * (i + 0.5f) / settlerCount;
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            world.SpawnUnit(SettlerId, config.Id, config.StartPosition + offset);
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
                StartPosition = new Vector2(-25f, 0f),
            },
            new PlayerConfig
            {
                Id = 2,
                Name = "KI",
                Color = new Color(0.80f, 0.25f, 0.20f),
                TeamId = 2,
                IsHuman = false,
                StartPosition = new Vector2(25f, 0f),
            },
        },
    };
}
