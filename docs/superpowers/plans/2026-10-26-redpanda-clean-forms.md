# 랫서판다 얼굴·귀·손발 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. No commits or push in this task.

**Goal:** 사용자 승인된 플레이어 점검 권고대로 랫서판다 얼굴·귀·손발을 개선하고 기존 커스터마이징을 유지한다.
**Architecture:** stable redpanda-fitted.glb를 Blender의 읽기 전용 원본으로 사용하고 새 redpanda-clean.glb를 내보낸다. 기존 노드/재질 계약 유지, 손만 Paw/Wrist/Grip 계약 추가. 기존 사자 고정 손 경로를 플레이어에 재사용한다.
**Tech Stack:** Blender Python, Godot C#, Studio actual Godot API.
**Spec:** doc/player-art-audit-2026-10-26.md + doc/player-species-guide.md + doc/character-model-spec.md. 사용자가 랫서판다 권고안 진행을 명시 승인했다.

## Global Constraints
- 기존 커스터마이징(색/무늬·눈·귀·꼬리·체형·장비), 저장 키와 범위를 유지한다.
- 전투·충돌·속도·한손 파지 정책은 변경하지 않는다.
- 다른 종 GLB와 원본 redpanda-fitted.glb를 변경하지 않는다.
- 봉제인형 스타일을 추가하지 않는다. 연속된 매끈한 형태, 얕은 작은 눈, 귀 깊이, 작고 납작한 손발을 목표로 한다.
- 이전 순록·좀비 미커밋 변경을 보존한다. 이번에는 커밋/푸시하지 않는다.

## Review Focus
- 기본 귀를 바꿨다가 복구할 때 새 GLB 귀가 정상 복원돼야 한다.
- 손/발 크기와 몸 크기의 극값에서도 파지·바닥 접촉이 유지돼야 한다.
- 무늬/주둥이/눈 간격 색 커스터마이징과 저장을 유지해야 한다.
- 무기 교체·재장전·등반·집라인·피격·숨김에서 손/팔 누락이 없어야 한다.
- Studio 새 모델과 실제 게임 모델이 같아야 한다.

## Task 1: Blender 조형 (model agent)
- Create prototypes/grounded-art/tools/build_redpanda_clean.py and validate_redpanda_clean.py; new game/assets/characters/redpanda/redpanda-clean.glb.
- Preserve source pivots/material channels/torso local coordinates. Sculpt muzzle/face connection, shallower eyes/brows, cupped rounded-triangle ears, compact paws with thumb and tapered forearms, flat foot soles and ankle continuity.
- Add redpanda_leftPaw/rightPaw under corresponding Arm, each Paw_mesh/Wrist/Grip. Existing redpanda_leftArm_mesh/rightArm_mesh becomes forearm-only. Rest shoulders/pivots must agree.
- Save before/after Blender views and independent reimport checks under ignored prototypes/grounded-art/output/redpanda-clean; authored .blend reproducible from script (output ignored). Report geometry counts, head/gear/ear limitations, exact anchors.
- Do not modify runtime/tres/docs. Parent owns these.

## Task 2: Runtime + focused tests (parent)
- Write meaningful missing rigid-paw contract test, verify red. Connect new model in redpanda.tres and extend existing paw route to redpanda.
- Check color copy lifetimes, source hiding, customization scales/restoring, traversal contracts; make smallest needed runtime changes.
- Focused new RedpandaCleanCheck scene: actual surfaces rigid across weapon/size/reload, palette/default restore and ears/styles, state visibility; actual Stage2 player motion/render.
- Existing WeaponGrip/ReloadPose/CharacterSurface/CustomizationTone/Modular checks and common paw regressions.

## Task 3: Visual QA + docs/review
- Studio default/angles/gear/weapon and customization extremes, same camera before/after; actual game play-scale and close-up screenshots.
- Debug/ExportRelease, dedicated tests, Blender reimport. Inspect final PNGs.
- Update decisions/issues/checklist/player guide/model spec with detailed evidence and limits.
- Independent reviewer receives current task diff, report, plan; fix important findings then final report.
