using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Maussteuerung: Einheiten auswaehlen und ihnen Befehle geben.
/// </summary>
/// <remarks>
/// Die Trefferpruefung laeuft rein rechnerisch ueber Strahl-Kugel-Schnitte und
/// Projektion in den Bildschirmraum — bewusst ohne Physik-Koerper. Fuer eine RTS mit
/// hunderten Einheiten waeren Kollisionsformen, die nur zum Anklicken existieren,
/// verschwendete Rechenzeit.
///
/// Ausgegeben wird ausschliesslich ueber die Befehlsqueue, genau wie bei der KI.
/// </remarks>
public sealed partial class SelectionController : Node
{
    /// <summary>Ab dieser Ziehstrecke in Pixeln gilt es als Rahmenauswahl statt als Klick.</summary>
    private const float DragThreshold = 8f;

    /// <summary>Grosszuegigkeit beim Einzelklick: Einheiten sind klein, der Cursor ungenau.</summary>
    private const float PickPadding = 0.45f;

    private readonly List<EntityId> _selection = new();
    private readonly HashSet<int> _selectionLookup = new();

    private SimulationWorld? _world;
    private RtsCamera? _camera;
    private ViewManager? _views;
    private SelectionBox? _box;

    private int _localPlayerId = 1;
    private Vector2 _dragStart;
    private bool _dragging;

    /// <summary>Kontrollgruppen 0–9.</summary>
    private readonly List<EntityId>[] _controlGroups = new List<EntityId>[10];

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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_world is null || _camera is null) return;

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
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

    // --- Auswahl ---------------------------------------------------------

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

        if (wasBox) SelectInRect(new Rect2(_dragStart, position - _dragStart).Abs(), additive);
        else SelectAt(position, additive);

        RaiseChanged();
    }

    /// <summary>Einzelklick: naechste Einheit entlang des Mausstrahls.</summary>
    private void SelectAt(Vector2 screenPosition, bool additive)
    {
        if (_world is null || _camera is null) return;

        Vector3 origin = _camera.Camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = _camera.Camera.ProjectRayNormal(screenPosition);

        Entity? best = null;
        float bestDistance = float.MaxValue;

        foreach (Entity entity in _world.Entities.All())
        {
            if (entity.OwnerId != _localPlayerId) continue;

            (Vector3 centre, float radius) = PickSphere(entity);
            if (!RayHitsSphere(origin, direction, centre, radius + PickPadding, out float distance)) continue;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            best = entity;
        }

        if (best is null) return;

        if (additive && _selectionLookup.Contains(best.Id.Value)) Deselect(best.Id);
        else Select(best.Id);
    }

    /// <summary>Rahmenauswahl: alles, dessen Bildschirmposition im Rechteck liegt.</summary>
    private void SelectInRect(Rect2 rect, bool additive)
    {
        if (_world is null || _camera is null) return;

        // Militaer haette Vorrang vor Zivilisten — im MVP gibt es noch kein Militaer,
        // daher werden vorerst alle eigenen Einheiten aufgenommen.
        foreach (Unit unit in _world.Entities.Units)
        {
            if (unit.OwnerId != _localPlayerId) continue;

            Vector3 world = ToWorld3D(unit.Position, unit);
            if (_camera.Camera.IsPositionBehind(world)) continue;

            if (rect.HasPoint(_camera.Camera.UnprojectPosition(world))) Select(unit.Id);
        }
    }

    /// <summary>Doppelklick: alle sichtbaren Einheiten desselben Typs.</summary>
    private void SelectSameTypeOnScreen(Vector2 screenPosition)
    {
        if (_world is null || _camera is null) return;

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

    // --- Befehle ---------------------------------------------------------

    private void IssueContextCommand(Vector2 screenPosition, bool queued)
    {
        if (_world is null || _selection.Count == 0) return;
        if (!TryPickGround(screenPosition, out Vector2 target)) return;

        // Kontextsensitiv: Auf Boden geklickt heisst bewegen. Angriff und Sammeln
        // kommen dazu, sobald es Gegner und Ressourcenknoten gibt (Phase 3).
        _world.Commands.Enqueue(new MoveCommand
        {
            PlayerId = _localPlayerId,
            Units = _selection.ToArray(),
            Target = target,
            Queued = queued,
        });

        _views?.FlashCommandMarker(target);
    }

    private void HandleKey(InputEventKey key)
    {
        if (key.Keycode == Key.S)
        {
            StopSelection();
            return;
        }

        int group = DigitToGroup(key.Keycode);
        if (group < 0) return;

        if (key.CtrlPressed) AssignControlGroup(group);
        else RecallControlGroup(group);
    }

    private void StopSelection()
    {
        if (_world is null || _selection.Count == 0) return;

        _world.Commands.Enqueue(new StopCommand
        {
            PlayerId = _localPlayerId,
            Units = _selection.ToArray(),
        });
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

    // --- Geometrie -------------------------------------------------------

    /// <summary>Schnittpunkt des Mausstrahls mit dem Gelaende, iterativ angenaehert.</summary>
    private bool TryPickGround(Vector2 screenPosition, out Vector2 target)
    {
        target = Vector2.Zero;
        if (_world is null || _camera is null) return false;

        Vector3 origin = _camera.Camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = _camera.Camera.ProjectRayNormal(screenPosition);

        if (Mathf.Abs(direction.Y) < 0.0001f) return false;

        NavGrid grid = _world.Nav;

        // Erster Schaetzwert: Schnitt mit der Ebene y=0. Danach ein paar Schritte
        // Nachfuehrung auf die tatsaechliche Gelaendehoehe. Konvergiert bei den
        // flachen Hoehen dieser Karte in wenigen Durchlaeufen.
        float distance = -origin.Y / direction.Y;
        if (distance <= 0f) return false;

        for (int i = 0; i < 6; i++)
        {
            Vector3 point = origin + direction * distance;
            float ground = grid.SampleHeight(new Vector2(point.X, point.Z));
            float error = point.Y - ground;

            if (Mathf.Abs(error) < 0.05f) break;
            distance += error / -direction.Y;
            if (distance <= 0f) return false;
        }

        Vector3 hit = origin + direction * distance;
        target = new Vector2(hit.X, hit.Z);
        return true;
    }

    private Vector3 ToWorld3D(Vector2 planar, Entity entity)
    {
        float ground = _world?.Nav.SampleHeight(planar) ?? 0f;
        float lift = entity is Building ? 1.5f : 0.9f;
        return new Vector3(planar.X, ground + lift, planar.Y);
    }

    private (Vector3 Centre, float Radius) PickSphere(Entity entity) => entity switch
    {
        Building building => (ToWorld3D(building.Position, building),
            Mathf.Max(building.Footprint.X, building.Footprint.Y) * 0.5f * NavGrid.CellSize * 0.6f),
        Unit unit => (ToWorld3D(unit.Position, unit), Mathf.Max(unit.Radius, 0.6f)),
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
