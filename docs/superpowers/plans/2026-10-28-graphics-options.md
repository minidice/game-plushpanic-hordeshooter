# Graphics options and explicit environment categories

Goal: ESC graphics subpage and main-menu equivalent, persistent per-client shadow/surface High/Low selection with always-visible mutually exclusive buttons. No random time selection, running clock, new weather effects or automatic change to map lighting.

Design: GameSettings owns GraphicsQualityLevel {High=0,Low=1}, ShadowQuality/SurfaceQuality properties and GraphicsQualityChanged event. High preserves authored rendering. Low disables real shadow maps and SSAO, draws bounded batched ground-contact ellipses for living actors and nearby selected nature props, respecting ground height. Surface Low uses cheaper environment shader paths; it is not advertised as texture memory reduction. Existing companion quality remains accessible.

Environment categories: independent indoor light (dark/daylit), outdoor time (dawn/morning/noon/sunset/dusk/night), weather (clear/sunny/overcast/rain/snow), ground condition (dry/wet/snow_covered). Store stable IDs and localized names as catalog only; valid combinations checked, no runtime randomization/weather/lighting mutation. Dark means environmental light intent, not forcing unreadable black gameplay now.

- [x] Settings/runtime: root owns GameSettings, Stage hookup, GraphicsQualityController, blob shader, project global parameter, surface shader branches.
- [x] UI worker: Hud pause graphics subpage, reusable GraphicsOptions control, MainMenu integration, kor/eng localization. API contract above. Back/Esc return to pause before resume, keep multiplayer semantics.
- [x] Catalog worker: dedicated environment category JSON + small validated loader + dedicated test and taxonomy document. No stage mutation or Studio editor feature.
- [x] Root tests: persistence/default/invalid values, actual runtime light restore and late-spawn actors, simple shadows on elevated ground, UI radio state/navigation, shader compilation and high/low render captures. Capture performance on same scene.
- [x] Review diffs, update feature docs and checklist, retain current worktree, no commit/merge/push requested.

Context: existing worktree codex/forest-surface-quality includes prior authorized surface pilot changes. Preserve those. Approved scope comes directly from user latest message; no extra approval gate.

Verified: build 0 warnings/errors; GraphicsOptionsCheck 33, MenuCheck 44, CompanionCheck 39, EnvironmentCategoriesCheck 265. Two independent review passes found and fixed pause focus, running-world E input, and low-quality CPU work. Direct-launch settings loading also fixed and tested. High/Low GPU render and performance evidence is recorded in doc/features/menu-ui/log/006-code-graphics-options.md. Low-end hardware and long multiplayer/horde runs remain unverified; no commit/merge/push performed.
