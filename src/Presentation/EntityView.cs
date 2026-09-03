using Godot;
using EpochAeterna.Core.Entities;

namespace EpochAeterna.Presentation;

/// <summary>
/// Sichtbare Darstellung einer Entity. Haelt eine Referenz auf das Simulationsobjekt,
/// schreibt es aber niemals — die Datenrichtung ist strikt Sim → View.
/// </summary>
public sealed partial class EntityView : Node3D
{
    private Entity? _entity;
    private SimulationRunner? _runner;

    public EntityId EntityId => _entity?.Id ?? EntityId.None;

    public void Bind(Entity entity, SimulationRunner runner, Node3D model)
    {
        _entity = entity;
        _runner = runner;
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

        // Y bleibt vorerst 0 — die Terrainhoehe kommt mit Phase 2.1 dazu.
        Position = new Vector3(planar.X, 0f, planar.Y);
        Rotation = new Vector3(0f, LerpAngle(_entity.PreviousRotation, _entity.Rotation, alpha), 0f);
    }

    /// <summary>Winkelinterpolation ueber den kuerzeren Weg, damit es bei ±PI nicht springt.</summary>
    private static float LerpAngle(float from, float to, float weight)
    {
        float difference = Mathf.Wrap(to - from, -Mathf.Pi, Mathf.Pi);
        return from + difference * weight;
    }
}
