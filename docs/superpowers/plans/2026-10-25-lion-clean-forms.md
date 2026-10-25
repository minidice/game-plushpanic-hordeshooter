# Lion Clean Forms Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for scoped Blender implementation and independent review. Parent owns game/Studio integration and records.

**Goal:** Apply the approved clean stylized animal direction to lion while preserving rigid weapon-grip paws and grounded limb connections.
**Architecture:** Revise lion-specific geometry/materials in the existing selective Blender generator, sharing the existing game and Studio asset path. Preserve current parrot/eagle work and CharacterLionPaw contracts.
**Tech Stack:** Blender5.2, Godot4.7.2 C#, existing Studio renderer.
**Spec:** doc/character-art-direction.md; doc/lion-limbs.md (existing runtime contract); doc/lion-quality.md (historical species identity); eagle-clean-forms.md and parrot-clean-forms.md (current style reference).

## Global Constraints
- Preserve all pending parrot/eagle/document work; reuse current feature checkout for live Studio path. No commit/push requested.
- Only lion GLB changes this turn. Other6GLB hashes unchanged. No gameplay balance, collisions, palette IDs, helmets or eye gear changes.
- Keep named pivots, separate rigid Paw plus Wrist/Grip anchors and forearm; no regression to whole-hand stretching. Existing flat sole and ankle connection remain.
- Base triangle budget below8000. No skin-rig overhaul, new finger IK or slope IK. Preserve materials needed by palettes/status/hide handling.
- New evidence folders lion-clean-forms; preserve historical lion-quality and lion-limbs.
- User delegated next recommended species in the approved style; no need to reopen design approval.

## Review Focus
- Mane must read as directional broad tapered masses, not padded rings or detached beads; inspect silhouette, neutral and moving head extrema.
- Small dark eyes and unified muzzle/face must retain lion recognition without glued cheeks or compulsory smile; inspect game scale.
- Smaller flatter paws/feet must preserve grip anchors and enough foot ground area; verify actual6weapon and movement states.
- Vest/pouches must wrap body with structured thickness and no clipping against mane/arms; compare side/armed views.
- Head offsets up to.14m/tilt.224rad, hunter poses, reload, scale0.8/1/1.2, hit/status/hide must preserve connections. Keep HeadSpringScale1 unless observed problem justifies a tested targeted change.

## Task1: Model
- [x] Capture lion baseline and7hashes, fixed-camera before11views.
- [x] Revise lion-only builder helpers/geometry/materials, selective export; preserve other6. Modify validation only for meaningful changed geometry contracts.
- [x] Reimport validate, inspect after11views and runtime-equivalent head stress; record hash/triangle cost/limitations and make comparison sheet.

## Task2: Integration and records
- [x] Import/build; LionLimbCheck, HunterPoseCheck, CommonRosterCheck, ParrotMotionCheck, CommonRosterNetCheck. Actual Stage2 movement/combat8renders via HunterCombatCheck -- --lion-stage2.
- [x] Studio6weapon views, hunter3attack clips/audio API and green/red/ochre palette evidence; browser final verification.
- [x] Independent model/source/integration review and needed fixes. Update A25/C31, issues, art checklist, roster/spec records and lion-clean-forms.md with real results and limits.

## Rulings
- This is an aesthetic model revision: use meaningful existing mesh/runtime checks and visual comparison, not tests hard-coding chosen artistic construction.
- Prior pending changes are baseline; use copied source snapshots for this turn's review delta. Do not reset or regenerate completed bird assets.
