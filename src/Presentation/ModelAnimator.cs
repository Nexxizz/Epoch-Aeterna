using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Picks the animation clip that matches what a unit is doing.
/// </summary>
/// <remarks>
/// The clip names are the contract with the Blender pipeline: whatever
/// <c>settler_motion.py</c> pushes onto an NLA strip arrives here under the same name.
/// A model without an AnimationPlayer — every placeholder, and every asset not
/// yet rebuilt — simply does nothing, so this never has to be guarded for.
/// </remarks>
public sealed class ModelAnimator
{
    public const string Idle = "Idle";
    public const string Walk = "Walk";
    public const string Run = "Run";
    public const string Carry = "Carry_Walk";
    public const string CarryRun = "Carry_Run";
    public const string GatherFood = "Gather_Food";
    public const string Chop = "Gather_Chop";
    public const string Mine = "Gather_Mine";
    public const string Build = "Build";
    public const string Attack = "Attack";
    public const string Death = "Death";

    /// <summary>Clips that should repeat rather than freeze on their last frame.</summary>
    private static readonly string[] Looping =
        { Idle, Walk, Run, Carry, CarryRun, GatherFood, Chop, Mine, Build, Attack };

    private readonly AnimationPlayer? _player;
    private string _current = string.Empty;
    private bool _dying;
    private float _timeScale = 1f;
    private float _strideScale = 1f;

    public bool IsAvailable => _player is not null;

    public ModelAnimator(Node model, RandomNumberGenerator random)
    {
        _player = FindPlayer(model);
        if (_player is null) return;

        ConfigureLoops();

        // Offset each unit slightly, otherwise a group of settlers breathes and
        // steps in perfect unison, which reads as obviously artificial.
        Play(Idle);
        if (_player.CurrentAnimation != string.Empty)
        {
            _player.Advance(random.RandfRange(0f, (float)_player.CurrentAnimationLength));
        }
    }

    /// <summary>Chooses and plays the clip for the unit's current state.</summary>
    public void Sync(Unit unit)
    {
        if (_player is null || _dying) return;

        float speed = unit.Position.DistanceTo(unit.PreviousPosition) / SimulationWorld.TickDelta;
        // Hysteresis prevents walk/run flicker while slowing down or turning.
        bool jogging = speed > (_current is Run or CarryRun ? 1.3f : 1.6f);

        string wanted = unit switch
        {
            { Order: UnitOrder.Gather, GatherPhase: GatherPhase.Harvesting,
                CarriedResource: ResourceType.Food } => GatherFood,
            { Order: UnitOrder.Gather, GatherPhase: GatherPhase.Harvesting,
                CarriedResource: ResourceType.Wood } => Chop,
            { Order: UnitOrder.Gather, GatherPhase: GatherPhase.Harvesting } => Mine,
            { Order: UnitOrder.Build } when !unit.HasPath => Build,
            { Order: UnitOrder.Attack } when !unit.HasPath => Attack,
            _ when unit.HasPath && unit.CarriedAmount > 0f && jogging && _player.HasAnimation(CarryRun) => CarryRun,
            _ when unit.HasPath && unit.CarriedAmount > 0f => Carry,
            _ when unit.HasPath && jogging => Run,
            _ when unit.HasPath => Walk,
            _ => Idle,
        };

        Play(wanted);
        // Match the backwards travel of a planted foot to world movement.
        float authoredSpeed = wanted switch
        {
            Walk => 0.847f, Run => 1.979f, Carry => 0.753f, CarryRun => 1.696f,
            _ => 0f,
        };
        _strideScale = authoredSpeed > 0 ? Mathf.Clamp(speed / authoredSpeed, 0.05f, 2f) : 1f;
        _player.SpeedScale = _timeScale * _strideScale;
    }

    /// <summary>Stops orders from replacing Death; returns its actual imported duration.</summary>
    public double PlayDeath()
    {
        if (_player is null || !_player.HasAnimation(Death)) return 0;
        _dying = true;
        _strideScale = 1f;
        _player.SpeedScale = _timeScale;
        _player.GetAnimation(Death).LoopMode = Animation.LoopModeEnum.None;
        Play(Death);
        return _player.GetAnimation(Death).Length;
    }

    public void SetTimeScale(float scale)
    {
        _timeScale = scale;
        if (_player is not null) _player.SpeedScale = scale * _strideScale;
    }

    private void Play(string name)
    {
        if (_player is null || _current == name) return;
        if (!_player.HasAnimation(name)) return;

        _current = name;
        _player.Play(name, customBlend: name == Death ? 0.08 : 0.16);
    }

    private void ConfigureLoops()
    {
        if (_player is null) return;

        foreach (string name in Looping)
        {
            Animation? animation = _player.HasAnimation(name) ? _player.GetAnimation(name) : null;
            if (animation is not null) animation.LoopMode = Animation.LoopModeEnum.Linear;
        }
    }

    /// <summary>Depth-first search — glTF nests the player under the imported root.</summary>
    private static AnimationPlayer? FindPlayer(Node node)
    {
        if (node is AnimationPlayer player) return player;

        foreach (Node child in node.GetChildren())
        {
            AnimationPlayer? found = FindPlayer(child);
            if (found is not null) return found;
        }
        return null;
    }
}
