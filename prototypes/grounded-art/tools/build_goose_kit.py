"""Standalone art candidate. Blender --background --python tools/build_goose_kit.py.

Reads the game goose once, writes ONLY beneath prototypes/grounded-art.
Blender coordinates: X right, Y back, Z up. Export: glTF +Y up, +Z front.
"""
import bpy
import math
import json
import hashlib
import shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
SOURCE = REPO / 'game/assets/characters/goose/goose.glb'
for sub in ('source/reference', 'assets', 'output'):
    (ROOT / sub).mkdir(parents=True, exist_ok=True)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


source_hash = sha(SOURCE)
reference = ROOT / 'source/reference/goose-original.glb'
shutil.copyfile(SOURCE, reference)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def descendants(ob):
    return [ob] + [child for c in ob.children for child in descendants(c)]


def import_default(prefix=''):
    prior = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(reference))
    objects = list(set(bpy.data.objects) - prior)
    by_name = {ob.name: ob for ob in objects}
    # Both variants use the same default outfit, no optional hat/headset or held gun.
    for name in ('goose_beret', 'goose_crestShort', 'goose_crestSwept',
                 'goose_goggles', 'goose_headset', 'goose_vestPadded', 'goose_weapon'):
        for ob in list(descendants(by_name[name])):
            if ob.type == 'MESH':
                objects.remove(ob)
                bpy.data.objects.remove(ob, do_unlink=True)
    for ob in objects:
        ob.name = prefix + ob.name
    return objects


original = import_default('reference_')
candidate = import_default()
roots = {name: bpy.data.objects[name] for name in ('goose_helmet', 'goose_vest', 'goose_pack')}
original_body = bpy.data.objects['reference_goose_torso_mesh']
candidate_body = bpy.data.objects['goose_torso_mesh']


def geometry_digest(ob):
    return hashlib.sha256(repr(([tuple(v.co) for v in ob.data.vertices],
                               [tuple(p.vertices) for p in ob.data.polygons],
                               list(ob.matrix_world))).encode()).hexdigest()


body_digest = geometry_digest(candidate_body)
assert body_digest == geometry_digest(original_body)
for parent in roots.values():
    for ob in list(descendants(parent)):
        if ob.type == 'MESH':
            candidate.remove(ob)
            bpy.data.objects.remove(ob, do_unlink=True)


def linear(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def material(name, color, roughness, metallic=0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    rgb = tuple(linear(int(color[i:i+2], 16)/255) for i in (0, 2, 4))
    mat.diffuse_color = (*rgb, 1)
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    return mat


M = {
    'shell': material('custom_hat_field_shell', '59604b', .80),
    'rubber': material('field_rubber', '292e28', .78),
    'cloth': material('custom_vest_field_canvas', '626b50', .92),
    'webbing': material('field_webbing', '454d3a', .95),
    'edge': material('field_binding', '7e8367', .90),
    'pack': material('custom_pack_field_canvas', '81765c', .94),
    'metal': material('field_hardware', '4d5349', .43, .55),
}
created = []


def attach(ob, group, mat):
    ob.data.materials.append(M[mat])
    world = ob.matrix_world.copy()
    ob.parent = roots[group]
    ob.matrix_world = world
    created.append(ob)
    candidate.append(ob)
    return ob


def mesh(name, vertices, faces, group, mat, smooth=False):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    ob = bpy.data.objects.new(name, data)
    scene.collection.objects.link(ob)
    attach(ob, group, mat)
    for face in data.polygons:
        face.use_smooth = smooth
    return ob


def bevel(ob, amount=.015, segments=2):
    bpy.context.view_layer.objects.active = ob
    mod = ob.modifiers.new('Small manufactured edge', 'BEVEL')
    # A bevel equal to half the thinnest dimension collapses corner faces on export.
    mod.width, mod.segments = min(amount, min(ob.dimensions)*.40), segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in ob.data.polygons:
        poly.use_smooth = True
    mod = ob.modifiers.new('Face weighted normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=mod.name)


def box(name, center, size, group, mat, edge=.012, rotation=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob = bpy.context.object
    ob.name = name
    ob.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if rotation:
        ob.rotation_euler = rotation
    attach(ob, group, mat)
    if edge:
        bevel(ob, edge)
    return ob


def tube(name, points, radius, group, mat, sides=6, closed=False):
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    for i, p in enumerate(pts):
        tangent = pts[(i+1) % len(pts)]-pts[(i-1) % len(pts)] if closed else pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]
        tangent.normalize()
        a = tangent.cross(Vector((0, 0, 1)))
        if a.length < .01:
            a = tangent.cross(Vector((0, 1, 0)))
        a.normalize()
        b = tangent.cross(a).normalized()
        for j in range(sides):
            verts.append(p + radius*(math.cos(j*math.tau/sides)*a+math.sin(j*math.tau/sides)*b))
    for i in range(len(pts) if closed else len(pts)-1):
        for j in range(sides):
            faces.append((i*sides+j, i*sides+(j+1)%sides, ((i+1)%len(pts))*sides+(j+1)%sides, ((i+1)%len(pts))*sides+j))
    if not closed:
        faces += [tuple(reversed(range(sides))), tuple((len(pts)-1)*sides+j for j in range(sides))]
    return mesh(name, verts, faces, group, mat, True)


def radius(z):
    t = max(.001, min(.999, (z-.22)/1.74))
    return .47 * math.sqrt(1-(2*t-1)**2)**.62 * (1.08-.14*t)


def front_y(x, z, extra=.0):
    r = radius(z)
    return -.86*r*math.sqrt(max(.001, 1-(x/r)**2))-extra


# Close-fitting composite helmet: shell follows the head, thin edge, quiet hardware.
N, R = 48, 12
verts, faces = [], []
for inner in (False, True):
    for row in range(R+1):
        phi = .035 + (math.pi/2-.035)*row/R
        for i in range(N):
            theta = math.tau*i/N
            # Raise the front edge above eyes; lower the rear for nape coverage.
            zedge = 1.68 - .045*math.sin(theta)*math.sin(phi)**2
            rx, ry = (.457, .395) if not inner else (.439, .377)
            z = zedge + (.365 if not inner else .348)*math.cos(phi)
            verts.append((rx*math.sin(phi)*math.cos(theta), ry*math.sin(phi)*math.sin(theta), z))
offset = (R+1)*N
for inner in (False, True):
    base = offset if inner else 0
    for row in range(R):
        for i in range(N):
            f=(base+row*N+i, base+row*N+(i+1)%N, base+(row+1)*N+(i+1)%N,base+(row+1)*N+i)
            faces.append(tuple(reversed(f)) if inner else f)
for i in range(N):
    faces.append((R*N+i,R*N+(i+1)%N,offset+R*N+(i+1)%N,offset+R*N+i))
faces += [tuple(reversed(range(N))),tuple(offset+i for i in range(N))]
mesh('field_helmet_shell',verts,faces,'goose_helmet','shell',True)
rim=[(.457*math.cos(t), .395*math.sin(t),1.68-.045*math.sin(t)) for t in [i*math.tau/N for i in range(N)]]
tube('field_helmet_rubber_edge',rim,.010,'goose_helmet','rubber',8,True)
for s in (-1,1):
    box('field_helmet_rail', (s*.437, .035,1.755),(.025,.20,.052),'goose_helmet','rubber',.009)
    for yy in (-.025,.085):
        box('field_helmet_rail_fastener',(s*.452,yy,1.756),(.008,.018,.020),'goose_helmet','metal',.004)
    # Deliberately small hook-and-loop patch on the rear quarter.
    box('field_helmet_patch',(s*.276,.283,1.79),(.12,.018,.055),'goose_helmet','webbing',.009,rotation=(0,0,-s*.58))
box('field_helmet_mount',(0,-.382,1.797),(.105,.018,.085),'goose_helmet','metal',.012)
box('field_helmet_mount_slot',(0,-.395,1.80),(.043,.008,.039),'goose_helmet','rubber',.003)


# Curved front and rear carrier panels, not a rigid ring/skirt around the belly.
def panel(name, rear=False):
    levels=[(.63,.25),(.68,.29),(.91,.32),(1.12,.30),(1.23,.235),(1.28,.22)]
    cols=16
    v,f=[],[]
    for layer in (0,1):
        for z,half in levels:
            for i in range(cols+1):
                x=half*(i/cols*2-1)
                y=front_y(x,z,.012+layer*.028)
                v.append((x, -y if rear else y,z))
    span=len(levels)*(cols+1)
    for layer in (0,1):
        for k in range(len(levels)-1):
            for i in range(cols):
                a=layer*span+k*(cols+1)+i
                f.append((a,a+1,a+cols+2,a+cols+1))
    border=list(range(cols+1))+[k*(cols+1)+cols for k in range(1,len(levels))]+list(range(span-2,span-cols-2,-1))+[k*(cols+1) for k in range(len(levels)-2,0,-1)]
    for a,b in zip(border,border[1:]+border[:1]):
        f.append((a,b,b+span,a+span))
    ob=mesh(name,v,f,'goose_vest','cloth',True)
    # Binding lies on the cloth perimeter, only a few mm thick.
    tube(name+'_binding',[v[i+span] for i in border],.005,'goose_vest','edge',5,True)
    return ob


panel('field_carrier_front')
panel('field_carrier_back',True)

# Side straps (cummerbund) sit behind the existing wing rather than wrapping its tip.
for s in (-1,1):
    for z0 in (.72,.80):
        v,f=[],[]
        for i in range(25):
            theta=.65+(math.pi-1.30)*i/24
            for dz in (-.029,.029):
                z=z0+dz
                r=radius(z)+.015
                v.append((s*r*math.sin(theta),-.86*r*math.cos(theta),z))
        for i in range(24): f.append((2*i,2*i+1,2*i+3,2*i+2))
        ob=mesh('field_cummerbund',v,f,'goose_vest','webbing',True)
        mod=ob.modifiers.new('Woven strap thickness','SOLIDIFY'); mod.thickness=.009
        bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=mod.name)
    # Shoulder straps follow the upper sides of the egg-shaped torso.
    v,f=[],[]
    for i in range(33):
        t=i/32; theta=.59+(math.pi-1.18)*t
        for dz in (-.040,.040):
            z=1.18+.25*math.sin(t*math.pi)+dz
            r=radius(z)+.017
            v.append((s*r*math.sin(theta),-.86*r*math.cos(theta),z))
    for i in range(32): f.append((i*2,i*2+1,i*2+3,i*2+2))
    ob=mesh('field_shoulder_strap',v,f,'goose_vest','webbing',True)
    mod=ob.modifiers.new('Strap padding','SOLIDIFY'); mod.thickness=.012
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=mod.name)
    for z in (1.17,):
        x=s*.25
        box('field_shoulder_adjuster',(x,front_y(x,z,.043),z),(.073,.023,.066),'goose_vest','rubber',.006,rotation=(0,0,-s*.35))

# Two curved rows of webbing; no oversized cosmetic markings on the chest.
for z in (1.075,1.15):
    for x in (-.18,-.09,0,.09,.18):
        box('field_molle',(x,front_y(x,z,.046),z),(.074,.012,.025),'goose_vest','webbing',.003,rotation=(0,0,-x*1.6))
box('field_id_tab',(0,front_y(0,1.235,.047),1.235),(.14,.014,.032),'goose_vest','edge',.003)

for s in (-1,1):
    x=s*.113; z=.855; y=front_y(x,z,.082)
    box('field_mag_pouch',(x,y,z),(.185,.095,.285),'goose_vest','cloth',.017,rotation=(0,0,-s*.14))
    box('field_mag_flap',(x,y-.049,.968),(.184,.018,.079),'goose_vest','webbing',.008,rotation=(0,0,-s*.14))
    box('field_mag_pull',(x,y-.064,.941),(.028,.009,.061),'goose_vest','edge',.003)
    for zz in (.764,.813):
        box('field_mag_stitch',(x,y-.050,zz),(.144,.005,.005),'goose_vest','edge',.001)

# Backpack: compact 18L-style daypack, close back panel, top handle, zip and compression.
box('field_pack_backpad',(0,.415,.99),(.45,.070,.58),'goose_pack','rubber',.04)
box('field_pack_body',(0,.524,1.015),(.50,.235,.59),'goose_pack','pack',.055)
box('field_pack_front_pocket',(0,.665,.91),(.38,.064,.29),'goose_pack','pack',.035)
# Actual pocket seam and zipper: restrained thin lines.
tube('field_pack_zip',[(-.19,.665,1.19),(-.19,.665,1.24),(-.15,.665,1.275),(0,.665,1.29),(.15,.665,1.275),(.19,.665,1.24),(.19,.665,1.19)],.006,'goose_pack','rubber',6)
box('field_zip_pull',(.16,.681,1.265),(.022,.009,.045),'goose_pack','metal',.005)
tube('field_pack_handle',[(-.09,.50,1.30),(-.09,.50,1.35),(-.06,.50,1.37),(.06,.50,1.37),(.09,.50,1.35),(.09,.50,1.30)],.012,'goose_pack','webbing',6)
for s in (-1,1):
    box('field_pack_compression',(s*.172,.661,1.075),(.034,.015,.40),'goose_pack','webbing',.004)
    box('field_pack_buckle',(s*.172,.676,1.055),(.052,.018,.055),'goose_pack','rubber',.006)
    box('field_pack_buckle_slot',(s*.172,.687,1.055),(.022,.005,.023),'goose_pack','pack',.002)
    # Narrow side pocket, tapered visual size rather than a second backpack.
    box('field_pack_side_pocket',(s*.269,.51,.925),(.085,.155,.25),'goose_pack','pack',.026)
    box('field_pack_side_binding',(s*.27,.51,1.043),(.089,.16,.024),'goose_pack','webbing',.006)
    # Shoulder straps belong to pack, rear anchors connect to carrier shoulders.
    tube('field_pack_anchor',[(s*.18,.38,1.285),(s*.25,.35,1.27),(s*.28,.32,1.24)],.018,'goose_pack','webbing',6)
for z in (.842,.902):
    box('field_pack_pocket_webbing',(0,.701,z),(.30,.010,.023),'goose_pack','webbing',.003)

# Consolidate by equipment attachment. Keep all three original pivots and only 7 new materials.
for group, parent in roots.items():
    meshes=[o for o in descendants(parent) if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.object.join()
    joined=bpy.context.object
    joined.name=group+'_field_mesh'
    if group=='goose_pack':
        # Include pockets in the size reduction; main bag size alone is misleading.
        inverse=joined.matrix_world.inverted()
        for vertex in joined.data.vertices:
            position=joined.matrix_world@vertex.co
            position.x*=.88
            position.y=.40+(position.y-.40)*.83
            vertex.co=inverse@position
    candidate=[o for o in scene.objects if not o.name.startswith('reference_')]
    # Recalculate face orientation consistently on exported cloth and shell solids.
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')

assert geometry_digest(candidate_body)==body_digest, 'Goose body changed'
preserved_geometry={}
for ob in candidate:
    if ob.type=='MESH' and not any(ob in descendants(p) for p in roots.values()):
        check_reference=bpy.data.objects['reference_'+ob.name]
        assert geometry_digest(ob)==geometry_digest(check_reference), ob.name+' changed'
        preserved_geometry[ob.name]=geometry_digest(ob)
assert sha(SOURCE)==source_hash, 'Source file changed'
assert not any(o.type=='ARMATURE' for o in candidate)


def stats(objects):
    meshes=[o for o in objects if o.type=='MESH']
    for o in meshes: o.data.calc_loop_triangles()
    pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
    return {'mesh_objects':len(meshes),'triangles':sum(len(o.data.loop_triangles) for o in meshes),
            'materials':len({m.name for o in meshes for m in o.data.materials}),
            'bounds_min':[min(p[i] for p in pts) for i in range(3)],
            'bounds_max':[max(p[i] for p in pts) for i in range(3)]}


gear=[ob for p in roots.values() for ob in descendants(p)]
assert stats(gear)['triangles'] < 18000
original_parts={name:stats(descendants(bpy.data.objects['reference_'+name])) for name in roots}
new_pack=stats(descendants(roots['goose_pack']))
old_pack=original_parts['goose_pack']
assert new_pack['bounds_max'][0]-new_pack['bounds_min'][0] < old_pack['bounds_max'][0]-old_pack['bounds_min'][0]
assert new_pack['bounds_max'][1] < old_pack['bounds_max'][1]


def export(path,objects):
    assert path.resolve().is_relative_to(ROOT)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
                             export_yup=True,export_extras=True,export_cameras=False,export_lights=False)


export(ROOT/'assets/goose-field-kit.glb',candidate)
ancestors=set()
for parent in roots.values():
    p=parent.parent
    while p: ancestors.add(p); p=p.parent
export(ROOT/'assets/goose-equipment-only.glb',list(set(gear)|ancestors))

manifest={'status':'review candidate; not integrated into game','date':'2026-10-22',
          'source':'source/reference/goose-original.glb','source_sha256':source_hash,
          'source_game_path':'game/assets/characters/goose/goose.glb',
          'units':'metres','export_axes':'+Y up, +Z front','blender_axes':'+Z up, -Y front',
          'unchanged_body_sha256':body_digest,
          'unchanged_meshes':preserved_geometry,
          'original_default':stats(original),'candidate':stats(candidate),'equipment':stats(gear),
          'parts':{k:stats(descendants(v)) for k,v in roots.items()},
          'original_parts':original_parts,
          'files':['assets/goose-field-kit.glb','assets/goose-equipment-only.glb','source/goose-field-kit.blend'],
          'preserved_attachment_nodes':list(roots),
          'material_contract':'Existing goose materials untouched. 7 new equipment materials, standard glTF PBR; shared webbing/hardware are not runtime color channels.',
          'render_contract':'Both imported from identical source; default helmet, vest and pack; alternate hats, hair, headset, goggles, padded vest and built-in gun excluded equally. No runtime Godot shader or appearance overrides.',
          'limitations':['Idle pose only; original wings and feet unchanged.', 'No Godot/multiplayer/animation/performance validation.', 'Outfit candidate replaces only default equipment; alternate variants are empty pivots.', 'Shared trim materials need a deliberate runtime customization mapping before integration.']}
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')

# Neutral studio for an honest A/B: each character rendered at origin in the same scene.
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.cycles.max_bounces=6
scene.render.resolution_x=900; scene.render.resolution_y=1050; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world=bpy.data.worlds.new('STUDIO_world')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.16,.16,.16,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012))
floor=bpy.context.object;floor.name='STUDIO_floor'
floor.data.materials.append(material('STUDIO_floor','343b38',.94))


def aim(ob,target): ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()


for name,pos,energy,size in [('key',(-3,-4,6),650,4),('fill',(4,-1,3),360,4),('rim',(1,4,5),850,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos)
    lamp=bpy.context.object; lamp.name='STUDIO_'+name
    lamp.data.energy=energy; lamp.data.shape='DISK';lamp.data.size=size
    aim(lamp,(0,0,1))
bpy.ops.object.camera_add(location=(4,-6,3.2))
camera=bpy.context.object;camera.name='STUDIO_camera';camera.data.type='ORTHO';scene.camera=camera
views={'quarter':((4,-6,3.2),2.65),'front':((0,-7,2.4),2.60),
       'side':((7,0,2.4),2.60),'rear':((0,7,2.4),2.60),
       'rear-quarter':((4,6,3.3),2.65),'gameplay':((4,-6,11.3),12.5)}

for ob in original: ob.hide_render=True;ob.hide_set(True)
for ob in candidate: ob.hide_render=False
camera.location=views['quarter'][0];camera.data.ortho_scale=2.65;aim(camera,(0,0,1.02))
scene.render.filepath=str(ROOT/'output/candidate-quarter.png')
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-field-kit.blend'))
for view,(position,scale) in views.items():
    camera.location=position;camera.data.ortho_scale=scale;aim(camera,(0,0,1.02))
    scene.render.resolution_x=900
    scene.render.resolution_y=600 if view=='gameplay' else 1050
    for variant,show,hide in [('original',original,candidate),('candidate',candidate,original)]:
        for ob in hide: ob.hide_render=True;ob.hide_set(True)
        for ob in show: ob.hide_render=False;ob.hide_set(False)
        scene.render.filepath=str(ROOT/f'output/{variant}-{view}.png')
        bpy.ops.render.render(write_still=True)
        print('RENDERED',variant,view,flush=True)
assert sha(SOURCE)==source_hash
print('GOOSE_KIT_READY',json.dumps(manifest['equipment']),flush=True)
