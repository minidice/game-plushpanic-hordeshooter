"""Two retained camera-facing outdoor edge masses; Blender Z-up, metres.

Authoring only: the reviewer runs Blender and inspects the exported assets.
Run: blender --background --python prototypes/outdoor-kit/tools/edge_kit.py
Helpers/palette copied from the kit; never import its build-on-import modules.
Front is -Y. These open shells must only be placed outside the playable area.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game' / 'assets' / 'edge-kit'
CONTRACT = 'plushpanic_outdoor_v1'
# Authoring order X/Y/Z. Manifest dimensions follow the existing game X/Y/Z.
EXPECTED = {
    'edge_shrub_band': (5, 1.4, 1),
    'edge_rock_wall': (6, 2.5, 2.6),
}
BUDGET = dict(zip(EXPECTED, (900, 1500)))
PALETTE = {
    'PineDark': ('37594d', .90), 'Pine': ('4f7b57', .88),
    'LeafTip': ('7e985b', .86), 'Leaf': ('668a59', .90),
    'LeafLight': ('91a765', .90), 'Grass': ('6c8053', .95),
    'Rock': ('667a7c', .92), 'RockLight': ('8c9c96', .92),
    'Moss': ('637952', .97), 'Bark': ('6b5440', .95),
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
    material.use_backface_culling = True
    material['wet_eligible'] = True
    material['snow_eligible'] = True
    return material


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def finish(obj, name, parent, at, scale, material):
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.scale = scale
    obj.data.materials.append(M[material])
    return obj


def cyl(parent, name, at, radius, height, material, r2=None, verts=10):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=radius,
        radius2=radius if r2 is None else r2, depth=height)
    return finish(bpy.context.object, name, parent, at, (1, 1, 1), material)


def mesh(parent, name, vertices, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    data.materials.append(M[material])
    return obj


def lobe(parent, at, size, material, sides=10, rocky=False):
    """Low-resolution plump crown/stone, with a bevelled shoulder and round top.

    Bottom is open already; remaining rear/downward faces are culled after join.
    Slight deterministic rock asymmetry keeps the wall from looking like spheres.
    """
    profile = ((0, .78), (.22, 1), (.66, .96), (.89, .65), (1, .12))
    vertices = []
    for row, (z, radius) in enumerate(profile):
        for j in range(sides):
            a = math.tau*j/sides
            wobble = 1 + (.06*math.sin(j*2.7+at[0]) if rocky else 0)
            vertices.append((at[0]+math.cos(a)*radius*size[0]*wobble,
                             at[1]+math.sin(a)*radius*size[1]*wobble,
                             at[2]+z*size[2]))
    faces = []
    for row in range(len(profile)-1):
        for j in range(sides):
            a = row*sides+j
            b = row*sides+(j+1) % sides
            faces.append((a, b, b+sides, a+sides))
    faces.append(tuple(range(4*sides, 5*sides)))
    obj = mesh(parent, 'RoundedMass', vertices, faces, material)
    for face in obj.data.polygons:
        face.use_smooth = not rocky
    return obj


def band(parent, width, depth, height, material, stations=10):
    """Continuous thick foliage/stone core, open underneath and at the rear.

    Full-width end sections meet at the nominal tile pitch. Matching end heights
    and broad shoulders hide seams; rounded lobes overlap this recessed core.
    This is real roof/front/side geometry, not a billboard or a ground plane.
    """
    vertices = []
    for i in range(stations+1):
        x = -width/2 + width*i/stations
        h = height*(1-.035*math.sin(math.pi*i/stations)**2)
        # Clockwise in Y/Z: front foot -> front shoulder -> roof -> rear foot.
        for y, z in ((-.5, 0), (-.5, .68), (-.40, .94),
                     (0, 1), (.40, .94), (.5, .68), (.5, 0)):
            vertices.append((x, y*depth, z*h))
    faces = []
    for i in range(stations):
        for j in range(6):
            a = i*7+j
            faces.append((a, a+7, a+8, a+1))
    faces.extend((tuple(range(7)), tuple(reversed(range(stations*7, (stations+1)*7)))))
    return mesh(parent, 'ContinuousBand', vertices, faces, material)


def shrub_band(k):
    band(k, 5, 1.4, .93, 'Leaf', 10)
    for i in range(8):
        lobe(k, (-2.15+i*.615, -.04, .12), (.47, .62, .88 if i % 3 else .84),
             'LeafLight' if i % 3 == 1 else 'Leaf', 8)
    for x in (-1.75, .05, 1.65):
        lobe(k, (x, -.55, 0), (.25, .18, .33), 'Rock', 8, True)
    for i in range(6):
        for dx, h in ((-.08, .28), (0, .42), (.08, .34)):
            cyl(k, 'GrassTuft', (-1.95+i*.78+dx, -.63, h/2),
                .065, h, 'Grass', .008, 5)


def rock_wall(k):
    band(k, 6, 2.5, 1.72, 'Rock', 8)
    for i, (x, h) in enumerate(((-2.25, 2.25), (-1.1, 2.5), (0, 2.32),
                                (1.13, 2.6), (2.24, 2.30))):
        obj = lobe(k, (x, .02, 0), (.86, 1.12, h), 'Rock', 10, True)
        obj.data.materials.append(M['RockLight'])
        obj.data.materials.append(M['Moss'])
        # Surface materials, not floating moss slabs; all are merged by material.
        for face in obj.data.polygons:
            if face.normal.z > .55:
                face.material_index = 2
            elif (face.index+i) % 4 == 0:
                face.material_index = 1


BUILDERS = (
    ('edge_shrub_band', '외곽 덤불 띠', shrub_band, '덤불 8개·풀 6다발·작은 돌 3개.'),
    ('edge_rock_wall', '외곽 이끼 바위벽', rock_wall, '바위 5개·연결 벽·표면 이끼.'),
)


def start(asset_id, name, note):
    root = empty(asset_id)
    root['asset_contract'] = CONTRACT
    root['edge_only'] = True
    keep = empty('Keep', root)
    keep['visibility_role'] = 'keep'
    data = dict(id=asset_id, name=name, category='외곽 배경', note=note,
                collision={'kind': 'none'}, occlusion_parts=[])
    return root, keep, data


def merge_and_cull(keep):
    """Bake transforms and merge faces by material, then cull in Blender axes.

    Rebuilding also drops orphan vertices: deleted rear faces cannot inflate AABB.
    Triangulate before measuring normals so validation matches exported triangles.
    """
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
            remove = [f for f in bm.faces
                      if f.normal.y > .35 or f.normal.z < -.5 or f.calc_area() < 1e-10]
            bmesh.ops.delete(bm, geom=remove, context='FACES')
            loose = [v for v in bm.verts if not v.link_faces]
            if loose:
                bmesh.ops.delete(bm, geom=loose, context='VERTS')
            bm.to_mesh(obj.data)
        finally:
            bm.free()
        obj.data.update()
        assert obj.data.polygons, (name, 'all material faces were culled')


def vertices_of(meshes):
    return [obj.matrix_world @ v.co for obj in meshes for v in obj.data.vertices]


def bounds(meshes):
    vertices = vertices_of(meshes)
    return (Vector(tuple(min(v[i] for v in vertices) for i in range(3))),
            Vector(tuple(max(v[i] for v in vertices) for i in range(3))))


def projected_coverage(meshes, low, high):
    """Approximate orthographic -Y silhouette using exact triangle scanline unions.

    Check 48 height samples in [0, min(1.5, model height)]: a 1m shrub cannot
    cover empty space above its crown. Return area-average and worst row coverage.
    Only front-facing triangles count, matching backface-culling materials.
    """
    triangles = []
    for obj in meshes:
        for face in obj.data.polygons:
            if face.normal.y < -1e-6:
                triangles.append([obj.matrix_world @ obj.data.vertices[i].co for i in face.vertices])
    ratios = []
    for row in range(48):
        z = low.z + min(1.5, high.z-low.z)*(row+.5)/48
        spans = []
        for tri in triangles:
            hits = []
            for a, b in zip(tri, tri[1:]+tri[:1]):
                if min(a.z, b.z) <= z < max(a.z, b.z):
                    hits.append(a.x+(b.x-a.x)*(z-a.z)/(b.z-a.z))
            if len(hits) >= 2:
                spans.append((min(hits), max(hits)))
        covered, end = 0.0, low.x
        for a, b in sorted(spans):
            a, b = max(a, low.x), min(b, high.x)
            covered += max(0, b-max(a, end))
            end = max(end, b)
        ratios.append(covered/(high.x-low.x))
    return sum(ratios)/len(ratios), min(ratios)


def validate_asset(root, data):
    asset_id = data['id']
    assert root.name == asset_id and root['asset_contract'] == CONTRACT
    assert root['edge_only'] is True and root.location.length < 1e-6
    assert len(root.children) == 1 and root.children[0].name == 'Keep'
    keep = root.children[0]
    assert keep.type == 'EMPTY'
    meshes = list(keep.children)
    assert meshes and all(obj.type == 'MESH' and not obj.children for obj in meshes)
    materials = set()
    area, rear_area, triangles = 0.0, 0.0, 0
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
            area += face.area
            rear_area += face.area if face.normal.y > .35 else 0
            triangles += 1
    assert len(meshes) == len(materials) <= 4, (asset_id, materials)
    assert 0 < triangles < BUDGET[asset_id], (asset_id, triangles)
    assert area > 0 and rear_area/area <= .03, (asset_id, 'rear area', rear_area/area)
    low, high = bounds(meshes)
    assert abs(low.z) <= .002, (asset_id, low.z)
    assert abs(low.x+high.x) <= .002 and abs(low.y+high.y) <= .002
    for actual, expected in zip(high-low, EXPECTED[asset_id]):
        assert .8*expected <= actual <= 1.2*expected, (asset_id, tuple(high-low))
    coverage, worst = projected_coverage(meshes, low, high)
    assert coverage >= .90, (asset_id, 'front coverage', coverage)
    if high.z >= 1.5:
        assert worst >= .90, (asset_id, 'lowest row coverage', worst)
    data['dimensions_m'] = [round(high.x-low.x, 3), round(high.z-low.z, 3), round(high.y-low.y, 3)]
    data['bounds_min_y'] = round(low.z, 6)
    data['triangles'] = triangles
    data['file'] = asset_id+'.glb'


def finish_asset(root, keep, data):
    merge_and_cull(keep)
    bpy.context.view_layer.update()
    low, high = bounds(list(keep.children))
    shift = Vector(((low.x+high.x)/2, (low.y+high.y)/2, low.z))
    # Keep all node transforms identity; geometry is origin-centred and grounded.
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
    # Blender names are globally unique. Prefix library parts only AFTER export;
    # the next model can then claim literal Keep / <material>_Mesh names.
    for obj in objects:
        if obj is not root:
            obj['contract_name'] = obj.name
            obj.name = data['id']+'__'+obj.name


def main():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    # Dedicated background build: remove unused names left by the startup file.
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    M.update({name: mat(name, color, rough) for name, (color, rough) in PALETTE.items()})
    assert all(material.name == name for name, material in M.items()), 'palette name collision'
    PACK.mkdir(parents=True, exist_ok=True)
    (ROOT/'source').mkdir(parents=True, exist_ok=True)
    assets = []
    roots = []
    for asset_id, name, builder, note in BUILDERS:
        root, keep, data = start(asset_id, name, note)
        builder(keep)
        finish_asset(root, keep, data)
        assets.append(data)
        roots.append(root)
    assert len(assets) == 2 and {asset['id'] for asset in assets} == set(EXPECTED)
    manifest = dict(revision=1, contract=CONTRACT, date='2026-10-22', units='metres',
                    up_axis='+Y', status='edge-only art; not integrated or render-reviewed', assets=assets)
    (PACK/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    # Library layout is applied after export, never to GLB pivots or measurements.
    for i, root in enumerate(roots):
        root.location = ((i % 3)*15, (i//3)*12, 0)
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version = 0
    result = bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source'/'edge-kit.blend'))
    assert result == {'FINISHED'}, ('blend save failed', result)
    for asset in assets:
        print('EDGE_KIT_READY', asset['id'], asset['triangles'])
    print('EDGE_KIT_BUILT 2')


if __name__ == '__main__':
    main()
