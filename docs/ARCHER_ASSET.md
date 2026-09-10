# Kupferzeit-Bogenschütze

Editierbare Quelle: `blender/unit_archer.blend`.
Spielmodell: `assets/models/units/unit_archer.glb`.
Generator: `tools/blender/assets/unit_archer.py`.
Portrait: `docs/previews/archer_final.png`.

Grüne Ledertunika, langer Armschutz, Rückenköcher mit befiederten Pfeilen,
Kupferschnalle und Holzbogen. Stirnband, Schärpe und Armbänder tragen die
Spielerfarbe. 10.676 Dreiecke und 18 Meshes mit zehn Materialien.
Das gemeinsame Skelett mit 21 Knochen wird um vier Bogen-, zwei Sehnenknochen
und `nocked_arrow` ergänzt. Alle Bewegungen werden mit 30 fps gebacken.

| Clip | Dauer | Bewegung |
|---|---|---|
| Idle | 4,00 s | Atmung, Bogen in der linken Hand |
| Walk | 1,07 s | Gehen mit korrigiertem Sohlenkontakt |
| Run | 0,80 s | Lauf mit ruhig getragenem Bogen |
| Attack | 1,80 s | Griff zum Köcher, Auflegen, Spannen, Zielen und Lösen |
| Death | 2,40 s | Einknicken, Fallen und Bogen neben dem Körper ablegen |

Der Bogengriff folgt der ausgewerteten linken Hand. Die Wurfarme bewegen
sich beim Spannen, beide Sehnenhälften verbinden die Spitzen mit der
Zughand. Der aufgelegte Pfeil sitzt an der Zughand und verschwindet nach dem
Lösen. Gleichartige Ruhegeometrien bleiben getrennte Meshes, damit ihre
Knochengewichte nicht durch die Mesh-Bereinigung verschmolzen werden.

Spielwerte unverändert: 50 Nahrung, 60 Holz, 20 Gold, 19 Sekunden Ausbildung,
75 HP, Rüstung 1, Tempo 2,8, Schaden 15, Reichweite 12 und Angriffspause 1,8 s.
Die Ausbildung im Schießstand ist bis zur Kupferzeit gesperrt.

## Reproduzieren und prüfen

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/build_all.py -- unit_archer
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/preview.py -- unit_archer C:/Projects/Epoch-Aeterna/docs/previews/archer_final.png three-quarter Idle:0
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_archer.blend --python-exit-code 1 --python tools/blender/inspect_archer.py
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
```

Build ohne Warnungen oder Fehler; 154 Simulations- und 169
Präsentationsprüfungen bestanden. Neue Präsentationsprüfungen decken das
importierte Modell, fünf Clips, sieben Ausrüstungsknochen, aufgelegten und
gelösten Pfeil, Bewegung, Angriff, Todeszustand sowie den tatsächlichen
Schießstandbutton ab: Portrait, Zeitaltersperre, Freischaltung, Warteschlange
und Ressourcenabbuchung.

Die Blender-Prüfung kontrolliert jeden Frame auf endliche Transformationen,
Bodenfreiheit, Schleifennähte und durchgängige Verbindungen zwischen
Bogengriff, Wurfarmen, Sehne, Zughand und Pfeil (Toleranz 1 mm).
Ergebnisse: `docs/previews/archer/validation.json`. Front-, Seiten-, Lauf-,
Spann-, Schuss- und Todesansichten wurden visuell geprüft.

Die bestehende Projektilsimulation bleibt unverändert. Abschuss und Treffer
sind noch nicht mit Animationsereignissen synchronisiert; das eigenständige
Pfeil-Projektilasset bleibt in Phase 5.4 offen. Keine Fußanpassung an unebenes
Gelände und kein neuer Performance-Nachweis für große Armeen; die zusätzlichen
Bogenmeshes erhöhen die Zahl der Meshinstanzen gegenüber den Nahkämpfern.
