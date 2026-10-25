"""Independent re-import check. Run with Blender --background --python this_file.py."""
import bpy
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'prototypes/common-monsters/output'
expected=json.loads((OUT/'audit.json').read_text(encoding='utf-8'))
checks=0
results=[]


def check(value,message):
    global checks
    assert value,message
    checks+=1


def owner(obj):
    while obj:
        if obj.type=='EMPTY' and obj.name.startswith(('Outfit_','Gear_')):return obj.name
        obj=obj.parent
    return 'base'


for record in expected:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    species=record['species'];asset=ROOT/f'game/assets/common-monsters/{species}.glb'
    bpy.ops.import_scene.gltf(filepath=str(asset))
    for name in record['required_pivots']:check(bpy.data.objects.get(name) is not None,f'{species} missing {name}')
    check(bpy.data.objects['Body'].parent.name=='Root',f'{species} Body parent')
    check(bpy.data.objects['Deform'].parent.name=='Body',f'{species} Deform parent')
    check(bpy.data.objects['Head'].parent.name=='Deform',f'{species} Head parent')
    check(abs(bpy.data.objects['Body'].location.z)<.0001,f'{species} Body floor pivot')
    check(abs(bpy.data.objects['Deform'].location.z)<.0001,f'{species} Deform floor pivot')
    check(bpy.data.objects['Head'].location.z>1,f'{species} Head neck pivot')
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];counts={}
    for o in meshes:
        key=owner(o);counts[key]=counts.get(key,0)+sum(len(p.vertices)-2 for p in o.data.polygons)
        check(all(m.use_nodes and m.node_tree.nodes.get('Principled BSDF') for m in o.data.materials),f'{species} material {o.name}')
        check(all(len(p.vertices)==3 for p in o.data.polygons),f'{species} triangulated {o.name}')
    check(counts==record['triangles_by_module'],f'{species} export triangle counts {counts}')
    check(not any(o.type=='ARMATURE' for o in bpy.context.scene.objects),f'{species} no skeletal rig')
    resource=(ROOT/f'game/content/enemies/common_{species}.tres').read_text(encoding='utf-8')
    check(f'res://assets/common-monsters/{species}.glb' in resource,f'{species} resource path')
    check(f'Id = "common_{species}"' in resource,f'{species} resource Id')
    check('MoveSpeed' not in resource and 'MaxHealth' not in resource,f'{species} no combat stats in art')
    points=[o.matrix_world@v.co for o in meshes if owner(o)=='base' for v in o.data.vertices]
    check(-.03<min(v.z for v in points)<.03,f'{species} soles at origin')
    check(max(v.z for v in points)<2.4,f'{species} silhouette bounds')
    check(not any('helmet' in o.name.lower() for o in bpy.context.scene.objects),f'{species} no helmet')
    # glTF default represents every selectable module; runtime hides before display.
    check(len(counts)==(10 if species=='zombie' else 3),f'{species} module inventory')
    results.append({'species':species,'triangles':counts,'bytes':asset.stat().st_size})
(OUT/'reimport-audit.json').write_text(json.dumps({'checks':checks,'results':results},indent=2),encoding='utf-8')
print(f'COMMON_REIMPORT_PASS checks={checks} assets={len(results)}')
