# 002 · code · 모든 맵 공통 기본 카메라

관련: [L-2](../decisions.md#l-2-카메라-기본-시점-2026-10-27), L-14, L-1, L-32, C-26. 2026-10-27.

## 요청

사용자 참고 이미지(덕코프식 숲길, 위에서 내려다보는 넓은 시야) — "이게 훨씬 넓게 많은게 보인다", "지금 각도처럼 내려다보게 인게임을 조정", "최신 pull로 적용 후 모든 맵 다 적용, 기본셋으로".

## 비교

`CameraViewCapture.tscn -- --candidates`(창 모드 1600×900)로 Stage 1 거리 · Stage 2 숲의 같은 자리를 찍어 참고 이미지와 나란히 봤다.

| 후보 | 수직 | 화면 높이 | 시야각 | 판단 |
|---|---|---|---|---|
| 이전 Stage 1 | 55° 직교 | 17 m | — | 좁고 낮음 |
| 이전 Stage 2 | 41° 원근 | 19 m | 38° | 낮게 깔려 앞만 보임 |
| p55-s19-f38 | 55° | 19 m | 38° | 아직 낮음 |
| p60-s19-f38 | 60° | 19 m | 38° | 각도는 비슷, 좁음 |
| **p60-s21-f34** | **60°** | **21 m** | **34°** | 참고 이미지와 가장 비슷, 넓음 → 채택 |
| p65-s21-f34 | 65° | 21 m | 34° | 바로 위에서 보는 느낌이 강함 |

## 변경

- `CameraRig`: `Pitch` 55 → **60**, `WideSize` 17 → **21**, `CloseSize` 10 → **12.5**, 새 `DefaultFov` **34**. `CameraView.Indoor` · `Outdoor`를 없애고 **`CameraView.Default`**(원근) 하나로. 지금 화면 높이를 읽는 `ViewSize`(원근에서는 `Size`가 쓰이지 않으므로) 추가.
- `StageMap.View` 기본이 `CameraView.Default`, `StageTwoMap` · `LayoutMap`의 `Outdoor` 재정의 제거. `StageTransition.FromView` 기본도 `Default`. 수평 -30°, 조준 쪽 기울기 · 저격 확대(C-26), 도착 때 가까운 크기에서 넓어지는 연출, 거리 안개는 그대로.
- 검사: `StageTransitionCheck` — Stage 1 · 2가 같은 60° 기본 시점, 전환 때 각도가 이어짐(이전: Stage 1 55° 직교, Stage 2 시작이 8° 이상 가파름). `WeaponHandlingCheck` — 조준 확대를 `Cam.Size` 대신 `Cam.ViewSize`로 잼.
- 비교 촬영 `game/tests/CameraViewCapture.cs` · `.tscn` 추가(창 모드 전용, `game/output/camera-view/`).
- 문서: [decisions.md](../../../decisions.md) L-2 · L-14 · 6장 「바뀐 결정」, [레벨 본문](../decisions.md), [level-design-rules.md](../../../level-design-rules.md), [character-model-spec.md](../../../character-model-spec.md) 크기 기준, [feature-checklist.md](../../../feature-checklist.md).

## 검사

- C# 빌드: 경고 · 오류 0.
- 렌더(창 모드): `CameraViewCapture` — Stage 1 · 2 · 3 모두 60.0° · 카메라 거리 34.3 m로 찍힘, 참고 이미지와 나란히 확인.
- 헤드리스 통과 28: `StageTransitionCheck` 215 · `WeaponHandlingCheck` 36 · `CorpseReactionCheck` 29 · `CorpseRetentionCheck` 16 · `CorpseTierCheck` 36 · `GroundMarkerCheck` 29 · `SquadCheck` 51 · `ModeCheck` 147 · `InventoryCheck` 41 · `CombatPacingCheck` 44 · `CompanionCheck` 39 · `BurstRadiusCheck` 49 · `TerrainEdgeCheck` 37 · `SizeDraftCheck` 76 · `HunterCombatCheck` 29 · `CommonRosterPlayCheck` 7 · `ReloadUiCheck` 38 · `StatusVisualCheck` 57 · `CardLabCheck` 172 · `DamageEffectsCheck` 31 · `CombatEffectsCheck` 158 · `WeaponEffectsCheck` 100 · `RifleEffectsCheck` 17 · `InfectedAlignmentCheck` 25 · `MammalArmStageCheck` 530 · `TraversalQualityCheck` 55 · `ReindeerZombieCheck` 8012 · **`Integration` 149**. 모두 종료 0, 누수(ObjectDB · orphan) 출력 0.
- 최신 main(`630a184`) 위로 다시 올린 뒤: 빌드 경고 · 오류 0, `StageTransitionCheck` 215 · `WeaponHandlingCheck` 36 · `Integration` 149 통과. 이때 에셋 가져오기가 한 번 종료 코드 139(비정상 종료)로 끝났다 — 로그상 `game/output/camera-view/`의 비교 사진을 다시 가져오던 중. 그 폴더를 지우고 다시 돌리자 종료 0 · 오류 0(`game/output/`에 `.gdignore`가 없어 촬영 사진도 가져오기 대상이 됨).
- 창 모드: `InputCheck` 14 통과(헤드리스 실패는 실제 마우스가 없어서 — 원래 창 모드 전용).
- **실패 4 — 변경 전 main(`eedab6d`, 별도 worktree)에서도 똑같이 실패하는 기존 실패**, 카메라와 무관:
  - `ForestExpansionCheck` · `ForestQualityCheck`: `Sequence contains no matching element` — 숲 소품 중 `.glb` 자식이 없는 것(`ForestExpansionCheck.cs` 38행 `Asset()`의 `First`). 추정: 최근 `LayoutMap` 변경(재질 소리 연결 `4bacc0d` 또는 L-30 큰 스테이지)으로 소품 노드 구성이 바뀜.
  - `CommonRosterFieldCheck`: `ElevatedTell`(160행)이 높은 단 총 예고 표시 `BoxMesh`를 `Effects`에서 찾지 못함. 추정: 고정 좌표 (488, -92)가 L-30 큰 Stage 2에서 다른 곳이 되었거나, 예고 표시 방식이 바뀜.
  - `AreaEffectsReadabilityCheck`(창 모드 전용): "actual boss disc and fill exist" — 보스 내려찍기 예고에 `CylinderMesh`가 2개 미만. 추정: 장판 효과 작업(area-effects)이 예고 그림을 바꿈.
  - 이슈 **I-126**(숲 확장) · **I-127**(숲 품질) · **I-128**(무리 필드) · **I-129**(장판 가독성)로 등록(색인 예약 커밋 `630a184` 먼저 푸시, 본문은 [환경](../../art-environment/issues.md) · [적](../../enemies/issues.md) · [이펙트](../../effects-audio/issues.md)).

## 남은 제약

- 60° · 21 m · 34°는 비교 렌더로 고른 `[임시]` 값. 실제 손 플레이(조준 · 이동 · 4인)로 다시 맞춘다.
- Stage 1 실내도 원근이 되어 화면 가장자리 벽이 조금 바깥으로 기운다. 실내 가림(벽 · 천장) 문제는 렌더에서 보이지 않았으나 모든 방을 돌아보지는 않았다.
- 촬영용 장면(`Capture*`, `*Capture`)과 미술 검수 장면 중 `Cam.Size`로 화면을 잡던 것은 원근에서 `Size`가 무시되어 구도가 달라질 수 있다(통과/실패 검사가 아니라 이번에 고치지 않음).
