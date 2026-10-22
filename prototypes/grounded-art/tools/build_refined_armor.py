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
OUT = ROOT/'output/refined-armor'
OUT.mkdir(parents=True, exist_ok=True)
protected = [ROOT/'source/goose-field-kit.blend', ROOT/'assets/goose-field-kit.glb',
             ROOT/'source/goose-workwear.blend', ROOT.parents[1]/'game/assets/characters/goose/goose.glb']
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
protected.extend([ROOT/'source/goose-wrap-armor.blend', ROOT/'assets/goose-wrap-armor.glb'])
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wrap-armor.blend'))
scene = bpy.context.scene
for ob in list(scene.objects):
    if ob.name.startswith(('before_', 'reference_')):
        bpy.data.objects.remove(ob, do_unlink=True)
baseline = [o for o in scene.objects if not o.name.startswith(('reference_', 'before_', 'STUDIO_'))]
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


unchanged = {o.name: digest(o) for o in candidate if o.type == 'MESH' and o.name != 'goose_vest_wrap_mesh'}
for name, expected in unchanged.items():
    original_ob, copied_ob = bpy.data.objects['before_'+name], bpy.data.objects[name]
    assert [tuple(v.co) for v in original_ob.data.vertices] == [tuple(v.co) for v in copied_ob.data.vertices], name
    assert [tuple(p.vertices) for p in original_ob.data.polygons] == [tuple(p.vertices) for p in copied_ob.data.polygons], name
    matrix_error = max(abs(original_ob.matrix_world[r][c]-copied_ob.matrix_world[r][c]) for r in range(4) for c in range(4))
    assert matrix_error < 1e-6, (name, matrix_error)
oldvest = bpy.data.objects['goose_vest_wrap_mesh']
def make_material(name, hexcolor, roughness, metallic=0):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    def linear(v): return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    rgba=tuple(linear(int(hexcolor[i:i+2],16)/255) for i in (0,2,4))+(1,)
    mat.diffuse_color=rgba
    p=mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=rgba
    p.inputs['Roughness'].default_value=roughness
    p.inputs['Metallic'].default_value=metallic
    return mat
cloth=make_material('refined_ballistic_fabric','303e47',.91)
webbing=make_material('refined_woven_webbing','536460',.90)
binding=make_material('refined_edge_binding','202b30',.94)
rubber=make_material('refined_rubber','182126',.80)
helmet_mat=make_material('refined_painted_helmet','4d613b',.46,.05)
# Material changes apply to the new candidate only; duplicated meshes have independent slots.
helmet=bpy.data.objects['goose_helmet_field_mesh']
for i,m in enumerate(helmet.data.materials):
    if m.name=='custom_hat_field_shell':
        helmet.data.materials[i]=helmet_mat
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
    mod.material_offset_rim = 1 if len(ob.data.materials)>1 else 0
    bpy.ops.object.modifier_apply(modifier=mod.name)
    normals=ob.modifiers.new('Area weighted cloth normals','WEIGHTED_NORMAL')
    normals.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=normals.name)


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
    bevel.width = min(.005, min(size)*.30)
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for p in ob.data.polygons:
        p.use_smooth = True
    normals = ob.modifiers.new('Weighted normals', 'WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=normals.name)
    parts.append(ob)
    return ob


body = bpy.data.objects['goose_torso_mesh']
bvh = BVHTree.FromPolygons([body.matrix_world@v.co for v in body.data.vertices], [p.vertices[:] for p in body.data.polygons])
def design_radius(t,z):
    rx=.476-.025*max(0,(z-1.0)/.35)
    ry=.423-.020*max(0,(z-1.0)/.35)
    exponent=2.8
    return (abs(math.sin(t)/rx)**exponent+abs(math.cos(t)/ry)**exponent)**(-1/exponent)

# One constant fit allowance keeps the manufactured panels smooth; per-vertex
# clamping to the source body would imprint its facets onto the new garment.
fit_allowance=0
for zi in range(65):
    z=.60+zi*.012
    for ti in range(128):
        t=ti*math.tau/128
        d=Vector((math.sin(t),-math.cos(t),0))
        hit,_,_,_=bvh.ray_cast(Vector((0,0,z)),d)
        assert hit is not None
        fit_allowance=max(fit_allowance, math.hypot(hit.x,hit.y)+.014-design_radius(t,z))

def surface(t,z,gap=.023):
    direction=Vector((math.sin(t),-math.cos(t),0))
    r=design_radius(t,z)+fit_allowance
    return Vector((direction.x*(r+gap-.023),direction.y*(r+gap-.023),z))

def neckline(t):
    a=abs(t)
    # Low front, slanted shoulder and slightly higher back. Not a cylindrical collar.
    return 1.205+.095*math.sin(a)+.075*a/math.pi

def hole(t):
    # Squared-off lower cut with a swept upper armhole, following wing position.
    distance=(abs(t)-1.18)/.56
    h=0 if abs(distance)>=1-1e-8 else (1-distance*distance)**.70
    tilt=.025*max(-1,min(1,distance))
    return .925-.24*h+tilt, .925+.27*h+tilt

ROWS = 4
# Include exact hole endpoints and extra samples near the turn to avoid a notched seam.
theta_samples=sorted(set([-math.pi+i*math.tau/80 for i in range(80)]+
    [side*(1.18+offset) for side in (-1,1) for offset in (-.56,-.555,-.54,-.51,.51,.54,.555,.56)]))
N=len(theta_samples)
vs, fs = [], []
for region in (0, 1):
    start = len(vs)
    for j in range(ROWS+1):
        for i in range(N):
            t = theta_samples[i]
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
shell.data.materials.append(binding)
solid(shell, .012)
def ribbon(name, angles, heights, width, mat, gap=.034):
    points,faces=[],[]
    for t,z in zip(angles,heights):
        for dz in (-width/2,width/2):
            points.append(surface(t,z+dz,gap))
    for i in range(len(angles)-1):
        faces.append((2*i,2*i+1,2*i+3,2*i+2))
    ob=mesh(name,points,faces,mat)
    solid(ob,.0035)
    return ob

angles=[-math.pi+i*math.tau/N for i in range(N+1)]
ribbon('refined_hem_binding',angles,[.624]*len(angles),.018,binding,.030)
ribbon('refined_neck_binding',angles,[neckline(t)-.008 for t in angles],.016,binding,.031)
# Three broad, flat front bands define the basic ballistic vest without box pouches.
for z in (.735,.875,1.015):
    angles=[-.58+i*1.16/24 for i in range(25)]
    ribbon('refined_front_band',angles,[z]*len(angles),.055,webbing,.033)
    for t in (-.40,0,.40):
        # Short dark stitch anchors, low contrast and almost flush.
        ribbon('refined_band_anchor',[t-.010,t+.010],[z,z],.057,binding,.036)
for side in (-1,1):
    angles=[side*(1.85+.95*i/20) for i in range(21)]
    for z in (.76,.90,1.04):
        ribbon('refined_side_band',angles,[z]*len(angles),.036,webbing,.033)
    seam_vs,seam_fs=[],[]
    for j in range(7):
        for offset in (-.012,.012):
            seam_vs.append(surface(side*2.15+offset,.72+j*.065,.036))
    for j in range(6): seam_fs.append((2*j,2*j+1,2*j+3,2*j+2))
    solid(mesh('refined_side_adjustment',seam_vs,seam_fs,binding),.003)
# A single restrained identity patch instead of a grid of small raised fittings.
box('refined_id_patch',surface(-.25,1.135,.033),(.15,.012,.063),binding,-.25)
bpy.ops.object.select_all(action='DESELECT')
for ob in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = shell
bpy.ops.object.join()
vest = bpy.context.object
vest.name = 'goose_vest_refined_mesh'
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


export('goose-refined-armor.glb', candidate)
vest_objects = [vest]
p = vest.parent
while p:
    vest_objects.append(p)
    p = p.parent
export('goose-refined-vest-only.glb', vest_objects)
manifest = dict(status='offline basic armor candidate 04; not game integrated', protected_files_sha256=hashes,
                unchanged_meshes=unchanged, candidate=stats(candidate), vest=stats([vest]),
                before_vest=stats([bpy.data.objects['before_goose_vest_wrap_mesh']]),
                axes='Blender Z up/-Y front; glTF Y up/+Z front',
                wing_opening=dict(center_angle=1.18, half_angle=.56, center_height=.925, lower_radius=.24, upper_radius=.27, profile_exponent=.70),
                limitations=['Original wing motion probes are not the procedural weapon-grip arms used by Godot.',
                             'No game integration, body-slider, reload, death or performance validation.'])
manifest['fit_allowance_metres']=fit_allowance
manifest['material_changes']={'helmet': 'green painted shell, roughness 0.46, metallic 0.05',
                              'vest': 'dark fabric 0.91, webbing 0.90, binding 0.94; no texture maps'}
manifest['render_contract']='Both models in identical neutral lighting, then identical cool ambient/warm key lighting. No postprocess retouch.'
manifest['geometry_changes']='Flatter front and back, slanted neckline, shaped armholes, flat bindings; three broad bands replace pouch blocks.'
(ROOT/'refined-armor-manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


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


scene.cycles.samples = 32
scene.render.resolution_x, scene.render.resolution_y = 720, 840
view((4, -6, 3.2), 2.65)
show(candidate)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-refined-armor.blend'))
views = {'quarter': ((4,-6,3.2),2.65), 'front': ((0,-7,2.4),2.60),
         'side': ((7,0,2.4),2.60), 'rear': ((0,7,2.4),2.60),
         'top': ((4,-6,11.3),2.85), 'small': ((4,-6,11.3),10.0)}
preview='--preview' in sys.argv
lights={o.name:(o.data.energy,tuple(o.data.color),o.data.size) for o in scene.objects if o.type=='LIGHT'}
world=scene.world.node_tree.nodes['Background']
neutral_world=(tuple(world.inputs['Color'].default_value),world.inputs['Strength'].default_value)
floor=bpy.data.objects['STUDIO_floor']
neutral_floor=floor.data.materials[0]
mood_floor=make_material('STUDIO_slate_floor','3f4d5b',.84)
for lighting in ('neutral','mood'):
    for name,(energy,color,size) in lights.items():
        lamp=bpy.data.objects[name]
        lamp.data.energy=energy;lamp.data.color=color;lamp.data.size=size
    world.inputs['Color'].default_value=neutral_world[0]
    world.inputs['Strength'].default_value=neutral_world[1]
    floor.data.materials[0]=neutral_floor
    if lighting=='mood':
        world.inputs['Color'].default_value=(.13,.20,.37,1)
        world.inputs['Strength'].default_value=.23
        floor.data.materials[0]=mood_floor
        for name,energy,color,size in [('STUDIO_key',780,(1.0,.86,.67),3),
                                      ('STUDIO_fill',130,(.40,.60,1.0),5),
                                      ('STUDIO_rim',650,(.48,.67,1.0),3)]:
            lamp=bpy.data.objects[name]
            lamp.data.energy=energy;lamp.data.color=color;lamp.data.size=size
        show(candidate)
        view((4,-6,3.2),2.65)
        scene.render.resolution_y=840
        bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-refined-armor-mood.blend'))
    for label,(pos,scale) in views.items():
        if preview and label!='quarter': continue
        if lighting=='mood' and label not in ('quarter','top','small'): continue
        view(pos,scale)
        scene.render.resolution_y=480 if label=='small' else 840
        for variant,objects in [('before',baseline),('after',candidate)]:
            if preview and variant=='before': continue
            show(objects)
            scene.render.filepath=str(OUT/f'{lighting}-{variant}-{label}.png')
            bpy.ops.render.render(write_still=True)
            print('RENDERED',lighting,variant,label,flush=True)
# Motion inspection uses the same neutral studio as the shape comparison.
for name,(energy,color,size) in lights.items():
    lamp=bpy.data.objects[name]
    lamp.data.energy=energy;lamp.data.color=color;lamp.data.size=size
world.inputs['Color'].default_value=neutral_world[0]
world.inputs['Strength'].default_value=neutral_world[1]
floor.data.materials[0]=neutral_floor
scene.render.resolution_y=840
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
print('REFINED_ARMOR_READY', json.dumps(manifest['vest']), flush=True)
