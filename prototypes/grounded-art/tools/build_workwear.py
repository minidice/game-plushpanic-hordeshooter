"""Offline workwear candidate; writes only inside prototypes/grounded-art.

Uses the field-kit Blender studio as a read-only comparison source.
Run with Blender --background --python-exit-code 1 --python this_file.py.
"""
import bpy
import math
import json
import hashlib
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
OUT = ROOT / 'output/workwear'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = REPO / 'game/assets/characters/goose/goose.glb'
FIELD = ROOT / 'source/goose-field-kit.blend'
HASHES = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (SOURCE, FIELD)}
bpy.ops.wm.open_mainfile(filepath=str(FIELD))
scene = bpy.context.scene
original = [o for o in scene.objects if o.name.startswith('reference_')]
field = [o for o in scene.objects if not o.name.startswith(('reference_', 'STUDIO_'))]
for ob in field:
    ob.name = 'field_' + ob.name
before = set(scene.objects)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
candidate = list(set(scene.objects) - before)


def descendants(ob):
    return [ob] + [child for c in ob.children for child in descendants(c)]


def digest(ob):
    return hashlib.sha256(repr(([tuple(v.co) for v in ob.data.vertices],
                               [tuple(p.vertices) for p in ob.data.polygons],
                               list(ob.matrix_world))).encode()).hexdigest()


for name in ('goose_helmet', 'goose_vest', 'goose_pack', 'goose_beret',
             'goose_crestShort', 'goose_crestSwept', 'goose_goggles',
             'goose_headset', 'goose_vestPadded', 'goose_weapon'):
    for ob in list(descendants(bpy.data.objects[name])):
        if ob.type == 'MESH':
            candidate.remove(ob)
            bpy.data.objects.remove(ob, do_unlink=True)
preserved = {ob.name: digest(ob) for ob in candidate if ob.type == 'MESH'}
for name, value in preserved.items():
    assert value == digest(bpy.data.objects['reference_' + name]), name
roots = {k: bpy.data.objects[k] for k in ('goose_helmet', 'goose_vest')}
created = []


def material(name, color, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    def linear(v):
        return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4
    rgba = tuple(linear(int(color[i:i+2], 16) / 255) for i in (0, 2, 4)) + (1,)
    mat.diffuse_color = rgba
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = rgba
    p.inputs['Roughness'].default_value = roughness
    return mat


M = {'cloth': material('workwear_canvas_blue', '456b78', .92),
     'binding': material('workwear_bound_edge', '304c56', .94),
     'pocket': material('workwear_pocket_blue', '527a85', .92),
     'hat': material('workwear_ochre_canvas', 'b28a42', .91),
     'hatband': material('workwear_hatband', '7a623b', .94),
     'hardware': material('workwear_zip_hardware', 'a69f88', .55)}


def mesh(name, verts, faces, group, mat, smooth=True):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    ob.parent = roots[group]
    ob.matrix_world.identity()
    ob.data.materials.append(M[mat])
    for p in data.polygons:
        p.use_smooth = smooth
    created.append(ob)
    candidate.append(ob)
    return ob


def solidify(ob, thickness):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new('Fabric thickness', 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 0
    bpy.ops.object.modifier_apply(modifier=mod.name)


def line(name, points, radius, group, mat, closed=False):
    # Small sewn bindings, not round shoulder ropes.
    n, verts, faces = 6, [], []
    for i, point in enumerate(points):
        p = Vector(point)
        a = Vector(points[(i+1) % len(points)] if closed else points[min(i+1, len(points)-1)])
        b = Vector(points[(i-1) % len(points)] if closed else points[max(i-1, 0)])
        tangent = (a-b).normalized()
        u = tangent.cross(Vector((0, 0, 1)))
        if u.length < .01:
            u = tangent.cross(Vector((0, 1, 0)))
        u.normalize()
        v = tangent.cross(u).normalized()
        verts.extend(p + radius * (math.cos(j*math.tau/n)*u + math.sin(j*math.tau/n)*v) for j in range(n))
    for i in range(len(points) if closed else len(points)-1):
        for j in range(n):
            faces.append((i*n+j, i*n+(j+1)%n, ((i+1)%len(points))*n+(j+1)%n, ((i+1)%len(points))*n+j))
    if not closed:
        faces.extend((tuple(reversed(range(n))), tuple((len(points)-1)*n+j for j in range(n))))
    return mesh(name, verts, faces, group, mat)


# Fit the cloth to the actual existing torso, rather than a guessed ellipsoid.
body = bpy.data.objects['goose_torso_mesh']
bvh = BVHTree.FromPolygons([body.matrix_world @ v.co for v in body.data.vertices],
                         [p.vertices[:] for p in body.data.polygons])


def surface(theta, z, ease=.018):
    direction = Vector((math.sin(theta), -math.cos(theta), 0))
    hit, _, _, _ = bvh.ray_cast(Vector((0, 0, z)), direction)
    assert hit is not None, ('Torso fit missed', theta, z)
    return hit + direction*ease


def top(theta):
    # A shallow V at the front becomes a continuous shoulder and back neckline.
    a = abs(theta)
    knots = [(0, 1.20), (.35, 1.27), (.72, 1.35), (1.10, 1.39),
             (1.57, 1.41), (2.43, 1.39), (math.pi, 1.39)]
    for (x0, z0), (x1, z1) in zip(knots, knots[1:]):
        if x0 <= a <= x1:
            t = (a-x0)/(x1-x0)
            t = t*t*(3-2*t)
            return z0+(z1-z0)*t
    return knots[-1][1]


N, ROWS = 128, 7
verts, faces = [], []


def opening(theta):
    distance = (abs(theta)-1.48)/.59
    height = math.sqrt(max(0, 1-distance*distance))
    return 1.055-.245*height, 1.055+.22*height


# Two cloth regions meet everywhere except at the oval arm openings.
# Welding their shared boundary makes the shoulder part of the same cloth shell.
for region in (0, 1):
    start = len(verts)
    for j in range(ROWS+1):
        for i in range(N):
            theta = -math.pi+i*math.tau/N
            low, high = opening(theta)
            bottom = .58+.07*max(0, -math.cos(theta))**8
            z0, z1 = (bottom, low) if region == 0 else (high, top(theta))
            verts.append(surface(theta, z0+(z1-z0)*j/ROWS))
    for j in range(ROWS):
        for i in range(N):
            a = start+j*N+i
            b = start+j*N+(i+1)%N
            faces.append((a, b, b+N, a+N))
vest = mesh('workwear_vest_shell', verts, faces, 'goose_vest', 'cloth')
bpy.ops.object.select_all(action='DESELECT')
vest.select_set(True)
bpy.context.view_layer.objects.active = vest
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=.00001)
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
solidify(vest, .012)
line('workwear_neck_binding', verts[-N:], .006, 'goose_vest', 'binding', True)
line('workwear_hem_binding', verts[:N], .007, 'goose_vest', 'binding', True)
for side in (-1, 1):
    # Exact oval border sampled independently of the surface grid.
    points = []
    for i in range(64):
        angle = i*math.tau/64
        theta = side*(1.48+.59*math.cos(angle))
        z = 1.055+(.22 if math.sin(angle) >= 0 else .245)*math.sin(angle)
        points.append(surface(theta, z, .019))
    line('workwear_armhole_binding', points, .005, 'goose_vest', 'binding', True)


def patch(name, center_angle, z0, z1, half_angle, ease, mat):
    rows, cols = 8, 12
    vs, fs = [], []
    for j in range(rows+1):
        for i in range(cols+1):
            # Rounded lower corners, kept subtle at gameplay size.
            width = half_angle*(.88 + .12*min(1, j/2))
            angle = center_angle + width*(2*i/cols-1)
            z = z0+(z1-z0)*j/rows
            vs.append(surface(angle, z, ease))
    for j in range(rows):
        for i in range(cols):
            a = j*(cols+1)+i
            fs.append((a, a+1, a+cols+2, a+cols+1))
    ob = mesh(name, vs, fs, 'goose_vest', mat)
    solidify(ob, .007)
    return vs[-cols-1:]


# Two shallow sewn pockets. No boxes, magazine flaps, webbing or backpack.
for side in (-1, 1):
    edge = patch('workwear_patch_pocket', side*.43, .69, .91, .23, .034, 'pocket')
    line('workwear_pocket_mouth', edge, .005, 'goose_vest', 'binding')
patch('workwear_front_placket', 0, .59, 1.20, .027, .028, 'binding')
line('workwear_zip', [surface(0, .62+i*.55/24, .038) for i in range(25)],
     .0035, 'goose_vest', 'hardware')
line('workwear_zip_pull', [surface(0, 1.145, .045), surface(0, 1.105, .049)],
     .008, 'goose_vest', 'hardware')

# Soft work cap: low crown, narrow front brim, no military mounts or goggles.
n, rings = 48, 12
vs, fs = [(0, 0, 2.01)], []
for j in range(1, rings+1):
    phi = (math.pi/2)*j/rings
    for i in range(n):
        theta = math.tau*i/n
        vs.append((.448*math.sin(phi)*math.cos(theta),
                   .385*math.sin(phi)*math.sin(theta),
                   1.72+.29*math.cos(phi)))
for i in range(n):
    fs.append((0, 1+i, 1+(i+1)%n))
for j in range(rings-1):
    for i in range(n):
        a = 1+j*n+i
        b = 1+j*n+(i+1)%n
        fs.append((a, a+n, b+n, b))
cap = mesh('workwear_cap_crown', vs, fs, 'goose_helmet', 'hat')
solidify(cap, .012)
bandverts, bandfaces = [], []
for z in (1.715, 1.753):
    for i in range(n):
        t = i*math.tau/n
        bandverts.append((.450*math.cos(t), .387*math.sin(t), z))
for i in range(n):
    bandfaces.append((i, (i+1)%n, (i+1)%n+n, i+n))
band = mesh('workwear_cap_band', bandverts, bandfaces, 'goose_helmet', 'hatband')
solidify(band, .006)
vs, fs = [], []
steps = 48
for j in range(5):
    for i in range(steps+1):
        t = -1.25+2.5*i/steps
        extend = .083*math.cos(t*math.pi/2.5)**2*j/4
        vs.append(((.448+extend*.25)*math.sin(t),
                   -(.385+extend)*math.cos(t), 1.724-.025*j/4))
for j in range(4):
    for i in range(steps):
        a = j*(steps+1)+i
        fs.append((a, a+1, a+steps+2, a+steps+1))
brim = mesh('workwear_cap_brim', vs, fs, 'goose_helmet', 'hat')
solidify(brim, .010)

# Subtle sewn panel seams distinguish a cloth cap from a hard helmet shell.
for theta in (-math.pi/2, math.pi/6, math.pi*5/6):
    points = []
    for j in range(24):
        phi = .10+(math.pi/2-.10)*j/23
        points.append((.456*math.sin(phi)*math.cos(theta),
                       .393*math.sin(phi)*math.sin(theta),
                       1.721+.297*math.cos(phi)))
    line('workwear_cap_panel_seam', points, .0018, 'goose_helmet', 'hatband')

# Consolidate each independently removable item and retain the existing pivots.
garments = []
for name, root in roots.items():
    items = [o for o in descendants(root) if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for ob in items:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = items[0]
    bpy.ops.object.join()
    ob = bpy.context.object
    ob.name = name + '_workwear_mesh'
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    garments.append(ob)
candidate = [o for o in scene.objects if not o.name.startswith(('reference_', 'field_', 'STUDIO_'))]
for name, value in preserved.items():
    assert digest(bpy.data.objects[name]) == value


def stats(objects):
    meshes = [o for o in objects if o.type == 'MESH']
    for o in meshes:
        o.data.calc_loop_triangles()
    pts = [o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    return {'triangles': sum(len(o.data.loop_triangles) for o in meshes),
            'mesh_objects': len(meshes),
            'materials': len({m.name for o in meshes for m in o.data.materials}),
            'bounds_min': [min(p[i] for p in pts) for i in range(3)],
            'bounds_max': [max(p[i] for p in pts) for i in range(3)]}


def export(filename, objects):
    path = ROOT/'assets'/filename
    assert path.resolve().is_relative_to(ROOT)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.hide_set(False)
        ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True,
                             export_yup=True, export_cameras=False, export_lights=False)


export('goose-workwear.glb', candidate)
gear = set(garments)
for ob in garments:
    p = ob.parent
    while p:
        gear.add(p)
        p = p.parent
export('goose-workwear-only.glb', gear)
manifest = {'status': 'offline review candidate; game not integrated', 'date': '2026-10-22',
            'protected_files_sha256': HASHES, 'unchanged_meshes': preserved,
            'candidate': stats(candidate), 'clothing': stats(garments),
            'parts': {o.name: stats([o]) for o in garments},
            'axes': 'Blender +Z up/-Y front; glTF +Y up/+Z front; metres',
            'clothing_nodes': list(roots),
            'design': 'Muted blue sleeveless work vest, ochre soft cap; no armor or pack.',
            'limitations': ['Idle fit only. Animation, body sliders, other species and Godot untested.',
                            'No armor layering or outfit selection system implemented.',
                            'Top-down preview is a Blender view, not a game screenshot.']}
(ROOT/'workwear-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


def show(objects):
    visible = set(objects)
    for ob in original+field+candidate:
        ob.hide_render = ob not in visible
        ob.hide_set(ob not in visible)


camera = scene.camera
def camera_view(position, scale):
    camera.location = position
    camera.data.ortho_scale = scale
    camera.rotation_euler = (Vector((0, 0, 1.02))-camera.location).to_track_quat('-Z', 'Y').to_euler()


scene.cycles.samples = 24
scene.render.resolution_x = 720
scene.render.resolution_y = 840
camera_view((4, -6, 3.2), 2.65)
show(candidate)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-workwear.blend'))
views = {'quarter': ((4, -6, 3.2), 2.65), 'front': ((0, -7, 2.4), 2.60),
         'side': ((7, 0, 2.4), 2.60), 'rear': ((0, 7, 2.4), 2.60),
         'top': ((4, -6, 11.3), 2.85), 'small': ((4, -6, 11.3), 12.5)}
preview = '--preview' in sys.argv
for view, (position, scale) in views.items():
    if preview and view not in ('quarter', 'top'):
        continue
    camera_view(position, scale)
    scene.render.resolution_y = 480 if view == 'small' else 840
    for label, objects in [('original', original), ('field', field), ('workwear', candidate)]:
        if preview and label != 'workwear':
            continue
        show(objects)
        scene.render.filepath = str(OUT/f'{label}-{view}.png')
        bpy.ops.render.render(write_still=True)
        print('RENDERED', label, view, flush=True)
for path, expected in HASHES.items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected
print('WORKWEAR_READY', json.dumps(manifest['clothing']), flush=True)
