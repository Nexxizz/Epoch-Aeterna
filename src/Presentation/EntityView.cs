using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Sichtbare Darstellung einer Entity. Haelt eine Referenz auf das Simulationsobjekt,
/// schreibt es aber niemals — die Datenrichtung ist strikt Sim → View.
/// </summary>
public sealed partial class EntityView : Node3D
{
    /// <summary>Ab diesem Schaden wird der Lebensbalken eingeblendet.</summary>
    private const float HealthBarThreshold = 0.995f;

    private Entity? _entity;
    private SimulationRunner? _runner;
    private NavGrid? _grid;

    private Node3D? _model;
    private MeshInstance3D? _selectionRing;
    private HealthBar? _healthBar;

    public EntityId EntityId => _entity?.Id ?? EntityId.None;

    public Entity? Entity => _entity;

    public void Bind(Entity entity, SimulationRunner runner, NavGrid grid, Node3D model)
    {
        _entity = entity;
        _runner = runner;
        _grid = grid;
        _model = model;
        AddChild(model);
        SyncTransform(1f);
    }

    /// <summary>Tauscht das Modell aus — fuer Baustufen und Zeitalter-Varianten.</summary>
    public void ReplaceModel(Node3D model)
    {
        _model?.QueueFree();
        _model = model;
        AddChild(model);
    }

    public override void _Process(double delta)
    {
        if (_entity is null || _runner is null) return;

        SyncTransform(_runner.IsPaused ? 1f : _runner.InterpolationAlpha);
        SyncHealthBar();
    }

    private void SyncTransform(float alpha)
    {
        if (_entity is null) return;

        Vector2 planar = _entity.PreviousPosition.Lerp(_entity.Position, alpha);
        float height = _grid?.SampleHeight(planar) ?? 0f;

        Position = new Vector3(planar.X, height, planar.Y);
        Rotation = new Vector3(0f, LerpAngle(_entity.PreviousRotation, _entity.Rotation, alpha), 0f);
    }

    // --- Lebensbalken ----------------------------------------------------

    private void SyncHealthBar()
    {
        if (_entity is null) return;

        float fraction = _entity.HealthFraction;

        // Baustellen zeigen den Baufortschritt statt der Lebenspunkte.
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

    // --- Auswahlring ------------------------------------------------------

    /// <summary>Blendet den Auswahlring ein oder aus; er wird beim ersten Mal erzeugt.</summary>
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
            // Knapp ueber dem Boden, damit der Ring nicht mit dem Gelaende flimmert.
            Position = new Vector3(0f, 0.12f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.35f, 1f, 0.45f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                // Immer sichtbar, auch wenn die Einheit hinter einem Huegel steht.
                NoDepthTest = true,
                RenderPriority = 1,
            },
        };

        AddChild(ring);
        return ring;
    }

    /// <summary>Winkelinterpolation ueber den kuerzeren Weg, damit es bei ±PI nicht springt.</summary>
    private static float LerpAngle(float from, float to, float weight)
    {
        float difference = Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);
        return from + difference * weight;
    }
}
