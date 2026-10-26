# Raccoon clean forms implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the isolated Blender model task; parent integrates and verifies. No commit/push requested.

**Goal:** Continue the approved player audit with raccoon face/ear/hand/foot improvements, preserving customization and inspecting actual Studio/game renders.
**Architecture:** Independent Blender builder reads raccoon-fitted.glb, writes raccoon-clean.glb, preserves body coordinates and original pivots/materials. Reuse existing CharacterLionPaw and optional Shoulder anchors introduced for redpanda. No new animation system.
**Tech Stack:** Blender Python, Godot C#, Studio Godot renderer.
**Spec:** doc/player-art-audit-2026-10-26.md, doc/player-species-guide.md, doc/character-model-spec.md; prior redpanda contracts in doc/redpanda-clean-forms.md.

## Global Constraints
- Existing appearance keys, palettes, mask shader coordinates, presets, ear/tail/face/size/gear customization preserved.
- Only raccoon model target; other species and raccoon-fitted source remain byte-identical.
- Existing short-wide muzzle, horizontal eye mask, gray coat/light belly, small round ears/ringed tail distinguish raccoon from redpanda. No plush fabric or round bead limbs.
- Gameplay collision0.48m/height2.06m, speed4.4m/s, one-handed M4 policy unchanged.
- Previous uncommitted work retained, no commits, no push, ignored outputs not tracked.

## Review Focus
- Default ears return to new authored mesh after alternate styles; pointed ears stay taller.
- Body mask must remain clean at custom palette/softness extremes.
- Rigid palms keep shape and actual forearm vertices remain between true shoulder and wrist under reload/size/movement.
- Gear, muzzle, face eye-spacing and size extremes must not lose source parts.
- Same GLB and shared rig in Studio/game; actual movement/fire and reload visual evidence rather than marker tests alone.

## Task 1: Blender model (model implementer)
- [x] Read current source and immutable redpanda builder/validator as patterns. Create build_raccoon_clean.py and validate_raccoon_clean.py in prototypes/grounded-art/tools; output game/assets/characters/raccoon/raccoon-clean.glb and ignored output/raccoon-clean/.
- [x] Sculpt a short wide muzzle with shallow eyes/brows; round cupped ears with connected inset (not floating disks); compact palms with thumb, tapered forearms, narrow ankles and flat soles. Keep torso/body mask vertices, source nodes, parent relationships, pivots, gear and material channels.
- [x] Arm_mesh is forearm only. Add raccoon_<side>Paw under Arm with Paw_mesh/Wrist/Grip and separate Arm child Shoulder at true surface top. Report exact Godot model-space anchors, preserving original Arm pivots.
- [x] Generate before/after neutral/silhouette/color/gear front/quarter/side renders and reusable .blend in ignored output. Validate closed geometry/outward faces, cupped ear connection/depth, actual forearm endpoint range, palm alignment, flat soles, source/other GLB hashes, baseline node/pivot/material contracts.
- [x] Report paths, counts, hashes, limitations. Model task owns only its builder/validator/new GLB and ignored outputs. No runtime/docs edits, no subagents.

## Task 2: integration / verification / docs (parent)
- [x] Add RaccoonCleanCheck derived from proven redpanda tests: species-specific anchors, missing rigid hands fails first, 7 weapons, customization/restore, surface/materials, vertex forearm bounds, actual Stage2 movement/fire/ladder. Avoid changing existing redpanda test behavior.
- [x] Connect raccoon.tres and add raccoon to rigid-hand condition, keeping optional true Shoulder behavior.
- [x] Studio raccoon test with controls, 21 actual render variants and default pixel restore; hashes of user save/presets unchanged.
- [x] Debug/ExportRelease + dedicated test and Surface/Tone/Modular/Reload/WeaponGrip and redpanda regression, actual reload captures. Inspect Blender/Studio/game renders and correct defects.
- [x] Update decisions/issues/checklist/audit/art direction/player guide/model spec/grounded README, detailed raccoon-clean-forms.md. Record failures, warnings and limits accurately.
- [x] Independent task/final review receives scoped diff/report and checks customization/model/runtime contract; fix important findings. Report succinctly with final image, no commit/push.
