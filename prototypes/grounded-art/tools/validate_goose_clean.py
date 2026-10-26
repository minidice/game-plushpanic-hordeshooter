"""Independently reimport the clean goose and verify the fitted GLB contract."""
import bpy
import bmesh
import hashlib
import json
import argparse
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'game/assets/characters/goose/goose-fitted.glb'
TARGET = ROOT / 'game/assets/characters/goose/goose-clean.glb'
OUT = ROOT / 'prototypes/grounded-art/output/goose-clean'
OUT.mkdir(parents=True, exist_ok=True)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--baseline-hashes', type=Path, help='Optional pre-edit immutable-before.json from a single-species build')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])


def read(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return {obj.name: obj for obj in bpy.context.scene.objects if obj.name.startswith('goose_') or obj.name == 'Muzzle'}


def points(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds(obj):
    vertices = points(obj)
    return [(min(p[i] for p in vertices), max(p[i] for p in vertices)) for i in range(3)]


def signature(obj):
    return {
        'parent': obj.parent.name if obj.parent else None,
        'matrix': [v for row in obj.matrix_world for v in row],
        'materials': [mat.name.split('.')[0] for mat in obj.data.materials] if obj.type == 'MESH' else [],
        'vertices': len(obj.data.vertices) if obj.type == 'MESH' else 0,
        'triangles': sum(len(face.vertices) - 2 for face in obj.data.polygons) if obj.type == 'MESH' else 0,
        'points': sorted(tuple(round(v, 5) for v in p) for p in points(obj)) if obj.type == 'MESH' else [],
    }


checks = []


def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)


source = read(SOURCE)
original = {name: signature(obj) for name, obj in source.items()}
source_bounds = {name: bounds(obj) for name, obj in source.items() if obj.type == 'MESH'}
target = read(TARGET)
changed = {'goose_beak_mesh', *(f'goose_{side}{part}_mesh' for side in ('left', 'right') for part in ('Eye', 'Arm', 'Foot'))}
check(set(source) == set(target), 'exactly the original named hierarchy')
check(not any(obj.type == 'ARMATURE' for obj in bpy.context.scene.objects), 'no armature')
for name, data in original.items():
    obj = target[name]
    now = signature(obj)
    check(now['parent'] == data['parent'], f'parent {name}')
    check(all(abs(a - b) < 2e-5 for a, b in zip(now['matrix'], data['matrix'])), f'transform {name}')
    if obj.type == 'MESH':
        check(now['materials'] == data['materials'], f'material order {name}')
        if name not in {'goose_leftFoot_mesh', 'goose_rightFoot_mesh'}:
            check(now['vertices'] == data['vertices'] and now['triangles'] == data['triangles'], f'topology {name}')
        if name not in changed:
            check(now['points'] == data['points'], f'untouched geometry {name}')

bill = bounds(target['goose_beak_mesh'])
old_bill = source_bounds['goose_beak_mesh']
check((bill[0][1] - bill[0][0]) < .88 * (old_bill[0][1] - old_bill[0][0]), 'goose bill narrowed')
check((bill[1][1] - bill[1][0]) < .96 * (old_bill[1][1] - old_bill[1][0]), 'bill shortened')
for side in ('left', 'right'):
    eye = bounds(target[f'goose_{side}Eye_mesh'])
    old_eye = source_bounds[f'goose_{side}Eye_mesh']
    check((eye[1][1] - eye[1][0]) < .61 * (old_eye[1][1] - old_eye[1][0]), f'{side} eye shallow')
    check((eye[2][1] - eye[2][0]) < .82 * (old_eye[2][1] - old_eye[2][0]), f'{side} eye compact')
    foot = bounds(target[f'goose_{side}Foot_mesh'])
    check(abs(foot[2][0]) < 1e-4, f'{side} sole at ground')
    check(foot[2][1] > .225, f'{side} ankle meets lower torso')
    foot_obj = target[f'goose_{side}Foot_mesh']
    foot_points = points(foot_obj)
    check(sum(abs(p.z) < 1e-4 for p in foot_points) >= 24, f'{side} flat sole ring')
    check(foot[1][0] < -.23, f'{side} projected web')
    check(len(foot_obj.data.materials) == 2 and set(p.material_index for p in foot_obj.data.polygons) == {0, 1}, f'{side} two palette channels')
    bm = bmesh.new()
    bm.from_mesh(foot_obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
    check(all(edge.is_manifold for edge in bm.edges), f'{side} closed manifold web and ankle')
    check(bm.calc_volume(signed=True) > 0, f'{side} outward normals')
    seen = set(); components = 0
    for vertex in bm.verts:
        if vertex in seen:
            continue
        components += 1
        stack = [vertex]; seen.add(vertex)
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                neighbor = edge.other_vert(current)
                if neighbor not in seen:
                    seen.add(neighbor); stack.append(neighbor)
    check(components == 1, f'{side} one connected foot surface')
    bm.free()
    arm = bounds(target[f'goose_{side}Arm_mesh'])
    check(arm[2][0] == source_bounds[f'goose_{side}Arm_mesh'][2][0], f'{side} wing lower extent')

if args.baseline_hashes:
    hashes = json.loads(args.baseline_hashes.read_text(encoding='utf-8'))
    def relocated(path):
        normalized = path.replace('\\', '/')
        marker = 'game/assets/characters/'
        if marker not in normalized:
            raise ValueError('Not a player model baseline path: ' + path)
        return ROOT / marker / normalized.split(marker, 1)[1]
    check(all(relocated(path).is_file() and hashlib.sha256(relocated(path).read_bytes()).hexdigest() == digest
              for path, digest in hashes.items()), 'other player GLBs unchanged against supplied authoring baseline')
report = {'checks': len(checks), 'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
          'target_sha256': hashlib.sha256(TARGET.read_bytes()).hexdigest(),
          'source_triangles': sum(item['triangles'] for item in original.values()),
          'target_triangles': sum(signature(obj)['triangles'] for obj in target.values()),
          'source_nodes': len(source), 'target_nodes': len(target)}
(OUT / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('GOOSE_VALIDATED', json.dumps(report), flush=True)
