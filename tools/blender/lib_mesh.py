"""Mesh building blocks.

Deliberately small primitives plus a finishing pass. The finishing pass is what
makes procedural geometry stop looking procedural: a slight bevel catches the
light along every edge, and smooth shading by angle keeps flat faces flat while
rounding what should be round.
"""

from __future__ import annotations

import math
import bpy


def _finish(obj, bevel: float, segments: int, smooth_angle: float):
    """Apply the standard finishing pass: bevel, then shade smooth by angle.

    The bevel is applied straight away rather than left as a modifier. Joining
    objects keeps only the *first* one's modifier stack, which would then be
    applied to the whole merged mesh at export time — on intersecting boxes that
    produces degenerate faces and a "mesh is not valid" warning.
    """
    bpy.context.view_layer.objects.active = obj

    if bevel > 0.0:
        modifier = obj.modifiers.new(name="Bevel", type="BEVEL")
        modifier.width = bevel
        modifier.segments = segments
        modifier.limit_method = "ANGLE"
        modifier.angle_limit = math.radians(30.0)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    if smooth_angle > 0.0:
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth_angle))

    return obj


def box(name: str, size, location=(0.0, 0.0, 0.0), bevel: float = 0.02,
        segments: int = 2, smooth_angle: float = 30.0):
    """Axis-aligned box. ``size`` is the full extent in metres, not the half extent."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (size[0], size[1], size[2])
    apply_transforms(obj)
    return _finish(obj, bevel, segments, smooth_angle)


def cylinder(name: str, radius: float, height: float, location=(0.0, 0.0, 0.0),
             vertices: int = 12, bevel: float = 0.02, smooth_angle: float = 40.0):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=height, vertices=vertices, location=location)
    obj = bpy.context.active_object
    obj.name = name
    return _finish(obj, bevel, 2, smooth_angle)


def cone(name: str, radius_bottom: float, radius_top: float, height: float,
         location=(0.0, 0.0, 0.0), vertices: int = 10, smooth_angle: float = 40.0):
    bpy.ops.mesh.primitive_cone_add(
        radius1=radius_bottom, radius2=radius_top, depth=height,
        vertices=vertices, location=location)
    obj = bpy.context.active_object
    obj.name = name
    return _finish(obj, 0.0, 0, smooth_angle)


def sphere(name: str, radius: float, location=(0.0, 0.0, 0.0),
           segments: int = 12, rings: int = 8, smooth_angle: float = 60.0):
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=radius, segments=segments, ring_count=rings, location=location)
    obj = bpy.context.active_object
    obj.name = name
    return _finish(obj, 0.0, 0, smooth_angle)


def wedge_roof(name: str, width: float, depth: float, height: float,
               location=(0.0, 0.0, 0.0)):
    """A gable roof: a box squeezed to a ridge along its X axis."""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = (width, depth, height)
    apply_transforms(obj)

    mesh = obj.data
    top = max(vertex.co.z for vertex in mesh.vertices)

    # Pull the two top edges together along Y to form the ridge.
    for vertex in mesh.vertices:
        if abs(vertex.co.z - top) < 1e-4:
            vertex.co.y = 0.0

    return _finish(obj, 0.0, 0, 35.0)


def apply_transforms(obj) -> None:
    """Bake location, rotation and scale into the mesh data.

    Godot reads the exported transform as the node transform. Unapplied scale
    would therefore arrive as a scaled node, which breaks child placement and
    physics later on.
    """
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)


def cleanup(obj):
    """Repair a mesh after joining.

    Joining objects that share coordinates leaves duplicate vertices and can
    produce degenerate faces. glTF export warns about those ("mesh is not
    valid"), and Godot would inherit the mess as shading artefacts.
    """
    bpy.context.view_layer.objects.active = obj

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")

    obj.data.validate(verbose=False)
    return obj


def join(objects, name: str):
    """Merge objects into one. The first one survives and keeps its origin."""
    if len(objects) == 1:
        objects[0].name = name
        return cleanup(objects[0])

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    result = bpy.context.active_object
    result.name = name
    return cleanup(result)


def ground_assembly(objects) -> None:
    """Put an asset's lowest point at z = 0, keeping its parts in place.

    The project convention: models sit *on* their origin, so the game can place
    them at terrain height without knowing anything about their dimensions.

    Grounding has to happen for the asset as a whole. Doing it per object — which
    is the obvious-looking mistake — drops every part to z = 0 individually and
    collapses a building into a pile at the origin.
    """
    if not isinstance(objects, (list, tuple)):
        objects = [objects]

    # Bake each object's location into its mesh, so every origin is the world
    # origin and the vertex coordinates carry the layout.
    for obj in objects:
        deselect_all_objects()
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    lowest = min(
        min(vertex.co.z for vertex in obj.data.vertices)
        for obj in objects
        if obj.type == "MESH" and obj.data.vertices)

    if abs(lowest) < 1e-6:
        return

    for obj in objects:
        if obj.type != "MESH":
            continue
        for vertex in obj.data.vertices:
            vertex.co.z -= lowest


def deselect_all_objects() -> None:
    for obj in bpy.data.objects:
        obj.select_set(False)


def triangle_count(obj) -> int:
    """Triangles after modifiers — the number the poly budget actually refers to."""
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()

    total = sum(max(len(polygon.vertices) - 2, 0) for polygon in mesh.polygons)
    evaluated.to_mesh_clear()
    return total
