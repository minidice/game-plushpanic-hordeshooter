"""Validate all drone bodies, interchangeable equipment and quality pivots."""
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BODIES = ['quadcopter', 'ladybug', 'bee', 'ufo']
MODULES = ['light', 'laser', 'blaster', 'support']

def read(name):
    p = ROOT / f'game/assets/drones/{name}.glb'
    assert p.exists(), f'Missing drone asset: {p}'
    raw = p.read_bytes()
    assert struct.unpack_from('<III', raw) == (0x46546c67, 2, len(raw))
    size, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4e4f534a
    return json.loads(raw[20:20+size])

def main():
    counts, pivots = {}, {}
    for name in BODIES + [n+'_low' for n in BODIES] + MODULES:
        doc = read(name)
        nodes = {n['name']: n for n in doc['nodes']}
        assert len(nodes) == len(doc['nodes']) and not doc.get('skins')
        body = name.removesuffix('_low') in BODIES
        required = ['Body', 'EquipmentSocket'] if body else ['Yaw', 'Pitch', 'Muzzle']
        for key in required: assert key in nodes, (name, key)
        if body:
            socket = nodes['EquipmentSocket']
            assert all(abs(a-b)<1e-5 for a,b in zip(socket['translation'], [0,-.22,0]))
            assert socket.get('rotation',[0,0,0,1]) == [0,0,0,1]
            assert socket.get('scale',[1,1,1]) == [1,1,1]
            assert 'Muzzle' not in nodes and not socket.get('children')
            pivots[name] = {n['name']:{k:n.get(k) for k in ['translation','rotation','scale']} for n in doc['nodes'] if 'mesh' not in n}
        else:
            assert nodes['Muzzle']['translation'][2] > .3
        count = sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
        assert count <= (10000 if name.endswith('_low') else 30000 if body else 3500), (name,count)
        assert len(doc['materials'])<=8 and len(doc['meshes'])<=10, name
        counts[name] = count
        print(f'DRONE_ASSET_PASS {name}: {count} triangles, {len(doc["meshes"])} meshes, {len(doc["materials"])} materials')
    for name in BODIES:
        assert pivots[name] == pivots[name+'_low'], name
        assert counts[name+'_low'] < .55*counts[name], name
    print('DRONE_TIERS_PASS shared socket and identical pivots')

if __name__ == '__main__': main()
