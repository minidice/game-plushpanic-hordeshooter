"""Same-camera GLB review: Blender -b -P review_parrot.py -- --capture-baseline / --label after.

The baseline copy and all images are local, ignored output. Eye gear is never shown.
"""
import argparse
import hashlib
import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path
import bpy

spec=importlib.util.spec_from_file_location('common_builder',Path(__file__).with_name('build_common_monsters.py'))
build=importlib.util.module_from_spec(spec);spec.loader.exec_module(build)
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--species',choices=['parrot','eagle','lion'],default='parrot')
parser.add_argument('--review-name',help='Optional local evidence folder name')
parser.add_argument('--capture-baseline',action='store_true')
parser.add_argument('--label',default='after')
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
output=build.OUT/(args.review_name or f'{args.species}-quality');output.mkdir(exist_ok=True)
source=build.ASSETS/f'{args.species}.glb'
if args.label=='before' and not args.capture_baseline:source=output/'before.glb'
if args.capture_baseline:
    baseline=output/'before.glb'
    if baseline.exists():raise RuntimeError('Baseline already exists; refusing to overwrite comparison evidence')
    shutil.copy2(source,baseline)
    hashes={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in build.ASSETS.glob('*.glb')}
    (output/'baseline-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
    args.label='before';source=baseline
build.clear();bpy.ops.import_scene.gltf(filepath=str(source))
report=build.audit(args.species)
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
materials={o.name:list(o.data.materials) for o in meshes}
material_indices={o.name:[p.material_index for p in o.data.polygons] for o in meshes}
build.stage();build.OUT=output
neutral=build.mat('QA neutral','AAA79E')
silhouette=build.mat('QA silhouette','101820')
emission=silhouette.node_tree.nodes.new('ShaderNodeEmission')
emission.inputs['Color'].default_value=(.008,.012,.018,1)
silhouette.node_tree.links.new(emission.outputs[0],silhouette.node_tree.nodes.get('Material Output').inputs['Surface'])
for mode in ['color','neutral','silhouette']:
    for obj in meshes:
        obj.data.materials.clear()
        for material in materials[obj.name]:obj.data.materials.append(material if mode=='color' else neutral if mode=='neutral' else silhouette)
        for poly,index in zip(obj.data.polygons,material_indices[obj.name]):poly.material_index=index
    views=[('front',0),('quarter',math.pi/4),('side',math.pi/2),('back',math.pi)] if mode=='color' else [('quarter',math.pi/4),('side',math.pi/2)]
    for view,angle in views:build.render(args.species,f'{args.label}-{mode}-{view}',angle)
for obj in meshes:
    obj.data.materials.clear()
    for material in materials[obj.name]:obj.data.materials.append(material)
    for poly,index in zip(obj.data.polygons,material_indices[obj.name]):poly.material_index=index
build.render(args.species,f'{args.label}-vest-quarter',math.pi/4,('Gear_Vest',))
build.render(args.species,f'{args.label}-vest-side',math.pi/2,('Gear_Vest',))
bpy.context.scene.camera.data.ortho_scale=7
build.render(args.species,f'{args.label}-gameplay',math.pi/4)
(output/f'{args.label}-audit.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
hashes=json.loads((output/'baseline-hashes.json').read_text(encoding='utf-8'))
for name,digest in hashes.items():
    if name!=f'{args.species}.glb':assert hashlib.sha256((build.ASSETS/name).read_bytes()).hexdigest()==digest,('other species changed',name)
print(args.species.upper()+'_REVIEW_COMPLETE',args.label,json.dumps(report),'OTHER_SIX_UNCHANGED')
