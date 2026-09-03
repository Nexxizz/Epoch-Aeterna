"""Keyframe animation.

Clips are described as data — bone name to a list of ``(frame, value)`` entries —
and turned into Blender actions here. That keeps the asset scripts readable: a
walk cycle reads as a table of poses rather than as fifty API calls.

Three things separate a walk cycle from a marching robot, and all three are
handled here rather than being left to the reader of the asset script:

  * vertical bob — the body rises over the standing leg and drops on contact
  * counter-rotation — hips and shoulders twist against each other
  * uneven timing — a swing is slow, a strike is fast, and the ease between
    poses is never linear

A note on axes, because getting this wrong is easy and looks like a rigging bug:
every bone's local Y runs *along* the bone. For the spine that means Y is the
twist around the body's axis, X is the forward bend, and Z is a sideways lean.
Putting a torso twist on Z tilts the character over instead of turning it.

Every clip is pushed onto its own NLA strip, because that is what makes the glTF
exporter emit several named animations instead of one merged timeline.
"""

from __future__ import annotations

import math
import bpy


def make_action(armature, name: str, length: int, rotations: dict,
                locations: dict | None = None, scales: dict | None = None,
                loop: bool = True):
    """Create one action from tables of bone poses.

    ``rotations`` maps a bone name to ``[(frame, (x, y, z) in degrees), ...]``.
    ``locations`` does the same with metres in *bone space* — for the root bone,
    which points straight up, its local Y is world up.

    With ``loop`` the first pose is repeated on the final frame, so the cycle
    closes without a visible jump.
    """
    action = bpy.data.actions.new(name)
    action.use_fake_user = True

    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    _insert(armature, rotations, "rotation_euler", length, loop, degrees=True)
    if locations:
        _insert(armature, locations, "location", length, loop, degrees=False)
    if scales:
        _insert(armature, scales, "scale", length, loop, degrees=False)

    _smooth(action)
    _push_to_nla(armature, action, name)
    return action


def _insert(armature, table: dict, data_path: str, length: int, loop: bool,
            degrees: bool) -> None:
    for bone_name, keyframes in table.items():
        bone = armature.pose.bones.get(bone_name)
        if bone is None:
            print(f"[anim] warning: bone '{bone_name}' not found, skipped")
            continue

        bone.rotation_mode = "XYZ"

        entries = list(keyframes)
        if loop and entries and entries[0][0] != length:
            entries.append((length, entries[0][1]))

        for frame, value in entries:
            if degrees:
                bone.rotation_euler = [math.radians(v) for v in value]
            elif data_path == "location":
                bone.location = value
            else:
                bone.scale = value

            bone.keyframe_insert(data_path=data_path, frame=frame)


def _fcurves(action):
    """Every F-curve of an action, across Blender's two action layouts.

    Blender 4.4 moved actions to layers, strips and channel bags; ``fcurves``
    only still exists on the legacy layout. Supporting both keeps the pipeline
    working on either side of that change.
    """
    if hasattr(action, "fcurves"):
        yield from action.fcurves
        return

    for layer in action.layers:
        for strip in layer.strips:
            for channelbag in strip.channelbags:
                yield from channelbag.fcurves


def _smooth(action) -> None:
    """Give every key smooth Bezier handles.

    Linear interpolation between poses is the single most robotic-looking
    default there is: the body changes direction instantly at every key.
    """
    for curve in _fcurves(action):
        for keyframe in curve.keyframe_points:
            keyframe.interpolation = "BEZIER"
            keyframe.handle_left_type = "AUTO_CLAMPED"
            keyframe.handle_right_type = "AUTO_CLAMPED"

        curve.update()


def _push_to_nla(armature, action, name: str) -> None:
    """Move the active action onto its own NLA track.

    Without this the exporter only sees the single active action and the model
    arrives in Godot with one animation instead of the whole set.
    """
    track = armature.animation_data.nla_tracks.new()
    track.name = name

    strip = track.strips.new(name, int(action.frame_range[0]), action)
    strip.name = name

    armature.animation_data.action = None


def rest_pose(armature) -> None:
    """Reset every bone to its rest transform.

    Called between clips so a pose left over from the previous one does not leak
    into the next as an unintended starting frame.
    """
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


# --- Standard clips ------------------------------------------------------
# The shared vocabulary every human unit is expected to provide. The game looks
# these names up by string, so they are part of the contract with the code.

EQUIPMENT_BONES = ("tool_axe", "tool_pick", "tool_spear", "tool_basket")


def equipment(active: str | None = None) -> dict:
    """Scale equipment slots so only the prop needed by a clip is visible."""
    hidden = (0.001, 0.001, 0.001)
    visible = (1.0, 1.0, 1.0)
    return {
        bone: [(0, visible if bone == active else hidden)]
        for bone in EQUIPMENT_BONES
    }

def idle(armature, length: int = 96):
    """Breathing and a slow shift of weight.

    Deliberately long and asymmetric: a short, symmetric idle reads as a machine
    ticking over. The head turn is offset against the breathing so the two never
    line up.
    """
    return make_action(armature, "Idle", length, {
        "chest": [(0, (0, 0, 0)), (26, (-2.4, 0.8, 0)), (52, (0, 0, 0)), (78, (-1.6, -0.6, 0))],
        "spine": [(0, (0, 0, 0)), (26, (-1.0, 0, 0)), (52, (0, 0, 0))],
        "hips": [(0, (0, 0, 1.8)), (48, (0, 0, -1.8))],
        "head": [(0, (0, -3.0, 0)), (34, (1.5, 2.5, 0)), (70, (0, -1.0, 0))],
        "arm_upper.L": [(0, (2.5, 0, 0)), (26, (0.5, 0, 0)), (52, (2.5, 0, 0))],
        "arm_upper.R": [(0, (0.5, 0, 0)), (30, (2.8, 0, 0)), (58, (0.5, 0, 0))],
        "arm_lower.L": [(0, (-8, 0, 0)), (40, (-11, 0, 0))],
        "arm_lower.R": [(0, (-10, 0, 0)), (44, (-7, 0, 0))],
    }, locations={
        # The chest rising lifts the whole body a few millimetres.
        "root": [(0, (0, 0, 0)), (26, (0, 0.012, 0)), (52, (0, 0, 0))],
    }, scales=equipment())


def walk(armature, length: int = 24):
    """Two-step cycle with bob, hip sway and counter-rotating shoulders."""
    return make_action(armature, "Walk", length, {
        # Thighs: forward at contact, back at push-off, bent through the swing.
        "leg_upper.L": [(0, (26, 0, 0)), (6, (2, 0, 0)), (12, (-22, 0, 0)), (18, (6, 0, 0))],
        "leg_upper.R": [(0, (-22, 0, 0)), (6, (6, 0, 0)), (12, (26, 0, 0)), (18, (2, 0, 0))],

        # Knees only ever bend one way, and most strongly while the foot swings.
        "leg_lower.L": [(0, (-6, 0, 0)), (6, (-14, 0, 0)), (12, (-10, 0, 0)), (18, (-46, 0, 0))],
        "leg_lower.R": [(0, (-10, 0, 0)), (6, (-46, 0, 0)), (12, (-6, 0, 0)), (18, (-14, 0, 0))],

        # Arms swing against the legs.
        "arm_upper.L": [(0, (-24, 0, 0)), (6, (-4, 0, 0)), (12, (24, 0, 0)), (18, (4, 0, 0))],
        "arm_upper.R": [(0, (24, 0, 0)), (6, (4, 0, 0)), (12, (-24, 0, 0)), (18, (-4, 0, 0))],
        "arm_lower.L": [(0, (-14, 0, 0)), (12, (-30, 0, 0))],
        "arm_lower.R": [(0, (-30, 0, 0)), (12, (-14, 0, 0))],

        # Hips and shoulders twist against each other — the detail that makes a
        # walk read as a body rather than as a puppet on rails.
        "hips": [(0, (0, -5, 0)), (12, (0, 5, 0))],
        "chest": [(0, (3, 4, 0)), (12, (3, -4, 0))],
        "head": [(0, (0, 2, 0)), (12, (0, -2, 0))],
    }, locations={
        # Highest over the standing leg, lowest on each foot contact — twice per
        # cycle, which is what actually sells the weight.
        "root": [(0, (0, 0, 0)), (6, (0, 0.045, 0)), (12, (0, 0, 0)), (18, (0, 0.045, 0))],
    }, scales=equipment())


def run(armature, length: int = 18):
    """Faster travel cycle with a forward lean and a brief airborne phase."""
    return make_action(armature, "Run", length, {
        "leg_upper.L": [(0, (38, 0, 0)), (4, (5, 0, 0)), (9, (-30, 0, 0)), (14, (8, 0, 0))],
        "leg_upper.R": [(0, (-30, 0, 0)), (4, (8, 0, 0)), (9, (38, 0, 0)), (14, (5, 0, 0))],
        "leg_lower.L": [(0, (-8, 0, 0)), (4, (-18, 0, 0)), (9, (-18, 0, 0)), (14, (-62, 0, 0))],
        "leg_lower.R": [(0, (-18, 0, 0)), (4, (-62, 0, 0)), (9, (-8, 0, 0)), (14, (-18, 0, 0))],
        "arm_upper.L": [(0, (-36, 0, 0)), (9, (34, 0, 0))],
        "arm_upper.R": [(0, (34, 0, 0)), (9, (-36, 0, 0))],
        "arm_lower.L": [(0, (-28, 0, 0)), (9, (-52, 0, 0))],
        "arm_lower.R": [(0, (-52, 0, 0)), (9, (-28, 0, 0))],
        "hips": [(0, (7, -7, 0)), (9, (7, 7, 0))],
        "chest": [(0, (10, 6, 0)), (9, (10, -6, 0))],
        "head": [(0, (-6, 0, 0))],
    }, locations={
        "root": [(0, (0, 0.015, 0)), (4, (0, 0.065, 0)), (9, (0, 0.015, 0)), (14, (0, 0.065, 0))],
    }, scales=equipment())


def carry_walk(armature, length: int = 28):
    """Careful walk while holding the straps of a loaded back basket."""
    return make_action(armature, "Carry_Walk", length, {
        "leg_upper.L": [(0, (21, 0, 0)), (7, (2, 0, 0)), (14, (-17, 0, 0)), (21, (5, 0, 0))],
        "leg_upper.R": [(0, (-17, 0, 0)), (7, (5, 0, 0)), (14, (21, 0, 0)), (21, (2, 0, 0))],
        "leg_lower.L": [(0, (-7, 0, 0)), (7, (-13, 0, 0)), (14, (-9, 0, 0)), (21, (-38, 0, 0))],
        "leg_lower.R": [(0, (-9, 0, 0)), (7, (-38, 0, 0)), (14, (-7, 0, 0)), (21, (-13, 0, 0))],
        "arm_upper.L": [(0, (-30, -8, -3)), (14, (-34, -6, -2))],
        "arm_upper.R": [(0, (-30, 8, 3)), (14, (-34, 6, 2))],
        "arm_lower.L": [(0, (-82, -8, 0)), (14, (-76, -6, 0))],
        "arm_lower.R": [(0, (-82, 8, 0)), (14, (-76, 6, 0))],
        "chest": [(0, (5, 2, 0)), (14, (5, -2, 0))],
        "head": [(0, (-3, 0, 0))],
    }, locations={
        "root": [(0, (0, 0, 0)), (7, (0, 0.025, 0)), (14, (0, 0, 0)), (21, (0, 0.025, 0))],
    }, scales=equipment("tool_basket"))


def gather_food(armature, length: int = 52):
    """Crouch, pick berries or butchered meat, and place it in a basket."""
    return make_action(armature, "Gather_Food", length, {
        "hips": [(0, (8, 0, 0)), (12, (28, 0, 0)), (26, (31, 5, 0)), (38, (24, -4, 0))],
        "spine": [(0, (6, 0, 0)), (12, (18, 0, 0)), (26, (24, 5, 0)), (38, (16, -4, 0))],
        "chest": [(0, (8, 0, 0)), (12, (22, 0, 0)), (26, (28, 8, 0)), (38, (18, -6, 0))],
        "head": [(0, (-10, 0, 0)), (12, (-24, 0, 0)), (26, (-18, -7, 0)), (38, (-20, 5, 0))],
        "leg_upper.L": [(0, (8, 0, 0)), (12, (48, 0, -6)), (26, (54, 0, -8)), (38, (44, 0, -4))],
        "leg_upper.R": [(0, (8, 0, 0)), (12, (42, 0, 8)), (26, (48, 0, 10)), (38, (39, 0, 6))],
        "leg_lower.L": [(0, (-10, 0, 0)), (12, (-66, 0, 0)), (26, (-72, 0, 0)), (38, (-58, 0, 0))],
        "leg_lower.R": [(0, (-10, 0, 0)), (12, (-61, 0, 0)), (26, (-68, 0, 0)), (38, (-55, 0, 0))],
        "arm_upper.R": [(0, (-24, 0, 0)), (12, (-45, 0, 0)), (24, (-78, -18, 0)),
                        (33, (-48, 16, 0)), (43, (-72, -10, 0))],
        "arm_lower.R": [(0, (-28, 0, 0)), (12, (-42, 0, 0)), (24, (-16, 0, 0)),
                        (33, (-72, 10, 0)), (43, (-25, 0, 0))],
        "arm_upper.L": [(0, (-52, -12, 0)), (26, (-57, -12, 0))],
        "arm_lower.L": [(0, (-58, -8, 0)), (26, (-62, -8, 0))],
    }, locations={
        "root": [(0, (0, 0, 0)), (12, (0, -0.18, 0)), (26, (0, -0.22, 0)), (38, (0, -0.16, 0))],
    }, scales=equipment("tool_basket"))


def work_chop(armature, length: int = 34):
    """A diagonal axe swing.

    The axe is carried in one hand, so both arms doing the same thing reads as
    star jumps rather than work. The diagonal therefore comes from the *torso*:
    it winds up away from the target and unwinds through the strike, while the
    off hand swings the other way.

    The arms themselves rotate on a single axis. Large X rotations combined with
    a Z rotation put an XYZ euler close to gimbal lock, where the second angle
    stops meaning what it reads as — that is what once threw the raised arm out
    sideways instead of over the shoulder.

    The timing is deliberately uneven: nine frames to wind up, four to strike,
    then a short recoil. Even spacing would look like a metronome.
    """
    return make_action(armature, "Gather_Chop", length, {
        # Working arm: up over the shoulder, then down and through.
        "arm_upper.R": [(0, (-98, 0, 0)), (9, (-128, 0, 0)), (13, (-54, 0, 0)),
                        (16, (-64, 0, 0)), (24, (-90, 0, 0))],
        "arm_lower.R": [(0, (-38, 0, 0)), (9, (-56, 0, 0)), (13, (-16, 0, 0)),
                        (16, (-26, 0, 0)), (24, (-34, 0, 0))],

        # Off hand grips the same haft: it follows the working arm's swing and is
        # carried across the body by a constant inward rotation. A one-handed
        # chop with an axe this size reads as flailing.
        "arm_upper.L": [(0, (-92, 34, 10)), (9, (-120, 38, 10)), (13, (-52, 30, 6)),
                        (16, (-62, 28, 8)), (24, (-85, 30, 12))],
        "arm_lower.L": [(0, (-44, 10, 8)), (9, (-62, 12, 8)), (13, (-18, 12, 4)),
                        (16, (-28, 11, 5)), (24, (-40, 9, 8))],

        # The torso carries the diagonal. Its angles stay small enough that the
        # combined X and Z rotation behaves the way it reads.
        "chest": [(0, (-8, 14, 0)), (9, (-15, 20, 0)), (13, (17, -11, 0)),
                  (16, (13, -7, 0)), (24, (-4, 9, 0))],
        "spine": [(0, (-4, 7, 0)), (9, (-8, 11, 0)), (13, (8, -6, 0)), (24, (-2, 4, 0))],
        "hips": [(0, (0, 6, 0)), (9, (0, 10, 0)), (13, (0, -5, 0)), (24, (0, 3, 0))],
        # The head counteracts the torso instead of riding along with it. Chest
        # and spine twist add up on the neck, and a head that inherits both ends
        # up swinging nearly 40 degrees from side to side while striking — which
        # is where the lifeless, lolling look came from.
        "head": [(0, (2, -17, 0)), (9, (6, -26, 0)), (13, (-8, 14, 0)), (24, (1, -11, 0))],

        # Knees take the impact.
        "leg_upper.L": [(0, (5, 0, 0)), (13, (15, 0, 0)), (24, (5, 0, 0))],
        "leg_upper.R": [(0, (7, 0, 0)), (13, (12, 0, 0)), (24, (7, 0, 0))],
        "leg_lower.L": [(0, (-7, 0, 0)), (13, (-22, 0, 0)), (24, (-7, 0, 0))],
        "leg_lower.R": [(0, (-9, 0, 0)), (13, (-19, 0, 0)), (24, (-9, 0, 0))],
    }, locations={
        # Rises with the wind-up, drops into the strike, settles on recovery.
        "root": [(0, (0, 0.015, 0)), (9, (0, 0.04, 0)), (13, (0, -0.055, 0)),
                 (16, (0, -0.035, 0)), (24, (0, 0.015, 0))],
    }, scales=equipment("tool_axe"))


def work_mine(armature, length: int = 38):
    """Two-handed stone-pick swing for stone, ore and future iron deposits."""
    return make_action(armature, "Gather_Mine", length, {
        "arm_upper.R": [(0, (-72, -5, 0)), (11, (-132, -4, 0)), (16, (-46, -3, 0)),
                        (22, (-58, -4, 0)), (30, (-78, -5, 0))],
        "arm_lower.R": [(0, (-45, 0, 0)), (11, (-66, 0, 0)), (16, (-18, 0, 0)),
                        (22, (-28, 0, 0)), (30, (-42, 0, 0))],
        "arm_upper.L": [(0, (-76, 28, 7)), (11, (-126, 30, 7)), (16, (-52, 24, 4)),
                        (22, (-61, 24, 5)), (30, (-80, 27, 7))],
        "arm_lower.L": [(0, (-48, 9, 5)), (11, (-70, 11, 6)), (16, (-23, 8, 3)),
                        (22, (-31, 8, 4)), (30, (-46, 9, 5))],
        "hips": [(0, (4, 5, 0)), (11, (-2, 8, 0)), (16, (12, -5, 0)), (30, (4, 4, 0))],
        "spine": [(0, (5, 5, 0)), (11, (-8, 9, 0)), (16, (19, -7, 0)), (30, (4, 4, 0))],
        "chest": [(0, (8, 8, 0)), (11, (-13, 13, 0)), (16, (27, -10, 0)), (30, (7, 6, 0))],
        "head": [(0, (-7, -8, 0)), (11, (6, -16, 0)), (16, (-20, 12, 0)), (30, (-6, -7, 0))],
        "leg_upper.L": [(0, (9, 0, 0)), (16, (19, 0, 0)), (30, (9, 0, 0))],
        "leg_upper.R": [(0, (11, 0, 0)), (16, (17, 0, 0)), (30, (11, 0, 0))],
        "leg_lower.L": [(0, (-12, 0, 0)), (16, (-27, 0, 0)), (30, (-12, 0, 0))],
        "leg_lower.R": [(0, (-14, 0, 0)), (16, (-24, 0, 0)), (30, (-14, 0, 0))],
    }, locations={
        "root": [(0, (0, 0, 0)), (11, (0, 0.045, 0)), (16, (0, -0.075, 0)),
                 (22, (0, -0.035, 0)), (30, (0, 0, 0))],
    }, scales=equipment("tool_pick"))


def attack(armature, length: int = 30):
    """Short spear thrust suitable for hunting animals or close combat."""
    return make_action(armature, "Attack", length, {
        "hips": [(0, (0, -12, 0)), (9, (0, 15, 0)), (14, (0, -9, 0)), (23, (0, -12, 0))],
        "spine": [(0, (2, -10, 0)), (9, (-3, 12, 0)), (14, (9, -8, 0)), (23, (2, -10, 0))],
        "chest": [(0, (4, -13, 0)), (9, (-5, 16, 0)), (14, (14, -12, 0)), (23, (4, -13, 0))],
        "head": [(0, (-4, 20, 0)), (9, (1, -22, 0)), (14, (-10, 17, 0)), (23, (-4, 20, 0))],
        "arm_upper.R": [(0, (-82, -8, 6)), (9, (-58, 12, 5)), (14, (-103, -12, 2)),
                        (19, (-92, -8, 5)), (23, (-82, -8, 6))],
        "arm_lower.R": [(0, (-64, 0, 0)), (9, (-92, 0, 0)), (14, (-18, 0, 0)),
                        (19, (-38, 0, 0)), (23, (-64, 0, 0))],
        "arm_upper.L": [(0, (-54, 24, 6)), (9, (-68, 18, 5)), (14, (-74, 25, 4)),
                        (23, (-54, 24, 6))],
        "arm_lower.L": [(0, (-72, 8, 0)), (9, (-58, 6, 0)), (14, (-40, 8, 0)),
                        (23, (-72, 8, 0))],
        "leg_upper.L": [(0, (23, 0, 0)), (14, (29, 0, 0)), (23, (23, 0, 0))],
        "leg_upper.R": [(0, (-15, 0, 0)), (14, (-21, 0, 0)), (23, (-15, 0, 0))],
        "leg_lower.L": [(0, (-25, 0, 0)), (14, (-32, 0, 0)), (23, (-25, 0, 0))],
        "leg_lower.R": [(0, (-16, 0, 0)), (14, (-21, 0, 0)), (23, (-16, 0, 0))],
    }, locations={
        "root": [(0, (0, 0, 0)), (9, (0, -0.015, 0)), (14, (0, -0.04, -0.09)),
                 (23, (0, 0, 0))],
    }, scales=equipment("tool_spear"))


def build(armature, length: int = 36):
    """Controlled kneeling hammer stroke using the flat of the stone axe."""
    return make_action(armature, "Build", length, {
        "hips": [(0, (18, 0, 0)), (12, (25, 0, 0)), (18, (31, 0, 0)), (28, (20, 0, 0))],
        "spine": [(0, (11, 0, 0)), (12, (17, 0, 0)), (18, (24, 0, 0)), (28, (13, 0, 0))],
        "chest": [(0, (14, 5, 0)), (12, (7, 8, 0)), (18, (31, -5, 0)), (28, (16, 4, 0))],
        "head": [(0, (-12, -4, 0)), (12, (-5, -7, 0)), (18, (-25, 5, 0)), (28, (-13, -3, 0))],
        "arm_upper.R": [(0, (-72, 0, 0)), (12, (-108, 0, 0)), (18, (-48, 0, 0)), (28, (-70, 0, 0))],
        "arm_lower.R": [(0, (-42, 0, 0)), (12, (-58, 0, 0)), (18, (-14, 0, 0)), (28, (-38, 0, 0))],
        "arm_upper.L": [(0, (-48, 18, 0)), (18, (-62, 12, 0)), (28, (-48, 18, 0))],
        "arm_lower.L": [(0, (-52, 5, 0)), (18, (-37, 4, 0)), (28, (-52, 5, 0))],
        "leg_upper.L": [(0, (35, 0, -8)), (18, (42, 0, -10)), (28, (35, 0, -8))],
        "leg_upper.R": [(0, (28, 0, 8)), (18, (36, 0, 10)), (28, (28, 0, 8))],
        "leg_lower.L": [(0, (-52, 0, 0)), (18, (-62, 0, 0)), (28, (-52, 0, 0))],
        "leg_lower.R": [(0, (-45, 0, 0)), (18, (-57, 0, 0)), (28, (-45, 0, 0))],
    }, locations={
        "root": [(0, (0, -0.12, 0)), (12, (0, -0.10, 0)), (18, (0, -0.20, 0)),
                 (28, (0, -0.12, 0))],
    }, scales=equipment("tool_axe"))


def death(armature, length: int = 44):
    """Stagger, buckle, fall forward. Deliberately not looping."""
    return make_action(armature, "Death", length, {
        "chest": [(0, (0, 0, 0)), (5, (-16, 0, 6)), (16, (38, 0, -4)), (28, (74, 0, 0)),
                  (36, (68, 0, 0)), (44, (72, 0, 0))],
        "spine": [(0, (0, 0, 0)), (5, (-8, 0, 0)), (16, (18, 0, 0)), (28, (30, 0, 0))],
        "hips": [(0, (0, 0, 0)), (5, (-6, 0, 4)), (16, (22, 0, -6)), (28, (44, 0, 0)),
                 (44, (46, 0, 0))],
        "head": [(0, (0, 0, 0)), (5, (-18, 0, 0)), (20, (14, 0, 8)), (30, (26, 0, 4))],
        "leg_upper.L": [(0, (0, 0, 0)), (12, (10, 0, 0)), (28, (-46, 0, 0)), (44, (-44, 0, 0))],
        "leg_upper.R": [(0, (0, 0, 0)), (12, (-6, 0, 0)), (28, (-34, 0, 0)), (44, (-36, 0, 0))],
        "leg_lower.L": [(0, (0, 0, 0)), (16, (-38, 0, 0)), (28, (-16, 0, 0))],
        "leg_lower.R": [(0, (0, 0, 0)), (16, (-24, 0, 0)), (28, (-10, 0, 0))],
        "arm_upper.L": [(0, (0, 0, 0)), (8, (-34, 0, 0)), (24, (46, 0, 0)), (36, (38, 0, 0))],
        "arm_upper.R": [(0, (0, 0, 0)), (8, (-28, 0, 0)), (24, (42, 0, 0)), (36, (36, 0, 0))],
        "arm_lower.L": [(0, (-10, 0, 0)), (24, (-52, 0, 0))],
        "arm_lower.R": [(0, (-10, 0, 0)), (24, (-48, 0, 0))],
    }, locations={
        # The body drops as the legs give way, with a small settle at the end.
        "root": [(0, (0, 0, 0)), (12, (0, -0.12, 0)), (28, (0, -0.78, 0)),
                 (34, (0, -0.72, 0)), (44, (0, -0.76, 0))],
    }, scales=equipment(), loop=False)
