"""Retain motion pivots; simplify and join static details for the Godot test."""
import bpy, json
from pathlib import Path
base=Path(__file__).resolve().parents[1]
out=base.parent/'goose-playtest'/'assets'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(base/'output'/'goose-source.glb'))
keep={'goose_root','goose_body','goose_deform','goose_torso','goose_head','goose_helmet','goose_vest','goose_pack','goose_weapon','goose_tail','goose_beak','goose_leftFoot','goose_rightFoot','Muzzle'}
groups={}
before=0
for obj in list(bpy.context.scene.objects):
    if obj.type!='MESH': continue
    before+=sum(len(p.vertices)-2 for p in obj.data.polygons)
    bpy.context.view_layer.objects.active=obj
    if len(obj.data.polygons)>100:
        mod=obj.modifiers.new('Game simplification','DECIMATE')
        mod.ratio=.50 if obj.name.endswith('continuous_skin') else .19
        bpy.ops.object.modifier_apply(modifier=mod.name)
    anchor=obj.parent
    while anchor and anchor.name not in keep: anchor=anchor.parent
    world=obj.matrix_world.copy();obj.parent=anchor;obj.matrix_world=world
    groups.setdefault(anchor,[]).append(obj)
for anchor,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    bpy.context.object.name=(anchor.name if anchor else 'goose')+'_mesh'
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for obj in meshes:
    tri=obj.modifiers.new('Stable triangles','TRIANGULATE')
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=tri.name)
bpy.ops.export_scene.gltf(filepath=str(out/'goose.glb'),export_format='GLB',export_animations=False,export_extras=True)
bpy.ops.wm.save_as_mainfile(filepath=str(base/'output'/'goose-game.blend'))
report={'source_triangles':before,'triangles':sum(len(o.data.polygons) for o in meshes),'meshes':len(meshes),'material_slots':sum(len(o.data.materials) for o in meshes),'bytes':(out/'goose.glb').stat().st_size,'nodes':[o.name for o in bpy.context.scene.objects],'forward':'+Z','collider':{'radius':.48,'height':2.18,'center_y':1.09},'notes':'Static details merged per movement pivot. Object animation at runtime; no skeleton, no soft body.'}
(out/'goose-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('GOOSE_OPTIMIZED '+json.dumps({k:v for k,v in report.items() if k!='nodes'}))
