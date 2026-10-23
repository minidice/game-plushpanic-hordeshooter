"""Blender round-trip validation of the actual exported assets."""
import bpy
import json
import hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[1]
manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
checks=[]


def check(condition,name):
    assert condition,name
    checks.append(name)


def hash_file(p): return hashlib.sha256(p.read_bytes()).hexdigest()


check(hash_file(REPO/manifest['source_game_path'])==manifest['source_sha256'],'Existing game GLB unchanged')
check(hash_file(ROOT/manifest['source'])==manifest['source_sha256'],'Reference is an exact source copy')
for asset,key in [('goose-field-kit.glb','candidate'),('goose-equipment-only.glb','equipment')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/asset))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    check(bool(meshes),asset+': imports meshes')
    check(not any(o.type in ('CAMERA','LIGHT','ARMATURE') for o in bpy.context.scene.objects),asset+': no studio or armature')
    for node in ('goose_root','goose_helmet','goose_vest','goose_pack'):
        check(node in bpy.data.objects,asset+': attachment '+node)
    points=[]
    tris=0
    for ob in meshes:
        ob.data.calc_loop_triangles();tris+=len(ob.data.loop_triangles)
        points.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        check(all(p.area>1e-12 for p in ob.data.polygons),asset+': nonzero faces '+ob.name)
    check(tris==manifest[key]['triangles'],asset+': triangle count survives export')
    bounds=[[min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]]
    expected=[manifest[key]['bounds_min'],manifest[key]['bounds_max']]
    check(max(abs(bounds[j][i]-expected[j][i]) for j in range(2) for i in range(3))<.0001,asset+': size and axes survive export')
    if key=='candidate':
        body=bpy.data.objects['goose_torso_mesh']
        check(body.parent.name=='goose_torso','Body parent preserved')
        check(bpy.data.objects['goose_helmet'].parent.name=='goose_head','Helmet head attachment preserved')
        check(bpy.data.objects['goose_pack'].parent.name=='goose_torso','Pack torso attachment preserved')
        check('Muzzle' in bpy.data.objects,'Weapon attachment marker preserved')
    else:
        check(len(meshes)==3,'Equipment consists of three separately attached meshes')
        check('goose_torso_mesh' not in bpy.data.objects,'Equipment export contains no goose body')

report={'passed':len(checks),'checks':checks,'scope':'Blender GLB round-trip, not game integration'}
(ROOT/'output/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ASSETS_VALIDATED',len(checks),flush=True)
