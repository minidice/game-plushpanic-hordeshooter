# Parrot Clean Forms Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for scoped Blender implementation and independent review; parent owns game/Studio integration and records.

**Goal:** Apply the user-corrected smooth stylized animal direction to parrot first, with actual Blender, Studio and Stage2 evidence.
**Architecture:** Revise only parrot branch of the existing Blender generator and parrot.glb. Keep authored-wing runtime, named pivots/material palette contracts and combat behavior. Replace sewn/padded forms with continuous silhouette, small eyes, tapered crest/wings, thin grounded bird feet and structured equipment.
**Tech Stack:** Blender 5.2, Godot 4.7.2 C#, existing Studio renderer.
**Spec:** doc/character-art-direction.md; user approved starting with parrot after reviewing that correction.

## Global Constraints
- Keep existing pending art-direction documentation. Reuse checkout on codex/parrot-clean-forms to keep the live Studio path and pending spec together; do not edit main or discard changes.
- No commit/push requested. No new helmet/eye gear, rig overhaul, palette IDs, collision/combat changes or other species GLB changes.
- Baseline and final evidence use new output/parrot-clean-forms folders, not old parrot-quality.
- Base triangle budget <8000. Existing gameplay-scale readability and grip/status/visibility behavior remain required.

## Review Focus
- Body/head seam and belly color must not look sewn, clipped or floating in side/neutral views.
- Feet must have thin readable bird shape and floor contact, not padded oval slippers.
- Wing geometry must still align with six weapon grips without shoulder protrusion.
- Equipment must wrap body, keep readable thickness and avoid detached pouches.
- Default red and yellow/ochre group palettes must retain identity; do not change unrelated global palette/material behavior.

## Tasks
- [x] Preserve current parrot GLB + all7 hashes and fixed-camera before views. Capture Studio old club/bow/rifle.
- [x] Revise generator parrot geometry/materials and selective-export GLB in Blender. Update obsolete belly-panel validation to meaningful new surface/sole/palette checks as needed. Validate exported GLB, inspect same-camera before/after; other6 unchanged. Agent owns builder/validator/parrot.glb; parent owns remaining files.
- [x] Import/build; ParrotGripCheck and relevant regression tests, six Studio weapon views + hunter attack clips, actual Stage2 combat/window renders. Fix observed issues and inspect actual screenshots.
- [x] Independent source/model/runtime/render review; document actual metrics/tests/limits, update A-25/C-31/issues/checklist/spec and distinguish old evidence from new state.

## Rulings
- User already approved the corrected style and starting parrot; no additional design approval needed.
- This is model revision, not a new gameplay feature. Existing meaningful geometry/runtime checks are the gate; do not write style tests that merely hard-code construction steps.

- Final review found head-spring translation exposing the neck cap. Added visual HeadSpringScale (default1, parrot0.05), red/green regression, and generator persistence; combat values unchanged.
- Re-export is geometrically equivalent but not byte-stable: negligible UV rounding and unused glasses triangle ordering differ. See doc/parrot-clean-forms.md.
