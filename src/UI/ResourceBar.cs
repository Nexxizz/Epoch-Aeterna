using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.UI;

/// <summary>
/// The strip along the top edge: stock of all four resources, population and age.
/// </summary>
/// <remarks>
/// Driven by the player's events rather than polled: the numbers only change when the
/// simulation moves them, and rebuilding four strings every frame for that would be
/// wasted work. The match clock is the exception — it is redrawn once a second.
/// </remarks>
public sealed partial class ResourceBar : CanvasLayer
{
    private static readonly Color[] ResourceColors =
    {
        new(0.85f, 0.45f, 0.35f), // Nahrung
        new(0.58f, 0.40f, 0.22f), // Holz
        new(0.64f, 0.64f, 0.66f), // Stein
        new(0.93f, 0.78f, 0.32f), // Gold
    };

    private readonly PanelContainer _panel = new();
    private readonly Label[] _amounts = new Label[ResourceTypes.Count];
    private readonly Label _population = new();
    private readonly Label _age = new();
    private readonly Label _clock = new();

    private SimulationWorld? _world;
    private Player? _player;
    private bool _dirty = true;
    private int _shownMinuteTenths = -1;

    public override void _Ready()
    {
        Layer = 3;

        _panel.Name = "ResourceBar";
        _panel.AnchorRight = 1f;
        _panel.OffsetLeft = HudTheme.Margin;
        _panel.OffsetRight = -HudTheme.Margin;
        _panel.OffsetTop = HudTheme.Margin;
        _panel.OffsetBottom = HudTheme.Margin + HudTheme.TopBarHeight;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        HudTheme.ApplyPanel(_panel);
        AddChild(_panel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        row.AddThemeConstantOverride("separation", 16);
        _panel.AddChild(row);

        foreach (ResourceType type in ResourceTypes.All)
        {
            _amounts[(int)type] = AddResourceEntry(row, type);
        }

        row.AddChild(new VSeparator());

        _population.AddThemeFontSizeOverride("font_size", 15);
        _population.TooltipText = "Verbrauchte und verfügbare Bevölkerung";
        row.AddChild(_population);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        _age.AddThemeFontSizeOverride("font_size", 15);
        _age.AddThemeColorOverride("font_color", HudTheme.Accent);
        row.AddChild(_age);

        row.AddChild(new VSeparator());

        _clock.AddThemeFontSizeOverride("font_size", 15);
        _clock.AddThemeColorOverride("font_color", HudTheme.TextDim);
        _clock.TooltipText = "Verstrichene Spielzeit";
        row.AddChild(_clock);
    }

    public void Attach(SimulationWorld world, int localPlayerId)
    {
        _world = world;
        _player = world.GetPlayer(localPlayerId);
        if (_player is null) return;

        _player.ResourceChanged += OnResourceChanged;
        _player.PopulationChanged += OnPopulationChanged;
        _player.AgeAdvanced += OnAgeAdvanced;
        _dirty = true;
    }

    public override void _ExitTree()
    {
        if (_player is null) return;
        _player.ResourceChanged -= OnResourceChanged;
        _player.PopulationChanged -= OnPopulationChanged;
        _player.AgeAdvanced -= OnAgeAdvanced;
    }

    public override void _Process(double delta)
    {
        if (_world is null || _player is null) return;

        if (_dirty)
        {
            Refresh();
            _dirty = false;
        }

        // One redraw per tenth of a minute is enough for a clock showing "12.3 min".
        int tenths = Mathf.FloorToInt(_world.ElapsedSeconds / 6f);
        if (tenths == _shownMinuteTenths) return;

        _shownMinuteTenths = tenths;
        _clock.Text = $"{tenths / 10f:0.0} min";
    }

    private void Refresh()
    {
        if (_world is null || _player is null) return;

        foreach (ResourceType type in ResourceTypes.All)
        {
            _amounts[(int)type].Text = _player.GetResource(type).ToString();
        }

        bool capped = _player.FreePopulation <= 0;
        _population.Text = $"Bevölkerung {_player.Population}/{_player.PopulationCap}";
        _population.AddThemeColorOverride("font_color", capped ? HudTheme.Warning : HudTheme.Text);

        _age.Text = _world.Definitions.GetAge(_player.AgeIndex)?.DisplayName ?? "?";
    }

    private void OnResourceChanged(Player player, ResourceType type, int amount) => _dirty = true;

    private void OnPopulationChanged(Player player) => _dirty = true;

    private void OnAgeAdvanced(Player player, int ageIndex) => _dirty = true;

    private static Label AddResourceEntry(HBoxContainer parent, ResourceType type)
    {
        var entry = new HBoxContainer { TooltipText = HudText.Resource(type) };
        entry.AddThemeConstantOverride("separation", 6);
        parent.AddChild(entry);

        entry.AddChild(new ColorRect
        {
            Color = ResourceColors[(int)type],
            CustomMinimumSize = new Vector2(13f, 13f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        });

        var amount = new Label { Text = "0", CustomMinimumSize = new Vector2(54f, 0f) };
        amount.AddThemeFontSizeOverride("font_size", 15);
        amount.AddThemeColorOverride("font_color", HudTheme.Text);
        entry.AddChild(amount);

        return amount;
    }
}
