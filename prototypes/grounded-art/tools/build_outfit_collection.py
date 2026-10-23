"""Three independent helmet/vest sets, generated only under grounded-art."""
import bpy
import math
import json
import hashlib
import sys
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/outfit-collection'
OUT.mkdir(parents=True,exist_ok=True)
protected=list((ROOT/'assets').glob('goose-*.glb'))+list((ROOT/'source').glob('goose-*.blend'))
protected=[p for p in protected if not p.name.startswith(('goose-light-','goose-recon-','goose-heavy-','goose-outfit-'))]
protected.append(ROOT.parents[1]/'game/assets/characters/goose/goose.glb')
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-refined-armor.blend'))
scene=bpy.context.scene
for ob in list(scene.objects):
    if ob.name.startswith(('before_','reference_')):
        bpy.data.objects.remove(ob,do_unlink=True)
base=[o for o in scene.objects if not o.name.startswith('STUDIO_')]
for ob in base: ob.name='base_'+ob.name

def descendants(ob):
    return [ob]+[c for child in ob.children for c in descendants(child)]

# This is a helmet/vest comparison; hide the same backpack in the reference too.
for ob in list(descendants(bpy.data.objects['base_goose_pack'])):
    if ob.type=='MESH':
        base.remove(ob);bpy.data.objects.remove(ob,do_unlink=True)

def material(name,color,roughness,metallic=0):
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    def linear(v): return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    rgba=tuple(linear(int(color[i:i+2],16)/255) for i in (0,2,4))+(1,)
    mat.diffuse_color=rgba
    shader=mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=rgba
    shader.inputs['Roughness'].default_value=roughness
    shader.inputs['Metallic'].default_value=metallic
    return mat

shared={'rubber':material('collection_rubber','172329',.84),
        'metal':material('collection_hardware','55636a',.40,.55),
        'lens':material('collection_smoked_lens','263f50',.19,.10)}
palette={
    'light':{'shell':('303d43',.85),'cloth':('a0804e',.92),'trim':('6e5536',.94),'accent':('bd9d68',.88)},
    'recon':{'shell':('88734c',.43),'cloth':('415e70',.92),'trim':('293e4c',.94),'accent':('708894',.88)},
    'heavy':{'shell':('435e6c',.44),'cloth':('8e774f',.93),'trim':('3c4039',.92),'accent':('b3a17c',.86)}}
collections={'basic':base}
records={}
parts=[]
parent=None

def mesh(name,vs,fs,mat,smooth=True):
    data=bpy.data.meshes.new(name);data.from_pydata(vs,[],fs);data.update()
    ob=bpy.data.objects.new(name,data);scene.collection.objects.link(ob)
    ob.parent=parent;ob.matrix_world=Matrix.Identity(4);data.materials.append(mat)
    for p in data.polygons:p.use_smooth=smooth
    parts.append(ob)
    return ob

def solid(ob,width):
    bpy.context.view_layer.objects.active=ob
    mod=ob.modifiers.new('Thickness','SOLIDIFY');mod.thickness=width;mod.offset=0
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=ob.modifiers.new('Flat fabric normals','WEIGHTED_NORMAL');mod.keep_sharp=True
    bpy.ops.object.modifier_apply(modifier=mod.name)

def box(name,pos,size,mat,edge=.008,rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    ob=bpy.context.object;ob.name=name;ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.rotation_euler.z=rotation
    world=ob.matrix_world.copy();ob.parent=parent;ob.matrix_world=world
    ob.data.materials.append(mat)
    bevel=ob.modifiers.new('Small edge radius','BEVEL');bevel.width=min(edge,min(size)*.35);bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for p in ob.data.polygons:p.use_smooth=True
    mod=ob.modifiers.new('Weighted normals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(ob);return ob

def tube(name,pos,radius,depth,mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=radius,depth=depth,location=pos,rotation=(math.pi/2,0,0))
    ob=bpy.context.object;ob.name=name
    world=ob.matrix_world.copy();ob.parent=parent;ob.matrix_world=world
    ob.data.materials.append(mat)
    for p in ob.data.polygons:p.use_smooth=len(p.vertices)==4
    parts.append(ob);return ob

def ring(name,rx,ry,z,width,mat):
    vs,fs=[],[]
    for i in range(65):
        t=i*math.tau/64
        for dz in (-width/2,width/2):vs.append((rx*math.sin(t),-ry*math.cos(t),z+dz))
    for i in range(64):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    ob=mesh(name,vs,fs,mat);solid(ob,.007)

def helmet_band(kind,z,width,mat):
    rx,ry=(.455,.397) if kind=='light' else (.468,.414)
    peak=2.035 if kind=='light' else 2.08
    vs,fs=[],[]
    for i in range(65):
        t=i*math.tau/64
        edge=(1.72-.065*max(0,-math.cos(t))) if kind=='light' else (1.70+.085*math.sin(t)**4-.035*max(0,-math.cos(t)))
        for target in (z-width/2,z+width/2):
            lo,hi=0,math.pi/2
            for _ in range(24):
                phi=(lo+hi)/2
                height=1.705+(peak-1.705)*math.cos(phi)+(edge-1.705)*math.sin(phi)**2
                if height>target:lo=phi
                else:hi=phi
            vs.append(((rx*math.sin(phi)+.012)*math.sin(t),-(ry*math.sin(phi)+.012)*math.cos(t),target))
    for i in range(64):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh(kind+'_fitted_goggle_band',vs,fs,mat),.006)

def helmet(kind,M):
    n,rows=48,12
    rx,ry=(.455,.397) if kind!='heavy' else (.468,.414)
    peak={'light':2.035,'recon':2.09,'heavy':2.08}[kind]
    vs=[(0,0,peak)];fs=[]
    for j in range(1,rows+1):
        phi=j/rows*math.pi/2
        for i in range(n):
            t=i*math.tau/n
            edge=1.705
            if kind=='light':edge=1.72-.065*max(0,-math.cos(t))
            if kind=='heavy':edge=1.70+.085*math.sin(t)**4-.035*max(0,-math.cos(t))
            z=1.705+(peak-1.705)*math.cos(phi)+(edge-1.705)*math.sin(phi)**2
            vs.append((rx*math.sin(phi)*math.sin(t),-ry*math.sin(phi)*math.cos(t),z))
    for i in range(n):fs.append((0,1+(i+1)%n,1+i))
    for j in range(rows-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    cap=mesh(kind+'_helmet_crown',vs,fs,M['shell']);solid(cap,.014)
    if kind=='light':
        helmet_band(kind,1.80,.055,shared['rubber'])
        box('light_goggle_frame',(0,-.379,1.835),(.69,.10,.205),shared['rubber'],.040)
        box('light_goggle_lens',(0,-.434,1.838),(.585,.015,.125),shared['lens'],.025)
        for side in (-1,1):
            box('light_goggle_buckle',(side*.345,-.325,1.835),(.06,.06,.095),M['accent'],.01,side*.4)
        # Flat sewn helmet panels, kept broad and restrained.
        for t in (1.1,2.05,4.25,5.2):
            points,faces=[],[]
            for j in range(12):
                phi=.18+j*.105
                for dt in (-.012,.012):
                    edge=1.72-.065*max(0,-math.cos(t+dt))
                    z=1.705+.33*math.cos(phi)+(edge-1.705)*math.sin(phi)**2
                    points.append((.463*math.sin(phi)*math.sin(t+dt),-.405*math.sin(phi)*math.cos(t+dt),z+.004))
            for j in range(11):faces.append((2*j,2*j+1,2*j+3,2*j+2))
            solid(mesh('light_panel_seam',points,faces,shared['rubber']),.002)
    elif kind=='recon':
        ring('recon_thick_rim',.46,.402,1.716,.09,shared['rubber'])
        for side in (-1,1):
            box('recon_strap_catch',(side*.31,-.298,1.752),(.065,.040,.078),shared['metal'],.01,side*.7)
        box('recon_rear_tab',(0,.399,1.76),(.13,.024,.07),shared['rubber'],.008)
    else:
        helmet_band(kind,1.82,.048,shared['rubber'])
        box('heavy_goggle_frame',(0,-.393,1.795),(.65,.10,.19),shared['rubber'],.035)
        box('heavy_goggle_lens',(0,-.448,1.80),(.55,.014,.105),shared['lens'],.020)
        box('heavy_optic_mount',(0,-.359,1.976),(.16,.075,.12),shared['rubber'],.012)
        box('heavy_optic_body',(0,-.414,1.996),(.135,.11,.10),shared['metal'],.016)
        tube('heavy_optic_rim',(0,-.48,1.996),.038,.025,shared['rubber'])
        tube('heavy_optic_glass',(0,-.495,1.996),.028,.008,shared['lens'])
        box('heavy_counterweight',(0,.397,1.83),(.22,.07,.105),shared['rubber'],.012)
        for side in (-1,1):
            box('heavy_rail',(side*.454,.01,1.85),(.028,.19,.055),shared['metal'],.008)
            for y in (-.045,.065):box('heavy_rail_stop',(side*.472,y,1.85),(.012,.022,.062),shared['rubber'],.003)

# Common fitting envelope from the validated basic vest; broad manufactured panels.
fit=json.loads((ROOT/'refined-armor-manifest.json').read_text())['fit_allowance_metres']
torso=bpy.data.objects['base_goose_torso_mesh']
body_surface_bvh=BVHTree.FromPolygons([torso.matrix_world@v.co for v in torso.data.vertices],[p.vertices[:] for p in torso.data.polygons])
active_kind=None
def surface(t,z,extra=0):
    if active_kind=='light':
        direction=Vector((math.sin(t),-math.cos(t),0))
        hit,_,_,_=body_surface_bvh.ray_cast(direction*2+Vector((0,0,z)),-direction,2)
        if hit is not None:return hit+direction*(.023+extra)
    rx=.476-.025*max(0,(z-1)/.35);ry=.423-.020*max(0,(z-1)/.35)
    r=(abs(math.sin(t)/rx)**2.8+abs(math.cos(t)/ry)**2.8)**(-1/2.8)+fit+extra
    return Vector((r*math.sin(t),-r*math.cos(t),z))

def ribbon(name,angles,heights,width,mat,extra=.01):
    vs,fs=[],[]
    for t,z in zip(angles,heights):
        for dz in (-width/2,width/2):vs.append(surface(t,z+dz,extra))
    for i in range(len(angles)-1):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    ob=mesh(name,vs,fs,mat);solid(ob,.005)

def patch(name,t0,t1,z0,z1,mat,extra=.01):
    vs,fs=[],[]
    for j in range(5):
        for i in range(17):vs.append(surface(t0+(t1-t0)*i/16,z0+(z1-z0)*j/4,extra))
    for j in range(4):
        for i in range(16):
            a=j*17+i;fs.append((a,a+1,a+18,a+17))
    ob=mesh(name,vs,fs,mat);solid(ob,.012)

def fitted_vest(kind,M):
    def hole(t):
        d=(abs(t)-1.18)/.56
        h=0 if abs(d)>=1-1e-8 else (1-d*d)**.70
        tilt=.025*max(-1,min(1,d))
        return .925-.24*h+tilt,.925+.27*h+tilt
    def neck(t):
        # Recon: deeper front opening. Heavy: higher rear collar and substantial shoulder.
        if kind=='recon':return 1.15+.15*math.sin(abs(t))+.08*abs(t)/math.pi
        return 1.26+.11*math.sin(abs(t))+.07*abs(t)/math.pi
    angles=sorted(set([-math.pi+i*math.tau/80 for i in range(80)]+[s*(1.18+d) for s in (-1,1) for d in (-.56,-.555,-.54,-.51,.51,.54,.555,.56)]))
    n=len(angles);vs,fs=[],[]
    for region in (0,1):
        start=len(vs)
        for j in range(5):
            for t in angles:
                low,high=hole(t)
                a,b=(.63,low) if region==0 else (high,neck(t))
                vs.append(surface(t,a+(b-a)*j/4,.004 if kind=='heavy' else 0))
        for j in range(4):
            for i in range(n):
                a=start+j*n+i;b=start+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    shell=mesh(kind+'_vest_shell',vs,fs,M['cloth'])
    bpy.ops.object.select_all(action='DESELECT');shell.select_set(True);bpy.context.view_layer.objects.active=shell
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    solid(shell,.016 if kind=='heavy' else .012)
    ring_angles=[-math.pi+i*math.tau/80 for i in range(81)]
    ribbon(kind+'_neck_edge',ring_angles,[neck(t)-.008 for t in ring_angles],.018,M['trim'],.014)
    ribbon(kind+'_hem',ring_angles,[.64]*len(ring_angles),.020,M['trim'],.014)
    if kind=='recon':
        # One broad map pouch creates a compact, distinct chest silhouette.
        p=surface(0,.83,.075)
        box('recon_map_pouch',p,(.37,.105,.235),M['cloth'],.025)
        box('recon_map_flap',p+Vector((0,-.056,.087)),(.37,.018,.085),M['trim'],.007)
        box('recon_map_latch',p+Vector((0,-.070,.053)),(.045,.012,.069),shared['rubber'],.004)
        for side in (-1,1):
            ts=[side*(1.82+.85*i/18) for i in range(19)]
            ribbon('recon_side_belt',ts,[.80]*len(ts),.075,M['trim'],.018)
            ribbon('recon_shoulder_tab',[side*(.57+.16*i/6) for i in range(7)],[1.225]*7,.058,M['accent'],.018)
    else:
        patch('heavy_chest_plate',-.52,.52,1.02,1.245,M['trim'],.018)
        # Raised rear/side collar ends before the beak and face.
        ts=[1.78+(math.tau-3.56)*i/40 for i in range(41)]
        ribbon('heavy_collar',ts,[1.37]*len(ts),.082,M['trim'],.012)
        for side in (-1,1):
            p=surface(side*.25,.83,.075)
            box('heavy_utility_pouch',p,(.18,.105,.235),M['cloth'],.018,side*.16)
            box('heavy_flap',p+Vector((0,-.057,.086)),(.18,.018,.075),M['accent'],.006,side*.16)
            box('heavy_pull',p+Vector((0,-.070,.051)),(.027,.012,.060),M['trim'],.004)
            ts=[side*(1.80+.95*i/20) for i in range(21)]
            ribbon('heavy_side_panel',ts,[.86]*len(ts),.25,M['trim'],.018)
            for z in (.79,.92):ribbon('heavy_side_webbing',ts,[z]*len(ts),.035,M['accent'],.025)
        box('heavy_chest_badge',surface(0,1.17,.032),(.14,.012,.040),M['accent'],.003)

def light_vest(M):
    patch('light_chest_rig',-.61,.61,.66,1.06,M['cloth'],.005)
    patch('light_back_anchor',2.64,3.64,.70,1.14,M['trim'],.004)
    ts=[-math.pi+i*math.tau/64 for i in range(65)]
    ribbon('light_waist_belt',ts,[.66]*len(ts),.045,M['trim'],.005)
    for side in (-1,1):
        ts=[side*(.51+2.12*i/32) for i in range(33)]
        zs=[1.065+.18*math.sin(math.pi*i/32) for i in range(33)]
        ribbon('light_shoulder_harness',ts,zs,.060,M['cloth'],.014)
        p=surface(side*.47,1.065,.025)
        box('light_harness_buckle',p,(.062,.025,.066),M['trim'],.005,side*.3)
    for t in (-.35,0,.35):
        p=surface(t,.83,.060)
        box('light_magazine_pouch',p,(.145,.085,.235),M['cloth'],.012,t*.75)
        box('light_pouch_flap',p+Vector((0,-.046,.084)),(.145,.015,.07),M['accent'],.005,t*.75)
        box('light_pouch_pull',p+Vector((0,-.057,.049)),(.025,.009,.047),M['trim'],.003)

def stats(objects):
    ms=[o for o in objects if o.type=='MESH']
    for o in ms:o.data.calc_loop_triangles()
    pts=[o.matrix_world@v.co for o in ms for v in o.data.vertices]
    return dict(mesh_objects=len(ms),triangles=sum(len(o.data.loop_triangles) for o in ms),
                materials=len({m.name for o in ms for m in o.data.materials}),
                bounds_min=[min(p[i] for p in pts) for i in range(3)],bounds_max=[max(p[i] for p in pts) for i in range(3)])

def ancestors(ob):
    result=[ob];p=ob.parent
    while p:result.append(p);p=p.parent
    return result

def export(path,objects,prefix):
    names={o:o.name for o in objects}
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.name=o.name.removeprefix(prefix);o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name

for kind in ('light','recon','heavy'):
    active_kind=kind
    copies={}
    for old in base:
        new=old.copy()
        if old.type=='MESH':new.data=old.data.copy()
        scene.collection.objects.link(new);new.name=kind+'_'+old.name.removeprefix('base_');copies[old]=new
    for old,new in copies.items():
        new.parent=copies.get(old.parent);new.matrix_parent_inverse=old.matrix_parent_inverse.copy();new.matrix_basis=old.matrix_basis.copy();new.hide_set(False)
    bpy.context.view_layer.update()
    objects=list(copies.values())
    for group in ('goose_helmet','goose_vest'):
        for ob in list(descendants(bpy.data.objects[kind+'_'+group])):
            if ob.type=='MESH':objects.remove(ob);bpy.data.objects.remove(ob,do_unlink=True)
    # Exact local geometry and tolerant evaluated world transforms for the 11 body parts.
    preserved=[]
    for ob in objects:
        if ob.type!='MESH':continue
        source=bpy.data.objects['base_'+ob.name.removeprefix(kind+'_')]
        assert [tuple(v.co) for v in ob.data.vertices]==[tuple(v.co) for v in source.data.vertices]
        assert [tuple(p.vertices) for p in ob.data.polygons]==[tuple(p.vertices) for p in source.data.polygons]
        assert max(abs(ob.matrix_world[r][c]-source.matrix_world[r][c]) for r in range(4) for c in range(4))<1e-6
        preserved.append(ob.name.removeprefix(kind+'_'))
    M={k:material(kind+'_'+k,c,r) for k,(c,r) in palette[kind].items()}
    items={}
    for slot,build in [('helmet',lambda:helmet(kind,M)),('vest',lambda:light_vest(M) if kind=='light' else fitted_vest(kind,M))]:
        parent=bpy.data.objects[kind+'_goose_'+slot];parts=[];build()
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
        ob=bpy.context.object;ob.name=kind+'_goose_'+slot+'_'+kind+'_mesh'
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
        objects.append(ob);items[slot]=ob
    collections[kind]=objects
    records[kind]={'full':stats(objects),'helmet':stats([items['helmet']]),'vest':stats([items['vest']]),'preserved_body_meshes':preserved}
    export(ROOT/f'assets/goose-{kind}-outfit.glb',objects,kind+'_')
    for slot,ob in items.items():export(ROOT/f'assets/goose-{kind}-{slot}.glb',ancestors(ob),kind+'_')

manifest={'date':'2026-10-23','status':'offline review collection; game not integrated',
          'protected_files_sha256':hashes,'outfits':records,
          'axes':'Blender Z up/-Y front; glTF Y up/+Z front; metres',
          'scope':'Three new helmet/vest sets. No backpacks, including in the basic reference render.',
          'limitations':['No runtime weapon-arm, body-slider or performance validation.','Only existing goose body; other species require fitting.']}
(ROOT/'outfit-collection-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

def show(objects):
    visible=set(objects)
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
camera=scene.camera
def view(pos,scale):
    camera.location=pos;camera.data.ortho_scale=scale
    camera.rotation_euler=(Vector((0,0,1.02))-camera.location).to_track_quat('-Z','Y').to_euler()
scene.cycles.samples=24;scene.render.resolution_x=720;scene.render.resolution_y=840
show(collections['light']);view((4,-6,3.2),2.75)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-outfit-collection.blend'))
light_settings={o.name:(o.data.energy,tuple(o.data.color),o.data.size) for o in scene.objects if o.type=='LIGHT'}
bg=scene.world.node_tree.nodes['Background'];neutral=(tuple(bg.inputs['Color'].default_value),bg.inputs['Strength'].default_value)
floor=bpy.data.objects['STUDIO_floor'];floor_mat=floor.data.materials[0]
mood_floor=material('STUDIO_collection_slate','3f4d5b',.84)
views={'quarter':((4,-6,3.2),2.75),'front':((0,-7,2.4),2.70),'side':((7,0,2.4),2.70),
       'rear':((0,7,2.4),2.70),'top':((4,-6,11.3),2.95),'small':((4,-6,11.3),10)}
preview='--preview' in sys.argv
for lighting in ('neutral','mood'):
    if preview and lighting=='mood':continue
    if lighting=='mood':
        bg.inputs['Color'].default_value=(.13,.20,.37,1);bg.inputs['Strength'].default_value=.23
        floor.data.materials[0]=mood_floor
        for name,energy,color,size in [('STUDIO_key',780,(1,.86,.67),3),('STUDIO_fill',130,(.40,.60,1),5),('STUDIO_rim',650,(.48,.67,1),3)]:
            o=bpy.data.objects[name];o.data.energy=energy;o.data.color=color;o.data.size=size
    for label,(pos,scale) in views.items():
        if preview and label!='quarter':continue
        if lighting=='neutral' and label=='small':continue
        if lighting=='mood' and label not in ('quarter','top','small'):continue
        view(pos,scale);scene.render.resolution_y=480 if label=='small' else 840
        for kind,objects in collections.items():
            if preview and kind=='basic':continue
            show(objects);scene.render.filepath=str(OUT/f'{lighting}-{kind}-{label}.png')
            bpy.ops.render.render(write_still=True);print('RENDERED',lighting,kind,label,flush=True)
for path,h in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==h
print('OUTFIT_COLLECTION_READY',json.dumps(records),flush=True)
