"""Read the other playable species; write only a local inspection report."""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];out=ROOT/'output/species-weapon-grips';out.mkdir(parents=True,exist_ok=True)
report={}
for species in ('duck','raccoon','redpanda'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(ROOT.parents[1]/f'game/assets/characters/{species}/{species}.glb'))
    rows=[]
    for o in bpy.context.scene.objects:
        row=dict(name=o.name,type=o.type,parent=o.parent.name if o.parent else None,position=list(o.matrix_world.translation))
        if o.type=='MESH':
            pts=[o.matrix_world@v.co for v in o.data.vertices]
            row.update(min=[min(v[i] for v in pts) for i in range(3)],max=[max(v[i] for v in pts) for i in range(3)],
                       materials=[dict(name=m.name,color=list(m.diffuse_color)) for m in o.data.materials])
        rows.append(row)
    report[species]=rows
(out/'source-inspection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('SPECIES_INSPECTED',[(k,len(v)) for k,v in report.items()],flush=True)
