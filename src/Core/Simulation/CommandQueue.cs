using System.Collections.Generic;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Sammelt Befehle, die zwischen zwei Ticks eingehen, und fuehrt sie zu Beginn des
/// naechsten Ticks aus.
/// </summary>
/// <remarks>
/// Die Verzoegerung ist gewollt: Alle Befehle eines Ticks werden in derselben
/// Reihenfolge auf demselben Weltzustand ausgefuehrt. Genau diese Eigenschaft
/// braucht spaeter der Lockstep-Multiplayer.
/// </remarks>
public sealed class CommandQueue
{
    private readonly List<ICommand> _incoming = new();
    private readonly List<ICommand> _executing = new();

    public int PendingCount => _incoming.Count;

    /// <summary>Zaehler ueber die gesamte Partie — nuetzlich fuer Debug-Overlay und Replays.</summary>
    public int TotalExecuted { get; private set; }

    public void Enqueue(ICommand command) => _incoming.Add(command);

    public void ExecutePending(SimulationWorld world)
    {
        if (_incoming.Count == 0) return;

        // Umhaengen statt direkt iterieren: Ein Befehl darf waehrend seiner
        // Ausfuehrung neue Befehle einreihen, die dann erst im naechsten Tick laufen.
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
