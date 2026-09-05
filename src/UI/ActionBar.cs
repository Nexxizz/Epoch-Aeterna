using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.UI;

/// <summary>
/// The command column on the right: stop, stances and demolition.
/// </summary>
/// <remarks>
/// Every button does exactly what its keyboard shortcut does — both paths end in the
/// same command on the queue. The caption carries the shortcut, so the panel teaches
/// the keyboard rather than replacing it, and it relabels itself when the key is
/// rebound.
///
/// Building and training have panels of their own; this column holds what is left
/// over and applies to any selection.
/// </remarks>
public sealed partial class ActionBar : CanvasLayer
{
    private readonly PanelContainer _panel = new();
    private readonly GridContainer _grid = new();

    private readonly Button _stop = new();
    private readonly Button _hold = new();
    private readonly Button _defensive = new();
    private readonly Button _aggressive = new();
    private readonly Button _demolish = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;
    private KeyBindings? _bindings;
    private int _localPlayerId = 1;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "ActionBar";
        _panel.AnchorLeft = 1f;
        _panel.AnchorRight = 1f;
        _panel.OffsetLeft = -(HudTheme.Margin + HudTheme.CommandWidth);
        _panel.OffsetRight = -HudTheme.Margin;
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetTop = -(HudTheme.BottomHeight + HudTheme.Margin);
        _panel.OffsetBottom = -HudTheme.Margin;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        HudTheme.ApplyPanel(_panel);
        AddChild(_panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(column);

        column.AddChild(HudTheme.MakeTitle("Befehle"));

        _grid.Columns = 2;
        _grid.AddThemeConstantOverride("h_separation", 4);
        _grid.AddThemeConstantOverride("v_separation", 4);
        column.AddChild(_grid);

        Setup(_stop, "Stopp", "cmd_stop", "Bricht alle laufenden Befehle ab", Stop);
        Setup(_hold, "Halten", "cmd_hold", "Hält die Stellung und verfolgt niemanden", () => SetStance(Stance.HoldPosition));
        Setup(_defensive, "Defensiv", "cmd_defensive", "Wehrt sich, bleibt aber in der Nähe", () => SetStance(Stance.Defensive));
        Setup(_aggressive, "Aggressiv", string.Empty, "Verfolgt Feinde in Sichtweite", () => SetStance(Stance.Aggressive));
        Setup(_demolish, "Abriss", "cmd_demolish", "Reißt das gewählte Gebäude ab und erstattet einen Teil der Kosten", Demolish);

        _panel.Visible = false;
    }

    public void Attach(SimulationWorld world, SelectionController selection, KeyBindings bindings,
        int localPlayerId)
    {
        _world = world;
        _selection = selection;
        _bindings = bindings;
        _localPlayerId = localPlayerId;

        selection.SelectionChanged += Refresh;
        bindings.Changed += Relabel;
        Relabel();
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_selection is not null) _selection.SelectionChanged -= Refresh;
        if (_bindings is not null) _bindings.Changed -= Relabel;
    }

    // --- State -----------------------------------------------------------

    private void Refresh()
    {
        if (_selection is null) return;

        bool hasUnits = SelectedUnits().Count > 0;
        bool hasBuilding = _selection.SelectedBuilding() is not null;

        _stop.Visible = hasUnits;
        _hold.Visible = hasUnits;
        _defensive.Visible = hasUnits;
        _aggressive.Visible = hasUnits;
        _demolish.Visible = hasBuilding;

        _panel.Visible = hasUnits || hasBuilding;
        UpdateStanceHighlight();
    }

    public override void _Process(double delta)
    {
        if (_panel.Visible) UpdateStanceHighlight();
    }

    /// <summary>Marks the stance the selection is actually in — only when they agree.</summary>
    private void UpdateStanceHighlight()
    {
        List<Unit> units = SelectedUnits();

        Stance? shared = null;
        foreach (Unit unit in units)
        {
            if (shared is null) shared = unit.Stance;
            else if (shared != unit.Stance) { shared = null; break; }
        }

        Highlight(_hold, shared == Stance.HoldPosition);
        Highlight(_defensive, shared == Stance.Defensive);
        Highlight(_aggressive, shared == Stance.Aggressive);
    }

    private static void Highlight(Button button, bool active) =>
        button.Modulate = active ? new Color(1.35f, 1.2f, 0.75f) : Colors.White;

    private List<Unit> SelectedUnits()
    {
        var units = new List<Unit>();
        if (_world is null || _selection is null) return units;

        foreach (EntityId id in _selection.Selection)
        {
            if (_world.Entities.GetUnit(id) is { } unit && unit.OwnerId == _localPlayerId) units.Add(unit);
        }
        return units;
    }

    // --- Commands --------------------------------------------------------

    private void Stop()
    {
        EntityId[] units = UnitIds();
        if (_world is null || units.Length == 0) return;
        _world.Commands.Enqueue(new StopCommand { PlayerId = _localPlayerId, Units = units });
    }

    private void SetStance(Stance stance)
    {
        EntityId[] units = UnitIds();
        if (_world is null || units.Length == 0) return;
        _world.Commands.Enqueue(new SetStanceCommand
        {
            PlayerId = _localPlayerId, Units = units, Stance = stance,
        });
    }

    private void Demolish()
    {
        Building? building = _selection?.SelectedBuilding();
        if (_world is null || building is null) return;
        _world.Commands.Enqueue(new DemolishCommand { PlayerId = _localPlayerId, Building = building.Id });
    }

    private EntityId[] UnitIds()
    {
        List<Unit> units = SelectedUnits();
        var ids = new EntityId[units.Count];
        for (int i = 0; i < units.Count; i++) ids[i] = units[i].Id;
        return ids;
    }

    // --- Buttons ---------------------------------------------------------

    private readonly List<(Button Button, string Label, string Action, string Tooltip)> _entries = new();

    private void Setup(Button button, string label, string action, string tooltip, System.Action pressed)
    {
        button.CustomMinimumSize = new Vector2(66f, 42f);
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += pressed;
        _grid.AddChild(button);
        _entries.Add((button, label, action, tooltip));
    }

    /// <summary>Writes the current key into every caption. Called again after a rebind.</summary>
    private void Relabel()
    {
        foreach ((Button button, string label, string action, string tooltip) in _entries)
        {
            // The aggressive stance has no key of its own — it stays a caption only.
            if (action.Length == 0)
            {
                button.Text = label;
                button.TooltipText = tooltip;
                continue;
            }

            string key = _bindings?.Label(action) ?? string.Empty;
            button.Text = $"{label}\n{key}";
            button.TooltipText = $"{tooltip} ({key})";
        }
    }
}
