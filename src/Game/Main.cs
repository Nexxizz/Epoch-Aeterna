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

    private Match? _match;
    private SimulationRunner? _runner;
    private RtsCamera? _camera;
    private SelectionController? _selection;

    private SimulationWorld? World => _match?.World;

    /// <summary>Der lokal gesteuerte Spieler. Ab Phase 6 aus dem Hauptmenue gesetzt.</summary>
    private readonly int _localPlayerId = 1;

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

        _match = MatchSetup.Build(MatchSetup.DefaultSkirmish(), _definitions);

        BuildWorldView();
        BuildSimulation();
        BuildControls();

        SetupDemoRallyPoints();
        ReportState();
        SetupScreenshotMode();
    }

    /// <summary>Gelaende und Streuobjekte — einmalig aus der erzeugten Karte gebaut.</summary>
    private void BuildWorldView()
    {
        if (_match is null) return;

        var terrain = new TerrainRenderer { Name = "Terrain" };
        AddChild(terrain);
        terrain.Build(_match.Map.Grid);

        var decorations = new DecorationRenderer { Name = "Decorations" };
        AddChild(decorations);
        decorations.Build(_match.Map.Decorations, _match.Map.Grid);
    }

    private void BuildSimulation()
    {
        if (World is null) return;

        _runner = new SimulationRunner { Name = "SimulationRunner" };
        AddChild(_runner);
        _runner.Attach(World);
    }

    private void BuildControls()
    {
        if (_match is null || World is null || _runner is null) return;

        var views = new ViewManager { Name = "ViewManager" };
        AddChild(views);
        views.Attach(World, _runner);

        _camera = new RtsCamera { Name = "RtsCamera" };
        AddChild(_camera);
        _camera.Attach(_match.Map.Grid, _match.Map.StartPositions[0]);

        _selection = new SelectionController { Name = "SelectionController" };
        AddChild(_selection);
        _selection.Attach(World, _camera, views, _localPlayerId);

        var overlay = new DebugOverlay { Name = "DebugOverlay" };
        AddChild(overlay);
        overlay.Attach(World, _runner, _selection);
    }

    /// <summary>
    /// Setzt fuer jedes Rathaus einen Sammelpunkt, damit frisch ausgebildete Einheiten
    /// sichtbar loslaufen.
    /// </summary>
    private void SetupDemoRallyPoints()
    {
        if (World is null) return;

        foreach (Building building in World.Entities.Buildings)
        {
            if (!building.CanAdvanceAge) continue;

            float direction = building.Position.X < 0f ? 1f : -1f;
            World.Commands.Enqueue(new SetRallyPointCommand
            {
                PlayerId = building.OwnerId,
                Building = building.Id,
                Target = building.Position + new Vector2(direction * 12f, 8f),
            });
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (World is null || _runner is null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.Keycode)
        {
            case Key.Escape:
                GetTree().Quit();
                break;

            case Key.Space:
                _runner.TimeScale = _runner.IsPaused ? 1f : 0f;
                break;

            case Key.F1:
                QueueUnit(MatchSetup.SettlerId);
                break;

            case Key.F2:
                QueueUnit("unit_scout");
                break;

            case Key.Home:
                JumpToOwnBase();
                break;
        }
    }

    /// <summary>Reiht eine Einheit im Rathaus des lokalen Spielers ein — ueber die Befehlsqueue, wie die KI auch.</summary>
    private void QueueUnit(string unitDefinitionId)
    {
        if (World is null) return;

        foreach (Building building in World.Entities.Buildings)
        {
            if (building.OwnerId != _localPlayerId || !building.CanAdvanceAge) continue;

            World.Commands.Enqueue(new TrainUnitCommand
            {
                PlayerId = _localPlayerId,
                Building = building.Id,
                UnitDefinitionId = unitDefinitionId,
            });
            return;
        }
    }

    private void JumpToOwnBase()
    {
        if (World is null || _camera is null) return;

        foreach (Building building in World.Entities.Buildings)
        {
            if (building.OwnerId != _localPlayerId || !building.CanAdvanceAge) continue;
            _camera.JumpTo(building.Position);
            return;
        }
    }

    private void ReportState()
    {
        if (_match is null || World is null) return;

        GD.Print($"[Karte] {_match.Map.Grid.Width}x{_match.Map.Grid.Height} Kacheln, " +
                 $"{_match.Map.Decorations.Count} Streuobjekte, " +
                 $"{_match.Map.StartPositions.Count} Startplaetze.");

        GD.Print($"[Match] {World.Players.Count} Spieler, {World.Entities.Count} Entities, " +
                 $"{World.Systems.Count} Systeme, Tickrate {SimulationWorld.TicksPerSecond} Hz.");

        foreach (Player player in World.Players)
        {
            GD.Print($"  {player.Name}: Bev {player.Population}/{player.PopulationCap}, " +
                     $"Nahrung {player.GetResource(ResourceType.Food)}, " +
                     $"Holz {player.GetResource(ResourceType.Wood)}");
        }
    }

    private void SetupScreenshotMode()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--shot=")) continue;

            _screenshotPath = argument["--shot=".Length..];
            // Genug Frames, damit Schatten, SSAO und ein paar Sim-Ticks stehen.
            _framesUntilShot = 90;

            // Edge-Scrolling wuerde die Kamera wegziehen, weil der Cursor beim
            // automatisierten Lauf irgendwo am Rand steht.
            if (_camera is not null) _camera.EdgeScrollEnabled = false;
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
}
