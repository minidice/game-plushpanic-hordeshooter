"""Five lightweight ground-border decorations; Blender Z-up, metres.

Authoring only: the reviewer runs Blender and checks the exports/renders.
Run: blender --background --python prototypes/outdoor-kit/tools/ground_bits.py
Helpers/palette copied from edge_kit.py and build_models.py, never imported.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game' / 'assets' / 'ground-bits'
CONTRACT = 'plushpanic_outdoor_v1'
# Authoring X/Y/Z; manifest dimensions are exported game X/Y/Z (X/Z/Y here).
EXPECTED = {
    'bit_grass_tuft': (.6, .5, .28),
    'bit_grass_clump': (.9, .8, .4),
    'bit_pebbles': (.7, .6, .08),
    'bit_gravel': (1.2, .5, .05),
    'bit_stone': (.24, .19, .09),
}
BUDGET = dict(zip(EXPECTED, (160, 260, 120, 200, 60)))
PALETTE = {
    'Grass': ('6c8053', .95), 'DryGrass': ('a2a16b', .95),
    'LeafTip': ('7e985b', .86), 'Moss': ('637952', .97),
    'Rock': ('667a7c', .92), 'RockLight': ('8c9c96', .92),
}
M = {}


def mat(name, color, rough=.78, metal=0):
    rgb = [int(color[i:i+2], 16)/255 for i in (0, 2, 4)]
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*rgb, 1)
    material.use_nodes = True
    bs = material.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*rgb, 1)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    # No directional rear culling: scattered bits must read at every yaw.
    material.use_backface_culling = False
    material['wet_eligible'] = True
    material['snow_eligible'] = True
    return material


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def mesh(parent, name, vertices, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    data.materials.append(M[material])
    return obj


def plate(parent, name, size, height, material, sides=10, phase=0):
    """Irregular convex-ish patch with vertical sides, a top fan, no bottom.

    Identical upper/lower outlines keep the requested thickness exact. Radial
    jitter is deterministic; the fan avoids concave-ngon triangulation surprises.
    """
    outline = []
    for i in range(sides):
        angle = math.tau*i/sides
        radius = 1 + .10*math.sin(i*2.3+phase)
        outline.append((math.cos(angle)*radius, math.sin(angle)*radius))
    low = [min(v[axis] for v in outline) for axis in (0, 1)]
    high = [max(v[axis] for v in outline) for axis in (0, 1)]
    outline = [tuple((v[axis]-(low[axis]+high[axis])/2)*size[axis]
                     /(high[axis]-low[axis]) for axis in (0, 1)) for v in outline]
    vertices = [(x, y, z) for z in (0, height) for x, y in outline]
    vertices.append((0, 0, height))
    faces = []
    for i in range(sides):
        nxt = (i+1) % sides
        faces.append((i, nxt, nxt+sides, i+sides))
        faces.append((2*sides, i+sides, nxt+sides))
    return mesh(parent, name, vertices, faces, material)


def blade(parent, center, angle, reach, height, width, tip_material='LeafTip'):
    """Bent, thick diamond-section leaf: 12 triangles before underside removal.

    A broad lower shoulder tapers to one pointed tip. Upper faces carry the tip
    color on the same geometry, avoiding detached/overlapping color patches.
    """
    dx, dy = math.cos(angle), math.sin(angle)
    vertices = []
    for advance, z, half_width, thickness in (
            (.035, 0, width*.65, .009),
            (reach*.47, height*.47, width, .012)):
        for radial, tangent in ((thickness, 0), (0, half_width),
                                (-thickness, 0), (0, -half_width)):
            vertices.append((center[0]+dx*(advance+radial)-dy*tangent,
                             center[1]+dy*(advance+radial)+dx*tangent, z))
    vertices.append((center[0]+dx*reach, center[1]+dy*reach, height))
    faces = [(i, (i+1) % 4, (i+1) % 4+4, i+4) for i in range(4)]
    faces.extend((i+4, (i+1) % 4+4, 8) for i in range(4))
    obj = mesh(parent, 'Blade', vertices, faces, 'Grass')
    obj.data.materials.append(M[tip_material])
    for face in obj.data.polygons:
        if face.index >= 4:
            face.material_index = 1
    return obj


def pebble(parent, at, size, material, yaw=0):
    """Kit subdivision-1 ico; ground each stone and size its retained top."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1)
    obj = bpy.context.object
    obj.name = 'Pebble'
    obj.parent = parent
    obj.data.materials.append(M[material])
    low = [min(v.co[i] for v in obj.data.vertices) for i in range(3)]
    high = [max(v.co[i] for v in obj.data.vertices) for i in range(3)]
    for vertex in obj.data.vertices:
        x = (vertex.co.x-(low[0]+high[0])/2)*size[0]/(high[0]-low[0])
        y = (vertex.co.y-(low[1]+high[1])/2)*size[1]/(high[1]-low[1])
        z = (vertex.co.z-low[2])*size[2]/(high[2]-low[2])
        vertex.co = (at[0]+x*math.cos(yaw)-y*math.sin(yaw),
                     at[1]+x*math.sin(yaw)+y*math.cos(yaw), at[2]+z)
    obj.data.update()
    # With no underlying patch, each stone needs its own contact plane. Culling
    # a flattened ico removes its lowest vertices, so size/ground the top AFTER
    # culling rather than letting the final group shift leave small stones aloft.
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bm.normal_update()
        bmesh.ops.delete(bm, geom=[face for face in bm.faces if face.normal.z < -.5],
                         context='FACES')
        loose = [vertex for vertex in bm.verts if not vertex.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bottom = min(vertex.co.z for vertex in bm.verts)
        top = max(vertex.co.z for vertex in bm.verts)
        for vertex in bm.verts:
            vertex.co.z = at[2]+(vertex.co.z-bottom)*size[2]/(top-bottom)
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.update()
    return obj


def grass_tuft(keep):
    plate(keep, 'MossBase', (.19, .15), .018, 'Moss', sides=8)
    for i in range(9):
        angle = math.tau*i/9
        # Elliptical tip ring, so the fan occupies about .60 x .50 m.
        reach = 1/math.sqrt((math.cos(angle)/.30)**2+(math.sin(angle)/.25)**2)
        blade(keep, (0, 0), angle, reach, (.28, .23, .26)[i % 3], .024,
              'LeafTip' if i % 3 == 0 else 'Grass')


def grass_clump(keep):
    # Three touching fans, seven leaves each; 252 triangles before culling.
    for cluster, (x, y, phase) in enumerate(((-.18, -.13, .2),
                                            (.18, -.13, .6), (0, .15, 1.1))):
        for i in range(7):
            angle = phase+math.tau*i/7
            height = (.4, .32, .36)[(i+cluster) % 3]
            shade = 'DryGrass' if i == 2 else ('LeafTip' if i % 3 == 0 else 'Grass')
            blade(keep, (x, y), angle, .28, height, .029, shade)


def pebbles(keep):
    # Three separated stones: conservative footprint gaps exceed the largest
    # stone's .22 m width, even after yaw. No soil plate underneath.
    for i, (x, y, sx, sy, height) in enumerate((
            (-.24, -.19, .22, .18, .08), (.26, -.16, .14, .11, .055),
            (-.01, .23, .10, .10, .04))):
        pebble(keep, (x, y, 0), (sx, sy, height),
               'RockLight' if i % 2 else 'Rock', yaw=i*.63)


def gravel(keep):
    # Nine small stones, with shorter gaps near the middle of the strip.
    for i, (x, y, sx, sy, height) in enumerate((
            (-.55, -.02, .10, .08, .03), (-.35, .18, .09, .07, .035),
            (-.20, -.18, .08, .06, .025), (-.10, .02, .12, .09, .05),
            (.04, -.07, .07, .05, .022), (.14, .15, .10, .07, .035),
            (.25, -.14, .11, .08, .04), (.40, .03, .08, .06, .028),
            (.55, -.15, .06, .05, .02))):
        pebble(keep, (x, y, 0), (sx, sy, height),
               'RockLight' if i % 3 == 1 else 'Rock', yaw=i*.71)


def stone(keep):
    pebble(keep, (0, 0, 0), (.24, .19, .09), 'Rock')


BUILDERS = (
    ('bit_grass_tuft', '작은 풀 다발', grass_tuft, '바깥으로 벌어진 풀잎 9개와 작은 이끼 밑동.'),
    ('bit_grass_clump', '큰 풀 덩어리', grass_clump, '7잎 다발 3개, 마른 잎 끝 3개.'),
    ('bit_pebbles', '흙길 돌 조각', pebbles, '판 없이 넓게 흩어진 납작한 돌 3개.'),
    ('bit_gravel', '갓길 자갈 조각', gravel, '판 없이 길쭉하게 흩어진 작은 자갈 9개.'),
    ('bit_stone', '작은 돌 하나', stone, '풀밭과 흙에 흩뿌리는 납작하고 둥근 돌 하나.'),
)


def start(asset_id, name, note):
    root = empty(asset_id)
    root['asset_contract'] = CONTRACT
    root['ground_bit'] = True
    keep = empty('Keep', root)
    keep['visibility_role'] = 'keep'
    data = dict(id=asset_id, name=name, category='길 경계', note=note,
                collision={'kind': 'none'}, occlusion_parts=[])
    return root, keep, data


def merge_and_cull(keep):
    """Copied edge-kit material merge; remove undersides, retain every rear face."""
    bpy.context.view_layer.update()
    buckets = {}
    originals = list(keep.children)
    for obj in originals:
        obj.data.calc_loop_triangles()
        for tri in obj.data.loop_triangles:
            material = obj.data.materials[tri.material_index]
            vertices, faces = buckets.setdefault(material.name, ([], []))
            offset = len(vertices)
            vertices.extend(tuple(obj.matrix_world @ obj.data.vertices[i].co) for i in tri.vertices)
            faces.append((offset, offset+1, offset+2))
    for obj in originals:
        old_data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if old_data.users == 0:
            bpy.data.meshes.remove(old_data)
    for name, (vertices, faces) in sorted(buckets.items()):
        obj = mesh(keep, name+'_Mesh', vertices, faces, name)
        bm = bmesh.new()
        try:
            bm.from_mesh(obj.data)
            bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00001)
            bm.normal_update()
            remove = [face for face in bm.faces
                      if face.normal.z < -.5 or face.calc_area() < 1e-10]
            bmesh.ops.delete(bm, geom=remove, context='FACES')
            loose = [vertex for vertex in bm.verts if not vertex.link_faces]
            if loose:
                bmesh.ops.delete(bm, geom=loose, context='VERTS')
            bm.to_mesh(obj.data)
        finally:
            bm.free()
        obj.data.update()
        assert obj.data.polygons, (name, 'all material faces were culled')


def bounds(meshes):
    vertices = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    return (Vector(tuple(min(v[i] for v in vertices) for i in range(3))),
            Vector(tuple(max(v[i] for v in vertices) for i in range(3))))


def validate_asset(root, data):
    asset_id = data['id']
    assert root.name == asset_id and root['asset_contract'] == CONTRACT
    assert root['ground_bit'] is True and root.location.length < 1e-6
    assert len(root.children) == 1 and root.children[0].name == 'Keep', asset_id
    keep = root.children[0]
    assert keep.type == 'EMPTY'
    meshes = list(keep.children)
    assert meshes and all(obj.type == 'MESH' and not obj.children for obj in meshes)
    materials = set()
    triangles = 0
    for obj in meshes:
        assert len(obj.data.materials) == 1
        material = obj.data.materials[0]
        assert obj.name == material.name+'_Mesh', (asset_id, obj.name)
        materials.add(material.name)
        assert not obj.data.color_attributes
        assert not any(node.type == 'TEX_IMAGE' for node in material.node_tree.nodes)
        for face in obj.data.polygons:
            assert len(face.vertices) == 3
            assert face.normal.z >= -.5, (asset_id, 'downward face')
            triangles += 1
    assert len(meshes) == len(materials) <= 3, (asset_id, materials)
    if asset_id in ('bit_pebbles', 'bit_gravel', 'bit_stone'):
        assert materials <= {'Rock', 'RockLight'}, (asset_id, materials)
    assert 0 < triangles < BUDGET[asset_id], (asset_id, triangles)
    low, high = bounds(meshes)
    assert abs(low.z) <= .002, (asset_id, low.z)
    assert abs(low.x+high.x) <= .002 and abs(low.y+high.y) <= .002, asset_id
    for actual, expected in zip(high-low, EXPECTED[asset_id]):
        assert .75*expected <= actual <= 1.25*expected, (asset_id, tuple(high-low))
    assert data['collision'] == {'kind': 'none'} and data['occlusion_parts'] == []
    data['dimensions_m'] = [round(high.x-low.x, 3), round(high.z-low.z, 3), round(high.y-low.y, 3)]
    data['bounds_min_y'] = round(low.z, 6)
    data['triangles'] = triangles
    data['file'] = asset_id+'.glb'


def finish_asset(root, keep, data):
    merge_and_cull(keep)
    bpy.context.view_layer.update()
    low, high = bounds(list(keep.children))
    shift = Vector(((low.x+high.x)/2, (low.y+high.y)/2, low.z))
    for obj in keep.children:
        for vertex in obj.data.vertices:
            vertex.co -= shift
        obj.data.update()
    bpy.context.view_layer.update()
    validate_asset(root, data)
    bpy.ops.object.select_all(action='DESELECT')
    objects = [root]+list(root.children_recursive)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    result = bpy.ops.export_scene.gltf(
        filepath=str(PACK/data['file']), export_format='GLB', use_selection=True,
        export_extras=True, export_yup=True, export_animations=False)
    assert result == {'FINISHED'}, (data['id'], 'GLB export failed', result)
    # Blender names are globally unique. Archive parts AFTER validating/exporting
    # exact names so the next asset can claim Keep and <material>_Mesh unchanged.
    for obj in objects:
        if obj is not root:
            obj['contract_name'] = obj.name
            obj.name = data['id']+'__'+obj.name


def main():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    M.update({name: mat(name, color, rough) for name, (color, rough) in PALETTE.items()})
    assert all(material.name == name for name, material in M.items()), 'palette name collision'
    PACK.mkdir(parents=True, exist_ok=True)
    (ROOT/'source').mkdir(parents=True, exist_ok=True)
    assets, roots = [], []
    for asset_id, name, builder, note in BUILDERS:
        root, keep, data = start(asset_id, name, note)
        builder(keep)
        finish_asset(root, keep, data)
        assets.append(data)
        roots.append(root)
    assert len(assets) == 5 and {asset['id'] for asset in assets} == set(EXPECTED)
    manifest = dict(revision=1, contract=CONTRACT, date='2026-10-22', units='metres',
                    up_axis='+Y', status='ground-border art; not integrated or render-reviewed',
                    assets=assets)
    (PACK/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    # Source-library layout only; GLB pivots and manifest measurements stay local.
    for i, root in enumerate(roots):
        root.location = ((i % 2)*1.8, (i//2)*1.3, 0)
    result = bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source'/'ground-bits.blend'))
    assert result == {'FINISHED'}, ('blend save failed', result)
    for asset in assets:
        print('GROUND_BIT_READY', asset['id'], asset['triangles'])
    print('GROUND_BITS_BUILT 5')


if __name__ == '__main__':
    main()
