using Godot;

namespace EpochAeterna.Presentation;

/// <summary>
/// Schwebender Balken ueber einer Entity — Lebenspunkte oder Baufortschritt.
/// </summary>
/// <remarks>
/// Als billboardartige Quads statt als 3D-UI: Ein Viewport pro Einheit waere bei
/// hunderten Einheiten unbezahlbar, zwei unbeleuchtete Rechtecke kosten fast nichts.
/// </remarks>
public sealed partial class HealthBar : Node3D
{
    private const float Width = 1.4f;
    private const float Height = 0.16f;

    private readonly MeshInstance3D _background = new();
    private readonly MeshInstance3D _fill = new();

    private StandardMaterial3D? _fillMaterial;

    public override void _Ready()
    {
        _background.Mesh = new QuadMesh { Size = new Vector2(Width, Height) };
        _background.MaterialOverride = MakeMaterial(new Color(0.05f, 0.05f, 0.05f, 0.75f));
        _background.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_background);

        _fillMaterial = MakeMaterial(new Color(0.3f, 0.85f, 0.3f));
        _fill.Mesh = new QuadMesh { Size = new Vector2(Width, Height) };
        _fill.MaterialOverride = _fillMaterial;
        _fill.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        // Minimal davor, damit der Fuellbalken nicht mit dem Hintergrund flimmert.
        _fill.Position = new Vector3(0f, 0f, 0.01f);
        AddChild(_fill);
    }

    public void SetValue(float fraction, bool construction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);

        // Von links fuellen: skalieren und um die halbe Fehlbreite verschieben.
        _fill.Scale = new Vector3(Mathf.Max(fraction, 0.001f), 1f, 1f);
        _fill.Position = new Vector3(-Width * 0.5f * (1f - fraction), 0f, 0.01f);

        if (_fillMaterial is null) return;

        _fillMaterial.AlbedoColor = construction
            ? new Color(0.95f, 0.78f, 0.25f)
            : new Color(1f - fraction, 0.3f + fraction * 0.55f, 0.25f);
    }

    public override void _Process(double delta)
    {
        // Zur Kamera drehen. Billboard im Material waere billiger, wuerde aber
        // auch die Skalierung des Fuellbalkens mitdrehen.
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null) return;

        Vector3 toCamera = camera.GlobalPosition - GlobalPosition;
        GlobalRotation = new Vector3(0f, Mathf.Atan2(toCamera.X, toCamera.Z), 0f);
    }

    private static StandardMaterial3D MakeMaterial(Color color) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = 2,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
