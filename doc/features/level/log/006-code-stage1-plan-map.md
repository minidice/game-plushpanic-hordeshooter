# 006 · code · Stage 1 설계안을 맵에 반영 (L-33)

- 작성일: 2026-10-27. 종류: code(맵 데이터 · 게임 코드 · 검사 · 모델 지시문). 관련: L-33, [가이드 맵 7.3](../guide-map.md#73-맵에-반영할-변경), [설계안 기록 005](005-doc-stage1-plan-guide.md).
- 요청: "확정된 Stage 1 설계안의 맵 변경 8개를 반영해줘(랜드마크 모델은 Codex 지시문으로)."
- 작업 위치: main 폴더에 다른 작업의 Studio 서버(Godot 렌더러 포함)가 실행 중이라 별도 worktree(`work/stage1-plan-map`)에서 작업. 앞선 리서치 · 가이드 맵 · 설계안의 커밋 전 변경도 같은 worktree로 옮기고 main 폴더에서는 지움(모두 이 작업의 파일).

## 변경 사항

맵 데이터(`stage1_large_layout.json` · `stage1_large_encounters.json`, 형식은 원래 들여쓰기 그대로):

1. 경보 차 B (64, −6) → C 골목 **(167, −55)**.
2. 특수 적 B · D · F 3마리 → 고가 위 **(572, −130)** 경계형 1마리(D에도 잠든 특수 적이 있어 셋 다 뺌).
3. 시간 습격 `raids.areas` → **야적장만**.
4. 드럼통 F 5 · D 2 → G 오르막 3개 (530, −111) · (540, −127) · (546, −114).
5. D 제어판 진지: 컨테이너 2(창고 벽에 붙여 세로) · 모래자루 3 · 방벽 1 · 모래자루 1로 입구 하나(약 7 ~ 9 m), 임무 경로를 입구 쪽으로.
6. F 경보 트럭 **(492, −99)** `alarm_truck`.
7. 길 안내 조명 9개(세이프룸 문 · 약국 간판 초록 · 광장 출구 · 골목 끝 · 제어판 위 · 초소 따뜻한 색 · 야적장 출구 · 고가 오르막 · 세이프하우스 문 따뜻한 색).
8. 창고 간판 랜드마크: 모델 지시문 [stage1-guide-landmark.md](../../../model-briefs/stage1-guide-landmark.md)(Codex, `warehouse_sign`, 글자 없는 7 m 조명 간판, 기존 키트 manifest 대신 `manifest_guide.json`) — 배치는 모델이 나온 뒤.

게임 코드:

- `StageLayout`: `lamps` 항목이 `[x, z]`(지금 가로등) 또는 `{pos, color, energy, range, post}`(길 안내 조명) — 새 `LampSpot`. `LayoutMap`이 색 · 밝기 · 범위대로 짓고 `post: false`면 기둥 없이 빛만(`GuideLight`).
- 경보 장치 여러 개: `StageMap.AlarmDevices`(기본은 `AlarmDevice` 하나), `LayoutMap`은 `noise` 표식마다 하나씩 짓고 첫 번째가 `AlarmDevice`. `StageNet`은 문 비트 뒤에 경보 장치마다 비트 하나(한 바이트라 문 + 경보 8개까지 — 주석). 경보가 울리면 웨이브를 부르는 쪽은 원래 `Stage.AlarmCars`로 여러 개를 다뤘다.
- 검사: `LargeStageCheck`에 Stage 1 경보 장치 2개(골목 · 야적장)가 모두 `AlarmCars`에 있고 첫 번째가 `AlarmDevice`인지 1개. `LargeStageCapture`에 장면 3개(`c-alarm-car` · `d-panel-pocket` · `f-alarm-truck`).

문서: 가이드 맵 7.3 표(반영 내용 · 좌표), 설계안 가이드 `applied` · B 메모, 현재 상태 가이드 설명("변경 전 맵"), L-33 갱신 줄, [큰 스테이지](../../../large-stages.md) Stage 1 구역 설명, 체크리스트, 평면도 `stage1-large-floorplan.svg` · `stage1-large-guide-plan-floor.svg` · 보드 `stage1-large-guide-plan.svg` 다시 그림.

## 검사

- C# 빌드 경고 · 오류 0.
- `LargeStageCheck` — 처음 실행 **실패**: 드럼통 (537, −131)이 숨은 등장 지점 (537, −130.5)과 겹침 → (540, −127)로 옮겨 통과. 경보 장치 위치 조정 · 새 검사 항목 뒤 **122 통과**(한 판 진행 · 임무 · 경로 · 등장 지점 포함).
- 헤드리스: `MapCheck` 24 · `NetSyncCheck` 32 · `NetCodecCheck` 12 · `LobbyCheck` 31 · `SupplyCheck` 12 · `StageTransitionCheck` 215 · `CombatPacingCheck` 44 · `ModeCheck` 147 · `SquadCheck` 51 · **`Integration` 149** 통과, 누수 출력 0.
- `Stage2RouteCheck` **실패 2회**(1회: 컨테이너 위 (333.6, −77.2) 끼임, 2회: 펌프장 출구 (199.7, −11.9) 막힘, 적 34마리). 이 검사는 예전 Stage 2 맵(`StageTwoMap`)이라 이번 Stage 1 데이터 · 가로등 코드를 거치지 않는다. [001 기록](001-code-large-stages.md)에 **변경 전에도 5번 중 4번 같은 두 메시지로 실패**한 흔들림으로 남아 있다(이슈 ID 미발급). 이번에 고치지 않음.
- 가이드 검사(설계안): 오류 0 · 경고 5(W2, 그대로).
- 렌더(창 모드 `LargeStageCapture`): 변경 전 11장 · 변경 후 14장을 나란히 확인 — 골목 길가 경보 차, D 제어판 진지(남쪽 입구), F 드럼통 빠짐 · 경보 트럭, G 드럼통, 세이프룸 문 조명. 사진은 로컬 `game/output/large-stage1-*.png`(커밋 안 함).

## 남은 제약

- **조명**: Stage 1 렌더가 밝은 편이라 길 안내 조명 차이가 작게 보인다. 어두운 기조(분위기 D안)를 적용한 뒤 밝기를 다시 맞춘다.
- 창고 간판 모델(Codex) 대기 — 그전까지 B의 랜드마크 길 안내는 없다.
- 경보 장치 여러 개의 멀티 동기화 비트는 코드로만 확인했다(실제 2 PC 미확인). `NetSyncCheck`는 경보 하나인 맵으로 통과.
- 진지 입구 폭 · 경보 트럭 거리 · 특수 적 위치는 시작값 — 손 플레이로 확인 필요.
- `Stage2RouteCheck` 흔들림에 이슈 ID가 아직 없다.
