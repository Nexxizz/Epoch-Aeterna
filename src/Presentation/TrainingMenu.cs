using System.Collections.Generic;
using System.Text;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>Context menu for every selected building that can train units.</summary>
public sealed partial class TrainingMenu : CanvasLayer
{
    private static readonly string[] UnitIds =
        { "unit_settler", "unit_spearman", "unit_swordsman", "unit_slinger", "unit_archer" };

    private readonly PanelContainer _panel = new();
    private readonly Label _title = new();
    private readonly HBoxContainer _actions = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _queue = new();
    private readonly Label _status = new();
    private readonly Button _advanceAgeButton = new();
    private readonly Dictionary<string, Button> _buttons = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;
    private int _localPlayerId;
    private string _feedback = string.Empty;
    private double _feedbackSeconds;

    public override void _Ready()
    {
        Layer = 4;
        _panel.Name = "TrainingMenu";
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = 20f;
        _panel.OffsetTop = -305f;
        _panel.OffsetRight = 550f;
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

        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(_title);

        _actions.Alignment = BoxContainer.AlignmentMode.Center;
        _actions.AddThemeConstantOverride("separation", 8);
        column.AddChild(_actions);

        _advanceAgeButton.Name = "AdvanceAge";
        _advanceAgeButton.CustomMinimumSize = new Vector2(360f, 48f);
        _advanceAgeButton.Pressed += AdvanceAge;
        column.AddChild(_advanceAgeButton);

        _progress.MinValue = 0;
        _progress.MaxValue = 100;
        _progress.ShowPercentage = true;
        _progress.CustomMinimumSize = new Vector2(360f, 18f);
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

        foreach (string unitId in UnitIds)
        {
            UnitDefinition? definition = world.Definitions.GetUnit(unitId);
            if (definition is null) continue;

            var button = new Button
            {
                Name = $"Train_{unitId}",
                Text = $"{definition.DisplayName}\n{CostText(definition.Cost)}\n{definition.BuildTimeSeconds:0} s",
                Icon = definition.Icon,
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(160f, 112f),
                TooltipText = definition.Description,
            };
            button.Pressed += () => QueueUnit(unitId);
            _buttons[unitId] = button;
            _actions.AddChild(button);
        }

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
        _feedbackSeconds = Mathf.Max(0.0, _feedbackSeconds - delta);
        Building? building = SelectedProductionBuilding();
        Player? player = _world.GetPlayer(_localPlayerId);
        if (building is null || player is null) { _panel.Visible = false; return; }

        BuildingDefinition? buildingDefinition = _world.Definitions.GetBuilding(building.DefinitionId);
        if (buildingDefinition is null) return;

        AgeDefinition? nextAge = _world.Definitions.GetAge(player.AgeIndex + 1);
        _advanceAgeButton.Visible = building.CanAdvanceAge && nextAge is not null;
        if (_advanceAgeButton.Visible && nextAge is not null)
        {
            int distinctBuildings = CountDistinctBuildings();
            string? lockReason = building.IsResearchingAge
                ? null
                : distinctBuildings < nextAge.RequiredDistinctBuildings
                    ? $"Benötigt {nextAge.RequiredDistinctBuildings} verschiedene fertige Gebäude"
                    : !player.CanAfford(nextAge.AdvanceCost)
                        ? $"Nicht genügend Ressourcen: {CostText(nextAge.AdvanceCost)}"
                        : null;

            _advanceAgeButton.Text = building.IsResearchingAge
                ? $"KUPFERZEIT WIRD ERFORSCHT • {building.AgeResearchLeft:0} s"
                : $"KUPFERZEIT ERFORSCHEN • {CostText(nextAge.AdvanceCost)} • {nextAge.ResearchTimeSeconds:0} s";
            _advanceAgeButton.Disabled = building.IsResearchingAge;
            _advanceAgeButton.Modulate = lockReason is null
                ? Colors.White
                : new Color(0.62f, 0.62f, 0.62f, 1f);
            _advanceAgeButton.TooltipText = lockReason ??
                "Schaltet Schießstand, Wachturm, Bogenschützen und Schwertkämpfer frei";
        }

        _title.Text = $"{buildingDefinition.DisplayName.ToUpperInvariant()} • AUSBILDEN";
        foreach ((string unitId, Button button) in _buttons)
        {
            bool offered = System.Array.IndexOf(buildingDefinition.TrainableUnitIds, unitId) >= 0;
            UnitDefinition? unit = _world.Definitions.GetUnit(unitId);
            button.Visible = offered;
            button.Disabled = building.QueueIsFull || unit is null ||
                              player.AgeIndex < unit.RequiredAgeIndex || !player.CanAfford(unit.Cost);
        }

        ProductionOrder? current = building.CurrentOrder;
        _progress.Value = current?.Progress * 100.0 ?? 0.0;
        _queue.Text = $"Warteschlange: {building.Queue.Count}/{Building.MaxQueueLength}";
        _status.Text = _feedbackSeconds > 0.0 ? _feedback
            : building.QueueIsFull ? "Warteschlange voll"
            : current?.IsComplete == true && player.FreePopulation <= 0
                ? "Bevölkerungslimit erreicht • Haus bauen"
                : current is null ? "Einheit anklicken" : "Ausbildung läuft";
    }

    private void QueueUnit(string unitId)
    {
        Building? building = SelectedProductionBuilding();
        if (_world is null || building is null) return;
        _world.Commands.Enqueue(new TrainUnitCommand
        {
            PlayerId = _localPlayerId, Building = building.Id, UnitDefinitionId = unitId,
        });
    }

    private void AdvanceAge()
    {
        Building? building = SelectedProductionBuilding();
        Player? player = _world?.GetPlayer(_localPlayerId);
        AgeDefinition? nextAge = player is null ? null : _world?.Definitions.GetAge(player.AgeIndex + 1);
        if (_world is null || building is null || player is null || nextAge is null) return;

        int distinctBuildings = CountDistinctBuildings();
        if (distinctBuildings < nextAge.RequiredDistinctBuildings)
        {
            ShowFeedback($"Kupferzeit benötigt {nextAge.RequiredDistinctBuildings} verschiedene fertige Gebäude");
            return;
        }
        if (!player.CanAfford(nextAge.AdvanceCost))
        {
            ShowFeedback($"Kupferzeit: Benötigt {CostText(nextAge.AdvanceCost)}");
            return;
        }

        _world.Commands.Enqueue(new AdvanceAgeCommand
        {
            PlayerId = _localPlayerId,
            Building = building.Id,
        });
        ShowFeedback("Kupferzeit-Forschung beauftragt");
    }

    private void ShowFeedback(string message)
    {
        _feedback = message;
        _feedbackSeconds = 3.0;
        _status.Text = message;
    }

    private int CountDistinctBuildings()
    {
        if (_world is null) return 0;
        var definitions = new HashSet<string>();
        foreach (Building building in _world.Entities.Buildings)
        {
            if (building.OwnerId == _localPlayerId && !building.IsUnderConstruction)
                definitions.Add(building.DefinitionId);
        }
        return definitions.Count;
    }

    private void RefreshVisibility() => _panel.Visible = SelectedProductionBuilding() is not null;

    private Building? SelectedProductionBuilding()
    {
        Building? building = _selection?.SelectedBuilding();
        if (_world is null || building is null || building.IsUnderConstruction) return null;
        BuildingDefinition? definition = _world.Definitions.GetBuilding(building.DefinitionId);
        return definition?.TrainableUnitIds.Length > 0 ? building : null;
    }

    private static string CostText(ResourceSet? cost)
    {
        if (cost is null) return "Kostenlos";
        var text = new StringBuilder();
        foreach (ResourceType type in ResourceTypes.All)
        {
            int amount = cost[type];
            if (amount <= 0) continue;
            if (text.Length > 0) text.Append(" • ");
            text.Append(amount).Append(' ').Append(ResourceTypes.DisplayName(type));
        }
        return text.Length > 0 ? text.ToString() : "Kostenlos";
    }
}
