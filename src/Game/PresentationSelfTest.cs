using System;
using System.Linq;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.Game;

/// <summary>Exercises the imported rig and real view lifecycle in a headless SceneTree.</summary>
public partial class PresentationSelfTest : Node
{
    private int _checks;

    public override async void _Ready()
    {
        try
        {
            var definitions = new DefinitionDatabase();
            definitions.LoadAll();
            var world = new SimulationWorld(definitions, new NavGrid(8, 8));
            var runner = new SimulationRunner { TimeScale = 0 };
            AddChild(runner);
            runner.Attach(world);
            runner.SetProcess(false);
            var manager = new ViewManager();
            AddChild(manager);
            manager.Attach(world, runner);
            Unit unit = world.SpawnUnit("unit_settler", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView view = manager.Views.Single().View;
            view.SetProcess(false);
            AnimationPlayer player = Find<AnimationPlayer>(view)!;
            Check("GLB has an AnimationPlayer", player is not null);
            player!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            string[] clips = { "Idle", "Walk", "Run", "Carry_Walk", "Carry_Run", "Gather_Food",
                "Gather_Chop", "Gather_Mine", "Attack", "Build", "Death" };
            foreach (string clip in clips)
            {
                Check($"Imported {clip}", player.HasAnimation(clip));
                Check($"{clip} has motion tracks", player.GetAnimation(clip).GetTrackCount() > 10);
            }
            Skeleton3D skeleton = Find<Skeleton3D>(view)!;
            foreach (string bone in new[] { "foot.L", "foot.R", "hand.L", "hand.R" })
                Check($"Imported {bone}", skeleton.FindBone(bone) >= 0);

            runner.TimeScale = 1;
            unit.OrderMoveTo(new Vector2(5, 0));
            unit.Path.Add(new Vector2(5, 0));
            unit.Position = new Vector2(.13f, 0);
            view._Process(0);
            Check("Fast movement uses Run", player.AssignedAnimation == ModelAnimator.Run);
            Check("Stride rate follows displacement", player.SpeedScale > 1.2f && player.SpeedScale < 1.4f);
            unit.CarriedAmount = 2;
            view._Process(0);
            Check("Loaded movement uses Carry_Run", player.AssignedAnimation == ModelAnimator.CarryRun);
            unit.Stop();
            unit.Order = UnitOrder.Gather;
            unit.GatherPhase = GatherPhase.Harvesting;
            foreach (var (resource, clip) in new[] { (ResourceType.Food, "Gather_Food"),
                (ResourceType.Wood, "Gather_Chop"), (ResourceType.Stone, "Gather_Mine") })
            {
                unit.CarriedResource = resource;
                view._Process(0);
                Check($"Work state selects {clip}", player.AssignedAnimation == clip);
                player.Advance(0.25);
                player.Advance(0); // Evaluate once more after the crossfade expires.
                string activeProp = resource switch
                {
                    ResourceType.Food => "tool_basket", ResourceType.Wood => "tool_axe", _ => "tool_pick",
                };
                Check($"{clip} exposes its tool", skeleton.GetBonePoseScale(skeleton.FindBone(activeProp)).X > 0.99f);
                Check($"{clip} hides the spear", skeleton.GetBonePoseScale(skeleton.FindBone("tool_spear")).X < 0.01f);
            }

            view.SetSelected(true);
            world.ApplyDamage(unit, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Death removes simulation unit immediately", !world.Entities.Exists(unit.Id));
            Check("Visible corpse survives removal", !view.IsQueuedForDeletion());
            Check("Corpse remains in fog visibility list", manager.Views.Count() == 1);
            Check("Death selected", player.AssignedAnimation == ModelAnimator.Death);
            Check("Death cannot loop", player.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);
            Check("Selection hidden", !view.GetNode<Node3D>("SelectionRing").Visible);
            double duration = player.GetAnimation("Death").Length;
            runner.TimeScale = 0;
            view._Process(20);
            Check("Pause stops animation clock", player.SpeedScale == 0);
            Check("Pause does not expire corpse", !view.IsQueuedForDeletion());
            runner.TimeScale = 2;
            view._Process(duration / 2);
            player.Advance(duration / 2);
            player.Advance(0);
            Check("Death retains its final pose", player.AssignedAnimation == ModelAnimator.Death);
            Check("Death lowers the pelvis", skeleton.GetBoneGlobalPose(skeleton.FindBone("hips")).Origin.Y < 0.35f);
            Check("Death keeps ankles above the floor", skeleton.GetBoneGlobalPose(skeleton.FindBone("foot.L")).Origin.Y > 0.085f);
            Check("Corpse holds after fall", !view.IsQueuedForDeletion());
            view._Process(2);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check("Corpse eventually freed", !GodotObject.IsInstanceValid(view));

            var fallback = new EntityView();
            AddChild(fallback);
            fallback.Bind(new Unit(), runner, world.Nav, new Node3D(), world.Random);
            Check("Models without Death retain immediate-removal fallback", !fallback.BeginDeath());
            fallback.QueueFree();

            runner.TimeScale = 1;
            Unit scout = world.SpawnUnit("unit_scout", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView scoutView = manager.Views.Single().View;
            scoutView.SetProcess(false);
            AnimationPlayer scoutPlayer = Find<AnimationPlayer>(scoutView)!;
            Check("Scout uses the imported animated model", scoutPlayer is not null);
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                Check($"Scout imports {clip}", scoutPlayer!.HasAnimation(clip)
                    && scoutPlayer.GetAnimation(clip).GetTrackCount() > 10);
            scout.OrderMoveTo(new Vector2(5, 0));
            scout.Path.Add(new Vector2(5, 0));
            scout.Position = new Vector2(.26f, 0);
            scoutView._Process(0);
            Check("Scout runs at scouting speed", scoutPlayer!.AssignedAnimation == ModelAnimator.Run);
            scout.Stop();
            scout.Order = UnitOrder.Attack;
            scoutView._Process(0);
            Check("Scout selects club attack", scoutPlayer.AssignedAnimation == ModelAnimator.Attack);
            world.ApplyDamage(scout, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Scout death leaves an animated corpse", !scoutView.IsQueuedForDeletion()
                && scoutPlayer.AssignedAnimation == ModelAnimator.Death);
            Check("Scout death does not loop", scoutPlayer.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);

            Unit spearman = world.SpawnUnit("unit_spearman", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView spearView = manager.Views.Single(entry => entry.Entity.Id == spearman.Id).View;
            spearView.SetProcess(false);
            AnimationPlayer spearPlayer = Find<AnimationPlayer>(spearView)!;
            Check("Spearman uses the imported animated model", spearPlayer is not null);
            spearPlayer!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            Skeleton3D spearRig = Find<Skeleton3D>(spearView)!;
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
            {
                Check($"Spearman imports {clip}", spearPlayer.HasAnimation(clip)
                    && spearPlayer.GetAnimation(clip).GetTrackCount() > 10);
                spearPlayer.Play(clip);
                spearPlayer.Advance(.4);
                spearPlayer.Advance(0);
                Check($"Spearman keeps spear visible in {clip}",
                    spearRig.GetBonePoseScale(spearRig.FindBone("tool_spear")).X > .99f);
            }
            Check("Spear attack matches the 1.6 second cooldown",
                Math.Abs(spearPlayer.GetAnimation("Attack").Length - spearman.AttackCooldownSeconds) < .01);
            spearman.OrderMoveTo(new Vector2(5, 0));
            spearman.Path.Add(new Vector2(5, 0));
            spearman.Position = new Vector2(.145f, 0);
            spearView._Process(0);
            Check("Spearman runs at infantry speed", spearPlayer.AssignedAnimation == ModelAnimator.Run);
            spearman.Stop();
            spearman.Order = UnitOrder.Attack;
            spearView._Process(0);
            Check("Spearman selects thrust attack", spearPlayer.AssignedAnimation == ModelAnimator.Attack);
            world.ApplyDamage(spearman, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Spearman leaves an animated corpse", !spearView.IsQueuedForDeletion()
                && spearPlayer.AssignedAnimation == ModelAnimator.Death);
            Check("Spearman death does not loop", spearPlayer.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);

            Unit slinger = world.SpawnUnit("unit_slinger", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView slingView = manager.Views.Single(entry => entry.Entity.Id == slinger.Id).View;
            slingView.SetProcess(false);
            AnimationPlayer slingPlayer = Find<AnimationPlayer>(slingView)!;
            Check("Slinger uses the imported animated model", slingPlayer is not null);
            slingPlayer!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            Skeleton3D slingRig = Find<Skeleton3D>(slingView)!;
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                Check($"Slinger imports {clip}", slingPlayer.HasAnimation(clip)
                    && slingPlayer.GetAnimation(clip).GetTrackCount() > 10);
            foreach (string bone in new[] { "sling_cord", "sling_pouch", "sling_release", "sling_stone" })
                Check($"Slinger imports {bone}", slingRig.FindBone(bone) >= 0);
            Check("Sling attack matches the two second cooldown",
                Math.Abs(slingPlayer.GetAnimation("Attack").Length - slinger.AttackCooldownSeconds) < .01);
            slingPlayer.Play("Attack");
            slingPlayer.Seek(.8, true);
            Check("Sling carries stone before release", slingRig.GetBonePoseScale(slingRig.FindBone("sling_stone")).X > .99f);
            slingPlayer.Seek(1.3, true);
            Check("Sling releases the loaded stone", slingRig.GetBonePoseScale(slingRig.FindBone("sling_stone")).X < .01f);
            slingPlayer.Seek(1.95, true);
            Check("Sling reloads for the next throw", slingRig.GetBonePoseScale(slingRig.FindBone("sling_stone")).X > .99f);
            slinger.OrderMoveTo(new Vector2(5, 0));
            slinger.Path.Add(new Vector2(5, 0));
            slinger.Position = new Vector2(.14f, 0);
            slingView._Process(0);
            Check("Slinger runs at ranged infantry speed", slingPlayer.AssignedAnimation == ModelAnimator.Run);
            slinger.Stop();
            slinger.Order = UnitOrder.Attack;
            slingView._Process(0);
            Check("Slinger selects sling attack", slingPlayer.AssignedAnimation == ModelAnimator.Attack);
            world.ApplyDamage(slinger, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Slinger leaves an animated corpse", !slingView.IsQueuedForDeletion()
                && slingPlayer.AssignedAnimation == ModelAnimator.Death);
            Check("Slinger death does not loop", slingPlayer.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);

            Unit swordsman = world.SpawnUnit("unit_swordsman", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView swordView = manager.Views.Single(entry => entry.Entity.Id == swordsman.Id).View;
            swordView.SetProcess(false);
            AnimationPlayer swordPlayer = Find<AnimationPlayer>(swordView)!;
            Check("Swordsman uses the imported animated model", swordPlayer is not null);
            swordPlayer!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            Skeleton3D swordRig = Find<Skeleton3D>(swordView)!;
            Check("Swordsman imports shield bone", swordRig.FindBone("shield") >= 0);
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
            {
                Check($"Swordsman imports {clip}", swordPlayer.HasAnimation(clip)
                    && swordPlayer.GetAnimation(clip).GetTrackCount() > 10);
                swordPlayer.Play(clip);
                swordPlayer.Advance(.4);
                swordPlayer.Advance(0);
                Check($"Swordsman keeps sword and shield visible in {clip}",
                    swordRig.GetBonePoseScale(swordRig.FindBone("tool_axe")).X > .99f
                    && swordRig.GetBonePoseScale(swordRig.FindBone("shield")).X > .99f);
            }
            Check("Sword attack matches the 1.5 second cooldown",
                Math.Abs(swordPlayer.GetAnimation("Attack").Length - swordsman.AttackCooldownSeconds) < .01);
            swordsman.OrderMoveTo(new Vector2(5, 0));
            swordsman.Path.Add(new Vector2(5, 0));
            swordsman.Position = new Vector2(.135f, 0);
            swordView._Process(0);
            Check("Swordsman runs at heavy infantry speed", swordPlayer.AssignedAnimation == ModelAnimator.Run);
            swordsman.Stop();
            swordsman.Order = UnitOrder.Attack;
            swordView._Process(0);
            Check("Swordsman selects sword attack", swordPlayer.AssignedAnimation == ModelAnimator.Attack);
            world.ApplyDamage(swordsman, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Swordsman leaves an animated corpse", !swordView.IsQueuedForDeletion()
                && swordPlayer.AssignedAnimation == ModelAnimator.Death);
            Check("Swordsman death does not loop", swordPlayer.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);

            Unit archer = world.SpawnUnit("unit_archer", 1, Vector2.Zero)!;
            world.FlushSpawns();
            EntityView bowView = manager.Views.Single(entry => entry.Entity.Id == archer.Id).View;
            bowView.SetProcess(false);
            AnimationPlayer bowPlayer = Find<AnimationPlayer>(bowView)!;
            Check("Archer uses the imported animated model", bowPlayer is not null);
            bowPlayer!.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            Skeleton3D bowRig = Find<Skeleton3D>(bowView)!;
            foreach (string clip in new[] { "Idle", "Walk", "Run", "Attack", "Death" })
                Check($"Archer imports {clip}", bowPlayer.HasAnimation(clip)
                    && bowPlayer.GetAnimation(clip).GetTrackCount() > 10);
            foreach (string bone in new[] { "bow_upper_0", "bow_upper_1", "bow_lower_0", "bow_lower_1",
                "string_upper", "string_lower", "nocked_arrow" })
                Check($"Archer imports {bone}", bowRig.FindBone(bone) >= 0);
            Check("Bow attack matches the 1.8 second cooldown",
                Math.Abs(bowPlayer.GetAnimation("Attack").Length - archer.AttackCooldownSeconds) < .01);
            bowPlayer.Play("Attack");
            bowPlayer.Seek(.9, true);
            Check("Bow carries arrow while aiming", bowRig.GetBonePoseScale(bowRig.FindBone("nocked_arrow")).X > .99f);
            bowPlayer.Seek(1.3, true);
            Check("Bow releases nocked arrow", bowRig.GetBonePoseScale(bowRig.FindBone("nocked_arrow")).X < .01f);
            archer.OrderMoveTo(new Vector2(5, 0));
            archer.Path.Add(new Vector2(5, 0));
            archer.Position = new Vector2(.14f, 0);
            bowView._Process(0);
            Check("Archer runs at ranged infantry speed", bowPlayer.AssignedAnimation == ModelAnimator.Run);
            archer.Stop();
            archer.Order = UnitOrder.Attack;
            bowView._Process(0);
            Check("Archer selects bow attack", bowPlayer.AssignedAnimation == ModelAnimator.Attack);
            world.ApplyDamage(archer, 1000, DamageType.Blunt, 2);
            world.Entities.Flush();
            Check("Archer leaves an animated corpse", !bowView.IsQueuedForDeletion()
                && bowPlayer.AssignedAnimation == ModelAnimator.Death);
            Check("Archer death does not loop", bowPlayer.GetAnimation("Death").LoopMode == Animation.LoopModeEnum.None);

            // Exercise the real menu: a valid training definition alone does not
            // guarantee that players can see or click its button.
            var owner = new Player { Id = 1, Name = "Menu test", Color = Colors.Blue };
            owner.SetResource(ResourceType.Food, 500);
            owner.SetResource(ResourceType.Wood, 500);
            owner.SetResource(ResourceType.Gold, 500);
            world.AddPlayer(owner);
            var camera = new RtsCamera();
            var selection = new SelectionController();
            AddChild(camera);
            AddChild(selection);
            selection.Attach(world, camera, manager, 1);
            var menu = new TrainingMenu();
            AddChild(menu);
            menu.Attach(world, selection, 1);
            foreach (string buildingId in new[] { "bld_towncenter", "bld_barracks", "bld_range" })
            {
                Building building = world.SpawnBuilding(buildingId, 1, Vector2.Zero)!;
                world.FlushSpawns();
                selection.SelectOnly(building.Id);
                menu._Process(0);
                string[] offered = definitions.GetBuilding(buildingId)!.TrainableUnitIds;
                foreach (string unitId in definitions.Units.Keys)
                {
                    Button? button = menu.FindChild($"Train_{unitId}", true, false) as Button;
                    Check($"{buildingId} menu visibility for {unitId}",
                        (button?.IsVisibleInTree() == true) == offered.Contains(unitId));
                }
                if (buildingId == "bld_towncenter")
                {
                    var button = (Button)menu.FindChild("Train_unit_scout", true, false);
                    Check("Scout training button is enabled and has a portrait", !button.Disabled && button.Icon is not null);
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    world.Tick();
                    Check("Scout button queues scout training", building.Queue.Count == 1
                        && building.CurrentOrder!.UnitDefinitionId == "unit_scout");
                    Check("Scout training costs 30 food", owner.GetResource(ResourceType.Food) == 470);
                }
                if (buildingId == "bld_barracks")
                {
                    var button = (Button)menu.FindChild("Train_unit_spearman", true, false);
                    Check("Spearman training button is enabled and has a portrait", !button.Disabled && button.Icon is not null);
                    float food = owner.GetResource(ResourceType.Food);
                    float wood = owner.GetResource(ResourceType.Wood);
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    world.Tick();
                    Check("Barracks button queues spearman", building.Queue.Count == 1
                        && building.CurrentOrder!.UnitDefinitionId == "unit_spearman");
                    Check("Spearman costs 60 food and 20 wood", owner.GetResource(ResourceType.Food) == food - 60
                        && owner.GetResource(ResourceType.Wood) == wood - 20);
                    var swordButton = (Button)menu.FindChild("Train_unit_swordsman", true, false);
                    Check("Swordsman has a portrait but is age-locked", swordButton.Icon is not null && swordButton.Disabled);
                    owner.SetAge(1);
                    menu._Process(0);
                    Check("Copper Age unlocks swordsman training", !swordButton.Disabled);
                    food = owner.GetResource(ResourceType.Food);
                    float gold = owner.GetResource(ResourceType.Gold);
                    swordButton.EmitSignal(BaseButton.SignalName.Pressed);
                    world.Tick();
                    Check("Barracks queues swordsman behind spearman", building.Queue.Count == 2
                        && building.Queue.Last().UnitDefinitionId == "unit_swordsman");
                    Check("Swordsman costs 70 food and 40 gold", owner.GetResource(ResourceType.Food) == food - 70
                        && owner.GetResource(ResourceType.Gold) == gold - 40);
                }
                if (buildingId == "bld_range")
                {
                    var button = (Button)menu.FindChild("Train_unit_slinger", true, false);
                    Check("Slinger training button is enabled and has a portrait", !button.Disabled && button.Icon is not null);
                    float food = owner.GetResource(ResourceType.Food);
                    float wood = owner.GetResource(ResourceType.Wood);
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    world.Tick();
                    Check("Range button queues slinger", building.Queue.Count == 1
                        && building.CurrentOrder!.UnitDefinitionId == "unit_slinger");
                    Check("Slinger costs 45 food and 35 wood", owner.GetResource(ResourceType.Food) == food - 45
                        && owner.GetResource(ResourceType.Wood) == wood - 35);
                    var archerButton = (Button)menu.FindChild("Train_unit_archer", true, false);
                    owner.SetAge(0);
                    menu._Process(0);
                    Check("Archer has portrait but is age-locked", archerButton.Icon is not null && archerButton.Disabled);
                    owner.SetAge(1);
                    menu._Process(0);
                    Check("Copper Age unlocks archer training", !archerButton.Disabled);
                    food = owner.GetResource(ResourceType.Food);
                    wood = owner.GetResource(ResourceType.Wood);
                    float gold = owner.GetResource(ResourceType.Gold);
                    archerButton.EmitSignal(BaseButton.SignalName.Pressed);
                    world.Tick();
                    Check("Range queues archer behind slinger", building.Queue.Count == 2
                        && building.Queue.Last().UnitDefinitionId == "unit_archer");
                    Check("Archer costs 50 food, 60 wood and 20 gold", owner.GetResource(ResourceType.Food) == food - 50
                        && owner.GetResource(ResourceType.Wood) == wood - 60 && owner.GetResource(ResourceType.Gold) == gold - 20);
                }
            }
            GD.Print($"All {_checks} presentation checks passed.");
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Check(string label, bool condition)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
        GD.Print("[presentation] " + label);
    }

    private static T? Find<T>(Node node) where T : Node
    {
        if (node is T result) return result;
        foreach (Node child in node.GetChildren())
            if (Find<T>(child) is { } found) return found;
        return null;
    }
}
