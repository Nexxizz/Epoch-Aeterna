# Epoch Aeterna

A real-time strategy game inspired by *Empire Earth*: gather resources, build a base,
raise an army, and advance through the ages.

The detailed implementation plan is available in **[docs/MVP_PLAN.md](docs/MVP_PLAN.md)**.

---

## Toolchain

| Tool | Version | Path |
|---|---|---|
| Godot .NET | 4.7.2 stable mono | `C:\Godot\Godot_v4.7.2-stable_mono_win64.exe` |
| .NET SDK | 8.0.424 | `C:\Program Files\dotnet` |
| Blender | 5.2 | `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` |
| Git LFS | 3.7.1 | — |

> Always use the build in `C:\Godot` for this project.
> You can identify it by `.mono.` in the `--version` output and a `GodotSharp/`
> directory next to the executable.

## Build and run

Build the C# assembly:

```bash
dotnet build
```

Start the game in the editor:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64.exe" --path .
```

Start the game directly without the editor:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64.exe" --path . scenes/Main.tscn
```

Run the simulation self-test without a window or SceneTree (CI-friendly; exit code
0 means all checks passed):

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . -- --verify
```

Capture a screenshot without interaction (checks lighting and scene setup and is
CI-friendly):

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- "--shot=C:/temp/shot.png"
```

## Controls (Phase 3)

**Camera**

| Input | Action |
|---|---|
| Arrow keys / screen edge | Pan across the map |
| Drag middle mouse button | Grab and drag the map |
| Mouse wheel | Zoom; camera pitch adjusts with it |
| `Q` / `E` | Rotate |
| `Home` | Jump to your base |

**Selection and commands**

| Input | Action |
|---|---|
| Left click | Select |
| Drag left mouse button | Box-select; military units take priority |
| Double-click | Select all visible units of the same type |
| `Shift` + click | Add to or remove from the selection |
| **Right click** | Context-sensitive: ground → move, resource → gather, own construction site → build, enemy → attack |
| `Shift` + right click | Queue a command instead of replacing the current one |
| `A` | Attack-move to the cursor position |
| `S` | Stop · `H` Hold position · `D` Defensive stance |
| `Ctrl` + `0`–`9` / `0`–`9` | Assign / recall a control group |

**Construction and training**

| Input | Action |
|---|---|
| `B` House · `N` Storehouse · `M` Barracks | Start building placement |
| `K` Farm · `T` Watchtower · `R` Archery Range | Start building placement |
| Left click / `Shift` + left click | Place a construction site / place another one |
| Right click or `Esc` | Cancel placement |
| `Delete` | Demolish the selected building |
| `F1` / `F2` / `F3` | Train Settler / Scout / Spearman |
| `F4` | Start advancing to the next age |

**Other:** `Space` pauses the game, `+` / `-` change the speed (0.5× to 2×), `F5` cycles
the graphics preset (Low / Medium / High), and `Esc` quits.

> `WASD` is deliberately unassigned because the letter keys are used for unit
> commands (`A` Attack Move, `S` Stop, `H` Hold), as in Empire Earth and Age of
> Empires.

## Build Blender assets

All models are generated procedurally by `bpy` scripts rather than modelled by hand.
That makes the source of truth a diffable script, turns a change of proportions into a
one-line edit that regenerates everything consistently, and means nobody has to open
Blender to rebuild the game's art.

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_all.py
```

Single assets:

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_all.py -- unit_settler
```

Then import into Godot:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --editor --quit --path .
```

The settler generator also saves `blender/unit_settler.blend`, with an editable
rig and eleven actions. Native sources are excluded from Godot's importer; the
game uses the exported GLB. See [the settler asset notes](docs/SETTLER_ASSET.md)
for animation previews, timings and the presentation test.

### Conventions

| Rule | Value |
|---|---|
| Scale | 1 Blender unit = 1 metre |
| Facing | -Y in Blender (becomes Godot's -Z) |
| Origin | centre of the footprint, lowest point at z = 0 |
| Export | glTF 2.0 binary (`.glb`), "+Y Up", animations as NLA strips |
| Naming | materials `MAT_*`, armatures `SK_*` |
| Team colour | material **`MAT_teamcolor`** — `ViewManager` replaces its albedo with the player's colour |
| File name | matches the definition id (`unit_settler.glb` belongs to `unit_settler.tres`) |

### Layout

| File | Purpose |
|---|---|
| `lib_scene.py` | scene reset and export — the conventions live here, in one place |
| `lib_mesh.py` | primitives, bevel/smooth pass, joining, grounding an asset |
| `lib_material.py` | PBR palette and the team-colour material |
| `lib_rig.py` | shared humanoid armature, automatic weights |
| `lib_anim.py` | animation clips written as tables of poses |
| `assets/*.py` | one script per asset |
| `build_all.py` | entry point |

A model is used automatically once its `.tres` points `ModelScene` at the `.glb`.
Without one, `ViewManager` falls back to placeholder geometry — both work side by side.

## Project structure

```text
src/
├─ Core/            Simulation — no Godot Node dependencies
│  ├─ Simulation/   World, fixed-tick loop, command queue
│  ├─ Entities/     Entity, Unit, Building, Player
│  ├─ Systems/      Movement, gathering, combat, production, construction
│  ├─ Pathfinding/  Grid, A*, flow fields, local avoidance
│  └─ Data/         Definition loader (data-driven)
├─ Presentation/    Views, camera, selection, VFX, fog of war
├─ UI/              HUD, panels, minimap
├─ AI/              Skirmish bot
└─ Game/            Bootstrap, match setup, victory conditions

data/               `.tres` definitions for units, buildings, and ages
assets/             Imported models, textures, and audio
scenes/             Scenes and entity prefabs
blender/            `.blend` source files managed through Git LFS
tools/blender/      `bpy` generator and export scripts
docs/               Plans and documentation
```

## Architectural principles

1. **Simulation and presentation are separate.** The simulation runs at a fixed
   20 Hz tick rate while the presentation interpolates between ticks. `src/Core/`
   does not know about views.
2. **Every game action goes through the command queue**, including AI actions. This
   keeps future multiplayer, replays, and save games possible.
3. **Data-driven:** adding a unit or building should require no new C# code—only a
   `.tres` definition and a model.

## Git LFS

Binary formats (`.blend`, `.glb`, `.png`, `.ogg`, and others) are managed through
Git LFS; see `.gitattributes`. Run this once after cloning:

```bash
git lfs install --local
```
