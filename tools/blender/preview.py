"""Render a close-up preview of one asset.

    blender --background --python tools/blender/preview.py -- bld_towncenter [out.png] [view]

``view`` is ``three-quarter`` (default), ``front`` or ``side``. A fourth
argument of the form ``Clip:frame`` renders one pose of an animation, which is
how a deformation problem gets diagnosed without starting the game.

Builds the asset with its own generator script, frames it with a three-quarter
camera and renders a PNG. Useful for judging a model without starting the game,
and the obvious place to generate unit and building icons from later on.
"""

from __future__ import annotations

import importlib
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
for extra in (HERE, os.path.join(HERE, "assets")):
    if extra not in sys.path:
        sys.path.insert(0, extra)

import lib_scene  # noqa: E402  (path setup has to come first)

RESOLUTION = (900, 700)


VIEWS = {
    "three-quarter": Vector((0.75, -1.0, 0.55)),
    "front": Vector((0.0, -1.0, 0.16)),
    "side": Vector((1.0, -0.05, 0.16)),
}


def _arguments():
    if "--" not in sys.argv:
        return "bld_towncenter", None, "three-quarter"

    rest = sys.argv[sys.argv.index("--") + 1:]
    name = rest[0] if rest else "bld_towncenter"
    output = rest[1] if len(rest) > 1 else None
    view = rest[2] if len(rest) > 2 else "three-quarter"

    # Optional "Clip:frame", so a pose can be inspected without starting the game.
    pose = None
    if len(rest) > 3 and ":" in rest[3]:
        clip, frame = rest[3].rsplit(":", 1)
        pose = (clip, int(frame))

    return name, output, view, pose


def _bounds(objects):
    """World-space bounding box over every mesh of the asset.

    Evaluated through the dependency graph rather than read from ``bound_box``,
    which holds the *rest* shape — a raised axe in a posed frame would otherwise
    fall outside the view.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()

    corners = []
    for obj in objects:
        if obj.type != "MESH":
            continue

        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        corners.extend(evaluated.matrix_world @ vertex.co for vertex in mesh.vertices)
        evaluated.to_mesh_clear()

    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return low, high


def _frame(objects, view: str = "three-quarter"):
    """Place a camera and two lights around the asset."""
    low, high = _bounds(objects)
    centre = (low + high) * 0.5
    radius = max((high - low).length * 0.5, 0.5)

    lens = 60.0
    sensor = 36.0

    # Fit the bounding sphere in view instead of guessing a distance: a tall,
    # narrow asset like a unit needs a very different distance from a wide one
    # like a building, and a fixed factor crops one or dwarfs the other.
    half_angle = math.atan(sensor * 0.5 / lens)
    distance = radius / math.tan(half_angle) * 1.15

    direction = VIEWS.get(view, VIEWS["three-quarter"]).normalized()
    bpy.ops.object.camera_add(location=centre + direction * distance)
    camera = bpy.context.active_object
    camera.data.lens = lens
    camera.data.sensor_width = sensor

    # Point it at the centre by aligning -Z with the view direction.
    camera.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    # Key light from the same side as the camera, fill from the opposite one.
    # Kept low: with the neutral view transform there is no tone mapping to
    # rescue an over-exposed image, and every material would render as the same
    # washed-out beige.
    bpy.ops.object.light_add(type="SUN", location=centre + Vector((3, -4, 6)))
    key = bpy.context.active_object
    key.data.energy = 1.15
    key.rotation_euler = (math.radians(50), 0.0, math.radians(35))

    bpy.ops.object.light_add(type="SUN", location=centre + Vector((-5, 3, 4)))
    fill = bpy.context.active_object
    fill.data.energy = 0.35
    fill.rotation_euler = (math.radians(60), 0.0, math.radians(-140))

    # A plain sky so the silhouette does not sit on black.
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.22, 0.28, 0.36, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.45
    bpy.context.scene.world = world


def _apply_pose(objects, clip: str, frame: int) -> None:
    """Put the armature into one frame of a named clip.

    The clips live on NLA strips, so the action has to be made active again
    before the frame can be evaluated.
    """
    armature = next((obj for obj in objects if obj.type == "ARMATURE"), None)
    if armature is None or armature.animation_data is None:
        print(f"[preview] no armature with animation, ignoring pose {clip}:{frame}")
        return

    action = bpy.data.actions.get(clip)
    if action is None:
        print(f"[preview] unknown clip '{clip}'")
        return

    armature.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    print(f"[preview] pose {clip} frame {frame}")


def _apply_pose(objects, clip: str, frame: int) -> None:
    """Put the armature into one frame of a named clip.

    The clips live on NLA strips, so the action has to be made active again
    before a frame can be evaluated.
    """
    armature = next((obj for obj in objects if obj.type == "ARMATURE"), None)
    if armature is None or armature.animation_data is None:
        print(f"[preview] no animated armature, ignoring pose {clip}:{frame}")
        return

    action = bpy.data.actions.get(clip)
    if action is None:
        print(f"[preview] unknown clip '{clip}'")
        return

    armature.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    print(f"[preview] pose {clip} frame {frame}")


def main() -> int:
    name, output, view, pose = _arguments()

    try:
        module = importlib.import_module(name)
    except ModuleNotFoundError:
        print(f"[preview] unknown asset: {name}")
        return 1

    objects = module.build()

    if pose is not None:
        _apply_pose(objects, *pose)

    _frame(objects, view)

    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = RESOLUTION
    scene.render.film_transparent = False

    # Blender defaults to AgX, which desaturates heavily — every material then
    # renders as the same beige and the preview says nothing about the colours.
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"

    if output is None:
        output = os.path.join(lib_scene.REPO_ROOT, "docs", "previews", f"{name}.png")

    os.makedirs(os.path.dirname(output), exist_ok=True)
    scene.render.filepath = output

    bpy.ops.render.render(write_still=True)
    print(f"[preview] {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
