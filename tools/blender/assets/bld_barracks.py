"""Fortified Stone Age barracks, three construction stages and rubble."""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_barracks"
CATEGORY = "buildings"
SEED = 20260906


def build(stage: int = 3):
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)
    groups = _groups()
    _foundation(groups, rng)
    if stage >= 1:
        _frame(groups)
        if stage < 3:
            _supplies(groups)
    if stage >= 2:
        _palisade(groups, partial=stage == 2)
        _roof(groups, partial=stage == 2)
    if stage >= 3:
        _training_yard(groups, rng)
    return _finish(groups)


def build_rubble():
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 77)
    groups = _groups()
    for index in range(24):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.2, 2.4)
        size = rng.uniform(0.15, 0.31)
        groups["stone"].append(lib_mesh.boulder(
            f"rubble_stone_{index}", size,
            (math.cos(a) * r, math.sin(a) * r, size * 0.3), rng))
    for index in range(14):
        a = rng.uniform(0, math.tau)
        x, y = rng.uniform(-2, 2), rng.uniform(-2, 2)
        groups["wood"].append(lib_mesh.strut(
            f"broken_palisade_{index}", (x, y, 0.08),
            (x + math.cos(a) * rng.uniform(0.6, 1.2),
             y + math.sin(a) * rng.uniform(0.6, 1.2), 0.16),
            width=0.09, bevel=0.008))
    groups["team"].append(lib_mesh.box(
        "torn_banner", (0.72, 0.05, 0.38), location=(0.3, -0.6, 0.11), bevel=0.01))
    return _finish(groups)


def _groups():
    return {name: [] for name in (
        "stone", "clay", "wood", "hide", "thatch", "ochre", "team", "bone", "fibre")}


def _foundation(groups, rng):
    groups["clay"].append(lib_mesh.box(
        "training_yard", (5.35, 5.15, 0.15), location=(0, 0, 0.075), bevel=0.12, segments=1))
    for index in range(32):
        side = index % 4
        t = (index // 4) / 7.0 * 4.8 - 2.4
        x, y = ((t, -2.62), (2.72, t), (-t, 2.62), (-2.72, -t))[side]
        # Front gate remains open.
        if y < -2.5 and abs(x) < 0.75:
            continue
        size = rng.uniform(0.16, 0.27)
        groups["stone"].append(lib_mesh.boulder(
            f"foundation_stone_{index}", size, (x, y, size * 0.3), rng))


def _frame(groups):
    posts = ((-2.35, -2.15), (2.35, -2.15), (-2.35, 2.15), (2.35, 2.15),
             (-0.72, -2.28), (0.72, -2.28))
    for index, (x, y) in enumerate(posts):
        top = 2.45 if y > -2.2 else 2.62
        groups["wood"].append(lib_mesh.strut(
            f"main_post_{index}", (x, y, 0.14), (x, y, top), width=0.16, bevel=0.016))

    for y in (-2.15, 2.15):
        groups["wood"].append(lib_mesh.strut(
            f"long_beam_{y}", (-2.48, y, 2.35), (2.48, y, 2.35), width=0.14, bevel=0.012))
    for x in (-2.35, 2.35):
        groups["wood"].append(lib_mesh.strut(
            f"side_beam_{x}", (x, -2.28, 2.30), (x, 2.28, 2.30), width=0.13, bevel=0.012))

    groups["wood"].append(lib_mesh.strut(
        "roof_ridge", (-2.55, 0, 3.48), (2.55, 0, 3.48), width=0.13, bevel=0.012))
    for index, x in enumerate((-2.35, -1.18, 0.0, 1.18, 2.35)):
        for side, y in (("front", -2.42), ("back", 2.42)):
            groups["wood"].append(lib_mesh.strut(
                f"rafter_{side}_{index}", (x, 0, 3.48), (x, y, 2.24),
                width=0.10, bevel=0.009))


def _supplies(groups):
    for index in range(7):
        groups["wood"].append(lib_mesh.strut(
            f"spare_log_{index}", (-2.75, -1.2 + index * 0.34, 0.10),
            (-1.75, -1.16 + index * 0.34, 0.13), width=0.075, bevel=0.007))
    for index in range(4):
        groups["hide"].append(lib_mesh.box(
            f"hide_bundle_{index}", (0.62, 0.38, 0.08),
            location=(2.65 - index * 0.12, 1.2 - index * 0.05, 0.10 + index * 0.07),
            bevel=0.012, segments=1))


def _palisade(groups, partial: bool):
    # Back and side walls are sharpened upright trunks; the front remains a broad gate.
    positions = []
    for x in (-2.28, -1.82, -1.36, -0.90, -0.44, 0.02, 0.48, 0.94, 1.40, 1.86, 2.32):
        positions.append((x, 2.18, 0))
    for y in (-1.72, -1.25, -0.78, -0.31, 0.16, 0.63, 1.10, 1.57):
        positions.extend(((-2.38, y, 0), (2.38, y, 1)))
    for x in (-2.28, -1.82, -1.36, 1.36, 1.82, 2.28):
        positions.append((x, -2.18, 2))

    for index, (x, y, side) in enumerate(positions):
        if partial and index % 3 == 1:
            continue
        height = 1.62 + (index % 3) * 0.09
        groups["ochre"].append(lib_mesh.cone(
            f"palisade_{index}", 0.19, 0.045, height,
            location=(x, y, 0.16 + height * 0.5), vertices=8))


def _roof(groups, partial: bool):
    slope = math.radians(28)
    for side, y, rotation in (("front", -1.15, slope), ("back", 1.15, -slope)):
        for index, x in enumerate((-2.45, -2.0, -1.55, -1.10, -0.65, -0.20,
                                   0.25, 0.70, 1.15, 1.60, 2.05, 2.50)):
            if partial and (index + (1 if side == "back" else 0)) % 3 == 1:
                continue
            panel = lib_mesh.box(
                f"roof_{side}_{index}", (0.50, 2.75, 0.10),
                location=(x, y, 2.84), bevel=0.014, segments=1)
            panel.rotation_euler.x = rotation
            groups["thatch"].append(panel)


def _training_yard(groups, rng):
    # Large ownership banner above the gate.
    groups["team"].append(lib_mesh.box(
        "gate_banner", (1.25, 0.08, 0.55), location=(0, -2.36, 2.28),
        bevel=0.035, segments=1))
    for x in (-0.38, 0.0, 0.38):
        groups["bone"].append(lib_mesh.cone(
            f"banner_tooth_{x}", 0.045, 0.0, 0.20,
            location=(x, -2.43, 1.92), vertices=6))

    # Training dummy with hide torso and crossed arms.
    groups["wood"].append(lib_mesh.strut(
        "dummy_post", (0.95, 0.70, 0.12), (0.95, 0.70, 1.78), width=0.10, bevel=0.008))
    groups["wood"].append(lib_mesh.strut(
        "dummy_arms", (0.30, 0.70, 1.35), (1.60, 0.70, 1.35), width=0.075, bevel=0.007))
    groups["hide"].append(lib_mesh.box(
        "dummy_torso", (0.62, 0.32, 0.72), location=(0.95, 0.70, 1.08),
        bevel=0.08, segments=1))
    groups["fibre"].append(lib_mesh.sphere(
        "dummy_head", 0.25, location=(0.95, 0.70, 1.76), segments=10, rings=6))

    # Weapon rack with spears: unmistakable purpose even at RTS camera distance.
    rack_x = -1.30
    for x in (rack_x - 0.48, rack_x + 0.48):
        groups["wood"].append(lib_mesh.strut(
            f"rack_post_{x}", (x, -0.10, 0.10), (x, -0.10, 1.35), width=0.075, bevel=0.007))
    groups["wood"].append(lib_mesh.strut(
        "rack_bar", (rack_x - 0.52, -0.10, 1.10), (rack_x + 0.52, -0.10, 1.10),
        width=0.065, bevel=0.006))
    for index, x in enumerate((-1.65, -1.42, -1.19, -0.96)):
        groups["wood"].append(lib_mesh.strut(
            f"practice_spear_{index}", (x, -0.04, 0.20), (x + 0.10, -0.04, 2.05),
            width=0.035, bevel=0.003))
        groups["stone"].append(lib_mesh.cone(
            f"spear_tip_{index}", 0.075, 0.0, 0.22,
            location=(x + 0.105, -0.04, 2.13), vertices=6))


def _finish(groups):
    materials = {
        "stone": lib_material.stone, "clay": lib_material.clay,
        "wood": lib_material.wood, "hide": lib_material.hide,
        "thatch": lib_material.thatch, "ochre": lib_material.ochre,
        "team": lib_material.team_color, "bone": lib_material.bone,
        "fibre": lib_material.fibre,
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
