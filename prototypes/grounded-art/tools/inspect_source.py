"""Read the existing GLB in Blender; only write a report inside grounded-art."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(REPO / 'game/assets/characters/goose/goose.glb'))
report = []
for ob in bpy.context.scene.objects:
    row = {'name': ob.name, 'type': ob.type, 'parent': ob.parent.name if ob.parent else None,
           'location': list(ob.matrix_world.translation)}
    if ob.type == 'MESH':
        points = [ob.matrix_world @ v.co for v in ob.data.vertices]
        row.update(min=[min(v[i] for v in points) for i in range(3)],
                   max=[max(v[i] for v in points) for i in range(3)],
                   materials=[m.name for m in ob.data.materials])
        if ob.name == 'goose_torso_mesh':
            row['sections'] = []
            for z in [.3, .45, .6, .75, .9, 1.05, 1.2, 1.35, 1.5, 1.65, 1.8]:
                layer = sorted(points, key=lambda p: abs(p.z-z))[:70]
                row['sections'].append([z, min(p.x for p in layer), max(p.x for p in layer), min(p.y for p in layer), max(p.y for p in layer)])
    report.append(row)
(ROOT/'output').mkdir(exist_ok=True)
(ROOT/'output/source-inspection.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('SOURCE_INSPECTED', len(report), flush=True)
