"""Keyframe animation.

Clips are described as data — a dict of bone name to a list of
``(frame, rotation_euler)`` entries — and turned into Blender actions here. That
keeps the asset scripts readable: a walk cycle reads as a table of poses rather
than as fifty API calls.

Every clip is pushed onto its own NLA strip, because that is what makes the glTF
exporter emit several named animations instead of one merged timeline.
"""

from __future__ import annotations

import math
import bpy


def _set_rotation(bone, euler_degrees) -> None:
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = [math.radians(value) for value in euler_degrees]


def make_action(armature, name: str, length: int, poses: dict, loop: bool = True):
    """Create one action from a table of bone poses.

    ``poses`` maps a bone name to ``[(frame, (x, y, z) in degrees), ...]``.
    With ``loop`` the first pose is repeated on the final frame, so the cycle
    closes without a visible jump.
    """
    action = bpy.data.actions.new(f"{name}")
    action.use_fake_user = True

    if armature.animation_data is None:
        armature.animation_data_create()
    armature.animation_data.action = action

    for bone_name, keyframes in poses.items():
        bone = armature.pose.bones.get(bone_name)
        if bone is None:
            print(f"[anim] warning: bone '{bone_name}' not found, skipped")
            continue

        entries = list(keyframes)
        if loop and entries and entries[0][0] != length:
            entries.append((length, entries[0][1]))

        for frame, euler in entries:
            _set_rotation(bone, euler)
            bone.keyframe_insert(data_path="rotation_euler", frame=frame)

    _push_to_nla(armature, action, name)
    return action


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
    """Reset every bone to its rest rotation.

    Called between clips so a pose left over from the previous one does not leak
    into the next as an unintended starting frame.
    """
    for bone in armature.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.location = (0.0, 0.0, 0.0)


# --- Standard clips ------------------------------------------------------
# The shared vocabulary every human unit is expected to provide. The game looks
# these names up by string, so they are part of the contract with the code.

def idle(armature, length: int = 60):
    """Barely-there breathing, so a standing unit does not look frozen."""
    return make_action(armature, "Idle", length, {
        "chest": [(0, (0, 0, 0)), (30, (-2, 0, 0))],
        "arm_upper.L": [(0, (3, 0, 0)), (30, (0, 0, 0))],
        "arm_upper.R": [(0, (0, 0, 0)), (30, (3, 0, 0))],
    })


def walk(armature, length: int = 32):
    """Two-step cycle: opposing arms and legs, slight torso counter-rotation."""
    return make_action(armature, "Walk", length, {
        "leg_upper.L": [(0, (28, 0, 0)), (8, (0, 0, 0)), (16, (-28, 0, 0)), (24, (0, 0, 0))],
        "leg_upper.R": [(0, (-28, 0, 0)), (8, (0, 0, 0)), (16, (28, 0, 0)), (24, (0, 0, 0))],
        "leg_lower.L": [(0, (-10, 0, 0)), (8, (-25, 0, 0)), (16, (0, 0, 0)), (24, (-12, 0, 0))],
        "leg_lower.R": [(0, (0, 0, 0)), (8, (-12, 0, 0)), (16, (-10, 0, 0)), (24, (-25, 0, 0))],
        "arm_upper.L": [(0, (-24, 0, 0)), (16, (24, 0, 0))],
        "arm_upper.R": [(0, (24, 0, 0)), (16, (-24, 0, 0))],
        "arm_lower.L": [(0, (-15, 0, 0)), (16, (-25, 0, 0))],
        "arm_lower.R": [(0, (-25, 0, 0)), (16, (-15, 0, 0))],
        "chest": [(0, (0, 0, 4)), (16, (0, 0, -4))],
    })


def work_chop(armature, length: int = 40):
    """Overhead swing — used for chopping wood and for mining."""
    return make_action(armature, "Gather_Chop", length, {
        "arm_upper.L": [(0, (-110, 0, 0)), (14, (-30, 0, 0)), (22, (-110, 0, 0))],
        "arm_upper.R": [(0, (-110, 0, 0)), (14, (-30, 0, 0)), (22, (-110, 0, 0))],
        "arm_lower.L": [(0, (-40, 0, 0)), (14, (-5, 0, 0)), (22, (-40, 0, 0))],
        "arm_lower.R": [(0, (-40, 0, 0)), (14, (-5, 0, 0)), (22, (-40, 0, 0))],
        "chest": [(0, (-8, 0, 0)), (14, (14, 0, 0)), (22, (-8, 0, 0))],
    })


def death(armature, length: int = 40):
    """Collapse forward. Deliberately not looping."""
    return make_action(armature, "Death", length, {
        "chest": [(0, (0, 0, 0)), (20, (55, 0, 0)), (38, (78, 0, 0))],
        "hips": [(0, (0, 0, 0)), (20, (25, 0, 0)), (38, (40, 0, 0))],
        "leg_upper.L": [(0, (0, 0, 0)), (38, (-45, 0, 0))],
        "leg_upper.R": [(0, (0, 0, 0)), (38, (-35, 0, 0))],
        "arm_upper.L": [(0, (0, 0, 0)), (38, (40, 0, 0))],
        "arm_upper.R": [(0, (0, 0, 0)), (38, (40, 0, 0))],
    }, loop=False)
