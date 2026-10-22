"""Validate the nine exported files and discrete original-wing clearances."""
import bpy
import json
import math
import hashlib
from pathlib import Path
from mathutils import Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'outfit-collection-manifest.json').read_text())
checks=[]
motions=[]
errors=[]

def check(ok,name):
    (checks if ok else errors).append(name)

def bvh(ob,transform=Matrix.Identity(4)):
    return BVHTree.FromPolygons([transform@ob.matrix_world@v.co for v in ob.data.vertices],
                               [p.vertices[:] for p in ob.data.polygons])

for path,value in manifest['protected_files_sha256'].items():
    check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==value,'Unchanged source: '+Path(path).name)

for kind,record in manifest['outfits'].items():
    for slot in ('full','helmet','vest'):
        filename=f'goose-{kind}-'+('outfit' if slot=='full' else slot)+'.glb'
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
        objects=list(bpy.context.scene.objects)
        meshes=[o for o in objects if o.type=='MESH']
        expected=record[slot]
        check(len(meshes)==expected['mesh_objects'],filename+': mesh count')
        check(not any(o.type in ('ARMATURE','CAMERA','LIGHT') for o in objects),filename+': no studio/armature')
        check(not any(o.name.startswith(('light_','recon_','heavy_','base_')) for o in objects),filename+': canonical nodes')
        pts=[];triangles=0
        for ob in meshes:
            ob.data.calc_loop_triangles();triangles+=len(ob.data.loop_triangles)
            pts.extend(ob.matrix_world@v.co for v in ob.data.vertices)
            check(all(p.area>1e-12 for p in ob.data.polygons),filename+': nonzero faces '+ob.name)
        check(triangles==expected['triangles'],filename+': triangles')
        actual=[[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]]
        bounds=[expected['bounds_min'],expected['bounds_max']]
        check(max(abs(actual[j][i]-bounds[j][i]) for j in range(2) for i in range(3))<.0001,filename+': bounds/axes')
        for item,target in [('helmet','goose_head'),('vest','goose_torso')]:
            if slot not in ('full',item):continue
            anchor=bpy.data.objects.get('goose_'+item)
            ob=bpy.data.objects.get(f'goose_{item}_{kind}_mesh')
            check(anchor is not None and anchor.parent.name==target,filename+': '+item+' anchor')
            check(ob is not None and ob.parent==anchor,filename+': '+item+' mesh parent')
        if slot!='full':
            check(len(meshes)==1 and 'goose_torso_mesh' not in bpy.data.objects,filename+': equipment only')
            continue
        check(all(n in bpy.data.objects for n in record['preserved_body_meshes']),filename+': 11 body meshes')
        check('Muzzle' in bpy.data.objects,filename+': weapon marker')
        check(not any('pack' in o.name for o in meshes),filename+': no backpack mesh')
        for role,(roughness,metallic) in {'cloth':(.92 if kind!='heavy' else .93,0),'shell':({'light':.85,'recon':.43,'heavy':.44}[kind],0)}.items():
            mat=bpy.data.materials.get(kind+'_'+role)
            bsdf=mat.node_tree.nodes.get('Principled BSDF') if mat else None
            check(bsdf is not None and abs(bsdf.inputs['Roughness'].default_value-roughness)<1e-5 and abs(bsdf.inputs['Metallic'].default_value-metallic)<1e-5,filename+': PBR '+role)
        armor=bvh(bpy.data.objects[f'goose_vest_{kind}_mesh'])
        pairs=len(armor.overlap(bvh(bpy.data.objects['goose_torso_mesh'])))
        check(pairs==0,f'{kind}: torso/vest surface intersection pairs {pairs}')
        for pose,pitch,spread in [('idle',0,0),('forward20',20,0),('back15',-15,0),('outward20',0,20),('forward20_out15',20,15)]:
            for side,name in [(-1,'leftArm'),(1,'rightArm')]:
                pivot=bpy.data.objects['goose_'+name].matrix_world.translation
                transform=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(pitch),4,'X')@Matrix.Rotation(math.radians(-side*spread),4,'Y')@Matrix.Translation(-pivot)
                pairs=len(armor.overlap(bvh(bpy.data.objects['goose_'+name+'_mesh'],transform)))
                motions.append(dict(outfit=kind,pose=pose,wing=name,intersecting_face_pairs=pairs))
                check(pairs==0,f'{kind}: {pose} {name} pairs {pairs}')

report=dict(passed=len(checks),failed=len(errors),checks=checks,errors=errors,wing_probes=motions,
            scope='Nine GLB round trips and five discrete authored-wing poses per outfit. No continuous sweep, runtime weapon-arm or Godot validation.')
(ROOT/'output/outfit-collection/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('OUTFIT_COLLECTION_VALIDATION',json.dumps({'passed':len(checks),'errors':errors}),flush=True)
assert not errors,'See output/outfit-collection/validation.json'
