"""Reproducible, rigid-part pet models. Run in Blender background mode.

Authored in game coordinates (+Y up, +Z forward, metres), then converted to
Blender coordinates before export. Base pets contain only an empty BackSocket.
The two weapon modules include their own removable mounting plate.
"""
import math
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / 'game/assets/pets'
OUT = ROOT / 'prototypes/character-lab/output/pets'
ASSETS.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)

def material(name, hex_color, metal=0, rough=.65):
    rgb = [int(hex_color[i:i+2],16)/255 for i in (0,2,4)]
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb,1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb,1)
    bsdf.inputs['Roughness'].default_value = rough
    bsdf.inputs['Metallic'].default_value = metal
    return m

def empty(name, parent=None, at=(0,0,0)):
    o = bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(o)
    o.parent=parent
    o.location=at
    return o

def finish(o,name,parent,at,scale,mat,rotation=(0,0,0)):
    o.name=name
    o.parent=parent
    o.location=at
    o.scale=scale
    o.rotation_euler=rotation
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth=True
    return o

def ball(parent,name,at,scale,mat,rotation=(0,0,0),segments=24,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1)
    return finish(bpy.context.object,name,parent,at,scale,mat,rotation)

def box(parent,name,at,size,mat,bevel=.04):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o=finish(bpy.context.object,name,parent,at,size,mat)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=o.modifiers.new('Soft toy corners','BEVEL');mod.width=bevel;mod.segments=2
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Flat faces retain crisp painted metal panels.
    for p in o.data.polygons: p.use_smooth=False
    return o

def tube(parent,name,at,radius,depth,mat,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=radius,depth=depth)
    rot=(math.pi/2,0,0) if axis=='Y' else (0,0,0)
    return finish(bpy.context.object,name,parent,at,(1,1,1),mat,rot)

def linear_hex(color):
    rgb=[int(color[i:i+2],16)/255 for i in (0,2,4)]
    return [v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]

def blend(a,b,t):
    t=max(0,min(1,t));t=t*t*(3-2*t)
    return tuple(x*(1-t)+y*t for x,y in zip(a,b))

def paint(obj,color_function):
    colors=obj.data.color_attributes.new(name='Coat',type='FLOAT_COLOR',domain='CORNER')
    obj.data.color_attributes.active_color=colors
    for loop in obj.data.loops:
        colors.data[loop.index].color=(*color_function(obj.data.vertices[loop.vertex_index].co),1)

def sculpt(parent,name,parts,mat,voxel=.022,triangles=2400):
    """Fuse overlapping volumes, smooth their transitions, then reduce for runtime.

    Separate articulation groups are never fused together. Vertex paint is applied
    after topology changes, so markings follow the finished continuous surface.
    """
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:part.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    if len(parts)>1:bpy.ops.object.join()
    obj=bpy.context.object;obj.name=name
    obj.data.transform(obj.matrix_basis);obj.matrix_basis=Matrix.Identity(4)
    obj.data.materials.clear();obj.data.materials.append(mat)
    remesh=obj.modifiers.new('Joined organic volume','REMESH');remesh.mode='VOXEL';remesh.voxel_size=voxel;remesh.use_smooth_shade=True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    smoothing=obj.modifiers.new('Soft anatomical transitions','SMOOTH');smoothing.factor=.75;smoothing.iterations=4
    bpy.ops.object.modifier_apply(modifier=smoothing.name)
    count=sum(len(p.vertices)-2 for p in obj.data.polygons)
    if count>triangles:
        decimate=obj.modifiers.new('Runtime surface budget','DECIMATE');decimate.ratio=triangles/count
        bpy.ops.object.modifier_apply(modifier=decimate.name)
    for polygon in obj.data.polygons:polygon.use_smooth=True
    return obj

def line(parent,name,points,mat,radius=.007):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=4
    spline=curve.splines.new('BEZIER');spline.bezier_points.add(len(points)-1)
    for p,co in zip(spline.bezier_points,points):
        p.co=co;p.handle_left_type='AUTO';p.handle_right_type='AUTO'
    curve.bevel_depth=radius;curve.bevel_resolution=1
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj);obj.parent=parent;obj.data.materials.append(mat)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object

def refined_dog(root,body,m):
    coat=material('Dog continuous painted coat','ffffff',rough=.8)
    shader=coat.node_tree.nodes.get('Principled BSDF')
    color_node=coat.node_tree.nodes.new('ShaderNodeVertexColor');color_node.layer_name='Coat'
    coat.node_tree.links.new(color_node.outputs['Color'],shader.inputs['Base Color'])
    dark=linear_hex('303b42');tan=linear_hex('bb8753');light=linear_hex('e4c494')
    torso=sculpt(body,'SculptedTorso',[
        ball(body,'Core',(0,.57,-.13),(.36,.385,.56),coat,segments=24,rings=16),
        ball(body,'Ribcage',(0,.58,.19),(.35,.34,.37),coat),
        ball(body,'Chest',(0,.62,.38),(.29,.34,.24),coat),
        ball(body,'Neck',(0,.78,.39),(.25,.28,.23),coat),
    ],coat,voxel=.010,triangles=4400)
    paint(torso,lambda v:blend(tan,dark,(v.y-.40)/.20))
    head=empty('Head',body,(0,.81,.52))
    skull=sculpt(head,'SculptedHead',[
        ball(head,'Cranium',(0,.075,.015),(.395,.34,.325),coat,segments=32,rings=20),
        ball(head,'CheekL',(-.15,-.075,.23),(.215,.18,.19),coat),
        ball(head,'CheekR',(.15,-.075,.23),(.215,.18,.19),coat),
        ball(head,'MuzzleBridge',(0,-.08,.335),(.245,.155,.22),coat),
        ball(head,'LowerJaw',(0,-.18,.29),(.20,.085,.20),coat),
    ],coat,voxel=.009,triangles=6200)
    def face_color(v):
        muzzle=max((v.z-.235)/.14,(-v.y-.025)/.17)
        brow=math.exp(-((abs(v.x)-.195)/.095)**2-((v.y-.205)/.057)**2-((v.z-.29)/.10)**2)
        base=blend(dark,tan,max(muzzle,brow*.95))
        return blend(base,light,max(0,(v.z-.35)/.20)*max(0,(-v.y+.005)/.23)*.50)
    paint(skull,face_color)
    for side in [-1,1]:
        # A folded tapered ear with a broad root and a soft inward turn at its tip.
        verts=[];faces=[];rows=24;sides=24
        for row in range(rows+1):
            t=row/rows;profile=max(.035,math.sin(math.pi*t)**.65)
            cx=side*(.30+.13*math.sin(math.pi*t*.8));cy=.23-.48*t;cz=-.055+.13*t*t
            for j in range(sides):
                a=2*math.pi*j/sides
                verts.append((cx+math.cos(a)*.13*profile,cy,cz+math.sin(a)*.095*profile))
        for row in range(rows):
            for j in range(sides):
                a=row*sides+j;b=row*sides+(j+1)%sides
                faces.append((a,b,b+sides,a+sides))
        faces.append(tuple(reversed(range(sides))))
        faces.append(tuple(rows*sides+j for j in range(sides)))
        mesh=bpy.data.meshes.new('FoldedEar');mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new('FoldedEar',mesh);bpy.context.collection.objects.link(obj)
        finish(obj,'FoldedEar',head,(0,0,0),(1,1,1),coat)
        paint(obj,lambda v:blend(dark,tan,max(0,(v.z-.02)/.11)*.32))
        z=max(v.co.z for v in skull.data.vertices if abs(v.co.x-side*.205)<.03 and abs(v.co.y-.105)<.03)
        ball(head,'EyeSocket',(side*.205,.105,z-.013),(.084,.097,.027),m['accent'],segments=24,rings=16)
        ball(head,'Eye',(side*.205,.105,z+.006),(.065,.079,.028),m['ink'],segments=24,rings=16)
        ball(head,'EyeCatchlight',(side*.205-.018,.133,z+.034),(.018,.022,.009),m['white'],segments=12,rings=8)
    nose=ball(head,'SoftTriangularNose',(0,-.012,.543),(.106,.066,.065),m['ink'],segments=24,rings=16)
    for vertex in nose.data.vertices:
        vertex.co.x*=.75+.25*(vertex.co.y+1)/2
    ball(head,'NoseSheen',(-.023,.015,.596),(.032,.013,.009),m['accent'],segments=12,rings=8)
    line(head,'Philtrum',[(0,-.06,.584),(0,-.11,.556),(0,-.133,.53)],m['ink'],.006)
    for side in [-1,1]:
        line(head,'Smile',[(0,-.133,.53),(side*.075,-.155,.513),(side*.145,-.126,.472)],m['accent'],.007)
    for front in [True,False]:
        for side in [-1,1]:
            name=('Front' if front else 'Rear')+('Left' if side<0 else 'Right')
            pivot=empty(name,body,(side*.295,.31,.35 if front else -.40))
            paw=sculpt(pivot,'SculptedPaw',[
                ball(pivot,'Shoulder',(0,.075,-.02),(.15,.25,.18),coat),
                ball(pivot,'Ankle',(0,-.075,.015),(.112,.19,.13),coat),
                ball(pivot,'Foot',(0,-.20,.075),(.145,.11,.205),coat),
            ],coat,voxel=.010,triangles=1200)
            # Remeshing rounds the sole; move only this mesh so all feet still rest at Y=0.
            floor=min(v.co.y for v in paw.data.vertices)
            for vertex in paw.data.vertices:vertex.co.y+=(-.31-floor)
            paint(paw,lambda v:blend(tan,dark,(v.y-.015)/.18))
    tail=empty('Tail',body,(0,.64,-.60))
    tail_parts=[]
    for i in range(7):
        t=i/6;radius=.095*(1-.68*t)
        tail_parts.append(ball(tail,'TailVolume',(0,.05+.30*t,-.04-.33*t+.06*t*t),(radius,radius,radius),coat,segments=24,rings=16))
    tail_mesh=sculpt(tail,'CurvedTail',tail_parts,coat,voxel=.008,triangles=900)
    paint(tail_mesh,lambda v:blend(dark,light,(v.y-.25)/.10))
    empty('BackSocket',body,(0,.95,-.23))['mount_contract']='quadruped_back_v1'
    return root

def pet(species):
    from pet_shapes import turtle, rabbit
    palettes={'dog':('bf854d','f4dfb5','75492f'), 'rabbit':('e8e3d7','fff3df','9b7d76'), 'turtle':('8ebc79','e6d49a','467260')}
    main,cream,accent=palettes[species]
    m={k:material(k,c) for k,c in dict(main=main,cream=cream,accent=accent,ink='222d32',white='ffffff',pink='db9693',shell='6f9462',rim='b9bc81').items()}
    root=empty(species);body=empty('Body',root)
    root['pet_mount_contract']='quadruped_back_v1'
    return {'dog':lambda:refined_dog(root,body,m), 'turtle':lambda:turtle(sys.modules[__name__],root,body,m), 'rabbit':lambda:rabbit(sys.modules[__name__],root,body,m)}[species]()


def weapon(kind):
    m={k:material(k,c,metal=.25,rough=.38) for k,c in dict(steel='384b59',edge='8095a2',cream='e3dbb4',accent='e4af49',dark='18242d',red='c65d49').items()}
    root=empty(kind);root['pet_mount_contract']='quadruped_back_v1'
    box(root,'MountPad',(0,.035,0),(.53,.07,.57),m['dark'])
    box(root,'MountPlate',(0,.085,0),(.49,.07,.53),m['cream'])
    for x in [-.18,.18]:
        for z in [-.18,.18]:
            tube(root,'Bolt',(x,.128,z),.028,.022,m['steel'],'Y')
    yaw=empty('Yaw',root,(0,.14,0))
    tube(yaw,'Turntable',(0,.025,0),.20,.06,m['steel'],'Y')
    pitch=empty('Pitch',yaw,(0,.13,0))
    if kind=='twin_blaster':
        box(pitch,'Receiver',(0,.03,-.045),(.43,.23,.38),m['steel'])
        box(pitch,'TopPanel',(0,.16,-.07),(.32,.045,.22),m['accent'],.012)
        for x in [-.14,.14]:
            tube(pitch,'Barrel',(x,.025,.31),.062,.50,m['edge'])
            tube(pitch,'MuzzleCollar',(x,.025,.57),.083,.12,m['steel'])
            tube(pitch,'Bore',(x,.025,.635),.048,.008,m['dark'])
        box(pitch,'Magazine',(0,-.04,-.25),(.29,.23,.14),m['cream'])
        empty('MuzzleLeft',pitch,(-.14,.025,.645));empty('MuzzleRight',pitch,(.14,.025,.645))
    else:
        box(pitch,'PodCase',(0,.035,.10),(.57,.34,.66),m['steel'])
        box(pitch,'CautionPanel',(0,.215,.04),(.40,.035,.29),m['accent'],.012)
        for x in [-.145,.145]:
            for y in [-.055,.115]:
                tube(pitch,'LaunchTube',(x,y,.455),.072,.17,m['edge'])
                tube(pitch,'TubeInterior',(x,y,.544),.054,.015,m['dark'])
                ball(pitch,'RocketNose',(x,y,.557),(.036,.036,.05),m['red'],segments=12,rings=8)
    empty('Muzzle',pitch,(0,.025,.65))
    return root

def export(root,asset):
    # Merge static pieces only within each articulation group. Material slots survive.
    groups={}
    for o in list(bpy.context.scene.objects):
        if o.type=='MESH': groups.setdefault(o.parent,[]).append(o)
    for parent,objects in groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects:o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1: bpy.ops.object.join()
        bpy.context.object.name=parent.name+'Mesh'
    bpy.context.view_layer.update()
    turn=Matrix.Rotation(math.pi/2,4,'X'); inverse=turn.inverted()
    objects=list(bpy.context.scene.objects)
    worlds={o:o.matrix_world.copy() for o in objects}
    def depth(o): return 0 if o.parent is None else 1+depth(o.parent)
    for o in sorted(objects,key=depth):
        o.matrix_world=turn @ worlds[o] @ inverse
        if o.type=='MESH':o.data.transform(turn)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/f'{asset}.glb'),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{asset}.blend'))
    print('PET_BUILD_READY',asset,flush=True)
    if asset in ['turtle','dog','rabbit']:
        # Derive low quality from the finished high shape, preserving all rigid pivots.
        for obj in bpy.context.scene.objects:
            if obj.type!='MESH':continue
            bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
            decimate=obj.modifiers.new('Low quality geometry','DECIMATE');decimate.ratio=.32
            bpy.ops.object.modifier_apply(modifier=decimate.name)
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.gltf(filepath=str(ASSETS/f'{asset}_low.glb'),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{asset}_low.blend'))
        print('PET_BUILD_READY',asset+'_low',flush=True)

if __name__ == '__main__':
    requested=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
    for asset in requested or ['turtle','dog','rabbit','twin_blaster','rocket_pod']:
        if asset not in ['turtle','dog','rabbit','twin_blaster','rocket_pod']:raise ValueError(asset)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        export(pet(asset) if asset in ['turtle','dog','rabbit'] else weapon(asset),asset)
