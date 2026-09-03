using Godot;
using EpochAeterna.Core.Data;
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
    private readonly Label _hint = new();

    private SelectionController? _selection;
    private BuildPlacementController? _placement;
    private BuildingDefinition? _house;
    private BuildingDefinition? _storehouse;
    private BuildingDefinition? _farm;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "SettlerBuildMenu";
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = 20f;
        _panel.OffsetTop = -218f;
        _panel.OffsetRight = 578f;
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

        _hint.Text = "Bild anklicken";
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.76f));
        column.AddChild(_hint);

        AddChild(_panel);
        _panel.Visible = false;
    }

    public void Attach(DefinitionDatabase definitions, SelectionController selection,
        BuildPlacementController placement)
    {
        _selection = selection;
        _placement = placement;
        _house = definitions.GetBuilding("bld_house");
        _storehouse = definitions.GetBuilding("bld_storehouse");
        _farm = definitions.GetBuilding("bld_farm");

        _houseButton.TextureNormal = _house?.Icon;
        _storehouseButton.TextureNormal = _storehouse?.Icon;
        _farmButton.TextureNormal = _farm?.Icon;
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
            Text = $"{label}  •  {cost}",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
    }
}
