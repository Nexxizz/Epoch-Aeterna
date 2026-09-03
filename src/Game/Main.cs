using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;

namespace EpochAeterna.Game;

/// <summary>
/// Entry point. Assembles the definition database, the simulation and the display
/// while keeping them apart: the node knows the world, the world knows no node.
/// </summary>
public partial class Main : Node3D
{
    private readonly DefinitionDatabase _definitions = new();

    private Match? _match;
    private SimulationRunner? _runner;
    private RtsCamera? _camera;
    private SelectionController? _selection;
    private BuildPlacementController? _placement;
    private TerrainRenderer? _terrain;
    private DecorationRenderer? _decorations;
    private Node3D? _worldRoot;
    private GraphicsSettings? _graphics;

    private SimulationWorld? World => _match?.World;

    /// <summary>The locally controlled player. Set from the main menu in phase 6.</summary>
    private const int LocalPlayerId = 1;

    // "--shot=<path>" renders a few frames and writes a PNG — for visual checks and CI.
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

        SetupGraphics();
        StartMatch();
        SetupScreenshotMode();
    }

    /// <summary>
    /// Hooks the quality presets up to the scene's environment and sun.
    /// </summary>
    /// <remarks>
    /// Lives outside <see cref="StartMatch"/> because it belongs to the scene,
    /// not to a match — restarting should not reset the player's quality choice.
    /// </remarks>
    private void SetupGraphics()
    {
        var worldEnvironment = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
        var sun = GetNodeOrNull<DirectionalLight3D>("Sun");
        if (worldEnvironment is null || sun is null) return;

        _graphics = new GraphicsSettings { Name = "GraphicsSettings" };
        AddChild(_graphics);
        _graphics.Attach(worldEnvironment, sun);
    }

    // --- Setup -----------------------------------------------------------

    private void StartMatch()
    {
        // Everything world-related hangs under one node, so a restart only has to
        // throw away that subtree.
        _worldRoot?.QueueFree();
        _worldRoot = new Node3D { Name = "World" };
        AddChild(_worldRoot);

        MatchConfig config = MatchSetup.DefaultSkirmish();
        _match = MatchSetup.Build(config, _definitions);

        BuildTerrain();
        BuildSimulation();
        BuildControls();

        ReportState();
    }

    private void BuildTerrain()
    {
        if (_match is null || _worldRoot is null) return;

        _terrain = new TerrainRenderer { Name = "Terrain" };
        _worldRoot.AddChild(_terrain);
        _terrain.Build(_match.Map.Grid);

        _decorations = new DecorationRenderer { Name = "Decorations" };
        _worldRoot.AddChild(_decorations);
        _decorations.Build(_match.Map.Decorations, _match.Map.Grid);
    }

    private void BuildSimulation()
    {
        if (World is null || _worldRoot is null) return;

        _runner = new SimulationRunner { Name = "SimulationRunner" };
        _worldRoot.AddChild(_runner);
        _runner.Attach(World);
    }

    private void BuildControls()
    {
        if (_match is null || World is null || _runner is null || _worldRoot is null) return;

        var views = new ViewManager { Name = "ViewManager" };
        _worldRoot.AddChild(views);
        views.Attach(World, _runner);

        var projectiles = new ProjectileRenderer { Name = "ProjectileRenderer" };
        _worldRoot.AddChild(projectiles);
        projectiles.Attach(World, _match.Map.Grid);

        _camera = new RtsCamera { Name = "RtsCamera" };
        _worldRoot.AddChild(_camera);
        _camera.Attach(_match.Map.Grid, _match.Map.StartPositions[0]);

        _selection = new SelectionController { Name = "SelectionController" };
        _worldRoot.AddChild(_selection);
        _selection.Attach(World, _camera, views, LocalPlayerId);

        _placement = new BuildPlacementController { Name = "BuildPlacement" };
        _worldRoot.AddChild(_placement);
        _placement.Attach(World, _camera, _selection, LocalPlayerId);
        _selection.SetPlacement(_placement);

        Player? local = World.GetPlayer(LocalPlayerId);
        if (local is not null && _terrain is not null && _decorations is not null)
        {
            var fog = new FogOfWarRenderer { Name = "FogOfWar" };
            _worldRoot.AddChild(fog);
            fog.Attach(local, _match.Map.Grid, views, _terrain, _decorations);
        }

        var floating = new FloatingTextLayer { Name = "FloatingText" };
        _worldRoot.AddChild(floating);
        floating.Attach(World, _camera, LocalPlayerId);

        var overlay = new DebugOverlay { Name = "DebugOverlay" };
        _worldRoot.AddChild(overlay);
        overlay.Attach(World, _runner, _selection, _placement, LocalPlayerId);

        var endScreen = new MatchEndScreen { Name = "MatchEndScreen" };
        _worldRoot.AddChild(endScreen);
        endScreen.Attach(World, LocalPlayerId);
        endScreen.RestartRequested += StartMatch;
    }

    // --- Input -----------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (World is null || _runner is null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        switch (key.Keycode)
        {
            case Key.Escape:
                if (_placement is { IsPlacing: true }) _placement.Cancel();
                else GetTree().Quit();
                break;

            case Key.Space:
                _runner.TimeScale = _runner.IsPaused ? 1f : 0f;
                break;

            case Key.Equal or Key.Plus or Key.KpAdd:
                _runner.TimeScale = Mathf.Min(2f, Mathf.Max(0.5f, _runner.TimeScale) * 2f);
                break;

            case Key.Minus or Key.KpSubtract:
                _runner.TimeScale = Mathf.Max(0.5f, _runner.TimeScale * 0.5f);
                break;

            case Key.Home:
                JumpToOwnBase();
                break;

            // --- Training and advancing ---
            case Key.F1: QueueUnit(MatchSetup.SettlerId); break;
            case Key.F2: QueueUnit("unit_scout"); break;
            case Key.F3: QueueUnit("unit_spearman"); break;
            case Key.F4: AdvanceAge(); break;

            // --- Building ---
            case Key.B: _placement?.Begin("bld_house"); break;
            case Key.N: _placement?.Begin("bld_storehouse"); break;
            case Key.M: _placement?.Begin("bld_barracks"); break;
            case Key.K: _placement?.Begin("bld_farm"); break;
            case Key.T: _placement?.Begin("bld_tower"); break;
            case Key.R: _placement?.Begin("bld_range"); break;

            case Key.Delete: DemolishSelected(); break;

            case Key.F5:
                if (_graphics is not null) GD.Print($"[Graphics] preset: {_graphics.Cycle()}");
                break;
        }
    }

    /// <summary>Queues a unit — in the selected building, otherwise in the town centre.</summary>
    private void QueueUnit(string unitDefinitionId)
    {
        if (World is null) return;

        Building? target = _selection?.SelectedBuilding() ?? FindTownCenter();
        if (target is null) return;

        World.Commands.Enqueue(new TrainUnitCommand
        {
            PlayerId = LocalPlayerId,
            Building = target.Id,
            UnitDefinitionId = unitDefinitionId,
        });
    }

    private void AdvanceAge()
    {
        Building? townCenter = FindTownCenter();
        if (World is null || townCenter is null) return;

        World.Commands.Enqueue(new AdvanceAgeCommand
        {
            PlayerId = LocalPlayerId,
            Building = townCenter.Id,
        });
    }

    private void DemolishSelected()
    {
        Building? building = _selection?.SelectedBuilding();
        if (World is null || building is null) return;

        World.Commands.Enqueue(new DemolishCommand
        {
            PlayerId = LocalPlayerId,
            Building = building.Id,
        });
    }

    private void JumpToOwnBase()
    {
        Building? townCenter = FindTownCenter();
        if (townCenter is not null) _camera?.JumpTo(townCenter.Position);
    }

    private Building? FindTownCenter()
    {
        if (World is null) return null;

        foreach (Building building in World.Entities.Buildings)
        {
            if (building.OwnerId == LocalPlayerId && building.CanAdvanceAge && !building.IsUnderConstruction)
            {
                return building;
            }
        }
        return null;
    }

    // --- Diagnostics -----------------------------------------------------

    private void ReportState()
    {
        if (_match is null || World is null) return;

        GD.Print($"[Map] {_match.Map.Grid.Width}x{_match.Map.Grid.Height} tiles, " +
                 $"{_match.Map.ResourceSpots.Count} resource deposits, {_match.Map.Decorations.Count} decorations.");

        GD.Print($"[Match] {World.Players.Count} players, {World.Entities.Count} entities, " +
                 $"{World.Systems.Count} systems, tick rate {SimulationWorld.TicksPerSecond} Hz.");
    }

    private void SetupScreenshotMode()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (!argument.StartsWith("--shot=")) continue;

            _screenshotPath = argument["--shot=".Length..];
            // Enough frames for shadows, SSAO and a few sim ticks to settle.
            _framesUntilShot = 90;

            // Edge scrolling would drag the camera away, because during an automated
            // run the cursor sits somewhere near the edge.
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
            : $"[Screenshot] Failed: {error}");

        _screenshotPath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
