"""Independent folded-wing study. Never writes game or previous candidates."""
import bpy, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/wing-study'; OUT.mkdir(parents=True,exist_ok=True)
protected=[p for p in list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend')) if not p.name.startswith('goose-wing-')]
protected += [ROOT.parents[1]/'game/assets/characters/goose/goose.glb',ROOT.parents[1]/'game/assets/weapons/m4.glb']
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-face-accessories.blend'))
scene=bpy.context.scene
for o in list(scene.objects):
    if not o.name.startswith(('refined_','STUDIO_')): bpy.data.objects.remove(o,do_unlink=True)
before=[o for o in scene.objects if not o.name.startswith('STUDIO_')]
for o in before:o.name='before_'+o.name.removeprefix('refined_');o.hide_set(False)
copies={}
for old in before:
    new=old.copy()
    if old.type=='MESH':new.data=old.data.copy()
    scene.collection.objects.link(new);new.name='after_'+old.name.removeprefix('before_');copies[old]=new
for old,new in copies.items():
    new.parent=copies.get(old.parent);new.matrix_parent_inverse=old.matrix_parent_inverse.copy();new.matrix_basis=old.matrix_basis.copy()
bpy.context.view_layer.update()
after=list(copies.values());preserved=[]
for old,new in copies.items():
    if old.type=='MESH' and 'Arm_mesh' not in old.name:
        assert [tuple(v.co) for v in old.data.vertices]==[tuple(v.co) for v in new.data.vertices]
        assert [tuple(p.vertices) for p in old.data.polygons]==[tuple(p.vertices) for p in new.data.polygons]
        assert max(abs(old.matrix_world[r][c]-new.matrix_world[r][c]) for r in range(4) for c in range(4))<1e-6
        preserved.append(new.name.removeprefix('after_'))

# One closed lens-shaped surface per wing: narrow shoulder, flattened folded
# coverts, three rounded feather ends. No disconnected floating feather pieces.
outline=[(-.038,.014),(-.078,.045),(-.105,.115),(-.096,.215),(-.065,.289),
         (-.028,.338),(-.007,.342),(.007,.311),(.033,.365),(.052,.367),
         (.068,.324),(.099,.352),(.119,.339),(.101,.263),(.107,.183),(.079,.080),(.032,.018)]
boundary=[]
for i in range(len(outline)):
    a,b,c,d=[Vector(outline[j%len(outline)]) for j in (i-1,i,i+1,i+2)]
    for k in range(4):
        t=k/4
        boundary.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
N=len(boundary);rings=5;center=Vector((.007,.174))
def wing(side,name):
    old=bpy.data.objects['after_goose_'+name+'_mesh'];parent=old.parent;material=old.data.materials[0]
    after.remove(old);bpy.data.objects.remove(old,do_unlink=True)
    vs=[];fs=[]
    def point(uv,r,front):
        u,v=uv; taper=.62+.38*math.sin(math.pi*min(1,max(0,v/.38)))
        thickness=(.047 if front else -.037)*math.sqrt(max(0,1-r*r))*taper
        fold=.009*math.exp(-((v-(.17+.65*u))/.042)**2)*(1-r*r) if front else 0
        root_blend=.030*math.exp(-(v/.065)**2)
        return (side*(.455+.035*v/.37+thickness+fold-root_blend),-.145-1.15*u-.08*v,1.150-1.13*v)
    for front in (True,False):
        start=len(vs);vs.append(point(center,0,front))
        for j in range(1,rings+1):
            r=j/rings
            for q in boundary:vs.append(point(center+(q-center)*r,r,front))
        for i in range(N):fs.append((start,start+1+i,start+1+(i+1)%N))
        for j in range(rings-1):
            a=start+1+j*N;b=a+N
            for i in range(N):fs.append((a+i,b+i,b+(i+1)%N,a+(i+1)%N))
    # Weld coincident front/back rim vertices, then recalculate actual normals.
    mesh=bpy.data.meshes.new('folded_'+name);mesh.from_pydata(vs,[],fs);mesh.update()
    obj=bpy.data.objects.new('after_goose_'+name+'_mesh',mesh);scene.collection.objects.link(obj)
    obj.parent=parent;obj.matrix_world=Matrix.Identity(4);mesh.materials.append(material)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00001)
    bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    for p in mesh.polygons:p.use_smooth=True
    after.append(obj);return obj
wings=[wing(-1,'leftArm'),wing(1,'rightArm')];bpy.context.view_layer.update()

def stats(objects):
    meshes=[o for o in objects if o.type=='MESH'];pts=[];tris=0
    for o in meshes:
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    return dict(mesh_objects=len(meshes),triangles=tris,bounds_min=[min(v[i] for v in pts) for i in range(3)],bounds_max=[max(v[i] for v in pts) for i in range(3)])
def export(filename,objects):
    names={o:o.name for o in objects};bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.name=o.name.removeprefix('after_');o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ROOT/'assets'/filename),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name
export('goose-wing-character.glb',after)
wing_objects=set(wings)
for o in wings:
    while o.parent:o=o.parent;wing_objects.add(o)
export('goose-wing-only.glb',list(wing_objects))

def bvh(o,transform=Matrix.Identity(4)):
    return BVHTree.FromPolygons([transform@o.matrix_world@v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
vest=bvh(bpy.data.objects['after_goose_vest_refined_mesh']);probes=[]
for pose,pitch,spread in [('idle',0,0),('forward20',20,0),('back15',-15,0),('outward20',0,20),('forward20_out15',20,15)]:
    for side,w in zip((-1,1),wings):
        pivot=w.parent.matrix_world.translation
        transform=Matrix.Translation(pivot)@Matrix.Rotation(math.radians(pitch),4,'X')@Matrix.Rotation(math.radians(-side*spread),4,'Y')@Matrix.Translation(-pivot)
        probes.append(dict(pose=pose,wing=w.name,pairs=len(vest.overlap(bvh(w,transform)))))
manifest=dict(date='2026-10-23',status='offline art candidate; not integrated',protected_files_sha256=hashes,
              preserved_meshes=preserved,before_wings=stats([o for o in before if 'Arm_mesh' in o.name]),wings=stats(wings),character=stats(after),
              before_right_wing=stats([bpy.data.objects['before_goose_rightArm_mesh']]),after_right_wing=stats([wings[1]]),
              wing_probes=probes,axes='Blender Z up/-Y front, GLB Y up/+Z front',
              limitations=['Discrete poses only; no continuous motion or Godot runtime test.','Runtime weapon arms are separately generated; held view is an offline design preview.'])
(ROOT/'wing-study-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('WING_PROBES',json.dumps(probes),flush=True)

camera=scene.camera
def show(objects,props=()):
    visible=set(objects)|set(props)
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
def view(pos,target,scale):
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
scene.cycles.samples=32;scene.render.resolution_x=720;scene.render.resolution_y=840
views={'quarter':((4,-6,3.2),(0,0,1.10),2.90),'detail':((6,-4,2.8),(.20,-.10,.96),1.48),
       'side':((7,0,2.6),(0,0,1.10),2.90),'top':((4,-6,10),(0,0,1.10),3.0)}
show(after);view(*views['quarter']);bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-wing-study.blend'))
preview='--preview' in sys.argv
for label,values in views.items():
    if preview and label not in ('detail','quarter'):continue
    view(*values)
    for kind,objects in [('before',before),('after',after)]:
        show(objects);scene.render.filepath=str(OUT/f'{kind}-{label}.png');bpy.ops.render.render(write_still=True)
        print('RENDERED',kind,label,flush=True)

if not preview:
    # Actual existing weapon asset, read-only; pose is an authored Blender mockup.
    old_objects=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(protected[-1]))
    props=list(set(scene.objects)-old_objects)
    for o in props:
        if not o.parent:o.location+=Vector((.50,-.28,.96))
        o.name='POSE_'+o.name
    bpy.context.view_layer.update()
    for kind,objects in [('before',before),('after',after)]:
        w=next(o for o in objects if o.name==kind+'_goose_rightArm_mesh');rest=w.matrix_world.copy()
        # Align a folded wing's downward axis from shoulder to the trigger hand.
        shoulder=Vector((.475,-.155,1.06));hand=Vector((.535,-.30,.88))
        rotation=Vector((0,0,-1)).rotation_difference((hand-shoulder).normalized()).to_matrix().to_4x4()
        w.matrix_world=Matrix.Translation(shoulder)@rotation@Matrix.Translation(-shoulder)@rest
        bpy.context.view_layer.update();show(objects,props);view((4,-6,3.2),(0,-.15,1.10),3.1)
        scene.render.filepath=str(OUT/f'{kind}-held.png');bpy.ops.render.render(write_still=True)
        w.matrix_world=rest
    for o in props:bpy.data.objects.remove(o,do_unlink=True)
for path,value in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==value,path
print('WING_STUDY_READY',flush=True)
