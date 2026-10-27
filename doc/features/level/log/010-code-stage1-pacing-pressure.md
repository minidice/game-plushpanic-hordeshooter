# 010 · code · Stage 1 유입 · 압박 반영(큰 구역 2분, 두 방향 웨이브, 드럼통 관문, 샛길 벽 보상)

- 작성일: 2026-10-27. 종류: code(감독 · 인원 보정 · 부서지는 벽 · 맵 데이터 · 검사). 관련: **L-31 · L-19 갱신**, **M-34(새)**, L-33, D-15, [유입 · 압박 설계안](../pacing-pressure-plan.md), 앞 기록 [009](009-doc-pacing-pressure-research.md).
- 사용자: "큰 구역 2분으로 확정하고 스테이지는 10~12분으로 줄여줘(L-31 · L-19 변경). AI는 0.5명으로 세고, 셔터 우회 확인부터 설계안 순서대로 진행해줘."

## 변경 사항

- **결정 기록**: L-31 큰 구역 약 2분 · 작은 구역 30초 ~ 1분, L-19 스테이지 10 ~ 12분(「바뀐 결정」에 이전 값 · 이유, L-22 캠페인 1 약 80분 → 약 40 ~ 48분), M-34 AI 동료 0.5명. [레벨 디자인 규칙](../../../level-design-rules.md) 구역 표 · 순수 걷기 줄, [시나리오 상세](../../../scenario-level-design.md) 스테이지 시간 줄.
- **0. 셔터 우회 확인**: `LargeStageCheck`에 "임무 문이 닫히면 출발점에서 세이프하우스로 갈 수 없음"(Stage 1 · 2 · 3) — 모두 통과. 걸어서 돌아가는 길은 없다.
- **AI 0.5명(M-34)**: `Stage.PartySize`가 소수(사람 1 + AI 동료 0.5씩), `PartyScaling.For(float)`가 표 사이를 보간(1.5명 = 일반 등장 60% · 체력 85%). `party_scaling.csv` 설명 줄, `PartyScaleCheck` 갱신.
- **유입(감독)** `HordeDirector`:
  - 웨이브 하나를 **두 입구**로 나눔 — 진행상 뒤쪽 입구(경로 거리로 판단, 65%)와 지도자 반대편 입구. 한 입구에서는 **4 ~ 6마리씩 3 ~ 5초 간격**(시간 무리). 경보 · 크레센도 웨이브는 이미 몇 초마다 오므로 두 입구로 나누되 한 번에(`trickle: false`)
  - 시간 무리 규칙을 데이터로: `mob_count_min/max`(기본 8 ~ 12), `rest_min/max_s`(정점 뒤 쉼, 기본 10), `first_in_area_min/max_s`(새 습격 구역에 들어가면 첫 무리, 기본 끔)
  - Stage 1: 습격 구역 **B 거리 · D 창고 · F 야적장**, 45 ~ 75초, **12 ~ 20마리**(4인 기준), 쉼 25 ~ 40초, 들어가면 20 ~ 35초 안에 첫 무리, 시작 대기 25초
- **배회**: 주 경로 위 경계형 3마리 무리 6개(B · D · F 둘씩, 6 × 6 m).
- **드럼통 관문 · 샛길 벽**: `destruction_placements.json`에 큰 Stage 1 줄(`stage1_large`)과 새 키 `gate`(부서질 때까지 길을 막음 — 지금 걸을 수 있는 흐름에만 문처럼 찍고, 경로 흐름은 열린 것으로 봄) · `panels`(옆 판 위치 목록, 옛 `plain_panel`은 `[1]`).
  - C 골목 입구(x 152): 가운데 금 간 벽 + 양옆 판 4 + 드럼통(B 쪽 1.5 m), 따뜻한 조명. 쏘면 터져 길이 열리고 폭발 소리가 골목을 깨움
  - G 진입로 남쪽 새 구석 `ramp_nook`(1단, 4.8 × 8 m): 금 간 벽 + 드럼통, 뒤에 **베넬리 M4 · 파편 수류탄 · 구급상자**(보급 `ramp_nook`), 따뜻한 조명
- **가이드(설계안)**: B 새 경험에 시간 무리(새 4개 → W1 경고, 이유를 메모에), C 새 경험 `breach_wall`, D · F는 시간 무리 다시 쓰기, G는 `breach_wall` 다시 쓰기(샛길 보상). 경험 목록의 시간 습격 설명. 보드 · 평면도 3장 다시 그림.
- **검사 추가 · 수정**: `LargeStageCheck`(습격 규칙, 16마리 웨이브가 반대편 두 입구로 나뉘고 일부가 몇 초 뒤 나옴, 관문이 임무까지의 길을 막고 드럼통으로 열림, 셔터 우회 없음, 보급 · 높은 곳 도달 검사는 관문을 연 것으로), `DestructionPlacementCheck`(큰 Stage 1 두 줄 · 판 여러 개), `DestructionCompatibilityCheck`(첫 줄 방향 바꾸기 시험이 90 → 0도 되게), `LargeStageCapture` 장면 2개(`c-alley-gate` · `g-ramp-nook`).

## 검사

- 통과(헤드리스, 종료 0, 실패 0 · 누수 0): `LargeStageCheck` 140, `DestructionPlacementCheck` 261, `SpawnCheck` 41, `PartyScaleCheck` 25, `CombatPacingCheck` 44, `SizeDraftCheck` 76, `LandmarkCArtCheck` 56, `NetSyncCheck` 32, `NetCodecCheck` 12, `LobbyCheck` 31, `SupplyCheck` 12, `ModeCheck` 147, `SquadCheck` 51, `MapCheck` 24, `StageTransitionCheck` 217, `DestructionCompatibilityCheck` 14, `CommonRosterCheck` 264, `Integration` 149. `guide.py check` 오류 0 · 경고 5(W1 B, W2 넷), 종료 0.
- 실패 → 고침:
  - `SpawnCheck` "hunter waves come while it sounds": 경보 웨이브까지 몇 마리씩 나눠 나와 2초 안에 6마리가 안 됨 → 경보 · 크레센도는 한 번에.
  - `StageTransitionCheck` · `ModeCheck`: 데이터만 새것이고 빌드가 옛것인 상태로 돈 첫 실행에서 실패(옛 코드가 `stage1_large` 줄을 거부) — 새 빌드로 다시 돌려 통과. `ModeCheck`의 `'?'` 실패도 같은 실행이었고 다시 돌리니 통과(원인 확정 못 함, 생존자 투표 카드가 무작위).
  - `DestructionCompatibilityCheck` "wall orientation": 첫 줄이 이미 90도인 관문이라 바꿔도 같은 값 → 시험이 방향을 뒤집게 고침.
  - `LargeStageCheck` 첫 빌드 오류(지역 변수 이름 `gate` 겹침) → 이름 바꿈.
- 렌더(창 모드 `LargeStageCapture`, 종료 0): C 골목 입구 바리케이드(콘크리트 판 줄 · 가운데 금 간 판 · 빨간 드럼통 · 조명), G 구석(진입로 아래 1단 구석, 금 간 벽 · 드럼통, 안에 산탄총 · 수류탄 · 구급상자). 사진은 로컬 `game/output/`(커밋 안 함).

## 남은 제약

- **플레이로 다시 재야 함**: 큰 구역이 실제로 약 2분인지, "위험한 순간"이 생기는지(1 + AI, 4인 봇). 수치는 모두 시작값.
- 사용자가 잰 1분 30초는 설명하지 못했다(걸어서 우회하는 길은 없음 확인).
- 사용자가 본 "벽 위의 무언가"는 아직 어느 것인지 모름.
- 관문 앞 드럼통을 미리 터뜨려도 벽은 함께 무너진다(반경 4.6 m 안). 드럼통 없이도 수류탄 등 폭발물로 부술 수 있다. 총으로는 안 부서진다.
- 웨이브 두 입구 · 나눠 나오기는 모든 스테이지 감독에 적용(습격 수치 · 구역은 Stage 1만 바꿈).
