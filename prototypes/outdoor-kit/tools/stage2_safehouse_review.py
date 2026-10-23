"""Independent GLB reimport checks and front/back Cycles renders for Stage 2 safehouse.

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
OUT = REPO/'game/output/stage2-safehouse-review'
BUDGET = dict(safehouse=6000,safehouse_door=1800)
ROLES = dict(safehouse=['Keep','Occlude_Walls'],safehouse_door=['Keep','Occlude_Door'])
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
    check(entry['collision']['kind']=='none',aid+' collision contract')
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
    check(triangles==entry['triangles'] and triangles<BUDGET[aid],aid+' triangle count')
    lo,hi=bounds(root.children_recursive)
    dims=hi-lo
    check(all(abs(a-b)<.002 for a,b in zip((dims.x,dims.z,dims.y),entry['dimensions_m'])),aid+' manifest dimensions')
    floor=0
    check(abs(lo.z-floor)<.002 and abs(entry['bounds_min_y']-floor)<.002,aid+' floor pivot')
    expected=dict(safehouse=(14.1615,3.75,12.0965),safehouse_door=(.3,2.4,2.6))[aid]
    check(all(abs(x-y)<.002 for x,y in zip((dims.x,dims.z,dims.y),expected)),aid+' independent exact dimensions')
    if aid=='safehouse':
        # Blender coordinates: doorway west, full interior volume has no duplicate floor or furnishings.
        for label,low,high in [('west entrance',(-7.3,-1.27,.03),(-6.3,1.27,2.39)),('walkable interior',(-6.35,-5.35,.03),(6.35,5.35,2.25)),('standing head clearance',(-6.35,-5.35,2.18),(6.35,5.35,2.35))]:
            hits=0
            for obj in root.children_recursive:
                if obj.type!='MESH': continue
                for face in obj.data.polygons:
                    pts=[obj.matrix_world@obj.data.vertices[i].co for i in face.vertices]
                    if all(max(p[k] for p in pts)>low[k] and min(p[k] for p in pts)<high[k] for k in range(3)): hits+=1
            check(hits==0,aid+' '+label)
        red=[]
        for obj in root.children_recursive:
            if obj.type!='MESH': continue
            for face in obj.data.polygons:
                if obj.data.materials[face.material_index].name.split('.')[0]=='Red':
                    red.extend(obj.matrix_world@obj.data.vertices[i].co for i in face.vertices)
        check(len(red)>0,aid+' modeled close button exists')
        mid=(Vector(tuple(min(p[i] for p in red) for i in range(3)))+Vector(tuple(max(p[i] for p in red) for i in range(3))))/2
        check((mid-Vector((-6.5,-1.8,1.4))).length<.002,aid+' close button center matches game interaction')
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
    check(len(entries)==2 and {e['id'] for e in entries}==set(BUDGET),'two distinct safehouse entries')
    for entry in entries:
        root=validate(entry)
        if '--checks-only' not in sys.argv:
            render(root)
            render(root,rear=True)
    (OUT/'report.json').write_text(json.dumps(REPORT,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('STAGE2_SAFEHOUSE_REVIEW_PASS',len(REPORT['assets']),'assets',len(REPORT['checks']),'checks',len(REPORT['renders']),'renders')


if __name__=='__main__':
    main()
