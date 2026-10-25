# Lion Grip and Grounded Limbs Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for model/runtime tasks and independent review.

**Goal:** Fix elongated lion weapon hands, spherical feet/thighs and weak ankle connection; verify Blender, Studio and actual Stage 2.
**Architecture:** Lion-specific split forearm and rigid paw. Preserve paw proportions, align its authored grip anchor to weapon, stretch only forearm to the wrist. Sculpt flatter planted feet and tapered thigh/ankle geometry. Existing birds stay unchanged.
**Tech Stack:** Blender 5.2, Godot 4.7.2 C#, Studio.
**Spec:** Explicit user request and doc/lion-quality.md prior limits.

## Shared model/runtime contract
- Existing `common_lion_leftArm` / `rightArm` shoulder pivots stay at authored Blender (+/- .39,0,.99).
- Each Arm contains forearm MeshInstance named `<Arm>_mesh` (primary fabric); its lower end connects to Wrist. Avoid geometry beyond shoulder/upper end.
- New child Node3D `common_lion_leftPaw` / `rightPaw` under Arm. Contains `<Paw>_mesh` with primary + secondary materials, and named world-authored anchors `common_lion_leftWrist` / `rightWrist`, `common_lion_leftGrip` / `rightGrip` under Paw.
- Wrist baseline suggested authoring (+/- .435,-.025,.80), grip (+/- .435,-.065,.685). Model may adjust but runtime reads actual node transforms, never fixed vertex indexes or coordinates.
- Paw is compact, moderately flattened, broad at knuckles with a small thumb/crease to suggest holding; wrist narrow, not a long oval. Forearm and paw overlap at wrist by a small amount to avoid cracks. No per-frame mesh allocation/skeletal overhaul.
- Feet: authored floor z=0, broad planar sole, rounded upper surface/toe and subtle integrated toe separation. Thigh narrows toward ankle and joins foot; existing foot/root/head/weapon pivots, collision and combat values unchanged.
- Other six current GLBs unchanged. Preserve all pending eagle/lion work, no commits/push this turn.

## Tasks
- [x] Model: preserve current lion baseline in a new `output/lion-limbs` review folder (do not overwrite previous lion-quality evidence), extend QA tools with optional review name, sculpt/export lion only, inspect before/after and check split-arm/paw anchors/sole/budget.
- [x] Runtime: implement lion-only rigid paw + independent forearm transforms; integrate grip/traversal/reset/visibility/status effects. Existing bird path remains unchanged. Add meaningful tests demonstrating old rigid-size failure/missing anchors then preserving paw distances, exact anchor alignment, visible wrist/shoulder connectivity through six weapons/attacks/reloads/scales.
- [x] Integration: import/build, six weapon Studio views and hunter attacks; actual Stage2 window movement/combat/renders; check feet and hands at actual play scale. Fix observed issues, independent review.
- [x] Records: update decisions/issues/checklist/roster/lion documentation with changes, evidence, failures and remaining limitations.
