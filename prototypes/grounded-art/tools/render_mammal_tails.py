"""Render actual exported tails, with optional pre-change local GLBs for comparison.

Use Blender --background --python-exit-code 1 --python this_file.py.
"""
import bpy
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'game/output/mammal-tails'
OUT.mkdir(parents=True,exist_ok=True)


def read(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return {o.name: {'parent':o.parent.name if o.parent else None,
                    'matrix':[round(v,6) for row in o.matrix_world for v in row],
                    'materials':[m.name.split('.')[0] for m in o.data.materials] if o.type=='MESH' else [],
                    'points':sorted(tuple(round(v,5) for v in p.co) for p in o.data.vertices) if o.type=='MESH' else []}
            for o in bpy.context.scene.objects}


report={}
for species in ('raccoon','redpanda'):
    before_path=OUT/f'{species}-before.glb'
    if not before_path.exists():
        before_path=ROOT/f'game/assets/characters/{species}/{species}-fitted.glb'
    before=read(before_path)
    target=ROOT/f'game/assets/characters/{species}/{species}-clean.glb'
    after=read(target)
    if before_path.name.endswith('-before.glb'):
        assert before.keys()==after.keys(), 'node set changed'
        changed=[name for name in before if before[name]!=after[name]]
        assert changed==[f'{species}_tail_mesh'], changed
        for key in ('parent','matrix','materials'):
            assert before[changed[0]][key]==after[changed[0]][key], f'tail {key} changed'
        report[species]={'changed_nodes':changed,'unchanged_nodes':len(before)-1}
    for label,path in (('before',before_path),('after',target)):
        read(path)
        scene=bpy.context.scene
        for obj in list(scene.objects):
            if any(term in obj.name for term in ('beret','goggles','headset','helmet','pack','vest','weapon','Muzzle')):
                for part in (obj,*obj.children_recursive): part.hide_render=True
        scene.world=bpy.data.worlds.new('Tail review world')
        scene.world.color=(.3,.34,.38)
        camera_data=bpy.data.cameras.new('Tail review camera')
        camera=bpy.data.objects.new('Tail review camera',camera_data)
        scene.collection.objects.link(camera);scene.camera=camera
        camera_data.type='ORTHO';camera_data.ortho_scale=2.9
        camera.location=(3.8,3.2,2.5)
        camera.rotation_euler=(Vector((0,.3,.95))-camera.location).to_track_quat('-Z','Y').to_euler()
        light_data=bpy.data.lights.new('Key','AREA');light_data.energy=550;light_data.size=4
        light=bpy.data.objects.new('Key',light_data);scene.collection.objects.link(light)
        light.location=(2,3,5)
        light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
        scene.render.engine='BLENDER_EEVEE'
        scene.render.resolution_x=720;scene.render.resolution_y=720;scene.render.resolution_percentage=100
        scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
        scene.render.filepath=str(OUT/f'{species}-{label}-blender.png')
        bpy.ops.render.render(write_still=True)
(OUT/'model-tail-only-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('TAIL_ONLY_MODEL_PASS',report,flush=True)
