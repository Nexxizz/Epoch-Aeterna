using System.Collections.Generic;
using Godot;
using EpochAeterna.Presentation;

namespace EpochAeterna.UI;

/// <summary>
/// Display, audio, control and key binding options.
/// </summary>
/// <remarks>
/// Every change takes effect immediately and is written to disk right away. An
/// "apply" button would only add a state that can be forgotten — and a resolution you
/// cannot see the effect of is hard to judge.
///
/// The same instance serves the main menu and the pause menu; who opened it is
/// remembered by the caller, which is why closing only raises an event.
/// </remarks>
public sealed partial class OptionsMenu : CanvasLayer
{
    private readonly VBoxContainer _content = new();
    private readonly OptionButton _resolution = new();
    private readonly CheckButton _fullscreen = new();
    private readonly OptionButton _graphics = new();
    private readonly HSlider _volume = new();
    private readonly Label _volumeValue = new();
    private readonly HSlider _scrollSpeed = new();
    private readonly Label _scrollValue = new();
    private readonly CheckButton _edgeScroll = new();

    private readonly List<(Button Button, KeyBinding Binding)> _keyButtons = new();

    private GameSettings? _settings;
    private KeyBinding? _awaitingRebind;

    /// <summary>Raised when the menu closes — the caller decides what becomes visible.</summary>
    public event System.Action? Closed;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 21;
        Visible = false;

        var backdrop = new ColorRect { Color = new Color(0.03f, 0.04f, 0.06f, 0.86f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        AddChild(backdrop);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        centre.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(centre);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(780f, 660f) };
        HudTheme.ApplyPanel(panel, opaque: true);
        centre.AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);

        column.AddChild(HudTheme.MakeLabel("OPTIONEN", 28, HudTheme.Accent, HorizontalAlignment.Center));

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        column.AddChild(scroll);

        _content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_content);

        BuildDisplaySection();
        BuildAudioSection();
        BuildControlSection();

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        column.AddChild(footer);

        var reset = new Button { Text = "Tastenbelegung zurücksetzen" };
        reset.Pressed += ResetBindings;
        footer.AddChild(reset);

        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var close = new Button { Text = "Schließen", CustomMinimumSize = new Vector2(160f, 40f) };
        close.Pressed += Close;
        footer.AddChild(close);
    }

    public void Attach(GameSettings settings)
    {
        _settings = settings;
        BuildKeySection();
        Refresh();
    }

    public void Open()
    {
        Refresh();
        Visible = true;
    }

    public void Close()
    {
        if (!Visible) return;

        _awaitingRebind = null;
        Visible = false;
        _settings?.SaveToDisk();
        Closed?.Invoke();
    }

    // --- Sections --------------------------------------------------------

    private void BuildDisplaySection()
    {
        _content.AddChild(HudTheme.MakeTitle("Anzeige", HorizontalAlignment.Left));

        foreach (Vector2I size in GameSettings.Resolutions) _resolution.AddItem($"{size.X} × {size.Y}");
        _resolution.ItemSelected += index =>
        {
            if (_settings is null) return;
            _settings.Resolution = GameSettings.Resolutions[Mathf.Clamp((int)index, 0,
                GameSettings.Resolutions.Length - 1)];
            ApplyAndSave();
        };
        AddRow("Auflösung", _resolution);

        _fullscreen.Toggled += pressed =>
        {
            if (_settings is null) return;
            _settings.Fullscreen = pressed;
            _resolution.Disabled = pressed;
            ApplyAndSave();
        };
        AddRow("Vollbild", _fullscreen);

        _graphics.AddItem("Niedrig");
        _graphics.AddItem("Mittel");
        _graphics.AddItem("Hoch");
        _graphics.ItemSelected += index =>
        {
            if (_settings is null) return;
            _settings.Graphics = (GraphicsPreset)Mathf.Clamp((int)index, 0, 2);
            ApplyAndSave();
        };
        AddRow("Grafikqualität", _graphics);
    }

    private void BuildAudioSection()
    {
        _content.AddChild(HudTheme.MakeTitle("Audio", HorizontalAlignment.Left));

        _volume.MinValue = 0;
        _volume.MaxValue = 100;
        _volume.Step = 1;
        _volume.CustomMinimumSize = new Vector2(260f, 0f);
        _volume.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _volume.ValueChanged += value =>
        {
            if (_settings is null) return;
            _settings.MasterVolume = (float)value / 100f;
            _volumeValue.Text = $"{value:0} %";
            ApplyAndSave();
        };
        AddRow("Gesamtlautstärke", _volume, _volumeValue);
    }

    private void BuildControlSection()
    {
        _content.AddChild(HudTheme.MakeTitle("Steuerung", HorizontalAlignment.Left));

        _scrollSpeed.MinValue = 0.5;
        _scrollSpeed.MaxValue = 2.0;
        _scrollSpeed.Step = 0.1;
        _scrollSpeed.CustomMinimumSize = new Vector2(260f, 0f);
        _scrollSpeed.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _scrollSpeed.ValueChanged += value =>
        {
            if (_settings is null) return;
            _settings.ScrollSpeed = (float)value;
            _scrollValue.Text = $"{value:0.0}×";
            ApplyAndSave();
        };
        AddRow("Scroll-Geschwindigkeit", _scrollSpeed, _scrollValue);

        _edgeScroll.Toggled += pressed =>
        {
            if (_settings is null) return;
            _settings.EdgeScroll = pressed;
            ApplyAndSave();
        };
        AddRow("Scrollen am Bildschirmrand", _edgeScroll);
    }

    /// <summary>One row per command, grouped by the category of the binding.</summary>
    private void BuildKeySection()
    {
        if (_settings is null) return;

        _content.AddChild(HudTheme.MakeTitle("Tastenbelegung", HorizontalAlignment.Left));

        string category = string.Empty;
        foreach (KeyBinding binding in _settings.Bindings.All)
        {
            if (binding.Category != category)
            {
                category = binding.Category;
                _content.AddChild(HudTheme.MakeLabel(category, 13, HudTheme.TextDim));
            }

            var button = new Button { CustomMinimumSize = new Vector2(200f, 30f) };
            KeyBinding captured = binding;
            button.Pressed += () => BeginRebind(captured);

            AddRow(binding.DisplayName, button);
            _keyButtons.Add((button, binding));
        }
    }

    private void AddRow(string label, Control control, Control? trailing = null)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        Label caption = HudTheme.MakeLabel(label, 14);
        caption.CustomMinimumSize = new Vector2(300f, 0f);
        caption.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(caption);

        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);

        if (trailing is not null)
        {
            trailing.CustomMinimumSize = new Vector2(70f, 0f);
            row.AddChild(trailing);
        }

        _content.AddChild(row);
    }

    // --- Rebinding -------------------------------------------------------

    private void BeginRebind(KeyBinding binding)
    {
        _awaitingRebind = binding;
        RefreshKeyLabels();
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || _awaitingRebind is null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        // Escape is the way out of every menu, so it never becomes a game command.
        if (key.Keycode != Key.Escape) _settings?.Bindings.Rebind(_awaitingRebind, KeyBindings.KeyOf(key));

        _awaitingRebind = null;
        RefreshKeyLabels();
        _settings?.SaveToDisk();
        GetViewport().SetInputAsHandled();
    }

    private void ResetBindings()
    {
        _settings?.Bindings.ResetToDefaults();
        RefreshKeyLabels();
        _settings?.SaveToDisk();
    }

    // --- State -----------------------------------------------------------

    private void Refresh()
    {
        if (_settings is null) return;

        int resolutionIndex = System.Array.IndexOf(GameSettings.Resolutions, _settings.Resolution);
        _resolution.Selected = resolutionIndex >= 0 ? resolutionIndex : 1;
        _resolution.Disabled = _settings.Fullscreen;

        // Written without signals: filling the controls from the settings must not
        // look like the player just changed them.
        _fullscreen.SetPressedNoSignal(_settings.Fullscreen);
        _graphics.Selected = (int)_settings.Graphics;

        _volume.SetValueNoSignal(Mathf.Round(_settings.MasterVolume * 100f));
        _volumeValue.Text = $"{_volume.Value:0} %";

        _scrollSpeed.SetValueNoSignal(_settings.ScrollSpeed);
        _scrollValue.Text = $"{_settings.ScrollSpeed:0.0}×";

        _edgeScroll.SetPressedNoSignal(_settings.EdgeScroll);

        RefreshKeyLabels();
    }

    private void RefreshKeyLabels()
    {
        foreach ((Button button, KeyBinding binding) in _keyButtons)
        {
            button.Text = binding == _awaitingRebind
                ? "Taste drücken …"
                : binding.Current == Key.None ? "Nicht belegt" : OS.GetKeycodeString(binding.Current);
        }
    }

    private void ApplyAndSave()
    {
        _settings?.Apply();
        _settings?.SaveToDisk();
    }
}
