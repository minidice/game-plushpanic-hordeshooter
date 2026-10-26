"""Blender re-import validation; optional --before DIR checks geometry/material/muzzle preservation."""
import bpy, pathlib, sys, argparse
from mathutils.bvhtree import BVHTree

ROOT=pathlib.Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser()
parser.add_argument('--before',type=pathlib.Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
ids=['mp5','m4','m16','ak47','m249','r870','benelli_m4','double_barrel','awm','svd','m82']
checks=0
def read(path,require_contact):
    global checks
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.data.orphans_purge(do_recursive=True)
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    triangles=[];points=[];faces=[]
    def key(v):return tuple(round(x,5) for x in v)
    for o in meshes:
        o.data.calc_loop_triangles()
        start=len(points);points.extend(o.matrix_world@v.co for v in o.data.vertices)
        for t in o.data.loop_triangles:
            material=o.data.materials[t.material_index].name
            triangles.append((material,tuple(sorted(key(o.matrix_world@o.data.vertices[i].co) for i in t.vertices))))
            faces.append(tuple(start+i for i in t.vertices))
    muzzle=bpy.data.objects['Muzzle'].matrix_world.translation
    if require_contact:
        contact=bpy.data.objects['StockContact'].matrix_world.translation
        nearest=BVHTree.FromPolygons(points,faces,all_triangles=True).find_nearest(contact)
        assert nearest and nearest[3]<.001,(path.name,'stock contact off physical pad',nearest)
        # Blender -Y is forward; rear contact must be behind the receiver and muzzle.
        assert contact.y>0 and muzzle.y<0,(path.name,'reversed contact')
        checks+=2
    return sorted(triangles),key(muzzle)
for ident in ids:
    current=read(ROOT/'game/assets/weapons'/f'{ident}.glb',True)
    if args.before:
        previous=read(args.before/f'{ident}.glb',False)
        assert current==previous,(ident,'visible triangles/material assignment or muzzle changed')
        checks+=2
    print('VALID_CONTACT',ident,len(current[0]))
print('WEAPON_CONTACTS_PASS',checks)
