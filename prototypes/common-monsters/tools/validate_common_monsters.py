"""Independent re-import check. Run with Blender --background --python this_file.py."""
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import json
import argparse
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'prototypes/common-monsters/output'
parser=argparse.ArgumentParser(description=__doc__)
species_choices=['eagle','parrot','lion','tiger','reindeer','rhino','zombie']
parser.add_argument('--species',choices=species_choices,nargs='+')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
audit_name='audit.json' if not args.species or set(args.species)==set(species_choices) else 'audit-'+'-'.join(args.species)+'.json'
expected=json.loads((OUT/audit_name).read_text(encoding='utf-8'))
if args.species:check_species={row['species'] for row in expected};assert check_species==set(args.species),'audit selection mismatch'
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
    # Reimport each file with its authored material names, without suffixes from a prior asset.
    for mesh in list(bpy.data.meshes):
        if mesh.users==0:bpy.data.meshes.remove(mesh)
    for material in list(bpy.data.materials):
        if material.users==0:bpy.data.materials.remove(material)
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
    if species=='parrot':
        for side in ['left','right']:
            arm=bpy.data.objects.get(f'common_parrot_{side}Arm_mesh')
            check(arm is not None,f'parrot {side} authored wing')
            check(arm.parent.name==f'common_parrot_{side}Arm',f'parrot {side} wing pivot')
            check({m.name for m in arm.data.materials}=={'common_primary','common_accent'},f'parrot {side} wing material channels')
        check(counts['base']<8000,'parrot base crowd budget')
        body=bpy.data.objects['Deform_mesh']
        vertices=[body.matrix_world@v.co for v in body.data.vertices]
        primary=[tuple(p.vertices) for p in body.data.polygons if body.data.materials[p.material_index].name=='common_primary']
        torso=BVHTree.FromPolygons(vertices,primary,all_triangles=True)
        clearance=[]
        for p in body.data.polygons:
            if body.data.materials[p.material_index].name!='common_secondary':continue
            center=sum((vertices[i] for i in p.vertices),Vector())/len(p.vertices)
            hit=torso.ray_cast(Vector((center.x,-2,center.z)),Vector((0,1,0)))[0]
            check(hit is not None,'parrot belly backed by torso')
            clearance.append(hit.y-center.y)
        check(clearance and min(clearance)>.001,f'parrot belly no surface intersection: {min(clearance):.6f}')
        print('PARROT_BELLY_MIN_CLEARANCE',min(clearance),'samples',len(clearance))
    # glTF default represents every selectable module; runtime hides before display.
    check(len(counts)==(10 if species=='zombie' else 3),f'{species} module inventory')
    results.append({'species':species,'triangles':counts,'bytes':asset.stat().st_size})
(OUT/('reimport-'+audit_name)).write_text(json.dumps({'checks':checks,'results':results},indent=2),encoding='utf-8')
print(f'COMMON_REIMPORT_PASS checks={checks} assets={len(results)}')
