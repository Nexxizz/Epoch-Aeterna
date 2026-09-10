# Kupferzeit-Schwertkämpfer

Editierbare Quelle: `blender/unit_swordsman.blend`.
Spielmodell: `assets/models/units/unit_swordsman.glb`.
Generator: `tools/blender/assets/unit_swordsman.py`.
Portrait: `docs/previews/swordsman_final.png`.

Kupferschwert mit Blattklinge, Rundschild mit Lederkante und Kupferbuckel,
Kupferhelm, Brustpanzer und Schulterkappen. Schärpe, Armbänder und Schildstreifen
tragen die Spielerfarbe. 12.906 Dreiecke, acht Meshes und Materialien.
Das gemeinsame Skelett mit 21 Knochen wird um `shield` ergänzt; das Schwert
nutzt den vorhandenen Slot `tool_axe`. Alle Bewegungen werden mit 30 fps gebacken.

| Clip | Dauer | Bewegung |
|---|---|---|
| Idle | 4,00 s | Atmung, aufrechtes Schwert und Schild vor dem Körper |
| Walk | 1,07 s | Gehen mit korrigiertem Sohlenkontakt |
| Run | 0,80 s | Lauf mit stabil gehaltener Ausrüstung |
| Attack | 1,50 s | Ausholen, Abwärtshieb, Rückkehr in Deckung |
| Death | 2,40 s | Einknicken, Fallen, Ausrüstung neben dem Körper |

Der rechte Griff folgt dem Schwertanker; die Schildposition wird von der
ausgewerteten linken Hand abgeleitet. In der Todesanimation werden beide
Gegenstände losgelassen und am Boden abgelegt. Die Blender-Datei öffnet mit
Idle und stummgeschalteten NLA-Tracks; der GLB-Export enthält alle fünf Clips.

Die bestehenden Spielwerte bleiben erhalten: 70 Nahrung, 40 Gold, 18 Sekunden
Ausbildung, 150 HP, Rüstung 3, Tempo 2,7, Schaden 18, Reichweite 1,3 und
Angriffspause 1,5 s. Die Kasernenausbildung ist bis zur Kupferzeit gesperrt.

## Reproduzieren und prüfen

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/build_all.py -- unit_swordsman
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python tools/blender/preview.py -- unit_swordsman C:/Projects/Epoch-Aeterna/docs/previews/swordsman_final.png three-quarter Idle:0
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_swordsman.blend --python-exit-code 1 --python tools/blender/inspect_swordsman.py
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
```

Build ohne Warnungen oder Fehler; 154 Simulations- und 145
Präsentationsprüfungen bestanden. Neue Prüfungen decken das importierte Modell,
fünf Clips, sichtbare Ausrüstung, Bewegung, Angriff, Todeszustand und den
tatsächlichen Kasernenbutton ab: Portrait, Zeitaltersperre, Freischaltung,
Warteschlange und Ressourcenabbuchung.

Die Blender-Prüfung kontrolliert jeden Frame auf endliche Transformationen,
Bodenfreiheit, Schleifennähte sowie die Verbindung beider Hände zur Ausrüstung
(Toleranz 5 mm). Ergebnisse: `docs/previews/swordsman/validation.json`.
Front-, Seiten-, Lauf-, Aushol-, Schlag- und Todesansichten wurden visuell geprüft.

Die Angriffsdauer entspricht dem Cooldown. Treffer werden weiterhin von der
Simulation ausgelöst; ein synchronisiertes Treffer-Animationsevent bleibt im
MVP-Plan offen. Keine Laufzeit-Fußanpassung an unebenes Gelände und kein neuer
Performance-Nachweis für große Armeen.
