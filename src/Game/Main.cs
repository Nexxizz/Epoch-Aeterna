using Godot;
using EpochAeterna.Core.Data;
using EpochAeterna.Core.Entities;
using EpochAeterna.Core.Simulation;
using EpochAeterna.Presentation;
using EpochAeterna.UI;

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
    private DebugOverlay? _overlay;
    private MatchEndScreen? _endScreen;
    private Minimap? _minimap;
    private Node3D? _worldRoot;
    private GraphicsSettings? _graphics;

    // The shell outlives a match: settings, main menu, pause menu and options.
    private GameSettings? _settings;
    private MainMenu? _mainMenu;
    private PauseMenu? _pauseMenu;
    private OptionsMenu? _options;
    private bool _optionsFromMainMenu;

    /// <summary>Game speed before the pause menu opened, so resuming restores it.</summary>
    private float _speedBeforePause = 1f;

    private SimulationWorld? World => _match?.World;

    /// <summary>The locally controlled player.</summary>
    private const int LocalPlayerId = 1;

    // "--shot=<path>" renders a few frames and writes a PNG — for visual checks and CI.
    private string? _screenshotPath;
    private int _framesUntilShot;

    public override void _Ready()
    {
        GD.Print("=== Epoch Aeterna ===");
        GD.Print($"Godot {Engine.GetVersionInfo()["string"]}");

        _definitions.LoadAll();

        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify-presentation") >= 0)
        {
            AddChild(new PresentationSelfTest());
            return;
        }

        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--verify") >= 0)
        {
            GetTree().Quit(SelfTest.Run(_definitions));
            return;
        }

        SetupSettings();
        SetupGraphics();
        _settings?.AttachGraphics(_graphics);
        _settings?.Apply();
        BuildShell();

        // Automated runs go straight into a match — a menu would be in the way of
        // a screenshot.
        if (StartsWithoutMenu())
        {
            StartMatch();
            SetupScreenshotMode();
        }
        else
        {
            ShowMainMenu();
        }
    }

    // --- Shell -----------------------------------------------------------

    private void SetupSettings()
    {
        _settings = new GameSettings { Name = "GameSettings" };
        AddChild(_settings);
        _settings.LoadFromDisk();

        // The key bindings have to reach the InputMap before anything reads an action.
        _settings.Bindings.Register();
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

    private void BuildShell()
    {
        if (_settings is null) return;

        _options = new OptionsMenu { Name = "OptionsMenu" };
        AddChild(_options);
        _options.Attach(_settings);
        _options.Closed += OnOptionsClosed;

        _mainMenu = new MainMenu { Name = "MainMenu" };
        AddChild(_mainMenu);
        _mainMenu.StartRequested += StartMatch;
        _mainMenu.OptionsRequested += () => OpenOptions(fromMainMenu: true);
        _mainMenu.QuitRequested += Quit;

        _pauseMenu = new PauseMenu { Name = "PauseMenu" };
        AddChild(_pauseMenu);
        _pauseMenu.ResumeRequested += ClosePauseMenu;
        _pauseMenu.OptionsRequested += () => OpenOptions(fromMainMenu: false);
        _pauseMenu.RestartRequested += RestartMatch;
        _pauseMenu.MainMenuRequested += ReturnToMainMenu;
        _pauseMenu.QuitRequested += Quit;
    }

    private bool MenuIsOpen =>
        _mainMenu?.IsOpen == true || _pauseMenu?.IsOpen == true || _options?.IsOpen == true;

    private static bool StartsWithoutMenu()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--shot=") || argument == "--skip-menu") return true;
        }
        return false;
    }

    private void ShowMainMenu()
    {
        if (_mainMenu is null) return;
        _mainMenu.Visible = true;
        SetWorldInputEnabled(false);
    }

    private void OpenOptions(bool fromMainMenu)
    {
        _optionsFromMainMenu = fromMainMenu;
        if (fromMainMenu) _mainMenu!.Visible = false;
        else if (_pauseMenu is not null) _pauseMenu.Visible = false;

        _options?.Open();
    }

    private void OnOptionsClosed()
    {
        if (_optionsFromMainMenu || _match is null) ShowMainMenu();
        else if (_pauseMenu is not null) _pauseMenu.Visible = true;
    }

    private void OpenPauseMenu()
    {
        if (_pauseMenu is null || _runner is null || _match is null) return;

        _speedBeforePause = _runner.TimeScale;
        _runner.TimeScale = 0f;
        _pauseMenu.Visible = true;
        SetWorldInputEnabled(false);
    }

    private void ClosePauseMenu()
    {
        if (_pauseMenu is null) return;

        _pauseMenu.Visible = false;
        if (_runner is not null) _runner.TimeScale = _speedBeforePause > 0f ? _speedBeforePause : 1f;
        SetWorldInputEnabled(true);
    }

    private void RestartMatch()
    {
        if (_pauseMenu is not null) _pauseMenu.Visible = false;
        StartMatch();
    }

    private void ReturnToMainMenu()
    {
        if (_pauseMenu is not null) _pauseMenu.Visible = false;

        _settings?.AttachCamera(null);

        _worldRoot?.QueueFree();
        _worldRoot = null;
        _match = null;
        _runner = null;
        _camera = null;
        _selection = null;
        _placement = null;
        _overlay = null;
        _endScreen = null;
        _minimap = null;

        ShowMainMenu();
    }

    /// <summary>
    /// Silences camera and selection while a menu is on top. The menu backdrop already
    /// swallows the mouse, but the keyboard would otherwise still reach the map.
    /// </summary>
    private void SetWorldInputEnabled(bool enabled)
    {
        _camera?.SetProcess(enabled);
        _camera?.SetProcessUnhandledInput(enabled);
        _selection?.SetProcessUnhandledInput(enabled);
        _placement?.SetProcessUnhandledInput(enabled);
    }

    private void Quit() => GetTree().Quit();

    // --- Setup -----------------------------------------------------------

    private void StartMatch()
    {
        if (_mainMenu is not null) _mainMenu.Visible = false;

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
        BuildHud();

        SetWorldInputEnabled(true);
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
        _settings?.AttachCamera(_camera);

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
    }

    /// <summary>
    /// The HUD. Built after the controls, because every panel reads the selection.
    /// </summary>
    private void BuildHud()
    {
        if (_match is null || World is null || _runner is null || _worldRoot is null ||
            _camera is null || _selection is null || _placement is null || _settings is null)
        {
            return;
        }

        var resourceBar = new ResourceBar { Name = "ResourceBar" };
        _worldRoot.AddChild(resourceBar);
        resourceBar.Attach(World, LocalPlayerId);

        _minimap = new Minimap { Name = "Minimap" };
        _worldRoot.AddChild(_minimap);
        _minimap.Attach(World, _match.Map.Grid, _camera, LocalPlayerId);

        var selectionPanel = new SelectionPanel { Name = "SelectionPanel" };
        _worldRoot.AddChild(selectionPanel);
        selectionPanel.Attach(World, _selection);

        var actionBar = new ActionBar { Name = "ActionBar" };
        _worldRoot.AddChild(actionBar);
        actionBar.Attach(World, _selection, _settings.Bindings, LocalPlayerId);

        var notifications = new NotificationFeed { Name = "Notifications" };
        _worldRoot.AddChild(notifications);
        notifications.Attach(World, LocalPlayerId, _minimap);

        var buildMenu = new BuildMenu { Name = "BuildMenu" };
        _worldRoot.AddChild(buildMenu);
        buildMenu.Attach(World, _selection, _placement, LocalPlayerId, _settings.Bindings, notifications);

        var trainingMenu = new TrainingMenu { Name = "TrainingMenu" };
        _worldRoot.AddChild(trainingMenu);
        trainingMenu.Attach(World, _selection, LocalPlayerId, notifications);

        _overlay = new DebugOverlay { Name = "DebugOverlay" };
        _worldRoot.AddChild(_overlay);
        _overlay.Attach(World, _runner, _selection, _placement, LocalPlayerId);

        _endScreen = new MatchEndScreen { Name = "MatchEndScreen" };
        _worldRoot.AddChild(_endScreen);
        _endScreen.Attach(World, LocalPlayerId);
        _endScreen.RestartRequested += StartMatch;
    }

    // --- Input -----------------------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            HandleEscape();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (MenuIsOpen || World is null || _runner is null) return;
        if (@event is not InputEventKey { Pressed: true, Echo: false }) return;

        if (@event.IsActionPressed("game_pause")) _runner.TimeScale = _runner.IsPaused ? 1f : 0f;
        else if (@event.IsActionPressed("game_speed_up"))
            _runner.TimeScale = Mathf.Min(2f, Mathf.Max(0.5f, _runner.TimeScale) * 2f);
        else if (@event.IsActionPressed("game_speed_down"))
            _runner.TimeScale = Mathf.Max(0.5f, _runner.TimeScale * 0.5f);
        else if (@event.IsActionPressed("camera_home")) JumpToOwnBase();

        else if (@event.IsActionPressed("train_settler")) QueueUnit(MatchSetup.SettlerId);
        else if (@event.IsActionPressed("train_scout")) QueueUnit("unit_scout");
        else if (@event.IsActionPressed("train_spearman")) QueueUnit("unit_spearman");
        else if (@event.IsActionPressed("advance_age")) AdvanceAge();

        else if (@event.IsActionPressed("build_house")) _placement?.Begin("bld_house");
        else if (@event.IsActionPressed("build_storehouse")) _placement?.Begin("bld_storehouse");
        else if (@event.IsActionPressed("build_barracks")) _placement?.Begin("bld_barracks");
        else if (@event.IsActionPressed("build_farm")) _placement?.Begin("bld_farm");
        else if (@event.IsActionPressed("build_tower")) _placement?.Begin("bld_tower");
        else if (@event.IsActionPressed("build_range")) _placement?.Begin("bld_range");
        else if (@event.IsActionPressed("cmd_demolish")) DemolishSelected();

        else if (@event.IsActionPressed("cycle_graphics")) CycleGraphics();
        else if (@event.IsActionPressed("toggle_debug")) _overlay?.Toggle();
        else return;

        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// Escape unwinds one layer at a time: options, pause menu, placement preview,
    /// end screen — and only in an untouched match does it open the pause menu.
    /// </summary>
    private void HandleEscape()
    {
        if (_options?.IsOpen == true) { _options.Close(); return; }
        if (_pauseMenu?.IsOpen == true) { ClosePauseMenu(); return; }
        if (_mainMenu?.IsOpen == true) { Quit(); return; }
        if (_placement is { IsPlacing: true }) { _placement.Cancel(); return; }
        if (_endScreen?.Visible == true) { ReturnToMainMenu(); return; }

        OpenPauseMenu();
    }

    private void CycleGraphics()
    {
        if (_graphics is null) return;

        GraphicsPreset preset = _graphics.Cycle();
        GD.Print($"[Graphics] preset: {preset}");

        if (_settings is null) return;
        _settings.Graphics = preset;
        _settings.SaveToDisk();
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
