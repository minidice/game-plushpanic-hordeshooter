"""Validate the delivered GLB contract without Blender or third-party packages."""
import json
import math
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def main():
    manifest_path = ROOT.parents[1] / 'game/assets/outdoor-kit/manifest.json'
    assert manifest_path.exists(), 'Model pack has not been generated: pack/manifest.json missing'
    data = json.loads(manifest_path.read_text(encoding='utf-8'))
    assert len(data['assets']) == 32
    by_id={a['id']:a for a in data['assets']}
    for name in ['pine_tall','pine_small','broadleaf','round_oak','round_linden']:
        assert len(by_id[name]['occlusion_parts'])==4, name
    for name in ['stream_straight','stream_curve']:
        assert by_id[name]['waterline_m']==0.24
        assert len(by_id[name]['connectors'])==2
    ids = set()
    total = 0
    for a in data['assets']:
        assert a['id'] not in ids
        ids.add(a['id'])
        path = ROOT.parents[1] / 'game/assets/outdoor-kit' / a['file']
        raw = path.read_bytes()
        magic, version, length = struct.unpack_from('<4sII', raw)
        assert (magic, version, length) == (b'glTF', 2, len(raw)), path
        size, kind = struct.unpack_from('<I4s', raw, 12)
        assert kind == b'JSON'
        gltf = json.loads(raw[20:20+size])
        assert not any('uri' in b for b in gltf.get('buffers', [])), path
        assert not any('uri' in i for i in gltf.get('images', [])), path
        names = {n.get('name') for n in gltf['nodes']}
        for node in gltf['nodes']:
            if 'mesh' in node and node.get('name','').startswith('Occlude_'):
                assert node['name'].endswith('_Mesh'), (path,'Mesh name must not replace semantic parent role',node['name'])
        assert 'Keep' in names, (path, names)
        assert all(n in names for n in a['occlusion_parts']), path
        assert all(m.get('alphaMode','OPAQUE') == 'OPAQUE' for m in gltf.get('materials',[])), path
        tris = 0
        for mesh in gltf['meshes']:
            for p in mesh['primitives']:
                assert p.get('mode',4) == 4, path
                pos = gltf['accessors'][p['attributes']['POSITION']]
                assert all(math.isfinite(v) for v in pos['min'] + pos['max']), path
                tris += gltf['accessors'][p['indices']]['count']//3
        assert tris == a['triangles'] and 0 < tris <= 16000, (path,tris,a['triangles'])
        assert all(v > 0 for v in a['dimensions_m']), path
        assert abs(a['bounds_min_y']) < .015, (path,a['bounds_min_y'])
        assert a['collision']['kind'] in ['none','box','cylinder','compound'], path
        total += tris
        print(f"PASS {a['id']}: {tris} triangles; {len(a['occlusion_parts'])} independently controllable parts")
    print(f'PASS {len(ids)} GLB assets; {total} triangles total (inventory, not a scene draw count)')

if __name__ == '__main__':
    main()
