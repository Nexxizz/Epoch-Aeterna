"""Town centre — the 4x4 tile starting building.

Validates the parts of the pipeline a building needs: a footprint that matches
the simulation exactly, several materials on one asset, and the team-colour
material the game recolours per player.
"""

from __future__ import annotations

import lib_material
import lib_mesh
import lib_scene

NAME = "bld_towncenter"
CATEGORY = "buildings"

# The simulation says 4x4 tiles at 2 m per tile. The model must not guess.
TILE_SIZE = 2.0
FOOTPRINT_TILES = 4
FOOTPRINT = TILE_SIZE * FOOTPRINT_TILES

# Slightly smaller than the footprint so neighbouring buildings do not touch.
BODY = FOOTPRINT * 0.86


def build():
    lib_scene.reset()

    parts_stone = []
    parts_clay = []
    parts_wood = []
    parts_thatch = []
    parts_team = []

    # Stone plinth the whole structure sits on.
    parts_stone.append(lib_mesh.box("plinth", (FOOTPRINT * 0.94, FOOTPRINT * 0.94, 0.45),
                                    location=(0.0, 0.0, 0.22), bevel=0.06))

    # Clay walls.
    parts_clay.append(lib_mesh.box("walls", (BODY, BODY, 3.2),
                                   location=(0.0, 0.0, 2.05), bevel=0.08))

    # Corner posts, which break up the flat walls and catch the light.
    half = BODY * 0.5
    for index, (x, y) in enumerate(((half, half), (-half, half), (half, -half), (-half, -half))):
        parts_wood.append(lib_mesh.box(f"post_{index}", (0.35, 0.35, 3.6),
                                       location=(x, y, 2.0), bevel=0.04))

    # Thatched gable roof. Overhangs the walls, as a roof should.
    roof = lib_mesh.wedge_roof("roof", BODY * 1.12, BODY * 1.12, 2.2,
                               location=(0.0, 0.0, 4.75))
    parts_thatch.append(roof)

    # Doorway frame on the -Y side, which is the model's front.
    parts_wood.append(lib_mesh.box("door_frame", (1.5, 0.25, 2.2),
                                   location=(0.0, -half - 0.05, 1.55), bevel=0.03))

    # Banner above the door — this is what carries the player colour.
    parts_team.append(lib_mesh.box("banner", (1.1, 0.12, 1.5),
                                   location=(0.0, -half - 0.2, 3.4), bevel=0.02))

    groups = (
        (parts_stone, "stone", lib_material.stone()),
        (parts_clay, "walls", lib_material.clay()),
        (parts_wood, "timber", lib_material.wood()),
        (parts_thatch, "roof", lib_material.thatch()),
        (parts_team, "banner", lib_material.team_color()),
    )

    objects = []
    for parts, name, material in groups:
        merged = lib_mesh.join(parts, name)
        lib_material.assign(merged, material)
        objects.append(merged)

    # Ground the whole building at once — see lib_mesh.ground_assembly.
    lib_mesh.ground_assembly(objects)
    return objects


def export():
    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)
    triangles = sum(lib_mesh.triangle_count(obj) for obj in objects)
    print(f"[asset] {NAME}: {triangles} triangles, footprint {FOOTPRINT:.0f} m")
    lib_scene.report(path)
    return path
