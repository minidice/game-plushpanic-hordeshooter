# 007 · code · 깨진 검사 4개 수정 (I-126 ~ I-129) · `game/output/.gdignore`

관련: I-126 · I-127([환경](../../art-environment/issues.md)), I-128([적](../../enemies/issues.md)), I-129([이펙트](../../effects-audio/issues.md)), 발견 기록 [레벨 002](../../level/log/002-code-camera-default-view.md). 2026-10-27.

## 요청

"I-126~I-129 깨진 검사 4개를 고쳐줘. game/output에 .gdignore 추가도 같이."

## 원인과 수정

네 개 모두 **게임은 정상이고 검사가 옛 구조를 가정**하고 있었다. 검사 의도는 그대로 두고 지금 구조에 맞췄다.

| ID | 검사 | 확인된 원인 | 수정 |
|---|---|---|---|
| I-126 | `ForestExpansionCheck` | `KitProps`에 야적장 파괴 콘크리트 벽 3개(`BreachWall_yard_west` · `_workshop_side` · `_east`, 코드로 그린 `BreakableWallVisual`)가 있는데 `Asset()`가 모든 소품에 GLB 자식이 있다고 가정(`First`) | GLB가 없으면 빈 문자열. 대신 "GLB 없는 소품은 파괴 벽뿐" 검사 추가 |
| I-127 | `ForestQualityCheck` | 같음 | 같은 수정 |
| I-128 | `CommonRosterFieldCheck` `ElevatedTell` | `cdc786d`(공격 예고 표시 다듬기)에서 원거리 조준 예고가 늘린 상자(`BoxMesh`) → `GroundMarker`. 좌표 (488, -92)는 여전히 높은 단(추정했던 맵 좌표 문제 아님) | 마지막 `GroundMarker`를 찾게. 높이 기대값(두 몸 높이 중간 + 0.055 m)은 새 표시 위치 규칙과 같아 그대로 |
| I-129 | `AreaEffectsReadabilityCheck` | 같은 커밋에서 보스 원판 + 채움 원기둥 2개 → `GroundMarker` 하나(테두리 · 채움을 한 셰이더가 그림) | `Footprint`가 내려찍기 반지름 × 2인 `GroundMarker`를 찾게(크기까지 확인) |

확인 방법: 숲 검사는 GLB 없는 소품의 이름 · 타입 · 자식을 한 번 출력해 파괴 벽 3개만 나오는 것을 본 뒤 출력을 지웠다. 예고 표시는 `RangedEnemy.StartWindup` · `BossEnemy.ShowDisc`의 `GroundMarker.Lane` · `Circle` 호출과 `git log`(`cdc786d`)로 확인.

## `.gdignore`

- `game/output/.gdignore`(빈 파일) 추가, `.gitignore` · `game/.gitignore`에 `!output/.gdignore` 예외.
- 이유: 검사 · 촬영 렌더가 `game/output/`에 쌓이는데 Godot가 이를 게임 에셋으로 가져왔다. [레벨 002](../../level/log/002-code-camera-default-view.md)에서 가져오기가 촬영 사진을 다시 가져오다 종료 코드 139로 끝난 적이 있다.
- 영향 확인: `res://output/…`를 쓰는 검사(`DuckCleanCheck` · `PlayerMammalCleanCheck` · `TailMotionCapture` · `WorkshopWalkthrough` · `HunterAudioMixCheck`)는 모두 파일 **저장만** 하고 리소스로 다시 불러오지 않는다(`GlobalizePath` · `SavePng` · `SaveToWav`).

## 검사

- C# 빌드: 경고 · 오류 0. `.gdignore` 추가 후 에셋 가져오기 종료 0 · 오류 0.
- `ForestExpansionCheck` 45 · `ForestQualityCheck` 22 · `CommonRosterFieldCheck` 93 · `GroundMarkerCheck` 29 · **`Integration` 149** 통과(헤드리스, 종료 0, 누수 출력 0).
- `AreaEffectsReadabilityCheck` 16 통과(창 모드, 실제 GPU).
- 기준 main: `4d6aa94`.

## 남은 제약

- `.gdignore`는 이 파일이 들어간 뒤 새로 만든 작업 폴더부터 효과가 있다. 이미 `game/output/` 안에 생긴 `.import` 파일은 각 로컬 폴더에 남아 있을 수 있다(git 추적 대상 아님).
- 공격 예고 표시를 바꾼 `cdc786d`가 관련 검사를 같이 고치지 않았다. 표시 구조를 바꿀 때는 `GroundMarker`를 찾는 검사들(`GroundMarkerCheck` · 이번 두 검사)을 함께 돌린다.
