"""Stage 2 environment shells; existing floors own collision. Run with Blender --background --python."""
import importlib.util
import json
import random
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location('safehouse_helpers', Path(__file__).with_name('stage2_safehouse.py'))
h = importlib.util.module_from_spec(spec)
spec.loader.exec_module(h)  # Definitions only; does not rebuild the existing pack.
PACK = ROOT / 'game/assets/stage2-props'
PALETTE = dict(Metal='56636a', MetalLight='a8b7b1', Rust='98674f', Ochre='c7a35c', Hazard='e2b84a', Ink='2a2a2a', Stone='737b67', Moss='697350')

def build_cliff():
    root=h.empty('cliff_face');keep=h.empty('Keep',root)
    rng=random.Random(24024)
    # +Blender Y lies inside the high ground; the rock relief protrudes at most 0.14m.
    front=[]
    for row in range(4):
        for col in range(9):
            x=-3+col*.75
            z=row*.8 if row in (0,3) else row*.8+rng.uniform(-.16,.16)
            front.append((x,-rng.uniform(.035,.14),z))
    faces=[]
    for row in range(3):
        for col in range(8):
            a=row*9+col; faces.extend([(a,a+1,a+10),(a,a+10,a+9)])
    h.mesh(keep,'StratifiedRock',front,faces,'Stone')
    # No cap: the runtime terrain already owns the top surface, avoiding coplanar flicker.
    return root

def build_shell():
    root = h.empty('container_stack_shell')
    keep = h.empty('Keep', root)
    # Four adjacent 3.5 x 12m freight modules, exactly inside the existing 14 x 12m deck.
    for i in range(4):
        x = -5.25 + i * 3.5
        color = ('Metal', 'Rust', 'Ochre', 'Metal')[i]
        for y in (-5.94, 5.94):
            h.box(keep, 'EndDoors', (x, y, 1.18), (3.48, .12, 2.36), color)
            for dx in (-1.62, -.8, 0, .8, 1.62):
                h.box(keep, 'DoorRib', (x+dx, y, 1.2), (.06, .12, 2.34), 'MetalLight', .01)
            h.box(keep, 'DoorLatch', (x, y, 1.05), (.55, .12, .07), 'Hazard', .01)
        for dx in (-1.69, 1.69):
            h.box(keep, 'LongWall', (x+dx, 0, 1.18), (.12, 11.88, 2.36), color)
            for j in range(24):
                h.box(keep, 'Corrugation', (x+dx, -5.7+j*.49, 1.18), (.12, .16, 2.24), 'Metal', .01)
        # The runtime roof at y=2.4 remains the walkable surface; trims stop at that height.
        for dx in (-1.68, 1.68):
            h.box(keep, 'RoofRail', (x+dx, 0, 2.36), (.12, 12, .08), 'MetalLight', .01)
        for y in (-5.94, 5.94):
            h.box(keep, 'BaseFrame', (x, y, .06), (3.5, .12, .12), 'Metal', .015)
    return root

def build_sign():
    root = h.empty('route_sign')
    keep = h.empty('Keep', root)
    h.box(keep, 'Foot', (0, 0, .08), (.55, .55, .16), 'Metal')
    h.box(keep, 'Post', (0, 0, 1), (.14, .14, 2), 'MetalLight')
    # Arrow points +X in both Blender and Godot. Both faces readable.
    outline = [(-.75, 1.35), (.35, 1.35), (.35, 1.12), (.95, 1.7), (.35, 2.28), (.35, 2.05), (-.75, 2.05)]
    verts = [(x,y,z) for y in (-.08,.08) for x,z in outline]
    n=len(outline)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    h.mesh(keep,'DirectionArrow',verts,faces,'Hazard')
    for y in (-.087,.087):
        h.box(keep,'RouteStripe',(-.22,y,1.7),(.72,.014,.12),'Ink',0)
    return root

def main():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for material in list(bpy.data.materials): bpy.data.materials.remove(material,do_unlink=True)
    h.M.update({n:h.material(n,c) for n,c in PALETTE.items()})
    manifest=json.loads((PACK/'manifest.json').read_text(encoding='utf-8'))
    records=[]
    roots=[]
    for make in (build_shell,build_sign,build_cliff):
        root=make(); bpy.context.view_layer.update()
        objs=[o for o in root.children_recursive if o.type=='MESH']
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:o.select_set(True)
        bpy.context.view_layer.objects.active=objs[0]
        if len(objs)>1: bpy.ops.object.join()
        o=bpy.context.object; o.name='Keep_Mesh'
        mod=o.modifiers.new('Triangles','TRIANGULATE'); bpy.ops.object.modifier_apply(modifier=mod.name)
        bpy.context.view_layer.update()
        points=[o.matrix_world@v.co for v in o.data.vertices]
        lo=Vector([min(v[i] for v in points) for i in range(3)])
        hi=Vector([max(v[i] for v in points) for i in range(3)])
        dims=hi-lo
        assert abs(lo.z)<.001 and len(o.data.polygons)<16000
        assert len({m.material for m in o.material_slots})<=6
        if root.name=='container_stack_shell':assert (dims-Vector((14,12,2.4))).length<.01
        notes=dict(container_stack_shell='14 x 12 m shell; game owns collision and roof at 2.4 m. No duplicate roof.',route_sign='Arrow points +X. Runtime adds a narrow post collider, outside the travel corridor.',cliff_face='6 m length / 2.4 m height. Local -Z inside high ground; relief extends at most 0.14 m toward +Z. Existing terrain owns collision; exclude traversal links.')
        data=dict(id=root.name,file=root.name+'.glb',label=root.name,dimensions_m=[round(dims.x,4),round(dims.z,4),round(dims.y,4)],bounds_min_y=0,triangles=len(o.data.polygons),collision=dict(kind='none'),occlusion_parts=[],note=notes[root.name])
        bpy.ops.object.select_all(action='DESELECT')
        for item in [root]+list(root.children_recursive): item.select_set(True)
        bpy.context.view_layer.objects.active=root
        bpy.ops.export_scene.gltf(filepath=str(PACK/data['file']),export_format='GLB',use_selection=True,export_yup=True,export_animations=False)
        records.append(data); roots.append(root)
        # Keep export names stable while allowing a second model in the same source scene.
        for child in root.children_recursive: child.name=root.name+'__'+child.name
        print('ENV_ASSET',data)
    replacements={r['id']:r for r in records}
    manifest['assets']=[replacements.pop(a['id'],a) for a in manifest['assets']]+list(replacements.values())
    (PACK/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    roots[1].location=(10,0,0)
    roots[2].location=(7,-6,0)
    bpy.ops.object.camera_add(location=(22,-26,20))
    camera=bpy.context.object; camera.rotation_euler=(Vector((2,0,1))-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=26; bpy.context.scene.camera=camera
    bpy.ops.object.light_add(type='AREA',location=(3,-8,18));bpy.context.object.data.energy=2400;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=14
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.world.color=(.22,.22,.22);scene.render.resolution_x=1440;scene.render.resolution_y=900;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='Standard'
    output=ROOT/'game/output/stage2-environment-blender.png';output.parent.mkdir(exist_ok=True)
    scene.render.filepath=str(output)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'prototypes/outdoor-kit/source/stage2-environment.blend'))
    bpy.ops.render.render(write_still=True)
    print('STAGE2_ENVIRONMENT_BUILT',len(records))

if __name__=='__main__':main()
