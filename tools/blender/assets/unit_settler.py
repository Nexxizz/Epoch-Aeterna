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

    tunic = _pin_and_join(_tunic_parts(), "ochre_tunic")
    lib_material.assign(tunic, lib_material.ochre())

    clothing = _pin_and_join(_clothing_parts(), "fur_clothing")
    lib_material.assign(clothing, lib_material.fur())

    clan_marks = _pin_and_join(_team_parts(), "clan_marks")
    lib_material.assign(clan_marks, lib_material.team_color())

    bone_details = _pin_and_join(_bone_parts(), "bone_details")
    lib_material.assign(bone_details, lib_material.bone())

    tool_wood = _pin_and_join(_tool_wood_parts(), "tool_wood")
    lib_material.assign(tool_wood, lib_material.wood())

    tool_stone = _pin_and_join(_tool_stone_parts(), "tool_stone")
    lib_material.assign(tool_stone, lib_material.stone())

    basket = _pin_and_join(_basket_parts(), "basket")
    lib_material.assign(basket, lib_material.fibre())

    food = _pin_and_join(_food_parts(), "gathered_food")
    lib_material.assign(food, lib_material.food())

    # Everything is pinned to a single bone — see _skin_parts for why a solved
    # skin is the wrong tool for a figure assembled from separate segments.
    meshes = [skin, hair, tunic, clothing, clan_marks, bone_details,
              tool_wood, tool_stone, basket, food]
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
            radius_start=0.088, radius_end=0.062, vertices=10), f"leg_upper.{bone}"))
        parts.append((lib_mesh.limb(
            f"shin_{side}", (x * HIP_X * 1.05, 0.01, KNEE_Z), (x * HIP_X * 1.05, 0.02, FOOT_Z + 0.02),
            radius_start=0.062, radius_end=0.044, vertices=10), f"leg_lower.{bone}"))

        # Ball in the knee, covering the pivot so the segments never gap.
        parts.append((lib_mesh.sphere(
            f"knee_{side}", 0.063, location=(x * HIP_X * 1.05, 0.008, KNEE_Z),
            segments=9, rings=6), f"leg_lower.{bone}"))

        # Feet, angled slightly outwards as feet actually stand.
        foot = lib_mesh.box(
            f"foot_{side}", (0.10, 0.26, 0.075),
            location=(x * HIP_X * 1.05, -0.045, FOOT_Z), bevel=0.0)
        foot.rotation_euler = (0.0, 0.0, x * 0.12)
        parts.append((foot, f"leg_lower.{bone}"))

        # Arms.
        parts.append((lib_mesh.limb(
            f"upperarm_{side}", (x * SHOULDER_X, 0.0, SHOULDER_Z), (x * 0.20, 0.01, ELBOW_Z),
            radius_start=0.060, radius_end=0.047, vertices=10), f"arm_upper.{bone}"))
        parts.append((lib_mesh.limb(
            f"forearm_{side}", (x * 0.20, 0.01, ELBOW_Z), (x * 0.205, 0.02, HAND_Z),
            radius_start=0.047, radius_end=0.038, vertices=10), f"arm_lower.{bone}"))

        parts.append((lib_mesh.sphere(
            f"elbow_{side}", 0.048, location=(x * 0.20, 0.01, ELBOW_Z),
            segments=9, rings=6), f"arm_lower.{bone}"))

        # Ball in the shoulder, which is where a raised arm would otherwise
        # tear open the biggest hole.
        parts.append((lib_mesh.sphere(
            f"shoulder_{side}", 0.063, location=(x * SHOULDER_X, 0.0, SHOULDER_Z),
            segments=9, rings=6), f"arm_upper.{bone}"))

        parts.append((lib_mesh.sphere(
            f"hand_{side}", 0.055, location=(x * 0.207, 0.025, HAND_Z - 0.03),
            segments=9, rings=6), f"arm_lower.{bone}"))

        # A separate thumb is a tiny addition that stops the hand reading as a
        # ball, especially when it wraps around a tool haft.
        parts.append((lib_mesh.limb(
            f"thumb_{side}", (x * 0.212, -0.005, HAND_Z - 0.005),
            (x * 0.250, -0.025, HAND_Z - 0.045),
            radius_start=0.018, radius_end=0.012, vertices=7), f"arm_lower.{bone}"))

    # Torso in two segments, so it narrows at the waist and widens at the chest.
    parts.append((lib_mesh.limb("waist", (0.0, 0.0, HIP_Z - 0.03), (0.0, 0.0, WAIST_Z),
                                radius_start=0.152, radius_end=0.142, vertices=12), "hips"))
    parts.append((lib_mesh.limb("chest", (0.0, 0.0, WAIST_Z), (0.0, 0.0, CHEST_Z + 0.04),
                                radius_start=0.142, radius_end=0.175, vertices=12), "chest"))

    parts.append((lib_mesh.limb("neck", (0.0, 0.0, NECK_Z - 0.06), (0.0, 0.005, NECK_Z + 0.05),
                                radius_start=0.058, radius_end=0.052, vertices=10), "chest"))

    # Head: a squashed sphere plus a brow ridge and a nose, which is all a face
    # needs to read as a face from a distance.
    head = lib_mesh.sphere("head", 0.118, location=(0.0, 0.01, HEAD_Z), segments=14, rings=9)
    head.scale = (0.92, 1.0, 1.08)
    parts.append((head, "head"))

    parts.append((lib_mesh.box("nose", (0.036, 0.032, 0.048),
                               location=(0.0, -0.115, HEAD_Z - 0.012), bevel=0.010, segments=1), "head"))

    for side, x in (("l", 0.108), ("r", -0.108)):
        ear = lib_mesh.sphere(f"ear_{side}", 0.030, location=(x, 0.005, HEAD_Z),
                              segments=8, rings=5)
        ear.scale = (0.55, 0.75, 1.0)
        parts.append((ear, "head"))

    return parts


def _hair_parts():
    """Layered hair, brows and beard that remain readable from the game camera."""
    parts = []
    cap = lib_mesh.sphere("hair_cap", 0.125, location=(0.0, 0.035, HEAD_Z + 0.03),
                          segments=14, rings=8)
    cap.scale = (0.94, 1.0, 0.9)
    parts.append((cap, "head"))

    # A rough mane breaks the perfect helmet-like edge of the hair cap.
    for index, (x, z, length) in enumerate((
        (-0.085, HEAD_Z + 0.015, 0.13),
        (-0.045, HEAD_Z - 0.005, 0.17),
        (0.0, HEAD_Z - 0.012, 0.18),
        (0.045, HEAD_Z - 0.005, 0.17),
        (0.085, HEAD_Z + 0.015, 0.13),
    )):
        lock = lib_mesh.limb(f"hair_lock_{index}", (x, 0.092, z + 0.06),
                             (x * 1.08, 0.094, z - length),
                             radius_start=0.026, radius_end=0.014, vertices=7)
        parts.append((lock, "head"))

    for side, x in (("l", 0.044), ("r", -0.044)):
        brow = lib_mesh.box(f"brow_{side}", (0.064, 0.018, 0.015),
                            location=(x, -0.108, HEAD_Z + 0.034), bevel=0.005, segments=1)
        brow.rotation_euler.y = -0.12 if x > 0 else 0.12
        parts.append((brow, "head"))

        pupil = lib_mesh.sphere(f"pupil_{side}", 0.010,
                                location=(x, -0.128, HEAD_Z + 0.006),
                                segments=6, rings=3)
        pupil.scale = (1.0, 0.45, 1.0)
        parts.append((pupil, "head"))

    beard = lib_mesh.limb("beard", (0.0, -0.045, HEAD_Z - 0.065), (0.0, -0.030, HEAD_Z - 0.145),
                          radius_start=0.070, radius_end=0.042, vertices=10)
    parts.append((beard, "head"))

    mouth = lib_mesh.box("mouth", (0.052, 0.010, 0.010),
                         location=(0.0, -0.123, HEAD_Z - 0.052), bevel=0.0)
    parts.append((mouth, "head"))

    # Two smaller side tufts avoid the single-cone goatee silhouette.
    for side, x in (("l", 0.047), ("r", -0.047)):
        tuft = lib_mesh.limb(f"beard_{side}", (x, -0.060, HEAD_Z - 0.040),
                             (x * 0.70, -0.045, HEAD_Z - 0.125),
                             radius_start=0.032, radius_end=0.014, vertices=7)
        parts.append((tuft, "head"))

    return parts


# --- Clothing ------------------------------------------------------------

def _tunic_parts():
    """Warm ochre-dyed hide tunic — the main colour mass of the unit."""
    wrap = lib_mesh.limb("tunic", (0.0, 0.0, 0.88), (0.0, 0.0, CHEST_Z + 0.02),
                         radius_start=0.185, radius_end=0.178, vertices=10)
    return [(wrap, "spine")]


def _clothing_parts():
    """Dark fur trim, belt, boots and working wraps."""
    parts = []

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
            bevel=0.0)
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
            bevel=0.0)
        tuft.rotation_euler = (0.0, 0.0, angle)

        # Same bone as the wrap. Pinning the collar to the chest instead lets it
        # slide off the garment it sits on as soon as the torso bends.
        parts.append((tuft, "spine"))

    # Shoulder pad on the strap side, so the silhouette is not symmetric.
    pad = lib_mesh.sphere("shoulder_pad", 0.072,
                          location=(SHOULDER_X - 0.01, 0.0, SHOULDER_Z), segments=8, rings=5)
    pad.scale = (1.0, 0.85, 0.6)
    parts.append((pad, "arm_upper.L"))

    # An asymmetric pelt mantle softens the exposed shoulder joints and gives
    # the silhouette a clear "hunter-gatherer" layer above the tunic.
    for index, (x, y, z, scale) in enumerate((
        (0.145, -0.025, 1.43, (1.25, 0.80, 0.55)),
        (0.105, 0.020, 1.445, (1.10, 0.85, 0.50)),
        (0.060, 0.045, 1.445, (1.00, 0.90, 0.48)),
    )):
        tuft = lib_mesh.sphere(f"mantle_tuft_{index}", 0.068,
                               location=(x, y, z), segments=8, rings=5)
        tuft.scale = scale
        parts.append((tuft, "chest"))

    # Belt pouches are deliberately uneven; perfect symmetry reads as uniform,
    # while a worker should look as though the kit was assembled over time.
    for name, x, width, height in (
        ("pouch_large", 0.105, 0.115, 0.145),
        ("pouch_small", -0.120, 0.090, 0.115),
    ):
        pouch = lib_mesh.box(name, (width, 0.065, height),
                             location=(x, -0.185, 1.00), bevel=0.012, segments=1)
        parts.append((pouch, "hips"))

    # Leather wraps thicken the wrists and shins, giving the worker a practical
    # silhouette instead of leaving every limb as a bare tapered cylinder.
    for side, sign, suffix in (("l", 1.0, "L"), ("r", -1.0, "R")):
        wrist = lib_mesh.limb(f"wrist_wrap_{side}",
                              (sign * 0.202, 0.016, 0.91),
                              (sign * 0.205, 0.020, 0.84),
                              radius_start=0.052, radius_end=0.048, vertices=8)
        parts.append((wrist, f"arm_lower.{suffix}"))

        shin_wrap = lib_mesh.limb(f"shin_wrap_{side}",
                                  (sign * HIP_X * 1.05, 0.015, 0.34),
                                  (sign * HIP_X * 1.05, 0.020, 0.18),
                                  radius_start=0.066, radius_end=0.056, vertices=8)
        parts.append((shin_wrap, f"leg_lower.{suffix}"))

        boot = lib_mesh.box(f"boot_{side}", (0.118, 0.275, 0.086),
                            location=(sign * HIP_X * 1.05, -0.046, FOOT_Z + 0.006),
                            bevel=0.0)
        boot.rotation_euler.z = sign * 0.12
        parts.append((boot, f"leg_lower.{suffix}"))

    return parts


def _team_parts():
    """Small clan-colour accents; the hide garment itself stays naturally brown."""
    parts = []

    # Overlapping plates follow the torso better than one rigid diagonal box.
    # At game distance they merge into a single cloth sash, while close up the
    # stepped edge suggests hand-cut woven strips.
    sash_angle = math.atan2(0.265, 0.395)
    for index in range(5):
        t = (index + 0.5) / 5.0
        x = 0.145 + (-0.12 - 0.145) * t
        z = (CHEST_Z + 0.055) + (1.00 - (CHEST_Z + 0.055)) * t
        panel = lib_mesh.box(f"clan_sash_{index}", (0.082, 0.024, 0.105),
                             location=(x, -0.192, z), bevel=0.004, segments=1)
        panel.rotation_euler.y = sash_angle
        parts.append((panel, "spine"))

    for side, sign, suffix in (("l", 1.0, "L"), ("r", -1.0, "R")):
        band = lib_mesh.limb(f"clan_band_{side}",
                             (sign * 0.198, 0.0, 1.02),
                             (sign * 0.200, 0.0, 0.965),
                             radius_start=0.051, radius_end=0.051, vertices=8)
        parts.append((band, f"arm_lower.{suffix}"))

    # Two short face-paint marks stay legible even when most of the tunic is
    # hidden by a tool or another unit.
    for side, x in (("l", 0.076), ("r", -0.076)):
        paint = lib_mesh.box(f"face_paint_{side}", (0.018, 0.010, 0.052),
                             location=(x * 1.26, -0.064, HEAD_Z - 0.018), bevel=0.0)
        paint.rotation_euler.y = -0.25 if x > 0 else 0.25
        parts.append((paint, "head"))

    return parts


def _bone_parts():
    """Eye whites and a simple bone necklace add readable facial detail."""
    parts = []

    for side, x in (("l", 0.044), ("r", -0.044)):
        eye = lib_mesh.sphere(f"eye_{side}", 0.021,
                              location=(x, -0.118, HEAD_Z + 0.006),
                              segments=6, rings=3)
        eye.scale = (1.0, 0.38, 0.72)
        parts.append((eye, "head"))

    bead_positions = (
        (-0.105, -0.170, 1.405),
        (-0.055, -0.183, 1.375),
        (0.0, -0.188, 1.360),
        (0.055, -0.183, 1.375),
        (0.105, -0.170, 1.405),
    )
    for index, position in enumerate(bead_positions):
        bead = lib_mesh.sphere(f"necklace_bead_{index}", 0.022,
                               location=position, segments=6, rings=3)
        parts.append((bead, "chest"))

    pendant = lib_mesh.cone("tooth_pendant", 0.030, 0.0, 0.085,
                            location=(0.0, -0.190, 1.305), vertices=6)
    pendant.rotation_euler.x = math.pi
    parts.append((pendant, "chest"))

    # Pale sinew bindings separate stone heads from their dark wooden hafts.
    for name, bone, z, height in (
        ("axe_binding", "tool_axe", HAND_Z + 0.225, 0.10),
        ("pick_binding", "tool_pick", HAND_Z + 0.315, 0.11),
        ("spear_binding", "tool_spear", 1.70, 0.12),
    ):
        binding = lib_mesh.cylinder(name, 0.031, height,
                                    location=(-0.232, 0.012, z),
                                    vertices=7, bevel=0.0)
        parts.append((binding, bone))

    return parts


# --- Task equipment ------------------------------------------------------

def _tool_wood_parts():
    """Wooden hafts for the axe, pick and hunting spear."""
    return [
        (lib_mesh.limb("axe_handle", (-0.232, 0.025, HAND_Z + 0.25),
                       (-0.232, 0.005, HAND_Z - 0.25),
                       radius_start=0.020, radius_end=0.025, vertices=7), "tool_axe"),
        (lib_mesh.limb("pick_handle", (-0.232, 0.025, HAND_Z + 0.34),
                       (-0.232, 0.005, HAND_Z - 0.31),
                       radius_start=0.022, radius_end=0.027, vertices=7), "tool_pick"),
        (lib_mesh.limb("spear_shaft", (-0.232, 0.012, 1.72),
                       (-0.232, 0.012, 0.08),
                       radius_start=0.018, radius_end=0.025, vertices=7), "tool_spear"),
    ]


def _tool_stone_parts():
    """Knapped stone heads, each weighted to the matching equipment slot."""
    axe = lib_mesh.box("axe_head", (0.060, 0.205, 0.125),
                       location=(-0.232, 0.020, HAND_Z + 0.255), bevel=0.018, segments=1)
    axe.rotation_euler = (0.18, 0.0, 0.0)

    pick = lib_mesh.limb("pick_head", (-0.232, -0.17, HAND_Z + 0.35),
                         (-0.232, 0.18, HAND_Z + 0.35),
                         radius_start=0.035, radius_end=0.018, vertices=6)

    spear = lib_mesh.cone("spear_head", 0.055, 0.0, 0.20,
                          location=(-0.232, 0.012, 1.82), vertices=6)

    return [(axe, "tool_axe"), (pick, "tool_pick"), (spear, "tool_spear")]


def _basket_parts():
    """Woven back basket used while gathering and carrying food."""
    parts = []
    bowl = lib_mesh.cone("basket_bowl", 0.17, 0.23, 0.36,
                         location=(0.0, 0.27, 1.08), vertices=9)
    parts.append((bowl, "tool_basket"))

    rim = lib_mesh.cylinder("basket_rim", 0.235, 0.035,
                            location=(0.0, 0.27, 1.26), vertices=9, bevel=0.0)
    parts.append((rim, "tool_basket"))

    # Shoulder straps remain visible at an angle, which reads more clearly than
    # a handle hidden behind the character's head.
    for side, x in (("l", 0.15), ("r", -0.15)):
        strap = lib_mesh.strut(f"basket_strap_{side}",
                               (x, 0.18, 1.25), (x * 0.75, 0.13, 0.91),
                               width=0.022, bevel=0.0)
        parts.append((strap, "tool_basket"))

    return parts


def _food_parts():
    """A few abstract berries/meat pieces that make the basket read as loaded."""
    parts = []
    for index, (x, y, z, radius) in enumerate((
        (-0.09, 0.27, 1.29, 0.050),
        (0.0, 0.25, 1.30, 0.060),
        (0.08, 0.29, 1.285, 0.047),
        (0.02, 0.34, 1.285, 0.042),
    )):
        parts.append((lib_mesh.sphere(f"food_{index}", radius, location=(x, y, z),
                                      segments=6, rings=3), "tool_basket"))
    return parts


def _build_animations(armature) -> None:
    """Full worker clip set, including every gathering and combat task."""
    for clip in (
        lib_anim.idle,
        lib_anim.walk,
        lib_anim.run,
        lib_anim.carry_walk,
        lib_anim.gather_food,
        lib_anim.work_chop,
        lib_anim.work_mine,
        lib_anim.attack,
        lib_anim.build,
        lib_anim.death,
    ):
        lib_anim.rest_pose(armature)
        clip(armature)

    lib_anim.rest_pose(armature)


def export():
    import lib_scene

    objects = build()
    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = sum(lib_mesh.triangle_count(obj) for obj in meshes)
    clips = len(objects[0].animation_data.nla_tracks) if objects[0].animation_data else 0

    print(f"[asset] {NAME}: {triangles} triangles, {clips} animation clips")
    print("[asset] meshes: " + ", ".join(
        f"{obj.name}={lib_mesh.triangle_count(obj)}" for obj in meshes))

    path = lib_scene.export_glb(objects, CATEGORY, NAME)
    lib_scene.report(path)
    return path
