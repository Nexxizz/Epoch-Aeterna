using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>Context menu for training settlers in a selected town centre.</summary>
public sealed partial class TrainingMenu : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly TextureButton _settlerButton = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _queue = new();
    private readonly Label _status = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;
    private UnitDefinition? _settler;
    private int _localPlayerId;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "TownCentreTrainingMenu";
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = 20f;
        _panel.OffsetTop = -250f;
        _panel.OffsetRight = 220f;
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
            Text = "RATHAUS • AUSBILDEN",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(title);

        _settlerButton.Name = "TrainSettler";
        _settlerButton.CustomMinimumSize = new Vector2(176f, 112f);
        _settlerButton.IgnoreTextureSize = true;
        _settlerButton.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
        _settlerButton.TooltipText = "Siedler ausbilden (50 Nahrung, 8 Sekunden)";
        _settlerButton.Pressed += QueueSettler;
        column.AddChild(_settlerButton);

        column.AddChild(new Label
        {
            Text = "Siedler  •  50 Nahrung",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _progress.MinValue = 0;
        _progress.MaxValue = 100;
        _progress.ShowPercentage = true;
        _progress.CustomMinimumSize = new Vector2(176f, 18f);
        column.AddChild(_progress);

        _queue.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_queue);

        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeColorOverride("font_color", new Color(0.92f, 0.80f, 0.38f));
        column.AddChild(_status);

        AddChild(_panel);
        _panel.Visible = false;
    }

    public void Attach(SimulationWorld world, SelectionController selection, int localPlayerId)
    {
        _world = world;
        _selection = selection;
        _localPlayerId = localPlayerId;
        _settler = world.Definitions.GetUnit("unit_settler");
        _settlerButton.TextureNormal = _settler?.Icon;

        selection.SelectionChanged += RefreshVisibility;
        RefreshVisibility();
    }

    public override void _ExitTree()
    {
        if (_selection is not null) _selection.SelectionChanged -= RefreshVisibility;
    }

    public override void _Process(double delta)
    {
        if (!_panel.Visible || _world is null) return;

        Building? townCentre = SelectedTownCentre();
        Player? player = _world.GetPlayer(_localPlayerId);
        if (townCentre is null || player is null)
        {
            _panel.Visible = false;
            return;
        }

        ProductionOrder? current = townCentre.CurrentOrder;
        _progress.Value = current?.Progress * 100.0 ?? 0.0;
        _queue.Text = $"Warteschlange: {townCentre.Queue.Count}/{Building.MaxQueueLength}";

        bool affordable = player.CanAfford(_settler?.Cost);
        _settlerButton.Disabled = townCentre.QueueIsFull || !affordable;

        _status.Text = townCentre.QueueIsFull
            ? "Warteschlange voll"
            : !affordable
                ? "Nicht genug Nahrung"
                : current?.IsComplete == true && player.FreePopulation <= 0
                    ? "Bevölkerungslimit erreicht • Haus bauen"
                    : current is null ? "Bild anklicken" : "Ausbildung läuft";
    }

    private void QueueSettler()
    {
        Building? townCentre = SelectedTownCentre();
        if (_world is null || townCentre is null) return;

        _world.Commands.Enqueue(new TrainUnitCommand
        {
            PlayerId = _localPlayerId,
            Building = townCentre.Id,
            UnitDefinitionId = "unit_settler",
        });
    }

    private void RefreshVisibility()
    {
        _panel.Visible = SelectedTownCentre() is not null;
    }

    private Building? SelectedTownCentre()
    {
        Building? building = _selection?.SelectedBuilding();
        return building is { DefinitionId: "bld_towncenter", IsUnderConstruction: false }
            ? building
            : null;
    }
}
