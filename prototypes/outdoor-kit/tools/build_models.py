"""Original stylized outdoor library. Blender 5.2, no external assets/dependencies.
Coordinates here are Blender Z-up; GLB export converts to game Y-up metres.
Run: blender --background --python tools/build_models.py [-- --trees-only]
"""
import bpy
import math
import random
import json
import sys
from types import SimpleNamespace
from pathlib import Path
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from nature_shapes import build_trees, build_rocks, build_streams

ROOT = Path(__file__).resolve().parents[1]
PACK = ROOT.parents[1] / 'game/assets/outdoor-kit'
TREES_ONLY = '--trees-only' in sys.argv
(PACK / 'models').mkdir(parents=True, exist_ok=True)
(ROOT / 'source').mkdir(exist_ok=True)
if TREES_ONLY:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'source/outdoor-library.blend'))
    for tree_id in ['pine_tall', 'pine_small', 'broadleaf', 'round_oak', 'round_linden']:
        tree = bpy.data.objects.get(tree_id)
        if tree:
            for obj in list(tree.children_recursive) + [tree]:
                bpy.data.objects.remove(obj, do_unlink=True)
else:
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
random.seed(22)

def mat(name, color, rough=.78, metal=0):
    rgb = [int(color[i:i+2],16)/255 for i in (0,2,4)]
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    m=bpy.data.materials.new(name);m.diffuse_color=(*rgb,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value=(*rgb,1)
    bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
    m['wet_eligible']=True;m['snow_eligible']=name not in ['Water','LampGlass']
    return m

M={k:mat(k,c,r,mt) for k,c,r,mt in [
 ('Bark','796044',.95,0),('BarkLight','a78a5d',.94,0),('CutWood','d1b37b',.92,0),
 ('PineDark','37594d',.9,0),('Pine','4f7b57',.88,0),('LeafTip','7e985b',.86,0),
 ('Leaf','668a59',.9,0),('LeafLight','91a765',.9,0),('Grass','6c8053',.95,0),
 ('DryGrass','a2a16b',.95,0),('Rock','667a7c',.92,0),('RockLight','8c9c96',.92,0),
 ('Moss','637952',.97,0),('Dirt','967653',1,0),('Soil','685c47',1,0),
 ('Asphalt','48565c',.93,0),('Concrete','a4aaa1',.9,0),('LinePaint','d5c9a2',.85,0),
 ('Ochre','c7a35c',.6,.15),('Teal','587c80',.65,.18),('Metal','56636a',.5,.45),
 ('MetalLight','a8b7b1',.4,.5),('Rust','98674f',.92,.1),('Wood','aa8356',.86,0),
 ('WoodDark','795b40',.91,0),('Sandbag','b3a17a',1,0),('Canvas','748375',.95,0),
 ('Rubber','35444a',.96,0),('LampGlass','efdb9c',.3,0),('Water','488c99',.23,.15),
 ('Foam','b4d3c6',.65,0),('Red','b96551',.75,0)
]}

def empty(name,parent=None):
    o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;return o

def finish(o,name,parent,at,scale,material):
    o.name=name;o.parent=parent;o.location=at;o.scale=scale;o.data.materials.append(M[material])
    return o

def box(p,n,at,size,m,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o=finish(bpy.context.object,n,p,at,size,m)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Rounded edges','BEVEL');mod.width=bevel;mod.segments=2
        bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=o.modifiers.new('Weighted corners','WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cyl(p,n,at,r,h,m,r2=None,verts=12):
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r,radius2=r if r2 is None else r2,depth=h)
    return finish(bpy.context.object,n,p,at,(1,1,1),m)

def ico(p,n,at,scale,m,sub=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1)
    return finish(bpy.context.object,n,p,at,scale,m)

def beam(p,n,a,b,r,m,verts=8):
    a,b=Vector(a),Vector(b);o=cyl(p,n,(a+b)*.5,r,(b-a).length,m,verts=verts)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def mesh(p,n,verts,faces,m):
    me=bpy.data.meshes.new(n);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(n,me);bpy.context.collection.objects.link(o);o.parent=p;o.data.materials.append(M[m]);return o

assets=[]
roots=[]
def start(asset_id,name,category,note,collision):
    root=empty(asset_id);root['asset_contract']='plushpanic_outdoor_v1'
    keep=empty('Keep',root);keep['visibility_role']='keep'
    data=dict(id=asset_id,name=name,category=category,note=note,collision=collision,occlusion_parts=[])
    roots.append(root);return root,keep,data

def occ(root,data,name):
    name='Occlude_'+name;o=empty(name,root);o['visibility_role']='occlude';data['occlusion_parts'].append(name);return o

def finish_asset(root,data):
    bpy.context.view_layer.update()
    # Merge within a role; distinct material surfaces and controllable parts remain.
    for group in list(root.children):
        children=[o for o in group.children if o.type=='MESH']
        if not children:continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in children:o.select_set(True)
        bpy.context.view_layer.objects.active=children[0]
        if len(children)>1:bpy.ops.object.join()
        o=bpy.context.object;o.name=group.name.split('.')[0]+'_Mesh'
        mod=o.modifiers.new('Explicit triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    objs=[root]+list(root.children_recursive)
    verts=[o.matrix_world@v.co for o in objs if o.type=='MESH' for v in o.data.vertices]
    lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)]
    # Seat the lowest vertex on z=0 so every asset has a dependable ground pivot.
    for o in root.children:o.location.z-=lo[2]
    bpy.context.view_layer.update()
    data['dimensions_m']=[round(hi[0]-lo[0],3),round(hi[2]-lo[2],3),round(hi[1]-lo[1],3)]
    data['bounds_min_y']=0
    data['triangles']=sum(len(o.data.polygons) for o in objs if o.type=='MESH')
    data['file']='models/'+data['id']+'.glb'
    data['weather']='PBR 원본 / 젖음·상향면 적설 재질 교체 가능 (검수 샘플)'
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=root
    # Blender enforces global unique names; export exact contract names per asset.
    original={o:o.name for o in objs}
    for o in objs:
        if o is not root:o.name=o.name.split('.')[0]
    # Other roots may already occupy those names. Temporarily prefix their objects.
    for other in bpy.data.objects:
        if other not in objs and (other.name.startswith(('Keep','Occlude_','Detail','Connection_'))):other.name='Stored_'+other.name
    for o in objs:
        if o is not root:o.name=original[o].split('.')[0]
    bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']),export_format='GLB',use_selection=True,export_extras=True,export_yup=True,export_animations=False)
    assets.append(data)

def coll(kind,**kw):return dict(kind=kind,**kw)

nature_api=SimpleNamespace(**{name:globals()[name] for name in ['mat','M','start','occ','coll','finish_asset','empty','mesh','ico','beam','cyl','box']})
build_trees(nature_api)

if TREES_ONLY:
    manifest = json.loads((PACK/'manifest.json').read_text(encoding='utf-8'))
    replacements = {a['id']: a for a in assets}
    manifest['assets'] = [replacements.get(a['id'], a) for a in manifest['assets']]
    manifest['tree_revision'] = 'foliage-art-2026-10-24'
    (PACK/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    for i, root in enumerate(roots): root.location = (i*8, 0, 0)
    bpy.data.orphans_purge(do_recursive=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/outdoor-library.blend'))
    print('REPLACED', len(assets), 'CANONICAL TREES')
    sys.exit(0)

r,k,d=start('stump','나무 그루터기','식생','낮은 장식/소형 장애물. 절단면 연륜.',coll('cylinder',radius=.42,height=.6))
cyl(k,'Bark',(0,0,.3),.46,.6,'Bark',.34)
cyl(k,'Cut',(0,0,.607),.325,.025,'CutWood',verts=24)
for rad in [.11,.21,.28]:
    bpy.ops.mesh.primitive_torus_add(major_radius=rad,minor_radius=.009,major_segments=24,minor_segments=5)
    finish(bpy.context.object,'GrowthRing',k,(0,0,.623),(1,1,1),'BarkLight')
for a in range(5):
    t=a*math.tau/5;beam(k,'Root',(0,0,.18),(.62*math.cos(t),.62*math.sin(t),.05),.12,'Bark')
finish_asset(r,d)
r,k,d=start('fallen_log','쓰러진 통나무','식생','양 끝 절단면과 작은 가지. 낮은 엄폐 후보.',coll('box',size=[2.6,.55,.65],center=[0,.275,0]))
beam(k,'Log',(-1.3,0,.32),(1.3,0,.32),.32,'Bark',12)
for x in [-1.31,1.31]:beam(k,'Cut',(x-.007,0,.32),(x+.007,0,.32),.265,'CutWood',16)
beam(k,'Branch',(.1,0,.46),(.38,.36,.75),.1,'Bark')
finish_asset(r,d)

r,k,d=start('shrub','들판 관목','식생','낮은 수풀. 기본 충돌 없음. 장식 밀도 조절용.',coll('none'))
for i,(x,y,s) in enumerate([(-.45,0,.6),(.1,.2,.8),(.45,-.16,.56)]):ico(k,'Foliage',(x,y,s*.5),(s,s*.7,s*.65),'Leaf' if i%2 else 'Pine',2)
finish_asset(r,d)
for asset_id,name,reed in [('grass_patch','풀 군락',False),('reeds','물가 갈대',True)]:
    r,k,d=start(asset_id,name,'식생','불투명 양면 두께 메시. alpha 카드 없음. 충돌 없음.',coll('none'))
    for i in range(18 if reed else 34):
        x=random.uniform(-.48,.48);y=random.uniform(-.4,.4);h=random.uniform(.55,1.1) if reed else random.uniform(.15,.42)
        if reed:
            beam(k,'Stem',(x,y,0),(x+.03,y,h),.012,'Grass',5)
            cyl(k,'SeedHead',(x+.03,y,h),.035,.18,'BarkLight',verts=7)
        for j in range(2):
            a=random.random()*math.tau;dx,dy=math.cos(a),math.sin(a);w=.032 if reed else .045
            vs=[(x-dy*w,y+dx*w,0),(x+dy*w,y-dx*w,0),(x+dx*.08,y+dy*.08,h*.65),(x+dx*.19,y+dy*.19,h), (x,y,.025)]
            mesh(k,'Blade',vs,[(0,1,2),(0,2,3),(1,3,2),(1,0,4),(0,3,4),(3,1,4)],'Grass' if i%3 else 'DryGrass')
    finish_asset(r,d)

build_rocks(nature_api)

for asset_id,name,material in [('ground_grass','草地 바닥 4m','Grass'),('path_dirt','흙길 바닥 4m','Dirt'),('road_asphalt','아스팔트 도로 4m','Asphalt')]:
    r,k,d=start(asset_id,name.replace('草地','잔디'), '지형','4×4m 스냅 모듈. 바닥 충돌은 레벨이 소유. 표면 장식은 시각용.',coll('none'))
    box(k,'Ground',(0,0,-.08),(4,4,.16),material,0)
    if asset_id=='road_asphalt':
        for y in [-1.35,0,1.35]:box(k,'LaneMark',(0,y,.006),(.075,.76,.012),'LinePaint',.005)
    else:
        for i in range(28):
            x=random.uniform(-1.85,1.85);y=random.uniform(-1.85,1.85)
            ico(k,'Pebble',(x,y,.015),(random.uniform(.025,.075),.05,.025),'Rock' if i%3==0 else ('Soil' if material=='Dirt' else 'Moss'),1)
    finish_asset(r,d)
r,k,d=start('curb','도로 경계석 4m','지형','도로 가장자리 조립용. 낮은 단차의 이동 판정은 적용 시 결정.',coll('box',size=[4,.22,.28],center=[0,.11,0]))
for x in [-1.5,-.5,.5,1.5]:box(k,'Block',(x,0,.11),(.985,.28,.22),'Concrete',.025)
finish_asset(r,d)

def container(asset_id,name,opened):
    r,k,d=start(asset_id,name,'구조물','지붕·앞/뒤 상부 패널 별도. 열린형은 바닥/하부/내부 유지. 문은 정적 조형.',coll('compound',parts='floor + two sides + back; door opening left clear') if opened else coll('box',size=[5.8,2.7,2.4],center=[0,1.35,0]))
    color='Teal' if opened else 'Ochre'
    box(k,'Floor',(0,0,.12),(5.8,2.4,.24),'Metal',.04)
    for y in [-1.17,1.17]:
        box(k,'LowerRail',(0,y,.30),(5.8,.10,.35),color,.018)
        p=occ(r,d,'WallFront' if y<0 else 'WallBack')
        box(p,'Wall',(0,y,1.6),(5.72,.07,2.1),color,.012)
        for i in range(28):box(p,'Corrugation',(-2.75+i*.204,y,1.59),(.055,.14,2.08),color,.014)
        box(p,'UpperRail',(0,y,2.64),(5.8,.13,.12),'MetalLight',.015)
    roof=occ(r,d,'Roof')
    box(roof,'Roof',(0,0,2.66),(5.8,2.4,.08),color,.025)
    for i in range(28):box(roof,'RoofRib',(-2.75+i*.204,0,2.72),(.065,2.26,.055),color,.01)
    for x in [-2.85,2.85]:
        for y in [-1.15,1.15]:box(k,'CornerPost',(x,y,1.35),(.14,.14,2.7),'Metal',.02)
    back=occ(r,d,'EndCap');box(back,'End',(-2.84,0,1.45),(.10,2.25,2.45),color,.02)
    if not opened:
        door=occ(r,d,'DoorUpper')
        for y in [-.57,.57]:
            box(door,'Door',(2.87,y,1.43),(.1,1.09,2.44),color,.02)
            for yy in [y-.34,y+.34]:
                beam(door,'LockRod',(2.95,yy,.35),(2.95,yy,2.51),.024,'MetalLight')
                box(door,'Handle',(3,yy,1.18),(.08,.18,.05),'MetalLight',.01)
    else:
        box(k,'Ramp',(3.12,0,.07),(.58,1.9,.14),'Metal',.025)
        for y in [-1.24,1.24]:
            p=occ(r,d,'OpenDoor'+('L' if y<0 else 'R'))
            box(p,'Door',(2.3,y,1.4),(1.1,.09,2.55),color,.025)
    # Small, original industrial markings, no copied brand/text.
    box(k,'IDPlate',(-1.85,-1.255,.49),(.42,.014,.18),'LinePaint',.01)
    finish_asset(r,d)

container('container_closed','닫힌 화물 컨테이너',False)
container('container_open','열린 화물 컨테이너',True)
for asset_id,name,wire in [('corrugated_fence','골함석 울타리 3m',False),('wire_fence','철망 울타리 3m',True)]:
    r,k,d=start(asset_id,name,'구조물','하부와 기둥은 유지, 상부 패널만 옅게 처리 가능.',coll('box',size=[3,2.1,.18],center=[0,1.05,0]))
    for x in [-1.45,1.45]:
        box(k,'Post',(x,0,1.1),(.12,.14,2.2),'Metal',.02)
        box(k,'Foot',(x,0,.09),(.35,.35,.18),'Concrete',.04)
    box(k,'LowerRail',(0,0,.25),(3,.12,.12),'Metal',.015)
    p=occ(r,d,'UpperPanel')
    box(p,'TopRail',(0,0,2.03),(3,.10,.1),'MetalLight',.015)
    if wire:
        for x in [i*.18-1.35 for i in range(16)]:
            beam(p,'Wire',(x,0,.3),(x,0,2),.009,'MetalLight',4)
        for z in [i*.18+.37 for i in range(10)]:beam(p,'Wire',(-1.4,0,z),(1.4,0,z),.009,'MetalLight',4)
    else:
        box(p,'Sheet',(0,0,1.17),(2.85,.045,1.65),'Teal',.006)
        for i in range(24):box(p,'Rib',(-1.38+i*.12,0,1.17),(.045,.11,1.65),'Teal',.008)
        for x in [-1.30,0,1.30]:box(p,'RustPatch',(x,-.06,.51),(.05,.008,.21),'Rust',.004)
    finish_asset(r,d)

r,k,d=start('sandbags','모래주머니 엄폐','소품','2단 낮은 엄폐. 봉합선으로 쌓임 방향 식별.',coll('box',size=[2.8,.85,.65],center=[0,.425,0]))
for row in range(2):
    for i in range(4 if row==0 else 3):
        x=(i-(1.5 if row==0 else 1))*.69;z=.22+row*.36
        box(k,'Bag',(x,0,z),(.72,.65,.43),'Sandbag',.14)
        box(k,'Seam',(x,-.32,z),(.53,.014,.015),'WoodDark',.006)
finish_asset(r,d)
r,k,d=start('wooden_crate','목재 보급 상자','소품','상자 자체는 시각 에셋. 아이템 기능 없음.',coll('box',size=[.95,.95,.9],center=[0,.475,0]))
box(k,'Core',(0,0,.46),(.9,.85,.9),'WoodDark',.025)
for x in [-.38,-.19,0,.19,.38]:
    box(k,'TopPlank',(x,0,.93),(.175,.92,.065),'Wood',.015)
    for y in [-.45,.45]:box(k,'FacePlank',(x,y,.46),(.175,.055,.85),'Wood',.014)
for y in [-.49,.49]:
    for z in [.10,.82]:box(k,'Brace',(0,y,z),(.98,.06,.10),'CutWood',.012)
    b=box(k,'Diagonal',(0,y,.46),(.09,.055,.99),'WoodDark',.01);b.rotation_euler.y=.75
finish_asset(r,d)
r,k,d=start('pallet','목재 팔레트','소품','독립 판재와 받침. 컨테이너/상자 주변 조합.',coll('box',size=[1.2,.16,1],center=[0,.08,0]))
for x in [-.47,0,.47]:box(k,'Runner',(x,0,.065),(.12,1,.13),'WoodDark',.012)
for y in [-.44,-.22,0,.22,.44]:box(k,'Plank',(0,y,.16),(1.2,.17,.065),'Wood',.015)
finish_asset(r,d)
r,k,d=start('barrel','도색 철제 드럼통','소품','폭발/이동 기능 없는 정적 원본.',coll('cylinder',radius=.31,height=.92))
cyl(k,'Body',(0,0,.46),.31,.92,'Teal',verts=24)
for z in [.055,.24,.68,.88]:cyl(k,'Band',(0,0,z),.327,.045,'Metal',verts=24)
cyl(k,'Lid',(0,0,.925),.29,.025,'MetalLight',verts=24)
cyl(k,'Cap',(.12,0,.95),.045,.025,'Metal',verts=12)
box(k,'Stripe',(0,-.31,.45),(.25,.016,.18),'LinePaint',.01)
finish_asset(r,d)

r,k,d=start('streetlamp','곡선 암 가로등','구조물','발광부 분리. 실제 Light3D는 레벨이 배치.',coll('cylinder',radius=.12,height=3.6))
box(k,'Foot',(0,0,.12),(.4,.4,.24),'Concrete',.05)
cyl(k,'Pole',(0,0,1.95),.09,3.7,'Metal',.06)
beam(k,'Arm',(0,0,3.72),(.15,-.65,3.9),.055,'Metal',10)
p=occ(r,d,'LampHead');box(p,'Hood',(.15,-.72,3.8),(.48,.65,.16),'Metal',.07)
box(p,'LampGlass',(.15,-.72,3.71),(.37,.53,.035),'LampGlass',.02)
finish_asset(r,d)
r,k,d=start('utility_shed','야외 관리 창고','구조물','지붕·상부 벽 분리, 내부 바닥 존재. 출입문은 열린 모델.',coll('compound',parts='floor + rear/side walls + front doorway; no runtime collision generated'))
box(k,'Foundation',(0,0,.12),(3.5,2.9,.24),'Concrete',.04)
for y in [-1.35,1.35]:
    if y<0:
        for x in [-1.14,1.14]:box(k,'LowerWall',(x,y,.4),(1,.13,.55),'Teal',.02)
    else:box(k,'LowerWall',(0,y,.4),(3.3,.13,.55),'Teal',.02)
    p=occ(r,d,'FrontWall' if y<0 else 'RearWall')
    if y<0:
        for x in [-1.14,1.14]:box(p,'Panel',(x,y,1.55),(1.0,.12,1.8),'Teal',.025)
        box(p,'Lintel',(0,y,2.23),(1.3,.13,.45),'Teal',.02)
    else:box(p,'Panel',(0,y,1.5),(3.3,.12,1.9),'Teal',.02)
for x in [-1.6,1.6]:
    box(k,'SideBase',(x,0,.4),(.14,2.7,.55),'Teal',.02)
    p=occ(r,d,'SideWall'+('L' if x<0 else 'R'));box(p,'Panel',(x,0,1.5),(.14,2.7,1.9),'Teal',.02)
for side in [-1,1]:
    p=occ(r,d,'Roof'+('L' if side<0 else 'R'))
    o=box(p,'Roof',(side*.88,0,2.63),(1.85,3.05,.14),'Metal',.025);o.rotation_euler.y=side*.2
    for y in [-1.4,-1.05,-.7,-.35,0,.35,.7,1.05,1.4]:
        beam(p,'Rib',(side*.04,y,2.88),(side*1.78,y,2.52),.028,'MetalLight',6)
box(k,'Doorstep',(0,-1.52,.1),(1.1,.5,.2),'Concrete',.03)
finish_asset(r,d)

r,k,d=start('timber_bridge','물가 목재 다리','구조물','바닥과 난간 분리. 수면/양안 높이는 레벨에서 맞춤.',coll('box',size=[2, .25,4.2],center=[0,.125,0]))
for x in [-.85,.85]:box(k,'Beam',(x,0,.12),(.18,4.2,.24),'WoodDark',.025)
for i in range(15):box(k,'Deck',(0,-1.96+i*.28,.3),(2.1,.25,.15),'Wood',.02)
for side in [-1,1]:
    p=occ(r,d,'Railing'+('L' if side<0 else 'R'))
    for y in [-1.95,0,1.95]:box(k,'Post',(side*1.05,y,.7),(.11,.12,1.4),'WoodDark',.02)
    beam(p,'Handrail',(side*1.05,-2.04,1.27),(side*1.05,2.04,1.27),.045,'Wood',8)
    beam(p,'LowerRail',(side*1.05,-2.04,.77),(side*1.05,2.04,.77),.025,'Wood',8)
finish_asset(r,d)
r,k,d=start('water_tile','잔잔한 수면 4m','지형','불투명 수면 조형 샘플. 파도·수영·반사 시스템은 후속.',coll('none'))
box(k,'Surface',(0,0,.015),(4,4,.03),'Water',0)
for i in range(12):
    x=random.uniform(-1.7,1.7);y=random.uniform(-1.7,1.7)
    box(k,'Ripple',(x,y,.033),(random.uniform(.12,.4),.015,.006),'Foam',0)
finish_asset(r,d)
r,k,d=start('bench','야외 벤치','소품','정비소·쉼터 주변 소품. 좌석 상호작용 없음.',coll('box',size=[1.7,.9,.65],center=[0,.45,0]))
for x in [-.6,.6]:
    for y in [-.24,.24]:box(k,'Leg',(x,y,.25),(.07,.08,.5),'Metal',.015)
    beam(k,'BackFrame',(x,.26,.36),(x,.38,1.0),.035,'Metal')
for y in [-.24,-.08,.08,.24]:box(k,'Seat',(0,y,.5),(1.7,.14,.07),'Wood',.015)
for z in [.74,.92]:box(k,'BackSlat',(0,.35,z),(1.7,.07,.14),'Wood',.02)
finish_asset(r,d)

build_streams(nature_api)

manifest=dict(revision=2,tree_revision='foliage-art-2026-10-24',contract='plushpanic_outdoor_v1',date='2026-10-24',units='metres',up_axis='+Y',status='integrated canonical art kit',assets=assets)
(PACK/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
# Source library is laid out for easy Blender inspection. Exported files remain at zero.
for i,r in enumerate(roots):r.location=(i%7*8,i//7*7,0)
# Drop unused Blender startup brush materials so source has no linked brush assets.
for material in list(bpy.data.materials):
    if material not in M.values():bpy.data.materials.remove(material,do_unlink=True)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source/outdoor-library.blend'))
print('BUILT',len(assets),'ASSETS')
