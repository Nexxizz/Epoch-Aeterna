"""Detailed Stone Age settler; exports the game GLB and editable Blender source."""
import os
import bpy
import lib_scene
import lib_mesh
import lib_rig
import settler_detail
import settler_motion

NAME = 'unit_settler'
CATEGORY = 'units'


def build():
    lib_scene.reset()
    bpy.context.scene.render.fps = 30
    armature = lib_rig.humanoid(NAME, 1.8)
    meshes = settler_detail.build()
    lib_rig.bind_rigid(meshes, armature)
    settler_motion.build(armature)
    return [armature, *meshes]


def export():
    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)
    lib_scene.report(path)
    print(f'[asset] {sum(lib_mesh.triangle_count(o) for o in objects if o.type == "MESH")} triangles')
    armature = objects[0]
    for track in armature.animation_data.nla_tracks:
        track.mute = True
    armature.animation_data.action = bpy.data.actions['Idle']
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end = 0, 119
    scene.frame_set(0)
    lib_scene.select([armature])
    armature.show_in_front = True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.region_3d.view_location = (0, 0, .9)
                area.spaces.active.region_3d.view_distance = 3.4
                area.spaces.active.shading.type = 'MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib_scene.REPO_ROOT, 'blender', NAME+'.blend'))
    return path
