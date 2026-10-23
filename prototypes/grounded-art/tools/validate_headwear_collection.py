"""Round-trip checks on the actual 14 headwear GLBs; static fit only."""
import bpy
import json
import hashlib
from pathlib import Path
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'headwear-collection-manifest.json').read_text())
passed=[];failed=[];fit=[]
def check(value,name):(passed if value else failed).append(name)
def bvh(ob):return BVHTree.FromPolygons([ob.matrix_world@v.co for v in ob.data.vertices],[p.vertices[:] for p in ob.data.polygons])
for path,h in manifest['protected_files_sha256'].items():
    check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==h,'Protected: '+Path(path).name)
for kind,record in manifest['headwear'].items():
    for slot,suffix in [('full','-character'),('helmet','')]:
        name=f'goose-headwear-{kind}{suffix}.glb'
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/name))
        objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH']
        expected=record[slot]
        check(len(meshes)==expected['mesh_objects'],name+': mesh count')
        check(not any(o.type in ('ARMATURE','LIGHT','CAMERA') for o in objects),name+': no studio or rig')
        pts=[];triangles=0
        for o in meshes:
            o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
            pts.extend(o.matrix_world@v.co for v in o.data.vertices)
            bad=sum(p.area<=1e-12 for p in o.data.polygons)
            check(bad==0,name+': nonzero faces '+o.name+f' ({bad} degenerate)')
        check(triangles==expected['triangles'],name+': triangle count')
        bounds=[[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]]
        check(max(abs(bounds[j][i]-expected[['bounds_min','bounds_max'][j]][i]) for j in range(2) for i in range(3))<1e-4,name+': bounds and axes')
        head=bpy.data.objects.get(f'goose_headwear_{kind}_mesh');anchor=bpy.data.objects.get('goose_helmet')
        check(head is not None and head.parent==anchor,name+': separate headwear')
        check(anchor is not None and anchor.parent.name=='goose_head',name+': attachment parent')
        check(not any(o.name.startswith(('base_',kind+'_')) for o in objects),name+': canonical node names')
        if slot=='helmet':
            check(len(meshes)==1 and 'goose_torso_mesh' not in bpy.data.objects,name+': equipment only')
            continue
        check(all(n in bpy.data.objects for n in record['preserved_meshes']),name+': body and vest retained')
        check('Muzzle' in bpy.data.objects,name+': muzzle marker')
        check(not any('pack' in o.name for o in meshes),name+': no backpack mesh')
        hb=bvh(head)
        for n in ('goose_torso_mesh','goose_beak_mesh','goose_leftEye_mesh','goose_rightEye_mesh','goose_vest_refined_mesh'):
            pairs=len(hb.overlap(bvh(bpy.data.objects[n])))
            fit.append(dict(kind=kind,against=n,surface_intersection_pairs=pairs))
            check(pairs==0,f'{kind}: static fit vs {n} ({pairs} pairs)')
        if kind in ('goggles','motorcycle','nvg'):
            lens=bpy.data.materials.get({'nvg':'headwear_optic_glass','motorcycle':'headwear_smoked_visor','goggles':'headwear_blue_glass'}[kind])
            check(lens is not None,kind+': optical material retained')
            if lens:
                p=lens.node_tree.nodes.get('Principled BSDF')
                check(p.inputs['Roughness'].default_value<.15,kind+': lens roughness retained')
report=dict(passed=len(passed),failed=len(failed),checks=passed,errors=failed,static_fit=fit,
            scope='14 actual GLB round trips. Default size static surface fit; no runtime motion, optics, cloth or body-slider validation.')
(ROOT/'output/headwear-collection/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('HEADWEAR_VALIDATION',json.dumps({'passed':len(passed),'errors':failed}),flush=True)
assert not failed,'See validation.json'
