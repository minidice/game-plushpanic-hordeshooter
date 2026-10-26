# Mammal tails implementation plan

> **For agentic workers:** Use subagent-driven-development for runtime task and parent model/visual integration. No commit/push.

**Goal:** Apply all three user-approved proposals to raccoon and redpanda: nearly uniform long-tail girth, gently curved resting silhouette, restrained delayed tip follow-through that settles quickly.
**Architecture:** Canonical small profile shared by C# procedural style generation and Blender default bake. Long styles0/1/2/4 use a plateau radius until80% then rounded tip; shortstyle3 and birds retain old shape/motion. Preserve tail_size/style/color/rings. Runtime tail-local lightweight Skeleton3D with root anchored and three sequential delayed bending regions, GPU skinning of static mesh so StandardMaterial and shadows remain. Handle status overlay skeleton consistently; no per-frame mesh rebuild/full-character rig.
**Tech Stack:** Godot C#, Blender Python, existing Studio Godot renderer.
**Spec:** User explicitly approved previous shape/curve/motion proposal; doc/character-model-spec.md tail contracts and existing CharacterTail.cs/CharacterTailMesh.cs. Existing art direction A-25.

## Global constraints
- Only raccoon/redpanda tail presentation changes. Birds and shortstyle3 retain existing geometry/motion.
- Preserve existing styleIDs0..4, TailSize .65..1.3, root attachment, two stripe/color channels, defaults restoration, per-character ownership; no gameplay collision/speed/weapon/network changes.
- Other geometry/source fitted GLBs immutable. Clean GLBs may change only tail mesh. Keep previous uncommitted work; no commits/pushes.
- Movement is bounded cosmetic follow-through, not physics collision/simulation; no tail-wall collision promised.
- Tail default rest profile contract: Raccoon length.95m radius.145m; redpanda length1.15m radius.18m. Keep existing style Length/Width/Curl multipliers. Long width plateau: radius factor1 for t<=.8, sqrt(max(0,1-((t-.8)/.2)^2)) for t>.8. Root cap embedded, smooth rounded terminal cap. Centerline longstyles: (0.10*sin(pi*t/2)^2, -0.075*sin(pi*t)+0.10*t*t+curl*.62, -length*shape.Length*t*(1-curl*.45)), curl=shape.Curl*pow(t,2.2). Godot axes Y up,Z rearward negative. Curve/width art values provisional not user-authored exact numbers.

## Review focus
- Actual cross sections uniform/root anchored with all longstyles; style3/birds byte-equivalent procedural geometry.
- Tail root never drifts and separate character meshes/materials/state do not contaminate each other.
- Turning causes successive delayed response, then stable rest, no slowmo-dependent explosion / teleport whip.
- Colors, infection overlay/hit flash, status surfaces, hide/show/corpse reset must follow deformation, no static ghost tail.
- Studio static/default restore and actual game motion must show same mesh/profile; genuine rendered multi-frame evidence.

## Task1 runtime and dedicated checks (agent)
- [x] Own CharacterTail.cs, CharacterTailMesh.cs, new CharacterTailSkin.cs (if needed), minimal CharacterRig.cs/StatusVisualController.cs changes for correct integration, new game/tests/MammalTailCheck.cs/.tscn (+uid) and TailMotionCapture.cs/.tscn if needed. Parent owns Blender, all docs and Studio API QA.
- [x] Implement profile above for longmammal styles, leave bird and shortprofile old. Defaults must use same profile even legacy mammal model variants. Expose tiny internal profile helpers only as needed for code/verification.
- [x] Add lightweight tail-only GPU skinning: stable root+progressively delayed middle/tip, based on movement and heading change in Animate; bounded response, stop settles within~1s, shortstyle3/bird untouched, dt0 preview neutral, reset/style changes clear history. Keep material channels and separate ownership; avoid per-frame mesh/resource allocation.
- [x] Write meaningful red tests before production edits then green. Actual mesh radius profile/root, distinct species radius, defaults/style/color/size transitions, sequential turn lag & settling, dt/teleport bounds, independent rigs/materials, status/hit/hide and reset. Test actual rendered posed geometry in windowed mode as well as mathematical state; do not claim marker-only validates rendering. Existing TailMeleeCheck must pass with meaningful expectations (adjust only if documented newcontract changes actual intended behavior).
- [x] Provide reproducible capture sequence for bothspecies rest/move/turn/settled (fixedcamera rearquarter), and actualStage2 player turning/stopping with screenshots/pose measurements. Save to game/output/mammal-tails. Can keep captures in dedicated test if compact.
- [x] Report commands/counts/limitations in .superpowers/sdd/2026-10-26-mammal-tails/task-1-report.md. No model assets/generator/docs edits, no subagents.

## Task2 model/studio/regression/docs (parent)
- [x] Add common Blender tail profile helper to two clean builders; bake same defaultprofile preserving all non-tail nodes and materials. Extend validation exception only fortail + new independent tail crosssection/profile/sourceimmutability checks. Generate actual Blender before/after side/rearquarter.
- [x] Import/build; actual Studio bothspecies style0..4, size/color extremes, defaultrestore. Run dedicatedTail test/capture plus TailMelee/CharacterSurface/Tone/Modular, prior mammalrig and buildDebug/ExportRelease checks as relevant. Inspect PNGs and multi-frame motion.
- [x] Document shape/animation contract, decisions/issues/checklist/spec, limitations and warnings. Independent task+final review; fix important findings; final evidence with no commit/push.

Review amendment: longmammal styles use7alternating bands ending in material0 to retain prior clean default lighttip. Previous6band provisional plan yielded a darktip regression; shortstyle3/bird retain existing banding.

Final status: current tail work implemented and scoped/finalreviews clean. Dedicated checks/render pass; fullIntegration shots-kill123failure remains explicitly unresolved. No commit/push.
