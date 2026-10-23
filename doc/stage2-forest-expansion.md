# Stage 2 숲 전체 확대 Implementation Plan

> 지형 경계 후속: 경사로 곡면/연속 높이 조회에 맞춘 풀 평탄도 판정으로 현재 풀24,813묶음이다(나무109/공간 묶음467 유지). 이전 수량/성능은 아래 이력이며 최신 [지형 경계 검수](stage2-terrain-edges.md)를 따른다.

> 후속2026-10-24: 전체 범위·위치·풀 배치는 유지하며 활엽수 형태를 보완했다. 현재 나무4종/풀2종과 검증은 [수관 형태 보완](stage2-tree-shapes.md)을 따른다. 아래4종 재사용과 성능 표는 확대 당시 이력이다.

> 실행: writing-plans / executing-plans. 기존20×20m 스타일을 숲 전체와 협곡·야적장 경계로 확대하라는 사용자2026-10-24 승인에 따라 진행한다.

**Goal:** 숲 저지대·중간 단·고지대·감시탑 분기와 협곡 양쪽 둑에 황록색 숲을 연결하고, 길/적의 식별성과 실제 렌더 비용을 확인한다.

**Architecture:** 기존 Blender4종을 재사용한다. LayoutMap의 식생/바닥/조명 마스크를 전체 숲으로 확장하되20×20m 비교 모드를 남긴다. 모든 경로·경사로·상호작용 주변을 비우고 공간별 MultiMesh와 거리 제한으로 풀 표시 비용을 제한한다. 협곡에서는 건조한 바위/흙으로, 동쪽 둑에서는 야적장 자갈로 넓게 전환한다.

**Tech Stack:** Godot4.7.2 C# / spatial shader / 기존 Blender GLB.

**Spec:** 사용자 최신 요청, [기준 구역](stage2-forest-quality.md), [평면도](stage2-floorplan.md), A-25/L-15/L-29.

## 선택과 제약

- 시작 공터 끝 X32부터 숲 저/중/고지·서쪽 둑 X185까지, 동쪽 둑 X215..255와 야적장 입구 X282까지 연결. 바위 협곡 바닥과 감시탑/건물 상판은 식생으로 덮지 않는다.
- 풀 중심 경로 여유2.3m, 새 줄기 최소4.8m. 링크 입출구·임무/보급·건물/소품 주변 별도 제외. 기존 높이·임무·링크·보급 데이터 유지.
- 기존 자산 재사용, 새 모델 추가 없음. 정식 StageTwoMap이 아닌 -Stage2Draft에 적용.
- 기존 main 작업 폴더와 미커밋 작업 유지. 이 요청에 커밋/푸시는 포함하지 않는다.
- 밀도/전환 폭/표시 거리는 구현 선택인 임시 아트 값. 최종 저사양/다인 성능으로 단정하지 않는다.

## 실행 계획 / 기록

- [x] 1. `ForestExpansionCheck`로 전체 숲 모델 분포 기대 실패 확인. `ForestPerformance`로 현재20×20m의 같은 카메라 기준 렌더/프레임/그리기 비용 측정.
- [x] 2. `LayoutMapForest.cs` 배치/범위·공간 묶음, `LayoutMapEnvironment.cs` 외곽/기존 식생 선택, `ForestStudyLight.cs` 전환, `forest_patch.gdshaderinc` 경로 기반 흙/이끼 연결. 전용 검사 통과.
- [x] 3. 기본/확대·경계·전투 렌더 검사와 순간이동 없는 `Stage2RouteCheck` 전체 플레이. 같은 조건 성능 비교 후 필요 시 밀도/그림자/묶음 조정.
- [x] 4. 최종 독립 리뷰, 관련 회귀 검사, 결정·이슈·체크리스트·평면도 갱신과 남은 성능 한계 기록.

## Review Focus

경사로/분기/보급과 나무 충돌, 감시소 내부/감시탑 상판의 식생, 협곡/야적장 경계 단절, 거리 표시 전환의 풀 소실, 구역 밖 조명 복원과 원경 식생 비용을 검토한다. 성능은 실제 Forward+ 창·VSync 해제·고정 FPS 없이 측정하며 프레임 제한이나 다른 프로세스 영향이 있으면 구분한다.

## 현재 구현

- 새 나무109그루·풀24,717묶음,467개 공간 MultiMesh. 이는 전체 배치 수이며 한 화면이 전부 그리는 수가 아니다. 표시는58m/8m 페이드, 같은 풀 종류는 메시/재질을 공유한다. 풀 높이는 배치 배율까지0.6m 미만이며 충돌 없음. 수관은 기존 플레이어 가림 완화를 사용하고 줄기는 물리/흐름장에 등록한다.
- 저지대·중간 단·고지대·서쪽/동쪽 둑에서 새 모델 분포를 검사했다. 외곽 비통행 숲도 같은 Blender 수관/줄기 메시를 합쳐 인스턴싱한다. 앞쪽 낮은 관목과 숲 밖 기존 식생은 유지한다.
- 바닥은 별도 겹치는 판 없이 기존 셰이더에서 흙/이끼를 혼합한다. 실제 Layout.Routes와 첫3개 경사로 선분을 전달해 분기 길까지 연결한다. 협곡 중앙은 건조한 기존 바위 바닥이며 야적장에는 식생이 감소한다.
- 기존 태양/환경광 보간 값은20×20m의 스타일을 유지하며 진입 범위만 넓혔다. 야적장 X282 이후에는 원래 조명을 복원한다. 하나의 태양을 시야 기준으로 바꾸는 방식이며 공간별로 독립적인 햇빛이 생기는 기능은 아니다.
- `ForestExpanded=false`는 기존20×20m 비교 모드, `ForestStudyEnabled=false`는 새 식생/조명 비활성화. 일반 플레이는 두 값 모두true. 최초 기준 구역의 재현 검사는 `ForestQualityCheck`로 유지했다.

## 발견·수정·판단

1. 초기 전체 분포 기대 검사는 나무5그루로 실패했다(`forest-expand-red.log`). 확대 후 숨은 적 등장 지점(76,−40)을 새 나무가 막았다. 실제 `EncounterErrors` 실패(`forest-expand-spawn-red.log`)를 확인하고 적 등장/특수 적 대기 위치3m를 제외해 해결했다.
2. 독립 리뷰에서 컨테이너 상판 모서리에 풀이 생기는 경로를 발견했다. 원형5.2m 제외가14×12m 직사각형 모서리를 덮지 못한 것이 원인이다. 실제 모든 MultiMesh 좌표 검사 실패(`forest-expand-roof-red.log`) → `stack_*`/감시탑의 사각 영역+1m 여유 제외 → 창 모드 통과. 수정으로 풀43묶음/공간 묶음1개가 줄었다. 컨테이너 금속 바닥색도 숲 색 혼합에서 제외하고 지붕 시점 렌더로 확인했다.
3. 성능 검사의 `before` 라벨이 처음에는 결과 파일명만 바꿨다. 최초 기준 측정은 구현 전 실제5그루/966묶음으로 유효하지만 재현 시에는 확대 맵을 측정하게 되므로, 라벨이 실제 `ForestExpanded`를 선택하고 모델 수를 검사하도록 수정했다. 같은 최종 코드로 두 모드를 다시 측정한다.
4. 야적장 경사로는 경계255에서261까지다. 검사 지점260은 아직 경사로인데 평지1.2m를 기대해 실패했다. 실제 위치/코드를 확인하고 경사로를 지난262로 검사 지점을 옮겼다. 게임 높이/이동 코드는 변경하지 않았다.
5. 바닥의 기존 큰 사각/삼각 색 변화는 확대 전 렌더에도 있다. 이번에는 숲 전체 분포와 생태 경계 연결을 적용했으며 지형 메시/전체 바닥 재질 마감은 별도다. 저사양용 자동 품질 단계는 추가하지 않았다.

## 실제 플레이·렌더 검증

창 모드의 실제 게임에 자동 이동/구르기/사격 입력을 전달했다. 사람이 손으로 조작한 플레이나 난이도 승인으로 표현하지 않는다.

- 확대 전용: 최초 헤드리스29통과, 지붕 검사 추가/수정 후 Forward+44통과·12장. 모든 풀의 경로 거리/실제 높이/구조물 제외/최대 높이, 저지대 보행·구르기, 야적장 진입 경사로, 고지에서6마리 실제 사격, 영역 밖 기존 소품/바닥과 조명 복원 확인.
- 연속 전체 동선:171통과·45장,365.6초 시뮬레이션, 사다리8회·짚라인1회·곤돌라·190처치·최대 적57·1,231발. 첫 시작부터 세이프하우스 E닫기 클리어까지 순간이동 없이 확인했다(`forest-expand-route.log`).
- 회귀: 최초20×20m `ForestQualityCheck`, `Stage2EnvironmentCheck`, `StageTransitionCheck`, `SizeDraftCheck`, `Integration`을 따로 실행했다. `Start-Game.ps1 -Test`만 실행한 것이 아니다.
- 최종 독립 리뷰는 읽기 전용 코드/렌더 검토이며 리뷰어가 게임을 재실행한 것은 아니다. 위2건을 수정했고 추가 확정 결함은 없었다.

### 확인 화면

- [저지대](../game/output/forest-expansion-low.png), [중간 단](../game/output/forest-expansion-mid.png), [고지](../game/output/forest-expansion-high.png)
- [협곡 서쪽](../game/output/forest-expansion-west.png), [협곡 바닥](../game/output/forest-expansion-canyon.png), [동쪽 둑](../game/output/forest-expansion-east.png), [야적장 입구](../game/output/forest-expansion-yard.png)
- [실제 적 시인성](../game/output/forest-expansion-high-enemies.png), [사격 후](../game/output/forest-expansion-high-combat-cleared.png), [경사로 통과](../game/output/forest-expansion-yard-ramp-walk.png)

`output/`은 재실행으로 만드는 로컬 산출물이며 커밋하지 않는다. 직접 플레이는 `game` 폴더에서 `.\Start-Game.ps1 -Stage2Draft`.

## 재현

저장소 루트에서 실행한다. 성능 측정은 두 모드를 순서대로 단독 실행하며 `--fixed-fps`를 사용하면 검사기가 거부한다. 화면 옵션이 저장된 사용자 해상도로 조정될 수 있어 보고서의 실제 viewport를 확인한다.

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --headless --editor --path game --import
& $godot --headless --path game --fixed-fps 60 res://tests/ForestExpansionCheck.tscn
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/ForestExpansionCheck.tscn
& $godot --path game --resolution 1440x900 res://tests/ForestPerformance.tscn -- before
& $godot --path game --resolution 1440x900 res://tests/ForestPerformance.tscn -- expanded
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2RouteCheck.tscn
```

남은 범위: 최저 사양 GPU/CPU와 실제6인 네트워크·장시간 호드 성능, 최종 사용자 미술 수락. 야적장 전체·고지 F/도착 G를 같은 울창한 숲으로 바꾸는 작업은 이번 숲/경계 범위에 포함하지 않는다.

## 실제 성능 비교

RTX5070Ti, Forward+, 실제 내부 viewport1600×1000, VSync 해제/MaxFps0, 고정 FPS 없음. 동일한 최종 검사 코드에서 before=20×20m(5그루/966묶음), expanded=전체 숲(109그루/24,717묶음)을 순서대로 단독 실행했다. 시점마다2초 준비 뒤4초 벽시계 프레임 간격을 측정했다. combat_mid는 실제 AI가 움직이는30마리와 플레이어의 작은 좌우 이동이며 사격/사망/폭발 효과는 없다.

| 시점 | 평균 프레임 ms 전→후 | p95 ms 전→후 | 평균 draw calls 전→후 | primitives 전→후 |
|---|---:|---:|---:|---:|
| low | 1.533 → 1.678 | 1.862 → 2.074 | 598 → 810 | 176,257 → 506,093 |
| mid | 1.536 → 1.633 | 1.867 → 1.969 | 658 → 768 | 275,215 → 525,016 |
| high | 1.497 → 1.619 | 1.816 → 1.995 | 611 → 736 | 219,753 → 604,292 |
| west | 1.526 → 1.629 | 1.849 → 1.988 | 645 → 716 | 211,641 → 437,528 |
| east | 1.512 → 1.688 | 1.837 → 2.024 | 561 → 807 | 230,681 → 558,040 |
| yard | 1.479 → 1.598 | 1.811 → 1.918 | 445 → 683 | 152,868 → 395,131 |
| combat_mid | 1.776 → 1.896 | 2.836 → 2.985 | 822 → 901 | 675,380 → 898,677 |

맵 짓기 시간은 3095 → 3482ms였다(단회 측정, 캐시/프로세스 상태 영향 포함). 위 ms는 프레임 간격이며 GPU 실행 시간 전용 측정이 아니다. p95는 해당4초 표본의95백분위다. primitives는 엔진 렌더 모니터의 패스 포함 값이며 고유 자산 삼각형 수가 아니다.

원시 결과: `game/output/forest-performance-before.json`, `forest-performance-expanded.json`, 각각의 동명 `.log`. 초기 측정은 `forest-performance-initial-before.json`/`initial-expanded.json`에 보존했다. 현재 PC에서는 확대 후에도 측정한 시점의 p95가16.7ms보다 낮았으나, 이것이 저사양이나6인 실전의60FPS 보장은 아니다. 증가한 그리기/그림자 비용을 기록하며 전역 배치 수 전체가 매 프레임 그려진다고 해석하지 않는다.

### 공유 작업 폴더 빌드 보정

검수 중 별도 Studio 작업이 추가한 `StudioLibrary.cs:110`의 params 인자에서 `new(...)`의 대상 타입이 배열로 해석되어 CS8752가 발생했다. 해당 한 곳을 `new LibraryProperty(...)`로 명시해 빌드를 복구했다. Studio 기능/구조는 변경하지 않았으며 제품 버그 ID를 만들지 않는다. 최종 빌드0경고/0오류.

## 최종 재검증

지붕 풀/바닥색 및 비교 모드 수정 후 다시 실행했다. 모든 프로세스 종료0.

| 검사 | 결과 | 종료 경고 |
|---|---|---|
| ForestExpansionCheck | FOREST_EXPANSION_PASS 29 | WARNING: 2 ObjectDB instances were leaked at exit (run with `--verbose` for details). |
| ForestQualityCheck | FOREST_QUALITY_PASS 21 | 없음 |
| Stage2EnvironmentCheck | STAGE2_ENVIRONMENT_PASS 13 | 없음 |
| StageTransitionCheck | STAGE_TRANSITION_CHECK_PASS 196 | WARNING: 6 ObjectDB instances were leaked at exit (run with `--verbose` for details). |
| SizeDraftCheck | SIZE_DRAFT_PASS 74 | WARNING: 6 ObjectDB instances were leaked at exit (run with `--verbose` for details). |
| Integration | INTEGRATION_PASS 149 | 없음 |
| ForestExpansionCheck 창 모드 | 44통과·12렌더 | 없음 |
| Stage2RouteCheck 창 모드 | 171통과·45렌더 | 없음 |
| C# build | 0경고/0오류 | 없음 |

최종 연속 동선:366.9초 시뮬레이션·사다리8회·짚라인1회·곤돌라·189처치·최대 적55·1,234발 후 클리어. 로그 `game/output/forest-expand-route-final.log`; 회귀 로그 `forest-expand-final-<장면>.log`. I-17의 기존 ObjectDB 종료 경고는 기능 통과와 구분하며 해결 처리하지 않는다. 문서 링크/공유 기록 연결과 `git diff --check`도 확인했다.
