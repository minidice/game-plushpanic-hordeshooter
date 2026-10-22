"""Reimport every species pose export; validate the new limbs and preservation."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1];OUT=ROOT/'output/species-weapon-grips'
m=json.loads((ROOT/'species-grips-manifest.json').read_text());checks=[];observations=[];baseline={};bad_source={}
def check(ok,label):
    assert ok,label
    checks.append(label)
def points(o):return {tuple(o.matrix_world@v.co) for v in o.data.vertices}
def distance(a,b):
    tree=KDTree(len(b))
    for i,p in enumerate(b):tree.insert(p,i)
    tree.balance();return max(tree.find(p)[2] for p in a)
def bvh(o):return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
for path,digest in m['protected_files_sha256'].items():check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Protected unchanged: '+Path(path).name)
for species,names in m['preserved_meshes'].items():
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(REPO/f'game/assets/characters/{species}/{species}.glb'))
    baseline[species]={name:points(bpy.data.objects[name]) for name in names}
    for name in names:bad_source[name]=sum(p.area<=1e-12 for p in bpy.data.objects[name].data.polygons)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'))
vest_points=points(bpy.data.objects['after_goose_vest_refined_mesh'])
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(REPO/'game/assets/weapons/m4.glb'))
for o in bpy.context.scene.objects:
    if o.type=='MESH':bad_source[o.name]=sum(p.area<=1e-12 for p in o.data.polygons)
for filename,item in m['exports'].items():
    species,pose,kind=[item[k] for k in ('species','pose','kind')];expected=item['stats'];record=m['poses'][species+'_'+pose+'_after']
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH']
    check(len(meshes)==expected['mesh_objects'],filename+': mesh count')
    check(not any(o.type in ('CAMERA','LIGHT','ARMATURE') for o in objects),filename+': no studio/rig')
    pts=[];tris=0
    for o in meshes:
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
        bad=sum(p.area<=1e-12 for p in o.data.polygons)
        check(bad==bad_source.get(o.name.removeprefix('prop_'),0),filename+': face quality '+o.name)
    check(tris==expected['triangles'],filename+': triangles')
    actual=[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]]
    check(max(abs(actual[j][i]-expected[['bounds_min','bounds_max'][j]][i]) for j in range(2) for i in range(3))<1e-4,filename+': axes/bounds')
    for side in ('left','right'):
        name=species+'_'+side+'Arm';o=bpy.data.objects[name+'_mesh'];check(o.parent.name==name,filename+': parent '+side)
        pivot=Vector((-.43 if side=='left' else .43,-.20,.89))
        check((o.parent.matrix_world.translation-pivot).length<1e-5,filename+': original pivot '+side)
        bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        check(all(e.is_manifold for e in bm.edges),filename+': closed limb '+side)
        bm.faces.ensure_lookup_table();bm.faces.index_update();tree=BVHTree.FromBMesh(bm)
        crossing=[(a,b) for a,b in tree.overlap(tree) if a<b and not set(bm.faces[a].verts).intersection(bm.faces[b].verts)]
        check(not crossing,filename+': no limb self intersections '+side);bm.free()
    for node,key in [('GripShoulder','shoulder'),('GripWrist','wrist'),('WeaponGripReference','grip')]:
        check((bpy.data.objects[node].matrix_world.translation-Vector(record[key])).length<1e-5,filename+': marker '+node)
    arm=bpy.data.objects[species+'_rightArm_mesh'];grip=Vector(record['grip']);tree=bvh(arm);gap=tree.find_nearest(grip)[3]
    check(gap<.06,filename+': grip proximity (<6 cm from grip center)')
    if kind=='only':check(len(meshes)==2 and not any(o.name.startswith('prop_') for o in objects),filename+': limbs only');continue
    for name,expected_points in baseline[species].items():
        actual_points=points(bpy.data.objects[name]);error=max(distance(actual_points,expected_points),distance(expected_points,actual_points))
        check(error<1e-5,filename+': source geometry preserved '+name)
    vest=bpy.data.objects[species+'_vest_refined_mesh'];actual_points=points(vest)
    check(max(distance(actual_points,vest_points),distance(vest_points,actual_points))<1e-5,filename+': accepted vest preserved')
    for side in ('left','right'):check(len(bvh(vest).overlap(bvh(bpy.data.objects[species+'_'+side+'Arm_mesh'])))==0,filename+': vest clearance '+side)
    if kind=='preview':
        props=[o for o in meshes if o.name.startswith('prop_')];check(len(props)==4,filename+': four separate M4 meshes')
        muzzle=bpy.data.objects['prop_Muzzle'];target=Matrix(record['weapon_matrix'])@Vector((0,-.8,.0304))
        check((muzzle.matrix_world.translation-target).length<1e-4,filename+': muzzle pose')
        contacts=[]
        for prop in props:
            pairs=tree.overlap(bvh(prop));distances=[((arm.matrix_world@arm.data.polygons[a].center)-grip).length for a,b in pairs]
            far=max(distances,default=0);contacts.append(dict(mesh=prop.name,pairs=len(pairs),max_face_center_distance=far))
            check(far<.10,filename+': gun contact only near grip '+prop.name)
        observations.append(dict(species=species,pose=pose,grip_surface_distance=gap,contacts=contacts))
        print('POSE_CHECKED',species,pose,json.dumps(observations[-1]),flush=True)
    else:check(not any(o.name.startswith('prop_') for o in objects),filename+': no embedded gun')
report=dict(passed=len(checks),checks=checks,pose_observations=observations,inherited_degenerate_faces={k:v for k,v in bad_source.items() if v},scope='27 static GLB round trips, new limb topology and contact, unchanged body/face/ears/tails/vest; no runtime or continuous motion test.')
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print('SPECIES_GRIPS_VALIDATED',len(checks),flush=True)
