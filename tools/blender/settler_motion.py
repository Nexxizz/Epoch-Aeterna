"""Bake grounded feet and two-hand tool grips to ordinary glTF bone keys.

The analytic two-bone solver is only an authoring aid: no IK constraints or
drivers are required at runtime. Each clip keys every channel so neither NLA
export nor crossfades can inherit a pose/prop from another action. 30 fps.
"""
import math
import bpy
from mathutils import Vector, Quaternion, Matrix, Euler
import lib_anim

UP = Vector((0,0,1))
ONE = Vector((1,1,1))
PROPS = ('tool_axe','tool_pick','tool_spear','tool_basket')


def smooth(t):
    t=max(0,min(1,t))
    return t*t*(3-2*t)


def sample(keys, t):
    """Unevenly timed author poses: slow anticipation, short strike, recovery."""
    for (a,va),(b,vb) in zip(keys,keys[1:]):
        if t <= b:
            u=smooth((t-a)/(b-a))
            if isinstance(va,(tuple,list)):
                return Vector(va).lerp(Vector(vb),u)
            return va+(vb-va)*u
    return Vector(keys[-1][1]) if isinstance(keys[-1][1],(tuple,list)) else keys[-1][1]


class Pose:
    def __init__(self, rig):
        self.rig=rig
        self.b=rig.pose.bones

    def update(self):
        bpy.context.view_layer.update()

    def rotate(self,name,angles):
        self.b[name].rotation_euler=tuple(math.radians(x) for x in angles)

    def base(self,x=0,y=0,z=-.045,lean=0,twist=0,sway=0):
        self.b['root'].location=(x,z,-y)
        self.rotate('hips',(0,twist*.25,sway))
        self.rotate('spine',(lean*.40,twist*.35,0))
        self.rotate('chest',(lean*.60,twist*.40,0))
        self.rotate('head',(-lean*.65,-twist*.8,-sway*.5))
        self.update()

    def world(self,name,pos,rotation):
        bone=self.b[name]
        bone.matrix=Matrix.LocRotScale(Vector(pos),rotation,ONE)
        self.update()

    def point(self,name,start,end):
        rest=self.rig.data.bones[name].matrix_local.to_quaternion()
        direction=(Vector(end)-Vector(start)).normalized()
        q=(rest @ Vector((0,1,0))).rotation_difference(direction) @ rest
        self.world(name,start,q)

    def ik(self,upper,lower,target,pole,min_joint_z=None):
        """Plane-constrained two-bone solve with a small extension safety margin."""
        a=self.b[upper].head.copy();target=Vector(target)
        l1=self.rig.data.bones[upper].length;l2=self.rig.data.bones[lower].length
        delta=target-a;direction=delta.normalized()
        d=max(abs(l1-l2)+.0001,min(delta.length,l1+l2-.0001))
        end=a+direction*d
        projected=Vector(pole)-a
        projected-=direction*projected.dot(direction)
        if projected.length < .0001: projected=Vector((1,0,0))
        projected.normalize()
        along=(l1*l1-l2*l2+d*d)/(2*d)
        centre=a+direction*along
        radius=math.sqrt(max(0,l1*l1-along*along))
        if min_joint_z is not None and centre.z+projected.z*radius < min_joint_z:
            # Rotate on the exact joint circle until the knee contacts the floor.
            # Bone lengths and the planted ankle remain unchanged.
            tangent=direction.cross(projected)
            amplitude=math.hypot(projected.z,tangent.z)
            if amplitude*radius > .000001:
                threshold=max(-1,min(1,(min_joint_z-centre.z)/(radius*amplitude)))
                offset=math.atan2(tangent.z,projected.z)
                arc=math.acos(threshold)
                candidates=[(offset+sign*arc+math.pi)%math.tau-math.pi for sign in (-1,1)]
                angle=min(candidates,key=abs)
                projected=projected*math.cos(angle)+tangent*math.sin(angle)
        knee=centre+projected*radius
        self.point(upper,a,knee)
        self.point(lower,knee,end)
        return end

    def leg(self,side,x,y,z=.054,roll=0):
        ankle=self.ik('leg_upper.'+side,'leg_lower.'+side,(x,y,z),(x,-1,.45))
        rest=self.rig.data.bones['foot.'+side].matrix_local.to_quaternion()
        self.world('foot.'+side,ankle,Quaternion((1,0,0),math.radians(roll))@rest)

    def stand(self,spread=.145,left_y=-.065,right_y=.085):
        self.leg('L',spread,left_y)
        self.leg('R',-spread,right_y)

    def arm(self,side,grip,q=None,pole=None):
        q=q or Quaternion()
        # Grip is in the curled fingers, 5 cm below the wrist.
        wrist=Vector(grip)-q@Vector((0,-.032,-.050))
        sign=1 if side=='L' else -1
        end=self.ik('arm_upper.'+side,'arm_lower.'+side,wrist,pole or (sign*.85,.18,1.02))
        rest=self.rig.data.bones['hand.'+side].matrix_local.to_quaternion()
        self.world('hand.'+side,end,q@rest)

    def tool(self,name,grip,direction,two_hands=True):
        direction=Vector(direction).normalized()
        q=UP.rotation_difference(direction)
        self.arm('R',grip,q)
        if two_hands:
            self.arm('L',Vector(grip)+direction*.145+Vector((.026,0,0)),q)
        # Independent transform is baked back into this hand-parented bone.
        # A grip anchor, rather than its bone origin, controls the stone head.
        rest=self.rig.data.bones[name].matrix_local
        delta=Matrix.Translation(Vector(grip)) @ q.to_matrix().to_4x4() @ Matrix.Translation(Vector((.19,.032,-.760)))
        self.b[name].matrix=delta@rest
        self.update()


def idle(p,t):
    breath=math.sin(t*math.tau)
    shift=math.sin(t*math.tau)*.012
    p.base(x=shift,z=-.008+.002*breath,lean=1+.6*breath,sway=.7*breath)
    p.rotate('head',(-1,3*math.sin(t*math.tau+.4),0));p.update()
    p.stand(spread=.118,left_y=0,right_y=.018)
    for s,sign in (('L',1),('R',-1)):
        p.arm(s,(sign*.215+shift,-.06,.79+.004*breath),pole=(sign*.24,.35,.95))


def gait(p,t,run=False,carry=False):
    phase=t*math.tau
    p.base(x=.012*math.sin(phase),z=(-.058 if not run else -.112)+.018*math.cos(phase*2),
           lean=9 if carry else 8 if run else 3,twist=5*math.cos(phase),sway=1.5*math.sin(phase))
    stance=.48 if run else .62
    stride=.76 if run else .56
    for side,offset,sign in (('L',0,1),('R',.5,-1)):
        u=(t+offset)%1
        if u<stance:
            a=u/stance;y=-stride/2+stride*a
            roll=sample([(0,-10),(.14,0),(.78,0),(1,22)],a)
            # Roll about the heel/toe while maintaining contact with the floor.
            z=.054+(.18*math.sin(math.radians(roll)) if roll>0 else .082*abs(math.sin(math.radians(roll))))
        else:
            a=(u-stance)/(1-stance);y=stride/2-stride*smooth(a)
            z=.054+(.20 if run else .13)*math.sin(math.pi*a)
            roll=sample([(0,22),(.45,4),(1,-10)],a)
        p.leg(side,sign*.115,y,z,roll)
        if carry:
            p.arm(side,(sign*.155,-.11,1.20),Quaternion((1,0,0),math.radians(-35)))
        else:
            p.arm(side,(sign*.22,-.055+(.20 if run else .13)*math.cos(phase+offset*math.tau),
                        .93 if run else .81),pole=(sign*.24,.40,.95))


def work(p,t,mine=False,build=False):
    wind=sample([(0,0),(.34,1),(.46,-.65),(.52,-.45),(.66,-.1),(1,0)],t)
    lean=(22 if build else 12 if mine else 5)-wind*(15 if mine else 10)
    p.base(x=-.018*wind,y=-.01,z=(-.25 if build else -.065)-max(0,-wind)*.055,
           lean=lean,twist=(8 if mine else 16)*wind)
    p.stand(spread=.16,left_y=-.13,right_y=.14)
    if build:
        grip=sample([(0,(-.14,-.32,.94)),(.34,(-.15,-.23,1.15)),(.46,(-.11,-.43,.70)),
                     (.58,(-.12,-.40,.74)),(1,(-.14,-.32,.94))],t)
        angle=sample([(0,35),(.34,-18),(.46,112),(.58,90),(1,35)],t)
    else:
        grip=sample([(0,(-.15,-.28,1.20)),(.34,(-.19,-.08,1.59)),
                     (.46,(-.12,-.47,.94 if mine else 1.07)),(.53,(-.13,-.43,1.00 if mine else 1.12)),
                     (.68,(-.16,-.31,1.18)),(1,(-.15,-.28,1.20))],t)
        angle=sample([(0,20),(.34,-32),(.46,125 if mine else 100),(.53,108 if mine else 88),(.68,34),(1,20)],t)
    direction=(.12*wind,-math.sin(math.radians(angle)),math.cos(math.radians(angle)))
    p.tool('tool_pick' if mine else 'tool_axe',grip,direction,not build)
    if build: p.arm('L',(.18,-.24,.67))


def gather(p,t):
    crouch=sample([(0,.65),(.22,1),(.47,1),(.74,.45),(1,.65)],t)
    p.base(z=-.12-.16*crouch,lean=22+14*crouch,twist=sample([(0,0),(.35,-8),(.75,18),(1,0)],t))
    p.stand(spread=.16,left_y=-.16,right_y=.10)
    p.arm('L',(.17,-.31,.64))
    grip=sample([(0,(-.15,-.40,.65)),(.24,(-.18,-.58,.44)),(.38,(-.16,-.58,.43)),
                 (.52,(-.20,-.36,.80)),(.73,(-.28,.10,1.23)),(.84,(-.27,.12,1.20)),(1,(-.15,-.40,.65))],t)
    p.arm('R',grip)


def attack(p,t):
    thrust=sample([(0,0),(.30,-.30),(.40,1),(.47,.90),(.66,0),(1,0)],t)
    p.base(y=-.055*thrust,z=-.08-.025*max(0,thrust),lean=7+7*thrust,twist=-8-12*thrust)
    p.stand(spread=.15,left_y=-.24,right_y=.22)
    p.tool('tool_spear',(-.17,-.21-.27*thrust,1.19),(.025,-1,.06),True)


def death(p,t):
    # Recoil, knees give way, hips travel down/forward, then a grounded prone hold.
    fall=sample([(0,0),(.10,-6),(.26,22),(.44,63),(.58,88),(.64,84),(.76,87),(1,87)],t)
    height=sample([(0,-.045),(.10,-.035),(.26,-.28),(.44,-.60),(.58,-.768),(.64,-.742),(.76,-.763),(1,-.763)],t)
    forward=sample([(0,0),(.10,.025),(.26,-.04),(.58,-.18),(1,-.18)],t)
    p.base(y=forward,z=height)
    p.rotate('hips',(fall,0,3*smooth(t/.58)))
    p.rotate('spine',(sample([(0,0),(.26,12),(.58,-4),(1,-4)],t),0,0))
    p.rotate('chest',(sample([(0,0),(.10,-8),(.26,9),(.58,3),(1,3)],t),0,0))
    p.rotate('head',(sample([(0,0),(.10,-12),(.4,-5),(.58,3),(1,3)],t),0,17*smooth(t/.65)))
    p.update()
    # Keep the feet in contact as the knees buckle. There is no IK/FK switch:
    # the toes roll and the feet slide back continuously while the hips fall.
    for s,sign in (('L',1),('R',-1)):
        initial=-.045 if sign==1 else .095
        slide=smooth((t-.26)/.34)
        y=initial+(.695-initial)*slide
        angle=180*smooth((t-.34)/.26)
        z=.054+.18*math.sin(math.radians(angle))+.045*smooth((t-.45)/.15)
        pole=Vector((sign*.14,-1,.45)).lerp(Vector((sign*.14,-.2,-.65)),smooth((t-.23)/.14))
        ankle=p.ik('leg_upper.'+s,'leg_lower.'+s,(sign*.14,y,z),pole,min_joint_z=.078)
        rest=p.rig.data.bones['foot.'+s].matrix_local.to_quaternion()
        p.world('foot.'+s,ankle,Quaternion((1,0,0),math.radians(angle))@rest)
    for s,sign in (('L',1),('R',-1)):
        grip=sample([(0,(sign*.22,-.06,.79)),(.10,(sign*.26,-.17,1.13)),
            (.36,(sign*.33,-.60,.50)),(.56,(sign*.37,-.79,.085)),
            (.64,(sign*.38,-.80,.095)),(.76,(sign*.40,-.77,.08)),(1,(sign*.40,-.77,.08))],t)
        pole=Vector((sign*.85,.18,1.02)).lerp(Vector((sign*.65,-.4,.045)),smooth((t-.30)/.28))
        p.arm(s,grip,Quaternion((1,0,0),math.radians(-65*smooth(t/.55))),pole)


CLIPS = [('Idle',120,idle,None),('Walk',32,lambda p,t:gait(p,t),None),
    ('Run',24,lambda p,t:gait(p,t,run=True),None),
    ('Carry_Walk',36,lambda p,t:gait(p,t,carry=True),'tool_basket'),
    ('Carry_Run',28,lambda p,t:gait(p,t,run=True,carry=True),'tool_basket'),
    ('Gather_Food',72,gather,'tool_basket'),('Gather_Chop',48,work,'tool_axe'),
    ('Gather_Mine',54,lambda p,t:work(p,t,mine=True),'tool_pick'),
    ('Attack',60,attack,'tool_spear'),('Build',48,lambda p,t:work(p,t,build=True),'tool_axe'),
    ('Death',72,death,None)]


def build(rig, clips=None):
    rig.animation_data_create()
    pose=Pose(rig)
    for name,length,animate,prop in CLIPS if clips is None else clips:
        action=bpy.data.actions.new(name);action.use_fake_user=True
        rig.animation_data.action=action
        for track in rig.animation_data.nla_tracks: track.mute=True
        for frame in range(length+1):
            bpy.context.scene.frame_set(frame)
            lib_anim.rest_pose(rig)
            animate(pose,frame/length)
            for bone in rig.pose.bones:
                if bone.name in PROPS:
                    bone.scale=(1,1,1) if bone.name==prop else (.001,.001,.001)
                for path in ('location','rotation_euler','scale'):
                    bone.keyframe_insert(data_path=path,frame=frame)
        # Baked samples interpolate linearly: Bezier overshoot breaks planted feet.
        for curve in lib_anim._fcurves(action):
            for key in curve.keyframe_points: key.interpolation='LINEAR'
        lib_anim._push_to_nla(rig,action,name)
        print(f'[motion] {name}: {length/30:.2f}s, complete pose bake')
    for track in rig.animation_data.nla_tracks: track.mute=False
    rig.animation_data.action=None
    lib_anim.rest_pose(rig)
    bpy.context.scene.frame_set(0)
