using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Aufsteigende Zahlen bei Ressourcenlieferungen — die Rueckmeldung, dass die
/// Wirtschaft tatsaechlich laeuft.
/// </summary>
/// <remarks>
/// Als 2D-Beschriftungen ueber der projizierten Weltposition, nicht als 3D-Text:
/// So bleibt die Schrift unabhaengig von der Zoomstufe lesbar, was bei einer
/// RTS-Kamera der ganze Sinn der Sache ist.
/// </remarks>
public sealed partial class FloatingTextLayer : CanvasLayer
{
    private const float Lifetime = 1.4f;
    private const float RiseSpeed = 34f;

    /// <summary>Obergrenze, damit ein Ansturm an Lieferungen die Anzeige nicht flutet.</summary>
    private const int MaxActive = 40;

    private sealed class Entry
    {
        public required Label Label { get; init; }
        public required Vector3 WorldPosition { get; init; }
        public float Age { get; set; }
    }

    private readonly List<Entry> _entries = new();

    private SimulationWorld? _world;
    private RtsCamera? _camera;
    private int _localPlayerId = 1;

    public void Attach(SimulationWorld world, RtsCamera camera, int localPlayerId)
    {
        Layer = 1;
        _world = world;
        _camera = camera;
        _localPlayerId = localPlayerId;

        world.Events.ResourceDelivered += OnResourceDelivered;
    }

    public override void _ExitTree()
    {
        if (_world is not null) _world.Events.ResourceDelivered -= OnResourceDelivered;
    }

    private void OnResourceDelivered(Unit unit, ResourceType type, int amount)
    {
        // Nur die eigene Wirtschaft anzeigen — fremde Lieferungen gehen niemanden etwas an.
        if (unit.OwnerId != _localPlayerId || _world is null || _entries.Count >= MaxActive) return;

        var label = new Label
        {
            Text = $"+{amount} {ResourceTypes.DisplayName(type)}",
            ZIndex = 10,
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", ColorFor(type));
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 4);
        AddChild(label);

        float ground = _world.Nav.SampleHeight(unit.Position);

        _entries.Add(new Entry
        {
            Label = label,
            WorldPosition = new Vector3(unit.Position.X, ground + 2.2f, unit.Position.Y),
        });
    }

    public override void _Process(double delta)
    {
        if (_camera is null) return;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            entry.Age += (float)delta;

            if (entry.Age >= Lifetime)
            {
                entry.Label.QueueFree();
                _entries.RemoveAt(i);
                continue;
            }

            if (_camera.Camera.IsPositionBehind(entry.WorldPosition))
            {
                entry.Label.Visible = false;
                continue;
            }

            float t = entry.Age / Lifetime;

            entry.Label.Visible = true;
            entry.Label.Position = _camera.Camera.UnprojectPosition(entry.WorldPosition)
                                   - new Vector2(0f, t * RiseSpeed);
            entry.Label.Modulate = entry.Label.Modulate with { A = 1f - t * t };
        }
    }

    private static Color ColorFor(ResourceType type) => type switch
    {
        ResourceType.Food => new Color(0.95f, 0.55f, 0.55f),
        ResourceType.Wood => new Color(0.65f, 0.85f, 0.5f),
        ResourceType.Stone => new Color(0.8f, 0.8f, 0.85f),
        _ => new Color(0.98f, 0.85f, 0.35f),
    };
}
