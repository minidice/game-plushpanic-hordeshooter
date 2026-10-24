# Combat effects implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline implementation with a fresh final review.

**Goal:** Improve explosion, fire and poison effects and expose their shared runtime data in Studio.
**Architecture:** Validated JSON catalog, three visual-only effect renderers, existing gameplay emitters and Studio consuming the same factories.
**Tech Stack:** Godot 4.7.2 .NET, C#, spatial shaders, Node Studio (no new packages).
**Spec:** `doc/vfx-quality.md` (approved recommendation in conversation).

## Global Constraints

- Preserve combat damage, collision, network authority and existing non-combat effects.
- Main workspace retained per established user preference. No commit/push requested this turn.
- Keep effect age explicit for Studio fixed stepping. Runtime hazard radius/lifetime supplied by gameplay.
- Update decisions/issues and actual render verification before completion.

## Review Focus

- Bad/duplicate data or binding silently falling back to a wrong visual.
- Repeated explosions leaving nodes/lights or changing combat RNG.
- Fire/poison outliving damage or visually extending the danger radius.
- Studio truncating a long effect, freezing shader time, or mutating gameplay/settings.
- Client emitters losing their effect identity, transparent overdraw hiding enemies.

### Task 1: Catalog and visual factories

Files: new `CombatEffectCatalog.cs`, `CombatBurstEffect.cs`, `CombatSurfaceEffect.cs`, `CombatProjectileEffect.cs` under `game/scripts/Effects`; JSON, volume/surface shaders; `game/tests/CombatEffectsCheck` scene.
Interfaces: `CombatEffectCatalog.Current.ForSource(string)` and `.All`; factories accept parent, definition, position, radius and optional lifetime/direction. `_Process` advances purely visual time.
- [x] Write/run missing-data check (expected FAIL), then strict catalog and variant definitions.
- [x] Add malformed-data and independent lifetime/radius assertions; implement shared visual parts and pass them.

### Task 2: Runtime and Studio

Files: Blast, barrel/object detonation, weapon explosion calls, FireZone, EnemyProjectile, VomitHazard, StudioEffects.
Interfaces: Blast optional effect source and direction; runtime profiles resolved from catalog; Studio auto enumeration and same factory invocation.
- [x] Add emitter/profile and Studio catalog assertions before connecting; observe failure.
- [x] Connect actual emitters without changing combat rules; Studio profiles expose source, parameters and sample conditions.
- [x] Capture whole lifetimes with bounded frame counts, add frame/pixel/unknown-ID tests and run them.

### Task 3: Verification and documentation

- [x] Run Debug/import, dedicated headless/window scene, Integration/Destruction, Studio media plus new profile checks.
- [x] Inspect actual frame sequences; measure overlapping effects on current GPU without fixed FPS.
- [x] Fresh reviewer checks complete diff; fix important findings and repeat affected checks.
- [x] Update decisions/issues/spec/checklist with facts, warnings and remaining constraints. Leave changes reviewable, uncommitted.

## Execution ledger — 2026-10-24

- Completed all three tasks in the shared main checkout. Missing-data/Studio registration checks failed before implementation; opacity assertion failed before the custom shader fix. No gameplay contract changed.
- Final validation: Debug + ExportRelease, import, dedicated78/window90 (12 captures), Integration149, Destruction42, EnemyExpansion60, Studio68 + media342. ExportRelease contains no Studio compile items. Actual Studio browser preview, source disclosure and timeline inspected.
- Independent reviewer inspected implementation and final opacity/schema delta, no important actionable findings. Reviewer flagged stale Studio captures; restarted renderer and regenerated every recipe after final shaders.
- Author visual review found opaque smoke despite GeometryInstance transparency; instance alpha fixed I-121. Final overlap render keeps actor shapes visible. RTX5070Ti1600x900 uncapped render sample1.86→2.81ms mean,2.38→3.30ms p95; not low-end/network/long-session certification.
- I-17 remains: accelerated headless audio exit references identified; final window/performance/EnemyExpansion no exit warnings. Updated decisions/issues/checklist and weapon/throwable/Studio specs; full evidence and limits in doc/vfx-quality.md.
- Initial implementation was left uncommitted; the user subsequently requested committing, integrating latest main and pushing. Fresh pre-commit Debug/effects78/Integration149 passed (Integration audio exit warning4, I-17). Own verification Studio at localhost:4321 remains running with the delivered browser preview. User attachment directory preserved.
