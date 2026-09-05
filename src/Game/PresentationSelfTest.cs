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
