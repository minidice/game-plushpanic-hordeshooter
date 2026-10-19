"""Render optional player gear from the optimizer's saved studio scene."""
import bpy
from pathlib import Path

OUT=Path(__file__).resolve().parents[1]/'output'/'roster'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'players-contact.blend'))
for kind in ['goose','duck','raccoon','redpanda']:
    for suffix,hidden in [('headset',False),('helmet',True),('vest',True),('vestPadded',False)]:
        obj=bpy.data.objects.get(f'{kind}_{suffix}')
        for part in [obj]+list(obj.children_recursive):
            part.hide_render=hidden
bpy.context.scene.render.filepath=str(OUT/'players-outfit-options.png')
bpy.ops.render.render(write_still=True)
