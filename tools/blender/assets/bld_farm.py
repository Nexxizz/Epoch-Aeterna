"""Stone Age field with three growth stages and a stocked final state."""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_farm"
CATEGORY = "buildings"
SEED = 20260905


def build(stage: int = 3):
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)
    groups = _groups()
    _soil(groups)
    if stage >= 1:
        _fence(groups)
        _crops(groups, stage, rng)
    if stage >= 2:
        _tools(groups)
    if stage >= 3:
        _harvest_details(groups, rng)
    return _finish(groups)


def build_rubble():
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 50)
    groups = _groups()
    _soil(groups)
    for index in range(10):
        x, y = rng.uniform(-2.4, 2.4), rng.uniform(-2.4, 2.4)
        angle = rng.uniform(0, math.tau)
        groups["wood"].append(lib_mesh.strut(
            f"broken_fence_{index}", (x, y, 0.08),
            (x + math.cos(angle) * 0.8, y + math.sin(angle) * 0.8, 0.13),
            width=0.06, bevel=0.006))
    return _finish(groups)


def _groups():
    return {name: [] for name in ("soil", "clay", "wood", "crop", "fibre", "food", "team")}


def _soil(groups):
    groups["soil"].append(lib_mesh.box(
        "dark_farm_soil", (5.35, 5.35, 0.12), location=(0, 0, 0.06), bevel=0.10, segments=1))
    for row, y in enumerate((-1.95, -1.30, -0.65, 0.0, 0.65, 1.30, 1.95)):
        groups["clay"].append(lib_mesh.cylinder(
            f"furrow_{row}", 0.10, 4.75, location=(0, y, 0.16), vertices=8, bevel=0.006))
        groups["clay"][-1].rotation_euler.y = math.pi / 2


def _fence(groups):
    corners = ((-2.65, -2.65), (2.65, -2.65), (2.65, 2.65), (-2.65, 2.65))
    for index, (x, y) in enumerate(corners):
        groups["wood"].append(lib_mesh.strut(
            f"fence_post_{index}", (x, y, 0.05), (x, y, 0.78), width=0.09, bevel=0.008))
    rails = (
        ((-2.65, -2.65, 0.48), (-0.55, -2.65, 0.48)),
        ((0.55, -2.65, 0.48), (2.65, -2.65, 0.48)),
        ((2.65, -2.65, 0.48), (2.65, 2.65, 0.48)),
        ((2.65, 2.65, 0.48), (-2.65, 2.65, 0.48)),
        ((-2.65, 2.65, 0.48), (-2.65, -2.65, 0.48)),
    )
    for index, (start, end) in enumerate(rails):
        groups["wood"].append(lib_mesh.strut(
            f"fence_rail_{index}", start, end, width=0.065, bevel=0.006))


def _crops(groups, stage, rng):
    rows = (-1.95, -1.30, -0.65, 0.0, 0.65, 1.30, 1.95)
    columns = (-2.05, -1.35, -0.68, 0.0, 0.68, 1.35, 2.05)
    density = 2 if stage == 1 else 4 if stage == 2 else 7
    height = 0.18 if stage == 1 else 0.42 if stage == 2 else 0.72
    for row_index, y in enumerate(rows):
        for col_index, x in enumerate(columns):
            if (row_index * 2 + col_index) % 7 >= density:
                continue
            jitter_x = rng.uniform(-0.08, 0.08)
            jitter_y = rng.uniform(-0.06, 0.06)
            groups["crop"].append(lib_mesh.cone(
                f"crop_{row_index}_{col_index}", 0.12, 0.025, height,
                location=(x + jitter_x, y + jitter_y, 0.18 + height * 0.5), vertices=6))
            if stage >= 2:
                groups["fibre"].append(lib_mesh.sphere(
                    f"grain_{row_index}_{col_index}", 0.085 if stage == 2 else 0.12,
                    location=(x + jitter_x, y + jitter_y, 0.20 + height), segments=8, rings=5))


def _tools(groups):
    groups["wood"].append(lib_mesh.strut(
        "hoe_handle", (-2.78, -1.75, 0.10), (-2.70, -1.75, 1.30), width=0.045, bevel=0.004))
    blade = lib_mesh.box("hoe_blade", (0.34, 0.07, 0.10),
                         location=(-2.69, -1.75, 1.25), bevel=0.008, segments=1)
    blade.rotation_euler.y = math.radians(12)
    groups["stone" if "stone" in groups else "clay"].append(blade)
    groups["team"].append(lib_mesh.box(
        "farm_marker", (0.72, 0.055, 0.42), location=(0.0, -2.70, 0.72), bevel=0.025, segments=1))


def _harvest_details(groups, rng):
    for index, x in enumerate((-1.0, 0.0, 1.0)):
        basket = lib_mesh.cylinder(
            f"harvest_basket_{index}", 0.28, 0.34,
            location=(x, -2.62, 0.27), vertices=10, bevel=0.018)
        groups["fibre"].append(basket)
        for berry in range(4):
            groups["food"].append(lib_mesh.sphere(
                f"basket_food_{index}_{berry}", 0.095,
                location=(x + rng.uniform(-0.14, 0.14), -2.62 + rng.uniform(-0.12, 0.12), 0.48),
                segments=8, rings=5))


def _finish(groups):
    materials = {
        "soil": lambda: lib_material.pbr("farm_soil", (0.20, 0.105, 0.045), roughness=0.98),
        "clay": lib_material.clay,
        "wood": lib_material.wood,
        "crop": lib_material.foliage,
        "fibre": lib_material.fibre,
        "food": lib_material.food,
        "team": lib_material.team_color,
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
