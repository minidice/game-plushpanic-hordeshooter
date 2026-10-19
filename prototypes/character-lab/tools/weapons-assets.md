# Stylized weapon assets

These are original, textureless stylized firearm models for the current Godot
weapon roster. Fifteen GLBs are backed by nine structural families: pistol,
compact SMG, rifle, pump shotgun, double barrel, scoped sniper, LMG, rocket
launcher and mortar. This is family-based art: pistols share geometry; R870 and
Benelli share a pump silhouette; AWM and SVD share a scoped rifle silhouette.
M16 adds a carry handle, AK adds a slanted magazine and wood finish, M82 adds a
muzzle brake and folded bipod. They are stylized representations rather than
accurate replicas of the named commercial firearms. The frying pan retains
its existing procedural visual.

Every model uses metres, +Z forward and +Y up in glTF/Godot. The origin is the
grip attachment; shoulder stocks extend behind it. A single `Muzzle` empty
sits at the barrel exit, at the weapon's resolved `length_m` forward distance
except for the elevated mortar. Double-barrel shots originate between the two
bores. Hands are supplied by the game and are absent from these models.

Rounded receiver, furniture and magazine edges are explicit geometry. Barrels
have hollow muzzle crowns, magazines and open trigger guards are separate
forms, and family-specific geometry includes rails, scope lenses, heat vents,
ammunition belts, folded bipods, launcher bands and mortar supports. Geometry
is consolidated by material into 4–6 meshes/materials per asset. Triangles
range from 1,168 to 4,128. No external textures or branded markings are used.

## Rebuild and validate

Run from the repository root:

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/character-lab/tools/weapons-build.py
python prototypes/character-lab/tools/weapons-validate.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/character-lab/tools/weapons-render.py
```

The builder writes shipping GLBs under `game/assets/weapons/` and updates only
the model column of `game/content/data/weapons.csv`. An assertion verifies
every other CSV cell is unchanged. It writes editable Blender source,
`asset-report.json` and a rendered contact sheet under
`prototypes/character-lab/output/weapons/`, outside Godot's import tree. Blender
source muzzle names are prefixed by weapon ID to keep them unique in the
combined scene; individual exported GLBs contain exactly `Muzzle`.

The standalone validator reads actual GLB JSON and checks 15 assigned models,
node naming, positive forward muzzle position, forward length, triangle budget,
mesh/material budget and absence of built-in hands. It does not replace Godot
import or runtime tests. `WeaponVisual` now loads custom models for both dual
pistols and retains the existing procedural fallback if no model can load.

The primary contact sheet keeps relative physical size; the alternate side
sheet exposes magazines and grip shapes more clearly. Both are Blender renders
of the same source geometry exported to the game.
