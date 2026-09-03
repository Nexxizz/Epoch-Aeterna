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

- [x] Grid-basierte Karte (128×128 Kacheln, 1 Kachel = 2 m) — `NavGrid`, um den Ursprung zentriert
- [x] Terrain-Mesh aus Heightmap, in Chunks (16×16) für Culling — `TerrainRenderer`, 64 Blöcke
- [x] Terrain-Shader: 4-fach-Splatting (Gras / Erde / Fels / Sand), PBR, Splat-Gewichte aus Höhe und Steilheit
  - [ ] Echte Texturen und Triplanar an Steilhängen — die Sampler sind angelegt und auf Weiß vorbelegt, es fehlen nur die Bilddateien (Phase 4/5)
- [x] Navigations-Grid: begehbar/blockiert als Bitmaske, Höhenwert, Belegung durch Gebäude, Freiraum-Karte
- [x] Statische Deko per `MultiMeshInstance3D` — ein Draw-Call je Typ statt einem je Objekt
- [x] Test-Karte „Ebene der Anfänge": seed-basiert, 2 Startplätze auf den flachsten Stellen, Wald und Felsen als echte Hindernisse
  - [ ] Steinbruch, Goldader und Beerenbüsche als **Ressourcenknoten** — kommen mit Phase 3.1; aktuell nur Deko

### 2.2 RTS-Kamera

- [x] Pan über Pfeiltasten, Edge-Scrolling und Mittelklick-Drag
- [x] Zoom (Mausrad) mit gekoppelter Neigung — nah 32°, fern 62°
- [x] Q/E-Rotation um die Y-Achse, Pan dreht mit
- [x] Kamera-Grenzen am Kartenrand, Drehpunkt folgt der Geländehöhe
- [x] `JumpTo` für Sprünge; auf **Pos1** gelegt (Sprung zur eigenen Basis)
  - [ ] Minimap-Klick und Sprung zum letzten Ereignis — brauchen die Minimap aus Phase 6

> **Abweichung:** WASD ist bewusst **nicht** belegt. Der Plan sah WASD-Pan vor, aber
> Phase 2.3/3.4 brauchen `A` (Angriffsbewegung), `S` (Stopp) und `H` (Halten) als
> Einheitenbefehle — so machen es Empire Earth und AoE auch. Beides gleichzeitig geht
> nicht; Kamera auf Pfeiltasten hält die Buchstaben frei.

### 2.3 Selektion & Befehle

- [x] Linksklick-Einzelselektion — Strahl-Kugel-Schnitt, bewusst ohne Physik-Körper
- [x] Box-Selektion mit Rahmen-Overlay
  - [ ] Priorisierung Militär vor Zivil — es gibt noch keine Militäreinheiten
- [x] Doppelklick = alle sichtbaren Einheiten desselben Typs
- [x] Shift = zur Auswahl hinzufügen / entfernen
- [x] Kontrollgruppen 0–9 (`Strg+Zahl` setzen, `Zahl` abrufen)
  - [ ] Doppeltipp = zur Gruppe springen
- [x] Rechtsklick = Standardbefehl; auf Boden → Bewegen
  - [ ] Gegner → Angriff, Ressource → Sammeln, Baustelle → Bauen (Phase 3)
- [x] `S` Stopp
  - [ ] `A` Angriffsbewegung und `H` Halten — brauchen das Kampfsystem (Phase 3.4)
- [x] Shift-Queue für mehrere Befehle hintereinander
- [x] Selektionsring am Boden und Ziel-Marker als Feedback

### 2.4 Pathfinding

- [x] A* auf dem Nav-Grid mit Kachel-Clearance, Oktil-Heuristik, kein Diagonal-Schnitt durch Ecken
- [x] Pfadglättung per String-Pulling über Supercover-Sichtlinien
- [x] Lokale Ausweichbewegung — `AvoidanceSystem` löst Überlappungen über ein Raster-Hashing auf
- [x] Gruppenbewegung: ringförmige Formations-Offsets um das Ziel
- [x] Pfad-Neuberechnung bei blockiertem Weg, Budget von 8 Suchen pro Tick
- [ ] 🟢 Flow-Field für große Gruppen (> 30 Einheiten) — nach dem MVP

**Verifikation Phase 2** — der Selbsttest deckt jetzt 61 Prüfungen ab:

```bash
"C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . -- --verify
```

| Prüfung | Ergebnis |
|---|---|
| `dotnet build` | 0 Warnungen, 0 Fehler |
| Selbsttest (61 Prüfungen) | alle bestanden, Exit-Code 0 |
| Darstellung | Gelände, Wald, Basen und Schatten im Screenshot bestätigt |

> Die Wegfindung wird gegen synthetische Gitter geprüft, nicht gegen die generierte Karte:
> Wand mit Umweg, vollständig eingemauertes Ziel, Klick auf ein Hindernis. So hängen die
> Tests nicht davon ab, wie der Zufall die Karte gerade formt.

---

## Phase 3 — Kernspielmechanik 🔴

### 3.1 Ressourcen-Wirtschaft

- [x] Ressourcenknoten als Entities mit endlichem Vorrat — Baum, Steinbruch, Goldader, Beerenbusch als `.tres`
- [x] Sammel-Loop: hingehen → sammeln → zur Abgabestelle → abliefern → zurück
- [x] Automatische Suche des nächsten gleichartigen Knotens, wenn der aktuelle erschöpft ist
- [x] Abgabestellen: Rathaus und Lagerhaus, jeweils die nächstgelegene
- [x] Visuelles Feedback: Baum schrumpft sichtbar, Ertrag steigt als Zahl auf
- [ ] Farm als erneuerbare Nahrungsquelle — Gebäude und Definition stehen, das Wiederbestellen fehlt

> Jede Startbasis bekommt garantiert alle vier Ressourcen in Reichweite. Ohne diese
> Zusicherung entscheidet der Zufall die Partie, bevor sie beginnt — der Selbsttest
> prüft das für beide Startplätze.

### 3.2 Bauen

- [x] Bauplatzierungs-Modus: Geistermodell folgt dem Cursor, rastet aufs Grid, grün/rot
- [x] Prüfung: Terrain flach genug, keine Überlappung, Ressourcen, Zeitalter, Voraussetzungs-Gebäude
- [x] Baustellen-Entity: Ressourcen sofort abgezogen, HP wachsen mit dem Fortschritt
- [x] Mehrere Siedler bauen schneller, mit abnehmendem Ertrag
- [x] **3 sichtbare Baustufen**, Umschaltung bei 33 % / 66 % / 100 %
  - Jedes Gebäude kann drei eigene `ConstructionStageScenes` bereitstellen. Der `ViewManager`
    tauscht Fundament, Rohbau und fast fertiges Modell anhand des Simulationsfortschritts aus;
    ohne eigene Modelle bleibt der bestehende Platzhalter aktiv.
- [x] Abriss mit Teilrückerstattung, anteilig zum Baufortschritt
- [ ] 🟢 Reparatur beschädigter Gebäude

> Vorschau und Befehl rufen dieselbe Prüfmethode auf. Damit kann die Vorschau nicht
> lügen — was grün leuchtet, wird auch gebaut.

### 3.3 Produktion & Bevölkerung

- [x] Produktionswarteschlange pro Gebäude (max. 5), Fortschritt im Datenmodell
- [x] Abbruch mit Rückerstattung
- [x] Sammelpunkt setzbar
- [x] Bevölkerungsprüfung hält die Produktion an, statt den Befehl zu verwerfen
- [ ] Fortschrittsbalken und Sammelpunkt-Flagge im HUD — Phase 6

### 3.4 Kampf

- [x] HP, Rüstung, Schadenstyp (Hieb / Stich / Distanz / Belagerung), Konter-Matrix als `.tres`
- [x] Nahkampf: anlaufen, in Reichweite zuschlagen, Abklingzeit
- [x] Fernkampf: Geschoss mit Flugzeit; Schaden fällt **beim Einschlag** an, nicht beim Abschuss
- [x] Zielsuche im Sichtradius, Haltungen Aggressiv / Defensiv / Halten
- [x] Tod entfernt die Entity und zählt in die Statistik
- [x] Wachturm schießt selbstständig
- [x] Trefferfeedback: Lebensbalken erscheinen bei Schaden
- [ ] Todesanimation, Leichen und Trümmer-Meshes — brauchen die Modelle aus Phase 5

### 3.5 Zeitalter-Aufstieg

- [x] `AgeDefinition`-Kette, MVP: Steinzeit → Kupferzeit
- [x] Aufstieg im Rathaus: Kosten, Forschungszeit, Voraussetzung von 2 verschiedenen Gebäuden
- [x] Freischaltung läuft rein über `RequiredAgeIndex` in den `.tres` — kein Codeeingriff je Zeitalter
- [x] Pauschaler HP-Zuwachs beim Aufstieg
- [ ] Zeitalter-Varianten der Gebäudemodelle und Fanfare — Phase 5 bzw. 6

### 3.6 Fog of War

- [x] Sichtbarkeits-Grid pro Spieler: unerforscht / erkundet / sichtbar
- [x] Sichtradius pro Entity, Neuaufbau alle 4 Ticks statt jeden
- [x] Rendering **im Gelände-Shader**, nicht als eigene Ebene
- [x] Gegner werden ausgeblendet; Gebäude und Vorkommen bleiben als Erinnerung stehen

> Eine schwebende Nebelebene müsste über jedem Hügel liegen, bräuchte abgeschalteten
> Tiefentest und verdeckt dann alles andere gleich mit — das war der erste Versuch und
> färbte den Bildschirm komplett schwarz. Ein Texturzugriff im ohnehin vorhandenen
> Shader kostet dagegen nichts.

### 3.7 Siegbedingung & Match-Ablauf

- [x] Startaufstellung: Rathaus + 4 Siedler + Startressourcen
- [x] Niederlage, wenn weder Gebäude noch Siedler übrig sind
- [x] Sieg-/Niederlage-Bildschirm mit Statistik (gesammelt, gebaut, getötet, verloren)
- [x] Pause und Spielgeschwindigkeit 0,5× / 1× / 2×
- [x] Neustart ohne Neustart der Anwendung
- [ ] Hauptmenü — Phase 6

**Verifikation Phase 3** — der Selbsttest deckt jetzt 126 Prüfungen ab:

| Prüfung | Ergebnis |
|---|---|
| `dotnet build` | 0 Warnungen, 0 Fehler |
| Selbsttest (126 Prüfungen) | alle bestanden, Exit-Code 0 |
| Headless-Import | keine Fehler |
| Laufzeit | keine Fehler, Screenshot bestätigt Nebel, Vorkommen und Basis |

> Zwei Fehler, die der Selbsttest zunächst **nicht** gefunden hat, weil er zu nachsichtig
> war: Die Konter-Matrix ließ sich gar nicht laden (Godot verlangt Dateiname = Klassenname)
> und fiel still auf den eingebauten Fallback zurück — der dieselben Werte hatte. Der
> Fallback besteht jetzt nur noch aus Einsen, und der Test prüft, dass etwas anderes
> ankommt. Eine Prüfung, die auch beim Ausfall grün bleibt, prüft nichts.

---

## Phase 4 — Assets: Blender-Pipeline 🔴

> Alle Modelle werden **prozedural per `bpy`-Python-Skript** erzeugt; die Skripte liegen im
> Repo. Vorteil: reproduzierbar, versionierbar, parametrisch anpassbar (Größe, Zeitalter-Variante),
> statt binärer Blackbox-Dateien. Ausgeführt wird headless:
> `blender --background --python tools/blender/build_all.py`

### 4.1 Konventionen festschreiben

- [x] Maßstab: **1 Blender-Einheit = 1 Meter**, alle Transforms applied, Origin = Bodenmittelpunkt
- [x] Ausrichtung: Modell schaut in Blender nach **−Y**, wird beim glTF-Export zu Godots −Z
- [x] Namensschema: `SK_` für Armatures, `MAT_` für Materialien
  - **Abweichung:** Die `.glb`-Dateinamen entsprechen den Definitions-IDs (`unit_settler.glb`, nicht `unt_settler`). Damit ist das Modell eindeutig seiner `.tres` zugeordnet.
- [x] Export: **glTF 2.0 binary (`.glb`)**, „+Y Up", nur Auswahl, Animationen als NLA-Strips
- [x] Polybudget nach Kategorie, wird bei jedem Build gemessen und ausgegeben:

  | Kategorie | Budget | Bisher gemessen |
  |---|---|---|
  | Ressourcenvorkommen, Requisiten | 150–800 | Baum 268 |
  | Einheiten | kein starres Limit — visuelle Lesbarkeit hat Vorrang | Siedler 3.830 |
  | Kleine Gebäude (Haus, Lagerhaus, Wachturm) | Richtwert 1.500–4.000; Lesbarkeit hat Vorrang | Haus 9.032 |
  | Große Gebäude (Rathaus, Kaserne, Schießstand) | 4.000–10.000 | Rathaus 6.734 |

  Die Vorkommen sind der eigentliche Posten: Auf einer 128×128-Karte stehen rund 280 davon,
  also etwa 75.000 Dreiecke allein für Bäume und Felsen — mehr als alle Gebäude und Einheiten
  zusammen. Wer dort 800 statt 270 Dreiecke verbaut, verdreifacht die Grundlast der Szene.
  - [ ] LOD1/LOD2 über Godots Auto-LOD — erst sinnvoll, wenn viele Modelle gleichzeitig auf der Karte stehen (Phase 8)
- [x] Team-Color über ein eigens benanntes Material `MAT_teamcolor`
  - **Abweichung:** Der Plan sah eine Maske im Alphakanal des Albedo vor. Ohne Texturen gäbe es dafür noch keinen Kanal; ein Materialname funktioniert sofort, überlebt Geometrieänderungen und lässt sich später zusätzlich mit einer Maske kombinieren.
- [x] Ein Material pro Objekt, `.import`-Presets werden mitversioniert
- [ ] PBR-Texturen mit ORM-Packing — braucht erst Texturen (Phase 5)

### 4.2 Werkzeuge

- [x] `tools/blender/lib_scene.py` — Szenen-Reset, Exportkonventionen und sperrsicherer temporärer GLB-Export an einer Stelle
- [x] `tools/blender/lib_mesh.py` — Grundkörper, Bevel-/Smooth-Nachbearbeitung, Verschmelzen, Gründung
- [x] `tools/blender/lib_material.py` — PBR-Palette (Holz, Rinde, Laub, Stroh, Lehm, Stein, Metall, Stoff, Haut, Fell, Ocker, Knochen, Fasern) plus Team-Color
- [x] `tools/blender/lib_rig.py` — humanoides Standard-Armature, starre Gewichtung und eigene Ausrüstungs-Bones
- [x] `tools/blender/lib_anim.py` — Keyframe-Generator, Clips als NLA-Strips und animierte Ausrüstungssichtbarkeit
- [x] `tools/blender/build_all.py` — baut alle Assets, einzeln ansteuerbar, Fehler stoppen den Lauf nicht
- [x] Import-Test in Godot: Maßstab, Ausrichtung, Materialien und Animationen bestätigt
- [ ] Bake-Schritt High-Poly → Normal-Map — braucht High-Poly-Quellen (Phase 5)

### 4.3 Realistischer Look (Godot-Seite)

- [x] `WorldEnvironment`: Sky, SSAO, SSIL, SDFGI, Glow, Tonemap ACES
- [x] Direktionales Licht mit Schattenkaskaden (2 bzw. 4 Splits je nach Stufe)
- [x] TAA + FXAA
- [x] Grafik-Presets **Niedrig / Mittel / Hoch**, zur Laufzeit über `F5` umschaltbar
- [x] Sichtprüfung per Screenshot

**Pipeline-Validierung** — Punkt 4 der Umsetzungsreihenfolge ist erledigt:

Vier Assets sind komplett durch die Kette gelaufen; beim Haus außerdem alle Baustufen und das
Trümmer-Mesh.

| Asset | Dreiecke | Besonderheit |
|---|---|---|
| `res_tree` | 268 | Geometrie und Origin-Konvention |
| `bld_towncenter` | 6.734 | Grundfläche exakt 8 m = 4 Kacheln, vier Materialien, Team-Color-Banner |
| `unit_settler` | 3.830 | detailliertes Armature-Modell, aufgabenspezifische Ausrüstung, 10 Animationsclips |
| `bld_house` | 9.032 | drei konsistente Baustufen (824 / 7.156 / 8.572), fertiges Modell und Trümmer-Mesh (1.476) |

```bash
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python tools/blender/build_all.py
```

| Prüfung | Ergebnis |
|---|---|
| Blender-Build | 4/4 Assets samt Hausvarianten, keine Warnungen |
| Godot-Import | fehlerfrei |
| Animationsclips | `Idle`, `Walk`, `Run`, `Carry_Walk`, `Gather_Food`, `Gather_Chop`, `Gather_Mine`, `Build`, `Attack`, `Death` + Skeleton3D bestätigt |
| Team-Color | Banner und Siedler nehmen die Spielerfarbe an |
| Selbsttest | 130/130, Exit-Code 0 |

> Genau die Fehler, die der Plan an dieser Stelle vorhergesagt hat, sind aufgetreten —
> und zwar an drei Assets statt an sechzehn:
>
> 1. **Das Rathaus fiel in sich zusammen.** Ich habe jede Materialgruppe einzeln
>    „auf den Boden gesetzt", wodurch Sockel, Wände, Dach und Banner alle auf z = 0
>    landeten. Gründung muss für das Asset als Ganzes passieren.
> 2. **glTF meldete ungültige Meshes.** Zwei Ursachen: Beim Verschmelzen überlebt nur
>    der Modifier-Stack des ersten Objekts und wird beim Export auf alles angewendet;
>    und die automatische Gewichtung hinterlässt inkonsistente Mesh-Daten. Bevel wird
>    jetzt sofort angewendet, `validate()` läuft nach dem Binden.

---

### 4.4 Worauf bei neuen Grafiken zu achten ist

> Jede Regel hier steht für einen Fehler, der beim Bau der ersten drei Assets
> tatsächlich aufgetreten ist. Die Liste ersetzt kein Auge, aber sie erspart es,
> dieselben Stunden noch dreizehn Mal zu verlieren.

**Geometrie und Export**

- **Ganze Assets gründen, nie einzelne Teile.** `ground_assembly()` setzt den tiefsten Punkt
  des *gesamten* Objekts auf z = 0. Pro Materialgruppe aufgerufen landet jedes Teil einzeln
  auf dem Boden — das Rathaus fiel dabei in einen Haufen zusammen.
- **Bevel sofort anwenden, nicht als Modifier stehen lassen.** Beim Verschmelzen überlebt nur
  der Stack des *ersten* Objekts und wird beim Export auf alles angewendet. Auf sich
  schneidenden Körpern erzeugt das entartete Flächen und die Meldung „mesh is not valid".
- **Bevel-Segmente sind der teuerste Posten.** Zwei Segmente kosten je Kästchen rund
  140 Dreiecke für eine Kante, die auf Kameradistanz niemand sieht. Bei den Fellteilen des
  Siedlers und den Hautbahnen des Rathauses hat `segments=1` beide Assets fast halbiert.
- **Kein starres Polygonlimit für Einheiten.** Silhouette, Gelenke, Gesicht und aufgabenspezifische
  Ausrüstung müssen aus der Spielkamera eindeutig lesbar sein. Dreiecke werden weiterhin gemessen,
  aber sichtbare Qualität hat Vorrang vor einer willkürlichen Obergrenze; optimiert wird zuerst dort,
  wo Geometrie weder Silhouette noch Animation verbessert.
- **Vorne ist −Y in Blender.** Der Exporter bildet `gltf_z = −blender_y` ab, ein so gebautes
  Modell landet also auf **+Z** — Godots „vorne" ist aber −Z. `lib_scene` dreht deshalb beim
  Export um 180°. Nie im Spielcode gegensteuern; die Blickrichtung gehört in `Facing`.
- **Grundfläche muss exakt zur Simulation passen.** Kacheln × 2 m. Ein Rathaus mit
  `Footprint = Vector2i(4, 4)` ist 8 m breit — der Modellkörper etwas schmaler, damit
  Nachbargebäude sich nicht berühren.
- **Team-Farbe über das Material `MAT_teamcolor`.** Der `ViewManager` sucht es beim Namen und
  ersetzt sein Albedo. Ein Materialname überlebt Geometrieänderungen, ein Slot-Index nicht.
- **Naturmaterialien und Team-Farbe trennen.** Fell, Leder, Ocker, Knochen und Fasern behalten
  ihre eigenen Farben. `MAT_teamcolor` gehört nur auf deutlich sichtbare Akzente wie Schärpen,
  Armbänder und Banner — sonst färbt das Spiel die gesamte Figur oder das ganze Gebäude um.
- **GLB zunächst temporär exportieren.** Ein laufender Godot-Import kann die bestehende Datei kurz
  sperren. `lib_scene.export_glb()` schreibt deshalb zuerst `.<name>.exporting.glb` und ersetzt die
  Live-Datei mit kurzen Wiederholungsversuchen; so bleibt bei einer Sperre immer ein gültiges Asset.

**Rigging und Gewichtung**

- **Zusammengesetzte Figuren nicht automatisch gewichten.** Die Wärmeverteilung arbeitet
  volumetrisch: Bei einer Figur aus getrennten Zylindern bekommen Rumpfpunkte nahe der
  Schulter Anteile vom Armknochen und werden beim Heben mitgezogen — sichtbar als gedehnte
  Schwimmhaut zwischen Arm und Körper. Stattdessen jedes Teil per `assign_to_bone()` an genau
  einen Knochen, dazu **Kugeln in Schulter, Ellbogen und Knie**, die den Drehpunkt abdecken.
- **`parent_set(type="ARMATURE")`**, nicht `ARMATURE_NAME`. Letzteres legt leere Knochengruppen
  an und löscht dabei die eigenen — die getragene Axt verschwand dadurch spurlos.
- **Zubehör an den Knochen des Teils, auf dem es sitzt.** Der Kragen sitzt auf dem Umhang: hängt
  der Umhang am `spine` und der Kragen am `chest`, rutscht der Kragen ab, sobald sich die Brust
  dreht. Dauerhaft sichtbares Zubehör folgt dem Körper-Bone, aufgabenspezifische Werkzeuge dagegen
  einem eigenen Slot wie `tool_axe`, `tool_pick`, `tool_spear` oder `tool_basket`. Diese Slots sind
  an Hand, Unterarm oder Rumpf geparentet und lassen sich pro Clip unabhängig ein- und ausblenden.
- **`validate()` erst *nach* dem Binden.** Die Gewichtung hinterlässt inkonsistente Mesh-Daten;
  vorher aufgerufen repariert sie nichts.

**Animation**

- **Die lokale Y-Achse läuft *entlang* des Knochens.** Für den Rumpf heißt das: **Y = Drehung**
  um die Körperachse, **X = Vorwärtsbeuge**, **Z = Seitwärtsneigung**. Eine Rumpfdrehung auf Z
  kippt die Figur zur Seite, statt sie zu drehen.
- **Über 90° keine zweite Achse dazunehmen.** Bei XYZ-Euler wird X zuerst angewandt, Y und Z
  drehen um die *Ruhe*achsen. Ein Arm bei −128° um X plus −26° um Z flog dadurch 0,8 m seitlich
  weg statt über die Schulter. Für erhobene Arme ist die lokale **Y**-Achse die Senkrechte —
  damit schwenkt man sie horizontal um den Körper.
- **Drei Dinge trennen eine Bewegung vom Roboter:** Auf-und-ab des Körpers (über die Position
  des `root`), Gegendrehung von Hüfte und Schultern, und ungleiches Timing — ein Ausholen ist
  langsam, ein Schlag schnell.
- **Bezier statt linear.** Lineare Interpolation ist die auffälligste Roboter-Ursache überhaupt:
  Der Körper wechselt an jedem Keyframe schlagartig die Richtung.
- **Der Kopf hält gegen.** Er erbt die Drehung von Brust *und* Wirbelsäule; ohne Gegenwinkel
  schlackert er beim Zuschlagen um fast 40°.
- **Werkzeuge zweihändig führen, wo es plausibel ist.** Ein Arm allein an einer großen Axt
  liest sich als Fuchteln. Handabstand am Stiel: 10–25 cm.
- **Jeder Clip keyframed alle Ausrüstungs-Bones.** Der benötigte Slot erhält Skalierung `(1, 1, 1)`,
  alle anderen `(0.001, 0.001, 0.001)`. Fehlt dieser Keyframe, bleiben Korb, Axt, Pickel und Speer
  gleichzeitig sichtbar. `lib_anim.equipment()` erzeugt die vollständige Tabelle dafür.
- **Clip-Namen sind ein Vertrag mit `ModelAnimator`.** `Gather_Food`, `Gather_Chop` und
  `Gather_Mine` werden anhand des Ressourcentyps gewählt, `Carry_Walk` bei getragener Ladung,
  `Build` beim Bauen und `Attack` bei Jagd oder Kampf. Neue Tätigkeiten brauchen auf beiden Seiten
  denselben Namen und einen sinnvollen Fallback.
- **Gliedmaßen dürfen nicht im Rumpf stecken.** Beim Schlag greifen die Arme nach vorn-unten,
  nicht seitlich herunter — sonst wandert der Ellbogen in die Brust. Nachprüfbar: Abstand des
  Ellbogens von der Körperachse gegen den Rumpfradius (Taille 14,5 cm, Brust 17,5 cm).

**Blender 5.2**

- `action.fcurves` existiert nicht mehr — Actions liegen seit 4.4 in Layern, Strips und
  Channel Bags. `lib_anim._fcurves()` bedient beide Varianten.
- `use_auto_smooth` ist weg; stattdessen `bpy.ops.object.shade_smooth_by_angle()`.
- Vor dem Export **`view_layer.update()`**, sonst sieht der Exporter eine per Skript gesetzte
  Transformation nicht und ignoriert sie stillschweigend.
- Für Vorschaubilder die Farbansicht auf `Standard` stellen. Blenders Standard-AgX entsättigt
  so stark, dass Holz, Haut und Stein alle gleich beige rendern.

**Vorgehen**

- **Messen statt Augenmaß.** Bei Winkeln über 90° trügt der Augenschein zuverlässig. Positionen
  von Händen, Ellbogen und Werkzeugen lassen sich in Blender direkt ausrechnen — das hat jeden
  der Animationsfehler oben schneller geklärt als jedes weitere Rendern.
- **Vorschau nutzen, nicht das Spiel starten.** `preview.py` rendert jedes Asset aus drei
  Blickwinkeln und auf Wunsch eine einzelne Animationspose oder Gebäudebaustufe:

  ```bash
  blender --background --python tools/blender/preview.py -- unit_settler out.png three-quarter "Gather_Chop:13"
  blender --background --python tools/blender/preview.py -- bld_house out.png three-quarter "stage:1"
  ```

  Bei einer Pose mutet das Werkzeug alle übrigen NLA-Tracks. Ohne diese Isolation würden sämtliche
  Clips gleichzeitig ausgewertet, Posen vermischt und alle Ausrüstungsgegenstände eingeblendet.

- **Prüfskripte selbst hinterfragen.** Zwei meiner Kontrollen waren wertlos: eine las nur die
  Wurzel-Transformation (immer Identität), eine andere maß nach dem Rundlauf die falsche Achse.
  Entschieden hat am Ende das direkte Auslesen der `.glb`.

---

## Phase 5 — Asset-Liste MVP 🔴

### 5.1 Gebäude (7)

| # | Gebäude | Zeitalter | Funktion | Status |
|---|---|---|---|---|
| 1 | **Rathaus** | 1 | Siedler ausbilden, Abgabestelle, Zeitalteraufstieg | [x] Modell [ ] Baustufen [ ] Zeitalter-2-Variante [x] Ingame |
| 2 | **Haus** | 1 | +10 Bevölkerungslimit | [x] Modell [x] Baustufen [x] Ingame |
| 3 | **Lagerhaus** | 1 | Abgabestelle für Holz / Stein / Gold | [ ] Modell [ ] Baustufen [ ] Ingame |
| 4 | **Farm** | 1 | Erneuerbare Nahrungsquelle | [ ] Modell [ ] Wachstumsstufen [ ] Ingame |
| 5 | **Kaserne** | 1 | Nahkampfeinheiten | [ ] Modell [ ] Baustufen [ ] Ingame |
| 6 | **Schießstand** | 2 | Fernkampfeinheiten | [ ] Modell [ ] Baustufen [ ] Ingame |
| 7 | **Wachturm** | 2 | Verteidigung, schießt automatisch, große Sichtweite | [ ] Modell [ ] Baustufen [ ] Ingame |

Pro Gebäude zusätzlich: 3 Baustufen, Trümmer-Mesh, Icon, Platzierungs-Footprint.

- [x] Beim ausgewählten Siedler zeigt ein kontextuelles Bau-Menü das fertige Haus als anklickbares
  Bild. Der Klick startet die eingefärbte Modellvorschau; ein gültiger Linksklick platziert die
  Baustelle und weist die ausgewählten Siedler direkt als Bauarbeiter zu.

### 5.2 Einheiten (6)

| # | Einheit | Zeitalter | Rolle | Status |
|---|---|---|---|---|
| 1 | **Siedler** | 1 | Sammeln, Bauen, Reparieren | [x] Modell [x] Rig [x] Anims [x] Ingame |
| 2 | **Späher** | 1 | Schnell, große Sicht, schwach | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 3 | **Speerkämpfer** | 1 | Nahkampf-Grundeinheit | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 4 | **Schleuderer** | 1 | Fernkampf, schwach im Nahkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 5 | **Schwertkämpfer** | 2 | Starker Nahkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |
| 6 | **Bogenschütze** | 2 | Starker Fernkampf | [ ] Modell [ ] Rig [ ] Anims [ ] Ingame |

Animationssatz pro Einheit: `Idle`, `Walk`, `Run`, `Attack`, `Death`
— Siedler zusätzlich: `Gather_Food`, `Gather_Chop`, `Gather_Mine`, `Build`, `Carry_Walk`

- [x] Gemeinsames Skelett und Clip-Vokabular in `lib_rig` / `lib_anim` — eine einmal angelegte
  Bewegung läuft auf jeder humanoiden Einheit
- [x] Siedler hat `Idle`, `Walk`, `Run`, `Carry_Walk`, `Gather_Food`, `Gather_Chop`,
  `Gather_Mine`, `Build`, `Attack` und `Death`
- [x] Zufälliger Anim-Offset pro Einheit, damit Gruppen nicht synchron zappeln (`ModelAnimator`)
- [ ] Animation-Events für Trefferzeitpunkt (`OnHitFrame`) und Werkzeugschlag (`OnGatherHit`)
- [ ] Blend zwischen Idle / Walk über `AnimationTree` + `BlendSpace1D`
- [ ] Fußabrollung beim Gehen — die Knöchel bleiben derzeit steif

### 5.3 Umgebung & Ressourcen (5)

- [x] Baum — eine Variante steht
  - [ ] Zwei weitere Varianten und gefällter Stumpf
- [ ] Felsen / Steinbruch
- [ ] Goldader
- [ ] Beerenbusch
- [x] Bodendeko: Grasbüschel, kleine Steine als MultiMesh, vom Nebel des Krieges miterfasst

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
4. ~~**Erste Assets aus Phase 4/5**: Siedler + Rathaus + Baum~~ — **erledigt**, Pipeline validiert
5. **Phase 3.1–3.3** → Wirtschaftskreislauf spielbar (sammeln, bauen, ausbilden)
6. **Restliche Assets** aus Phase 5
7. **Phase 3.4–3.7** → Kampf, Zeitalter, Fog of War, Siegbedingung
8. **Phase 6** → UI
9. **Phase 7** → KI-Gegner
10. **Phase 8** → Politur und Build

> **Wichtigste Regel:** Punkt 4 nicht überspringen. Ein einziges Asset komplett durch die
> gesamte Pipeline zu schicken deckt den Großteil der Pipeline-Probleme auf, solange sie noch
> billig zu beheben sind.
