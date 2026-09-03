using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Keeps the visible world in step with the simulation: creates an
/// <see cref="EntityView"/> on every spawn and clears it away on death.
/// </summary>
/// <remarks>
/// As long as no Blender models exist for an entity, the manager produces
/// placeholder geometry. As soon as a definition has a <c>ModelScene</c>, that gets
/// instantiated instead — without any change to this code.
/// </remarks>
public sealed partial class ViewManager : Node3D
{
    /// <summary>How long the target marker is shown after a right click, in seconds.</summary>
    private const float MarkerLifetime = 0.6f;

    private readonly Dictionary<int, EntityView> _views = new();
    private readonly Dictionary<int, StandardMaterial3D> _playerMaterials = new();

    private SimulationWorld? _world;
    private SimulationRunner? _runner;

    private MeshInstance3D? _commandMarker;
    private float _markerTimeLeft;

    /// <summary>Every living view with its entity — the fog needs both.</summary>
    public IEnumerable<(Entity Entity, EntityView View)> Views
    {
        get
        {
            foreach (EntityView view in _views.Values)
            {
                if (view.Entity is { } entity) yield return (entity, view);
            }
        }
    }

    public void Attach(SimulationWorld world, SimulationRunner runner)
    {
        _world = world;
        _runner = runner;

        world.Events.EntitySpawned += OnEntitySpawned;
        world.Events.EntityRemoved += OnEntityRemoved;
        world.Events.ConstructionStageChanged += OnConstructionChanged;
        world.Events.ConstructionCompleted += OnConstructionChanged;

        // Pick up anything that already existed before subscribing.
        foreach (Entity entity in world.Entities.All()) OnEntitySpawned(entity);
    }

    public override void _ExitTree()
    {
        if (_world is null) return;
        _world.Events.EntitySpawned -= OnEntitySpawned;
        _world.Events.EntityRemoved -= OnEntityRemoved;
        _world.Events.ConstructionStageChanged -= OnConstructionChanged;
        _world.Events.ConstructionCompleted -= OnConstructionChanged;
    }

    public override void _Process(double delta)
    {
        if (_markerTimeLeft <= 0f || _commandMarker is null) return;

        _markerTimeLeft -= (float)delta;

        // Fade out while expanding — brief, unobtrusive feedback.
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

    /// <summary>Briefly shows where the last order went.</summary>
    public void FlashCommandMarker(Vector2 target, Color color)
    {
        if (_world is null) return;

        _commandMarker ??= CreateCommandMarker();
        _commandMarker.Position = new Vector3(target.X, _world.Nav.SampleHeight(target) + 0.15f, target.Y);
        _markerTimeLeft = MarkerLifetime;

        if (_commandMarker.MaterialOverride is StandardMaterial3D material) material.AlbedoColor = color;
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

    // --- Lifecycle -------------------------------------------------------

    private void OnEntitySpawned(Entity entity)
    {
        if (_runner is null || _world is null || _views.ContainsKey(entity.Id.Value)) return;

        var view = new EntityView { Name = $"View_{entity.Id.Value}_{entity.DefinitionId}" };
        AddChild(view);
        view.Bind(entity, _runner, _world.Nav, BuildModel(entity), _world.Random);

        _views[entity.Id.Value] = view;
    }

    private void OnEntityRemoved(Entity entity)
    {
        if (!_views.Remove(entity.Id.Value, out EntityView? view)) return;
        view.QueueFree();
    }

    /// <summary>Stage reached or finished — the model is swapped.</summary>
    private void OnConstructionChanged(Building building)
    {
        if (_world is null) return;
        if (_views.TryGetValue(building.Id.Value, out EntityView? view))
        {
            view.ReplaceModel(BuildModel(building), _world.Random);
        }
    }

    // --- Models ----------------------------------------------------------

    /// <summary>The finished model from the definition, otherwise a placeholder.</summary>
    private Node3D BuildModel(Entity entity)
    {
        EntityDefinition? definition = _world?.Definitions.GetEntity(entity.DefinitionId);

        if (definition?.ModelScene is not null &&
            entity is not Building { IsUnderConstruction: true } &&
            definition.ModelScene.Instantiate() is Node3D model)
        {
            ApplyTeamColor(model, entity.OwnerId);
            return model;
        }

        return BuildPlaceholder(entity, definition);
    }

    private Node3D BuildPlaceholder(Entity entity, EntityDefinition? definition) => entity switch
    {
        Building building => BuildBuildingPlaceholder(building),
        ResourceNode node => BuildResourcePlaceholder(node),
        _ => BuildUnitPlaceholder(entity, definition),
    };

    /// <summary>
    /// Buildings as boxes. Construction sites grow visibly in three stages — first a
    /// flat foundation, then the shell, then the finished house.
    /// </summary>
    private Node3D BuildBuildingPlaceholder(Building building)
    {
        float footprint = building.FootprintRadius * 1.7f;
        float fullHeight = Mathf.Clamp(footprint * 0.75f, 2.5f, 6f);

        float heightFactor = building.IsUnderConstruction
            ? building.ConstructionStage switch { 0 => 0.15f, 1 => 0.55f, _ => 0.85f }
            : 1f;

        var size = new Vector3(footprint, fullHeight * heightFactor, footprint);

        var instance = new MeshInstance3D
        {
            Name = "Placeholder",
            Mesh = new BoxMesh { Size = size },
            Position = new Vector3(0f, size.Y * 0.5f, 0f),
        };

        instance.MaterialOverride = building.IsUnderConstruction
            ? ConstructionMaterial()
            : GetPlayerMaterial(building.OwnerId);

        return instance;
    }

    private static Node3D BuildResourcePlaceholder(ResourceNode node)
    {
        // The tree shrinks visibly as it is felled.
        float wear = Mathf.Lerp(0.45f, 1f, node.RemainingFraction);

        (Mesh mesh, Color color, float lift) = node.Resource switch
        {
            ResourceType.Wood => (
                new CylinderMesh { TopRadius = 0.05f, BottomRadius = 1.4f, Height = 6f * wear, RadialSegments = 6, Rings = 1 },
                new Color(0.16f, 0.31f, 0.15f), 3f * wear),

            ResourceType.Stone => (
                new SphereMesh { Radius = 1.1f, Height = 1.7f, RadialSegments = 6, Rings = 3 },
                new Color(0.46f, 0.45f, 0.43f), 0.55f),

            ResourceType.Gold => (
                new SphereMesh { Radius = 1.0f, Height = 1.5f, RadialSegments = 6, Rings = 3 },
                new Color(0.78f, 0.63f, 0.18f), 0.5f),

            _ => (
                (Mesh)new SphereMesh { Radius = 0.65f, Height = 1.0f, RadialSegments = 6, Rings = 3 },
                new Color(0.45f, 0.18f, 0.28f), 0.45f),
        };

        return new MeshInstance3D
        {
            Name = "Placeholder",
            Mesh = mesh,
            Position = new Vector3(0f, lift, 0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f },
        };
    }

    private Node3D BuildUnitPlaceholder(Entity entity, EntityDefinition? definition)
    {
        float radius = (definition as UnitDefinition)?.Radius ?? 0.4f;
        const float height = 1.8f;

        var instance = new MeshInstance3D
        {
            Name = "Placeholder",
            Mesh = new CapsuleMesh { Radius = radius, Height = height },
            Position = new Vector3(0f, height * 0.5f, 0f),
            MaterialOverride = GetPlayerMaterial(entity.OwnerId),
        };

        // A small wedge marks the facing while there is no animation.
        instance.AddChild(new MeshInstance3D
        {
            Name = "Facing",
            Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.5f) },
            Position = new Vector3(0f, height * 0.65f, -radius - 0.25f),
        });

        return instance;
    }

    /// <summary>
    /// Recolours the parts of an imported model that carry the team-colour material.
    /// </summary>
    /// <remarks>
    /// The Blender pipeline marks those surfaces by material name
    /// (<c>MAT_teamcolor</c>) rather than by mesh name or slot index. A name
    /// survives geometry edits; a slot index does not.
    /// </remarks>
    private void ApplyTeamColor(Node node, int ownerId)
    {
        if (node is MeshInstance3D instance && instance.Mesh is not null)
        {
            StandardMaterial3D playerMaterial = GetPlayerMaterial(ownerId);

            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                Material? material = instance.Mesh.SurfaceGetMaterial(surface);
                if (material is null) continue;

                if (material.ResourceName.Contains("teamcolor", System.StringComparison.OrdinalIgnoreCase))
                {
                    instance.SetSurfaceOverrideMaterial(surface, playerMaterial);
                }
            }
        }

        foreach (Node child in node.GetChildren()) ApplyTeamColor(child, ownerId);
    }

    private static StandardMaterial3D ConstructionMaterial() => new()
    {
        AlbedoColor = new Color(0.62f, 0.52f, 0.34f),
        Roughness = 0.95f,
    };

    /// <summary>
    /// One material per player, shared across all their entities — it saves draw call
    /// state changes and brings the faction colour forward already.
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
