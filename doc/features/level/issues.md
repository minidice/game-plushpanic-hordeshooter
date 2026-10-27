# 레벨 — 이슈 본문

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

## I-131 G 고가 진입로의 "폭발 드럼통" 3개가 터지지 않는 장식 드럼통

- 증상: L-33 가이드 설계안 4번("드럼통을 G로", [가이드 맵](guide-map.md) 9장)으로 G 오르막 (530, −111) · (540, −127) · (546, −114)에 둔 드럼통 3개가 쏴도 터지지 않음. 가이드 · 평면도 · 공간 검사는 이 셋을 폭발 드럼통으로 셌다.
- 원인(확인): 레이아웃 소품 `"id": "barrel"`은 야외 키트의 장식 드럼통(`PropLibrary`)으로만 지어진다. 터지는 드럼통은 코드의 `ExplosiveBarrel.Create`(옛 `StageMap.Barrel`, 파괴 배치의 `barrel`)뿐이고, 레이아웃 데이터에서 그것을 만드는 길이 없었다. 가이드 반영(`4280b44`) 때 옛 코드 드럼통을 데이터 소품으로 옮기며 장식으로 바뀜.
- 해결: 레이아웃 소품 `"id": "explosive_barrel"`을 `LayoutMap.BuildDataProps`가 `ExplosiveBarrel`로 짓게 함. G의 셋을 `explosive_barrel`로 바꿈(`apply_narrow.py small`), 길 그물 채우기의 드럼통(B 입구 2 · B 뒷길 2 · F 북쪽 길 2 · P5 1)도 같은 소품.
- 재발 방지 · 검사 경로: `LargeStageCheck` "G's three barrels are explosive"(G의 `ExplosiveBarrel` 3개). `space.py`는 `explosive_barrel`만 의미 있는 것으로 셈(장식 `barrel`은 세지 않음).
- 남은 제약: 장식 `barrel`은 그대로 남음(환경 소품). 데이터에 드럼통을 둘 때는 `explosive_barrel`을 써야 함 — [log/016-code-stage1-lanes.md](log/016-code-stage1-lanes.md).

## I-132 `LargeStageCheck` "a survivor walks up to base 2's door"가 가끔 실패

- 증상: 길 그물 반영 중 같은 dll · 같은 데이터에서 통과와 실패가 번갈아 나옴. 실패하면 걷는 사람이 P5 → P6 길목 (−8.9, 0.5)에서 22 s 만에 멈춤(7번 중 3번).
- 원인(확인): 멈춘 순간 진단(`Walk`가 멈추면 주변 몬스터와 앞쪽 광선을 출력)에서 사람형 몬스터 하나가 앞을 막고 있었다(광선이 `Enemies/humanoid`에 맞음). P5의 잠든 무리 6이 지나가는 사람 때문에 깨어 따라와 길목에 섰다. 기지 걷기 구간에서는 몬스터를 치우지 않았다. 맵 데이터가 바뀌어 무리가 서는 자리(무작위 순서)가 달라지면서 길목에 걸리는 일이 잦아졌다(추정).
- 해결: 기지마다 문으로 걷기 전에 몬스터를 치움(`ClearEnemies`) — 이 구간은 길과 문을 보는 검사이고 전투는 보지 않음. 멈춤 진단은 남김.
- 재발 방지 · 검사 경로: `LargeStageCheck` 기지 걷기, 멈추면 `stuck at … monsters […], ray hits …` 줄로 원인이 남음.
- 남은 제약: 다른 걷기 구간(출구까지 걷기 등)은 원래대로 앞에서 몬스터를 치운 뒤 걷는다 — [log/016-code-stage1-lanes.md](log/016-code-stage1-lanes.md).
