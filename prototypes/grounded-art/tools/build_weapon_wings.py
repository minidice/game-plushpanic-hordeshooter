"""Offline weapon-specific goose wings: ready, aim, aim-up. Game files read only."""
import bpy, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/weapon-wings';OUT.mkdir(parents=True,exist_ok=True)
protected=[p for p in list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend')) if not p.name.startswith('goose-weapon-wings-')]
protected += [ROOT.parents[1]/p for p in ['game/assets/characters/goose/goose.glb','game/assets/weapons/m4.glb','game/scripts/Characters/CharacterWeaponGrip.cs','game/scripts/Weapons/WeaponVisual.cs']]
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'));scene=bpy.context.scene
for o in list(scene.objects):
    if not o.name.startswith(('after_','STUDIO_')):bpy.data.objects.remove(o,do_unlink=True)
base=[o for o in scene.objects if o.name.startswith('after_')]
for o in base:o.name='template_'+o.name.removeprefix('after_');o.hide_set(False)
bpy.context.view_layer.update()
rest=bpy.data.objects['template_goose_rightArm_mesh']
rest_vertices=[rest.matrix_world@v.co for v in rest.data.vertices]
POSES={'ready':((.52,-.52,1.02),12),'aim':((.54,-.65,1.22),0),'aim_up':((.54,-.65,1.22),-20)}
variants={};exports={};records={}
def clone(prefix):
    mapping={}
    for o in base:
        new=o.copy()
        if o.type=='MESH':new.data=o.data.copy()
        new.name=prefix+o.name.removeprefix('template_');scene.collection.objects.link(new);mapping[o]=new
    for o,new in mapping.items():new.parent=mapping.get(o.parent);new.matrix_parent_inverse=o.matrix_parent_inverse.copy();new.matrix_basis=o.matrix_basis.copy()
    bpy.context.view_layer.update();return list(mapping.values())
def bvh(o):return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
def stats(objects):
    meshes=[o for o in objects if o.type=='MESH'];pts=[];tris=0
    for o in meshes:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    return dict(mesh_objects=len(meshes),triangles=tris,bounds_min=[min(v[i] for v in pts) for i in range(3)],bounds_max=[max(v[i] for v in pts) for i in range(3)])
def export(filename,objects,prefix):
    bpy.ops.object.select_all(action='DESELECT');names={o:o.name for o in objects}
    for o in objects:o.name=o.name.removeprefix(prefix);o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets'/filename),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name
    exports[filename]=stats(objects)
def marker(name,position,parent):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.parent=parent;o.matrix_world=Matrix.Translation(position);return o
def make_wing(o,grip):
    # Same topology as the accepted folded wing. Bend the shoulder-to-wrist
    # surface; curl the last three feather ends around the outside of the grip.
    shoulder=Vector((.435,-.150,1.12))
    elbow=Vector((.640,-.165,.98));approach=grip+Vector((.140,-.025,.005))
    end=grip+Vector((-.012,-.035,-.025))
    def center(s):
        t=max(0,min(1,s))
        p=(1-t)**3*shoulder+3*(1-t)**2*t*elbow+3*(1-t)*t*t*approach+t**3*end
        # A lowered gun raises its stock toward the shoulder; route the folded
        # middle of the wing outside that stock, keeping shoulder/tips anchored.
        if grip.z<1.0:p.x+=.065*math.sin(math.pi*t)
        return p
    wrist=center(.78)
    o.matrix_world=Matrix.Identity(4)
    for vertex,p in zip(o.data.vertices,rest_vertices):
        v=(1.150-p.z)/1.13;u=(-p.y-.145-.08*v)/1.15;s=(v-.01)/.357
        old_mid=.455+.035*v/.37-.030*math.exp(-(v/.065)**2)
        depth=p.x-old_mid
        at=max(.005,min(.995,s))
        tangent=(center(at+.005)-center(at-.005)).normalized()
        if tangent.length<.5:tangent=Vector((0,-1,0))
        width=Vector((0,0,-1));width=(width-tangent*width.dot(tangent)).normalized()
        normal=tangent.cross(width).normalized()
        taper=1-.40*max(0,min(1,(s-.55)/.45))
        vertex.co=center(s)+width*(u-.007)*1.15*taper+normal*(depth*.85)
    o.data.update()
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    return shoulder,wrist

for pose,(position,pitch) in POSES.items():
    gun_matrix=Matrix.Translation(position)@Matrix.Rotation(math.radians(pitch),4,'X')
    grip=gun_matrix@Vector((.035,0,-.0675))
    for kind in ('before','after'):
        prefix=pose+'_'+kind+'_';objects=clone(prefix);w=bpy.data.objects[prefix+'goose_rightArm_mesh']
        if kind=='before':
            # Prior candidate's rigid-wing approach, positioned at this gun pose.
            shoulder=Vector((.475,-.155,1.06));rotation=Vector((0,0,-1)).rotation_difference((grip-shoulder).normalized()).to_matrix().to_4x4()
            w.matrix_world=Matrix.Translation(shoulder)@rotation@Matrix.Translation(-shoulder)@w.matrix_world
        else:
            shoulder,wrist=make_wing(w,grip)
            objects.extend([marker(prefix+'WingShoulder',shoulder,w.parent),marker(prefix+'WingWrist',wrist,w.parent),marker(prefix+'WeaponGripReference',grip,w.parent)])
        old=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(ROOT.parents[1]/'game/assets/weapons/m4.glb'));props=list(set(scene.objects)-old)
        for o in props:
            if not o.parent:o.matrix_world=gun_matrix@o.matrix_world
            o.name=prefix+'prop_'+o.name
        bpy.context.view_layer.update();variants[(pose,kind)]=(objects,props)
        vest=bpy.data.objects[prefix+'goose_vest_refined_mesh'];pairs=len(bvh(vest).overlap(bvh(w)))
        record=dict(vest_intersecting_face_pairs=pairs,weapon_matrix=[list(row) for row in gun_matrix],grip=list(grip))
        if kind=='after':
            record['wrist']=list(wrist);record['shoulder']=list(shoulder)
            export(f'goose-weapon-wings-{pose}-character.glb',objects,prefix)
            wings=[o for o in objects if o.type=='MESH' and 'Arm_mesh' in o.name];wing_objects=set(wings)
            for o in wings:
                while o.parent:o=o.parent;wing_objects.add(o)
            wing_objects.update(o for o in objects if o.name.endswith(('WingShoulder','WingWrist','WeaponGripReference')))
            export(f'goose-weapon-wings-{pose}-only.glb',list(wing_objects),prefix)
            export(f'goose-weapon-wings-{pose}-preview.glb',objects+props,prefix)
        records[pose+'_'+kind]=record
        print('POSE',pose,kind,json.dumps(record),flush=True)

for o in base:bpy.data.objects.remove(o,do_unlink=True)
def show(pair):
    visible=set(sum(variants[pair],[]))
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
camera=scene.camera
def view(pos,target,scale):
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
scene.cycles.samples=32;scene.render.resolution_x=720;scene.render.resolution_y=840
views={'quarter':((4,-6,3.1),(.10,-.22,1.1),3.0),'detail':((6,-4,2.7),(.38,-.27,1.08),1.45),
       'side':((7,-.3,2.5),(.08,-.23,1.1),3.0),'top':((4,-6,10),(.08,-.25,1.1),3.0)}
show(('aim','after'));view(*views['quarter']);bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-weapon-wings-study.blend'))
manifest=dict(date='2026-10-23',status='offline authored pose candidates; game unchanged',protected_files_sha256=hashes,exports=exports,poses=records,
              baseline='Previous folded-wing candidate rigidly rotated toward the grip, not a runtime screenshot.',
              preserved_meshes=[o.name.removeprefix('aim_after_') for o in variants[('aim','after')][0] if o.type=='MESH' and 'Arm_mesh' not in o.name],
              limitations=['Three static one-handed authored poses, no runtime animation or continuous transition validation.','Gun is included only in preview exports; character and wings-only exports exclude it.'])
(ROOT/'weapon-wings-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
preview='--preview' in sys.argv
for pose in POSES:
    if preview and pose!='aim':continue
    for label,values in views.items():
        if preview and label not in ('quarter','detail'):continue
        if label=='detail' and pose=='aim_up':continue
        view(*values)
        for kind in ('before','after'):
            show((pose,kind));scene.render.filepath=str(OUT/f'{pose}-{kind}-{label}.png');bpy.ops.render.render(write_still=True)
            print('RENDERED',pose,kind,label,flush=True)
for path,digest in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,path
print('WEAPON_WINGS_READY',flush=True)
