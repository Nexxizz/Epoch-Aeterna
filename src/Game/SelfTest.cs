using System;
using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Pathfinding;
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

        GD.Print("=== Self-test ===");

        Section("Phase 1 — Definitions");
        CheckDefinitions(definitions);

        Section("Phase 2 — Navigation grid");
        CheckNavGrid();

        Section("Phase 2 — Pathfinding");
        CheckPathfinding();

        Section("Phase 2 — Map generation");
        CheckMapGeneration();

        Section("Phase 1 — Match and economy");
        Match match = MatchSetup.Build(MatchSetup.DefaultSkirmish(), definitions);
        SimulationWorld world = match.World;

        CheckMatchSetup(match);
        CheckTickAdvances(world);
        CheckProduction(world, definitions);

        Section("Phase 2 — Map movement");
        CheckMovement(world);
        CheckWaypointQueue(world);

        Section("Phase 1 — Edge cases");
        CheckPopulationCap(world, definitions);
        CheckRefund(world, definitions);

        SelfTestPhase3.Run(definitions, Section, Check);

        GD.Print(Failures.Count == 0
            ? $"\nAll {_checks} checks passed."
            : $"\n{Failures.Count} of {_checks} checks FAILED:");

        foreach (string failure in Failures) GD.PrintErr("  - " + failure);

        return Failures.Count == 0 ? 0 : 1;
    }

    // --- Phase 1 ---------------------------------------------------------

    private static void CheckDefinitions(DefinitionDatabase definitions)
    {
        Check("Unit definitions loaded", definitions.Units.Count >= 2);
        Check("Building definitions loaded", definitions.Buildings.Count >= 2);
        Check("Ages loaded", definitions.Ages.Count >= 2);
        Check("Ages sorted by index", definitions.GetAge(0)?.Index == 0 && definitions.GetAge(1)?.Index == 1);
        Check("Settler cost read from .tres", definitions.GetUnit("unit_settler")?.Cost?.Food == 50);
        Check("Town centre knows its trainable units",
            definitions.GetBuilding("bld_towncenter")?.TrainableUnitIds.Length == 2);
    }

    // --- Phase 2: Gitter -------------------------------------------------

    private static void CheckNavGrid()
    {
        var grid = new NavGrid(32, 32);

        Vector2 world = grid.CellToWorld(10, 20);
        Vector2I back = grid.WorldToCell(world);
        Check("Tile and world coordinates are reversible", back == new Vector2I(10, 20));

        Check("Map is centred around the origin",
            Mathf.IsEqualApprox(grid.CellToWorld(16, 16).X, NavGrid.CellSize * 0.5f));

        Check("Free tile is walkable", grid.IsWalkable(5, 5));
        grid.Block(5, 5, BlockFlags.Decoration);
        Check("Blocked tile is not walkable", !grid.IsWalkable(5, 5));
        grid.Unblock(5, 5, BlockFlags.Decoration);
        Check("Unblocking restores walkability", grid.IsWalkable(5, 5));

        Check("Positions outside the map are blocked", !grid.IsWalkable(-1, 5) && !grid.IsWalkable(32, 5));

        grid.SetCornerHeight(4, 4, 10f);
        Check("Heights are interpolated",
            grid.SampleHeight(grid.CellToWorld(4, 4)) is > 0f and < 10f);

        Check("Slope detects the height jump", grid.CellSlope(3, 3) > 5f);

        // Freiraum: direkt neben einer Sperre ist er kleiner als mitten im Freien.
        grid.Block(16, 16, BlockFlags.Building);
        Check("Clearance decreases near obstacles",
            grid.GetClearance(17, 16) < grid.GetClearance(16, 24));

        Check("Nearest free position is found",
            grid.FindNearestPassable(new Vector2I(16, 16), 1) != new Vector2I(16, 16));
    }

    // --- Phase 2: Wegfindung ---------------------------------------------

    private static void CheckPathfinding()
    {
        var grid = new NavGrid(40, 40);
        var pathfinder = new AStarPathfinder(grid);
        var path = new List<Vector2>();

        Vector2 from = grid.CellToWorld(4, 20);
        Vector2 to = grid.CellToWorld(35, 20);

        Check("Path across open ground found", pathfinder.TryFindPath(from, to, 1, path));
        Check("Open path is smoothed to few waypoints", path.Count <= 3);

        // Wand mit Luecke: der Weg muss den Umweg nehmen, aber existieren.
        for (int y = 5; y < 35; y++) grid.Block(20, y, BlockFlags.Decoration);

        Check("Path around the wall found", pathfinder.TryFindPath(from, to, 1, path));
        Check("Detour needs more waypoints than the open path", path.Count > 2);
        Check("No waypoint lies inside the wall", NoWaypointInsideWall(grid, path));

        // Ziel vollstaendig einmauern: jetzt darf es keinen Weg mehr geben.
        var sealedGrid = new NavGrid(40, 40);
        var sealedFinder = new AStarPathfinder(sealedGrid);
        for (int i = 28; i <= 32; i++)
        {
            sealedGrid.Block(i, 28, BlockFlags.Decoration);
            sealedGrid.Block(i, 32, BlockFlags.Decoration);
            sealedGrid.Block(28, i, BlockFlags.Decoration);
            sealedGrid.Block(32, i, BlockFlags.Decoration);
        }

        Check("Unreachable target yields no path",
            !sealedFinder.TryFindPath(sealedGrid.CellToWorld(5, 5), sealedGrid.CellToWorld(30, 30), 1, path));

        // Klick auf ein Hindernis: das Ziel wird auf den naechsten freien Platz gezogen.
        var nudgeGrid = new NavGrid(40, 40);
        var nudgeFinder = new AStarPathfinder(nudgeGrid);
        nudgeGrid.Block(30, 30, BlockFlags.Building);

        Check("Clicking an obstacle redirects to a neighbouring tile",
            nudgeFinder.TryFindPath(nudgeGrid.CellToWorld(5, 5), nudgeGrid.CellToWorld(30, 30), 1, path));
    }

    private static bool NoWaypointInsideWall(NavGrid grid, List<Vector2> path)
    {
        foreach (Vector2 point in path)
        {
            Vector2I cell = grid.WorldToCell(point);
            if (!grid.IsWalkable(cell.X, cell.Y)) return false;
        }
        return true;
    }

    // --- Phase 2: Karte --------------------------------------------------

    private static void CheckMapGeneration()
    {
        GeneratedMap map = MapGenerator.Generate(12345);

        Check("Map has the expected size", map.Grid.Width == 128 && map.Grid.Height == 128);
        Check("Resource deposits were placed", map.ResourceSpots.Count > 100);
        Check("Two starting positions selected", map.StartPositions.Count == 2);

        Check("Starting positions are far apart",
            map.StartPositions[0].DistanceTo(map.StartPositions[1]) > 80f);

        foreach (Vector2 start in map.StartPositions)
        {
            Vector2I cell = map.Grid.WorldToCell(start);
            Check($"Starting position {cell} is walkable", map.Grid.IsWalkable(cell.X, cell.Y));
        }

        // Gleicher Seed, gleiche Karte — Voraussetzung fuer reproduzierbare Tests.
        GeneratedMap again = MapGenerator.Generate(12345);
        Check("Same seed generates the same map",
            again.ResourceSpots.Count == map.ResourceSpots.Count &&
            again.StartPositions[0].IsEqualApprox(map.StartPositions[0]));

        GeneratedMap other = MapGenerator.Generate(999);
        Check("Different seed generates a different map",
            other.ResourceSpots.Count != map.ResourceSpots.Count ||
            !other.StartPositions[0].IsEqualApprox(map.StartPositions[0]));
    }

    // --- Match -----------------------------------------------------------

    private static void CheckMatchSetup(Match match)
    {
        SimulationWorld world = match.World;

        Check("Two players created", world.Players.Count == 2);
        Check("Two town centres built", world.Entities.Buildings.Count == 2);
        Check("Eight starting settlers placed", world.Entities.Units.Count == 8);
        Check("Eleven systems registered", world.Systems.Count == 11);

        Player player = world.Players[0];
        Check("Population derived from entities", player.Population == 4);
        Check("Population cap provided by town centre", player.PopulationCap == 5);
        Check("Starting resources granted", player.GetResource(ResourceType.Food) == 250);

        // Das Rathaus muss im Gitter als belegt eingetragen sein.
        Building townCenter = FindTownCenter(world, player.Id);
        Vector2I cell = world.Nav.WorldToCell(townCenter.Position);
        Check("Building blocks its footprint", !world.Nav.IsWalkable(cell.X, cell.Y));
    }

    private static void CheckTickAdvances(SimulationWorld world)
    {
        int before = world.CurrentTick;
        world.Tick();
        Check("Tick counter advances", world.CurrentTick == before + 1);
        Check("Elapsed time matches the tick rate",
            Mathf.IsEqualApprox(world.ElapsedSeconds, world.CurrentTick * SimulationWorld.TickDelta));
    }

    private static void CheckProduction(SimulationWorld world, DefinitionDatabase definitions)
    {
        Player player = world.Players[0];
        Building townCenter = FindTownCenter(world, player.Id);

        // Erst Platz schaffen, sonst blockiert das Bevoelkerungslimit die Fertigstellung.
        Vector2 housePosition = townCenter.Position + new Vector2(14f, 14f);
        world.SpawnBuilding("bld_house", player.Id, housePosition);
        world.FlushSpawns();
        Check("House raises the population cap", player.PopulationCap == 15);

        int foodBefore = player.GetResource(ResourceType.Food);
        int unitsBefore = world.Entities.Units.Count;

        world.Commands.Enqueue(new TrainUnitCommand
        {
            PlayerId = player.Id,
            Building = townCenter.Id,
            UnitDefinitionId = "unit_settler",
        });

        world.Tick();
        Check("Command was executed", world.Commands.TotalExecuted > 0);
        Check("Cost deducted immediately", player.GetResource(ResourceType.Food) == foodBefore - 50);
        Check("Item added to the queue", townCenter.Queue.Count == 1);

        float buildTime = definitions.GetUnit("unit_settler")!.BuildTimeSeconds;
        int ticks = Mathf.CeilToInt(buildTime / SimulationWorld.TickDelta) + 2;
        for (int i = 0; i < ticks; i++) world.Tick();

        Check("Queue completed", townCenter.Queue.Count == 0);
        Check("Unit spawned", world.Entities.Units.Count == unitsBefore + 1);
        Check("Population increased", player.Population == 5);
    }

    // --- Bewegung --------------------------------------------------------

    private static void CheckMovement(SimulationWorld world)
    {
        Unit unit = world.Entities.Units[0];

        // Ziel auf eine garantiert freie Kachel in der Naehe legen.
        Vector2I startCell = world.Nav.WorldToCell(unit.Position);
        Vector2 target = ToWorld(world, startCell + new Vector2I(5, 0), unit.Clearance);

        world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = unit.OwnerId,
            Units = new[] { unit.Id },
            Target = target,
        });

        Vector2 start = unit.Position;
        world.Tick();

        Check("Move command assigned", unit.Order == UnitOrder.Move);
        Check("Path calculated in the same tick", !unit.NeedsPath && unit.Path.Count > 0);
        Check("Unit moves", !unit.Position.IsEqualApprox(start));
        Check("Interpolation state populated", !unit.PreviousPosition.IsEqualApprox(unit.Position));

        for (int i = 0; i < 400 && unit.Order == UnitOrder.Move; i++) world.Tick();

        Check("Target reached", unit.Position.DistanceTo(target) < 1.5f);
        Check("Unit returned to idle", unit.Order == UnitOrder.Idle);
    }

    private static void CheckWaypointQueue(SimulationWorld world)
    {
        Unit unit = world.Entities.Units[1];
        Vector2I origin = world.Nav.WorldToCell(unit.Position);

        Vector2 first = ToWorld(world, origin + new Vector2I(4, 0), unit.Clearance);
        Vector2 second = ToWorld(world, origin + new Vector2I(4, 5), unit.Clearance);

        world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = unit.OwnerId,
            Units = new[] { unit.Id },
            Target = first,
        });
        world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = unit.OwnerId,
            Units = new[] { unit.Id },
            Target = second,
            Queued = true,
        });

        world.Tick();
        Check("Shift-click queues another target", unit.QueuedTargets.Count == 1);

        for (int i = 0; i < 400 && unit.Order == UnitOrder.Move; i++) world.Tick();

        Check("Both targets completed", unit.QueuedTargets.Count == 0);
        Check("Unit arrived at the second target", unit.Position.DistanceTo(second) < 1.5f);
    }

    private static Vector2 ToWorld(SimulationWorld world, Vector2I cell, int clearance)
    {
        Vector2I passable = world.Nav.FindNearestPassable(world.Nav.ClampCell(cell), clearance);
        return world.Nav.CellToWorld(passable.X, passable.Y);
    }

    // --- Grenzfaelle -----------------------------------------------------

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

        Check("Population cap halts production", player.Population == 5);
        Check("Blocked item remains in the queue", townCenter.Queue.Count == 1);
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

        Check("Cancellation removes the item", townCenter.Queue.Count == queueBefore - 1);
        Check("Cancellation refunds the cost", player.GetResource(ResourceType.Food) == foodBefore + 50);
    }

    // --- Hilfsmittel -----------------------------------------------------

    private static Building FindTownCenter(SimulationWorld world, int ownerId)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == ownerId && building.CanAdvanceAge) return building;
        }
        throw new InvalidOperationException($"No town centre found for player {ownerId}.");
    }

    private static void Section(string title) => GD.Print($"\n-- {title}");

    private static void Check(string description, bool condition)
    {
        _checks++;
        GD.Print(condition ? $"  [ok]   {description}" : $"  [FAIL] {description}");
        if (!condition) Failures.Add(description);
    }
}
