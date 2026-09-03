"""Stone Age house with three visible construction stages and rubble.

The finished building is a compact oval roundhouse: a stone footing, lashed
timber frame, ochre hide walls and a deep thatched roof.  Construction variants
reuse the same seeded layout, so posts and stones never jump when the simulation
advances to the next stage.
"""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh

NAME = "bld_house"
CATEGORY = "buildings"
SEED = 20260903

TILE_SIZE = 2.0
FOOTPRINT_TILES = 2
FOOTPRINT = TILE_SIZE * FOOTPRINT_TILES

WALL_RADIUS = 1.48
WALL_HEIGHT = 1.82
ROOF_HEIGHT = 3.05
DOOR_ANGLE = -math.pi / 2
DOOR_HALF_WIDTH = 0.34


def build(stage: int = 3):
    """Build stage 0..3, where 3 is the finished house used by previews."""
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED)

    groups = _groups()
    _foundation(groups, rng)

    if stage >= 1:
        _frame(groups, rng)
        if stage < 3:
            _material_piles(groups, rng)

    if stage >= 2:
        _walls(groups, partial=stage == 2)
        _roof(groups, rng, partial=stage == 2)
        _doorway(groups)

    if stage >= 3:
        _details(groups, rng)

    return _finish(groups)


def build_rubble():
    """Collapsed stones, snapped poles and remnants of clan cloth."""
    import lib_scene
    lib_scene.reset()
    rng = random.Random(SEED + 99)
    groups = _groups()

    for index in range(15):
        angle = rng.uniform(0.0, math.tau)
        radius = rng.uniform(0.25, 1.65)
        size = rng.uniform(0.16, 0.32)
        groups["stone"].append(lib_mesh.boulder(
            f"rubble_stone_{index}", size,
            (math.cos(angle) * radius, math.sin(angle) * radius, size * 0.28), rng))

    for index in range(8):
        angle = rng.uniform(0.0, math.tau)
        centre = (math.cos(angle) * rng.uniform(0.2, 1.25),
                  math.sin(angle) * rng.uniform(0.2, 1.25),
                  rng.uniform(0.10, 0.22))
        length = rng.uniform(0.75, 1.55)
        direction = (math.cos(angle) * length * 0.5,
                     math.sin(angle) * length * 0.5, rng.uniform(-0.04, 0.10))
        groups["wood"].append(lib_mesh.strut(
            f"broken_beam_{index}",
            (centre[0] - direction[0], centre[1] - direction[1], max(0.05, centre[2] - direction[2])),
            (centre[0] + direction[0], centre[1] + direction[1], max(0.05, centre[2] + direction[2])),
            width=rng.uniform(0.08, 0.13), bevel=0.012))

    for index in range(5):
        panel = lib_mesh.box(
            f"thatch_scrap_{index}",
            (rng.uniform(0.38, 0.75), rng.uniform(0.22, 0.48), 0.045),
            location=(rng.uniform(-1.25, 1.25), rng.uniform(-1.25, 1.25), 0.07),
            bevel=0.0)
        panel.rotation_euler.z = rng.uniform(0.0, math.tau)
        groups["thatch"].append(panel)

    torn_mark = lib_mesh.box("torn_clan_cloth", (0.48, 0.32, 0.035),
                             location=(0.45, -0.60, 0.10), bevel=0.0)
    torn_mark.rotation_euler.z = -0.32
    groups["team"].append(torn_mark)

    return _finish(groups)


def _groups():
    return {
        "stone": [],
        "clay": [],
        "wood": [],
        "ochre": [],
        "hide": [],
        "thatch": [],
        "team": [],
        "bone": [],
    }


def _inside_door(angle: float) -> bool:
    delta = abs((angle - DOOR_ANGLE + math.pi) % math.tau - math.pi)
    return delta < DOOR_HALF_WIDTH


def _foundation(groups, rng) -> None:
    groups["clay"].append(lib_mesh.cylinder(
        "packed_earth_floor", WALL_RADIUS * 0.94, 0.12,
        location=(0.0, 0.0, 0.06), vertices=18, bevel=0.015))

    for index in range(20):
        angle = math.tau * index / 20
        if _inside_door(angle):
            continue
        size = rng.uniform(0.19, 0.31)
        radius = WALL_RADIUS + rng.uniform(-0.04, 0.08)
        groups["stone"].append(lib_mesh.boulder(
            f"foundation_stone_{index}", size,
            (math.cos(angle) * radius, math.sin(angle) * radius, size * 0.32), rng))


def _frame(groups, rng) -> None:
    post_count = 12
    for index in range(post_count):
        angle = math.tau * index / post_count
        if _inside_door(angle):
            continue

        lean = rng.uniform(-0.025, 0.025)
        groups["wood"].append(lib_mesh.strut(
            f"wall_post_{index}",
            (math.cos(angle) * WALL_RADIUS, math.sin(angle) * WALL_RADIUS, 0.12),
            (math.cos(angle + lean) * (WALL_RADIUS - 0.08),
             math.sin(angle + lean) * (WALL_RADIUS - 0.08), WALL_HEIGHT + rng.uniform(-0.05, 0.08)),
            width=rng.uniform(0.105, 0.135), bevel=0.014))

        groups["wood"].append(lib_mesh.strut(
            f"roof_rib_{index}",
            (math.cos(angle) * (WALL_RADIUS + 0.06),
             math.sin(angle) * (WALL_RADIUS + 0.06), WALL_HEIGHT - 0.05),
            (math.cos(angle) * 0.16, math.sin(angle) * 0.16,
             ROOF_HEIGHT + rng.uniform(-0.04, 0.08)),
            width=rng.uniform(0.075, 0.095), bevel=0.010))

    for ring_index, (height, radius) in enumerate(((0.62, 1.43), (1.28, 1.40), (1.80, 1.36))):
        for index in range(post_count):
            a0 = math.tau * index / post_count
            a1 = math.tau * (index + 1) / post_count
            if _inside_door(a0) or _inside_door(a1):
                continue
            groups["wood"].append(lib_mesh.strut(
                f"wall_ring_{ring_index}_{index}",
                (math.cos(a0) * radius, math.sin(a0) * radius, height),
                (math.cos(a1) * radius, math.sin(a1) * radius, height),
                width=0.055, bevel=0.006))


def _material_piles(groups, rng) -> None:
    """Loose supplies make the unfinished stages read as an active building site."""
    for index in range(5):
        x = 1.72 + rng.uniform(-0.18, 0.18)
        y = -0.85 + index * 0.30 + rng.uniform(-0.08, 0.08)
        groups["wood"].append(lib_mesh.strut(
            f"spare_pole_{index}", (x - 0.45, y, 0.08), (x + 0.45, y + 0.04, 0.10),
            width=0.07, bevel=0.008))

    for index in range(4):
        bundle = lib_mesh.box(
            f"hide_bundle_{index}", (0.48, 0.30, 0.07),
            location=(-1.55 + index * 0.13, 0.78 + index * 0.06, 0.10 + index * 0.055),
            bevel=0.015, segments=1)
        bundle.rotation_euler.z = -0.20 + index * 0.09
        groups["hide"].append(bundle)


def _walls(groups, partial: bool) -> None:
    count = 16
    for index in range(count):
        angle = math.tau * (index + 0.5) / count
        if _inside_door(angle):
            continue
        if partial and index % 3 == 1:
            continue

        material = "hide" if index in (2, 7, 12) else "ochre"
        groups[material].append(lib_mesh.radial_panel(
            f"wall_hide_{index}", angle,
            base_radius=WALL_RADIUS - 0.045, base_z=0.16,
            top_radius=WALL_RADIUS - 0.10, top_z=WALL_HEIGHT,
            width=math.tau * WALL_RADIUS / count * 1.18,
            thickness=0.060, bevel=0.008, segments=1))


def _roof(groups, rng, partial: bool) -> None:
    count = 20
    for index in range(count):
        if partial and index % 3 == 1:
            continue
        angle = math.tau * (index + 0.5) / count
        top = ROOF_HEIGHT + rng.uniform(-0.035, 0.05)
        groups["thatch"].append(lib_mesh.radial_panel(
            f"roof_thatch_{index}", angle,
            base_radius=WALL_RADIUS + 0.22, base_z=WALL_HEIGHT - 0.12,
            top_radius=0.13, top_z=top,
            width=math.tau * (WALL_RADIUS + 0.22) / count * 1.30,
            thickness=0.055, bevel=0.006, segments=1))

    cap = lib_mesh.cone("roof_cap", 0.30, 0.07, 0.52,
                        location=(0.0, 0.0, ROOF_HEIGHT - 0.02), vertices=10)
    groups["thatch"].append(cap)


def _doorway(groups) -> None:
    half = 0.46
    y = -WALL_RADIUS
    for side, x in (("left", -half), ("right", half)):
        groups["wood"].append(lib_mesh.strut(
            f"door_post_{side}", (x, y, 0.08), (x, y, 1.55),
            width=0.13, bevel=0.014))
    groups["wood"].append(lib_mesh.strut(
        "door_lintel", (-half, y, 1.55), (half, y, 1.55),
        width=0.13, bevel=0.014))

    flap = lib_mesh.box("door_hide", (0.72, 0.055, 1.22),
                        location=(0.0, y - 0.035, 0.88), bevel=0.015, segments=1)
    groups["hide"].append(flap)

    clan_band = lib_mesh.box("door_clan_band", (0.72, 0.070, 0.18),
                             location=(0.0, y - 0.070, 1.22), bevel=0.008, segments=1)
    groups["team"].append(clan_band)


def _details(groups, rng) -> None:
    # Crossed roof tips and a bone charm make the front readable at RTS scale.
    for index, angle in enumerate((-0.28, 0.28)):
        tip = lib_mesh.strut(
            f"roof_tip_{index}",
            (math.sin(angle) * 0.12, -0.06, ROOF_HEIGHT - 0.02),
            (math.sin(angle) * 0.34, 0.03, ROOF_HEIGHT + 0.48),
            width=0.065, bevel=0.008)
        groups["wood"].append(tip)

    for side, x in (("left", -0.22), ("right", 0.22)):
        charm = lib_mesh.cone(f"door_tooth_{side}", 0.035, 0.0, 0.14,
                              location=(x, -WALL_RADIUS - 0.10, 1.42), vertices=6)
        charm.rotation_euler.x = math.pi
        groups["bone"].append(charm)

    # A low drying rail and two hide pieces make the inhabited house distinct
    # from the larger town centre without relying on scale alone.
    rack_x = -1.72
    for side, y in (("a", 0.52), ("b", -0.52)):
        groups["wood"].append(lib_mesh.strut(
            f"rack_post_{side}", (rack_x, y, 0.05), (rack_x, y, 1.05),
            width=0.075, bevel=0.008))
    groups["wood"].append(lib_mesh.strut(
        "rack_bar", (rack_x, -0.52, 1.03), (rack_x, 0.52, 1.03),
        width=0.065, bevel=0.008))

    for index, y in enumerate((-0.25, 0.20)):
        hide = lib_mesh.box(f"rack_hide_{index}", (0.045, 0.32, 0.54),
                            location=(rack_x, y, 0.72), bevel=0.008, segments=1)
        hide.rotation_euler.x = rng.uniform(-0.04, 0.04)
        groups["ochre"].append(hide)


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
