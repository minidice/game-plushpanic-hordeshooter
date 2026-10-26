"""Large stage kit: standalone bpy generator for doc/model-briefs/large-stage-kit.md.
Helpers and safehouse geometry copied from stage2_safehouse.py, never imported.
Only this script and this kit's GLBs, manifest, source blend and reviews are written.
All geometry uses Blender Z-up; manifest dimensions/collision use game X/Y/Z.
"""
import json
import math
import struct
from pathlib import Path
import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game/assets/large-stage-kit'
SOURCE = REPO / 'prototypes/outdoor-kit/source/large-stage-kit.blend'
OUTPUT = REPO / 'prototypes/outdoor-kit/output/large-stage-kit'
CONTRACT = 'plushpanic_outdoor_v1'
PALETTE = dict(Concrete='a4aaa1', Metal='56636a', MetalLight='a8b7b1',
    Rust='98674f', Wood='8a6a46', WoodDark='5e4632', Ochre='c7a35c',
    Rubber='35444a', Red='b96551', Hazard='e2b84a', Ink='2a2a2a',
    Green='8fd19e', Brick='a8634c', BrickDark='7c4a3b', Plaster='d8cdb4', Glass='5f7f8c')
M = {}
# Target export envelopes, in game X/height/depth order. Safehouse includes
# the unchanged roof/beacon; panel includes conduit and warning lamp.
SPECS = dict(
    safehouse_b=(6000, (14.15,3.75,12.1), ('Keep','Occlude_Walls')),
    wall_button_panel=(1200, (.9,1.99,.43), ('Keep',)),
    shutter_leaf=(1500, (6,3.6,.2), ('Occlude_Leaf',)),
    urban_rowhouse=(4000, (12,7,10), ('Keep','Occlude_Upper')),
    urban_shop=(3000, (10,4.2,8), ('Keep','Occlude_Upper')),
    urban_warehouse=(5000, (24,7.5,16), ('Keep','Occlude_Upper')),
    car_wreck=(2000, (4.4,1.5,1.9), ('Keep',)),
    bus_wreck=(3000, (11,3,2.6), ('Keep','Occlude_Upper')),
    dumpster=(900, (2,1.3,1.3), ('Keep',)))
BUTTONS = dict(safehouse_b=(-4.6,5.55,1.4),wall_button_panel=(0,-.37,1.2))

def material(name, color):
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    rgb = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    if not result.use_nodes: result.use_nodes = True
    shader = result.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*rgb, 1)
    shader.inputs['Roughness'].default_value = .55 if name.startswith('Metal') else .85
    shader.inputs['Metallic'].default_value = .4 if name.startswith('Metal') else 0
    result['wet_eligible'] = True
    result['snow_eligible'] = True
    return result


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def mesh(parent, name, vertices, faces, color):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    data.materials.append(M[color])
    return obj


def box(parent, name, at, size, color, bevel=.02):
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = at
    obj.scale = size
    obj.data.materials.append(M[color])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('Soft toy edges', 'BEVEL')
        mod.width = min(bevel, min(size) / 3)
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def beam(parent, name, a, b, radius, color, sides=8):
    a, b = Vector(a), Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius,
                                      depth=(b - a).length, end_fill_type='NGON')
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.location = (a + b) / 2
    obj.rotation_euler = (b - a).to_track_quat('Z', 'Y').to_euler()
    obj.data.materials.append(M[color])
    return obj



def bounds(root):
    bpy.context.view_layer.update()
    points = [o.matrix_world @ v.co for o in root.children_recursive
              if o.type == 'MESH' for v in o.data.vertices]
    assert points, (root.name, 'empty geometry')
    return (Vector(tuple(min(p[i] for p in points) for i in range(3))),
            Vector(tuple(max(p[i] for p in points) for i in range(3))))


def validate(root, data):
    aid = data['id']
    limit, expected, groups = SPECS[aid]
    assert root.name == aid and root['asset_contract'] == CONTRACT
    assert root.location.length < 1e-6 and root.rotation_euler.to_matrix().is_identity
    assert {o.name for o in root.children} == set(groups), (aid, 'groups')
    for g in root.children:
        assert g.type == 'EMPTY' and len(g.children) == 1, (aid, g.name)
        assert g.children[0].type == 'MESH' and g.children[0].name == g.name+'_Mesh'
    lo, hi = bounds(root)
    dims = hi-lo
    actual = (dims.x,dims.z,dims.y)
    assert abs(lo.z) < .002, (aid, 'ground', lo.z)
    assert all(abs(a-b) <= b*.03 for a,b in zip(actual,expected)), (aid,'dimensions',actual,expected)
    meshes = [o for o in root.children_recursive if o.type == 'MESH']
    mats = {m for o in meshes for m in o.data.materials}
    assert 0 < len(mats) <= 6, (aid,'materials',len(mats))
    assert all(not any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes) for m in mats)
    triangles = sum(len(o.data.polygons) for o in meshes)
    assert all(len(p.vertices) == 3 for o in meshes for p in o.data.polygons)
    assert 0 < triangles < limit, (aid,'triangles',triangles,limit)
    assert all(o.rigid_body is None for o in meshes)
    data.update(file=aid+'.glb', dimensions_m=[round(v,4) for v in actual],
                bounds_min_y=round(lo.z,6), triangles=triangles)


def prepare(root, data):
    bpy.context.view_layer.update()
    if data['id'] in BUTTONS:
        buttons = [o for o in root.children_recursive if o.get('button_center')]
        assert len(buttons) == 1
        obj = buttons[0]
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        center = Vector(tuple((min(p[i] for p in points)+max(p[i] for p in points))/2 for i in range(3)))
        assert (center-Vector(BUTTONS[data['id']])).length < 1e-5, (data['id'],'button',center)
        assert center.y < 0 if data['id']=='wall_button_panel' else center.y > 5
        root['button_center_game'] = [center.x,center.z,-center.y]
    for group in root.children:
        objects = list(group.children)
        assert objects
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects)>1: bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = group.name+'_Mesh'
        mod = obj.modifiers.new('Explicit triangles','TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=mod.name)
        # Recalculate face normals after custom prisms and object joins.
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')
    validate(root,data)


def start(aid, title, note, part=None, collision=None):
    root = empty(aid)
    root['asset_contract'] = CONTRACT
    keep = empty('Keep',root) if 'Keep' in SPECS[aid][2] else None
    upper = empty('Occlude_'+part,root) if part else None
    return root,keep,upper,dict(id=aid,name=title,category='Large stage kit',note=note,
        collision=collision or dict(kind='none'),occlusion_parts=['Occlude_'+part] if part else [])


def safehouse_b():
    root,keep,upper,data=start('safehouse_b','Stage 2 도착 세이프하우스',
        '외벽14×12 m/두께0.4 m/높이2.6 m, 서쪽 입구2.6×2.4 m. 게임 바닥/벽 충돌 사용. 제어판 버튼 게임로컬(-4.6,1.4,-5.55), rear 반지붕 최저3.03 m.', 'Walls')
    # Segmented wall rectangles match the existing physical footprint, without corner overlap.
    walls=[(0,-5.8,14,.4),(0,5.8,14,.4),(6.8,0,.4,11.2),
           (-6.8,-3.45,.4,4.3),(-6.8,3.45,.4,4.3)]
    for x,y,w,d in walls:
        box(keep,'LowWall',(x,y,.6),(w,d,1.2),'Concrete',.025)
        box(upper,'HighWall',(x,y,1.9),(w,d,1.4),'Concrete',.025)
        # Green identification bands on both sides, inside the collision margin.
        if w>d:
            for side in (-1,1):
                box(keep,'GreenBand',(x,y+side*(d/2+.012),.95),(w-.12,.024,.18),'Green',.008)
        else:
            for side in (-1,1):
                box(keep,'GreenBand',(x+side*(w/2+.012),y,.95),(.024,d-.12,.18),'Green',.008)
    # Jambs stay OUTSIDE the clear opening; lintel bottom exactly 2.4 m.
    for y in (-1.36,1.36):
        box(keep,'JambLow',(-6.8,y,.6),(.38,.12,1.2),'Metal',.015)
        box(upper,'JambHigh',(-6.8,y,1.8),(.38,.12,1.2),'Metal',.015)
    box(upper,'Lintel',(-6.8,0,2.5),(.4,2.6,.2),'Metal',.015)
    for y in (-.85,.85):
        box(upper,'EntryLampHousing',(-7.06,y,2.5),(.12,.38,.18),'Metal',.025)
        box(upper,'EntryGreen',(-7.125,y,2.5),(.025,.26,.1),'Green',.01)
    # Only changed assembly: rear-wall control, facing the camera (-Y).
    box(keep,'ControlPlate',(-4.6,5.59,1.4),(.46,.14,.28),'Metal',.025)
    button=box(keep,'CloseButton',(-4.6,5.55,1.4),(.18,.12,.12),'Red',.02)
    # The interaction center is the mesh center, not the face surface.
    button['button_center']=True
    for x in (-4.79,-4.41):
        box(keep,'ButtonTrim',(x,5.512,1.4),(.06,.016,.23),'Hazard',.004)
        for z in (1.34,1.46):
            stripe=box(keep,'TrimStripe',(x,5.501,z),(.055,.006,.027),'Metal',.002)
            stripe.rotation_euler.y=math.radians(25)
    beam(keep,'WarningLamp',(-4.6,5.5,1.69),(-4.6,5.5,1.81),.06,'Hazard',10)
    # Half-roof on the camera-away side, with clear standing room and coarse sheet ribs.
    y0,y1=.3,6
    z0,z1=3.15,3.55
    verts=[(-7,y0,z0),(7,y0,z0),(7,y1,z1),(-7,y1,z1),
           (-7,y0,z0-.12),(7,y0,z0-.12),(7,y1,z1-.12),(-7,y1,z1-.12)]
    mesh(upper,'HalfRoof',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'Metal')
    # Anchor the existing game-owned light at local (0, 2.85, 0) to the roof edge.
    box(upper,'LampBracket',(0,.35,2.9),(.4,.16,.5),'Metal',.02)
    for x in (-6.8,6.8):
        for y,top in ((.5,3.15),(5.8,3.53)):
            box(upper,'RoofSupport',(x,y,(2.6+top)/2),(.2,.2,top-2.6),'MetalLight',.015)
    for x in (-5.5,-3.3,-1.1,1.1,3.3,5.5):
        beam(upper,'RoofRib',(x,y0,z0+.025),(x,y1,z1+.025),.035,'MetalLight',6)
    # Readable wall equipment: shallow details within the wall-adjacent collision clearance.
    for x in (-3.5,1,4.5):
        box(upper,'Vent', (x,-6.025,1.92),(1.1,.05,.48),'Metal',.025)
        for z in (1.8,1.92,2.04):
            box(upper,'VentSlat',(x,-6.06,z),(.9,.025,.035),'MetalLight',.008)
    box(upper,'BeaconBase',(-5.5,4.4,3.5),(.8,.65,.15),'Metal',.03)
    box(upper,'SafeBeacon',(-5.5,4.4,3.66),(.6,.45,.18),'Green',.06)
    return root,data



def solid_collision(size):
    return dict(kind='box',size=list(size),center=[0,size[1]/2,0])


def soft_box(parent,name,at,size,color,bevel=.08):
    obj = box(parent,name,at,size,color,0)
    mod = obj.modifiers.new('Rounded toy silhouette','BEVEL')
    mod.width = min(bevel,min(size)/3)
    mod.segments = 3
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def stripe_band(parent,name,x,y,z,width,height,base='Hazard',stripe='Ink',pitch=.5):
    box(parent,name,(x,y+.006,z),(width,.012,height),base,.003)
    # Clipped diagonal paint strips: actual geometry, no textures.
    left,right=x-width/2,x+width/2
    n=math.ceil(width/pitch)+1
    for i in range(n):
        a=left-pitch/2+i*pitch
        poly=[(a,z-height/2),(a+pitch*.45,z-height/2),
              (a+pitch*.45+height*.6,z+height/2),(a+height*.6,z+height/2)]
        for edge,sign in ((left,1),(right,-1)):
            clipped=[]
            for j,p in enumerate(poly):
                q=poly[(j+1)%len(poly)]
                pin=(p[0]-edge)*sign>=0
                qin=(q[0]-edge)*sign>=0
                if pin: clipped.append(p)
                if pin != qin:
                    t=(edge-p[0])/(q[0]-p[0])
                    clipped.append((edge,p[1]+t*(q[1]-p[1])))
            poly=clipped
            if not poly: break
        if len(poly)>=3:
            mesh(parent,name+'Stripe',[(px,y-.002,pz) for px,pz in poly],
                 [tuple(range(len(poly)))],stripe)


def wall_button_panel():
    root,keep,_,data=start('wall_button_panel','Wall button panel',
        'Wall backplate y=0; cabinet z=0.5..1.8. Button center game local (0,1.2,0.37). Envelope includes conduit and beacon; no wall collision.')
    box(keep,'Cabinet',(0,-.125,1.15),(.9,.25,1.3),'Metal',.045)
    box(keep,'DoorInset',(0,-.258,1.15),(.78,.026,1.15),'Metal',.022)
    stripe_band(keep,'WarningFrame',0,-.282,1.2,.58,.55,pitch=.17)
    box(keep,'ButtonBacking',(0,-.294,1.2),(.41,.024,.39),'Metal',.025)
    beam(keep,'ButtonStem',(0,-.27,1.2),(0,-.34,1.2),.085,'Rubber',12)
    button=beam(keep,'MushroomButton',(0,-.31,1.2),(0,-.43,1.2),.16,'Red',16)
    button['button_center']=True
    beam(keep,'BeaconMount',(0,-.125,1.78),(0,-.125,1.83),.1,'Rubber',12)
    beam(keep,'WarningBeacon',(0,-.125,1.83),(0,-.125,1.99),.08,'Hazard',12)
    beam(keep,'Conduit',(0,-.10,0),(0,-.10,.55),.055,'Rubber',8)
    for z in (.16,.42):
        box(keep,'ConduitClip',(0,-.105,z),(.16,.14,.055),'Metal',.01)
    return root,data


def shutter_leaf():
    root,_,leaf,data=start('shutter_leaf','Rolling shutter leaf',
        '6 x 3.6 m leaf; bottom-center pivot, opens by translation upward. Handles face Blender -Y.', 'Leaf')
    box(leaf,'Sheet',(0,.04,1.8),(6,.12,3.6),'MetalLight',.025)
    for z in [i*.3 for i in range(1,12)]:
        box(leaf,'Corrugation',(0,-.025,z),(5.96,.05,.055),'MetalLight',.014)
    stripe_band(leaf,'BottomWarning',0,-.055,.125,5.96,.25,pitch=.55)
    for x in (-1.8,1.8):
        for dx in (-.22,.22):
            beam(leaf,'HandleFoot',(x+dx,-.025,.48),(x+dx,-.09,.48),.01,'Ink',8)
        beam(leaf,'Handle',(x-.22,-.09,.48),(x+.22,-.09,.48),.01,'Ink',8)
    return root,data


def boarded_window(group,x,y,z,side=0):
    # Side=0 front; side=+/-1 rotates the whole shallow window to an end wall.
    pieces=[]
    pieces.append(box(group,'Window',(0,0,0),(1.35,.06,1.1),'Glass',.03))
    for angle in (-.58,.58):
        obj=box(group,'Board',(0,-.052,0),(1.52,.07,.18),'WoodDark',.025)
        obj.rotation_euler.y=angle
        pieces.append(obj)
    if side:
        for obj in pieces:
            local=obj.location.copy()
            obj.location=(x+side*(-local.y),y+side*local.x,z+local.z)
            obj.rotation_euler.z=side*math.pi/2
    else:
        for obj in pieces: obj.location+=Vector((x,y,z))


def urban_rowhouse():
    root,keep,upper,data=start('urban_rowhouse','Boarded rowhouse',
        '12 x 10 m footprint, 7 m envelope including water tank. Brick first storey, plaster second storey. Keep ends at 2.4 m.',
        'Upper',solid_collision((12,7,10)))
    box(keep,'BrickGround',(0,0,1.2),(11.8,9.8,2.4),'Brick',.09)
    box(upper,'BrickTop',(0,0,2.95),(11.8,9.8,1.1),'Brick',.07)
    box(upper,'PlasterStorey',(0,0,4.65),(11.8,9.8,2.3),'Plaster',.09)
    box(upper,'Cornice',(0,0,3.5),(12,10,.18),'Concrete',.045)
    box(upper,'FlatRoof',(0,0,5.92),(12,10,.24),'Concrete',.05)
    for y in (-4.87,4.87): box(upper,'Parapet',(0,y,6.23),(12,.26,.38),'Concrete',.035)
    for x in (-5.87,5.87): box(upper,'Parapet',(x,0,6.23),(.26,9.5,.38),'Concrete',.035)
    for z in (2.92,4.65):
        for x in (-3.8,0,3.8): boarded_window(upper,x,-4.93,z)
        for side in (-1,1):
            for y in (-2.5,2.5): boarded_window(upper,side*5.93,y,z,side)
    box(keep,'ClosedDoor',(0,-4.94,1.04),(1.5,.08,2.08),'WoodDark',.035)
    beam(keep,'DoorHandle',(.5,-4.99,.93),(.5,-4.99,1.15),.025,'Concrete',8)
    # Small recessed porch canopy stays inside the nominal 10 m envelope.
    box(keep,'PorchAwning',(0,-4.7,2.29),(2.05,.6,.14),'Concrete',.035)
    beam(upper,'WaterTank',(2.7,1.8,6.04),(2.7,1.8,6.92),.9,'Concrete',16)
    beam(upper,'TankCap',(2.7,1.8,6.92),(2.7,1.8,7),.94,'Concrete',16)
    box(upper,'VentStack',(-2.2,1.7,6.42),(.65,.65,.8),'Concrete',.05)
    box(upper,'VentCap',(-2.2,1.7,6.85),(.85,.85,.12),'WoodDark',.025)
    return root,data


def urban_shop():
    root,keep,upper,data=start('urban_shop','Shuttered corner shop',
        '10 x 8 x 4.2 m complete envelope. Front wall y=-2.8, awning projects 1.2 m to y=-4; rear wall y=4. No lettering.',
        'Upper',solid_collision((10,4.2,8)))
    box(keep,'ShopLower',(0,.6,1.2),(10,6.8,2.4),'Plaster',.08)
    box(upper,'ShopUpper',(0,.6,3.04),(10,6.8,1.28),'Plaster',.08)
    box(upper,'Roof',(0,.6,3.72),(10,6.8,.18),'MetalLight',.045)
    for x in (-2.35,2.35):
        box(keep,'ShutterFrame',(x,-2.79,1.2),(4.18,.12,2.4),'Metal',.035)
        box(keep,'ShopShutter',(x,-2.88,1.18),(3.9,.09,2.32),'MetalLight',.025)
        for z in [i*.28 for i in range(1,9)]:
            box(keep,'ShutterRib',(x,-2.94,z),(3.8,.035,.035),'MetalLight',.007)
    # The canvas strips share a sloped closed-prism profile, with a scallop-free chunky valance.
    for i in range(16):
        x0=-4.8+i*.6; x1=x0+.6
        color='Red' if i%2==0 else 'Plaster'
        verts=[(x0,-2.8,3.16),(x1,-2.8,3.16),(x1,-4,2.83),(x0,-4,2.83),
               (x0,-2.8,3.08),(x1,-2.8,3.08),(x1,-4,2.75),(x0,-4,2.75)]
        obj=mesh(upper,'Canvas',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],color)
        box(upper,'Valance',(x0+.3,-3.97,2.70),(.6,.06,.18),color,.015)
    box(upper,'BlankSign',(0,-2.86,3.43),(7.6,.13,.36),'Ochre',.04)
    for x in (-2.6,2.6):
        box(upper,'AirConditioner',(x,.8,3.99),(1.45,.8,.42),'MetalLight',.05)
        box(upper,'ACGrille',(x,.382,3.99),(1.15,.04,.28),'Metal',.015)
        for dx in (-.42,-.21,0,.21,.42):
            box(upper,'ACSlat',(x+dx,.353,3.99),(.035,.02,.23),'MetalLight',.005)
    return root,data


def urban_warehouse():
    root,keep,upper,data=start('urban_warehouse','Sawtooth warehouse',
        '24 x 16 x 7.5 m envelope; three north-glazed sawtooth bays. Lower wall split exactly at 2.4 m. Closed six-metre front sliding door.',
        'Upper',solid_collision((24,7.5,16)))
    box(keep,'Foundation',(0,0,.5),(24,16,1),'Concrete',.09)
    box(keep,'LowerWalls',(0,0,1.7),(23.8,15.8,1.4),'Metal',.035)
    box(upper,'UpperWalls',(0,0,4.15),(23.8,15.8,3.5),'Metal',.045)
    # Thin rounded corrugation profiles, split at the occlusion height.
    for x in [i*.8 for i in range(-14,15)]:
        for y in (-7.94,7.94):
            if y<0 and abs(x)<3.1: continue
            for group,z,h in ((keep,1.7,1.4),(upper,4.15,3.5)):
                box(group,'VerticalRib',(x,y,z),(.085,.10,h),'MetalLight',0)
    for side in (-1,1):
        for y in [i*.9 for i in range(-8,9)]:
            for group,z,h in ((keep,1.7,1.4),(upper,4.15,3.5)):
                box(group,'EndRib',(side*11.94,y,z),(.10,.085,h),'Metal',0)
    # Sawtooth runs along Y. Each steep glazing face points north (+Y).
    for i in range(3):
        y0=-8+i*16/3; y1=y0+16/3
        verts=[(-12,y0,5.9),(12,y0,5.9),(12,y1,7.5),(-12,y1,7.5),
               (-12,y0,5.8),(12,y0,5.8),(12,y1,5.8),(-12,y1,5.8)]
        obj=mesh(upper,'Sawtooth',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'MetalLight')
        for x in (-9,-3,3,9):
            box(upper,'NorthGlass',(x,y1+.005,6.72),(5.65,.035,1.38),'Glass',.01)
    for group,z,h in ((keep,1.35,2.1),(upper,3.65,2.5)):
        box(group,'SlidingDoor',(0,-7.99,z),(6,.08,h),'MetalLight',.025)
        for x in (-2.8,0,2.8):
            box(group,'DoorRib',(x,-8.045,z),(.08,.03,h-.08),'Metal',.006)
    box(upper,'DoorTrack',(0,-7.99,5),(6.65,.08,.16),'MetalLight',.02)
    for x,z,w,h in ((-2.4,.7,.6,.32),(1.7,1.9,.8,.2),(.5,3.9,.8,.35),(-1,3,.4,.45)):
        box(keep if z<2.4 else upper,'RustPatch',(x,-8.04,z),(w,.012,h),'Rust',.025)
    for x in (-11.8,11.8):
        for y in (-7.8,7.8):
            beam(keep,'DrainLower',(x,y,0),(x,y,2.4),.095,'MetalLight',8)
            beam(upper,'DrainUpper',(x,y,2.4),(x,y,5.95),.095,'MetalLight',8)
    return root,data


def wheel(parent,name,x,y,z,radius,width,color='Rubber',flat=False):
    obj=beam(parent,name,(x,y-width/2,z),(x,y+width/2,z),radius,color,12)
    if flat:
        bpy.context.view_layer.update()
        inv=obj.matrix_world.inverted()
        for v in obj.data.vertices:
            p=obj.matrix_world @ v.co
            p.z=max(p.z,.015)
            v.co=inv @ p
    return obj


def car_wreck():
    root,keep,_,data=start('car_wreck','Abandoned sedan',
        '4.4 x 1.9 x 1.5 m; slightly rolled body, flattened camera-side rear tyre. No roof alarm mount. Rear (-X): two Red tail lights (0.28 x 0.12 m); four corner Hazard indicators (0.14 x 0.1 m); one Red dash alarm lens (0.12 m) at the windshield base. Six palette materials; AlarmCar prefix binding.',
        collision=solid_collision((4.4,1.5,1.9)))
    body=[]
    body.append(soft_box(keep,'Body',(0,0,.64),(4.2,1.72,.68),'Ochre',.2))
    body.append(soft_box(keep,'Cabin',(-.25,0,1.12),(2.25,1.48,.68),'Ochre',.18))
    for side in (-1,1):
        for x in (-.78,.28):
            obj=box(keep,'SideWindow',(x,side*.736,1.19),(.86,.045,.37),'Glass',.06)
            body.append(obj)
        for x in (-.9,.3): body.append(box(keep,'DoorHandle',(x,side*.875,.85),(.24,.04,.055),'MetalLight',.012))
        body.append(box(keep,'Mirror',(.91,side*.875,1.02),(.22,.14,.12),'Ochre',.04))
    body.append(box(keep,'Windshield',(.88,0,1.16),(.045,1.22,.36),'Glass',.06))
    # The lens sits in the dash; expose its face at the opaque windshield base.
    body.append(box(keep,'DashAlarmLamp',(.86,-.32,1.05),(.12,.12,.12),'Red',.02))
    body.append(box(keep,'RearGlass',(-1.38,0,1.16),(.04,1.18,.34),'Glass',.05))
    for obj in body:
        # A modest lean towards the deflated tyre; all details follow the body.
        angle=math.radians(1.2)
        y,z=obj.location.y,obj.location.z-.65
        obj.location.y=y*math.cos(angle)-z*math.sin(angle)
        obj.location.z=.65+y*math.sin(angle)+z*math.cos(angle)
        obj.rotation_euler.x+=angle
    for x in (-1.32,1.38):
        for side in (-1,1):
            flat=x<0 and side<0
            wheel(keep,'Tyre',x,side*.77,.27 if flat else .34,.34,.32,flat=flat)
            wheel(keep,'Hub',x,side*.94,.27 if flat else .34,.16,.02,'MetalLight')
    for x in (-2.12,2.12):
        soft_box(keep,'Bumper',(x,0,.45),(.16,1.73,.19),'MetalLight',.055)
    for y in (-.57,.57):
        box(keep,'Headlight',(2.105,y,.78),(.035,.31,.16),'MetalLight',.04)
        box(keep,'TailLight',(-2.105,y,.78),(.035,.28,.12),'Red',.025)
    # Chamfered eight-sided lenses: 28 triangles each, within the 2,000 budget.
    outline=[(-.056,-.05),(.056,-.05),(.07,-.036),(.07,.036),
             (.056,.05),(-.056,.05),(-.07,.036),(-.07,-.036)]
    for end in (-1,1):
        for side in (-1,1):
            vertices=[(end*(2.09+depth),side*.755+y,.78+z)
                      for depth in (0,.04) for y,z in outline]
            faces=[tuple(reversed(range(8))),tuple(range(8,16))]
            faces += [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
            mesh(keep,'HazardIndicator',vertices,faces,'Hazard')
    assert {m.name for obj in keep.children for m in obj.data.materials} == {
        'Ochre','Glass','Rubber','MetalLight','Red','Hazard'}
    return root,data


def bus_wreck():
    root,keep,upper,data=start('bus_wreck','Abandoned bus',
        '11 x 2.6 x 3 m; six wheels, green stripe, broken opaque window holes and dented front bumper. Split at 1.4 m.',
        'Upper',solid_collision((11,3,2.6)))
    soft_box(keep,'LowerBody',(0,0,.94),(10.8,2.45,.92),'Plaster',.16)
    soft_box(upper,'UpperBody',(0,0,2.12),(10.8,2.45,1.44),'Plaster',.14)
    soft_box(upper,'Roof',(0,0,2.9),(10.8,2.5,.2),'Plaster',.07)
    for side in (-1,1):
        box(keep,'GreenBelt',(0,side*1.235,1.21),(10.5,.045,.28),'Green',.025)
        for i,x in enumerate((-4.35,-2.9,-1.45,0,1.45,2.9,4.25)):
            box(upper,'Window',(x,side*1.24,2.12),(1.20,.035,.87),'Glass',.06)
            if i in (1,4):
                # Irregular dark silhouette inside a retained glass border.
                verts=[(x-.43,side*1.262,1.85),(x-.47,side*1.262,2.35),
                       (x-.12,side*1.262,2.30),(x+.10,side*1.262,2.48),
                       (x+.42,side*1.262,2.28),(x+.34,side*1.262,1.86),
                       (x+.02,side*1.262,1.95)]
                mesh(upper,'BrokenPane',verts,[tuple(range(7))],'Ink')
    for x in (-3.65,-2.1,3.65):
        for side in (-1,1):
            wheel(keep,'Tyre',x,side*1.06,.47,.47,.44)
            wheel(keep,'Hub',x,side*1.29,.47,.23,.02,'Plaster')
    for x in (-5.405,5.405):
        box(upper,'EndGlass',(x,0,2.15),(.035,2.05,.92),'Glass',.065)
    # Three offset bumper pieces read as a dent, while preserving the envelope.
    for y,xx,ang in ((-.8,5.40,-.12),(0,5.32,.08),(.8,5.40,.12)):
        obj=box(keep,'DentedBumper',(xx,y,.65),(.16,.78,.22),'Rubber',.04)
        obj.rotation_euler.z=ang
    box(keep,'RearBumper',(-5.42,0,.65),(.16,2.35,.22),'Rubber',.04)
    for y in (-.85,.85): box(keep,'Headlight',(5.397,y,1.02),(.05,.30,.22),'Plaster',.04)
    return root,data


def dumpster():
    root,keep,_,data=start('dumpster','Open-lid dumpster',
        '2 x 1.3 x 1.3 m envelope, including half-open rubber lid. Four wheels, hollow visible interior.',
        collision=solid_collision((2,1.3,1.3)))
    box(keep,'BinBottom',(0,0,.24),(1.88,1.18,.16),'Green',.05)
    for x in (-.94,.94): box(keep,'BinSide',(x,0,.55),(.12,1.3,.64),'Green',.04)
    for y in (-.59,.59): box(keep,'BinFace',(0,y,.55),(1.8,.12,.64),'Green',.04)
    box(keep,'DarkInterior',(0,0,.33),(1.73,1.04,.035),'Rubber',.01)
    for x in (-.74,.74):
        for y in (-.42,.42):
            wheel(keep,'Caster',x,y,.12,.12,.12)
    # Rear hinge; half-open lid (45 degrees) remains inside the 1.3 m envelope.
    hinge_y,hinge_z=.55,.86
    depth=.58
    lid=box(keep,'HalfOpenLid',(0,hinge_y-depth*.5/math.sqrt(2),hinge_z+depth*.5/math.sqrt(2)),
            (1.97,depth,.055),'Rubber',.018)
    lid.rotation_euler.x=-math.pi/4
    # Fixed rear lid segment makes this a two-leaf commercial bin.
    box(keep,'RearLid',(0,.33,.88),(1.97,.62,.06),'Rubber',.02)
    beam(keep,'LidHinge',(-.94,.55,.87),(.94,.55,.87),.045,'Green',8)
    for x in (-.6,.6): box(keep,'FrontRib',(x,-.654,.54),(.065,.025,.43),'Green',.01)
    return root,data



def export_glb(root,data):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root]+list(root.children_recursive): obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    result=bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']),export_format='GLB',
        use_selection=True,export_extras=True,export_yup=True,export_animations=False)
    assert result=={'FINISHED'}


def verify_glb(data):
    from mathutils import Matrix, Quaternion
    raw=(PACK/data['file']).read_bytes()
    magic,version,length=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and length==len(raw)
    count,kind=struct.unpack_from('<II',raw,12)
    assert kind==0x4e4f534a
    gltf=json.loads(raw[20:20+count])
    assert not gltf.get('textures') and not gltf.get('images') and not gltf.get('animations')
    assert len(gltf.get('materials',[]))<=6
    nodes=gltf['nodes']
    roots=gltf['scenes'][gltf.get('scene',0)]['nodes']
    assert len(roots)==1
    root=nodes[roots[0]]
    assert root['name']==data['id'] and root['extras']['asset_contract']==CONTRACT
    groups=[nodes[i] for i in root.get('children',[])]
    assert {g['name'] for g in groups}==set(SPECS[data['id']][2])
    for g in groups:
        assert len(g['children'])==1 and 'mesh' not in g
        assert nodes[g['children'][0]]['name']==g['name']+'_Mesh'
    tris=sum(gltf['accessors'][p['indices']]['count']//3 for m in gltf['meshes'] for p in m['primitives'])
    assert tris==data['triangles'],(data['id'],'GLB triangles',tris)
    points=[]
    binary_start=20+count+8
    def visit(index,parent):
        node=nodes[index]
        if 'matrix' in node:
            local=Matrix([node['matrix'][i:i+4] for i in range(0,16,4)]).transposed()
        else:
            q=node.get('rotation',[0,0,0,1])
            local=Matrix.LocRotScale(Vector(node.get('translation',[0,0,0])),
                Quaternion((q[3],q[0],q[1],q[2])),Vector(node.get('scale',[1,1,1])))
        world=parent@local
        if 'mesh' in node:
            for p in gltf['meshes'][node['mesh']]['primitives']:
                acc=gltf['accessors'][p['attributes']['POSITION']]
                assert acc['componentType']==5126 and acc['type']=='VEC3'
                view=gltf['bufferViews'][acc['bufferView']]
                offset=binary_start+view.get('byteOffset',0)+acc.get('byteOffset',0)
                stride=view.get('byteStride',12)
                for i in range(acc['count']):
                    points.append(world@Vector(struct.unpack_from('<3f',raw,offset+i*stride)))
        for child in node.get('children',[]): visit(child,world)
    visit(roots[0],Matrix.Identity(4))
    lo=[min(p[i] for p in points) for i in range(3)]
    hi=[max(p[i] for p in points) for i in range(3)]
    assert abs(lo[1])<.002,(data['id'],'export ground',lo)
    assert all(abs(hi[i]-lo[i]-data['dimensions_m'][i])<.001 for i in range(3)),(data['id'],'export bounds',lo,hi)
    if data['id'] in BUTTONS:
        x,y,z=BUTTONS[data['id']]
        actual=root['extras']['button_center_game']
        assert all(abs(a-b)<1e-5 for a,b in zip(actual,(x,z,-y)))


def namespace(root,data,restore=False):
    for obj in root.children_recursive:
        if restore:
            obj.name=obj['contract_name']
        else:
            if 'contract_name' not in obj: obj['contract_name']=obj.name
            obj.name=data['id']+'__'+obj['contract_name']


def setup_review():
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1100
    scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.21,.25,.29,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
    bpy.ops.object.camera_add()
    camera=bpy.context.object
    camera.name='ReviewCamera'
    camera.data.type='ORTHO'
    camera.data.clip_end=1000
    scene.camera=camera
    bpy.ops.mesh.primitive_plane_add(size=250,location=(0,0,-.025))
    ground=bpy.context.object
    ground.name='ReviewGround_NotExported'
    ground.data.materials.append(M['Concrete'])
    lights=[]
    for name,at,energy,size in [('Key',(-14,-18,26),3800,12),('Fill',(15,-4,18),2000,10),('Rim',(3,16,22),2600,9)]:
        bpy.ops.object.light_add(type='AREA',location=at)
        obj=bpy.context.object; obj.name='Review'+name
        obj.data.energy=energy; obj.data.shape='DISK'; obj.data.size=size
        obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
        lights.append(obj)
    return camera,ground,lights


def render_review(root,data,camera):
    lo,hi=bounds(root)
    center=(lo+hi)/2
    # Camera from -X/-Y with 48 degrees elevation, matching the game-facing sides.
    horizontal=Vector((-.43,-1,0)).normalized()
    direction=horizontal*math.cos(math.radians(48))+Vector((0,0,math.sin(math.radians(48))))
    camera.location=center+direction*60
    camera.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    bpy.context.view_layer.update()
    inv=camera.matrix_world.inverted()
    points=[inv@(o.matrix_world@v.co) for o in root.children_recursive if o.type=='MESH' for v in o.data.vertices]
    width=max(p.x for p in points)-min(p.x for p in points)
    height=max(p.y for p in points)-min(p.y for p in points)
    camera.data.ortho_scale=max(width,height*1100/900)*1.18
    # Center projected extents rather than the volumetric bounding-box center.
    cx=(min(p.x for p in points)+max(p.x for p in points))/2
    cy=(min(p.y for p in points)+max(p.y for p in points))/2
    camera.location+=camera.rotation_euler.to_matrix()@Vector((cx,cy,0))
    bpy.context.scene.render.filepath=str(OUTPUT/(data['id']+'.png'))
    assert bpy.ops.render.render(write_still=True)=={'FINISHED'}


def main():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials): bpy.data.materials.remove(mat,do_unlink=True)
    M.update({name:material(name,color) for name,color in PALETTE.items()})
    bpy.context.scene.unit_settings.system='METRIC'
    bpy.context.scene.unit_settings.scale_length=1
    bpy.context.preferences.filepaths.save_version=0
    for path in (PACK,SOURCE.parent,OUTPUT): path.mkdir(parents=True,exist_ok=True)
    prepared=[]
    for build in (safehouse_b,wall_button_panel,shutter_leaf,urban_rowhouse,urban_shop,
                  urban_warehouse,car_wreck,bus_wreck,dumpster):
        root,data=build()
        prepare(root,data)
        export_glb(root,data)
        verify_glb(data)
        print('LARGE_STAGE_ASSET_READY',data['id'],data['triangles'],data['dimensions_m'],flush=True)
        namespace(root,data)
        prepared.append((root,data))
    assert len(prepared)==9 and {d['id'] for _,d in prepared}==set(SPECS)
    camera,ground,lights=setup_review()
    for root,data in prepared:
        for other,_ in prepared:
            for obj in other.children_recursive: obj.hide_render=other!=root
        render_review(root,data,camera)
    # Store an inspectable source contact sheet. Export pivots remain at origin.
    for i,(root,data) in enumerate(prepared):
        for obj in root.children_recursive: obj.hide_render=False
        root.location=((i%3)*32,(i//3)*25,0)
        root['source_display_offset']=list(root.location)
    camera.location=(25,-43,75)
    camera.rotation_euler=(Vector((28,24,0))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=110
    assert bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))=={'FINISHED'}
    manifest=dict(revision=1,contract=CONTRACT,units='metres',up_axis='+Y',assets=[d for _,d in prepared])
    (PACK/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # Final checks repeat after all writes; a partial build never prints success.
    for root,data in prepared:
        root.location=(0,0,0)
        namespace(root,data,restore=True)
        validate(root,data)
        verify_glb(data)
        namespace(root,data)
        png=(OUTPUT/(data['id']+'.png')).read_bytes()
        assert png[:8]==b'\x89PNG\r\n\x1a\n' and len(png)>1000
        assert struct.unpack_from('>II',png,16)==(1100,900)
    assert SOURCE.stat().st_size>1000
    assert len(json.loads((PACK/'manifest.json').read_text(encoding='utf-8'))['assets'])==9
    print('LARGE_STAGE_KIT_BUILT 9',flush=True)


if __name__=='__main__':
    main()
