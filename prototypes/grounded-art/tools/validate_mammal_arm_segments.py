"""Independently re-import the runtime GLB and verify closed, consistently wound sections."""
import bpy, pathlib
ROOT=pathlib.Path(__file__).resolve().parents[3]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'game/assets/characters/shared/mammal-arm-segments.glb'))
parts={o.name:o for o in bpy.context.scene.objects if o.type=='MESH'}
assert set(parts)=={'UpperArm','Forearm'}
for name,obj in parts.items():
    mesh=obj.data;mesh.calc_loop_triangles();edges={};volume=0
    def key(i):return tuple(round(v,5) for v in mesh.vertices[i].co)
    for tri in mesh.loop_triangles:
        a,b,c=[mesh.vertices[i].co for i in tri.vertices];volume+=a.dot(b.cross(c))/6
        ids=[key(i) for i in tri.vertices]
        for a,b in zip(ids,ids[1:]+ids[:1]):
            edge=tuple(sorted((a,b)));edges[edge]=edges.get(edge,0)+1
    assert all(count==2 for count in edges.values()),name+' open surface'
    assert volume>0,name+' inverted winding'
    assert abs(min(v.co.z for v in mesh.vertices))<1e-5 and abs(max(v.co.z for v in mesh.vertices)-1)<1e-5,name+' wrong axis/length'
    assert len(mesh.materials)==1 and len(mesh.loop_triangles)<=260
    print('VALID_ARM',name,'triangles',len(mesh.loop_triangles),'volume',volume)
print('MAMMAL_ARM_GLB_PASS 11')
