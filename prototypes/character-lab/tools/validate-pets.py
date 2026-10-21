"""Check exported pet assets, including attachment axes and independent articulation."""
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ASSETS = ROOT / 'game/assets/pets'

def read(asset):
    path = ASSETS / f'{asset}.glb'
    assert path.exists(), f'Missing pet asset: {path}'
    raw = path.read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    assert (magic, version, length) == (0x46546C67, 2, len(raw))
    size, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4E4F534A
    return json.loads(raw[20:20+size])

def main():
    counts={}
    pivots={}
    for asset in ['turtle', 'dog', 'rabbit', 'turtle_low', 'dog_low', 'rabbit_low', 'twin_blaster', 'rocket_pod']:
        doc = read(asset)
        nodes = {n['name']: n for n in doc['nodes']}
        assert len(nodes) == len(doc['nodes']), 'Duplicate node names'
        assert not doc.get('skins'), 'Use separate rigid parts, not a skeleton'
        pet = asset.removesuffix('_low') in ['turtle', 'dog', 'rabbit']
        required = ['Body', 'Head', 'Tail', 'FrontLeft', 'FrontRight', 'RearLeft', 'RearRight', 'BackSocket'] if pet else ['Yaw', 'Pitch', 'Muzzle']
        for name in required:
            assert name in nodes, (asset, name)
        if pet:
            socket = nodes['BackSocket']
            assert socket.get('rotation', [0,0,0,1]) == [0,0,0,1], 'Socket must not rotate a shared weapon'
            assert socket.get('scale', [1,1,1]) == [1,1,1]
            assert .7 < socket['translation'][1] < 1.3
            assert 'Muzzle' not in nodes and 'Yaw' not in nodes, 'Base pet must be unarmed'
            body_children = [doc['nodes'][i]['name'] for i in nodes['Body']['children']]
            assert 'BackSocket' in body_children
        else:
            assert nodes['Muzzle']['translation'][2] > .5, 'Muzzle points +Z'
            assert 'Muzzle' in [doc['nodes'][i]['name'] for i in nodes['Pitch']['children']]
        tris = sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
        assert tris <= ((10000 if asset.endswith('_low') else 30000) if pet else 3500), (asset, tris)
        counts[asset]=tris
        if pet:
            pivots[asset]={name:{key:nodes[name].get(key) for key in ['translation','rotation','scale']} for name in required}
            assert any('COLOR_0' in p['attributes'] for mesh in doc['meshes'] for p in mesh['primitives']), asset
        assert len(doc['materials']) <= 8
        assert len(doc['meshes']) <= 14
        print(f'PET_ASSET_PASS {asset}: {tris} triangles, {len(doc["meshes"])} meshes, {len(doc["materials"])} materials')
    for species in ['turtle','dog','rabbit']:
        assert counts[species+'_low']<counts[species]*.55, species
        assert pivots[species]==pivots[species+'_low'], species
    print('PET_TIERS_PASS identical pivots; low triangle count below 55% of high')

if __name__ == '__main__':
    main()
