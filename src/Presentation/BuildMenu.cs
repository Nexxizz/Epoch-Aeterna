using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>
/// Small contextual build palette. It is deliberately limited to the house for
/// now and only appears while at least one builder is selected.
/// </summary>
public sealed partial class BuildMenu : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly TextureButton _houseButton = new();
    private readonly Label _hint = new();

    private SelectionController? _selection;
    private BuildPlacementController? _placement;
    private BuildingDefinition? _house;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "SettlerBuildMenu";
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = 20f;
        _panel.OffsetTop = -208f;
        _panel.OffsetRight = 208f;
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

        _houseButton.Name = "BuildHouse";
        _houseButton.CustomMinimumSize = new Vector2(164f, 112f);
        _houseButton.IgnoreTextureSize = true;
        _houseButton.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        _houseButton.TooltipText = "Haus bauen (30 Holz)";
        _houseButton.Pressed += BeginHousePlacement;
        column.AddChild(_houseButton);

        var caption = new Label
        {
            Text = "Haus  •  30 Holz",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        column.AddChild(caption);

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

        _houseButton.TextureNormal = _house?.Icon;
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

    private void OnPlacementChanged(BuildingDefinition? definition)
    {
        _hint.Text = definition?.Id == "bld_house"
            ? "Bauplatz wählen • Rechtsklick: Abbrechen"
            : "Bild anklicken";
    }

    private void Refresh()
    {
        _panel.Visible = _selection?.SelectedBuilders().Length > 0;
    }
}
