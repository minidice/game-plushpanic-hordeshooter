"""Standalone bundle C generator; no other generator is imported or executed."""
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector
REPO=Path(__file__).resolve().parents[3]
PACK=REPO/'game/assets/stage2-props'
SOURCE=REPO/'prototypes/outdoor-kit/source/stage2-props-c.blend'
CONTRACT='plushpanic_outdoor_v1'
BUDGET=dict(watchtower=6000,crane=4000,alarm_truck=4500,bunker_gate=5000)
PALETTE=dict(Concrete='a4aaa1',Metal='56636a',MetalLight='a8b7b1',Wood='8a6a46',WoodDark='5e4632',Ochre='c7a35c',Rubber='35444a',Rust='98674f',Red='b96551',Hazard='e2b84a')
M={}

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


def start(aid, title, note, part=None, collision=None):
    root=empty(aid)
    root['asset_contract']=CONTRACT
    keep=empty('Keep',root)
    upper=empty('Occlude_'+part,root) if part else None
    data=dict(id=aid,name=title,category='Stage 2 랜드마크',note=note,
              collision=collision or dict(kind='none'),occlusion_parts=[upper.name] if upper else [])
    return root,keep,upper,data


def watchtower():
    root,keep,top,data=start('watchtower','숲 감시탑',
        '피벗 바닥중심. 14×10 m, 게임 상판4.8 m(모델에는 없음), 지붕7.5 m/처마7.15 m. 남쪽 사다리 입구 X±1.2 m. 짚라인 기둥 (게임로컬4,4.8,-1)부터 동쪽 출발 경로까지 지붕 홈 X3.1..7 / Z-2.1..0, 동쪽 난간 Z±1.2 m 개방.', 'Top')
    for x in (-6.5,0,6.5):
        for y in (-4.5,4.5):
            box(keep,'LowerLeg',(x,y,1.2),(.42,.42,2.4),'WoodDark',.04)
            box(top,'UpperLeg',(x,y,3.6),(.42,.42,2.4),'WoodDark',.04)
    for x in (-6.5,6.5):
        for y in (-4.5,4.5):
            box(keep,'Foot',(x,y,.2),(.9,.9,.4),'Concrete',.08)
    for y in (-4.5,4.5):
        for a,b in ((-6.5,0),(0,6.5)):
            beam(keep,'LowerBraceA',(a,y,.45),((a+b)/2,y,2.4),.12,'Wood',6)
            beam(top,'UpperBraceA',((a+b)/2,y,2.4),(b,y,4.35),.12,'Wood',6)
            beam(top,'UpperBraceB',(a,y,4.35),((a+b)/2,y,2.4),.12,'Wood',6)
            beam(keep,'LowerBraceB',((a+b)/2,y,2.4),(b,y,.45),.12,'Wood',6)
    for x in (-6.5,6.5):
        beam(top,'SideBrace',(x,-4.5,2.5),(x,4.5,4.35),.12,'Wood',6)
    for y in (-4.86,4.86):
        box(top,'DeckFascia',(0,y,4.65),(14,.28,.3),'WoodDark')
    for x in (-6.86,6.86):
        box(top,'SideFascia',(x,0,4.65),(.28,9.44,.3),'WoodDark')
    # Front opening follows the existing centered ladder; rear half carries roof.
    for y,height in ((-4.85,.6),(4.85,1)):
        runs=((-6.9,-1.2),(1.2,6.9)) if y<0 else ((-6.9,6.9),)
        for a,b in runs:
            for z in (4.8+height*.5,4.8+height-.075):
                box(top,'Rail',((a+b)/2,y,z),(b-a,.16,.15),'Wood')
            count=math.ceil((b-a)/1.9)
            for i in range(count+1):
                x=a+(b-a)*i/count
                box(top,'RailPost',(x,y,4.8+(height-.04)/2),(.2,.22,height-.04),'Wood')
    for x in (-6.85,6.85):
        for z in (5.25,5.725):
            for a,b in (((-4.75,4.75),) if x<0 else ((-4.75,-1.2),(1.2,4.75))):
                box(top,'SideRail',(x,(a+b)/2,z),(.16,b-a,.15),'Wood')
        for y in ((-4.5,0,4.5) if x<0 else (-4.5,4.5)):
            box(top,'SidePost',(x,y,5.28),(.22,.2,.96),'Wood')
        for y in ((.15,4.65) if x<0 else (2.1,4.65)):
            box(top,'RoofPost',(x,y,5.975),(.26,.26,2.35),'WoodDark')
    # Keep 2.18 m survivors clear beneath eaves; open the whole departure corridor.
    for a,b,y0 in ((-7,0,0),(0,3.1,0),(3.1,7,2.1)):
        z0,z1=7.5-abs(a)*.05,7.5-abs(b)*.05
        verts=[(a,y0,z0),(b,y0,z1),(b,5,z1),(a,5,z0),
               (a,y0,z0-.12),(b,y0,z1-.12),(b,5,z1-.12),(a,5,z0-.12)]
        mesh(top,'RoofPanel',verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],'WoodDark')
    beam(top,'FlagPole',(0,3.8,7.5),(0,3.8,8.2),.035,'Metal',8)
    box(top,'Flag',(.26,3.8,7.98),(.5,.025,.24),'Hazard',0)
    return root,data


def crane():
    root,keep,mast,data=start('crane','야적장 크레인',
        '탑 바닥중심, 상단14 m. 팔 X-6..12 m, 후면 Y(Blender)+0.5 m. 상부/줄/갈고리는 시각전용. 게임에서 기초2.4×4×2.4 m 충돌만 사용.', 'Mast')
    box(keep,'Base',(0,0,.2),(2.4,2.4,.4),'Concrete',.08)
    for x in (-.8,.8):
        for y in (-.8,.8):
            box(keep,'LowerChord',(x,y,2.1),(.2,.2,3.8),'Ochre')
            box(mast,'UpperChord',(x,y,8.9),(.2,.2,9.8),'Ochre')
    for a,b in ((.5,2.25),(2.25,4),(4,6.4),(6.4,8.8),(8.8,11.2),(11.2,13.6)):
        part=keep if b<=4 else mast
        for side in (-.8,.8):
            beam(part,'FaceBrace',(-.8,side,a),(.8,side,b),.1,'Ochre',4)
            beam(part,'SideBrace',(side,-.8,a),(side,.8,b),.1,'Ochre',4)
        box(part,'Collar',(0,0,b-.06),(1.9,1.9,.12),'Metal')
    for y in (.1,.9):
        for z in (13.15,13.9):
            box(mast,'BoomChord',(3,y,z),(18,.2,.2),'Ochre')
        for i in range(9):
            x=-5.9+i*1.98
            beam(mast,'BoomDiagonal',(x,y,13.25),(x+1.78,y,13.8),.09,'Ochre',4)
    for x in (-5.8,-2,2,6,10,11.8):
        box(mast,'BoomCross',(x,.5,13.9),(.16,.8,.16),'Metal')
    box(mast,'Counterweight',(-4.8,.5,12.65),(2,1.4,1),'Concrete',.08)
    box(mast,'Cab',(-.4,.5,12.65),(1.3,1.4,.8),'Ochre',.06)
    box(mast,'CabWindow',(-.4,-.215,12.7),(.9,.04,.4),'Metal')
    beam(mast,'Cable',(11.6,.5,13.1),(11.6,.5,4.7),.035,'Metal',8)
    box(mast,'HookBlock',(11.6,.5,4.85),(.35,.3,.4),'Ochre')
    points=[(11.6,.5,4.65),(11.6,.5,4.35),(11.8,.5,4.15),(12,.5,4.3),(12,.5,4.5)]
    for a,b in zip(points,points[1:]):
        beam(mast,'Hook',a,b,.075,'Metal',8)
    return root,data


def horn(parent,x):
    # Hollow square flare, mouth toward Blender -Y / game +Z.
    verts=[]
    for y,r in ((.35,.1),(-.4,.28),(-.4,.22),(.25,.06)):
        verts.extend((x+u,y,1.85+v) for u,v in ((-r,-r),(r,-r),(r,r),(-r,r)))
    faces=[]
    for ring in range(3):
        for i in range(4):
            faces.append((ring*4+i,ring*4+(i+1)%4,(ring+1)*4+(i+1)%4,(ring+1)*4+i))
    mesh(parent,'Loudspeaker',verts,faces,'Metal')
    box(parent,'HornBack',(x,.28,1.85),(.15,.04,.15),'Rubber',0)
    beam(parent,'HornMount',(x,.1,1.25),(x,.1,1.65),.08,'Metal',8)


def alarm_truck():
    root,keep,_,data=start('alarm_truck','경보 트럭',
        '게임길이X5.2 m/폭Z2.2 m/높이2.2 m. 피벗 지면. Red(경광등)/Hazard(램프) 재질은 게임 AlarmCar가 인스턴스별 발광으로 교체. 사격·폭발로 1회 경보.',
        collision=dict(kind='box',size=[5.2,2,2.2],center=[0,1,0]))
    box(keep,'Chassis',(0,0,.63),(4.9,1.65,.32),'Metal',.06)
    box(keep,'Body',(0,0,.95),(4.9,1.9,.5),'Ochre',.08)
    for x in (-2.5,2.5):
        box(keep,'Bumper',(x,0,.65),(.2,1.95,.22),'Metal',.04)
    box(keep,'Hood',(-1.85,0,1.25),(1.25,1.82,.25),'Ochre',.08)
    box(keep,'Cab',(-.55,0,1.55),(1.4,1.82,.9),'Ochre',.1)
    box(keep,'Windshield',(-1.265,0,1.64),(.03,1.45,.48),'Metal',.01)
    for y in (-.925,.925):
        box(keep,'SideWindow',(-.55,y,1.67),(1,.03,.42),'Metal',.01)
        box(keep,'DoorHandle',(-.35,y*1.015,1.35),(.24,.04,.05),'Metal',.01)
        box(keep,'BedSide',(1.45,y,1.28),(2,.13,.42),'Ochre')
        box(keep,'RustPatch',(1.9,y*1.08,1.15),(.38,.025,.16),'Rust',.01)
    box(keep,'Tailgate',(2.4,0,1.28),(.15,1.72,.42),'Ochre')
    for x in (-1.6,1.6):
        for y in (-.94,.94):
            beam(keep,'Tyre',(x,y-.16,.4),(x,y+.16,.4),.4,'Rubber',12)
            beam(keep,'Hub',(x,y-.17,.4),(x,y+.17,.4),.2,'Metal',12)
    horn(keep,1)
    horn(keep,1.8)
    box(keep,'LightBarBase',(-.55,0,2.035),(.4,1.5,.07),'Metal')
    for y in (-.5,.5):
        box(keep,'RedBeacon',(-.55,y,2.135),(.33,.4,.13),'Red',.03)
    box(keep,'AmberBeacon',(-.55,0,2.13),(.3,.4,.12),'Hazard',.025)
    for y in (-.62,.62):
        box(keep,'Headlamp',(-2.465,y,1.12),(.05,.3,.18),'Hazard',.025)
        box(keep,'TailLamp',(2.49,y,1.22),(.04,.18,.13),'Red',.02)
    return root,data


def bunker_gate():
    root,keep,upper,data=start('bunker_gate','벙커 고지 정문',
        '닫힌 배경정문, 열림 상호작용 없음. 바닥피벗, X8×Y4×Z1.4 m. 하부1.2 m Keep. 문 뒤/통과는 막힌 충돌.', 'Gate',
        collision=dict(kind='box',size=[8,4,1.4],center=[0,2,0]))
    for x in (-3.5,3.5):
        box(keep,'Foundation',(x,.15,.6),(1,1.1,1.2),'Concrete',.06)
        box(upper,'Pier',(x,.15,2.3),(1,1.1,2.2),'Concrete',.06)
    box(upper,'Lintel',(0,.15,3.7),(8,1.1,.6),'Concrete',.06)
    for x in (-1.49,1.49):
        box(keep,'DoorLow',(x,.13,.6),(2.96,.18,1.2),'Metal',.02)
        box(upper,'DoorHigh',(x,.13,2.3),(2.96,.18,2.2),'Metal',.02)
        for z in (1.55,2.65):
            stripe=box(upper,'Warning',(x,.015,z),(2.3,.025,.2),'Hazard',.01)
            stripe.rotation_euler.y=.35
        box(upper,'Handle',(x*.12,-.04,1.65),(.09,.13,.36),'MetalLight')
    for x in (-2.4,2.4):
        box(upper,'LampHousing',(x,-.46,3.66),(.7,.25,.3),'Metal')
        box(upper,'LampGlass',(x,-.595,3.66),(.5,.025,.15),'Hazard')
    for side in (-1,1):
        for row in range(3):
            for i in range(2):
                box(keep,'Sandbag',(side*(2.95+i*.53),-.52,row*.26+.14),(.57,.36,.28),'Ochre',.1)
    return root,data


def prepare(root,data):
    bpy.context.view_layer.update()
    for group in root.children:
        objects=list(group.children)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects: obj.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        if len(objects)>1: bpy.ops.object.join()
        obj=bpy.context.object
        obj.name=group.name+'_Mesh'
        mod=obj.modifiers.new('Explicit triangles','TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.context.view_layer.update()
    points=[o.matrix_world@v.co for o in root.children_recursive if o.type=='MESH' for v in o.data.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    dims=hi-lo
    aid=data['id']
    data.update(file=aid+'.glb',dimensions_m=[round(dims.x,4),round(dims.z,4),round(dims.y,4)],bounds_min_y=round(lo.z,6),
        triangles=sum(len(o.data.polygons) for o in root.children_recursive if o.type=='MESH'),weather='PBR 원본 / 젖음·적설 미검수')
    assert root.location.length<.00001 and abs(lo.z)<.002,(aid,'floor pivot',lo.z)
    assert {g.name for g in root.children}=={'Keep',*data['occlusion_parts']}
    materials={s.material for o in root.children_recursive if o.type=='MESH' for s in o.material_slots}
    assert 0<len(materials)<=6,(aid,'materials',len(materials))
    assert all(not any(n.type=='TEX_IMAGE' for n in m.node_tree.nodes) for m in materials)
    assert 0<data['triangles']<BUDGET[aid],(aid,'triangles',data['triangles'])
    expected=dict(watchtower=(14,8.2,10),crane=(18,14,2.4),alarm_truck=(5.2,2.2,2.2),bunker_gate=(8,4,1.4))[aid]
    assert all(.85*b<=a<=1.15*b for a,b in zip(data['dimensions_m'],expected)),(aid,'dimensions',data['dimensions_m'])
    if aid=='watchtower':
        assert abs(lo.x+7)<.02 and abs(hi.x-7)<.02 and abs(lo.y+5)<.02 and abs(hi.y-5)<.02
        assert any(abs(p.z-4.8)<.002 for p in points),'tower supports must meet 4.8 m deck'
    for group in root.children:
        assert group.type=='EMPTY' and len(group.children)==1 and group.children[0].name==group.name+'_Mesh'
        if group.name.startswith('Occlude_'):
            o=group.children[0]
            area=0
            weighted=Vector((0,0,0))
            for face in o.data.polygons:
                a,b,c=[o.matrix_world@o.data.vertices[i].co for i in face.vertices]
                weight=(b-a).cross(c-a).length/2
                area+=weight; weighted+=(a+b+c)*weight/3
            assert (weighted/area).y>0,(aid,'rear occluder centroid',weighted/area)


def main():
    path=PACK/'manifest.json'
    manifest=json.loads(path.read_text(encoding='utf-8')) if path.exists() else dict(revision=1,contract=CONTRACT,units='metres',up_axis='+Y',assets=[])
    assert manifest['contract']==CONTRACT and manifest['revision']==1
    assert len({a['id'] for a in manifest['assets']})==len(manifest['assets'])
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials): bpy.data.materials.remove(mat,do_unlink=True)
    M.update({n:material(n,c) for n,c in PALETTE.items()})
    prepared=[]
    for build in (watchtower,crane,alarm_truck,bunker_gate):
        root,data=build(); prepare(root,data)
        for obj in root.children_recursive:
            obj['contract_name']=obj.name; obj.name=data['id']+'__'+obj.name
        prepared.append((root,data))
    PACK.mkdir(parents=True,exist_ok=True); SOURCE.parent.mkdir(parents=True,exist_ok=True)
    for root,data in prepared:
        objects=[root]+list(root.children_recursive)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            if obj!=root: obj.name=obj['contract_name']
            obj.select_set(True)
        bpy.context.view_layer.objects.active=root
        assert bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']),export_format='GLB',use_selection=True,export_extras=True,export_yup=True,export_animations=False)=={'FINISHED'}
        for obj in objects[1:]: obj.name=data['id']+'__'+obj['contract_name']
    replacements={d['id']:d for _,d in prepared}
    manifest['assets']=[replacements.pop(a['id'],a) for a in manifest['assets']]
    manifest['assets'].extend(replacements.values())
    for i,(root,_) in enumerate(prepared): root.location=((i%2)*25,(i//2)*22,0)
    assert bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))=={'FINISHED'}
    temporary=PACK/'manifest.json.tmp'
    temporary.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8'); temporary.replace(path)
    for _,data in prepared: print('STAGE2_PROP_READY',data['id'],data['triangles'],data['dimensions_m'])
    print('STAGE2_PROPS_BUILT C',len(prepared))


if __name__=='__main__': main()
