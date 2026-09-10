"""Copper Age archer with bending bow limbs, drawn string and nocked arrow."""
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

NAME='unit_archer'
FOOT_POINTS={}
BOW_BONES=tuple(f'bow_{side}_{i}' for side in ('upper','lower') for i in range(2))
STRING_BONES=('string_upper','string_lower')


def grip(p,side):
    hand=p.b['hand.'+side]
    q=hand.matrix.to_quaternion() @ p.rig.data.bones[hand.name].matrix_local.to_quaternion().inverted()
    return hand.head+q @ Vector((0,-.032,-.050))


def segment(p,name,a,b):
    a,b=Vector(a),Vector(b);delta=b-a
    q=motion.UP.rotation_difference(delta.normalized())
    p.b[name].matrix=(Matrix.Translation(a) @ q.to_matrix().to_4x4()
        @ Matrix.Diagonal((1,1,delta.length,1)) @ p.rig.data.bones[name].matrix_local)
    p.update()


def bow(p,draw=0,loaded=False,rotation=None,centre=None,nock=None):
    centre=grip(p,'L') if centre is None else Vector(centre)
    rotation=rotation or Quaternion()
    point=lambda v:centre+rotation @ Vector(v)
    nock=point((0,.25+.28*draw,0)) if nock is None else Vector(nock)
    for side,sign in (('upper',1),('lower',-1)):
        points=[centre,point((0,.05+.06*draw,sign*.31)),point((0,.25+.07*draw,sign*(.60-.05*draw)))]
        for i in range(2):segment(p,f'bow_{side}_{i}',points[i],points[i+1])
        segment(p,'string_'+side,nock,points[-1])
    segment(p,'nocked_arrow',nock,nock+(centre-nock).normalized()*.78)
    if not loaded:p.b['nocked_arrow'].scale=(.001,.001,.001)
    p.update()


def idle(p,t):
    motion.idle(p,t)
    p.arm('L',(.28,-.19,1.02+.004*math.sin(t*math.tau)))
    bow(p)


def gait(p,t,run=False):
    motion.gait(p,t,run=run)
    for side in ('L','R'):
        name='foot.'+side;bone=p.b[name]
        transform=bone.matrix @ p.rig.data.bones[name].matrix_local.inverted()
        bottom=min((transform @ v).z for v in FOOT_POINTS[name])
        if bottom<.003:
            ankle=bone.head.copy();ankle.z+=.003-bottom
            rotation=bone.matrix.to_quaternion()
            end=p.ik('leg_upper.'+side,'leg_lower.'+side,ankle,(ankle.x,-1,.45))
            p.world(name,end,rotation)
    p.arm('L',(.28,-.22,1.05+.015*math.cos(t*math.tau*2)))
    bow(p)


def attack(p,t):
    draw=motion.sample([(0,0),(.23,0),(.47,1),(.61,1),(.66,0),(1,0)],t)
    p.base(z=-.025,lean=2,twist=-25*motion.sample([(0,0),(.25,1),(.73,1),(1,0)],t))
    p.stand(spread=.14,left_y=-.17,right_y=.13)
    p.arm('L',motion.sample([(0,(.28,-.19,1.02)),(.28,(.19,-.54,1.39)),
        (.73,(.19,-.54,1.39)),(1,(.28,-.19,1.02))],t))
    centre=grip(p,'L')
    desired=centre+Vector((0,.25+.28*draw,0))
    if t<.23:
        right=motion.sample([(0,(-.23,-.10,.94)),(.10,(-.12,.10,1.57)),
            (.23,tuple(desired))],t)
    elif t>.66:
        right=motion.sample([(.66,(.19,.02,1.39)),(.76,(-.04,.09,1.40)),
            (1,(-.23,-.10,.94))],t)
    else:right=desired
    p.arm('R',right,pole=(-.65,.40,1.40))
    bow(p,draw,loaded=.23<=t<=.63,nock=grip(p,'R') if .23<=t<=.63 else None)


def death(p,t):
    motion.death(p,t)
    angle=math.pi/2*motion.smooth(t/.60)
    centre=motion.sample([(0,(.28,-.19,1.02)),(.60,(.90,-.45,.035)),(1,(.90,-.45,.035))],t)
    centre.z=max(centre.z,.60*math.cos(angle)+.025)
    bow(p,rotation=Quaternion((0,1,0),angle),centre=centre)


def build():
    lib_scene.reset();bpy.context.scene.render.fps=30
    rig=lib_rig.humanoid(NAME,1.8)
    lib_scene.select([rig]);bpy.ops.object.mode_set(mode='EDIT')
    for name in (*BOW_BONES,*STRING_BONES,'nocked_arrow'):
        b=rig.data.edit_bones.new(name);b.head=(0,0,0);b.tail=(0,0,.1);b.parent=rig.data.edit_bones['root']
    bpy.ops.object.mode_set(mode='OBJECT')
    kit=detail.leather()
    kit.append(detail.profile('back_quiver',[(.12,.18,1.02,.065,.055),(.12,.18,1.49,.08,.063),
        (.12,.18,1.51,.08,.063),(.12,.18,1.51,.064,.049),(.12,.18,1.07,.05,.04)],lambda p:{'chest':1},20))
    kit+=detail.cord('quiver_strap',[(-.12,-.10,1.43),(-.04,-.14,1.30),(.13,-.12,1.10),
        (.20,.07,1.12),(.12,.19,1.40)],.014,'chest')
    # Long bracer on the bow arm and a small copper fastening.
    kit.append(detail.profile('archery_bracer',[(.19,0,.86,.043,.045),(.19,0,1.02,.054,.051)],lambda p:{'arm_lower.L':1},16))
    shafts=[];feathers=[]
    for x,y,h in ((.09,.16,1.70),(.15,.17,1.73),(.12,.21,1.68)):
        shafts.append(detail.pin(geo.limb('quiver_arrow',(x,y,1.22),(x,y,h),.005,.005,vertices=8),'chest'))
        feathers.append(detail.oval('fletching',(x,y,h-.045),(.016,.009,.04),'chest',8,5))
    hair=detail.hair()
    for obj in list(hair):
        if obj.name.startswith('beard'):
            hair.remove(obj);bpy.data.objects.remove(obj,do_unlink=True)
    marks=detail.team()
    marks.append(detail.profile('archer_headband',[(0,.015,1.665,.122,.121),
        (0,.017,1.69,.120,.119)],lambda p:{'head':1},32))
    groups=[(detail.body(),'archer_body',mat.pbr('archer_skin',(.43,.245,.135),.8)),
        (detail.garment(),'archer_tunic',mat.pbr('archer_tunic',(.18,.25,.22),.96)),
        (kit,'archer_leather',mat.pbr('archer_leather',(.20,.09,.035),.95)),
        (hair,'archer_hair',mat.pbr('archer_hair',(.065,.035,.019),.98)),
        (marks,'clan_marks',mat.team_color()),(shafts,'quiver_shafts',mat.wood()),
        (feathers,'quiver_feathers',mat.bone()),
        ([detail.oval('buckle',(.01,-.145,1.24),(.027,.012,.035),'spine',12,6)],
            'copper_buckle',mat.pbr('archer_copper',(.55,.26,.11),.4,.7)),
        ([detail.oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6)
            for s in (-1,1)],'archer_eyes',mat.pbr('archer_eyes',(.48,.40,.29),.7))]
    # Keep equal rest geometry separate to avoid merged skin weights.
    for name in (*BOW_BONES,*STRING_BONES):
        radius=.019 if name in BOW_BONES else .0045
        groups.append(([detail.pin(geo.limb(name+'_part',(0,0,0),(0,0,1),radius,radius,vertices=8),name)],
            name+'_mesh',mat.wood() if name in BOW_BONES else mat.fibre()))
    arrow=[detail.pin(geo.limb('held_arrow',(0,0,0),(0,0,.94),.006,.006,vertices=8),'nocked_arrow')]
    groups.append((arrow,'held_arrow_mesh',mat.wood()))
    groups.append(([detail.pin(geo.cone('arrowhead',.020,0,.06,(0,0,.97),vertices=4),'nocked_arrow')],
        'arrowhead_mesh',mat.pbr('archer_copper',(.55,.26,.11),.4,.7)))
    groups.append(([detail.oval('arrow_feather',(0,0,.07),(.023,.008,.05),'nocked_arrow',8,5)],'held_feather_mesh',mat.bone()))
    meshes=[]
    for parts,name,material in groups:
        obj=geo.join(parts,name);mat.assign(obj,material)
        colors=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co;c=1-.08*(.5+.5*math.sin(p.x*119+p.z*37)*math.sin(p.y*93-p.z*71))
            colors.data[v.index].color=(c,c,c,1)
        meshes.append(obj)
    for name in ('foot.L','foot.R'):
        FOOT_POINTS[name]=[obj.matrix_world@v.co for obj in meshes if obj.vertex_groups.get(name) is not None
            for v in obj.data.vertices if any(g.group==obj.vertex_groups[name].index and g.weight>.99 for g in v.groups)]
    lib_rig.bind_rigid(meshes,rig)
    motion.build(rig,[('Idle',120,idle,None),('Walk',32,gait,None),
        ('Run',24,lambda p,t:gait(p,t,run=True),None),('Attack',54,attack,None),('Death',72,death,None)])
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
