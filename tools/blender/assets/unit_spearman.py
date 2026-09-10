"""Stone Age infantry: reinforced hide, clan sash and a two-handed flint spear."""
import math
import os
import bpy
from mathutils import Vector, Matrix
import lib_scene
import lib_mesh as geo
import lib_material as mat
import lib_rig
import settler_detail as detail
import settler_motion as motion

NAME = 'unit_spearman'
FOOT_POINTS = {}


def spear_pose(p, grip, direction):
    # Wider grip than a working tool: both hands stay on the same shaft.
    direction = Vector(direction).normalized()
    p.tool('tool_spear', grip, direction, False)
    q = motion.UP.rotation_difference(direction)
    p.arm('L', Vector(grip) + direction * .25, q)


def idle(p, t):
    motion.idle(p, t)
    spear_pose(p, (-.23, -.18, .99 + .004*math.sin(t*math.tau)), (.36, -.35, .87))


def attack(p, t):
    thrust = motion.sample([(0,0),(.28,-.30),(.40,1),(.48,.88),(.72,0),(1,0)],t)
    p.base(y=-.045*thrust,z=-.08-.025*max(0,thrust),lean=7+7*thrust,twist=-8-12*thrust)
    p.stand(spread=.15,left_y=-.24,right_y=.22)
    spear_pose(p,(-.13,-.18-.12*thrust,1.16),(.18,-1,.055))


def gait(p, t, run=False):
    motion.gait(p,t,run=run)
    for side in ('L','R'):
        name='foot.'+side
        bone=p.b[name]
        transform=bone.matrix @ p.rig.data.bones[name].matrix_local.inverted()
        bottom=min((transform @ v).z for v in FOOT_POINTS[name])
        if bottom<.003:
            ankle=bone.head.copy()
            ankle.z+=.003-bottom
            rotation=bone.matrix.to_quaternion()
            end=p.ik('leg_upper.'+side,'leg_lower.'+side,ankle,(ankle.x,-1,.45))
            p.world(name,end,rotation)
    spear_pose(p,(-.23,-.20,.98+.018*math.cos(t*math.tau*2)),(.36,-.55 if run else -.35,.87))


def death(p, t):
    motion.death(p,t)
    # Release the weapon as the knees buckle, then let it settle beside the body.
    grip=motion.sample([(0,(-.23,-.18,.99)),(.16,(-.29,-.18,.95)),
        (.52,(-.49,-.30,.085)),(.60,(-.50,-.31,.105)),(.72,(-.50,-.31,.082)),
        (1,(-.50,-.31,.082))],t)
    direction=motion.sample([(0,(.36,-.35,.87)),(.52,(-.12,-1,0)),(1,(-.12,-1,0))],t).normalized()
    q=motion.UP.rotation_difference(direction)
    p.b['tool_spear'].matrix=(Matrix.Translation(grip) @ q.to_matrix().to_4x4()
        @ Matrix.Translation(Vector((.19,.032,-.760)))
        @ p.rig.data.bones['tool_spear'].matrix_local)
    p.update()


def weapon():
    wood=[detail.pin(geo.limb('spear_haft',(-.19,-.032,.12),(-.19,-.032,1.94),
        .022,.015,vertices=12),'tool_spear')]
    # Faceted leaf blade with a central ridge, visibly larger than the hunting spear.
    outline=[(-.032,2.17),(-.108,1.98),(-.064,1.89),(-.004,1.89),(.044,1.98)]
    verts=[(-.19+s*.021,y,z) for s in (-1,1) for y,z in outline]
    verts += [(-.221,-.032,1.99),(-.159,-.032,1.99)]
    faces=[]
    for i in range(5):
        j=(i+1)%5
        faces.extend([(i,j,10),(5+j,5+i,11),(i,5+i,5+j,j)])
    stone=[detail.mesh('flint_spearhead',verts,faces,[{'tool_spear':1}]*12,False)]
    bindings=[]
    for z in (1.86,1.875,1.89,1.905,1.92):
        bindings+=detail.ring('spear_lashing',(-.19,-.032,z),.026,.028,.005,'tool_spear',12)
    return wood,stone,bindings


def build():
    lib_scene.reset()
    bpy.context.scene.render.fps=30
    rig=lib_rig.humanoid(NAME,1.8)
    wood,stone,bindings=weapon()
    armor=detail.leather()
    # Overlapping chest hide strips and a broad belt distinguish infantry from scouts.
    for x in (-.125,-.075,-.025,.025,.075,.125):
        armor.append(detail.profile('reinforced_hide',[(x,-.108,1.09,.026,.018),
            (x,-.131,1.26,.028,.018),(x,-.115,1.37,.025,.018)],detail.torso,8))
    groups=[(detail.body(),'spearman_body',mat.pbr('spearman_skin',(.43,.245,.135),.8)),
        (detail.garment(),'spearman_hide',mat.pbr('spearman_hide',(.34,.17,.065),.96)),
        (armor,'spearman_leather',mat.pbr('spearman_leather',(.145,.07,.032),.95)),
        (detail.fur(),'spearman_pelt',mat.pbr('spearman_pelt',(.24,.20,.155),1)),
        (detail.hair(),'spearman_hair',mat.pbr('spearman_hair',(.065,.035,.019),.98)),
        (detail.team(),'clan_marks',mat.team_color()),
        (bindings,'spear_bindings',mat.bone()),(wood,'spear_wood',mat.wood()),
        (stone,'spear_flint',mat.pbr('spearman_flint',(.25,.28,.30),.83)),
        ([detail.oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6)
          for s in (-1,1)],'spearman_eyes',mat.pbr('spearman_eyes',(.48,.40,.29),.7))]
    meshes=[]
    for parts,name,material in groups:
        obj=geo.join(parts,name)
        mat.assign(obj,material)
        colors=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co
            c=1-.09*(.5+.5*math.sin(p.x*119+p.z*37)*math.sin(p.y*93-p.z*71))
            colors.data[v.index].color=(c,c,c,1)
        meshes.append(obj)
    for name in ('foot.L','foot.R'):
        FOOT_POINTS[name]=[obj.matrix_world@v.co for obj in meshes
            if obj.vertex_groups.get(name) is not None for v in obj.data.vertices
            if any(g.group==obj.vertex_groups[name].index and g.weight>.99 for g in v.groups)]
    lib_rig.bind_rigid(meshes,rig)
    motion.build(rig,[('Idle',120,idle,'tool_spear'),('Walk',32,gait,'tool_spear'),
        ('Run',24,lambda p,t:gait(p,t,run=True),'tool_spear'),
        ('Attack',48,attack,'tool_spear'),('Death',72,death,'tool_spear')])
    return [rig,*meshes]


def export():
    objects=build()
    path=lib_scene.export_glb(objects,'units',NAME)
    lib_scene.report(path)
    print(f'[asset] {sum(geo.triangle_count(o) for o in objects if o.type=="MESH")} triangles')
    rig=objects[0]
    for track in rig.animation_data.nla_tracks: track.mute=True
    rig.animation_data.action=bpy.data.actions['Idle']
    scene=bpy.context.scene
    scene.frame_start,scene.frame_end=0,119
    scene.frame_set(0)
    lib_scene.select([rig])
    rig.show_in_front=True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_location=(0,0,1.2)
                area.spaces.active.region_3d.view_distance=4
                area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib_scene.REPO_ROOT,'blender',NAME+'.blend'))
    return path
