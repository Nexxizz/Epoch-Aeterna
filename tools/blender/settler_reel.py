"""Render every clip at 10 fps for an animated review sheet (no rebuild)."""
import os
import sys
import json
import bpy
from mathutils import Vector
sys.path.insert(0,os.path.dirname(__file__))
import preview

ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'.godot/settler-reel')
os.makedirs(OUT,exist_ok=True)
objects=[o for o in bpy.data.objects if o.type in ('MESH','ARMATURE')]
preview._apply_pose(objects,'Idle',0)
preview._frame(objects)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.eevee.taa_render_samples=16
scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='Standard';scene.view_settings.look='None'
camera=scene.camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.45
target=Vector((0,-.10,.86));camera.location=target+Vector((2.6,-4.2,2.1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.008))
floor=bpy.context.object
material=bpy.data.materials.new('Preview_floor');material.diffuse_color=(.13,.155,.18,1)
floor.data.materials.append(material)
sequence=[('Idle','Ruhe'),('Walk','Gehen'),('Run','Laufen'),('Carry_Run','Tragen'),
          ('Gather_Food','Sammeln'),('Gather_Chop','Holz hacken'),('Gather_Mine','Abbauen'),
          ('Attack','Angreifen'),('Build','Bauen'),('Death','Sterben')]
manifest=[]
only=next((arg.split('=',1)[1] for arg in sys.argv if arg.startswith('--clip=')),None)
for clip,label in sequence:
    preview._apply_pose(objects,clip,0)
    length=round(bpy.data.actions[clip].frame_range[1])
    for frame in range(0,length+1,3):
        scene.frame_set(frame)
        path=os.path.join(OUT,f'{len(manifest):04}.png')
        if only is None or only==clip:
            scene.render.filepath=path;bpy.ops.render.render(write_still=True)
        manifest.append({'path':path,'label':label,'clip':clip,'frame':frame})
with open(os.path.join(OUT,'manifest.json'),'w',encoding='utf-8') as f:json.dump(manifest,f)
