# Stone Age settler

The game model is `assets/models/units/unit_settler.glb`; its editable native
source is `blender/unit_settler.blend`. Both are generated together by:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background --python tools/blender/build_all.py -- unit_settler
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path .
```

The Blender file opens on Idle. Choose another action in the Action Editor;
the NLA tracks are muted in this editing view. The generator enables all tracks
for GLB export. `blender/.gdignore` avoids a second import of the native file.

## Model

24,922 triangles at full detail, 21 bones and 12 material surfaces. Godot's
existing GLB import generates mesh LODs. Army-scale performance has not been
benchmarked. All units retain the existing faction-colour material.

Anatomy uses elliptical profile meshes with explicit blending at knees, elbows
and the torso. Hands have palms, curled fingers and thumbs; independent wrist
and ankle bones allow real grips and foot roll. The hide tunic follows the hips
and thighs, with a shoulder pelt, stitches, moccasin lacing, pouches, a woven
basket and faceted flint tools. Vertex-colour variation is exported as COLOR_0.

Geometry lives in `tools/blender/assets/settler_detail.py`, motion authoring in
`tools/blender/settler_motion.py`, and the export entry in `unit_settler.py`.

## Animation contract

| Clip | Seconds | Behavior |
|---|---:|---|
| Idle | 4.00 | Breathing, relaxed weight shift, stable feet |
| Walk | 1.07 | Planted stance, swing clearance, heel/toe roll |
| Run | 0.80 | Longer stride, stronger arm swing |
| Carry_Walk | 1.20 | Careful steps with a basket |
| Carry_Run | 0.93 | Faster loaded travel |
| Gather_Food | 2.40 | Crouch, reach, lift, deposit toward basket |
| Gather_Chop | 1.60 | Two-hand wind-up, fast axe strike, recoil |
| Gather_Mine | 1.80 | Lower pick strike with torso follow-through |
| Attack | 2.00 | Horizontal spear thrust and recovery |
| Build | 1.60 | Low working posture and controlled hammer stroke |
| Death | 2.40 | Recoil, buckling knees, forward fall, final prone pose |

Clips bake complete local bone transforms at 30 fps. Analytical two-bone IK
keeps working grips together and the standing feet planted during authoring;
the GLB needs no Blender constraints. Loops repeat the initial pose exactly.
Death does not loop. The foot contacts are authored against a flat floor;
per-foot adaptation to uneven terrain is not implemented.

`ModelAnimator` blends state transitions over 0.16 seconds (0.08 for death),
chooses walk/run from actual displacement with hysteresis and adjusts playback
to stride speed. This changes presentation only, not simulation move speed or
damage timings. Work/hit timing remains visual; it does not drive resource or
combat events.

On removal, `ViewManager` removes the living view lookup immediately. A model
with Death keeps its visual corpse for the clip's imported duration, three
seconds of hold and 0.8 seconds of sinking. Selection and health bars disappear
immediately. Corpses still participate in fog visibility, pause and time scale.
Models without Death keep immediate removal.

## Verification and previews

```powershell
dotnet build --no-restore
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify
& 'C:/Godot/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path . -- --verify-presentation
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_settler.blend --python tools/blender/inspect_settler.py
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' --background blender/unit_settler.blend --python tools/blender/settler_reel.py
python tools/blender/assemble_settler_preview.py # Requires Pillow
```

The presentation test instantiates the actual imported GLB and exercises clip
selection, stride speed, death removal, final hold, pause, time scale, cleanup
and the missing-animation fallback. The Blender check asserts finite poses,
closed loops and ground clearance throughout the fall, and writes its measurements
to `docs/previews/settler/validation.json`.

Still poses and the animated overview are in `docs/previews/settler/`. Raw reel
frames are disposable and live under `.godot/settler-reel/`.
