using System;
using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Game;

/// <summary>
/// Headless-Durchlauf der Simulation ohne SceneTree, Views oder Eingaben.
/// </summary>
/// <remarks>
/// Belegt, dass die Sim wirklich autark ist: Wenn sie ohne Godot-Nodes laufen kann,
/// ist die Trennung zwischen Simulation und Darstellung tatsaechlich sauber und nicht
/// nur so benannt. Aufruf:
/// <c>Godot_console.exe --headless --path . -- --verify</c>
/// </remarks>
public static class SelfTest
{
    private static readonly List<string> Failures = new();
    private static int _checks;

    public static int Run(DefinitionDatabase definitions)
    {
        Failures.Clear();
        _checks = 0;

        GD.Print("=== Selbsttest Phase 1 ===");

        CheckDefinitions(definitions);

        SimulationWorld world = MatchSetup.Build(MatchSetup.DefaultSkirmish(), definitions);
        CheckMatchSetup(world);
        CheckTickAdvances(world);
        CheckProduction(world, definitions);
        CheckMovement(world);
        CheckPopulationCap(world, definitions);
        CheckRefund(world, definitions);

        GD.Print(Failures.Count == 0
            ? $"\nAlle {_checks} Pruefungen bestanden."
            : $"\n{Failures.Count} von {_checks} Pruefungen FEHLGESCHLAGEN:");

        foreach (string failure in Failures) GD.PrintErr("  - " + failure);

        return Failures.Count == 0 ? 0 : 1;
    }

    // --- Pruefungen ------------------------------------------------------

    private static void CheckDefinitions(DefinitionDatabase definitions)
    {
        Check("Einheiten-Definitionen geladen", definitions.Units.Count >= 2);
        Check("Gebaeude-Definitionen geladen", definitions.Buildings.Count >= 2);
        Check("Zeitalter geladen", definitions.Ages.Count >= 2);
        Check("Zeitalter nach Index sortiert", definitions.GetAge(0)?.Index == 0 && definitions.GetAge(1)?.Index == 1);
        Check("Siedlerkosten aus .tres gelesen", definitions.GetUnit("unit_settler")?.Cost?.Food == 50);
        Check("Rathaus kennt ausbildbare Einheiten",
            definitions.GetBuilding("bld_towncenter")?.TrainableUnitIds.Length == 2);
    }

    private static void CheckMatchSetup(SimulationWorld world)
    {
        Check("Zwei Spieler angelegt", world.Players.Count == 2);
        Check("Zwei Rathaeuser gebaut", world.Entities.Buildings.Count == 2);
        Check("Acht Startsiedler gesetzt", world.Entities.Units.Count == 8);

        Player player = world.Players[0];
        Check("Bevoelkerung aus Entities abgeleitet", player.Population == 4);
        Check("Bevoelkerungslimit vom Rathaus", player.PopulationCap == 5);
        Check("Startressourcen gutgeschrieben", player.GetResource(ResourceType.Food) == 200);
    }

    private static void CheckTickAdvances(SimulationWorld world)
    {
        int before = world.CurrentTick;
        world.Tick();
        Check("Tickzaehler laeuft", world.CurrentTick == before + 1);
        Check("Elapsed passt zur Tickrate",
            Mathf.IsEqualApprox(world.ElapsedSeconds, world.CurrentTick * SimulationWorld.TickDelta));
    }

    private static void CheckProduction(SimulationWorld world, DefinitionDatabase definitions)
    {
        Player player = world.Players[0];
        Building townCenter = FindTownCenter(world, player.Id);

        // Erst Platz schaffen, sonst blockiert das Bevoelkerungslimit die Fertigstellung.
        world.SpawnBuilding("bld_house", player.Id, new Vector2(-30f, -10f));
        world.FlushSpawns();
        Check("Haus hebt das Bevoelkerungslimit", player.PopulationCap == 15);

        int foodBefore = player.GetResource(ResourceType.Food);
        int unitsBefore = world.Entities.Units.Count;

        world.Commands.Enqueue(new TrainUnitCommand
        {
            PlayerId = player.Id,
            Building = townCenter.Id,
            UnitDefinitionId = "unit_settler",
        });

        world.Tick();
        Check("Befehl wurde ausgefuehrt", world.Commands.TotalExecuted > 0);
        Check("Kosten sofort abgebucht", player.GetResource(ResourceType.Food) == foodBefore - 50);
        Check("Posten in der Warteschlange", townCenter.Queue.Count == 1);

        float buildTime = definitions.GetUnit("unit_settler")!.BuildTimeSeconds;
        int ticks = Mathf.CeilToInt(buildTime / SimulationWorld.TickDelta) + 2;
        for (int i = 0; i < ticks; i++) world.Tick();

        Check("Warteschlange abgearbeitet", townCenter.Queue.Count == 0);
        Check("Einheit erschienen", world.Entities.Units.Count == unitsBefore + 1);
        Check("Bevoelkerung mitgewachsen", player.Population == 5);
    }

    private static void CheckMovement(SimulationWorld world)
    {
        Unit unit = world.Entities.Units[0];
        var target = new Vector2(unit.Position.X + 6f, unit.Position.Y);

        world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = unit.OwnerId,
            Units = new[] { unit.Id },
            Target = target,
        });

        world.Tick();
        Check("Bewegungsbefehl gesetzt", unit.Order == UnitOrder.Move);

        Vector2 start = unit.Position;
        world.Tick();
        Check("Einheit bewegt sich", !unit.Position.IsEqualApprox(start));
        Check("Interpolationszustand gefuellt", !unit.PreviousPosition.IsEqualApprox(unit.Position));

        // Genug Ticks fuer 6 m bei 2,6 m/s, plus Bremsweg.
        for (int i = 0; i < 120; i++) world.Tick();

        Check("Ziel erreicht", unit.Position.DistanceTo(target) < 0.2f);
        Check("Einheit wieder untaetig", unit.Order == UnitOrder.Idle);
    }

    private static void CheckPopulationCap(SimulationWorld world, DefinitionDatabase definitions)
    {
        Player player = world.Players[1];
        Building townCenter = FindTownCenter(world, player.Id);

        // Limit ist 5, belegt sind 4 — zwei Siedler passen nicht mehr beide hinein.
        for (int i = 0; i < 2; i++)
        {
            world.Commands.Enqueue(new TrainUnitCommand
            {
                PlayerId = player.Id,
                Building = townCenter.Id,
                UnitDefinitionId = "unit_settler",
            });
        }

        int ticks = Mathf.CeilToInt(definitions.GetUnit("unit_settler")!.BuildTimeSeconds * 2f
                                    / SimulationWorld.TickDelta) + 10;
        for (int i = 0; i < ticks; i++) world.Tick();

        Check("Bevoelkerungslimit haelt Produktion an", player.Population == 5);
        Check("Blockierter Posten bleibt in der Warteschlange", townCenter.Queue.Count == 1);
    }

    private static void CheckRefund(SimulationWorld world, DefinitionDatabase definitions)
    {
        Player player = world.Players[1];
        Building townCenter = FindTownCenter(world, player.Id);

        int foodBefore = player.GetResource(ResourceType.Food);
        int queueBefore = townCenter.Queue.Count;

        world.Commands.Enqueue(new CancelTrainingCommand
        {
            PlayerId = player.Id,
            Building = townCenter.Id,
            QueueIndex = 0,
        });
        world.Tick();

        Check("Abbruch entfernt den Posten", townCenter.Queue.Count == queueBefore - 1);
        Check("Abbruch erstattet die Kosten", player.GetResource(ResourceType.Food) == foodBefore + 50);
    }

    // --- Hilfsmittel -----------------------------------------------------

    private static Building FindTownCenter(SimulationWorld world, int ownerId)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == ownerId && building.CanAdvanceAge) return building;
        }
        throw new InvalidOperationException($"Kein Rathaus fuer Spieler {ownerId} gefunden.");
    }

    private static void Check(string description, bool condition)
    {
        _checks++;
        GD.Print(condition ? $"  [ok]   {description}" : $"  [FEHL] {description}");
        if (!condition) Failures.Add(description);
    }
}
