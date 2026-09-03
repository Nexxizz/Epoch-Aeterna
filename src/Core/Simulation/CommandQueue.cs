using System.Collections.Generic;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Collects commands that arrive between two ticks and executes them at the start
/// of the next one.
/// </summary>
/// <remarks>
/// The delay is intentional: every command of a tick runs in the same order on the
/// same world state. That property is exactly what lockstep multiplayer will need
/// later on.
/// </remarks>
public sealed class CommandQueue
{
    private readonly List<ICommand> _incoming = new();
    private readonly List<ICommand> _executing = new();

    public int PendingCount => _incoming.Count;

    /// <summary>Counter across the whole match — useful for the debug overlay and for replays.</summary>
    public int TotalExecuted { get; private set; }

    public void Enqueue(ICommand command) => _incoming.Add(command);

    public void ExecutePending(SimulationWorld world)
    {
        if (_incoming.Count == 0) return;

        // Move them across rather than iterating directly: a command may queue further
        // commands while executing, and those then run in the next tick.
        _executing.AddRange(_incoming);
        _incoming.Clear();

        foreach (ICommand command in _executing)
        {
            command.Execute(world);
            TotalExecuted++;
        }
        _executing.Clear();
    }

    public void Clear()
    {
        _incoming.Clear();
        _executing.Clear();
    }
}
