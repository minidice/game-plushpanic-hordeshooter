"""Separate wraparound armor revision; existing game and candidates are read-only."""
import bpy
import math
import json
import hashlib
import sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'output/wrap-armor'
OUT.mkdir(parents=True, exist_ok=True)
protected = [ROOT/'source/goose-field-kit.blend', ROOT/'assets/goose-field-kit.glb',
             ROOT/'source/goose-workwear.blend', ROOT.parents[1]/'game/assets/characters/goose/goose.glb']
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(protected[0]))
scene = bpy.context.scene
baseline = [o for o in scene.objects if not o.name.startswith(('reference_', 'STUDIO_'))]
for ob in baseline:
    ob.name = 'before_'+ob.name
copies = {}
for ob in baseline:
    new = ob.copy()
    if ob.type == 'MESH':
        new.data = ob.data.copy()
    scene.collection.objects.link(new)
    new.name = ob.name.removeprefix('before_')
    copies[ob] = new
for old, new in copies.items():
    new.parent = copies.get(old.parent)
    new.matrix_parent_inverse = old.matrix_parent_inverse.copy()
    new.matrix_basis = old.matrix_basis.copy()
    new.hide_set(False)
bpy.context.view_layer.update()
candidate = list(copies.values())


def digest(ob):
    return hashlib.sha256(repr(([tuple(v.co) for v in ob.data.vertices],
                               [tuple(p.vertices) for p in ob.data.polygons],
                               list(ob.matrix_world))).encode()).hexdigest()


unchanged = {o.name: digest(o) for o in candidate if o.type == 'MESH' and o.name != 'goose_vest_field_mesh'}
for name, expected in unchanged.items():
    original_ob, copied_ob = bpy.data.objects['before_'+name], bpy.data.objects[name]
    assert [tuple(v.co) for v in original_ob.data.vertices] == [tuple(v.co) for v in copied_ob.data.vertices], name
    assert [tuple(p.vertices) for p in original_ob.data.polygons] == [tuple(p.vertices) for p in copied_ob.data.polygons], name
    matrix_error = max(abs(original_ob.matrix_world[r][c]-copied_ob.matrix_world[r][c]) for r in range(4) for c in range(4))
    assert matrix_error < 1e-6, (name, matrix_error)
oldvest = bpy.data.objects['goose_vest_field_mesh']
M = {m.name: m for m in oldvest.data.materials}
cloth = M['custom_vest_field_canvas']
webbing = M['field_webbing']
binding = M['field_binding']
rubber = M['field_rubber']
candidate.remove(oldvest)
bpy.data.objects.remove(oldvest, do_unlink=True)
parent = bpy.data.objects['goose_vest']
parts = []


def mesh(name, vs, fs, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vs, [], fs)
    data.update()
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    ob.parent = parent
    ob.matrix_world = Matrix.Identity(4)
    data.materials.append(mat)
    for p in data.polygons:
        p.use_smooth = True
    parts.append(ob)
    return ob


def solid(ob, thickness):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new('Panel thickness', 'SOLIDIFY')
    mod.thickness = thickness
    mod.offset = 0
    bpy.ops.object.modifier_apply(modifier=mod.name)


def seam(name, points, radius, mat, closed=False):
    vs, fs, n = [], [], 6
    for i, p in enumerate(points):
        p = Vector(p)
        a = Vector(points[(i+1)%len(points)] if closed else points[min(i+1, len(points)-1)])
        b = Vector(points[(i-1)%len(points)] if closed else points[max(i-1, 0)])
        t = (a-b).normalized()
        u = t.cross(Vector((0, 0, 1)))
        if u.length < .01:
            u = t.cross(Vector((0, 1, 0)))
        u.normalize()
        v = t.cross(u).normalized()
        vs.extend(p+radius*(math.cos(j*math.tau/n)*u+math.sin(j*math.tau/n)*v) for j in range(n))
    for i in range(len(points) if closed else len(points)-1):
        for j in range(n):
            fs.append((i*n+j, i*n+(j+1)%n, ((i+1)%len(points))*n+(j+1)%n, ((i+1)%len(points))*n+j))
    if not closed:
        fs.extend((tuple(reversed(range(n))), tuple((len(points)-1)*n+j for j in range(n))))
    return mesh(name, vs, fs, mat)


def box(name, pos, size, mat, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    ob = bpy.context.object
    ob.name = name
    ob.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.rotation_euler.z = rotation
    world = ob.matrix_world.copy()
    ob.parent = parent
    ob.matrix_world = world
    ob.data.materials.append(mat)
    bevel = ob.modifiers.new('Rounded manufactured edges', 'BEVEL')
    bevel.width = min(.014, min(size)*.35)
    bevel.segments = 3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for p in ob.data.polygons:
        p.use_smooth = True
    normals = ob.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=normals.name)
    parts.append(ob)
    return ob


body = bpy.data.objects['goose_torso_mesh']
bvh = BVHTree.FromPolygons([body.matrix_world@v.co for v in body.data.vertices], [p.vertices[:] for p in body.data.polygons])
def surface(t, z, gap=.033):
    direction = Vector((math.sin(t), -math.cos(t), 0))
    hit, _, _, _ = bvh.ray_cast(Vector((0, 0, z)), direction)
    assert hit is not None
    return hit+gap*direction


def neckline(t):
    # Wide, flat shoulder panels; original helmet and backpack unchanged.
    return 1.275+.075*math.sin(abs(t))**2


def hole(t):
    # Local opening around the forward-set wing, retaining broad rear side coverage.
    distance = (abs(t)-1.16)/.59
    height = math.sqrt(max(0, 1-distance*distance))
    return .91-.26*height, .91+.27*height


N, ROWS = 128, 6
vs, fs = [], []
for region in (0, 1):
    start = len(vs)
    for j in range(ROWS+1):
        for i in range(N):
            t = -math.pi+i*math.tau/N
            low, high = hole(t)
            z0, z1 = (.615, low) if region == 0 else (high, neckline(t))
            vs.append(surface(t, z0+(z1-z0)*j/ROWS))
    for j in range(ROWS):
        for i in range(N):
            a, b = start+j*N+i, start+j*N+(i+1)%N
            fs.append((a, b, b+N, a+N))
shell = mesh('armor_wrap_shell', vs, fs, cloth)
bpy.ops.object.select_all(action='DESELECT')
shell.select_set(True)
bpy.context.view_layer.objects.active = shell
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=.00001)
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
solid(shell, .024)
seam('armor_neck_binding', vs[-N:], .007, webbing, True)
seam('armor_bottom_binding', vs[:N], .008, webbing, True)
for side in (-1, 1):
    points = []
    for i in range(64):
        a = i*math.tau/64
        points.append(surface(side*(1.16+.59*math.cos(a)), .91+(.27 if math.sin(a)>=0 else .26)*math.sin(a), .034))
    seam('armor_wing_opening', points, .006, webbing, True)
    # Broad flat side cinch overlays the new armor panel behind the wing.
    band_v, band_f = [], []
    for i in range(25):
        t = side*(1.83+.82*i/24)
        for z in (.78, .88):
            band_v.append(surface(t, z, .052))
    for i in range(24):
        band_f.append((i*2, i*2+1, i*2+3, i*2+2))
    solid(mesh('armor_side_cinch', band_v, band_f, webbing), .008)
    for z in (.99, 1.075):
        # Side webbing stops behind the wing opening.
        band_v, band_f = [], []
        for i in range(19):
            t = side*(1.82+.80*i/18)
            for dz in (-.015, .015):
                band_v.append(surface(t, z+dz, .050))
        for i in range(18):
            band_f.append((i*2, i*2+1, i*2+3, i*2+2))
        solid(mesh('armor_side_webbing', band_v, band_f, binding), .006)

# Keep the two-pouch vocabulary of candidate 01; fit them to the wider front shell.
for side in (-1, 1):
    theta = side*.24
    p = surface(theta, .855, .088)
    box('armor_mag_pouch', p, (.185, .095, .285), cloth, side*.14)
    box('armor_mag_flap', p+Vector((0, -.05, .113)), (.184, .018, .079), webbing, side*.14)
    box('armor_mag_pull', p+Vector((0, -.065, .085)), (.028, .009, .061), binding)
for z in (1.075, 1.15):
    for t in (-.38, -.19, 0, .19, .38):
        box('armor_front_webbing', surface(t, z, .056), (.074, .012, .025), webbing, t)
box('armor_id_tab', surface(0, 1.235, .058), (.14, .014, .032), binding)

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = shell
bpy.ops.object.join()
vest = bpy.context.object
vest.name = 'goose_vest_wrap_mesh'
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
candidate.append(vest)
for name, expected in unchanged.items():
    assert digest(bpy.data.objects[name]) == expected, name


def stats(objects):
    meshes = [o for o in objects if o.type == 'MESH']
    for ob in meshes:
        ob.data.calc_loop_triangles()
    pts = [o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    return dict(triangles=sum(len(o.data.loop_triangles) for o in meshes), mesh_objects=len(meshes),
                materials=len({m.name for o in meshes for m in o.data.materials}),
                bounds_min=[min(p[i] for p in pts) for i in range(3)], bounds_max=[max(p[i] for p in pts) for i in range(3)])


def export(name, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        ob.hide_set(False)
        ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets'/name), export_format='GLB', use_selection=True,
                             export_yup=True, export_cameras=False, export_lights=False)


export('goose-wrap-armor.glb', candidate)
vest_objects = [vest]
p = vest.parent
while p:
    vest_objects.append(p)
    p = p.parent
export('goose-wrap-vest-only.glb', vest_objects)
manifest = dict(status='offline art candidate 03; not game integrated', protected_files_sha256=hashes,
                unchanged_meshes=unchanged, candidate=stats(candidate), vest=stats([vest]),
                before_vest=stats([bpy.data.objects['before_goose_vest_field_mesh']]),
                axes='Blender Z up/-Y front; glTF Y up/+Z front',
                wing_opening=dict(center_angle=1.16, half_angle=.59, center_height=.91, lower_radius=.26, upper_radius=.27),
                limitations=['Original wing motion probes are not the procedural weapon-grip arms used by Godot.',
                             'No game integration, body-slider, reload, death or performance validation.'])
(ROOT/'wrap-armor-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


def show(objects):
    selected = set(objects)
    for ob in scene.objects:
        if not ob.name.startswith('STUDIO_'):
            ob.hide_render = ob not in selected
            ob.hide_set(ob not in selected)


camera = scene.camera
def view(pos, scale):
    camera.location = pos
    camera.data.ortho_scale = scale
    camera.rotation_euler = (Vector((0, 0, 1.02))-camera.location).to_track_quat('-Z', 'Y').to_euler()


scene.cycles.samples = 24
scene.render.resolution_x, scene.render.resolution_y = 720, 840
view((4, -6, 3.2), 2.65)
show(candidate)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-wrap-armor.blend'))
views = {'quarter': ((4, -6, 3.2), 2.65), 'front': ((0, -7, 2.4), 2.60),
         'side': ((7, 0, 2.4), 2.60), 'rear': ((0, 7, 2.4), 2.60),
         'top': ((4, -6, 11.3), 2.85)}
preview = '--preview' in sys.argv
for label, (pos, scale) in views.items():
    if preview and label not in ('quarter', 'side'):
        continue
    view(pos, scale)
    for variant, objects in [('before', baseline), ('after', candidate)]:
        if preview and variant == 'before':
            continue
        show(objects)
        scene.render.filepath = str(OUT/f'{variant}-{label}.png')
        bpy.ops.render.render(write_still=True)
        print('RENDERED', variant, label, flush=True)
if not preview:
    show(candidate)
    view((4, -6, 3.2), 2.65)
    wings = [bpy.data.objects['goose_'+s+'Arm'] for s in ('left', 'right')]
    homes = [o.matrix_world.copy() for o in wings]
    for pose, pitch, spread in [('forward20', 20, 0), ('outward20', 0, 20)]:
        for side, ob, home in zip((-1, 1), wings, homes):
            pivot = home.translation
            ob.matrix_world = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(pitch), 4, 'X') @ Matrix.Rotation(math.radians(-side*spread), 4, 'Y') @ Matrix.Translation(-pivot) @ home
        bpy.context.view_layer.update()
        scene.render.filepath = str(OUT/f'probe-{pose}.png')
        bpy.ops.render.render(write_still=True)
        for ob, home in zip(wings, homes):
            ob.matrix_world = home
        bpy.context.view_layer.update()
for path, expected in hashes.items():
    assert hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected
print('WRAP_ARMOR_READY', json.dumps(manifest['vest']), flush=True)
