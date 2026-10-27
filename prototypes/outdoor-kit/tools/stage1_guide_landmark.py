"""Build the standalone Stage 1 warehouse sign; run with Blender --background."""

import bpy
import json
import math
from pathlib import Path
from mathutils import Matrix, Vector


REPO = Path(__file__).resolve().parents[3]
ASSETS = REPO / "game/assets/large-stage-kit"
SOURCE = REPO / "prototypes/outdoor-kit/source/stage1-guide-landmark.blend"
RENDERS = REPO / "prototypes/outdoor-kit/output/stage1-guide-landmark"
TILT = Matrix.Rotation(math.radians(-20), 4, "X")
PALETTE = {"Ochre": "c7a35c", "Plaster": "d8cdb4", "Hazard": "e2b84a",
           "Metal": "56636a", "Concrete": "a4aaa1", "Lamp": "fff1c9"}


def material(name, hex_color):
    srgb = [int(hex_color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
              for c in srgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*linear, 1)
    # Blender 5.2 creates shader nodes by default (use_nodes is deprecated).
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*linear, 1)
    shader.inputs["Roughness"].default_value = 0.65
    return mat


def bevel(obj, width, segments=1):
    mod = obj.modifiers.new("Soft edges", "BEVEL")
    mod.width, mod.segments = width, segments
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)


def box(name, size, location, mat, width=0.035, segments=1):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if width:
        bevel(obj, width, segments)
    return obj


def beam(name, start, end, thickness, mat):
    start, end = Vector(start), Vector(end)
    obj = box(name, (thickness, thickness, (end - start).length),
              (start + end) / 2, mat, 0.025)
    obj.rotation_euler = (end - start).to_track_quat("Z", "Y").to_euler()
    return obj


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def clip_at_fade_boundary(obj, lower):
    """Cut an already beveled member without beveling its shared cut plane."""
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.bisect(plane_co=(0, 0, 2.4), plane_no=(0, 0, 1),
                       use_fill=True, clear_outer=lower, clear_inner=not lower)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def join_part(name, pieces, root):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in pieces:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    mesh = bpy.context.object
    mesh.name = name + "_Mesh"
    mesh.data.name = mesh.name
    # Bake transforms so the two meshes, part empties and root share the pivot.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.parent = empty(name, root)
    return mesh


def build_asset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    mats = {name: material(name, value) for name, value in PALETTE.items()}
    root = empty("warehouse_sign")
    root["asset_contract"] = "plushpanic_outdoor_v1"
    keep, upper = [], []
    # The 7 m total includes the two slightly exposed rear mast ends. The
    # tilted panel itself remains exactly 6 x 2.6 x 0.3 m with its bottom at 4.2.
    for x in (-1.85, 1.85):
        keep.append(box("Foot", (1.6, 1.2, 0.3), (x, 0, 0.15),
                        mats["Concrete"], 0.075, 2))
        for lower in (True, False):
            post = box("Post", (0.3, 0.3, 6.7), (x, 0.40, 3.65), mats["Metal"])
            (keep if lower else upper).append(clip_at_fade_boundary(post, lower))
    # Make each diagonal as one closed beam, then bisect at the fade boundary.
    for x1, x2 in ((-1.85, 1.85), (1.85, -1.85)):
        for lower in (True, False):
            obj = beam("X brace", (x1, 0.40, 0.55), (x2, 0.40, 4.35), 0.16, mats["Metal"])
            (keep if lower else upper).append(clip_at_fade_boundary(obj, lower))
    upper.append(box("Panel support", (4.1, 0.24, 0.24), (0, 0.40, 4.45), mats["Metal"]))

    panel = box("Panel", (6, 0.3, 2.6), (0, 0, 0), mats["Ochre"], 0.10, 3)
    panel.matrix_world = TILT
    bpy.context.view_layer.update()
    bottom = min((panel.matrix_world @ v.co).z for v in panel.data.vertices)
    panel.location.z = 4.2 - bottom
    bpy.context.view_layer.update()
    panel_transform = panel.matrix_world.copy()
    panel.vertex_groups.new(name="Panel").add(list(range(len(panel.data.vertices))), 1, "REPLACE")
    upper.append(panel)

    # Relief pictogram: three unoutlined cream boxes with ochre packing tape.
    for x, z in ((-1.92, -0.44), (-0.91, -0.44), (-1.415, 0.49)):
        obj = box("Pictogram box", (0.88, 0.13, 0.84), (x, -0.205, z), mats["Plaster"], 0.065)
        obj.matrix_world = panel_transform @ obj.matrix_world
        upper.append(obj)
        tape = box("Packing tape", (0.13, 0.025, 0.34), (x, -0.277, z + 0.22), mats["Ochre"], 0.008)
        tape.matrix_world = panel_transform @ tape.matrix_world
        upper.append(tape)

    # A single thick, closed +X arrow; no text, Ink outline or texture.
    outline = [(0.10, -0.29), (1.31, -0.29), (1.31, -0.70),
               (2.40, 0), (1.31, 0.70), (1.31, 0.29), (0.10, 0.29)]
    verts = [(x, y, z) for y in (-0.17, -0.31) for x, z in outline]
    count = len(outline)
    faces = [tuple(reversed(range(count))), tuple(range(count, count * 2))]
    faces += [(i, (i + 1) % count, (i + 1) % count + count, i + count) for i in range(count)]
    data = bpy.data.meshes.new("Arrow")
    data.from_pydata(verts, [], faces)
    data.update()
    arrow = bpy.data.objects.new("Arrow", data)
    bpy.context.collection.objects.link(arrow)
    arrow.data.materials.append(mats["Hazard"])
    bpy.ops.object.select_all(action="DESELECT")
    arrow.select_set(True)
    bpy.context.view_layer.objects.active = arrow
    bevel(arrow, 0.035)
    arrow.matrix_world = panel_transform
    upper.append(arrow)

    # Eight bulbs per horizontal edge and three per vertical edge = 22.
    bulb_positions = [(x, z) for z in (-1.08, 1.08)
                      for x in (-2.73, -1.95, -1.17, -0.39, 0.39, 1.17, 1.95, 2.73)]
    bulb_positions += [(x, z) for x in (-2.73, 2.73) for z in (-0.54, 0, 0.54)]
    for x, z in bulb_positions:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.09,
                                            location=(x, -0.20, z))
        bulb = bpy.context.object
        bulb.name = "Bulb"
        bulb.data.materials.append(mats["Lamp"])
        for polygon in bulb.data.polygons:
            polygon.use_smooth = True
        bulb.matrix_world = panel_transform @ bulb.matrix_world
        upper.append(bulb)
    join_part("Keep", keep, root)
    join_part("Occlude_Sign", upper, root)
    bpy.context.view_layer.update()
    return root, panel_transform


def validate_asset():
    root = bpy.data.objects.get("warehouse_sign")
    assert root is not None, "Missing warehouse_sign root"
    assert root.get("asset_contract") == "plushpanic_outdoor_v1"
    assert {o.name for o in root.children} == {"Keep", "Occlude_Sign"}
    meshes = []
    for part in root.children:
        assert part.type == "EMPTY"
        assert len(part.children) == 1
        mesh = part.children[0]
        assert mesh.type == "MESH" and mesh.name == part.name + "_Mesh"
        meshes.append(mesh)
    points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    low = [min(v[i] for v in points) for i in range(3)]
    high = [max(v[i] for v in points) for i in range(3)]
    assert abs(low[2]) < 1e-5, "Ground pivot must be z=0"
    assert abs(high[2] - low[2] - 7.0) <= 7.0 * 0.03
    assert abs(high[0] - low[0] - 6.0) <= 6.0 * 0.03
    triangles = 0
    for mesh in meshes:
        mesh.data.calc_loop_triangles()
        triangles += len(mesh.data.loop_triangles)
        assert not mesh.modifiers and not mesh.rigid_body
    assert triangles < 2500, f"Triangle budget exceeded: {triangles}"
    materials = {m.name for o in meshes for m in o.data.materials}
    assert materials == {"Ochre", "Plaster", "Hazard", "Metal", "Concrete", "Lamp"}
    assert len(materials) <= 6
    keep = bpy.data.objects["Keep_Mesh"]
    assert max((keep.matrix_world @ v.co).z for v in keep.data.vertices) <= 2.40001
    assert root.location.length < 1e-6 and root.rotation_euler.to_matrix() == Matrix.Identity(3)
    assert all(o.type in {"EMPTY", "MESH"} for o in root.children_recursive)
    for name in materials:
        mat = bpy.data.materials[name]
        assert not any(n.type == "TEX_IMAGE" for n in mat.node_tree.nodes)
    # Measure the actual joined board, not merely its requested transform.
    sign = bpy.data.objects["Occlude_Sign_Mesh"]
    panel_group = sign.vertex_groups["Panel"].index
    panel_points = [sign.matrix_world @ v.co for v in sign.data.vertices
                    if any(g.group == panel_group for g in v.groups)]
    assert abs(min(v.z for v in panel_points) - 4.2) < 1e-5
    untilted = [TILT.inverted() @ v for v in panel_points]
    for axis, wanted in enumerate((6.0, 0.3, 2.6)):
        actual = max(v[axis] for v in untilted) - min(v[axis] for v in untilted)
        assert abs(actual - wanted) < 1e-4, (axis, actual, wanted)
    panel_faces = [p for p in sign.data.polygons
                   if sign.data.materials[p.material_index].name == "Ochre" and p.area > 5]
    front = min(panel_faces, key=lambda p: p.normal.y)
    expected = Vector((0, -math.cos(math.radians(20)), math.sin(math.radians(20))))
    assert front.normal.dot(expected) > 0.99999, "Panel front must face -Y and 20 degrees upward"
    return low, high, triangles


def render_checks(root, mats):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.22, 0.26, 0.32, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.5
    # Studio objects are outside the asset hierarchy and never exported.
    studio = bpy.data.collections.new("Check render studio - not exported")
    scene.collection.children.link(studio)
    def to_studio(obj):
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        studio.objects.link(obj)
    ground = box("Studio ground", (200, 200, 0.1), (0, 0, -0.055), mats["Concrete"], 0)
    to_studio(ground)
    for name, pos, power, size in (("Key", (-6, -8, 13), 1800, 7),
                                    ("Fill", (7, -4, 9), 1000, 6),
                                    ("Rim", (2, 6, 12), 1500, 5)):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size = power, "DISK", size
        obj = bpy.data.objects.new(name, data)
        studio.objects.link(obj)
        obj.location = pos
        obj.rotation_euler = (Vector((0, 0, 4)) - obj.location).to_track_quat("-Z", "Y").to_euler()
    data = bpy.data.cameras.new("Check camera")
    camera = bpy.data.objects.new("Check camera", data)
    studio.objects.link(camera)
    scene.camera = camera
    target = Vector((0, 0, 3.5))
    pitch, azimuth = math.radians(60), math.radians(-30)
    direction = Vector((math.sin(azimuth) * math.cos(pitch),
                        -math.cos(azimuth) * math.cos(pitch), math.sin(pitch)))
    camera.location = target + direction * 22
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "PERSP"
    data.lens_unit = "FOV"
    data.angle = math.radians(34)
    scene.render.filepath = str(RENDERS / "warehouse_sign.png")
    bpy.ops.render.render(write_still=True)
    camera.location = (0, -22, 3.5)
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "ORTHO"
    data.ortho_scale = 8.7
    ground.hide_render = True
    scene.render.filepath = str(RENDERS / "warehouse_sign_front.png")
    bpy.ops.render.render(write_still=True)
    ground.hide_render = False
    # Store the source with the principal 60-degree camera selected.
    camera.location = target + direction * 22
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.type = "PERSP"
    scene.render.filepath = str(RENDERS / "warehouse_sign.png")


def main():
    root, panel_transform = build_asset()
    low, high, triangles = validate_asset()
    for directory in (ASSETS, SOURCE.parent, RENDERS):
        directory.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *root.children_recursive]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(ASSETS / "warehouse_sign.glb"),
                              export_format="GLB", use_selection=True,
                              export_yup=True, export_extras=True,
                              export_cameras=False, export_lights=False,
                              export_animations=False)
    center = panel_transform @ Vector((0, -0.15, 0))
    game_center = [round(center.x, 4), round(center.z, 4), round(-center.y, 4)]
    asset = {
        "id": "warehouse_sign", "file": "warehouse_sign.glb",
        "name": "Warehouse guide sign", "category": "Large stage kit",
        "note": ("6 x 2.6 x 0.3 m panel, bottom z=4.2 m; 7 m rear masts. "
                 "Front normal faces Blender -Y and 20 degrees upward; arrow points +X. "
                 "Three stacked boxes, 22 bulbs (diameter 0.18 m). "
                 f"Panel front center (game local XYZ): {game_center}. "
                 "Front outward normal (game local): [0, 0.342020, 0.939693]; "
                 "place the game's warm OmniLight outward from this surface. "
                 "No model lights or physics. Box collision covers the two legs, their feet and the cross braces between them (5.3 m wide, 2.4 m tall); "
                 "the overhead panel must not block movement."),
        "dimensions_m": [round(high[i] - low[i], 4) for i in (0, 2, 1)],
        "bounds_min_y": round(low[2], 6), "triangles": triangles,
        "collision": {"kind": "box", "size": [5.3, 2.4, 1.2], "center": [0, 1.2, 0]},
        "occlusion_parts": ["Occlude_Sign"]}
    manifest = {"revision": 1, "contract": "plushpanic_outdoor_v1", "units": "metres",
                "up_axis": "+Y", "assets": [asset]}
    (ASSETS / "manifest_guide.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    render_checks(root, {name: bpy.data.materials[name] for name in PALETTE})
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    validate_asset()
    for path in (ASSETS / "warehouse_sign.glb", ASSETS / "manifest_guide.json", SOURCE,
                 RENDERS / "warehouse_sign.png", RENDERS / "warehouse_sign_front.png"):
        assert path.is_file() and path.stat().st_size > 0, f"Missing output: {path}"
    print("GUIDE_LANDMARK_BUILT 1", flush=True)


if __name__ == "__main__":
    main()
