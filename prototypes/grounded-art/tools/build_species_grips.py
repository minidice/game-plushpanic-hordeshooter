"""Other playable species: folded duck wings and species-specific mammal paws.
All outputs are isolated prototypes. No game file or earlier candidate is edited.
"""
import bpy,math,json,hashlib,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1];OUT=ROOT/'output/species-weapon-grips';OUT.mkdir(parents=True,exist_ok=True)
SPECIES=('duck','raccoon','redpanda')
POSES={'ready':((.52,-.52,1.02),12),'aim':((.54,-.65,1.22),0),'aim_up':((.54,-.65,1.22),-20)}
protected=[p for p in list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend')) if not p.name.startswith('species-grips-')]
protected += [REPO/f'game/assets/characters/{s}/{s}.glb' for s in ('goose',)+SPECIES]
protected += [REPO/p for p in ['game/assets/weapons/m4.glb','game/scripts/Characters/CharacterWeaponGrip.cs','game/scripts/Weapons/WeaponVisual.cs']]
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'));scene=bpy.context.scene
seed_wing=bpy.data.objects['after_goose_rightArm_mesh'].data.copy()
seed_vest=bpy.data.objects['after_goose_vest_refined_mesh'].data.copy()
vest_matrix=bpy.data.objects['after_goose_vest_refined_mesh'].matrix_world.copy()
for o in list(scene.objects):
    if not o.name.startswith('STUDIO_'):bpy.data.objects.remove(o,do_unlink=True)
variants={};exports={};records={};preserved={};bases={}

def mesh_object(name,data,parent):
    o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.parent=parent;o.matrix_world=Matrix.Identity(4);return o
def normals(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=1e-6)
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    for p in o.data.polygons:p.use_smooth=True
def paw_data(species):
    # Three shallow rounded toe lobes, short wrist and fuller palm. These are
    # mammal paws, deliberately not the duck's long split flight feathers.
    outline=[(-.025,.01),(-.060,.05),(-.078,.15),(-.081,.26),(-.073,.325),
             (-.054,.350),(-.028,.347),(-.012,.362),(.010,.363),(.031,.349),
             (.050,.356),(.072,.341),(.080,.30),(.075,.23),(.068,.15),(.050,.05),(.022,.012)]
    boundary=[]
    for i in range(len(outline)):
        a,b,c,d=[Vector(outline[j%len(outline)]) for j in (i-1,i,i+1,i+2)]
        for k in range(4):
            t=k/4;boundary.append(.5*(2*b+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    n=len(boundary);rings=5;center=Vector((0,.18));vs=[];fs=[]
    broad=1.13 if species=='redpanda' else 1.0
    def point(uv,r,front):
        u,v=uv;depth=(.064 if front else -.045)*(1.08 if species=='redpanda' else 1)*math.sqrt(max(0,1-r*r))
        # The same rest parameterization permits the checked shoulder/grip path.
        x=.455+.035*v/.37-.030*math.exp(-(v/.065)**2)
        return (x+depth,-.145-1.15*u*broad-.08*v,1.150-1.13*v)
    for front in (True,False):
        start=len(vs);vs.append(point(center,0,front))
        for j in range(1,rings+1):
            r=j/rings
            for q in boundary:vs.append(point(center+(q-center)*r,r,front))
        for i in range(n):fs.append((start,start+1+i,start+1+(i+1)%n))
        for j in range(rings-1):
            a=start+1+j*n;b=a+n
            for i in range(n):fs.append((a+i,b+i,b+(i+1)%n,a+(i+1)%n))
    data=bpy.data.meshes.new(species+'_paw_seed');data.from_pydata(vs,[],fs);data.update();return data

def bend(o,species,grip):
    shoulder=Vector((.435,-.150,1.12));elbow=Vector((.640,-.165,.98))
    approach=grip+Vector((.140,-.025,.005));end=grip+Vector((-.012,-.035,-.025))
    def center(s):
        t=max(0,min(1,s));p=(1-t)**3*shoulder+3*(1-t)**2*t*elbow+3*(1-t)*t*t*approach+t**3*end
        offset=(.065 if species=='duck' else .085) if grip.z<1 else (0 if species=='duck' else .020)
        p.x+=offset*math.sin(math.pi*t);return p
    for vertex in o.data.vertices:
        p=vertex.co.copy();v=(1.150-p.z)/1.13;u=(-p.y-.145-.08*v)/1.15;s=(v-.01)/.357
        depth=p.x-(.455+.035*v/.37-.030*math.exp(-(v/.065)**2))
        at=max(.005,min(.995,s));tangent=(center(at+.005)-center(at-.005)).normalized()
        width=Vector((0,0,-1));width=(width-tangent*width.dot(tangent)).normalized();normal=tangent.cross(width).normalized()
        taper=1-(.40 if species=='duck' else .15)*max(0,min(1,(s-.55)/.45))
        vertex.co=center(s)+width*(u-(.007 if species=='duck' else 0))*1.15*taper+normal*(depth*.85)
    o.data.update();normals(o);return shoulder,center(.78)
def clone(base,prefix):
    mapping={}
    for old in base:
        new=old.copy()
        if old.type=='MESH':new.data=old.data.copy()
        scene.collection.objects.link(new);new.name=prefix+old.name.removeprefix('template_');mapping[old]=new
    for old,new in mapping.items():new.parent=mapping.get(old.parent);new.matrix_parent_inverse=old.matrix_parent_inverse.copy();new.matrix_basis=old.matrix_basis.copy()
    bpy.context.view_layer.update();return list(mapping.values())
def bvh(o):return BVHTree.FromPolygons([o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
def stats(objects):
    meshes=[o for o in objects if o.type=='MESH'];pts=[];tris=0
    for o in meshes:o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    return dict(mesh_objects=len(meshes),triangles=tris,bounds_min=[min(v[i] for v in pts) for i in range(3)],bounds_max=[max(v[i] for v in pts) for i in range(3)])
def export(filename,objects,prefix,species,pose,kind):
    bpy.ops.object.select_all(action='DESELECT');names={o:o.name for o in objects}
    for o in objects:o.name=o.name.removeprefix(prefix);o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets'/filename),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name
    exports[filename]=dict(stats=stats(objects),species=species,pose=pose,kind=kind)
def marker(name,position,parent):
    o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.parent=parent;o.matrix_world=Matrix.Translation(position);return o

for species in SPECIES:
    previous=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(REPO/f'game/assets/characters/{species}/{species}.glb'))
    base=list(set(scene.objects)-previous)
    # Bare head shows the species silhouette. A single previously accepted vest
    # is shared by both sides of the comparison; unused alternatives are omitted.
    omit=('beret_mesh','crestSwept_mesh','goggles_mesh','headset_mesh','helmet_mesh','pack_mesh','vest_mesh','vestPadded_mesh','weapon_mesh')
    for o in list(base):
        if o.type=='MESH' and any(o.name==species+'_'+suffix for suffix in omit):base.remove(o);bpy.data.objects.remove(o,do_unlink=True)
    preserved[species]=[o.name for o in base if o.type=='MESH' and 'Arm_mesh' not in o.name]
    for o in base:o.name='template_'+o.name;o.hide_set(False)
    vest=mesh_object('template_'+species+'_vest_refined_mesh',seed_vest.copy(),bpy.data.objects['template_'+species+'_vest']);vest.matrix_world=vest_matrix
    base.append(vest);bases[species]=base
    material=bpy.data.objects['template_'+species+'_rightArm_mesh'].data.materials[0]
    paw=seed_wing if species=='duck' else paw_data(species)
    for pose,(position,pitch) in POSES.items():
        gun_matrix=Matrix.Translation(position)@Matrix.Rotation(math.radians(pitch),4,'X');grip=gun_matrix@Vector((.035,0,-.0675))
        for kind in ('before','after'):
            prefix=species+'_'+pose+'_'+kind+'_';objects=clone(base,prefix)
            if kind=='before':
                w=bpy.data.objects[prefix+species+'_rightArm_mesh'];shoulder=Vector((.475,-.155,1.06))
                rotation=Vector((0,0,-1)).rotation_difference((grip-shoulder).normalized()).to_matrix().to_4x4()
                w.matrix_world=Matrix.Translation(shoulder)@rotation@Matrix.Translation(-shoulder)@w.matrix_world
            else:
                for side in ('left','right'):
                    old=bpy.data.objects[prefix+species+'_'+side+'Arm_mesh'];parent=old.parent;objects.remove(old);bpy.data.objects.remove(old,do_unlink=True)
                    arm=mesh_object(prefix+species+'_'+side+'Arm_mesh',paw.copy(),parent);arm.data.materials.clear();arm.data.materials.append(material)
                    if side=='left':
                        for v in arm.data.vertices:v.co.x=-v.co.x
                        normals(arm)
                    else:shoulder,wrist=bend(arm,species,grip);w=arm
                    objects.append(arm)
                objects.extend([marker(prefix+'GripShoulder',shoulder,w.parent),marker(prefix+'GripWrist',wrist,w.parent),marker(prefix+'WeaponGripReference',grip,w.parent)])
            old=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(REPO/'game/assets/weapons/m4.glb'));props=list(set(scene.objects)-old)
            for o in props:
                if not o.parent:o.matrix_world=gun_matrix@o.matrix_world
                o.name=prefix+'prop_'+o.name
            bpy.context.view_layer.update();variants[(species,pose,kind)]=(objects,props)
            vest=bpy.data.objects[prefix+species+'_vest_refined_mesh'];pairs={side:len(bvh(vest).overlap(bvh(bpy.data.objects[prefix+species+'_'+side+'Arm_mesh']))) for side in ('left','right')}
            record=dict(vest_intersections=pairs,weapon_matrix=[list(r) for r in gun_matrix],grip=list(grip))
            if kind=='after':
                record.update(shoulder=list(shoulder),wrist=list(wrist))
                export(f'species-grips-{species}-{pose}-character.glb',objects,prefix,species,pose,'character')
                arms=[o for o in objects if o.type=='MESH' and 'Arm_mesh' in o.name];arm_objects=set(arms)
                for o in arms:
                    while o.parent:o=o.parent;arm_objects.add(o)
                arm_objects.update(o for o in objects if o.name.endswith(('GripShoulder','GripWrist','WeaponGripReference')))
                export(f'species-grips-{species}-{pose}-only.glb',list(arm_objects),prefix,species,pose,'only')
                export(f'species-grips-{species}-{pose}-preview.glb',objects+props,prefix,species,pose,'preview')
            records[species+'_'+pose+'_'+kind]=record
            print('POSE',species,pose,kind,json.dumps(pairs),flush=True)
    for o in base:bpy.data.objects.remove(o,do_unlink=True)

camera=scene.camera
def show(key):
    visible=set(sum(variants[key],[]))
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
def view(pos,target,scale):
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
scene.cycles.samples=32;scene.render.resolution_x=720;scene.render.resolution_y=840
views={'quarter':((4,-6,3.1),(.10,-.22,1.1),3.0),'detail':((6,-4,2.7),(.38,-.27,1.08),1.45),
       'side':((7,-.3,2.5),(.08,-.23,1.1),3.0),'top':((4,-6,10),(.08,-.25,1.1),3.0)}
show(('raccoon','aim','after'));view(*views['quarter']);bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/species-grips-study.blend'))
manifest=dict(date='2026-10-23',status='offline species art candidates; game unchanged',protected_files_sha256=hashes,exports=exports,poses=records,preserved_meshes=preserved,
              vest_source='goose-wing-study.blend: after_goose_vest_refined_mesh; identical in both comparison columns',
              baseline='Each species original static arm rigidly rotated toward the same gun pose; not a runtime screenshot',
              styles={'duck':'three rounded feather tips','raccoon':'narrower paw, short rounded toe lobes','redpanda':'13% broader palm, 8% fuller thickness than raccoon'},
              limitations=['Nine static one-handed poses; no runtime animation, transitions, reload or other weapons tested.','Original GLB vertex colors used; Godot procedural skin shader is not evaluated by Blender.'])
(ROOT/'species-grips-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
preview='--preview' in sys.argv
for species in SPECIES:
    for pose in POSES:
        for label,values in views.items():
            if pose!='aim' and label!='quarter':continue
            if preview and (pose!='aim' or label not in ('quarter','detail')):continue
            view(*values)
            for kind in ('before','after'):
                if kind=='before' and (pose!='aim' or label not in ('quarter','detail')):continue
                show((species,pose,kind));scene.render.filepath=str(OUT/f'{species}-{pose}-{kind}-{label}.png');bpy.ops.render.render(write_still=True)
                print('RENDERED',species,pose,kind,label,flush=True)
for path,digest in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==digest,path
print('SPECIES_GRIPS_READY',flush=True)
