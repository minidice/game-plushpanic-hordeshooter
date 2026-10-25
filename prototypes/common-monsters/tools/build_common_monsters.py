"""Deterministic crowd assets. Blender --background --python this_file.py -- [--no-render].

Authoring coordinates are Blender Z up / -Y forward. GLB exports Y up / +Z forward.
The source humanoid head and arms are imported, never overwritten. Clothing and gear
are explicit optional ancestor nodes; runtime selects them before displaying a rig.
"""
import bpy
import bmesh
import json
import math
import sys
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
        if kind=='lion' and parent.name=='Head':
            # The dense ring of mane lobes dominates this one species' crowd cost.
            mod=objects[0].modifiers.new('Mane crowd budget','DECIMATE');mod.ratio=.78
            bpy.ops.object.modifier_apply(modifier=mod.name)
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


reports=[]
for kind in SPECIES:
    clear();root=zombie() if kind=='zombie' else animal(kind)
    consolidate(kind);reports.append(audit(kind));resource(kind)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{kind}.blend'))
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/f'{kind}.glb'),export_format='GLB',export_yup=True,
                             export_animations=False,export_cameras=False,export_lights=False)
    if '--no-render' not in sys.argv:
        stage()
        default=('Outfit_Jeans','Outfit_Shirt','Outfit_Stains') if kind=='zombie' else ()
        for view,angle in [('front',0),('quarter',math.pi/4),('side',math.pi/2),('back',math.pi)]:render(kind,view,angle,default)
        if kind!='zombie':render(kind,'armed',math.pi/4,('Gear_Vest','Gear_Glasses'))
        else:
            for title,mods in [('chinos',('Outfit_Chinos','Outfit_Shirt','Outfit_Pattern')),
                               ('torn',('Outfit_Torn','Outfit_Shirt','Outfit_Stains')),
                               ('overalls',('Outfit_Overalls',)),('suit',('Outfit_Suit',)),
                               ('bare',('Outfit_Shirt',)),('eyepatch',('Outfit_Jeans','Outfit_Shirt','Gear_Eyepatch'))]:render(kind,title,math.pi/4,mods)
    print('COMMON_ASSET_AUDIT',json.dumps(reports[-1]))
(OUT/'audit.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('COMMON_MONSTERS_COMPLETE',len(reports))
