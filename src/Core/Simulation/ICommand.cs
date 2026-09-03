namespace EpochAeterna.Core.Simulation;

/// <summary>
/// A command to the simulation. The only way to change it from outside —
/// for the human player as much as for the AI.
/// </summary>
/// <remarks>
/// This narrowness is deliberate: when every state change goes through a serialisable
/// list of commands, lockstep multiplayer, replays and save games become extensions
/// later on rather than rewrites.
/// </remarks>
public interface ICommand
{
    /// <summary>Who issues the command. Execution uses it to check ownership.</summary>
    int PlayerId { get; }

    void Execute(SimulationWorld world);
}
