using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Detects when a player is out and ends the match.
/// </summary>
/// <remarks>
/// Defeated means having neither buildings nor settlers — pure military units are
/// not enough, because you cannot rebuild with them. Empire Earth handles it the
/// same way: an army without a base is only an epilogue.
/// </remarks>
public sealed class VictorySystem : ISimulationSystem
{
    /// <summary>Only checked every few ticks — the state rarely changes.</summary>
    private const int CheckInterval = 10;

    public string Name => "Victory";

    public void Tick(SimulationWorld world, float deltaSeconds)
    {
        if (world.IsOver) return;
        if (world.CurrentTick % CheckInterval != 0) return;

        foreach (Player player in world.Players)
        {
            if (player.IsDefeated || CanStillPlay(world, player)) continue;

            player.IsDefeated = true;
            world.Events.RaisePlayerDefeated(player);
        }

        CheckForWinner(world);
    }

    private static bool CanStillPlay(SimulationWorld world, Player player)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == player.Id) return true;
        }

        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.OwnerId == player.Id && unit.CanBuild) return true;
        }

        return false;
    }

    private static void CheckForWinner(SimulationWorld world)
    {
        Player? survivor = null;
        int remainingTeams = 0;

        foreach (Player player in world.Players)
        {
            if (player.IsDefeated) continue;

            // Count teams, not players — otherwise a 2v2 would never end.
            if (survivor is null || survivor.TeamId != player.TeamId)
            {
                remainingTeams++;
                survivor ??= player;
            }
        }

        if (remainingTeams > 1) return;

        world.Winner = survivor;
        world.IsOver = true;
        world.Events.RaiseMatchEnded(survivor);
    }
}
