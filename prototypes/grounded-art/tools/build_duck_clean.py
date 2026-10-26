"""Build the clean duck from the immutable fitted GLB with Blender 5.2."""
import bpy
import hashlib
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'game/assets/characters/duck/duck-fitted.glb'
TARGET = ROOT / 'game/assets/characters/duck/duck-clean.glb'
OUT = ROOT / 'prototypes/grounded-art/output/duck-clean'
OUT.mkdir(parents=True, exist_ok=True)
immutable = [p for p in (ROOT / 'game/assets/characters').glob('*/*.glb') if p != TARGET]
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in immutable}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
scene = bpy.context.scene
root = bpy.data.objects['duck_root']
gear = ('duck_helmet', 'duck_beret', 'duck_goggles', 'duck_headset',
        'duck_vest', 'duck_vestPadded', 'duck_pack')


def review_setup():
    world = bpy.data.worlds.new('Duck review world')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.72, .77, .83, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .6
    camera_data = bpy.data.cameras.new('Duck review camera')
    camera = bpy.data.objects.new('Duck review camera', camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 2.75
    scene.camera = camera
    light_data = bpy.data.lights.new('Duck review key', 'AREA')
    light = bpy.data.objects.new('Duck review key', light_data)
    scene.collection.objects.link(light)
    light.location = (-3, -4, 6)
    light_data.energy = 450
    light_data.shape = 'DISK'
    light_data.size = 5
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    neutral = bpy.data.materials.new('Duck review neutral')
    neutral.diffuse_color = (.57, .59, .60, 1)
    neutral.use_nodes = True
    neutral.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.57, .59, .60, 1)
    shadow = bpy.data.materials.new('Duck review silhouette')
    shadow.diffuse_color = (.035, .039, .043, 1)
    shadow.use_nodes = True
    shadow.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.035, .039, .043, 1)
    return camera, neutral, shadow


camera, neutral, shadow = review_setup()


def render_set(label):
    for mode in ('silhouette', 'neutral', 'color', 'gear'):
        scene.view_layers[0].material_override = shadow if mode == 'silhouette' else (neutral if mode == 'neutral' else None)
        for name in gear:
            show = mode == 'gear' and name in ('duck_helmet', 'duck_vest', 'duck_pack')
            obj = bpy.data.objects[name]
            for part in (obj, *obj.children_recursive):
                part.hide_render = not show
        obj = bpy.data.objects['duck_weapon']
        for part in (obj, *obj.children_recursive):
            part.hide_render = True
        for angle, view in ((0, 'front'), (math.radians(35), 'quarter'), (math.radians(90), 'side')):
            camera.location = (3.4 * math.sin(angle), -3.4 * math.cos(angle), 2.35)
            direction = Vector((0, 0, 1.03)) - camera.location
            camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(OUT / f'{label}-{mode}-{view}.png')
            bpy.ops.render.render(write_still=True)
    scene.view_layers[0].material_override = None
    for name in gear:
        obj = bpy.data.objects[name]
        for part in (obj, *obj.children_recursive):
            part.hide_render = False
    obj = bpy.data.objects['duck_weapon']
    for part in (obj, *obj.children_recursive):
        part.hide_render = False


render_set('before')


def reshape(name, adjust):
    obj = bpy.data.objects[name]
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        vertex.co = inverse @ Vector(adjust(obj.matrix_world @ vertex.co))
    obj.data.update()


# Keep the torso, local face/body mask, custom material channels, pivots and gear
# untouched. The short body is already authored; the wider wing line emphasizes it.
def bill(point):
    x, y, z = point
    front = max(0.0, min(1.0, (-y - .304) / .380))
    x *= 1.13 - .05 * front
    y = -.304 + (y + .304) * .91
    z = 1.260 + (z - 1.260) * .73 + .014 * (1 - front)
    return x, y, z


reshape('duck_beak_mesh', bill)

for side in ('left', 'right'):
    eye = bpy.data.objects[f'duck_{side}Eye_mesh']
    center = bpy.data.objects[f'duck_{side}Eye'].matrix_world.translation.copy()

    def smaller_eye(point):
        return center + (point - center) * Vector((.76, .74, .79))

    reshape(eye.name, smaller_eye)
    brow = bpy.data.objects[f'duck_{side}Brow_mesh']
    brow_center = bpy.data.objects[f'duck_{side}Brow'].matrix_world.translation.copy()

    def shallower_brow(point):
        return brow_center + (point - brow_center) * Vector((.88, .60, .70))

    reshape(brow.name, shallower_brow)

    sign = -1 if side == 'left' else 1

    def wing(point):
        x, y, z = point
        # A broad shoulder narrows into the existing few feather tips. The same
        # closed surface remains the runtime deformation/grip source.
        weight = max(0.0, min(1.0, (1.14 - z) / .41))
        x = sign * (.43 + (abs(x) - .43) * (1.19 + .35 * weight))
        y += -.018 * weight
        return x, y, z

    reshape(f'duck_{side}Arm_mesh', wing)


def replace_webbed_foot(side, sign):
    name = f'duck_{side}Foot_mesh'
    obj = bpy.data.objects[name]
    old = obj.data
    mesh = bpy.data.meshes.new(name + '_clean')
    for material in old.materials:
        mesh.materials.append(material)
    x0 = sign * .20
    count = 24
    # Flattened web at ground and a tapered ankle. The front outline carries
    # three shallow lobes, all connected to the same closed mesh.
    rings = ((0.000, -.080, .145, .188),
             (.024, -.080, .151, .190),
             (.043, -.073, .145, .177),
             (.067, -.041, .062, .073),
             (.140, -.041, .055, .055),
             (.235, -.041, .051, .049))
    vertices = []
    for z, yc, rx, ry in rings:
        for k in range(count):
            angle = math.tau * k / count
            c, s = math.cos(angle), math.sin(angle)
            front = max(0.0, -s)
            lobes = .046 * math.cos(3 * math.pi * c) * front * (1 if z <= .043 else 0)
            vertices.append((x0 + rx * c, yc + (ry + lobes) * s, z))
    faces = [tuple(reversed(tuple(range(count))))]
    material_ids = [1]
    for ring in range(len(rings) - 1):
        for k in range(count):
            j = (k + 1) % count
            faces.append((ring * count + k, ring * count + j,
                          (ring + 1) * count + j, (ring + 1) * count + k))
            material_ids.append(1 if ring == 0 else 0)
    faces.append(tuple((len(rings) - 1) * count + k for k in range(count)))
    material_ids.append(0)
    inverse = obj.matrix_world.inverted()
    mesh.from_pydata([inverse @ Vector(v) for v in vertices], [], faces)
    mesh.update()
    for face, material_id in zip(mesh.polygons, material_ids):
        face.material_index = material_id
        face.use_smooth = material_id == 0
    obj.data = mesh
    if old.users == 0:
        bpy.data.meshes.remove(old)


for side, sign in (('left', -1), ('right', 1)):
    replace_webbed_foot(side, sign)

bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.gltf(filepath=str(TARGET), export_format='GLB', use_selection=True,
                          export_apply=False, export_extras=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(TARGET))
scene = bpy.context.scene
camera, neutral, shadow = review_setup()
render_set('after')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'duck-clean.blend'))
assert {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in immutable} == hashes, 'immutable player GLB changed'
print('BUILT', TARGET, flush=True)
