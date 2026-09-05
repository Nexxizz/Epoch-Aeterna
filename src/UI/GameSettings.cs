using Godot;
using EpochAeterna.Presentation;

namespace EpochAeterna.UI;

/// <summary>
/// The player's options, kept in <c>user://settings.cfg</c>.
/// </summary>
/// <remarks>
/// The settings object is the single owner of these values; everything else — window,
/// audio bus, graphics preset, camera — is written to by <see cref="Apply"/>. The
/// options menu therefore only ever changes a field and calls Apply, and never has to
/// know which subsystem a value ends up in.
///
/// It survives a restart of the match, so it hangs off the scene root rather than off
/// the world node.
/// </remarks>
public sealed partial class GameSettings : Node
{
    public const string FilePath = "user://settings.cfg";

    private const string DisplaySection = "display";
    private const string AudioSection = "audio";
    private const string ControlsSection = "controls";
    private const string KeysSection = "keys";

    /// <summary>The resolutions offered in the options.</summary>
    public static readonly Vector2I[] Resolutions =
    {
        new(1280, 720),
        new(1600, 900),
        new(1920, 1080),
        new(2560, 1440),
    };

    public Vector2I Resolution { get; set; } = new(1600, 900);
    public bool Fullscreen { get; set; }
    public GraphicsPreset Graphics { get; set; } = GraphicsPreset.Medium;

    /// <summary>Master volume from 0 to 1.</summary>
    public float MasterVolume { get; set; } = 0.8f;

    /// <summary>Multiplier on the camera pan speed, 0.5 to 2.</summary>
    public float ScrollSpeed { get; set; } = 1f;

    public bool EdgeScroll { get; set; } = true;

    public KeyBindings Bindings { get; } = new();

    /// <summary>Raised after <see cref="Apply"/> — panels use it to relabel themselves.</summary>
    public event System.Action? Changed;

    private GraphicsSettings? _graphics;
    private RtsCamera? _camera;

    /// <summary>The graphics node lives in the scene and outlives a match.</summary>
    public void AttachGraphics(GraphicsSettings? graphics)
    {
        _graphics = graphics;
        _graphics?.Apply(Graphics);
    }

    /// <summary>
    /// The camera is rebuilt with every match and has to re-register. Passing null
    /// on the way back to the main menu drops the reference to the freed node.
    /// </summary>
    public void AttachCamera(RtsCamera? camera)
    {
        _camera = camera;
        ApplyCamera();
    }

    public void Apply()
    {
        ApplyWindow();
        ApplyAudio();
        _graphics?.Apply(Graphics);
        ApplyCamera();
        Changed?.Invoke();
    }

    private void ApplyWindow()
    {
        // Headless runs (self-test, CI) have no window to resize.
        if (DisplayServer.GetName() == "headless") return;

        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);

        if (Fullscreen) return;

        DisplayServer.WindowSetSize(Resolution);

        // Re-centre, otherwise a larger resolution pushes the window off screen.
        Vector2I screen = DisplayServer.ScreenGetSize();
        DisplayServer.WindowSetPosition((screen - Resolution) / 2);
    }

    private void ApplyAudio()
    {
        bool silent = MasterVolume <= 0.001f;
        AudioServer.SetBusMute(0, silent);
        AudioServer.SetBusVolumeDb(0, silent ? -60f : Mathf.LinearToDb(MasterVolume));
    }

    private void ApplyCamera()
    {
        // A match that has just been left leaves a freed node behind — the options
        // can still be opened from the main menu afterwards.
        if (_camera is null || !GodotObject.IsInstanceValid(_camera)) return;

        _camera.PanSpeedScale = ScrollSpeed;
        _camera.EdgeScrollEnabled = EdgeScroll;
    }

    // --- Persistence -----------------------------------------------------

    public void LoadFromDisk()
    {
        var file = new ConfigFile();
        if (file.Load(FilePath) != Error.Ok) return;

        Resolution = new Vector2I(
            (int)file.GetValue(DisplaySection, "width", Resolution.X),
            (int)file.GetValue(DisplaySection, "height", Resolution.Y));
        Fullscreen = (bool)file.GetValue(DisplaySection, "fullscreen", Fullscreen);
        Graphics = (GraphicsPreset)Mathf.Clamp(
            (int)file.GetValue(DisplaySection, "graphics", (int)Graphics),
            (int)GraphicsPreset.Low, (int)GraphicsPreset.High);

        MasterVolume = Mathf.Clamp((float)file.GetValue(AudioSection, "master", MasterVolume), 0f, 1f);

        ScrollSpeed = Mathf.Clamp((float)file.GetValue(ControlsSection, "scroll_speed", ScrollSpeed), 0.5f, 2f);
        EdgeScroll = (bool)file.GetValue(ControlsSection, "edge_scroll", EdgeScroll);

        Bindings.Load(file, KeysSection);
    }

    public void SaveToDisk()
    {
        var file = new ConfigFile();

        file.SetValue(DisplaySection, "width", Resolution.X);
        file.SetValue(DisplaySection, "height", Resolution.Y);
        file.SetValue(DisplaySection, "fullscreen", Fullscreen);
        file.SetValue(DisplaySection, "graphics", (int)Graphics);

        file.SetValue(AudioSection, "master", MasterVolume);

        file.SetValue(ControlsSection, "scroll_speed", ScrollSpeed);
        file.SetValue(ControlsSection, "edge_scroll", EdgeScroll);

        Bindings.Save(file, KeysSection);

        Error error = file.Save(FilePath);
        if (error != Error.Ok) GD.PushWarning($"[Settings] Could not be saved: {error}");
    }
}
