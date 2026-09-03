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
    private Entity? _entity;
    private SimulationRunner? _runner;
    private NavGrid? _grid;
    private MeshInstance3D? _selectionRing;

    public EntityId EntityId => _entity?.Id ?? EntityId.None;

    public void Bind(Entity entity, SimulationRunner runner, NavGrid grid, Node3D model)
    {
        _entity = entity;
        _runner = runner;
        _grid = grid;
        AddChild(model);
        SyncTransform(1f);
    }

    public override void _Process(double delta)
    {
        if (_entity is null || _runner is null) return;
        SyncTransform(_runner.IsPaused ? 1f : _runner.InterpolationAlpha);
    }

    private void SyncTransform(float alpha)
    {
        if (_entity is null) return;

        Vector2 planar = _entity.PreviousPosition.Lerp(_entity.Position, alpha);
        float height = _grid?.SampleHeight(planar) ?? 0f;

        Position = new Vector3(planar.X, height, planar.Y);
        Rotation = new Vector3(0f, LerpAngle(_entity.PreviousRotation, _entity.Rotation, alpha), 0f);
    }

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
            Building building => Mathf.Max(building.Footprint.X, building.Footprint.Y)
                                 * 0.5f * NavGrid.CellSize + 0.4f,
            Unit unit => unit.Radius + 0.45f,
            _ => 0.8f,
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
