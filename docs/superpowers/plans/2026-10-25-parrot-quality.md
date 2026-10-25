# Parrot quality benchmark implementation plan

Use subagent-driven-development with bounded model/runtime ownership and a fresh final review. User approved the preceding recommendation; proceed without further design approval. Keep the shared workspace and preserve existing uncommitted hunter presentation work. No commit/push requested.

**Goal:** Improve one parrot species as an art benchmark, including authored wings while holding weapons, then verify in Blender, Studio and actual game rendering.
**Tech:** Blender Python/GLB, Godot C#, existing Studio.

## Approved design and implementation choices

- Parrot only: coherent plush head/neck/pear-shaped torso, integrated belly color instead of protruding bib, compact eye surround and highlight, reduced separate cheeks, recognizable hooked beak, swept feather crest and tail. Preserve friendly plush aesthetic, palette themes and species identity.
- Unarmed shape first, then club/knife/bow poses and rifle/vest. No helmets/eye gear. Preserve collision, gameplay numbers, pose timing, catalog identity and network data.
- Keep existing hierarchy/pivots and material channels. Blender exported parrot GLB replaces the current path. Other six GLBs stay byte-identical.
- Held parrot wings must derive from authored `common_parrot_leftArm_mesh` / `common_parrot_rightArm_mesh`, preserving shape and colors; avoid restoring hidden duplicate arms. Limit runtime change to parrot; player arms and other species remain as before.
- Use same lighting/view for before/after; silhouette, neutral material, final color; both close-up and gameplay scale. Check idle, walking, all hunter attacks, gun/reload, vest intersection and state visibility.

## Tasks

- [x] Capture baseline and implement parrot-only Blender source/build selection and repeatable comparison renders.
- [x] Implement authored parrot weapon wings and meaningful regression checks.
- [x] Import/export check; actual Blender/Studio/game rendering and attack integration checks.
- [x] Independent review, docs decisions/issues/checklist/source contracts and validation limitations.

## Ownership

Model agent: `prototypes/common-monsters/tools/build_common_monsters.py`, parrot GLB, supporting model QA tools only.
Runtime agent: parrot grip code/shader and new focused tests only; preserve prior hunter changes.
Primary: evidence capture, integration, Studio reload/tests, documentation, final review.

Final evidence and bounded review fixes: [doc/parrot-quality.md](../../../doc/parrot-quality.md).
