# Four-species art implementation plan

> For agentic workers: user explicitly requests parallel isolated species agents. Use dispatching-parallel-agents and native managed worktrees. Parent integrates shared code and final validation; do not edit primary checkout.

**Goal:** Improve goose, duck, tiger, rhino in Blender to existing approved clean stylized animal direction, connect to game/Studio, validate actual renders and preserve customization/gameplay.
**Architecture:** Four independent managed worktrees from de43ab9; parent integration codex/four-species-art. Player birds use immutable fitted source and separate clean GLB/generator. Monster species use separate builder modules dispatched from existing common generator, retaining palette/module/rig contracts. Parent coordinates shared runtime/test/document merges and private Studio renderer.
**Tech Stack:** Blender5.2 Python, Godot4.7.2 C#, existing Studio Node API.
**Spec:** doc/character-art-direction.md, doc/player-species-guide.md, doc/character-model-spec.md, doc/monster-design-guide.md, doc/common-monster-roster.md; existing user reference art direction and request to improve all4 in parallel.

## Constraints
- Primary D:/Project/Game/game-hordeshooter is owned by anotherLLM and has dirty shooting/player changes. Never write there, switch its branch, refresh its Studio4323/4317, or kill its processes.
- Base de43ab9 contains previous model/tail work and exitAudio fix. Do not copy primary's uncommittedchanges. Final combine into isolated integration branch only; no merge/main/push in this task.
- Smooth continuous matte large forms, small shallow eyes, recognizable bill/horns/stripes, connected compact hands/feet, fitted gear. No plush/stitches/balloon cheeks or generic sphericalmittens. Original collision/speed/health/weapon/network IDs and saved customization keys/ranges remain.
- Player helmets/goggles remain available; common monsters nohelmet/noeyewear policy unchanged. Do not broaden eyebrowfeature orcombatmechanics.
- Baseline/source and unselected species GLBs immutable; generated output ignored, runtimeassets/generator/tests/uid tracked.
- Docs decisions/issues updated in same local integrationcommit as functionality. Eachagent append species-specific entries andownspec; parent resolves appendconflicts preservingall.
- No sharedworkingdirectory betweenagents. Agent commits only itsownwork forcherrypick; parent integratesreviewedcommits andneverpushes.

## Tasks
- [ ] Goose agent: own goose-clean.glb/.import, goose.tres, standalone build/validate_goose_clean.py, GooseCleanCheck/.tscn/.uid and speciesStudioQA/doc. Keep recognizable tallbody/slimmerbill; refine billroot/eye, featherhand grips, ankle/webbedfoot. Preserve bodyfacemasksurface ifneeded ratherthan breaking shader. Capture Blenderbeforeafter/profile and signalready forGPUqueue.
- [ ] Duck agent: analogous duckfiles; lowwidebill/shortbroadbody contrast togoose, shalloweyes, shapedwingtips andflatwebbedfeet. Preserve savedface/billstyles, materials/pivots. No goosefileedits.
- [ ] Tiger agent: own tiger_clean.py build(api) module dispatched from animal(kind), tiger.glb, independent validation/test/doc. Flattenjoinedmuzzle, smallalmond eyes, patterned continuous body(no raisedtubular tattoo stripes), cuppedroundears, taperedsmallhands andflatpaw/ankle. Reuse lion fixedpaw childcontract ifappropriate; parent coordinates CharacterWeaponGrip whitelist. Preserve palette names including ink/secondary mapping, moduleIDs and armed/hunter contracts. OtherGLBs unchanged.
- [ ] Parent rhino: rhino_clean.py analogous; broadhead/heavyjaw+distincthorn, small eyes andears, stocky continuous torso, compactthickhands, toe/flatsole definition; preserve speciesheft withoutspherical parts. Existing combat remains.
- [ ] Parent integrate modelcommits; reconcile sharedbuilderdispatch/runtime/tests/docs; privateStudio port independent from4323/4317. Import/build perworktree. Verify actualbase/gear/weapon/color/style extremes and defaultrestore; moving/fire/reload/melee actualStage2 captures; relevantregressions. No reference-onlymockups as gameplayevidence.
- [ ] Readonly visual+code review; fixconcreteissues, update decisions/issues/checklist/specs; final comparison and limitations. Maincheckoutunchanged evidence; no mainmerge/push.

## Verification focus
- Preserve each species' namedpivots/material channels and source hashes; no unrelatedasset overwrite. Baseline andfinalsamecamera lighting silhouette/neutral/color views.
- Hands bind actualweapongrips, sourcewingvertices avoid stretchedspikes; groundsoles/ankles connected, head/neck duringmovement/reload.
- Existing birdtailgeometry and mammaltailchanges unchanged; fieldprofiles preservedefaultreset andperactorownership/hit/status/hide.
- Both tinygameplay andcloseview matter; numbers/colors cannot maskbadshapes. Metrics andtestpasses do not equaluserfinalartacceptance.
- Private user:// during runtime tests: askparentforGPU slot anddataisolation if needed. No testwrite original appearance.cfg oruse existing Studio ports.

## Tools
Blender C:/Util/Blender Foundation/Blender 5.2/blender.exe; Godot D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe; Python D:/Util/Python314/python.exe; powershell loginfalse; explicitworkdirfor everycommand. Primary's originalreference images onlyreadonly ifneeded, artguidescaptureapproveddirection.
