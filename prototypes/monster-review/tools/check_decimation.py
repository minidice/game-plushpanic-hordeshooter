"""Blender read-only experiment: same bull source and ratio, weld before vs after.
Run Blender --background --python this_file. Writes only an inspection JSON.
"""
from pathlib import Path
import bpy
import bmesh
import json

ROOT = Path(__file__).resolve().parents[3]
source = ROOT / 'prototypes/character-lab/output/roster/bull-source.glb'


def weld(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bm.to_mesh(mesh)
    bm.free()


report = {}
for early_weld in (False, True):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(source))
    boundaries = triangles = 0
    for obj in list(bpy.context.scene.objects):
        if obj.type != 'MESH':
            continue
        bpy.context.view_layer.objects.active = obj
        # Use the same source qualification and reduction ratio as the game exporter.
        reduce = len(obj.data.polygons) > 60
        if early_weld:
            weld(obj.data)
        if reduce:
            modifier = obj.modifiers.new('Inspection only', 'DECIMATE')
            modifier.ratio = .25
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        weld(obj.data)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        boundaries += sum(edge.is_boundary for edge in bm.edges)
        bm.free()
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    report['weld_before_reduce' if early_weld else 'current_reduce_then_weld_for_measurement'] = {
        'triangles': triangles, 'boundary_edges': boundaries}
print('DECIMATION_EXPERIMENT', json.dumps(report))
out = ROOT / 'game/output/monster-review/decimation-experiment.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, indent=2), encoding='utf-8')
