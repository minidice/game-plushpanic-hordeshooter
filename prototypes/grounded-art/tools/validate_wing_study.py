"""Check actual GLB round trips and discrete wing/vest clearance."""
import bpy, bmesh, json, math, hashlib
from pathlib import Path
from mathutils import Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'wing-study-manifest.json').read_text())
checks=[];probes=[]
def check(ok,label):
    assert ok,label
    checks.append(label)
def points(obj):return {tuple(obj.matrix_world@v.co) for v in obj.data.vertices}
def distance(a,b):
    tree=KDTree(len(b))
    for i,p in enumerate(b):tree.insert(p,i)
    tree.balance()
    return max(tree.find(p)[2] for p in a)
def bvh(o,transform=Matrix.Identity(4)):
    return BVHTree.FromPolygons([transform@o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
for path,digest in manifest['protected_files_sha256'].items():
    check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Protected unchanged: '+Path(path).name)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-face-accessories.blend'))
baseline={name:points(bpy.data.objects['refined_'+name]) for name in manifest['preserved_meshes']}
for filename,key in [('goose-wing-character.glb','character'),('goose-wing-only.glb','wings')]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH']
    check(len(meshes)==manifest[key]['mesh_objects'],filename+': mesh count')
    check(not any(o.type in ('CAMERA','LIGHT','ARMATURE') for o in objects),filename+': no studio or extra rig')
    pts=[];tris=0
    for o in meshes:
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
        check(all(p.area>1e-12 for p in o.data.polygons),filename+': nonzero faces '+o.name)
    check(tris==manifest[key]['triangles'],filename+': triangles')
    actual=[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]]
    expected=[manifest[key]['bounds_min'],manifest[key]['bounds_max']]
    check(max(abs(actual[j][i]-expected[j][i]) for j in range(2) for i in range(3))<1e-4,filename+': bounds and axes')
    for name in ('leftArm','rightArm'):
        o=bpy.data.objects['goose_'+name+'_mesh'];anchor=bpy.data.objects['goose_'+name]
        check(o.parent==anchor,filename+': wing parent '+name)
        side=-1 if name=='leftArm' else 1
        check((anchor.matrix_world.translation-Matrix.Translation((side*.43,-.20,.89)).translation).length<1e-5,filename+': original pivot '+name)
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        check(all(e.is_manifold for e in bm.edges),filename+': closed wing '+name);bm.free()
    if key=='wings':
        check(all('Arm_mesh' in o.name for o in meshes),'Wing-only export has no body or clothes')
        continue
    # Export can split vertices at UV/material seams; compare world-position sets.
    for name,expected_points in baseline.items():
        actual_points=points(bpy.data.objects[name])
        delta=max(distance(actual_points,expected_points),distance(expected_points,actual_points))
        print('GEOMETRY_DELTA',name,delta,flush=True)
        check(delta<1e-5,'Unchanged exported geometry within 0.00001 m: '+name)
    vest=bvh(bpy.data.objects['goose_vest_refined_mesh'])
    for pose,pitch,spread in [('idle',0,0),('forward20',20,0),('back15',-15,0),('outward20',0,20),('forward20_out15',20,15)]:
        for side,name in [(-1,'leftArm'),(1,'rightArm')]:
            w=bpy.data.objects['goose_'+name+'_mesh'];pivot=w.parent.matrix_world.translation
            transform=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(pitch),4,'X')@Matrix.Rotation(math.radians(-side*spread),4,'Y')@Matrix.Translation(-pivot)
            pairs=len(vest.overlap(bvh(w,transform)))
            probes.append(dict(pose=pose,wing=name,intersecting_face_pairs=pairs))
            check(pairs==0,'Vest clearance: '+pose+' '+name)
report=dict(passed=len(checks),checks=checks,wing_probes=probes,scope='Actual GLB reimport, previous assets unchanged, unchanged non-wing geometry, closed wing surfaces, five discrete poses per wing; not continuous or runtime testing.')
(ROOT/'output/wing-study/validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('WING_STUDY_VALIDATED',len(checks),flush=True)
