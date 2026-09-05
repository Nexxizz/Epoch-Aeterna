using System.Text;
using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;

namespace EpochAeterna.Presentation;

/// <summary>Victory or defeat screen with statistics for both sides.</summary>
public sealed partial class MatchEndScreen : CanvasLayer
{
    private readonly ColorRect _backdrop = new();
    private readonly Label _title = new();
    private readonly Label _body = new();
    private readonly Label _hint = new();

    private SimulationWorld? _world;
    private int _localPlayerId = 1;

    /// <summary>Raised when the player requests a new match.</summary>
    public event System.Action? RestartRequested;

    public override void _Ready()
    {
        Layer = 5;
        Visible = false;

        _backdrop.Color = new Color(0f, 0f, 0f, 0.72f);
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_backdrop);

        _title.Position = new Vector2(0f, 150f);
        _title.AddThemeFontSizeOverride("font_size", 54);
        AddChild(_title);

        _body.Position = new Vector2(0f, 240f);
        _body.AddThemeFontSizeOverride("font_size", 18);
        _body.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.92f));
        AddChild(_body);

        _hint.Position = new Vector2(0f, 240f);
        _hint.AddThemeFontSizeOverride("font_size", 17);
        _hint.AddThemeColorOverride("font_color", new Color(0.75f, 0.78f, 0.85f));
        AddChild(_hint);
    }

    public void Attach(SimulationWorld world, int localPlayerId)
    {
        _world = world;
        _localPlayerId = localPlayerId;
        world.Events.MatchEnded += Show;
    }

    public override void _ExitTree()
    {
        if (_world is not null) _world.Events.MatchEnded -= Show;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter }) return;

        RestartRequested?.Invoke();
    }

    private void Show(Player? winner)
    {
        if (_world is null) return;

        bool won = winner?.Id == _localPlayerId;

        _title.Text = winner is null ? "Draw" : won ? "Victory" : "Defeat";
        _title.AddThemeColorOverride("font_color",
            won ? new Color(0.5f, 0.95f, 0.55f) : new Color(0.95f, 0.45f, 0.4f));

        _body.Text = BuildStatistics(_world);
        _hint.Text = "\n\nEnter — new match      Esc — main menu";

        Layout();
        Visible = true;
    }

    /// <summary>Centres the labels. Called again whenever the viewport size changes.</summary>
    private void Layout()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;

        Centre(_title, size, 150f);
        Centre(_body, size, 250f);
        Centre(_hint, size, 250f + _body.GetMinimumSize().Y);
    }

    private static void Centre(Label label, Vector2 viewport, float top)
    {
        label.Position = new Vector2((viewport.X - label.GetMinimumSize().X) * 0.5f, top);
    }

    private static string BuildStatistics(SimulationWorld world)
    {
        var builder = new StringBuilder();
        builder.Append("Match time ").Append((world.ElapsedSeconds / 60f).ToString("0.0")).AppendLine(" minutes\n");

        foreach (Player player in world.Players)
        {
            builder.Append(player.Name).Append(player.IsDefeated ? "  (defeated)" : string.Empty).AppendLine();
            builder.Append("   Gathered: ");

            foreach (ResourceType type in ResourceTypes.All)
            {
                builder.Append(ResourceTypes.DisplayName(type)).Append(' ')
                       .Append(player.Stats.GetGathered(type)).Append("   ");
            }

            builder.AppendLine();
            builder.Append("   Trained ").Append(player.Stats.UnitsTrained)
                   .Append("   Built ").Append(player.Stats.BuildingsCompleted)
                   .Append("   Killed ").Append(player.Stats.EnemiesKilled)
                   .Append("   Lost ").Append(player.Stats.UnitsLost + player.Stats.BuildingsLost)
                   .AppendLine();
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
