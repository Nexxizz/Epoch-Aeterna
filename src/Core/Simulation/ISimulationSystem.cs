namespace EpochAeterna.Core.Simulation;

/// <summary>
/// One building block of the simulation logic. Systems run once per tick, in registration order.
/// </summary>
/// <remarks>
/// The game grows by adding systems here — Gathering, Construction, Combat,
/// Vision, AgeProgression — not by rebuilding existing ones.
/// </remarks>
public interface ISimulationSystem
{
    /// <summary>Name for the profiling overlay.</summary>
    string Name { get; }

    /// <param name="deltaSeconds">Always <see cref="SimulationWorld.TickDelta"/> — a fixed time step.</param>
    void Tick(SimulationWorld world, float deltaSeconds);
}
