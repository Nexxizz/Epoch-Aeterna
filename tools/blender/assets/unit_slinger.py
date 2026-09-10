"""Stone Age slinger with a baked, opening two-cord leather sling."""
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

NAME='unit_slinger'
FOOT_POINTS={}
SLING_BONES=('sling_cord','sling_pouch','sling_release','sling_stone')


def equipment_rig(rig):
    lib_scene.select([rig])
    bpy.ops.object.mode_set(mode='EDIT')
    for name in SLING_BONES:
        bone=rig.data.edit_bones.new(name)
        bone.head=(0,0,0)
        bone.tail=(0,0,.1)
        bone.parent=rig.data.edit_bones['root']
    bpy.ops.object.mode_set(mode='OBJECT')


def place(p,name,origin,direction):
    q=motion.UP.rotation_difference(Vector(direction).normalized())
    p.b[name].matrix=(Matrix.Translation(Vector(origin)) @ q.to_matrix().to_4x4()
        @ p.rig.data.bones[name].matrix_local)
    p.update()


def hand_grip(p):
    hand=p.b['hand.R']
    q=hand.matrix.to_quaternion() @ p.rig.data.bones['hand.R'].matrix_local.to_quaternion().inverted()
    return hand.head + q @ Vector((0,-.032,-.050))


def sling(p,direction,opening=0,loaded=True):
    # Retention cord always starts at the evaluated fingers, even at full reach.
    grip=hand_grip(p)
    direction=Vector(direction).normalized()
    pouch=grip+direction*.50
    place(p,'sling_cord',grip,direction)
    place(p,'sling_pouch',pouch,direction)
    # Release cord pivots around its attachment to the pouch; no stretching.
    release=(-direction).lerp(Vector((-.3,-.85,.15)).normalized(),opening).normalized()
    place(p,'sling_release',pouch+Vector((.012,0,0)),release)
    place(p,'sling_stone',pouch,direction)
    p.b['sling_stone'].scale=(1,1,1) if loaded else (.001,.001,.001)
    p.update()


def idle(p,t):
    motion.idle(p,t)
    p.arm('R',(-.27,-.13,.90+.004*math.sin(t*math.tau)))
    sling(p,(-.18,-.08,-1))


def gait(p,t,run=False):
    motion.gait(p,t,run=run)
    for side in ('L','R'):
        name='foot.'+side
        bone=p.b[name]
        transform=bone.matrix @ p.rig.data.bones[name].matrix_local.inverted()
        bottom=min((transform @ v).z for v in FOOT_POINTS[name])
        if bottom<.003:
            ankle=bone.head.copy(); ankle.z+=.003-bottom
            rotation=bone.matrix.to_quaternion()
            end=p.ik('leg_upper.'+side,'leg_lower.'+side,ankle,(ankle.x,-1,.45))
            p.world(name,end,rotation)
    p.arm('R',(-.28,-.17,.97+.025*math.cos(t*math.tau)))
    sling(p,(-.3,.14*math.sin(t*math.tau),-1))


def attack(p,t):
    wind=motion.sample([(0,0),(.23,-1),(.48,-.6),(.60,1),(.72,.6),(1,0)],t)
    p.base(z=-.035,lean=3+6*max(0,wind),twist=24*wind)
    p.stand(spread=.145,left_y=-.13,right_y=.14)
    grip=motion.sample([(0,(-.27,-.13,.90)),(.16,(-.29,-.20,1.18)),
        (.28,(-.37,.02,1.82)),(.48,(-.37,-.02,1.84)),
        (.60,(-.30,-.47,1.47)),(.70,(-.27,-.44,1.15)),
        (.86,(-.27,-.13,.90)),(1,(-.27,-.13,.90))],t)
    p.arm('R',grip,pole=(-.85,.10,1.4))
    # Free hand balances the cast, then returns to the ammunition bag.
    p.arm('L',motion.sample([(0,(.19,-.14,.98)),(.30,(.30,-.30,1.23)),
        (.60,(.29,-.31,1.16)),(.82,(.20,-.11,.97)),
        (.90,(.2275,-.13,.95)),(.92,(.2275,-.13,.95)),(1,(.19,-.14,.98))],t))
    if .25<=t<=.52:
        a=(t-.25)/.27*math.tau
        direction=Vector((-math.cos(a),math.sin(a),.28)).normalized()
    else:
        direction=motion.sample([(0,(-.18,-.08,-1)),(.25,(-1,0,.28)),
            (.52,(-1,0,.28)),(.62,(-.35,-1,.15)),(.75,(-.4,-.3,-.7)),
            (.82,(1,0,.10)),(.92,(1,0,.10)),(1,(-.18,-.08,-1))],t)
    opening=motion.sample([(0,0),(.55,0),(.62,1),(.76,.8),(.90,0),(1,0)],t)
    sling(p,direction,opening,not (.57<t<.90))


def death(p,t):
    motion.death(p,t)
    direction=motion.sample([(0,(-.18,-.08,-1)),(.35,(-.5,-.5,-.2)),
        (.55,(-.75,-.65,0)),(1,(-.75,-.65,0))],t)
    sling(p,direction,0,True)


def build():
    lib_scene.reset()
    bpy.context.scene.render.fps=30
    rig=lib_rig.humanoid(NAME,1.8)
    equipment_rig(rig)
    kit=detail.leather()
    kit += [detail.oval('stone_bag',(.18,-.02,.98),(.088,.077,.115),'hips'),
        detail.oval('bag_rim',(.18,-.02,1.075),(.09,.08,.022),'hips')]
    stones=[detail.oval('ammunition',(.18+x,-.02+y,1.086),(.022,.023,.018),'hips',8,5)
        for x,y in ((-.036,0),(.015,-.03),(.037,.016))]
    hair=detail.hair()
    for obj in list(hair):
        if obj.name.startswith('beard'):
            hair.remove(obj); bpy.data.objects.remove(obj,do_unlink=True)
    marks=detail.team()
    marks.append(detail.profile('headband',[(0,.015,1.662,.121,.119),
        (0,.017,1.691,.118,.117)],lambda p:{'head':1},32))
    cords=detail.cord('retention_cord',[(0,0,0),(0,0,.50)],.0065,'sling_cord')
    release=detail.cord('release_cord',[(0,0,0),(0,0,.50)],.0065,'sling_release')
    pouch=[detail.oval('sling_cradle',(0,0,0),(.04,.022,.065),'sling_pouch',16,8)]
    stone=[detail.oval('loaded_stone',(0,-.022,0),(.029,.025,.037),'sling_stone',10,6)]
    groups=[(detail.body(),'slinger_body',mat.pbr('slinger_skin',(.43,.245,.135),.8)),
        (detail.garment(),'slinger_hide',mat.pbr('slinger_hide',(.40,.30,.17),.96)),
        (kit+pouch,'slinger_leather',mat.pbr('slinger_leather',(.18,.081,.032),.95)),
        (hair,'slinger_hair',mat.pbr('slinger_hair',(.065,.035,.019),.98)),
        # Identical rest geometry must stay separate: merge-by-distance would
        # blend the two bones' weights and collapse the opposed cords to a point.
        (marks,'clan_marks',mat.team_color()),(cords,'sling_retention',mat.fibre()),
        (release,'sling_release_mesh',mat.fibre()),
        (stones+stone,'slinger_stones',mat.pbr('slinger_stone',(.30,.32,.32),.90)),
        ([detail.oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6)
          for s in (-1,1)],'slinger_eyes',mat.pbr('slinger_eyes',(.48,.40,.29),.7))]
    meshes=[]
    for parts,name,material in groups:
        obj=geo.join(parts,name);mat.assign(obj,material)
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
    motion.build(rig,[('Idle',120,idle,None),('Walk',32,gait,None),
        ('Run',24,lambda p,t:gait(p,t,run=True),None),
        ('Attack',60,attack,None),('Death',72,death,None)])
    return [rig,*meshes]


def export():
    objects=build()
    path=lib_scene.export_glb(objects,'units',NAME)
    lib_scene.report(path)
    print(f'[asset] {sum(geo.triangle_count(o) for o in objects if o.type=="MESH")} triangles')
    rig=objects[0]
    for track in rig.animation_data.nla_tracks:track.mute=True
    rig.animation_data.action=bpy.data.actions['Idle']
    scene=bpy.context.scene
    scene.frame_start,scene.frame_end=0,119
    scene.frame_set(0)
    lib_scene.select([rig]);rig.show_in_front=True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_location=(0,0,.9)
                area.spaces.active.region_3d.view_distance=3.4
                area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib_scene.REPO_ROOT,'blender',NAME+'.blend'))
    return path
