"""Build the Onslaught slot 3 honk and rubber duck GLB models with Blender.

Run from the repository root:
    blender --background --python prototypes/character-lab/tools/gadget-models.py

The models use +Z as their forward axis, +Y as up, and the origin as the grip
or support point. Geometry is joined by material before each GLB is exported.
"""
import json
import math
import struct
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / "game/assets/weapons"
OUT = ROOT / "prototypes/character-lab/output/weapons"
ASSETS.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)


def material(name, color, metallic=0.0, roughness=0.5):
    # The colors are display sRGB (the brief's hex values); Blender node inputs are linear (as in weapons-build.py).
    color = tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in color)
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return mat


def make_materials():
    return {
        "honk_red": material("Honk red rubber", (0.847, 0.263, 0.231), 0.0, 0.42),
        "gold": material("Honk warm gold", (0.886, 0.702, 0.290), 0.48, 0.3),
        "bell_inner": material("Bell dark gold", (0.541, 0.416, 0.141), 0.35, 0.4),
        "cream": material("Honk cream trim", (0.953, 0.890, 0.769), 0.0, 0.46),
        "duck_yellow": material("Duck bath yellow", (1.0, 0.824, 0.247), 0.0, 0.38),
        "beak_orange": material("Duck beak orange", (1.0, 0.541, 0.239), 0.0, 0.4),
        "eye_ink": material("Duck ink eyes", (0.165, 0.122, 0.102), 0.0, 0.3),
        "eye_glint": material("Duck eye glints", (1.0, 1.0, 1.0), 0.0, 0.22),
    }


def smooth(obj):
    if obj.type == "MESH":
        for face in obj.data.polygons:
            face.use_smooth = True
    return obj


def ellipsoid(name, location, scale, mat, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return smooth(obj)


def rounded_box(name, location, size, mat, bevel):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new("Rounded toy edge", "BEVEL")
    mod.width = bevel
    mod.segments = 3
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return smooth(obj)


def lathe_path(name, points, mat, sides=24, inner_mat=None):
    """Create a smooth hollow tube or trumpet around a curved centerline.

    Each point is (center xyz, outer radius, inner radius). The final rings
    leave the bell mouth visibly hollow from the front.
    """
    vertices, faces, material_ids = [], [], []
    for i, (center, outer, inner) in enumerate(points):
        previous = Vector(points[max(i - 1, 0)][0])
        following = Vector(points[min(i + 1, len(points) - 1)][0])
        tangent = (following - previous).normalized()
        reference = Vector((1, 0, 0))
        if abs(tangent.dot(reference)) > 0.9:
            reference = Vector((0, 1, 0))
        axis_a = tangent.cross(reference).normalized()
        axis_b = tangent.cross(axis_a).normalized()
        for radius in (outer, inner):
            for j in range(sides):
                angle = math.tau * j / sides
                p = Vector(center) + radius * (math.cos(angle) * axis_a + math.sin(angle) * axis_b)
                vertices.append(tuple(p))
    ring_count = len(points)
    for ring in range(ring_count - 1):
        for wall in range(2):
            a = (ring * 2 + wall) * sides
            b = ((ring + 1) * 2 + wall) * sides
            for j in range(sides):
                j2 = (j + 1) % sides
                face = (a + j, a + j2, b + j2, b + j) if wall == 0 else (a + j2, a + j, b + j, b + j2)
                faces.append(face)
                material_ids.append(0 if wall == 0 else 1)
    mesh = bpy.data.meshes.new(name + " geometry")
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(mat)
    if inner_mat:
        mesh.materials.append(inner_mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if inner_mat:
        for polygon, material_index in zip(mesh.polygons, material_ids):
            polygon.material_index = material_index
    return smooth(obj)


def horn_model(mats):
    parts = []
    parts.append(ellipsoid("Squeeze bulb", (0, 0.035, 0.055), (0.064, 0.064, 0.067), mats["honk_red"], 24, 16))
    parts.append(lathe_path("Bent brass neck", [
        ((0, 0.035, 0.095), 0.024, 0.0),
        ((0, 0.035, 0.145), 0.022, 0.0),
        ((0, 0.043, 0.195), 0.020, 0.0),
        ((0, 0.062, 0.232), 0.020, 0.0),
    ], mats["gold"], 20))
    parts.append(lathe_path("Bell and flared mouth", [
        ((0, 0.062, 0.218), 0.025, 0.0),
        ((0, 0.062, 0.252), 0.027, 0.0),
        ((0, 0.062, 0.295), 0.039, 0.006),
        ((0, 0.062, 0.345), 0.061, 0.018),
        ((0, 0.062, 0.400), 0.080, 0.028),
    ], mats["gold"], 32, mats["bell_inner"]))
    # Cream ring between the red bulb and brass tubing.
    parts.append(lathe_path("Cream accent ring", [
        ((0, 0.035, 0.104), 0.031, 0.018),
        ((0, 0.035, 0.116), 0.031, 0.018),
    ], mats["cream"], 24, mats["cream"]))
    return parts


def duck_model(mats):
    parts = []
    # The thin disk supplies a genuinely flat contact face under the body.
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.071, depth=0.010, location=(0, 0.007, 0))
    base = bpy.context.object
    base.name = "Flat duck base"
    base.rotation_euler.x = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    base.data.materials.append(mats["duck_yellow"])
    parts.append(base)
    # A flattened ellipsoid keeps the body broad and low above that base.
    parts.append(ellipsoid("Rounded duck body", (0, 0.081, 0.005), (0.100, 0.079, 0.148), mats["duck_yellow"], 20, 12))
    parts.append(ellipsoid("Raised tail", (0, 0.132, -0.132), (0.034, 0.030, 0.043), mats["duck_yellow"], 12, 8))
    parts.append(ellipsoid("Duck neck", (0, 0.150, 0.115), (0.050, 0.060, 0.059), mats["duck_yellow"], 16, 10))
    parts.append(ellipsoid("Round duck head", (0, 0.202, 0.170), (0.073, 0.071, 0.073), mats["duck_yellow"], 18, 12))
    # Flattened bill protrudes along +Z and terminates at the 0.35 m muzzle.
    # Sunk into the head's front so it reads as one piece, its tip near the 0.35 m muzzle.
    parts.append(ellipsoid("Orange bill", (0, 0.185, 0.272), (0.050, 0.025, 0.066), mats["beak_orange"], 14, 8))
    for side in (-1, 1):
        parts.append(ellipsoid("Wing", (side * 0.078, 0.091, 0.006), (0.030, 0.047, 0.082), mats["duck_yellow"], 12, 8))
        # On the head's surface (centre z 0.170, radius ~0.073), facing forward and a little up.
        parts.append(ellipsoid("Ink eye", (side * 0.050, 0.226, 0.222), (0.012, 0.015, 0.010), mats["eye_ink"], 12, 8))
        parts.append(ellipsoid("Eye glint", (side * 0.053, 0.232, 0.231), (0.0042, 0.0052, 0.0040), mats["eye_glint"], 8, 6))
    return parts


# Parts are laid out in game axes (+Y up, +Z forward); Blender is +Z up and the glTF exporter
# turns Blender (x, y, z) into (x, z, -y). A quarter turn about X maps game (x, y, z) to Blender
# (x, -z, y), so the exported GLB comes out +Y up, +Z forward like weapons-build.py's V().
TO_BLENDER = Matrix.Rotation(math.pi / 2, 4, "X")


def export_asset(asset_id, parts, length_m, scale=1.0):
    # Laid out at the brief's size, then scaled up about the grip so the prop reads from the game camera.
    grow = Matrix.Scale(scale, 4)
    for part in parts:
        part.matrix_world = TO_BLENDER @ grow @ part.matrix_world
    length_m *= scale
    root = bpy.data.objects.new(asset_id, None)
    bpy.context.collection.objects.link(root)
    root["forward"] = "+Z in glTF/Godot"
    root["grip_origin"] = "hand/support attachment"
    grouped = {}
    for part in parts:
        grouped.setdefault(part.data.materials[0].name, []).append(part)
    mesh_objects = []
    for mat_name, objects in grouped.items():
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.object.join()
        joined = bpy.context.object
        joined.name = f"{asset_id}_{mat_name}"
        joined.parent = root
        mesh_objects.append(joined)
    muzzle = bpy.data.objects.new("Muzzle", None)
    bpy.context.collection.objects.link(muzzle)
    muzzle.location = TO_BLENDER @ Vector((0, (0.062 if asset_id == "honk" else 0.185) * scale, length_m))
    muzzle.parent = root
    destination = ASSETS / f"{asset_id}.glb"
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    muzzle.select_set(True)
    for obj in mesh_objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(destination), export_format="GLB", use_selection=True,
        export_yup=True, export_extras=True, export_animations=False,
    )
    # Inspect the serialized GLB too, so checks cover the actual exported axes
    # and node structure rather than only Blender's pre-export scene.
    raw = destination.read_bytes()
    magic, version, total_length = struct.unpack_from("<III", raw)
    assert magic == 0x46546C67 and version == 2 and total_length == len(raw), asset_id
    json_length, json_kind = struct.unpack_from("<II", raw, 12)
    assert json_kind == 0x4E4F534A, asset_id
    document = json.loads(raw[20:20 + json_length])
    nodes = document["nodes"]
    muzzles = [node for node in nodes if node.get("name") == "Muzzle"]
    assert len(muzzles) == 1, asset_id
    muzzle_position = muzzles[0].get("translation", [0, 0, 0])
    serialized_triangles = sum(
        document["accessors"][primitive["indices"]]["count"] // 3
        for mesh in document.get("meshes", []) for primitive in mesh["primitives"]
    )
    material_count = len(document.get("materials", []))
    mesh_count = len(document.get("meshes", []))
    assert serialized_triangles < 2500, (asset_id, serialized_triangles)
    assert mesh_count <= 5 and material_count <= 5, (asset_id, mesh_count, material_count)
    assert muzzle_position[2] > 0, (asset_id, muzzle_position)
    assert abs(muzzle_position[2] - length_m) <= length_m * 0.15, (asset_id, muzzle_position, length_m)
    assert not any("hand" in node.get("name", "").lower() for node in nodes), asset_id
    return {
        "id": asset_id,
        "length_m": length_m,
        "triangles": serialized_triangles,
        "meshes": mesh_count,
        "materials": material_count,
        "muzzle": muzzle_position,
        "model": f"res://assets/weapons/{asset_id}.glb",
    }


def main():
    report = []
    # Comedy props a size up (horn 0.56 m, duck 0.46 m): at the brief's 0.40 / 0.35 m they were specks in hand.
    # weapon_types.csv length_m follows the exported size.
    for asset_id, builder, length, scale in (
        ("honk", horn_model, 0.40, 1.4),
        ("rubber_duck", duck_model, 0.35, 1.3),
    ):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        mats = make_materials()
        parts = builder(mats)
        record = export_asset(asset_id, parts, length, scale)
        report.append(record)
        print(f"GADGET_READY {asset_id} {record['triangles']}", flush=True)
    (OUT / "gadget-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
