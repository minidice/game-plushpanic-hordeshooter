"""Face-fitted accessories and a revised aviator cap; isolated art candidates."""
import bpy
import math
import json
import hashlib
import sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/face-accessories';OUT.mkdir(parents=True,exist_ok=True)
protected=[p for p in list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend')) if not p.name.startswith('goose-face-')]
protected.append(ROOT.parents[1]/'game/assets/characters/goose/goose.glb')
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-headwear-collection.blend'))
scene=bpy.context.scene
for o in list(scene.objects):
    if not o.name.startswith(('goggles_','STUDIO_')):bpy.data.objects.remove(o,do_unlink=True)
before=[o for o in scene.objects if not o.name.startswith('STUDIO_')]
for o in before:o.name='base_'+o.name.removeprefix('goggles_')
bare=[o for o in before if o.name!='base_goose_headwear_goggles_mesh']
collections={'before':before,'bare':bare};records={};exports={}
parts=[];parent=None

def mat(name,color,roughness,metal=0,alpha=1,coat=0):
    m=bpy.data.materials.new('face_'+name)
    if not m.use_nodes:m.use_nodes=True
    def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    rgba=tuple(linear(int(color[i:i+2],16)/255) for i in (0,2,4))+(alpha,)
    m.diffuse_color=rgba;p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=rgba;p.inputs['Alpha'].default_value=alpha
    p.inputs['Roughness'].default_value=roughness;p.inputs['Metallic'].default_value=metal
    p.inputs['Coat Weight'].default_value=coat
    return m
M={'frame':mat('thin_frame','363c40',.5,.25),'sun_frame':mat('sun_frame','26303a',.65),
   'clear':mat('clear_lens','e8eef0',.08,alpha=.035,coat=.10),
   'sun':mat('dark_lens','131e28',.23,.1,coat=.25),
   'goggle_lens':mat('goggle_lens','45636a',.20,alpha=.72,coat=.2),
   'rim':mat('goggle_rubber','303a3d',.82),'strap':mat('fabric_strap','3b3934',.95),
   'leather':mat('cap_front','705446',.85),'leather_side':mat('cap_side','654c40',.88),
   'seam':mat('cap_binding','4c3a31',.92),'metal':mat('hardware','7e8585',.4,.55),
   'pad':mat('earpad','282c2c',.95),'shell':mat('headset_shell','56675a',.72),
   'mic':mat('mic_foam','242827',1)}

def mesh(name,vs,fs,material):
    d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update()
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o)
    o.parent=parent;o.matrix_world=Matrix.Identity(4);d.materials.append(material)
    for p in d.polygons:p.use_smooth=True
    parts.append(o);return o
def solid(o,width):
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Material thickness','SOLIDIFY');mod.thickness=width;mod.offset=0
    bpy.ops.object.modifier_apply(modifier=mod.name)
def finish(o,name,material):
    bpy.context.view_layer.update();world=o.matrix_world.copy();o.name=name;o.parent=parent;o.matrix_world=world
    o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=True
    parts.append(o);return o
def ellipsoid(name,pos,scale,material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=pos)
    o=bpy.context.object;o.scale=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,material)
def box(name,pos,size,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=o.modifiers.new('Rounded edge','BEVEL');bevel.width=min(size)*.3;bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    wn=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=wn.name)
    return finish(o,name,material)
def tube(name,points,radius,material):
    # Mesh tube, not an unexported Blender curve.
    vs,fs=[],[];n=10;points=[Vector(p) for p in points]
    for j,p in enumerate(points):
        tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
        helper=Vector((0,0,1)) if abs(tangent.z)<.9 else Vector((0,1,0))
        u=tangent.cross(helper).normalized();v=tangent.cross(u).normalized()
        for i in range(n):vs.append(p+radius*(u*math.cos(i*math.tau/n)+v*math.sin(i*math.tau/n)))
    for j in range(len(points)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    fs.append(tuple(reversed(range(n))));fs.append(tuple((len(points)-1)*n+i for i in range(n)))
    return mesh(name,vs,fs,material)

torso=bpy.data.objects['base_goose_torso_mesh']
body_bvh=BVHTree.FromPolygons([torso.matrix_world@v.co for v in torso.data.vertices],[p.vertices[:] for p in torso.data.polygons])
def body_surface(t,z,clearance=.025):
    direction=Vector((math.sin(t),-math.cos(t),0))
    p,_,_,_=body_bvh.ray_cast(direction*2+Vector((0,0,z)),-direction,2)
    assert p is not None,(t,z)
    return p+direction*clearance

def cap_point(t,phi,offset=0):
    # Taller and slightly narrower than candidate 10, with a defined front panel.
    rx,ry,rz=.445,.400,.45
    p=Vector((rx*math.sin(phi)*math.sin(t),-ry*math.sin(phi)*math.cos(t),1.705+rz*math.cos(phi)))
    n=Vector((p.x/(rx*rx),p.y/(ry*ry),math.cos(phi)/rz)).normalized()
    return p+n*offset
def cap_surface(t,z,clearance=.015):
    phi=math.acos(max(0,min(1,(z-1.705)/.45)))
    return cap_point(t,phi,clearance)

def aviator_cap():
    n,rows=64,16;vs=[(0,0,2.155)];fs=[]
    for j in range(1,rows+1):
        for i in range(n):vs.append(cap_point(-math.pi+i*math.tau/n,j*math.pi/(rows*2)))
    for i in range(n):fs.append((0,1+i,1+(i+1)%n))
    for j in range(rows-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    o=mesh('taller_aviator_crown',vs,fs,M['leather']);o.data.materials.append(M['leather_side'])
    for p in o.data.polygons:
        if p.center.y>0:p.material_index=1
    solid(o,.012)
    ts=[-math.pi+i*math.tau/80 for i in range(81)]
    vs=[];fs=[]
    for t in ts:
        for z in (1.696,1.718):vs.append(cap_surface(t,z,.010))
    # Explicit lower edge avoids clamping two rows onto the same crown edge.
    for i,t in enumerate(ts):vs[2*i].z=1.696;vs[2*i+1].z=1.718
    for i in range(80):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh('cap_bound_edge',vs,fs,M['seam']),.006)
    for sign in (-1,1):
        points=[cap_point(sign*.73,.25+j*1.27/24,.009) for j in range(25)]
        tube('subtle_panel_seam',points,.0025,M['seam'])

def eyewear(kind,raised=False):
    is_goggle=kind=='goggles';is_sun=kind=='sunglasses'
    cx=.18 if raised else .225
    cz=1.84 if raised else 1.535
    halfw=.122 if is_goggle else .112
    halfh=.075 if is_goggle else (.060 if is_sun else .073)
    power=3.4 if is_goggle else (4.4 if is_sun else 2.9)
    border=.011 if is_goggle else .006
    base_y=-.425 if raised else -.414
    curve=.74
    bulge=.003 if is_goggle else .001
    frame=M['rim'] if is_goggle else (M['sun_frame'] if is_sun else M['frame'])
    lens=M['goggle_lens'] if is_goggle else (M['sun'] if is_sun else M['clear'])
    n=64
    def contour(a,rx,rz):
        c,s=math.cos(a),math.sin(a)
        return (math.copysign(abs(c)**(2/power),c)*rx,math.copysign(abs(s)**(2/power),s)*rz)
    def place(side,x,z,depth=0):
        x+=side*cx
        return Vector((x,base_y+curve*x*x-depth,cz+z))
    for side in (-1,1):
        vs=[];fs=[]
        for rx,rz in [(halfw+border,halfh+border),(halfw,halfh)]:
            for i in range(n):
                x,z=contour(i*math.tau/n,rx,rz);vs.append(place(side,x,z,.002))
        for i in range(n):fs.append((i,(i+1)%n,n+(i+1)%n,n+i))
        solid(mesh('thin_contoured_frame',vs,fs,frame),.012 if is_goggle else .007)
        vs=[place(side,0,0,bulge)];fs=[]
        for j in range(1,5):
            q=j/4
            for i in range(n):
                x,z=contour(i*math.tau/n,halfw*q,halfh*q)
                vs.append(place(side,x,z,bulge*(1-q*q)))
        for i in range(n):fs.append((0,1+i,1+(i+1)%n))
        for j in range(3):
            for i in range(n):
                a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
        solid(mesh('shallow_optical_lens',vs,fs,lens),.0015)
        outer=place(side,side*(halfw+border*.5),.015)
        fit_surface=cap_surface if raised else body_surface
        if not is_goggle:
            path=[outer]+[fit_surface(side*(1.05+.85*i/18),cz+.015-.04*i/18,.010) for i in range(19)]
            tube('spectacle_temple',path,.0055,frame)
        else:
            p=fit_surface(side*.94,cz,.024)
            tube('goggle_side_connector',[outer,p],.012,frame)
    gap=cx-halfw
    points=[Vector((x,base_y+curve*x*x-.005,cz+.020+.012*math.sin(math.pi*i/16))) for i in range(17) for x in [-gap+2*gap*i/16]]
    tube('small_nose_bridge',points,.008 if is_goggle else .0045,frame)
    if is_goggle:
        surface=cap_surface if raised else body_surface;vs=[];fs=[]
        for i in range(81):
            t=.91+(math.tau-1.82)*i/80
            for z in (cz-.018,cz+.018):vs.append(surface(t,z,.024))
        for i in range(80):fs.append((2*i,2*i+1,2*i+3,2*i+2))
        solid(mesh('flat_goggle_strap',vs,fs,M['strap']),.004)

def headset():
    for side in (-1,1):
        ellipsoid('ear_cushion',(side*.469,.035,1.56),(.035,.100,.129),M['pad'])
        ellipsoid('earcup_shell',(side*.497,.035,1.56),(.040,.094,.119),M['shell'])
        box('outer_earcup_panel',(side*.532,.035,1.56),(.014,.103,.128),M['shell'])
        pts=[(side*.505,-.055,1.55),(side*.507,-.058,1.69),(side*.467,.03,1.72),(side*.507,.125,1.69),(side*.505,.125,1.55)]
        tube('earcup_yoke',pts,.009,M['frame'])
        box('earcup_control',(side*.539,.032,1.60),(.013,.027,.034),M['metal'])
    vs=[];fs=[]
    for i in range(65):
        a=-math.pi/2+i*math.pi/64
        for y in (-.022,.082):vs.append((.455*math.sin(a),y,1.56+.435*math.cos(a)))
    for i in range(64):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh('headset_headband',vs,fs,M['frame']),.015)
    vs=[];fs=[]
    for i in range(25):
        a=-.55+1.10*i/24
        for y in (-.016,.076):vs.append((.445*math.sin(a),y,1.56+.422*math.cos(a)))
    for i in range(24):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh('headband_cushion',vs,fs,M['pad']),.012)
    tube('microphone_boom',[(.52,-.04,1.50),(.515,-.16,1.45),(.46,-.29,1.41),(.34,-.43,1.385),(.225,-.52,1.385)],.008,M['frame'])
    ellipsoid('microphone_foam',(.225,-.535,1.385),(.032,.044,.024),M['mic'])

def stats(objects):
    ms=[o for o in objects if o.type=='MESH'];pts=[];triangles=0
    for o in ms:
        o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    return dict(mesh_objects=len(ms),triangles=triangles,materials=len({m.name for o in ms for m in o.data.materials}),
                bounds_min=[min(p[i] for p in pts) for i in range(3)],bounds_max=[max(p[i] for p in pts) for i in range(3)])
def ancestors(o):
    result=[o]
    while o.parent:o=o.parent;result.append(o)
    return result
def export(path,objects,prefix):
    names={o:o.name for o in objects};bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.name=o.name.removeprefix(prefix);o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name

KINDS=('glasses','sunglasses','headset','goggles','refined','combo')
for kind in KINDS:
    copies={}
    for old in bare:
        new=old.copy()
        if old.type=='MESH':new.data=old.data.copy()
        scene.collection.objects.link(new);new.name=kind+'_'+old.name.removeprefix('base_');copies[old]=new
    for old,new in copies.items():
        new.parent=copies.get(old.parent);new.matrix_parent_inverse=old.matrix_parent_inverse.copy();new.matrix_basis=old.matrix_basis.copy();new.hide_set(False)
    bpy.context.view_layer.update();objects=list(copies.values());preserved=[]
    for o in objects:
        if o.type!='MESH':continue
        source=bpy.data.objects['base_'+o.name.removeprefix(kind+'_')]
        assert [tuple(v.co) for v in o.data.vertices]==[tuple(v.co) for v in source.data.vertices]
        assert [tuple(p.vertices) for p in o.data.polygons]==[tuple(p.vertices) for p in source.data.polygons]
        assert max(abs(o.matrix_world[r][c]-source.matrix_world[r][c]) for r in range(4) for c in range(4))<1e-6
        preserved.append(o.name.removeprefix(kind+'_'))
    builders=[]
    if kind in ('glasses','sunglasses','goggles'):builders=[(kind,'goose_goggles',lambda:eyewear(kind))]
    if kind=='headset':builders=[('headset','goose_headset',headset)]
    if kind=='refined':builders=[('aviator-cap','goose_helmet',aviator_cap),('raised-goggles','goose_goggles',lambda:eyewear('goggles',True))]
    if kind=='combo':builders=[('sunglasses','goose_goggles',lambda:eyewear('sunglasses')),('headset','goose_headset',headset)]
    equipment=[]
    for label,node,build in builders:
        parent=bpy.data.objects[kind+'_'+node];parts=[];build()
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts:o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();o=bpy.context.object
        o.name=kind+'_goose_face_'+label.replace('-','_')+'_mesh'
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
        objects.append(o);equipment.append(o)
        if kind!='combo':
            name=f'goose-face-{label}.glb';export(ROOT/'assets'/name,ancestors(o),kind+'_')
            exports[name]={'stats':stats([o]),'equipment':{o.name.removeprefix(kind+'_'):node},'kind':kind,'full':False}
    collections[kind]=objects
    records[kind]={'full':stats(objects),'equipment':stats(equipment),'preserved_meshes':preserved}
    name=f'goose-face-{kind}-character.glb';export(ROOT/'assets'/name,objects,kind+'_')
    exports[name]={'stats':stats(objects),'equipment':{o.name.removeprefix(kind+'_'):o.parent.name.removeprefix(kind+'_') for o in equipment},'kind':kind,'full':True}

manifest={'date':'2026-10-23','status':'offline candidate; game unchanged','protected_files_sha256':hashes,
 'variants':records,'exports':exports,
 'scope':'Glasses, sunglasses, headset, face goggles, raised goggles with taller cap, sunglasses+headset combination.',
 'axes':'Blender Z up/-Y front; GLB Y up/+Z front; metres',
 'lens_changes':{'candidate10_bulge_metres':.028,'revised_bulge_metres':.003,'candidate10_frame_outer_width_metres':.79064,'revised_raised_frame_width_metres':.626},
 'limitations':['Static fit only; no Godot motion, body sliders or performance validation.','Transparent lenses use glTF alpha blending; game renderer requires validation.']}
(ROOT/'face-accessories-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

def show(objects):
    visible=set(objects)
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
camera=scene.camera
def view(pos,target,scale):
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
scene.cycles.samples=32;scene.render.resolution_x=720;scene.render.resolution_y=840
show(collections['refined']);view((3,-6,2.9),(0,-.03,1.72),1.50)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-face-accessories.blend'))
views={'detail':((3,-6,2.9),(0,-.03,1.72),1.50),'quarter':((4,-6,3.2),(0,0,1.10),2.90),
       'front':((0,-7,2.4),(0,0,1.10),2.90),'side':((7,0,2.4),(0,0,1.10),2.90),
       'rear':((0,7,2.4),(0,0,1.10),2.90),'top':((4,-6,10),(0,0,1.10),3.0),
       'small':((4,-6,10),(0,0,1.10),10)}
preview='--preview' in sys.argv
for lighting in ('neutral','mood'):
    if preview and lighting=='mood':continue
    if lighting=='mood':
        bg=scene.world.node_tree.nodes['Background'];bg.inputs['Color'].default_value=(.13,.20,.37,1);bg.inputs['Strength'].default_value=.23
        bpy.data.objects['STUDIO_floor'].data.materials[0]=mat('studio_slate','3f4d5b',.84)
        for name,energy,color,size in [('STUDIO_key',780,(1,.86,.67),3),('STUDIO_fill',130,(.40,.60,1),5),('STUDIO_rim',650,(.48,.67,1),3)]:
            o=bpy.data.objects[name];o.data.energy=energy;o.data.color=color;o.data.size=size
    for label,(pos,target,scale) in views.items():
        if preview and label!='detail':continue
        if lighting=='neutral' and label=='small':continue
        if lighting=='mood' and label not in ('quarter','small'):continue
        view(pos,target,scale);scene.render.resolution_y=480 if label=='small' else 840
        for kind,objects in collections.items():
            show(objects);scene.render.filepath=str(OUT/f'{lighting}-{kind}-{label}.png')
            bpy.ops.render.render(write_still=True);print('RENDERED',lighting,kind,label,flush=True)
for p,h in hashes.items():assert hashlib.sha256(Path(p).read_bytes()).hexdigest()==h,p
print('FACE_ACCESSORIES_READY',flush=True)
