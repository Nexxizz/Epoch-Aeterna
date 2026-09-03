"""Scene handling and glTF export.

Central place for the project's asset conventions, so every generator obeys them
without having to remember them:

  * 1 Blender unit = 1 metre
  * origin sits at the centre of the footprint, on the ground (z = 0)
  * models face -Y in Blender, which the glTF exporter turns into Godot's -Z "forward"
  * all transforms applied, one export per asset, glTF 2.0 binary (.glb)
"""

from __future__ import annotations

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

    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        # Blender is Z-up, Godot is Y-up. The exporter converts, which is also what
        # turns our -Y "front" into Godot's -Z.
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

    return path


def report(path: str) -> None:
    size_kb = os.path.getsize(path) / 1024.0
    print(f"[export] {os.path.relpath(path, REPO_ROOT)}  ({size_kb:.0f} KB)")
