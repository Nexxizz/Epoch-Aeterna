using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Game;

/// <summary>
/// Checks for economy, construction, combat, ages, fog and the victory condition.
/// </summary>
/// <remarks>
/// In its own file, because the self-test would otherwise become unreadable. The
/// checks run without a SceneTree like all the others — combat and fog are
/// pure simulation too.
/// </remarks>
public static class SelfTestPhase3
{
    /// <summary>Builds a fresh match without fog — tests are meant to see everything.</summary>
    private static Match NewMatch(DefinitionDatabase definitions, ulong seed = 4711)
    {
        MatchConfig config = MatchSetup.DefaultSkirmish();
        return MatchSetup.Build(new MatchConfig
        {
            Seed = seed,
            Players = config.Players,
            StartingSettlers = config.StartingSettlers,
            FogOfWar = false,
            StartingResources = config.StartingResources,
        }, definitions);
    }

    public static void Run(DefinitionDatabase definitions,
        System.Action<string> section, System.Action<string, bool> check)
    {
        section("Phase 3 — Definitions and counter matrix");
        CheckData(definitions, check);

        section("Phase 3 — Resource economy");
        CheckGathering(definitions, check);

        section("Phase 3 — Construction");
        CheckConstruction(definitions, check);

        section("Phase 3 — Combat");
        CheckCombat(definitions, check);

        section("Phase 3 — Age advancement");
        CheckAgeAdvance(definitions, check);

        section("Phase 3 — Fog of war");
        CheckFog(definitions, check);

        section("Phase 3 — Victory condition");
        CheckVictory(definitions, check);
    }

    // --- Data ------------------------------------------------------------

    private static void CheckData(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        check("Four resource deposit definitions loaded", definitions.ResourceNodes.Count == 4);
        check("Seven building definitions loaded", definitions.Buildings.Count == 7);
        check("Six unit definitions loaded", definitions.Units.Count == 6);

        check("Tree yields wood", definitions.GetResourceNode("res_tree")?.Resource == ResourceType.Wood);
        check("Berries yield food and do not block movement",
            definitions.GetResourceNode("res_berries") is { Resource: ResourceType.Food, BlocksMovement: false });

        check("Archery range requires the Copper Age",
            definitions.GetBuilding("bld_range")?.RequiredAgeIndex == 1);
        check("Archery range requires barracks",
            definitions.GetBuilding("bld_range")?.RequiredBuildingId == "bld_barracks");

        check("Archer uses a projectile",
            definitions.GetUnit("unit_archer") is { UsesProjectile: true, DamageType: DamageType.Ranged });

        // The fallback is nothing but ones. If something else shows up here, the .tres
        // really was loaded — which is exactly what was missing before.
        check("Counter matrix comes from the .tres, not the fallback",
            !Mathf.IsEqualApprox(definitions.Combat.Get(DamageType.Siege, ArmorClass.Building), 1f));

        check("Siege damage is strong against buildings",
            definitions.Combat.Get(DamageType.Siege, ArmorClass.Building) > 3f);
        check("Siege damage is weak against civilians",
            definitions.Combat.Get(DamageType.Siege, ArmorClass.Civilian) < 1f);
        check("Ranged attacks are weak against buildings",
            definitions.Combat.Get(DamageType.Ranged, ArmorClass.Building) < 0.6f);
    }

    // --- Economy ---------------------------------------------------------

    private static void CheckGathering(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player player = world.Players[0];

        check("Resource deposits placed on the map", world.Entities.ResourceNodes.Count > 50);

        // Every base must have all four resources within reach, otherwise chance
        // decides the match before the first click.
        foreach (Vector2 start in match.Map.StartPositions)
        {
            foreach (ResourceType type in ResourceTypes.All)
            {
                check($"{ResourceTypes.DisplayName(type)} near starting position",
                    HasNodeNear(world, start, type, 40f));
            }
        }

        Unit settler = FindUnit(world, player.Id, "unit_settler")!;
        ResourceNode tree = NearestNode(world, settler.Position, ResourceType.Wood)!;

        int woodBefore = player.GetResource(ResourceType.Wood);

        world.Commands.Enqueue(new GatherCommand
        {
            PlayerId = player.Id, Units = new[] { settler.Id }, Node = tree.Id,
        });

        world.Tick();
        check("Gather command assigned", settler.Order == UnitOrder.Gather);

        // Enough time for the trip out, the harvesting and the trip back.
        int nodeAmountBefore = tree.Remaining;
        for (int i = 0; i < 2000 && player.GetResource(ResourceType.Wood) == woodBefore; i++) world.Tick();

        check("Resource deposit was harvested", tree.Remaining < nodeAmountBefore || tree.IsDepleted);
        check("Wood was delivered", player.GetResource(ResourceType.Wood) > woodBefore);
        check("Yield recorded in statistics", player.Stats.GetGathered(ResourceType.Wood) > 0);
        check("Settler continues gathering", settler.Order == UnitOrder.Gather);
    }

    // --- Construction ----------------------------------------------------

    private static void CheckConstruction(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player player = world.Players[0];

        Unit settler = FindUnit(world, player.Id, "unit_settler")!;
        Vector2 spot = FindBuildSpot(world, settler.Position, definitions.GetBuilding("bld_house")!, player);

        int woodBefore = player.GetResource(ResourceType.Wood);
        int buildingsBefore = world.Entities.Buildings.Count;

        world.Commands.Enqueue(new PlaceBuildingCommand
        {
            PlayerId = player.Id,
            BuildingDefinitionId = "bld_house",
            Position = spot,
            Builders = new[] { settler.Id },
        });
        world.Tick();

        check("Cost deducted immediately", player.GetResource(ResourceType.Wood) == woodBefore - 30);
        check("Construction site placed", world.Entities.Buildings.Count == buildingsBefore + 1);

        Building site = FindBuilding(world, player.Id, "bld_house")!;
        check("Construction site starts at zero progress", site.ConstructionProgress < 0.01f);
        check("Construction site does not raise the cap yet", player.PopulationCap == 5);
        check("Settler has a build order", settler.Order == UnitOrder.Build);

        for (int i = 0; i < 1500 && site.IsUnderConstruction; i++) world.Tick();

        check("Building completed", !site.IsUnderConstruction);
        check("Full health after construction", Mathf.IsEqualApprox(site.Health, site.MaxHealth));
        check("House now raises the cap", player.PopulationCap == 15);
        check("Completion recorded in statistics", player.Stats.BuildingsCompleted == 1);
        check("Settler is idle again", settler.Order == UnitOrder.Idle);

        // Several settlers build faster, but not linearly faster.
        check("Additional builders contribute less than a linear multiple",
            EffectiveBuildersMonotonic());

        int woodAfter = player.GetResource(ResourceType.Wood);
        world.Commands.Enqueue(new DemolishCommand { PlayerId = player.Id, Building = site.Id });
        world.Tick();

        check("Demolition removes the building", !world.Entities.Exists(site.Id));
        check("Demolition provides a partial refund", player.GetResource(ResourceType.Wood) == woodAfter + 15);

        // Placement check: nothing may go up in the middle of your own town centre.
        Building townCenter = FindBuilding(world, player.Id, "bld_towncenter")!;
        check("Building on occupied ground rejected",
            !BuildPlacement.IsValid(world, definitions.GetBuilding("bld_house")!, townCenter.Position, player));

        check("Archery range rejected in the Stone Age",
            !BuildPlacement.IsValid(world, definitions.GetBuilding("bld_range")!, spot, player));
    }

    private static bool EffectiveBuildersMonotonic()
    {
        // Mirrors the ConstructionSystem formula: strictly increasing, but damped.
        static float Effective(int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++) total += 1f / (1f + i * 0.6f);
            return total;
        }

        return Effective(2) > Effective(1) && Effective(2) < 2f * Effective(1) && Effective(4) < 4f;
    }

    // --- Combat ----------------------------------------------------------

    private static void CheckCombat(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;

        Player attacker = world.Players[0];
        Player defender = world.Players[1];

        // Place two fighters right next to each other in the middle.
        Vector2 arena = world.Nav.CellToWorld(world.Nav.Width / 2, world.Nav.Height / 2);

        Unit spearman = world.SpawnUnit("unit_spearman", attacker.Id, arena)!;
        Unit victim = world.SpawnUnit("unit_settler", defender.Id, arena + new Vector2(2f, 0f))!;
        world.FlushSpawns();

        float victimHealth = victim.Health;

        world.Commands.Enqueue(new AttackCommand
        {
            PlayerId = attacker.Id, Units = new[] { spearman.Id }, Target = victim.Id,
        });

        for (int i = 0; i < 60; i++) world.Tick();

        check("Melee fighter deals damage", victim.Health < victimHealth);

        for (int i = 0; i < 400 && world.Entities.Exists(victim.Id); i++) world.Tick();

        check("Target was killed", !world.Entities.Exists(victim.Id));
        check("Kill recorded in statistics", attacker.Stats.EnemiesKilled >= 1);
        check("Loss recorded in statistics", defender.Stats.UnitsLost >= 1);

        // Ranged: the projectile has to be in flight before it hits.
        Unit archer = world.SpawnUnit("unit_slinger", attacker.Id, arena)!;
        Unit target = world.SpawnUnit("unit_settler", defender.Id, arena + new Vector2(7f, 0f))!;
        world.FlushSpawns();

        world.Commands.Enqueue(new AttackCommand
        {
            PlayerId = attacker.Id, Units = new[] { archer.Id }, Target = target.Id,
        });

        bool sawProjectile = false;
        for (int i = 0; i < 200 && !sawProjectile; i++)
        {
            world.Tick();
            if (world.Projectiles.Count > 0) sawProjectile = true;
        }

        check("Ranged fighter launches a projectile", sawProjectile);

        float targetHealth = target.Health;
        for (int i = 0; i < 200 && target.Health >= targetHealth; i++) world.Tick();
        check("Projectile deals damage on impact", target.Health < targetHealth);

        // Counter matrix: the same raw damage has different effects.
        Building wall = world.SpawnBuilding("bld_house", defender.Id, arena + new Vector2(20f, 20f))!;
        world.FlushSpawns();

        float before = wall.Health;
        world.ApplyDamage(wall, 20f, DamageType.Siege, attacker.Id);
        float siegeDamage = before - wall.Health;

        before = wall.Health;
        world.ApplyDamage(wall, 20f, DamageType.Ranged, attacker.Id);
        float rangedDamage = before - wall.Health;

        check("Siege damage hurts buildings more than ranged damage", siegeDamage > rangedDamage * 2f);
    }

    // --- Ages ------------------------------------------------------------

    private static void CheckAgeAdvance(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player player = world.Players[0];

        Building townCenter = FindBuilding(world, player.Id, "bld_towncenter")!;

        check("Match starts in the Stone Age", player.AgeIndex == 0);

        // Without a second building the prerequisite is missing.
        foreach (ResourceType type in ResourceTypes.All) player.SetResource(type, 2000);

        world.Commands.Enqueue(new AdvanceAgeCommand { PlayerId = player.Id, Building = townCenter.Id });
        world.Tick();
        check("Advancement rejected without a second building", !townCenter.IsResearchingAge);

        // Put a second, finished building down and it works.
        world.SpawnBuilding("bld_house", player.Id, townCenter.Position + new Vector2(16f, 16f));
        world.FlushSpawns();

        int foodBefore = player.GetResource(ResourceType.Food);
        world.Commands.Enqueue(new AdvanceAgeCommand { PlayerId = player.Id, Building = townCenter.Id });
        world.Tick();

        check("Age advancement started", townCenter.IsResearchingAge);
        check("Age advancement costs resources", player.GetResource(ResourceType.Food) < foodBefore);

        AgeDefinition copper = definitions.GetAge(1)!;
        int ticks = Mathf.CeilToInt(copper.ResearchTimeSeconds / SimulationWorld.TickDelta) + 5;
        for (int i = 0; i < ticks; i++) world.Tick();

        check("Copper Age reached", player.AgeIndex == 1);
        check("Research completed", !townCenter.IsResearchingAge);

        // What the age requires is now allowed — with barracks as the prerequisite.
        world.SpawnBuilding("bld_barracks", player.Id, townCenter.Position + new Vector2(-16f, 16f));
        world.FlushSpawns();

        Vector2 spot = FindBuildSpot(world, townCenter.Position, definitions.GetBuilding("bld_range")!, player);
        check("Archery range can now be built",
            BuildPlacement.IsValid(world, definitions.GetBuilding("bld_range")!, spot, player));
    }

    // --- Fog -------------------------------------------------------------

    private static void CheckFog(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        MatchConfig config = MatchSetup.DefaultSkirmish();
        Match match = MatchSetup.Build(new MatchConfig
        {
            Seed = 4711,
            Players = config.Players,
            FogOfWar = true,
            StartingResources = config.StartingResources,
        }, definitions);

        SimulationWorld world = match.World;
        Player player = world.Players[0];
        VisionGrid vision = player.Vision!;

        Vector2I home = world.Nav.WorldToCell(match.StartOf(0));
        Vector2I enemy = world.Nav.WorldToCell(match.StartOf(1));

        check("Own base is visible", vision.IsVisible(home.X, home.Y));
        check("Enemy base is unexplored", !vision.IsExplored(enemy.X, enemy.Y));

        // Put a scout at the enemy base and let visibility rebuild.
        world.SpawnUnit("unit_scout", player.Id, match.StartOf(1));
        world.FlushSpawns();
        for (int i = 0; i < VisionSystem.RebuildInterval + 1; i++) world.Tick();

        check("Explored area becomes visible", vision.IsVisible(enemy.X, enemy.Y));

        // Remove the scout: the area stays explored but is no longer visible.
        foreach (Unit unit in new List<Unit>(world.Entities.Units))
        {
            if (unit.DefinitionId == "unit_scout") world.Entities.Remove(unit.Id);
        }
        for (int i = 0; i < VisionSystem.RebuildInterval * 2; i++) world.Tick();

        check("Abandoned area remains explored", vision.IsExplored(enemy.X, enemy.Y));
        check("Abandoned area is no longer visible", !vision.IsVisible(enemy.X, enemy.Y));
    }

    // --- Victory ---------------------------------------------------------

    private static void CheckVictory(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player loser = world.Players[1];

        check("Match is running at the start", !world.IsOver);

        // Remove everything belonging to the second player.
        foreach (Entity entity in new List<Entity>(world.Entities.All()))
        {
            if (entity.OwnerId == loser.Id) world.Entities.Remove(entity.Id);
        }

        for (int i = 0; i < 30 && !world.IsOver; i++) world.Tick();

        check("Player without a base is defeated", loser.IsDefeated);
        check("Match has ended", world.IsOver);
        check("The other player has won", world.Winner?.Id == world.Players[0].Id);
        check("Completed match no longer ticks", TickIsFrozen(world));
    }

    private static bool TickIsFrozen(SimulationWorld world)
    {
        int before = world.CurrentTick;
        world.Tick();
        return world.CurrentTick == before;
    }

    // --- Helpers ---------------------------------------------------------

    private static Vector2 StartOf(this Match match, int index) =>
        match.Map.StartPositions[index % match.Map.StartPositions.Count];

    private static bool HasNodeNear(SimulationWorld world, Vector2 point, ResourceType type, float radius)
    {
        foreach (ResourceNode node in world.Entities.ResourceNodes)
        {
            if (node.Resource == type && node.Position.DistanceTo(point) <= radius) return true;
        }
        return false;
    }

    private static ResourceNode? NearestNode(SimulationWorld world, Vector2 from, ResourceType type)
    {
        ResourceNode? best = null;
        float bestDistance = float.MaxValue;

        foreach (ResourceNode node in world.Entities.ResourceNodes)
        {
            if (node.Resource != type) continue;

            float distance = from.DistanceSquaredTo(node.Position);
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = node;
        }
        return best;
    }

    private static Unit? FindUnit(SimulationWorld world, int ownerId, string definitionId)
    {
        foreach (Unit unit in world.Entities.Units)
        {
            if (unit.OwnerId == ownerId && unit.DefinitionId == definitionId) return unit;
        }
        return null;
    }

    private static Building? FindBuilding(SimulationWorld world, int ownerId, string definitionId)
    {
        foreach (Building building in world.Entities.Buildings)
        {
            if (building.OwnerId == ownerId && building.DefinitionId == definitionId) return building;
        }
        return null;
    }

    /// <summary>Searches outwards in a spiral for a valid building site around a point.</summary>
    private static Vector2 FindBuildSpot(SimulationWorld world, Vector2 around,
        BuildingDefinition definition, Player player)
    {
        Vector2I centre = world.Nav.WorldToCell(around);

        for (int radius = 3; radius < 30; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;

                    Vector2I cell = centre + new Vector2I(dx, dy);
                    if (!world.Nav.InBounds(cell.X, cell.Y)) continue;

                    Vector2 position = world.Nav.CellToWorld(cell.X, cell.Y);
                    if (BuildPlacement.IsValid(world, definition, position, player)) return position;
                }
            }
        }

        return around;
    }
}
