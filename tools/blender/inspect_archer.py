"""Validate and render the saved archer: Blender --background blender/unit_archer.blend --python tools/blender/inspect_archer.py."""
import os
import sys
import math
import json
import bpy
from mathutils import Vector
sys.path.insert(0,os.path.dirname(__file__))
import preview

objects=[o for o in bpy.data.objects if o.type in ('MESH','ARMATURE')]
rig=next(o for o in objects if o.type=='ARMATURE')
out=os.path.join(preview.lib_scene.REPO_ROOT,'docs/previews/archer')
os.makedirs(out,exist_ok=True)
expected={'Idle','Walk','Run','Attack','Death'}
assert {a.name for a in bpy.data.actions}==expected
for track in rig.animation_data.nla_tracks:track.mute=True
report={}
for action in bpy.data.actions:
    rig.animation_data.action=action
    end_frame=round(action.frame_range[1])
    first=None
    minimum=10
    connection_error=0
    for frame in range(end_frame+1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        values=[v for b in rig.pose.bones for row in b.matrix for v in row]
        assert all(math.isfinite(v) for v in values),action.name
        if first is None:first=values
        low,high=preview._bounds(objects)
        minimum=min(minimum,low.z)
        def end(name,z):
            return rig.pose.bones[name].matrix @ rig.data.bones[name].matrix_local.inverted() @ Vector((0,0,z))
        for side in ('upper','lower'):
            connection_error=max(connection_error,(end('bow_'+side+'_0',1)-end('bow_'+side+'_1',0)).length,
                (end('bow_'+side+'_1',1)-end('string_'+side,1)).length)
        if action.name!='Death':
            hand=rig.pose.bones['hand.L']
            q=hand.matrix.to_quaternion() @ rig.data.bones[hand.name].matrix_local.to_quaternion().inverted()
            connection_error=max(connection_error,(end('bow_upper_0',0)-hand.head-q @ Vector((0,-.032,-.050))).length)
        if action.name=='Attack' and .23<=frame/end_frame<=.63:
            hand=rig.pose.bones['hand.R']
            q=hand.matrix.to_quaternion() @ rig.data.bones[hand.name].matrix_local.to_quaternion().inverted()
            hand_position=hand.head+q @ Vector((0,-.032,-.050))
            connection_error=max(connection_error,(end('string_upper',0)-hand_position).length,
                (end('nocked_arrow',0)-hand_position).length)
    seam=max(abs(a-b) for a,b in zip(first,values))
    if action.name!='Death':assert seam<.002,(action.name,seam)
    assert minimum>-.02,(action.name,'ground penetration',minimum)
    assert connection_error<.001,(action.name,'bow connection',connection_error)
    report[action.name]={'seconds':end_frame/30,'loop_error':seam,'minimum_z':minimum,'connection_error':connection_error}
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
        ('Attack',28,'three-quarter'),('Attack',28,'side'),('Attack',39,'front'),('Death',72,'three-quarter')]:
        preview._apply_pose(objects,clip,frame)
        # Reframe each pose and remove earlier preview lights and cameras.
        for obj in list(bpy.data.objects):
            if obj.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(obj,do_unlink=True)
        preview._frame(objects,view)
        scene.render.filepath=os.path.join(out,f'{clip}_{view}.png')
        bpy.ops.render.render(write_still=True)


