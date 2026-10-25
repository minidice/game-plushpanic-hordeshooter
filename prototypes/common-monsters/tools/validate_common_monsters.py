"""Independent re-import check. Run with Blender --background --python this_file.py."""
import bpy
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
    if species in ['parrot','eagle','lion']:
        for side in ['left','right']:
            arm=bpy.data.objects.get(f'common_{species}_{side}Arm_mesh')
            check(arm is not None,f'{species} {side} authored limb')
            check(arm.parent.name==f'common_{species}_{side}Arm',f'{species} {side} limb pivot')
            channels={'common_primary'} if species=='lion' else {'common_primary','common_accent' if species=='parrot' else 'common_secondary'}
            check({m.name for m in arm.data.materials}==channels,f'{species} {side} limb material channels')
            if species=='lion':
                paw=bpy.data.objects.get(f'common_lion_{side}Paw')
                check(paw is not None and paw.parent==arm.parent,f'lion {side} separate paw under shoulder')
                hand=bpy.data.objects.get(f'common_lion_{side}Paw_mesh')
                check(hand is not None and hand.parent==paw,f'lion {side} rigid paw mesh')
                check({m.name for m in hand.data.materials}=={'common_primary'},f'lion {side} natural paw surface without palm pad')
                anchors=[bpy.data.objects.get(f'common_lion_{side}{name}') for name in ['Wrist','Grip']]
                check(all(a is not None and a.parent==paw for a in anchors),f'lion {side} authored wrist/grip anchors')
                check(.06<(anchors[0].matrix_world.translation-anchors[1].matrix_world.translation).length<.25,f'lion {side} compact wrist-to-grip length')
        check(counts['base']<8000,f'{species} base crowd budget')
    if species=='lion':
        palette={m.name for obj in meshes if owner(obj)=='base' for m in obj.data.materials}
        check({'common_primary','common_secondary','fixed_mane'}.issubset(palette),'lion body muzzle and mane channels retained')
        check(not {'fixed_glint','fixed_thread'} & palette,'lion no glint beads or body stitching')
        check('fixed_cream' in {m.name for m in bpy.data.objects['Head_mesh'].data.materials},'lion light muzzle survives runtime palette swaps')
        check('common_secondary' in {m.name for m in bpy.data.objects['common_lion_tail_mesh'].data.materials},'lion secondary channel retained naturally on tail tuft')
        for foot in ['LeftFoot','RightFoot']:
            mesh=bpy.data.objects.get(foot+'_mesh')
            check(mesh is not None,f'lion {foot} authored lower limb mesh')
            vertices=[mesh.matrix_world@v.co for v in mesh.data.vertices]
            sole_area=0
            for poly in mesh.data.polygons:
                pts=[vertices[i] for i in poly.vertices]
                if all(abs(p.z)<.0002 for p in pts):sole_area+=(pts[1]-pts[0]).cross(pts[2]-pts[0]).length*.5
            check(sole_area>.02,f'lion {foot} planar ground-contact area: {sole_area:.5f} m2')
            check(min(v.z for v in vertices)>-.0002,f'lion {foot} no below-floor geometry')
            print('LION_SOLE_AREA',foot,sole_area)
    if species=='eagle':
        for foot in ['LeftFoot','RightFoot']:
            mesh=bpy.data.objects.get(foot+'_mesh')
            check(mesh is not None,f'eagle {foot} authored lower limb mesh')
            points=[mesh.matrix_world@v.co for v in mesh.data.vertices]
            sole_area=0
            for poly in mesh.data.polygons:
                pts=[points[i] for i in poly.vertices]
                if all(abs(p.z)<.0002 for p in pts):sole_area+=(pts[1]-pts[0]).cross(pts[2]-pts[0]).length*.5
            check(sole_area>.015,f'eagle {foot} planar ground contact: {sole_area:.5f} m2')
            check(min(v.z for v in points)>-.0002,f'eagle {foot} no below-floor geometry')
            high=[v for v in points if v.z>.09]
            check(max(v.x for v in high)-min(v.x for v in high)<.15,f'eagle {foot} narrow ankle')
            check({m.name for m in mesh.data.materials}=={'fixed_beak','fixed_ink'},f'eagle {foot} foot and claw material channels')
            print('EAGLE_SOLE_AREA',foot,sole_area)
    if species=='parrot':
        check('HeadSpringScale = 0.05' in resource,'parrot reduced visual head spring survives generation')
        body=bpy.data.objects['Deform_mesh']
        vertices=[body.matrix_world@v.co for v in body.data.vertices]
        check({m.name for m in body.data.materials}=={'common_primary'},'parrot coherent torso material without separate belly panel')
        # glTF may split vertices at custom normal boundaries. Weld only position keys
        # for topology inspection; one closed component rules out a detached skin patch.
        keys=[tuple(round(v,5) for v in p) for p in vertices];adjacency={};edges={}
        for p in body.data.polygons:
            for a,b in zip(p.vertices,list(p.vertices[1:])+[p.vertices[0]]):
                ka,kb=keys[a],keys[b];edge=tuple(sorted((ka,kb)));edges[edge]=edges.get(edge,0)+1
                adjacency.setdefault(ka,set()).add(kb);adjacency.setdefault(kb,set()).add(ka)
        pending=[keys[0]];visited=set()
        while pending:
            vertex=pending.pop()
            if vertex in visited:continue
            visited.add(vertex);pending.extend(adjacency[vertex]-visited)
        check(len(visited)==len(adjacency),'parrot torso is one shared connected surface')
        check(all(count==2 for count in edges.values()),'parrot torso closed without panel boundary edges')
        head=bpy.data.objects['Head_mesh'];neck_height=bpy.data.objects['Head'].matrix_world.translation.z
        head_ring=[head.matrix_world@v.co for v in head.data.vertices if abs((head.matrix_world@v.co).z-neck_height)<.0002]
        body_ring=[v for v in vertices if abs(v.z-neck_height)<.0002]
        check(len(body_ring)>=32 and len(head_ring)>=32,'parrot shared neck boundary rings')
        neck_gap=max(min((a-b).length for b in head_ring) for a in body_ring)
        check(neck_gap<.0002,f'parrot head torso continuous at rest: {neck_gap:.6f}')
        head_skin=[head.matrix_world@head.data.vertices[i].co for p in head.data.polygons
                   if head.data.materials[p.material_index].name=='common_primary' for i in p.vertices]
        check(min(v.z for v in head_skin)<neck_height-.10,'parrot hidden neck overlap for existing head nod poses')
        palette={m.name for obj in meshes if owner(obj)=='base' for m in obj.data.materials}
        check({'common_primary','common_secondary','common_accent'}.issubset(palette),'parrot three palette slots retained')
        check(not {'fixed_cream','fixed_glint','fixed_thread'} & palette,'parrot no eye ring glint bead or body stitching')
        for foot in ['LeftFoot','RightFoot']:
            mesh=bpy.data.objects[foot+'_mesh'];points=[mesh.matrix_world@v.co for v in mesh.data.vertices]
            sole_area=0
            for poly in mesh.data.polygons:
                pts=[points[i] for i in poly.vertices]
                if all(abs(p.z)<.0002 for p in pts):sole_area+=(pts[1]-pts[0]).cross(pts[2]-pts[0]).length*.5
            check(sole_area>.035,f'parrot {foot} planar ground contact: {sole_area:.5f} m2')
            check(min(v.z for v in points)>-.0002,f'parrot {foot} no below-floor geometry')
            high=[v for v in points if v.z>.09];low=[v for v in points if v.z<=.08]
            check(max(v.x for v in high)-min(v.x for v in high)<.17,f'parrot {foot} narrow ankle above thin foot')
            check(max(v.x for v in low)-min(v.x for v in low)>.23,f'parrot {foot} readable flat toe spread')
            check({m.name for m in mesh.data.materials}=={'fixed_beak'},f'parrot {foot} bird surface without stitched padding')
            print('PARROT_SOLE_AREA',foot,sole_area)
        print('PARROT_NECK_MAX_GAP',neck_gap)
    # glTF default represents every selectable module; runtime hides before display.
    check(len(counts)==(10 if species=='zombie' else 3),f'{species} module inventory')
    results.append({'species':species,'triangles':counts,'bytes':asset.stat().st_size})
(OUT/('reimport-'+audit_name)).write_text(json.dumps({'checks':checks,'results':results},indent=2),encoding='utf-8')
print(f'COMMON_REIMPORT_PASS checks={checks} assets={len(results)}')
