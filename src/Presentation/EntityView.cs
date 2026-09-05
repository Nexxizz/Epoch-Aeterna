using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Visible representation of an entity. Holds a reference to the simulation object
/// but never writes it — the data flows strictly from sim to view.
/// </summary>
public sealed partial class EntityView : Node3D
{
    /// <summary>Above this much damage the health bar is shown.</summary>
    private const float HealthBarThreshold = 0.995f;

    private Entity? _entity;
    private SimulationRunner? _runner;
    private NavGrid? _grid;

    private Node3D? _model;
    private ModelAnimator? _animator;
    private MeshInstance3D? _selectionRing;
    private HealthBar? _healthBar;
    private bool _dying;
    private double _corpseTimeLeft;
    private const double CorpseHoldSeconds = 3.0;
    private const double CorpseSinkSeconds = 0.8;

    public EntityId EntityId => _entity?.Id ?? EntityId.None;

    public Entity? Entity => _entity;

    public void Bind(Entity entity, SimulationRunner runner, NavGrid grid, Node3D model,
        RandomNumberGenerator random)
    {
        _entity = entity;
        _runner = runner;
        _grid = grid;
        _model = model;
        AddChild(model);

        _animator = new ModelAnimator(model, random);
        SyncTransform(1f);
    }

    /// <summary>Swaps the model — used for construction stages and age variants.</summary>
    public void ReplaceModel(Node3D model, RandomNumberGenerator random)
    {
        _model?.QueueFree();
        _model = model;
        AddChild(model);

        _animator = new ModelAnimator(model, random);
    }

    public override void _Process(double delta)
    {
        if (_entity is null || _runner is null) return;
        _animator?.SetTimeScale(_runner.TimeScale);

        if (_dying)
        {
            double elapsed = delta * _runner.TimeScale;
            _corpseTimeLeft -= elapsed;
            if (_corpseTimeLeft <= CorpseSinkSeconds && _model is not null)
                _model.Position -= Vector3.Up * (float)(elapsed * 0.45 / CorpseSinkSeconds);
            if (_corpseTimeLeft <= 0) QueueFree();
            return;
        }

        SyncTransform(_runner.IsPaused ? 1f : _runner.InterpolationAlpha);
        SyncHealthBar();

        if (_entity is Unit unit) _animator?.Sync(unit);
    }

    /// <summary>Keep only the visual corpse; simulation and selection remove it immediately.</summary>
    public bool BeginDeath()
    {
        if (_dying) return true;
        if (_entity is not Unit || _animator is null) return false;
        double duration = _animator.PlayDeath();
        if (duration <= 0) return false;
        _dying = true;
        _corpseTimeLeft = duration + CorpseHoldSeconds + CorpseSinkSeconds;
        SyncTransform(1f);
        SetSelected(false);
        if (_healthBar is not null) _healthBar.Visible = false;
        return true;
    }

    private void SyncTransform(float alpha)
    {
        if (_entity is null) return;

        Vector2 planar = _entity.PreviousPosition.Lerp(_entity.Position, alpha);
        float height = _grid?.SampleHeight(planar) ?? 0f;

        Position = new Vector3(planar.X, height, planar.Y);
        Rotation = new Vector3(0f, LerpAngle(_entity.PreviousRotation, _entity.Rotation, alpha), 0f);
    }

    // --- Health bar ------------------------------------------------------

    private void SyncHealthBar()
    {
        if (_entity is null) return;

        float fraction = _entity.HealthFraction;

        // Construction sites show build progress instead of health.
        bool underConstruction = _entity is Building { IsUnderConstruction: true };
        if (underConstruction) fraction = ((Building)_entity).ConstructionProgress;

        if (fraction >= HealthBarThreshold && !underConstruction)
        {
            if (_healthBar is not null) _healthBar.Visible = false;
            return;
        }

        _healthBar ??= CreateHealthBar();
        _healthBar.Visible = true;
        _healthBar.SetValue(fraction, underConstruction);
    }

    private HealthBar CreateHealthBar()
    {
        float height = _entity switch
        {
            Building building => building.FootprintRadius > 3f ? 5.5f : 4.2f,
            _ => 2.3f,
        };

        var bar = new HealthBar { Position = new Vector3(0f, height, 0f) };
        AddChild(bar);
        return bar;
    }

    // --- Selection ring ---------------------------------------------------

    /// <summary>Shows or hides the selection ring; it is created on first use.</summary>
    public void SetSelected(bool selected)
    {
        if (!selected)
        {
            if (_selectionRing is not null) _selectionRing.Visible = false;
            return;
        }

        _selectionRing ??= CreateSelectionRing();
        _selectionRing.Visible = true;
    }

    private MeshInstance3D CreateSelectionRing()
    {
        float radius = _entity switch
        {
            Building building => building.FootprintRadius + 0.4f,
            Unit unit => unit.Radius + 0.45f,
            _ => 0.9f,
        };

        var ring = new MeshInstance3D
        {
            Name = "SelectionRing",
            Mesh = new TorusMesh
            {
                InnerRadius = radius - 0.12f,
                OuterRadius = radius,
                RingSegments = 24,
                Rings = 4,
            },
            // Just above the ground, so the ring does not z-fight with the terrain.
            Position = new Vector3(0f, 0.12f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 1f, 0.45f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                // Always visible, even when the unit stands behind a hill.
                NoDepthTest = true,
                RenderPriority = 1,
            },
        };

        AddChild(ring);
        return ring;
    }

    /// <summary>Angle interpolation along the shorter way, so it does not jump at ±PI.</summary>
    private static float LerpAngle(float from, float to, float weight)
    {
        float difference = Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);
        return from + difference * weight;
    }
}
