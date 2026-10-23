# Stage 2 전체 동선 환경 1차 Implementation Plan

> 실행: `superpowers:executing-plans`. 사용자가 전체 동선 적용·Blender 제작·게임 연결·실제 플레이와 렌더 검수를 승인했다.

**Goal:** 더미 바닥 대신 숲길, 협곡 시설, 컨테이너 야적장, 벙커 고지와 도착장을 읽을 수 있는 기본 환경을 만든다.

**Architecture:** `LayoutMap`의 이동 지형·높이·링크·임무를 보존하고 환경 외형을 별도 partial 파일로 구성한다. 바닥은 월드 좌표 재질, 길은 실제 지면 높이를 샘플한 메시, 외곽은 배치된 숲·암석을 사용한다. 기존 GLB를 재사용하고 충돌 크기에 맞는 컨테이너 외장과 방향 표지는 Blender로 제작한다.

**Tech Stack:** Godot 4 C#, Godot spatial shader, Blender Python / GLB.

**Spec:** [평면도](stage2-floorplan.md), [랜드마크 계약](model-briefs/stage2-landmarks.md), 사용자 2026-10-24 전체 동선 기본 배치 요청.

## 범위와 제약

- Stage2Draft 전용. 정식 Stage 2 교체, 새 전투 규칙, 보급 예산 변경은 포함하지 않는다.
- 1.2m 높이 단·램프·사다리·다리·펌프·곤돌라·세이프하우스 충돌과 E 상호작용을 유지한다.
- 자연 바닥 / 작업장 바닥 구분, 길 연결, 경계 식생, 야적장 외장과 작업 소품, 주요 갈림길 표지.
- 길 폭·소품 밀도·색은 이번 검수용 임시 아트 값. 최종 아트·성능·난이도 승인으로 표현하지 않는다.
- 높은 식생은 바깥 경계에, 통행로에는 낮은 바닥 장식. 게임 내부 고형 소품은 실제 충돌과 함께 배치한다.

## 검토 초점

1. 램프의 길 그림이 수직 단차를 관통하거나 공중에 뜨지 않는가: 지면 샘플과 창 모드 렌더.
2. 펌프 복귀·컨테이너 사다리·곤돌라 승하차의 접근 공간: 기존 전체 실제 이동 검사.
3. 바깥 식생이 플레이어·앞쪽 적을 덮는가: 기본 카메라 구역별 렌더.
4. 컨테이너 외장의 상면과 원래 지형 높이 일치: GLB 치수와 게임 높이 검사.
5. 드래프트 외 맵 및 기존 랜드마크 영향: 전용 회귀 검사와 Integration.

## Task 1: Blender 연결 자산

- [x] `prototypes/outdoor-kit/tools/stage2_environment.py`: 바닥 피벗 / Keep 계약의 `container_stack_shell`(14×2.4×12m), `route_sign`, `cliff_face` 제작, 기존 manifest 항목 보존.
- [x] Blender 실행, 원본 blend·GLB 저장, 치수/삼각형/재질 검사 및 실제 Blender 렌더.

## Task 2: 전체 동선 환경 연결

- [x] `game/scripts/World/LayoutMapEnvironment.cs`: 실제 지면을 따른 길, 외곽 식생·암석, 기존 키트 소품, 표지와 컨테이너 외장.
- [x] `game/scripts/World/LayoutMap.cs`: 환경 호출, 바닥/램프 색 교체, 더미 포스트 제거와 시작 해치 연결.
- [x] `game/shaders/stage2_ground.gdshader`, `stage2_path.gdshader`: 월드 좌표 바닥 변화와 가장자리 혼합.
- [x] `game/tests/Stage2EnvironmentCheck`: 에셋 치수·배치·접근·렌더 검수. 실제 이동은 별도 `Stage2RouteCheck`로 검증한다.
- [x] 최종 플레이에서 발견한 곤돌라 더미 배경도 기존 `OutdoorEdgeArt`/`FoliageArt`로 교체하고 월드 가로등의 숨김·복원을 검사한다.

## Task 3: 플레이·렌더·기록

- [x] 빌드·import·환경 검사·SizeDraft/Elevation/TraversalArt/LandmarkArt/LandmarkC/SafehouseArt/Bridge/RouteRegression 검사.
- [x] 창 모드 전체 경로 실제 입력·전투·임무·승하차·클리어 검사. 스크린샷 확인 후 발견한 문제 수정.
- [x] Integration, 독립 최종 리뷰, decisions/issues/feature-checklist/평면도와 본 문서에 실제 결과·제약 기록.

## 실행 기록

- 시작: `main` / `bb5ba52`, 기존 미추적 `.codex-remote-attachments/` 보존. 이번 요청에는 새 변경의 커밋·푸시가 포함되지 않아 결과를 작업 폴더에 남긴다.
- 작업 중 별도 Studio 변경(`game/HordeShooter.csproj`, `game/tests/StudioPreview.*`, `studio/`)이 나타났다. 이번 환경 작업이 만든 변경이 아니므로 수정/복구/포함하지 않는다.
- Final: fixed P2 창고 무충돌 — `Stage2EnvironmentCheck` 정비 구역 레이 `False` RED → 작업대/공구장/발전기 기존 충돌 모델로 교체 후 `True` GREEN.
- Final: fixed P2 가로등 없는 조명 — 같은 검사 `unmounted=1` RED → 배치 성공에만 조명을 생성한 뒤 `0` GREEN. `compound` 모델은 전용 충돌 배치 없이 일반 소품 도우미를 사용할 수 없게 했다.
- 추가 렌더 발견: 곤돌라 이동 배경의 큐브를 암석·소나무 군집으로 교체. `TraversalArtCheck` 군집 검사 RED→GREEN. 승강장 가로등의 모델 자식이 공중에 남는 현상은 월드 `OmniLight3D`도 숨김/복원하도록 수정하고, 조명 가시성 회귀 RED→GREEN. 태양광과 탑승자의 자식 조명은 유지한다.

## 생성 자산 / 배치

| ID | 크기 X/Y/Z (m) | 삼각형 | 용도 |
|---|---|---:|---|
| `container_stack_shell` | 14 / 2.4 / 12 | 11,968 | 4개 화물 모듈 외장. 3곳 배치, 게임 바닥1.2m에서 지붕3.6m. 지붕과 물리는 게임 소유 |
| `route_sign` | 1.7 / 2.28 / 0.55 | 136 | 로컬 +X 방향 화살표, 경로별 목적지로 회전. 게임에서 기둥 충돌 추가 |
| `cliff_face` | 6 / 2.4 / 0.1028 | 48 | 높이 단 경계의 암벽 부조. 로컬 +Z로 최대0.14m, 겹치는 상면 없음 |

모든 GLB는 `Keep`, 바닥 피벗, Y-up, 이미지 텍스처 없는 PBR 재질, 모델당 6재질 이하를 사용한다. 파일은 `game/assets/stage2-props/`, 원본은 `prototypes/outdoor-kit/source/stage2-environment.blend`, 생성기는 `prototypes/outdoor-kit/tools/stage2_environment.py`다. 팩의 기존15개 항목은 유지한다.

이번 실행 측정: 기존 랜드마크 포함 kit113개, 암벽59개·바깥 암석158개·풀284개를 인스턴싱, 길10,916삼각형. 바깥 나무는32m 공간 묶음으로 인스턴싱한다. 정비 구역·고정 표지 이외 일반 소품은 경로 여유/링크/보급/표식/평탄성 검사에 통과한 경우만 배치한다.

## 검증 결과 — 2026-10-24

Windows / Godot4.7.2 .NET / Forward+ / RTX5070Ti / 1440×900. Blender5.2.2 LTS로 생성·Cycles 렌더. 최종 Blender 생성 종료0, 경고 없음. 빌드 경고0·오류0, import 완료.

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---:|---|
| Stage2EnvironmentCheck | headless13 / 창31, 구성 렌더18장 | `environment-Stage2EnvironmentCheck.log`, `environment-render.log` |
| SizeDraftCheck | 74 | `environment-SizeDraftCheck.log` |
| ElevationCheck | 76 | `environment-ElevationCheck.log` |
| TraversalArtCheck | headless152 / 창166, 6인 승하차·가림·숨김/복원, 렌더14장 | `environment-TraversalArtCheck.log`, `environment-gondola-render.log` |
| LandmarkArtCheck / LandmarkCArtCheck / SafehouseArtCheck | 63 / 56 / 54 | `environment-LandmarkArtCheck.log`, `environment-LandmarkCArtCheck.log`, `environment-SafehouseArtCheck.log` |
| Stage2BridgeCheck / Stage2RouteRegression | 5개 실제 이동 사례 / 10 | `environment-Stage2BridgeCheck.log`, `environment-Stage2RouteRegression.log` |
| StageTransitionCheck | 194, 팩65개 자산 검사 포함 | `environment-StageTransitionCheck.log` |
| Integration | 149, fixed-fps 없이 실행 | `environment-Integration.log` |
| Stage2RouteCheck | 창171, 연속 실제 입력 플레이·렌더45장 | `environment-full-route-final.log` |

연속 검사는 플레이어 순간이동·임무 강제완료·적 삭제 없이 실제 이동/사격과 E 입력으로 시작→감시탑/짚라인→펌프50초→복귀/다리→감시소→컨테이너3곳 왕복→트럭/정비 분기→고지→곤돌라12초→세이프하우스 문 닫기를 검사한다. 무적 싱글 자동 조작이므로 생존 난이도·인간의 길찾기 성공률 측정은 아니다. 구성 검사18장은 구도 확인을 위한 순간이동을 사용하며 연속 플레이와 구분한다.

최종 배경/조명 수정 후 연속 실행: **357.6 시뮬레이션초, 195처치, 최대61적, 1,157발, 사다리8회·짚라인1회**,171검사·45캡처 통과. 이 실행과 최종 환경/이동장치 창 렌더에는 오류/종료 경고가 없었다. 독립 리뷰의 P2 두 건은 모두 수정했고 보류한 리뷰 항목은 없다.

SizeDraft 측정은 주 경로620m, 순수 걷기2.3분, 펌프 복귀22초, 전체 비전투2.6분이다. 환경 포함 생성2,898ms와 흐름장1.0~1.7ms 평균은 이 PC의 한 실행값이며 일반 성능 보장이 아니다.

검사 실패는 모두 수정 후 재검증했다. 일부 headless 장면 종료에 기존 I-17 계열 `ObjectDB instances were leaked at exit` 경고가 남는다(SizeDraft10, Landmark8, LandmarkC4, Safehouse8, RouteRegression12, StageTransition6, Integration2인 실행). 렌더/실제 플레이 성공과 종료 경고 해결을 구분한다. 해당 경고의 개수는 실행별로 변동한다.

## 재현

저장소 루트 PowerShell에서:

```powershell
dotnet build .\game\HordeShooter.csproj --nologo -v q
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python .\prototypes\outdoor-kit\tools\stage2_environment.py
& 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --path game --import
.\game\Start-Game.ps1 -Stage2Draft
```

구성/플레이 재현은 Godot에 `--path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2EnvironmentCheck.tscn` 또는 `res://tests/Stage2RouteCheck.tscn`을 전달한다. `TraversalArtCheck.tscn`은6인 탑승/도착과 별도 렌더를 검사한다. 전용 scene 검사는 `Start-Game.ps1 -Test`에 포함되지 않는다.

Blender 렌더 `game/output/stage2-environment-blender.png`, 게임 구성 `environment-01-start.png`~`environment-18-arrival.png`, 전체 플레이 `route-*.png`, 곤돌라 `traversal-gondola-riding.png`는 로컬 재생성 산출물이다. 위 마지막 로그의 `PASS: render` 목록이 그 실행의 정확한45장 목록이며 폴더에 남은 이전 실패 캡처와 섞어 세지 않는다.

## 남은 범위

최종 아트/텍스처/날씨 반응, 모든 자산 가림, 멀티2PC·저사양 성능·생존 난이도·사람의 길찾기 체감은 이번 완료 범위가 아니다. 기존 그리드 지형의 각진 외곽과 넓은 전투 여백은 유지한다. 정식 Stage 2 교체 없이 `-Stage2Draft`로 테스트하며, I-116 원인 미확정 기록은 이 작업으로 닫지 않는다.
