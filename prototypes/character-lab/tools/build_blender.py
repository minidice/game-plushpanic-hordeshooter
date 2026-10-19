"""Run with Blender --background --python build_blender.py -- [optional.glb]."""
import bpy
import math
import sys
import json
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent.parent
OUT = HERE / 'output'
OUT.mkdir(exist_ok=True)
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
source = Path(args[0]).resolve() if args else OUT / 'characters-equipped.glb'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(source))
scene = bpy.context.scene
scene.render.fps = 24
scene.frame_start = 1
scene.frame_end = 192
scene.frame_set(1)

species = ['raccoon', 'redpanda', 'duck', 'goose']
limits = {'width': (.75, 1.3), 'height': (.75, 1.3), 'tail': (.5, 1.5), 'beak': (.6, 1.6), 'helmet': (.95, 1.2)}
descriptions = {'head': 'Head size / 머리 크기', 'width': 'Body width / 몸통 너비', 'height': 'Body height / 몸통 높이', 'tail': 'Tail size / 꼬리 크기', 'beak': 'Bill length / 부리 길이', 'neck': 'Goose neck length / 거위 목 길이', 'helmet': 'Helmet size / 헬멧 크기'}

def driver(obj, path, axis, control, prop, expression):
    fc = obj.driver_add(path, axis)
    d = fc.driver
    d.type = 'SCRIPTED'
    var = d.variables.new()
    var.name = 'v'
    var.type = 'SINGLE_PROP'
    var.targets[0].id = control
    var.targets[0].data_path = f'["{prop}"]'
    d.expression = expression

roots = []
for kind in species:
    root = bpy.data.objects.get(f'{kind}_root')
    if not root:
        continue
    roots.append(root)
    root.empty_display_type = 'CIRCLE'
    root.empty_display_size = .55
    for prop, (lo, hi) in limits.items():
        root[prop] = 1.0
        root.id_properties_ui(prop).update(min=lo, max=hi, soft_min=lo, soft_max=hi, description=descriptions[prop])
    # Multipliers on the imported snapshot, so exported custom proportions stay intact.
    for part, prop in [('tail', 'tail'), ('helmet', 'helmet')]:
        obj = bpy.data.objects.get(f'{kind}_{part}')
        if obj:
            for axis in range(3):
                driver(obj, 'scale', axis, root, prop, f'{obj.scale[axis]:.8f}*v')
    torso = bpy.data.objects.get(f'{kind}_torso')
    if torso:
        for axis in range(3):
            driver(torso, 'scale', axis, root, 'height' if axis == 2 else 'width', f'{torso.scale[axis]:.8f}*v')
    if kind in ('duck', 'goose'):
        beak = bpy.data.objects.get(f'{kind}_beak')
        if beak:
            driver(beak, 'scale', 1, root, 'beak', f'{beak.scale.y:.8f}*v')

def material(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*color, 1)
    bsdf.inputs['Roughness'].default_value = .85
    return m

stage_mat = material('Stage warm slate', (.16, .135, .10))
text_mat = material('Warm ivory', (.82, .86, .70))
for i, kind in enumerate(species):
    if not bpy.data.objects.get(f'{kind}_root'):
        continue
    x = (i - 1.5) * 2.6
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=1.18, depth=.06, location=(x, 0, -.055))
    obj = bpy.context.object
    obj.name = f'Stage_{kind}'
    obj.data.materials.append(stage_mat)
    bpy.ops.object.text_add(location=(x, -1.25, -.015), rotation=(0, 0, 0))
    txt = bpy.context.object
    txt.name = f'Label_{kind}'
    txt.data.body = kind.replace('redpanda', 'RED PANDA').upper()
    txt.data.align_x = 'CENTER'
    txt.data.size = .16
    txt.data.materials.append(text_mat)

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.10))
bpy.context.object.name = 'Studio_floor'
bpy.context.object.data.materials.append(material('Background', (.10, .085, .065)))

def aim(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

def make_camera(name, elevation, scale):
    angle = math.radians(elevation)
    bpy.ops.object.camera_add(location=(0, -math.cos(angle)*16, 1+math.sin(angle)*16))
    camera = bpy.context.object
    camera.name = name
    aim(camera, (0, 0, 1.0))
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = scale
    return camera
portrait_camera = make_camera('Portrait_12deg', 12, 11.5)
close_camera = make_camera('Quarter_Close_35deg', 35, 11.5)
gameplay_camera = make_camera('Gameplay_55deg_DEFAULT', 55, 36)
scene.camera = gameplay_camera
for name, loc, power, size, color in [
    ('Key', (-4, -4, 8), 2200, 7, (1, .88, .72)),
    ('Fill', (4, -1, 5), 900, 6, (.75, .89, 1)),
    ('Rim', (0, 4, 6), 2400, 6, (1, .82, .60)),
]:
    bpy.ops.object.light_add(type='AREA', location=loc)
    light = bpy.context.object
    light.name = name
    light.data.energy = power
    light.data.shape = 'DISK'
    light.data.size = size
    light.data.color = color
    aim(light, (0, 0, 1))

scene.world.color = (.14, .14, .14)
scene.render.engine = 'CYCLES'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 1600
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
bpy.ops.object.select_all(action='DESELECT')
if roots:
    roots[0].select_set(True)
    bpy.context.view_layer.objects.active = roots[0]
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'MATERIAL'
        if area.type == 'PROPERTIES':
            area.spaces.active.context = 'OBJECT'

readme = bpy.data.texts.new('READ_ME')
readme.write('CHARACTER LAB / v0.3 first art pass\n\n'
    'Browser editor is the primary proportion editor; its GLB export preserves your exact shape.\n'
    'Select raccoon_root / redpanda_root / duck_root / goose_root.\n'
    'Object Properties > Custom Properties contains relative scale multipliers.\n'
    'One continuous skin per animal; head is only a face attachment anchor.\n'
    'For upper-body profile changes, use the browser editor.\n'
    'Space plays the 8-direction walk. Imported NLA tracks include Strafe_Aim, Recoil, Hit_Fall.\n'
    'Tumble_Squash includes visual scale animation. Camera defaults to Gameplay_55deg_DEFAULT.\n'
    'All parts inherit a shared deform pivot. Root physics metadata describes a single capsule collider.\n'
    'The browser physics mode runs Rapier; these GLB clips are authored motions, not that physics simulation.\n'
    'The flash visibility is preview-only and is not included in exported animation.\n')

stem = 'character-comparison' if not args else source.stem
blend = OUT / f'{stem}.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
scene.camera = portrait_camera
scene.frame_set(25)
scene.render.filepath = str(OUT / f'{stem}.png')
bpy.ops.render.render(write_still=True)
scene.camera = gameplay_camera
scene.frame_set(1)
scene.render.filepath = str(OUT / f'{stem}-gameplay.png')
bpy.ops.render.render(write_still=True)
report = {'blender': bpy.app.version_string, 'file': str(blend), 'characters': len(roots), 'meshes': sum(o.type == 'MESH' for o in scene.objects), 'actions': [a.name for a in bpy.data.actions], 'drivers': sum(len(o.animation_data.drivers) for o in scene.objects if o.animation_data), 'render': scene.render.filepath}
assert len(roots) == 4, report
assert len(bpy.data.actions) >= 5, report
assert sum(o.name.endswith('_continuous_skin') for o in scene.objects) == 4
for obj in scene.objects:
    if obj.type == 'MESH':
        assert all(math.isfinite(v) for vertex in obj.data.vertices for v in vertex.co), obj.name
(OUT / 'blender-verification.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('CHARACTER_LAB_VERIFIED ' + json.dumps(report))
