# Common monster roster implementation plan

> **For agentic workers:** Use subagent-driven-development with bounded independent art/runtime/Studio ownership, then fresh read-only reviews. Continue without repeated approval; user approved the conversational design.

**Goal:** Complete common-monster species/loadout/palette composition in game, Blender assets and Studio.
**Architecture:** Shared validated catalog + immutable birth identity → composed combat definition/rig appearance; stage pack profiles select coherent groups; spawn network carries identity; Studio uses same factories.
**Tech Stack:** Godot C#, Blender Python/GLB, JSON, existing Node/HTML Studio.
**Spec:** `doc/common-monster-roster.md`.

## Global constraints

- Existing shared main workspace retained per prior user preference; no commit/push requested. Preserve unrelated files and attachments.
- Six animal species eagle/parrot/lion/tiger/reindeer/rhino and one improved human zombie; no named/boss redesign.
- Species then weapon role then weapon differences; fixed baseline movement4.55–4.75, no player-speed rubber band.
- Hunters/armed/zombies isolated by default; one species/palette per pack; only explicit zone exceptions.
- Ten zombie appearance presets, no cosmetic stats; no helmets; palette variants remain in theme.
- Live game and Studio share identity/model/runtime helpers. No duplicate web models.

## Review focus

- Appearance and composed MonsterDefinition caching must not leak colors/stats across actors or change named/bosses.
- Network spawn and late join preserve composition and reject mismatched catalogs.
- Spawn cycles and party scaling must not mix factions/species/palettes within a pack or bypass explicit exception intent.
- Weapons, attack telegraphs, projectile/damage behavior and grips must match actual selected weapon without giving ordinary zombies skills.
- Imported anatomy/gear, status overlays, hidden actors, corpses and crowd cost must remain usable at game camera size.

## Tasks

- [x] 1. Blender assets: author reproducible generator, six species + refined existing humanoid, modular clothes/gear/material channels; GLB import and contact renders, actual mesh/triangle checks. Art owner only prototypes/common-monsters, game/assets/common-monsters, game/content/enemies/common_*.tres. Contract agreed before dispatch.
- [x] 2. Runtime/data: strict common roster catalog, immutable identity, appearance helper, combat composition/weapons/traits, Stage/HordeDirector pack routing, explicit encounter profiles, spawn replication/data hash. Focused failing checks first; dedicated actual play/render tests, existing regressions.
- [x] 3. Studio: roster tab/controls backed by common catalog and runtime factory, individual and pack renders, live properties/preset origins. API/browser tests and existing library/status regressions.
- [x] 4. Review/test integration, fix material findings, actual render/performance samples, decisions/issues/checklist/detailed specs update and final inventory. No commit/push.

## Execution ledger

Base: `542ef14`, shared `main`; initial working tree clean except user attachments. Task workers cannot commit or mutate branch/index.

| Interface / task | Producer → consumer | Inspection / ruling |
|---|---|---|
| 1→2 | common_<species>.tres,7GLBs, named semantic material prefixes and Outfit_/Gear_ modules → runtime appearance | Human catalog key maps common_zombie resource; authored arm names use common_<species>_leftArm/rightArm so grip fallback can hide them. No file ownership overlap. |
| 2→3 | CommonIdentity, CommonRoster catalog/Compose/LookFor/Apply → Studio actual Enemy factory | Studio waits for schema before compile. Independent web shell can proceed. All names/gear/faction restrictions shared from catalog. |
| 1→3 | Imported meshes → renderer | Studio render only after GLB import succeeds. No model copies in web. |
| 1 self | Generator → GLB + tRES + rendered/audited output | Generated outputs are intended game assets, local contact sheets/output excluded from Git. |
| 2 self | Data→gameplay/spawn/network and dedicated checks | Readonly cosmetic variants do not change stats; variant identity must precede client _Ready. |
| 3 self | Shared catalog→validated requests→real render | No damage/save calls in Studio; initialization processes disabled. |
| 4 self | Result→fresh review, regressions, docs | Parent owns independent CommonRosterPlayCheck and docs; others do not edit them. |

- Ruling: use six explicitly requested animal species; additional bird species remain extensible data rather than unbounded new model scope. Cost: additional art needed for later species.
- Ruling: follow approved same-appearance network recommendation using birth identity; no per-frame cosmetic replication. Cost: spawn payload increases modestly.
- Ruling: execute independent asset/runtime/Studio tasks through subagents as instructed by subagent-driven-development and dispatching-parallel-agents; shared main checkout remains user-established workspace. No branch/commit mutation by workers.


## Final integration review

Independent reviewer found P2 exact look fallback, ranged common Outpost Core exclusion, and client bow rifle sound. Runtime reproduced look/core failures, fixed strict resources/real ranged Core projectile/shared ShotSound; re-review found no further blocking runtime issue. Parent verified rendered grip stretching and corrected common-only compact grips (legacy fallback tessellation preserved). User's random cosmetics requirement now host shuffled bag; zombie palette restricted green/ochre. Old mode table and Integration assumptions migrated to current contract.

Parent independent PlayCheck18 actual Forward+ renders and Stage2 forest combat passed; common262/network62, Studio71, model309. All task details, warnings and limits recorded in doc/common-monster-roster.md. No commit/push. Final root Integration149 passed (4 ObjectDB exit warnings); Debug/ExportRelease builds passed0warnings/errors, final Studio71 rerun passed.


2026-10-25 follow-up: user explicitly requested commit, latest main merge and push. Current-tree builds/common262/network62/Integration149/Mode147/window16/Studio71+36+200+52+37 passed; warnings and unchanged remote baseline recorded in detailed spec. This authorization supersedes the implementation phase no-commit instruction.
