namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Ein Befehl an die Simulation. Der einzige Weg, sie von aussen zu veraendern —
/// fuer den menschlichen Spieler wie fuer die KI.
/// </summary>
/// <remarks>
/// Diese Enge ist Absicht: Wenn jede Zustandsaenderung durch eine serialisierbare
/// Befehlsliste laeuft, sind Lockstep-Multiplayer, Replays und Savegames spaeter
/// Erweiterungen statt Umbauten.
/// </remarks>
public interface ICommand
{
    /// <summary>Wer den Befehl gibt. Die Ausfuehrung prueft damit Besitzverhaeltnisse.</summary>
    int PlayerId { get; }

    void Execute(SimulationWorld world);
}
