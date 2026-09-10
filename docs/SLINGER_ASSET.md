# Steinzeit-Schleuderer

Editierbare Quelle: `blender/unit_slinger.blend`.
Spielmodell: `assets/models/units/unit_slinger.glb`.
Generator: `tools/blender/assets/unit_slinger.py`.
Portrait: `docs/previews/slinger_final.png`.

Leichte sandfarbene Lederkleidung, Stirnband und Schärpe in Spielerfarbe,
Steinbeutel und Zweistrangschleuder. 10.810 Dreiecke, neun Meshes mit acht
Materialien. Das gemeinsame Skelett mit 21 Knochen wird um vier Ausrüstungsknochen
ergänzt: `sling_cord`, `sling_pouch`, `sling_release`, `sling_stone`.
Alle Bewegungen werden bei 30 fps gebacken; keine Laufzeit-IK erforderlich.

| Clip | Dauer | Bewegung |
|---|---|---|
| Idle | 4,00 s | Atmung, geladene Schleuder neben dem Körper |
| Walk | 1,07 s | Gehen mit korrigiertem Sohlenkontakt |
| Run | 0,80 s | Lauf, Schleuder von den Beinen ferngehalten |
| Attack | 2,00 s | Überkopfschwung, Öffnen, Nachladen und Rückkehr |
| Death | 2,40 s | Einknicken und Fallen, Schleuder bleibt an der Hand |

Die Halteschnur folgt den ausgewerteten Fingern, die Tasche ihrem Ende.
Beim Abwurf öffnet sich die zweite Schnur und der geladene Stein wird
ausgeblendet; beim Nachladen erscheint er wieder. Beide Schnüre behalten ihre
Länge von 50 cm. Sie bleiben getrennte Meshes, damit die Mesh-Bereinigung ihre
identischen Ruhegeometrien nicht verschmilzt und die Gewichte vermischt.
Mesh- und Knochennamen sind eindeutig, damit der Import die Knochen nicht umbenennt.

Spielwerte unverändert: 45 Nahrung, 35 Holz, 13 Sekunden Ausbildung, 60 HP,
Tempo 2,8, Schaden 9, Reichweite 9 und Angriffspause 2 s. Der vorhandene
Schießstand bildet den Schleuderer über seine datengetriebene Liste aus.
Der Schleuderer gehört zur Steinzeit; der Bau des Schießstands bleibt gemäß
bestehender Gebäudedefinition an die Kupferzeit gebunden.

## Reproduzieren und prüfen

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/build_all.py -- unit_slinger
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/preview.py -- unit_slinger C:/Projects/Epoch-Aeterna/docs/previews/slinger_final.png three-quarter Idle:0
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_slinger.blend --python-exit-code 1 --python tools/blender/inspect_slinger.py
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
```

Build ohne Warnungen oder Fehler; 154 Simulations- und 124
Präsentationsprüfungen bestanden. Die bestehenden Simulationstests prüfen
bereits Schleuderer-Projektile und Einschlagschaden. Die ergänzten
Präsentationstests prüfen Modell, fünf Clips, vier Schleuderknochen, Freigabe
und Nachladen des Steins, Bewegung, Angriff, Todeszustand sowie den
Schießstandbutton mit Portrait, Warteschlange und Ressourcenabbuchung.

Die Blender-Prüfung misst jeden Frame auf endliche Transformationen,
Bodenfreiheit, Schleifennähte, Verbindung von Hand und Halteschnur sowie
Verbindung von Schnur und Tasche. Beide Schnurmeshes werden auf getrennte,
eindeutige Gewichte geprüft. Ergebnisse: `docs/previews/slinger/validation.json`.
Front-, Seiten-, Lauf-, Wurf-, Nachlade- und Todesansichten wurden visuell geprüft.

Die vorhandene Projektilsimulation bleibt unverändert: Abwurf und Treffer
sind noch nicht mit Animationsereignissen synchronisiert. Das separate
Schleuderstein-Projektilasset bleibt in Phase 5.4 offen; derzeit verwendet das
Spiel seine vorhandene Projektilvisualisierung. Keine Fußanpassung an unebenes
Gelände und kein neuer Performance-Nachweis für große Armeen.
