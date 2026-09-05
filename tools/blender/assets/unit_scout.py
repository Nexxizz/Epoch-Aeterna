"""Stone Age pathfinder: light hide, clan headband, travel bag and stone club."""
import math
import os
import bpy
import lib_scene
import lib_mesh as geo
import lib_material as mat
import lib_rig
import settler_detail as detail
import settler_motion as motion

NAME = 'unit_scout'
FOOT_POINTS = {}


def outfit():
    # Short, sleeveless hide vest and split skirt retain the shared skin weights.
    parts = detail.garment()
    for v in parts[0].data.vertices:
        if v.co.z < .94:
            v.co.z += .045 * (1 - (v.co.z-.795)/.145)
    parts.append(detail.profile('back_mantle',[(0,.085,1.14,.125,.04),
        (0,.09,1.30,.19,.045),(0,.055,1.43,.17,.075)],detail.torso,24,.025))
    for sign in (-1,1):
        points=[(sign*.11,-.116,1.35),(sign*.11,-.09,1.43),
                (sign*.11,-.03,1.466),(sign*.11,.055,1.452),(sign*.11,.116,1.35)]
        verts=[(x+dx,y,z) for x,y,z in points for dx in (-.028,.028)]
        parts.append(detail.mesh('hide_shoulder_strap',verts,
            [(2*i,2*i+1,2*i+3,2*i+2) for i in range(4)], [{'chest':1}]*10))
    return parts


def kit():
    parts = detail.leather()
    parts += [detail.oval('travel_satchel',(.17,.105,1.03),(.082,.054,.112),'hips'),
              detail.oval('satchel_flap',(.17,.149,1.085),(.082,.018,.057),'hips')]
    parts += detail.cord('satchel_strap',[(.17,.14,1.03),(.16,.105,1.29),
        (.10,.06,1.445),(.065,-.08,1.425),(.13,-.13,1.23),(.17,-.02,1.04)],.011,'chest')
    return parts


def markings():
    parts = detail.team()
    parts.append(detail.profile('clan_headband',[(0,.015,1.653,.121,.119),
        (0,.017,1.682,.120,.119)],lambda p:{'head':1},32))
    parts += detail.cord('headband_ties',[(.08,.09,1.68),(.10,.135,1.61),
        (.09,.14,1.52)],.012,'head')
    return parts


def attack(p,t):
    swing = motion.sample([(0,20),(.3,-42),(.46,100),(.57,84),(.78,25),(1,20)],t)
    p.base(z=-.06,lean=5+max(0,swing)*.07,twist=-swing*.16)
    p.stand(spread=.15,left_y=-.16,right_y=.12)
    grip = motion.sample([(0,(-.23,-.19,1.08)),(.3,(-.26,-.06,1.48)),
        (.46,(-.17,-.49,1.03)),(.57,(-.18,-.43,1.02)),(1,(-.23,-.19,1.08))],t)
    angle=math.radians(swing)
    p.tool('tool_axe',grip,(0,-math.sin(angle),math.cos(angle)),False)
    p.arm('L',(.24,-.22,1.02))


def idle(p,t):
    motion.idle(p,t)
    p.rotate('head',(-2,12*math.sin(t*math.tau),0))
    p.update()


def gait(p,t,run=False):
    motion.gait(p,t,run=run)
    # The shared gait's sole pivot predates these rounded moccasins. Correct
    # each ankle from its actual skinned sole, without raising the whole body.
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


def build():
    lib_scene.reset()
    bpy.context.scene.render.fps=30
    rig=lib_rig.humanoid(NAME,1.8)
    hair=detail.hair()
    # Shorter facial hair gives the scout a separate face silhouette.
    for obj in list(hair):
        if obj.name.startswith('beard'):
            hair.remove(obj)
            bpy.data.objects.remove(obj,do_unlink=True)
    bindings=[]
    for z in (.77,.79,.81,1.065,1.085,1.105):
        bindings += detail.ring('club_lashing',(-.19,-.032,z),.027,.029,.004,'tool_axe',12)
    bindings += detail.ring('collar',(0,0,1.43),.082,.07,.007,'chest',24)
    groups=[(detail.body(),'scout_body',mat.pbr('scout_skin',(.43,.245,.135),.8)),
        (outfit(),'scout_hide',mat.pbr('scout_hide',(.22,.245,.12),.96)),
        (kit(),'scout_kit',mat.pbr('scout_leather',(.18,.081,.032),.95)),
        (hair,'scout_hair',mat.pbr('scout_hair',(.065,.035,.019),.98)),
        (markings(),'clan_marks',mat.team_color()),
        (bindings,'scout_sinew',mat.bone()),
        ([detail.pin(geo.limb('club_haft',(-.19,-.032,.66),(-.19,-.032,1.13),
            .021,.027,vertices=12),'tool_axe')],'club_wood',mat.wood()),
        ([detail.oval('club_stone',(-.19,-.032,1.09),(.055,.068,.077),'tool_axe',8,5)],
            'club_stone',mat.pbr('scout_stone',(.25,.27,.25),.88)),
        ([detail.oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6)
          for s in (-1,1)],'scout_eyes',mat.pbr('scout_eyes',(.48,.40,.29),.7))]
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
    clips=[('Idle',120,idle,'tool_axe'),
           ('Walk',32,gait,'tool_axe'),
           ('Run',24,lambda p,t:gait(p,t,run=True),'tool_axe'),
           ('Attack',60,attack,'tool_axe'),('Death',72,motion.death,'tool_axe')]
    motion.build(rig,clips)
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
                area.spaces.active.region_3d.view_location=(0,0,.9)
                area.spaces.active.region_3d.view_distance=3.4
                area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib_scene.REPO_ROOT,'blender',NAME+'.blend'))
    return path
