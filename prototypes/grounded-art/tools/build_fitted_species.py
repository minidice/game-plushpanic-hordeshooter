"""Repair isolated candidates; never write to game or previous candidate files."""
import bpy,bmesh,math,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1];OUT=ROOT/'output/fitted-species';OUT.mkdir(parents=True,exist_ok=True)
SPECIES=('goose','duck','raccoon','redpanda');POSES=('ready','aim','aim_up')
paths=list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend'))+list((REPO/'game/assets/characters').rglob('*.glb'))
paths=[p for p in paths if not p.name.startswith('fitted-species')]
paths += [REPO/'doc/decisions.md',REPO/'doc/issues.md',REPO/'game/scripts/Characters/CharacterPartShapes.cs']
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'));scene=bpy.context.scene
for o in list(scene.objects):
 if not o.name.startswith('STUDIO_'):bpy.data.objects.remove(o,do_unlink=True)
variants={};exports=[];repairs={};checks=[]
def check(v,label):
 assert v,label
 checks.append(label)
def import_glb(path):
 old=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(path));return list(set(scene.objects)-old)
def smooth_closed(o,cap=False):
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
 if cap:bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 check(all(e.is_manifold for e in bm.edges),'closed '+o.name)
 check(all(f.calc_area()>1e-10 for f in bm.faces),'nonzero '+o.name)
 bm.to_mesh(o.data);bm.free();o.data.update()
 for p in o.data.polygons:p.use_smooth=True
 # Recompute normals after weld; exported split normals from damaged meshes are discarded.
 if o.data.has_custom_normals:
  bpy.context.view_layer.objects.active=o;bpy.ops.mesh.customdata_custom_splitnormals_clear()
 if not cap:
  bpy.context.view_layer.objects.active=o;m=o.modifiers.new('Face weighted soft bevels','WEIGHTED_NORMAL');m.keep_sharp=True;m.weight=40
  bpy.ops.object.modifier_apply(modifier=m.name)
def collect_sources(s):
 previous=set(scene.objects);import_glb(REPO/f'prototypes/character-lab/output/roster/{s}-source.glb');data={}
 for part in ('leftEar','rightEar','tail'):
  chosen=[o for o in set(scene.objects)-previous if o.type=='MESH' and o.parent and o.parent.name==s+'_'+part]
  for o in chosen:smooth_closed(o,part=='tail')
  bpy.ops.object.select_all(action='DESELECT')
  for o in chosen:o.select_set(True)
  bpy.context.view_layer.objects.active=chosen[0];bpy.ops.object.join();joined=chosen[0]
  data[part]=(joined.data.copy(),joined.matrix_world.copy())
  repairs[s+'_'+part]=dict(vertices=len(joined.data.vertices),faces=len(joined.data.polygons))
 for o in list(set(scene.objects)-previous):bpy.data.objects.remove(o,do_unlink=True)
 return data
sources={s:collect_sources(s) for s in ('raccoon','redpanda')}
def clone(objects,prefix):
 mapping={}
 for o in objects:
  n=o.copy()
  if o.type=='MESH':n.data=o.data.copy()
  scene.collection.objects.link(n);n.name=prefix+o.name;mapping[o]=n
 for o,n in mapping.items():n.parent=mapping.get(o.parent);n.matrix_parent_inverse=o.matrix_parent_inverse.copy();n.matrix_basis=o.matrix_basis.copy()
 bpy.context.view_layer.update();return list(mapping.values())
def smoothstep(x):x=max(0,min(1,x));return x*x*(3-2*x)
def fit_vest(o,s):
 if s=='goose':return
 inv=o.matrix_world.inverted()
 for v in o.data.vertices:
  p=o.matrix_world@v.co;a=abs(math.atan2(p.x,-p.y))
  # Preserve the side armhole. Lower the neckline and its patch/webbing together.
  weight=max(smoothstep((.85-a)/.30),smoothstep((a-1.85)/.65))
  p.z-=.12*weight*smoothstep((p.z-.88)/.32);v.co=inv@p
 o.data.update()
def bvh(o):return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
def find(objects,suffix):return next(o for o in objects if o.name.endswith(suffix))
def root_for(objects,name):
 root=bpy.data.objects.new(name,None);scene.collection.objects.link(root)
 for o in objects:
  if o.parent is None:world=o.matrix_world.copy();o.parent=root;o.matrix_world=world
 root['body_width']=1.;root['body_height']=1.;root['uniform_size']=1.
 for key,lo,hi in [('body_width',.88,1.12),('body_height',.9,1.1),('uniform_size',.5,1.5)]:root.id_properties_ui(key).update(min=lo,max=hi,default=1.)
 for axis,prop in enumerate(('body_width','body_width','body_height')):
  d=root.driver_add('scale',axis).driver;d.expression='dimension * size'
  for name,key in [('dimension',prop),('size','uniform_size')]:
   v=d.variables.new();v.name=name;v.type='SINGLE_PROP';v.targets[0].id=root;v.targets[0].data_path='["'+key+'"]'
 objects.append(root);return root
def set_size(root,w,h,u=1):
 root['body_width']=w;root['body_height']=h;root['uniform_size']=u;root.update_tag();scene.frame_set(scene.frame_current);bpy.context.view_layer.update()
for s in SPECIES:
 for pose in POSES:
  file=(f'goose-weapon-wings-{pose}-preview.glb' if s=='goose' else f'species-grips-{s}-{pose}-preview.glb')
  base=import_glb(ROOT/'assets'/file)
  for o in base:o.name=s+'_'+pose+'_before_'+o.name
  prefix=s+'_'+pose+'_before_';after=clone(base,'new_')
  for o in after:o.name=s+'_'+pose+'_after_'+o.name.removeprefix('new_'+prefix)
  if s in sources:
   for part,(data,matrix) in sources[s].items():
    o=find(after,s+'_'+part+'_mesh');o.data=data.copy();o.matrix_world=matrix
  vest=find(after,s+'_vest_refined_mesh');fit_vest(vest,s)
  root=root_for(after,s+'_'+pose+'_after_FitRoot');bpy.context.view_layer.update()
  for side in ('left','right'):check(len(bvh(vest).overlap(bvh(find(after,s+'_'+side+'Arm_mesh'))))==0,f'{s} {pose} {side} vest clearance')
  # All fitted geometry and grip references share a single, nonaccumulating size root.
  baseline={o:[o.matrix_world@v.co for v in o.data.vertices] for o in after if o.type=='MESH'}
  for w,h,u in [(1,1,1),(.88,.9,1),(.88,1.1,1),(1.12,.9,1),(1.12,1.1,1),(1,1,.6),(1,1,1)]:
   set_size(root,w,h,u)
   for o,pts in baseline.items():
    error=max(((o.matrix_world@v.co)-Vector((p.x*w*u,p.y*w*u,p.z*h*u))).length for v,p in zip(o.data.vertices,pts))
    check(error<2e-6,f'{s} {pose} size {w} {h} {u} {o.name}')
  for kind,objects in [('character',[o for o in after if 'prop_' not in o.name]),('preview',after)]:
   bpy.ops.object.select_all(action='DESELECT')
   for o in objects:o.hide_set(False);o.select_set(True)
   path=ROOT/'assets'/f'fitted-species-{s}-{pose}-{kind}.glb'
   bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False,export_cameras=False,export_lights=False)
   exports.append(path.name)
  variants[s,pose,'before']=base;variants[s,pose,'after']=after
  print('FIT_READY',s,pose,flush=True)
def show(s,pose,kind):
 visible=set(variants[s,pose,kind])
 for o in scene.objects:
  if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
def view(pos,target,scale):
 scene.camera.location=pos;scene.camera.rotation_euler=(Vector(target)-scene.camera.location).to_track_quat('-Z','Y').to_euler();scene.camera.data.ortho_scale=scale
def render(name):scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True);print('RENDER',name,flush=True)
scene.cycles.samples=24;scene.render.resolution_x=640;scene.render.resolution_y=720
view((4,-6,3.1),(.08,-.15,1.07),2.85);show('raccoon','aim','after')
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/fitted-species-study.blend'))
manifest=dict(status='isolated static candidates; runtime unchanged',protected_files_sha256=hashes,exports=exports,repairs=repairs,checks=len(checks),size_controls='FitRoot custom properties body_width, body_height, uniform_size. GLB: scale FitRoot TRS, drivers are Blender-only.',fit='Goose unchanged; other species front/back upper vest lowered up to 0.12, side armholes preserved.')
(ROOT/'fitted-species-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
if '--no-render' not in sys.argv:
 for s in SPECIES:
  for kind in ('before','after'):
   show(s,'aim',kind);view((4,-6,3.1),(.08,-.15,1.07),2.85);render(s+'-'+kind+'-front')
   if s in sources:
    view((4,6,3),(.05,.45,1.1),2.65);render(s+'-'+kind+'-rear')
  root=find(variants[s,'aim','after'],'FitRoot');show(s,'aim','after')
  for tag,w,h in [('slim-tall',.88,1.1),('wide-short',1.12,.9)]:
   set_size(root,w,h);view((4,-6,3.1),(.08,-.15,1.07),2.85);render(s+'-'+tag)
  set_size(root,1,1)
for path,digest in hashes.items():check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'unchanged '+path)
(OUT/'build-checks.json').write_text(json.dumps(dict(count=len(checks),checks=checks),indent=2),encoding='utf-8')
print('FITTED_SPECIES_DONE',len(checks),flush=True)
