"""Colourful Stone Age storehouse with construction stages and rubble."""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_storehouse"
CATEGORY = "buildings"
SEED = 20260904


def build(stage: int = 3):
    """Build stage 0..3; stage 3 is the finished in-game model."""
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)
    groups = _groups()

    _foundation(groups, rng)
    if stage >= 1:
        _frame(groups)
        if stage < 3:
            _building_supplies(groups, rng)
    if stage >= 2:
        _walls(groups, partial=stage == 2)
        _roof(groups, partial=stage == 2)
    if stage >= 3:
        _storefront(groups, rng)

    return _finish(groups)


def build_rubble():
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 91)
    groups = _groups()

    for index in range(18):
        angle = rng.uniform(0.0, math.tau)
        radius = rng.uniform(0.2, 1.7)
        size = rng.uniform(0.14, 0.30)
        groups["stone"].append(lib_mesh.boulder(
            f"rubble_stone_{index}", size,
            (math.cos(angle) * radius, math.sin(angle) * radius, size * 0.28), rng))

    for index in range(10):
        angle = rng.uniform(0.0, math.tau)
        centre = (rng.uniform(-1.25, 1.25), rng.uniform(-1.15, 1.15), rng.uniform(0.08, 0.20))
        direction = (math.cos(angle) * rng.uniform(0.35, 0.75),
                     math.sin(angle) * rng.uniform(0.35, 0.75), 0.04)
        groups["wood"].append(lib_mesh.strut(
            f"broken_timber_{index}",
            (centre[0] - direction[0], centre[1] - direction[1], centre[2]),
            (centre[0] + direction[0], centre[1] + direction[1], centre[2] + direction[2]),
            width=rng.uniform(0.07, 0.12), bevel=0.008))

    for index in range(4):
        scrap = lib_mesh.box(
            f"thatch_scrap_{index}", (rng.uniform(0.45, 0.85), rng.uniform(0.25, 0.48), 0.05),
            location=(rng.uniform(-1.2, 1.2), rng.uniform(-1.2, 1.2), 0.08), bevel=0.006)
        scrap.rotation_euler.z = rng.uniform(0.0, math.tau)
        groups["thatch"].append(scrap)

    groups["team"].append(lib_mesh.box(
        "torn_storehouse_banner", (0.58, 0.04, 0.30),
        location=(0.35, -0.55, 0.10), bevel=0.006))
    return _finish(groups)


def _groups():
    return {name: [] for name in (
        "stone", "clay", "wood", "ochre", "hide", "thatch", "team", "bone", "fibre")}


def _foundation(groups, rng):
    groups["clay"].append(lib_mesh.box(
        "raised_packed_floor", (3.25, 2.85, 0.18), location=(0.0, 0.0, 0.09),
        bevel=0.06, segments=1))

    for index in range(22):
        side = index % 4
        along = (index // 4) / 5.0 * 2.8 - 1.4
        if side == 0:
            x, y = along, -1.45
        elif side == 1:
            x, y = 1.65, along * 0.88
        elif side == 2:
            x, y = -along, 1.45
        else:
            x, y = -1.65, -along * 0.88
        size = rng.uniform(0.16, 0.25)
        groups["stone"].append(lib_mesh.boulder(
            f"footing_stone_{index}", size, (x, y, size * 0.30), rng))

    # Broad front loading ramp.
    ramp = lib_mesh.box("loading_ramp", (1.15, 0.72, 0.12),
                        location=(0.0, -1.70, 0.11), bevel=0.025, segments=1)
    ramp.rotation_euler.x = math.radians(-7)
    groups["wood"].append(ramp)


def _frame(groups):
    for x in (-1.42, 0.0, 1.42):
        for y in (-1.18, 1.18):
            groups["wood"].append(lib_mesh.strut(
                f"post_{x}_{y}", (x, y, 0.15), (x, y, 2.02), width=0.13, bevel=0.014))

    for y in (-1.18, 1.18):
        groups["wood"].append(lib_mesh.strut(
            f"eave_beam_{y}", (-1.55, y, 1.98), (1.55, y, 1.98), width=0.12, bevel=0.012))
    for x in (-1.42, 1.42):
        groups["wood"].append(lib_mesh.strut(
            f"side_beam_{x}", (x, -1.28, 1.92), (x, 1.28, 1.92), width=0.11, bevel=0.010))

    groups["wood"].append(lib_mesh.strut(
        "ridge_beam", (-1.62, 0.0, 2.82), (1.62, 0.0, 2.82), width=0.105, bevel=0.010))
    for index, x in enumerate((-1.42, -0.72, 0.0, 0.72, 1.42)):
        for side, y in (("front", -1.43), ("back", 1.43)):
            groups["wood"].append(lib_mesh.strut(
                f"rafter_{side}_{index}", (x, 0.0, 2.82), (x, y, 1.88),
                width=0.085, bevel=0.008))

    # Raised internal storage deck keeps supplies dry and reads through the doorway.
    for index, y in enumerate((-0.72, -0.24, 0.24, 0.72)):
        groups["wood"].append(lib_mesh.box(
            f"floor_plank_{index}", (2.72, 0.36, 0.09),
            location=(0.0, y, 0.34), bevel=0.018, segments=1))


def _building_supplies(groups, rng):
    for index in range(5):
        y = -0.75 + index * 0.30
        groups["wood"].append(lib_mesh.strut(
            f"spare_beam_{index}", (1.65, y, 0.10), (2.25, y + 0.05, 0.13),
            width=0.065, bevel=0.007))
    for index in range(4):
        groups["thatch"].append(lib_mesh.box(
            f"thatch_bundle_{index}", (0.50, 0.25, 0.10),
            location=(-1.72 + index * 0.12, 0.65 + index * 0.10, 0.11 + index * 0.07),
            bevel=0.012, segments=1))


def _walls(groups, partial: bool):
    # Horizontal split-log boards; gaps preserve a handmade silhouette.
    for row, z in enumerate((0.62, 1.02, 1.42, 1.78)):
        if not partial or row % 3 != 1:
            groups["ochre"].append(lib_mesh.box(
                f"back_wall_{row}", (2.72, 0.10, 0.30),
                location=(0.0, 1.19, z), bevel=0.025, segments=1))

        for side, x in (("left", -1.43), ("right", 1.43)):
            if partial and (row + (0 if side == "left" else 1)) % 3 == 1:
                continue
            groups["ochre"].append(lib_mesh.box(
                f"{side}_wall_{row}", (0.10, 2.22, 0.30),
                location=(x, 0.0, z), bevel=0.025, segments=1))

        # Front is open in the middle for loading.
        if not partial or row % 2 == 0:
            for side, x in (("left", -1.05), ("right", 1.05)):
                groups["ochre"].append(lib_mesh.box(
                    f"front_{side}_{row}", (0.66, 0.10, 0.30),
                    location=(x, -1.19, z), bevel=0.025, segments=1))


def _roof(groups, partial: bool):
    slope = math.radians(33)
    for side, y, rotation in (("front", -0.74, slope), ("back", 0.74, -slope)):
        for index, x in enumerate((-1.50, -1.20, -0.90, -0.60, -0.30, 0.0,
                                   0.30, 0.60, 0.90, 1.20, 1.50)):
            if partial and (index + (0 if side == "front" else 1)) % 3 == 1:
                continue
            strip = lib_mesh.box(
                f"roof_{side}_{index}", (0.34, 1.75, 0.085),
                location=(x, y, 2.34), bevel=0.012, segments=1)
            strip.rotation_euler.x = rotation
            groups["thatch"].append(strip)

    groups["thatch"].append(lib_mesh.cylinder(
        "ridge_thatch", 0.13, 3.45, location=(0.0, 0.0, 2.85), vertices=8, bevel=0.008))
    groups["thatch"][-1].rotation_euler.y = math.pi / 2


def _storefront(groups, rng):
    # Team-colour sign makes ownership obvious without recolouring the resources.
    groups["team"].append(lib_mesh.box(
        "storehouse_banner", (1.05, 0.07, 0.34),
        location=(0.0, -1.29, 1.82), bevel=0.035, segments=1))
    for x in (-0.32, 0.32):
        groups["bone"].append(lib_mesh.cone(
            f"banner_tooth_{x}", 0.045, 0.0, 0.18,
            location=(x, -1.35, 1.57), vertices=6))

    # Immediately recognisable resource samples around the loading entrance.
    for row in range(2):
        for index in range(3):
            groups["wood"].append(lib_mesh.cylinder(
                f"stored_log_{row}_{index}", 0.13, 0.82,
                location=(-1.78, 0.58 + index * 0.29, 0.18 + row * 0.23),
                vertices=8, bevel=0.012))
            groups["wood"][-1].rotation_euler.y = math.pi / 2

    for index in range(7):
        size = rng.uniform(0.15, 0.25)
        groups["stone"].append(lib_mesh.boulder(
            f"stored_stone_{index}", size,
            (1.70 + rng.uniform(-0.18, 0.20), 0.45 + rng.uniform(-0.40, 0.40), size * 0.32), rng))

    for index, x in enumerate((-0.58, 0.0, 0.58)):
        sack = lib_mesh.sphere(
            f"ore_sack_{index}", 0.30, location=(x, -1.40, 0.39),
            segments=10, rings=6)
        sack.scale = (0.78, 0.62, 1.18)
        groups["hide"].append(sack)
        groups["fibre"].append(lib_mesh.strut(
            f"sack_tie_{index}", (x - 0.10, -1.40, 0.68), (x + 0.10, -1.40, 0.68),
            width=0.025, bevel=0.003))


def _finish(groups):
    materials = {
        "stone": lib_material.stone,
        "clay": lib_material.clay,
        "wood": lib_material.wood,
        "ochre": lib_material.ochre,
        "hide": lib_material.hide,
        "thatch": lib_material.thatch,
        "team": lib_material.team_color,
        "bone": lib_material.bone,
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
