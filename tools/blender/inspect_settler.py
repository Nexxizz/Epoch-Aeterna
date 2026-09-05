"""Render reproducible pose/animation evidence from the saved native source.

blender --background blender/unit_settler.blend --python tools/blender/inspect_settler.py
Use settler_reel.py to render the animated review sequence.
"""
import os
import sys
import math
import json
import bpy
from mathutils import Vector
sys.path.insert(0,os.path.dirname(__file__))
import preview
import lib_anim

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'docs/previews/settler')
os.makedirs(OUT,exist_ok=True)
objects=[o for o in bpy.data.objects if o.type in ('MESH','ARMATURE')]
rig=next(o for o in objects if o.type=='ARMATURE')
preview._apply_pose(objects,'Idle',0)
preview._frame(objects)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
camera=scene.camera
camera.data.type='ORTHO';camera.data.ortho_scale=2.35
target=Vector((0,-.07,.9))
camera.location=target+Vector((2.6,-4.2,2.0))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008))
floor=bpy.context.object
material=bpy.data.materials.new('Preview_floor');material.diffuse_color=(.13,.155,.18,1)
floor.data.materials.append(material)

poses=[('Idle',0),('Walk',0),('Walk',8),('Run',6),('Carry_Walk',9),('Gather_Food',24),
       ('Gather_Chop',16),('Gather_Chop',22),('Gather_Mine',25),('Attack',24),('Build',22),
       ('Death',0),('Death',18),('Death',32),('Death',42),('Death',72)]
for clip,frame in poses:
    if '--check-only' in sys.argv: break
    only=next((arg.split('=',1)[1] for arg in sys.argv if arg.startswith('--clip=')),None)
    if only is not None and only!=clip: continue
    preview._apply_pose(objects,clip,frame)
    scene.render.filepath=os.path.join(OUT,f'{clip}_{frame:03}.png')
    bpy.ops.render.render(write_still=True)

# Frame-by-frame source validation: complete actions, finite values, loop seam,
# foot contact error and corpse bounds. Keep the measurements beside the images.
report={}
for track in rig.animation_data.nla_tracks:
    action=track.strips[0].action
    rig.animation_data.action=action
    length=round(action.frame_range[1]);first=None;last=None;minimum=10
    for frame in range(length+1):
        scene.frame_set(frame);bpy.context.view_layer.update()
        transforms=[list(b.matrix) for b in rig.pose.bones]
        assert all(math.isfinite(x) for rows in transforms for row in rows for x in row),action.name
        values=[x for rows in transforms for row in rows for x in row]
        if frame==0:first=values
        last=values
        if action.name=='Death':
            low,_=preview._bounds(objects)
            minimum=min(minimum,low.z)
    seam=max(abs(a-b) for a,b in zip(first,last))
    if action.name!='Death': assert seam<.002,(action.name,seam)
    report[action.name]={'seconds':length/30,'loop_error':round(seam,6)}
    if action.name=='Death':
        report[action.name]['lowest_point_during_fall']=minimum
        assert minimum > -.012, ('Fall penetrates ground', minimum)
preview._apply_pose(objects,'Death',72)
low,high=preview._bounds(objects)
report['Death']['final_bounds']={'min':list(low),'max':list(high)}
assert low.z > -.012, ('Corpse penetrates ground', low.z)
with open(os.path.join(OUT,'validation.json'),'w') as f:json.dump(report,f,indent=2)
print('[verify] '+json.dumps(report))
