import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/fitted-species';manifest=json.loads((ROOT/'fitted-species-manifest.json').read_text())
checks=[];stats={};contacts={}
def check(ok,label):
 assert ok,label
 checks.append(label)
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path));return list(bpy.context.scene.objects)
def points(o):return [o.matrix_world@v.co for v in o.data.vertices]
def bvh(o):return BVHTree.FromPolygons(points(o),[p.vertices[:] for p in o.data.polygons])
def distance(a,b):
 tree=KDTree(len(b))
 for i,p in enumerate(b):tree.insert(p,i)
 tree.balance();return max(tree.find(p)[2] for p in a)
for name in manifest['exports']:
 objects=load(ROOT/'assets'/name);root=next(o for o in objects if o.name.endswith('FitRoot'));s=name.split('-')[2];pose=name.split('-')[3];kind=name.split('-')[4].split('.')[0]
 meshes=[o for o in objects if o.type=='MESH'];snapshot={};tri=0
 for o in meshes:
  p=o.parent
  while p and p!=root:p=p.parent
  check(p==root,'root ancestry '+o.name);check(all(abs(root.scale[i]-1)<1e-6 for i in range(3)),'neutral root '+name)
  o.data.calc_loop_triangles();tri+=len(o.data.loop_triangles)
  suffix=o.name.split('_after_',1)[1];snapshot[suffix]=points(o)
  if s in ('raccoon','redpanda') and any(o.name.endswith(s+'_'+p+'_mesh') for p in ('leftEar','rightEar','tail')):
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
   check(all(e.is_manifold for e in bm.edges),'watertight '+o.name)
   check(all(f.calc_area()>1e-10 for f in bm.faces),'nonzero '+o.name)
   check(bm.calc_volume(signed=True)>0,'outward '+o.name);bm.free()
 vest=next(o for o in meshes if o.name.endswith('_vest_refined_mesh'));body=next(o for o in meshes if o.name.endswith('_torso_mesh'))
 arm_counts={side:len(bvh(vest).overlap(bvh(next(o for o in meshes if o.name.endswith('_'+side+'Arm_mesh'))))) for side in ('left','right')}
 for side,n in arm_counts.items():check(n==0,'vest arm '+name+side)
 body_contacts=len(bvh(vest).overlap(bvh(body)));contacts[name]=dict(arms=arm_counts,body=body_contacts)
 check(body_contacts==0,'vest body '+name)
 for width,height in [(.88,.9),(.88,1.1),(1.12,.9),(1.12,1.1)]:
  root.scale=(width,width,height);bpy.context.view_layer.update()
  for o in meshes:
   suffix=o.name.split('_after_',1)[1];expected=[Vector((p.x*width,p.y*width,p.z*height)) for p in snapshot[suffix]]
   check(max((a-b).length for a,b in zip(points(o),expected))<3e-6,'export size '+name+o.name+str((width,height)))
 stats[name]=dict(meshes=len(meshes),triangles=tri)
 if kind=='preview':
  old=(f'goose-weapon-wings-{pose}-preview.glb' if s=='goose' else f'species-grips-{s}-{pose}-preview.glb');oldobjs=load(ROOT/'assets'/old)
  for o in oldobjs:
   if o.type!='MESH':continue
   if o.name.endswith('_vest_refined_mesh') and s!='goose':continue
   if s in ('raccoon','redpanda') and any(o.name.endswith('_'+p+'_mesh') for p in ('leftEar','rightEar','tail')):continue
   a=points(o);b=snapshot[o.name];check(max(distance(a,b),distance(b,a))<1e-5,'preserved '+name+' '+o.name)
for path,digest in manifest['protected_files_sha256'].items():check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'protected '+path)
(OUT/'validation.json').write_text(json.dumps(dict(passed=len(checks),stats=stats,contacts=contacts,checks=checks),indent=2),encoding='utf-8')
print('FITTED_VALIDATED',len(checks),flush=True)
