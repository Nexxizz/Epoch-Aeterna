"""Settler — the rigged, animated validation asset.

This is the one that matters. A tree proves geometry export; a settler proves the
whole chain: armature, automatic weights, several named animation clips on NLA
strips, and the -Y facing convention surviving the trip into Godot.

If this arrives correctly, the remaining thirteen assets are variations on a
solved problem.
"""

from __future__ import annotations

import lib_anim
import lib_material
import lib_mesh
import lib_rig
import lib_scene

NAME = "unit_settler"
CATEGORY = "units"

HEIGHT = 1.8


def build():
    lib_scene.reset()

    armature = lib_rig.humanoid(NAME, HEIGHT)

    body = _build_body()
    clothes = _build_clothes()
    tool = _build_tool()

    # The tool is joined into the clothing mesh so it is skinned to the hand
    # along with the sleeve, instead of needing its own attachment socket.
    equipment = lib_mesh.join([clothes, tool], "equipment")
    lib_material.assign(equipment, lib_material.team_color())

    lib_rig.bind([body, equipment], armature)

    _build_animations(armature)

    return [armature, body, equipment]


def _build_body():
    """Head, hands and feet — everything that shows skin."""
    parts = [
        lib_mesh.box("head", (0.22, 0.22, 0.24), location=(0.0, 0.0, 1.68), bevel=0.05),
        lib_mesh.box("hand_l", (0.09, 0.09, 0.12), location=(0.19, 0.0, 0.80), bevel=0.03),
        lib_mesh.box("hand_r", (0.09, 0.09, 0.12), location=(-0.19, 0.0, 0.80), bevel=0.03),
        lib_mesh.box("foot_l", (0.10, 0.24, 0.09), location=(0.10, -0.03, 0.05), bevel=0.02),
        lib_mesh.box("foot_r", (0.10, 0.24, 0.09), location=(-0.10, -0.03, 0.05), bevel=0.02),
    ]

    body = lib_mesh.join(parts, "body")
    lib_material.assign(body, lib_material.skin())
    return body


def _build_clothes():
    """Tunic, sleeves and trousers. Carries the player colour."""
    parts = [
        # Torso, tapering is faked by two stacked boxes.
        lib_mesh.box("torso", (0.34, 0.20, 0.42), location=(0.0, 0.0, 1.36), bevel=0.04),
        lib_mesh.box("waist", (0.28, 0.18, 0.22), location=(0.0, 0.0, 1.05), bevel=0.04),

        lib_mesh.box("arm_l", (0.11, 0.11, 0.52), location=(0.19, 0.0, 1.20), bevel=0.03),
        lib_mesh.box("arm_r", (0.11, 0.11, 0.52), location=(-0.19, 0.0, 1.20), bevel=0.03),

        lib_mesh.box("leg_l", (0.13, 0.14, 0.86), location=(0.10, 0.0, 0.52), bevel=0.03),
        lib_mesh.box("leg_r", (0.13, 0.14, 0.86), location=(-0.10, 0.0, 0.52), bevel=0.03),
    ]

    return lib_mesh.join(parts, "clothes")


def _build_tool():
    """A carried axe — the silhouette cue that says 'worker' at RTS distance."""
    handle = lib_mesh.box("axe_handle", (0.05, 0.05, 0.7),
                          location=(-0.24, -0.10, 0.95), bevel=0.01)
    head = lib_mesh.box("axe_head", (0.07, 0.22, 0.16),
                        location=(-0.24, -0.10, 1.28), bevel=0.02)

    return lib_mesh.join([handle, head], "axe")


def _build_animations(armature) -> None:
    """The clip set every human unit is expected to provide."""
    for clip in (lib_anim.idle, lib_anim.walk, lib_anim.work_chop, lib_anim.death):
        lib_anim.rest_pose(armature)
        clip(armature)

    lib_anim.rest_pose(armature)


def export():
    objects = build()
    path = lib_scene.export_glb(objects, CATEGORY, NAME)

    meshes = [obj for obj in objects if obj.type == "MESH"]
    triangles = sum(lib_mesh.triangle_count(obj) for obj in meshes)
    clips = len(objects[0].animation_data.nla_tracks) if objects[0].animation_data else 0

    print(f"[asset] {NAME}: {triangles} triangles, {clips} animation clips")
    lib_scene.report(path)
    return path
