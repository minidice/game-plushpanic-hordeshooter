# Eagle Quality Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement and review the scoped model work.

**Goal:** Improve eagle alone, keeping the plush style and validating Blender, Studio and actual Stage 2 rendering.
**Architecture:** Extend the existing species-selective Blender generator; reuse authored-wing runtime geometry for eagle and preserve palettes/pivots/combat data. Reuse existing QA paths with species selection.
**Tech Stack:** Blender 5.2 Python, Godot 4.7.2 C#, Studio Node.
**Spec:** User-approved eagle follow-up; doc/parrot-quality.md and doc/common-monster-roster.md contracts.

## Constraints and review focus
- Other six GLBs remain byte-identical. No combat numbers, collision, palette or identity changes.
- Eagle has compact eyes/brows, hooked beak, coherent neck ruff and body, broader layered wings and fitted vest; retain plush softness.
- Held wings retain source mesh/material and remain connected without shoulder protrusion across six weapons. Eye gear stays disabled.
- Check silhouettes, neutral/color, vest and actual gameplay scale. Actual automated play is not manual full-map completion.
- Work on codex/eagle-quality in current checkout to retain the user's running local Studio/game context; no commit/push requested this turn.

## Tasks
- [x] Model: capture baseline/hashes; add eagle-specific geometry in build_common_monsters.py, export only eagle.glb, validate and inspect same-camera before/after renders. Reuse QA tools with --species eagle, preserving default parrot behavior.
- [x] Runtime: extend ParrotGripCheck to run eagle via argument and confirm authored-wing assertion fails, then enable eagle in CharacterWeaponGrip; keep parrot regression. Add --eagle-stage2 to HunterCombatCheck for actual terrain review.
- [x] Integration: import/build, Studio six weapons and hunter attack frames, actual Stage 2 combat/renders. Fix observed geometry/pose issues and perform independent review.
- [x] Records: update decisions/issues/checklist/roster and eagle quality evidence, failures and limitations; retain reproducible commands and ignored outputs.
