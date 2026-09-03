using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Rebuilds the fog of war for every player.
/// </summary>
/// <remarks>
/// Not every tick but every few: visibility changes more slowly than positions, and
/// a full rebuild over 16,384 tiles twenty times a second would be pure waste. At
/// five updates per second there is no visible difference, but the tick time
/// halves.
/// </remarks>
public sealed class VisionSystem : ISimulationSystem
{
    /// <summary>Rebuild every N ticks. At 20 Hz that is five updates per second.</summary>
    public const int RebuildInterval = 4;

    public string Name => "Vision";

    /// <summary>Can be turned off for tests and debugging — then everyone sees everything.</summary>
    public bool Enabled { get; set; } = true;

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        if (!Enabled) return;
        if (world.CurrentTick % RebuildInterval != 0) return;

        foreach (Player player in world.Players)
        {
            VisionGrid? vision = player.Vision;
            if (vision is null) continue;

            vision.BeginRebuild();

            foreach (Unit unit in world.Entities.Units)
            {
                if (unit.OwnerId == player.Id) vision.Reveal(world.Nav, unit.Position, unit.VisionRange);
            }

            foreach (Building building in world.Entities.Buildings)
            {
                if (building.OwnerId == player.Id) vision.Reveal(world.Nav, building.Position, building.VisionRange);
            }

            vision.EndRebuild();
        }
    }

    /// <summary>Reveals the whole map for every player.</summary>
    public void RevealAll(SimulationWorld world)
    {
        Enabled = false;
        foreach (Player player in world.Players) player.Vision?.RevealAll();
    }
}
