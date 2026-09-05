using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.UI;

namespace EpochAeterna.Presentation;

/// <summary>
/// Contextual build palette that only appears while at least one builder is selected.
/// </summary>
/// <remarks>
/// The cards are built from the building definitions: picture, name, cost and
/// description all come out of the <c>.tres</c>. A seventh building therefore needs
/// one line in <see cref="Entries"/> and no further code.
///
/// A locked card stays clickable on purpose. A button that does nothing teaches the
/// player nothing; one that answers "requires the Copper Age" teaches them the rule.
/// </remarks>
public sealed partial class BuildMenu : CanvasLayer
{
    /// <summary>Building id and the key binding that starts the same placement.</summary>
    private static readonly (string Id, string Action)[] Entries =
    {
        ("bld_house", "build_house"),
        ("bld_storehouse", "build_storehouse"),
        ("bld_farm", "build_farm"),
        ("bld_barracks", "build_barracks"),
        ("bld_range", "build_range"),
        ("bld_tower", "build_tower"),
    };

    private sealed class Card
    {
        public required string DefinitionId { get; init; }
        public required string Action { get; init; }
        public required TextureButton Button { get; init; }
        public required Label Caption { get; init; }
        public BuildingDefinition? Definition { get; set; }
    }

    private readonly PanelContainer _panel = new();
    private readonly HBoxContainer _cards = new();
    private readonly Label _hint = new();
    private readonly List<Card> _entries = new();

    private SimulationWorld? _world;
    private SelectionController? _selection;
    private BuildPlacementController? _placement;
    private KeyBindings? _bindings;
    private NotificationFeed? _notifications;
    private int _localPlayerId;

    public override void _Ready()
    {
        Layer = 4;

        _panel.Name = "SettlerBuildMenu";
        _panel.AnchorLeft = 1f;
        _panel.AnchorRight = 1f;
        _panel.AnchorTop = 1f;
        _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = HudTheme.ActionRight - HudTheme.ActionWidth;
        _panel.OffsetRight = HudTheme.ActionRight;
        _panel.OffsetTop = -(HudTheme.BottomHeight + HudTheme.Margin);
        _panel.OffsetBottom = -HudTheme.Margin;
        _panel.MouseFilter = Control.MouseFilterEnum.Stop;
        HudTheme.ApplyPanel(_panel);
        AddChild(_panel);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 4);
        _panel.AddChild(column);

        column.AddChild(HudTheme.MakeTitle("Bauen"));

        _cards.Alignment = BoxContainer.AlignmentMode.Center;
        _cards.AddThemeConstantOverride("separation", 8);
        column.AddChild(_cards);

        _hint.Text = "Bild anklicken";
        _hint.HorizontalAlignment = HorizontalAlignment.Center;
        _hint.AddThemeFontSizeOverride("font_size", 12);
        _hint.AddThemeColorOverride("font_color", HudTheme.TextDim);
        column.AddChild(_hint);

        _panel.Visible = false;
    }

    public void Attach(SimulationWorld world, SelectionController selection,
        BuildPlacementController placement, int localPlayerId, KeyBindings bindings,
        NotificationFeed? notifications = null)
    {
        _world = world;
        _selection = selection;
        _placement = placement;
        _bindings = bindings;
        _notifications = notifications;
        _localPlayerId = localPlayerId;

        foreach ((string id, string action) in Entries) AddCard(id, action);

        selection.SelectionChanged += Refresh;
        placement.PlacementChanged += OnPlacementChanged;
        bindings.Changed += Relabel;
        Relabel();
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_selection is not null) _selection.SelectionChanged -= Refresh;
        if (_placement is not null) _placement.PlacementChanged -= OnPlacementChanged;
        if (_bindings is not null) _bindings.Changed -= Relabel;
    }

    // --- Cards -----------------------------------------------------------

    private void AddCard(string definitionId, string action)
    {
        BuildingDefinition? definition = _world?.Definitions.GetBuilding(definitionId);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 2);
        _cards.AddChild(box);

        var button = new TextureButton
        {
            Name = $"Build_{definitionId}",
            TextureNormal = definition?.Icon,
            CustomMinimumSize = new Vector2(104f, 78f),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
        };
        button.Pressed += () => BeginPlacement(definitionId);
        box.AddChild(button);

        Label caption = HudTheme.MakeLabel(string.Empty, 11, HudTheme.Text, HorizontalAlignment.Center);
        caption.CustomMinimumSize = new Vector2(104f, 0f);
        caption.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(caption);

        _entries.Add(new Card
        {
            DefinitionId = definitionId,
            Action = action,
            Button = button,
            Caption = caption,
            Definition = definition,
        });
    }

    /// <summary>Name, cost and shortcut on the card. Called again after a rebind.</summary>
    private void Relabel()
    {
        foreach (Card card in _entries)
        {
            if (card.Definition is null) continue;
            card.Caption.Text = $"{card.Definition.DisplayName}{_bindings?.Shortcut(card.Action)}\n" +
                                HudText.Cost(card.Definition.Cost);
        }
    }

    // --- Placement -------------------------------------------------------

    private void BeginPlacement(string definitionId)
    {
        Card? card = Find(definitionId);
        if (card?.Definition is null || _selection?.SelectedBuilders().Length <= 0) return;

        string? reason = LockReason(card.Definition);
        if (reason is not null)
        {
            _hint.Text = $"Gesperrt: {reason}";
            _notifications?.Post($"locked_{definitionId}",
                $"{card.Definition.DisplayName}: {reason}", HudTheme.Warning, 4f);
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
        if (!_panel.Visible || _world is null) return;

        foreach (Card card in _entries)
        {
            if (card.Definition is null) continue;

            string? reason = LockReason(card.Definition);
            // Keep the card clickable: a click explains the lock instead of doing nothing.
            card.Button.Modulate = reason is null ? Colors.White : new Color(0.52f, 0.52f, 0.52f, 1f);
            card.Button.TooltipText = reason is null
                ? $"{card.Definition.DisplayName} — {card.Definition.Description}"
                : $"Gesperrt — {reason}";
        }
    }

    // --- Rules -----------------------------------------------------------

    private string? LockReason(BuildingDefinition definition)
    {
        Player? player = _world?.GetPlayer(_localPlayerId);
        if (player is null) return "Spieler nicht verfügbar";

        if (player.AgeIndex < definition.RequiredAgeIndex)
        {
            string age = _world?.Definitions.GetAge(definition.RequiredAgeIndex)?.DisplayName ?? "ein späteres Zeitalter";
            return $"erfordert {age}";
        }

        if (!HasCompletedBuilding(definition.RequiredBuildingId))
        {
            string required = _world?.Definitions.GetBuilding(definition.RequiredBuildingId)?.DisplayName
                              ?? definition.RequiredBuildingId;
            return $"erfordert ein fertiges Gebäude: {required}";
        }

        if (!player.CanAfford(definition.Cost)) return $"nicht genügend Ressourcen ({HudText.Cost(definition.Cost)})";

        return null;
    }

    private bool HasCompletedBuilding(string definitionId)
    {
        if (_world is null || string.IsNullOrEmpty(definitionId)) return true;

        foreach (Building building in _world.Entities.Buildings)
        {
            if (building.OwnerId == _localPlayerId &&
                building.DefinitionId == definitionId && !building.IsUnderConstruction)
            {
                return true;
            }
        }
        return false;
    }

    private Card? Find(string definitionId)
    {
        foreach (Card card in _entries)
        {
            if (card.DefinitionId == definitionId) return card;
        }
        return null;
    }
}
