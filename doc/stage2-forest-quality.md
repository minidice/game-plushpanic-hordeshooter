# Stage 2 숲길 20×20m 품질 기준 구역 Implementation Plan

> **후속 상태:** 이 문서는 최초20×20m 제작/검수 이력이다. 현재 기본값은 [숲 전체 확대](stage2-forest-expansion.md)이며, `ForestExpanded=false`로 이 기준 구역을 재현한다. 아래의 전체 확대 미완료 표기는 당시 범위다.

> 실행: `superpowers:executing-plans`. 사용자가 참고 이미지의 따뜻한 황록색 분위기, Blender 제작, 게임 연결과 실제 플레이/렌더를 승인했다.

**Goal:** 첫 숲 오르막 뒤20×20m에서 나무·흙길·풀·조명을 함께 개선하고 기본 플레이 카메라에서 길과 적이 읽히는 품질 기준을 만든다.

**Architecture:** 기존 LayoutMap에 한정한 선택 가능한 외형 구역을 추가한다. 기존 지형/높이/이동 링크는 유지하며, 해당 구역의 기존 드문 식생 대신 Blender 나무2종과 풀2종을 사용한다. 바닥/길 셰이더의 같은 영역 마스크와 배치 규칙을 공유하고 구역 경계에서 색/조명을 부드럽게 전환한다.

**Tech Stack:** Godot4.7.2 C#·spatial shader·MultiMesh, Blender5.2.2 Python·GLB.

**Spec:** 사용자2026-10-24 참고 스크린샷, [환경 1차](stage2-environment-pass.md), [높이/동선](stage2-floorplan.md), A-25.

## 설계와 제약

- 구역: X84..104 / Z−34..−14, 중심(94,−24), 기존1.2m 바닥. 경계 안쪽3m 색/밀도 전환. Stage2Draft 기본 활성화, 비교 검수 시 비활성화 가능.
- 길 중심 최소 폭4m는 키 큰 풀/나무로 막지 않는다. 나무 줄기 충돌과 흐름장을 일치시키고 수관은 기존 가림 처리를 사용한다. 풀은 충돌 없음.
- 침엽수는4층의 비대칭 잎층·불규칙 가장자리·부드러운 명암, 활엽수는 넓은 수관 덩어리와 변형된 윤곽. 수정한 공통 원형은 유지하고 새 기준 구역 모델을 별도 ID로 검수한다.
- 황록색 풀은 크기/방향/군집 밀도를 달리하고 흙길 중앙은 비운다. 기존 작은 돌을 소량 재사용한다. 정점색과 불투명 재질을 우선 사용한다.
- 구역 접근에 맞춰 태양/환경광을 부드럽게 조정하고 밖에서는 원래 값을 복원한다. 조명 변경으로 플레이어·적이 검게 묻히지 않는지 확인한다.
- 전체 Stage2로 확대, 최종 성능/난이도 승인, 원격 푸시는 이번 범위 밖이다. 기존 미커밋 환경/이동 장치/Studio 작업을 보존한다.

## 실행 순서

- [x] 1. 기준 구역 기존 화면과 새 모델/구역 부재를 전용 검사에서 확인. 실제 보행 검사는 연결 후 수행.
- [x] 2. `forest_study.py`로 나무2·풀2 GLB/원본 blend 생성, 독립 재가져오기 검사와 Blender 렌더.
- [x] 3. `LayoutMapForest.cs`, 바닥/길 셰이더와 구역 조명을 연결. 길 여유/충돌/가림/경계/영역 밖 복구 검사.
- [x] 4. 실제 이동·구르기·사격과 기본/확대/근접 렌더, 관련 동선/통합 회귀 검사.
- [x] 5. 최종 독립 리뷰, 결정/이슈/체크리스트/실행 결과 기록.

## 검수에서 우선 확인할 위험

식생이 길 또는 실제 적을 가림, 줄기 충돌과 메시 불일치, 지형 높이/경사로 변화, 경계 색 단절, 영역 밖 조명 잔류, GLB 재생성으로 다른 모델 덮어쓰기, 풀의 과도한 그림자/드로콜을 각각 검사한다. 정확한 밀도·색·광량은 이번 비교용 임시 아트 값이다.

## 구현 계약과 결과 — 2026-10-24

| 자산 ID | 삼각형 | 높이(m) | 역할 |
|---|---:|---:|---|
| `forest_pine` | 2,764 | 4.55 | 비대칭4층·물결/홈 가장자리, 정점색·부드러운 법선 |
| `forest_broadleaf` | 1,676 | 4.4079 | 변형된 수관5덩어리, 위/아래 색 분리 |
| `forest_grass_a` | 84 | 0.4458 | 휜 풀잎14개 |
| `forest_grass_b` | 60 | 0.3077 | 낮은 풀잎10개 |

- 제작: [생성기](../prototypes/outdoor-kit/tools/forest_study.py), [Blender 원본](../prototypes/outdoor-kit/source/forest-study.blend), [manifest](../game/assets/stage2-props/manifest.json). 생성기는 자신의4개 레코드만 교체한다. 다른18개 Stage2 소품 레코드는 유지하며 전체 로더65종/Stage2팩22종이다.
- 나무는 `Keep` 줄기/`Occlude_Canopy` 수관을 분리한다. 두 종류 모두 원본 기준 반경0.25m·높이1.4m 줄기 충돌과 흐름장 장애물, 배치 스케일을 함께 사용한다. 발밑0.12m의 작은 뿌리 확장은 충돌 원통보다 조금 크며 검토 후 유지했다. 풀은 충돌이 없고 불투명 양면 재질을 사용한다. [정점색 import 후처리](../game/assets/stage2-props/forest_material_import.gd)가4개 GLB에 연결된다.
- 최종 배치: 나무5그루, 풀966묶음. 5m 공간 셀×풀2종의32개 MultiMesh, 풀 삼각형 합72,960개(그림자 등 추가 패스 비용은 이 수치에 포함하지 않음). 거리42m/마진6m에서 가시성 페이드. **32배치는 실측 총 드로콜32개를 뜻하지 않는다.**
- 바닥은 기존 셰이더 안에서 흙/이끼/작은 색 얼룩을 혼합한다. 별도 겹치는 바닥판을 만들지 않으며 기존 길 표면은 같은3m 마스크로 사라진다. 구역 밖 식생 난수 순서를 보존해 구역 밖 모든 `KitProps` 자산/좌표/회전과 바닥/비통행 직사각형 수를 비활성 기준과 대조했다.
- [지역 조명](../game/scripts/World/ForestStudyLight.cs)은 기존 태양/환경광을 보간한다. 중심 태양 `fff2c7`/1.35/(-42,-105,0), 환경광 `bcc6a2`/0.38, 진입·이탈 최대 가중치 변화1.5/s. 밖에서는 원래 색/방향/광량을 복원한다. 구역 안에 있을 때 화면 전체에 적용되는 공통 조명이며 실제 국소 태양은 아니다.
- 선택: 기본 작업 폴더의 기존 미커밋 모델/이동 장치/Studio 작업을 유지했다. 사용자 승인된20×20m 기준 구역만 구현하며 전체 맵 확장과 최종 미술/성능 승인은 분리했다. 이번 작업의 커밋·원격 푸시는 수행하지 않는다.

## 실행 검증

Windows, Blender5.2.2 LTS, Godot4.7.2 Mono Forward+, RTX5070Ti. 자동 입력을 실제 게임의 이동/충돌/사격에 전달해 검사했으며 사람이 키보드로 조작한 결과로 표현하지 않는다.

| 검사 | 결과 | 로컬 로그 |
|---|---|---|
| 구현 전 전용 검사 | 새 모델 부재로 예상 실패, 기준 화면2장 | `game/output/forest-quality-red.log` |
| Blender 실제 생성·GLB 재가져오기·렌더 | 4종 통과, contact sheet1장 | `game/output/forest-blender-build.log`, `forest-blender/report.json` |
| Godot import | 성공, ERROR/WARNING 없음 | `game/output/forest-import.log` |
| ForestQualityCheck headless | 21 통과 | `game/output/forest-quality-headless.log` |
| ForestQualityCheck Forward+ | 30 통과, 실제 렌더8장 | `game/output/forest-quality-window.log` |
| C# build | 경고0·오류0 | `game/output/forest-build.log` |
| Stage2EnvironmentCheck | 13 통과, 종료 ObjectDB2개 경고 | `game/output/forest-regression-Stage2EnvironmentCheck.log` |
| StageTransitionCheck | 196 통과, 종료 ObjectDB6개 경고 | `game/output/forest-regression-StageTransitionCheck.log` |
| SizeDraftCheck | 74 통과, 종료 ObjectDB6개 경고 | `game/output/forest-regression-SizeDraftCheck.log` |
| Stage2RouteCheck Forward+ | 171 통과·45장, 종료 경고 없음 | `game/output/forest-route-window.log` |
| Integration | 149 통과, 종료 ObjectDB2개 경고 | `game/output/forest-regression-Integration.log` |

전체 동선은 순간이동 없이 시작부터 세이프하우스 문 닫기까지366.6초(검사 시뮬레이션), 사다리8회·짚라인1회·곤돌라,192처치/1,215발/최대 동시 적55를 확인했다. 자동 조작·무적 검사이므로 실제 플레이어의 클리어 시간/난이도 측정이 아니다. [클리어 화면](../game/output/route-28-cleared.png). 기존 I-17 ObjectDB 경고는 별도이며 이번 전용 헤드리스/창 모드와 전체 동선에서는 없었다.

문서 상대 링크, manifest22개 ID의 중복 부재, 신규 GLB4종의 정점색/외부 텍스처 없음/import 후처리 연결, `git diff --check`를 확인했다. 결정/이슈/기능 체크리스트와 평면도에도 같은 범위·임시 수치·남은 제약을 기록했다.

전용 검사는 양방향 보행·구르기, 실제 사격6마리 처치, 줄기 물리 레이/흐름장, 풀966개의 위치/높이/길 여유, 수관 가림 발생/복원, 구역 밖 조명 복원과 기존 소품/바닥 대조를 포함한다. 헤드리스 dummy 렌더러는 MultiMesh 변환을 읽지 못하므로 개별 풀 위치 검사는 창 모드에서만 수행한다.

제작 중 수정: Blender5.2 glTF의 미지원 `export_colors` 인자를 제거했다. 검사의 자동 노드명 의존성을 실제 자산 경로 탐색으로 교체하고 지역 조명 노드 이름을 명시했다. 첫 렌더 이후 태양 방향을 측면으로 옮기고 잎층 끝에 홈을 추가해 층간 형태를 보완했다. Blender 종료0/모델 검사 통과와 별개로, `Material.use_nodes` 사용 중단 예정 경고 및 설치 기본 Grease Pencil 브러시2개 경로를 상대화할 수 없다는 저장 경고가 있다. 내보낸4개 GLB는 텍스처/브러시 참조 없이 정점색을 사용하며 게임 import 오류는 없다.

최종 독립 리뷰: 생성기/자산/셰이더/배치/조명/물리 연결과 실제 화면을 읽기 전용 검토했다. 수정이 필요한 확정 결함은 없었다. 리뷰 자체에서 빌드/게임을 별도 재실행한 것은 아니며 풀 그림자 성능·최저 사양·다인 성능은 검증하지 않았다.

## 렌더와 재현

- [기존 확대 화면](../game/output/forest-quality-before-close.png)
- [적용 후 기본 화면](../game/output/forest-quality-after-wide.png), [확대 화면](../game/output/forest-quality-after-close.png)
- [나무/바닥 근접](../game/output/forest-quality-tree-ground-detail.png), [실제 전투 시인성](../game/output/forest-quality-combat-readable.png), [수관 가림](../game/output/forest-quality-canopy-fade.png)
- [Blender 모델 렌더](../game/output/forest-blender/models.png)

위 `output/` 파일은 로컬 실행 산출물이며 버전 관리하지 않는다. 모델 제작/검수는 저장소 루트에서 다음처럼 재현한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/outdoor-kit/tools/forest_study.py
dotnet build game/HordeShooter.csproj --nologo -v q
$godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --headless --editor --path game --import
& $godot --headless --path game --fixed-fps 60 res://tests/ForestQualityCheck.tscn
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/ForestQualityCheck.tscn
& $godot --headless --path game res://tests/Stage2EnvironmentCheck.tscn
& $godot --headless --path game res://tests/StageTransitionCheck.tscn
& $godot --headless --path game res://tests/SizeDraftCheck.tscn
& $godot --headless --path game res://tests/Integration.tscn
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2RouteCheck.tscn
```

직접 플레이는 `game` 폴더에서 `.\Start-Game.ps1 -Stage2Draft`. 시작 해치에서 주 경로를 따라 첫 숲 오르막을 오른 뒤 중심(94,1.2,−24)의 새 숲길을 확인한다. 정식 메뉴의 기존 Stage2와 다른 시안 실행 옵션이다.

남은 범위: 사람의 자유 이동/전투 체감·최종 미술 수락, 풀 그림자 포함 최저 사양/6인 성능, 전체 숲 확대와 이음새 최종 마감. 이20×20m 결과를 전체 Stage2 품질 완료로 간주하지 않는다.
