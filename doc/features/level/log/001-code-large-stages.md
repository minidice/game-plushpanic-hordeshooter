# 001 큰 스테이지 시안: Stage 3 지정 · Stage 1 · 2 새 규모 · 구역 등급 · 버튼 · 사다리 노출

2026-10-27. 결정 L-30 · L-31 · L-32([본문](../decisions.md)). 규격: [큰 스테이지 시안](../../../large-stages.md), [레벨 디자인 규칙](../../../level-design-rules.md).

## 사용자 요청

"레벨 디자인 규칙을 여기서 진행. 지금 테스트 지역 정도의 규모를 3스테이지라고 치고 1, 2스테이지도 해당 정도의 규모로 바꾸고, 필요한 리소스는 Codex로 생성하고, 작은 구역 · 큰 구역 나누고, 버튼과 사다리는 보일 수 있게 아래쪽을 향하는 방향에 버튼을 두지 말고 위쪽(북동이나 북서)으로 배치 노출 교체. 버튼과 사다리 보일 수 있게는 규칙 문서에도." 사용자 선택: 버튼 규칙 해석(화면 위쪽 두 벽 = 월드 북쪽 · 동쪽 벽) 맞음, Stage 1 테마 도시 · 창고 유지, 시안 맵 먼저(확정 뒤 교체), 구역 수치 추천안.

## 변경

- **규칙 · 결정**: `level-design-rules.md`에 「스테이지 규모」 「구역 등급」 「버튼 · 사다리 노출」 절과 확인 항목 2줄. L-30 · L-31 · L-32 본문(이 폴더 `decisions.md`)과 색인, L-29 「바뀐 결정」 줄.
- **Stage 3 지정**: `stage2_layout.json` · `stage2_draft_encounters.json` · `stage2_draft_supplies.json` → `stage3_*`(git 이름 변경). `stage: 3`, `dressing: stage3`, 제목 `ui_hud_stage3`. 파괴 시험 배치 키 `stage2_draft` → `stage3`. 구역 10개(큰 3 · 작은 5 + 분기 2). A 공터 폭 36 → 28 m, G 건너편 고지 폭 38 → 30 m(작은 구역 폭), 그에 따라 숨은 등장 지점 2개를 안으로.
- **L-32 적용**: 협곡 복귀 사다리를 서쪽으로 오르는 `(188,-22)→(183,-22)`에서 새 2단 바위턱(x 185~191, z −26~−18, 기어오를 수 있는 가장자리)으로 북쪽으로 오르는 `(188,-15)→(188,-21)`로. 세이프하우스는 버튼을 북쪽 벽 안쪽으로 옮긴 `safehouse_b`, 닫기 상호작용 `(-4.6,1.4,-5.55)`.
- **새 Stage 1(도시) · Stage 2(숲)**: `stage{1,2}_large_layout.json` · `_encounters.json` · `_supplies.json`. 구역 · 경로 · 임무 · 보급은 [규격](../../../large-stages.md) 1 · 2장. 경사로 옆 흐름장 막기(아래 「발견한 문제」).
- **코드**: `StageLayout`(새 항목, 사다리 방향 오류), `LayoutMap`(데이터 스테이지 번호 · 다음 · 문구, `ForStage`, 세이프룸, 셔터 + 벽 제어판, `safehouse_b` · 바닥 해치, 데이터 소품 · 가로등, 도시 조명 · 바닥, 숲 연구 구역은 Stage 3만), `LayoutMapEnvironment`(공통 / Stage 3 전용 / 숲 산포 / 도시 경계), `GameSession` · `StageMap.Create` · `Start-Game.ps1`(`-LargeStages`, `-Stage3Draft` + 옛 `-Stage2Draft`), `NetMessages`(데이터 지문에 새 조우 파일), 언어 키 7개(한 · 영).
- **도구**: `tools/floorplan/render_floorplan.py`가 구역(큰 · 작은)을 그리고 경로 이름을 일반화. 평면도 `doc/images/stage1-large-floorplan.svg` · `stage2-large-floorplan.svg` · `stage3-floorplan.svg`.
- **모델(Codex)**: [지시문](../../../model-briefs/large-stage-kit.md) → `prototypes/outdoor-kit/tools/large_stage_kit.py`, `game/assets/large-stage-kit/` 9종(+ manifest), `asset_paths.json` 등록. 경보 차로 쓰려고 `car_wreck`에 `Red` · `Hazard` 등 재질을 추가하는 후속 실행.
- **검사**: 새 `tests/LargeStageCheck.tscn`, 렌더 `tests/LargeStageCapture.tscn`. `SizeDraftCheck`(Stage 3 연결 문구 · 옵션), `SafehouseArtCheck`(`safehouse_b` · 새 버튼 자리), 파괴 배치 검사 2개(키 이름).

## 실행한 검사

모두 Windows 로컬 Godot 4.7.2 .NET, 헤드리스는 `--fixed-fps 60`. C# 빌드 오류 · 경고 없음. 새 GLB 가져오기(`--editor --import`) 종료 0.

| 검사 | 결과 |
|---|---|
| `LargeStageCheck`(새) | **통과 121**. 세 스테이지: 흐름장 출구 경로 664 · 634 · 620 m, 구역 순서 A-B-C-D-E-F-G-H / A-B-C-D-E-F-G / A-B-C-D-E-EF-F-G(큰 구역끼리 붙지 않음), 사다리 전부 북 · 동으로 오르고 카메라 광선이 막히지 않음, 스위치 · 제어판 · 세이프하우스 버튼 보임, 옛 서쪽 벽 버튼 자리는 막힘(대조), 시작 → 임무 장치 → 크레센도 → 세이프하우스 → 문 닫기 → 바닥 해치(Stage 1 · 2) / 클리어(Stage 3), 임무 장치 → 세이프하우스 1.3 · 1.4 · 2.0분(몬스터 없이) |
| `SizeDraftCheck`(Stage 3) | **통과 76**. 펌프 → 감시소 22초(옮긴 사다리 1회), 해치 → 세이프하우스 2.6분 |
| `SafehouseArtCheck`(창 모드) | **통과 63**. `safehouse_b` · 북쪽 벽 버튼 E 선택 · 문 닫기 |
| `StageTransitionCheck` | **통과 215**(prop 수 67 → 76, `shutter_leaf`도 `bridge_leaf`처럼 Keep 없음으로 기대값 갱신) |
| `DestructionPlacementCheck` | **통과 185**(키 `stage3`) |
| `Stage2BridgeCheck` · `MapCheck` 24 · `ElevationCheck` 76 · `TraversalArtCheck` 152 · `LandmarkCArtCheck` 56 | 통과 |
| `Integration` | **통과 149**. 첫 실행은 "awake zombies nearby groan" 1건 실패, 바꾼 것 없이 다시 실행해 통과(소리 타이밍 흔들림으로 판단, 이번 변경과 무관한 장면) |
| `Stage2RouteCheck` | **흔들림**: 크레센도 뒤 펌프장 입구(약 200, −12.3)에서 몬스터 40여 마리에 막혀 "pump-exit stuck"으로 실패하는 실행이 있다. **변경 전 HEAD(`ff4802c`)를 따로 작업 트리로 빌드해 5번 돌린 결과: 같은 자리 · 같은 메시지 실패 3, 컨테이너 위(328, −65.6) 끼임 실패 1, 통과 1(126)**. 이번 변경 뒤 5번: 같은 자리 실패 4, 통과 직전까지 간 실행 1(그때는 옛 버튼 자리 기대값으로 실패, 갱신함). 실패율이 같아 이번 변경 전부터 있던 흔들림으로 판단. 세이프하우스 버튼 자리만 새 위치로 갱신 |
| 렌더 `LargeStageCapture`(창 모드, 23장) | `output/large-stage{1,2,3}-*.png`. 세이프룸 북쪽 벽 스위치, 창고 벽 앞 제어판, 오두막 바위턱 · 언덕 동쪽 사다리, Stage 3 새 복귀 사다리가 카메라 쪽 면에 보임. Stage 1 · 2는 그레이박스 + 키트 소품이라 빈 바닥이 넓게 보인다(마감 전) |
| Codex 모델 | `LARGE_STAGE_KIT_BUILT 9`, 자체 검사 통과(삼각형 492 ~ 3,532). 경보 차 등 후속 `CAR_LAMPS_CHECK_PASSED`(1,964 삼각형) |

## 발견한 문제

- **경사로 옆으로 들어가는 흐름장**: 경사로 칸은 낮은 단(`TierMap.PaintRamp`의 `_tier = lowTier`)이라 `CanStep`이 같은 단으로 보고 옆 칸에서 경사로로 들어가는 걸 허용한다. 위쪽 끝 근처에서는 쐐기 옆면이 1 m 가까운 벽이라 몸이 막힌다. `LargeStageCheck`에서 Stage 1 생존자가 광장 경사로 서쪽 옆(109.5, −39.4)에 걸려 재현. Stage 3은 경사로를 정면으로만 지나 드러나지 않았다. 이번에는 **`LayoutMap`에서 피했다**: 경사로를 지을 때 낮은 끝 1.5 m를 뺀 양옆 1칸 줄을 흐름장에서 막는다(`BlockFlow`, 몸은 원래 벽에 막히는 자리). 같은 현상이 하역장 경사로(224.5, −103.5)에서도 재현됐다. 엔진 수정(경사로 칸과 옆 칸의 바닥 높이 차가 크면 못 건너게)은 모든 맵의 흐름장에 영향이 있어 새 이슈 ID로 따로 하는 것이 맞다 — 사용자 확인 대기.
- **Codex 산출물 한 장이 저장소 밖에**: 경보 차 후속 실행이 확인 렌더 `C:\prototypes\outdoor-kit\output\large-stage-kit\car_wreck_front_check.png`를 저장소 밖(드라이브 루트)에 남겼다. 게임과 무관, 지우지 않고 사용자에게 알림.

## 남은 제약

- 실제 사람 플레이(전투 · 20분 · 이동 피로) 전. 수치 · 배치는 모두 `[임시]`.
- 메뉴 흐름 교체 · 멀티 · Stage 1 · 2 환경 마감은 다음 단계(L-30). 방에서 `-LargeStages`를 쓰면 다른 PC는 지금 스테이지를 짓는다(혼자만).
- 경사로 옆 흐름장 문제는 `LayoutMap`에서만 막았다. `TierMap.CanStep` 자체 수정은 이슈로 따로.
- `Stage2RouteCheck` 펌프장 입구 흔들림(변경 전에도 5번 중 4번 실패)은 이번에 고치지 않았다. 이슈 ID를 따로 받아 기록할 대상.
