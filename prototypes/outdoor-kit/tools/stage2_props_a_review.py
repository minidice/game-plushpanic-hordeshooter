"""Reimport/validate/render bundle A GLBs, independently of their generator.

blender --background --factory-startup --python-exit-code 1 --python <this file>
Outputs are local-only game/output/stage2-props-a-review/*.png and report.json.
No game scenes, collision, layout or manifest entries are modified.
Append -- --manifest-merge to exercise repeat generation in a disposable pack
and check that missing/duplicate review inputs are rejected (no image renders).
"""
import json
import math
import runpy
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game/assets/stage2-props'
OUT = REPO / 'game/output/stage2-props-a-review'
BUDGET = dict(ladder_section=600, ladder_top=500, zipline_anchor=1500,
              gondola_cabin=3000, gondola_station=3000)
REPORT = {'assets': [], 'renders': [], 'checks': []}


def check(ok, label):
    assert ok, label
    REPORT['checks'].append(label)


def bundle_entries(entries):
    selected = [entry for entry in entries if entry['id'] in BUDGET]
    check(len(selected) == len(BUDGET) and {entry['id'] for entry in selected} == set(BUDGET),
          'exactly five distinct bundle A manifest entries')
    return selected


def glb_json(path):
    payload = path.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', payload)
    check(magic == b'glTF' and version == 2 and length == len(payload), path.stem + ' GLB header')
    count, kind = struct.unpack_from('<I4s', payload, 12)
    check(kind == b'JSON', path.stem + ' JSON chunk')
    return json.loads(payload[20:20 + count])


def points(objects):
    bpy.context.view_layer.update()
    return [obj.matrix_world @ vertex.co for obj in objects if obj.type == 'MESH' for vertex in obj.data.vertices]


def bounds(objects):
    verts = points(objects)
    return (Vector(tuple(min(v[i] for v in verts) for i in range(3))),
            Vector(tuple(max(v[i] for v in verts) for i in range(3))))


def top_face_overlaps(objects):
    """Positive-area coplanar intersections at the station's upper joints.

    Shared triangle edges are allowed. Coincident separate skins cause black
    patches in Cycles and unstable depth ordering in a raster renderer.
    """
    planes = {}
    for obj in objects:
        if obj.type != 'MESH':
            continue
        obj.data.calc_loop_triangles()
        for tri in obj.data.loop_triangles:
            verts = [obj.matrix_world @ obj.data.vertices[i].co for i in tri.vertices]
            # Only exposed side joints. Spoke end caps at the wheel center are
            # intentionally enclosed by its solid hub, and cannot depth-fight.
            if min(v.z for v in verts) < 4.7 or max(abs(v.x) for v in verts) < 1.5:
                continue
            normal = (verts[1] - verts[0]).cross(verts[2] - verts[0]).normalized()
            axis = max(range(3), key=lambda i: abs(normal[i]))
            if normal[axis] < 0:
                normal = -normal
            plane = tuple(round(n, 4) for n in normal) + (round(normal.dot(verts[0]), 4),)
            axes = [i for i in range(3) if i != axis]
            flat = [(v[axes[0]], v[axes[1]]) for v in verts]
            planes.setdefault(plane, []).append(flat)

    def cross(a, b, p):
        return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])

    def intersection_area(subject, clip):
        if cross(*clip) < 0:
            clip = list(reversed(clip))
        for a, b in zip(clip, clip[1:] + clip[:1]):
            result = []
            for p, q in zip(subject, subject[1:] + subject[:1]):
                dp, dq = cross(a, b, p), cross(a, b, q)
                if dp >= 0:
                    result.append(p)
                if (dp >= 0) != (dq >= 0):
                    t = dp / (dp - dq)
                    result.append(tuple(p[i] + t * (q[i] - p[i]) for i in (0, 1)))
            subject = result
            if len(subject) < 3:
                return 0
        return abs(sum(a[0] * b[1] - a[1] * b[0]
                       for a, b in zip(subject, subject[1:] + subject[:1]))) / 2

    overlaps = 0
    for plane, triangles in planes.items():
        for i, a in enumerate(triangles):
            for b in triangles[i + 1:]:
                area = intersection_area(a, b)
                if area > .000001:
                    overlaps += 1
                    print('COPLANAR_TOP_OVERLAP', plane, 'area', round(area, 8), 'triangles', a, b)
    return overlaps


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)


def load(asset_id):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(PACK / (asset_id + '.glb')))
    objects = list(set(bpy.data.objects) - before)
    roots = [obj for obj in objects if obj.parent not in objects]
    check(len(roots) == 1, asset_id + ' single imported root')
    return roots[0], objects


def validate(entry):
    asset_id = entry['id']
    data = glb_json(PACK / entry['file'])
    check(not data.get('textures') and not data.get('images'), asset_id + ' texture free')
    check(0 < len(data['materials']) <= 6, asset_id + ' material budget')
    triangles = 0
    for mesh in data['meshes']:
        for primitive in mesh['primitives']:
            check(primitive.get('mode', 4) == 4, asset_id + ' triangles primitive')
            triangles += data['accessors'][primitive['indices']]['count'] // 3
    check(triangles == entry['triangles'] and triangles < BUDGET[asset_id], asset_id + ' exported triangle budget')
    root, objects = load(asset_id)
    check(root.name == asset_id and root.get('asset_contract') == 'plushpanic_outdoor_v1', asset_id + ' root contract')
    check({g.name for g in root.children} == {'Keep', *entry['occlusion_parts']}, asset_id + ' exact roles')
    for group in root.children:
        check(group.type == 'EMPTY' and len(group.children) == 1, asset_id + ' role hierarchy')
        child = group.children[0]
        check(child.name == group.name + '_Mesh' and child.type == 'MESH', asset_id + ' mesh name')
    lo, hi = bounds(objects)
    dims = hi - lo
    actual = (dims.x, dims.z, dims.y)
    check(all(abs(a - b) < .002 for a, b in zip(actual, entry['dimensions_m'])), asset_id + ' reimport dimensions')
    check(abs(lo.z - entry['bounds_min_y']) < .002, asset_id + ' reimport vertical pivot')
    if asset_id == 'ladder_section':
        check(abs(lo.z) < .002 and abs(hi.z - 1.2) < .002, 'section 1.2 m pitch')
    if asset_id == 'ladder_top':
        check(abs(lo.z + .15) < .002 and abs(hi.z - .9) < .002, 'top -0.15 / 0.9 m')
    if asset_id == 'gondola_station':
        overlaps = top_face_overlaps(objects)
        check(overlaps == 0, 'station top coplanar overlapping triangle pairs: ' + str(overlaps))
    REPORT['assets'].append(dict(id=asset_id, triangles=triangles,
                                  materials=len(data['materials']), dimensions_m=list(actual)))
    return objects


def plain_material(name, color):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (*color, 1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = .8
    return mat


def light(name, location, energy, size, target):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.shape, data.size = energy, 'DISK', size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()


def render(objects, name, rear=False, ground=True):
    scene = bpy.context.scene
    lo, hi = bounds(objects)
    center, extent = (lo + hi) / 2, max(hi - lo)
    before = set(bpy.data.objects)
    if ground:
        bpy.ops.mesh.primitive_plane_add(size=max(200, extent * 10), location=(0, 0, lo.z - .025))
        bpy.context.object.data.materials.append(plain_material('ReviewFloor', (.16, .20, .23)))
    camera_data = bpy.data.cameras.new('ReviewCamera')
    camera = bpy.data.objects.new('ReviewCamera', camera_data)
    bpy.context.collection.objects.link(camera)
    # Camera from Blender -Y / game +Z, 42-degree depression, small -X offset.
    direction = Vector((-.27, 1 if rear else -1, math.tan(math.radians(42))))
    camera.location = center + direction.normalized() * max(extent * 3.4, 3)
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    inverse = camera.rotation_euler.to_matrix().transposed()
    projected = [inverse @ (v - center) for v in points(objects)]
    spans = [max(v[i] for v in projected) - min(v[i] for v in projected) for i in (0, 1)]
    camera_data.ortho_scale = max(spans) * 1.24
    offset = Vector(tuple((min(v[i] for v in projected) + max(v[i] for v in projected)) / 2
                          for i in (0, 1)) + (0,))
    camera.location += camera.rotation_euler.to_matrix() @ offset
    check(all(abs(v[i] - offset[i]) < camera_data.ortho_scale / 2 for v in projected for i in (0, 1)),
          name + ' all geometry inside camera frame')
    camera_data.clip_end = 1000
    scene.camera = camera
    light('Key', center + Vector((-4, -6, 10)) * max(extent / 4, .7), 1600 * max(extent / 4, .7) ** 2, 7 * max(extent / 4, .7), center)
    light('Fill', center + Vector((5, 1, 6)) * max(extent / 4, .7), 900 * max(extent / 4, .7) ** 2, 5 * max(extent / 4, .7), center)
    scene.render.filepath = str(OUT / (name + '.png'))
    bpy.ops.render.render(write_still=True)
    REPORT['renders'].append(name + '.png')
    helpers = set(bpy.data.objects) - before
    for obj in helpers:
        bpy.data.objects.remove(obj, do_unlink=True)


def mannequin(at, color):
    """1.2 m scale figure only; does not claim to reproduce a game character."""
    objects = []
    mat = plain_material('ScaleFigure', color)
    for height, scale in ((.52, (.23, .19, .43)), (1.0, (.20, .18, .20))):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8,
                                            location=Vector(at) + Vector((0, 0, height)))
        obj = bpy.context.object
        obj.scale = scale
        obj.data.materials.append(mat)
        objects.append(obj)
    return objects


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.25, .30, .36, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .45
    scene.view_settings.view_transform = 'AgX'
    entries = json.loads((PACK / 'manifest.json').read_text(encoding='utf-8'))['assets']
    for entry in bundle_entries(entries):
        clear()
        objects = validate(entry)
        render(objects, entry['id'] + '-front')
        render(objects, entry['id'] + '-back', rear=True)
    clear()
    stack = []
    for tier in range(3):
        root, objects = load('ladder_section')
        root.location.z += tier * 1.2
        stack.extend(objects)
    root, objects = load('ladder_top')
    root.location.z += 3.6
    stack.extend(objects)
    lo, hi = bounds(stack)
    check(abs(lo.z) < .002 and abs(hi.z - 4.5) < .002, 'three sections plus top reaches 4.5 m')
    render(stack, 'ladder-assembled')
    clear()
    root, objects = load('gondola_cabin')
    for x in (-.7, .7):
        for y in (-.7, .7):
            objects += mannequin((x, y, 0), (.55, .28, .12) if x < 0 else (.15, .45, .52))
    render(objects, 'cabin-scale-figures')
    # Compare identical camera framing with the authored occlusion role hidden.
    frame = next(g for g in root.children if g.name == 'Occlude_Frame')
    for obj in frame.children_recursive:
        obj.hide_render = True
    render(objects, 'cabin-frame-hidden')
    (OUT / 'report.json').write_text(json.dumps(REPORT, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('STAGE2_REVIEW_PASS', len(REPORT['assets']), 'assets', len(REPORT['checks']), 'checks', len(REPORT['renders']), 'renders')


def check_manifest_merge():
    """Exercise the actual generator twice against a disposable mixed pack."""
    sandbox = OUT / 'manifest-merge-check'
    sandbox.mkdir(parents=True, exist_ok=True)
    manifest_path = sandbox / 'manifest.json'
    sentinel = dict(id='bundle_b_sentinel', file='unchanged.glb', custom={'keep': [1, 2, 3]})
    metadata = dict(revision=1, contract='plushpanic_outdoor_v1', units='metres',
                    up_axis='+Y', status='preserve existing status', custom={'owner': 'B'})
    fixture = dict(metadata, assets=[sentinel, dict(id='ladder_section', stale=True)])
    manifest_path.write_text(json.dumps(fixture), encoding='utf-8')
    original_manifest = (PACK / 'manifest.json').read_bytes()
    module = runpy.run_path(str(Path(__file__).with_name('stage2_props_a.py')))
    build = module['main']
    build.__globals__['PACK'] = sandbox
    build.__globals__['SOURCE'] = sandbox / 'test-source.blend'
    previous = None
    for iteration in range(2):
        build()
        result = json.loads(manifest_path.read_text(encoding='utf-8'))
        assert {k: v for k, v in result.items() if k != 'assets'} == metadata
        assert result['assets'][0] == sentinel
        assert len(result['assets']) == 6
        assert {a['id'] for a in result['assets']} == {'bundle_b_sentinel', *BUDGET}
        assert 'stale' not in result['assets'][1]
        if previous is not None:
            assert result == previous, 'second generation must not duplicate or drift metadata'
        previous = result
    assert (PACK / 'manifest.json').read_bytes() == original_manifest
    # The reviewer must reject absent/duplicate assets rather than silently skip.
    valid = [dict(id=asset_id) for asset_id in BUDGET]
    assert len(bundle_entries(valid)) == 5
    for invalid in (valid[:-1], valid + valid[:1]):
        try:
            bundle_entries(invalid)
        except AssertionError:
            pass
        else:
            raise AssertionError('review accepted missing or duplicate asset')
    print('STAGE2_MANIFEST_MERGE_PASS two builds; other bundle and metadata preserved')
    print('STAGE2_REVIEW_INPUT_PASS missing and duplicate bundle entries rejected')


if __name__ == '__main__':
    if '--manifest-merge' in sys.argv:
        check_manifest_merge()
    else:
        main()
