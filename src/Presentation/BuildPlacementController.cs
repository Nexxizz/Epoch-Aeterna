using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// The building placement mode: a ghost model follows the cursor and turns green
/// or red depending on whether building there is allowed.
/// </summary>
/// <remarks>
/// The validity check calls the very same method the command does
/// (<see cref="BuildPlacement.IsValid"/>). That way the preview cannot lie —
/// whatever glows green really does get built.
/// </remarks>
public sealed partial class BuildPlacementController : Node3D
{
    private static readonly Color ValidColor = new(0.35f, 0.9f, 0.4f, 0.45f);
    private static readonly Color InvalidColor = new(0.9f, 0.3f, 0.25f, 0.45f);

    private readonly MeshInstance3D _ghost = new();
    private StandardMaterial3D? _material;

    private SimulationWorld? _world;
    private RtsCamera? _camera;
    private SelectionController? _selection;
    private int _localPlayerId = 1;

    private BuildingDefinition? _pending;
    private Vector2 _position;
    private bool _valid;

    public bool IsPlacing => _pending is not null;

    /// <summary>Raised when the mode starts or ends — the HUD shows a hint accordingly.</summary>
    public event System.Action<BuildingDefinition?>? PlacementChanged;

    public void Attach(SimulationWorld world, RtsCamera camera, SelectionController selection, int localPlayerId)
    {
        _world = world;
        _camera = camera;
        _selection = selection;
        _localPlayerId = localPlayerId;

        _material = new StandardMaterial3D
        {
            AlbedoColor = ValidColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _ghost.Name = "BuildGhost";
        _ghost.MaterialOverride = _material;
        _ghost.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _ghost.Visible = false;
        AddChild(_ghost);
    }

    /// <summary>Starts placement mode for a building.</summary>
    public void Begin(string buildingDefinitionId)
    {
        BuildingDefinition? definition = _world?.Definitions.GetBuilding(buildingDefinitionId);
        Player? player = _world?.GetPlayer(_localPlayerId);

        if (definition is null || player is null) return;

        // What the age does not offer yet cannot even be picked up.
        if (player.AgeIndex < definition.RequiredAgeIndex) return;

        _pending = definition;

        float footprint = Mathf.Max(definition.Footprint.X, definition.Footprint.Y) * NavGrid.CellSize * 0.85f;
        _ghost.Mesh = new BoxMesh { Size = new Vector3(footprint, 3f, footprint) };
        _ghost.Visible = true;

        PlacementChanged?.Invoke(_pending);
    }

    public void Cancel()
    {
        if (_pending is null) return;

        _pending = null;
        _ghost.Visible = false;
        PlacementChanged?.Invoke(null);
    }

    public override void _Process(double delta)
    {
        if (_pending is null || _world is null || _camera is null) return;

        if (!GroundPicker.TryPick(_world.Nav, _camera.Camera, GetViewport().GetMousePosition(), out Vector2 target))
        {
            return;
        }

        // Snap to the tile grid so buildings stand flush beside one another.
        Vector2I cell = _world.Nav.ClampCell(_world.Nav.WorldToCell(target));
        _position = _world.Nav.CellToWorld(cell.X, cell.Y);

        Player? player = _world.GetPlayer(_localPlayerId);
        _valid = player is not null &&
                 player.CanAfford(_pending.Cost) &&
                 BuildPlacement.IsValid(_world, _pending, _position, player);

        _ghost.Position = new Vector3(_position.X, _world.Nav.SampleHeight(_position) + 1.5f, _position.Y);
        if (_material is not null) _material.AlbedoColor = _valid ? ValidColor : InvalidColor;
    }

    /// <summary>
    /// Places the construction site. Returns false when the click did nothing —
    /// the caller then lets it through as an ordinary click.
    /// </summary>
    public bool TryPlace(bool keepPlacing)
    {
        if (_pending is null || _world is null || !_valid) return false;

        _world.Commands.Enqueue(new PlaceBuildingCommand
        {
            PlayerId = _localPlayerId,
            BuildingDefinitionId = _pending.Id,
            Position = _position,
            Builders = _selection?.SelectedBuilders() ?? System.Array.Empty<EntityId>(),
        });

        // Shift held: place the next one straight away, as one expects from walls.
        if (!keepPlacing) Cancel();
        return true;
    }
}

/// <summary>Intersection of the mouse ray with the terrain. Used by selection and build preview.</summary>
public static class GroundPicker
{
    public static bool TryPick(NavGrid grid, Camera3D camera, Vector2 screenPosition, out Vector2 target)
    {
        target = Vector2.Zero;

        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);

        if (Mathf.Abs(direction.Y) < 0.0001f) return false;

        // First estimate: intersection with the plane y=0. Then a few steps of
        // correction onto the actual terrain height. Converges within a few passes
        // at the gentle heights of this map.
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
}
