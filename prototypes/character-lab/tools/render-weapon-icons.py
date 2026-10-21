"""Render original weapon assets as transparent inventory icons with Blender.

Run: blender --background --python prototypes/character-lab/tools/render-weapon-icons.py
The frying pan is built here from primitives because it has no world GLB yet.
"""
import argparse
import csv
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "game/assets/icons"
OUT.mkdir(parents=True, exist_ok=True)


def requested_ids():
    blender_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", help="comma-separated weapon IDs to render")
    args = parser.parse_args(blender_args)
    return set(args.only.split(",")) if args.only else None


def material(name, color, metal, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = rough
    return mat


def frying_pan():
    iron = material("Pan cast iron", (.07, .085, .10), .65, .38)
    rubber = material("Pan grip", (.025, .03, .035), .08, .65)
    vertices = []
    count = 48
    for radius, z in [(.155, 0), (.205, .055), (.188, .055), (.145, .014)]:
        vertices.extend((radius * math.cos(i * math.tau / count),
                         radius * math.sin(i * math.tau / count) - .13, z)
                        for i in range(count))
    faces = []
    for ring in range(3):
        for i in range(count):
            faces.append((ring * count + i, ring * count + (i + 1) % count,
                          (ring + 1) * count + (i + 1) % count, (ring + 1) * count + i))
    faces.extend([tuple(reversed(range(count))), tuple(range(3 * count, 4 * count))])
    mesh = bpy.data.meshes.new("Pan bowl")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bowl = bpy.data.objects.new("Pan bowl", mesh)
    bpy.context.collection.objects.link(bowl)
    bowl.data.materials.append(iron)
    for at, size, mat in [((0, .13, .025), (.055, .19, .035), iron),
                          ((0, .32, .025), (.07, .25, .05), rubber)]:
        bpy.ops.mesh.primitive_cube_add(size=1, location=at)
        obj = bpy.context.object
        obj.dimensions = size
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        obj.data.materials.append(mat)
        bevel = obj.modifiers.new("Rounded handle", "BEVEL")
        bevel.width = .018
        bevel.segments = 3


def area_light(name, position, energy, size, center):
    bpy.ops.object.light_add(type="AREA", location=center + Vector(position))
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()


with (ROOT / "game/content/data/weapons.csv").open(encoding="utf-8-sig", newline="") as source:
    weapons = [row for row in csv.DictReader(source) if not row["id"].startswith("#")]

only = requested_ids()
if only is not None:
    known_ids = {weapon["id"] for weapon in weapons}
    unknown_ids = only - known_ids
    if unknown_ids:
        raise SystemExit(f"Unknown weapon ID(s): {', '.join(sorted(unknown_ids))}")
    weapons = [weapon for weapon in weapons if weapon["id"] in only]

for weapon in weapons:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if weapon["model"]:
        model = ROOT / "game" / weapon["model"].removeprefix("res://")
        bpy.ops.import_scene.gltf(filepath=str(model))
    elif weapon["id"] == "frying_pan":
        frying_pan()
    else:
        continue

    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in bpy.context.scene.objects
              if obj.type == "MESH" for corner in obj.bound_box]
    center = Vector(tuple((min(p[i] for p in points) + max(p[i] for p in points)) / 2
                          for i in range(3)))
    scene = bpy.context.scene
    bpy.ops.object.camera_add(location=center + Vector((-4, -1.35, 2.2)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    scene.camera = camera
    bpy.context.view_layer.update()
    projected = [camera.matrix_world.inverted() @ point for point in points]
    width = max(p.x for p in projected) - min(p.x for p in projected)
    height = max(p.y for p in projected) - min(p.y for p in projected)
    camera.data.ortho_scale = max(width, height * 1.6) * 1.13
    # Center the projected silhouette, including asymmetrical stocks and magazines.
    offset = Vector(((max(p.x for p in projected) + min(p.x for p in projected)) / 2,
                     (max(p.y for p in projected) + min(p.y for p in projected)) / 2, 0))
    camera.location += camera.rotation_euler.to_matrix() @ offset
    area_light("Large key", (-2, -2, 4), 650, 4, center)
    area_light("Soft front fill", (-3, 3, 1.5), 450, 3, center)
    area_light("Edge light", (2, 1, 3), 850, 3, center)
    scene.world = bpy.data.worlds.new("Neutral studio")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.3, .3, .3, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .6
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 256
    scene.render.resolution_y = 160
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = .7
    scene.render.filepath = str(OUT / f"{weapon['id']}.png")
    bpy.ops.render.render(write_still=True)
    print(f"ICON_READY {weapon['id']}", flush=True)
