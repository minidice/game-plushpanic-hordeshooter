# Shared weapon poses implementation plan

> Use superpowers:executing-plans inline. User approved shared pose/body/weapon separation; no additional design confirmation needed. Keep existing uncommitted arm work, same isolated worktree. No merge/push requested.

**Spec:** doc/shared-weapon-poses.md. Common player/monster shouldered firearms and two-hand pistols; model-specific contacts rather than species-specific pose branches.

- [x] RED: SharedWeaponPoseCheck captures actual stock surface/shoulder alignment and shared two-hand policy on ten characters, M4/MP5/AK47/pistol. Preserve prior tests; explicitly replace obsolete one-hand expectations.
- [x] Add StockContact to Blender firearm generator, selectively regenerate stocked guns; validate meshes/weapon data unaffected except new marker. Add fallback stock contact and common pose family to WeaponVisual.Built.
- [x] Implement CharacterWeaponPose common shoulder/body calibration, actual stock-first transform solve, pistol and movement transitions. Existing arm/wing renderers receive same endpoints. No per-species pose branching. WeaponGripCheck/SharedWeaponPoseCheck plus dedicated limb/customization/reload tests.
- [x] Actual Studio matrix ten characters, rifles/pistols and side views; actual Stage2 AI/player firing, muzzle and movement/reload inspections. Correct visible contact/occlusion issues before accepting numerical pass.
- [x] Whole-change read-only review; final build/Integration + relevant tests; decisions/issues/checklist/model spec and evidence/limitations. Restore temporary project name, leave existing user's processes/main untouched.

Files: WeaponVisual.cs (family/contact), CharacterWeaponPose.cs (shared calculation), CharacterWeaponGrip.cs (dispatch/support/transition), weapons-build.py + stocked GLBs (author contact), SharedWeaponPoseCheck.cs/.tscn, Studio test matrix, existing affected tests/docs. Do not add generic animation framework or change gameplay balances.

Evidence and bounded remaining art issues: doc/shared-weapon-poses.md. Final review found offset load order and pre-tree global transforms; both reproduced and fixed, Companion253 and Shared1118 green.
