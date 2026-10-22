import bpy,bmesh,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[1]
report={}
for s in ('raccoon','redpanda'):
 for kind,path in [('source',REPO/f'prototypes/character-lab/output/roster/{s}-source.glb'),('game',REPO/f'game/assets/characters/{s}/{s}.glb')]:
  bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(path));rows=[]
  for o in bpy.context.scene.objects:
   if o.type!='MESH' or not any(t in o.name.lower() for t in ('ear','tail')):continue
   bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
   pts=[o.matrix_world@v.co for v in o.data.vertices]
   rows.append(dict(name=o.name,parent=o.parent.name if o.parent else None,vertices=len(o.data.vertices),faces=len(o.data.polygons),boundary=sum(e.is_boundary for e in bm.edges),zero_faces=sum(f.calc_area()<1e-10 for f in bm.faces),bounds=[[min(v[i] for v in pts) for i in range(3)],[max(v[i] for v in pts) for i in range(3)]],matrix=[list(r) for r in o.matrix_world],materials=[m.name for m in o.data.materials]))
   bm.free()
  report[s+'_'+kind]=rows
(ROOT/'output/fit-surface-diagnosis.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report),flush=True)
