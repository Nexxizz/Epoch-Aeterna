using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.Game;

/// <summary>
/// Einstiegspunkt. Baut Definitionsdatenbank, Simulation und Darstellung zusammen
/// und haelt sie auseinander: Der Node kennt die Welt, die Welt kennt keinen Node.
/// </summary>
public partial class Main : Node3D
{
    private readonly DefinitionDatabase _definitions = new();

    private SimulationWorld? _world;
    private SimulationRunner? _runner;
    private DebugOverlay? _overlay;

    /// <summary>Der lokal gesteuerte Spieler. Ab Phase 6 aus dem Hauptmenue gesetzt.</summary>
    private int _localPlayerId = 1;

    // --- Screenshot-Modus ------------------------------------------------
    // "--shot=<pfad>" rendert einige Frames und legt ein PNG ab. Dient dazu,
    // Beleuchtung und Szenenaufbau pruefen zu koennen, ohne das Spiel zu starten.
    private string? _screenshotPath;
    private int _framesUntilShot;

    public override void _Ready()
    {
        GD.Print("=== Epoch Aeterna ===");
        GD.Print($"Godot {Engine.GetVersionInfo()["string"]}");

        _definitions.LoadAll();

        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify") >= 0)
        {
            GetTree().Quit(SelfTest.Run(_definitions));
            return;
        }

        _world = MatchSetup.Build(MatchSetup.DefaultSkirmish(), _definitions);

        _runner = new SimulationRunner { Name = "SimulationRunner" };
        AddChild(_runner);
        _runner.Attach(_world);

        var views = new ViewManager { Name = "ViewManager" };
        AddChild(views);
        views.Attach(_world, _runner);

        _overlay = new DebugOverlay { Name = "DebugOverlay" };
        AddChild(_overlay);
        _overlay.Attach(_world, _runner);

        SetupDemoRallyPoints();
        ReportState();
        SetupScreenshotMode();
    }

    private void SetupScreenshotMode()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--shot=")) continue;

            _screenshotPath = argument["--shot=".Length..];
            // Genug Frames, damit Schatten, SSAO und ein paar Sim-Ticks stehen.
            _framesUntilShot = 90;
            return;
        }
    }

    public override void _Process(double delta)
    {
        if (_screenshotPath is null) return;

        if (--_framesUntilShot > 0) return;

        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_screenshotPath);

        GD.Print(error == Error.Ok
            ? $"[Screenshot] {_screenshotPath} ({image.GetWidth()}x{image.GetHeight()})"
            : $"[Screenshot] Fehlgeschlagen: {error}");

        _screenshotPath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    /// <summary>
    /// Setzt fuer jedes Rathaus einen Sammelpunkt, damit frisch ausgebildete Einheiten
    /// sichtbar loslaufen. Faellt weg, sobald der Spieler in Phase 2.3 selbst klickt.
    /// </summary>
    private void SetupDemoRallyPoints()
    {
        if (_world is null) return;

        foreach (Building building in _world.Entities.Buildings)
        {
            if (!building.CanAdvanceAge) continue;

            float direction = building.Position.X < 0f ? 1f : -1f;
            _world.Commands.Enqueue(new SetRallyPointCommand
            {
                PlayerId = building.OwnerId,
                Building = building.Id,
                Target = building.Position + new Vector2(direction * 12f, 8f),
            });
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_world is null || _runner is null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit();
                break;

            case Key.Space:
                _runner.TimeScale = _runner.IsPaused ? 1f : 0f;
                break;

            case Key.Key1:
                QueueUnit(MatchSetup.SettlerId);
                break;

            case Key.Key2:
                QueueUnit("unit_scout");
                break;
        }
    }

    /// <summary>Reiht eine Einheit im Rathaus des lokalen Spielers ein — ueber die Befehlsqueue, wie die KI auch.</summary>
    private void QueueUnit(string unitDefinitionId)
    {
        if (_world is null) return;

        foreach (Building building in _world.Entities.Buildings)
        {
            if (building.OwnerId != _localPlayerId || !building.CanAdvanceAge) continue;

            _world.Commands.Enqueue(new TrainUnitCommand
            {
                PlayerId = _localPlayerId,
                Building = building.Id,
                UnitDefinitionId = unitDefinitionId,
            });
            return;
        }
    }

    private void ReportState()
    {
        if (_world is null) return;

        GD.Print($"[Match] {_world.Players.Count} Spieler, {_world.Entities.Count} Entities, " +
                 $"{_world.Systems.Count} Systeme, Tickrate {SimulationWorld.TicksPerSecond} Hz.");

        foreach (Player player in _world.Players)
        {
            GD.Print($"  {player.Name}: Bev {player.Population}/{player.PopulationCap}, " +
                     $"Nahrung {player.GetResource(ResourceType.Food)}, " +
                     $"Holz {player.GetResource(ResourceType.Wood)}");
        }
    }
}
