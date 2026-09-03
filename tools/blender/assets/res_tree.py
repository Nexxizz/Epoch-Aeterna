"""Tree — the wood resource node.

Simplest asset in the project, and therefore the one that validates the export
path: geometry, material and origin convention with nothing else in the way.
"""

from __future__ import annotations

import lib_material
import lib_mesh
import lib_scene

NAME = "res_tree"
CATEGORY = "nature"


def build():
    lib_scene.reset()

    trunk = lib_mesh.cylinder("trunk", radius=0.22, height=3.0,
                              location=(0.0, 0.0, 1.5), vertices=8)
    lib_material.assign(trunk, lib_material.bark())

    # Three stacked, shrinking cones read as a conifer from any distance —
    # and stay far below the poly budget.
    crowns = []
    for index, (radius, height, z) in enumerate((
        (1.6, 2.4, 3.1),
        (1.25, 2.1, 4.3),
        (0.85, 1.8, 5.4),
    )):
        crown = lib_mesh.cone(f"crown_{index}", radius_bottom=radius, radius_top=0.0,
                              height=height, location=(0.0, 0.0, z), vertices=9)
        crowns.append(crown)

    foliage = lib_mesh.join(crowns, "foliage")
    lib_material.assign(foliage, lib_material.foliage())

    objects = [trunk, foliage]
    lib_mesh.ground_assembly(objects)
    return objects


def export():
    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)
    triangles = sum(lib_mesh.triangle_count(obj) for obj in objects)
    print(f"[asset] {NAME}: {triangles} triangles")
    lib_scene.report(path)
    return path
