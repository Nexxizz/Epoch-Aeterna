"""Validate and render the saved scout: Blender --background blender/unit_scout.blend --python tools/blender/inspect_scout.py."""
import os
import sys
import math
import json
import bpy
sys.path.insert(0,os.path.dirname(__file__))
import preview

objects=[o for o in bpy.data.objects if o.type in ('MESH','ARMATURE')]
rig=next(o for o in objects if o.type=='ARMATURE')
out=os.path.join(preview.lib_scene.REPO_ROOT,'docs/previews/scout')
os.makedirs(out,exist_ok=True)
expected={'Idle','Walk','Run','Attack','Death'}
assert {a.name for a in bpy.data.actions}==expected
for track in rig.animation_data.nla_tracks:track.mute=True
report={}
for action in bpy.data.actions:
    rig.animation_data.action=action
    end=round(action.frame_range[1])
    first=None
    minimum=10
    for frame in range(end+1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        values=[v for b in rig.pose.bones for row in b.matrix for v in row]
        assert all(math.isfinite(v) for v in values),action.name
        if first is None:first=values
        low,high=preview._bounds(objects)
        minimum=min(minimum,low.z)
    seam=max(abs(a-b) for a,b in zip(first,values))
    if action.name!='Death':assert seam<.002,(action.name,seam)
    assert minimum>-.02,(action.name,'ground penetration',minimum)
    report[action.name]={'seconds':end/30,'loop_error':seam,'minimum_z':minimum}
with open(os.path.join(out,'validation.json'),'w') as f:json.dump(report,f,indent=2)
print('[verify] '+json.dumps(report))
if '--check-only' not in sys.argv:
    scene=bpy.context.scene
    scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=640
    scene.render.resolution_y=640
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='Standard'
    scene.view_settings.look='None'
    for clip,frame,view in [('Idle',0,'front'),('Idle',0,'side'),('Run',6,'three-quarter'),
        ('Attack',27,'three-quarter'),('Death',72,'three-quarter')]:
        preview._apply_pose(objects,clip,frame)
        # Reframe each pose and remove earlier preview lights and cameras.
        for obj in list(bpy.data.objects):
            if obj.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(obj,do_unlink=True)
        preview._frame(objects,view)
        scene.render.filepath=os.path.join(out,f'{clip}_{view}.png')
        bpy.ops.render.render(write_still=True)
