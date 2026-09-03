"""Mesh building blocks.

Deliberately small primitives plus a finishing pass. The finishing pass is what
makes procedural geometry stop looking procedural: a slight bevel catches the
light along every edge, and smooth shading by angle keeps flat faces flat while
rounding what should be round.
"""

from __future__ import annotations

import math
import random

import bpy
from mathutils import Vector


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


def strut(name: str, start, end, width: float = 0.15, thickness: float = 0.0,
          bevel: float = 0.02):
    """A beam spanning two points.

    The workhorse for hand-built structures: leaning poles, ridge beams, rack
    frames. Expressing them as "from here to there" keeps the asset scripts
    readable, instead of burying every pole in its own rotation matrix.
    """
    start = Vector(start)
    end = Vector(end)
    delta = end - start
    length = delta.length

    obj = box(name, (width, thickness or width, length), bevel=bevel)

    # Point the box's local Z along the beam, then move it to the midpoint.
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    obj.location = (start + end) * 0.5
    return obj


def radial_panel(name: str, angle: float, base_radius: float, base_z: float,
                 top_radius: float, top_z: float, width: float,
                 thickness: float = 0.08, bevel: float = 0.01, segments: int = 2):
    """A panel leaning inward at a given azimuth, with its width kept tangential.

    ``strut`` cannot be used for this: it derives the beam's roll from a world-up
    reference, so the width axis ends up pointing in an arbitrary direction. On a
    roof made of panels that leaves gaps you can see straight through. Here the
    rotation is built explicitly — tilt around Y, then azimuth around Z — which
    keeps the width running along the circle.
    """
    dr = top_radius - base_radius
    dz = top_z - base_z
    length = math.hypot(dr, dz)

    # Local X is thickness, local Y is width (tangential), local Z is length.
    obj = box(name, (thickness, width, length), bevel=bevel, segments=segments)

    mid_radius = (base_radius + top_radius) * 0.5
    obj.location = (
        math.cos(angle) * mid_radius,
        math.sin(angle) * mid_radius,
        (base_z + top_z) * 0.5,
    )
    obj.rotation_euler = (0.0, math.atan2(dr, dz), angle)
    return obj


def limb(name: str, start, end, radius_start: float, radius_end: float = 0.0,
         vertices: int = 8, smooth_angle: float = 60.0):
    """A tapered cylinder spanning two points.

    What turns a blocky figure into a body: arms, legs and torsos are round and
    they get thinner towards the joints. Boxes cannot do either, and at RTS
    distance the silhouette is all that carries the impression.
    """
    start = Vector(start)
    end = Vector(end)
    delta = end - start
    length = delta.length

    bpy.ops.mesh.primitive_cone_add(
        radius1=radius_start,
        radius2=radius_end if radius_end > 0.0 else radius_start * 0.6,
        depth=length,
        vertices=vertices)

    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    obj.location = (start + end) * 0.5

    return _finish(obj, 0.0, 0, smooth_angle)


def boulder(name: str, radius: float, location, rng: random.Random,
            flatten: float = 0.65):
    """An irregular rock. Squashed and randomly turned so no two look alike."""
    obj = sphere(name, radius, location=location, segments=6, rings=4, smooth_angle=25.0)

    obj.scale = (
        rng.uniform(0.8, 1.25),
        rng.uniform(0.8, 1.25),
        flatten * rng.uniform(0.8, 1.2),
    )
    obj.rotation_euler = (
        rng.uniform(-0.25, 0.25),
        rng.uniform(-0.25, 0.25),
        rng.uniform(0.0, math.tau),
    )
    return obj


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
