using Godot;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// The link between Godot's variable frame rate and the fixed simulation tick.
/// </summary>
/// <remarks>
/// Accumulates elapsed time and calls <see cref="SimulationWorld.Tick"/> as often as
/// whole time steps fit into it. The remainder stays as <see cref="InterpolationAlpha"/>,
/// which the views use to blend between the previous and the current state.
/// </remarks>
public sealed partial class SimulationRunner : Node
{
    /// <summary>
    /// Cap on ticks per frame. Prevents the death spiral when a frame stalls
    /// (alt-tab, a loading hitch) and the backlog keeps growing.
    /// </summary>
    private const int MaxTicksPerFrame = 5;

    private float _accumulator;

    public SimulationWorld? World { get; private set; }

    /// <summary>0 = state before the last tick, 1 = after it.</summary>
    public float InterpolationAlpha { get; private set; }

    /// <summary>0 = paused, 1 = normal, 2 = double speed.</summary>
    public float TimeScale { get; set; } = 1f;

    public bool IsPaused => TimeScale <= 0f;

    /// <summary>Ticks in the last frame — for the profiling overlay.</summary>
    public int LastFrameTickCount { get; private set; }

    public void Attach(SimulationWorld world)
    {
        World = world;
        _accumulator = 0f;
        InterpolationAlpha = 0f;
    }

    public override void _Process(double delta)
    {
        if (World is null || IsPaused)
        {
            LastFrameTickCount = 0;
            return;
        }

        _accumulator += (float)delta * TimeScale;

        int ticks = 0;
        while (_accumulator >= SimulationWorld.TickDelta && ticks < MaxTicksPerFrame)
        {
            World.Tick();
            _accumulator -= SimulationWorld.TickDelta;
            ticks++;
        }

        // Discard the backlog rather than pushing it along: better a single jump in
        // time than a permanently stuttering catch-up.
        if (ticks >= MaxTicksPerFrame) _accumulator = 0f;

        LastFrameTickCount = ticks;
        InterpolationAlpha = _accumulator / SimulationWorld.TickDelta;
    }
}
