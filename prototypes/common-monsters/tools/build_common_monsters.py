"""Deterministic crowd assets. Blender -b -P this_file.py -- [--species parrot] [--no-render].

Authoring coordinates are Blender Z up / -Y forward. GLB exports Y up / +Z forward.
The source humanoid head and arms are imported, never overwritten. Clothing and gear
are explicit optional ancestor nodes; runtime selects them before displaying a rig.
"""
import bpy
import bmesh
import json
import math
import sys
import argparse
import hashlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / 'prototypes/common-monsters'
OUT = BASE / 'output'
ASSETS = ROOT / 'game/assets/common-monsters'
SPECIES = ['eagle', 'parrot', 'lion', 'tiger', 'reindeer', 'rhino', 'zombie']
for folder in [OUT, ASSETS]: folder.mkdir(parents=True, exist_ok=True)
M = {}


def linear(v): return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def mat(name, hexcolor, rough=.88):
    if name in M: return M[name]
    rgb = tuple(linear(int(hexcolor[i:i+2], 16)/255) for i in (0, 2, 4))
    m = bpy.data.materials.new(name); m.diffuse_color = (*rgb, 1); m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*rgb, 1)
    bs.inputs['Roughness'].default_value = rough
    M[name] = m
    return m


def empty(name, parent=None, at=(0, 0, 0)):
    o = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(o)
    o.parent = parent; o.location = at
    return o


def parent_world(o, parent):
    bpy.context.view_layer.update(); world = o.matrix_world.copy()
    o.parent = parent; o.matrix_world = world


def oval(name, at, size, material, parent, seg=16, rings=10, rot=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=at)
    o = bpy.context.object; o.name = name; o.scale = size
    if rot: o.rotation_euler = rot
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(material)
    for p in o.data.polygons: p.use_smooth = True
    parent_world(o, parent)
    return o


def tube(name, points, radii, material, parent, sides=8):
    verts=[]; faces=[]
    for i, point in enumerate(points):
        tangent = Vector(points[min(i+1,len(points)-1)]) - Vector(points[max(0,i-1)])
        tangent.normalize(); side = tangent.cross(Vector((0,1,0)))
        if side.length < .01: side = tangent.cross(Vector((1,0,0)))
        side.normalize(); other = tangent.cross(side).normalized()
        for k in range(sides):
            a=math.tau*k/sides; verts.append(Vector(point)+radii[i]*(math.cos(a)*side+math.sin(a)*other))
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))),tuple(range((len(points)-1)*sides,len(points)*sides))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
    for p in mesh.polygons:p.use_smooth=True
    parent_world(o,parent);return o


def patch(name, coords, material, parent):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(coords,[],[tuple(range(len(coords)))]);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
    parent_world(o,parent);return o


def shell(name, parent, material, z0, z1, rx=.385, ry=.31, center=.78, rz=.49, steps=4, jagged=False):
    # Closed thin garment band fitted to the same ellipsoid as the skin torso.
    n=24;verts=[];faces=[]
    for layer in range(2):
        for j in range(steps+1):
            z=z0+(z1-z0)*j/steps
            for k in range(n):
                a=math.tau*k/n
                zz=z+(.025*(k%2) if jagged and j==0 else 0)
                f=math.sqrt(max(.03,1-((zz-center)/rz)**2))
                thick=.012 if layer==0 else -.004
                verts.append(((rx*f+thick)*math.sin(a),-(ry*f+thick)*math.cos(a),zz))
    stride=(steps+1)*n
    for layer in range(2):
        for j in range(steps):
            for k in range(n):
                a=layer*stride+j*n+k;b=layer*stride+j*n+(k+1)%n
                faces.append((a,b,b+n,a+n) if layer==0 else (a+n,b+n,b,a))
    for j in [0,steps]:
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,a+stride,b+stride,b))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.data.materials.append(material)
    for p in mesh.polygons:p.use_smooth=True
    parent_world(o,parent);return o


def clear():
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for data in list(bpy.data.meshes):
        if data.users==0:bpy.data.meshes.remove(data)
    for data in list(bpy.data.materials):
        if data.users==0:bpy.data.materials.remove(data)
    M.clear()


def colors(kind):
    primary={'eagle':'756554','parrot':'B43F43','lion':'C99B4D','tiger':'D28A43','reindeer':'94715C','rhino':'8D9BA5','zombie':'8DAA78'}[kind]
    return dict(primary=mat('common_primary',primary), secondary=mat('common_secondary','D9CDA7'),
                accent=mat('common_accent','447569'), skin=mat('common_skin','93AC7B'),
                cloth=mat('common_cloth','75909B'), pants=mat('common_pants','3E556E'),
                ink=mat('fixed_ink','252A31'), cream=mat('fixed_cream','F0E3BD'),
                eye=mat('fixed_eye','181B22',.42), glint=mat('fixed_glint','FFF4DA',.35),
                gold=mat('fixed_beak','D8A33F'), pink=mat('fixed_innerEar','BD8C7B'),
                horn=mat('fixed_horn','D4C5A3'), mane=mat('fixed_mane','765139'),
                seam=mat('fixed_thread','BFAF87'), vest=mat('common_gear','39464B'))


def frame(kind):
    root=empty('Root');body=empty('Body',root);deform=empty('Deform',body)
    head=empty('Head',deform,(0,0,1.18))
    left=empty('LeftFoot',deform,(-.22,-.07,.13));right=empty('RightFoot',deform,(.22,-.07,.13))
    weapon=empty('Weapon',deform,(.28,-.49,.95));empty('Muzzle',weapon,(0,-.45,0))
    arms=[empty(f'common_{kind}_{s}Arm',deform,(sign*.39,0,.99)) for sign,s in [(-1,'left'),(1,'right')]]
    return root,deform,head,left,right,arms


def eyes(head,c,wide=.23,y=-.355,z=1.49,small=False):
    for s in [-1,1]:
        oval('eye_socket',(s*wide,y+.022,z),(.11,.055,.125),c['cream'],head,12,8)
        oval('button_eye',(s*wide,y-.022,z),(.057,.034,.074),c['eye'],head,12,8)
        oval('eye_glint',(s*wide-.015,y-.051,z+.025),(.017,.008,.019),c['glint'],head,8,6)


def seam(parent,c,points):tube('stitched_seam',points,[.006]*len(points),c['seam'],parent,5)


def gear(deform,head,c,kind):
    vest=empty('Gear_Vest',deform)
    shell('wrap_ballistic_vest',vest,c['vest'],.45,1.06,rx=.44,ry=.38)
    for s in [-1,1]:
        oval('vest_front_pouch',(s*.17,-.365,.77),(.125,.045,.16),c['vest'],vest,12,8)
        tube('vest_shoulder',[(s*.24,-.24,.99),(s*.25,0,1.11),(s*.24,.24,.99)],[.05]*3,c['vest'],vest)
    patch('vest_center_closure',[(-.018,-.353,.65),(.018,-.353,.65),(.018,-.307,1.02),(-.018,-.307,1.02)],c['ink'],vest)
    glasses=empty('Gear_Glasses',head)
    y=-.415 if kind in ['eagle','parrot'] else -.405
    if kind=='rhino':y=-.455
    for s in [-1,1]:
        oval('sunglass_lens',(s*.23,y-.015,1.49),(.145,.035,.13),c['ink'],glasses,12,8)
        tube('temple',[(s*.35,y,1.51),(s*.48,-.12,1.50)],[.017,.012],c['ink'],glasses)
    tube('nose_bridge',[(-.10,y,1.5),(.10,y,1.5)],[.016,.016],c['ink'],glasses)


def animal(kind):
    if kind=='parrot':return parrot()
    if kind=='eagle':return eagle()
    if kind=='lion':return lion()
    if kind=='reindeer':return reindeer()
    c=colors(kind);root,deform,head,left,right,arms=frame(kind)
    bird=kind in ['eagle','parrot']
    oval('plush_torso',(0,0,.78),(.38,.31,.49),c['primary'],deform,20,12)
    oval('belly_bib',(0,-.265,.77),(.255,.075,.30),c['secondary'],deform)
    for sign,foot,arm in zip([-1,1],[left,right],arms):
        oval('short_ankle',(sign*.20,-.005,.29),(.12,.14,.17),c['primary'],foot,10,7)
        oval('plush_foot',(sign*.22,-.105,.13),(.17,.23,.13),c['gold'] if bird else c['primary'],foot)
        for x in [-.055,.055]:
            seam(foot,c,[(sign*.22+x,-.30,.14),(sign*.22+x,-.255,.195)])
        oval('wing' if bird else 'foreleg',(sign*.44,-.025,.86),(.145,.20,.28),c['primary'],arm,16,10,rot=(0,sign*-.25,0))
        if bird:
            for j in range(3):
                oval('layered_feather',(sign*(.46+j*.025),-.12,.82-j*.045),(.06,.14,.15),c['accent'] if kind=='parrot' else c['secondary'],arm,10,7,rot=(0,sign*-.3,0))
        else:
            oval('soft_paw',(sign*.48,-.105,.66),(.13,.145,.14),c['primary'],arm,12,8)
    oval('round_head',(0,-.015,1.45),(.46,.36,.43),c['cream'] if kind=='eagle' else c['primary'],head,24,14)
    eyes(head,c,y=-.37 if bird else -.36)
    if bird:
        # Hooked beak with a proper curved silhouette and a split lower mandible.
        tube('upper_hooked_beak',[(0,-.32,1.42),(0,-.49,1.39),(0,-.61,1.31),(0,-.59,1.22)],
             [.15,.14,.09,.008],c['gold'] if kind=='eagle' else c['horn'],head,12)
        oval('lower_mandible',(0,-.43,1.25),(.10,.17,.055),c['gold'] if kind=='eagle' else c['ink'],head,12,8)
        for s in [-1,1]:
            oval('nostril',(s*.085,-.52,1.405),(.019,.012,.014),c['ink'],head,8,6)
        if kind=='eagle':
            for s in [-1,1]:
                oval('stern_brow',(s*.24,-.377,1.61),(.16,.04,.045),c['cream'],head,12,8,rot=(0,s*.20,0))
            for i in range(5):
                oval('neck_ruff',((i-2)*.11,-.255,1.15),(.085,.10,.14),c['cream'],head,10,7)
        else:
            for s in [-1,1]:
                oval('white_cheek',(s*.34,-.265,1.37),(.13,.075,.16),c['cream'],head,12,8)
            for i in range(3):
                tube('parrot_crest',[(.09*(i-1),.01,1.76),(.11*(i-1),.06,1.96),(.12*(i-1),.18,2.03)], [.07,.055,.005],c['accent'],head,8)
        tail=empty(f'common_{kind}_tail',deform,(0,.23,.62))
        for i in [-1,0,1]:
            oval('tail_fan',(i*.09,.36,.52),(.075,.13,.24),c['accent'] if kind=='parrot' else c['secondary'],tail,12,8,rot=(.55,0,i*-.18))
    else:
        for s in [-1,1]:
            ear=oval('rounded_ear',(s*.36,-.015,1.77),(.16,.09,.18),c['primary'],head,12,8,rot=(0,s*.4,0))
            oval('inner_ear',(s*.36,-.087,1.79),(.085,.021,.11),c['pink'],head,10,7,rot=(0,s*.4,0))
        if kind in ['lion','tiger']:
            for s in [-1,1]:
                oval('muzzle_cheek',(s*.12,-.355,1.285),(.175,.14,.13),c['cream'],head)
            oval('nose',(0,-.47,1.36),(.105,.055,.065),c['ink'],head,12,8)
            tube('mouth',[(-.12,-.466,1.245),(0,-.488,1.22),(.12,-.466,1.245)],[.012]*3,c['ink'],head,6)
            for s in [-1,1]:
                for i in range(2):oval('whisker_dot',(s*(.16+i*.045),-.47+i*.012,1.29),(.014,.01,.014),c['ink'],head,8,6)
            if kind=='lion':
                # Lobes form a chunky scalloped mane, always outside optional glasses/vest.
                for i in range(12):
                    a=math.tau*i/12
                    oval('mane_lobe',(.43*math.sin(a),.015,1.45+.43*math.cos(a)),(.20,.25,.21),c['mane'],head,10,6)
                oval('mane_back',(0,.25,1.45),(.43,.15,.43),c['mane'],head,16,10)
                for i in [-1,0,1]:
                    oval('forehead_tuft',(i*.115,-.15,1.83),(.10,.17,.13),c['mane'],head,10,7)
            else:
                for s in [-1,1]:
                    for i in range(3):
                        z=1.39+i*.12
                        tube('tiger_cheek_stripe',[(s*.39,-.19,z+.035),(s*.37,-.285,z),(s*.29,-.34,z-.035)], [.025,.028,.004],c['ink'],head,6)
                    for i in range(3):
                        z=.65+i*.13
                        tube('tiger_flank_stripe',[(s*.34,.08,z),(s*.37,-.02,z-.025),(s*.30,-.17,z-.06)],[.025,.025,.004],c['ink'],deform,6)
                for i in [-1,0,1]:
                    tube('forehead_stripe',[(i*.12,-.23,1.78),(i*.10,-.31,1.68)],[.035,.005],c['ink'],head,6)
                for s in [-1,1]:
                    for z in [1.35,1.52,1.67]:
                        f=math.sqrt(max(.1,1-((z-1.45)/.43)**2))
                        tube('tiger_back_head_stripe',[(s*.34*f,.23*f,z+.02),(s*.20*f,.31*f,z),(s*.07*f,.35*f,z-.025)], [.018,.027,.004],c['ink'],head,6)
            tail=empty(f'common_{kind}_tail',deform,(0,.24,.60))
            tube('curled_tail',[(0,.23,.60),(0,.47,.50),(.17,.53,.55),(.25,.50,.73)],[.075,.065,.055,.04],c['primary'],tail,8)
            oval('tail_tip',(.25,.50,.74),(.09,.085,.11),c['mane'] if kind=='lion' else c['ink'],tail,12,8)
        elif kind=='reindeer':
            oval('deer_muzzle',(0,-.36,1.28),(.27,.20,.17),c['secondary'],head)
            oval('deer_nose',(0,-.53,1.32),(.12,.055,.075),c['ink'],head,12,8)
            for s in [-1,1]:
                tube('antler_beam',[(s*.25,.03,1.76),(s*.32,.03,1.99),(s*.42,.05,2.14),(s*.48,.04,2.25)],[.07,.06,.045,.015],c['horn'],head,8)
                tube('antler_front_tine',[(s*.32,.03,1.98),(s*.20,-.01,2.10),(s*.18,-.02,2.20)],[.045,.032,.008],c['horn'],head,8)
                tube('antler_outer_tine',[(s*.39,.05,2.10),(s*.57,.07,2.14),(s*.62,.05,2.25)],[.04,.027,.008],c['horn'],head,8)
                oval('deer_hoof',(s*.22,-.13,.11),(.17,.23,.11),c['mane'],left if s<0 else right,12,8)
            oval('short_deer_tail',(0,.31,.64),(.10,.18,.14),c['secondary'],deform,12,8)
        else:
            oval('heavy_rhino_muzzle',(0,-.31,1.29),(.37,.28,.24),c['primary'],head,20,12)
            for s in [-1,1]:oval('nostril',(s*.19,-.56,1.30),(.035,.013,.024),c['ink'],head,10,7)
            tube('large_nose_horn',[(0,-.49,1.44),(0,-.50,1.61),(0,-.45,1.79)],[.115,.075,.008],c['horn'],head,12)
            tube('small_nose_horn',[(0,-.30,1.57),(0,-.29,1.69)],[.065,.005],c['horn'],head,10)
            for s in [-1,1]:
                for i in range(3):oval('hoof_toe',(s*.22+(i-1)*.08,-.29,.12),(.045,.045,.06),c['horn'],left if s<0 else right,10,7)
            tube('rhino_tail',[(0,.28,.68),(0,.41,.49)],[.045,.025],c['primary'],deform)
    gear(deform,head,c,kind)
    return root


PARROT_BODY=[(.115,.025,.025,.015),(.15,.17,.135,.015),(.23,.275,.225,.01),
             (.37,.335,.28,.005),(.53,.365,.307,0),(.74,.373,.315,0),
             (.89,.363,.309,0),(1.04,.348,.298,0),(1.18,.345,.295,0)]
PARROT_HEAD=[PARROT_BODY[-1],(1.32,.353,.301,0),(1.46,.365,.311,.005),
             (1.60,.36,.309,.01),(1.72,.326,.28,.015),(1.81,.254,.22,.02),
             (1.87,.14,.125,.025),(1.89,.025,.025,.025)]


def loft(name,profiles,material,parent,sides=28):
    """Horizontal, individually shaped cross sections (z, half-width, depth, y)."""
    verts=[];faces=[]
    for z,rx,ry,cy in profiles:
        for k in range(sides):
            a=math.tau*k/sides;verts.append((rx*math.sin(a),cy-ry*math.cos(a),z))
    for j in range(len(profiles)-1):
        for k in range(sides):
            a=j*sides+k;b=j*sides+(k+1)%sides;faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))),tuple(range((len(profiles)-1)*sides,len(profiles)*sides))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    for face in mesh.polygons:face.use_smooth=True
    parent_world(obj,parent);return obj


def feather(name,points,widths,depths,material,parent,sides=10):
    """A padded feather with a broad vane and swept tapered tip, not a round tube."""
    verts=[];faces=[]
    for j,point in enumerate(points):
        tangent=(Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])).normalized()
        across=Vector((1,0,0));normal=tangent.cross(across).normalized()
        for k in range(sides):
            a=math.tau*k/sides
            verts.append(Vector(point)+across*(math.cos(a)*widths[j])+normal*(math.sin(a)*depths[j]))
    for j in range(len(points)-1):
        for k in range(sides):
            a=j*sides+k;b=j*sides+(k+1)%sides;faces.append((a,b,b+sides,a+sides))
    faces.extend([tuple(reversed(range(sides))),tuple(range((len(points)-1)*sides,len(points)*sides))])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.data.materials.append(material)
    # Cross section orientation differs from the torso loft. Recalculate once for export.
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for face in mesh.polygons:face.use_smooth=True
    parent_world(obj,parent);return obj


def parrot_body_section(z):
    for a,b in zip(PARROT_BODY,PARROT_BODY[1:]):
        if a[0]<=z<=b[0]:
            t=(z-a[0])/(b[0]-a[0]);return tuple(a[i]+(b[i]-a[i])*t for i in range(1,4))
    raise ValueError(('Outside parrot body',z))


def parrot_skin(name,profiles,c,parent):
    # Interpolate the silhouette, then share the visible neck ring and its normals.
    source=PARROT_BODY+PARROT_HEAD[1:];complete=[]
    for j,(a,b) in enumerate(zip(source,source[1:])):
        before=source[max(0,j-1)];after=source[min(len(source)-1,j+2)];dz=b[0]-a[0]
        complete.append(a);t=.5
        complete.append(tuple((a[0]+b[0])*.5 if k==0 else
            (2*t**3-3*t*t+1)*a[k]+(t**3-2*t*t+t)*dz*(b[k]-before[k])/(b[0]-before[0])+
            (-2*t**3+3*t*t)*b[k]+(t**3-t*t)*dz*(after[k]-a[k])/(after[0]-a[0]) for k in range(4)))
    complete.append(source[-1]);surface=[p for p in complete if profiles[0][0]<=p[0]<=profiles[-1][0]]
    # This hidden tapered skirt overlaps the torso during existing head nod/attack poses.
    # It replaces a coincident cut plane, without adding an exterior neck bead.
    if parent.name=='Head':surface=[(1.055,.295,.25,0),(1.115,.327,.278,0)]+surface
    obj=loft(name,surface,c['primary'],parent,40);normals=[]
    for profile in surface:
        if profile in complete:
            j=complete.index(profile);a=complete[max(0,j-1)];b=complete[min(len(complete)-1,j+1)]
        else:
            j=surface.index(profile);a=surface[max(0,j-1)];b=surface[min(len(surface)-1,j+1)]
        dz=b[0]-a[0];dx=(b[1]-a[1])/dz;dy=(b[2]-a[2])/dz;dc=(b[3]-a[3])/dz
        _,rx,ry,_=profile
        for k in range(40):
            angle=math.tau*k/40;s=math.sin(angle);co=math.cos(angle)
            normals.append(Vector((ry*s,-rx*co,-dx*ry*s*s-dy*rx*co*co+dc*rx*co)).normalized())
    obj.data.normals_split_custom_set_from_vertices(normals)
    return obj


def parrot_foot(sign,c,foot):
    # One closed, low webbed foot; the perimeter has three tapered bird toe tips.
    outline=[(-.055,.09),(-.105,.025),(-.135,-.12),(-.105,-.205),
             (-.054,-.175),(-.018,-.235),(.024,-.237),(.057,-.174),
             (.108,-.205),(.134,-.13),(.101,.025),(.05,.09)]
    verts=[];faces=[];n=len(outline)
    for z,scale in [(0,1),(.032,1),(.060,.72)]:
        verts.extend((sign*.205+x*scale,y*scale,z) for x,y in outline)
    verts.append((sign*.205,-.035,.074))
    faces.append(tuple(reversed(range(n))))
    for j in range(2):
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    for k in range(n):faces.append((2*n+k,2*n+(k+1)%n,3*n))
    mesh=bpy.data.meshes.new('flat_bird_foot');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('flat_bird_foot',mesh);bpy.context.collection.objects.link(obj)
    mesh.materials.append(c['gold'])
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for p in mesh.polygons:p.use_smooth=p.index>0
    parent_world(obj,foot)
    ankle=loft('narrow_bird_ankle',[(.045,.046,.052,-.005),(.09,.043,.048,.005),
         (.15,.051,.058,.014),(.195,.06,.067,.014)],c['gold'],foot,12)
    # loft uses a centered x axis; offset the ankle without moving the foot pivot.
    for v in ankle.data.vertices:v.co.x+=sign*.205


def parrot_box(name,at,size,c,parent,rotation=0,bevel=.018):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at)
    obj=bpy.context.object;obj.name=name;obj.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    obj.rotation_euler.z=rotation;obj.data.materials.append(c)
    edge=obj.modifiers.new('small manufactured edge','BEVEL');edge.width=bevel;edge.segments=2
    bpy.ops.object.modifier_apply(modifier=edge.name)
    parent_world(obj,parent);return obj


def parrot():
    c=colors('parrot');root,deform,head,left,right,arms=frame('parrot')
    for key,rough in [('primary',.73),('accent',.73),('secondary',.73),('gold',.58),('horn',.54),('eye',.65),('vest',.9)]:
        c[key].node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=rough
    parrot_skin('Deform_mesh',PARROT_BODY,c,deform)
    parrot_skin('continuous_parrot_head',PARROT_HEAD,c,head)
    for s in [-1,1]:
        oval('small_dark_eye',(s*.235,-.236,1.565),(.049,.020,.055),c['eye'],head,16,10,rot=(0,0,s*.67))
    feather('broad_hooked_beak',[(0,-.282,1.46),(0,-.365,1.472),(0,-.465,1.427),
            (0,-.517,1.352),(0,-.508,1.281)],
            [.132,.157,.133,.080,.003],[.094,.101,.091,.062,.002],c['horn'],head,16)
    feather('lower_beak',[(0,-.284,1.321),(0,-.369,1.298),(0,-.448,1.31),(0,-.489,1.331)],
            [.105,.112,.080,.005],[.035,.030,.022,.003],c['horn'],head,12)
    for s in [-1,1]:
        oval('beak_nostril',(s*.083,-.425,1.514),(.011,.007,.008),c['ink'],head,8,6)
    for i in [-1,0,1]:
        cresttop=2.17 if i==0 else 2.07
        feather('pointed_crest',[(i*.083,.015,1.79),(i*.10,.045,1.91),
                (i*.137,.10,cresttop-.095),(i*.19,.17,cresttop)],
                [.055,.078,.045,.001],[.036,.041,.025,.001],c['accent'],head,12)
    for sign,foot,arm in zip([-1,1],[left,right],arms):
        parrot_foot(sign,c,foot)
        feather('wing_shoulder',[(sign*.325,.005,1.11),(sign*.407,-.02,1.065),
                (sign*.443,-.035,.98),(sign*.462,-.04,.85),(sign*.435,-.018,.68)],
                [.035,.09,.116,.109,.018],[.042,.075,.086,.066,.014],c['primary'],arm,16)
        for j in range(3):
            x=sign*(.39+j*.064)
            feather('pointed_flight_feather',[(x,-.095,.91-j*.025),(x+sign*.018,-.12,.80-j*.028),
                    (x+sign*.014,-.085,.68-j*.033),(x-sign*.016,-.015,.57-j*.034)],
                    [.045,.057,.040,.001],[.021,.029,.020,.001],c['accent'],arm,12)
    tail=empty('common_parrot_tail',deform,(0,.23,.62))
    for i in [-1,0,1]:
        feather('swept_tail_feather',[(i*.055,.20,.66),(i*.085,.36,.57),
                (i*.12,.50,.42),(i*.14,.56,.30+abs(i)*.035)],
                [.048,.077,.054,.001],[.025,.031,.021,.001],c['secondary'] if i==0 else c['accent'],tail,12)
    vest=empty('Gear_Vest',deform)
    profile=[]
    for z in [.46,.59,.74,.89,1.04,1.10]:
        rx,ry,cy=parrot_body_section(z);profile.append((z,rx+.025,ry+.027,cy))
    loft('fitted_vest_shell',profile,c['vest'],vest,32)
    for s in [-1,1]:
        parrot_box('rectangular_vest_pouch',(s*.16,-.335,.74),(.225,.115,.285),c['vest'],vest,s*.32)
        parrot_box('pouch_lid',(s*.16,-.393,.85),(.229,.035,.069),c['vest'],vest,s*.32,.009)
        parrot_box('pouch_fastener',(s*.16,-.414,.793),(.04,.013,.052),c['ink'],vest,s*.32,.004)
        tube('vest_shoulder',[(s*.24,-.24,1.04),(s*.31,-.19,1.11),(s*.375,0,1.14),
             (s*.31,.19,1.11),(s*.24,.24,1.04)],[.028]*5,c['vest'],vest,6)
    parrot_box('vest_front_closure',(0,-.344,.81),(.023,.018,.42),c['ink'],vest,bevel=.005)
    # Keep the optional legacy node contract; current roster never enables eye gear.
    glasses=empty('Gear_Glasses',head)
    for s in [-1,1]:oval('sunglass_lens',(s*.22,-.372,1.535),(.108,.02,.105),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.11,-.372,1.55),(.11,-.372,1.55)],[.013,.013],c['ink'],glasses)
    return root



def eagle():
    """Clean bald-eagle silhouette, thin overlapping vanes and grounded talons."""
    c=colors('eagle');root,deform,head,left,right,arms=frame('eagle')
    for key,rough in [('primary',.73),('secondary',.74),('cream',.76),('gold',.56),('eye',.64),('vest',.9)]:
        c[key].node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=rough

    def skin(name,profiles,material,parent,sides=40):
        # Shape-preserving intermediate rings remove the old polygonal outline.
        smooth=[]
        for j,(a,b) in enumerate(zip(profiles,profiles[1:])):
            before=profiles[max(0,j-1)];after=profiles[min(len(profiles)-1,j+2)]
            smooth.append(a);t=.5;dz=b[0]-a[0]
            smooth.append(tuple((a[0]+b[0])*.5 if k==0 else
                (2*t**3-3*t*t+1)*a[k]+(t**3-2*t*t+t)*dz*(b[k]-before[k])/(b[0]-before[0])+
                (-2*t**3+3*t*t)*b[k]+(t**3-t*t)*dz*(after[k]-a[k])/(after[0]-a[0]) for k in range(4)))
        smooth.append(profiles[-1]);return loft(name,smooth,material,parent,sides)

    body=[(.16,.10,.11,.015),(.27,.21,.195,.01),(.43,.30,.26,.005),
          (.64,.365,.305,0),(.85,.37,.30,.005),(1.04,.30,.25,.015),
          (1.20,.235,.205,.015),(1.285,.18,.16,.015)]
    skin('eagle_body',body,c['primary'],deform)
    skin('eagle_head_neck',[(1.075,.205,.178,.015),(1.17,.255,.22,.012),
         (1.29,.32,.27,.005),(1.43,.395,.32,-.005),(1.565,.407,.33,.005),
         (1.69,.365,.297,.025),(1.79,.275,.23,.045),(1.85,.145,.13,.055),
         (1.87,.008,.008,.055)],c['cream'],head)
    # Long, shallow vanes follow the neck instead of making a padded bead ring.
    for i in range(13):
        a=math.tau*i/13;x=math.sin(a);y=-math.cos(a)
        feather('tapered_neck_feather',[(x*.294,y*.249+.01,1.275),
                (x*.274,y*.232+.01,1.18),(x*.245,y*.211+.012,1.035+(i%2)*.033)],
                [.041,.048,.0015],[.011,.014,.001],c['cream'],head,10)
    for sign in [-1,1]:
        oval('low_dark_eye',(sign*.235,-.270,1.555),(.040,.016,.033),c['eye'],head,16,10,rot=(0,sign*-.14,sign*.60))
        # The upper eyelid lies inside the head surface; no floating eyebrow bead.
        feather('integrated_upper_lid',[(sign*.174,-.299,1.576),(sign*.233,-.284,1.589),
                (sign*.296,-.251,1.617)], [.006,.025,.003],[.008,.017,.004],c['cream'],head,10)
    feather('golden_hooked_beak',[(0,-.293,1.468),(0,-.392,1.477),(0,-.503,1.438),
            (0,-.57,1.365),(0,-.565,1.29),(0,-.538,1.258)],
            [.105,.123,.10,.065,.027,.002],[.078,.084,.066,.049,.021,.002],c['gold'],head,16)
    feather('lower_mandible',[(0,-.296,1.352),(0,-.406,1.351),(0,-.508,1.35)],
            [.078,.082,.006],[.035,.033,.004],c['gold'],head,12)
    for sign in [-1,1]:
        oval('nostril',(sign*.073,-.449,1.499),(.011,.007,.008),c['ink'],head,8,6)

    def talon_toe(name,path,widths,heights,material,foot):
        verts=[];faces=[];n=8
        # Flat-bottom cross sections give each toe a genuine contact strip.
        section=[(-1,0),(-1,.45),(-.65,.9),(0,1),(.65,.9),(1,.45),(1,0),(0,0)]
        for (x,y),w,h in zip(path,widths,heights):
            verts.extend((x+sx*w,y,z*h) for sx,z in section)
        for j in range(len(path)-1):
            for k in range(n):
                q=j*n+k;r=j*n+(k+1)%n;faces.append((q,r,r+n,q+n))
        faces.extend([tuple(reversed(range(n))),tuple(range((len(path)-1)*n,len(path)*n))])
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
        obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(material)
        bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
        for poly in mesh.polygons:poly.use_smooth=True
        parent_world(obj,foot)

    for sign,foot,arm in zip([-1,1],[left,right],arms):
        ankle=skin('narrow_scaly_ankle',[(.027,.048,.059,.012),(.075,.043,.05,.018),
             (.14,.044,.051,.026),(.195,.057,.065,.028)],c['gold'],foot,16)
        for v in ankle.data.vertices:v.co.x+=sign*.205
        for j in [-1,0,1]:
            x=sign*.205
            tipx=x+j*.105;tipy=-.269+abs(j)*.033
            talon_toe('spread_talon_toe',[(x+j*.029,.021),(x+j*.058,-.084),
                      (tipx,tipy+.04),(tipx,tipy)], [.026,.03,.019,.009],
                      [.063,.048,.03,.013],c['gold'],foot)
            talon_toe('curved_dark_claw',[(tipx,tipy+.008),(tipx+j*.008,tipy-.019),
                      (tipx+j*.012,tipy-.037)],[.016,.012,.001],[.026,.018,.002],c['ink'],foot)
        talon_toe('rear_talon',[(sign*.205,.015),(sign*.205+sign*.037,.101),
                  (sign*.205+sign*.057,.139)],[.028,.023,.003],[.057,.037,.005],c['gold'],foot)
        # Compact caps preserve the existing authored-wing grip interface.
        feather('wing_coverts',[(sign*.355,0,1.105),(sign*.435,-.019,1.02),
                (sign*.477,-.03,.84),(sign*.46,-.019,.68)],
                [.039,.116,.128,.025],[.038,.067,.063,.015],c['primary'],arm,16)
        for j in range(4):
            x=sign*(.385+j*.059)
            points=[(x,-.077,.94-j*.021),(x+sign*.008,-.097,.837-j*.022),
                    (x-sign*.012,-.075,.712-j*.018),(sign*(.432+j*.012),-.021,.60-j*.009)]
            widths=[.033,.057,.047,.001];depths=[.012,.021,.015,.001]
            # Interpolate the vanes, keeping the existing narrow upper/lower caps.
            sections=[]
            for k in range(3):
                a=points[k];b=points[k+1];before=points[max(0,k-1)];after=points[min(3,k+2)]
                sections.append((a,widths[k],depths[k]))
                mid=tuple(.5*(a[d]+b[d])+.0625*(a[d]-before[d]+b[d]-after[d]) for d in range(3))
                sections.append((mid,(widths[k]+widths[k+1])*.5,(depths[k]+depths[k+1])*.5))
            sections.append((points[-1],widths[-1],depths[-1]))
            vane=feather('tapered_flight_feather',[p for p,w,d in sections],[w for p,w,d in sections],
                    [d for p,w,d in sections],c['primary'],arm,10)
            if j%2==0:
                vane.data.materials.append(c['secondary'])
                for poly in vane.data.polygons:
                    if 40<=poly.index<60:poly.material_index=1
    tail=empty('common_eagle_tail',deform,(0,.23,.62))
    for i in [-1,0,1]:
        feather('tapered_tail_fan',[(i*.055,.20,.65),(i*.09,.34,.54),
                (i*.12,.46,.385),(i*.13,.485,.33+abs(i)*.025)],
                [.051,.075,.055,.002],[.018,.023,.015,.001],c['secondary'],tail,12)
    vest=empty('Gear_Vest',deform)
    skin('fitted_eagle_vest',[(z,rx+.025,ry+.028,cy) for z,rx,ry,cy in body[2:6]],c['vest'],vest)
    for sign in [-1,1]:
        parrot_box('structured_vest_pouch',(sign*.153,-.328,.731),(.221,.112,.273),c['vest'],vest,sign*.34)
        parrot_box('pouch_lid',(sign*.153,-.386,.829),(.224,.033,.065),c['vest'],vest,sign*.34,.009)
        parrot_box('pouch_fastener',(sign*.153,-.407,.774),(.037,.012,.048),c['ink'],vest,sign*.34,.004)
        tube('vest_shoulder',[(sign*.211,-.191,1.04),(sign*.263,-.151,1.10),(sign*.299,0,1.115),
             (sign*.263,.17,1.10),(sign*.211,.211,1.04)],[.027]*5,c['vest'],vest,6)
    parrot_box('vest_front_closure',(0,-.335,.76),(.022,.018,.41),c['ink'],vest,bevel=.005)
    glasses=empty('Gear_Glasses',head)
    for sign in [-1,1]:oval('sunglass_lens',(sign*.22,-.36,1.55),(.108,.02,.10),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.11,-.36,1.55),(.11,-.36,1.55)],[.013,.013],c['ink'],glasses)
    return root


def lion_mane(c,head):
    """Continuous swept mane with broad tapering locks and a closed rear cap."""
    n=88;verts=[];faces=[]
    # Broad roots tuck under the face. The thin outer crest sweeps down/back,
    # rather than rounding forward into the old padded halo.
    profiles=[(.54,-.045),(.72,-.11),(.90,-.085),(1.0,.055),(.90,.225),(.64,.33),(.02,.365)]
    for j,(r,y) in enumerate(profiles):
        for k in range(n):
            angle=math.tau*k/n
            phase=(k*11/n)%1
            lock=(1-abs(phase*2-1))**.85
            crest=(.075*lock-.022)*(r**3)
            swept=angle+.16*math.sin(angle)*r
            x=(.505*r+crest)*math.sin(swept)
            z=1.465+(.565*r+crest)*math.cos(swept)
            if math.cos(angle)<0:x*=.83
            verts.append((x,y+.035*lock*r*r,z))
    for j in range(len(profiles)-1):
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple(reversed(range(n))))
    faces.append(tuple(range((len(profiles)-1)*n,len(profiles)*n)))
    mesh=bpy.data.meshes.new('directional_lion_mane');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('directional_lion_mane',mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(c['mane'])
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for p in mesh.polygons:p.use_smooth=True
    parent_world(obj,head)


def lion():
    c=colors('lion');root,deform,head,left,right,arms=frame('lion')
    for key,rough in [('primary',.73),('secondary',.75),('cream',.75),('mane',.78),('eye',.66),('ink',.64),('vest',.9)]:
        c[key].node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=rough
    body=[(.27,.12,.12,.02),(.36,.255,.22,.015),(.48,.335,.28,.01),
          (.60,.375,.31,.005),(.74,.39,.32,.005),(.88,.365,.30,.01),
          (1.02,.30,.25,.02),(1.14,.235,.205,.025),(1.24,.165,.16,.025),
          (1.34,.07,.085,.025)]
    loft('continuous_lion_body',body,c['primary'],deform,32)
    lion_mane(c,head)
    loft('continuous_lion_face',[(1.08,.14,.15,.015),(1.19,.245,.22,-.015),
         (1.30,.335,.275,-.035),(1.42,.38,.305,-.04),(1.54,.385,.308,-.025),
         (1.66,.35,.28,-.005),(1.76,.275,.22,.01),(1.83,.17,.145,.025),
         (1.86,.025,.03,.03)],c['primary'],head,36)
    for s in [-1,1]:
        oval('feline_ear',(s*.322,-.06,1.815),(.094,.058,.099),c['primary'],head,14,8,rot=(0,s*.20,0))
        oval('ear_inset',(s*.322,-.109,1.82),(.052,.011,.056),c['mane'],head,12,8)
        oval('small_dark_eye',(s*.19,-.306,1.554),(.033,.018,.040),c['eye'],head,16,10,rot=(0,0,s*.38))
    # A single shallow pale muzzle keeps contrast through runtime palette swaps.
    # Tail tuft carries secondary; no stuffed cheek balls or palm pad.
    loft('unified_feline_muzzle',[(1.205,.055,.028,-.294),(1.23,.14,.058,-.32),
         (1.28,.208,.078,-.335),(1.35,.224,.087,-.338),(1.41,.174,.064,-.33),
         (1.435,.075,.028,-.32)],c['cream'],head,32)
    feather('feline_triangle_nose',[(0,-.385,1.435),(0,-.439,1.414),(0,-.443,1.375)],
            [.057,.067,.015],[.023,.028,.008],c['ink'],head,12)
    tube('short_philtrum',[(0,-.427,1.378),(0,-.430,1.331)],[.005,.005],c['ink'],head,6)
    tube('neutral_mouth',[(-.052,-.420,1.318),(0,-.429,1.326),(.052,-.420,1.318)],
         [.004]*3,c['ink'],head,6)
    for s,foot,arm in zip([-1,1],[left,right],arms):
        # Tapered continuous thigh/ankle enters the low feline foot.
        leg=loft('tapered_leg',[(.085,.067,.079,-.005),(.15,.071,.082,.005),
             (.225,.083,.094,.012),(.32,.115,.12,.018),(.405,.124,.13,.018),
             (.46,.10,.105,.018)],c['primary'],foot,16)
        for v in leg.data.vertices:v.co.x+=s*.205
        shoe=loft('flat_feline_foot',[(0,.108,.154,-.075),(.021,.125,.177,-.075),
             (.058,.13,.18,-.075),(.10,.12,.163,-.06),(.135,.095,.126,-.035),
             (.165,.067,.08,-.005)],c['primary'],foot,20)
        for v in shoe.data.vertices:
            v.co.x+=s*.22
            v.co.y=max(v.co.y,-.236)
        for poly in shoe.data.polygons:
            if all(abs(shoe.data.vertices[i].co.z)<1e-6 for i in poly.vertices):poly.use_smooth=False
        # Only the forearm stretches; rigid paw/anchor coordinates stay authored.
        forearm=loft('forearm',[(.785,.055,.052,-.025),(.84,.072,.066,-.02),
             (.92,.094,.081,-.008),(.98,.069,.061,0),(.99,.025,.025,0)],c['primary'],arm,16)
        for v in forearm.data.vertices:v.co.x+=s*(.435-.045*max(0,min(1,(v.co.z-.80)/.19)))
        forearm.name=arm.name+'_mesh'
        side='left' if s<0 else 'right'
        paw_node=empty(f'common_lion_{side}Paw')
        parent_world(paw_node,arm)
        wrist=empty(f'common_lion_{side}Wrist',paw_node)
        wrist.location=(s*.435,-.025,.80)
        grip=empty(f'common_lion_{side}Grip',paw_node)
        grip.location=(s*.435,-.065,.685)
        paw=loft('compact_feline_grip',[(.615,.025,.027,-.045),(.64,.060,.047,-.055),
             (.68,.078,.058,-.065),(.725,.080,.054,-.055),(.77,.063,.047,-.035),
             (.815,.052,.049,-.025)],c['primary'],paw_node,16)
        for v in paw.data.vertices:v.co.x+=s*.435
        paw.name=paw_node.name+'_mesh'
        oval('tucked_thumb',(s*.379,-.092,.708),(.035,.039,.043),c['primary'],paw_node,12,8,rot=(0,s*.35,0))
    tail=empty('common_lion_tail',deform,(0,.24,.60))
    tube('curved_feline_tail',[(0,.24,.60),(0,.31,.54),(.025,.40,.505),(.09,.47,.505),
         (.16,.50,.54),(.21,.50,.60),(.24,.48,.69),(.25,.47,.735)],
         [.051,.050,.047,.044,.039,.034,.029,.026],c['primary'],tail,12)
    feather('tapered_tail_tuft',[(.25,.47,.69),(.265,.46,.755),(.25,.445,.82),(.21,.435,.855)],
            [.045,.068,.048,.002],[.041,.052,.038,.002],c['secondary'],tail,12)
    vest=empty('Gear_Vest',deform)
    loft('fitted_lion_vest',[(z,rx+.024,ry+.027,cy) for z,rx,ry,cy in body[2:7]],c['vest'],vest,32)
    for s in [-1,1]:
        parrot_box('rectangular_lion_pouch',(s*.16,-.338,.72),(.218,.105,.255),c['vest'],vest,s*.30)
        parrot_box('pouch_lid',(s*.16,-.393,.818),(.222,.032,.060),c['vest'],vest,s*.30,.009)
        parrot_box('pouch_fastener',(s*.16,-.41,.768),(.036,.014,.045),c['ink'],vest,s*.30,.004)
        tube('vest_shoulder',[(s*.21,-.21,1.00),(s*.265,-.15,1.075),(s*.305,0,1.105),
             (s*.26,.165,1.07),(s*.21,.21,1.00)],[.027]*5,c['vest'],vest,6)
    parrot_box('vest_front_closure',(0,-.349,.75),(.022,.018,.39),c['ink'],vest,bevel=.005)
    glasses=empty('Gear_Glasses',head)
    for s in [-1,1]:oval('sunglass_lens',(s*.20,-.37,1.55),(.105,.02,.10),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.10,-.37,1.55),(.10,-.37,1.55)],[.013,.013],c['ink'],glasses)
    return root


def reindeer():
    """Continuous deer neck, split weight-bearing hooves and spreading tapered antlers."""
    c=colors('reindeer');root,deform,head,left,right,arms=frame('reindeer')
    for sign,arm in zip([-1,1],arms):arm.location=(sign*.34,0,1.055)
    body=[(.18,.11,.13,.02),(.30,.23,.21,.015),(.48,.32,.275,0),(.70,.365,.30,0),
          (.90,.35,.285,.01),(1.08,.29,.24,.02),(1.25,.235,.205,.025),(1.36,.20,.18,.025)]
    loft('deer_continuous_body',body,c['primary'],deform,32)
    loft('deer_head_neck',[(1.10,.20,.18,.025),(1.24,.25,.22,.02),(1.39,.345,.285,.015),
         (1.55,.385,.30,.015),(1.69,.35,.27,.025),(1.80,.265,.21,.03),
         (1.86,.14,.12,.035),(1.88,.008,.008,.035)],c['primary'],head,32)
    # One long muzzle with a narrow nasal bridge, no paired cheek cushions.
    oval('long_deer_muzzle',(0,-.295,1.34),(.235,.32,.155),c['secondary'],head,24,12)
    oval('deer_nose',(0,-.575,1.36),(.128,.055,.072),c['ink'],head,16,8)
    for sign,foot,arm in zip([-1,1],[left,right],arms):
        oval('deer_eye',(sign*.245,-.247,1.565),(.037,.023,.046),c['eye'],head,12,8)
        feather('pointed_deer_ear',[(sign*.27,.015,1.70),(sign*.43,.015,1.77),(sign*.59,.025,1.86)],
                [.055,.115,.002],[.04,.052,.002],c['primary'],head,10)
        feather('inner_deer_ear',[(sign*.34,-.035,1.745),(sign*.44,-.038,1.79),(sign*.55,-.021,1.845)],
                [.02,.058,.001],[.008,.012,.001],c['pink'],head,8)
        tube('antler_main_beam',[(sign*.22,.045,1.78),(sign*.25,.045,1.97),(sign*.36,.06,2.13),
             (sign*.50,.075,2.27),(sign*.66,.09,2.34)],[.061,.051,.039,.025,.002],c['horn'],head,10)
        tube('antler_inner_tine',[(sign*.27,.045,2.01),(sign*.16,.015,2.13),(sign*.13,-.015,2.27)],
             [.039,.025,.001],c['horn'],head,10)
        tube('antler_crown_tine',[(sign*.40,.065,2.17),(sign*.40,.045,2.29),(sign*.38,.03,2.37)],
             [.030,.018,.001],c['horn'],head,10)
        tube('antler_outer_tine',[(sign*.48,.075,2.25),(sign*.66,.045,2.23),(sign*.75,.025,2.31)],
             [.025,.017,.001],c['horn'],head,10)
        tube('deer_lower_leg',[(sign*.20,.015,.49),(sign*.22,-.015,.24),(sign*.22,-.035,.11)],
             [.108,.083,.072],c['primary'],foot,12)
        for split in [-1,1]:
            parrot_box('split_flat_hoof',(sign*.22+split*.052,-.10,.075),(.094,.265,.15),c['mane'],foot,bevel=.018)
        forearm=feather('deer_forearm',[(sign*.34,0,1.055),(sign*.43,-.025,.91),(sign*.465,-.07,.735)],
                [.105,.102,.075],[.105,.10,.072],c['primary'],arm,12)
        forearm.name=arm.name+'_mesh'
        side='left' if sign<0 else 'right'
        paw_node=empty(f'common_reindeer_{side}Paw');parent_world(paw_node,arm)
        wrist=empty(f'common_reindeer_{side}Wrist',paw_node);wrist.location=(sign*.465,-.07,.735)
        grip=empty(f'common_reindeer_{side}Grip',paw_node);grip.location=(sign*.465,-.09,.69)
        hand=oval('compact_deer_hand',(sign*.465,-.09,.695),(.083,.09,.095),c['primary'],paw_node,12,8)
        hand.name=paw_node.name+'_mesh'
    feather('short_deer_tail',[(0,.235,.70),(0,.36,.68),(0,.43,.61)],[.065,.09,.002],[.06,.06,.001],c['secondary'],deform,10)
    vest=empty('Gear_Vest',deform)
    loft('fitted_deer_vest',[(z,rx+.025,ry+.028,cy) for z,rx,ry,cy in body[2:6]],c['vest'],vest,32)
    for sign in [-1,1]:
        parrot_box('rectangular_deer_pouch',(sign*.16,-.33,.74),(.215,.105,.255),c['vest'],vest,sign*.28)
        parrot_box('pouch_lid',(sign*.16,-.385,.84),(.22,.03,.065),c['vest'],vest,sign*.28,.008)
        parrot_box('pouch_fastener',(sign*.16,-.404,.79),(.035,.012,.045),c['ink'],vest,sign*.28,.004)
        tube('vest_shoulder',[(sign*.21,-.19,1.025),(sign*.26,0,1.12),(sign*.21,.23,1.025)],[.032]*3,c['vest'],vest,8)
    parrot_box('vest_closure',(0,-.34,.77),(.022,.018,.38),c['ink'],vest,bevel=.005)
    glasses=empty('Gear_Glasses',head)
    for sign in [-1,1]:oval('legacy_lens',(sign*.245,-.275,1.565),(.09,.015,.085),c['ink'],glasses,12,8)
    return root


def trousers(module,c,style):
    shell('trouser_waist',module,c['pants'],.40,.69,rx=.40,ry=.325)
    for s in [-1,1]:
        oval('trouser_leg',(s*.20,0,.34),(.178,.235,.24),c['pants'],module,12,8)
        tube('outer_leg_seam',[(s*.35,-.055,.21),(s*.37,-.05,.46),(s*.32,-.16,.62)],[.006]*3,c['seam'],module,5)
        if style=='torn':
            oval('torn_knee',(s*.20,-.229,.38),(.09,.014,.055),c['skin'],module,12,8)
            for k in range(3):
                tube('frayed_threads',[(s*.20-.07+k*.06,-.247,.41),(s*.20-.06+k*.06,-.249,.36)],[.006,.003],c['seam'],module,5)
        else:
            tube('pocket_stitch',[(s*.11,-.29,.61),(s*.17,-.28,.53),(s*.27,-.235,.55)],[.007]*3,c['seam'],module,5)
    tube('waist_band',[(-.28,-.22,.64),(0,-.325,.64),(.28,-.22,.64)],[.018]*3,c['mane'],module,6)
    oval('waist_button',(0,-.343,.64),(.024,.012,.024),c['horn'],module,8,6)


def zombie():
    c=colors('zombie')
    bpy.ops.import_scene.gltf(filepath=str(ROOT/'game/assets/enemies/humanoid/humanoid.glb'))
    root=bpy.data.objects['humanoid_root'];root.name='Root'
    deform=bpy.data.objects['Deform'];head=bpy.data.objects['Head']
    # Retain the source human skull, ears, crooked hair and teeth. Separate its
    # disconnected semantic surfaces only to reduce the oversized eye/cheek pieces.
    bpy.data.objects.remove(bpy.data.objects['Deform_mesh'],do_unlink=True)
    replacements={'infected_skin':c['skin'],'infected_cloth':c['skin'],'infected_trim':c['ink'],
                  'infected_cream':c['cream'],'infected_ink':c['ink'],'infected_mouth':mat('fixed_mouth','5E3D46')}
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith('humanoid_'):obj.name=obj.name.replace('humanoid_','common_zombie_')
        if obj.type=='MESH':
            for i,m in enumerate(obj.data.materials):obj.data.materials[i]=replacements.get(m.name,m)
            for p in obj.data.polygons:p.use_smooth=True
    source=bpy.data.objects['Head_mesh']
    bpy.ops.object.select_all(action='DESELECT');source.select_set(True);bpy.context.view_layer.objects.active=source
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='LOOSE');bpy.ops.object.mode_set(mode='OBJECT')
    for piece in list(bpy.context.selected_objects):
        coords=[piece.matrix_world@v.co for v in piece.data.vertices]
        center=sum(coords,Vector())/len(coords)
        material=piece.data.materials[piece.data.polygons[0].material_index].name
        # The white pinpoints and round cheek blush are disconnected source pieces.
        if material=='fixed_cream' and center.y<-.48 and center.z>1.40:
            bpy.data.objects.remove(piece,do_unlink=True);continue
        if material=='fixed_mouth' and center.y<-.30 and center.z<1.32 and abs(center.x)>.19:
            bpy.data.objects.remove(piece,do_unlink=True);continue
        # Retain uneven human eyes, but make the sclera and socket shallow planes.
        if center.y<-.34 and center.z>1.30 and abs(center.x)>.05 and material in ('fixed_ink','fixed_cream'):
            width=.52 if material=='fixed_cream' else .58
            height=.52 if material=='fixed_cream' else .60
            # Separate source eye shells had different depths. Moving each by the
            # same offset left the pupil floating ahead of the white in profile.
            eye_depth=(-.375 if material=='fixed_cream' else
                       -.384 if center.y<-.45 else
                       -.363 if center.z<1.55 else center.y+.04)
            for v in piece.data.vertices:
                world=piece.matrix_world@v.co
                world.x=center.x+(world.x-center.x)*width
                world.z=center.z+(world.z-center.z)*height
                world.y=eye_depth+(world.y-center.y)*.28
                v.co=piece.matrix_world.inverted()@world
        if material=='fixed_ink' and center.z<1.27 and abs(center.x)<.10:
            # Mouth opening, not an oversized second eye.
            for v in piece.data.vertices:
                world=piece.matrix_world@v.co
                world.x=center.x+(world.x-center.x)*.77
                world.z=center.z+(world.z-center.z)*.57
                world.y=center.y+(world.y-center.y)*.40+.022
                v.co=piece.matrix_world.inverted()@world
        if material=='fixed_cream' and center.z<1.27:
            for v in piece.data.vertices:
                world=piece.matrix_world@v.co
                world.x=center.x+(world.x-center.x)*.72
                world.z=center.z+(world.z-center.z)*.72
                world.y=center.y+(world.y-center.y)*.45+.058
                v.co=piece.matrix_world.inverted()@world
        if material=='common_skin' and center.z<1.27 and center.y<-.10:
            # Broad source lower jaw sits closer to the skull under head motion.
            for v in piece.data.vertices:
                world=piece.matrix_world@v.co
                world.y=center.y+(world.y-center.y)*.85+.015
                v.co=piece.matrix_world.inverted()@world
    oval('human_torso',(0,0,.78),(.38,.31,.49),c['skin'],deform,20,12)
    # Neck overlap stays inside the original human jaw during its maximum idle motion.
    oval('human_neck',(0,.015,1.13),(.205,.18,.25),c['skin'],deform,16,10)
    for sign,side in [(-1,'left'),(1,'right')]:
        oval('bare_human_leg',(sign*.20,0,.34),(.145,.185,.25),c['skin'],deform,16,10)
        arm=bpy.data.objects[f'common_zombie_{side}Arm']
        bpy.data.objects.remove(bpy.data.objects[f'common_zombie_{side}Arm_mesh'],do_unlink=True)
        tube('human_smooth_arm',[(sign*.31,0,1.035),(sign*.415,-.09,.83),(sign*.44,-.22,.66)],
             [.115,.095,.073],c['skin'],arm,12)
        parrot_box('compact_human_hand',(sign*.44,-.255,.61),(.15,.16,.19),c['skin'],arm,bevel=.045)
        oval('human_thumb',(sign*.38,-.29,.635),(.04,.055,.062),c['skin'],arm,10,7)
        foot=bpy.data.objects['LeftFoot' if sign<0 else 'RightFoot']
        bpy.data.objects.remove(bpy.data.objects[foot.name+'_mesh'],do_unlink=True)
        parrot_box('flat_human_foot',(sign*.22,-.075,.075),(.235,.32,.15),c['ink'],foot,bevel=.025)
    for name,style in [('Jeans','jeans'),('Chinos','chinos'),('Torn','torn')]:
        m=empty('Outfit_'+name,deform);trousers(m,c,style)
        if style=='chinos':
            for s in [-1,1]:tube('pressed_crease',[(s*.20,-.234,.20),(s*.20,-.24,.49)],[.005,.005],c['seam'],m,5)
    shirt=empty('Outfit_Shirt',deform)
    shell('short_shirt',shirt,c['cloth'],.63,1.11,rx=.38,ry=.31)
    for s in [-1,1]:
        oval('shirt_fitted_sleeve',(s*.35,-.005,1.01),(.115,.15,.105),c['cloth'],shirt,12,8)
    tube('shirt_hem',[(-.27,-.245,.64),(0,-.319,.64),(.27,-.245,.64)],[.006]*3,c['seam'],shirt,6)
    overalls=empty('Outfit_Overalls',deform);trousers(overalls,c,'jeans')
    shell('overall_bib',overalls,c['pants'],.62,.94,rx=.38,ry=.31)
    for s in [-1,1]:
        tube('overall_strap',[(s*.17,-.32,.84),(s*.23,-.21,1.08),(s*.23,.15,1.10),(s*.17,.31,.80)],[.043]*4,c['pants'],overalls,8)
        oval('brass_button',(s*.17,-.344,.89),(.024,.012,.024),c['gold'],overalls,8,6)
    patch('bib_pocket',[(-.105,-.345,.78),(.105,-.345,.78),(.105,-.34,.88),(-.105,-.34,.88)],c['cloth'],overalls)
    suit=empty('Outfit_Suit',deform);trousers(suit,c,'chinos')
    shell('suit_jacket',suit,c['cloth'],.57,1.11,rx=.39,ry=.32)
    patch('shirt_front',[(-.11,-.33,1.045),(.11,-.33,1.045),(.07,-.357,.76),(-.07,-.357,.76)],c['cream'],suit)
    for s in [-1,1]:
        patch('folded_lapel',[(s*.09,-.35,1.06),(s*.22,-.30,.96),(s*.08,-.364,.80),(s*.13,-.352,.98)],c['pants'],suit)
        oval('jacket_sleeve',(s*.35,-.005,1.01),(.125,.16,.115),c['cloth'],suit,12,8)
    patch('crooked_tie',[(-.025,-.369,1.02),(.025,-.369,1.02),(.05,-.372,.80),(.0,-.373,.75),(-.025,-.372,.80)],c['accent'],suit)
    stains=empty('Outfit_Stains',deform)
    for x,z,r in [(-.17,.82,.065),(.15,.97,.045),(.11,.73,.037)]:
        y=-.344*math.sqrt(max(.01,1-((z-.78)/.49)**2-(x/.405)**2))-.008
        oval('cloth_stain',(x,y,z),(r,.011,r*.7),mat('fixed_stain','685C48'),stains,10,7)
    pattern=empty('Outfit_Pattern',deform)
    for z in [.74,.84,.94,1.04]:
        # Surface-fitted narrow horizontal knit bands, distinct from marks on skin.
        shell('knit_band',pattern,c['accent'],z,z+.016,rx=.39,ry=.32,steps=1)
    eye=empty('Gear_Eyepatch',head)
    oval('fabric_eyepatch',(.19,-.418,1.47),(.13,.035,.13),c['ink'],eye,16,10)
    tube('eyepatch_strap',[(-.43,-.22,1.58),(.04,-.411,1.51),(.42,-.30,1.42)],[.015]*3,c['ink'],eye,6)
    return root


def module_for(obj):
    node=obj
    while node:
        if node.type=='EMPTY' and node.name.startswith(('Outfit_','Gear_')):return node.name
        node=node.parent
    return None


def consolidate(kind):
    # A mesh per animated pivot or optional module; material surfaces remain semantic.
    groups={}
    for o in list(bpy.context.scene.objects):
        if o.type=='MESH':groups.setdefault(o.parent,[]).append(o)
    for parent,objects in groups.items():
        if len(objects)<2:continue
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
        objects[0].name=parent.name+'_mesh'
    bpy.context.view_layer.update()


def audit(kind):
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    tri=lambda o:sum(len(p.vertices)-2 for p in o.data.polygons)
    modules={}
    for o in meshes:
        key=module_for(o) or 'base';modules[key]=modules.get(key,0)+tri(o)
        assert all(math.isfinite(v) for p in o.data.vertices for v in p.co),(kind,o.name,'nonfinite')
        assert o.data.materials,(kind,o.name,'missing material')
    expected=['Root','Body','Deform','Head','LeftFoot','RightFoot','Weapon','Muzzle',f'common_{kind}_leftArm',f'common_{kind}_rightArm']
    for name in expected:assert bpy.data.objects.get(name),(kind,'missing pivot',name)
    points=[o.matrix_world@v.co for o in meshes if module_for(o) is None for v in o.data.vertices]
    bounds=[[round(min(p[i] for p in points),4),round(max(p[i] for p in points),4)] for i in range(3)]
    assert -.03<bounds[2][0]<.03,(kind,'sole origin',bounds)
    assert 1.7<bounds[2][1]<2.4,(kind,'height',bounds)
    assert modules['base']<8000,(kind,'crowd base budget',modules)
    return {'species':kind,'triangles_total':sum(tri(o) for o in meshes),'triangles_by_module':modules,
            'mesh_count':len(meshes),'materials':sorted({m.name for o in meshes for m in o.data.materials}),
            'bounds_blender_xyz':bounds,'required_pivots':expected}


def optional_visibility(selected=()):
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':obj.hide_render=module_for(obj) is not None and module_for(obj) not in selected


def stage():
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=480;scene.render.resolution_y=560;scene.render.resolution_percentage=100
    scene.world.color=(.3,.3,.3)
    scene.view_settings.view_transform='AgX'
    floor=mat('Review floor','D8D7CB')
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,-.016));bpy.context.object.data.materials.append(floor)
    for name,loc,energy,size in [('Key',(-3,-4,6),650,4),('Fill',(4,-2,3),380,4),('Rim',(2,4,5),800,3)]:
        d=bpy.data.lights.new(name,'AREA');o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o)
        o.location=loc;d.energy=energy;d.shape='DISK';d.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('ReviewCamera');o=bpy.data.objects.new('ReviewCamera',d);bpy.context.collection.objects.link(o)
    d.type='ORTHO';d.ortho_scale=2.8;scene.camera=o


def render(kind,view,angle,selected=()):
    optional_visibility(selected);scene=bpy.context.scene
    scene.camera.location=(4*math.sin(angle),-4*math.cos(angle),2.25)
    scene.camera.rotation_euler=(Vector((0,0,1.10))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'{kind}-{view}.png');bpy.ops.render.render(write_still=True)


def resource(kind):
    source='''[gd_resource type="Resource" script_class="CharacterDefinition" load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/Characters/CharacterDefinition.cs" id="1_def"]
[ext_resource type="PackedScene" path="res://assets/common-monsters/SPECIES.glb" id="2_model"]

[resource]
script = ExtResource("1_def")
Id = "common_SPECIES"
DisplayName = "name_monster_common_SPECIES"
Description = "Shared common roster model; optional modules selected by immutable spawn identity."
Role = 1
Model = ExtResource("2_model")
ModelFacesPositiveZ = true
BodyNode = "Body"
DeformNode = "Deform"
HeadNode = "Head"
LeftFootNode = "LeftFoot"
RightFootNode = "RightFoot"
WeaponNode = "Weapon"
MuzzleNode = "Muzzle"
HandColor = Color(0.66, 0.58, 0.42, 1)
ColliderRadius = 0.43
ColliderHeight = 1.8
StepRate = 13.0
WaddleDegrees = 6.0
StuffingColor = Color(0.87, 0.83, 0.67, 1)
'''.replace('SPECIES',kind)
    if kind=='parrot':source=source.replace('WaddleDegrees = 6.0\n','WaddleDegrees = 6.0\nHeadSpringScale = 0.05\n')
    (ROOT/f'game/content/enemies/common_{kind}.tres').write_text(source,encoding='utf-8')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--species',choices=SPECIES,nargs='+',default=SPECIES)
    parser.add_argument('--no-render',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    untouched={p:hashlib.sha256(p.read_bytes()).hexdigest() for p in ASSETS.glob('*.glb') if p.stem not in args.species}
    reports=[]
    for kind in args.species:
        build_one(kind,reports,args.no_render)
    for path,digest in untouched.items():
        assert hashlib.sha256(path.read_bytes()).hexdigest()==digest,('unselected asset changed',path)
    report_path=OUT/('audit.json' if set(args.species)==set(SPECIES) else 'audit-'+'-'.join(args.species)+'.json')
    report_path.write_text(json.dumps(reports,indent=2),encoding='utf-8')
    print('COMMON_MONSTERS_COMPLETE',len(reports),'UNSELECTED_UNCHANGED',len(untouched))


def build_one(kind,reports,no_render):
    clear();root=zombie() if kind=='zombie' else animal(kind)
    consolidate(kind);reports.append(audit(kind));resource(kind)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{kind}.blend'))
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/f'{kind}.glb'),export_format='GLB',export_yup=True,
                             export_animations=False,export_cameras=False,export_lights=False)
    if not no_render:
        stage()
        default=('Outfit_Jeans','Outfit_Shirt','Outfit_Stains') if kind=='zombie' else ()
        for view,angle in [('front',0),('quarter',math.pi/4),('side',math.pi/2),('back',math.pi)]:render(kind,view,angle,default)
        if kind!='zombie':render(kind,'armed',math.pi/4,('Gear_Vest',))
        else:
            for title,mods in [('chinos',('Outfit_Chinos','Outfit_Shirt','Outfit_Pattern')),
                               ('torn',('Outfit_Torn','Outfit_Shirt','Outfit_Stains')),
                               ('overalls',('Outfit_Overalls',)),('suit',('Outfit_Suit',)),
                               ('bare',('Outfit_Shirt',)),('pattern',('Outfit_Jeans','Outfit_Shirt','Outfit_Pattern'))]:render(kind,title,math.pi/4,mods)
    print('COMMON_ASSET_AUDIT',json.dumps(reports[-1]))
if __name__=='__main__':main()
