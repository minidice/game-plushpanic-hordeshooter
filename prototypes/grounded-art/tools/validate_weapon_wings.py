"""Validate the nine actual pose GLBs, source preservation, clearance and topology."""
import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/weapon-wings'
m=json.loads((ROOT/'weapon-wings-manifest.json').read_text());checks=[];observations=[]
def check(ok,label):
    assert ok,label
    checks.append(label)
def points(o):return {tuple(o.matrix_world@v.co) for v in o.data.vertices}
def error(a,b):
    tree=KDTree(len(b))
    for i,p in enumerate(b):tree.insert(p,i)
    tree.balance();return max(tree.find(p)[2] for p in a)
def bvh(o):return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
for path,digest in m['protected_files_sha256'].items():check(hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,'Unchanged protected file: '+Path(path).name)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'))
baseline={name:points(bpy.data.objects['after_'+name]) for name in m['preserved_meshes']+['goose_leftArm_mesh']}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT.parents[1]/'game/assets/weapons/m4.glb'))
weapon_degenerate={o.name:sum(p.area<=1e-12 for p in o.data.polygons) for o in bpy.context.scene.objects if o.type=='MESH'}
print('SOURCE_WEAPON_DEGENERATE_FACES',json.dumps(weapon_degenerate),flush=True)
for filename,expected in m['exports'].items():
    pose=filename.removeprefix('goose-weapon-wings-').rsplit('-',1)[0];kind=filename.rsplit('-',1)[1].removesuffix('.glb')
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(ROOT/'assets'/filename))
    objects=list(bpy.context.scene.objects);meshes=[o for o in objects if o.type=='MESH']
    check(len(meshes)==expected['mesh_objects'],filename+': mesh count')
    check(not any(o.type in ('LIGHT','CAMERA','ARMATURE') for o in objects),filename+': no studio/rig')
    pts=[];triangles=0
    for o in meshes:
        o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
        bad=sum(p.area<=1e-12 for p in o.data.polygons)
        if o.name.startswith('prop_'):
            check(bad==weapon_degenerate[o.name.removeprefix('prop_')],filename+': source weapon face quality preserved '+o.name)
        else:check(bad==0,filename+': nonzero faces '+o.name)
    check(triangles==expected['triangles'],filename+': triangle count')
    actual=[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]]
    check(max(abs(actual[j][i]-expected[['bounds_min','bounds_max'][j]][i]) for j in range(2) for i in range(3))<1e-4,filename+': bounds and axes')
    for side in ('left','right'):
        name='goose_'+side+'Arm';w=bpy.data.objects[name+'_mesh'];check(w.parent.name==name,filename+': wing parent '+side)
        pivot=Vector((-.43 if side=='left' else .43,-.20,.89))
        check((w.parent.matrix_world.translation-pivot).length<1e-5,filename+': original anchor '+side)
        bm=bmesh.new();bm.from_mesh(w.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        check(all(e.is_manifold for e in bm.edges),filename+': closed surface '+side)
        bm.faces.ensure_lookup_table();bm.faces.index_update()
        tree=BVHTree.FromBMesh(bm);pairs=tree.overlap(tree)
        cross=[(a,b) for a,b in pairs if a<b and not set(bm.faces[a].verts).intersection(bm.faces[b].verts)]
        check(len(cross)==0,filename+': no nonadjacent self intersections '+side);bm.free()
    record=m['poses'][pose+'_after']
    for node,key in [('WingShoulder','shoulder'),('WingWrist','wrist'),('WeaponGripReference','grip')]:
        check((bpy.data.objects[node].matrix_world.translation-Vector(record[key])).length<1e-5,filename+': reference marker '+node)
    wing=bpy.data.objects['goose_rightArm_mesh'];tree=bvh(wing);grip=Vector(record['grip'])
    distance=tree.find_nearest(grip)[3]
    check(distance<.045,filename+': wing near grip (<4.5 cm)')
    if kind=='only':check(len(meshes)==2 and not any(o.name.startswith('prop_') for o in objects),filename+': wings only');continue
    for name,expected_points in baseline.items():
        actual_points=points(bpy.data.objects[name]);delta=max(error(actual_points,expected_points),error(expected_points,actual_points))
        check(delta<1e-5,filename+': unchanged geometry '+name)
    vest=bvh(bpy.data.objects['goose_vest_refined_mesh'])
    for side in ('left','right'):
        pairs=len(vest.overlap(bvh(bpy.data.objects['goose_'+side+'Arm_mesh'])))
        check(pairs==0,filename+': vest clearance '+side)
    if kind=='preview':
        props=[o for o in meshes if o.name.startswith('prop_')];check(len(props)==4,filename+': separate four-part M4')
        muzzle=bpy.data.objects['prop_Muzzle'];expected_muzzle=Matrix(record['weapon_matrix'])@Vector((0,-.8,.0304))
        check((muzzle.matrix_world.translation-expected_muzzle).length<1e-4,filename+': weapon pose and muzzle')
        contacts=[]
        for prop in props:
            pairs=tree.overlap(bvh(prop))
            distances=[((wing.matrix_world@wing.data.polygons[a].center)-grip).length for a,b in pairs]
            check(max(distances,default=0)<.10,filename+': gun contact limited to grip region '+prop.name)
            contacts.append(dict(mesh=prop.name,intersecting_face_pairs=len(pairs),outside_grip_14cm=sum(d>.14 for d in distances),max_wing_face_center_distance=max(distances,default=0)))
        observations.append(dict(pose=pose,grip_surface_distance=distance,weapon_contacts=contacts))
    else:check(not any(o.name.startswith('prop_') for o in objects),filename+': no embedded gun')
report=dict(passed=len(checks),checks=checks,pose_observations=observations,inherited_weapon_degenerate_faces=weapon_degenerate,scope='Nine static GLBs; actual reimport, preservation, manifold/self intersections, vest clearance, grip proximity and weapon pose. No runtime or continuous animation tests.')
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print('WEAPON_WINGS_VALIDATED',len(checks),json.dumps(observations),flush=True)
