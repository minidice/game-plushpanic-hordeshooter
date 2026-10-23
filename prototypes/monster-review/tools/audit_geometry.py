"""Read-only GLB audit. Position-welded boundaries exclude ordinary UV/normal seams.

Not every boundary is a defect (e.g. intentionally open clothing). Compare source
and runtime assets, then corroborate with rendered surfaces. Does not edit GLBs.
"""
from pathlib import Path
from collections import Counter
import json
import struct
import sys

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'game/output/monster-review'


def audit(path):
    raw = path.read_bytes()
    offset = 12
    chunks = {}
    while offset < len(raw):
        size, kind = struct.unpack_from('<II', raw, offset)
        chunks[kind] = raw[offset + 8:offset + 8 + size]
        offset += 8 + size
    doc = json.loads(chunks[0x4E4F534A])
    binary = chunks[0x004E4942]

    def values(index):
        a = doc['accessors'][index]
        v = doc['bufferViews'][a['bufferView']]
        fmt = {5126: 'f', 5125: 'I', 5123: 'H', 5121: 'B'}[a['componentType']]
        width = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        size = struct.calcsize('<' + fmt * width)
        start = v.get('byteOffset', 0) + a.get('byteOffset', 0)
        return [struct.unpack_from('<' + fmt * width, binary, start + i * v.get('byteStride', size)) for i in range(a['count'])]

    meshes = []
    for mesh in doc.get('meshes', []):
        edges = Counter()
        vertex_ids = {}
        triangles = 0
        for p in mesh['primitives']:
            assert p.get('mode', 4) == 4
            points = values(p['attributes']['POSITION'])
            ids = []
            for point in points:
                key = tuple(round(c, 5) for c in point)
                ids.append(vertex_ids.setdefault(key, len(vertex_ids)))
            indices = [i[0] for i in values(p['indices'])] if 'indices' in p else list(range(len(points)))
            triangles += len(indices) // 3
            for i in range(0, len(indices), 3):
                a, b, c = (ids[j] for j in indices[i:i + 3])
                if len({a, b, c}) < 3:
                    continue
                for u, v in ((a, b), (b, c), (c, a)):
                    edges[tuple(sorted((u, v)))] += 1
        meshes.append({'name': mesh.get('name', ''), 'triangles': triangles,
                       'position_welded_boundary_edges': sum(n == 1 for n in edges.values())})
    return {'path': str(path.relative_to(ROOT)), 'triangles': sum(m['triangles'] for m in meshes),
            'position_welded_boundary_edges': sum(m['position_welded_boundary_edges'] for m in meshes), 'meshes': meshes}


if __name__ == '__main__':
    report = {}
    for model in ('humanoid', 'crawler', 'tether', 'leaper', 'spitter', 'bull', 'balloon', 'boss', 'boss_balloon_king', 'boss_spitter_mother'):
        report[model] = {}
        for label, path in [('source', ROOT / f'prototypes/character-lab/output/roster/{model}-source.glb'),
                            ('game', ROOT / f'game/assets/enemies/{model}/{model}.glb')]:
            if path.exists():
                report[model][label] = audit(path)
        print(model, {k: (v['triangles'], v['position_welded_boundary_edges']) for k, v in report[model].items()})
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'geometry-audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    if '--check' in sys.argv:
        failures = [model for model, versions in report.items()
                    if versions['game']['position_welded_boundary_edges'] > versions['source']['position_welded_boundary_edges']]
        assert not failures, f'Optimization introduced open surface boundaries: {failures}'
        print('MONSTER_SURFACE_PASS 10 (source openings preserved; no new boundary edges)')
