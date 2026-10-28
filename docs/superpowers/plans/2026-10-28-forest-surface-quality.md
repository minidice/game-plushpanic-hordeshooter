# Forest surface quality implementation

Approved direction: warm yellow-green stylized forest, broad rock planes/chipped edges, distinct matte stone and moss, natural dirt/grass transition. User requested parallel implementation.

Scope: current Stage 3 contains the former Stage 2 forest. First quality patch is x64..108, z-42..-2 (44x40 m), including the existing cliff, ramp and forest path. Blend outer 4 m. Keep collision, navigation, routes, foliage exclusions and other stages unchanged. No new global postprocess. Asset changes restricted to dedicated new resources if needed.

Architecture: shared ForestSurfaceQuality.Weight mirrors shader weight; material uniform forest_surface_quality enabled only by ForestMaterial for ForestStudyEnabled. Existing material and batching remain. Compare same actual game camera and uncapped benchmark labels surface-baseline/surface-after.

- [x] Baseline build/import, real renderer screenshots/performance.
- [x] Rock/moss worker: TerrainEdgeArt.cs and dedicated forest_rock_surface.gdshaderinc. Existing 18cm collision dressing budget remains. Parent stage2_ground integration owned by root.
- [x] Dirt worker: forest_patch.gdshaderinc only. Preserve forest_ground signature and original outside patch. Uniform/helper owned here: forest_surface_quality default false, forest_surface_weight(vec2) Rect(64,-42,44,40) 4m fade.
- [x] Grass worker: forest_grass.gdshader and LayoutMapForest.cs grass placement only. Preserve counts/exclusions/RNG sequence outside patch; keep material setup edits for root. Avoid asset regeneration unless isolated necessary.
- [x] Root: shared C# mask, material integration, real renderer before/after, gameplay boundary checks and performance.
- [x] Review integrated diffs; record measurements, visual limitations and test results under doc/features/art-environment/log.

Ruling: use existing procedural rock mesh and grass assets rather than duplicate Blender assets where code-native geometry is already authoritative; geometry, materials and placement matter more than asset format. Runtime-render validation is mandatory.

Ruling after first real render: the shallow cliff skin still reads as a retaining wall. Keep its collision budget, darken stone and soften joints. Author a separate Blender `surface_boulder` model with broad fracture planes/bevels and fitted thick moss, replacing only the existing pilot boulder's visual child. Preserve original collider, cover, surface audio and manifest dimensions used for vegetation exclusion. This adds model/source/manifest files but directly addresses the reference's large forms.

Visual review: first ground was too smooth; restore subtle filtered 5.5/m grain and broad moisture variation. Grass full clumps now width 1.14/height 1.12 maximum multiplier while gaps remain small; roots/counts unchanged.

Intermediate validation: baseline TerrainEdgeCheck 41 pass; initial combined TerrainEdgeCheck logic passed but shader compilation failed (do not count render); world parameter collision corrected and rendered rerun 41 pass. ForestExpansionCheck 66 pass before new boulder. Final validation complete: Blender re-import, TerrainEdgeCheck 45, ForestExpansionCheck 69 (including pilot gunfire), ForestPerformance baseline/after. Results and remaining art scope: doc/features/art-environment/log/002-art-forest-surface-quality.md. No merge or push requested this turn; retain branch/worktree.
