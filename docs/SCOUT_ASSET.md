# Steinzeit-Späher

Editierbare Quelle: `blender/unit_scout.blend`.
Spielmodell: `assets/models/units/unit_scout.glb`.
Generator: `tools/blender/assets/unit_scout.py`.
Portrait: `docs/previews/scout_final.png`.

Leichte olivfarbene Fellkleidung mit Schulterriemen, Ledermokassins,
Reisetasche, Spielerfarben an Stirnband und Armschmuck, kurze Steinkeule.
12.578 Dreiecke, neun Materialflächen, gemeinsames Skelett mit 21 Knochen.
Der Generator verwendet die Anatomie des Siedlers, erstellt eigene Kleidung
und Ausrüstung und backt fünf Clips mit 30 Bildern pro Sekunde:

| Clip | Dauer | Bewegung |
|---|---|---|
| Idle | 4,00 s | Atmung und suchender Blick |
| Walk | 1,07 s | Gehen mit korrigiertem Sohlenkontakt |
| Run | 0,80 s | Laufbewegung mit Armschwung |
| Attack | 2,00 s | Einhändiger Keulenschlag und Rückkehr |
| Death | 2,40 s | Einknicken und Fallen, anschließend Liegepose |

Die Keule nutzt den vorhandenen Ausrüstungsknochen `tool_axe` und bleibt in
allen Clips sichtbar. Die Blender-Datei öffnet in Idle; die NLA-Tracks sind
für die Bearbeitung stummgeschaltet. Der GLB-Export enthält alle fünf Clips.

Die bestehende Definition behält ihre Spielwerte: 30 Nahrung, sechs Sekunden
Ausbildung, 45 HP, Tempo 5,2 und Sichtweite 20. Das Rathaus bildet den Späher
bereits über seine datengetriebene Einheitenliste aus. Modell und Portrait
sind nun in `data/units/unit_scout.tres` zugewiesen.

## Reproduzieren und prüfen

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/build_all.py -- unit_scout
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/preview.py -- unit_scout C:/Projects/Epoch-Aeterna/docs/previews/scout_final.png three-quarter Idle:0
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_scout.blend --python-exit-code 1 --python tools/blender/inspect_scout.py
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
```

Verifiziert: Build ohne Warnungen/Fehler, 154 Simulationsprüfungen und 63
Präsentationsprüfungen bestanden. Der Präsentationstest instanziiert das
importierte Spähermodell und prüft Clips, Lauf-, Angriffs- und Todeszustand.
Die Blender-Prüfung kontrolliert jeden Animationsframe auf endliche
Transformationen und Bodenfreiheit sowie die Naht aller vier Schleifen.
Die niedrigste Geometrie liegt mindestens 3 mm über der ebenen Bodenfläche.
Front-, Seiten-, Lauf-, Angriffs- und Todesansichten wurden gerendert und
visuell geprüft; Messwerte liegen in `docs/previews/scout/validation.json`.

Wie beim Siedler gibt es keine Laufzeit-Fußanpassung an unebenes Gelände.
Die bestehende Begrenzung der Animationsgeschwindigkeit auf Faktor zwei
kann bei maximalem Spähertempo zu Fußgleiten führen. Armeeperformance
wurde nicht gemessen; die bestehenden Godot-Mesh-LODs bleiben aktiviert.
