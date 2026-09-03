using System.Collections.Generic;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Haelt alle lebenden Entities und vergibt IDs.
/// </summary>
/// <remarks>
/// Hinzufuegen und Entfernen werden bis zum Tick-Ende gepuffert. Systeme duerfen so
/// waehrend der Iteration Entities erzeugen und toeten, ohne die Auflistung zu zerstoeren.
/// </remarks>
public sealed class EntityRegistry
{
    private readonly Dictionary<int, Entity> _byId = new();
    private readonly List<Unit> _units = new();
    private readonly List<Building> _buildings = new();
    private readonly List<ResourceNode> _resourceNodes = new();

    private readonly List<Entity> _pendingAdd = new();
    private readonly List<EntityId> _pendingRemove = new();

    private int _nextId = 1;

    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Building> Buildings => _buildings;
    public IReadOnlyList<ResourceNode> ResourceNodes => _resourceNodes;
    public int Count => _byId.Count;

    /// <summary>Wird nach dem Anlegen gefeuert, sobald die Entity wirklich in den Listen steht.</summary>
    public event System.Action<Entity>? EntityAdded;

    public event System.Action<Entity>? EntityRemoved;

    public T Add<T>(T entity) where T : Entity
    {
        entity.Id = new EntityId(_nextId++);
        entity.ResetInterpolation();

        // Sofort auffindbar machen, aber erst beim Flush in die Typlisten einreihen.
        // Damit kann ein Befehl im selben Tick etwas erzeugen und darauf verweisen,
        // ohne dass laufende Iterationen ueber die Listen brechen.
        _byId[entity.Id.Value] = entity;
        _pendingAdd.Add(entity);
        return entity;
    }

    public void Remove(EntityId id) => _pendingRemove.Add(id);

    public Entity? Get(EntityId id) => _byId.GetValueOrDefault(id.Value);

    public Unit? GetUnit(EntityId id) => Get(id) as Unit;

    public Building? GetBuilding(EntityId id) => Get(id) as Building;

    public ResourceNode? GetResourceNode(EntityId id) => Get(id) as ResourceNode;

    public bool Exists(EntityId id) => _byId.ContainsKey(id.Value);

    /// <summary>Uebernimmt alle gepufferten Aenderungen. Wird am Ende jedes Ticks aufgerufen.</summary>
    public void Flush()
    {
        // Erst entfernen, dann hinzufuegen: eine im selben Tick erzeugte Entity
        // soll nicht versehentlich von einem aelteren Remove getroffen werden.
        if (_pendingRemove.Count > 0)
        {
            foreach (EntityId id in _pendingRemove)
            {
                if (!_byId.Remove(id.Value, out Entity? entity)) continue;

                switch (entity)
                {
                    case Unit unit: _units.Remove(unit); break;
                    case Building building: _buildings.Remove(building); break;
                    case ResourceNode node: _resourceNodes.Remove(node); break;
                }
                EntityRemoved?.Invoke(entity);
            }
            _pendingRemove.Clear();
        }

        if (_pendingAdd.Count > 0)
        {
            // Kopie, damit EntityAdded-Handler ihrerseits spawnen duerfen.
            var batch = new List<Entity>(_pendingAdd);
            _pendingAdd.Clear();

            foreach (Entity entity in batch)
            {
                // _byId wurde bereits in Add gefuellt.
                switch (entity)
                {
                    case Unit unit: _units.Add(unit); break;
                    case Building building: _buildings.Add(building); break;
                    case ResourceNode node: _resourceNodes.Add(node); break;
                }
                EntityAdded?.Invoke(entity);
            }
        }
    }

    /// <summary>Speichert fuer alle Entities den Zustand vor dem Tick — Grundlage der View-Interpolation.</summary>
    public void CaptureInterpolationSnapshots()
    {
        foreach (Unit unit in _units) unit.CaptureInterpolationSnapshot();
        foreach (Building building in _buildings) building.CaptureInterpolationSnapshot();
    }

    public IEnumerable<Entity> All()
    {
        foreach (Unit unit in _units) yield return unit;
        foreach (Building building in _buildings) yield return building;
        foreach (ResourceNode node in _resourceNodes) yield return node;
    }
}
