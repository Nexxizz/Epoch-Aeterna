# Epoch Aeterna

Echtzeit-Strategiespiel im Stil von *Empire Earth*: Ressourcen sammeln, Basis bauen,
Armee aufstellen, durch Zeitalter aufsteigen.

Der Umsetzungsplan mit allen Arbeitspaketen steht in **[docs/MVP_PLAN.md](docs/MVP_PLAN.md)**.

---

## Toolchain

| Werkzeug | Version | Pfad |
|---|---|---|
| Godot .NET | 4.7.2 stable mono | `C:\Godot\Godot_v4.7.2-stable_mono_win64.exe` |
| .NET SDK | 8.0.424 | `C:\Program Files\dotnet` |
| Blender | 5.2 | `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` |
| Git LFS | 3.7.1 | — |

> ⚠️ Unter `C:\Program Files\Godot` liegt zusätzlich ein **Godot 4.4.1 ohne C#-Support**.
> Für dieses Projekt immer den Build aus `C:\Godot` verwenden.
> Erkennungsmerkmal: `--version` enthält `.mono.`, und neben dem Exe liegt ein `GodotSharp/`-Ordner.

## Bauen und Starten

C#-Assembly bauen:

```bash
dotnet build
```

Spiel starten (Editor):

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64.exe" --path .
```

Spiel direkt starten, ohne Editor:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64.exe" --path . scenes/Main.tscn
```

Selbsttest der Simulation ohne Fenster und ohne SceneTree (CI-tauglich, Exit-Code 0 = alles grün):

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . -- --verify
```

Screenshot ohne Interaktion aufnehmen (prüft Beleuchtung und Szenenaufbau, CI-tauglich):

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- "--shot=C:/temp/shot.png"
```

## Steuerung (Stand Phase 2)

**Kamera**

| Eingabe | Wirkung |
|---|---|
| Pfeiltasten / Bildschirmrand | Karte verschieben |
| Mittlere Maustaste ziehen | Karte greifen und ziehen |
| Mausrad | Zoom (die Neigung folgt mit) |
| `Q` / `E` | Drehen |
| `Pos1` | Sprung zur eigenen Basis |

**Einheiten**

| Eingabe | Wirkung |
|---|---|
| Linksklick | Einheit auswählen |
| Linksklick ziehen | Rahmenauswahl |
| Doppelklick | alle sichtbaren Einheiten desselben Typs |
| `Shift` + Klick | zur Auswahl hinzufügen / entfernen |
| Rechtsklick | Bewegungsbefehl |
| `Shift` + Rechtsklick | Befehl anhängen statt ersetzen |
| `S` | Stopp |
| `Strg` + `0`–`9` | Kontrollgruppe setzen |
| `0`–`9` | Kontrollgruppe abrufen |

**Sonstiges:** `F1` bildet einen Siedler aus, `F2` einen Späher, `Leertaste` pausiert, `ESC` beendet.

> `WASD` ist bewusst nicht belegt: Die Buchstabentasten bleiben für Einheitenbefehle frei
> (`A` Angriffsbewegung, `S` Stopp, `H` Halten), wie in Empire Earth und AoE.

## Blender-Assets bauen

Alle Modelle werden prozedural per `bpy`-Skript erzeugt (ab Phase 4), nicht von Hand modelliert.
Ausgeführt wird headless:

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_all.py
```

## Projektstruktur

```
src/
├─ Core/            Simulation — frei von Godot-Node-Abhaengigkeiten
│  ├─ Simulation/   World, Fixed-Tick-Loop, Command-Queue
│  ├─ Entities/     Entity, Unit, Building, Player
│  ├─ Systems/      Movement, Gathering, Combat, Production, Construction
│  ├─ Pathfinding/  Grid, A*, Flow-Field, lokale Ausweichbewegung
│  └─ Data/         Definitions-Loader (data-driven)
├─ Presentation/    Views, Kamera, Selektion, VFX, Fog-of-War
├─ UI/              HUD, Panels, Minimap
├─ AI/              Skirmish-Bot
└─ Game/            Bootstrap, Match-Setup, Siegbedingungen

data/               .tres-Definitionen (Einheiten, Gebaeude, Zeitalter)
assets/             Importierte Modelle, Texturen, Audio
scenes/             Szenen und Entity-Prefabs
blender/            .blend-Quelldateien (Git LFS)
tools/blender/      bpy-Generator- und Export-Skripte
docs/               Plan und Dokumentation
```

## Architektur-Grundregeln

1. **Simulation und Darstellung sind getrennt.** Die Sim läuft mit festem 20-Hz-Tick,
   die Darstellung interpoliert dazwischen. `src/Core/` kennt keine Views.
2. **Alle Spielaktionen laufen über die Command-Queue.** Auch die KI. Das hält
   Multiplayer, Replays und Savegames später offen.
3. **Data-driven:** Eine neue Einheit oder ein neues Gebäude soll *keinen* neuen
   C#-Code erfordern — nur eine `.tres`-Definition plus Modell.

## Git LFS

Binärformate (`.blend`, `.glb`, `.png`, `.ogg` …) laufen über Git LFS, siehe `.gitattributes`.
Nach dem Klonen einmalig:

```bash
git lfs install --local
```
