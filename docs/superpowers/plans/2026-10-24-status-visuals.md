# Status visuals implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, then a fresh whole-change reviewer.

**Goal:** Shared readable character/monster status visuals, existing-state integration and Studio combination previews.
**Architecture:** Strict visual catalog → bounded rig-local composer → live-state reader or Studio sample. No new combat statuses/damage rules. Original mesh material slots remain owned by existing gameplay visuals.
**Tech Stack:** Godot C#, shaders/SVG icon textures, existing local Node Studio.
**Spec:** `doc/status-visuals.md`; user approved recommendation in conversation.

## Global constraints

- Keep the established shared main checkout preference; no new worktree, commit or push this turn.
- Game owns state lifetime; live visual adapter only reads it. No preview-only status may affect gameplay.
- Preserve customization, hit flash and reveal/silhouette. Never expose hidden enemies through status effects.
- Update decisions/issues/checklist and Studio/spec; actual render is required.

## Review focus

- Shared material contamination, status layer captured by hit/reveal logic, stale overlay after clearing.
- Hidden/dead/removed rigs leaving particles or revealing positions.
- Many stacks or simultaneous states spawning unlimited nodes or lights.
- Preview-only statuses silently presented as live gameplay, client limitations hidden.
- Studio failed/invalid requests corrupting camera/subject or source files.

### Task 1: Common rig visuals

Files: `Effects/StatusVisualCatalog.cs`, `StatusVisualController.cs`, `StatusVisualParts.cs`, `StatusVisualIcons.cs`, `content/data/status_visuals.json`, `shaders/status_surface.gdshader`, `tests/StatusVisualCheck.cs/.tscn`.
Interface: `StatusVisualController.Attach(CharacterRig)`, `SetStates(IEnumerable<StatusVisualState>)`, `Active`, `Clear()`, explicit `_Process` animation. State is ID/remaining seconds/stacks; catalog is visual-only.
- [x] Missing catalog check RED. Implement validated12 profiles and shape icons.
- [x] Composition, duplicate merge, finite limits, no mutation/restoration/visibility tests. Implement independent surface and bounded parts; pass.

### Task 2: Actual states and Studio

Files: live binding helper; minimal Enemy/Player initialization; remove replaced legacy burn/weak decoration only. Hud status partial. Studio status partial, dedicated page/js and server routes/nav.
- [x] Test actual burn/stun/blind/weak lifecycle; connect existing states without modifying their rules.
- [x] Studio catalog/API validation, then player/monster picker, multiple statuses/stacks, hit/release sample, raw shared records, timeline.
- [x] Verify invalid input, unchanged files, final cleanup/restored subject.

### Task 3: Render, regressions, records

- [x] Build/import, dedicated headless/window tests and single/stack/clear renders; overlapping actor render cost sample.
- [x] HitFlash/Vision/EnemyExpansion/Integration and relevant Studio checks; record warnings separately.
- [x] Fresh independent review, fix important findings and re-run affected checks.
- [x] Final docs and execution ledger; leave changes uncommitted.

## Execution ledger

- Ruling: user approved the concrete conversational recommendation; implementation proceeds without a repeated approval menu. Main workspace and inline method retained from preceding work.
- Ruling: prototype statuses absent in combat are renderer inputs only. Do not invent gameplay behavior or network replication to support a preview.
- Task1 → Task2 interface: same catalog/state/controller for live and Studio; no duplicated web VFX.

- Task 1 complete: missing catalog RED→GREEN; strict profiles, bounded composition and original material restoration. Final StatusVisualCheck headless57/window77, crowd80 assertions.
- Task 2 complete: missing actual enemy binding RED→GREEN; Studio37, catalog35, smoke36, library200. Browser reload/multi-select/stack/speed/loop/scrub verified.
- Task 3 complete: Debug and ExportRelease builds clean; HitFlash, VisionTier23, EnemyExpansion60, Integration149. Status window renders, 80-actor baseline5.93ms/active12.67ms sample; low-end/long-duration/2PC unverified. Existing headless audio exit warnings verified by verbose log.
- Final review: /root/review_status_visuals (fresh gpt-6-astra, high), read-only whole working diff/new files. Critical0, Important2, Minor0.
- Final: fixed badge layout rotates with actor — four headings RED→GREEN; final dedicated and regression suites above pass.
- Final: fixed bare Studio monster omitted equipment — armed target actual RangedEnemy/weapon test RED→GREEN; Studio37 and library200 pass.
- Final: Ruling: new gameplay rules/network replication remain excluded as approved — preview labels and source bindings avoid claiming missing behavior; cost is a separate implementation before the five prototype statuses can be used in combat.
- Final: Ruling: 2PC/low-end/long-duration results remain unverified — local bounded render sample is reported only as a sample; cost is additional validation before broad performance/network guarantees.
- Final documentation check: decisions A-19/T-25, issues I-122, feature-checklist, status-visuals and studio-prototype updated together. No minor findings deferred. Manual ledger retains this uncommitted Windows working-tree evidence; no commit range is claimed.
