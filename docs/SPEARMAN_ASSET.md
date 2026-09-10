# Steinzeit-Speerkämpfer

Editierbare Quelle: `blender/unit_spearman.blend`.
Spielmodell: `assets/models/units/unit_spearman.glb`.
Generator: `tools/blender/assets/unit_spearman.py`.
Portrait: `docs/previews/spearman_final.png`.

Verstärkte Lederkleidung, Schulterfell, Spielerfarben auf Schärpe und Armbändern
und ein 2,05 m langer Holzspeer mit gebundener Feuersteinspitze unterscheiden
die Infanterie vom Späher. Gemeinsame Siedler-Anatomie und Rig mit 21 Knochen;
13.270 Dreiecke und zehn Materialflächen. Alle Bewegungen sind mit 30 fps
gebacken und benötigen keine IK zur Laufzeit.

| Clip | Dauer | Bewegung |
|---|---|---|
| Idle | 4,00 s | Atmung, Speer diagonal in beiden Händen |
| Walk | 1,07 s | Gehen mit korrigiertem Sohlenkontakt |
| Run | 0,80 s | Lauf mit stabiler zweihändiger Waffenführung |
| Attack | 1,60 s | Ausholen, kurzer Stoß, Rückkehr in Kampfhaltung |
| Death | 2,40 s | Einknicken, Speer loslassen, Liegepose |

Der Speer bleibt über `tool_spear` in jedem Clip sichtbar. Die Hände liegen
25 cm auseinander am Schaft. Beim Tod fällt die Waffe neben den Körper.
Die Blender-Datei öffnet mit Idle und stummgeschalteten NLA-Tracks; der
GLB-Export enthält alle fünf Clips.

Die Spielwerte bleiben erhalten: 60 Nahrung, 20 Holz, zwölf Sekunden Ausbildung,
90 HP, Rüstung 1, Tempo 2,9, Schaden 11, Reichweite 1,3 und Angriffspause 1,6 s.
Die vorhandene Kasernenliste verwendet Modell und Portrait über die Definition.

## Reproduzieren und prüfen

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/build_all.py -- unit_spearman
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/preview.py -- unit_spearman C:/Projects/Epoch-Aeterna/docs/previews/spearman_final.png three-quarter Idle:0
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_spearman.blend --python-exit-code 1 --python tools/blender/inspect_spearman.py
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
```

Build: keine Warnungen oder Fehler. 154 Simulationsprüfungen und 103
Präsentationsprüfungen bestanden. Der Präsentationstest prüft importierte Clips,
sichtbare Waffe, Bewegung, Angriff, Todeszustand sowie den tatsächlichen
Kasernenbutton einschließlich Portrait, Warteschlange und Ressourcenabbuchung.

Die Blender-Prüfung misst jeden Frame auf endliche Transformationen,
Bodenfreiheit, Schleifennähte und Handabstand zum Schaft (Toleranz 5 mm).
Messwerte: `docs/previews/spearman/validation.json`. Geometrie bleibt mindestens
3 mm über dem ebenen Boden; beide Griffe liegen innerhalb von 0,001 mm am Schaft.
Front-, Seiten-, Lauf-, Angriffs- und Todesansichten wurden visuell geprüft.

Die Angriffsdauer passt zum bestehenden Cooldown. Treffer werden weiterhin
von der Simulation ausgelöst; ein synchronisiertes Treffer-Animationsevent
bleibt im MVP-Plan offen. Ebenso gibt es noch keine Fußanpassung an unebenes
Gelände und keinen neuen Performance-Nachweis für große Armeen.
