"""Independent GLB shape/rig contract check for the clean tiger."""
import bpy
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
bpy.ops.import_scene.gltf(filepath=str(ROOT/'game/assets/common-monsters/tiger.glb'))
objects=bpy.data.objects
checks=0


def check(condition,message):
    global checks
    assert condition,message
    checks+=1


for name in ('Root','Body','Deform','Head','LeftFoot','RightFoot','Weapon','Muzzle'):
    check(name in objects,f'missing {name}')
check(objects['Head'].parent==objects['Deform'],'head pivot parent')
check(objects['Deform_mesh'].parent==objects['Deform'],'continuous body parent')
check(objects['Head_mesh'].parent==objects['Head'],'face parent')
check(objects['Gear_Vest'].parent==objects['Deform'],'optional fitted vest')
check(objects['Gear_Glasses'].parent==objects['Head'],'legacy optional eyewear node')

base_meshes=[]
for obj in objects:
    if obj.type!='MESH':continue
    node=obj.parent
    while node and not node.name.startswith('Gear_'):node=node.parent
    if node is None:base_meshes.append(obj)
base_triangles=sum(len(poly.vertices)-2 for obj in base_meshes for poly in obj.data.polygons)
check(base_triangles<8000,f'base crowd triangle budget: {base_triangles}')
palette={mat.name for obj in base_meshes for mat in obj.data.materials}
check({'common_primary','common_secondary','fixed_ink','fixed_cream','fixed_eye'}<=palette,
      f'tiger palette channels: {palette}')
check(not {'fixed_glint','fixed_thread'}&palette,'no bead glint or body stitching')

for side in ('left','right'):
    arm=objects[f'common_tiger_{side}Arm']
    forearm=objects[f'common_tiger_{side}Arm_mesh']
    paw=objects[f'common_tiger_{side}Paw']
    hand=objects[f'common_tiger_{side}Paw_mesh']
    wrist=objects[f'common_tiger_{side}Wrist']
    grip=objects[f'common_tiger_{side}Grip']
    shoulder=objects[f'common_tiger_{side}Shoulder']
    check(arm.parent==objects['Deform'] and forearm.parent==arm,f'{side} shoulder and forearm')
    check(paw.parent==arm and hand.parent==paw,f'{side} rigid paw under arm')
    check(shoulder.parent==arm and wrist.parent==paw and grip.parent==paw,
          f'{side} authored grip anchors')
    distance=(wrist.matrix_world.translation-grip.matrix_world.translation).length
    check(.06<distance<.16,f'{side} compact wrist/grip {distance:.4f}')
    check({material.name for material in hand.data.materials}=={'common_primary'},
          f'{side} palette paw without mitten pad')

for foot_name in ('LeftFoot','RightFoot'):
    foot=objects[f'{foot_name}_mesh']
    vertices=[foot.matrix_world @ vertex.co for vertex in foot.data.vertices]
    sole_area=0.0
    for polygon in foot.data.polygons:
        positions=[vertices[i] for i in polygon.vertices]
        if len(positions)==3 and all(abs(p.z)<.0002 for p in positions):
            sole_area+=(positions[1]-positions[0]).cross(positions[2]-positions[0]).length*.5
    check(sole_area>.019,f'{foot_name} planar sole area {sole_area:.5f}')
    check(min(v.z for v in vertices)>-.0002,f'{foot_name} floor penetration')
    check(max(v.z for v in vertices)>.43,f'{foot_name} tapered ankle-to-body overlap')
    print('TIGER_SOLE_AREA',foot_name,round(sole_area,6))

head=objects['Head_mesh']
eye_indices={i for polygon in head.data.polygons
             if head.data.materials[polygon.material_index].name=='fixed_eye'
             for i in polygon.vertices}
eyes=[head.matrix_world @ head.data.vertices[i].co for i in eye_indices]
check(eyes and max(p.z for p in eyes)-min(p.z for p in eyes)<.14,'small shallow eyes')
check(max(p.x for p in eyes)-min(p.x for p in eyes)<.48,'eyes inside feline face')

body=objects['Deform_mesh']
check({'common_primary','fixed_ink'}<=
      {material.name for material in body.data.materials},'painted torso channels')
check('common_secondary' in {m.name for m in objects['common_tiger_tail_mesh'].data.materials},
      'secondary palette remains on tail tip')
ink_triangles=sum(len(polygon.vertices)-2 for polygon in body.data.polygons
                  if body.data.materials[polygon.material_index].name=='fixed_ink')
check(ink_triangles>=140,f'surface stripe topology {ink_triangles}')
check(max((body.matrix_world @ v.co).z for v in body.data.vertices)<1.4,
      'torso skin remains below separate head')
check(not any('helmet' in obj.name.lower() for obj in objects),'no helmet')
resource=(ROOT/'game/content/enemies/common_tiger.tres').read_text(encoding='utf-8')
check('Id = "common_tiger"' in resource and 'tiger.glb' in resource,'unchanged resource contract')
check('MoveSpeed' not in resource and 'MaxHealth' not in resource,'no art-authored combat stats')
print('TIGER_CLEAN_VALIDATION',checks,'BASE_TRIANGLES',base_triangles,
      'STRIPE_TRIANGLES',ink_triangles)
