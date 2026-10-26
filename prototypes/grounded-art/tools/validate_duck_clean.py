"""Independently reimport fitted/clean duck and check the GLB contract."""
import bpy
import bmesh
import hashlib
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'game/assets/characters/duck/duck-fitted.glb'
TARGET = ROOT / 'game/assets/characters/duck/duck-clean.glb'
OUT = ROOT / 'prototypes/grounded-art/output/duck-clean'
OUT.mkdir(parents=True, exist_ok=True)
immutable = [p for p in (ROOT / 'game/assets/characters').glob('*/*.glb') if p != TARGET]
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in immutable}


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return {obj.name: obj for obj in bpy.context.scene.objects if obj.name.startswith('duck_') or obj.name == 'Muzzle'}


def points(obj):
    return [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]


def bounds(obj):
    xyz = points(obj)
    return [[min(p[i] for p in xyz), max(p[i] for p in xyz)] for i in range(3)]


def near(a, b, tolerance=2e-4):
    return (Vector(a) - Vector(b)).length < tolerance


checks = []


def check(ok, label):
    if not ok:
        raise AssertionError(label)
    checks.append(label)


original = load(SOURCE)
snapshot = {}
for name, obj in original.items():
    snapshot[name] = {
        'parent': obj.parent.name if obj.parent else None,
        'matrix': [list(row) for row in obj.matrix_world],
        'materials': [material.name.split('.')[0] for material in obj.data.materials] if obj.type == 'MESH' else [],
        'points': [tuple(round(v, 5) for v in p) for p in points(obj)] if obj.type == 'MESH' else [],
        'bounds': bounds(obj) if obj.type == 'MESH' else None,
    }
source_tris = sum(sum(len(face.vertices) - 2 for face in o.data.polygons) for o in original.values() if o.type == 'MESH')
target = load(TARGET)
check(set(snapshot) == set(target), 'all named nodes preserved, no added gameplay node')
changed = {'duck_beak_mesh', 'duck_leftEye_mesh', 'duck_rightEye_mesh',
           'duck_leftBrow_mesh', 'duck_rightBrow_mesh',
           'duck_leftArm_mesh', 'duck_rightArm_mesh',
           'duck_leftFoot_mesh', 'duck_rightFoot_mesh'}
for name, before in snapshot.items():
    obj = target[name]
    check((obj.parent.name if obj.parent else None) == before['parent'], f'{name} parent')
    check(all(near(row, old) for row, old in zip(obj.matrix_world, before['matrix'])), f'{name} transform')
    if obj.type == 'MESH':
        check([m.name.split('.')[0] for m in obj.data.materials] == before['materials'], f'{name} material channels')
        if name not in changed:
            after_points = [tuple(round(v, 5) for v in p) for p in points(obj)]
            check(after_points == before['points'], f'{name} geometry unchanged')

check(bounds(target['duck_beak_mesh'])[0][1] > .24, 'broad bill')
check(bounds(target['duck_beak_mesh'])[2][1] - bounds(target['duck_beak_mesh'])[2][0] < .12, 'shallow bill')
for side in ('left', 'right'):
    eye = target[f'duck_{side}Eye_mesh']
    wing = target[f'duck_{side}Arm_mesh']
    foot = target[f'duck_{side}Foot_mesh']
    check(bounds(eye)[2][1] - bounds(eye)[2][0] < .093, f'{side} small eye')
    check(bounds(wing)[0][1] - bounds(wing)[0][0] > .13, f'{side} wider wing feather edge')
    foot_points = points(foot)
    check(abs(min(point.z for point in foot_points)) < 1e-4, f'{side} sole z0')
    check(sum(abs(point.z) < 1e-4 for point in foot_points) >= 20, f'{side} flat sole rim')
    check(bounds(foot)[2][1] > .22, f'{side} narrow ankle joins web')
    check(bounds(foot)[1][0] < -.26, f'{side} projected web')
    bm = bmesh.new()
    bm.from_mesh(foot.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
    check(all(edge.is_manifold for edge in bm.edges), f'{side} closed foot')
    check(bm.calc_volume(signed=True) > 0, f'{side} outward foot normals')
    bm.free()

target_tris = sum(sum(len(face.vertices) - 2 for face in o.data.polygons) for o in target.values() if o.type == 'MESH')
check(target_tris < source_tris, 'triangle count reduced')
check({str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in immutable} == hashes, 'other player GLBs unchanged')
report = {'checks': len(checks), 'source_triangles': source_tris,
          'target_triangles': target_tris, 'target_bytes': TARGET.stat().st_size,
          'target_sha256': hashlib.sha256(TARGET.read_bytes()).hexdigest()}
(OUT / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('PASS', json.dumps(report), flush=True)
