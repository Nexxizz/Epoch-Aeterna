using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.UI;

/// <summary>
/// The panel at the bottom edge showing what is currently selected.
/// </summary>
/// <remarks>
/// Two modes with the same footprint: a single selection gets a portrait with full
/// values, several get a grid of icons. The grid is clickable, which is the quickest
/// way to pull one wounded unit out of a group.
///
/// The layout is rebuilt on selection change only; the values inside it are refreshed
/// every frame, because health and progress change continuously.
/// </remarks>
public sealed partial class SelectionPanel : CanvasLayer
{
    /// <summary>Beyond this many icons the grid is summarised instead of scrolled.</summary>
    private const int MaxIcons = 30;

    private readonly PanelContainer _panel = new();
    private readonly VBoxContainer _column = new();

    // Single selection.
    private readonly HBoxContainer _detail = new();
    private readonly TextureRect _portrait = new();
    private readonly Label _name = new();
    private readonly Label _health = new();
    private readonly ProgressBar _healthBar = HudTheme.MakeBar(HudTheme.Good);
    private readonly Label _stats = new();
    private readonly Label _status = new();
    private readonly ProgressBar _taskBar = HudTheme.MakeBar(HudTheme.Accent, 8f);

    // Multiple selection.
    private readonly VBoxContainer _group = new();
    private readonly Label _groupTitle = new();
    private readonly GridContainer _grid = new();
    private readonly List<(Button Button, EntityId Id)> _icons = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;

    public override void _Ready()
    {
        Layer = 3;

        _panel.Name = "SelectionPanel";
        HudTheme.AnchorBottom(_panel, HudTheme.SelectionLeft,
            HudTheme.SelectionLeft + HudTheme.SelectionWidth, HudTheme.BottomHeight);
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        HudTheme.ApplyPanel(_panel);
        AddChild(_panel);

        _column.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(_column);

        BuildDetail();
        BuildGroup();

        _panel.Visible = false;
    }

    private void BuildDetail()
    {
        _detail.AddThemeConstantOverride("separation", 12);
        _column.AddChild(_detail);

        _portrait.CustomMinimumSize = new Vector2(96f, 96f);
        _portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _portrait.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        _detail.AddChild(_portrait);

        var values = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        values.AddThemeConstantOverride("separation", 4);
        _detail.AddChild(values);

        _name.AddThemeFontSizeOverride("font_size", 18);
        _name.AddThemeColorOverride("font_color", HudTheme.Text);
        values.AddChild(_name);

        values.AddChild(_healthBar);

        _health.AddThemeFontSizeOverride("font_size", 12);
        _health.AddThemeColorOverride("font_color", HudTheme.TextDim);
        values.AddChild(_health);

        _stats.AddThemeFontSizeOverride("font_size", 13);
        _stats.AddThemeColorOverride("font_color", HudTheme.Text);
        values.AddChild(_stats);

        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", HudTheme.Accent);
        values.AddChild(_status);

        values.AddChild(_taskBar);
    }

    private void BuildGroup()
    {
        _group.AddThemeConstantOverride("separation", 6);
        _column.AddChild(_group);

        _groupTitle.AddThemeFontSizeOverride("font_size", 15);
        _groupTitle.AddThemeColorOverride("font_color", HudTheme.Text);
        _group.AddChild(_groupTitle);

        _grid.Columns = 10;
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        _group.AddChild(_grid);
    }

    public void Attach(SimulationWorld world, SelectionController selection)
    {
        _world = world;
        _selection = selection;

        selection.SelectionChanged += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_selection is not null) _selection.SelectionChanged -= Rebuild;
    }

    // --- Layout ----------------------------------------------------------

    private void Rebuild()
    {
        if (_world is null || _selection is null) return;

        int count = _selection.Selection.Count;
        _panel.Visible = count > 0;
        _detail.Visible = count == 1;
        _group.Visible = count > 1;

        if (count == 1) RebuildDetail();
        else if (count > 1) RebuildGroup();
    }

    private void RebuildDetail()
    {
        Entity? entity = _world?.Entities.Get(_selection!.Selection[0]);
        if (entity is null) return;

        EntityDefinition? definition = _world?.Definitions.GetEntity(entity.DefinitionId);

        // Not every definition has a picture yet — an empty frame would only take
        // space away from the values.
        _portrait.Texture = definition?.Icon;
        _portrait.Visible = definition?.Icon is not null;

        _name.Text = definition?.DisplayName ?? entity.DefinitionId;
        _stats.Text = StatsText(entity, definition);
    }

    private void RebuildGroup()
    {
        // Removed before freeing: a queued free only takes effect at the end of the
        // frame, and until then the old icons would sit in the grid alongside the new.
        foreach (Node child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _icons.Clear();

        IReadOnlyList<EntityId> selection = _selection!.Selection;
        _groupTitle.Text = $"{selection.Count} Einheiten ausgewählt — Symbol anklicken, um eine einzelne zu wählen";

        int shown = Mathf.Min(selection.Count, MaxIcons);
        for (int i = 0; i < shown; i++)
        {
            EntityId id = selection[i];
            Entity? entity = _world?.Entities.Get(id);
            if (entity is null) continue;

            EntityDefinition? definition = _world?.Definitions.GetEntity(entity.DefinitionId);

            var button = new Button
            {
                Icon = definition?.Icon,
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(42f, 42f),
                TooltipText = definition?.DisplayName ?? entity.DefinitionId,
            };
            button.Pressed += () => _selection.SelectOnly(id);

            _grid.AddChild(button);
            _icons.Add((button, id));
        }

        if (selection.Count > shown)
        {
            var more = HudTheme.MakeLabel($"+{selection.Count - shown}", 14, HudTheme.TextDim,
                HorizontalAlignment.Center);
            more.CustomMinimumSize = new Vector2(42f, 42f);
            more.VerticalAlignment = VerticalAlignment.Center;
            _grid.AddChild(more);
        }
    }

    // --- Live values -----------------------------------------------------

    public override void _Process(double delta)
    {
        if (!_panel.Visible || _world is null || _selection is null) return;

        if (_selection.Selection.Count == 1) UpdateDetail();
        else UpdateGroup();
    }

    private void UpdateDetail()
    {
        Entity? entity = _world?.Entities.Get(_selection!.Selection[0]);
        if (entity is null) return;

        float fraction = entity.HealthFraction;
        _healthBar.Value = fraction * 100.0;
        Tint(_healthBar, HudTheme.HealthColor(fraction));
        _health.Text = $"{Mathf.CeilToInt(entity.Health)} / {Mathf.RoundToInt(entity.MaxHealth)} Trefferpunkte";

        (string status, double progress) = StatusOf(entity);
        _status.Text = status;
        _taskBar.Visible = progress >= 0.0;
        if (progress >= 0.0) _taskBar.Value = progress * 100.0;
    }

    private void UpdateGroup()
    {
        foreach ((Button button, EntityId id) in _icons)
        {
            Entity? entity = _world?.Entities.Get(id);
            // Wounded units read as red without having to click through the group.
            button.Modulate = entity is null
                ? new Color(0.4f, 0.4f, 0.4f)
                : Colors.White.Lerp(HudTheme.HealthColor(entity.HealthFraction), 0.55f);
        }
    }

    private (string Status, double Progress) StatusOf(Entity entity)
    {
        switch (entity)
        {
            case Unit unit:
                return ($"{HudText.Order(unit)} • {HudText.StanceName(unit.Stance)}", -1.0);

            case Building { IsUnderConstruction: true } site:
                return ($"Im Bau • {site.ActiveBuilders} Bauarbeiter", site.ConstructionProgress);

            case Building { IsResearchingAge: true } research:
                return ($"Erforscht das nächste Zeitalter • {research.AgeResearchLeft:0} s", -1.0);

            case Building building when building.CurrentOrder is { } order:
                string name = _world?.Definitions.GetUnit(order.UnitDefinitionId)?.DisplayName
                              ?? order.UnitDefinitionId;
                return ($"Bildet aus: {name} ({building.Queue.Count} in der Warteschlange)", order.Progress);

            case Building { IsFarmRegrowing: true } farm:
                return ("Feld wächst nach", farm.FarmRegrowProgress);

            case Building { IsFarmReady: true } farm:
                return ($"Erntereif • {farm.FarmFoodRemaining} Nahrung", -1.0);

            case ResourceNode node:
                return ($"{HudText.Resource(node.Resource)} • {node.Remaining} übrig", -1.0);

            default:
                return (string.Empty, -1.0);
        }
    }

    private string StatsText(Entity entity, EntityDefinition? definition)
    {
        switch (entity)
        {
            case Unit unit:
                string attack = unit.AttackDamage > 0f
                    ? $"Angriff {unit.AttackDamage:0} • Reichweite {unit.AttackRange:0.#} m"
                    : "Kein Angriff";
                return $"{attack}\nRüstung {unit.Armor:0} • Tempo {unit.MoveSpeed:0.#} m/s" +
                       (unit.CanGather ? $"\nTraglast {unit.CarriedAmount:0}/{unit.CarryCapacity}" : string.Empty);

            case Building building:
                var parts = new List<string>();
                if (building.PopulationProvided > 0) parts.Add($"Bevölkerung +{building.PopulationProvided}");
                if (building.IsDropOffPoint) parts.Add("Abgabestelle");
                if (building.AttackDamage > 0f) parts.Add($"Angriff {building.AttackDamage:0}");
                if (building.CanAdvanceAge) parts.Add("Zeitalteraufstieg");
                parts.Add($"Rüstung {building.Armor:0}");
                return string.Join(" • ", parts);

            default:
                return definition?.Description ?? string.Empty;
        }
    }

    private static void Tint(ProgressBar bar, Color color)
    {
        if (bar.GetThemeStylebox("fill") is StyleBoxFlat style) style.BgColor = color;
    }
}
