"""Independent Blender reimport and geometry contract check for redpanda-clean.glb."""
import bpy
import bmesh
import hashlib
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT/'game/assets/characters/redpanda/redpanda-fitted.glb'
TARGET = ROOT/'game/assets/characters/redpanda/redpanda-clean.glb'
OUT = ROOT/'prototypes/grounded-art/output/redpanda-clean'
OUT.mkdir(parents=True,exist_ok=True)
player_files=[p for p in (ROOT/'game/assets/characters').glob('*/*.glb') if p != TARGET]
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in player_files}


def read(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return {o.name:o for o in bpy.context.scene.objects if o.name.startswith('redpanda_') or o.name=='Muzzle'}


def world_points(obj): return [obj.matrix_world@v.co for v in obj.data.vertices]
def bounds(obj):
    points=world_points(obj)
    return [[min(p[i] for p in points),max(p[i] for p in points)] for i in range(3)]
def near(a,b,tol=2e-4): return (Vector(a)-Vector(b)).length<tol
def check(ok,label):
    if not ok: raise AssertionError(label)
    checks.append(label)


checks=[]
source=read(SOURCE)
original={name:{'parent':o.parent.name if o.parent else None,
                'world':list(o.matrix_world.translation),
                'materials':[m.name.split('.')[0] for m in o.data.materials] if o.type=='MESH' else [],
                'vertices':len(o.data.vertices) if o.type=='MESH' else 0,
                'triangles':sum(len(p.vertices)-2 for p in o.data.polygons) if o.type=='MESH' else 0,
                'bounds':bounds(o) if o.type=='MESH' else None}
          for name,o in source.items()}
torso_points=sorted(tuple(round(v,5) for v in p) for p in world_points(source['redpanda_torso_mesh']))
source_tris=sum(v['triangles'] for v in original.values())
target=read(TARGET)
check(set(original)<=set(target),'all source contract nodes preserved')
check(not any(o.type=='ARMATURE' for o in bpy.context.scene.objects),'no armature')
for name,data in original.items():
    o=target[name]
    check((o.parent.name if o.parent else None)==data['parent'],f'parent {name}')
    check(near(o.matrix_world.translation,data['world']),f'pivot {name}')
    if o.type=='MESH':
        mats=[m.name.split('.')[0] for m in o.data.materials]
        check(mats==data['materials'],f'materials {name}')
        if name not in [f'redpanda_{s}{p}_mesh' for s in ('left','right') for p in ('Ear','Eye','Brow','Arm','Foot')] and name not in ('redpanda_snout_mesh','redpanda_tail_mesh'):
            check(len(o.data.vertices)==data['vertices'],f'vertices {name}')
            check(all(abs(a-b)<2e-4 for old,new in zip(data['bounds'],bounds(o)) for a,b in zip(old,new)),f'bounds {name}')
check(sorted(tuple(round(v,5) for v in p) for p in world_points(target['redpanda_torso_mesh']))==torso_points,
      'torso local face mapping preserved')

for side,sign in (('left',-1),('right',1)):
    prefix=f'redpanda_{side}'
    arm=target[prefix+'Arm']; paw=target[prefix+'Paw']
    check(paw.parent==arm,f'{side} paw under arm')
    shoulder_marker=target[prefix+'Shoulder']
    check(shoulder_marker.parent==arm,f'{side} authored shoulder marker under original arm pivot')
    check(near(shoulder_marker.matrix_world.translation,(sign*.44,-.158,1.125)),f'{side} true shoulder surface anchor')
    for suffix in ('Paw_mesh','Wrist','Grip'):
        check(target[prefix+suffix].parent==paw,f'{side} {suffix} under paw')
    check(near(target[prefix+'Wrist'].matrix_world.translation,paw.matrix_world.translation),f'{side} wrist at paw origin')
    check(near(paw.matrix_world.translation,(sign*.43,-.207,.752)),f'{side} paw rest anchor')
    check(near(target[prefix+'Grip'].matrix_world.translation,(sign*.39,-.315,.687)),f'{side} grip rest anchor')
    forearm=target[prefix+'Arm_mesh']; palm=target[prefix+'Paw_mesh']
    check(forearm.parent==arm and palm.parent==paw,f'{side} separated forearm and palm')
    shoulder=shoulder_marker.matrix_world.translation
    wrist_at=target[prefix+'Wrist'].matrix_world.translation
    axis=wrist_at-shoulder
    projection=[(point-shoulder).dot(axis)/axis.length_squared for point in world_points(forearm)]
    check(min(projection)>-.06 and max(projection)<1.06,f'{side} forearm endpoints lie between true shoulder and wrist within cap radius')
    check(bounds(forearm)[2][0]>.73 and bounds(forearm)[2][1]<1.14,f'{side} forearm limits')
    check(bounds(palm)[2][1]<.79 and bounds(palm)[2][0]>.60,f'{side} compact paw')
    check([m.name.split('.')[0] for m in palm.data.materials]==[m.name.split('.')[0] for m in forearm.data.materials],f'{side} palette paw')
    for mesh_obj in (forearm,palm,target[prefix+'Ear_mesh'],target[prefix+'Foot_mesh']):
        bm=bmesh.new();bm.from_mesh(mesh_obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
        check(all(edge.is_manifold for edge in bm.edges),f'{mesh_obj.name} closed manifold')
        check(bm.calc_volume(signed=True)>0,f'{mesh_obj.name} outward normals')
        if mesh_obj.name.endswith('Ear_mesh'):
            seen=set();components=0
            for vertex in bm.verts:
                if vertex in seen:continue
                components+=1;stack=[vertex];seen.add(vertex)
                while stack:
                    current=stack.pop()
                    for edge in current.link_edges:
                        other=edge.other_vert(current)
                        if other not in seen:seen.add(other);stack.append(other)
            check(components==1,f'{side} cream inset connects to red rim and back')
        bm.free()
    foot=target[prefix+'Foot_mesh']; foot_points=world_points(foot)
    check(abs(min(v.z for v in foot_points))<1e-4,f'{side} sole z0')
    check(sum(abs(v.z)<1e-4 for v in foot_points)>=12,f'{side} flat sole ring')
    check(len(foot.data.materials)==3,f'{side} foot channels')
    ear=target[prefix+'Ear_mesh']
    check(len(ear.data.materials)==2 and set(p.material_index for p in ear.data.polygons)=={0,1},f'{side} two-channel cupped ear')
    ear_points=world_points(ear)
    center_front=min(p.y for p in ear_points if abs(p.x-sign*.332)<.004 and abs(p.z-1.735)<.004)
    check(center_front-min(p.y for p in ear_points)>.035,f'{side} inner ear recessed behind rim')
    check(1.88<bounds(ear)[2][1]<1.92 and bounds(ear)[2][0]>1.56,f'{side} rounded triangle default ear')
    check(bounds(ear)[2][1]-bounds(ear)[2][0]<.46/1.15,f'{side} pointed alternate stays over 15 percent taller')

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from validate_mammal_tail import validate_tail
validate_tail(target, 'redpanda', check)

target_tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in target.values() if o.type=='MESH')
check(target_tris<source_tris+2000,'triangle growth under 2000')
check({str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in player_files}==hashes,'source and other player GLBs untouched during validation')
report={'source_sha256':hashes[str(SOURCE)],'target_sha256':hashlib.sha256(TARGET.read_bytes()).hexdigest(),
        'source_triangles':source_tris,'target_triangles':target_tris,
        'source_nodes':len(original),'target_nodes':len(target),'checks':checks,
        'anchors':{s:{n:list(target[f'redpanda_{s}{n}'].matrix_world.translation) for n in ('Shoulder','Paw','Wrist','Grip')} for s in ('left','right')}}
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('REDPANDA_VALIDATED',len(checks),source_tris,target_tris,flush=True)
