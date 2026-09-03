using Godot;
using EpochAeterna.Core.Pathfinding;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Draws every projectile in flight as a single <see cref="MultiMeshInstance3D"/>.
/// </summary>
/// <remarks>
/// The sim computes projectiles flat on the XZ plane. The arc appears only here in
/// the display: it is purely visual and must not influence hit calculation —
/// otherwise damage would depend on the frame rate.
/// </remarks>
public sealed partial class ProjectileRenderer : Node3D
{
    /// <summary>Upper bound on projectiles drawn at once.</summary>
    private const int Capacity = 512;

    /// <summary>Peak height of the trajectory, relative to the throwing distance.</summary>
    private const float ArcFactor = 0.12f;

    private readonly MultiMeshInstance3D _instance = new();
    private MultiMesh? _multiMesh;

    private SimulationWorld? _world;
    private NavGrid? _grid;

    public void Attach(SimulationWorld world, NavGrid grid)
    {
        _world = world;
        _grid = grid;

        _multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new CapsuleMesh { Radius = 0.06f, Height = 0.55f, RadialSegments = 5, Rings = 1 },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
        };

        _instance.Name = "Projectiles";
        _instance.Multimesh = _multiMesh;
        _instance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        _instance.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.86f, 0.62f),
            Roughness = 0.8f,
        };

        AddChild(_instance);
    }

    public override void _Process(double delta)
    {
        if (_world is null || _grid is null || _multiMesh is null) return;

        int count = Mathf.Min(_world.Projectiles.Count, Capacity);
        _multiMesh.VisibleInstanceCount = count;

        for (int i = 0; i < count; i++)
        {
            Projectile projectile = _world.Projectiles[i];

            Vector2 planar = projectile.Position;
            float ground = _grid.SampleHeight(planar);

            // Parabola: zero at both ends, maximum in the middle.
            float t = projectile.FlightProgress;
            float arc = 4f * t * (1f - t) * projectile.TotalDistance * ArcFactor;

            var position = new Vector3(planar.X, ground + 1.1f + arc, planar.Y);

            // Tilt along the direction of flight, so the arrow does not drift sideways.
            Vector2 heading = projectile.TargetPosition - planar;
            float yaw = heading.LengthSquared() > 0.0001f ? Mathf.Atan2(heading.X, heading.Y) : 0f;
            float pitch = Mathf.Lerp(-0.6f, 0.6f, t);

            var basis = Basis.FromEuler(new Vector3(pitch, yaw, 0f));
            _multiMesh.SetInstanceTransform(i, new Transform3D(basis, position));
        }
    }
}
