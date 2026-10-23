"""Portable, opaque trees from the exact approved FoliageArt geometry snapshot.

Refresh source/tree-geometry.json with game/tests/TreeAssetExport.tscn only when
the procedural authoring geometry changes. No previous leaf-pad recipe remains.
"""
import json
import math
from pathlib import Path

SOURCE = Path(__file__).resolve().parents[1] / 'source/tree-geometry.json'
REVISION = 'foliage-art-2026-10-24'
TREES = [('pine_tall', 'pine', 1, .32, 1.5),
         ('pine_small', 'pine', .68, .2176, 1.02),
         ('broadleaf', 'broadleaf', 1, .36, 1.7),
         ('round_oak', 'oak', 1, .4392, 1.7),
         ('round_linden', 'broadleaf', .8, .288, 1.7)]


def albedo(p, normal, color):
    # Evaluate the static material tint from foliage.gdshader, without lights/AO.
    # Vertex alpha is an authoring leaf/bark mask, never output transparency.
    if color[3] < .5:
        return (*color[:3], 1)
    inner = [.15, .29, .22]
    inner = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in inner]
    t = max(0, min(1, (normal[1] + .65) / 1.4)); t = t*t*(3-2*t)
    patch = .97 + .025*math.sin(p[0]*3.1+p[2]*2.7) + .015*math.sin(p[1]*4-p[2]*3)
    return (*( (a*(1-t)+c*1.12*t)*patch for a,c in zip(inner,color[:3]) ), 1)


def build_trees(b):
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    assert data['revision'] == REVISION
    meshes = {m['id']: m for m in data['meshes']}
    for key, roughness in [('RevisedBark', .96), ('RevisedCanopy', .88)]:
        material = b.mat(key, 'ffffff', roughness)
        nodes = material.node_tree.nodes
        vertex_color = nodes.new('ShaderNodeVertexColor'); vertex_color.layer_name = 'TreeAlbedo'
        material.node_tree.links.new(vertex_color.outputs['Color'], nodes.get('Principled BSDF').inputs['Base Color'])
        nodes.get('Principled BSDF').inputs['Specular IOR Level'].default_value = .2
        b.M[key] = material
    names = {'pine_tall':'겹잎 침엽수 · 대', 'pine_small':'겹잎 침엽수 · 소', 'broadleaf':'겹잎 활엽수', 'round_oak':'둥근 참나무 · 넓은 수관', 'round_linden':'둥근 피나무 · 작은 수관'}
    for asset_id, source_id, scale, radius, height in TREES:
        source = meshes[source_id]
        root, keep, info = b.start(asset_id, names[asset_id], '식생',
            '승인된 둥근 가지층/수관. FoliageArt 원형, 줄기와 수관 4구역 분리.',
            b.coll('cylinder', radius=radius, height=height))
        root['tree_revision'] = REVISION
        info['tree_revision'] = REVISION
        labels = ['NE', 'NW', 'SW', 'SE'] if source_id == 'pine' else ['0', '1', '2', '3']
        parents = [keep] + [b.occ(root, info, 'Canopy_'+label) for label in labels]
        groups = [[] for _ in parents]
        for start in range(0, len(source['indices']), 3):
            triangle = source['indices'][start:start+3]
            if source['colors'][triangle[0]][3] < .5:
                group = 0
            else:
                x = sum(source['vertices'][i][0] for i in triangle)
                z = -sum(source['vertices'][i][2] for i in triangle)
                group = int((math.atan2(z,x) % math.tau) / (math.pi/2)) + 1
            groups[group].append(triangle)
        for index, triangles in enumerate(groups):
            ids = [i for triangle in triangles for i in reversed(triangle)]  # Godot CW -> Blender CCW
            verts = [(source['vertices'][i][0]*scale, -source['vertices'][i][2]*scale,
                      source['vertices'][i][1]*scale) for i in ids]
            normals = [(source['normals'][i][0], -source['normals'][i][2], source['normals'][i][1]) for i in ids]
            obj = b.mesh(parents[index], 'RevisedTree', verts,
                         [(i,i+1,i+2) for i in range(0,len(ids),3)],
                         'RevisedBark' if index == 0 else 'RevisedCanopy')
            for polygon in obj.data.polygons: polygon.use_smooth = True
            obj.data.normals_split_custom_set_from_vertices(normals)
            colors = obj.data.color_attributes.new(name='TreeAlbedo', type='FLOAT_COLOR', domain='POINT')
            for vertex, source_index in enumerate(ids):
                colors.data[vertex].color = albedo(source['vertices'][source_index], source['normals'][source_index], source['colors'][source_index])
        b.finish_asset(root, info)
