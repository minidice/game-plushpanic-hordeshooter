# Goose field kit candidate — production plan

Date: 2026-10-22. User authorized a separate-folder model and comparison renders.

Goal: preserve the existing goose and create one grounded helmet, carrier/pouches and backpack set.
Spec: ../../doc/grounded-art-direction.md. Scope is an offline art candidate, not a game feature.

All generated files stay under this folder. Existing game/source files are read-only. No branch switch, staging, game build, or integration.

1. Inspect source GLB geometry, hierarchy and materials; copy the original into source/reference and record SHA-256.
2. Create a standalone Blender generator, replacing only the default helmet/vest/pack geometry in an imported copy. Keep goose body, face, wings, feet and original attachment pivots. Keep original assets untouched.
3. Export full-character and equipment-only GLBs plus editable Blender source. Validate geometry, bounds, node ownership, material export, and source hash.
4. Render old/new under identical camera/light settings: front, side, rear, quarter and top-down gameplay scale. Assemble labeled comparison boards from these actual renders.
5. Inspect images; repair fit/occlusion/export problems. Record measured counts, checks, limitations and reproduction commands in README and manifest.

Verification focus: original body unchanged; gear doesn't cover eyes/beak; wings can be seen; carrier follows the body rather than floating; backpack straps connect; same camera and materials for unchanged body; GLB re-import; no source changes. Rendering is Blender review, not Godot/multiplayer validation.

No implementation-mirroring unit tests for a one-off modeling tool. Geometry assertions and exported-file inspection validate the actual assets. All exact visual proportions are candidate choices, not approved gameplay values.

## Follow-up workwear candidate 02

User approved modeling after wardrobe-direction.md and supplied a third, top-down reference. Scope: one blue work vest and ochre cloth cap on the unchanged goose, no pack or armor. Preserve candidate 01 as a read-only comparison. Build a fitted cloth shell with oval wing openings, export character/clothing-only GLBs and a separate Blender file, validate the actual exports, and render three variants with identical settings in six views. Record results in WORKWEAR-RESULTS.md. Game integration, layering systems and other species remain out of scope.
