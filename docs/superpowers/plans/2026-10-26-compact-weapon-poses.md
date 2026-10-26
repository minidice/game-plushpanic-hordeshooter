# Compact weapon pose implementation plan

> Execute inline with superpowers:executing-plans. User approved the recorded arm-proportion improvement; no additional approval gate. Reuse the isolated four-species-art worktree, branch codex/compact-weapon-poses from93802c8. Do not alter original main or its Studio server.

**Goal:** Keep the short original animal-arm silhouette when holding a firearm; preserve shared poses, actual hand/stock contacts and customization.
**Architecture:** Measure authored shoulder/wrist/grip geometry. Use a bounded arm chain and a compact carry fit from anatomy/weapon contacts instead of silently extending arms. Adjust the weapon/support grip and shoulder placement together where necessary. Keep common data/calculation, no species-specific pose copies. Existing dedicated hunter/heavy/traversal paths get regression checks.
**Spec:** doc/shared-weapon-poses.md user feedback and approved follow-up.

- [x] Diagnose and RED: compare actual loaded rest arm and held section lengths across six mammals, weapon classes and wing sizes. Capture raccoon M4 before under same Studio lighting.
- [x] Implement original-proportion arm chain and shared reach-compatible weapon placement. Validate actual surfaces, finite transforms, exact grip, stable idle/move/fire/reload/reset and pre-tree companion construction. Avoid solving contact by detached hands or arbitrary diagnostic markers.
- [x] Same-camera Studio before/after and custom-size matrix; actual Stage2 player/AI firing and dedicated hunter regressions. Correct visual defects before numeric acceptance.
- [x] Independent read-only review, relevant integration/build/asset checks; update A-4, issues, model spec/checklist and exact evidence/limitations. Restore private project name and stop owned test server. No commit/push requested in this turn.

Evidence: doc/shared-weapon-poses.md follow-up section; game/output/compact-poses/ and studio/output/compact-poses/. Fresh read-only review found scale/overlay risks; measured and fixed, final review no blocking findings.
