using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Mouse control: selecting units and giving them orders.
/// </summary>
/// <remarks>
/// Hit testing is purely arithmetic, via ray-sphere intersection and projection into
/// screen space — deliberately without physics bodies. In an RTS with hundreds of
/// units, collision shapes that exist only to be clicked would be
/// wasted computation.
///
/// Everything is issued through the command queue, exactly as the AI does it.
/// </remarks>
public sealed partial class SelectionController : Node
{
    /// <summary>Beyond this drag distance in pixels it counts as a box selection rather than a click.</summary>
    private const float DragThreshold = 8f;

    /// <summary>Generosity on a single click: units are small and the cursor is imprecise.</summary>
    private const float PickPadding = 0.45f;

    private static readonly Color MoveMarker = new(0.4f, 1f, 0.5f);
    private static readonly Color AttackMarker = new(1f, 0.35f, 0.3f);
    private static readonly Color WorkMarker = new(1f, 0.85f, 0.35f);

    private readonly List<EntityId> _selection = new();
    private readonly HashSet<int> _selectionLookup = new();
    private readonly List<EntityId>[] _controlGroups = new List<EntityId>[10];

    private SimulationWorld? _world;
    private RtsCamera? _camera;
    private ViewManager? _views;
    private SelectionBox? _box;
    private BuildPlacementController? _placement;

    private int _localPlayerId = 1;
    private Vector2 _dragStart;
    private bool _dragging;

    public IReadOnlyList<EntityId> Selection => _selection;

    public event System.Action? SelectionChanged;

    public SelectionController()
    {
        for (int i = 0; i < _controlGroups.Length; i++) _controlGroups[i] = new List<EntityId>();
    }

    public void Attach(SimulationWorld world, RtsCamera camera, ViewManager views, int localPlayerId)
    {
        _world = world;
        _camera = camera;
        _views = views;
        _localPlayerId = localPlayerId;

        _box = new SelectionBox();
        AddChild(_box);

        world.Events.EntityRemoved += entity => Deselect(entity.Id);
    }

    public void SetPlacement(BuildPlacementController placement) => _placement = placement;

    /// <summary>The selected units that can build — for the build order.</summary>
    public EntityId[] SelectedBuilders()
    {
        var builders = new List<EntityId>();

        foreach (EntityId id in _selection)
        {
            if (_world?.Entities.GetUnit(id) is { CanBuild: true }) builders.Add(id);
        }
        return builders.ToArray();
    }

    /// <summary>The first selected building you own — the target for training and advancing.</summary>
    public Building? SelectedBuilding()
    {
        foreach (EntityId id in _selection)
        {
            if (_world?.Entities.GetBuilding(id) is { } building && building.OwnerId == _localPlayerId)
            {
                return building;
            }
        }
        return null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_world is null || _camera is null) return;

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
                // In building placement mode the left click places the site.
                if (_placement is { IsPlacing: true } && _placement.TryPlace(press.ShiftPressed)) return;
                BeginDrag(press.Position);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false, DoubleClick: true } release:
                SelectSameTypeOnScreen(release.Position);
                EndDrag();
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                CompleteDrag(release.Position, release.ShiftPressed);
                break;

            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } click:
                if (_placement is { IsPlacing: true })
                {
                    _placement.Cancel();
                    return;
                }
                IssueContextCommand(click.Position, click.ShiftPressed);
                break;

            case InputEventMouseMotion motion when _dragging:
                _box?.UpdateRect(_dragStart, motion.Position);
                break;

            case InputEventKey { Pressed: true, Echo: false } key:
                HandleKey(key);
                break;
        }
    }

    // --- Selection -------------------------------------------------------

    private void BeginDrag(Vector2 position)
    {
        _dragging = true;
        _dragStart = position;
        _box?.UpdateRect(position, position);
    }

    private void EndDrag()
    {
        _dragging = false;
        _box?.Hide();
    }

    private void CompleteDrag(Vector2 position, bool additive)
    {
        if (!_dragging) return;

        bool wasBox = _dragStart.DistanceTo(position) >= DragThreshold;
        EndDrag();

        if (!additive) ClearSelection();

        if (wasBox) SelectInRect(new Rect2(_dragStart, position - _dragStart).Abs());
        else SelectAt(position, additive);

        RaiseChanged();
    }

    /// <summary>Single click: nearest entity of your own along the mouse ray.</summary>
    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        Entity? best = PickEntity(screenPosition, ownedOnly: true);
        if (best is null) return;

        if (additive && _selectionLookup.Contains(best.Id.Value)) Deselect(best.Id);
        else Select(best.Id);
    }

    /// <summary>Box selection: everything whose screen position lies inside the rectangle.</summary>
    private void SelectInRect(Rect2 rect)
    {
        if (_world is null || _camera is null) return;

        // Military takes precedence: whoever boxes an army does not want to send along
        // the settlers who happen to be standing nearby.
        var military = new List<EntityId>();
        var civilian = new List<EntityId>();

        foreach (Unit unit in _world.Entities.Units)
        {
            if (unit.OwnerId != _localPlayerId) continue;

            Vector3 world = ToWorld3D(unit.Position, unit);
            if (_camera.Camera.IsPositionBehind(world)) continue;
            if (!rect.HasPoint(_camera.Camera.UnprojectPosition(world))) continue;

            if (unit.AttackDamage > 0f && !unit.CanGather) military.Add(unit.Id);
            else civilian.Add(unit.Id);
        }

        foreach (EntityId id in military.Count > 0 ? military : civilian) Select(id);
    }

    /// <summary>Double click: every visible unit of the same type.</summary>
    private void SelectSameTypeOnScreen(Vector2 screenPosition)
    {
        if (_world is null || _camera is null) return;

        ClearSelection();
        SelectAt(screenPosition, additive: false);
        if (_selection.Count == 0) return;

        Entity? reference = _world.Entities.Get(_selection[0]);
        if (reference is null) return;

        Rect2 screen = new(Vector2.Zero, GetViewport().GetVisibleRect().Size);

        foreach (Unit unit in _world.Entities.Units)
        {
            if (unit.OwnerId != _localPlayerId || unit.DefinitionId != reference.DefinitionId) continue;

            Vector3 world = ToWorld3D(unit.Position, unit);
            if (_camera.Camera.IsPositionBehind(world)) continue;

            if (screen.HasPoint(_camera.Camera.UnprojectPosition(world))) Select(unit.Id);
        }

        RaiseChanged();
    }

    private void Select(EntityId id)
    {
        if (!_selectionLookup.Add(id.Value)) return;
        _selection.Add(id);
        _views?.SetSelected(id, true);
    }

    private void Deselect(EntityId id)
    {
        if (!_selectionLookup.Remove(id.Value)) return;
        _selection.Remove(id);
        _views?.SetSelected(id, false);
    }

    private void ClearSelection()
    {
        foreach (EntityId id in _selection) _views?.SetSelected(id, false);
        _selection.Clear();
        _selectionLookup.Clear();
    }

    private void RaiseChanged() => SelectionChanged?.Invoke();

    // --- Orders ----------------------------------------------------------

    /// <summary>
    /// Rechtsklick. Was passiert, haengt davon ab, worauf geklickt wurde:
    /// attack an enemy, harvest a deposit, build your own site, otherwise walk there.
    /// </summary>
    private void IssueContextCommand(Vector2 screenPosition, bool queued)
    {
        if (_world is null || _selection.Count == 0) return;

        EntityId[] units = _selection.ToArray();
        Entity? hit = PickEntity(screenPosition, ownedOnly: false);

        switch (hit)
        {
            case ResourceNode node:
                _world.Commands.Enqueue(new GatherCommand
                {
                    PlayerId = _localPlayerId, Units = units, Node = node.Id,
                });
                _views?.FlashCommandMarker(node.Position, WorkMarker);
                return;

            case Building { IsUnderConstruction: true } site when site.OwnerId == _localPlayerId:
                _world.Commands.Enqueue(new RepairOrBuildCommand
                {
                    PlayerId = _localPlayerId, Units = units, Site = site.Id,
                });
                _views?.FlashCommandMarker(site.Position, WorkMarker);
                return;

            case not null when hit.OwnerId != _localPlayerId && hit.OwnerId != 0:
                _world.Commands.Enqueue(new AttackCommand
                {
                    PlayerId = _localPlayerId, Units = units, Target = hit.Id,
                });
                _views?.FlashCommandMarker(hit.Position, AttackMarker);
                return;
        }

        if (!GroundPicker.TryPick(_world.Nav, _camera!.Camera, screenPosition, out Vector2 target)) return;

        _world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = _localPlayerId,
            Units = units,
            Target = target,
            Queued = queued,
        });

        _views?.FlashCommandMarker(target, MoveMarker);
    }

    private void HandleKey(InputEventKey key)
    {
        switch (key.Keycode)
        {
            case Key.S:
                SendToSelection(new StopCommand { PlayerId = _localPlayerId, Units = _selection.ToArray() });
                return;

            case Key.H:
                SendToSelection(new SetStanceCommand
                {
                    PlayerId = _localPlayerId, Units = _selection.ToArray(), Stance = Stance.HoldPosition,
                });
                return;

            case Key.D:
                SendToSelection(new SetStanceCommand
                {
                    PlayerId = _localPlayerId, Units = _selection.ToArray(), Stance = Stance.Defensive,
                });
                return;

            case Key.A when !key.CtrlPressed:
                BeginAttackMove();
                return;
        }

        int group = DigitToGroup(key.Keycode);
        if (group < 0) return;

        if (key.CtrlPressed) AssignControlGroup(group);
        else RecallControlGroup(group);
    }

    /// <summary>Attack move to the current mouse position.</summary>
    private void BeginAttackMove()
    {
        if (_world is null || _camera is null || _selection.Count == 0) return;

        Vector2 mouse = GetViewport().GetMousePosition();
        if (!GroundPicker.TryPick(_world.Nav, _camera.Camera, mouse, out Vector2 target)) return;

        _world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = _localPlayerId,
            Units = _selection.ToArray(),
            Target = target,
            Attacking = true,
        });

        _views?.FlashCommandMarker(target, AttackMarker);
    }

    private void SendToSelection(ICommand command)
    {
        if (_world is null || _selection.Count == 0) return;
        _world.Commands.Enqueue(command);
    }

    private void AssignControlGroup(int group)
    {
        _controlGroups[group].Clear();
        _controlGroups[group].AddRange(_selection);
    }

    private void RecallControlGroup(int group)
    {
        if (_world is null || _controlGroups[group].Count == 0) return;

        ClearSelection();
        foreach (EntityId id in _controlGroups[group])
        {
            if (_world.Entities.Exists(id)) Select(id);
        }
        RaiseChanged();
    }

    private static int DigitToGroup(Key keycode) => keycode switch
    {
        >= Key.Key0 and <= Key.Key9 => (int)(keycode - Key.Key0),
        _ => -1,
    };

    // --- Geometry --------------------------------------------------------

    /// <summary>Nearest entity along the mouse ray.</summary>
    private Entity? PickEntity(Vector2 screenPosition, bool ownedOnly)
    {
        if (_world is null || _camera is null) return null;

        Vector3 origin = _camera.Camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = _camera.Camera.ProjectRayNormal(screenPosition);

        Entity? best = null;
        float bestDistance = float.MaxValue;

        foreach (Entity entity in _world.Entities.All())
        {
            if (ownedOnly && entity.OwnerId != _localPlayerId) continue;

            (Vector3 centre, float radius) = PickSphere(entity);
            if (!RayHitsSphere(origin, direction, centre, radius + PickPadding, out float distance)) continue;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = entity;
        }

        return best;
    }

    private Vector3 ToWorld3D(Vector2 planar, Entity entity)
    {
        float ground = _world?.Nav.SampleHeight(planar) ?? 0f;
        float lift = entity is Building ? 1.5f : 0.9f;
        return new Vector3(planar.X, ground + lift, planar.Y);
    }

    private (Vector3 Centre, float Radius) PickSphere(Entity entity) => entity switch
    {
        Building building => (ToWorld3D(building.Position, building), building.FootprintRadius * 0.9f),
        Unit unit => (ToWorld3D(unit.Position, unit), Mathf.Max(unit.Radius, 0.6f)),
        ResourceNode => (ToWorld3D(entity.Position, entity), 1.1f),
        _ => (ToWorld3D(entity.Position, entity), 0.6f),
    };

    private static bool RayHitsSphere(Vector3 origin, Vector3 direction, Vector3 centre, float radius,
        out float distance)
    {
        distance = 0f;

        Vector3 toCentre = centre - origin;
        float projection = toCentre.Dot(direction);
        if (projection < 0f) return false;

        float perpendicularSquared = toCentre.LengthSquared() - projection * projection;
        if (perpendicularSquared > radius * radius) return false;

        distance = projection - Mathf.Sqrt(radius * radius - perpendicularSquared);
        return true;
    }
}
