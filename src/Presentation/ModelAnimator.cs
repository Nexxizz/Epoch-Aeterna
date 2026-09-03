using Godot;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Presentation;

/// <summary>
/// Picks the animation clip that matches what a unit is doing.
/// </summary>
/// <remarks>
/// The clip names are the contract with the Blender pipeline: whatever
/// <c>lib_anim.py</c> pushes onto an NLA strip arrives here under the same name.
/// A model without an AnimationPlayer — every placeholder, and every asset not
/// yet rebuilt — simply does nothing, so this never has to be guarded for.
/// </remarks>
public sealed class ModelAnimator
{
    public const string Idle = "Idle";
    public const string Walk = "Walk";
    public const string Chop = "Gather_Chop";
    public const string Death = "Death";

    /// <summary>Clips that should repeat rather than freeze on their last frame.</summary>
    private static readonly string[] Looping = { Idle, Walk, Chop };

    private readonly AnimationPlayer? _player;
    private string _current = string.Empty;

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
        if (_player is null) return;

        string wanted = unit switch
        {
            { Order: UnitOrder.Gather, GatherPhase: GatherPhase.Harvesting } => Chop,
            { Order: UnitOrder.Build } when !unit.HasPath => Chop,
            _ when unit.HasPath => Walk,
            _ => Idle,
        };

        Play(wanted);
    }

    public void PlayDeath() => Play(Death);

    private void Play(string name)
    {
        if (_player is null || _current == name) return;
        if (!_player.HasAnimation(name)) return;

        _current = name;
        _player.Play(name);
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
