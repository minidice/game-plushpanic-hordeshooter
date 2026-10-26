"""Closed shared arm sections, Blender Z=0..1 becomes Godot Y=0..1.
Run blender --background --python-exit-code 1 --python <this file>.
Runtime scales cross-section and length independently; meshes are immutable.
"""
import bpy, math, pathlib, json
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[3]
DEST=ROOT/'game/assets/characters/shared/mammal-arm-segments.glb'
OUT=ROOT/'prototypes/grounded-art/output/mammal-arms'
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
mat=bpy.data.materials.new('arm_template'); mat.diffuse_color=(.55,.32,.12,1); mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF'); bsdf.inputs['Base Color'].default_value=mat.diffuse_color; bsdf.inputs['Roughness'].default_value=.85
def section(name, rings):
    n=16; verts=[]; faces=[]
    for z,r in rings:
        for i in range(n):
            a=math.tau*i/n; verts.append((math.cos(a)*r,math.sin(a)*r*.85,z))
    for j in range(len(rings)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;faces.append((a,b,b+n,a+n))
    faces += [tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=len(p.vertices)==4
    edges={}
    for face in faces:
        for a,b in zip(face,face[1:]+face[:1]):
            key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
    assert all(v==2 for v in edges.values()),name+' must be manifold'
    return obj
upper=section('UpperArm',[(0,.12),(.03,.64),(.10,.92),(.24,1),(.55,.94),(.85,.77),(.97,.69),(1,.60)])
lower=section('Forearm',[(0,.68),(.06,.81),(.22,.93),(.48,.88),(.77,.72),(.97,.61),(1,.53)])
DEST.parent.mkdir(parents=True,exist_ok=True);OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(DEST),export_format='GLB',export_yup=True)
audit={o.name:{'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in (upper,lower)}
(OUT/'audit.json').write_text(json.dumps(audit,indent=2))
for obj,x in [(upper,-.17),(lower,.17)]:obj.location.x=x;obj.scale=(.10,.10,.34)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01));floor=bpy.context.object
floor_mat=bpy.data.materials.new('floor');floor_mat.diffuse_color=(.19,.22,.20,1);floor.data.materials.append(floor_mat)
bpy.ops.object.camera_add(location=(.8,-1.1,.85));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.17))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.9;bpy.context.scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-1,-2,3));bpy.context.object.data.energy=180;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=3
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32;scene.world.color=(.25,.25,.25);scene.render.resolution_x=900;scene.render.resolution_y=700;scene.render.resolution_percentage=100;scene.render.filepath=str(OUT/'segments.png');bpy.ops.render.render(write_still=True)
print('MAMMAL_ARM_MODEL_PASS',json.dumps(audit))
