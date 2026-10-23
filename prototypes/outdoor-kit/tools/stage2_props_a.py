"""Stage 2 traversal props, bundle A. Standalone Blender script; metres, Z up.

Source brief: origin/claude/confident-hypatia-hq67dc at 9412666,
doc/model-briefs/stage2-landmarks.md (2026-10-23), sections 1, 2, 5-7.
User-authorized Blender generation/render review completed separately with
stage2_props_a_review.py; game integration remains separate. Do not import the
existing kit generators (side effects).

Reviewer command:
  blender --background --python prototypes/outdoor-kit/tools/stage2_props_a.py
Outputs: game/assets/stage2-props/{five GLBs,manifest.json} and
  prototypes/outdoor-kit/source/stage2-props-a.blend
The manifest merge preserves other bundles and all unrelated top-level metadata.
Append -- followed by asset IDs to export/replace only those records. The blend
library is always rebuilt in full; omitted GLBs and manifest records stay intact.
The cabin slab is the explicit brief exception to the no-walking-surface rule;
the station only has a perimeter. Replace the game's placeholder cabin floor.
"""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game/assets/stage2-props'
SOURCE = REPO / 'prototypes/outdoor-kit/source/stage2-props-a.blend'
CONTRACT = 'plushpanic_outdoor_v1'
BUDGET = dict(ladder_section=600, ladder_top=500, zipline_anchor=1500,
              gondola_cabin=3000, gondola_station=3000)
# Overall extents in exported X/Y/Z order, including projecting fittings.
EXPECTED = dict(ladder_section=(.95, 1.2, .20), ladder_top=(.95, 1.05, .71),
                zipline_anchor=(.60, 3.20, 1.00),
                gondola_cabin=(6.0, 3.73, 5.0),
                gondola_station=(6.8, 5.6, 6.2))
PALETTE = dict(Concrete='a4aaa1', Metal='56636a', MetalLight='a8b7b1',
               Rust='98674f', WoodDark='5e4632', Ochre='c7a35c',
               Red='b96551', Hazard='e2b84a', Ink='2a2a2a')
M = {}


# Small geometry helpers adapted from transition_props.py. No generator imports.
def material(name, color):
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    rgb = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = .55 if name.startswith('Metal') else .85
    shader.inputs['Metallic'].default_value = .4 if name.startswith('Metal') else 0
    result['wet_eligible'] = True
    result['snow_eligible'] = True
    return result


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def mesh(parent, name, vertices, faces, color):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    data.materials.append(M[color])
    return obj


def box(parent, name, at, size, color, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.scale = size
    obj.data.materials.append(M[color])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft toy edges', 'BEVEL')
        mod.width = min(bevel, min(size) / 3)
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def beam(parent, name, a, b, radius, color, sides=8):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius,
                                      depth=(b - a).length, end_fill_type='NGON')
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = (a + b) / 2
    obj.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    obj.data.materials.append(M[color])
    return obj


def rail(parent, name, x, y, bottom, top, color, width=.07):
    """Rounded-square extrusion. No end bevel: identical stacked cross sections."""
    h, cut = width / 2, width * .17
    outline = [(-h + cut, -h), (h - cut, -h), (h, -h + cut), (h, h - cut),
               (h - cut, h), (-h + cut, h), (-h, h - cut), (-h, -h + cut)]
    vertices = [(x + u, y + v, z) for z in (bottom, top) for u, v in outline]
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
    return mesh(parent, name, vertices, faces, color)


def tube_path(parent, name, points, radius, color):
    """Continuous chamfered-square handrail, matching the straight rails."""
    points = [Vector(p) for p in points]
    vertices, faces = [], []
    for i, point in enumerate(points):
        tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        side = Vector((1, 0, 0))
        other = tangent.cross(side).normalized()
        # The first 0.15 m is an inserted tongue inside the final section.
        # Insetting its bottom prevents coincident side faces in the assembly.
        h = radius * (.94 if i == 0 else 1)
        cut = h * .34
        outline = [(-h + cut, -h), (h - cut, -h), (h, -h + cut), (h, h - cut),
                   (h - cut, h), (-h + cut, h), (-h, h - cut), (-h, -h + cut)]
        for u, v in outline:
            vertices.append(point + u * side + v * other)
    for i in range(len(points) - 1):
        for j in range(8):
            a, b = i * 8 + j, i * 8 + (j + 1) % 8
            faces.append((a, b, b + 8, a + 8))
    faces += [tuple(reversed(range(8))),
              tuple((len(points) - 1) * 8 + j for j in range(8))]
    return mesh(parent, name, vertices, faces, color)


def pulley(parent, name, center, radius, width, color='MetalLight', axis='X', segments=12):
    """Open spoked wheel with two lips and a recessed cable channel; no texture."""
    center = Vector(center)
    # Revolved section in (radius, axle distance), clockwise around solid rim.
    profile = [(radius * .65, -width / 2), (radius, -width / 2),
               (radius, -width * .28), (radius * .84, 0),
               (radius, width * .28), (radius, width / 2),
               (radius * .65, width / 2)]

    def point(r, angle, along):
        u, v = r * math.cos(angle), r * math.sin(angle)
        return center + Vector((along, u, v) if axis == 'X' else (u, along, -v))

    vertices = [point(r, i * math.tau / segments, d)
                for i in range(segments) for r, d in profile]
    count = len(profile)
    faces = []
    for i in range(segments):
        for j in range(count):
            faces.append((i * count + j, ((i + 1) % segments) * count + j,
                          ((i + 1) % segments) * count + (j + 1) % count,
                          i * count + (j + 1) % count))
    wheel = mesh(parent, name, vertices, faces, color)
    for i in range(4):
        beam(parent, name + 'Spoke', center, point(radius * .72, i * math.pi / 2, 0),
             radius * .09, color, 6)
    beam(parent, name + 'Hub', point(0, 0, -width * .7),
         point(0, 0, width * .7), radius * .21, 'Metal', 8)
    return wheel


def start(asset_id, name, note, occlusion=None):
    root = empty(asset_id)
    root['asset_contract'] = CONTRACT
    keep = empty('Keep', root)
    keep['visibility_role'] = 'keep'
    data = dict(id=asset_id, name=name, category='Stage 2 이동 장치', note=note,
                collision=dict(kind='none'), occlusion_parts=[])
    upper = None
    if occlusion:
        upper = empty('Occlude_' + occlusion, root)
        upper['visibility_role'] = 'occlude'
        data['occlusion_parts'].append(upper.name)
    return root, keep, upper, data


def ladder_section():
    root, keep, _, data = start('ladder_section', '사다리 한 단',
        '1.2 m 간격으로 쌓기. 폭 0.95 m, 적갈색 사각 레일 0.15 m, 중심 X=±0.4, Blender Y=-0.025. '
        '가로대 높이 0.15/0.45/0.75/1.05 m. 게임 +Z 쪽에서 오름.')
    for x in (-.4, .4):
        rail(keep, 'ContinuousRail', x, -.025, 0, 1.2, 'Red', .15)
        box(keep, 'WallBracket', (x, .075, .6), (.12, .05, .14), 'Metal', .008)
    for z in (.15, .45, .75, 1.05):
        box(keep, 'BroadTread', (0, -.005, z), (.80, .18, .11), 'Ochre', .012)
    return root, data, {}


def ladder_top():
    root, keep, _, data = start('ladder_top', '사다리 위 손잡이',
        '피벗=윗단 가장자리. 바닥 0, 하단 -0.15, 최고 0.9 m. '
        '레일 중심은 section과 동일. 아치는 뒤(Blender +Y)로 0.56 m 휨.')
    for x in (-.4, .4):
        points = [(x, -.025, -.15), (x, -.025, 0), (x, -.025, .545)]
        for i in range(1, 7):
            angle = math.pi * i / 6
            points.append((x, -.025 + .28 * (1 - math.cos(angle)), .545 + .28 * math.sin(angle)))
        points.append((x, .535, .42))
        tube_path(keep, 'ArchGrip', points, .075, 'Red')
        box(keep, 'YellowBand', (x, -.103, .45), (.132, .006, .08), 'Hazard', 0)
        # Black inset stripe is raised on the camera-facing surface, not coplanar.
        box(keep, 'DarkStripe', (x, -.108, .45), (.11, .004, .025), 'Ink', 0)
    return root, data, {}


def zipline_anchor():
    root, keep, upper, data = start('zipline_anchor', '짚라인 고정 기둥',
        '양 끝 공용. 발판 윗면 0.4 m. 도르래 중심 높이 3.0 m. '
        '기존 Zipline.BuildLook 줄 높이 2.8 m와 차이: 연결 시 조정 필요.', 'Pole')
    box(keep, 'ConcreteShoe', (0, 0, .1), (.6, .6, .2), 'Concrete', .035)
    beam(keep, 'LowerPole', (0, 0, .16), (0, 0, 1.15), .14, 'WoodDark', 12)
    beam(upper, 'UpperPole', (0, 0, 1.15), (0, 0, 3.2), .14, 'WoodDark', 12)
    for x in (-.25, .25):
        beam(keep, 'DiagonalBrace', (x, .12, .22), (0, .05, .85), .045, 'Metal')
    box(keep, 'Step', (0, -.325, .35), (.48, .40, .1), 'MetalLight', .018)
    beam(keep, 'StepBrace', (0, -.06, .2), (0, -.38, .31), .045, 'Metal')
    box(upper, 'Arm', (0, .20, 3.12), (.14, .54, .12), 'Metal', .025)
    box(upper, 'AxleFork', (.09, .4, 3.05), (.06, .12, .22), 'Metal', .012)
    pulley(upper, 'CableWheel', (0, .4, 3.0), .125, .10)
    # Smooth wood-to-metal collar, deliberately part of the occluding pole.
    beam(upper, 'Collar', (0, 0, 2.94), (0, 0, 3.04), .15, 'Metal', 12)
    return root, data, {'cable_center': Vector((0, .4, 3.0))}


def gondola_cabin():
    root, keep, upper, data = start('gondola_cabin', '개방형 곤돌라 객차',
        '피벗=6×5 m 바닥 윗면, 바닥판 -0.15…0 m. 앞벽 0.6 m, 나머지 1 m. '
        '모서리 기둥 2.4 m, 바퀴 중심 높이 3.4 m. 기존 탑승판 그림 교체용; '
        '양옆 ±X 중앙 폭 2.4 m 출입구. 기둥 가림은 게임 Occlude_Frame 처리.', 'Frame')
    floor = box(keep, 'Floor', (0, 0, -.075), (6, 5, .15), 'Metal', .025)
    box(keep, 'FrontWall', (0, -2.42, .30), (6, .16, .60), 'Ochre', .035)
    box(keep, 'RearWall', (0, 2.42, .50), (6, .16, 1.0), 'Ochre', .035)
    for x in (-2.92, 2.92):
        # Walk through the actual opening rather than a decorative solid wall.
        for sign in (-1, 1):
            box(keep, 'SideWall', (x, sign * 1.77, .50), (.16, 1.14, 1.0), 'Ochre', .035)
            beam(keep, 'SideRail', (x, sign * 1.2, 1.0), (x, sign * 2.38, 1.0), .055, 'Metal')
        for y in (-2.42, 2.42):
            beam(upper, 'CornerPost', (x, y, .55), (x, y, 2.4), .075, 'Metal')
            beam(upper, 'HangerArm', (x, y, 2.4), (0, 0, 3.23), .07, 'Metal')
    beam(keep, 'FrontRail', (-2.94, -2.42, .555), (2.94, -2.42, .555), .045, 'Metal')
    # A rear crossbar stiffens the frame without covering the overhead opening.
    beam(upper, 'RearCrossbar', (-2.92, 2.42, 2.32), (2.92, 2.42, 2.32), .065, 'Metal')
    box(upper, 'WheelCarrier', (0, 0, 3.22), (1.05, .18, .12), 'Metal', .025)
    for x in (-.34, .34):
        pulley(upper, 'TravelWheel', (x, 0, 3.4), .18, .12, axis='Y')
    for x, z, width in ((-1.3, .18, .25), (.85, .36, .19), (1.45, .23, .12)):
        box(keep, 'RustPatch', (x, -2.502, z), (width, .008, .09), 'Rust', .01)
    return root, data, {'floor': floor, 'cable_center': Vector((0, 0, 3.4))}


def gondola_station():
    root, keep, upper, data = start('gondola_station', '곤돌라 승강장',
        '피벗=6×5 m 탑승 영역 중심 바닥. 중앙 바닥 없음; 테두리만 z=0 아래 면부터. '
        '뒤 탑 X=-3…3, Blender Y=2.5…3.7, 높이 5 m. '
        '바퀴 지름 1.2 m, +X 출선 팔. 앞왼쪽 레버 게임(-2.65,1.05,2.28).', 'Tower')
    # A thin perimeter only; the game owns the actual walkable ground.
    for y in (-2.45, 2.45):
        box(keep, 'PlatformEdge', (0, y, .06), (6, .10, .12), 'Metal', .018)
    for x in (-2.95, 2.95):
        box(keep, 'PlatformEdge', (x, 0, .06), (.10, 4.8, .12), 'Metal', .018)
    tower_parts = []
    for x in (-2.88, 2.88):
        for y in (2.62, 3.58):
            # Ends sit inside the crossbar, with narrower sides. Matching the
            # beam's top/side planes produces coplanar black patches on export.
            tower_parts.append(box(upper, 'TowerLeg', (x, y, 2.42), (.22, .22, 4.84), 'Metal', .03))
            box(keep, 'TowerFoot', (x, y, .10), (.34, .24, .20), 'Concrete', .02)
        for a, b in (((x, 2.62, .35), (x, 3.58, 4.75)),
                     ((x, 3.58, .35), (x, 2.62, 4.75))):
            beam(upper, 'SideBrace', a, b, .055, 'Metal')
    for a, b in (((-2.88, 3.58, .35), (2.88, 3.58, 4.75)),
                 ((2.88, 3.58, .35), (-2.88, 3.58, 4.75))):
        beam(upper, 'BackXBrace', a, b, .075, 'Metal')
    for y in (2.62, 3.58):
        tower_parts.append(box(upper, 'TopCrossbar', (0, y, 4.86), (6, .24, .28), 'Metal', .035))
    beam(upper, 'WheelAxle', (0, 2.62, 5.0), (0, 3.58, 5.0), .12, 'MetalLight', 12)
    pulley(upper, 'StationWheel', (0, 3.1, 5.0), .6, .22, axis='Y', segments=16)
    box(upper, 'CableArm', (1.85, 3.1, 4.74), (3.90, .22, .22), 'Metal', .035)
    beam(upper, 'ArmBrace', (2.88, 3.1, 3.9), (3.72, 3.1, 4.73), .065, 'Metal')
    box(keep, 'LeverFoot', (-2.65, -2.1, .08), (.38, .38, .16), 'Concrete', .025)
    box(keep, 'LeverPost', (-2.65, -2.1, .56), (.16, .16, 1.08), 'Metal', .025)
    beam(keep, 'LeverStem', (-2.65, -2.1, .90), (-2.65, -2.28, 1.04), .035, 'MetalLight')
    beam(keep, 'RedHandle', (-2.83, -2.28, 1.05), (-2.47, -2.28, 1.05), .05, 'Red')
    return root, data, {'tower': tower_parts, 'cable_center': Vector((0, 3.1, 5.0))}


def vertices(objects):
    return [obj.matrix_world @ v.co for obj in objects if obj.type == 'MESH' for v in obj.data.vertices]


def bounds(points):
    return (Vector(tuple(min(v[i] for v in points) for i in range(3))),
            Vector(tuple(max(v[i] for v in points) for i in range(3))))


def join_roles(root):
    for group in root.children:
        children = list(group.children)
        assert children and all(obj.type == 'MESH' for obj in children)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in children:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = children[0]
        if len(children) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = group.name + '_Mesh'
        mod = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=mod.name)


def centroid(group):
    total, weighted = 0.0, Vector((0, 0, 0))
    for obj in group.children:
        for poly in obj.data.polygons:
            a, b, c = [obj.matrix_world @ obj.data.vertices[i].co for i in poly.vertices]
            area = (b - a).cross(c - a).length / 2
            total += area
            weighted += (a + b + c) * (area / 3)
    assert total > 0
    return weighted / total


def prepare(root, data, probes):
    bpy.context.view_layer.update()
    asset_id = data['id']
    # Ground-centered assets only. Top/cabin/station have explicit semantic pivots.
    shift = Vector((0, 0, 0))
    if asset_id in ('ladder_section', 'zipline_anchor'):
        lo, hi = bounds(vertices(root.children_recursive))
        shift = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, 0))
        for group in root.children:
            group.location -= shift
        bpy.context.view_layer.update()
    if 'floor' in probes:
        lo, hi = bounds(vertices([probes['floor']]))
        assert abs(hi.z) <= .02 and abs(lo.z + .15) <= .002, 'cabin floor plane'
        assert abs(hi.x - lo.x - 6) <= .02 and abs(hi.y - lo.y - 5) <= .02
    if 'tower' in probes:
        lo, hi = bounds(vertices(probes['tower']))
        for actual, expected in zip(hi - lo, (6, 1.2, 5)):
            assert abs(actual - expected) <= .02, ('tower frame', actual, expected)
    if 'cable_center' in probes:
        point = probes['cable_center'] - shift
        data['note'] += ' 줄/도르래 기준점(게임 X/Y/Z m)=' + str([round(point.x, 4), round(point.z, 4), round(-point.y, 4)]) + '.'
    join_roles(root)
    bpy.context.view_layer.update()
    lo, hi = bounds(vertices(root.children_recursive))
    dims = hi - lo
    data.update(dimensions_m=[round(dims.x, 4), round(dims.z, 4), round(dims.y, 4)],
                bounds_min_y=round(lo.z, 6), file=asset_id + '.glb',
                triangles=sum(len(obj.data.polygons) for obj in root.children_recursive if obj.type == 'MESH'),
                weather='PBR 원본 / 젖음·상향면 적설 재질 교체 가능 (미검수)')
    validate(root, data, lo, hi)


def validate(root, data, lo, hi):
    asset_id = data['id']
    assert root.name == asset_id and root['asset_contract'] == CONTRACT
    assert root.location.length < .000001
    assert {group.name for group in root.children} == {'Keep', *data['occlusion_parts']}
    materials = set()
    for group in root.children:
        assert group.type == 'EMPTY' and len(group.children) == 1
        obj = group.children[0]
        assert obj.type == 'MESH' and obj.name == group.name + '_Mesh'
        assert not obj.children and all(len(p.vertices) == 3 for p in obj.data.polygons)
        materials.update(slot.material for slot in obj.material_slots if slot.material)
        if group.name.startswith('Occlude_'):
            assert centroid(group).y > 0, (asset_id, 'occluder centroid must be behind')
    assert 0 < len(materials) <= 6, (asset_id, 'materials', len(materials))
    assert all(not any(n.type == 'TEX_IMAGE' for n in mat.node_tree.nodes) for mat in materials)
    assert 0 < data['triangles'] < BUDGET[asset_id], (asset_id, 'triangles', data['triangles'])
    for actual, expected in zip(data['dimensions_m'], EXPECTED[asset_id]):
        assert .85 * expected <= actual <= 1.15 * expected, (asset_id, 'dimensions', data['dimensions_m'])
    floor = -.15 if asset_id in ('ladder_top', 'gondola_cabin') else 0
    assert abs(lo.z - floor) <= .002, (asset_id, 'pivot bottom', lo.z)
    if asset_id == 'ladder_section':
        assert abs(hi.z - 1.2) <= .02 and abs(hi.z - lo.z - 1.2) <= .02
    if asset_id == 'ladder_top':
        assert abs(hi.z - .9) <= .02
    if asset_id in ('ladder_section', 'zipline_anchor'):
        assert abs(lo.x + hi.x) <= .002 and abs(lo.y + hi.y) <= .002


def read_manifest():
    path = PACK / 'manifest.json'
    if path.exists():
        data = json.loads(path.read_text(encoding='utf-8'))
        assert data['contract'] == CONTRACT and data['revision'] == 1
        assert isinstance(data['assets'], list)
        ids = [asset['id'] for asset in data['assets']]
        assert len(ids) == len(set(ids)), 'duplicate manifest IDs'
        return data
    return dict(revision=1, contract=CONTRACT, date='2026-10-23', units='metres',
                up_axis='+Y', status='Stage 2 prop assets; game integration pending', assets=[])


def main(selected=None):
    selected = set(BUDGET) if selected is None else set(selected)
    assert selected and selected <= set(BUDGET), 'unknown or empty asset selection'
    manifest = read_manifest()  # Fail before touching outputs on an incompatible pack.
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    M.update({name: material(name, color) for name, color in PALETTE.items()})
    prepared = []
    for build in (ladder_section, ladder_top, zipline_anchor, gondola_cabin, gondola_station):
        root, data, probes = build()
        prepare(root, data, probes)
        # Blender names are globally unique. Archive exact role names between assets;
        # restore them only while validating/exporting that asset.
        for obj in root.children_recursive:
            obj['contract_name'] = obj.name
            obj.name = data['id'] + '__' + obj.name
        prepared.append((root, data))
    assert {data['id'] for _, data in prepared} == set(BUDGET)
    PACK.mkdir(parents=True, exist_ok=True)
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    for root, data in prepared:
        if data['id'] not in selected:
            continue
        objects = [root] + list(root.children_recursive)
        for obj in objects[1:]:
            obj.name = obj['contract_name']
        bpy.context.view_layer.update()
        lo, hi = bounds(vertices(objects))
        validate(root, data, lo, hi)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = root
        result = bpy.ops.export_scene.gltf(filepath=str(PACK / data['file']), export_format='GLB',
                                         use_selection=True, export_extras=True, export_yup=True,
                                         export_animations=False)
        assert result == {'FINISHED'}, (data['id'], result)
        for obj in objects[1:]:
            obj.name = data['id'] + '__' + obj['contract_name']
    # Preserve ordering, other bundles and their custom fields without regenerating them.
    replacements = {data['id']: data for _, data in prepared if data['id'] in selected}
    manifest['assets'] = [replacements.pop(old['id'], old) for old in manifest['assets']]
    manifest['assets'].extend(replacements.values())
    # Library staging happens after origin-centered GLB export; names remain recoverable.
    for i, (root, _) in enumerate(prepared):
        root.location = ((i % 3) * 7, (i // 3) * 9, 0)
    result = bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    assert result == {'FINISHED'}, result
    temporary = PACK / 'manifest.json.tmp'
    temporary.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(PACK / 'manifest.json')
    for _, data in prepared:
        if data['id'] in selected:
            print('STAGE2_PROP_READY', data['id'], data['triangles'])
    print('STAGE2_PROPS_BUILT A', len(selected))


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    main(args or None)
