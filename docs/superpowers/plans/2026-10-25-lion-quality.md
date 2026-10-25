# Lion Quality Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the isolated model task and independent final review.

**Goal:** Establish the mammal quality benchmark with lion alone, integrated and verified in Blender, Studio and Stage 2.
**Architecture:** Add lion-specific authored geometry to the selective generator. Reuse the existing original-limb pose path for lion and parameterize existing QA checks. Preserve the pending eagle work.
**Tech Stack:** Blender 5.2 Python, Godot 4.7.2 C#, Studio Node.
**Spec:** User-approved next recommendation (lion after eagle); common-monster-roster.md and eagle-quality.md contracts.

## Constraints and review focus
- One species only: other six current GLBs, including improved eagle, remain byte-identical. Combat, colliders, palettes and IDs unchanged.
- Coherent plush face/neck/body; layered continuous mane instead of bead ring; compact eyes, readable muzzle/nose; broad soft paws and fitted vest. No helmet/eye gear.
- Preserve original paw shape/material in all six weapon poses without floating shoulders, overlong paws or belly/vest interference. Whole-limb posing is acceptable if actual renders read well; no new skeletal system.
- Compare before/after silhouette, neutral and color under same lights and actual game scale; document automated play versus manual full-map scope.
- Continue current codex/eagle-quality checkout, preserve prior dirty files. No commit/push this turn.

## Tasks
- [x] Model: capture baseline/hashes, improve lion in build_common_monsters.py, add lion selection to existing QA tools, export lion.glb only and inspect before/after renders and reimport audit.
- [x] Runtime: add lion option to grip test, demonstrate original-mesh failure, enable lion original limb path; retain eagle/parrot checks. Add lion Stage 2 argument and species option in Studio attack QA.
- [x] Integration: import/build, six weapon Studio views + three attacks, actual Stage2 attack/render review. Fix observed issues and obtain independent review.
- [x] Records: decisions/issues/checklist/roster/lion-quality evidence, results and remaining art/test limitations.
