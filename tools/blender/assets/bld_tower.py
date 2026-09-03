"""Tall Stone Age watchtower, three construction stages and rubble."""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_tower"
CATEGORY = "buildings"
SEED = 20260908


def build(stage: int = 3):
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)
    groups = _groups()
    _foundation(groups, rng)
    if stage >= 1:
        _main_frame(groups)
        _ladder(groups)
        if stage < 3:
            _supplies(groups)
    if stage >= 2:
        _platform(groups, partial=stage == 2)
        _roof_frame(groups)
    if stage >= 3:
        _roof(groups)
        _lookout_details(groups, rng)
    return _finish(groups)


def build_rubble():
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 109)
    groups = _groups()
    for index in range(28):
        a = rng.uniform(0.0, math.tau)
        radius = rng.uniform(0.15, 2.0)
        size = rng.uniform(0.12, 0.29)
        groups["stone"].append(lib_mesh.boulder(
            f"rubble_stone_{index}", size,
            (math.cos(a) * radius, math.sin(a) * radius, size * 0.28), rng))
    for index in range(20):
        x, y = rng.uniform(-1.8, 1.8), rng.uniform(-1.8, 1.8)
        angle = rng.uniform(0, math.tau)
        length = rng.uniform(0.55, 1.65)
        groups["wood"].append(lib_mesh.strut(
            f"fallen_beam_{index}", (x, y, 0.08),
            (x + math.cos(angle) * length, y + math.sin(angle) * length, 0.17),
            width=rng.uniform(0.07, 0.13), bevel=0.007))
    groups["thatch"].append(lib_mesh.box(
        "collapsed_roof", (1.55, 1.05, 0.12), location=(-0.30, 0.45, 0.15),
        bevel=0.02, segments=1))
    groups["team"].append(lib_mesh.box(
        "fallen_shield", (0.58, 0.06, 0.58), location=(0.62, -0.50, 0.14),
        bevel=0.08, segments=2))
    return _finish(groups)


def _groups():
    return {name: [] for name in (
        "clay", "stone", "wood", "bark", "thatch", "hide", "ochre",
        "team", "bone", "fibre", "dark")}


def _foundation(groups, rng):
    groups["clay"].append(lib_mesh.box(
        "tower_ground", (3.75, 3.75, 0.16), location=(0, 0, 0.08),
        bevel=0.16, segments=1))

    # Four broad stone footings communicate the future load-bearing structure.
    for post_index, (x, y) in enumerate(((-1.25, -1.25), (1.25, -1.25),
                                         (-1.25, 1.25), (1.25, 1.25))):
        for stone_index in range(7):
            a = stone_index / 7.0 * math.tau
            radius = 0.27 + (stone_index % 2) * 0.06
            size = rng.uniform(0.20, 0.31)
            groups["stone"].append(lib_mesh.boulder(
                f"footing_{post_index}_{stone_index}", size,
                (x + math.cos(a) * radius, y + math.sin(a) * radius,
                 size * 0.33), rng, flatten=0.72))

    for index in range(20):
        side = index % 4
        t = (index // 4) / 4.0 * 3.2 - 1.6
        x, y = ((t, -1.82), (1.82, t), (-t, 1.82), (-1.82, -t))[side]
        size = rng.uniform(0.11, 0.20)
        groups["stone"].append(lib_mesh.boulder(
            f"foundation_curb_{index}", size, (x, y, size * 0.28), rng))


def _main_frame(groups):
    # The legs flare outward for a stable, unmistakably hand-built silhouette.
    legs = (
        ((-1.38, -1.38, 0.16), (-1.02, -1.02, 4.42)),
        ((1.38, -1.38, 0.16), (1.02, -1.02, 4.42)),
        ((-1.38, 1.38, 0.16), (-1.02, 1.02, 4.42)),
        ((1.38, 1.38, 0.16), (1.02, 1.02, 4.42)),
    )
    for index, (start, end) in enumerate(legs):
        groups["bark"].append(lib_mesh.strut(
            f"tower_leg_{index}", start, end, width=0.23, bevel=0.018))

    # Cross braces on all four sides stop the tower looking like four loose poles.
    braces = (
        ((-1.30, -1.34, 0.55), (0.97, -1.05, 3.76)),
        ((1.30, -1.34, 0.55), (-0.97, -1.05, 3.76)),
        ((-1.30, 1.34, 0.55), (0.97, 1.05, 3.76)),
        ((1.30, 1.34, 0.55), (-0.97, 1.05, 3.76)),
        ((-1.34, -1.30, 0.55), (-1.05, 0.97, 3.76)),
        ((-1.34, 1.30, 0.55), (-1.05, -0.97, 3.76)),
        ((1.34, -1.30, 0.55), (1.05, 0.97, 3.76)),
        ((1.34, 1.30, 0.55), (1.05, -0.97, 3.76)),
    )
    for index, (start, end) in enumerate(braces):
        groups["wood"].append(lib_mesh.strut(
            f"cross_brace_{index}", start, end, width=0.105, bevel=0.009))

    for index, (start, end) in enumerate((
            ((-1.18, -1.18, 2.15), (1.18, -1.18, 2.15)),
            ((-1.18, 1.18, 2.15), (1.18, 1.18, 2.15)),
            ((-1.18, -1.18, 2.15), (-1.18, 1.18, 2.15)),
            ((1.18, -1.18, 2.15), (1.18, 1.18, 2.15)),
    )):
        groups["wood"].append(lib_mesh.strut(
            f"middle_belt_{index}", start, end, width=0.115, bevel=0.010))


def _ladder(groups):
    # Front is -Y. The ladder leans from the entrance onto the platform.
    groups["wood"].append(lib_mesh.strut(
        "ladder_side_left", (-0.48, -1.72, 0.12), (-0.48, -1.12, 4.25),
        width=0.085, bevel=0.007))
    groups["wood"].append(lib_mesh.strut(
        "ladder_side_right", (0.48, -1.72, 0.12), (0.48, -1.12, 4.25),
        width=0.085, bevel=0.007))
    for index in range(12):
        z = 0.42 + index * 0.32
        y = -1.68 + index * 0.046
        groups["wood"].append(lib_mesh.strut(
            f"ladder_rung_{index}", (-0.52, y, z), (0.52, y, z),
            width=0.065, bevel=0.006))


def _supplies(groups):
    for index in range(6):
        groups["wood"].append(lib_mesh.strut(
            f"spare_timber_{index}", (-1.70, -0.72 + index * 0.23, 0.09),
            (-0.55, -0.68 + index * 0.23, 0.13), width=0.065, bevel=0.006))
    for index in range(4):
        groups["fibre"].append(lib_mesh.box(
            f"rope_bundle_{index}", (0.42, 0.28, 0.10),
            location=(1.55 - index * 0.08, 0.72, 0.10 + index * 0.08),
            bevel=0.025, segments=2))


def _platform(groups, partial: bool):
    # Thick beams and individually readable deck planks.
    for y in (-1.16, 0.0, 1.16):
        groups["wood"].append(lib_mesh.strut(
            f"platform_beam_{y}", (-1.46, y, 4.08), (1.46, y, 4.08),
            width=0.15, bevel=0.012))
    for index, x in enumerate((-1.32, -1.08, -0.84, -0.60, -0.36, -0.12,
                               0.12, 0.36, 0.60, 0.84, 1.08, 1.32)):
        if partial and index % 3 == 1:
            continue
        groups["wood"].append(lib_mesh.box(
            f"deck_plank_{index}", (0.22, 2.82, 0.11),
            location=(x, 0, 4.20), bevel=0.012, segments=1))

    # Waist-high hide parapets leave firing gaps at every corner.
    panels = (
        ("front_l", (-0.88, -1.34, 4.67), (0.72, 0.10, 0.70)),
        ("front_r", (0.88, -1.34, 4.67), (0.72, 0.10, 0.70)),
        ("back", (0.0, 1.34, 4.67), (2.48, 0.10, 0.70)),
        ("left", (-1.34, 0.0, 4.67), (0.10, 2.48, 0.70)),
        ("right", (1.34, 0.0, 4.67), (0.10, 2.48, 0.70)),
    )
    for index, (name, location, size) in enumerate(panels):
        if partial and index % 2 == 1:
            continue
        groups["hide"].append(lib_mesh.box(
            f"parapet_{name}", size, location=location, bevel=0.035, segments=1))

    for index, (x, y) in enumerate(((-1.36, -1.36), (1.36, -1.36),
                                     (-1.36, 1.36), (1.36, 1.36))):
        groups["wood"].append(lib_mesh.strut(
            f"rail_post_{index}", (x, y, 4.18), (x, y, 5.18),
            width=0.10, bevel=0.009))


def _roof_frame(groups):
    groups["wood"].append(lib_mesh.strut(
        "roof_ridge", (-1.72, 0, 6.12), (1.72, 0, 6.12),
        width=0.12, bevel=0.010))
    for index, x in enumerate((-1.46, -0.72, 0.0, 0.72, 1.46)):
        groups["wood"].append(lib_mesh.strut(
            f"rafter_front_{index}", (x, 0, 6.10), (x, -1.66, 5.15),
            width=0.085, bevel=0.007))
        groups["wood"].append(lib_mesh.strut(
            f"rafter_back_{index}", (x, 0, 6.10), (x, 1.66, 5.15),
            width=0.085, bevel=0.007))


def _roof(groups):
    slope = math.radians(30)
    for side, y, rotation in (("front", -0.82, slope), ("back", 0.82, -slope)):
        for index, x in enumerate((-1.52, -1.14, -0.76, -0.38, 0.0,
                                   0.38, 0.76, 1.14, 1.52)):
            panel = lib_mesh.box(
                f"roof_{side}_{index}", (0.41, 1.92, 0.10),
                location=(x, y, 5.62), bevel=0.014, segments=1)
            panel.rotation_euler.x = rotation
            groups["thatch"].append(panel)

    # Blue ridge bindings make ownership legible without painting the whole tower.
    for x in (-1.05, 0.0, 1.05):
        groups["team"].append(lib_mesh.box(
            f"ridge_binding_{x}", (0.16, 0.34, 0.14),
            location=(x, 0, 6.10), bevel=0.025, segments=2))


def _lookout_details(groups, rng):
    # Large front shield, visible from the normal gameplay camera.
    shield = lib_mesh.cylinder(
        "tribal_shield", 0.47, 0.10, location=(0, -1.43, 4.78),
        vertices=16, bevel=0.025)
    shield.rotation_euler.x = math.pi / 2
    groups["team"].append(shield)
    boss = lib_mesh.cylinder(
        "shield_boss", 0.17, 0.13, location=(0, -1.51, 4.78),
        vertices=12, bevel=0.018)
    boss.rotation_euler.x = math.pi / 2
    groups["bone"].append(boss)

    # Arrow rack on the platform and a supply basket below it.
    for index, x in enumerate((0.73, 0.88, 1.03, 1.18)):
        groups["dark"].append(lib_mesh.strut(
            f"tower_arrow_{index}", (x, 0.88, 4.30),
            (x - 0.05, 0.88, 5.38), width=0.022, bevel=0.002))
        groups["bone"].append(lib_mesh.cone(
            f"tower_arrow_tip_{index}", 0.043, 0.0, 0.14,
            location=(x - 0.056, 0.88, 5.44), vertices=6))
    groups["fibre"].append(lib_mesh.cylinder(
        "arrow_basket", 0.27, 0.46, location=(0.97, 0.88, 4.43),
        vertices=12, bevel=0.018))

    # Warning horn and dangling bone charms add life at close zoom.
    groups["ochre"].append(lib_mesh.cone(
        "warning_horn", 0.18, 0.055, 0.72,
        location=(-0.84, -1.30, 5.00), vertices=10))
    groups["ochre"][-1].rotation_euler.y = math.radians(72)
    for index, x in enumerate((-0.80, -0.56, 0.56, 0.80)):
        groups["fibre"].append(lib_mesh.strut(
            f"charm_string_{index}", (x, -1.40, 4.34),
            (x, -1.40, 4.12 - (index % 2) * 0.08), width=0.012, bevel=0.001))
        groups["bone"].append(lib_mesh.cone(
            f"charm_tooth_{index}", 0.045, 0.0, 0.16,
            location=(x, -1.40, 4.04 - (index % 2) * 0.08), vertices=6))

    # Ground-level sling-stone cache visually connects the tower to ranged defence.
    groups["fibre"].append(lib_mesh.cylinder(
        "stone_cache", 0.35, 0.38, location=(1.40, -1.18, 0.28),
        vertices=12, bevel=0.018))
    for index in range(7):
        groups["stone"].append(lib_mesh.boulder(
            f"cache_stone_{index}", 0.10,
            (1.40 + rng.uniform(-0.20, 0.20), -1.18 + rng.uniform(-0.18, 0.18),
             0.48 + rng.uniform(0.0, 0.11)), rng, flatten=0.8))


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
        "dark": lambda: lib_material.pbr("tower_arrow_wood", (0.11, 0.05, 0.02), roughness=0.90),
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
