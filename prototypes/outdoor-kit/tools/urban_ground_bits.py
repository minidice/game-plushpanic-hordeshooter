"""Seven lightweight urban ground decorations; Blender Z-up, metres.

Authoring only: the reviewer runs Blender and checks the exports/renders.
Run: blender --background --python prototypes/outdoor-kit/tools/urban_ground_bits.py
Helpers copied from ground_bits.py; no other kit generator is imported.
"""
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game' / 'assets' / 'urban-bits'
CONTRACT = 'plushpanic_outdoor_v1'
# Authoring X/Y/Z; exported manifest dimensions use game X/Y/Z = X/Z/Y here.
EXPECTED = {
    'bit_manhole': (.9, .9, .03),
    'bit_drain': (.9, .45, .03),
    'bit_litter': (.8, .6, .07),
    'bit_leaves': (.7, .5, .04),
    'bit_rubble': (.7, .5, .12),
    'bit_crack_weed': (.3, .25, .15),
    'bit_puddle': (1.4, .9, .006),
}
BUDGET = dict(zip(EXPECTED, (160, 160, 160, 140, 160, 90, 60)))
PALETTE = {
    'Iron': ('3e4144', .6), 'IronLight': ('5d6266', .6),
    'Paper': ('e6e0cf', .9), 'Can': ('b8452f', .4),
    'Cup': ('d9d3c2', .8), 'Leaf': ('a8743a', .9),
    'LeafDark': ('7b5530', .9), 'Brick': ('9e5640', .9),
    'Concrete': ('a4aaa1', .95), 'Weed': ('6c8053', .9),
    'WeedTip': ('8a9a5b', .9), 'Water': ('2f3a44', .08),
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


def blade(parent, center, angle, reach, height, width, tip_material='WeedTip'):
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
    obj = mesh(parent, 'Blade', vertices, faces, 'Weed')
    obj.data.materials.append(M[tip_material])
    for face in obj.data.polygons:
        if face.index >= 4:
            face.material_index = 1
    return obj


def box(parent, name, at, size, material):
    """Open-bottom bar: ten triangles, no modifiers or hidden bottom cap."""
    x, y, z = at
    sx, sy, sz = size
    vertices = [(x+dx*sx/2, y+dy*sy/2, z+dz*sz)
                for dz in (0, 1) for dx, dy in ((-1, -1), (1, -1),
                                               (1, 1), (-1, 1))]
    faces = [(i, (i+1) % 4, (i+1) % 4+4, i+4) for i in range(4)]
    faces.append((4, 5, 6, 7))
    return mesh(parent, name, vertices, faces, material)


def place(obj, center, yaw):
    """Bake only a yaw and XY translation; all models are authored Z-up."""
    c, s = math.cos(yaw), math.sin(yaw)
    for vertex in obj.data.vertices:
        x, y, z = vertex.co
        vertex.co = (center[0]+c*x-s*y, center[1]+s*x+c*y, z)
    obj.data.update()


def ground_open_piece(obj):
    """Cull in final geometry, then ground this individual piece (no scaling).

    A lying can/cup loses its underside and possibly its lowest vertices.
    Ground it before scattering so other pieces cannot leave it floating.
    Triangulate first, matching the final material merge's face normals.
    """
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bmesh.ops.triangulate(bm, faces=list(bm.faces))
        bm.normal_update()
        remove = [face for face in bm.faces
                  if face.normal.z < -.5 or face.calc_area() < 1e-10]
        bmesh.ops.delete(bm, geom=remove, context='FACES')
        loose = [vertex for vertex in bm.verts if not vertex.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context='VERTS')
        assert bm.verts and bm.faces, obj.name
        bottom = min(vertex.co.z for vertex in bm.verts)
        for vertex in bm.verts:
            vertex.co.z -= bottom
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.update()


def manhole(keep):
    # 16-sided cover: 112 triangles, including the lowered centre fan.
    # The outer lip reaches .03; the recessed centre is .02 above the road.
    sides = 16
    vertices = []
    for radius, z in ((.45, 0), (.45, .03), (.395, .03), (.395, .02)):
        vertices.extend((radius*math.cos(math.tau*i/sides),
                         radius*math.sin(math.tau*i/sides), z)
                        for i in range(sides))
    vertices.append((0, 0, .02))
    faces = []
    for ring in range(3):
        for i in range(sides):
            nxt = (i+1) % sides
            a, b = ring*sides, (ring+1)*sides
            faces.append((a+i, a+nxt, b+nxt, b+i))
    faces.extend((4*sides, 3*sides+i, 3*sides+(i+1) % sides)
                 for i in range(sides))
    mesh(keep, 'Cover', vertices, faces, 'Iron')
    # Four raised ribs, 40 triangles: 152 total before bottom culling.
    # Crossing ribs have different top heights, avoiding coplanar crossings.
    for offset in (-.12, .12):
        box(keep, 'GridX', (0, offset, .02), (.58, .024, .005), 'IronLight')
        box(keep, 'GridY', (offset, 0, .02), (.024, .58, .007), 'IronLight')


def drain(keep):
    # Four frame bars and six long ribs. Every slot stays genuinely open.
    for y in (-.20, .20):
        box(keep, 'FrameX', (0, y, 0), (.90, .05, .03), 'Iron')
    for x in (-.425, .425):
        box(keep, 'FrameY', (x, 0, 0), (.05, .35, .03), 'Iron')
    for i in range(6):
        box(keep, 'DrainRib', (0, -.145+i*.058, 0),
            (.80, .026, .027), 'Iron')


def paper(keep, center, size, yaw, fold):
    sx, sy = size
    # Two creased panels with raised middle ridge and lifted opposite corners.
    vertices = [(-sx/2, -sy/2, 0), (0, -sy/2, fold),
                (sx/2, -sy/2, .003), (-sx/2, sy/2, .005),
                (0, sy/2, fold*.7), (sx/2, sy/2, 0)]
    obj = mesh(keep, 'FoldedPaper', vertices,
               [(0, 1, 4), (0, 4, 3), (1, 2, 5), (1, 5, 4)], 'Paper')
    place(obj, center, yaw)


def crushed_can(keep, center, yaw):
    # Three uneven octagonal rings along X: a crushed, lying cylinder.
    # Flattened cross-section and a pinched/twisted middle read from above.
    sides = 8
    vertices = []
    for ring, (x, ry, rz) in enumerate(((-.075, .050, .032),
                                       (0, .037, .022), (.065, .046, .030))):
        for i in range(sides):
            angle = math.tau*i/sides+math.pi/8
            dent = .80 if (i+ring) % 5 == 0 else 1
            vertices.append((x+.006*math.sin(i*2+ring),
                             ry*math.cos(angle)*dent,
                             .034+rz*math.sin(angle)*dent))
    faces = []
    for ring in range(2):
        for i in range(sides):
            nxt = (i+1) % sides
            a, b = ring*sides, (ring+1)*sides
            faces.append((a+i, a+nxt, b+nxt, b+i))
    faces.extend((tuple(reversed(range(sides))), tuple(range(16, 24))))
    obj = mesh(keep, 'CrushedCan', vertices, faces, 'Can')
    ground_open_piece(obj)
    place(obj, center, yaw)


def fallen_cup(keep, center, yaw):
    # Hollow tapered cup lying on X. The open mouth has a thick octagonal lip;
    # inner walls and a recessed inner base make the opening legible above.
    sides = 8
    vertices = []
    for x, radius in ((-.075, .024), (.075, .035),
                       (.075, .029), (-.067, .018)):
        vertices.extend((x, radius*math.cos(math.tau*i/sides),
                         .035+radius*math.sin(math.tau*i/sides))
                        for i in range(sides))
    faces = []
    for ring in range(3):
        for i in range(sides):
            nxt = (i+1) % sides
            a, b = ring*sides, (ring+1)*sides
            faces.append((a+i, a+nxt, b+nxt, b+i))
    faces.extend((tuple(reversed(range(sides))), tuple(range(24, 32))))
    obj = mesh(keep, 'FallenCup', vertices, faces, 'Cup')
    ground_open_piece(obj)
    place(obj, center, yaw)


def litter(keep):
    # Five separated pieces; no common ground patch.
    paper(keep, (-.26, -.20), (.23, .16), -.18, .021)
    paper(keep, (.00, .19), (.21, .17), .31, .016)
    paper(keep, (.26, -.19), (.19, .15), -.35, .025)
    crushed_can(keep, (-.27, .20), -.35)
    fallen_cup(keep, (.28, .19), .40)


def leaf(keep, center, size, yaw, height, material):
    # Six-point outline, peaked centre and a lightly curled tip, six triangles.
    sx, sy = size
    outline = ((-.5, 0), (-.20, -.5), (.25, -.35),
               (.5, 0), (.15, .5), (-.28, .42))
    vertices = [(x*sx, y*sy, .006 if i == 3 else 0)
                for i, (x, y) in enumerate(outline)]
    vertices.append((0, 0, height))
    faces = [(6, i, (i+1) % 6) for i in range(6)]
    obj = mesh(keep, 'CurledLeaf', vertices, faces, material)
    place(obj, center, yaw)


def leaves(keep):
    # Ten scattered leaves, 60 triangles; no stems or underlying dirt slab.
    layout = ((-.26, -.17, .2), (-.06, -.20, -.4), (.17, -.17, .7),
              (.28, -.02, 1.1), (-.28, .02, -.9), (-.09, -.03, .4),
              (.09, .04, -.3), (-.23, .19, .5), (-.02, .18, -.6),
              (.21, .18, .1))
    for i, (x, y, yaw) in enumerate(layout):
        leaf(keep, (x, y), (.16 if i % 3 else .18, .075), yaw,
             .04 if i == 5 else .018+.004*(i % 3),
             'LeafDark' if i % 3 == 0 else 'Leaf')


def fragment(keep, center, size, yaw, material, bevel=False):
    # A clipped, slightly uneven five-sided chunk, with no bottom.
    sx, sy, height = size
    outline = ((-.5, -.5), (.32, -.5), (.5, -.18), (.44, .5), (-.5, .37))
    vertices = [(x*sx, y*sy, 0) for x, y in outline]
    if bevel:
        vertices.extend((x*sx, y*sy, height*.75) for x, y in outline)
        vertices.extend((x*sx*.82, y*sy*.82, height) for x, y in outline)
        rings = 3
    else:
        vertices.extend((x*sx*.77, y*sy*.82, height*(.8 if i == 2 else 1))
                        for i, (x, y) in enumerate(outline))
        rings = 2
    faces = []
    for ring in range(rings-1):
        for i in range(5):
            nxt = (i+1) % 5
            a, b = ring*5, (ring+1)*5
            faces.append((a+i, a+nxt, b+nxt, b+i))
    vertices.append((0, 0, height))
    top = (rings-1)*5
    faces.extend((rings*5, top+i, top+(i+1) % 5) for i in range(5))
    obj = mesh(keep, 'Fragment', vertices, faces, material)
    place(obj, center, yaw)


def rubble(keep):
    for x, y, sx, sy, h, yaw in (
            (-.23, -.16, .19, .12, .09, -.20),
            (.01, -.17, .18, .13, .075, .25),
            (.23, -.12, .17, .13, .085, -.40)):
        fragment(keep, (x, y), (sx, sy, h), yaw, 'Brick', bevel=True)
    for x, y, sx, sy, h, yaw in (
            (-.25, .16, .16, .14, .12, .35),
            (-.02, .17, .17, .13, .08, -.30),
            (.23, .16, .16, .13, .10, .55)):
        fragment(keep, (x, y), (sx, sy, h), yaw, 'Concrete')


def crack_weed(keep):
    # One sparse six-leaf plant, at most 72 triangles before underside culling.
    for i in range(6):
        angle = math.tau*i/6
        reach = 1/math.sqrt((math.cos(angle)/.15)**2+(math.sin(angle)/.125)**2)
        blade(keep, (0, 0), angle, reach, (.15, .10, .125)[i % 3], .013,
              'WeedTip' if i % 2 == 0 else 'Weed')


def puddle(keep):
    # 18 top triangles plus 36 side triangles, exactly .006 m thick.
    plate(keep, 'Puddle', (1.4, .9), .006, 'Water', sides=18, phase=.7)


BUILDERS = (
    ('bit_manhole', '맨홀 뚜껑', manhole, '16각 주철 뚜껑, 높은 테두리와 볼록한 격자 4줄.'),
    ('bit_drain', '빗물받이', drain, '테두리 틀과 긴 살 6개, 살 사이가 뚫린 격자.'),
    ('bit_litter', '도시 쓰레기 조각', litter, '접힌 종이 3장, 눌린 캔과 쓰러진 속 빈 종이컵.'),
    ('bit_leaves', '마른 낙엽', leaves, '조금씩 휜 낙엽 10장, 두 가지 갈색.'),
    ('bit_rubble', '벽돌과 콘크리트 조각', rubble, '모서리 깨진 벽돌 3개와 콘크리트 3개.'),
    ('bit_crack_weed', '틈 잡초', crack_weed, '바닥 틈에서 드물게 벌어진 작은 잎 6개.'),
    ('bit_puddle', '작은 물웅덩이', puddle, '불규칙한 18각 윤곽, 두께 0.006 m의 어두운 물 판.'),
)


def start(asset_id, name, note):
    root = empty(asset_id)
    root['asset_contract'] = CONTRACT
    root['ground_bit'] = True
    keep = empty('Keep', root)
    keep['visibility_role'] = 'keep'
    data = dict(id=asset_id, name=name, category='도시 바닥', note=note,
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
    assert len(assets) == 7 and {asset['id'] for asset in assets} == set(EXPECTED)
    manifest = dict(revision=1, contract=CONTRACT, date='2026-10-28', units='metres',
                    up_axis='+Y', status='urban ground art; not integrated or render-reviewed',
                    assets=assets)
    (PACK/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    # Source-library layout only; GLB pivots and manifest measurements stay local.
    for i, root in enumerate(roots):
        root.location = ((i % 2)*1.8, (i//2)*1.3, 0)
    result = bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source'/'urban-bits.blend'))
    assert result == {'FINISHED'}, ('blend save failed', result)
    for asset in assets:
        print('URBAN_BIT_READY', asset['id'], asset['triangles'])
    print('URBAN_BITS_BUILT 7')


if __name__ == '__main__':
    main()
