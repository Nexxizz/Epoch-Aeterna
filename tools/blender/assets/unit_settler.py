"""Settler — the rigged, animated worker.

Built from tapered limbs rather than boxes. At RTS distance a unit is almost
pure silhouette, and a silhouette made of cuboids reads as a robot no matter how
good the animation is. Round limbs that narrow towards the joints, a head that
is not a cube and a bit of fur trim do more for the impression than any amount
of surface detail would.

The geometry is deliberately laid out along the bones of the shared humanoid
skeleton (see lib_rig), which is what lets automatic weights produce usable
deformation without hand-painting anything.
"""

from __future__ import annotations

import math

import lib_anim
import lib_material
import lib_mesh
import lib_rig

NAME = "unit_settler"
CATEGORY = "units"

HEIGHT = 1.8

# Joint positions, matching lib_rig.PROPORTIONS scaled to HEIGHT. Kept as named
# constants because both the body and the clothing have to hit the same joints.
FOOT_Z = 0.06
KNEE_Z = 0.50
HIP_Z = 0.95
WAIST_Z = 1.12
CHEST_Z = 1.34
SHOULDER_Z = 1.43
NECK_Z = 1.49
HEAD_Z = 1.63

HIP_X = 0.10
SHOULDER_X = 0.185
HAND_Z = 0.83
ELBOW_Z = 1.12


def build():
    import lib_scene
    lib_scene.reset()

    armature = lib_rig.humanoid(NAME, HEIGHT)

    skin = _pin_and_join(_skin_parts(), "body")
    lib_material.assign(skin, lib_material.skin())

    hair = _pin_and_join(_hair_parts(), "hair")
    lib_material.assign(hair, lib_material.hair())

    clothing = _pin_and_join(_clothing_parts(), "clothing")
    lib_material.assign(clothing, lib_material.team_color())

    tool = _pin_and_join(_tool_parts(), "tool")
    lib_material.assign(tool, lib_material.wood())

    # Everything is pinned to a single bone — see _skin_parts for why a solved
    # skin is the wrong tool for a figure assembled from separate segments.
    meshes = [skin, hair, clothing, tool]
    lib_rig.bind_rigid(meshes, armature)

    _build_animations(armature)

    return [armature, *meshes]


def _pin_and_join(parts, name: str):
    """Pin each part to its bone, then merge them into one object.

    Vertex groups survive a join, so assigning them first is what lets a single
    merged mesh still have its hem follow the hips while its collar follows the
    chest.
    """
    for obj, bone in parts:
        lib_rig.assign_to_bone(obj, bone)

    return lib_mesh.join([obj for obj, _ in parts], name)


# --- Body ----------------------------------------------------------------

def _skin_parts():
    """Head, torso and limbs, each tagged with the bone it belongs to.

    Every part is pinned to a single bone rather than skinned. On a figure
    assembled from separate cylinders, a solved skin spreads arm weights onto
    nearby torso vertices, and raising an arm then drags a web of stretched
    geometry out of the chest. Segments plus a ball in each joint have no such
    failure mode, and at RTS distance the difference is invisible.
    """
    parts = []

    for side, x, bone in (("l", 1.0, "L"), ("r", -1.0, "R")):
        # Legs.
        parts.append((lib_mesh.limb(
            f"thigh_{side}", (x * HIP_X, 0.0, HIP_Z), (x * HIP_X * 1.05, 0.01, KNEE_Z),
            radius_start=0.088, radius_end=0.062), f"leg_upper.{bone}"))
        parts.append((lib_mesh.limb(
            f"shin_{side}", (x * HIP_X * 1.05, 0.01, KNEE_Z), (x * HIP_X * 1.05, 0.02, FOOT_Z + 0.02),
            radius_start=0.062, radius_end=0.044), f"leg_lower.{bone}"))

        # Ball in the knee, covering the pivot so the segments never gap.
        parts.append((lib_mesh.sphere(
            f"knee_{side}", 0.063, location=(x * HIP_X * 1.05, 0.008, KNEE_Z),
            segments=7, rings=5), f"leg_lower.{bone}"))

        # Feet, angled slightly outwards as feet actually stand.
        foot = lib_mesh.box(
            f"foot_{side}", (0.10, 0.26, 0.075),
            location=(x * HIP_X * 1.05, -0.045, FOOT_Z), bevel=0.02, segments=1)
        foot.rotation_euler = (0.0, 0.0, x * 0.12)
        parts.append((foot, f"leg_lower.{bone}"))

        # Arms.
        parts.append((lib_mesh.limb(
            f"upperarm_{side}", (x * SHOULDER_X, 0.0, SHOULDER_Z), (x * 0.20, 0.01, ELBOW_Z),
            radius_start=0.060, radius_end=0.047), f"arm_upper.{bone}"))
        parts.append((lib_mesh.limb(
            f"forearm_{side}", (x * 0.20, 0.01, ELBOW_Z), (x * 0.205, 0.02, HAND_Z),
            radius_start=0.047, radius_end=0.038), f"arm_lower.{bone}"))

        parts.append((lib_mesh.sphere(
            f"elbow_{side}", 0.048, location=(x * 0.20, 0.01, ELBOW_Z),
            segments=7, rings=5), f"arm_lower.{bone}"))

        # Ball in the shoulder, which is where a raised arm would otherwise
        # tear open the biggest hole.
        parts.append((lib_mesh.sphere(
            f"shoulder_{side}", 0.063, location=(x * SHOULDER_X, 0.0, SHOULDER_Z),
            segments=7, rings=5), f"arm_upper.{bone}"))

        parts.append((lib_mesh.sphere(
            f"hand_{side}", 0.055, location=(x * 0.207, 0.025, HAND_Z - 0.03),
            segments=7, rings=4), f"arm_lower.{bone}"))

    # Torso in two segments, so it narrows at the waist and widens at the chest.
    parts.append((lib_mesh.limb("waist", (0.0, 0.0, HIP_Z - 0.03), (0.0, 0.0, WAIST_Z),
                                radius_start=0.152, radius_end=0.142), "hips"))
    parts.append((lib_mesh.limb("chest", (0.0, 0.0, WAIST_Z), (0.0, 0.0, CHEST_Z + 0.04),
                                radius_start=0.142, radius_end=0.175), "chest"))

    parts.append((lib_mesh.limb("neck", (0.0, 0.0, NECK_Z - 0.06), (0.0, 0.005, NECK_Z + 0.05),
                                radius_start=0.058, radius_end=0.052), "chest"))

    # Head: a squashed sphere plus a brow ridge and a nose, which is all a face
    # needs to read as a face from a distance.
    head = lib_mesh.sphere("head", 0.118, location=(0.0, 0.01, HEAD_Z), segments=10, rings=7)
    head.scale = (0.92, 1.0, 1.08)
    parts.append((head, "head"))

    parts.append((lib_mesh.box("brow", (0.155, 0.035, 0.028),
                               location=(0.0, -0.098, HEAD_Z + 0.030), bevel=0.010, segments=1), "head"))
    parts.append((lib_mesh.box("nose", (0.036, 0.032, 0.048),
                               location=(0.0, -0.115, HEAD_Z - 0.012), bevel=0.010, segments=1), "head"))

    return parts


def _hair_parts():
    """Hair and beard. Its own material, because skin-coloured hair looks bald."""
    cap = lib_mesh.sphere("hair_cap", 0.125, location=(0.0, 0.035, HEAD_Z + 0.03),
                          segments=10, rings=6)
    cap.scale = (0.94, 1.0, 0.9)

    beard = lib_mesh.limb("beard", (0.0, -0.045, HEAD_Z - 0.065), (0.0, -0.030, HEAD_Z - 0.145),
                          radius_start=0.070, radius_end=0.042, vertices=8)

    return [(cap, "head"), (beard, "head")]


# --- Clothing ------------------------------------------------------------

def _clothing_parts():
    """Fur wrap, shoulder strap and belt — this is what carries the team colour."""
    parts = []

    # The wrap sits just outside the torso and stops at the hip.
    parts.append((lib_mesh.limb("wrap", (0.0, 0.0, 0.88), (0.0, 0.0, CHEST_Z + 0.02),
                                radius_start=0.185, radius_end=0.178, vertices=10), "spine"))

    # A ragged hem of fur strips around the hip. Uneven lengths are what stop
    # the wrap reading as a moulded tube.
    hem_count = 11
    hem_radius = 0.176
    for index in range(hem_count):
        angle = math.tau * index / hem_count
        drop = (0.10, 0.16, 0.12, 0.19, 0.13, 0.17, 0.11, 0.20, 0.14, 0.15, 0.12)[index]

        # After the Z rotation the local X points outwards and Y runs along the
        # circle, so the width has to sit on Y — otherwise the strips stand out
        # sideways like a ruff instead of hanging flat against the hip.
        strip = lib_mesh.box(
            f"hem_{index}", (0.045, 0.085, 0.09 + drop),
            location=(math.cos(angle) * hem_radius,
                      math.sin(angle) * hem_radius,
                      0.90 - (0.09 + drop) * 0.5),
            bevel=0.01, segments=1)
        strip.rotation_euler = (0.0, 0.0, angle)
        parts.append((strip, "hips"))

    # Diagonal strap over one shoulder.
    parts.append((lib_mesh.limb("strap", (0.155, -0.09, CHEST_Z + 0.06), (-0.10, 0.05, 0.98),
                                radius_start=0.045, radius_end=0.040, vertices=6), "spine"))

    # Belt.
    parts.append((lib_mesh.limb("belt", (0.0, 0.0, 0.995), (0.0, 0.0, 1.05),
                                radius_start=0.163, radius_end=0.163, vertices=10), "hips"))

    # A fur collar of short tufts around the neckline. Without it the wrap is a
    # smooth cylinder, which is exactly what makes cheap models look moulded.
    collar_count = 9
    for index in range(collar_count):
        angle = math.tau * index / collar_count
        length = (0.10, 0.13, 0.09, 0.14, 0.11, 0.12, 0.10, 0.15, 0.11)[index]

        tuft = lib_mesh.box(
            f"collar_{index}", (0.05, 0.075, length),
            location=(math.cos(angle) * 0.172,
                      math.sin(angle) * 0.172,
                      CHEST_Z + 0.02 - length * 0.35),
            bevel=0.012, segments=1)
        tuft.rotation_euler = (0.0, 0.0, angle)

        # Same bone as the wrap. Pinning the collar to the chest instead lets it
        # slide off the garment it sits on as soon as the torso bends.
        parts.append((tuft, "spine"))

    # Shoulder pad on the strap side, so the silhouette is not symmetric.
    pad = lib_mesh.sphere("shoulder_pad", 0.072,
                          location=(SHOULDER_X - 0.01, 0.0, SHOULDER_Z), segments=8, rings=5)
    pad.scale = (1.0, 0.85, 0.6)
    parts.append((pad, "arm_upper.L"))

    return parts


def _tool_parts():
    """A stone axe carried in the right hand.

    Centred on the hand so automatic weights bind it to the hand and forearm —
    a tool that reaches far past the wrist ends up split between bones and tears
    apart as soon as the arm moves.
    """
    handle = lib_mesh.limb("axe_handle", (-0.232, 0.030, HAND_Z + 0.19), (-0.232, 0.005, HAND_Z - 0.20),
                           radius_start=0.021, radius_end=0.024, vertices=6)

    head = lib_mesh.box("axe_head", (0.05, 0.165, 0.10),
                        location=(-0.232, 0.022, HAND_Z + 0.195), bevel=0.02, segments=1)
    head.rotation_euler = (0.18, 0.0, 0.0)

    lashing = lib_mesh.limb("axe_lashing", (-0.232, 0.045, HAND_Z + 0.165), (-0.232, 0.018, HAND_Z + 0.225),
                            radius_start=0.030, radius_end=0.030, vertices=6)

    # All three follow the forearm of the hand that holds the axe. Weighting
    # them by proximity would put the head on the chest and the shaft on the
    # hip, and the axe would come apart the moment the arm swings.
    return [(handle, "arm_lower.R"), (head, "arm_lower.R"), (lashing, "arm_lower.R")]


def _build_animations(armature) -> None:
    """The clip set every human unit is expected to provide."""
    for clip in (lib_anim.idle, lib_anim.walk, lib_anim.work_chop, lib_anim.death):
        lib_anim.rest_pose(armature)
        clip(armature)

    lib_anim.rest_pose(armature)


def export():
    import lib_scene

    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)

    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = sum(lib_mesh.triangle_count(obj) for obj in meshes)
    clips = len(objects[0].animation_data.nla_tracks) if objects[0].animation_data else 0

    print(f"[asset] {NAME}: {triangles} triangles, {clips} animation clips")
    lib_scene.report(path)
    return path
