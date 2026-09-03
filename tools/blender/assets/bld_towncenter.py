"""Town centre — the 4x4 tile starting building of the Stone Age.

A large hide-covered roundhouse rather than a hut with clay walls and a gable
roof: at this point in the age chain there is no masonry and no carpentry, only
gathered stone, lashed poles and cured hide. The silhouette has to say "Stone
Age" from RTS camera distance, which is why the shape is a cone of leaning poles
and not a box.

Every detail is placed from a seeded RNG, so the building looks hand-built
rather than stamped out — and still rebuilds identically every time.
"""

from __future__ import annotations

import math
import random

import lib_material
import lib_mesh
import lib_scene

NAME = "bld_towncenter"
CATEGORY = "buildings"

# The simulation says 4x4 tiles at 2 m per tile. The model must not guess.
TILE_SIZE = 2.0
FOOTPRINT_TILES = 4
FOOTPRINT = TILE_SIZE * FOOTPRINT_TILES

HUT_RADIUS = 3.4
HUT_HEIGHT = 5.9

# The entrance faces -Y, which is the model's front and becomes Godot's forward.
ENTRANCE_ANGLE = -math.pi / 2
ENTRANCE_WIDTH = 0.62  # radians of the ring left open

SEED = 20260903


def build():
    lib_scene.reset()
    rng = random.Random(SEED)

    stone = []
    timber = []
    hide = []
    team = []

    _stone_ring(stone, rng)
    _roof_frame(timber, rng)
    _hide_covering(hide, rng)
    _entrance(timber, hide)
    _fire_pit(stone, timber, rng)
    _drying_rack(timber, hide, rng)
    _totem(timber, team, rng)

    groups = (
        (stone, "stone", lib_material.stone()),
        (timber, "timber", lib_material.wood()),
        (hide, "hide", lib_material.hide()),
        (team, "banner", lib_material.team_color()),
    )

    objects = []
    for parts, name, material in groups:
        merged = lib_mesh.join(parts, name)
        lib_material.assign(merged, material)
        objects.append(merged)

    # Ground the whole building at once — see lib_mesh.ground_assembly.
    lib_mesh.ground_assembly(objects)
    return objects


def _is_entrance(angle: float) -> bool:
    """True inside the gap left open for the doorway."""
    delta = abs((angle - ENTRANCE_ANGLE + math.pi) % math.tau - math.pi)
    return delta < ENTRANCE_WIDTH


def _stone_ring(parts, rng) -> None:
    """Gathered boulders weighing down the base of the hide covering."""
    count = 26
    for i in range(count):
        angle = math.tau * i / count

        # Leave the doorway clear, and skip the odd stone so the ring reads as
        # collected rather than laid.
        if _is_entrance(angle) or rng.random() < 0.12:
            continue

        radius = HUT_RADIUS + rng.uniform(-0.12, 0.22)
        size = rng.uniform(0.30, 0.52)

        parts.append(lib_mesh.boulder(
            f"base_stone_{i}", size,
            (math.cos(angle) * radius, math.sin(angle) * radius, size * 0.35),
            rng))


def _roof_frame(parts, rng) -> None:
    """Poles leaning inward to a lashed apex, plus two binding rings."""
    count = 16
    for i in range(count):
        angle = math.tau * i / count
        if _is_entrance(angle):
            continue

        lean = rng.uniform(-0.06, 0.06)
        # The poles cross over at the top and stick out past the apex.
        head = (0.0, 0.0, HUT_HEIGHT + rng.uniform(0.0, 0.45))

        width = rng.uniform(0.13, 0.18)
        parts.append(lib_mesh.radial_panel(
            f"pole_{i}", angle + lean,
            base_radius=HUT_RADIUS + 0.1, base_z=0.0,
            top_radius=0.28, top_z=head[2],
            width=width, thickness=width, bevel=0.02, segments=1))

    # Horizontal binding rings, approximated by short chords between the poles.
    for ring_index, (height, radius) in enumerate(((1.5, 2.62), (3.3, 1.72))):
        segments = 14
        for i in range(segments):
            a0 = math.tau * i / segments
            a1 = math.tau * (i + 1) / segments
            if _is_entrance(a0) or _is_entrance(a1):
                continue

            parts.append(lib_mesh.strut(
                f"ring_{ring_index}_{i}",
                (math.cos(a0) * radius, math.sin(a0) * radius, height),
                (math.cos(a1) * radius, math.sin(a1) * radius, height),
                width=0.09))


def _hide_covering(parts, rng) -> None:
    """Cured hides laid over the frame as overlapping panels."""
    count = 22
    base_radius = HUT_RADIUS - 0.08

    # Wider than the spacing between panels, so they overlap like sewn hides
    # rather than leaving the frame visible between them.
    overlap = 1.7

    # Two layers, the second offset by half a step. A single ring of straight
    # panels always leaves wedges open towards the apex, because the gap between
    # panels widens as their width stays constant and the radius shrinks.
    for layer, (phase, z_base, z_top) in enumerate((
        (0.5, 0.16, 0.35),
        (1.0, 1.30, 0.30),
    )):
        for i in range(count):
            angle = math.tau * (i + phase) / count
            if _is_entrance(angle):
                continue

            # Each panel reaches a slightly different height, which is what makes
            # a covering look stitched together instead of moulded.
            top = HUT_HEIGHT - rng.uniform(z_top, z_top + 0.5)
            top_radius = base_radius * (1.0 - top / HUT_HEIGHT) + 0.16
            start_radius = base_radius * (1.0 - z_base / HUT_HEIGHT)

            parts.append(lib_mesh.radial_panel(
                f"hide_{layer}_{i}", angle,
                base_radius=start_radius, base_z=z_base,
                top_radius=top_radius, top_z=top,
                width=math.tau * start_radius / count * overlap,
                thickness=0.07, bevel=0.0))

    # A cap over the point where the poles cross, so the apex is not an open hole.
    cap = lib_mesh.cone("hide_cap", radius_bottom=0.62, radius_top=0.0, height=0.9,
                        location=(0.0, 0.0, HUT_HEIGHT - 0.25), vertices=10)
    parts.append(cap)


def _entrance(timber, hide) -> None:
    """Two heavy posts, a lintel and a hide flap hanging in the opening."""
    half = ENTRANCE_WIDTH * 0.92
    door_radius = HUT_RADIUS - 0.15
    height = 2.35

    for side, angle in (("l", ENTRANCE_ANGLE - half), ("r", ENTRANCE_ANGLE + half)):
        x = math.cos(angle) * door_radius
        y = math.sin(angle) * door_radius
        timber.append(lib_mesh.strut(f"door_post_{side}", (x, y, 0.0), (x, y, height), width=0.26))

    left = (math.cos(ENTRANCE_ANGLE - half) * door_radius,
            math.sin(ENTRANCE_ANGLE - half) * door_radius, height)
    right = (math.cos(ENTRANCE_ANGLE + half) * door_radius,
             math.sin(ENTRANCE_ANGLE + half) * door_radius, height)

    timber.append(lib_mesh.strut("door_lintel", left, right, width=0.22))

    # The flap hangs from the lintel and covers the upper half of the doorway.
    flap = lib_mesh.box("door_flap", (1.55, 0.06, 1.05),
                        location=(0.0, -door_radius - 0.05, height - 0.6), bevel=0.01)
    hide.append(flap)


def _fire_pit(stone, timber, rng) -> None:
    """A ring of stones with burnt logs, just outside the entrance."""
    centre = (0.0, -(HUT_RADIUS + 2.4))
    count = 11

    for i in range(count):
        angle = math.tau * i / count + rng.uniform(-0.12, 0.12)
        radius = 0.95 + rng.uniform(-0.08, 0.08)
        size = rng.uniform(0.20, 0.30)

        stone.append(lib_mesh.boulder(
            f"pit_stone_{i}", size,
            (centre[0] + math.cos(angle) * radius,
             centre[1] + math.sin(angle) * radius,
             size * 0.3),
            rng))

    # Logs leaning inward into a cone, the way a fire is actually laid.
    for i in range(6):
        angle = math.tau * i / 6 + rng.uniform(-0.15, 0.15)
        foot = 0.62
        timber.append(lib_mesh.strut(
            f"pit_log_{i}",
            (centre[0] + math.cos(angle) * foot, centre[1] + math.sin(angle) * foot, 0.05),
            (centre[0] + math.cos(angle) * 0.1, centre[1] + math.sin(angle) * 0.1, 0.85),
            width=rng.uniform(0.10, 0.14)))


def _drying_rack(timber, hide, rng) -> None:
    """A frame beside the hut with hides hung out to cure."""
    base_x = HUT_RADIUS + 1.6
    base_y = -0.8
    height = 2.0
    span = 2.3

    for side, offset in (("a", -span * 0.5), ("b", span * 0.5)):
        timber.append(lib_mesh.strut(
            f"rack_post_{side}",
            (base_x, base_y + offset, 0.0),
            (base_x + rng.uniform(-0.08, 0.08), base_y + offset, height),
            width=0.16))

    timber.append(lib_mesh.strut(
        "rack_beam",
        (base_x, base_y - span * 0.5, height),
        (base_x, base_y + span * 0.5, height),
        width=0.12))

    for i in range(3):
        offset = -span * 0.32 + i * span * 0.32
        drop = rng.uniform(0.9, 1.25)
        hide.append(lib_mesh.box(
            f"rack_hide_{i}", (0.05, 0.62, drop),
            location=(base_x, base_y + offset, height - drop * 0.5 - 0.08),
            bevel=0.01))


def _totem(timber, team, rng) -> None:
    """A carved pole carrying the clan's colours — the model's team-colour part."""
    base_x = -(HUT_RADIUS + 1.5)
    base_y = -1.4
    height = 4.2

    timber.append(lib_mesh.strut(
        "totem_pole", (base_x, base_y, 0.0), (base_x, base_y, height), width=0.24))

    # Crossbar with the banner hanging from it.
    timber.append(lib_mesh.strut(
        "totem_arm",
        (base_x - 0.7, base_y, height - 0.35),
        (base_x + 0.7, base_y, height - 0.35),
        width=0.13))

    team.append(lib_mesh.box(
        "totem_banner", (1.25, 0.06, 1.7),
        location=(base_x, base_y, height - 1.25), bevel=0.01))

    # Two carved rings further down, so the pole is not a bare stick.
    for i, z in enumerate((1.1, 2.0)):
        team.append(lib_mesh.box(
            f"totem_band_{i}", (0.34, 0.34, 0.18),
            location=(base_x, base_y, z), bevel=0.03))


def export():
    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)
    triangles = sum(lib_mesh.triangle_count(obj) for obj in objects)
    print(f"[asset] {NAME}: {triangles} triangles, footprint {FOOTPRINT:.0f} m")
    lib_scene.report(path)
    return path
