# Corpse reactions implementation plan

> Execute inline with `superpowers:executing-plans`. The user approved the discussed behavior and requested implementation.

**Goal:** Walking, rolling and explosions move common corpses; named corpses remain fixed.
**Architecture:** Keep cosmetic bodies out of gameplay collision masks. Player contact applies one-way impulses; the shared Blast path affects common bodies after lethal damage has created them. Use native rigid-body sleeping rather than permanent freezing. Fixed named visuals retain death yaw and metadata for future host replication.
**Tech Stack:** Godot 4.7.2 C#, Jolt.
**Spec:** `doc/corpse-retention.md`, plus the approved conversation.

## Constraints

- No multiplayer transport exists: do not claim peer synchronization is implemented.
- Preserve common cleanup and named 300-second lifetime; no player/prop/projectile obstruction.
- Preserve earlier uncommitted model changes and unrelated `.claude/`.
- The optional sound/gag suggestions are not part of the agreed physical response work.

## Tasks

- [x] Add `game/tests/CorpseReactionCheck.cs` and scene: real walk/roll contact, late wake, explosion falloff/cover/range/fresh deaths, fixed named body. Run against current code and observe failures.
- [x] Update `Corpse.cs`, `Effects.cs`, new `EffectsCorpsePhysics.cs`, `Player.cs`, `Blast.cs`, `NamedCorpseRecord.cs`. Reuse the existing retention list without changing cleanup. Verify movement cannot feed back into gameplay.
- [x] Update obsolete named-motion assertions in `CorpseTierCheck.cs`; run reaction/tier/retention, explosion-related tests and Integration.
- [x] Render and inspect the actual reactions. Update decisions/issues/spec/checklist/multiplayer plan with evidence and limits.

## Review focus

- Sleeping corpses must wake after more than five seconds.
- Newly killed corpses must receive the same blast; named bodies never launch.
- Cover and explosion source props must not create false hits or self-occlusion.
- Walk/roll impulses must not accelerate stationary contacts indefinitely or push through walls.
- Native sleeping can reduce physics cost but the previous five-second performance measurements no longer certify this policy.

## Execution record

- Existing working tree is reused because this is continuation of the model/collider work; no branch integration or commit is requested.
- Baseline: 8 of 15 new behavior checks failed. Grounding test then reproduced airborne freezing; all 6 model bounds failed before support correction. A separate material restoration test reproduced stale living silhouettes.
- Final reaction render: 26 checks passed. Native sleeping/re-waking, all 6 named mesh bounds and before/after explosion screenshots verified. Regression counts and exit warnings are recorded in `doc/issues.md` I-69.
- Ruling: retain existing living-enemy blast damage/death impulse behavior. Only the additional cosmetic corpse impulse uses the new cover ray; changing combat cover rules would expand this task.
- Independent final review found the named nose/mesh penetration; fixed using face-up pose and transformed mesh bounds. Follow-up review found no remaining concrete correctness concern.
