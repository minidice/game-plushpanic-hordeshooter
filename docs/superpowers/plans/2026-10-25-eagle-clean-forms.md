# Eagle Clean Forms Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for scoped Blender implementation and independent review. Parent owns runtime integration and documentation.

**Goal:** Apply the approved clean stylized animal direction to eagle after parrot, preserving eagle recognition and combat contracts.
**Architecture:** Selectively revise eagle geometry/materials in the existing Blender generator. Reuse game and Studio loading paths and authored-wing grips. Preserve pending parrot work.
**Tech Stack:** Blender 5.2, Godot 4.7.2 C#, existing Studio renderer.
**Spec:** doc/character-art-direction.md, doc/eagle-quality.md (historical contracts), doc/parrot-clean-forms.md (current style reference).

## Global Constraints
- Existing pending parrot/doc changes must remain. Reuse the current feature checkout so Studio and uncommitted models stay together; no main edit, commit or push.
- Only eagle GLB may change this turn; other6 hashes unchanged. No helmets/eye gear, gameplay balance, collision or palette ID changes.
- Keep named pivots, authored-wing interface, silhouette recognition and base triangle budget below8000.
- Evidence goes in new eagle-clean-forms output folders; preserve earlier eagle-quality evidence.
- User delegated next recommended work in the approved style; do not reopen style or execution approval.

## Review Focus
- Neck feathers must read as thin tapered overlapping feathers, not a padded ring; inspect neutral side/quarter views and head spring extrema.
- Retain lowered eagle gaze and hooked gold beak without large eye beads or detached brows; inspect actual play scale.
- Thin grounded taloned feet, narrow ankles and clean body silhouette; inspect soles and walk frames.
- Original wings must grip all6weapons and remain coherent through hunter3attacks/status/hide; use existing grip and pose tests.
- Vest/pouches must wrap the body and preserve material contrast; inspect side and armed renders.

## Task1: Eagle model and validation
- [x] Capture current eagle baseline and7hashes; fixed-camera before renders, preserving old evidence.
- [x] Revise eagle branch in build_common_monsters.py; selective Blender export. Update validate_common_monsters.py only for meaningful new geometry contracts. Avoid modifying shared helpers in ways changing parrot/other models.
- [x] Validate reimport, inspect same-camera11before/after views and head-spring stress render; record actual triangle counts/size/hashes/limits. Parent handles .tres if observed head motion requires change.

## Task2: Integration and records
- [x] Build/import final asset, run eagle grip, hunter poses, common roster, parrot motion regression and localhost ENet checks. Extend HunterCombatCheck's existing movement review to eagle; execute actual Stage2 movement/combat/render checks.
- [x] Studio reload,6weapon views and hunter3attack frames, red/green palettes; verify browser result.
- [x] Independent scoped model and overall integration review. Fix observed issues; record decisions/issues/checklist/spec, commands/evidence/limits in eagle-clean-forms.md.

## Rulings
- Model-only aesthetic change uses actual render comparisons and existing meaningful asset/runtime tests; no test merely asserting the chosen aesthetic construction.
- HeadSpringScale stays at1 unless real motion or stress renders show a visible discontinuity. If changed, preserve defaults for other species and test the intended new bound and generator persistence.
