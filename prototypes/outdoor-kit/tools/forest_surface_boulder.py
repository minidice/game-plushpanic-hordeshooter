"""Dedicated forest pilot boulder. Never rewrites the shared outdoor asset pack.

Run with Blender --background --python prototypes/outdoor-kit/tools/forest_surface_boulder.py.
Metres, Blender Z up; export is glTF Y up. Seed-free authored geometry is deterministic.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
PACK = REPO / 'game/assets/stage2-props'
OUT = REPO / 'game/output/forest-surface-boulder'
ASSET = 'surface_boulder'


def material(name, color, roughness=.96):
    rgb = [int(color[i:i+2], 16)/255 for i in (0, 2, 4)]
    rgb = [v/12.92 if v <= .04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1)
    mat.use_nodes = True
    bs = mat.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*rgb, 1)
    bs.inputs['Roughness'].default_value = roughness
    bs.inputs['Specular IOR Level'].default_value = .16
    return mat


def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    return obj


def mesh(name, parent, vertices, faces, materials):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.parent = parent
    for mat in materials:
        data.materials.append(mat)
    return obj


def rock(name, parent, outline, heights, bevel, materials):
    n = len(outline)
    vertices = []
    cx = sum(x for x, _ in outline)/n
    cy = sum(y for _, y in outline)/n
    # A low buried foot, strong fracture shoulder, then one broad tilted roof.
    for ring, (scale, height) in enumerate(((.82, 0), (1., .58), (.77, 1.))):
        for i, (x, y) in enumerate(outline):
            local_scale = scale*(1+.075*math.sin(i*2.1)) if ring != 1 else scale
            px, py = cx+(x-cx)*local_scale, cy+(y-cy)*local_scale
            z = heights[i]*(height+.12*math.sin(i*2.4)) if ring == 1 else heights[i]*height
            if ring == 2 and name == 'Main fractured stone':
                z = 1.22+.07*px+.09*py
            vertices.append((px, py, z))
    faces = [tuple(reversed(range(n))), tuple(range(2*n, 3*n))]
    for ring in range(2):
        for i in range(n):
            a = ring*n+i
            b = ring*n+(i+1)%n
            faces.append((a, b, b+n, a+n))
    obj = mesh(name, parent, vertices, faces, materials)
    for face in obj.data.polygons:
        face.material_index = 1 if face.normal.z > .6 else (2 if face.normal.y < -.3 else 0)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new('Authored broad chipped edges', 'BEVEL')
    modifier.width = bevel
    modifier.segments = 1
    modifier.affect = 'EDGES'
    modifier.material = -1
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def moss(name, parent, stone, center, radii, materials, phase=0):
    """Clip the actual stone triangles to a scalloped contour, then thicken.

    Keeping every underlying fracture edge avoids interpolated caps cutting
    through a bevel or bridging over the steep rear shoulder.
    """
    n = 28
    outline = []
    for i in range(n):
        angle = math.tau*i/n
        radius = 1+.10*math.sin(angle*3+phase)+.055*math.sin(angle*7+.8)
        outline.append((center[0]+math.cos(angle)*radii[0]*radius,
                        center[1]+math.sin(angle)*radii[1]*radius))

    def clip(poly, a, b):
        def side(p):
            return (b[0]-a[0])*(p.y-a[1])-(b[1]-a[1])*(p.x-a[0])
        result = []
        for previous, current in zip(poly[-1:]+poly[:-1], poly):
            d0, d1 = side(previous), side(current)
            if (d0 >= 0) != (d1 >= 0):
                result.append(previous.lerp(current, d0/(d0-d1)))
            if d1 >= 0:
                result.append(current)
        return result

    vertices, faces = [], []
    stone.data.calc_loop_triangles()
    for triangle in stone.data.loop_triangles:
        if triangle.normal.z < .15:
            continue
        for i in range(n):
            poly = [stone.data.vertices[j].co.copy() for j in triangle.vertices]
            mask = [center, outline[i], outline[(i+1)%n]]
            for a, b in zip(mask, mask[1:]+mask[:1]):
                if poly:
                    poly = clip(poly, a, b)
            if len(poly) >= 3:
                start = len(vertices)
                vertices.extend(tuple(v+Vector((0, 0, .028))) for v in poly)
                faces.append(tuple(range(start, len(vertices))))
    obj = mesh(name, parent, vertices, faces, materials)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00005)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=.00005)
    # Remove coplanar radial clipping seams, preserving the rock's own fractures.
    bmesh.ops.dissolve_limit(bm, angle_limit=.001, verts=list(bm.verts), edges=list(bm.edges))
    bm.to_mesh(obj.data)
    bm.free()
    bpy.context.view_layer.objects.active = obj
    solid = obj.modifiers.new('Shallow fitted moss thickness', 'SOLIDIFY')
    solid.thickness = .030
    solid.offset = -1
    solid.material_offset_rim = 1
    bpy.ops.object.modifier_apply(modifier=solid.name)
    bevel = obj.modifiers.new('Soft moss lip', 'BEVEL')
    bevel.width = .008
    bevel.segments = 1
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for face in obj.data.polygons:
        face.use_smooth = False
    return obj


def measure(objects):
    bpy.context.view_layer.update()
    points = [obj.matrix_world@vertex.co for obj in objects if obj.type == 'MESH' for vertex in obj.data.vertices]
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    triangles = 0
    for obj in objects:
        if obj.type == 'MESH':
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)
    return low, high, triangles


def run():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    OUT.mkdir(parents=True, exist_ok=True)
    root = empty(ASSET)
    keep = empty('Keep', root)
    stone_mats = [material('Stone neutral grey', '7d8079'), material('Stone broad upper plane', '95978b'),
                  material('Stone dark fracture', '646d69'), material('Stone bevel', '92968c')]
    moss_mats = [material('Moss warm olive', '899b42', 1.), material('Moss contact edge', '667a37', 1.)]
    outline = [(-1.06, -.48), (-.65, -.84), (.53, -.78), (1.11, -.35),
               (1.00, .46), (.41, .81), (-.52, .76), (-1.10, .24)]
    # All roof points are coplanar, so the large roof remains a deliberate readable plane.
    heights = [1.22+.07*x+.09*y for x, y in outline]
    main = rock('Main fractured stone', keep, outline, heights, .065, stone_mats)
    moss('Fitted roof moss', keep, main, (-.12, .10), (.84, .64), moss_mats)
    # One low angular shoulder, separated by a single readable fracture seam.
    shoulder_outline = [(.64, -.61), (.97, -.77), (1.33, -.50), (1.28, -.04), (.91, .07), (.63, -.22)]
    shoulder = rock('Broken shoulder', keep, shoulder_outline, [.48, .53, .43, .62, .65, .57], .045, stone_mats)
    moss('Small shoulder moss', keep, shoulder, (.96, -.29), (.16, .17), moss_mats, 1.7)
    objects = [root]+list(root.children_recursive)
    low, high, triangles = measure(objects)
    dimensions = [high[0]-low[0], high[2]-low[2], high[1]-low[1]]
    assert triangles <= 1500, triangles
    assert all(a <= b for a, b in zip(dimensions, (2.771, 1.39, 1.911))), dimensions
    assert abs(low[2]) < .001, low
    record = dict(id=ASSET, name='Forest pilot fractured moss boulder', category='Forest surface quality pilot',
                  file=ASSET+'.glb', dimensions_m=[2.771, 1.39, 1.911],
                  actual_dimensions_m=[round(v, 5) for v in dimensions], bounds_min_y=round(low[2], 6),
                  collision=dict(kind='cylinder', radius=1.05, height=1.2), occlusion_parts=[], triangles=triangles,
                  note='Dedicated pilot asset; broad bevelled stone and fitted shallow moss. Keep role. Dimensions preserve original boulder placement/exclusion envelope; actual_dimensions_m measures mesh.')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(PACK/(ASSET+'.glb')), export_format='GLB', use_selection=True,
                             export_yup=True, export_cameras=False, export_lights=False)
    manifest = dict(revision=1, contract='plushpanic_outdoor_v1', units='metres', up_axis='+Y', assets=[record])
    (PACK/'manifest_forest_surface.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    source = REPO/'prototypes/outdoor-kit/source/forest-surface-boulder.blend'
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source))
    # Explicit import settings, without invoking Godot. Godot supplies its resource UID on import.
    resource = 'res://assets/stage2-props/'+ASSET+'.glb'
    cache = 'res://.godot/imported/'+ASSET+'.glb-'+hashlib.md5(resource.encode()).hexdigest()+'.scn'
    import_file = PACK/(ASSET+'.glb.import')
    if not import_file.exists():
        import_file.write_text('[remap]\n\nimporter="scene"\nimporter_version=1\ntype="PackedScene"\npath="'+cache+'"\n\n'
                              '[deps]\n\nsource_file="'+resource+'"\ndest_files=["'+cache+'"]\n\n'
                              '[params]\n\nnodes/root_scale=1.0\nnodes/apply_root_scale=true\n'
                              'meshes/generate_lods=true\nmeshes/create_shadow_meshes=true\nanimation/import=false\n'
                              'gltf/naming_version=2\ngltf/embedded_image_handling=1\n', encoding='utf-8')
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(PACK/(ASSET+'.glb')))
    imported = set(bpy.data.objects)-before
    ilow, ihigh, itriangles = measure(imported)
    assert itriangles == triangles
    assert any(obj.name.split('.')[0] == 'Keep' for obj in imported)
    assert all(abs(a-b) < .0001 for a, b in zip(low+high, ilow+ihigh))
    assert all(mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value >= .95
               for obj in imported if obj.type == 'MESH' for mat in obj.data.materials)
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    (OUT/'report.json').write_text(json.dumps(dict(asset=record, glb_reimport='passed',
        bounds_min=low, bounds_max=high, source=str(source)), indent=2)+'\n', encoding='utf-8')
    print('FOREST_SURFACE_BOULDER_PASS '+json.dumps(record), flush=True)
    # Preview only; the saved source and export contain the asset without this staging.
    bpy.ops.mesh.primitive_plane_add(size=200)
    floor = bpy.context.object
    floor.location.z = -.015
    floor.data.materials.append(material('Preview ground', 'a7a18b'))
    bpy.ops.object.light_add(type='AREA', location=(-3, -4, 7))
    sun = bpy.context.object
    sun.data.energy = 650
    sun.data.shape = 'DISK'
    sun.data.size = 4
    sun.rotation_euler = (Vector((0, 0, .5))-sun.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(location=(3.5, -5, 3.6))
    cam = bpy.context.object
    cam.rotation_euler = (Vector((.1, 0, .55))-cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 3.8
    scene = bpy.context.scene
    scene.camera = cam
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 32
    scene.world.color = (.30, .30, .30)
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = str(OUT/'boulder-front.png')
    bpy.ops.render.render(write_still=True)
    cam.location = (-3.5, 4, 4.5)
    cam.rotation_euler = (Vector((0, 0, .55))-cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(OUT/'boulder-rear.png')
    bpy.ops.render.render(write_still=True)


if __name__ == '__main__':
    run()
