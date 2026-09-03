using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Core.Systems;

/// <summary>
/// Baut den Nebel des Krieges pro Spieler neu auf.
/// </summary>
/// <remarks>
/// Nicht jeden Tick, sondern alle paar Ticks: Sichtbarkeit aendert sich langsamer als
/// Positionen, und ein voller Neuaufbau ueber 16.384 Kacheln waere 20 Mal pro Sekunde
/// pure Verschwendung. Bei fuenf Aktualisierungen je Sekunde sieht man keinen
/// Unterschied, aber die Tick-Zeit halbiert sich.
/// </remarks>
public sealed class VisionSystem : ISimulationSystem
{
    /// <summary>Neuaufbau alle N Ticks. Bei 20 Hz sind das fuenf Aktualisierungen pro Sekunde.</summary>
    public const int RebuildInterval = 4;

    public string Name => "Vision";

    /// <summary>Abschaltbar fuer Tests und Debug — dann sieht jeder alles.</summary>
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

    /// <summary>Deckt fuer alle Spieler die ganze Karte auf.</summary>
    public void RevealAll(SimulationWorld world)
    {
        Enabled = false;
        foreach (Player player in world.Players) player.Vision?.RevealAll();
    }
}
