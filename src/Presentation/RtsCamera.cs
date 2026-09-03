using Godot;
using EpochAeterna.Core.Pathfinding;

namespace EpochAeterna.Presentation;

/// <summary>
/// Klassische RTS-Kamera: ein Drehpunkt auf dem Boden, die Kamera haengt im Abstand daran.
/// </summary>
/// <remarks>
/// Der Drehpunkt ist der Blickpunkt auf der Karte, nicht die Kameraposition. Dadurch
/// bleiben Zoom und Rotation intuitiv — man dreht sich um das, was man ansieht.
///
/// Die Neigung ist an den Zoom gekoppelt: nah am Boden flacher (man sieht die Gebaeude
/// von der Seite), weit weg steiler (man sieht die Lage von oben).
/// </remarks>
public sealed partial class RtsCamera : Node3D
{
    private const float MinDistance = 18f;
    private const float MaxDistance = 95f;
    private const float MinPitchDegrees = 32f;
    private const float MaxPitchDegrees = 62f;

    private const float PanSpeed = 34f;
    private const float RotateSpeed = 1.8f;
    private const float ZoomStep = 5.5f;

    /// <summary>Wie schnell Zoom und Position dem Sollwert folgen. Hoeher = direkter.</summary>
    private const float Smoothing = 12f;

    /// <summary>Randbreite in Pixeln, in der Edge-Scrolling ausloest.</summary>
    private const int EdgeMargin = 6;

    private readonly Camera3D _camera = new();

    private NavGrid? _grid;

    private Vector2 _focus;
    private float _yaw;
    private float _distance = 55f;
    private float _targetDistance = 55f;

    private bool _dragging;

    /// <summary>Edge-Scrolling stoert beim Entwickeln mit Fenstermodus — abschaltbar.</summary>
    public bool EdgeScrollEnabled { get; set; } = true;

    public Camera3D Camera => _camera;

    /// <summary>Blickpunkt auf der XZ-Ebene.</summary>
    public Vector2 Focus => _focus;

    public override void _Ready()
    {
        _camera.Fov = 50f;
        _camera.Far = 600f;
        AddChild(_camera);
    }

    public void Attach(NavGrid grid, Vector2 initialFocus)
    {
        _grid = grid;
        _focus = initialFocus;
        UpdateTransform(snap: true);
    }

    /// <summary>Springt sofort an eine Stelle — fuer Minimap-Klicks und Ereignis-Sprünge.</summary>
    public void JumpTo(Vector2 position)
    {
        _focus = position;
        UpdateTransform(snap: true);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true } button:
                HandleMouseButton(button);
                break;

            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Middle }:
                _dragging = false;
                break;

            case InputEventMouseMotion motion when _dragging:
                // Weltbewegung gegen die Mausbewegung, damit sich die Karte
                // unter dem Cursor mitzieht.
                Vector2 delta = -motion.Relative * _distance * 0.0016f;
                _focus += Rotate(delta, _yaw);
                break;
        }
    }

    private void HandleMouseButton(InputEventMouseButton button)
    {
        switch (button.ButtonIndex)
        {
            case MouseButton.WheelUp:
                _targetDistance = Mathf.Clamp(_targetDistance - ZoomStep, MinDistance, MaxDistance);
                break;

            case MouseButton.WheelDown:
                _targetDistance = Mathf.Clamp(_targetDistance + ZoomStep, MinDistance, MaxDistance);
                break;

            case MouseButton.Middle:
                _dragging = true;
                break;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        Vector2 pan = ReadPanInput();
        if (pan != Vector2.Zero)
        {
            // Panning skaliert mit der Zoomstufe: weit draussen legt man mehr Strecke zurueck.
            float scale = Mathf.Lerp(0.55f, 1.4f, Mathf.InverseLerp(MinDistance, MaxDistance, _distance));
            _focus += Rotate(pan.Normalized() * PanSpeed * scale * dt, _yaw);
        }

        if (Input.IsActionPressed("camera_rotate_left")) _yaw -= RotateSpeed * dt;
        if (Input.IsActionPressed("camera_rotate_right")) _yaw += RotateSpeed * dt;

        _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-Smoothing * dt));

        ClampToMap();
        UpdateTransform(snap: false);
    }

    private Vector2 ReadPanInput()
    {
        var pan = new Vector2(
            Input.GetActionStrength("camera_right") - Input.GetActionStrength("camera_left"),
            Input.GetActionStrength("camera_down") - Input.GetActionStrength("camera_up"));

        if (!EdgeScrollEnabled || !DisplayServer.WindowIsFocused()) return pan;

        Vector2 mouse = GetViewport().GetMousePosition();
        Vector2 size = GetViewport().GetVisibleRect().Size;

        if (mouse.X <= EdgeMargin) pan.X -= 1f;
        else if (mouse.X >= size.X - EdgeMargin) pan.X += 1f;

        if (mouse.Y <= EdgeMargin) pan.Y -= 1f;
        else if (mouse.Y >= size.Y - EdgeMargin) pan.Y += 1f;

        return pan;
    }

    private void ClampToMap()
    {
        if (_grid is null) return;

        float halfWidth = _grid.WorldWidth * 0.5f;
        float halfHeight = _grid.WorldHeight * 0.5f;

        _focus.X = Mathf.Clamp(_focus.X, -halfWidth, halfWidth);
        _focus.Y = Mathf.Clamp(_focus.Y, -halfHeight, halfHeight);
    }

    private void UpdateTransform(bool snap)
    {
        if (snap) _distance = _targetDistance;

        float groundHeight = _grid?.SampleHeight(_focus) ?? 0f;
        Position = new Vector3(_focus.X, groundHeight, _focus.Y);
        Rotation = new Vector3(0f, _yaw, 0f);

        // Nah = flach, fern = steil.
        float zoom = Mathf.InverseLerp(MinDistance, MaxDistance, _distance);
        float pitch = Mathf.DegToRad(Mathf.Lerp(MinPitchDegrees, MaxPitchDegrees, zoom));

        _camera.Position = new Vector3(0f, Mathf.Sin(pitch) * _distance, Mathf.Cos(pitch) * _distance);
        _camera.Rotation = new Vector3(-pitch, 0f, 0f);
    }

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(value.X * cos + value.Y * sin, -value.X * sin + value.Y * cos);
    }
}
