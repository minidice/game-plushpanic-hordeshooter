# Mammal weapon arm implementation plan

> For agentic workers: use superpowers:executing-plans inline. User approved the recommended tiger+M4 → MP5/AK47 → other mammals improvement. Existing isolated worktree is reused on codex/mammal-weapon-arms; original main is untouched.

**Goal:** Replace straight stretched armed mammal limbs with compact shoulder/elbow/wrist forms, preserving authored paws, weapon grip/muzzle contracts, customization and effects.
**Architecture:** Measure actual reach first. Use Blender-authored closed upper/lower arm surfaces and a deterministic two-segment elbow solver in the existing CharacterLionPaw path. Match species thickness/material from its authored arm. Keep old unarmed model and other species paths. Preserve player one-handed policy. If weapon hold requires excess reach, adjust only the appropriate visual carry placement with explicit muzzle/regression verification; do not alter combat stats.
**Tech stack:** Blender5.2, Godot4.7.2 C#, existing Studio rendering/tests.

## Scope and design choices

Existing runtime stretches a roughly0.2–0.3m arm surface to the entire shoulder–wrist distance. A larger hand or smoother mesh cannot create an elbow. Full-body skeleton replacement would unnecessarily disturb customization and existing poses. The selected bounded solution adds two connected arm sections while the existing rigid paw continues to contact the real grip. The precise lengths/pole/thickness are implementation art tuning, not user-approved balance values.

## Tasks

- [x] Capture current tiger rifle pose and measure shoulder/wrist/grip reach across six mammals and M4/MP5/AK47; add failing geometric checks for an actual elbow and bounded sections.
- [x] Build reusable closed tapered upper/lower arm meshes in Blender, export GLB and render/validate it. Implement deterministic elbow/hand placement in CharacterLionPaw; keep shared template geometry immutable and original per-species color.
- [x] Verify tiger rifles in Studio, revise the shape/pose as needed; extend checks to lion/reindeer/rhino/redpanda/raccoon, hunter/other weapons, reload/traversal and scale extremes. Preserve grip, reset, hidden/silhouette/hit/status surfaces and player one-hand policy.
- [x] Capture actual Stage2 and Studio rendered poses; run relevant existing regressions and build. Document any remaining unreachable/extreme pose limitations honestly.
- [x] Read-only review and fixes; update decisions/issues, feature checklist and detailed contract. No main merge/push unless requested anew.

## Files

- prototypes/grounded-art/tools/build_mammal_arm_segments.py: Blender source/export and validation.
- game/assets/characters/shared/mammal-arm-segments.glb: runtime reusable geometry.
- game/scripts/Characters/CharacterLionPaw.cs: rigid paw with two articulated arm surfaces.
- game/scripts/Characters/CharacterWeaponGrip.cs and effects only where the extra surface must join existing contracts.
- game/tests/MammalArmCheck.cs/.tscn and existing limb tests: geometric contacts and rendering.
- studio/tests/mammal-arms.mjs: six-species weapon/pose previews.
- doc/mammal-weapon-arms.md plus decisions/issues/checklist: evidence and limits.

## Completion record

Implemented .36m nominal sections, -0.10m two-hand mammal firearm carry; source rest arm retained. First -.20m trial was rejected for vest proximity. Geometry1044, Stage2 rifle539, limb4536/4536/4536/4272, player windowed7620 each, hunter42, Studio51+50 and Integration149 passed. Fresh read-only code/render review had no blocker after camera Size restoration and RestArm effect coverage. Details/limits: doc/mammal-weapon-arms.md. Work remains uncommitted on codex/mammal-weapon-arms; main integration was not requested in this turn.
