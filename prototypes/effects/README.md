# Area effect texture source

Run from the repository root:

```powershell
python prototypes/effects/tools/generate_area_textures.py
```

Dependencies: NumPy and Pillow. The generator writes seven original PNGs and a hash manifest under `game/assets/effects/area/`. It checks transparent atlas borders and the flame's closed time seam. Rerunning with the same NumPy/Pillow versions produces identical PNG hashes. Runtime imports must retain mipmaps and alpha-border correction; commit `.import` and shader/script `.uid` alongside the resources.

- Smoke: four512px variants packed2×2, grayscale brightness with soft alpha; runtime color is `smoke_color`.
- Flame: sixteen128px frames packed4×4. Time loops at2π; start frame and speed vary per particle.
- Flow: two512px periodic noise masks.
- Scorch: three512px flat-ground masks. No terrain projection.

The game/Studio share `combat_effects.json`, `CombatSurfaceEffect`, and `AreaSurfaceVisual`. See [current VFX contract](../../doc/vfx-quality.md) and [implementation record](../../doc/features/effects-audio/log/004-code-area-effects.md).
