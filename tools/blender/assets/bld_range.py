"""Colourful Stone Age archery range, three construction stages and rubble."""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_range"
CATEGORY = "buildings"
SEED = 20260907


def build(stage: int = 3):
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)
    groups = _groups()
    _foundation(groups, rng)
    if stage >= 1:
        _shelter_frame(groups)
        _supplies(groups)
    if stage >= 2:
        _fence(groups, partial=True)
        _shelter(groups, partial=True)
        _target_stands(groups, faces=False)
    if stage >= 3:
        _fence(groups, partial=False)
        _shelter(groups, partial=False)
        _target_stands(groups, faces=True)
        _training_equipment(groups, rng)
    return _finish(groups)


def build_rubble():
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 91)
    groups = _groups()
    for index in range(22):
        x, y = rng.uniform(-2.5, 2.5), rng.uniform(-2.5, 2.5)
        size = rng.uniform(0.12, 0.27)
        groups["stone"].append(lib_mesh.boulder(
            f"rubble_stone_{index}", size, (x, y, size * 0.28), rng))
    for index in range(15):
        x, y = rng.uniform(-2.25, 2.25), rng.uniform(-2.25, 2.25)
        angle = rng.uniform(0, math.tau)
        length = rng.uniform(0.45, 1.25)
        groups["wood"].append(lib_mesh.strut(
            f"broken_beam_{index}", (x, y, 0.07),
            (x + math.cos(angle) * length, y + math.sin(angle) * length, 0.13),
            width=0.07, bevel=0.006))
    broken_target = lib_mesh.cylinder(
        "broken_target", 0.48, 0.09, location=(-0.65, 0.25, 0.10),
        vertices=16, bevel=0.012)
    broken_target.rotation_euler.x = math.pi / 2
    groups["hide"].append(broken_target)
    groups["team"].append(lib_mesh.box(
        "torn_range_banner", (0.72, 0.05, 0.34),
        location=(0.55, -0.55, 0.13), bevel=0.012, segments=1))
    return _finish(groups)


def _groups():
    return {name: [] for name in (
        "clay", "stone", "wood", "bark", "thatch", "hide", "ochre",
        "team", "bone", "fibre", "dark")}


def _foundation(groups, rng):
    groups["clay"].append(lib_mesh.box(
        "range_yard", (5.45, 5.35, 0.14), location=(0, 0, 0.07),
        bevel=0.13, segments=1))

    # Three shooting lanes remain visible even in the earliest construction phase.
    for lane, x in enumerate((-1.55, 0.0, 1.55)):
        for marker in range(5):
            y = -2.15 + marker * 0.72
            size = 0.105 + (marker % 2) * 0.018
            groups["stone"].append(lib_mesh.boulder(
                f"lane_{lane}_marker_{marker}", size,
                (x - 0.48, y, 0.11), rng, flatten=0.45))
            groups["stone"].append(lib_mesh.boulder(
                f"lane_{lane}_marker_r_{marker}", size,
                (x + 0.48, y, 0.11), rng, flatten=0.45))

    # A rough stone curb frames the yard but leaves the broad front entrance open.
    for index in range(28):
        side = index % 3
        t = (index // 3) / 9.0 * 4.9 - 2.45
        if side == 0:
            x, y = t, 2.63
        elif side == 1:
            x, y = -2.72, t
        else:
            x, y = 2.72, -t
        size = rng.uniform(0.13, 0.23)
        groups["stone"].append(lib_mesh.boulder(
            f"curb_{index}", size, (x, y, size * 0.3), rng))


def _shelter_frame(groups):
    # A broad lean-to protects the target wall and gives the range its silhouette.
    for index, x in enumerate((-2.35, -0.78, 0.78, 2.35)):
        groups["wood"].append(lib_mesh.strut(
            f"rear_post_{index}", (x, 2.18, 0.12), (x, 2.18, 2.72),
            width=0.14, bevel=0.014))
        groups["wood"].append(lib_mesh.strut(
            f"front_post_{index}", (x, 0.82, 0.12), (x, 0.82, 2.20),
            width=0.13, bevel=0.013))
        groups["wood"].append(lib_mesh.strut(
            f"roof_rail_{index}", (x, 0.72, 2.17), (x, 2.30, 2.75),
            width=0.09, bevel=0.008))
    groups["wood"].append(lib_mesh.strut(
        "rear_crossbeam", (-2.52, 2.18, 2.63), (2.52, 2.18, 2.63),
        width=0.14, bevel=0.012))
    groups["wood"].append(lib_mesh.strut(
        "front_crossbeam", (-2.52, 0.82, 2.12), (2.52, 0.82, 2.12),
        width=0.13, bevel=0.012))
    for x in (-1.55, 0.0, 1.55):
        groups["fibre"].append(lib_mesh.strut(
            f"frame_lashing_{x}", (x - 0.12, 0.78, 2.08),
            (x + 0.12, 0.86, 2.18), width=0.035, bevel=0.003))


def _supplies(groups):
    for index in range(6):
        groups["wood"].append(lib_mesh.strut(
            f"spare_pole_{index}", (-2.55, -1.70 + index * 0.22, 0.10),
            (-1.48, -1.66 + index * 0.22, 0.14), width=0.055, bevel=0.005))
    for index in range(3):
        groups["thatch"].append(lib_mesh.box(
            f"thatch_bundle_{index}", (0.72, 0.36, 0.12),
            location=(2.30 - index * 0.13, -1.42, 0.12 + index * 0.10),
            bevel=0.018, segments=1))


def _fence(groups, partial: bool):
    # Low rails keep the range visually open. There is no front fence.
    segments = []
    for y in (-1.82, -0.95, -0.08, 0.79, 1.66):
        segments.extend(((-2.52, y), (2.52, y)))
    for index, (x, y) in enumerate(segments):
        if partial and index % 3 == 1:
            continue
        groups["wood"].append(lib_mesh.strut(
            f"fence_post_{index}", (x, y, 0.10), (x, y, 1.02),
            width=0.085, bevel=0.008))

    rails = (
        ((-2.52, -2.15, 0.68), (-2.52, 1.82, 0.68)),
        ((2.52, -2.15, 0.68), (2.52, 1.82, 0.68)),
    )
    for index, (start, end) in enumerate(rails):
        if partial and index == 1:
            continue
        groups["wood"].append(lib_mesh.strut(
            f"side_rail_{index}", start, end, width=0.065, bevel=0.006))


def _shelter(groups, partial: bool):
    for index, x in enumerate((-2.35, -1.88, -1.41, -0.94, -0.47, 0.0,
                               0.47, 0.94, 1.41, 1.88, 2.35)):
        if partial and index % 3 == 1:
            continue
        panel = lib_mesh.box(
            f"awning_{index}", (0.51, 1.78, 0.10),
            location=(x, 1.52, 2.43), bevel=0.014, segments=1)
        panel.rotation_euler.x = math.radians(20)
        groups["thatch"].append(panel)

    # Coloured hide strips make the finished roof vivid and readable at distance.
    if not partial:
        for index, x in enumerate((-1.55, 0.0, 1.55)):
            strip = lib_mesh.box(
                f"awning_colour_{index}", (0.18, 1.74, 0.035),
                location=(x, 1.505, 2.49), bevel=0.008, segments=1)
            strip.rotation_euler.x = math.radians(20)
            groups["team"].append(strip)


def _target_stands(groups, faces: bool):
    for index, x in enumerate((-1.55, 0.0, 1.55)):
        groups["wood"].append(lib_mesh.strut(
            f"target_leg_l_{index}", (x - 0.46, 1.86, 0.10),
            (x - 0.18, 1.58, 1.18), width=0.075, bevel=0.007))
        groups["wood"].append(lib_mesh.strut(
            f"target_leg_r_{index}", (x + 0.46, 1.86, 0.10),
            (x + 0.18, 1.58, 1.18), width=0.075, bevel=0.007))
        groups["wood"].append(lib_mesh.strut(
            f"target_bar_{index}", (x - 0.40, 1.66, 0.45),
            (x + 0.40, 1.66, 0.45), width=0.065, bevel=0.006))
        if not faces:
            continue

        # Three shallow cylinders form a high-contrast concentric target.
        for ring_name, radius, y, group in (
                ("outer", 0.58, 1.565, "hide"),
                ("middle", 0.37, 1.500, "ochre"),
                ("bull", 0.16, 1.445, "team")):
            disc = lib_mesh.cylinder(
                f"target_{ring_name}_{index}", radius, 0.10,
                location=(x, y, 1.31), vertices=20, bevel=0.012)
            disc.rotation_euler.x = math.pi / 2
            groups[group].append(disc)
        for arrow in range(2):
            dx = (-0.12, 0.17)[arrow] + index * 0.01
            groups["dark"].append(lib_mesh.strut(
                f"target_arrow_{index}_{arrow}", (x + dx, 1.33, 1.35 + arrow * 0.13),
                (x + dx, 0.93, 1.35 + arrow * 0.13), width=0.022, bevel=0.002))
            groups["bone"].append(lib_mesh.cone(
                f"arrow_tip_{index}_{arrow}", 0.045, 0.0, 0.13,
                location=(x + dx, 0.875, 1.35 + arrow * 0.13), vertices=6))
            groups["bone"][-1].rotation_euler.x = math.pi / 2


def _bow(groups, name, x, y, z, scale=1.0):
    # A faceted curve is much clearer than a thin torus from the game camera.
    points = (
        (x, y, z - 0.62 * scale),
        (x - 0.22 * scale, y, z - 0.30 * scale),
        (x - 0.28 * scale, y, z),
        (x - 0.22 * scale, y, z + 0.30 * scale),
        (x, y, z + 0.62 * scale),
    )
    for index in range(len(points) - 1):
        groups["ochre"].append(lib_mesh.strut(
            f"{name}_limb_{index}", points[index], points[index + 1],
            width=0.045 * scale, bevel=0.004))
    groups["fibre"].append(lib_mesh.strut(
        f"{name}_string", points[0], points[-1], width=0.014, bevel=0.001))


def _training_equipment(groups, rng):
    # Bow display and arrow rack beside the entrance.
    for x in (-2.18, -1.72):
        groups["wood"].append(lib_mesh.strut(
            f"bow_rack_post_{x}", (x, -1.85, 0.10), (x, -1.85, 1.62),
            width=0.075, bevel=0.007))
    for z in (0.65, 1.38):
        groups["wood"].append(lib_mesh.strut(
            f"bow_rack_bar_{z}", (-2.28, -1.85, z), (-1.62, -1.85, z),
            width=0.06, bevel=0.006))
    _bow(groups, "practice_bow_low", -1.82, -1.77, 0.78, 0.72)
    _bow(groups, "practice_bow_high", -2.10, -1.76, 1.12, 0.70)

    for index, x in enumerate((-2.20, -2.05, -1.90, -1.75, -1.60)):
        groups["dark"].append(lib_mesh.strut(
            f"rack_arrow_{index}", (x, -1.63, 0.22),
            (x + 0.05, -1.63, 1.58), width=0.023, bevel=0.002))
        groups["bone"].append(lib_mesh.cone(
            f"rack_arrow_tip_{index}", 0.042, 0.0, 0.14,
            location=(x + 0.055, -1.63, 1.64), vertices=6))

    # Sling-stone baskets occupy the opposite side of the entrance.
    for basket_index, x in enumerate((1.72, 2.18)):
        groups["fibre"].append(lib_mesh.cylinder(
            f"stone_basket_{basket_index}", 0.31, 0.34,
            location=(x, -1.78, 0.25), vertices=12, bevel=0.018))
        for stone in range(6):
            groups["stone"].append(lib_mesh.boulder(
                f"sling_stone_{basket_index}_{stone}", 0.095,
                (x + rng.uniform(-0.18, 0.18), -1.78 + rng.uniform(-0.16, 0.16),
                 0.43 + rng.uniform(0.0, 0.10)), rng, flatten=0.8))

    # Tall ownership standard at the open entrance.
    groups["wood"].append(lib_mesh.strut(
        "range_standard", (0.0, -2.48, 0.10), (0.0, -2.48, 2.38),
        width=0.075, bevel=0.007))
    groups["team"].append(lib_mesh.box(
        "range_banner", (0.92, 0.055, 0.64),
        location=(0.49, -2.48, 1.94), bevel=0.035, segments=1))
    for x in (0.16, 0.48, 0.80):
        groups["bone"].append(lib_mesh.cone(
            f"banner_tusk_{x}", 0.05, 0.0, 0.22,
            location=(x, -2.53, 1.52), vertices=6))


def _finish(groups):
    materials = {
        "clay": lib_material.clay,
        "stone": lib_material.stone,
        "wood": lib_material.wood,
        "bark": lib_material.bark,
        "thatch": lib_material.thatch,
        "hide": lib_material.hide,
        "ochre": lib_material.ochre,
        "team": lib_material.team_color,
        "bone": lib_material.bone,
        "fibre": lib_material.fibre,
        "dark": lambda: lib_material.pbr("range_arrow_wood", (0.12, 0.055, 0.025), roughness=0.88),
    }
    objects = []
    for name, parts in groups.items():
        if not parts:
            continue
        joined = lib_mesh.join(parts, name)
        lib_material.assign(joined, materials[name]())
        objects.append(joined)
    lib_mesh.ground_assembly(objects)
    return objects


def export():
    import lib_scene
    variants = (
        (f"{NAME}_stage_0", lambda: build(0)),
        (f"{NAME}_stage_1", lambda: build(1)),
        (f"{NAME}_stage_2", lambda: build(2)),
        (NAME, lambda: build(3)),
        (f"{NAME}_rubble", build_rubble),
    )
    paths = []
    for variant_name, factory in variants:
        objects = factory()
        triangles = sum(lib_mesh.triangle_count(obj) for obj in objects if obj.type == "MESH")
        print(f"[asset] {variant_name}: {triangles} triangles")
        path = lib_scene.export_glb(objects, CATEGORY, variant_name)
        lib_scene.report(path)
        paths.append(path)
    return paths
