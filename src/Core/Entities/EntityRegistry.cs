using System.Collections.Generic;

namespace EpochAeterna.Core.Entities;

/// <summary>
/// Holds every living entity and hands out ids.
/// </summary>
/// <remarks>
/// Additions and removals are buffered until the end of the tick. Systems may therefore
/// create and kill entities while iterating without destroying the enumeration.
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

    /// <summary>Raised after creation, once the entity is actually in the lists.</summary>
    public event System.Action<Entity>? EntityAdded;

    public event System.Action<Entity>? EntityRemoved;

    public T Add<T>(T entity) where T : Entity
    {
        entity.Id = new EntityId(_nextId++);
        entity.ResetInterpolation();

        // Make it findable immediately, but only file it into the typed lists on flush.
        // That lets a command create something and refer to it in the same tick
        // without breaking iterations already running over the lists.
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

    /// <summary>Applies every buffered change. Called at the end of each tick.</summary>
    public void Flush()
    {
        // Remove first, then add: an entity created in this same tick
        // must not be caught by an older pending removal.
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
            // A copy, so that EntityAdded handlers may spawn in turn.
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

    /// <summary>Stores the pre-tick state of every entity — the basis of view interpolation.</summary>
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
