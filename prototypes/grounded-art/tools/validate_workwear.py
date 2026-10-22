"""Inspect actual workwear GLB exports in fresh Blender scenes."""
import bpy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT/'workwear-manifest.json').read_text(encoding='utf-8'))
checks = []


def check(value, name):
    assert value, name
    checks.append(name)


for path, expected in manifest['protected_files_sha256'].items():
    check(hashlib.sha256(Path(path).read_bytes()).hexdigest() == expected,
          'Read-only source unchanged: '+Path(path).name)
for filename, key in [('goose-workwear.glb', 'candidate'), ('goose-workwear-only.glb', 'clothing')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == 'MESH']
    check(len(meshes) == manifest[key]['mesh_objects'], filename+': mesh count')
    check(not any(o.type in ('ARMATURE', 'CAMERA', 'LIGHT') for o in objects), filename+': no studio or rig')
    points, triangles = [], 0
    for ob in meshes:
        ob.data.calc_loop_triangles()
        triangles += len(ob.data.loop_triangles)
        points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        check(all(p.area > 1e-12 for p in ob.data.polygons), filename+': nondegenerate '+ob.name)
    check(triangles == manifest[key]['triangles'], filename+': exported triangles')
    actual = [[min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]]
    expected = [manifest[key]['bounds_min'], manifest[key]['bounds_max']]
    check(max(abs(actual[j][i]-expected[j][i]) for j in range(2) for i in range(3)) < .0001,
          filename+': axis and bounds round trip')
    for name, parent in [('goose_helmet', 'goose_head'), ('goose_vest', 'goose_torso')]:
        check(name in bpy.data.objects and bpy.data.objects[name].parent.name == parent,
              filename+': parent '+name)
    for name in ('goose_helmet_workwear_mesh', 'goose_vest_workwear_mesh'):
        check(name in bpy.data.objects, filename+': removable '+name)
    if key == 'candidate':
        check(all(name in bpy.data.objects for name in manifest['unchanged_meshes']), 'All 11 original body parts preserved')
        check('Muzzle' in bpy.data.objects, 'Original weapon marker retained')
        check(not any(o.type == 'MESH' for o in bpy.data.objects['goose_pack'].children), 'No pack attached')
    else:
        check('goose_torso_mesh' not in bpy.data.objects, 'Clothing-only export contains no body')
        check(len(meshes) == 2, 'Vest and cap separately removable')

report = {'passed': len(checks), 'checks': checks, 'scope': 'GLB round trip; no runtime validation'}
(ROOT/'output/workwear/validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('WORKWEAR_VALIDATED', len(checks), flush=True)
