# 환경 아트 — 이슈 본문

새 이슈는 여기 ID 아래에 쓰고, [전체 색인](../../issues.md)에는 ID · 한 줄 증상 · 상태 · 날짜 · 링크만 한 줄로 적는다. 새 이슈 ID는 전체 번호(`I-126`부터)를 쓰고, **색인에 한 줄을 먼저 추가해 커밋 · 푸시한 뒤** 본문을 쓴다(먼저 예약). 푸시가 막히면 받아서 다음 번호로 바꾼다.

**옛 기록은 옮기지 않고 그대로 있다**(2026-10-26 결정, [기록 문서 나누기](../../records-split-plan.md) 6.4). 이 기능의 2026-10-26 이전 결정 · 이슈 · 확인 기록은 [decisions.md](../../decisions.md) · [issues.md](../../issues.md) · [feature-checklist.md](../../feature-checklist.md)에 있다.

<!-- 새 이슈 예:
## I-126 증상 한 줄
- 증상:
- 원인(추정이면 추정):
- 해결:
- 재발 방지 · 검사 경로:
- 남은 제약:
-->

## I-126 ForestExpansionCheck가 숲 소품 GLB를 찾지 못하고 멈춤
- 증상: `ForestExpansionCheck`(헤드리스)가 나무 · 풀 개수 검사 3개를 통과한 뒤 `System.InvalidOperationException: Sequence contains no matching element`로 멈춤(`FOREST_EXPANSION_FAIL`).
- 원인(추정): `ForestExpansionCheck.cs` 38행 `Asset(holder)`가 `KitProps`의 각 소품 아래에서 `SceneFilePath`가 `.glb`인 자식을 `First`로 찾는데, 그런 자식이 없는 소품이 생김. 최근 `LayoutMap` 변경(재질 소리 연결 `4bacc0d`, 큰 스테이지 L-30 `0543049`)으로 소품 노드 구성이 바뀐 것으로 추정 — 미확인.
- 확인: 2026-10-27 카메라 기본 시점 작업(L-2) 중 검사 묶음에서 실패. 카메라 변경 전 main `eedab6d`를 별도 worktree로 받아 같은 장면을 돌려도 **똑같이 실패** — 카메라와 무관한 기존 실패([레벨 기록 002](../level/log/002-code-camera-default-view.md)).
- 확인된 원인(2026-10-27): 야적장의 파괴 가능한 콘크리트 벽 3개(`BreachWall_yard_west` · `_workshop_side` · `_east`, `ObjectBody` + 코드로 만든 `BreakableWallVisual`)가 `KitProps`에 들어 있다. 검사의 `Asset()`가 모든 소품에 GLB 자식이 있다고 가정해 `First`에서 예외. 게임 쪽은 정상(파괴 벽은 원래 GLB가 아님).
- 해결(2026-10-27, [기록 007](../tech-tools/log/007-code-fix-broken-checks.md)): `Asset()`가 GLB가 없으면 빈 문자열을 돌려주게 하고, 대신 "GLB 없는 소품은 `BreakableWallVisual`을 가진 파괴 벽뿐"을 검사로 추가(다른 이상한 소품이 생기면 여기서 잡힘).
- 재발 방지 · 검사 경로: `Godot --headless --path game res://tests/ForestExpansionCheck.tscn`. 고칠 때 소품 노드 구성을 확인하고, 검사는 GLB 자식이 없는 소품을 이름과 함께 알려 주게.
- 검사: `ForestExpansionCheck` 45 통과(헤드리스).
- 상태: **해결**. (해결 전 제약: 이 검사가 멈춰 숲 형태 분포 · 크기 · 줄 검사가 돌지 않았음.)

## I-127 ForestQualityCheck가 같은 예외로 멈춤
- 증상: `ForestQualityCheck`(헤드리스)가 `Sequence contains no matching element`로 멈춤(`FOREST_QUALITY_FAIL`).
- 원인(추정): I-126과 같음(숲 소품에서 GLB 자식 찾기) — 미확인.
- 확인: 2026-10-27 카메라 기본 시점 작업(L-2) 중 검사 묶음에서 실패. 카메라 변경 전 main `eedab6d`를 별도 worktree로 받아 같은 장면을 돌려도 **똑같이 실패** — 카메라와 무관한 기존 실패([레벨 기록 002](../level/log/002-code-camera-default-view.md)).
- 확인된 원인(2026-10-27): I-126과 같음.
- 해결(2026-10-27, [기록 007](../tech-tools/log/007-code-fix-broken-checks.md)): I-126과 같은 수정(`ForestQualityCheck.cs`).
- 재발 방지 · 검사 경로: `Godot --headless --path game res://tests/ForestQualityCheck.tscn`.
- 검사: `ForestQualityCheck` 22 통과(헤드리스).
- 상태: **해결**.
