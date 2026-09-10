"""Copper Age heavy infantry: copper sword, round shield, helmet and cuirass."""
import math
import os
import bpy
from mathutils import Vector, Matrix, Quaternion
import lib_scene
import lib_mesh as geo
import lib_material as mat
import lib_rig
import settler_detail as detail
import settler_motion as motion

NAME='unit_swordsman'
FOOT_POINTS={}


def shield_pose(p,grip):
    p.arm('L',grip)
    hand=p.b['hand.L']
    q=hand.matrix.to_quaternion() @ p.rig.data.bones['hand.L'].matrix_local.to_quaternion().inverted()
    centre=hand.head+q @ Vector((0,-.032,-.050))+Vector((0,-.08,0))
    p.b['shield'].matrix=Matrix.Translation(centre) @ p.rig.data.bones['shield'].matrix_local
    p.update()


def idle(p,t):
    motion.idle(p,t)
    p.tool('tool_axe',(-.28,-.15,1.00+.004*math.sin(t*math.tau)),(-.25,-.3,.92),False)
    shield_pose(p,(.26,-.24,1.12))


def attack(p,t):
    swing=motion.sample([(0,15),(.30,-42),(.46,112),(.57,100),(.78,30),(1,15)],t)
    p.base(z=-.055,lean=4+max(0,swing)*.05,twist=-swing*.16)
    p.stand(spread=.15,left_y=-.17,right_y=.13)
    grip=motion.sample([(0,(-.29,-.20,1.16)),(.30,(-.32,.02,1.62)),
        (.46,(-.27,-.46,1.08)),(.57,(-.27,-.43,1.02)),
        (.78,(-.29,-.25,1.13)),(1,(-.29,-.20,1.16))],t)
    angle=math.radians(swing)
    p.tool('tool_axe',grip,(-.18,-math.sin(angle),math.cos(angle)),False)
    shield_pose(p,(.24,-.30,1.18))


def gait(p,t,run=False):
    motion.gait(p,t,run=run)
    for side in ('L','R'):
        name='foot.'+side
        bone=p.b[name]
        transform=bone.matrix @ p.rig.data.bones[name].matrix_local.inverted()
        bottom=min((transform @ v).z for v in FOOT_POINTS[name])
        if bottom<.003:
            ankle=bone.head.copy();ankle.z+=.003-bottom
            rotation=bone.matrix.to_quaternion()
            end=p.ik('leg_upper.'+side,'leg_lower.'+side,ankle,(ankle.x,-1,.45))
            p.world(name,end,rotation)
    p.tool('tool_axe',(-.29,-.20,1.03+.015*math.cos(t*math.tau*2)),(-.3,-.4,.85),False)
    shield_pose(p,(.27,-.24,1.13+.015*math.cos(t*math.tau*2)))


def death(p,t):
    motion.death(p,t)
    grip=motion.sample([(0,(-.28,-.15,1)),(.16,(-.32,-.18,.96)),
        (.55,(-.50,-.50,.05)),(.63,(-.52,-.52,.07)),(.76,(-.52,-.52,.04)),
        (1,(-.52,-.52,.04))],t)
    direction=motion.sample([(0,(-.25,-.3,.92)),(.55,(-.4,-1,0)),(1,(-.4,-1,0))],t).normalized()
    q=motion.UP.rotation_difference(direction)
    p.b['tool_axe'].matrix=(Matrix.Translation(grip) @ q.to_matrix().to_4x4()
        @ Matrix.Translation(Vector((.19,.032,-.760))) @ p.rig.data.bones['tool_axe'].matrix_local)
    angle=math.pi/2*motion.smooth(t/.60)
    centre=motion.sample([(0,(.26,-.32,1.12)),(.60,(.56,-.67,.125)),
        (.72,(.57,-.68,.115)),(1,(.57,-.68,.115))],t)
    centre.z=max(centre.z,.31*math.cos(angle)+.115)
    p.b['shield'].matrix=(Matrix.Translation(centre)
        @ Quaternion((1,0,0),angle).to_matrix().to_4x4() @ p.rig.data.bones['shield'].matrix_local)
    p.update()


def weapon():
    # Symmetric leaf blade with a central ridge; grip follows the shared tool anchor.
    outline=[(-.19,1.66),(-.247,1.43),(-.229,.91),(-.151,.91),(-.133,1.43)]
    verts=[(x,-.032+s*.009,z) for s in (-1,1) for x,z in outline]
    verts += [(-.19,-.05,1.26),(-.19,-.014,1.26)]
    faces=[]
    for i in range(5):
        j=(i+1)%5;faces.extend([(i,j,10),(5+j,5+i,11),(i,5+i,5+j,j)])
    metal=[detail.mesh('copper_blade',verts,faces,[{'tool_axe':1}]*12,False),
        detail.pin(geo.box('short_guard',(.16,.04,.03),(-.19,-.032,.885),bevel=.009,segments=1),'tool_axe'),
        detail.oval('pommel',(-.19,-.032,.665),(.035,.029,.03),'tool_axe',12,6)]
    leather=[detail.pin(geo.limb('sword_grip',(-.19,-.032,.68),(-.19,-.032,.875),.024,.024,vertices=12),'tool_axe')]
    for z in (.70,.73,.76,.79,.82,.85):
        leather+=detail.ring('grip_wrap',(-.19,-.032,z),.025,.025,.004,'tool_axe',12)
    return metal,leather


def build():
    lib_scene.reset();bpy.context.scene.render.fps=30
    rig=lib_rig.humanoid(NAME,1.8)
    lib_scene.select([rig]);bpy.ops.object.mode_set(mode='EDIT')
    bone=rig.data.edit_bones.new('shield');bone.head=(0,0,0);bone.tail=(0,0,.1)
    bone.parent=rig.data.edit_bones['root'];bpy.ops.object.mode_set(mode='OBJECT')
    metal,grip=weapon()
    metal.append(detail.profile('cuirass',[(0,0,1.07,.161,.117),(0,0,1.17,.16,.117),
        (0,0,1.28,.192,.134),(0,0,1.37,.203,.135),(0,0,1.405,.18,.117)],detail.torso,32))
    metal.append(detail.profile('helmet',[(0,.018,1.682,.124,.122),(0,.018,1.72,.128,.125),
        (0,.018,1.78,.098,.101),(0,.018,1.818,.054,.057),(0,.018,1.832,.003,.003)],lambda p:{'head':1},32))
    for sign,side in ((1,'L'),(-1,'R')):
        metal.append(detail.oval('shoulder_guard',(sign*.20,0,1.397),(.083,.091,.068),'arm_upper.'+side,16,8))
    metal.append(detail.oval('shield_boss',(0,-.066,0),(.08,.045,.08),'shield',16,8))
    shield=[detail.pin(geo.limb('shield_board',(0,-.035,0),(0,.025,0),.30,.30,vertices=32),'shield')]
    rim=detail.cord('shield_rim',[(.30*math.cos(i*math.tau/40),-.022,.30*math.sin(i*math.tau/40)) for i in range(41)],.017,'shield')
    marks=detail.team()
    # Move the sash beyond the copper breastplate.
    for v in marks[0].data.vertices:v.co.y-=.025
    marks.append(detail.pin(geo.box('shield_clan_stripe',(.065,.012,.52),(0,-.045,0),bevel=.004,segments=1),'shield'))
    hair=detail.hair()
    # Helmet covers the scalp; keep face details and beard without intersecting hair.
    for obj in list(hair):
        if obj.name.startswith(('open_hairline','hair_lock')):
            hair.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    groups=[(detail.body(),'swordsman_body',mat.pbr('swordsman_skin',(.43,.245,.135),.8)),
        (detail.garment(),'swordsman_tunic',mat.pbr('swordsman_tunic',(.18,.20,.15),.96)),
        (detail.leather()+grip+rim,'swordsman_leather',mat.pbr('swordsman_leather',(.13,.06,.027),.95)),
        (metal,'swordsman_copper',mat.pbr('swordsman_copper',(.55,.26,.11),roughness=.36,metallic=.72)),
        (shield,'round_shield',mat.wood()),(marks,'clan_marks',mat.team_color()),
        (hair,'swordsman_hair',mat.pbr('swordsman_hair',(.065,.035,.019),.98)),
        ([detail.oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6)
          for s in (-1,1)],'swordsman_eyes',mat.pbr('swordsman_eyes',(.48,.40,.29),.7))]
    meshes=[]
    for parts,name,material in groups:
        obj=geo.join(parts,name);mat.assign(obj,material)
        colors=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co;c=1-.07*(.5+.5*math.sin(p.x*119+p.z*37)*math.sin(p.y*93-p.z*71))
            colors.data[v.index].color=(c,c,c,1)
        meshes.append(obj)
    for name in ('foot.L','foot.R'):
        FOOT_POINTS[name]=[obj.matrix_world@v.co for obj in meshes if obj.vertex_groups.get(name) is not None
            for v in obj.data.vertices if any(g.group==obj.vertex_groups[name].index and g.weight>.99 for g in v.groups)]
    lib_rig.bind_rigid(meshes,rig)
    motion.build(rig,[('Idle',120,idle,'tool_axe'),('Walk',32,gait,'tool_axe'),
        ('Run',24,lambda p,t:gait(p,t,run=True),'tool_axe'),
        ('Attack',45,attack,'tool_axe'),('Death',72,death,'tool_axe')])
    return [rig,*meshes]


def export():
    objects=build();path=lib_scene.export_glb(objects,'units',NAME);lib_scene.report(path)
    print(f'[asset] {sum(geo.triangle_count(o) for o in objects if o.type=="MESH")} triangles')
    rig=objects[0]
    for track in rig.animation_data.nla_tracks:track.mute=True
    rig.animation_data.action=bpy.data.actions['Idle']
    scene=bpy.context.scene;scene.frame_start,scene.frame_end=0,119;scene.frame_set(0)
    lib_scene.select([rig]);rig.show_in_front=True
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_location=(0,0,1)
                area.spaces.active.region_3d.view_distance=3.8
                area.spaces.active.shading.type='MATERIAL'
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(lib_scene.REPO_ROOT,'blender',NAME+'.blend'))
    return path
