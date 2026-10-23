"""Standalone bundle B generator. See doc/model-briefs/stage2-landmarks.md sections 1/3.
Geometry helpers copied from bundle A; no generator is imported or executed.
"""
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game/assets/stage2-props'
SOURCE = REPO / 'prototypes/outdoor-kit/source/stage2-props-b.blend'
CONTRACT = 'plushpanic_outdoor_v1'
BUDGET = dict(bridge_truss=2000, bridge_leaf=1800, pump_house=5000, watch_post=5000)
PALETTE = dict(Concrete='a4aaa1', Metal='56636a', MetalLight='a8b7b1',
    Wood='8a6a46', WoodDark='5e4632', Red='b96551', Hazard='e2b84a', Green='8fd19e')
M = {}

def material(name, color):
    rgb = [int(color[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    rgb = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in rgb]
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*rgb, 1)
    result.use_nodes = True
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


def rail(parent, name, x, y, bottom, top, color, width=.07):
    """Rounded-square extrusion. No end bevel: identical stacked cross sections."""
    h, cut = width / 2, width * .17
    outline = [(-h + cut, -h), (h - cut, -h), (h, -h + cut), (h, h - cut),
               (h - cut, h), (-h + cut, h), (-h, h - cut), (-h, -h + cut)]
    vertices = [(x + u, y + v, z) for z in (bottom, top) for u, v in outline]
    faces = [tuple(reversed(range(8))), tuple(range(8, 16))]
    faces += [(i, (i + 1) % 8, (i + 1) % 8 + 8, i + 8) for i in range(8)]
    return mesh(parent, name, vertices, faces, color)


def tube_path(parent, name, points, radius, color):
    """Continuous capped eight-sided tube for a path in a YZ plane."""
    points = [Vector(p) for p in points]
    vertices, faces = [], []
    for i, point in enumerate(points):
        tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
        side = Vector((1, 0, 0))
        other = tangent.cross(side).normalized()
        for j in range(8):
            angle = j * math.tau / 8
            vertices.append(point + radius * (math.cos(angle) * side + math.sin(angle) * other))
    for i in range(len(points) - 1):
        for j in range(8):
            a, b = i * 8 + j, i * 8 + (j + 1) % 8
            faces.append((a, b, b + 8, a + 8))
    faces += [tuple(reversed(range(8))),
              tuple((len(points) - 1) * 8 + j for j in range(8))]
    return mesh(parent, name, vertices, faces, color)


def pulley(parent, name, center, radius, width, color='MetalLight', axis='X', segments=12):
    """Open spoked wheel with two lips and a recessed cable channel; no texture."""
    center = Vector(center)
    # Revolved section in (radius, axle distance), clockwise around solid rim.
    profile = [(radius * .65, -width / 2), (radius, -width / 2),
               (radius, -width * .28), (radius * .84, 0),
               (radius, width * .28), (radius, width / 2),
               (radius * .65, width / 2)]

    def point(r, angle, along):
        u, v = r * math.cos(angle), r * math.sin(angle)
        return center + Vector((along, u, v) if axis == 'X' else (u, along, -v))

    vertices = [point(r, i * math.tau / segments, d)
                for i in range(segments) for r, d in profile]
    count = len(profile)
    faces = []
    for i in range(segments):
        for j in range(count):
            faces.append((i * count + j, ((i + 1) % segments) * count + j,
                          ((i + 1) % segments) * count + (j + 1) % count,
                          i * count + (j + 1) % count))
    wheel = mesh(parent, name, vertices, faces, color)
    for i in range(4):
        beam(parent, name + 'Spoke', center, point(radius * .72, i * math.pi / 2, 0),
             radius * .09, color, 6)
    beam(parent, name + 'Hub', point(0, 0, -width * .7),
         point(0, 0, width * .7), radius * .21, 'Metal', 8)
    return wheel


def start(asset_id, name, note, occlusion=None):
    root = empty(asset_id)
    root['asset_contract'] = CONTRACT
    keep = empty('Keep', root)
    keep['visibility_role'] = 'keep'
    data = dict(id=asset_id, name=name, category='Stage 2 다리 임무·감시소', note=note,
                collision=dict(kind='none'), occlusion_parts=[])
    upper = None
    if occlusion:
        upper = empty('Occlude_' + occlusion, root)
        upper['visibility_role'] = 'occlude'
        data['occlusion_parts'].append(upper.name)
    return root, keep, upper, data


def bridge_truss():
    root, keep, front, data = start('bridge_truss', '다리 트러스',
        '길이 X 6 m, 폭 4 m. 피벗은 상판 윗면. 보행 상판은 게임이 제공. 앞 난간만 Occlude_Rail.', 'Rail')
    for y in (-1.9, 1.9):
        group = front if y < 0 else keep
        box(keep, 'Fascia', (0, y, -.15), (6, .2, .3), 'WoodDark')
        box(keep, 'BottomChord', (0, y, -1.63), (6, .16, .14), 'Metal')
        for x in (-3, -1.5, 0, 1.5, 3):
            # End posts are inset, so adjacent modules do not overlap.
            px = max(-2.9, min(2.9, x))
            # Rail/post skins must not share a plane at front faces or at the top.
            box(group, 'Post', (px, y, .485), (.14, .18, .97), 'Wood')
        for z in (.48, .94):
            box(group, 'Handrail', (0, y, z), (6, .14, .12), 'Wood')
        for x in (-3, -1.5, 0, 1.5):
            a, b = max(-2.94,x), min(2.94,x+1.5)
            beam(keep, 'TrussA', (a, y, -.34), (b, y, -1.57), .065, 'Metal', 4)
            beam(keep, 'TrussB', (a, y, -1.57), (b, y, -.34), .065, 'Metal', 4)
    for x in (-2.9, 0, 2.9):
        box(keep, 'CrossTie', (x, 0, -.32), (.16, 3.8, .14), 'Metal')
    return root, data


def bridge_leaf():
    root, keep, leaf, data = start('bridge_leaf', '들린 다리 판',
        '피벗=경첩 축 아래/상판 높이. 판 4.4×3.6×0.3 m, 경첩 폭 4.6 m. 게임에서 Y -90° 회전(서쪽 진입면에 경고), 개방 시 -3.9 m 내려 숨김. Keep 없음.', 'Leaf')
    bpy.data.objects.remove(keep, do_unlink=True)
    for i in range(11):
        box(leaf, 'Plank', (-2 + i * .4, 0, 1.8), (.392, .3, 3.6), 'WoodDark', .012)
    for x in (-2.14, 2.14):
        box(leaf, 'Rim', (x, -.17, 1.8), (.12, .08, 3.6), 'Metal')
    for z in (.12, 1.2, 2.4, 3.48):
        box(leaf, 'Strap', (0, -.18, z), (4.4, .08, .12), 'Metal')
    beam(leaf, 'Hinge', (-2.3, 0, .175), (2.3, 0, .175), .175, 'Metal', 12)
    for x in (-1.85, 1.85):
        # Alternating oval links with real holes, not disconnected beads.
        a, b = Vector((x, .12, 3.35)), Vector((x, 1.05, 1.8))
        tangent=(b-a).normalized()
        for i in range(6):
            p=a.lerp(b,i/5)
            side=Vector((1,0,0)) if i%2 else tangent.cross(Vector((1,0,0))).normalized()
            normal=tangent.cross(side).normalized()
            verts=[]
            for j in range(8):
                angle=j*math.tau/8
                along=tangent*math.cos(angle)*.21+side*math.sin(angle)*.07
                radial=(tangent*math.cos(angle)+side*math.sin(angle)).normalized()
                for k in range(4):
                    cross=k*math.tau/4
                    verts.append(p+along+.025*(radial*math.cos(cross)+normal*math.sin(cross)))
            faces=[(j*4+k,((j+1)%8)*4+k,((j+1)%8)*4+(k+1)%4,j*4+(k+1)%4)
                   for j in range(8) for k in range(4)]
            mesh(leaf,'ChainLink',verts,faces,'MetalLight')
    for i in range(11):
        box(leaf, 'Warning', (-2 + i * .4, -.232, .32), (.26, .025, .18), 'Hazard' if i % 2 == 0 else 'Metal', 0)
    return root, data


def wall(group, name, x0, x1, y0, y1, z0, z1, color='Concrete'):
    return box(group, name, ((x0+x1)/2, (y0+y1)/2, (z0+z1)/2),
               (x1-x0, y1-y0, z1-z0), color, .015)


def pump_house():
    root, keep, roof, data = start('pump_house', '계곡 펌프장',
        '바닥 피벗. 정면은 게임 +Z, 전면 3 m 개구부. 밸브 중심(게임 X/Y/Z m)=[0,1.1,1.6]. 건물 6×5 m, 후면 높이 3.2 m, 전면 처마 2.2 m. 배관 꼭대기 3 m.', 'Roof')
    # Split every wall at 1.2 m. Front has a full-height 3 m opening.
    segments = [(-3,-2.7,-2.5,2.5), (2.7,3,-2.5,2.5),
                (-2.7,2.7,2.2,2.5), (-2.7,-1.5,-2.5,-2.2), (1.5,2.7,-2.5,-2.2)]
    for x0,x1,y0,y1 in segments:
        wall(keep, 'LowWall', x0,x1,y0,y1,0,1.2)
        wall(roof, 'HighWall', x0,x1,y0,y1,1.2,2.15)
    wall(roof, 'RearGable', -2.7,2.7,2.2,2.5,2.15,3.08)
    # Sloped side infill; roof rear high, camera-facing eave low.
    for x0,x1 in ((-3,-2.7),(2.7,3)):
        mesh(roof, 'SlopeWall', [(x,y,z) for x in (x0,x1) for y,z in ((-2.5,2.15),(2.5,2.15),(2.5,3.08))],
             [(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)], 'Concrete')
    for i in range(24):
        panel = box(roof, 'Corrugation', (-2.875+i*.25,0,2.67), (.25,5.12,.085 if i%2 else .12), 'Metal', .01)
        panel.rotation_euler.x = math.atan2(1,5)
    beam(keep, 'PumpDrum', (0,0,.72), (0,-1.2,.72), .7, 'MetalLight', 16)
    box(keep, 'PumpFoot', (0,-.5,.12), (1.8,1.7,.24), 'Metal')
    beam(keep, 'ValveStem', (0,-1.15,1.1), (0,-1.6,1.1), .1, 'Metal', 10)
    pulley(keep, 'Valve', (0,-1.6,1.1), .4,.09,'Red',axis='Y')
    for x in (-1.25,1.25):
        beam(keep,'PumpConnector',(math.copysign(.5,x),0,.55),(x,0,.55),.18,'Metal',8)
        tube_path(keep, 'LowPipe', [(x,0,.55),(x,1.7,.55),(x,1.9,1.15)], .18,'Metal')
        tube_path(roof, 'Riser', [(x,1.9,1.15),(x,1.9,2.7),(x,2.2,2.82),(x,2.65,2.82)], .18,'Metal')
    box(roof, 'Lintel', (0,-2.36,2.03), (3,.3,.24), 'Metal')
    for i in range(10):
        box(roof, 'Warning', (-1.35+i*.3,-2.52,2.03), (.21,.025,.18), 'Hazard' if i%2==0 else 'Metal', .005)
    return root, data


def watch_post():
    root, keep, upper, data = start('watch_post', '감시소',
        '외곽 12×10 m, 벽 높이 2.6/두께 0.4 m. ±X 입구 중앙 3 m. 기존 벽 충돌 유지. 후면(게임 -Z) 절반 지붕; 후면 창은 시각 전용, 통과 불가. 바닥은 게임 제공.', 'Walls')
    def boards(group,name,x0,x1,y0,y1,z0,z1):
        if z0 == 0:
            wall(group,'Foundation',x0,x1,y0,y1,0,.24)
            z0=.24
        count=math.ceil((z1-z0)/.32)
        step=(z1-z0)/count
        for i in range(count):
            wall(group,name,x0,x1,y0,y1,z0+i*step+.004,z0+(i+1)*step-.004,'Wood')
    # Exact footprints match LayoutMap.BuildHut. Back windows begin above the keep band.
    boards(keep,'FrontLow',-6,6,-5,-4.6,0,1.2)
    boards(upper,'FrontHigh',-6,6,-5,-4.6,1.2,2.6)
    boards(keep,'RearLow',-6,6,4.6,5,0,1.2)
    for a,b in ((-6,-4),(-1,1),(4,6)):
        boards(upper,'WindowPier',a,b,4.6,5,1.2,2.6)
    for a,b in ((-4,-1),(1,4)):
        boards(upper,'WindowLintel',a,b,4.6,5,2.15,2.6)
        wall(upper,'WindowSill',a,b,4.55,5,1.2,1.28,'MetalLight')
        for x in (a+.75,b-.75):
            beam(upper,'WindowBar',(x,4.8,1.28),(x,4.8,2.15),.035,'Metal')
    for x0,x1 in ((-6,-5.6),(5.6,6)):
        for y0,y1 in ((-4.6,-1.5),(1.5,4.6)):
            boards(keep,'SideLow',x0,x1,y0,y1,0,1.2)
            boards(upper,'SideHigh',x0,x1,y0,y1,1.2,2.6)
        wall(upper,'EntranceLintel',x0,x1,-1.5,1.5,2.35,2.6,'Wood')
        # Inset canopy avoids exceeding the 12 m collision footprint.
        box(upper,'Canopy',(math.copysign(5.55,x0),0,2.55),(.65,3.4,.1),'Metal')
    for i in range(30):
        box(upper,'HalfRoof',(-5.8+i*.4,2.5,2.66),(.4,5,.1 if i%2 else .12),'Metal')
    box(upper,'LampHousing',(0,2.5,2.85),(.55,.45,.3),'Metal')
    box(upper,'GreenLamp',(0,2.5,3.04),(.4,.3,.12),'Green')
    # Empty wall shelf, clear of both doorways and the existing supply points.
    for z in (.45,.95):
        box(keep,'Shelf',(0,4.15,z),(3,.65,.1),'Wood')
    for x in (-1.35,1.35):
        box(keep,'ShelfLeg',(x,4.15,.5),(.1,.5,1),'Metal')
    return root, data


def vertices(objects):
    return [obj.matrix_world @ v.co for obj in objects if obj.type == 'MESH' for v in obj.data.vertices]


def bounds(points):
    return (Vector(tuple(min(v[i] for v in points) for i in range(3))),
            Vector(tuple(max(v[i] for v in points) for i in range(3))))


def prepare(root, data):
    bpy.context.view_layer.update()
    for group in root.children:
        objects = list(group.children)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        if len(objects) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = group.name + '_Mesh'
        mod = obj.modifiers.new('Explicit triangles', 'TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.update()
    lo,hi = bounds(vertices(root.children_recursive))
    dims = hi-lo
    data.update(file=data['id']+'.glb', dimensions_m=[round(dims.x,4),round(dims.z,4),round(dims.y,4)],
                bounds_min_y=round(lo.z,6), triangles=sum(len(o.data.polygons) for o in root.children_recursive if o.type=='MESH'),
                weather='PBR 원본 / 젖음·상향면 적설 재질 교체 가능 (미검수)')
    groups = {g.name for g in root.children}
    assert groups == set(data['occlusion_parts']) | (set() if data['id']=='bridge_leaf' else {'Keep'})
    mats = {s.material for o in root.children_recursive if o.type=='MESH' for s in o.material_slots}
    assert 0 < len(mats) <= 6, (data['id'],'materials',len(mats))
    assert 0 < data['triangles'] < BUDGET[data['id']], (data['id'],'triangles',data['triangles'])
    assert all(not any(n.type=='TEX_IMAGE' for n in m.node_tree.nodes) for m in mats)
    aid=data['id']
    assert root.location.length < .00001
    assert abs(lo.z-(-1.7 if aid=='bridge_truss' else 0)) < .002, (aid,'floor pivot',lo.z)
    expected=dict(bridge_truss=(6,2.7,4),bridge_leaf=(4.6,3.6,1.4),pump_house=(6,3.2,5),watch_post=(12,3.1,10))[aid]
    assert all(.85*b<=a<=1.15*b for a,b in zip(data['dimensions_m'],expected)), (aid,'dimensions',data['dimensions_m'])
    if aid=='bridge_truss':
        assert abs(dims.x-6)<.002 and abs(dims.y-4)<.002 and abs(hi.z-1)<.002
    if aid=='watch_post':
        assert abs(dims.x-12)<.002 and abs(dims.y-10)<.002
    for group in root.children:
        assert group.type=='EMPTY' and len(group.children)==1
        assert group.children[0].name==group.name+'_Mesh'
        if group.name.startswith('Occlude_'):
            obj=group.children[0]
            weighted=Vector((0,0,0))
            area=0
            for face in obj.data.polygons:
                a,b,c=[obj.matrix_world@obj.data.vertices[i].co for i in face.vertices]
                weight=(b-a).cross(c-a).length/2
                weighted+=(a+b+c)*(weight/3)
                area+=weight
            cy=(weighted/area).y
            # Front rail is explicitly camera-facing; the complete leaf is exempt.
            if aid=='bridge_truss':
                assert cy<0, (aid,'front rail centroid',cy)
            elif aid!='bridge_leaf':
                assert cy>0, (aid,'rear occluder centroid',cy)
    return data


def main():
    manifest_path = PACK/'manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8')) if manifest_path.exists() else dict(
        revision=1,contract=CONTRACT,date='2026-10-23',units='metres',up_axis='+Y',assets=[])
    assert manifest['contract']==CONTRACT and manifest['revision']==1
    assert len({a['id'] for a in manifest['assets']})==len(manifest['assets'])
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    M.update({n:material(n,c) for n,c in PALETTE.items()})
    prepared=[]
    for build in (bridge_truss,bridge_leaf,pump_house,watch_post):
        root,data=build()
        prepare(root,data)
        for obj in root.children_recursive:
            obj['contract_name']=obj.name
            obj.name=data['id']+'__'+obj.name
        prepared.append((root,data))
    PACK.mkdir(parents=True,exist_ok=True)
    SOURCE.parent.mkdir(parents=True,exist_ok=True)
    for root,data in prepared:
        objects=[root]+list(root.children_recursive)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            if obj!=root:
                obj.name=obj['contract_name']
            obj.select_set(True)
        bpy.context.view_layer.objects.active=root
        assert bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']),export_format='GLB',use_selection=True,
            export_extras=True,export_yup=True,export_animations=False)=={'FINISHED'}
        for obj in objects[1:]:
            obj.name=data['id']+'__'+obj['contract_name']
    replacements={d['id']:d for _,d in prepared}
    manifest['assets']=[replacements.pop(a['id'],a) for a in manifest['assets']]
    manifest['assets'].extend(replacements.values())
    for i,(root,_) in enumerate(prepared):
        root.location=((i%2)*18,(i//2)*16,0)
    assert bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))=={'FINISHED'}
    temporary=PACK/'manifest.json.tmp'
    temporary.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    temporary.replace(manifest_path)
    for _,data in prepared:
        print('STAGE2_PROP_READY',data['id'],data['triangles'],data['dimensions_m'])
    print('STAGE2_PROPS_BUILT B',len(prepared))


if __name__=='__main__':
    main()
