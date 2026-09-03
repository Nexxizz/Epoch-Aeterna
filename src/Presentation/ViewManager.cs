using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Haelt die sichtbare Welt mit der Simulation im Gleichstand: legt bei jedem Spawn
/// eine <see cref="EntityView"/> an und raeumt sie beim Tod wieder ab.
/// </summary>
/// <remarks>
/// Solange keine Modelle aus Blender vorliegen (Phase 5), erzeugt der Manager
/// Platzhaltergeometrie. Sobald eine Definition eine <c>ModelScene</c> hat, wird
/// diese stattdessen instanziiert — ohne Aenderung an diesem Code.
/// </remarks>
public sealed partial class ViewManager : Node3D
{
    /// <summary>Anzeigedauer der Zielmarkierung nach einem Rechtsklick, in Sekunden.</summary>
    private const float MarkerLifetime = 0.6f;

    private readonly Dictionary<int, EntityView> _views = new();
    private readonly Dictionary<int, StandardMaterial3D> _playerMaterials = new();

    private SimulationWorld? _world;
    private SimulationRunner? _runner;

    private MeshInstance3D? _commandMarker;
    private float _markerTimeLeft;

    public void Attach(SimulationWorld world, SimulationRunner runner)
    {
        _world = world;
        _runner = runner;

        world.Events.EntitySpawned += OnEntitySpawned;
        world.Events.EntityRemoved += OnEntityRemoved;

        // Alles, was vor dem Anmelden schon existiert, nachtraeglich aufnehmen.
        foreach (Entity entity in world.Entities.All()) OnEntitySpawned(entity);
    }

    public override void _ExitTree()
    {
        if (_world is null) return;
        _world.Events.EntitySpawned -= OnEntitySpawned;
        _world.Events.EntityRemoved -= OnEntityRemoved;
    }

    public override void _Process(double delta)
    {
        if (_markerTimeLeft <= 0f || _commandMarker is null) return;

        _markerTimeLeft -= (float)delta;

        // Ausblenden und dabei aufziehen — kurzes, unaufdringliches Feedback.
        float t = Mathf.Clamp(_markerTimeLeft / MarkerLifetime, 0f, 1f);
        _commandMarker.Scale = Vector3.One * Mathf.Lerp(1.6f, 0.7f, t);
        _commandMarker.Visible = _markerTimeLeft > 0f;

        if (_commandMarker.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = material.AlbedoColor with { A = t };
        }
    }

    public void SetSelected(EntityId id, bool selected)
    {
        if (_views.TryGetValue(id.Value, out EntityView? view)) view.SetSelected(selected);
    }

    /// <summary>Zeigt kurz an, wohin der letzte Befehl ging.</summary>
    public void FlashCommandMarker(Vector2 target)
    {
        if (_world is null) return;

        _commandMarker ??= CreateCommandMarker();
        _commandMarker.Position = new Vector3(target.X, _world.Nav.SampleHeight(target) + 0.15f, target.Y);
        _markerTimeLeft = MarkerLifetime;
    }

    private MeshInstance3D CreateCommandMarker()
    {
        var marker = new MeshInstance3D
        {
            Name = "CommandMarker",
            Mesh = new TorusMesh { InnerRadius = 0.5f, OuterRadius = 0.7f, RingSegments = 20, Rings = 4 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.4f, 1f, 0.5f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            },
        };

        AddChild(marker);
        return marker;
    }

    private void OnEntitySpawned(Entity entity)
    {
        if (_runner is null || _world is null || _views.ContainsKey(entity.Id.Value)) return;

        var view = new EntityView { Name = $"View_{entity.Id.Value}_{entity.DefinitionId}" };
        AddChild(view);
        view.Bind(entity, _runner, _world.Nav, BuildModel(entity));

        _views[entity.Id.Value] = view;
    }

    private void OnEntityRemoved(Entity entity)
    {
        if (!_views.Remove(entity.Id.Value, out EntityView? view)) return;
        view.QueueFree();
    }

    /// <summary>Fertiges Modell aus der Definition, sonst Platzhalter.</summary>
    private Node3D BuildModel(Entity entity)
    {
        EntityDefinition? definition = _world?.Definitions.GetEntity(entity.DefinitionId);

        if (definition?.ModelScene is not null &&
            definition.ModelScene.Instantiate() is Node3D model)
        {
            return model;
        }

        return BuildPlaceholder(entity, definition);
    }

    private Node3D BuildPlaceholder(Entity entity, EntityDefinition? definition)
    {
        var instance = new MeshInstance3D { Name = "Placeholder" };

        if (entity is Building building)
        {
            var size = new Vector3(
                building.Footprint.X * NavGrid.CellSize * 0.85f,
                4f,
                building.Footprint.Y * NavGrid.CellSize * 0.85f);

            instance.Mesh = new BoxMesh { Size = size };
            instance.Position = new Vector3(0f, size.Y * 0.5f, 0f);
        }
        else
        {
            float radius = (definition as UnitDefinition)?.Radius ?? 0.4f;
            const float height = 1.8f;
            instance.Mesh = new CapsuleMesh { Radius = radius, Height = height };
            instance.Position = new Vector3(0f, height * 0.5f, 0f);

            // Kleiner Keil als Blickrichtungsmarkierung, solange es keine Animation gibt.
            var nose = new MeshInstance3D
            {
                Name = "Facing",
                Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.5f) },
                Position = new Vector3(0f, height * 0.65f, -radius - 0.25f),
            };
            instance.AddChild(nose);
        }

        instance.MaterialOverride = GetPlayerMaterial(entity.OwnerId);
        return instance;
    }

    /// <summary>
    /// Ein Material pro Spieler, geteilt ueber alle seine Entities — spart Draw-Call-Zustandswechsel
    /// und nimmt die Fraktionsfarbe schon jetzt vorweg (Phase 1.3).
    /// </summary>
    private StandardMaterial3D GetPlayerMaterial(int ownerId)
    {
        if (_playerMaterials.TryGetValue(ownerId, out StandardMaterial3D? cached)) return cached;

        Color color = _world?.GetPlayer(ownerId)?.Color ?? new Color(0.6f, 0.6f, 0.6f);
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.7f,
        };

        _playerMaterials[ownerId] = material;
        return material;
    }
}
