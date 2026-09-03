namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Ein Baustein der Simulationslogik. Systeme laufen pro Tick in Registrierungsreihenfolge.
/// </summary>
/// <remarks>
/// Das Spiel waechst, indem hier Systeme dazukommen — Gathering, Construction, Combat,
/// Vision, AgeProgression —, nicht indem bestehende umgebaut werden.
/// </remarks>
public interface ISimulationSystem
{
    /// <summary>Name fuers Profiling-Overlay.</summary>
    string Name { get; }

    /// <param name="deltaSeconds">Immer <see cref="SimulationWorld.TickDelta"/> — fester Zeitschritt.</param>
    void Tick(SimulationWorld world, float deltaSeconds);
}
