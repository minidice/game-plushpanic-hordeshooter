"""Render the exported gear variants from the saved inspection scene."""
import bpy
from pathlib import Path
from mathutils import Vector
OUT=Path(__file__).resolve().parents[1]/'output'/'roster'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'roster-contact.blend'))
for kind in ['humanoid','crawler','tether','leaper','spitter']:
    root=bpy.data.objects.get(f'{kind}_root')
    for obj in [root]+list(root.children_recursive):obj.hide_render=True
    bpy.data.objects[f'label_{kind}'].hide_render=True
for kind in ['goose','duck','raccoon','redpanda']:
    for suffix in ['helmet','vest','pack']:
        obj=bpy.data.objects[f'{kind}_{suffix}']
        for child in [obj]+list(obj.children_recursive):child.hide_render=True
    if kind in ['duck','redpanda']:
        obj=bpy.data.objects[f'{kind}_beret']
        for child in [obj]+list(obj.children_recursive):child.hide_render=False
scene=bpy.context.scene
scene.camera.location=(2,-12,7)
scene.camera.rotation_euler=(Vector((0,2,1))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=9.7
scene.render.resolution_x=1500;scene.render.resolution_y=750
scene.render.filepath=str(OUT/'roster-gear-variants.png')
bpy.ops.render.render(write_still=True)
