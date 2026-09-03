"""Scene handling and glTF export.

Central place for the project's asset conventions, so every generator obeys them
without having to remember them:

  * 1 Blender unit = 1 metre
  * origin sits at the centre of the footprint, on the ground (z = 0)
  * models are authored facing -Y in Blender, which is what Blender's own front
    view looks at, and are turned 180 degrees on export so they end up facing
    Godot's -Z "forward"
  * all transforms applied, one export per asset, glTF 2.0 binary (.glb)
"""

from __future__ import annotations

import math
import os

import bpy

# Repository root, derived from this file's location (tools/blender/lib_scene.py).
REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MODEL_ROOT = os.path.join(REPO_ROOT, "assets", "models")


def reset() -> None:
    """Wipe the scene down to nothing.

    Blender starts with a cube, a camera and a light; leaving them in would export
    them along with the asset. Orphaned data blocks are purged too, otherwise a
    long ``build_all`` run accumulates every mesh it ever created.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)

    for collection in (
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.armatures,
        bpy.data.actions,
        bpy.data.images,
    ):
        for block in list(collection):
            collection.remove(block)


def deselect_all() -> None:
    for obj in bpy.data.objects:
        obj.select_set(False)
    bpy.context.view_layer.objects.active = None


def select(objects) -> None:
    """Select exactly the given objects and make the first one active."""
    deselect_all()
    for obj in objects:
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def export_glb(objects, category: str, name: str) -> str:
    """Write the given objects to ``assets/models/<category>/<name>.glb``.

    Returns the absolute path so the caller can report it.
    """
    target_dir = os.path.join(MODEL_ROOT, category)
    os.makedirs(target_dir, exist_ok=True)
    path = os.path.join(target_dir, f"{name}.glb")

    select(objects)
    restore = _face_godot_forward(objects)

    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        # Blender is Z-up, Godot is Y-up; the exporter converts. It maps
        # gltf_z = -blender_y, which is why the facing has to be corrected
        # separately — see _face_godot_forward.
        export_yup=True,
        # Bake modifiers into the exported mesh — Godot should not have to know
        # about bevels and weighted normals.
        export_apply=True,
        export_animations=True,
        export_skins=True,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
    )

    restore()
    return path


def _face_godot_forward(objects):
    """Turn the asset around so its authored front ends up as Godot's forward.

    The glTF exporter converts Blender's Z-up to Y-up by mapping
    ``gltf_z = -blender_y``. An asset authored facing -Y therefore arrives
    facing *+Z*, while Godot treats -Z as forward — so every model would walk
    backwards. Rotating by 180 degrees here fixes it in the one place that owns
    the convention, instead of every asset script having to know about it.

    Only parentless objects are turned; children (meshes under an armature)
    inherit the rotation and would otherwise be turned twice.

    Returns a callable that restores the original rotations.
    """
    roots = [obj for obj in objects if obj.parent is None]
    original = [(obj, tuple(obj.rotation_euler)) for obj in roots]

    for obj in roots:
        obj.rotation_euler.z += math.pi

    # The exporter reads the evaluated dependency graph, which does not yet know
    # about a transform set from a script. Without this the rotation is silently
    # ignored and the asset exports facing the wrong way.
    bpy.context.view_layer.update()

    def restore():
        for obj, rotation in original:
            obj.rotation_euler = rotation
        bpy.context.view_layer.update()

    return restore


def report(path: str) -> None:
    size_kb = os.path.getsize(path) / 1024.0
    print(f"[export] {os.path.relpath(path, REPO_ROOT)}  ({size_kb:.0f} KB)")
