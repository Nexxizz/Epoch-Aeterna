# Epoch Aeterna — MVP-Plan

> Echtzeit-Strategiespiel im Stil von *Empire Earth*: Ressourcen sammeln, Basis bauen,
> Armee aufstellen, durch Zeitalter aufsteigen.
> **Engine:** Godot 4.7.2 (.NET / C#, `net8.0`) · **Assets:** Blender 5.2 (prozedural via `bpy`-Skripte)

**Ziel des MVP:** Ein spielbares 1v1-Skirmish gegen eine einfache KI auf einer Karte —
Ressourcen sammeln → Basis bauen → Zeitalter aufsteigen → Armee bauen → Gegner besiegen.
Alles darüber hinaus ist Backlog. Die Architektur ist aber von Anfang an auf Erweiterung
ausgelegt (data-driven, Sim/View getrennt).

---

## Legende

- `[ ]` offen · `[x]` erledigt
- 🔴 Blocker für MVP · 🟡 wichtig · 🟢 Nice-to-have (kann ins Backlog rutschen)

---

## Phase 0 — Toolchain & Projekt-Setup 🔴

Toolchain ist **vollständig vorhanden**, nichts muss installiert werden:

| Werkzeug | Version | Pfad |
|---|---|---|
| Godot .NET | 4.7.2 stable mono | `C:\Godot\Godot_v4.7.2-stable_mono_win64.exe` |
| .NET SDK | 8.0.424 (Runtime 8.0.30) | `C:\Program Files\dotnet` |
| Blender | 5.2 | `C:\Program Files\Blender Foundation\Blender 5.2` |

Godot 4.7.2 erwartet `net8.0` (`rollForward: LatestMajor`) — das installierte SDK 8.0.424 passt exakt.

> ⚠️ In `C:\Program Files\Godot` liegt zusätzlich ein **Godot 4.4.1 ohne** C#-Support.
> Nicht verwechseln — für dieses Projekt immer `C:\Godot\...mono...` verwenden.
> Erkennungsmerkmal: der Versionsstring enthält `.mono.`, und neben dem Exe liegt ein `GodotSharp/`-Ordner.

- [ ] Godot-Editor: externen Editor auf VS Code / Rider setzen — *offen, reine Geschmacksfrage; aktuell `dotnet/editor/external_editor = 0` (Godot-intern)*
- [x] Godot-Projekt `EpochAeterna` in `C:\Projects\Epoch-Aeterna` anlegen, Renderer **Forward+** (nötig für SDFGI/SSAO/moderne Beleuchtung)
- [x] C#-Solution erzeugen, TargetFramework `net8.0`, `Godot.NET.Sdk/4.7.2`
- [x] Verifizieren: `dotnet build` läuft durch und ein `Node3D` mit angehängtem C#-Skript startet
- [x] `.gitignore` für Godot + .NET (`.godot/`, `bin/`, `obj/`, `*.blend1`, `.mono/`)
- [x] `.gitattributes` mit Git-LFS für `*.blend`, `*.glb`, `*.png`, `*.exr`, `*.ogg` (+ `git lfs install --local`)
- [x] Blender-Pfad in den Godot-Editor-Settings — war bereits gesetzt: `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`
- [x] Erster Commit: Projekt startet ohne Fehler
- [x] `README.md` mit Build- und Run-Anleitung

**Verifikation Phase 0** (alle Befehle fehlerfrei):

| Prüfung | Ergebnis |
|---|---|
| `dotnet build` | 0 Warnungen, 0 Fehler, 6,0 s |
| Headless-Import | `reimport DONE`, keine Fehler |
| Headless-Lauf | gibt `Bootstrap OK`, Renderer `forward_plus` aus |

### Ordnerstruktur festlegen

- [x] Struktur anlegen und committen:

```
EpochAeterna/
├─ project.godot
├─ EpochAeterna.sln / .csproj
├─ src/
│  ├─ Core/            # Simulation — möglichst frei von Godot-Node-Abhängigkeiten
│  │  ├─ Simulation/   # World, Fixed-Tick-Loop, Command-Queue
│  │  ├─ Entities/     # Entity, Unit, Building, Player
│  │  ├─ Systems/      # Movement, Gathering, Combat, Production, Construction
│  │  ├─ Pathfinding/  # Grid, A*, Flow-Field, lokale Ausweichbewegung
│  │  └─ Data/         # Definitions-Loader (data-driven)
│  ├─ Presentation/    # Views, Kamera, Selektion, VFX, Fog-of-War-Rendering
│  ├─ UI/              # HUD, Panels, Minimap
│  ├─ AI/              # Skirmish-Bot
│  └─ Game/            # Bootstrap, Match-Setup, Sieg-/Niederlagebedingungen
├─ data/               # .tres / .json Definitionen (Einheiten, Gebäude, Zeitalter)
├─ assets/
│  ├─ models/{buildings,units,props,nature}
│  ├─ materials/  textures/  audio/  icons/
├─ scenes/             # Main, Match, UI-Szenen, Entity-Prefabs
├─ blender/            # .blend Quelldateien (LFS)
├─ tools/blender/      # bpy-Generator- und Batch-Export-Skripte
└─ docs/
```

---

## Phase 1 — Architektur-Grundgerüst 🔴

> Das Fundament, auf dem alles Weitere wächst. Hier wird entschieden, wie leicht das Spiel
> später erweiterbar ist. Nicht abkürzen.

### 1.1 Simulation / Presentation-Trennung

- [x] `SimulationWorld` mit **fixem Tick** (20 Hz), getrennt vom Godot-Frame-Rendering — `SimulationRunner` sammelt Frame-Zeit, deckelt bei 5 Ticks/Frame gegen die Todesspirale
- [x] `_Process` interpoliert Views zwischen Sim-Ticks — jede Entity hält `PreviousPosition`/`PreviousRotation`, `EntityView` blendet mit `InterpolationAlpha`
- [x] Alle Spielaktionen laufen über eine **Command-Queue** — implementiert: `Move`, `Stop`, `TrainUnit`, `CancelTraining`, `SetRallyPoint`
  - [ ] `Attack`, `Build`, `Gather` folgen zusammen mit ihren Systemen in Phase 3 — bewusst noch nicht als leere Hüllen angelegt
- [x] `Entity`-Basis mit stabiler `EntityId` (int), zentrales `EntityRegistry` (gepufferte Adds/Removes, Flush am Tick-Ende)
- [x] `EntityView`-Basis: Godot-Node, der einer `EntityId` folgt; die Sim kennt Views nicht
- [x] Event-Bus (`GameEvents`) für Sim → UI / VFX / Audio — `EntitySpawned`, `EntityRemoved`, `EntityDamaged`, `ProductionQueued`, `ProductionCompleted`; Ressourcen-, Bevölkerungs- und Zeitalter-Events sitzen auf `Player`

### 1.2 Data-driven Definitionen

- [x] `UnitDefinition`, `BuildingDefinition`, `AgeDefinition` als Godot-`Resource` (`.tres`), im Editor bearbeitbar
  - Statt einer `ResourceDefinition` gibt es `ResourceType` (Enum) + `ResourceSet` (Resource) — Ressourcen brauchen keine eigenen Bauplaene, nur Mengen
  - [ ] `TechDefinition` kommt mit dem Technologiebaum; aktuell würde sie nichts konsumieren
- [x] Felder: Kosten, Bauzeit, HP, Rüstung, Reichweite, Angriffsrate, Tempo, Sichtweite, Bevölkerungskosten, Grundfläche, benötigtes Zeitalter, Voraussetzungs-Gebäude, Modellpfad, Icon
  - [ ] Schadenstyp und Konter-Matrix folgen mit dem Kampfsystem (Phase 3.4)
- [x] `DefinitionDatabase` lädt alle `.tres` rekursiv aus `res://data/`, Zugriff per String-ID, behandelt `.remap` im Export
- [x] **Regel etabliert:** neue Einheit/neues Gebäude = neue `.tres`, kein C#-Code

### 1.3 Spieler & Ressourcen

- [x] `Player`: ID, Fraktionsfarbe, Ressourcenkonto, Zeitalter, Bevölkerung/Limit, Team
- [x] Ressourcen im MVP: **Nahrung, Holz, Stein, Gold** — eine weitere hinzuzufügen kostet 3 Zeilen (Enum, `ResourceSet`-Feld, Indexer-Zeile)
- [x] Bevölkerungslimit über Häuser, Hard-Cap 200 — pro Tick aus dem Entity-Bestand neu abgeleitet, kann daher nicht driften
- [x] Fraktionsfarbe: ein geteiltes Material pro Spieler
  - [ ] Team-Color-Maske im Texturkanal per Shader — braucht die echten Modelle, kommt mit Phase 4/5

**Verifikation Phase 1** — `SelfTest.cs` spielt die Simulation ohne SceneTree durch:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . -- --verify
```

| Prüfung | Ergebnis |
|---|---|
| `dotnet build` | 0 Warnungen, 0 Fehler |
| Selbsttest (30 Prüfungen) | alle bestanden, Exit-Code 0 |
| Fenstermodus | Vulkan 1.3.260 Forward+, keine Laufzeitfehler |

> Dass der Selbsttest **ohne SceneTree** läuft, ist der eigentliche Nachweis: Die Trennung
> zwischen Simulation und Darstellung ist real und nicht nur so benannt.

---

## Phase 2 — Welt, Kamera, Steuerung 🔴

### 2.1 Terrain & Karte

- [ ] Grid-basierte Karte (MVP: 128×128 Kacheln, 1 Kachel = 2 m)
- [ ] Terrain-Mesh aus Heightmap, in Chunks (16×16) für Culling
- [ ] Terrain-Shader: 4-fach Textur-Splatting (Gras / Erde / Fels / Sand), PBR, Triplanar an Steilhängen
- [ ] Navigations-Grid: pro Kachel begehbar/blockiert, Höhenwert, Belegung durch Gebäude
- [ ] Statische Deko per `MultiMeshInstance3D` (Bäume, Felsen, Gras) — Performance
- [ ] Test-Karte "Ebene der Anfänge": 2 Startpositionen, Wald, Steinbruch, Goldader, Beerenbüsche, leichte Hügel

### 2.2 RTS-Kamera

- [ ] WASD- / Pfeiltasten-Pan, Edge-Scrolling, Mittelklick-Drag-Pan
- [ ] Zoom (Mausrad) mit gekoppelter Neigung (nah = flacher, fern = steiler)
- [ ] Q/E-Rotation um die Y-Achse
- [ ] Kamera-Grenzen am Kartenrand, Höhe folgt dem Terrain
- [ ] Sprung zur Minimap-Position bzw. zum letzten Ereignis (Leertaste)

### 2.3 Selektion & Befehle

- [ ] Linksklick-Einzelselektion (Raycast auf Kollisionsformen)
- [ ] Box-Selektion mit Rahmen-Overlay, Priorisierung Militär vor Zivil
- [ ] Doppelklick = alle sichtbaren Einheiten desselben Typs
- [ ] Shift = zur Auswahl hinzufügen / entfernen
- [ ] Kontrollgruppen 0–9 (`Strg+Zahl` setzen, `Zahl` abrufen, Doppeltipp = hinspringen)
- [ ] Rechtsklick = kontextsensitiver Standardbefehl (Boden → Bewegen, Gegner → Angriff, Ressource → Sammeln, eigene Baustelle → Bauen)
- [ ] Befehls-Modifier: `A` Angriffsbewegung, `S` Stopp, `H` Halten
- [ ] Shift-Queue für mehrere Befehle hintereinander
- [ ] Selektionsring und Ziel-Marker als Feedback am Boden

### 2.4 Pathfinding

- [ ] A* auf dem Nav-Grid (mit Kachel-Clearance für große Einheiten)
- [ ] Pfadglättung (String-Pulling / Line-of-Sight-Kürzung)
- [ ] Lokale Ausweichbewegung zwischen Einheiten (einfaches Steering / Push-Apart)
- [ ] Gruppenbewegung: Formation-Offsets, gemeinsame Zielgeschwindigkeit
- [ ] Pfad-Neuberechnung bei blockiertem Weg (z. B. neu gebautes Gebäude), Budget pro Tick (max. N Pfade)
- [ ] 🟢 Flow-Field für große Gruppen (> 30 Einheiten) — kann nach dem MVP kommen

---

## Phase 3 — Kernspielmechanik 🔴

### 3.1 Ressourcen-Wirtschaft

- [ ] Ressourcenknoten als Entities mit endlichem Vorrat (Baum, Steinbruch, Goldader, Beerenbusch)
- [ ] Sammel-Loop: hingehen → sammeln (Rate/s, Traglimit) → zur nächsten Abgabestelle → abliefern → zurück
- [ ] Automatische Suche des nächsten gleichartigen Knotens, wenn der aktuelle erschöpft ist
- [ ] Farm als erneuerbare Nahrungsquelle (endlicher Vorrat, wieder bebaubar)
- [ ] Abgabestellen: Rathaus und Lagerhaus
- [ ] Visuelles Feedback: Baum fällt bzw. schrumpft, Ressourcenzahl steigt als Floating-Text

### 3.2 Bauen

- [ ] Bauplatzierungs-Modus: Geistermodell folgt dem Cursor, rastet aufs Grid, grün/rot je nach Gültigkeit
- [ ] Prüfung: Terrain flach genug, keine Überlappung, Ressourcen vorhanden, Zeitalter/Voraussetzung erfüllt
- [ ] Baustellen-Entity: Ressourcen werden sofort abgezogen, HP wachsen mit dem Baufortschritt
- [ ] Mehrere Siedler an einer Baustelle = schnellerer Bau (mit abnehmendem Ertrag)
- [ ] **3 sichtbare Baustufen** (Fundament → Rohbau → fertig), Umschaltung bei 33 % / 66 % / 100 %
- [ ] Abriss eigener Gebäude (mit Teilrückerstattung)
- [ ] 🟢 Reparatur beschädigter Gebäude

### 3.3 Produktion & Bevölkerung

- [ ] Produktionswarteschlange pro Gebäude (max. 5 sichtbar), Fortschrittsbalken
- [ ] Abbruch mit Rückerstattung
- [ ] Sammelpunkt (Rally Point) setzbar, sichtbare Flagge
- [ ] Bevölkerungsprüfung blockiert Produktion bei vollem Limit (mit UI-Warnung)

### 3.4 Kampf

- [ ] HP, Rüstung, Schadenstyp (Hieb / Stich / Distanz / Belagerung), Konter-Matrix in den Daten
- [ ] Nahkampf: anlaufen → Angriffsanimation → Treffer im Animation-Event-Frame
- [ ] Fernkampf: Projektil-Entity mit Flugzeit und Bogenbahn, Treffer beim Aufschlag
- [ ] Aggro-/Zielsuche: Einheiten greifen Feinde im Sichtradius automatisch an (Haltung: Aggressiv / Defensiv / Halten)
- [ ] Tod: Todesanimation → Leiche bleibt X s → Entfernung; Gebäude → Trümmer-Mesh
- [ ] Gebäude greifen an: Wachturm mit Pfeilen
- [ ] Trefferfeedback: Aufblitzen, Partikel, Sound, HP-Balken über beschädigten Einheiten

### 3.5 Zeitalter-Aufstieg

- [ ] `AgeDefinition`-Kette; MVP: **Zeitalter 1 "Steinzeit" → Zeitalter 2 "Kupferzeit"**
- [ ] Aufstieg im Rathaus: Kosten + Forschungszeit + Voraussetzung (z. B. 2 verschiedene Zeitalter-1-Gebäude)
- [ ] Beim Aufstieg: neue Einheiten und Gebäude freigeschaltet, Basiswerte-Boni
- [ ] 🟡 Gebäudemodelle wechseln beim Aufstieg das Aussehen (Zeitalter-Varianten) — im MVP mindestens beim Rathaus
- [ ] Sichtbares Feedback: Fanfare, Bildschirmmeldung, HUD-Zeitalteranzeige

### 3.6 Fog of War

- [ ] Sichtbarkeits-Grid pro Spieler (unerforscht / erkundet / sichtbar)
- [ ] Sichtradius pro Entity, Aktualisierung bei Bewegung (inkrementell, nicht jeden Tick alles)
- [ ] Rendering: Fog-Textur + Shader-Overlay auf dem Terrain, weiche Kanten
- [ ] Gegner-Entities werden ausgeblendet, wenn nicht sichtbar; Gebäude bleiben als "Erinnerung" stehen

### 3.7 Siegbedingung & Match-Ablauf

- [ ] Startaufstellung: Rathaus + 4 Siedler + Startressourcen pro Spieler
- [ ] Niederlage, wenn alle Gebäude **und** Siedler eines Spielers zerstört sind
- [ ] Sieg-/Niederlage-Bildschirm mit Statistik (gesammelt, gebaut, getötet, verloren)
- [ ] Spiel pausieren, Spielgeschwindigkeit 0,5× / 1× / 2×
- [ ] Neustart und Rückkehr ins Hauptmenü ohne Neustart der Anwendung

---

## Phase 4 — Assets: Blender-Pipeline 🔴

> Alle Modelle werden **prozedural per `bpy`-Python-Skript** erzeugt; die Skripte liegen im
> Repo. Vorteil: reproduzierbar, versionierbar, parametrisch anpassbar (Größe, Zeitalter-Variante),
> statt binärer Blackbox-Dateien. Ausgeführt wird headless:
> `blender --background --python tools/blender/build_all.py`

### 4.1 Konventionen festschreiben

- [ ] Maßstab: **1 Blender-Einheit = 1 Meter**, alle Transforms applied, Origin = Bodenmittelpunkt
- [ ] Ausrichtung: Modell schaut in Blender nach **−Y** (wird beim glTF-Export zu Godots "forward" = −Z)
- [ ] Namensschema: `bld_towncenter_age1`, `unt_spearman`, `SK_` für Armatures, `MAT_` für Materialien
- [ ] Export: **glTF 2.0 binary (`.glb`)**, "+Y Up", nur ausgewählte Collection, Animationen als NLA-Strips
- [ ] Polybudget: Einheiten 1,5k–3k Tris · Gebäude 4k–10k Tris · LOD1/LOD2 über Godots Auto-LOD
- [ ] Texturen: PBR, **ORM-gepackt** (R = AO, G = Roughness, B = Metallic) plus Albedo und Normal
  - Gebäude 2048² · Einheiten 1024² · gemeinsame Trim-/Atlas-Sheets pro Fraktion
- [ ] Team-Color: dedizierter Maskenkanal (Alpha des Albedo) statt separater Textur
- [ ] Ein Material pro Objekt (Draw-Call-Reduktion), Godot-Import-Presets (`.import`) mit versionieren

### 4.2 Werkzeuge

- [ ] `tools/blender/lib_mesh.py` — Hilfsfunktionen (Quader, Bogen, Dachstuhl, Palisade, Bevel-+-Weighted-Normals-Stack)
- [ ] `tools/blender/lib_material.py` — prozedurale PBR-Materialien (Holz, Stroh, Lehm, Stein, Metall, Stoff) via Shader-Nodes, anschließend auf Texturen gebacken
- [ ] `tools/blender/lib_rig.py` — humanoides Standard-Armature (Root, Hips, Spine, Head, 2× Arm, 2× Bein), automatische Gewichtung
- [ ] `tools/blender/lib_anim.py` — Keyframe-Generator für die Standard-Animationen
- [ ] `tools/blender/build_all.py` — baut alle Assets und exportiert nach `assets/models/`
- [ ] Bake-Schritt: High-Poly-Detail → Normal-Map auf das Low-Poly
- [ ] Import-Test in Godot: Maßstab, Ausrichtung, Materialien und Animationen korrekt

### 4.3 Realistischer Look (Godot-Seite)

- [ ] `WorldEnvironment`: HDRI-Sky, SSAO, SSIL, SDFGI oder VoxelGI, Glow, Tonemap ACES
- [ ] Direktionales Licht mit Schattenkaskaden, sinnvoller Sonnenstand (schräg, weiche Schatten)
- [ ] TAA + FXAA
- [ ] Grafik-Presets Niedrig / Mittel / Hoch (GI und Schatten abschaltbar)
- [ ] Sichtprüfung: Screenshot bei typischer Kameradistanz — sind Gebäude eindeutig unterscheidbar?

---

## Phase 5 — Asset-Liste MVP 🔴

### 5.1 Gebäude (7)

| # | Gebäude | Zeitalter | Funktion | Status |
|---|---|---|---|---|
| 1 | **Rathaus** | 1 | Siedler ausbilden, Abgabestelle, Zeitalteraufstieg | [ ] Modell [ ] Baustufen [ ] Zeitalter-2-Variante [ ] Ingame |
| 2 | **Haus** | 1 | +10 Bevölkerungslimit | [ ] Modell [ ] Baustufen [ ] Ingame |
| 3 | **Lagerhaus** | 1 | Abgabestelle für Holz / Stein / Gold | [ ] Modell [ ] Baustufen [ ] Ingame |
| 4 | **Farm** | 1 | Erneuerbare Nahrungsquelle | [ ] Modell [ ] Wachstumsstufen [ ] Ingame |
| 5 | **Kaserne** | 1 | Nahkampfeinheiten | [ ] Modell [ ] Baustufen [ ] Ingame |
| 6 | **Schießstand** | 2 | Fernkampfeinheiten | [ ] Modell [ ] Baustufen [ ] Ingame |
| 7 | **Wachturm** | 2 | Verteidigung, schießt automatisch, große Sichtweite | [ ] Modell [ ] Baustufen [ ] Ingame |

Pro Gebäude zusätzlich: 3 Baustufen, Trümmer-Mesh, Icon, Platzierungs-Footprint.

### 5.2 Einheiten (6)

| # | Einheit | Zeitalter | Rolle | Status |
|---|---|---|---|---|
| 1 | **Siedler** | 1 | Sammeln, Bauen, Reparieren | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 2 | **Späher** | 1 | Schnell, große Sicht, schwach | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 3 | **Speerkämpfer** | 1 | Nahkampf-Grundeinheit | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 4 | **Schleuderer** | 1 | Fernkampf, schwach im Nahkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 5 | **Schwertkämpfer** | 2 | Starker Nahkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 6 | **Bogenschütze** | 2 | Starker Fernkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |

Animationssatz pro Einheit: `Idle`, `Walk`, `Run`, `Attack`, `Death`
— Siedler zusätzlich: `Gather_Chop`, `Gather_Mine`, `Build`, `Carry_Walk`

- [ ] Animation-Events für Trefferzeitpunkt (`OnHitFrame`) und Werkzeugschlag (`OnGatherHit`)
- [ ] Blend zwischen Idle / Walk über `AnimationTree` + `BlendSpace1D`
- [ ] Zufälliger Anim-Offset pro Einheit, damit Gruppen nicht synchron zappeln

### 5.3 Umgebung & Ressourcen (5)

- [ ] Baum (3 Varianten + gefällter Stumpf)
- [ ] Felsen / Steinbruch
- [ ] Goldader
- [ ] Beerenbusch
- [ ] Bodendeko: Grasbüschel, kleine Steine (MultiMesh)

### 5.4 Effekte & Audio 🟡

- [ ] Projektile: Pfeil, Schleuderstein
- [ ] Partikel: Treffer-Funken, Staub beim Bauen, Rauch bei brennendem Gebäude
- [ ] Sounds: Selektion, Befehl bestätigt, Schwerthieb, Bogenschuss, Holzhacken, Bau fertig, Zeitalteraufstieg, Gebäude zerstört
- [ ] Musik: 1 Loop pro Zeitalter (Platzhalter genügt)

---

## Phase 6 — UI / HUD 🔴

- [ ] Ressourcenleiste oben: Nahrung, Holz, Stein, Gold, Bevölkerung (aktuell/max), Zeitalter
- [ ] Selektionspanel unten: Portrait, Name, HP, Werte; bei Mehrfachauswahl Icon-Gitter
- [ ] Aktionsleiste: kontextabhängige Buttons (Bauen, Ausbilden, Aufsteigen, Stopp, Abriss) mit Tastenkürzeln
- [ ] Bau-Menü mit Kosten-Tooltip; gesperrte Einträge ausgegraut samt Begründung ("Erfordert Kupferzeit")
- [ ] Minimap: Terrain, Fog of War, eigene und feindliche Einheiten als Punkte, Kamera-Rahmen, Klick zum Springen
- [ ] Produktionswarteschlange mit Fortschrittsbalken
- [ ] Benachrichtigungen: "Zu wenig Nahrung", "Bevölkerungslimit erreicht", "Wir werden angegriffen!"
- [ ] Hauptmenü: Spiel starten, Optionen, Beenden
- [ ] Optionen: Auflösung, Vollbild, Lautstärke, Grafik-Preset, Scroll-Geschwindigkeit
- [ ] Pause-Menü (ESC)
- [ ] 🟢 Tastenkürzel frei belegbar

---

## Phase 7 — Gegner-KI 🟡

> MVP-Ziel: ein Gegner, der ein Spiel *interessant* macht — nicht clever, aber kompetent.

- [ ] `AIPlayer` mit fester Build-Order aus einer Datendatei (leicht austauschbar)
- [ ] Wirtschafts-Manager: Siedler nachbauen, Zuteilung auf Ressourcen nach Bedarfsverhältnis
- [ ] Bau-Manager: Häuser bei nahem Bevölkerungslimit, Gebäude gemäß Build-Order, Platzsuche im Umkreis der Basis
- [ ] Militär-Manager: Armee bis zum Schwellenwert sammeln → Angriffswelle auf das nächste Feindgebäude
- [ ] Zeitalteraufstieg, sobald leistbar
- [ ] Reaktion auf Angriff: nahe Militäreinheiten zur Verteidigung rufen
- [ ] Schwierigkeit "Leicht / Normal" über Ressourcen-Handicap und Reaktionsverzögerung
- [ ] KI nutzt dieselbe Command-Queue wie der Spieler (kein Cheaten unter der Haube)

---

## Phase 8 — Speichern, Performance, Politur 🟡

- [ ] Serialisierung des Sim-Zustands nach JSON, Speichern und Laden im laufenden Match
- [ ] Objekt-Pooling für Projektile, Partikel, Floating-Text
- [ ] Zeitscheiben-Budget: Pathfinding, KI und Sichtbarkeit über mehrere Ticks verteilen
- [ ] Performance-Ziel: **300 Einheiten @ 60 FPS** auf Mittelklasse-Hardware
- [ ] Profiling-Overlay (F3): FPS, Sim-Tick-Zeit, Entity-Anzahl, Draw-Calls
- [ ] Unit-Tests für die Kern-Logik (Kampfrechnung, Ressourcenkonto, Bauvalidierung, Zeitalter-Voraussetzungen)
- [ ] Export-Preset Windows x64; testen, dass der Build ohne Editor läuft

---

## Definition of Done — das MVP ist fertig, wenn:

- [ ] Der Windows-Build startet ohne Editor und zeigt das Hauptmenü
- [ ] Ein 1v1-Skirmish gegen die KI ist von Anfang bis Ende spielbar
- [ ] Alle 4 Ressourcen können gesammelt und ausgegeben werden
- [ ] Alle 7 Gebäude sind baubar, zeigen sichtbaren Baufortschritt und sind funktional
- [ ] Alle 6 Einheiten sind ausbildbar, bewegen sich, kämpfen und sterben mit Animation
- [ ] Der Zeitalteraufstieg 1 → 2 funktioniert und schaltet Inhalte frei
- [ ] Fog of War funktioniert
- [ ] Sieg und Niederlage werden korrekt erkannt und angezeigt
- [ ] 300 Einheiten laufen flüssig
- [ ] Ein neues Gebäude oder eine neue Einheit lässt sich **ohne C#-Änderung** über eine `.tres` plus Modell hinzufügen

---

## Backlog nach dem MVP (bewusst nicht im Scope)

- Weitere Zeitalter bis in die Neuzeit (Bronze, Eisen, Mittelalter, Renaissance, Industrie, Moderne, Zukunft)
- Zivilisationen mit Boni und einzigartigen Einheiten
- Technologiebaum, Upgrades, Heldeneinheiten
- Belagerungswaffen, Mauern und Tore, Türme mit Besatzung
- Schiffe, Häfen, Wasserkarten
- Flugeinheiten (spätere Zeitalter)
- Kampagne, Missionsskripting, Zwischensequenzen
- Karteneditor, Zufallskartengenerator
- Multiplayer (Lockstep über die bestehende Command-Queue), Replays
- Formationen, Truppenmoral, Erfahrungsstufen
- Wetter, Tag-/Nachtwechsel, Jahreszeiten
- Bessere KI (Aufklärung, Konter-Zusammenstellung, Belagerungstaktik)

---

## Empfohlene Reihenfolge der Umsetzung

1. **Phase 0** → Projekt läuft
2. **Phase 1** → Grundgerüst steht (wichtigster Schritt, hier nicht sparen)
3. **Phase 2** → man kann sich auf einer Karte umsehen und Platzhalter-Würfel selektieren und bewegen
4. **Erste Assets aus Phase 4/5**: Siedler + Rathaus + Baum — damit ist die Blender→Godot-Pipeline validiert, *bevor* 13 Assets gebaut werden
5. **Phase 3.1–3.3** → Wirtschaftskreislauf spielbar (sammeln, bauen, ausbilden)
6. **Restliche Assets** aus Phase 5
7. **Phase 3.4–3.7** → Kampf, Zeitalter, Fog of War, Siegbedingung
8. **Phase 6** → UI
9. **Phase 7** → KI-Gegner
10. **Phase 8** → Politur und Build

> **Wichtigste Regel:** Punkt 4 nicht überspringen. Ein einziges Asset komplett durch die
> gesamte Pipeline zu schicken deckt den Großteil der Pipeline-Probleme auf, solange sie noch
> billig zu beheben sind.
