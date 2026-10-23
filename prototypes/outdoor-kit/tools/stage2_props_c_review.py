"""Independent GLB reimport checks and front/back Cycles renders for bundle C.

Run Blender --background --factory-startup --python-exit-code 1 --python <file>.
Append -- --checks-only to skip rendering. Outputs are ignored game/output/.
"""
import json
import math
import struct
import sys
from pathlib import Path
import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACK = REPO/'game/assets/stage2-props'
OUT = REPO/'game/output/stage2-props-c-review'
BUDGET = dict(watchtower=6000,crane=4000,alarm_truck=4500,bunker_gate=5000)
ROLES = dict(watchtower=['Keep','Occlude_Top'],crane=['Keep','Occlude_Mast'],alarm_truck=['Keep'],bunker_gate=['Keep','Occlude_Gate'])
REPORT = dict(assets=[],checks=[],renders=[])


def check(ok,label):
    assert ok,label
    REPORT['checks'].append(label)


def bounds(objects):
    bpy.context.view_layer.update()
    points=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))),Vector(tuple(max(p[i] for p in points) for i in range(3)))


def validate(entry):
    aid=entry['id']
    payload=(PACK/entry['file']).read_bytes()
    magic,version,length=struct.unpack_from('<4sII',payload)
    check(magic==b'glTF' and version==2 and length==len(payload),aid+' GLB header')
    count,kind=struct.unpack_from('<I4s',payload,12)
    check(kind==b'JSON',aid+' JSON chunk')
    gltf=json.loads(payload[20:20+count])
    check(not gltf.get('textures') and not gltf.get('images'),aid+' no textures')
    check(0<len(gltf['materials'])<=6,aid+' material budget')
    check(entry['collision']['kind']==('box' if aid in ('alarm_truck','bunker_gate') else 'none'),aid+' collision contract')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    check(bpy.ops.import_scene.gltf(filepath=str(PACK/entry['file']))=={'FINISHED'},aid+' reimport')
    root=bpy.data.objects.get(aid)
    check(root is not None and root.get('asset_contract')=='plushpanic_outdoor_v1',aid+' root contract')
    check(root.location.length<.0001,aid+' origin')
    check({o.name for o in root.children}==set(ROLES[aid]),aid+' exact role groups')
    triangles=0
    for role in root.children:
        check(role.type=='EMPTY' and len(role.children)==1,aid+' '+role.name+' one mesh')
        obj=role.children[0]
        check(obj.type=='MESH' and obj.name==role.name+'_Mesh' and not obj.children,aid+' '+role.name+' mesh name')
        obj.data.calc_loop_triangles()
        triangles+=len(obj.data.loop_triangles)
        if role.name.startswith('Occlude_'):
            weighted=Vector((0,0,0))
            area=0
            for tri in obj.data.loop_triangles:
                a,b,c=[obj.matrix_world@obj.data.vertices[i].co for i in tri.vertices]
                weight=(b-a).cross(c-a).length/2
                weighted+=(a+b+c)*(weight/3)
                area+=weight
            cy=(weighted/area).y
            check(cy>0,aid+' occlusion direction')
    check(triangles==entry['triangles'] and triangles<BUDGET[aid],aid+' triangle count')
    lo,hi=bounds(root.children_recursive)
    dims=hi-lo
    check(all(abs(a-b)<.002 for a,b in zip((dims.x,dims.z,dims.y),entry['dimensions_m'])),aid+' manifest dimensions')
    floor=0
    check(abs(lo.z-floor)<.002 and abs(entry['bounds_min_y']-floor)<.002,aid+' floor pivot')
    expected=dict(watchtower=(14,8.2,10),crane=(18,14,2.4),alarm_truck=(5.2,2.2,2.2),bunker_gate=(8,4,1.4))[aid]
    check(all(.85*y<=x<=1.15*y for x,y in zip((dims.x,dims.z,dims.y),expected)),aid+' independent dimensions')
    if aid=='watchtower':
        check(abs(dims.x-14)<.02 and abs(dims.y-10)<.02,aid+' exact platform footprint')
        points=[o.matrix_world@v.co for o in root.children_recursive if o.type=='MESH' for v in o.data.vertices]
        check(any(abs(p.z-4.8)<.002 for p in points),aid+' supports meet 4.8 m')
        check(any(abs(p.z-7.5)<.002 for p in points),aid+' ridge 7.5 m')
        volumes=[('ladder opening',(-1.1,-5.05,4.82),(1.1,-4.65,7)),('standing head clearance',(-6.4,.4,6.85),(6.4,4.4,7.0))]
        # Existing draft line: (124,4.8,-76) -> (176,2.4,-56), hang .9 above ground,
        # 1.2 m cable sag. Check the actual diagonal corridor, not its oversized enclosing box.
        for i in range(17):
            t=i*.005
            x,y,z=4+52*t,1-20*t,5.7-2.4*t-4.8*t*(1-t)
            volumes.append((f'zipline body clearance {i}',(x-.48,y-.48,z),(x+.48,y+.48,z+2.18)))
        for label,low,high in volumes:
            hits=0
            for obj in root.children_recursive:
                if obj.type!='MESH': continue
                for face in obj.data.polygons:
                    pts=[obj.matrix_world@obj.data.vertices[i].co for i in face.vertices]
                    if all(max(p[k] for p in pts)>low[k] and min(p[k] for p in pts)<high[k] for k in range(3)): hits+=1
            check(hits==0,aid+' '+label)
    if aid=='crane': check(abs(hi.z-14)<.002,aid+' mast height14 m')
    if aid=='alarm_truck':
        check({'Red','Hazard'}.issubset({m['name'].split('.')[0] for m in gltf['materials']}),aid+' alarm light materials')
        check(entry['collision']==dict(kind='box',size=[5.2,2,2.2],center=[0,1,0]),aid+' collider dimensions and center')
    if aid=='bunker_gate':
        check(abs(dims.x-8)<.002 and abs(dims.z-4)<.002,aid+' exact gate envelope')
        check(entry['collision']==dict(kind='box',size=[8,4,1.4],center=[0,2,0]),aid+' collider dimensions and center')
    check(entry['occlusion_parts']==[r for r in ROLES[aid] if r.startswith('Occlude_')],aid+' manifest occlusion list')
    REPORT['assets'].append(dict(id=aid,triangles=triangles,materials=len(gltf['materials']),dimensions=entry['dimensions_m']))
    return root


def render(root,rear=False):
    scene=bpy.context.scene
    lo,hi=bounds(root.children_recursive)
    center=(lo+hi)/2
    extent=max(hi-lo)
    helpers=[]
    camera_data=bpy.data.cameras.new('ReviewCamera')
    camera=bpy.data.objects.new('ReviewCamera',camera_data)
    bpy.context.collection.objects.link(camera)
    helpers.append(camera)
    direction=Vector((-.55,1 if rear else -1,.9)).normalized()
    camera.location=center+direction*extent*3
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO'
    camera_data.ortho_scale=extent*1.5
    scene.camera=camera
    for name,at,power in [('Key',(-4,-6,9),1500),('Fill',(4,3,6),900)]:
        data=bpy.data.lights.new(name,'AREA')
        data.energy=power*(extent/4)**2
        data.shape='DISK'
        data.size=extent*1.4
        light=bpy.data.objects.new(name,data)
        bpy.context.collection.objects.link(light)
        helpers.append(light)
        light.location=center+Vector(at)*extent/4
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    name=root.name+('-back' if rear else '-front')+'.png'
    scene.render.filepath=str(OUT/name)
    bpy.ops.render.render(write_still=True)
    REPORT['renders'].append(name)
    for obj in helpers:
        bpy.data.objects.remove(obj,do_unlink=True)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    scene=bpy.context.scene
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=scene.render.resolution_y=900
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.16,.20,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
    scene.view_settings.view_transform='AgX'
    manifest=json.loads((PACK/'manifest.json').read_text(encoding='utf-8'))
    entries=[a for a in manifest['assets'] if a['id'] in BUDGET]
    check(len(entries)==4 and {e['id'] for e in entries}==set(BUDGET),'four distinct bundle C entries')
    for entry in entries:
        root=validate(entry)
        if '--checks-only' not in sys.argv:
            render(root)
            render(root,rear=True)
    (OUT/'report.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('STAGE2_C_REVIEW_PASS',len(REPORT['assets']),'assets',len(REPORT['checks']),'checks',len(REPORT['renders']),'renders')


if __name__=='__main__':
    main()
