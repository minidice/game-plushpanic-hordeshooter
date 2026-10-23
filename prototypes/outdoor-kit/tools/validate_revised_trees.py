"""Prevent old tree models and generation recipes from returning to the catalog."""
import ast
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
IDS = ['pine_tall', 'pine_small', 'broadleaf', 'round_oak', 'round_linden']
REMOVED = ['edge_forest_pine', 'edge_forest_round', 'edge_forest_low', 'edge_backdrop']


def main():
    manifest = json.loads((REPO/'game/assets/outdoor-kit/manifest.json').read_text(encoding='utf-8'))
    trees = {a['id']: a for a in manifest['assets'] if a['id'] in IDS}
    assert len(trees) == 5
    assert manifest['tree_revision'] == 'foliage-art-2026-10-24'
    for asset_id, item in trees.items():
        raw = (REPO/'game/assets/outdoor-kit'/item['file']).read_bytes()
        size = struct.unpack_from('<I', raw, 12)[0]
        gltf = json.loads(raw[20:20+size])
        assert any(n.get('extras', {}).get('tree_revision') == manifest['tree_revision'] for n in gltf['nodes']), asset_id
        assert len(item['occlusion_parts']) == 4
        assert item['triangles'] == (2384 if asset_id.startswith('pine') else 2600)
        assert not any(term in raw for term in [b'LeafPad', b'SoftFoliageCore', b'OverlappingBough', b'RecessedNeedles'])
        assert all('COLOR_0' in p['attributes'] for mesh in gltf['meshes'] for p in mesh['primitives'])
        assert all(m.get('alphaMode', 'OPAQUE') == 'OPAQUE' for m in gltf['materials'])
        print('PASS canonical tree', asset_id, item['triangles'])
    edge = json.loads((REPO/'game/assets/edge-kit/manifest.json').read_text(encoding='utf-8'))
    assert not any(a['id'] in REMOVED for a in edge['assets'])
    for asset_id in REMOVED:
        assert not list((REPO/'game/assets/edge-kit').glob(asset_id+'.glb*')), asset_id
    for path in [ROOT/'tools/nature_shapes.py', ROOT/'tools/edge_kit.py']:
        functions = {node.name for node in ast.walk(ast.parse(path.read_text(encoding='utf-8'))) if isinstance(node, ast.FunctionDef)}
        assert not functions.intersection({'leaf_spray', 'forest_pine', 'forest_round', 'forest_low', 'backdrop'})
    assert 'from revised_trees import build_trees' in (ROOT/'tools/nature_shapes.py').read_text(encoding='utf-8')
    assert "source/tree-geometry.json" in (ROOT/'tools/revised_trees.py').read_text(encoding='utf-8')
    assert not (ROOT/'pack').exists(), 'Duplicate prototype pack can restore stale trees'
    print('PASS removed legacy tree assets, recipes and duplicate loadable pack')


if __name__ == '__main__':
    main()
