"""PBR materials.

Two kinds exist:

  * ordinary materials — wood, thatch, clay, stone, metal, cloth
  * the team-colour material, named ``MAT_teamcolor``

The team-colour material is the contract with the game: the view layer looks for
that exact material name on an imported model and overrides its albedo with the
owning player's colour. That keeps faction colouring out of the texture pipeline
entirely, which matters while the models are still placeholder geometry.
"""

from __future__ import annotations

import bpy

TEAM_COLOR_MATERIAL = "MAT_teamcolor"


def pbr(name: str, base_color, roughness: float = 0.85, metallic: float = 0.0):
    """Create (or reuse) a Principled BSDF material."""
    full_name = name if name.startswith("MAT_") else f"MAT_{name}"

    existing = bpy.data.materials.get(full_name)
    if existing is not None:
        return existing

    material = bpy.data.materials.new(full_name)
    material.use_nodes = True

    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is not None:
        principled.inputs["Base Color"].default_value = (*base_color, 1.0)
        principled.inputs["Roughness"].default_value = roughness
        principled.inputs["Metallic"].default_value = metallic

    return material


def team_color(base_color=(0.55, 0.55, 0.58)):
    """The material the game recolours per player.

    The base colour here is only what shows up in Blender and in a raw glTF
    viewer — in game it is replaced.
    """
    return pbr(TEAM_COLOR_MATERIAL, base_color, roughness=0.6)


def assign(obj, material) -> None:
    """Give an object exactly one material.

    One material per object keeps the draw call count down, which is the whole
    reason the meshes get joined before export.
    """
    obj.data.materials.clear()
    obj.data.materials.append(material)


# --- The project palette -------------------------------------------------
# Named so that asset scripts read like a description, not like a colour table.

def wood():
    return pbr("wood", (0.29, 0.19, 0.11), roughness=0.9)


def bark():
    return pbr("bark", (0.22, 0.16, 0.11), roughness=0.95)


def foliage():
    return pbr("foliage", (0.16, 0.30, 0.13), roughness=0.9)


def thatch():
    return pbr("thatch", (0.52, 0.41, 0.21), roughness=0.95)


def clay():
    return pbr("clay", (0.62, 0.53, 0.42), roughness=0.9)


def stone():
    return pbr("stone", (0.44, 0.43, 0.41), roughness=0.85)


def metal():
    return pbr("metal", (0.55, 0.56, 0.58), roughness=0.35, metallic=1.0)


def cloth():
    return pbr("cloth", (0.55, 0.47, 0.36), roughness=0.95)


def hide():
    """Cured animal hide — the Stone Age's roofing and cladding material."""
    return pbr("hide", (0.46, 0.34, 0.23), roughness=0.9)


def hair():
    """Hair and beard — dark enough that a head does not read as bald."""
    return pbr("hair", (0.16, 0.11, 0.07), roughness=0.95)


def skin():
    return pbr("skin", (0.68, 0.51, 0.39), roughness=0.75)
