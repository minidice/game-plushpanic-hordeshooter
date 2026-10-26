# Common monster source and verification

Current art target (2026-10-25): [clean stylized animals with grounded equipment](../../doc/character-art-direction.md). Do not use plush/stuffed-toy styling as the character brief. Parrot, eagle, lion, reindeer and the human infected have received the revised forms; tiger and rhino still use earlier iterations. See [reindeer/zombie evidence and limits](../../doc/reindeer-zombie-clean-forms.md).

2026-10-25. This is the reproducible Blender source for the six animal species and the
refined existing human infected. The original humanoid GLB is imported read-only: its
asymmetric face, teeth, ears and arm geometry remain the visual identity. Its former
baked clothing is replaced by a rounded skin torso, short legs and selectable garments.
The current infected pass retains the asymmetric human face with shallow eyes, a smaller attached mouth/teeth, compact hands and flat soles; it removes detached glint beads.

## Rebuild

From the repository root (Blender 5.2.2 was used):

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/build_common_monsters.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/validate_common_monsters.py
python prototypes/common-monsters/tools/contact_sheet.py
```

`-- --no-render` skips images but still builds, audits and exports. The first command
writes seven game GLBs and seven `common_*.tres` resources. `.blend` authoring snapshots,
40 rendered PNGs, contact sheets and audit JSON belong in ignored `output/`.
The generator contains no random calls and no external texture dependencies.
Pillow is required only by the contact sheet tool. Blender's material API currently
prints a `Material.use_nodes` deprecation warning for Blender 6.0; this is not an export failure.
Check the final `COMMON_MONSTERS_COMPLETE 7` / `COMMON_REIMPORT_PASS` markers as well as
the process exit code because Blender may return zero for a Python assertion failure.

## Asset and animation contract

- Assets: `res://assets/common-monsters/{eagle,parrot,lion,tiger,reindeer,rhino,zombie}.glb`.
- Resources: `res://content/enemies/common_<species>.tres`, with matching `Id`.
  The runtime catalog's human species maps to `common_zombie`.
- Meters, soles at Y=0, Y-up and +Z-forward in GLB. Blender works in Z-up/-Y-forward.
- `Root → Body → Deform`; `Head`, feet, weapon and arms descend from `Deform`.
  `Head` pivots at the neck; `Body`/`Deform` pivot at the floor. No armature.
- Required names: `Root`, `Body`, `Deform`, `Head`, `LeftFoot`, `RightFoot`, `Weapon`, `Muzzle`.
- Rest arms: `common_<species>_leftArm` and `common_<species>_rightArm`.
  Attack animation reuses these suffixes; weapon attachment hides the exact Id arm nodes
  before creating its procedural grip arms. Never add a second visible arm over these.
- Animals have `Gear_Vest`, `Gear_Glasses`; no helmet exists.
- Human has `Outfit_Jeans`, `Outfit_Chinos`, `Outfit_Torn`, `Outfit_Shirt`,
  `Outfit_Overalls`, `Outfit_Suit`, `Outfit_Stains`, `Outfit_Pattern`, `Gear_Eyepatch`.
- Optional nodes are ancestor containers; their mesh is named `<module>_mesh`.
  Hide/select the ancestor once, rather than independently selecting every prefixed mesh.
  All modules are present in the exported file; runtime must select visibility before display.
- Pick at most one of Jeans/Chinos/Torn/Overalls/Suit. Overalls and Suit include their own
  trousers. Shirt can accompany Jeans/Chinos/Torn or no lower garment. Stains and Pattern
  are cloth-surface details intended to accompany Shirt, never skin tattoos.
- GLB does not embed a weapon. `CharacterRig` attaches the shared weapon to `Weapon`.

## Material channels

| Name prefix | Meaning |
|---|---|
| `common_primary` | Animal fur/feathers and limbs |
| `common_secondary` | Belly and secondary fur |
| `common_accent` | Parrot feathers or cloth accents/pattern |
| `common_skin` | Human infected skin; independent of faction palette |
| `common_cloth` | Shirt/jacket |
| `common_pants` | Trousers, overall bib and suit trim |
| `common_gear` | Optional dark vest |
| `fixed_*` | Species identity: eyes, beak, lion mane, antlers/horns, tiger markings, thread, stains |

Runtime appearance must avoid mutating shared materials. Its own palette and preset data
select the ten zombie looks. Rig hand color must follow the chosen animal primary or human
skin because procedural grip meshes use `HandColor` rather than GLB material channels.

## Inspection scope

Final Blender generation and independent GLB reimport passed **309 assertions / 7 assets**.
All 40 full-body images were generated; both contact sheets were visually inspected.

| Species | Base triangles | With vest + glasses / stored total |
|---|---:|---:|
| Eagle | 6,220 | 7,546 |
| Parrot | 5,752 | 7,078 |
| Lion | 6,628 | 7,954 |
| Tiger | 6,512 | 7,838 |
| Reindeer | 5,808 | 7,134 |
| Rhino | 6,084 | 7,410 |
| Human zombie | 4,944 | 14,399 stored across all mutually exclusive garments |

Representative human visible counts: jeans+shirt+stains 7,184; chinos+shirt+pattern 7,624;
torn+shirt+stains 7,564; overalls 6,738; suit 6,801; shirt without trousers 5,792.
This is close to the crowd art target, with selected detailed outfits and armed animals
above 7k; there is no separate low-poly LOD asset in this delivery.

The independent reimport tool validates actual exported meshes, pivots, parents, material
nodes, resource paths, floor bounds, triangle totals, optional modules and no skeleton/helmet.
`output/contact-sheet.png` contains all seven full bodies from front/quarter/side/back.
`output/variants-sheet.png` shows six animals with both gear options and six representative
human garment combinations; it is not the runtime ten-preset contact sheet.

The first art review corrected a gap between animal bodies/feet, belly intersections at
the vest edge, incomplete rear tiger markings and a missing rear lion mane. Final image
review covers static rest-pose fit; game animation, weapons, status/corpses, palette presets
and Studio renders need their separate integration checks. No low-end FPS or real two-PC
claim is made by this asset audit. Hidden optional source geometry is not visible triangle cost.


## Parrot quality benchmark (2026-10-25)

Use `-- --species parrot` on both build and validation commands to modify/check only
this species; `audit-parrot.json` is separate from the complete `audit.json`. An explicit
selection of all seven species uses the complete audit name in both commands. Other
GLBs are hash-checked and not rewritten by a selective build.

Parrot now has a continuous head/torso profile, a closed torso without a belly panel,
small dark eyes without rings/glint beads, pointed crest/wings and thin planar bird feet.
The fitted vest has beveled rectangular pouches. Base 5,436 triangles; with vest 6,684.
Existing pivots/palette contracts remain. The parrot resource emits `HeadSpringScale = 0.05`
to avoid exposing the rigid neck joint during the shared head spring animation.
Equipped parrots use `CharacterAuthoredWing` to preserve the original wing mesh and
palette surfaces. Eagles use the same path; lions use separate rigid paws and forearms (see below). Other common species still use compact procedural grip arms;
player authored surface deformation is unchanged.

`review_parrot.py -- --capture-baseline --label before` preserves a local old GLB
(refuses overwrite); `-- --label before` / `-- --label after` render the same camera,
light, silhouette/neutral/color/vest/gameplay-scale views. `parrot_contact_sheet.py`
assembles the local comparison. [Baseline recovery, results and limitations](../../doc/parrot-quality.md).

Use `--review-name parrot-clean-forms` for the current iteration's separate baseline and views.
Run `ParrotMotionCheck` for acceleration/hit head response and `ParrotGripCheck` for six weapon grips.
[Current model and motion evidence](../../doc/parrot-clean-forms.md). Repeated GLB exports can differ in
UV float rounding/inactive triangle order; compare geometry semantics as well as file hashes.

## Eagle quality follow-up (2026-10-25)

Build/validate with `-- --species eagle`; the current eagle has clean body/neck forms, small dark low-set eyes,
gold hooked beak, thin overlapping neck feathers, tapered flight vanes, planar taloned feet and structured vest. Base 7,148 triangles;
with vest 8,572. Other six GLBs are unchanged from this revision's baseline. `review_parrot.py` also accepts
`-- --species eagle` (capture baseline before building), and `parrot_contact_sheet.py --species eagle`
assembles its before/after views. Omitting species retains the parrot QA path.
Game `ParrotGripCheck -- --eagle` checks the same authored-wing contract; actual terrain review
uses `HunterCombatCheck -- --eagle-stage2`, now including approach/walk frames. Use `--review-name eagle-clean-forms`
for the new comparison evidence. The existing head spring remains at scale1 after actual offset/tilt stress review.
[Current results and limitations](../../doc/eagle-clean-forms.md), [historical results](../../doc/eagle-quality.md).

## Lion quality follow-up (2026-10-25)

Build/validate with `-- --species lion`. The current lion uses a swept tapered mane, small dark eyes,
unified fixed-cream muzzle, clean torso and structured vest. Secondary palette color now belongs to the tail tuft, not a palm patch.
The limb follow-up separates compact rigid paws from the forearms,
adds authored Wrist/Grip anchors, planar soles and tapered thighs/ankles.
Current base6,464 triangles; vest included7,648. `CharacterLionPaw` preserves paw proportions
while connecting the forearm shoulder to wrist. No new finger skeleton or terrain foot IK.
Both QA scripts accept species lion, retaining default parrot behavior and eagle support.
Run `LionLimbCheck` (also routed from `ParrotGripCheck -- --lion`) and `HunterCombatCheck -- --lion-stage2` for runtime checks.
Studio attack QA accepts `STUDIO_SPECIES=lion`; default remains eagle and output folders are distinct.
Other six current GLBs remain unchanged. Review scripts accept `--review-name lion-clean-forms` for current style evidence,
preserving historical lion-limbs images. Existing head spring scale1 is retained after actual tilt/offset review.
[Current style and validation](../../doc/lion-clean-forms.md), [limb contract history](../../doc/lion-limbs.md).

## Reindeer / human infected clean forms (2026-10-25)

Use `build_common_monsters.py -- --species reindeer zombie` followed by the same selection in `validate_common_monsters.py`; add Blender `--python-exit-code 1` for reliable failure exit codes. Selected generation preserves the other species GLBs. `review_parrot.py` and `parrot_contact_sheet.py` also accept `--species reindeer` or `zombie`; zombie before/after uses the same Jeans+Shirt+Stains outfit.

Lion and reindeer now preserve authored rigid hands and separate forearms via CharacterLionPaw. Each `common_<species>_<side>Paw` contains `Paw_mesh`, `Wrist`, `Grip`. Runtime hides the rest arms before creating the equipped arm; it must not add capsule hands over them. Reindeer Head-local brow attachment is in common_brows.json, not baked in GLB. Human infected remains unarmed with its ten existing outfit presets. [Coordinates, checks and remaining limits](../../doc/reindeer-zombie-clean-forms.md).
