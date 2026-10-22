"""Publish approved surfaces into the existing game rig, preserving all other wardrobe slots.
Run explicitly after regenerating the original character-lab models. Originals and candidates stay intact.
"""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Matrix
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1];report={}
for s in ('goose','duck','raccoon','redpanda'):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/f'fitted-species-{s}-aim-character.glb'))
 candidates=list(bpy.context.scene.objects);sources={}
 for part in ('vest_refined','leftArm')+(('leftEar','rightEar','tail') if s in ('raccoon','redpanda') else ()):
  o=next(o for o in candidates if o.name.endswith(s+'_'+part+'_mesh'));sources[part]=(o.data.copy(),o.matrix_world.copy())
 for o in candidates:bpy.data.objects.remove(o,do_unlink=True)
 original=REPO/f'game/assets/characters/{s}/{s}.glb';digest=hashlib.sha256(original.read_bytes()).hexdigest()
 bpy.ops.import_scene.gltf(filepath=str(original))
 for target,source in [('vest','vest_refined'),('leftArm','leftArm'),('rightArm','leftArm')]+([(p,p) for p in ('leftEar','rightEar','tail')] if s in ('raccoon','redpanda') else []):
  o=bpy.data.objects[s+'_'+target+'_mesh'];materials=list(o.data.materials);data,world=sources[source]
  replacement=data.copy();transform=o.matrix_world.inverted()@(Matrix.Diagonal((-1,1,1,1)) if target=='rightArm' else Matrix.Identity(4))@world
  replacement.transform(transform);replacement.name='fitted_'+s+'_'+target;o.data=replacement
  if target.endswith('Arm'):
   o.data.materials.clear()
   for mat in materials:o.data.materials.append(mat)
  if target=='rightArm':
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
  if target=='vest':
   for i,mat in enumerate(list(o.data.materials)):
    own=mat.copy();own.name='custom_vest_fitted_'+str(i);o.data.materials[i]=own
  # Imported loops can retain stale custom normals after reflection; recalculate just the new mirror.
  if target=='rightArm':
   bpy.context.view_layer.objects.active=o
   if o.data.has_custom_normals:bpy.ops.mesh.customdata_custom_splitnormals_clear()
   for p in o.data.polygons:p.use_smooth=True
 out=original.with_name(s+'-fitted.glb')
 bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',export_yup=True,export_cameras=False,export_lights=False)
 assert hashlib.sha256(original.read_bytes()).hexdigest()==digest
 report[s]=dict(path=str(out.relative_to(REPO)),original_sha256=digest,bytes=out.stat().st_size)
(ROOT/'game-publication.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('GAME_MODELS_PUBLISHED',flush=True)
