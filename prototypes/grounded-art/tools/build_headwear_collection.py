"""Seven silhouette-led headwear candidates. All writes stay in grounded-art."""
import bpy
import math
import json
import hashlib
import sys
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/headwear-collection'
OUT.mkdir(parents=True,exist_ok=True)
protected=[p for p in list((ROOT/'assets').glob('*.glb'))+list((ROOT/'source').glob('*.blend')) if not p.name.startswith('goose-headwear-')]
protected.append(ROOT.parents[1]/'game/assets/characters/goose/goose.glb')
hashes={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in protected}
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/goose-outfit-collection.blend'))
scene=bpy.context.scene
old=bpy.data.objects['light_goose_helmet_light_mesh']
old_data=old.data.copy();old_world=old.matrix_world.copy()
for o in list(scene.objects):
    if not o.name.startswith(('base_','STUDIO_')):bpy.data.objects.remove(o,do_unlink=True)
base=[o for o in scene.objects if o.name.startswith('base_')]
for o in list(base):
    if o.type=='MESH' and 'helmet' in o.name:base.remove(o);bpy.data.objects.remove(o,do_unlink=True)
old=bpy.data.objects.new('base_goose_old_goggles_mesh',old_data);scene.collection.objects.link(old)
old.parent=bpy.data.objects['base_goose_helmet'];old.matrix_world=old_world;base.append(old)
collections={'before':base};records={}
parts=[];parent=None

def mat(name,color,roughness,metal=0,coat=0):
    m=bpy.data.materials.new('headwear_'+name)
    if not m.use_nodes:m.use_nodes=True
    def linear(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
    c=tuple(linear(int(color[i:i+2],16)/255) for i in (0,2,4))+(1,)
    m.diffuse_color=c;p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=c;p.inputs['Roughness'].default_value=roughness
    p.inputs['Metallic'].default_value=metal;p.inputs['Coat Weight'].default_value=coat
    return m
M={
 'rubber':mat('rubber','20282b',.8),'metal':mat('steel','75818a',.33,.65),
 'lens':mat('blue_glass','2d5969',.11,.22,.5),'nvg_lens':mat('optic_glass','266a5b',.09,.3,.6),
 'visor':mat('smoked_visor','354a55',.14,.12,.5),
 'olive':mat('paint_olive','50644b',.46,0,.12),'ivory':mat('ivory','d6cfac',.65),
 'navy':mat('cap_canvas','314c61',.91),'tan':mat('cap_patch','b79b68',.9),
 'leather':mat('aviator_leather','614637',.82),'leather_edge':mat('leather_edge','362c25',.9),
 'yellow':mat('motorcycle_yellow','c39c39',.3,0,.35),'black_paint':mat('black_paint','253039',.39,0,.25),
 'tactical':mat('tactical_paint','4b5d68',.47),'pink':mat('bunny_canvas','a77582',.92),
 'ear':mat('bunny_cream','d4c9b7',.93),'ear_inner':mat('ear_inner','b37d80',.96),
 'sage':mat('bucket_canvas','829184',.94),'rose':mat('hat_band','a46966',.9),
 'petal':mat('flower_petals','e7d9b6',.85),'pollen':mat('flower_center','c4a04b',.88),
 'leaf':mat('flower_leaf','506d50',.93)}

def mesh(name,vs,fs,material):
    d=bpy.data.meshes.new(name);d.from_pydata(vs,[],fs);d.update()
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o)
    o.parent=parent;o.matrix_world=Matrix.Identity(4);d.materials.append(material)
    for p in d.polygons:p.use_smooth=True
    parts.append(o);return o

def solid(o,width):
    bpy.context.view_layer.objects.active=o
    mod=o.modifiers.new('Cloth or shell thickness','SOLIDIFY');mod.thickness=width;mod.offset=0
    bpy.ops.object.modifier_apply(modifier=mod.name)

def finish_primitive(o,name,material):
    bpy.context.view_layer.update()
    o.name=name;world=o.matrix_world.copy();o.parent=parent;o.matrix_world=world
    o.data.materials.append(material)
    for p in o.data.polygons:p.use_smooth=True
    parts.append(o);return o

def box(name,pos,size,material,edge=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=o.modifiers.new('Rounded corners','BEVEL');mod.width=min(edge,min(size)*.4);mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish_primitive(o,name,material)

def ellipsoid(name,pos,scale,material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,location=pos)
    o=bpy.context.object;o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish_primitive(o,name,material)

def cylinder(name,pos,radius,depth,material,axis=(0,-1,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=radius,depth=depth,location=pos)
    o=bpy.context.object;o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    finish_primitive(o,name,material)
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    bevel=o.modifiers.new('Rim bevel','BEVEL');bevel.width=min(.006,depth*.18);bevel.segments=2
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=bevel.name)
    return o

def edge_for(kind,t):
    if kind=='motorcycle':return 1.705
    if kind=='goggles':return 1.72-.10*math.sin(t)**4-.06*max(0,-math.cos(t))
    if kind=='nvg':return 1.72+.065*math.sin(t)**4-.045*max(0,-math.cos(t))
    return {'stripe':1.705,'cap':1.73,'bunny':1.71,'flower':1.74}[kind]

DIMS={'stripe':(.47,.415,2.045),'cap':(.46,.40,2.015),'goggles':(.465,.414,2.055),
      'motorcycle':(.49,.437,2.10),'nvg':(.475,.423,2.07),'bunny':(.463,.405,2.045),'flower':(.458,.405,2.04)}

def cap_point(kind,t,phi,offset=0):
    rx,ry,peak=DIMS[kind];edge=edge_for(kind,t)
    z=1.705+(peak-1.705)*math.cos(phi)+(edge-1.705)*math.sin(phi)**2
    return Vector(((rx*math.sin(phi)+offset)*math.sin(t),-(ry*math.sin(phi)+offset)*math.cos(t),z+offset*.6))

def crown(kind,material):
    vs=[(0,0,DIMS[kind][2])];fs=[];n=64;rows=14
    for j in range(1,rows+1):
        for i in range(n):vs.append(cap_point(kind,-math.pi+i*math.tau/n,j*math.pi/(2*rows)))
    for i in range(n):fs.append((0,1+i,1+(i+1)%n))
    for j in range(rows-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    o=mesh(kind+'_shell',vs,fs,material);solid(o,.018 if kind in ('stripe','motorcycle','nvg') else .012)

def brim(kind,material,extent=.06,front_only=False):
    vs,fs=[],[];n=64;rows=5
    for j in range(rows+1):
        q=j/rows
        for i in range(n+1):
            t=(-1.12+2.24*i/n) if front_only else (-math.pi+math.tau*i/n)
            p=cap_point(kind,t,math.pi/2)
            extension=extent*(max(0,math.cos(t/1.16*math.pi/2))**.55 if front_only else (1+.18*math.cos(t)))
            p.x+=q*extension*math.sin(t);p.y-=q*extension*math.cos(t)
            p.z-=q*(.045 if kind in ('cap','flower') else .012)
            if kind=='cap':p.z+=.025*q*math.sin(t)**2
            vs.append(p)
    for j in range(rows):
        for i in range(n):
            a=j*(n+1)+i;fs.append((a,a+1,a+n+2,a+n+1))
    solid(mesh(kind+'_brim',vs,fs,material),.018 if kind!='flower' else .011)

def rim(kind,material,width=.026):
    vs,fs=[],[]
    for i in range(81):
        t=-math.pi+i*math.tau/80;p=cap_point(kind,t,math.pi/2,.009)
        for dz in (-width/2,width/2):vs.append(p+Vector((0,0,dz)))
    for i in range(80):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh(kind+'_rim',vs,fs,material),.009)

def center_stripe(kind,material,width=.11):
    # A continuous front-to-back ribbon follows the crown, not a floating block.
    rx,ry,peak=DIMS[kind];vs,fs=[],[];columns=8
    for i in range(65):
        limit=math.pi/2-.06
        s=-limit+i*(2*limit)/64
        for j in range(columns+1):
            x=-width/2+width*j/columns
            y=ry*math.sin(s)*math.sqrt(1-(x/rx)**2)
            radius=math.sqrt((x/rx)**2+(y/ry)**2)
            t=math.atan2(x,-y);phi=math.asin(min(1,radius))
            p=cap_point(kind,t,phi)
            normal=Vector((p.x/(rx*rx),p.y/(ry*ry),math.cos(phi)/(peak-1.705))).normalized()
            vs.append(p+normal*.012)
    for i in range(64):
        for j in range(columns):
            a=i*(columns+1)+j;fs.append((a,a+1,a+columns+2,a+columns+1))
    solid(mesh(kind+'_center_stripe',vs,fs,material),.003)

def forehead_band(kind,z,width,material):
    vs,fs=[],[]
    for i in range(81):
        t=-math.pi+i*math.tau/80
        for target in (z-width/2,z+width/2):
            lo,hi=0,math.pi/2
            for _ in range(24):
                phi=(lo+hi)/2
                if cap_point(kind,t,phi).z>target:lo=phi
                else:hi=phi
            p=cap_point(kind,t,phi,.014);p.z=target;vs.append(p)
    for i in range(80):fs.append((2*i,2*i+1,2*i+3,2*i+2))
    solid(mesh(kind+'_strap',vs,fs,material),.008)

def goggles(kind):
    forehead_band(kind,1.80,.065,M['leather_edge'])
    # Two oval, convex lenses and recessed inner frames; no rectangular plates.
    for side in (-1,1):
        cx=side*.19;cz=1.81;n=48
        def point(a,r,bulge=0):
            x=cx+.174*r*math.cos(a);z=cz+.116*r*math.sin(a)
            return (x,-.487+.66*x*x-bulge,z)
        vs=[];fs=[]
        for radius in (1.18,1.04,1.0):
            for i in range(n):vs.append(point(i*math.tau/n,radius,.005 if radius==1.04 else 0))
        for j in range(2):
            for i in range(n):fs.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        solid(mesh('goggle_rounded_frame',vs,fs,M['rubber']),.026)
        vs=[point(0,0,.028)];fs=[]
        for j in range(1,6):
            r=j/5
            for i in range(n):vs.append(point(i*math.tau/n,r,.028*(1-r*r)))
        for i in range(n):fs.append((0,1+i,1+(i+1)%n))
        for j in range(4):
            for i in range(n):
                a=1+j*n+i;b=1+j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
        solid(mesh('goggle_convex_lens',vs,fs,M['lens']),.004)
        cylinder('goggle_side_hinge',(side*.395,-.355,1.81),.031,.027,M['metal'],(side,0,0))
    ellipsoid('goggle_nose_bridge',(0,-.478,1.81),(.045,.03,.031),M['leather_edge'])

def ear(side,floppy,material,inner=False):
    # Hollow-looking fabric ears with flattened oval section and an asymmetric bend.
    vs,fs=[],[];rows=24;n=20
    for j in range(rows+1):
        u=j/rows
        x=side*(.21+.09*u+(.22*max(0,(u-.60)/.40)**2 if floppy else .03*u))
        z=1.995+.69*u-(.30*max(0,(u-.62)/.38)**2 if floppy else 0)
        y=.01+(.05*u if floppy else .025*u)
        radius=max(.007,.10*math.sin(math.pi*(.12+.88*u))**.68)
        if inner:
            # Front inset is a separate narrower ribbon following the ear curve.
            for q in (-1,1):vs.append((x+q*radius*.58,y-.051,z))
        else:
            for i in range(n):
                a=i*math.tau/n;vs.append((x+radius*math.cos(a),y+.047*math.sin(a),z))
    if inner:
        for j in range(rows):fs.append((2*j,2*j+1,2*j+3,2*j+2))
        solid(mesh('bunny_inner_ear',vs,fs,material),.004)
    else:
        for j in range(rows):
            for i in range(n):
                a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
        fs.append(tuple(reversed(range(n))));fs.append(tuple(rows*n+i for i in range(n)))
        mesh('bunny_fabric_ear',vs,fs,material)

def build(kind):
    primary={'stripe':'olive','cap':'navy','goggles':'leather','motorcycle':'yellow','nvg':'tactical','bunny':'pink','flower':'sage'}[kind]
    crown(kind,M[primary])
    if kind=='stripe':
        brim(kind,M['olive'],.065);rim(kind,M['rubber'],.021);center_stripe(kind,M['ivory'],.13)
        for side in (-1,1):cylinder('helmet_side_rivet',(side*.476,0,1.77),.022,.018,M['metal'],(side,0,0))
    elif kind=='cap':
        brim(kind,M['navy'],.24,True);rim(kind,M['navy'],.025)
        ellipsoid('cap_top_button',(0,0,2.026),(.035,.035,.018),M['navy'])
        # Shallow curved front label, without rigid rectangular goggles.
        vs=[];fs=[]
        for j in range(5):
            for i in range(17):vs.append(cap_point(kind,-.23+.46*i/16,.78+.28*j/4,.012))
        for j in range(4):
            for i in range(16):
                a=j*17+i;fs.append((a,a+1,a+18,a+17))
        solid(mesh('cap_front_patch',vs,fs,M['tan']),.003)
    elif kind=='goggles':
        brim(kind,M['leather_edge'],.035,True);rim(kind,M['leather_edge'],.035);goggles(kind)
    elif kind=='motorcycle':
        rim(kind,M['rubber'],.034);center_stripe(kind,M['black_paint'],.14)
        # Rounded crown above a separate side/back shell, keeping a wide open face.
        vs,fs=[],[];edge_points=[]
        for i in range(65):
            t=.87+(math.tau-1.74)*i/64
            blend=max(0,1-(t-.87)/.30)+max(0,1-(math.tau-.87-t)/.30)
            low=1.45+.24*blend
            for z in (low,1.71):vs.append((.491*math.sin(t),-.438*math.cos(t),z))
            edge_points.append(Vector((.496*math.sin(t),-.443*math.cos(t),low)))
        for i in range(64):fs.append((2*i,2*i+1,2*i+3,2*i+2))
        solid(mesh('motorcycle_side_and_back_shell',vs,fs,M['yellow']),.020)
        vs,fs=[],[]
        for p in edge_points:
            for dz in (-.014,.014):vs.append(p+Vector((0,0,dz)))
        for i in range(64):fs.append((2*i,2*i+1,2*i+3,2*i+2))
        solid(mesh('motorcycle_lower_padding',vs,fs,M['rubber']),.015)
        # Lifted, wraparound smoked visor: eyes and beak remain fully visible.
        vs=[];fs=[]
        for j in range(7):
            u=j/6
            for i in range(49):
                half=1.02 if j in (0,6) else (1.085 if j in (1,5) else 1.10)
                t=-half+2*half*i/48
                vs.append((.505*math.sin(t),-.48*math.cos(t),1.71+.20*u+.045*abs(math.sin(t))))
        for j in range(6):
            for i in range(48):
                a=j*49+i;fs.append((a,a+1,a+50,a+49))
        solid(mesh('motorcycle_curved_visor',vs,fs,M['visor']),.007)
        for side in (-1,1):
            cylinder('motorcycle_visor_pivot',(side*.464,-.12,1.76),.054,.032,M['rubber'],(side,0,0))
            cylinder('motorcycle_pivot_cap',(side*.485,-.12,1.76),.027,.012,M['metal'],(side,0,0))
    elif kind=='nvg':
        brim(kind,M['tactical'],.045,True);rim(kind,M['rubber'],.026)
        center_stripe(kind,M['rubber'],.055)
        box('nvg_forehead_mount',(0,-.403,1.88),(.13,.07,.16),M['rubber'])
        box('nvg_hinge_arm',(0,-.485,2.00),(.07,.21,.06),M['metal'])
        cylinder('nvg_hinge',(0,-.40,2.00),.045,.16,M['rubber'],(1,0,0))
        box('nvg_bridge',(0,-.55,2.035),(.32,.065,.06),M['tactical'])
        for side in (-1,1):
            cylinder('nvg_optical_barrel',(side*.145,-.56,2.055),.083,.24,M['tactical'])
            cylinder('nvg_objective_ring',(side*.145,-.69,2.055),.093,.036,M['rubber'])
            ellipsoid('nvg_objective_glass',(side*.145,-.709,2.055),(.069,.016,.069),M['nvg_lens'])
            cylinder('nvg_rear_ocular',(side*.145,-.43,2.055),.065,.04,M['rubber'])
            box('helmet_side_rail',(side*.463,.02,1.81),(.035,.23,.042),M['rubber'])
        box('helmet_rear_battery',(0,.415,1.80),(.23,.075,.12),M['rubber'])
    elif kind=='bunny':
        brim(kind,M['pink'],.12,True);rim(kind,M['pink'],.034)
        ear(-1,False,M['ear']);ear(1,True,M['ear'])
        ear(-1,False,M['ear_inner'],True);ear(1,True,M['ear_inner'],True)
    else:
        brim(kind,M['sage'],.145);rim(kind,M['rose'],.07)
        # One large side flower is readable at gameplay scale.
        center=Vector((.325,-.326,1.87))
        for i in range(7):
            a=i*math.tau/7
            p=center+Vector((.107*math.cos(a),-.01,.107*math.sin(a)))
            o=ellipsoid('flower_petal',p,(.072,.027,.046),M['petal']);o.rotation_euler.y=-a
        ellipsoid('flower_center',center+Vector((0,-.045,0)),(.064,.032,.064),M['pollen'])
        for x,z,angle in [(.425,1.72,-.65),(.21,1.77,.55)]:
            o=ellipsoid('flower_leaf',(x,-.31,z),(.085,.019,.034),M['leaf']);o.rotation_euler.y=angle

def stats(objects):
    ms=[o for o in objects if o.type=='MESH'];pts=[];tris=0
    for o in ms:
        o.data.calc_loop_triangles();tris+=len(o.data.loop_triangles);pts.extend(o.matrix_world@v.co for v in o.data.vertices)
    return dict(mesh_objects=len(ms),triangles=tris,materials=len({m.name for o in ms for m in o.data.materials}),
                bounds_min=[min(p[i] for p in pts) for i in range(3)],bounds_max=[max(p[i] for p in pts) for i in range(3)])

def ancestors(o):
    obs=[o]
    while o.parent:o=o.parent;obs.append(o)
    return obs

def export(path,objects,prefix):
    names={o:o.name for o in objects};bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.name=o.name.removeprefix(prefix);o.hide_set(False);o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_yup=True,export_cameras=False,export_lights=False)
    for o,name in names.items():o.name=name

KINDS=('stripe','cap','goggles','motorcycle','nvg','bunny','flower')
for kind in KINDS:
    copies={}
    for old in base:
        new=old.copy()
        if old.type=='MESH':new.data=old.data.copy()
        scene.collection.objects.link(new);new.name=kind+'_'+old.name.removeprefix('base_');copies[old]=new
    for old,new in copies.items():
        new.parent=copies.get(old.parent);new.matrix_parent_inverse=old.matrix_parent_inverse.copy();new.matrix_basis=old.matrix_basis.copy();new.hide_set(False)
    bpy.context.view_layer.update();objects=list(copies.values())
    old=bpy.data.objects[kind+'_goose_old_goggles_mesh'];objects.remove(old);bpy.data.objects.remove(old,do_unlink=True)
    preserved=[]
    for o in objects:
        if o.type!='MESH':continue
        source=bpy.data.objects['base_'+o.name.removeprefix(kind+'_')]
        assert [tuple(v.co) for v in o.data.vertices]==[tuple(v.co) for v in source.data.vertices]
        assert [tuple(p.vertices) for p in o.data.polygons]==[tuple(p.vertices) for p in source.data.polygons]
        assert max(abs(o.matrix_world[r][c]-source.matrix_world[r][c]) for r in range(4) for c in range(4))<1e-6
        preserved.append(o.name.removeprefix(kind+'_'))
    parent=bpy.data.objects[kind+'_goose_helmet'];parts=[];build(kind)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join()
    helmet=bpy.context.object;helmet.name=kind+'_goose_headwear_'+kind+'_mesh'
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
    objects.append(helmet);collections[kind]=objects
    records[kind]={'full':stats(objects),'helmet':stats([helmet]),'preserved_meshes':preserved}
    export(ROOT/f'assets/goose-headwear-{kind}.glb',ancestors(helmet),kind+'_')
    export(ROOT/f'assets/goose-headwear-{kind}-character.glb',objects,kind+'_')

manifest={'date':'2026-10-23','status':'offline headwear candidates; no game integration',
          'protected_files_sha256':hashes,'headwear':records,
          'scope':'Seven distinct head silhouettes. Same basic vest and body, no backpack. Reference head is old light rectangular goggles.',
          'axes':'Blender +Z up/-Y front; GLB +Y up/+Z front; metres',
          'limitations':['No runtime motion/body sliders/other species tested.','Headwear includes fixed accessories, not functioning optics or cloth simulation.']}
(ROOT/'headwear-collection-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

def show(objects):
    visible=set(objects)
    for o in scene.objects:
        if not o.name.startswith('STUDIO_'):o.hide_render=o not in visible;o.hide_set(o not in visible)
camera=scene.camera
def view(pos,target,scale):
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
scene.cycles.samples=32;scene.render.resolution_x=720;scene.render.resolution_y=840
show(collections['goggles']);view((4,-6,3.3),(0,0,1.28),3.1)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/goose-headwear-collection.blend'))
views={'quarter':((4,-6,3.3),(0,0,1.28),3.1),
       'front':((0,-7,2.7),(0,0,1.28),3.1),'side':((7,0,2.7),(0,0,1.28),3.1),
       'rear':((0,7,2.7),(0,0,1.28),3.1),'top':((4,-6,10),(0,0,1.1),3.25),
       'detail':((3,-6,3.2),(0,-.05,2.03),1.63),
       'small':((4,-6,10),(0,0,1.1),10)}
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
            if label=='detail':
                high=records[kind]['helmet']['bounds_max'][2] if kind!='before' else 2.05
                view((3,-6,3.2),(0,-.05,(1.36+high)/2),max(1.26,(high-1.36)*1.3))
                if kind in ('before','goggles'):view((3,-6,3.2),(0,-.05,1.74),1.30)
            show(objects);scene.render.filepath=str(OUT/f'{lighting}-{kind}-{label}.png')
            bpy.ops.render.render(write_still=True);print('RENDERED',lighting,kind,label,flush=True)
for path,h in hashes.items():assert hashlib.sha256(Path(path).read_bytes()).hexdigest()==h,path
print('HEADWEAR_COLLECTION_READY',json.dumps(records),flush=True)
