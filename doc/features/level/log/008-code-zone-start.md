# 008 · code · 구역 테스트 시작 옵션 `-Zone` (L-33)

- 작성일: 2026-10-27. 종류: code(실행 옵션 · 스테이지 시작 · 검사). 관련: L-33(가이드 맵), L-30(큰 캠페인 실행 옵션), [가이드 맵 9장](../guide-map.md#9-구역-테스트).
- 요청: "구역 시작 옵션(-Zone D 같은 걸로 그 구역 입구에서 시작, 그 구역 무리는 그대로) 만들어줘".

## 변경 사항

- **실행 옵션**: `Start-Game.ps1 -Zone D`(Stage 1) · `-Zone 2:D`(Stage 2) → 메뉴 없이 `res://scenes/Stage1.tscn`을 `--large-stages --zone=…`로 연다. `GameSession.StartZone` · `StartZoneStage`가 `--zone=`을 읽고, 옵션만으로도 큰 캠페인(`LargeCampaign`)이 되며 시작 스테이지를 그 스테이지로 둔다(메뉴의 `StartCampaign`에서도 같음).
- **시작 자리** `LayoutMap.ZoneEntry` · `ZoneEntryOf`: 주 경로(`routes.main`), 없으면 다른 경로를 0.5 m 간격으로 걸어 구역에 들어간 뒤 3 m 안쪽의 막히지 않은 바닥. 경로가 지나지 않는 구역(Stage 3 `B-tower`)은 구역 가운데에 가장 가까운 빈 바닥. 구역 ID는 대소문자 무시.
- **스테이지 시작** `Stage`: `StartZoneOverride`(검사용, null이면 실행 중인 게임 장면의 옵션)로 플레이어 · 카메라 · 동료를 그 자리에 둔다. 세이프룸 목표였다면 `LeaveSafeRoom()`을 불러 문을 열고 다음 목표 · 몹 감독 시작으로 넘어간다. 무리 · 특수 적 · 경보 · 보급은 맵 그대로(지나온 구역 무리도 남음). 해치로 도착한 다음 스테이지에는 적용하지 않는다. 모르는 ID면 경고 후 세이프룸 시작. `StartedZone`에 실제 시작 구역.
- **검사** `LargeStageCheck` +7: `--zone=2:D`가 큰 캠페인 Stage 2로 시작, 세 스테이지 모든 구역에 시작 자리(구역 안 · 빈 바닥), `-Zone d`로 D 입구 · 열린 세이프룸 문 · 목표 `ReachShutter`, 모르는 구역은 세이프룸 시작.
- **앞 커밋 검사 수정**: [007](007-code-stage1-warehouse-sign.md)에서 등록한 `warehouse_sign` 때문에 `StageTransitionCheck`의 등록 소품 수 검사(76)가 깨져 있었다 — 007에서 이 검사를 돌리지 않아 놓쳤다. 77(+ 길 안내 간판 1)로 고침.
- 문서: [가이드 맵](../guide-map.md) 9장(사용법 · 시작 규칙 · 확인할 것), [큰 스테이지](../../../large-stages.md) 실행 옵션 줄, 체크리스트 1줄. 새 결정 없음 — 개발용 실행 옵션이고 게임 규칙은 바뀌지 않는다.

## 검사

- 통과(헤드리스, 종료 0, 실패 0 · 누수 0): `dotnet build` 오류 0, `LargeStageCheck` `LARGE_STAGE_PASS 130`, `MapCheck` 24, `StageTransitionCheck` 217(소품 수 고친 뒤), `SquadCheck` 51, `ModeCheck` 147, `SurfaceAudioCheck` 243, `Integration` 149.
- 실패 → 고침: `StageTransitionCheck` 첫 실행 `FAIL … 9 large stage kit are listed (77)` — 위 007의 간판 때문, 고친 뒤 통과. `LargeStageCheck` 첫 실행 `Stage 3: … (none: B-tower)` — 경로 밖 구역 대체 자리를 넣어 통과.
- 실제 게임 장면 렌더(창 모드 `Stage1.tscn --write-movie`, 150프레임, 종료 0): `--zone=D` — Stage 1 D 입구(골목 출구, 창고 간판 아래)에서 거위 · 동료 오리, 목표 "창고 단지 북쪽 셔터 제어판으로 이동". `--zone=2:D` — Stage 2 D 앞 숲길, 목표 "발전기 단의 제어판으로 벙커 철문 열기". 첫 `2:D` 녹화는 메뉴를 건너뛰어 Stage 1 세이프룸으로 떨어졌고, 옵션이 시작 스테이지를 직접 정하게 고친 뒤 다시 확인. 프레임은 로컬 임시 폴더(커밋 안 함).
- 실행하지 않음: `Start-Game.ps1 -Zone D` 스크립트 자체(같은 인자로 같은 장면을 직접 실행해 확인), 사람 손 플레이.

## 남은 제약

- 동료 수 · 캐릭터는 기본값(메뉴를 건너뜀). 혼자 플레이만.
- 지나온 구역의 무리가 남아 있어 뒤로 가면 만난다(요청대로 맵 그대로).
- 구역 끝이나 특정 장치 앞에서 시작하는 옵션은 없다(구역 입구만).
