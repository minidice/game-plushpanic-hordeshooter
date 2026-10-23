"""Actual GLB round trips, transparent-material checks and default fit checks."""
import bpy
import json
import hashlib
import struct
from pathlib import Path
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'face-accessories-manifest.json').read_text())
passed=[];errors=[];fit=[]
def check(value,name):(passed if value else errors).append(name)
def bvh(ob):return BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],[p.vertices[:] for p in ob.data.polygons])
for path,h in manifest['protected_files_sha256'].items():check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==h,'Preserved: '+Path(path).name)
for filename,record in manifest['exports'].items():
    path=ROOT/'assets'/filename
    data=path.read_bytes();length,kind=struct.unpack_from('<II',data,12)
    raw=json.loads(data[20:20+length])
    materials={m['name']:m for m in raw.get('materials',[])}
    for name,alpha in [('face_clear_lens',.035),('face_goggle_lens',.72)]:
        if name not in materials:continue
        m=materials[name]
        check(m.get('alphaMode')=='BLEND',filename+': '+name+' alpha blend in GLB')
        check(abs(m['pbrMetallicRoughness']['baseColorFactor'][3]-alpha)<1e-5,filename+': '+name+' alpha value')
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path))
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH'];expected=record['stats']
    check(len(meshes)==expected['mesh_objects'],filename+': mesh count')
    check(not any(o.type in ('CAMERA','LIGHT','ARMATURE') for o in objects),filename+': no studio or armature')
    triangles=0;pts=[]
    for o in meshes:
        o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
        pts.extend(o.matrix_world@v.co for v in o.data.vertices)
        bad=sum(p.area<=1e-12 for p in o.data.polygons)
        check(bad==0,filename+': nonzero faces '+o.name+f' ({bad} bad)')
    check(triangles==expected['triangles'],filename+': triangles')
    actual=[[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]]
    check(max(abs(actual[j][i]-expected[['bounds_min','bounds_max'][j]][i]) for j in range(2) for i in range(3))<1e-4,filename+': bounds/axes')
    equipment=[]
    for name,node in record['equipment'].items():
        o=bpy.data.objects.get(name);anchor=bpy.data.objects.get(node)
        check(o is not None and o.parent==anchor,filename+': '+name+' attachment')
        check(anchor is not None and anchor.parent.name=='goose_head',filename+': '+node+' follows head')
        if o:equipment.append(o)
    if not record['full']:
        check(len(meshes)==1 and 'goose_torso_mesh' not in bpy.data.objects,filename+': accessory-only')
        continue
    check(all(n in bpy.data.objects for n in manifest['variants'][record['kind']]['preserved_meshes']),filename+': body and vest nodes')
    check('Muzzle' in bpy.data.objects,filename+': weapon marker')
    check(not any('pack' in o.name for o in meshes),filename+': no backpack')
    for gear in equipment:
        gb=bvh(gear)
        for name in ('goose_torso_mesh','goose_beak_mesh','goose_leftEye_mesh','goose_rightEye_mesh','goose_leftBrow_mesh','goose_rightBrow_mesh','goose_vest_refined_mesh'):
            pairs=len(gb.overlap(bvh(bpy.data.objects[name])))
            fit.append(dict(variant=record['kind'],equipment=gear.name,against=name,pairs=pairs))
            check(pairs==0,f'{record["kind"]}: {gear.name} vs {name} ({pairs} surface pairs)')
    if len(equipment)==2:
        pairs=len(bvh(equipment[0]).overlap(bvh(equipment[1])))
        fit.append(dict(variant=record['kind'],equipment=equipment[0].name,against=equipment[1].name,pairs=pairs))
        check(pairs==0,f'{record["kind"]}: accessory-to-accessory ({pairs} surface pairs)')
report=dict(passed=len(passed),failed=len(errors),checks=passed,errors=errors,static_fit=fit,
            scope='12 actual GLB exports; material alpha and default static fit. Not Godot/runtime/body-slider validation.')
(ROOT/'output/face-accessories/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('FACE_ACCESSORIES_VALIDATION',json.dumps({'passed':len(passed),'errors':errors}),flush=True)
assert not errors,'See validation.json'
