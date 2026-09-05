using System.Collections.Generic;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.UI;

/// <summary>
/// Short messages above the minimap: attacks, population cap, empty stores.
/// </summary>
/// <remarks>
/// Every message carries a key with a cooldown. Without one a battle would post
/// "we are under attack" twenty times a second — the interesting thing is that it is
/// happening at all, not how often the event fires.
///
/// Attacks also drop a marker on the minimap, so the message says not only what
/// happened but where.
/// </remarks>
public sealed partial class NotificationFeed : CanvasLayer
{
    /// <summary>Messages fade after this long; the oldest is dropped beyond the limit.</summary>
    private const float LifeSeconds = 7f;

    private const int MaxMessages = 5;

    /// <summary>How often the state checks (population, stores) run.</summary>
    private const float PollInterval = 1f;

    private readonly VBoxContainer _column = new();
    private readonly List<(Label Label, float Left)> _messages = new();
    private readonly Dictionary<string, float> _cooldowns = new();

    private SimulationWorld? _world;
    private Player? _player;
    private Minimap? _minimap;
    private float _sincePoll;

    public override void _Ready()
    {
        Layer = 4;

        _column.Name = "Notifications";
        _column.AnchorTop = 1f;
        _column.AnchorBottom = 1f;
        _column.OffsetLeft = HudTheme.Margin;
        _column.OffsetRight = HudTheme.Margin + 460f;
        _column.OffsetTop = -(HudTheme.BottomHeight + 220f);
        _column.OffsetBottom = -(HudTheme.BottomHeight + 2f * HudTheme.Margin);
        _column.Alignment = BoxContainer.AlignmentMode.End;
        _column.AddThemeConstantOverride("separation", 4);

        // Purely informational — it must never swallow a click meant for the map.
        _column.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_column);
    }

    public void Attach(SimulationWorld world, int localPlayerId, Minimap? minimap = null)
    {
        _world = world;
        _player = world.GetPlayer(localPlayerId);
        _minimap = minimap;

        world.Events.EntityDamaged += OnEntityDamaged;
        world.Events.ConstructionCompleted += OnConstructionCompleted;
        world.Events.AgeAdvanced += OnAgeAdvanced;
    }

    public override void _ExitTree()
    {
        if (_world is null) return;
        _world.Events.EntityDamaged -= OnEntityDamaged;
        _world.Events.ConstructionCompleted -= OnConstructionCompleted;
        _world.Events.AgeAdvanced -= OnAgeAdvanced;
    }

    /// <summary>
    /// Posts a message unless the same key was posted within the cooldown.
    /// </summary>
    public void Post(string key, string message, Color color, float cooldownSeconds = 8f)
    {
        if (_cooldowns.TryGetValue(key, out float left) && left > 0f) return;
        _cooldowns[key] = cooldownSeconds;

        Label label = HudTheme.MakeLabel(message, 15, color);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        label.AddThemeConstantOverride("outline_size", 5);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;

        _column.AddChild(label);
        _messages.Add((label, LifeSeconds));

        if (_messages.Count <= MaxMessages) return;

        (Label oldest, float _) = _messages[0];
        _messages.RemoveAt(0);
        _column.RemoveChild(oldest);
        oldest.QueueFree();
    }

    public override void _Process(double delta)
    {
        float step = (float)delta;

        foreach (string key in new List<string>(_cooldowns.Keys))
        {
            _cooldowns[key] = Mathf.Max(0f, _cooldowns[key] - step);
        }

        for (int i = _messages.Count - 1; i >= 0; i--)
        {
            (Label label, float left) = _messages[i];
            left -= step;

            if (left <= 0f)
            {
                _messages.RemoveAt(i);
                _column.RemoveChild(label);
                label.QueueFree();
                continue;
            }

            // Fade out over the last second instead of vanishing abruptly.
            label.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, left));
            _messages[i] = (label, left);
        }

        _sincePoll += step;
        if (_sincePoll < PollInterval) return;
        _sincePoll = 0f;
        PollState();
    }

    /// <summary>
    /// Conditions nobody raises an event for: they are states, not moments.
    /// </summary>
    private void PollState()
    {
        if (_player is null) return;

        if (_player.PopulationCap > 0 && _player.FreePopulation <= 0)
        {
            Post("population", "Bevölkerungslimit erreicht — baue ein Haus", HudTheme.Warning, 30f);
        }

        foreach (ResourceType type in ResourceTypes.All)
        {
            if (_player.GetResource(type) > 0) continue;
            Post($"empty_{type}", $"Zu wenig {HudText.Resource(type)}", HudTheme.Warning, 45f);
        }
    }

    // --- Simulation events -----------------------------------------------

    private void OnEntityDamaged(Entity entity, float amount)
    {
        if (_player is null || entity.OwnerId != _player.Id) return;

        Post("attacked", "Wir werden angegriffen!", HudTheme.Danger, 20f);
        _minimap?.Ping(entity.Position, HudTheme.Danger);
    }

    private void OnConstructionCompleted(Building building)
    {
        if (_player is null || building.OwnerId != _player.Id) return;

        string name = _world?.Definitions.GetBuilding(building.DefinitionId)?.DisplayName
                      ?? building.DefinitionId;
        Post($"built_{building.DefinitionId}", $"{name} fertiggestellt", HudTheme.Good, 3f);
    }

    private void OnAgeAdvanced(Player player, AgeDefinition age)
    {
        if (_player is null || player.Id != _player.Id) return;
        Post("age", $"{age.DisplayName} erreicht", HudTheme.Accent, 5f);
    }
}
