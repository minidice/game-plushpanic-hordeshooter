"""Original, deterministic area VFX masks. Run with Python + numpy + Pillow.

All noise is periodic; flame frames sample a closed time loop. RGB remains
defined outside alpha to avoid dark fringes when filtering the atlas.
"""
from pathlib import Path
import hashlib
import json
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'game/assets/effects/area'


def noise(x, y, seed, t=0):
    rng = np.random.default_rng(seed)
    value = np.zeros_like(x + y)
    weight = 0
    for octave, amp in enumerate((1, .48, .23, .11)):
        for _ in range(9):
            kx, ky = rng.integers(-3, 4, 2) * 2**octave
            if kx == 0 and ky == 0:
                kx = 2**octave
            phase = rng.uniform(0, 2*np.pi)
            value += amp * np.sin(2*np.pi*(kx*x+ky*y)+phase+t)
            weight += amp
    return np.clip(.5 + value / weight * 1.8, 0, 1)


def grid(n):
    y, x = np.mgrid[0:n, 0:n] / (n-1)
    return x, y


def rgba(rgb, alpha):
    rgb = np.broadcast_to(rgb, (*alpha.shape, 3))
    return np.uint8(np.clip(np.dstack((rgb, alpha)), 0, 1)*255+.5)


def smoke(seed):
    x, y = grid(512)
    rng = np.random.default_rng(seed)
    density = np.zeros_like(x)
    for _ in range(13):
        cx, cy = rng.uniform(.25, .75, 2)
        sx, sy = rng.uniform(.09, .22, 2)
        density += np.exp(-((x-cx)/sx)**2-((y-cy)/sy)**2)*rng.uniform(.4, 1)
    density /= density.max()
    n = noise(x, y, seed)
    edge = np.clip(np.minimum.reduce((x, y, 1-x, 1-y))*12, 0, 1)**2
    alpha = np.clip(density*(.35+1.1*n), 0, 1)*edge
    light = .58 + .42*noise(x, y, seed+91)
    return rgba(light[..., None], alpha)


def flame(t):
    x, y = grid(128)
    height = 1-y
    # Moving fingers with a wider hot base. Every term closes at 2*pi.
    bend = .08*np.sin(height*9+t) + .035*np.sin(height*19-2*t)
    width = .06+.26*y**1.35
    fingers = np.exp(-((x-.5-bend)/width)**2)
    flow = noise(x*.6, y*.6, 82, t)
    a = np.clip((fingers*(.55+.95*flow)-height*.72-.12)*2.5, 0, 1)
    a *= np.clip(y*12, 0, 1)*np.clip((1-y)*16, 0, 1)
    a *= np.clip(np.minimum(x,1-x)*12,0,1)
    hot = np.clip(y*.85+flow*.3, 0, 1)
    rgb = np.stack((np.ones_like(x), .16+.78*hot**1.4, .02+.32*hot**4), -1)
    return rgba(rgb, a)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    tiles = [smoke(70+i) for i in range(4)]
    Image.fromarray(np.concatenate((np.concatenate(tiles[:2],1),np.concatenate(tiles[2:],1)),0)).save(OUT/'smoke_atlas.png')
    flames = [flame(2*np.pi*i/16) for i in range(16)]
    atlas = np.concatenate([np.concatenate(flames[i:i+4],1) for i in range(0,16,4)],0)
    Image.fromarray(atlas).save(OUT/'flame_atlas.png')
    x, y = grid(512)
    for i in range(2):
        n = noise(x, y, 310+i)
        Image.fromarray(np.uint8(n*255)).save(OUT/f'flow_{i}.png')
    for i in range(3):
        n = noise(x,y,130+i)
        r = np.sqrt((x-.5)**2+(y-.5)**2)*2
        a = np.clip((1-r)*3 + (n-.5)*2, 0, 1)*( .25+.65*n)
        a *= np.clip((1-r)*8,0,1)
        Image.fromarray(rgba(np.array([.16,.13,.12]),a)).save(OUT/f'scorch_{i}.png')
    # Validate the assets rather than relying on successful PNG encoding alone.
    for name, frames in [('smoke',tiles),('flame',flames)]:
        for f in frames:
            assert f[0,:,3].max()==0 and f[-1,:,3].max()==0
            assert f[:,0,3].max()==0 and f[:,-1,3].max()==0
            assert 0 < f[...,3].mean() < 190, name
    diffs = [np.abs(flames[(i+1)%16].astype(float)-flames[i]).mean() for i in range(16)]
    assert max(diffs)<12 and diffs[-1]<max(diffs[:-1])*1.5
    hashes = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(OUT.glob('*.png'))}
    (OUT/'manifest.json').write_text(json.dumps({'generator':'prototypes/effects/tools/generate_area_textures.py',
        'seed_version':1,'flame_frames':16,'smoke_variants':4,'sha256':hashes},indent=2)+'\n',encoding='utf-8')
    print('AREA_TEXTURES_PASS', len(hashes), 'textures; flame seam MAE', round(diffs[-1],3))


if __name__ == '__main__':
    main()
