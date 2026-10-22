"""Stage transition props; standalone Blender script, Z-up metres.

Run with Blender --background --python prototypes/outdoor-kit/tools/transition_props.py.
Only this script is authored here; GLB/manifest/blend generation is reviewer-owned.
Helpers and palette copied from build_models.py, never imported (import builds the kit).
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game' / 'assets' / 'stage-props'
CONTRACT = 'plushpanic_outdoor_v1'

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


# Everything below is independent of the outdoor-kit build and its pack directory.
EXPECTED = {
    'floor_hatch': (1.5, 1.3, 1.6),
    'bunker_hatch': (3.0, 1.9, 2.8),
    'concrete_barrier': (2.0, .8, .6),
    # Sign height includes the triangle above the 1.45 m post; base sets depth.
    'warning_sign': (.75, 1.73, .35),
}
BUDGET = dict(floor_hatch=6000, bunker_hatch=6000,
              concrete_barrier=2500, warning_sign=2500)
checks = {}
pending = []


def coll(kind, **kw):
    return dict(kind=kind, **kw)


def bevel_mesh(o, width, segments=2):
    bpy.context.view_layer.objects.active = o
    mod = o.modifiers.new('Rounded edges', 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return o


def ring(p, n, outer, inner, bottom, top, m, bevel=.02, segments=32):
    """Closed annulus with a rounded eight-point section, never a capped disk."""
    b = min(bevel, (outer-inner)/3, (top-bottom)/3)
    section = [(outer-b, bottom), (outer, bottom+b), (outer, top-b),
               (outer-b, top), (inner+b, top), (inner, top-b),
               (inner, bottom+b), (inner+b, bottom)]
    vs = [(r*math.cos(i*math.tau/segments),
           r*math.sin(i*math.tau/segments), z)
          for i in range(segments) for r, z in section]
    faces = []
    for i in range(segments):
        for j in range(8):
            a, b = i*8+j, ((i+1) % segments)*8+j
            c, d = ((i+1) % segments)*8+(j+1) % 8, i*8+(j+1) % 8
            faces.append((a, b, c, d))
    return mesh(p, n, vs, faces, m)


def torus(p, n, at, major, minor, m, segments=24):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor,
                                    major_segments=segments, minor_segments=8)
    return finish(bpy.context.object, n, p, at, (1, 1, 1), m)


def tube_path(p, n, points, radius, m):
    """One continuous rounded rail, with no sphere joints or open tube ends."""
    points = [Vector(pt) for pt in points]
    vs, faces = [], []
    sides = 8
    for i, pt in enumerate(points):
        tangent = (points[min(i+1, len(points)-1)] - points[max(0, i-1)]).normalized()
        side = Vector((1, 0, 0))
        other = tangent.cross(side).normalized()
        for j in range(sides):
            a = j*math.tau/sides
            vs.append(pt + radius*(math.cos(a)*side + math.sin(a)*other))
    for i in range(len(points)-1):
        for j in range(sides):
            a = i*sides+j
            b = i*sides+(j+1) % sides
            faces.append((a, b, b+sides, a+sides))
    faces.extend([tuple(reversed(range(sides))),
                  tuple((len(points)-1)*sides+j for j in range(sides))])
    return mesh(p, n, vs, faces, m)


def ladder(p, base, rise, y):
    for x in (-.28, .28):
        points = [(x, y, .02), (x, y, base+rise-.09)]
        # Upper rail curls rearward; its highest point is base + rise.
        for i in range(1, 7):
            a = math.pi*i/6
            points.append((x, y+.06*(1-math.cos(a)),
                           base+rise-.09+.06*math.sin(a)))
        points.append((x, y+.12, base+rise-.17))
        tube_path(p, 'LadderRail', points, .03, 'MetalLight')
    for z in (.10, base+.15):
        beam(p, 'LadderRung', (-.28, y, z), (.28, y, z), .032, 'MetalLight')


def clip(poly, axis, bound, less):
    out = []
    for a, b in zip(poly, poly[1:]+poly[:1]):
        ia = a[axis] <= bound if less else a[axis] >= bound
        ib = b[axis] <= bound if less else b[axis] >= bound
        if ia:
            out.append(a)
        if ia != ib:
            t = (bound-a[axis])/(b[axis]-a[axis])
            out.append(tuple(a[j]+t*(b[j]-a[j]) for j in range(2)))
    return out


def hazard_strip(p, x0, x1, y0, y1):
    # Clip diagonal black ribbons to each yellow rail; nothing bridges the hole.
    for i in range(-12, 13):
        t = i*.24
        poly = [(t+y0-.2, y0-.2), (t+.115+y0-.2, y0-.2),
                (t+.115+y1+.2, y1+.2), (t+y1+.2, y1+.2)]
        for axis, bound, less in [(0, x0, False), (0, x1, True),
                                  (1, y0, False), (1, y1, True)]:
            poly = clip(poly, axis, bound, less)
            if not poly:
                break
        if len(poly) >= 3:
            mesh(p, 'DiagonalStripe', [(x, y, .083) for x, y in poly],
                 [tuple(range(len(poly)))], 'Ink')


def pose_lid(group, y, z, opening):
    # Local +Y runs from hinge to top edge of the open lid; local +Z faces camera.
    # A closed lid extends toward -Y, so 100/105 degrees open => 80/75 degrees here.
    group.location = (0, y, z)
    group.rotation_euler.x = math.radians(180-opening)


def floor_hatch():
    r, k, d = start('floor_hatch', '바닥 사다리 해치', '전환 소품',
                    '실내 출구. 상호작용과 바닥 충돌은 레벨 소유.', coll('none'))
    box(k, 'DarkWell', (0, 0, .005), (1.14, 1.14, .01), 'Ink', 0)
    rails = [(-.66, 0, .18, 1.5), (.66, 0, .18, 1.5),
             (0, -.66, 1.14, .18), (0, .66, 1.14, .18)]
    for x, y, w, h in rails:
        box(k, 'SteelFrame', (x, y, .04), (w, h, .08), 'Metal', .018)
        box(k, 'HazardPaint', (x, y, .079), (w-.036, h-.036, .006), 'Hazard', .002)
        hazard_strip(k, x-w/2+.02, x+w/2-.02, y-h/2+.02, y+h/2-.02)
    ladder(k, .08, .35, .23)
    lid = occ(r, d, 'Lid')
    box(lid, 'LidPlate', (0, .575, 0), (1.15, 1.15, .08), 'Metal', .035)
    for x in (-.32, .32):
        box(lid, 'LidRib', (x, .575, .058), (.075, .87, .055), 'MetalLight', .022)
    for x in (-.13, .13):
        beam(lid, 'HandleMount', (x, .40, .045), (x, .40, .12), .035, 'MetalLight')
    beam(lid, 'HandleGrip', (-.13, .40, .12), (.13, .40, .12), .04, 'MetalLight')
    for x in (-.39, .39):
        beam(lid, 'Hinge', (x-.10, 0, 0), (x+.10, 0, 0), .07, 'MetalLight', 12)
    pose_lid(lid, .64, .11, 100)
    pending.append((r, d))


def bunker_hatch():
    r, k, d = start('bunker_hatch', '야외 벙커 해치', '전환 소품',
                    '도착 랜드마크. 모래주머니와 계단은 시각용.',
                    coll('cylinder', radius=1.05, height=.45))
    ring(k, 'ConcreteNeck', 1.05, .60, 0, .45, 'Concrete', .045)
    cyl(k, 'DarkWell', (0, 0, .005), .60, .01, 'Ink', verts=32)
    ring(k, 'SteelRim', .65, .60, .445, .49, 'Metal', .012)
    ladder(k, .45, .40, .18)
    # Nine bottom bags span 200 degrees, eight top bags offset half a pitch.
    # Tangential long axes and an unobstructed front leave the exit readable.
    for row, count in ((0, 9), (1, 8)):
        for i in range(count):
            angle = math.radians(-10 + (i + .5*row)*25)
            o = box(k, 'Sandbag', (1.30*math.cos(angle), 1.30*math.sin(angle),
                                  .11+row*.21), (.55, .30, .22), 'Sandbag', .085)
            o.rotation_euler.z = angle+math.pi/2
    for y, z, depth in [(-1.25, .075, .44), (-1.02, .15, .36), (-.80, .225, .30)]:
        box(k, 'Step', (0, y, z), (.72, depth, z*2), 'MetalLight', .035)
    lid = occ(r, d, 'Lid')
    plate = cyl(lid, 'LidPlate', (0, .625, 0), .625, .16, 'Metal', verts=32)
    bevel_mesh(plate, .028)
    # Thin raised rim plus robust red ring with four spokes; no seventh material.
    rim = ring(lid, 'LidLip', .595, .555, .075, .10, 'Metal', .008)
    rim.location.y = .625
    torus(lid, 'ValveWheel', (0, .625, .20), .24, .035, 'HatchRed')
    for angle in (0, math.pi/2, math.pi, 3*math.pi/2):
        beam(lid, 'ValveSpoke', (0, .625, .20),
             (.235*math.cos(angle), .625+.235*math.sin(angle), .20),
             .026, 'HatchRed')
    cyl(lid, 'ValveHub', (0, .625, .165), .065, .17, 'HatchRed', verts=12)
    for x in (-.34, .34):
        box(lid, 'HingeBlock', (x, .015, -.02), (.24, .18, .22), 'Metal', .035)
        beam(lid, 'HingePin', (x-.15, .015, -.02), (x+.15, .015, -.02),
             .065, 'MetalLight', 12)
    pose_lid(lid, .66, .53, 105)
    # Record real local geometry centers before role meshes are joined.
    bpy.context.view_layer.update()
    checks[r.name] = {'lid_center': lid.matrix_world @ Vector((0, .625, 0)),
                      'valve_center': lid.matrix_world @ Vector((0, .625, .20))}
    pending.append((r, d))


def jersey_body(p):
    """Watertight extruded Jersey profile with two genuine bottom lift notches."""
    verts, faces, indices = [], [], {}

    def index(pt):
        pt = tuple(pt)
        if pt not in indices:
            indices[pt] = len(verts)
            verts.append(pt)
        return indices[pt]

    def face(points):
        faces.append(tuple(index(pt) for pt in points))

    stations = [-1, -.82, -.58, .58, .82, 1]
    floors = [0, .12, 0, .12, 0]
    for i, low in enumerate(floors):
        a, b = stations[i:i+2]
        # CCW section in Y/Z; normals point outward along the extrusion.
        profile = [(-.30, low), (.30, low)]
        if low < .12:
            profile.append((.30, .12))
        profile.extend([(.30, .25), (.11, .80), (-.11, .80), (-.30, .25)])
        if low < .12:
            profile.append((-.30, .12))
        for j in range(len(profile)):
            y0, z0 = profile[j]
            y1, z1 = profile[(j+1) % len(profile)]
            face([(a, y0, z0), (a, y1, z1), (b, y1, z1), (b, y0, z0)])
        if i == 0:
            face([(a, y, z) for y, z in reversed(profile)])
        if i == len(floors)-1:
            face([(b, y, z) for y, z in profile])
        if i < len(floors)-1 and low != floors[i+1]:
            nxt = floors[i+1]
            points = [(b, -.30, min(low, nxt)), (b, .30, min(low, nxt)),
                      (b, .30, max(low, nxt)), (b, -.30, max(low, nxt))]
            face(points if low < nxt else list(reversed(points)))
    o = mesh(p, 'JerseyBody', verts, faces, 'Concrete')
    # Notch-height vertices above split adjacent edges consistently; weld shared corners.
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    bevel_mesh(o, .018)


def concrete_barrier():
    r, k, d = start('concrete_barrier', '낮은 콘크리트 방벽', '소품',
                    '저지 단면과 하부 인양 홈. 추천 충돌은 시각 홈을 포함하지 않음.',
                    coll('box', size=[2.0, .8, .5], center=[0, .4, 0]))
    jersey_body(k)
    z = .665
    slope = .19/.55
    y = -.30+slope*(z-.25)
    stripe = box(k, 'FrontReflector', (0, y-.004, z), (1.70, .012, .09), 'Hazard', .005)
    stripe.rotation_euler.x = -math.atan(slope)
    pending.append((r, d))


def triangular_plate(p, n, side, depth, z, m, rounding):
    h = side*math.sqrt(3)/2
    outline = [(-side/2, -h/3), (side/2, -h/3), (0, 2*h/3)]
    vs = [(x, y, z+dz) for dz in (-depth/2, depth/2) for x, y in outline]
    faces = [(2, 1, 0), (3, 4, 5), (0, 1, 4, 3), (1, 2, 5, 4), (2, 0, 3, 5)]
    o = mesh(p, n, vs, faces, m)
    # Round the triangular outline in its plane first: depth alone would clamp bevel.
    mod = o.modifiers.new('Rounded triangle corners', 'BEVEL')
    mod.width = rounding
    mod.segments = 3
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(100)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bevel_mesh(o, min(.006, depth/4))
    return o


def warning_sign():
    r, k, d = start('warning_sign', '삼각 경고 표지', '소품',
                    '카메라 쪽으로 읽히는 기하학 느낌표. 판은 뒤로 15도 기울임.',
                    coll('cylinder', radius=.12, height=1.4))
    box(k, 'Foot', (0, 0, .06), (.35, .35, .12), 'Concrete', .035)
    pole = cyl(k, 'Post', (0, .045, .775), .035, 1.35, 'Metal', verts=12)
    bevel_mesh(pole, .009)
    sign = occ(r, d, 'Sign')
    triangular_plate(sign, 'RedBorder', .75, .04, 0, 'HatchRed', .028)
    # Equilateral inset: moving each edge inward .08 shrinks side by 2*sqrt(3)*.08.
    inner = .75-2*math.sqrt(3)*.08
    triangular_plate(sign, 'CreamFace', inner, .012, .021, 'LinePaint', .017)
    box(sign, 'ExclamationStem', (0, .025, .032), (.065, .185, .01), 'Ink', .012)
    dot = cyl(sign, 'ExclamationDot', (0, -.115, .032), .038, .01, 'Ink', verts=12)
    bevel_mesh(dot, .003)
    sign.location = (0, 0, 1.31)
    sign.rotation_euler.x = math.radians(75)
    bpy.context.view_layer.update()
    checks[r.name] = {'normal': sign.matrix_world.to_3x3() @ Vector((0, 0, 1))}
    pending.append((r, d))


def world_vertices(objects):
    return [o.matrix_world @ v.co for o in objects if o.type == 'MESH' for v in o.data.vertices]


def finish_asset(root, data):
    """Copied kit pipeline: role join, triangulate, seat, exact names, selected GLB.

    Adaptations: stage-props path, horizontal centering, assertions before export,
    and archive names after export so Blender's unique-name suffixes cannot leak.
    """
    bpy.context.view_layer.update()
    for group in list(root.children):
        role = group.name.split('.')[0]
        group['contract_name'] = role
        children = [o for o in group.children if o.type == 'MESH']
        assert children, (data['id'], 'empty role', role)
        bpy.ops.object.select_all(action='DESELECT')
        for o in children:
            o.select_set(True)
        bpy.context.view_layer.objects.active = children[0]
        if len(children) > 1:
            bpy.ops.object.join()
        o = bpy.context.object
        o.name = role+'_Mesh'
        o['contract_name'] = role+'_Mesh'
        mod = o.modifiers.new('Explicit triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.update()
    objs = [root]+list(root.children_recursive)
    verts = world_vertices(objs)
    lo = Vector(tuple(min(v[i] for v in verts) for i in range(3)))
    hi = Vector(tuple(max(v[i] for v in verts) for i in range(3)))
    shift = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
    for group in root.children:
        group.location -= shift
    bpy.context.view_layer.update()
    data['dimensions_m'] = [round(hi.x-lo.x, 3), round(hi.z-lo.z, 3), round(hi.y-lo.y, 3)]
    data['bounds_min_y'] = round(min(v.z for v in world_vertices(objs)), 6)
    data['triangles'] = sum(len(o.data.polygons) for o in objs if o.type == 'MESH')
    data['file'] = data['id']+'.glb'
    data['weather'] = 'PBR 원본 / 젖음·상향면 적설 재질 교체 가능 (미검수)'
    if data['collision']['kind'] != 'none':
        center = data['collision'].get('center', [0, data['collision'].get('height', 0)/2, 0])
        data['collision']['center'] = [round(center[0]-shift.x, 6),
                                       round(center[1]-shift.z, 6),
                                       round(center[2]+shift.y, 6)]
    # Move competing part names aside before assigning exact export-contract names.
    for other in bpy.data.objects:
        if other not in objs and other.name.startswith(('Keep', 'Occlude_')):
            other.name = 'Stored_'+other.name
    for group in root.children:
        group.name = group['contract_name']
        for o in group.children:
            o.name = o['contract_name']
    validate_asset(root, data, shift)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']), export_format='GLB',
                              use_selection=True, export_extras=True, export_yup=True,
                              export_animations=False)
    assets.append(data)
    # Source library uses root-prefixed names; GLB names above remain exact.
    for o in objs:
        if o is not root:
            o.name = data['id']+'__'+o.name


def validate_asset(root, data, shift):
    asset_id = data['id']
    groups = list(root.children)
    objs = list(root.children_recursive)
    meshes = [o for o in objs if o.type == 'MESH']
    assert root.name == asset_id and root['asset_contract'] == CONTRACT
    assert root.location.length < .00001
    assert {g.name for g in groups} == {'Keep', *data['occlusion_parts']}, asset_id
    for group in groups:
        assert group.type == 'EMPTY'
        assert group.name == 'Keep' or group.name.startswith('Occlude_')
        assert len(group.children) == 1, (asset_id, group.name)
        child = group.children[0]
        assert child.type == 'MESH' and child.name == group.name+'_Mesh'
        assert all(len(poly.vertices) == 3 for poly in child.data.polygons)
    materials = {slot.material for o in meshes for slot in o.material_slots if slot.material}
    assert len(materials) <= 6, (asset_id, 'materials', len(materials))
    assert all(not any(n.type == 'TEX_IMAGE' for n in m.node_tree.nodes) for m in materials)
    assert 0 < data['triangles'] < BUDGET[asset_id], (asset_id, data['triangles'])
    verts = world_vertices(meshes)
    assert abs(min(v.z for v in verts)) <= .002, asset_id
    for axis in (0, 1):
        assert abs(min(v[axis] for v in verts)+max(v[axis] for v in verts)) <= .002
    for actual, expected in zip(data['dimensions_m'], EXPECTED[asset_id]):
        assert .75*expected <= actual <= 1.25*expected, (asset_id, data['dimensions_m'])
    for group in groups:
        if group.name == 'Occlude_Lid':
            # Surface-area weighted centroid stays meaningful across tessellation density.
            total_area, centroid = 0.0, Vector((0, 0, 0))
            for o in group.children:
                for tri in o.data.polygons:
                    a, b, c = [o.matrix_world @ o.data.vertices[i].co for i in tri.vertices]
                    area = (b-a).cross(c-a).length*.5
                    centroid += (a+b+c)/3*area
                    total_area += area
            assert total_area > 0 and centroid.y/total_area > 0, (asset_id, 'lid must be behind')
    if asset_id == 'bunker_hatch':
        probe = checks[asset_id]
        assert (probe['valve_center']-shift).y < (probe['lid_center']-shift).y
    if asset_id == 'warning_sign':
        assert checks[asset_id]['normal'].y < 0


def main():
    PACK.mkdir(parents=True, exist_ok=True)
    (ROOT/'source').mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    M.update({name: mat(name, color, .68 if name != 'Ink' else .96)
              for name, color in [('HatchRed', 'c8413a'), ('Hazard', 'e2b84a'), ('Ink', '2a2a2a')]})
    floor_hatch()
    bunker_hatch()
    concrete_barrier()
    warning_sign()
    # Finish one at a time: exact node names are validated immediately before export.
    for root, data in pending:
        # Earlier exports archive competing names; recover the pending semantic names.
        for group in root.children:
            group.name = group.name.removeprefix('Stored_').split('.')[0]
        finish_asset(root, data)
    assert len(assets) == 4 and {a['id'] for a in assets} == set(EXPECTED)
    manifest = dict(revision=1, contract=CONTRACT, date='2026-10-22', units='metres',
                    up_axis='+Y', status='stage transition art; not integrated or render-reviewed',
                    assets=assets)
    (PACK/'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    # Layout is exclusively for the .blend library, after all origin-centered exports.
    for i, root in enumerate(roots):
        root.location = (i % 2*4.5, i//2*4.5, 0)
    for material in list(bpy.data.materials):
        if material.users == 0:
            bpy.data.materials.remove(material)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'source'/'transition-props.blend'))
    for data in assets:
        print('TRANSITION_PROP_READY', data['id'], data['triangles'])
    print('TRANSITION_PROPS_BUILT', len(assets))


if __name__ == '__main__':
    main()
