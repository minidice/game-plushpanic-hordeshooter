"""Sculpt the fitted goose without changing its nodes, UVs, face mask, or palette."""
import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'game/assets/characters/goose/goose-fitted.glb'
TARGET = ROOT / 'game/assets/characters/goose/goose-clean.glb'
OUT = ROOT / 'prototypes/grounded-art/output/goose-clean'
OUT.mkdir(parents=True, exist_ok=True)
IMMUTABLE = [p for p in (ROOT / 'game/assets/characters').glob('*/*.glb') if p != TARGET]
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in IMMUTABLE}
(OUT / 'immutable-before.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
scene = bpy.context.scene
root = bpy.data.objects['goose_root']
gear = ('goose_beret', 'goose_goggles', 'goose_headset', 'goose_helmet',
        'goose_pack', 'goose_vest', 'goose_vestPadded')

world = bpy.data.worlds.new('Review world')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (.72, .77, .83, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = .6
camera_data = bpy.data.cameras.new('Review camera')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 2.75
camera = bpy.data.objects.new('Review camera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
light_data = bpy.data.lights.new('Review key', 'AREA')
light_data.energy = 450
light_data.shape = 'DISK'
light_data.size = 5
light = bpy.data.objects.new('Review key', light_data)
scene.collection.objects.link(light)
light.location = (-3, -4, 6)
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 480
scene.render.resolution_y = 480
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
neutral = bpy.data.materials.new('Review neutral')
neutral.diffuse_color = (.57, .59, .60, 1)
neutral.use_nodes = True
neutral.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.57, .59, .60, 1)
shadow = bpy.data.materials.new('Review silhouette')
shadow.diffuse_color = (.035, .039, .043, 1)
shadow.use_nodes = True
shadow.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.035, .039, .043, 1)


def render_set(label):
    for mode in ('silhouette', 'neutral', 'color', 'gear'):
        scene.view_layers[0].material_override = shadow if mode == 'silhouette' else (neutral if mode == 'neutral' else None)
        for name in gear:
            for part in (bpy.data.objects[name], *bpy.data.objects[name].children_recursive):
                part.hide_render = mode != 'gear' or name not in ('goose_helmet', 'goose_vest', 'goose_pack')
        for part in (bpy.data.objects['goose_weapon'], *bpy.data.objects['goose_weapon'].children_recursive):
            part.hide_render = True
        for angle, view in ((0, 'front'), (math.radians(35), 'quarter'), (math.radians(90), 'side')):
            camera.location = (3.4 * math.sin(angle), -3.4 * math.cos(angle), 2.35)
            camera.rotation_euler = (Vector((0, 0, 1.03)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
            scene.render.filepath = str(OUT / f'{label}-{mode}-{view}.png')
            bpy.ops.render.render(write_still=True)
    scene.view_layers[0].material_override = None
    for name in gear:
        for part in (bpy.data.objects[name], *bpy.data.objects[name].children_recursive):
            part.hide_render = False
    for part in (bpy.data.objects['goose_weapon'], *bpy.data.objects['goose_weapon'].children_recursive):
        part.hide_render = False


render_set('before')


def sculpt(name, operation):
    obj = bpy.data.objects[name]
    inverse = obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        world_point = obj.matrix_world @ vertex.co
        vertex.co = inverse @ Vector(operation(world_point))
    obj.data.update()


# Keep all authored beak layers and style masks while making a slimmer goose bill.
sculpt('goose_beak_mesh', lambda p: (p.x * (.79 if p.y < -.42 else .85),
                                    -.299 + (p.y + .299) * .94, p.z))

# The visible cornea and eyelid shrink together; retaining their two channels
# keeps the eye palette and existing eye spacing controls intact.
for side, sign in (('left', -1), ('right', 1)):
    sculpt(f'goose_{side}Eye_mesh',
           lambda p, s=sign: (s * .225 + (p.x - s * .225) * .79,
                              -.322 + (p.y + .322) * .56,
                              1.525 + (p.z - 1.525) * .79))

    # Existing feather topology is already one continuous wing. Bring the
    # distal feather fan forward and slightly out, while narrowing its end.
    def shape_wing(p, s=sign):
        tip = max(0.0, min(1.0, (1.138 - p.z) / .408))
        x = s * (.43 + (abs(p.x) - .43) * (1.04 - .39 * tip) + .035 * tip)
        return (x, -.17 + (p.y + .17) * (1.0 - .25 * tip) - .046 * tip, p.z)
    sculpt(f'goose_{side}Arm_mesh', shape_wing)

    # Flatten the upper web and taper the rear ankle; preserve both foot
    # materials and the authored sole's height relative to the foot pivot.
    def shape_foot(p, s=sign):
        ankle = max(0.0, min(1.0, (p.y + .10) / .15))
        return (s * .2 + (p.x - s * .2) * (1 - .20 * ankle),
                p.y - .025 * max(0.0, 1 - ankle),
                .016 + (p.z - .016) * (1.0 - .30 * (1.0 - ankle) if .055 < p.z < .15 else 1.0))
    sculpt(f'goose_{side}Foot_mesh', shape_foot)

bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for child in root.children_recursive:
    child.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.gltf(filepath=str(TARGET), export_format='GLB', use_selection=True,
                          export_apply=False, export_extras=True)
render_set('after')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'goose-clean.blend'))
assert {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in IMMUTABLE} == hashes
print('GOOSE_BUILT', TARGET, flush=True)
