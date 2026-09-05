"""Humanoid armature and skinning.

One shared skeleton for every human unit. That is the point: settlers, spearmen
and archers differ in geometry and equipment, not in anatomy, so an animation
authored once plays on all of them.

Bone names follow the glTF convention of plain, lower-case, side-suffixed names
(``arm_upper.L``) so the export stays readable in any viewer.
"""

from __future__ import annotations

import bpy
from mathutils import Vector

# Proportions of a 1.8 m human, as fractions of total height.
# Kept as data so a child or a giant is a parameter change, not a rewrite.
PROPORTIONS = {
    "hips": 0.53,
    "spine": 0.62,
    "chest": 0.72,
    "neck": 0.82,
    "head_top": 1.00,
    "shoulder": 0.80,
    "elbow": 0.62,
    "hand": 0.45,
    "knee": 0.28,
    "foot": 0.03,
}

SHOULDER_WIDTH = 0.19
HIP_WIDTH = 0.10


def humanoid(name: str, height: float = 1.8):
    """Build the standard skeleton. Returns the armature object."""
    armature_data = bpy.data.armatures.new(f"SK_{name}")
    armature = bpy.data.objects.new(f"SK_{name}", armature_data)
    bpy.context.collection.objects.link(armature)

    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="EDIT")

    def at(fraction: float, x: float = 0.0, y: float = 0.0) -> Vector:
        return Vector((x, y, fraction * height))

    def bone(bone_name: str, head: Vector, tail: Vector, parent=None):
        edit_bone = armature_data.edit_bones.new(bone_name)
        edit_bone.head = head
        edit_bone.tail = tail
        if parent is not None:
            edit_bone.parent = parent
            # Not connected: the limbs need to be able to translate slightly,
            # which a connected chain forbids.
            edit_bone.use_connect = False
        return edit_bone

    # Root stays at the origin so the whole character can be moved as one.
    root = bone("root", at(0.0), at(0.08))

    hips = bone("hips", at(PROPORTIONS["hips"]), at(PROPORTIONS["spine"]), root)
    spine = bone("spine", at(PROPORTIONS["spine"]), at(PROPORTIONS["chest"]), hips)
    chest = bone("chest", at(PROPORTIONS["chest"]), at(PROPORTIONS["neck"]), spine)
    bone("head", at(PROPORTIONS["neck"]), at(PROPORTIONS["head_top"]), chest)

    for side, sign in (("L", 1.0), ("R", -1.0)):
        shoulder = sign * SHOULDER_WIDTH
        upper = bone(
            f"arm_upper.{side}",
            at(PROPORTIONS["shoulder"], shoulder),
            at(PROPORTIONS["elbow"], shoulder),
            chest)
        forearm = bone(
            f"arm_lower.{side}",
            at(PROPORTIONS["elbow"], shoulder),
            at(PROPORTIONS["hand"], shoulder),
            upper)
        bone(f"hand.{side}", at(PROPORTIONS["hand"], shoulder),
             at(0.405, shoulder), forearm)

        hip = sign * HIP_WIDTH
        thigh = bone(
            f"leg_upper.{side}",
            at(PROPORTIONS["hips"], hip),
            at(PROPORTIONS["knee"], hip),
            hips)
        shin = bone(
            f"leg_lower.{side}",
            at(PROPORTIONS["knee"], hip),
            at(PROPORTIONS["foot"], hip),
            thigh)
        bone(f"foot.{side}", at(PROPORTIONS["foot"], hip),
             at(PROPORTIONS["foot"], hip, -0.17), shin)

    # Equipment slots are regular bones so a single exported model can carry
    # several task-specific props and reveal only the one used by an animation.
    # They inherit the hand movement, while their own scale controls visibility.
    right_hand = armature_data.edit_bones["hand.R"]
    bone("tool_axe", at(0.57, -SHOULDER_WIDTH, -0.01),
         at(0.38, -SHOULDER_WIDTH, -0.01), right_hand)
    bone("tool_pick", at(0.57, -SHOULDER_WIDTH, -0.01),
         at(0.38, -SHOULDER_WIDTH, -0.01), right_hand)
    bone("tool_spear", at(0.70, -SHOULDER_WIDTH, -0.01),
         at(0.20, -SHOULDER_WIDTH, -0.01), right_hand)
    bone("tool_basket", at(0.70, 0.0, 0.22),
         at(0.48, 0.0, 0.22), chest)

    bpy.ops.object.mode_set(mode="OBJECT")
    return armature


def assign_to_bone(obj, bone_name: str):
    """Weight every vertex of an object rigidly to one bone.

    Used for stiff parts — fur strips, a belt, a carried axe. They do not bend,
    they follow whatever they are attached to, and saying so explicitly is both
    cheaper and more predictable than asking a solver to guess it.
    """
    group = obj.vertex_groups.new(name=bone_name)
    group.add(range(len(obj.data.vertices)), 1.0, "REPLACE")
    return obj


def bind(meshes, armature) -> None:
    """Skin meshes to the armature with automatic weights.

    Suitable for the body itself, where a continuous surface has to bend across
    joints. Stiff accessories should use :func:`bind_rigid` instead.
    """
    if not isinstance(meshes, (list, tuple)):
        meshes = [meshes]

    bpy.ops.object.select_all(action="DESELECT")
    for mesh_obj in meshes:
        mesh_obj.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    _repair(meshes)


def bind_rigid(meshes, armature) -> None:
    """Attach meshes that already carry their own vertex groups.

    Automatic weighting is deliberately *not* used here. It solves each mesh on
    its own and does badly on the loose islands clothing is made of: a hem strip
    is a small floating box with nothing to propagate weights along, so the
    solver spreads them across whatever happens to be near and the garment tears
    itself apart as soon as the body moves.
    """
    if not isinstance(meshes, (list, tuple)):
        meshes = [meshes]

    bpy.ops.object.select_all(action="DESELECT")
    for mesh_obj in meshes:
        mesh_obj.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature

    # Plain ARMATURE parenting keeps the vertex groups that are already there.
    # ARMATURE_NAME would instead create empty groups for every bone and wipe
    # them — which silently drops the carried axe.
    bpy.ops.object.parent_set(type="ARMATURE")
    _repair(meshes)


def _repair(meshes) -> None:
    """Fix mesh data left inconsistent by skinning.

    The glTF exporter reports it as "mesh is not valid"; validate() repairs it
    in place. Running it before binding would be too early to help.
    """
    for mesh_obj in meshes:
        if mesh_obj.data.validate(verbose=False):
            print(f"[rig] repaired mesh data of '{mesh_obj.name}' after skinning")


def pose_bone(armature, bone_name: str):
    return armature.pose.bones.get(bone_name)
