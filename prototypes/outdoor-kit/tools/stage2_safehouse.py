"""Standalone safehouse generator. Geometry helpers copied; no existing generator is imported.
See doc/stage2-safehouse-validation.md for the specification and validation plan.
"""
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector

REPO=Path(__file__).resolve().parents[3]
PACK=REPO/'game/assets/stage2-props'
SOURCE=REPO/'prototypes/outdoor-kit/source/stage2-safehouse.blend'
CONTRACT='plushpanic_outdoor_v1'
BUDGET=dict(safehouse=6000,safehouse_door=1800)
PALETTE=dict(Concrete='a4aaa1',Metal='56636a',MetalLight='a8b7b1',Hazard='e2b84a',Green='8fd19e',Red='b96551')
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


def start(aid, title, note, part=None, collision=None):
    root=empty(aid)
    root['asset_contract']=CONTRACT
    keep=empty('Keep',root)
    upper=empty('Occlude_'+part,root) if part else None
    data=dict(id=aid,name=title,category='Stage 2 랜드마크',note=note,
              collision=collision or dict(kind='none'),occlusion_parts=[upper.name] if upper else [])
    return root,keep,upper,data


def safehouse():
    root,keep,upper,data=start('safehouse','Stage 2 도착 세이프하우스',
        '외벽14×12 m/두께0.4 m/높이2.6 m, 서쪽 입구2.6×2.4 m. 게임 바닥/벽 충돌 사용. 제어판 버튼 게임로컬(-6.5,1.4,1.8), rear 반지붕 최저3.03 m.', 'Walls')
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
    # Inside close-door button; its center is the interaction marker, not a fake floor post.
    box(keep,'ControlPlate',(-6.55,-1.8,1.3),(.28,.46,.14),'Metal',.025)
    box(keep,'CloseButton',(-6.5,-1.8,1.4),(.18,.28,.06),'Red',.02)
    box(keep,'ButtonTrim',(-6.405,-1.8,1.3),(.01,.3,.05),'Hazard',.004)
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


def safehouse_door():
    root,keep,upper,data=start('safehouse_door','세이프하우스 슬라이딩 철문',
        '게임 X두께0.3/Y높이2.4/Z폭2.6 m, 바닥중심. Door가 +Z2.5 m 이동 후 벽 속 숨김. 충돌은 게임 소유.', 'Door')
    for group,z in ((keep,.6),(upper,1.8)):
        box(group,'Slab',(0,0,z),(.22,2.6,1.2),'Metal',.035)
        for y in (-1.22,1.22):
            box(group,'EdgeRib',(-.12,y,z),(.06,.12,1.12),'MetalLight',.015)
        for side in (-1,1):
            box(group,'HorizontalRib',(side*.12,0,z),(.06,2.35,.08),'MetalLight',.015)
    for side in (-1,1):
        box(upper,'WarningBand',(side*.123,0,2.16),(.054,2.3,.18),'Hazard',.008)
        box(keep,'Handle',(side*.125,-.7,1.02),(.05,.28,.12),'MetalLight',.015)
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
    expected=dict(safehouse=(14.15,3.75,12.1),safehouse_door=(.3,2.4,2.6))[aid]
    assert all(abs(a-b)<.03 for a,b in zip(data['dimensions_m'],expected)),(aid,'dimensions',data['dimensions_m'])
    for group in root.children:
        assert group.type=='EMPTY' and len(group.children)==1 and group.children[0].name==group.name+'_Mesh'
    # Unlike rear-only props, room walls and the movable door surround the player.
    # Their intentional symmetric occlusion groups have no rear-centroid requirement.


def main():
    path=PACK/'manifest.json'
    manifest=json.loads(path.read_text(encoding='utf-8')) if path.exists() else dict(revision=1,contract=CONTRACT,units='metres',up_axis='+Y',assets=[])
    assert manifest['contract']==CONTRACT and manifest['revision']==1
    assert len({a['id'] for a in manifest['assets']})==len(manifest['assets'])
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for mat in list(bpy.data.materials): bpy.data.materials.remove(mat,do_unlink=True)
    M.update({n:material(n,c) for n,c in PALETTE.items()})
    prepared=[]
    for build in (safehouse,safehouse_door):
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
    print('STAGE2_SAFEHOUSE_BUILT',len(prepared))


if __name__=='__main__': main()
