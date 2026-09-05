using System;
using System.Collections.Generic;
using Godot;

namespace EpochAeterna.UI;

/// <summary>One rebindable command.</summary>
public sealed class KeyBinding
{
    /// <summary>Name of the InputMap action. Stable — it is what the game code asks for.</summary>
    public required string Action { get; init; }

    public required string DisplayName { get; init; }

    /// <summary>Heading it appears under in the options.</summary>
    public required string Category { get; init; }

    public required Key Default { get; init; }

    /// <summary>Fixed second key that is never rebound — the numeric keypad, for instance.</summary>
    public Key Secondary { get; init; } = Key.None;

    public Key Current { get; set; }
}

/// <summary>
/// Every keyboard command of the game, as InputMap actions.
/// </summary>
/// <remarks>
/// The game never asks for a concrete key, only for an action
/// (<c>event.IsActionPressed("build_house")</c>). Rebinding therefore only has to
/// rewrite the InputMap — no game code knows which key sits behind an action.
///
/// Bindings are stored as *physical* keycodes, so the layout stays put on a QWERTZ
/// keyboard as well: B is where B is printed.
/// </remarks>
public sealed class KeyBindings
{
    private readonly List<KeyBinding> _bindings = new()
    {
        new() { Action = "camera_up",           DisplayName = "Kamera nach oben",     Category = "Kamera", Default = Key.Up },
        new() { Action = "camera_down",         DisplayName = "Kamera nach unten",    Category = "Kamera", Default = Key.Down },
        new() { Action = "camera_left",         DisplayName = "Kamera nach links",    Category = "Kamera", Default = Key.Left },
        new() { Action = "camera_right",        DisplayName = "Kamera nach rechts",   Category = "Kamera", Default = Key.Right },
        new() { Action = "camera_rotate_left",  DisplayName = "Kamera drehen links",  Category = "Kamera", Default = Key.Q },
        new() { Action = "camera_rotate_right", DisplayName = "Kamera drehen rechts", Category = "Kamera", Default = Key.E },
        new() { Action = "camera_home",         DisplayName = "Zur eigenen Basis",    Category = "Kamera", Default = Key.Home },

        new() { Action = "cmd_attack_move", DisplayName = "Angriffsbewegung",  Category = "Befehle", Default = Key.A },
        new() { Action = "cmd_stop",        DisplayName = "Stopp",             Category = "Befehle", Default = Key.S },
        new() { Action = "cmd_hold",        DisplayName = "Stellung halten",   Category = "Befehle", Default = Key.H },
        new() { Action = "cmd_defensive",   DisplayName = "Defensive Haltung", Category = "Befehle", Default = Key.D },
        new() { Action = "cmd_demolish",    DisplayName = "Gebäude abreißen",  Category = "Befehle", Default = Key.Delete },

        new() { Action = "build_house",      DisplayName = "Haus",        Category = "Bauen", Default = Key.B },
        new() { Action = "build_storehouse", DisplayName = "Lagerhaus",   Category = "Bauen", Default = Key.N },
        new() { Action = "build_farm",       DisplayName = "Farm",        Category = "Bauen", Default = Key.K },
        new() { Action = "build_barracks",   DisplayName = "Kaserne",     Category = "Bauen", Default = Key.M },
        new() { Action = "build_range",      DisplayName = "Schießstand", Category = "Bauen", Default = Key.R },
        new() { Action = "build_tower",      DisplayName = "Wachturm",    Category = "Bauen", Default = Key.T },

        new() { Action = "train_settler",  DisplayName = "Siedler ausbilden",      Category = "Ausbildung", Default = Key.F1 },
        new() { Action = "train_scout",    DisplayName = "Späher ausbilden",       Category = "Ausbildung", Default = Key.F2 },
        new() { Action = "train_spearman", DisplayName = "Speerkämpfer ausbilden", Category = "Ausbildung", Default = Key.F3 },
        new() { Action = "advance_age",    DisplayName = "Zeitalter aufsteigen",   Category = "Ausbildung", Default = Key.F4 },

        new() { Action = "game_pause",      DisplayName = "Pausieren",            Category = "Spiel", Default = Key.Space },
        new() { Action = "game_speed_up",   DisplayName = "Schneller",            Category = "Spiel", Default = Key.Equal, Secondary = Key.KpAdd },
        new() { Action = "game_speed_down", DisplayName = "Langsamer",            Category = "Spiel", Default = Key.Minus, Secondary = Key.KpSubtract },
        new() { Action = "cycle_graphics",  DisplayName = "Grafikstufe wechseln", Category = "Spiel", Default = Key.F5 },
        new() { Action = "toggle_debug",    DisplayName = "Diagnose-Anzeige",     Category = "Spiel", Default = Key.F10 },
    };

    public IReadOnlyList<KeyBinding> All => _bindings;

    /// <summary>Raised after a rebind, so open menus can relabel their buttons.</summary>
    public event Action? Changed;

    public KeyBindings()
    {
        foreach (KeyBinding binding in _bindings) binding.Current = binding.Default;
    }

    /// <summary>
    /// Writes every binding into the InputMap, replacing whatever the project
    /// settings brought along.
    /// </summary>
    public void Register()
    {
        foreach (KeyBinding binding in _bindings)
        {
            if (!InputMap.HasAction(binding.Action)) InputMap.AddAction(binding.Action);
            InputMap.ActionEraseEvents(binding.Action);

            if (binding.Current != Key.None)
            {
                InputMap.ActionAddEvent(binding.Action, new InputEventKey { PhysicalKeycode = binding.Current });
            }
            if (binding.Secondary != Key.None)
            {
                InputMap.ActionAddEvent(binding.Action, new InputEventKey { PhysicalKeycode = binding.Secondary });
            }
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Assigns a key. A key can only serve one command, so the previous owner
    /// loses it rather than both firing at once.
    /// </summary>
    public void Rebind(KeyBinding target, Key key)
    {
        if (key == Key.None || key == Key.Escape) return;

        foreach (KeyBinding binding in _bindings)
        {
            if (binding != target && binding.Current == key) binding.Current = Key.None;
        }

        target.Current = key;
        Register();
    }

    public void ResetToDefaults()
    {
        foreach (KeyBinding binding in _bindings) binding.Current = binding.Default;
        Register();
    }

    public KeyBinding? Find(string action)
    {
        foreach (KeyBinding binding in _bindings)
        {
            if (binding.Action == action) return binding;
        }
        return null;
    }

    /// <summary>Readable key name for tooltips and menus.</summary>
    public string Label(string action)
    {
        KeyBinding? binding = Find(action);
        return binding is null || binding.Current == Key.None
            ? "Nicht belegt"
            : OS.GetKeycodeString(binding.Current);
    }

    /// <summary>Short form for a button caption: " (B)". Empty when unbound.</summary>
    public string Shortcut(string action)
    {
        KeyBinding? binding = Find(action);
        return binding is null || binding.Current == Key.None
            ? string.Empty
            : $" ({OS.GetKeycodeString(binding.Current)})";
    }

    // --- Persistence -----------------------------------------------------

    public void Save(ConfigFile file, string section)
    {
        foreach (KeyBinding binding in _bindings) file.SetValue(section, binding.Action, (int)binding.Current);
    }

    public void Load(ConfigFile file, string section)
    {
        foreach (KeyBinding binding in _bindings)
        {
            if (!file.HasSectionKey(section, binding.Action)) continue;
            binding.Current = (Key)(int)file.GetValue(section, binding.Action, (int)binding.Default);
        }
    }

    /// <summary>The key an event carries, preferring the physical one.</summary>
    public static Key KeyOf(InputEventKey key) =>
        key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
}
