using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Contextual build palette that only appears while at least one builder is selected.
/// </summary>
public sealed partial class BuildMenu : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly TextureButton _houseButton = new();
    private readonly TextureButton _storehouseButton = new();
    private readonly TextureButton _farmButton = new();
    private readonly TextureButton _barracksButton = new();
    private readonly TextureButton _rangeButton = new();
    private readonly TextureButton _towerButton = new();
    private readonly Label _hint = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;
    private BuildPlacementController? _placement;
    private int _localPlayerId;
    private BuildingDefinition? _house;
    private BuildingDefinition? _storehouse;
    private BuildingDefinition? _farm;
    private BuildingDefinition? _barracks;
    private BuildingDefinition? _range;
    private BuildingDefinition? _tower;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "SettlerBuildMenu";
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = 20f;
        _panel.OffsetTop = -218f;
        _panel.OffsetRight = 1084f;
        _panel.OffsetBottom = -20f;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _panel.AddChild(margin);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 5);
        margin.AddChild(column);

        var title = new Label
        {
            Text = "BAUEN",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(title);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 8);
        column.AddChild(actions);

        AddBuildingAction(actions, _houseButton, "BuildHouse", "Haus", "30 Holz",
            "Haus bauen (30 Holz)", BeginHousePlacement);
        AddBuildingAction(actions, _storehouseButton, "BuildStorehouse", "Lagerhaus", "80 Holz",
            "Lagerhaus bauen (80 Holz) — Abgabestelle für Holz, Stein und Gold",
            BeginStorehousePlacement);
        AddBuildingAction(actions, _farmButton, "BuildFarm", "Farm", "60 Holz",
            "Farm bauen (60 Holz) — erneuerbare Nahrungsquelle", BeginFarmPlacement);
        AddBuildingAction(actions, _barracksButton, "BuildBarracks", "Kaserne", "150 Holz",
            "Kaserne bauen (150 Holz) — bildet Nahkämpfer aus", BeginBarracksPlacement);
        AddBuildingAction(actions, _rangeButton, "BuildRange", "Schießstand", "150 Holz + 50 Stein",
            "Schießstand bauen — erfordert Kupferzeit und eine fertige Kaserne",
            BeginRangePlacement);
        AddBuildingAction(actions, _towerButton, "BuildTower", "Wachturm", "50 Holz + 120 Stein",
            "Wachturm bauen — erfordert Kupferzeit, große Sichtweite und automatischer Angriff",
            BeginTowerPlacement);

        _hint.Text = "Bild anklicken";
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.76f));
        column.AddChild(_hint);

        AddChild(_panel);
        _panel.Visible = false;
    }

    public void Attach(SimulationWorld world, SelectionController selection,
        BuildPlacementController placement, int localPlayerId)
    {
        _world = world;
        _selection = selection;
        _placement = placement;
        _localPlayerId = localPlayerId;
        _house = world.Definitions.GetBuilding("bld_house");
        _storehouse = world.Definitions.GetBuilding("bld_storehouse");
        _farm = world.Definitions.GetBuilding("bld_farm");
        _barracks = world.Definitions.GetBuilding("bld_barracks");
        _range = world.Definitions.GetBuilding("bld_range");
        _tower = world.Definitions.GetBuilding("bld_tower");

        _houseButton.TextureNormal = _house?.Icon;
        _storehouseButton.TextureNormal = _storehouse?.Icon;
        _farmButton.TextureNormal = _farm?.Icon;
        _barracksButton.TextureNormal = _barracks?.Icon;
        _rangeButton.TextureNormal = _range?.Icon;
        _towerButton.TextureNormal = _tower?.Icon;
        selection.SelectionChanged += Refresh;
        placement.PlacementChanged += OnPlacementChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_selection is not null) _selection.SelectionChanged -= Refresh;
        if (_placement is not null) _placement.PlacementChanged -= OnPlacementChanged;
    }

    private void BeginHousePlacement()
    {
        if (_selection?.SelectedBuilders().Length > 0) _placement?.Begin("bld_house");
    }

    private void BeginStorehousePlacement()
    {
        if (_selection?.SelectedBuilders().Length > 0) _placement?.Begin("bld_storehouse");
    }

    private void BeginFarmPlacement()
    {
        if (_selection?.SelectedBuilders().Length > 0) _placement?.Begin("bld_farm");
    }

    private void BeginBarracksPlacement()
    {
        if (_selection?.SelectedBuilders().Length > 0) _placement?.Begin("bld_barracks");
    }

    private void BeginRangePlacement()
    {
        BeginLockedPlacement(_range, "bld_range");
    }

    private void BeginTowerPlacement()
    {
        BeginLockedPlacement(_tower, "bld_tower");
    }

    private void BeginLockedPlacement(BuildingDefinition? definition, string definitionId)
    {
        if (_selection?.SelectedBuilders().Length <= 0 || definition is null) return;
        string? reason = BuildLockReason(definition);
        if (reason is not null)
        {
            _hint.Text = $"Gesperrt: {reason}";
            return;
        }
        _placement?.Begin(definitionId);
    }

    private void OnPlacementChanged(BuildingDefinition? definition)
    {
        _hint.Text = definition is not null
            ? "Bauplatz wählen • Rechtsklick: Abbrechen"
            : "Bild anklicken";
    }

    private void Refresh()
    {
        _panel.Visible = _selection?.SelectedBuilding() is null &&
                         _selection?.SelectedBuilders().Length > 0;
    }

    public override void _Process(double delta)
    {
        if (!_panel.Visible || _world is null || _range is null || _tower is null) return;
        Player? player = _world.GetPlayer(_localPlayerId);
        if (player is null) return;

        UpdateLockedButton(_rangeButton, _range,
            "Schießstand bauen — bildet Schleuderer und Bogenschützen aus");
        UpdateLockedButton(_towerButton, _tower,
            "Wachturm bauen — große Sichtweite und automatischer Fernkampfangriff");
    }

    private void UpdateLockedButton(TextureButton button, BuildingDefinition definition,
        string availableTooltip)
    {
        string? reason = BuildLockReason(definition);
        // Keep the card clickable: a click explains the lock instead of doing nothing.
        button.Disabled = false;
        button.Modulate = reason is null ? Colors.White : new Color(0.52f, 0.52f, 0.52f, 1f);
        button.TooltipText = reason is null ? availableTooltip : $"Gesperrt — {reason}";
    }

    private string? BuildLockReason(BuildingDefinition definition)
    {
        Player? player = _world?.GetPlayer(_localPlayerId);
        if (player is null) return "Spieler nicht verfügbar";
        if (player.AgeIndex < definition.RequiredAgeIndex) return "erfordert die Kupferzeit";
        if (!HasCompletedBuilding(definition.RequiredBuildingId)) return "erfordert eine fertige Kaserne";
        if (!player.CanAfford(definition.Cost)) return "nicht genügend Ressourcen";
        return null;
    }

    private bool HasCompletedBuilding(string definitionId)
    {
        if (_world is null || string.IsNullOrEmpty(definitionId)) return true;
        foreach (Building building in _world.Entities.Buildings)
        {
            if (building.OwnerId == _localPlayerId &&
                building.DefinitionId == definitionId && !building.IsUnderConstruction)
                return true;
        }
        return false;
    }

    private static void AddBuildingAction(HBoxContainer parent, TextureButton button,
        string name, string label, string cost, string tooltip, System.Action pressed)
    {
        var card = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        parent.AddChild(card);

        button.Name = name;
        button.CustomMinimumSize = new Vector2(164f, 112f);
        button.IgnoreTextureSize = true;
        button.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        button.TooltipText = tooltip;
        button.Pressed += pressed;
        card.AddChild(button);

        card.AddChild(new Label
        {
            Text = $"{label}\n{cost}",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
    }
}
