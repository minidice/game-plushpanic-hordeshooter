"""Independent cross-section and closed-surface checks on reimported GLBs."""
import math
import bmesh
from mathutils import Vector


def validate_tail(objects, species, check):
    pivot = objects[f'{species}_tail']
    mesh = objects[f'{species}_tail_mesh']
    inverse = pivot.matrix_world.inverted()
    points = {tuple(round(c,6) for c in inverse @ mesh.matrix_world @ v.co)
              for v in mesh.data.vertices}
    points = [Vector(p) for p in points]
    length, radius = (.95,.145) if species == 'raccoon' else (1.15,.18)
    # Independent section planes from the specified analytic centerline/derivative.
    for ring in (0,6,12,18,24,28,31,34,35):
        t = ring/36
        center = Vector((.05*(1-math.cos(math.pi*t)),length*t,
                         -.075*math.sin(math.pi*t)+.1*t*t))
        tangent = Vector((.05*math.pi*math.sin(math.pi*t),length,
                          -.075*math.pi*math.cos(math.pi*t)+.2*t)).normalized()
        section = [p-center for p in points if abs((p-center).dot(tangent)) < .0002]
        check(len(section)==24, f'{species} tail ring {ring} has 24 cross-section samples')
        expected = radius if t <= .8 else radius*math.sqrt(1-((t-.8)/.2)**2)
        check(all(abs(p.length-expected)<.00005 for p in section),
              f'{species} tail ring {ring} uniform radius / rounded final fifth')
    check(any((p-Vector((.1,length,.1))).length<.00001 for p in points),
          f'{species} tail gently curved tip at specified length')
    check(len(mesh.data.materials)==2 and set(p.material_index for p in mesh.data.polygons)=={0,1},
          f'{species} tail retains both stripe channels')
    tip_ids = {v.index for v in mesh.data.vertices
               if (inverse @ mesh.matrix_world @ v.co - Vector((.1,length,.1))).length < .00001}
    tip_faces = [p for p in mesh.data.polygons if any(i in tip_ids for i in p.vertices)]
    check(bool(tip_faces) and all(p.material_index == 0 for p in tip_faces),
          f'{species} tail preserves original light terminal cap')
    bm=bmesh.new(); bm.from_mesh(mesh.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
    check(all(e.is_manifold for e in bm.edges),f'{species} tail closed manifold')
    check(bm.calc_volume(signed=True)>0,f'{species} tail positive volume')
    bm.normal_update(); normals=[p.normal.copy() for p in bm.faces]
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    check(all(a.dot(b.normal)>.999 for a,b in zip(normals,bm.faces)),
          f'{species} tail consistently outward winding')
    bm.free()
