"""Default mammal tail bake; matches CharacterTailMesh's long-tail profile.

Only the tail mesh data changes. Original object/pivot/material channels survive.
"""
import math
import bpy
import bmesh
from mathutils import Vector


def replace_tail(species):
    length, radius = (.95, .145) if species == 'raccoon' else (1.15, .18)
    rings, sides = 36, 24
    pivot = bpy.data.objects[f'{species}_tail']
    obj = bpy.data.objects[f'{species}_tail_mesh']
    old = obj.data

    def center(t):
        # Blender coordinates: +Z up, +Y rearward.
        return Vector((.10 * math.sin(math.pi*t/2)**2,
                       length*t, -.075*math.sin(math.pi*t)+.10*t*t))

    points, faces, bands = [], [], []
    to_mesh = obj.matrix_world.inverted() @ pivot.matrix_world
    for i in range(rings):
        t = i/rings
        c = center(t)
        tangent = (center(min(1,t+.001))-center(max(0,t-.001))).normalized()
        across = tangent.cross(Vector((0,0,1))).normalized()
        up = across.cross(tangent).normalized()
        r = radius * (1 if t <= .8 else math.sqrt(max(0,1-((t-.8)/.2)**2)))
        for j in range(sides):
            angle = 2*math.pi*j/sides
            points.append(to_mesh @ (c+r*(math.cos(angle)*across+math.sin(angle)*up)))
    tip = len(points)
    points.append(to_mesh @ center(1))
    faces.append(tuple(reversed(range(sides)))); bands.append(0)
    for i in range(rings-1):
        for j in range(sides):
            k = (j+1)%sides
            faces.append((i*sides+j,i*sides+k,(i+1)*sides+k,(i+1)*sides+j))
            bands.append(min(6,i*7//rings)%2)
    for j in range(sides):
        faces.append(((rings-1)*sides+j,(rings-1)*sides+(j+1)%sides,tip))
        bands.append(0)  # Original clean mammal tails have a light terminal cap.
    mesh = bpy.data.meshes.new(f'{species}_tail_uniform')
    mesh.from_pydata(points,[],faces)
    for material in old.materials: mesh.materials.append(material)
    for polygon, band in zip(mesh.polygons,bands):
        polygon.material_index = band
        polygon.use_smooth = True
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free()
    obj.data = mesh
    if old.users == 0: bpy.data.meshes.remove(old)
