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


PARROT_BODY=[(.29,.045,.045,.025),(.35,.19,.16,.025),(.44,.29,.24,.015),
             (.56,.355,.292,.005),(.70,.38,.315,0),(.84,.365,.30,0),
             (.97,.325,.265,.012),(1.08,.26,.215,.015),(1.17,.19,.16,.01),
             (1.24,.10,.09,0),(1.27,.01,.01,0)]


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


def parrot_belly(c,deform):
    # Dense surface sampling keeps the panel outside triangulated body faces.
    # Six mm clearance reads as a sewn panel, rather than the old 75 mm belly lobe.
    verts=[];faces=[];n=32
    for j in range(13):
        r=max(.001,j/12)
        for k in range(n):
            a=math.tau*k/n;z=.765+.295*r*math.cos(a);x=.248*r*math.sin(a)
            rx,ry,cy=parrot_body_section(z)
            verts.append((x,cy-ry*math.sqrt(max(0,1-(x/rx)**2))-.006,z))
    for j in range(12):
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    mesh=bpy.data.meshes.new('flush_belly_panel');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('flush_belly_panel',mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(c['secondary'])
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for p in mesh.polygons:p.use_smooth=True
    parent_world(obj,deform)


def parrot():
    c=colors('parrot');root,deform,head,left,right,arms=frame('parrot')
    loft('pear_plush_body',PARROT_BODY,c['primary'],deform,32)
    parrot_belly(c,deform)
    # The lower head is a tapered neck seated inside the torso, with a softly broad brow.
    loft('shaped_parrot_head',[(1.095,.115,.12,-.015),(1.15,.21,.20,-.015),
         (1.24,.31,.275,-.025),(1.36,.395,.325,-.035),(1.50,.425,.34,-.025),
         (1.63,.405,.325,-.005),(1.74,.335,.275,.01),(1.82,.235,.195,.025),
         (1.87,.11,.10,.035),(1.885,.01,.01,.035)],c['primary'],head,32)
    for s in [-1,1]:
        # Thin inset-looking button surround follows the sloping face; no cheek spheres.
        oval('compact_eye_rim',(s*.222,-.307,1.535),(.088,.025,.103),c['cream'],head,16,10,rot=(0,0,s*.38))
        oval('button_eye',(s*.222,-.328,1.54),(.050,.024,.065),c['eye'],head,16,10,rot=(0,0,s*.38))
        oval('eye_glint',(s*.222-.012,-.349,1.561),(.013,.006,.016),c['glint'],head,8,6)
    feather('sculpted_upper_beak',[(0,-.303,1.415),(0,-.385,1.44),(0,-.49,1.408),
            (0,-.565,1.335),(0,-.574,1.265),(0,-.552,1.235)],
            [.108,.128,.109,.070,.034,.004],[.089,.096,.081,.063,.029,.003],c['horn'],head,16)
    feather('lower_beak',[(0,-.315,1.29),(0,-.405,1.275),(0,-.49,1.279),(0,-.533,1.295)],
            [.080,.089,.061,.006],[.046,.041,.026,.004],c['ink'],head,12)
    for s in [-1,1]:
        oval('beak_nostril',(s*.075,-.465,1.469),(.013,.009,.010),c['ink'],head,8,6)
    for i in [-1,0,1]:
        cresttop=2.025 if i==0 else 1.965
        feather('swept_crest',[(i*.085,.015,1.79),(i*.105,.085,1.88),
                (i*.12,.19,cresttop-.025),(i*.13,.30,cresttop)],
                [.047,.065,.049,.004],[.031,.033,.022,.003],c['accent'],head,10)
    for sign,foot,arm in zip([-1,1],[left,right],arms):
        oval('soft_ankle',(sign*.20,.005,.295),(.105,.115,.155),c['primary'],foot,12,8)
        oval('padded_bird_foot',(sign*.21,-.09,.125),(.145,.205,.125),c['gold'],foot,16,10)
        for x in [-.044,.044]:seam(foot,c,[(sign*.21+x,-.273,.12),(sign*.21+x,-.235,.196)])
        feather('wing_shoulder',[(sign*.355,.005,1.095),(sign*.435,-.035,1.015),
                (sign*.47,-.04,.85),(sign*.445,-.018,.68)],
                [.055,.127,.128,.047],[.07,.115,.10,.031],c['primary'],arm,16)
        for j in range(3):
            x=sign*(.391+j*.070)
            feather('wing_flight_feather',[(x,-.10,.91-j*.028),(x+sign*.022,-.145,.81-j*.036),
                    (x+sign*.015,-.12,.68-j*.028),(x-sign*.012,-.055,.62-j*.018)],
                    [.041,.053,.044,.005],[.028,.038,.027,.003],c['accent'],arm,10)
    tail=empty('common_parrot_tail',deform,(0,.23,.62))
    for i in [-1,0,1]:
        feather('swept_tail_feather',[(i*.055,.20,.66),(i*.085,.36,.57),
                (i*.12,.50,.42),(i*.14,.56,.30+abs(i)*.035)],
                [.048,.077,.061,.006],[.037,.040,.026,.004],c['accent'],tail,12)
    vest=empty('Gear_Vest',deform)
    profile=[]
    for z in [.48,.57,.70,.84,.97,1.075]:
        rx,ry,cy=parrot_body_section(z);profile.append((z,rx+.025,ry+.027,cy))
    loft('fitted_padded_vest',profile,c['vest'],vest,32)
    for s in [-1,1]:
        oval('vest_front_pouch',(s*.145,-.306,.76),(.115,.065,.145),c['vest'],vest,12,8,rot=(0,0,s*.28))
        tube('vest_shoulder',[(s*.205,-.19,1.06),(s*.205,0,1.155),(s*.205,.20,1.055)],
             [.042,.047,.042],c['vest'],vest,8)
    tube('vest_front_closure',[(0,-.300,.52),(0,-.346,.70),(0,-.331,.84),(0,-.287,.98),(0,-.239,1.065)],
         [.009]*5,c['ink'],vest,6)
    # Keep the optional legacy node contract; current roster never enables eye gear.
    glasses=empty('Gear_Glasses',head)
    for s in [-1,1]:oval('sunglass_lens',(s*.22,-.372,1.535),(.108,.02,.105),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.11,-.372,1.55),(.11,-.372,1.55)],[.013,.013],c['ink'],glasses)
    return root



def eagle():
    """Broad, quiet plush eagle shapes; compact feather caps suit authored-wing grips."""
    c=colors('eagle');root,deform,head,left,right,arms=frame('eagle')
    body=[(.27,.13,.13,.015),(.37,.25,.23,.01),(.52,.34,.285,.005),
          (.72,.385,.31,0),(.90,.365,.29,.005),(1.06,.28,.235,.015),
          (1.19,.19,.17,.015),(1.25,.10,.10,.015)]
    loft('eagle_plush_body',body,c['primary'],deform,32)
    loft('eagle_head_neck',[(1.025,.16,.15,.015),(1.12,.245,.21,.005),
         (1.25,.32,.27,-.005),(1.40,.405,.325,-.015),(1.55,.415,.335,-.005),
         (1.68,.37,.295,.015),(1.78,.28,.225,.035),(1.845,.15,.13,.045),
         (1.865,.015,.015,.045)],c['cream'],head,32)
    # A broad overlapping collar replaces the old row of detached neck beads.
    for i in range(7):
        a=(i-3)*.38;x=math.sin(a)*.275;y=-math.cos(a)*.218
        feather('padded_neck_ruff',[(x,y,1.32),(x*1.12,y*1.08,1.22),
                (x*1.08,y*1.04,1.105+abs(i-3)*.016)],
                [.065,.080,.012],[.035,.042,.008],c['cream'],head,10)
    for sign in [-1,1]:
        oval('compact_eye_rim',(sign*.215,-.299,1.546),(.073,.023,.079),c['gold'],head,12,8,rot=(0,0,sign*.35))
        oval('button_eye',(sign*.215,-.319,1.546),(.043,.023,.052),c['eye'],head,12,8,rot=(0,0,sign*.35))
        oval('eye_glint',(sign*.215-.01,-.34,1.561),(.011,.005,.012),c['glint'],head,8,6)
        oval('soft_stern_brow',(sign*.227,-.302,1.614),(.125,.041,.045),c['cream'],head,14,8,rot=(0,sign*-.16,sign*.20))
    feather('golden_hooked_beak',[(0,-.296,1.46),(0,-.393,1.467),(0,-.498,1.424),
            (0,-.563,1.355),(0,-.559,1.283),(0,-.532,1.258)],
            [.102,.12,.098,.062,.027,.004],[.077,.085,.069,.051,.022,.003],c['gold'],head,16)
    feather('lower_mandible',[(0,-.30,1.345),(0,-.412,1.345),(0,-.506,1.348)],
            [.077,.083,.008],[.037,.038,.005],c['gold'],head,12)
    for sign in [-1,1]:
        oval('nostril',(sign*.07,-.451,1.491),(.013,.009,.01),c['ink'],head,8,6)
    for sign,foot,arm in zip([-1,1],[left,right],arms):
        oval('soft_ankle',(sign*.205,.005,.29),(.11,.12,.16),c['primary'],foot,12,8)
        oval('padded_talon_foot',(sign*.215,-.10,.125),(.157,.215,.125),c['gold'],foot,16,10)
        for dx in [-.049,.049]:seam(foot,c,[(sign*.215+dx,-.29,.12),(sign*.215+dx,-.245,.198)])
        # Narrow upper/lower 12% caps prevent the posed fan projecting past anchors.
        feather('broad_wing_coverts',[(sign*.365,0,1.105),(sign*.447,-.025,1.025),
                (sign*.485,-.035,.84),(sign*.46,-.025,.685)],
                [.040,.135,.145,.045],[.044,.10,.088,.024],c['primary'],arm,16)
        for j in range(4):
            x=sign*(.385+j*.064)
            feather('layered_flight_feather',[(x,-.102,.935-j*.025),
                    (x+sign*.018,-.13,.82-j*.023),(x+sign*.008,-.098,.70-j*.017),
                    (sign*.451,-.035,.615+j*.004)],
                    [.036,.048,.044,.005],[.026,.033,.026,.003],
                    c['secondary'] if j%2==0 else c['primary'],arm,10)
    tail=empty('common_eagle_tail',deform,(0,.23,.62))
    for i in [-1,0,1]:
        feather('broad_tail_fan',[(i*.055,.20,.65),(i*.09,.33,.55),
                (i*.12,.42,.40),(i*.12,.445,.355+abs(i)*.025)],
                [.053,.080,.073,.022],[.035,.04,.025,.01],c['secondary'],tail,12)
    vest=empty('Gear_Vest',deform)
    loft('fitted_eagle_vest',[(z,rx+.024,ry+.028,cy) for z,rx,ry,cy in body[2:6]],c['vest'],vest,32)
    for sign in [-1,1]:
        oval('vest_front_pouch',(sign*.15,-.31,.76),(.115,.06,.145),c['vest'],vest,12,8,rot=(0,0,sign*.25))
        tube('vest_shoulder',[(sign*.205,-.19,1.045),(sign*.205,0,1.14),(sign*.205,.19,1.04)],
             [.043,.047,.043],c['vest'],vest,8)
    tube('vest_closure',[(0,-.316,.54),(0,-.348,.72),(0,-.326,.90),(0,-.252,1.06)],[.009]*4,c['ink'],vest,6)
    glasses=empty('Gear_Glasses',head)
    for sign in [-1,1]:oval('sunglass_lens',(sign*.22,-.36,1.55),(.108,.02,.10),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.11,-.36,1.55),(.11,-.36,1.55)],[.013,.013],c['ink'],glasses)
    return root

def lion_mane(c,head):
    """One continuous padded ruff with broad swept ridges, never a bead necklace."""
    n=48;verts=[];faces=[]
    # Inner lip seats under face, outward folds crest then turn into the back cap.
    profiles=[(.60,-.02),(.80,-.185),(.94,-.105),(1.0,.035),(.94,.22),(.74,.32),(.43,.36),(.015,.37)]
    for j,(r,y) in enumerate(profiles):
        for k in range(n):
            a=math.tau*k/n
            wave=math.cos(9*a+.32*j)
            fold=(.014+.019*math.sin(math.pi*j/(len(profiles)-1)))*wave*r
            # Top crown is generous; lower ruff narrows into the neck.
            rx=.575*r+fold;rz=.555*r+fold
            x=rx*math.sin(a);z=1.465+rz*math.cos(a)
            if math.cos(a)<0:x*=.90
            verts.append((x,y-.018*wave*math.sin(math.pi*j/7),z))
    for j in range(len(profiles)-1):
        for k in range(n):
            a=j*n+k;b=j*n+(k+1)%n;faces.append((a,b,b+n,a+n))
    faces.append(tuple(range((len(profiles)-1)*n,len(profiles)*n)))
    mesh=bpy.data.meshes.new('continuous_swept_mane');mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new('continuous_swept_mane',mesh);bpy.context.collection.objects.link(obj)
    obj.data.materials.append(c['mane'])
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    for p in mesh.polygons:p.use_smooth=True
    parent_world(obj,head)


def lion():
    c=colors('lion');root,deform,head,left,right,arms=frame('lion')
    body=[(.27,.12,.12,.02),(.36,.26,.225,.015),(.51,.355,.29,.01),
          (.70,.395,.32,.005),(.88,.38,.305,.01),(1.04,.31,.25,.02),
          (1.17,.225,.19,.025),(1.24,.12,.12,.025)]
    loft('stout_lion_body',body,c['primary'],deform,32)
    lion_mane(c,head)
    loft('soft_lion_face',[(1.105,.13,.14,-.005),(1.19,.25,.225,-.025),
         (1.32,.365,.30,-.04),(1.48,.407,.32,-.04),(1.63,.38,.30,-.025),
         (1.75,.30,.235,-.005),(1.83,.18,.15,.015),(1.855,.01,.01,.02)],c['primary'],head,32)
    for s in [-1,1]:
        oval('round_lion_ear',(s*.355,-.065,1.81),(.125,.075,.13),c['primary'],head,14,8,rot=(0,s*.2,0))
        oval('ear_inset',(s*.355,-.128,1.815),(.071,.018,.076),c['mane'],head,12,8)
        oval('compact_button_eye',(s*.193,-.326,1.545),(.045,.026,.057),c['eye'],head,16,10,rot=(0,0,s*.28))
        oval('eye_glint',(s*.193-.011,-.350,1.562),(.011,.005,.014),c['glint'],head,8,6)
        oval('soft_brow',(s*.195,-.308,1.618),(.096,.031,.032),c['primary'],head,12,8,rot=(0,s*-.1,s*.1))
        oval('integrated_muzzle',(s*.108,-.327,1.335),(.153,.125,.112),c['cream'],head,16,10)
        for i in range(2):
            oval('whisker_stitch',(s*(.14+i*.045),-.443+i*.018,1.35),(.009,.006,.009),c['mane'],head,8,6)
    oval('soft_chin',(0,-.299,1.255),(.159,.105,.068),c['cream'],head,16,10)
    feather('rounded_triangle_nose',[(0,-.413,1.421),(0,-.455,1.405),(0,-.453,1.363)],
            [.068,.076,.018],[.029,.035,.012],c['ink'],head,12)
    tube('nose_seam',[(0,-.457,1.372),(0,-.455,1.319)],[.008,.008],c['ink'],head,6)
    tube('quiet_smile',[(-.10,-.429,1.317),(-.056,-.446,1.299),(0,-.454,1.318),(.056,-.446,1.299),(.10,-.429,1.317)],
         [.007]*5,c['ink'],head,6)
    for s,foot,arm in zip([-1,1],[left,right],arms):
        # One tapered thigh/ankle surface; ankle enters the padded shoe upper.
        leg=loft('tapered_leg',[(.105,.083,.095,-.005),(.18,.083,.09,.005),
             (.245,.094,.104,.012),(.33,.125,.129,.018),(.405,.128,.132,.018),
             (.46,.10,.105,.018)],c['primary'],foot,16)
        for v in leg.data.vertices:v.co.x+=s*.205
        # Broad closed sole at z=0 with a flatter front and rounded upper/toe.
        shoe=loft('planted_foot',[(0,.147,.193,-.10),(.024,.174,.222,-.10),
             (.075,.18,.23,-.10),(.135,.168,.212,-.09),(.185,.131,.159,-.065),
             (.215,.075,.092,-.025)],c['primary'],foot,20)
        for v in shoe.data.vertices:
            v.co.x+=s*.22
            v.co.y=max(v.co.y,-.305)
        for poly in shoe.data.polygons:
            if all(abs(shoe.data.vertices[i].co.z)<1e-6 for i in poly.vertices):poly.use_smooth=False
        # Forearm terminates at wrist; only this fabric section stretches in-game.
        forearm=loft('forearm',[(.785,.067,.063,-.025),(.84,.082,.077,-.02),
             (.92,.105,.09,-.008),(.98,.072,.064,0),(.99,.025,.025,0)],c['primary'],arm,16)
        for v in forearm.data.vertices:v.co.x+=s*(.435-.045*max(0,min(1,(v.co.z-.80)/.19)))
        forearm.name=arm.name+'_mesh'
        side='left' if s<0 else 'right'
        paw_node=empty(f'common_lion_{side}Paw')
        parent_world(paw_node,arm)
        wrist=empty(f'common_lion_{side}Wrist',paw_node)
        wrist.location=(s*.435,-.025,.80)
        grip=empty(f'common_lion_{side}Grip',paw_node)
        grip.location=(s*.435,-.065,.685)
        paw=loft('closed_grip_paw',[(.60,.035,.035,-.045),(.625,.08,.065,-.055),
             (.665,.105,.082,-.065),(.725,.108,.077,-.055),(.77,.079,.060,-.035),
             (.815,.062,.059,-.025)],c['primary'],paw_node,16)
        for v in paw.data.vertices:v.co.x+=s*.435
        paw.name=paw_node.name+'_mesh'
        # Small tucked thumb and compact palm patch read as a closed mitten grip.
        oval('tucked_thumb',(s*.365,-.098,.71),(.046,.05,.055),c['primary'],paw_node,12,8,rot=(0,s*.35,0))
        oval('soft_palm_pad',(s*.435,-.136,.678),(.061,.012,.044),c['secondary'],paw_node,12,8)
    tail=empty('common_lion_tail',deform,(0,.24,.60))
    tube('soft_curled_tail',[(0,.24,.60),(0,.45,.49),(.17,.51,.54),(.25,.47,.73)],
         [.065,.058,.048,.035],c['primary'],tail,12)
    oval('lion_tail_tuft',(.25,.47,.75),(.09,.08,.115),c['mane'],tail,14,10)
    vest=empty('Gear_Vest',deform)
    loft('fitted_lion_vest',[(z,rx+.022,ry+.026,cy) for z,rx,ry,cy in body[2:6]],c['vest'],vest,32)
    for s in [-1,1]:
        oval('vest_pocket',(s*.15,-.321,.74),(.112,.045,.13),c['vest'],vest,12,8,rot=(0,0,s*.22))
        tube('vest_strap',[(s*.225,-.20,1.03),(s*.225,0,1.13),(s*.225,.20,1.035)],[.043]*3,c['vest'],vest,8)
    tube('vest_closure',[(0,-.335,.52),(0,-.351,.70),(0,-.337,.88),(0,-.257,1.04)],[.009]*4,c['ink'],vest,6)
    glasses=empty('Gear_Glasses',head)
    for s in [-1,1]:oval('sunglass_lens',(s*.20,-.37,1.55),(.105,.02,.10),c['ink'],glasses,12,8)
    tube('nose_bridge',[(-.10,-.37,1.55),(.10,-.37,1.55)],[.013,.013],c['ink'],glasses)
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
    # Keep the recognizable source face, ear silhouette, asymmetry, teeth and mitten arms.
    bpy.data.objects.remove(bpy.data.objects['Deform_mesh'],do_unlink=True)
    replacements={'infected_skin':c['skin'],'infected_cloth':c['skin'],'infected_trim':c['ink'],
                  'infected_cream':c['cream'],'infected_ink':c['ink'],'infected_mouth':mat('fixed_mouth','5E3D46')}
    for obj in list(bpy.context.scene.objects):
        if obj.name.startswith('humanoid_'):obj.name=obj.name.replace('humanoid_','common_zombie_')
        if obj.type=='MESH':
            for i,m in enumerate(obj.data.materials):obj.data.materials[i]=replacements.get(m.name,m)
            for p in obj.data.polygons:p.use_smooth=True
    oval('plush_human_torso',(0,0,.78),(.38,.31,.49),c['skin'],deform,20,12)
    for s in [-1,1]:oval('bare_leg',(s*.20,0,.34),(.16,.21,.24),c['skin'],deform,16,10)
    # Visible sewn scar is fabric repair, not a body tattoo; jaw patch is skin-toned.
    oval('soft_lower_jaw',(0,-.255,1.11),(.25,.11,.11),c['skin'],head,16,10)
    for s in [-1,1]:
        oval('human_eye_glint',(s*.19-.018,-.438,1.51),(.014,.009,.018),c['glint'],head,8,6)
    for name,style in [('Jeans','jeans'),('Chinos','chinos'),('Torn','torn')]:
        m=empty('Outfit_'+name,deform);trousers(m,c,style)
        if style=='chinos':
            for s in [-1,1]:tube('pressed_crease',[(s*.20,-.234,.20),(s*.20,-.24,.49)],[.005,.005],c['seam'],m,5)
    shirt=empty('Outfit_Shirt',deform)
    shell('short_shirt',shirt,c['cloth'],.66,1.11,rx=.397,ry=.327)
    for s in [-1,1]:
        oval('shirt_cap_sleeve',(s*.34,0,1.04),(.16,.22,.125),c['cloth'],shirt,12,8)
    tube('shirt_hem',[(-.27,-.25,.68),(0,-.337,.68),(.27,-.25,.68)],[.011]*3,c['seam'],shirt,6)
    overalls=empty('Outfit_Overalls',deform);trousers(overalls,c,'jeans')
    shell('overall_bib',overalls,c['pants'],.62,.94,rx=.397,ry=.327)
    for s in [-1,1]:
        tube('overall_strap',[(s*.17,-.32,.84),(s*.23,-.21,1.08),(s*.23,.15,1.10),(s*.17,.31,.80)],[.043]*4,c['pants'],overalls,8)
        oval('brass_button',(s*.17,-.344,.89),(.024,.012,.024),c['gold'],overalls,8,6)
    patch('bib_pocket',[(-.105,-.345,.78),(.105,-.345,.78),(.105,-.34,.88),(-.105,-.34,.88)],c['cloth'],overalls)
    suit=empty('Outfit_Suit',deform);trousers(suit,c,'chinos')
    shell('suit_jacket',suit,c['cloth'],.57,1.11,rx=.405,ry=.337)
    patch('shirt_front',[(-.11,-.33,1.045),(.11,-.33,1.045),(.07,-.357,.76),(-.07,-.357,.76)],c['cream'],suit)
    for s in [-1,1]:
        patch('folded_lapel',[(s*.09,-.35,1.06),(s*.22,-.30,.96),(s*.08,-.364,.80),(s*.13,-.352,.98)],c['pants'],suit)
        oval('jacket_sleeve',(s*.35,0,1.03),(.18,.23,.16),c['cloth'],suit,12,8)
    patch('crooked_tie',[(-.025,-.369,1.02),(.025,-.369,1.02),(.05,-.372,.80),(.0,-.373,.75),(-.025,-.372,.80)],c['accent'],suit)
    stains=empty('Outfit_Stains',deform)
    for x,z,r in [(-.17,.82,.065),(.15,.97,.045),(.11,.73,.037)]:
        y=-.344*math.sqrt(max(.01,1-((z-.78)/.49)**2-(x/.405)**2))-.008
        oval('cloth_stain',(x,y,z),(r,.011,r*.7),mat('fixed_stain','685C48'),stains,10,7)
    pattern=empty('Outfit_Pattern',deform)
    for z in [.74,.84,.94,1.04]:
        # Surface-fitted narrow horizontal knit bands, distinct from marks on skin.
        shell('knit_band',pattern,c['accent'],z,z+.016,rx=.406,ry=.337,steps=1)
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
                               ('bare',('Outfit_Shirt',)),('eyepatch',('Outfit_Jeans','Outfit_Shirt','Gear_Eyepatch'))]:render(kind,title,math.pi/4,mods)
    print('COMMON_ASSET_AUDIT',json.dumps(reports[-1]))
if __name__=='__main__':main()
