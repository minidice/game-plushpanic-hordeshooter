"""Validate shipping weapon GLBs without requiring Godot or third-party packages."""
import csv
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
def rows(path):
    with path.open(encoding='utf-8-sig') as f:
        return list(csv.DictReader(line for line in f if not line.startswith('#')))

types = {r['type']: r for r in rows(ROOT / 'game/content/data/weapon_types.csv')}
weapons = rows(ROOT / 'game/content/data/weapons.csv')
report = []
for row in weapons:
    if row['type'] == 'melee':
        continue
    assert row['model'], f"{row['id']}: missing model"
    path = ROOT / 'game' / row['model'].removeprefix('res://')
    data = path.read_bytes()
    magic, version, size = struct.unpack_from('<III', data)
    assert magic == 0x46546c67 and version == 2 and size == len(data)
    length, kind = struct.unpack_from('<II', data, 12)
    doc = json.loads(data[20:20+length])
    muzzles = [n for n in doc['nodes'] if n.get('name') == 'Muzzle']
    assert len(muzzles) == 1, row['id']
    muzzle = muzzles[0].get('translation', [0, 0, 0])
    expected = float(row['length_m'] or types[row['type']]['length_m'])
    assert muzzle[2] > 0 and muzzle[1] > -.05, (row['id'], muzzle)
    if row['type'] != 'mortar':
        assert abs(muzzle[2] - expected) < .025, (row['id'], muzzle, expected)
    tris = sum(doc['accessors'][p['indices']]['count']//3 for m in doc['meshes'] for p in m['primitives'])
    assert 100 <= tris <= 5000, (row['id'], tris)
    assert len(doc['meshes']) <= 7
    assert len(doc.get('materials', [])) <= 7
    assert not any('hand' in n.get('name','').lower() for n in doc['nodes'])
    report.append({'id': row['id'], 'triangles': tris, 'meshes': len(doc['meshes']), 'materials': len(doc.get('materials', [])), 'muzzle': muzzle})
print(json.dumps(report, indent=2))
print(f'PASS: {len(report)} weapon assets, orientation/muzzles and triangle budgets')
