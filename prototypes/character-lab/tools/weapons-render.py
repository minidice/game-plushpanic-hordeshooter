"""Blender --background --python weapons-render.py: alternate side-detail view."""
import bpy
from pathlib import Path
from mathutils import Vector

out=Path(__file__).resolve().parents[1]/'output/weapons'
bpy.ops.wm.open_mainfile(filepath=str(out/'weapons-source.blend'))
scene=bpy.context.scene
scene.camera.location=(0,-8,6)
scene.camera.rotation_euler=(Vector((0,0,.15))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera.data.ortho_scale=7.1
roots=[obj for obj in scene.objects if 'family' in obj]
for obj in roots:
    row=round((2.05-obj.location.y)/1.05)
    obj.location.y+=.7-row*.35
for obj in scene.objects:
    if obj.name.startswith('Label_'):
        row=round((1.62-obj.location.y)/1.05)
        obj.location.z=.36
        obj.location.y+=.46-row*.35
        obj.rotation_euler=scene.camera.rotation_euler
scene.render.resolution_y=1350
scene.render.filepath=str(out/'weapons-side-sheet.png')
bpy.ops.render.render(write_still=True)
