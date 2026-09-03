using Godot;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Bindeglied zwischen Godots variabler Bildrate und dem festen Simulationstakt.
/// </summary>
/// <remarks>
/// Sammelt die verstrichene Zeit und ruft <see cref="SimulationWorld.Tick"/> so oft auf,
/// wie ganze Zeitschritte hineinpassen. Der Rest bleibt als <see cref="InterpolationAlpha"/>
/// stehen, mit dem die Views zwischen dem vorigen und dem aktuellen Zustand blenden.
/// </remarks>
public sealed partial class SimulationRunner : Node
{
    /// <summary>
    /// Obergrenze der Ticks pro Frame. Verhindert die Todesspirale, wenn ein Frame
    /// einbricht (Alt-Tab, Ladehaenger) und der Rueckstand immer weiter waechst.
    /// </summary>
    private const int MaxTicksPerFrame = 5;

    private float _accumulator;

    public SimulationWorld? World { get; private set; }

    /// <summary>0 = Zustand vor dem letzten Tick, 1 = danach.</summary>
    public float InterpolationAlpha { get; private set; }

    /// <summary>0 = pausiert, 1 = normal, 2 = doppelte Geschwindigkeit (Phase 3.7).</summary>
    public float TimeScale { get; set; } = 1f;

    public bool IsPaused => TimeScale <= 0f;

    /// <summary>Ticks im letzten Frame — fuers Profiling-Overlay (Phase 8).</summary>
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

        // Rueckstand verwerfen, statt ihn vor sich herzuschieben: lieber ein
        // einmaliger Zeitsprung als dauerhaft ruckelnde Aufholjagd.
        if (ticks >= MaxTicksPerFrame) _accumulator = 0f;

        LastFrameTickCount = ticks;
        InterpolationAlpha = _accumulator / SimulationWorld.TickDelta;
    }
}
