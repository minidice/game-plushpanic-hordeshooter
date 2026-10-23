# Stage 2 이동 장치 품질 보완 — 2026-10-24

사용자 요청: 6인 곤돌라 여유 공간과 도착 전 구르기 이탈 금지, 짚라인 가속·매달리기·뛰어내리기 자세, 사다리 하강 방향과 굵은 적갈색 외형, 모든 나무 로딩 경로의 신형 모델 교체 및 구형 제거.

## 구현 기준

- 곤돌라 바닥 6×5m, 양옆 출입구 2.4m. 출발 시 실제 탑승자 목록과 위치를 저장하고 이동·구르기·입력 예약을 막는다. 도착/중단 시 잠금을 해제한다. 전원 탑승·12초·배경 이동 규칙은 유지한다.
- 짚라인은 시작 5m/s, 가속 12m/s², 최대 24m/s. 마지막3% 구간에서 손을 놓고0.45초 동안 하차한다. 끝 기둥을 관통하지 않도록 옆1.25m의 같은 높이/충돌 없는 바닥을 우선 확인하며 반대쪽도 막히면 기존 끝점으로 착지한다. 시간/경로 비용은 같은 식을 사용한다. 매달린 팔과 접은 다리, 하차 시 펼친 팔·착지 웅크림을 적용하고 정상 종료/텔레포트/다운 때 복구한다. 이동 장치 진입 시 조준 확대도 초기화한다. 수치는 체감 확인용 임시값이다.
- 사다리는 상하행 모두 사다리를 향한다. 섹션 피치 1.2m 유지, 폭 0.95m와 굵은 적갈색 레일로 식별성을 높인다.
- 나무는 특정 맵의 런타임 대체에 의존하지 않도록 원본 GLB부터 교체한다. 상세 작업은 [나무 교체](tree-asset-replacement.md), 모델은 [이동 장치 모델](stage2-traversal-model-quality.md)에 기록한다.

## 검증 범위

전용 검사에서 하강 방향, 가속 변화/거리/시간, 네 캐릭터의 탑승·하차 자세와 종료 복구, 6인 곤돌라 및 구르기/항상 처리 입력/중단 복구를 확인한다. 묶음 A, 높이 단, 전체 동선과 통합 검사를 실행하고 실제 게임 렌더 및 Blender 모델 렌더를 확인한다. 아래에 실제 실행 결과를 기록한다.


## 실행 결과

Windows / Godot4.7.2 .NET Forward+ / Blender5.2.2. 최종 C# 빌드 오류0·경고0.

| 검사 | 결과 | 확인 범위 |
|---|---|---|
| `TraversalQualityCheck` | 헤드리스55, 창81검사·26렌더 통과 | 상하행 방향, 실제 조준 후 진입, 가속 이동 거리,4종 캐릭터 자세/하차/텔레포트 복구,6인 Always 입력/구르기 차단, 정상 도착/중단, 다운 자세 |
| `TraversalArtCheck` | 창166검사·14렌더 통과 | 실제 조작으로6인 출입, GLB 출입구 삼각형/가림/복원, 배경·조명 숨김, 도착/해제 |
| `Stage2RouteCheck` | 창171검사·45렌더 통과 | 순간이동·임무 건너뛰기 없이 시작→양갈래→펌프50초→다리→야적장→곤돌라→세이프하우스.367.1초, 사다리8회·짚라인1회·194처치·최대57적·1261발 |
| `ElevationCheck` |76통과 | 사다리·짚라인·분대원 흐름장·높이 단 이동 |
| `StageTransitionCheck` |190통과 | 총61자산(구형 edge4종 삭제), 팩/단계 전환 |
| `Stage2EnvironmentCheck` / `LandmarkCArtCheck` / `SizeDraftCheck` |13 /56 /74통과 | 배치·높이·이동 경로·모델 계약 |
| `Stage2BridgeCheck` / `Stage2RouteRegression` |5경로 /10통과 | 과거 다리 실패 좌표·전투 통과, 검수 입력 회귀 |
| `Integration` |149통과 | 실제 시간 오디오 포함 기본 통합 |
| 나무 / Blender A |35+80 /120검사·13렌더 | 각 상세 문서의 실제 자산·기하·색·원본·도감 검사 |

### 실패를 먼저 확인한 항목

- `output/quality-red.log`: 사다리 하강 방향 실패→같은 검사 통과.
- `output/quality-aim-red.log`: 실제 조준 후 사다리 진입 시 AimBlend 유지 실패→진입 때 초기화 후 통과.
- `output/quality-downed-red.log`: 새 곤돌라 잠금에서 다운 자세 소실 실패→기존 다운 자세 보존 후 통과.
- 하차 위치를 끝 기둥 옆으로 바꾼 뒤 `LandmarkCArtCheck`의 기존 끝점0.2m 검사가 실패했다. 검사를 새 착지점+기존 하단 높이 계약으로 갱신하고56검사 통과를 확인했다.
- 나무 구형 직접 로드·Godot 정점색 플래그, 사다리 삽입부 표면 중복은 각 자산 문서에 실패/수정/재검증 기록이 있다.
- `Stage2RouteRegressionCheck.tscn`은 이름을 잘못 입력해 실행 실패했다. 실제 장면 `Stage2RouteRegression.tscn`으로 정정해10검사 통과했으며 잘못된 실행을 통과로 세지 않았다.

### 재실행

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
# Godot .NET 실행파일에 다음 인수(창 모드):
# --path game --resolution 1440x900 --fixed-fps 60 res://tests/TraversalQualityCheck.tscn
# --path game --resolution 1440x900 --fixed-fps 60 res://tests/TraversalArtCheck.tscn
# --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2RouteCheck.tscn
# Integration은 --fixed-fps 없이 실행. 그 외 전용 검사는 --headless 추가 가능.
```

로컬 로그: `game/output/quality-*.log`. 화면: `quality-ladder-*`, `quality-*-zip-*`(기본 카메라+손/발 근접 카메라), `quality-gondola-*`, `tree-assets-revised.png`, `traversal-*`, `route-*`. 생성 output은 커밋하지 않는다.

## 경고·남은 범위

- 최종 창 모드 이동/전체 동선 검사에는 ERROR/WARNING이 없다. 일부 헤드리스 종료에서 ObjectDB 잔여 경고(I-17)를 확인했다(품질55검사6개, 높이 단4개, 초기 단계전환6개, 시안6개, 다리15개, 경로회귀12개). 검사 실패와 별도로 기록하며 해결했다고 주장하지 않는다. Blender의 기존 재질 API 폐기 예정/브러시 경로 경고는 자산 문서 참조.
- 전체 동선은 자동 입력·1인 무적 플레이이고,6인 검사는 로컬 생존자들이다. 사람이 느끼는 최종 가속감/난이도 승인이나 실제2PC 이동 상태 동기화를 뜻하지 않는다. 연출 전용 카메라·최종 협곡 미술은 후속이다.
- I-116의 과거 다리 정지 원인 미확정 이력은 유지한다. 이번 연속 플레이와 전용5경로에서 재현되지 않았다는 결과만 기록한다.
- 기존 환경 배치와 병행 Studio 작업은 보존했다. 이 요청에서 커밋/푸시는 실행하지 않았다.
