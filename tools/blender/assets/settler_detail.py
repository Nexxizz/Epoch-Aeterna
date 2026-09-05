"""Profiled anatomy, hand-sewn hide and knapped tools, explicitly weighted.

Surface detail uses portable geometry and COLOR_0, never Blender-only shaders.
"""
import math
import random
import bpy
from mathutils import Vector
import lib_mesh as geo
import lib_material as mat
import lib_rig as rig


def mesh(name, vertices, faces, weights, smooth=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    for face in data.polygons: face.use_smooth = smooth
    for i, mapping in enumerate(weights):
        for bone, weight in mapping.items():
            if weight > 0:
                group = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
                group.add([i], weight, 'REPLACE')
    return obj


def profile(name, rings, weighting, sides=20, ragged=0):
    vertices, weights, faces = [], [], []
    for j, (x,y,z,rx,ry) in enumerate(rings):
        for i in range(sides):
            a = math.tau*i/sides
            ripple = 1+.025*math.sin(a*5+j*.7)
            zz = z+(ragged*(.5+.5*math.sin(a*7+1)) if j == 0 else 0)
            vertices.append((x+rx*math.cos(a)*ripple,y+ry*math.sin(a)*ripple,zz))
            weights.append(weighting(Vector(vertices[-1])))
            if j:
                k=j*sides+i; n=j*sides+(i+1)%sides
                faces.append((k-sides,n-sides,n,k))
    faces.extend([tuple(reversed(range(sides))),tuple(range((len(rings)-1)*sides,len(rings)*sides))])
    return mesh(name,vertices,faces,weights)


def blend(z, lower, upper, centre, width):
    t=max(0,min(1,(z-centre)/width+.5)); t=t*t*(3-2*t)
    return {lower:1-t,upper:t}


def torso(p):
    if p.z < .96:
        t=max(0,min(.6,(.96-p.z)*3.7))
        return {'hips':1-t,'leg_upper.L' if p.x>0 else 'leg_upper.R':t}
    return blend(p.z,'hips','spine',1.075,.14) if p.z<1.12 else blend(p.z,'spine','chest',1.29,.20)


def pin(obj,bone):
    rig.assign_to_bone(obj,bone)
    return obj


def oval(name,pos,scale,bone,segments=16,rings=10):
    obj=geo.sphere(name,1,pos,segments,rings); obj.scale=scale
    return pin(obj,bone)


def cord(name,points,radius,bone):
    return [pin(geo.limb(f'{name}_{i}',a,b,radius,radius,vertices=6),bone)
            for i,(a,b) in enumerate(zip(points,points[1:]))]


def ring(name,centre,rx,ry,radius,bone,count=20):
    x,y,z=centre
    return cord(name,[(x+rx*math.cos(i*math.tau/count),y+ry*math.sin(i*math.tau/count),z)
                     for i in range(count+1)],radius,bone)


def body():
    parts=[profile('torso',[(0,0,.91,.135,.092),(0,0,.97,.153,.10),(0,0,1.07,.139,.089),
        (0,0,1.15,.138,.088),(0,0,1.25,.171,.106),(0,0,1.34,.197,.109),
        (0,0,1.40,.193,.097),(0,0,1.445,.139,.072),(0,0,1.48,.055,.051)],torso,24)]
    parts.append(profile('neck',[(0,0,1.44,.060,.05),(0,0,1.49,.052,.049),(0,0,1.55,.054,.055)],
        lambda p:blend(p.z,'chest','head',1.49,.1)))
    parts.append(profile('face',[(0,-.021,1.51,.044,.046),(0,-.015,1.535,.067,.069),
        (0,-.002,1.58,.089,.087),(0,.004,1.625,.104,.097),(0,.010,1.67,.099,.095),
        (0,.013,1.715,.085,.082),(0,.019,1.75,.052,.055),(0,.02,1.76,.008,.01)],lambda p:{'head':1},24))
    parts += [oval('nose_bridge',(0,-.091,1.626),(.017,.026,.040),'head'),
              oval('nose_tip',(0,-.113,1.60),(.024,.026,.018),'head')]
    for sign,side in ((1,'L'),(-1,'R')):
        x=.19*sign
        parts.append(profile('arm_'+side,[(x,0,.802,.029,.031),(x,0,.87,.032,.035),
            (x,.002,.98,.046,.044),(x,0,1.07,.041,.039),(x,0,1.10,.035,.034),
            (x,0,1.116,.035,.033),(x,0,1.135,.037,.035),(x,0,1.19,.047,.046),
            (x,0,1.29,.062,.056),(x,0,1.38,.067,.061),(x,0,1.44,.052,.052),
            (x,0,1.465,.025,.025)],lambda p,s=side:blend(p.z,'arm_lower.'+s,'arm_upper.'+s,1.116,.115)))
        x=.1*sign
        parts.append(profile('leg_'+side,[(x,0,.052,.036,.040),(x,.006,.16,.041,.047),
            (x,.014,.29,.060,.066),(x,.008,.40,.055,.056),(x,-.002,.476,.047,.049),
            (x,-.007,.504,.048,.051),(x,0,.536,.050,.05),(x,0,.61,.066,.069),
            (x,0,.75,.087,.088),(x,0,.87,.096,.094),(x,0,.96,.077,.075)],
            lambda p,s=side:blend(p.z,'leg_lower.'+s,'leg_upper.'+s,.504,.12)))
        x=.19*sign
        parts.append(oval('palm_'+side,(x,-.008,.777),(.036,.027,.049),'hand.'+side))
        for f in range(4):
            fx=x+(f-1.5)*.016
            parts+=cord('finger',[(fx,-.012,.759),(fx,-.021,.737),(fx,-.038,.738),(fx,-.042,.754)],.009,'hand.'+side)
        parts+=cord('thumb',[(x-sign*.031,-.012,.794),(x-sign*.046,-.034,.77),(x-sign*.032,-.051,.758)],.013,'hand.'+side)
        parts.append(oval('ear',(sign*.104,.006,1.622),(.019,.021,.036),'head'))
    return parts


def garment():
    obj=profile('sewn_hide',[(0,0,.795,.177,.118),(0,0,.90,.18,.12),(0,0,1.015,.163,.110),
        (0,0,1.075,.15,.100),(0,0,1.16,.148,.100),(0,0,1.26,.18,.117),
        (0,0,1.35,.197,.119),(0,0,1.395,.18,.103)],torso,32,.045)
    for v in obj.data.vertices:
        v.co.y*=1+.045*math.cos(math.atan2(v.co.y,v.co.x)*11+v.co.z*3)
    return [obj]


def leather():
    parts=[]
    for sign,side in ((1,'L'),(-1,'R')):
        x=sign*.1
        parts.append(profile('moccasin',[(x,-.049,.006,.055,.125),(x,-.051,.024,.060,.132),
            (x,-.047,.061,.058,.129),(x,-.015,.099,.044,.067),(x,0,.135,.039,.045)],lambda p,s=side:{'foot.'+s:1}))
        parts.append(profile('calf_wrap',[(x,.005,.17,.045,.05),(x,.013,.23,.055,.060),
            (x,.012,.32,.064,.069)],lambda p,s=side:{'leg_lower.'+s:1}))
        parts.append(profile('wrist_wrap',[(sign*.19,0,.841,.037,.039),(sign*.19,0,.935,.046,.044)],
            lambda p,s=side:{'arm_lower.'+s:1}))
    parts.append(profile('belt',[(0,0,1.004,.17,.117),(0,0,1.046,.164,.113)],lambda p:{'hips':1},32))
    for x,size in ((-.138,.055),(.125,.042)):
        parts.append(oval('pouch',(x,-.115,.966),(size,.040,.065),'hips'))
    return parts


def fur():
    parts=[oval('shoulder_pelt',(.12,.013,1.429),(.143,.136,.074),'chest',24,14)]
    rng=random.Random(81)
    for i in range(42):
        a=math.tau*i/42; x=.12+.135*math.cos(a); y=.012+.121*math.sin(a); z=1.433+rng.uniform(-.008,.008)
        parts.append(pin(geo.limb('pelt_tip',(x,y,z),(x+.009*math.cos(a),y+.012*math.sin(a),z-rng.uniform(.03,.07)),
                                   .017,.003,vertices=5),'chest'))
    for i in range(32):
        a=math.tau*i/32
        parts.append(pin(geo.limb('hem',(.177*math.cos(a),.12*math.sin(a),.832),
            (.18*math.cos(a),.12*math.sin(a),.775+rng.uniform(0,.038)),.013,.003,vertices=5),'hips'))
    # The shoulder edge follows the upper arm, the inner pelt follows the chest.
    # This prevents an overhead stroke pushing the deltoid through a rigid cape.
    for obj in parts[:43]:
        obj.vertex_groups.clear()
        chest=obj.vertex_groups.new(name='chest')
        arm=obj.vertex_groups.new(name='arm_upper.L')
        for v in obj.data.vertices:
            x=(obj.matrix_world@v.co).x
            t=max(0,min(1,(x-.07)/.15));t=t*t*(3-2*t)
            if t<1:chest.add([v.index],1-t,'REPLACE')
            if t>0:arm.add([v.index],t,'REPLACE')
    return parts


def hair():
    verts=[];faces=[];parts=[]
    for j in range(9):
        for i in range(32):
            a=math.tau*i/32; theta=.05+j/8*(1.87-.60*max(0,-math.sin(a)))
            verts.append((.116*math.sin(theta)*math.cos(a),.016+.113*math.sin(theta)*math.sin(a),1.637+.141*math.cos(theta)))
            if j:
                k=j*32+i;n=j*32+(i+1)%32;faces.append((k-32,n-32,n,k))
    parts.append(mesh('open_hairline',verts,faces,[{'head':1}]*len(verts)))
    for i in range(19):
        a=.05+math.pi*i/18;x=.097*math.cos(a);y=.018+.097*math.sin(a)
        parts.append(pin(geo.limb('hair_lock',(x,y,1.666),(x*.9,y+.012,1.53+.024*math.sin(i*3)),.018,.006,vertices=7),'head'))
        a=math.pi+math.pi*i/18;x=.078*math.cos(a);y=.004+.084*math.sin(a)
        parts.append(pin(geo.limb('beard',(x,y,1.564),(x*.76,y*.87,1.511+.009*math.cos(i*2)),.014,.006,vertices=6),'head'))
    for s in (-1,1):
        parts += [oval('brow',(s*.042,-.085,1.652),(.028,.012,.007),'head'),
                  oval('iris',(s*.040,-.095,1.634),(.005,.004,.005),'head',10,6),
                  oval('nostril',(s*.012,-.128,1.595),(.005,.003,.003),'head',8,5)]
    parts+=cord('mouth',[(-.024,-.081,1.556),(0,-.088,1.552),(.024,-.081,1.556)],.003,'head')
    return parts


def sinew():
    parts=[]
    for sign,side in ((1,'L'),(-1,'R')):
        for z in (.186,.218,.252,.285,.31):
            r=.047+(z-.17)*.13
            parts+=ring('calf_lace',(sign*.1,.013,z),r,r+.004,.0035,'leg_lower.'+side,14)
        for i in range(5):
            y=-.143+i*.019
            parts+=cord('shoe_stitch',[(sign*.1-.025,y,.072),(sign*.1+.025,y+.007,.077)],.0025,'foot.'+side)
    for i in range(17):
        z=.85+i*.028;x=-.09+.02*math.sin(z*4)
        bone='hips' if z<1.07 else 'spine' if z<1.27 else 'chest'
        parts+=cord('seam',[(x-.009,-.109,z),(x+.008,-.115,z+.013)],.0028,bone)
    parts+=cord('necklace',[(-.073,-.064,1.46),(-.066,-.12,1.40),(0,-.132,1.367),(.066,-.12,1.40),(.073,-.064,1.46)],.004,'chest')
    for x in (-.035,0,.035):
        parts.append(pin(geo.cone('pendant',.006,.011,.043,(x,-.133,1.355+abs(x)*.5),vertices=6),'chest'))
    for tool,z in (('axe',1.02),('pick',1.10),('spear',1.59)):
        for i in range(5): parts+=ring('haft_binding',(-.19,-.032,z+i*.009),.025,.027,.004,'tool_'+tool,10)
    return parts


def team():
    verts=[];weights=[];faces=[];parts=[]
    for i in range(16):
        t=i/15;z=1.395-.36*t;x=.115-.22*t
        for dx in (-.021,.021):
            p=Vector((x+dx,-.126,z));verts.append(tuple(p));weights.append(torso(p))
        if i: faces.append((2*i-2,2*i-1,2*i+1,2*i))
    parts.append(mesh('clan_sash',verts,faces,weights))
    for s,side in ((1,'L'),(-1,'R')):
        parts.append(profile('armband',[(s*.19,0,1.237,.059,.053),(s*.19,0,1.268,.064,.059)],lambda p,b=side:{'arm_upper.'+b:1}))
        parts.append(oval('clan_paint',(s*.070,-.070,1.608),(.009,.004,.022),'head',10,6))
    return parts


def wood():
    return [pin(geo.limb(tool+'_haft',(-.19,-.032,bottom),(-.19,-.032,top),.019,.014,vertices=10),'tool_'+tool)
            for tool,bottom,top in (('axe',.54,1.08),('pick',.48,1.16),('spear',.16,1.65))]


def flint():
    parts=[]
    for name,outline,thickness in (
        ('axe',[(-.05,.97),(-.15,.96),(-.19,1.005),(-.185,1.07),(-.10,1.10),(.04,1.055),(.04,1.005)],.033),
        ('pick',[(-.25,1.025),(-.21,1.13),(-.06,1.16),(.12,1.11),(.18,1.025),(.09,1.08),(-.12,1.09)],.027),
        ('spear',[(-.032,1.83),(-.078,1.69),(-.053,1.62),(-.011,1.62),(.014,1.69)],.017)):
        verts=[(-.19+s*thickness,y,z) for s in (-1,1) for y,z in outline];n=len(outline)
        verts.extend([(-.19-thickness*1.35,-.055,sum(z for y,z in outline)/n),(-.19+thickness*1.35,-.055,sum(z for y,z in outline)/n)])
        faces=[]
        for i in range(n):
            j=(i+1)%n;faces.extend([(i,j,2*n),(n+j,n+i,2*n+1),(i,n+i,n+j,j)])
        parts.append(mesh(name+'_flint',verts,faces,[{'tool_'+name:1}]*len(verts),False))
    return parts


def basket():
    # Fold the profile back down inside the basket to leave a hollow opening.
    parts=[profile('basket',[(0,.235,.93,.105,.085),(0,.235,1.06,.145,.109),
        (0,.235,1.22,.169,.128),(0,.235,1.22,.155,.114),(0,.235,.95,.092,.072)],lambda p:{'tool_basket':1},24)]
    for i in range(11):
        t=i/10;parts+=ring('weave',(0,.235,.94+.28*t),.109+.06*t,.087+.041*t,.004,'tool_basket',24)
    for i in range(18):
        a=i*math.tau/18
        parts+=cord('reed',[(.107*math.cos(a),.235+.086*math.sin(a),.94),(.171*math.cos(a),.235+.13*math.sin(a),1.225)],.004,'tool_basket')
    for s in (-1,1):
        parts+=cord('basket_strap',[(s*.12,.23,1.22),(s*.15,.10,1.43),(s*.16,-.02,1.44),
            (s*.155,-.125,1.30),(s*.145,-.13,1.17),(s*.13,.02,1.02),(s*.10,.23,.96)],.012,'tool_basket')
    return parts


def build():
    groups=[(body(),'body',mat.pbr('settler_skin',(.43,.245,.135),.78),.06),
        (garment(),'sewn_hide',mat.pbr('settler_hide',(.31,.125,.041),.96),.14),
        (leather(),'leather_kit',mat.pbr('settler_leather',(.13,.062,.027),.93),.09),
        (fur(),'pelt',mat.pbr('settler_pelt',(.18,.125,.075),1),.22),
        (hair(),'hair_face',mat.pbr('settler_hair',(.052,.029,.018),.97),.12),
        (sinew(),'sinew',mat.bone(),.05),(team(),'clan_marks',mat.team_color(),0),
        (wood(),'hafts',mat.wood(),.15),(flint(),'knapped_flint',mat.pbr('flint',(.23,.24,.225),.77),.13),
        (basket(),'woven_basket',mat.fibre(),.10),
        ([oval('gathered_food',(x,.24+y,1.18),(.042,.042,.040),'tool_basket',10,6)
          for x,y in ((-.08,0),(0,-.04),(.065,.005),(0,.05))],'gathered_food',mat.food(),.1),
        ([oval('eye',(s*.040,-.087,1.634),(.016,.007,.006),'head',12,6) for s in (-1,1)],'eyes',mat.pbr('eye',(.48,.40,.29),.7),0)]
    result=[]
    for parts,name,material,variation in groups:
        obj=geo.join(parts,name);mat.assign(obj,material)
        colors=obj.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for i,v in enumerate(obj.data.vertices):
            p=obj.matrix_world@v.co
            c=1-variation*(.5+.5*math.sin(p.x*119+p.z*37)*math.sin(p.y*93-p.z*71))
            colors.data[i].color=(c,c,c,1)
        result.append(obj)
    return result
