using System;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Core.Simulation;

/// <summary>
/// Einbahn-Meldeweg der Simulation an alles ausserhalb: Views, HUD, VFX, Audio, KI.
/// </summary>
/// <remarks>
/// Die Richtung ist strikt Sim → Aussenwelt. Wer die Simulation aendern will, schickt
/// einen Befehl durch die <see cref="CommandQueue"/>. Damit bleibt die Sim autark
/// und spaeter deterministisch reproduzierbar.
/// </remarks>
public sealed class GameEvents
{
    public event Action<Entity>? EntitySpawned;
    public event Action<Entity>? EntityRemoved;
    public event Action<Entity, float>? EntityDamaged;
    public event Action<Building, string>? ProductionQueued;
    public event Action<Building, Unit>? ProductionCompleted;

    internal void RaiseEntitySpawned(Entity entity) => EntitySpawned?.Invoke(entity);

    internal void RaiseEntityRemoved(Entity entity) => EntityRemoved?.Invoke(entity);

    internal void RaiseEntityDamaged(Entity entity, float amount) => EntityDamaged?.Invoke(entity, amount);

    internal void RaiseProductionQueued(Building building, string unitId) =>
        ProductionQueued?.Invoke(building, unitId);

    internal void RaiseProductionCompleted(Building building, Unit unit) =>
        ProductionCompleted?.Invoke(building, unit);
}
