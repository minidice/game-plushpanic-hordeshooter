"""Reimport actual exports and check limited authored-wing motion against the vest."""
import bpy
import json
import math
import hashlib
from pathlib import Path
from mathutils import Matrix
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT/'wrap-armor-manifest.json').read_text())
checks, motions = [], []
def check(value, name):
    assert value, name
    checks.append(name)

for path, value in manifest['protected_files_sha256'].items():
    check(hashlib.sha256(Path(path).read_bytes()).hexdigest() == value, 'Source unchanged: '+Path(path).name)
for filename, key in [('goose-wrap-armor.glb', 'candidate'), ('goose-wrap-vest-only.glb', 'vest')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
    objects = list(bpy.context.scene.objects)
    meshes = [o for o in objects if o.type == 'MESH']
    check(len(meshes) == manifest[key]['mesh_objects'], filename+': mesh count')
    check(not any(o.type in ('ARMATURE', 'CAMERA', 'LIGHT') for o in objects), filename+': no studio or rig')
    pts, triangles = [], 0
    for ob in meshes:
        ob.data.calc_loop_triangles()
        triangles += len(ob.data.loop_triangles)
        pts.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        check(all(p.area > 1e-12 for p in ob.data.polygons), filename+': nonzero faces '+ob.name)
    check(triangles == manifest[key]['triangles'], filename+': triangle count')
    expected = [manifest[key]['bounds_min'], manifest[key]['bounds_max']]
    actual = [[min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]]
    check(max(abs(actual[j][i]-expected[j][i]) for j in range(2) for i in range(3)) < .0001, filename+': axes and bounds')
    check(bpy.data.objects['goose_vest'].parent.name == 'goose_torso', filename+': torso attachment')
    vest = bpy.data.objects['goose_vest_wrap_mesh']
    check(vest.parent.name == 'goose_vest', filename+': vest separate')
    if key == 'vest':
        check(len(meshes) == 1 and 'goose_torso_mesh' not in bpy.data.objects, 'Vest-only export')
        continue
    check(all(name in bpy.data.objects for name in manifest['unchanged_meshes']), 'Body, helmet and backpack retained')
    check('Muzzle' in bpy.data.objects, 'Weapon marker retained')
    armor = BVHTree.FromPolygons([vest.matrix_world@v.co for v in vest.data.vertices], [p.vertices[:] for p in vest.data.polygons])
    # Discrete offline poses only. This is not a continuous sweep or Godot weapon-arm test.
    for pose, pitch, spread in [('idle', 0, 0), ('forward20', 20, 0), ('back15', -15, 0),
                                 ('outward20', 0, 20), ('forward20_out15', 20, 15)]:
        for side, name in [(-1, 'leftArm'), (1, 'rightArm')]:
            pivot = bpy.data.objects['goose_'+name].matrix_world.translation
            wing = bpy.data.objects['goose_'+name+'_mesh']
            transform = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(pitch), 4, 'X') @ Matrix.Rotation(math.radians(-side*spread), 4, 'Y') @ Matrix.Translation(-pivot)
            vertices = [transform@wing.matrix_world@v.co for v in wing.data.vertices]
            moved = BVHTree.FromPolygons(vertices, [p.vertices[:] for p in wing.data.polygons])
            intersections = len(armor.overlap(moved))
            motions.append(dict(pose=pose, wing=name, intersecting_face_pairs=intersections))
            check(intersections == 0, 'Wing/vest no surface intersections: '+pose+' '+name)

report = dict(passed=len(checks), checks=checks, wing_probes=motions,
              scope='GLB round trip and 5 discrete authored-wing poses; no continuous or runtime animation validation')
(ROOT/'output/wrap-armor/validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('WRAP_ARMOR_VALIDATED', len(checks), flush=True)
