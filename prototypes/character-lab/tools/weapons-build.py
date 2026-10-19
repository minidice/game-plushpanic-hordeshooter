"""Blender --background --python weapons-build.py. Original stylized game assets.

Author coordinates are Godot metres (+Y up, +Z barrel), mapped to Blender
at construction. Geometry is deliberately authored without hand meshes.
"""
import bpy
import csv
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'prototypes/character-lab/output/weapons'
ASSETS = ROOT / 'game/assets/weapons'
OUT.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

def material(name, hexcolor, metal=0, rough=.6):
    rgb = [int(hexcolor[i:i+2], 16)/255 for i in (0,2,4)]
    # Hex values represent display sRGB, Blender node inputs are linear.
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Roughness'].default_value = rough
    return mat

steel = material('Cerakote graphite', '414950', .45, .38)
dark = material('Recesses and rubber', '171E25', .05, .8)
edge = material('Machined edges', '8B9A9E', .65, .32)
glass = material('Optic blue glass', '49BCD1', .45, .2)
brass = material('Warm brass details', 'C39B54', .55, .36)
parts = []
scale = 1
accent = None

def V(p):
    return Vector((p[0]*scale, -p[2]*scale, p[1]*scale))

def finish(obj, name, mat, bevel=0):
    obj.name = name
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Soft manufactured edge', 'BEVEL')
        mod.width = bevel*scale
        mod.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    mod = obj.modifiers.new('Face weighted normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(obj)
    return obj

def box(name, at, size, mat, bevel=.009, tilt=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=V(at))
    obj=bpy.context.object
    obj.dimensions = (size[0]*scale, size[2]*scale, size[1]*scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.rotation_euler.x=math.radians(tilt)
    return finish(obj,name,mat,bevel)

def rod(name, a, b, radius, mat, radius2=None, vertices=12):
    start,end=V(a),V(b)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius*scale,
        radius2=(radius if radius2 is None else radius2)*scale, depth=(end-start).length,
        location=(start+end)/2)
    obj=bpy.context.object
    obj.rotation_euler=(end-start).to_track_quat('Z','Y').to_euler()
    return finish(obj,name,mat)

def tube(name, a, b, radius, mat, inner=.65):
    # Four rings create a real hollow bore, rather than a solid cylinder cap.
    start,end=V(a),V(b)
    axis=(end-start).normalized()
    u=axis.cross(Vector((1,0,0)))
    if u.length < .1: u=axis.cross(Vector((0,1,0)))
    u.normalize(); v=axis.cross(u)
    verts=[]
    for center,r in [(start,radius),(end,radius),(end,radius*inner),(start,radius*inner)]:
        for i in range(16):
            p=center+(u*math.cos(i*math.tau/16)+v*math.sin(i*math.tau/16))*r*scale
            verts.append(p)
    faces=[]
    for k in range(4):
        for i in range(16):
            faces.append((k*16+i,k*16+(i+1)%16,((k+1)%4)*16+(i+1)%16,((k+1)%4)*16+i))
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj)
    return finish(obj,name,mat)

def rail(z=.36, length=.30, y=.105):
    box('Optic rail', (0,y,z), (.065,.025,length),dark,.003)
    for i in range(5):
        box('Rail lug', (0,y+.016,z-length*.4+i*length*.2),(.078,.012,.017),edge,.002)

def grip(z=0):
    box('Pistol grip',(0,-.085,z),(.075,.17,.10),dark,.018,tilt=-14)
    box('Grip heel',(0,-.163,z-.018),(.082,.025,.10),accent,.007)
    # Open trigger guard, with trigger visibly inside.
    box('Trigger guard lower',(0,-.115,z+.105),(.025,.025,.13),steel,.007)
    box('Trigger guard front',(0,-.063,z+.158),(.025,.105,.022),steel,.006)
    rod('Curved trigger',(0,-.025,z+.08),(0,-.075,z+.06),.009,edge)

def stock(short=False):
    back=-.16 if short else -.25
    rod('Stock extension',(0,.015,back+.02),(0,.015,-.035),.026,steel)
    box('Shoulder stock',(0,-.005,back),(.10,.15,.17 if not short else .10),accent,.018)
    box('Butt rubber',(0,-.012,back-(.09 if not short else .06)),(.11,.16,.025),dark,.009)
    box('Cheek comb',(0,.074,back+.025),(.09,.036,.15),dark,.011)

def barrel(start=.52, end=1, radius=.027, y=.038):
    rod('Barrel',(0,y,start),(0,y,end-.055),radius,steel)
    tube('Muzzle crown',(0,y,end-.06),(0,y,end),radius*1.4,edge)
    rod('Bore shadow',(0,y,end-.065),(0,y,end-.06),radius*.8,dark)

def magazine(z=.26, lmg=False, curved=False):
    if lmg:
        box('Belt ammunition box',(0,-.16,z),(.23,.24,.24),accent,.026)
        box('Ammo box lid',(0,-.041,z),(.25,.035,.25),dark,.006)
        for i in range(5):
            rod('Visible belt cartridge',(.10+i*.018,-.025,z-.02),(.10+i*.018,-.025,z+.09),.013,brass)
    else:
        box('Magazine',(0,-.15,z),(.068,.24,.115),accent,.013,tilt=12 if curved else -5)
        box('Magazine foot',(0,-.27,z+(.024 if curved else -.009)),(.08,.028,.13),dark,.006)
        for side in [-1,1]:
            box('Magazine pressed groove',(side*.035,-.15,z),(.006,.13,.07),dark,.002)

def receiver(length=.50):
    box('Upper receiver',(0,.025,.19),(.115,.14,length),steel,.016)
    box('Lower receiver',(0,-.045,.13),(.10,.09,.30),accent,.012)
    box('Ejection recess',(.060,.033,.23),(.008,.045,.12),dark,.004)
    box('Bolt surface',(.066,.036,.23),(.009,.019,.084),edge,.003)
    rod('Charging handle',(-.06,.035,.12),(-.11,.035,.12),.014,edge)
    grip()

def optic(z=.30, large=True):
    for p in [z-.10,z+.10]:
        box('Scope mount',(0,.13,p),(.06,.07,.035),dark,.005)
    rod('Scope body',(0,.19,z-.19),(0,.19,z+.16),.039 if large else .028,dark)
    tube('Objective bell',(0,.19,z+.10),(0,.19,z+.22),.058 if large else .04,steel,.78)
    rod('Objective lens',(0,.19,z+.208),(0,.19,z+.212),.044 if large else .03,glass)
    tube('Eyepiece',(0,.19,z-.23),(0,.19,z-.15),.046,dark)
    rod('Elevation turret',(0,.21,z),(0,.267,z),.024,edge)
    rod('Windage turret',(.025,.19,z),(.07,.19,z),.02,dark)

def rifle(kind):
    receiver(); stock(); magazine(curved=kind=='ak47')
    box('Ventilated handguard',(0,.026,.56),(.13,.135,.32),accent,.016)
    for side in [-1,1]:
        for z in [.46,.53,.60,.67]:
            box('Cooling slot',(side*.066,.035,z),(.007,.035,.032),dark,.004)
    rail(.43,.42); barrel(.68)
    box('Front sight',(0,.115,.81),(.025,.12,.04),steel,.005)
    box('Rear sight',(0,.13,.07),(.065,.045,.032),dark,.005)
    if kind=='m16':
        box('Carry handle top',(0,.18,.25),(.047,.025,.26),dark,.006)
        for z in [.12,.38]: box('Carry handle post',(0,.13,z),(.035,.10,.03),steel,.004)

def smg():
    receiver(.37); stock(True); magazine(.21)
    box('Compact forend',(0,.02,.47),(.12,.135,.22),dark,.018)
    for z in [.39,.44,.49,.54]: box('Forend groove',(0,-.051,z),(.10,.012,.015),steel,.003)
    barrel(.56,end=1,radius=.03)
    # Suppressor gives the compact family a strongly different silhouette.
    tube('Integral suppressor',(0,.038,.65),(0,.038,1),.047,steel,.63)
    rail(.24,.24)
    tube('Front ring sight',(0,.115,.63),(0,.115,.655),.032,edge,.65)

def pistol(kind):
    box('Slide',(0,.04,.43),(.23,.24,1.02),steel,.034)
    box('Polymer frame',(0,-.10,.28),(.20,.13,.72),accent,.026)
    box('Angled grip',(0,-.30,.04),(.22,.49,.29),dark,.034,tilt=-13)
    box('Grip inlay',(.115,-.31,.045),(.014,.31,.18),accent,.009,tilt=-13)
    box('Grip magazine base',(0,-.54,-.015),(.245,.055,.32),steel,.009)
    tube('Recessed muzzle',(0,.045,.94),(0,.045,1),.073,edge,.7)
    rod('Bore shadow',(0,.045,.915),(0,.045,.92),.055,dark)
    for z in [.04,.11,.18]:
        for side in [-1,1]: box('Slide serration',(side*.116,.045,z),(.01,.12,.018),dark,.002)
    box('Rear notch sight',(0,.182,.02),(.16,.04,.045),dark,.004)
    box('Front blade sight',(0,.184,.87),(.034,.04,.046),brass,.003)
    box('Ejection port',(.116,.08,.58),(.01,.095,.18),dark,.006)
    box('Trigger guard bottom',(0,-.33,.43),(.065,.055,.36),steel,.009)
    box('Trigger guard front',(0,-.23,.59),(.065,.22,.055),steel,.009)
    rod('Trigger',(0,-.13,.40),(0,-.26,.36),.025,edge)

def shotgun(double=False):
    receiver(.35); stock()
    if double:
        for x in [-.038,.038]:
            rod('Twin barrel',(x,.03,.32),(x,.03,.94),.034,steel)
            tube('Twin muzzle',(x,.03,.94),(x,.03,1),.037,edge,.75)
        box('Walnut forend',(0,-.022,.53),(.13,.085,.30),accent,.021)
        box('Break hinge',(0,-.028,.24),(.15,.12,.10),edge,.010)
    else:
        barrel(.35,radius=.031)
        rod('Magazine tube',(0,-.047,.25),(0,-.047,.91),.029,dark)
        box('Pump forend',(0,-.046,.59),(.14,.115,.28),accent,.023)
        for z in [.49,.54,.59,.64,.69]:
            box('Pump finger rib',(0,-.10,z),(.13,.018,.022),dark,.004)
    box('Bead sight',(0,.081,.94),(.014,.028,.02),brass,.003)

def sniper(kind):
    receiver(.48); stock(); magazine(.28)
    box('Precision forend',(0,-.016,.52),(.12,.11,.30),accent,.017)
    barrel(.56,radius=.029 if kind!='m82' else .040)
    optic()
    rod('Bolt stem',(.055,.04,.11),(.115,.02,.11),.015,edge)
    rod('Bolt knob',(.11,.02,.11),(.145,.02,.11),.025,dark)
    if kind=='m82':
        box('Recoil brake',(0,.038,.95),(.15,.09,.10),steel,.012)
        tube('Brake muzzle',(0,.038,.96),(0,.038,1),.031,edge,.75)
        for side in [-1,1]:
            rod('Folded bipod',(side*.06,-.05,.61),(side*.09,-.08,.85),.013,dark)

def lmg():
    receiver(.52); stock(); magazine(.27,True)
    box('Heat shield',(0,.04,.61),(.14,.15,.30),accent,.017)
    for side in [-1,1]:
        for z in [.50,.56,.62,.68,.74]: box('Heat vent',(side*.071,.05,z),(.008,.047,.025),dark,.003)
        rod('Folded bipod',(side*.07,-.04,.68),(side*.12,-.11,.93),.014,steel)
    barrel(.74,radius=.032); rail(.30,.28)
    box('Carry handle',(0,.22,.48),(.038,.035,.25),dark,.008)
    for z in [.36,.60]: rod('Handle riser',(0,.10,z),(0,.22,z),.014,steel)

def rocket():
    tube('Launcher tube',(0,.08,-.28),(0,.08,.77),.095,accent,.79)
    tube('Rear vent collar',(0,.08,-.32),(0,.08,-.20),.115,steel,.82)
    tube('Front collar',(0,.08,.62),(0,.08,.77),.11,steel,.83)
    for z in [.03,.42]: tube('Barrel band',(0,.08,z),(0,.08,z+.045),.102,dark,.8)
    box('Shoulder pad',(0,-.03,-.12),(.17,.07,.26),dark,.02)
    grip(.16)
    rod('Warhead neck',(0,.08,.70),(0,.08,.80),.054,steel)
    rod('Warhead body',(0,.08,.79),(0,.08,.90),.125,accent,radius2=.125)
    rod('Warhead ogive',(0,.08,.90),(0,.08,1),.125,brass,radius2=.012)
    box('Sight bracket',(-.13,.15,.28),(.035,.14,.05),steel,.005)
    rod('Side optic',(-.13,.22,.14),(-.13,.22,.35),.029,dark)
    rod('Optic glass',(-.13,.22,.35),(-.13,.22,.355),.022,glass)

def mortar():
    box('Baseplate',(0,-.13,.10),(.56,.075,.48),accent,.025)
    for x in [-.21,.21]: box('Baseplate strengthening rib',(x,-.081,.10),(.035,.035,.41),steel,.008)
    a=(0,-.04,0); b=(0,.74,.62)
    tube('Elevated mortar tube',a,b,.094,accent,.74)
    tube('Muzzle reinforcing ring',(0,.67,.564),b,.111,steel,.68)
    rod('Dark bore recess',(0,.55,.468),(0,.56,.477),.072,dark)
    for side in [-1,1]:
        rod('Bipod leg',(side*.07,.30,.28),(side*.32,-.09,.48),.023,steel)
        box('Bipod foot',(side*.32,-.105,.48),(.11,.025,.09),dark,.007)
    rod('Elevation screw',(0,-.03,.38),(0,.30,.28),.017,brass)
    rod('Traverse axle',(-.16,.30,.28),(.16,.30,.28),.022,steel)
    rod('Adjustment wheel',(.16,.30,.28),(.18,.30,.28),.057,dark,vertices=12)
    box('Carry grip',(0,.17,.05),(.20,.035,.055),dark,.008)

def read_rows(path):
    with path.open(encoding='utf-8-sig') as f:
        return list(csv.DictReader(line for line in f if not line.startswith('#')))

csvpath=ROOT/'game/content/data/weapons.csv'
original=csvpath.read_text(encoding='utf-8-sig')
types={r['type']:r for r in read_rows(ROOT/'game/content/data/weapon_types.csv')}
rows=read_rows(csvpath)
records=[]; roots=[]
families={'pistol':'pistol','smg':'compact smg','assault_rifle':'rifle','shotgun':'pump shotgun','bolt_sniper':'scoped sniper','dmr':'scoped sniper','lmg':'light machine gun','rocket_launcher':'rocket launcher','mortar':'mortar'}
for row in rows:
    if row['type']=='melee': continue
    parts=[]
    scale=float(row['length_m'] or types[row['type']]['length_m'])
    ident=row['id']; kind=row['type']
    color=row['accent_color'] or types[kind]['accent_color']
    # A restrained brighter finish helps readable top-down silhouettes.
    palettes={'pistol':'856449','smg':'657A83','assault_rifle':'8A896B','shotgun':'9C633C','bolt_sniper':'87936B','dmr':'758D80','lmg':'918362','rocket_launcher':'8A9460','mortar':'8D9668'}
    if ident=='ak47': palettes[kind]='AD754B'
    if ident=='glock17': palettes[kind]='627C82'
    accent=material(ident+' painted furniture',palettes[kind],.10,.68)
    if kind=='pistol': pistol(ident)
    elif kind=='smg': smg()
    elif kind=='assault_rifle': rifle(ident)
    elif kind=='shotgun': shotgun(ident=='double_barrel')
    elif kind in ['bolt_sniper','dmr']: sniper(ident)
    elif kind=='lmg': lmg()
    elif kind=='rocket_launcher': rocket()
    elif kind=='mortar': mortar()
    root=bpy.data.objects.new(ident,None); bpy.context.collection.objects.link(root)
    root['family']='double barrel shotgun' if ident=='double_barrel' else families[kind]
    root['forward']='+Z in glTF/Godot'; root['grip_origin']='(0,0,0) metres'
    # Consolidate all parts by material: six or fewer mesh draw groups.
    grouped={}
    for part in parts: grouped.setdefault(part.data.materials[0].name,[]).append(part)
    meshes=[]
    for matname,objects in grouped.items():
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.join()
        obj=bpy.context.object; obj.name=ident+'_'+matname
        obj.parent=root; meshes.append(obj)
    muzzle=bpy.data.objects.new('Muzzle',None); bpy.context.collection.objects.link(muzzle)
    muzzle.location=V((0,.74,.62) if kind=='mortar' else (0,.08 if kind=='rocket_launcher' else .045 if kind=='pistol' else .038,1))
    muzzle.parent=root
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True); muzzle.select_set(True)
    for mesh in meshes: mesh.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/(ident+'.glb')),export_format='GLB',use_selection=True,export_yup=True,export_extras=True,export_animations=False)
    triangles=0
    for mesh in meshes:
        mesh.data.calc_loop_triangles(); triangles+=len(mesh.data.loop_triangles)
    records.append({'id':ident,'family':root['family'],'triangles':triangles,'mesh_count':len(meshes),'length_m':scale,'model':f'res://assets/weapons/{ident}.glb'})
    roots.append(root)
    # Subsequent exports must also have an exact Muzzle node name.
    muzzle.name=ident+'_Muzzle'

# Preserve every CSV cell except model, including original comments and blanks.
modelcol=original.splitlines()[0].split(',').index('model')
mapping={r['id']:r['model'] for r in records}
lines=[]
for line in original.splitlines():
    cells=line.split(',')
    if cells[0] in mapping:
        cells[modelcol]=mapping[cells[0]]
    lines.append(','.join(cells))
updated='\n'.join(lines)+'\n'
for old,new in zip(original.splitlines(),updated.splitlines()):
    a,b=old.split(','),new.split(',')
    assert len(a)==len(b)
    assert all(x==y for i,(x,y) in enumerate(zip(a,b)) if i!=modelcol)
csvpath.write_text(updated,encoding='utf-8')
(OUT/'asset-report.json').write_text(json.dumps(records,indent=2),encoding='utf-8')

# A single source scene with the models posed as an indexed contact sheet.
scale=1
labelmat=material('Studio ivory text','D8E1DF')
background=material('Studio charcoal','242E37')
for i,root in enumerate(roots):
    col=i%3; row=i//3
    root.rotation_euler.z=math.radians(90)
    root.location=(col*2.1-2.6, -row*1.05+2.05, .36)
    bpy.ops.object.text_add(location=(col*2.1-2.6,-row*1.05+1.62,.012))
    label=bpy.context.object; label.name='Label_'+root.name
    label.data.body=root.name.upper().replace('_',' ')
    label.data.size=.105; label.data.materials.append(labelmat)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.035))
bpy.context.object.data.materials.append(background)
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.world.color=(.35,.35,.35)
for at,power,size in [((1,-1,7),1800,7),((-5,3,5),1300,6),((6,4,4),1400,5)]:
    bpy.ops.object.light_add(type='AREA',location=at)
    light=bpy.context.object; light.data.energy=power; light.data.shape='DISK'; light.data.size=size
    light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-4.8,9.8))
camera=bpy.context.object; camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=7.3; scene.camera=camera
scene.render.resolution_x=1800; scene.render.resolution_y=1600; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.filepath=str(OUT/'weapons-contact-sheet.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'weapons-source.blend'))
bpy.ops.render.render(write_still=True)
print('WEAPON_ASSETS_READY',json.dumps(records))
