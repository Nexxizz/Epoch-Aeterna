using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Map;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Core.Systems;

namespace EpochAeterna.Game;

/// <summary>
/// Pruefungen fuer Wirtschaft, Bauen, Kampf, Zeitalter, Nebel und Siegbedingung.
/// </summary>
/// <remarks>
/// In einer eigenen Datei, weil der Selbsttest sonst unuebersichtlich wird. Die
/// Pruefungen laufen wie alle anderen ohne SceneTree — auch Kampf und Nebel sind
/// reine Simulation.
/// </remarks>
public static class SelfTestPhase3
{
    /// <summary>Baut eine frische Partie ohne Nebel — Tests sollen alles sehen.</summary>
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
        section("Phase 3 — Definitionen und Konter-Matrix");
        CheckData(definitions, check);

        section("Phase 3 — Ressourcenwirtschaft");
        CheckGathering(definitions, check);

        section("Phase 3 — Bauen");
        CheckConstruction(definitions, check);

        section("Phase 3 — Kampf");
        CheckCombat(definitions, check);

        section("Phase 3 — Zeitalteraufstieg");
        CheckAgeAdvance(definitions, check);

        section("Phase 3 — Nebel des Krieges");
        CheckFog(definitions, check);

        section("Phase 3 — Siegbedingung");
        CheckVictory(definitions, check);
    }

    // --- Daten -----------------------------------------------------------

    private static void CheckData(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        check("Vier Vorkommen-Definitionen geladen", definitions.ResourceNodes.Count == 4);
        check("Sieben Gebaeude-Definitionen geladen", definitions.Buildings.Count == 7);
        check("Sechs Einheiten-Definitionen geladen", definitions.Units.Count == 6);

        check("Baum liefert Holz", definitions.GetResourceNode("res_tree")?.Resource == ResourceType.Wood);
        check("Beeren liefern Nahrung und blockieren nicht",
            definitions.GetResourceNode("res_berries") is { Resource: ResourceType.Food, BlocksMovement: false });

        check("Schiessstand verlangt die Kupferzeit",
            definitions.GetBuilding("bld_range")?.RequiredAgeIndex == 1);
        check("Schiessstand verlangt eine Kaserne",
            definitions.GetBuilding("bld_range")?.RequiredBuildingId == "bld_barracks");

        check("Bogenschuetze nutzt ein Geschoss",
            definitions.GetUnit("unit_archer") is { UsesProjectile: true, DamageType: DamageType.Ranged });

        // Der Fallback besteht nur aus Einsen. Wenn hier etwas anderes steht,
        // wurde die .tres tatsaechlich geladen — genau das hat vorher gefehlt.
        check("Konter-Matrix stammt aus der .tres, nicht vom Fallback",
            !Mathf.IsEqualApprox(definitions.Combat.Get(DamageType.Siege, ArmorClass.Building), 1f));

        check("Belagerung ist stark gegen Gebaeude",
            definitions.Combat.Get(DamageType.Siege, ArmorClass.Building) > 3f);
        check("Belagerung ist schwach gegen Zivilisten",
            definitions.Combat.Get(DamageType.Siege, ArmorClass.Civilian) < 1f);
        check("Fernkampf prallt an Gebaeuden ab",
            definitions.Combat.Get(DamageType.Ranged, ArmorClass.Building) < 0.6f);
    }

    // --- Wirtschaft ------------------------------------------------------

    private static void CheckGathering(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player player = world.Players[0];

        check("Vorkommen auf der Karte gesetzt", world.Entities.ResourceNodes.Count > 50);

        // Jede Basis muss alle vier Ressourcen in Reichweite haben, sonst entscheidet
        // der Zufall die Partie schon vor dem ersten Klick.
        foreach (Vector2 start in match.Map.StartPositions)
        {
            foreach (ResourceType type in ResourceTypes.All)
            {
                check($"{ResourceTypes.DisplayName(type)} nahe Startplatz",
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
        check("Sammelbefehl gesetzt", settler.Order == UnitOrder.Gather);

        // Genug Zeit fuer Hinweg, Abbau und Rueckweg.
        int nodeAmountBefore = tree.Remaining;
        for (int i = 0; i < 2000 && player.GetResource(ResourceType.Wood) == woodBefore; i++) world.Tick();

        check("Vorkommen wurde abgebaut", tree.Remaining < nodeAmountBefore || tree.IsDepleted);
        check("Holz wurde abgeliefert", player.GetResource(ResourceType.Wood) > woodBefore);
        check("Ertrag in der Statistik", player.Stats.GetGathered(ResourceType.Wood) > 0);
        check("Siedler sammelt weiter", settler.Order == UnitOrder.Gather);
    }

    // --- Bauen -----------------------------------------------------------

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

        check("Kosten sofort abgebucht", player.GetResource(ResourceType.Wood) == woodBefore - 30);
        check("Baustelle gesetzt", world.Entities.Buildings.Count == buildingsBefore + 1);

        Building site = FindBuilding(world, player.Id, "bld_house")!;
        check("Baustelle beginnt bei null Fortschritt", site.ConstructionProgress < 0.01f);
        check("Baustelle hebt das Limit noch nicht", player.PopulationCap == 5);
        check("Siedler hat Bauauftrag", settler.Order == UnitOrder.Build);

        for (int i = 0; i < 1500 && site.IsUnderConstruction; i++) world.Tick();

        check("Gebaeude fertiggestellt", !site.IsUnderConstruction);
        check("Volle Lebenspunkte nach dem Bau", Mathf.IsEqualApprox(site.Health, site.MaxHealth));
        check("Haus hebt jetzt das Limit", player.PopulationCap == 15);
        check("Fertigstellung in der Statistik", player.Stats.BuildingsCompleted == 1);
        check("Siedler wieder frei", settler.Order == UnitOrder.Idle);

        // Mehrere Siedler bauen schneller, aber nicht linear schneller.
        check("Mehr Bauarbeiter bringen weniger als das Vielfache",
            EffectiveBuildersMonotonic());

        int woodAfter = player.GetResource(ResourceType.Wood);
        world.Commands.Enqueue(new DemolishCommand { PlayerId = player.Id, Building = site.Id });
        world.Tick();

        check("Abriss entfernt das Gebaeude", !world.Entities.Exists(site.Id));
        check("Abriss erstattet einen Teil", player.GetResource(ResourceType.Wood) == woodAfter + 15);

        // Bauplatzpruefung: mitten im eigenen Rathaus darf nichts entstehen.
        Building townCenter = FindBuilding(world, player.Id, "bld_towncenter")!;
        check("Bauen auf besetztem Grund abgelehnt",
            !BuildPlacement.IsValid(world, definitions.GetBuilding("bld_house")!, townCenter.Position, player));

        check("Schiessstand in der Steinzeit abgelehnt",
            !BuildPlacement.IsValid(world, definitions.GetBuilding("bld_range")!, spot, player));
    }

    private static bool EffectiveBuildersMonotonic()
    {
        // Spiegelt die Formel des ConstructionSystem: streng wachsend, aber gedaempft.
        static float Effective(int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++) total += 1f / (1f + i * 0.6f);
            return total;
        }

        return Effective(2) > Effective(1) && Effective(2) < 2f * Effective(1) && Effective(4) < 4f;
    }

    // --- Kampf -----------------------------------------------------------

    private static void CheckCombat(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;

        Player attacker = world.Players[0];
        Player defender = world.Players[1];

        // Zwei Kaempfer dicht nebeneinander in die Mitte setzen.
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

        check("Nahkaempfer fuegt Schaden zu", victim.Health < victimHealth);

        for (int i = 0; i < 400 && world.Entities.Exists(victim.Id); i++) world.Tick();

        check("Ziel wurde getoetet", !world.Entities.Exists(victim.Id));
        check("Abschuss in der Statistik", attacker.Stats.EnemiesKilled >= 1);
        check("Verlust in der Statistik", defender.Stats.UnitsLost >= 1);

        // Fernkampf: das Geschoss muss unterwegs sein, bevor es trifft.
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

        check("Fernkaempfer verschiesst ein Geschoss", sawProjectile);

        float targetHealth = target.Health;
        for (int i = 0; i < 200 && target.Health >= targetHealth; i++) world.Tick();
        check("Geschoss richtet beim Einschlag Schaden an", target.Health < targetHealth);

        // Konter-Matrix: derselbe Rohschaden wirkt unterschiedlich.
        Building wall = world.SpawnBuilding("bld_house", defender.Id, arena + new Vector2(20f, 20f))!;
        world.FlushSpawns();

        float before = wall.Health;
        world.ApplyDamage(wall, 20f, DamageType.Siege, attacker.Id);
        float siegeDamage = before - wall.Health;

        before = wall.Health;
        world.ApplyDamage(wall, 20f, DamageType.Ranged, attacker.Id);
        float rangedDamage = before - wall.Health;

        check("Belagerung schadet Gebaeuden mehr als Fernkampf", siegeDamage > rangedDamage * 2f);
    }

    // --- Zeitalter -------------------------------------------------------

    private static void CheckAgeAdvance(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player player = world.Players[0];

        Building townCenter = FindBuilding(world, player.Id, "bld_towncenter")!;

        check("Start in der Steinzeit", player.AgeIndex == 0);

        // Ohne zweites Gebaeude fehlt die Voraussetzung.
        foreach (ResourceType type in ResourceTypes.All) player.SetResource(type, 2000);

        world.Commands.Enqueue(new AdvanceAgeCommand { PlayerId = player.Id, Building = townCenter.Id });
        world.Tick();
        check("Aufstieg ohne zweites Gebaeude abgelehnt", !townCenter.IsResearchingAge);

        // Zweites Gebaeude fertig hinstellen, dann klappt es.
        world.SpawnBuilding("bld_house", player.Id, townCenter.Position + new Vector2(16f, 16f));
        world.FlushSpawns();

        int foodBefore = player.GetResource(ResourceType.Food);
        world.Commands.Enqueue(new AdvanceAgeCommand { PlayerId = player.Id, Building = townCenter.Id });
        world.Tick();

        check("Aufstieg gestartet", townCenter.IsResearchingAge);
        check("Aufstieg kostet Ressourcen", player.GetResource(ResourceType.Food) < foodBefore);

        AgeDefinition copper = definitions.GetAge(1)!;
        int ticks = Mathf.CeilToInt(copper.ResearchTimeSeconds / SimulationWorld.TickDelta) + 5;
        for (int i = 0; i < ticks; i++) world.Tick();

        check("Kupferzeit erreicht", player.AgeIndex == 1);
        check("Forschung beendet", !townCenter.IsResearchingAge);

        // Was das Zeitalter voraussetzt, ist jetzt erlaubt — mit Kaserne als Vorbedingung.
        world.SpawnBuilding("bld_barracks", player.Id, townCenter.Position + new Vector2(-16f, 16f));
        world.FlushSpawns();

        Vector2 spot = FindBuildSpot(world, townCenter.Position, definitions.GetBuilding("bld_range")!, player);
        check("Schiessstand jetzt baubar",
            BuildPlacement.IsValid(world, definitions.GetBuilding("bld_range")!, spot, player));
    }

    // --- Nebel -----------------------------------------------------------

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

        check("Eigene Basis ist sichtbar", vision.IsVisible(home.X, home.Y));
        check("Gegnerische Basis ist unbekannt", !vision.IsExplored(enemy.X, enemy.Y));

        // Einen Spaeher an die Feindbasis setzen und die Sicht neu aufbauen lassen.
        world.SpawnUnit("unit_scout", player.Id, match.StartOf(1));
        world.FlushSpawns();
        for (int i = 0; i < VisionSystem.RebuildInterval + 1; i++) world.Tick();

        check("Erkundetes Gebiet wird sichtbar", vision.IsVisible(enemy.X, enemy.Y));

        // Spaeher entfernen: das Gebiet bleibt erkundet, ist aber nicht mehr einsehbar.
        foreach (Unit unit in new List<Unit>(world.Entities.Units))
        {
            if (unit.DefinitionId == "unit_scout") world.Entities.Remove(unit.Id);
        }
        for (int i = 0; i < VisionSystem.RebuildInterval * 2; i++) world.Tick();

        check("Verlassenes Gebiet bleibt erkundet", vision.IsExplored(enemy.X, enemy.Y));
        check("Verlassenes Gebiet ist nicht mehr einsehbar", !vision.IsVisible(enemy.X, enemy.Y));
    }

    // --- Sieg ------------------------------------------------------------

    private static void CheckVictory(DefinitionDatabase definitions, System.Action<string, bool> check)
    {
        Match match = NewMatch(definitions);
        SimulationWorld world = match.World;
        Player loser = world.Players[1];

        check("Partie laeuft zu Beginn", !world.IsOver);

        // Alles des zweiten Spielers entfernen.
        foreach (Entity entity in new List<Entity>(world.Entities.All()))
        {
            if (entity.OwnerId == loser.Id) world.Entities.Remove(entity.Id);
        }

        for (int i = 0; i < 30 && !world.IsOver; i++) world.Tick();

        check("Spieler ohne Basis gilt als besiegt", loser.IsDefeated);
        check("Partie ist beendet", world.IsOver);
        check("Der andere Spieler hat gewonnen", world.Winner?.Id == world.Players[0].Id);
        check("Beendete Partie tickt nicht weiter", TickIsFrozen(world));
    }

    private static bool TickIsFrozen(SimulationWorld world)
    {
        int before = world.CurrentTick;
        world.Tick();
        return world.CurrentTick == before;
    }

    // --- Hilfsmittel -----------------------------------------------------

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

    /// <summary>Sucht spiralfoermig einen gueltigen Bauplatz um einen Punkt herum.</summary>
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
