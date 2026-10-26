"""Independent exported rhino shape, palette, rig, source-preservation checks."""
import bpy
import bmesh
import hashlib
import json
import argparse
import sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'prototypes/common-monsters/output/rhino-clean-forms'
ASSETS=ROOT/'game/assets/common-monsters'
parser=argparse.ArgumentParser()
parser.add_argument('--baseline-hashes',type=Path,help='Optional pre-edit asset hashes for a single-species authoring check')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT.mkdir(parents=True,exist_ok=True)
checks=[]
def check(ok,label):
    assert ok,label
    checks.append(label)
def points(obj):return [obj.matrix_world@v.co for v in obj.data.vertices]
def near(a,b):return (Vector(a)-Vector(b)).length<.0001

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ASSETS/'rhino.glb'))
objects={o.name:o for o in bpy.context.scene.objects}
for node in ('Root','Body','Deform','Head','Weapon','Muzzle','Gear_Vest','Gear_Glasses','LeftFoot','RightFoot'):
    check(node in objects,'preserved node '+node)
check(near(objects['Head'].matrix_world.translation,(0,0,1.18)),'original head pivot')
check(objects['Head'].parent.name=='Deform','head parent contract')
check(not any(o.type=='ARMATURE' for o in objects.values()),'common pivot rig preserved')
base=[o for o in objects.values() if o.type=='MESH' and not o.name.startswith('Gear_')]
palette={m.name for o in base for m in o.data.materials}
check({'common_primary','common_secondary','fixed_horn','fixed_eye'}<=palette,'semantic primary/secondary/horn/eye channels')
check(not {'fixed_glint','fixed_thread','fixed_innerEar'} & palette,'no eye glint beads or body stitching')
allpoints=[p for o in base for p in points(o)]
check(abs(min(p.z for p in allpoints))<.0001,'soles grounded')
check(1.90<max(p.z for p in allpoints)<2.05,'recognizable ears within crowd height')
check(sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in base)<8000,'crowd base budget')
head=objects['Head_mesh']
def material_points(obj,material):
    indices={i for poly in obj.data.polygons if obj.data.materials[poly.material_index].name==material for i in poly.vertices}
    return [obj.matrix_world@obj.data.vertices[i].co for i in indices]
eyes=material_points(head,'fixed_eye')
check(max(p.z for p in eyes)-min(p.z for p in eyes)<.078,'small shallow eyes')
horn=material_points(head,'fixed_horn')
check(min(p.y for p in horn)<-.55 and max(p.z for p in horn)>1.80,'front horn clearly projects from muzzle')
check(max(p.x for p in horn)-min(p.x for p in horn)<.22,'horn distinct from broad muzzle')
for side,sign,footname in [('left',-1,'LeftFoot'),('right',1,'RightFoot')]:
    prefix=f'common_rhino_{side}'
    for suffix in ('Arm','Arm_mesh','Shoulder','Paw','Paw_mesh','Wrist','Grip'):
        check(prefix+suffix in objects,side+' authored '+suffix)
    arm=objects[prefix+'Arm'];paw=objects[prefix+'Paw'];mesh=objects[prefix+'Paw_mesh']
    check(near(arm.matrix_world.translation,(sign*.39,0,.99)),side+' original shoulder pivot')
    check(paw.parent==arm and mesh.parent==paw,side+' separate rigid palm under arm')
    check(near(objects[prefix+'Shoulder'].matrix_world.translation,(sign*.39,0,1.05)),side+' actual shoulder cap marker')
    check(all(objects[prefix+n].parent==paw for n in ('Wrist','Grip')),side+' attached wrist/grip')
    check({m.name for m in mesh.data.materials}=={'common_primary'},side+' palm uses body palette')
    palm_points=points(mesh)
    check(max(p.z for p in palm_points)-min(p.z for p in palm_points)<.22,side+' compact hand length')
    check(max(p.y for p in palm_points)-min(p.y for p in palm_points)<.13,side+' shallow palm thickness')
    foot=objects[footname+'_mesh'];fp=points(foot)
    check(sum(abs(p.z)<.0001 for p in fp)>=20,side+' actual flat sole samples')
    check(max(p.z for p in fp)>.44,side+' connected ankle reaches body')
    check('fixed_horn' in {m.name for m in foot.data.materials},side+' toe nails semantic horn')
    for part in (mesh,objects[prefix+'Arm_mesh'],foot):
        bm=bmesh.new();bm.from_mesh(part.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        check(all(e.is_manifold for e in bm.edges),part.name+' closed component surfaces')
        bm.normal_update();normals=[f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        check(all(a.dot(b.normal)>.999 for a,b in zip(normals,bm.faces)),part.name+' outward consistent winding')
        check(bm.calc_volume(signed=True)>0,part.name+' positive volume')
        if part==mesh:
            visited=set();pending=[next(iter(bm.verts))]
            while pending:
                v=pending.pop()
                if v in visited:continue
                visited.add(v);pending.extend(e.other_vert(v) for e in v.link_edges)
            check(len(visited)==len(bm.verts),side+' palm is one connected surface')
            nearest=BVHTree.FromBMesh(bm).find_nearest(part.matrix_world.inverted()@objects[prefix+'Grip'].matrix_world.translation)
            check(nearest[3]<.04,side+' actual grip lies near palm surface')
        bm.free()
if args.baseline_hashes:
    baseline=json.loads(args.baseline_hashes.read_text(encoding='utf-8'))
    check(all(hashlib.sha256((ASSETS/name).read_bytes()).hexdigest()==digest for name,digest in baseline.items() if name!='rhino.glb'),
          'unselected monster assets unchanged against supplied authoring baseline')
report={'checks':checks,'sha256':hashlib.sha256((ASSETS/'rhino.glb').read_bytes()).hexdigest()}
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('RHINO_CLEAN_VALIDATED',len(checks),flush=True)
