using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Erkennt, wann ein Spieler ausgeschieden ist, und beendet die Partie.
/// </summary>
/// <remarks>
/// Besiegt ist, wer weder Gebaeude noch Siedler hat — reine Militaereinheiten
/// reichen nicht, weil man damit nichts wieder aufbauen kann. Genau so handhabt es
/// Empire Earth auch: Eine Armee ohne Basis ist nur noch ein Nachspiel.
/// </remarks>
public sealed class VictorySystem : ISimulationSystem
{
    /// <summary>Nur alle paar Ticks pruefen — der Zustand aendert sich selten.</summary>
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

            // Teams zaehlen, nicht Spieler — sonst endet ein 2v2 nie.
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
