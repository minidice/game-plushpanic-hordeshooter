# Stage 2 이동 장치 묶음 A 생성·렌더 검수

**현재 상태(2026-10-23 후속): 게임 연결 완료.** 아래 최초 생성·병합 기록 이후 사용자가 게임 연결과 실제 렌더 검수를 요청했다. 현재 `LayoutMap`의 사다리·짚라인·곤돌라가 GLB를 사용한다. 마지막 [게임 연결 검수](#게임-연결-검수-2026-10-23) 절에 현재 계약·재현·제약을 기록한다. 앞부분의 미연결/미검증 문장은 해당 최초 실행 당시의 이력이다.

2026-10-23. 사용자 후속 요청으로 Codex의 범위를 스크립트 작성에서 **Blender 실행, GLB 생성 및 렌더 검수**까지 확장했다. 게임 연결은 포함하지 않는다. 기준은 원격 작업 브랜치 `9412666`의 [모델 브리프](https://github.com/minidice/game-plushpanic-hordeshooter/blob/9412666/doc/model-briefs/stage2-landmarks.md) 1·2·5장이다. 최초 제작 때는 해당 브리프/높이 단 구현이 `main`에 없었다. 이후 사용자 병합 요청으로 최신 원격 `main`(`0098738`, PR #5)을 가져와 모델 작업과 합쳤으며, 이제 [브리프](model-briefs/stage2-landmarks.md)·높이 단·연출 이동 코드가 같은 작업 트리에 있다. 모델의 게임 연결은 여전히 별도다.

## 결과와 자산 계약

모든 파일은 [game/assets/stage2-props](../game/assets/stage2-props)에 있다. 원본은 [stage2-props-a.blend](../prototypes/outdoor-kit/source/stage2-props-a.blend). 모델은 시각용이며 충돌은 `none`, 런타임 게임 코드가 소유한다. 부위는 루트 아래 빈 노드와 그 아래 메시 하나씩, 게임 +Y 위·Blender +Z 위, 카메라 쪽은 Blender −Y다. 재질은 텍스처 없이 키트 색상을 선형 변환한다.

| ID | 실제 삼각형 / 예산 미만 | 실제 크기 X×Y×Z(m, 게임 축) | 구조·피벗 |
|---|---:|---|---|
| `ladder_section` | 256 / 600 | 0.620×1.200×0.120 | `Keep`, 바닥 0, 1.2m 간격으로 적층 |
| `ladder_top` | 360 / 500 | 0.626×1.050×0.636 | `Keep`, 윗단 바닥 0, 하단 −0.15·정상 0.9 |
| `zipline_anchor` | 668 / 1,500 | 0.600×3.200×1.050 | `Keep`·`Occlude_Pole`, 바닥 0, 도르래 중심 `(0,3.0,−0.4)` |
| `gondola_cabin` | 1,428 / 3,000 | 4.000×3.730×4.006 | `Keep`·`Occlude_Frame`, 바닥 윗면 0·하단 −0.15, 바퀴 중심 높이 3.4, ±X 출입구 폭 1.6 |
| `gondola_station` | 1,376 / 3,000 | 4.850×5.600×5.200 | `Keep`·`Occlude_Tower`, 4×4 탑승 영역 중심 피벗, 뒤 탑 4×1.2×5, 바퀴 포함 높이 5.6 |

발판·팔·녹 표면 장식까지 포함한 AABB이며, 충돌 크기나 사람이 걷는 영역을 뜻하지 않는다. 재질 개수는 모델별 2/3/4/3/4개. `Occlude_*` 분리는 후속 가림 연출용이며 게임의 자동 숨김/반투명 처리가 구현됐다는 뜻은 아니다.

## 재생성·검사

저장소 루트 PowerShell에서 실행한다. Blender 5.2.2 LTS에서 확인했다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py -- --manifest-merge
python -m py_compile prototypes/outdoor-kit/tools/stage2_props_a.py prototypes/outdoor-kit/tools/stage2_props_a_review.py
```

- 생성기: 5종의 예산·치수·피벗·재질·부위 검사를 통과하고 `STAGE2_PROPS_BUILT A 5`를 출력했다. 기존 다른 묶음의 manifest 항목은 보존하며 기존 키트 생성기를 import/수정하지 않는다.
- 검토기: **내보낸 GLB를 다시 읽어** 헤더·실제 삼각형·재질·부위·치수·축·피벗, 묶음 A 정확히 5종 포함 여부 및 검수 카메라 화면 범위를 확인한다. 최종 **112개 검사, 13장 Cycles 렌더**를 통과했다. 5종의 카메라 쪽/뒷면, 사다리 3단+손잡이(전체 4.5m), 객차의 1.2m 크기 비교용 인형 4개, 상부 프레임 숨김 비교를 확인했다.
- manifest 검사: 임시 팩에 다른 묶음의 항목/사용자 메타데이터와 오래된 A 항목을 넣고 실제 생성기를 두 번 실행했다. 다른 묶음 보존·A 항목 교체·중복 없음·반복 결과 동일 및 검토 입력의 누락/중복 거부를 확인했다. 실제 배포 팩은 이 검사에서 변경하지 않는다.
- 로그: `game/output/stage2-props-a-build-final.log`, `stage2-props-a-review-verified.log`, `stage2-props-a-manifest-verified.log`. 렌더와 보고서는 `game/output/stage2-props-a-review/`에 재생성된다. 이 `output/`은 커밋 대상이 아니다.

## 발견·수정과 경고

- 첫 실제 렌더에서 승강장 기둥·가로보 접합부가 검게 보였다. 기둥 끝과 가로보의 윗면/옆면이 같은 평면을 공유했다. 높이 5m 기둥을 가로보 안에 끝나는 4.84m로 줄이고 단면을 0.24→0.22m로 좁혔다. 가로보가 탑 전체 높이 5m를 유지한다. 검사에서 최초 겹친 삼각형 쌍 40개를 검출했고, 수정 후 남은 10개는 바퀴 허브 안에 가려진 살대 끝면이었다. 재발 검사는 **노출된 탑 상단 좌우 접합부**로 한정했으며 최종 0개와 전후 렌더로 확인했다. 전체 모델의 모든 내부 겹침을 제거했다는 의미가 아니다.
- 독립 검토에서 뒷면 렌더의 하단 잘림과 필수 자산이 manifest에서 빠졌을 때 건너뛰는 검수 도구 문제를 발견했다. 투영된 경계의 중앙으로 카메라를 보정하고 전 정점 화면 포함 검사를 추가했다. 검사 전에 A의 5개 ID/중복 여부를 확인하며 누락·중복 입력이 거부되는 것을 별도로 확인했다.
- Blender `Material.use_nodes`의 6.0 폐기 예정 경고와 기본 브러시 재질 외부 경로를 상대 경로로 만들 수 없다는 저장 경고가 남았다. GLB 생성·재가져오기·최종 렌더 실행은 종료 코드 0이다. 중간 검사 종료 지연 중 로그가 잠겨 재실행 시도가 한 번 실패했으나 후속 실행은 정상 종료했다. 이 경고/시도를 통과 결과에 숨기지 않는다.

## 남은 연결 작업

- 원격 `Zipline.BuildLook` 줄 높이 2.8m와 브리프/모델 도르래 기준 3.0m의 차이, 로컬 기준점 `(0,3.0,−0.4)` 및 설치 회전을 맞춰야 한다. 줄 자체는 게임이 그린다.
- 객차 바닥판 윗면은 0m다. 기존 게임의 탑승판 그림과 겹치지 않도록 교체해야 한다. 승강장에는 중앙 바닥판을 만들지 않았다.
- 객차 매달기 팔은 기본 쿼터뷰에서 일부 위치의 인형 머리를 가린다. 숨김 비교는 부위 분리만 확인한 것이며, 실제 게임의 가림 연출·최대 인원 탑승·승하차 경로 검증은 남는다.
- 실제 캐릭터·맵·Godot 가져오기/전용 장면·이동/충돌/멀티·성능 검사는 실행하지 않았다. C#이나 맵 데이터는 변경하지 않아 `Integration`도 이번에 실행하지 않았다. 자산 렌더 검수와 제품 내 통합 검수를 구분한다.

## 최신 main 병합 검증 (2026-10-23 후속)

- 사용자 요청으로 자산 작업을 `399ea8f`로 커밋하고 원격 `main`의 `0098738`(PR #5)을 로컬 `main`에 병합했다. 충돌 없이 합쳐졌으며 원격의 높이 단/연출 이동/시안 기록과 이 문서의 모델 검수 이력을 함께 보존했다. 브리프·맵 확장 계획의 묶음 A 상태를 자산 생성/렌더 완료·게임 미연결로 정리했다.
- 위 최초 검수의 Godot 미실행 상태에서 후속 검증을 추가했다. 병합 전 GLB 재가져오기 5종·93개 검사 통과. 병합 트리에서 `game/Start-Game.ps1 -Test`: C# 빌드 경고/오류 0, Godot 가져오기 성공, `Integration` 149개 통과. 별도 `--headless --fixed-fps 60` 실행으로 `ElevationCheck` 76개·`SizeDraftCheck` 74개·`StageTransitionCheck` 164개 통과. 시안 검사의 자동 이동은 2.6분의 시뮬레이션이며 실제 전투 플레이 시간 측정은 아니다.
- Godot가 새 GLB 5종을 `PackedScene`으로 읽고 내보낸 루트 아래 `Keep`/`Occlude_*` 및 각 메시를 갖는지 별도 보조 검사로 확인했다. 첫 검사에서는 Godot의 최상위 장면 래퍼를 빠뜨려 실패했고 트리 출력으로 `장면 래퍼 → 자산 ID → 부위 → 메시` 구조를 확인해 검사 경로만 수정했다. GLB 수정은 없었다. 재생 가능한 가져오기 설정 `game/assets/stage2-props/*.glb.import` 5개를 보존하며 `.godot/` 캐시는 커밋하지 않는다.
- 로그는 `game/output/stage2-props-merge-integration.log`, `stage2-props-merge-ElevationCheck.log`, `stage2-props-merge-SizeDraftCheck.log`, `stage2-props-merge-StageTransitionCheck.log`, `stage2-props-merge-import-verified.log`; 초기 보조 검사 실패는 `stage2-props-merge-import-check.log`·`-inspect.log`다. 종료 경고는 통합 ObjectDB 2개·전환 6개이며 객체별 원인을 이번에 식별하지 않아 I-17을 유지한다. 높이 단·시안·최종 가져오기 보조 검사에는 종료 경고가 없었다.
- 이번 병합에서 새 외형 변경은 없어서 기존 13장 렌더를 재촬영하지 않았다. 모델은 아직 게임의 사다리/짚라인/곤돌라 임시 도형에 연결하지 않았으며 실제 캐릭터 가림·승하차·멀티/성능 검증은 그대로 남는다. 원격 푸시는 하지 않는다.

## 게임 연결 검수 (2026-10-23)

사용자가 기존 역할 분담을 확장해 Codex에 게임 연결과 실제 렌더 확인을 요청했다. 기준 커밋은 앞서 푸시한 `29c3121`이다. [작업 계획](stage2-props-a-runtime-plan.md), [결정 기록](decisions.md), [I-113](issues.md)을 함께 갱신했다. 기존 정식 Stage 2 대신 **Stage 2 크기 시안 `LayoutMap`**에서 확인했으며, 시안 실행은 `game/Start-Game.ps1 -Stage2Draft`다.

### 연결 내용

- `PropLibrary`의 다섯 번째 팩으로 A의 5종을 등록했다(전체 52종). 사다리는 실제 단 수만큼 1.2m 조각을 쌓고, 윗단 방향으로 손잡이를 회전한다. 상단 피벗은 해당 단 바닥 높이다.
- 짚라인은 기둥 피벗과 도르래의 수평 차이 0.4m를 설치 회전에 반영한다. 줄 끝은 실제 모델 기준점 `(0,3,-0.4)`이며 **지면 +3m**다. 24개 줄 조각과 탑승자 모두 `CablePoint`의 같은 처짐(중간 최대 1.2m)을 사용하고, 탑승자 발은 줄보다 2.1m 아래다. 12m/s와 착지 지점은 유지한다.
- 곤돌라 임시 탑승판을 객차로 바꾸고 양쪽 승강장을 배치했다. 객차 ±X 벽과 난간 중앙을 1.6m 비워 실제 출입구로 쓴다. 삼각형은 1,284→1,428로 여전히 3,000 미만. 원본 바닥 윗면은 0m이며, 게임에서는 기존 지면과 깊이 충돌을 피하려고 **객차 외형만 +0.012m**로 둔다. 충돌/흐름장/바닥 판정은 기존 지형을 그대로 쓴다. 모델 벽에 새로운 물리 충돌을 추가한 작업은 아니다.
- 탑승은 생존자의 충돌 반지름만큼 4×4m 영역 안쪽, 바닥 높이 차이 0.3m 이하, 사다리/짚라인 미사용을 검사한다. 전원 탑승 후 세계를 정지하고 객차·생존자는 고정한다. 연출 동안 원래 보이던 주변 3D 루트와 양쪽 승강장을 숨겨 아래의 가짜 협곡을 드러낸다. 정상 도착과 이동 장치 제거 모두 가시성과 정지를 복구하며 원래 숨겨진 루트는 유지한다. 도착 시 객차도 도착 승강장으로 옮기고 출발 상호작용은 완료 상태로 잠근다.
- `PropOcclusion`은 묶음 A의 `Occlude_*`만 처리한다. 활성 카메라와 생존자 몸/머리 사이 선분이 부위 AABB를 지나면 상부가 투명도 0.85로 옅어지고, 가림이 끝나면 복원된다. `Keep`는 그대로다. 막대마다 깜빡이지 않도록 부위 전체 AABB를 쓰는 보수적 판정이며, 빈 공간을 지나도 프레임 전체가 옅어질 수 있다. 현재 Stage 2 원근 시점에서 검증했으며 직교 카메라 전반으로 일반화한 가림 시스템은 아니다.

### 실행 결과

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---|---|
| C# 빌드 / Godot 가져오기 | 경고 0·오류 0 / 성공 | `traversal-art-integration.log`, `import.log` |
| Blender 실제 생성 | GLB 5종, 종료 0 | `traversal-art-blender.log` |
| 내보낸 GLB 재가져오기 | 5종·93개 구조/크기/피벗/예산 검사, 종료 0 | `traversal-glb-contract.log` |
| `TraversalArtCheck` 헤드리스 | 149개 통과 | `traversal-art-final-headless.log` |
| `TraversalArtCheck` 창 모드 | 163개 통과(동작149 + PNG 저장14), Forward+, 1440×900, RTX 5070 Ti | `traversal-art-final-render.log` |
| `ElevationCheck` | 76개 통과, 종료 ObjectDB 잔존 4개 경고 | `traversal-regression-ElevationCheck.log` |
| `SizeDraftCheck` | 74개 통과, 끝까지 자동 이동 2.6분(시뮬레이션)·곤돌라 1회 | `traversal-regression-SizeDraftCheck.log` |
| `StageTransitionCheck` | 172개 통과(추가 5종의 구조/가림 부위 포함), 종료 ObjectDB 6개 경고 | `traversal-regression-StageTransitionCheck.log` |
| `Start-Game.ps1 -Test` | `Integration` 149개 통과, 종료 ObjectDB 4개 경고 | `traversal-art-integration.log` |

전용 최종 헤드리스/창 모드와 시안 검사에는 종료 경고가 없었다. 위 ObjectDB 객체별 원인은 이번에 식별하지 않아 I-17은 유지한다. Blender 생성에는 기존 `Material.use_nodes` 6.0 폐기 예정 경고와 기본 브러시 외부 경로 경고가 남았으며 생성 실패는 아니다.

검사는 실제 GLB 인스턴스·정점과 실제 생존자 이동을 사용한다. 사다리 적층/방향, 양쪽 도르래 연결, 줄과 탑승자 간격, 출입구 삼각형 침범 없음, 높은 단/객차 밖 오판 방지, 6인 중 미탑승 대기, 6명의 출입구 진입/배치/도착/하차, 상부 가림 발생/복원, 배경 가시성, 원래 숨겨진 루트 유지, 이동 중 제거 복구를 확인했다. 여섯 캐릭터는 **같은 프로세스의 생존자**이며 네트워크 6인 검증이 아니다.

실제 이미지 14장을 열어 확인했다: `traversal-ladder-{bottom,climb,top}.png`, `traversal-zipline-{start,middle,end}.png`, `traversal-gondola-{tower-occlusion,approach,boarded,six-close,six-wide,riding,arrived,disembarked}.png`. 사다리 단면·상단 높이, 줄 연결, 탑 뒤 생존자 노출, 객차 바닥 중복 제거, 6인 머리 가독성, 협곡 배경 노출, 도착 후 객차 위치와 출입구 하차를 확인했다. 마지막 사진에서 모두 벗어난 뒤 상부가 다시 불투명해지는 것도 확인했다. 독립 코드 리뷰에서 차단 결함은 없었고 제안한 복구 검사 3종을 반영했다.

### 실패에서 수정한 경로

- 최초 등록 검사: `ladder_section` 미등록 실패(`traversal-art-red.log`) → 팩 연결 후 통과.
- 첫 적층 검사: 이름으로 GLB 루트/래퍼를 센 가정이 Godot의 형제 자동 이름 변경과 달라 실패(`traversal-art-first.log`, 종료 ObjectDB 2개). 실제 자산 경로로 인스턴스를 구별하도록 검사 수정. 모델 오류로 처리하지 않는다.
- 첫 창 모드 155개 검사는 통과했지만 실제 `gondola-riding` 이미지에서 지면이 협곡을 덮는 문제를 발견. 가시성 검사를 추가해 실패 재현(`traversal-backdrop-red.log`) → 연출 중 숨김/복구 적용 → 최종 통과. 테스트 통과만으로 외형 검수를 대체하지 않았다.
- 객차 바닥은 첫 렌더에서 기존 지면과 같은 평면으로 겹쳐 지면 색이 드러났다. 시각 여유 0.012m를 적용한 후 입구·6인·빈 객차 렌더에서 금속 바닥을 확인했다.

### 재현과 남은 범위

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$enginePath = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $enginePath --headless --editor --import --path game --quit
& $enginePath --headless --path game --fixed-fps 60 res://tests/TraversalArtCheck.tscn
& $enginePath --path game --resolution 1440x900 --fixed-fps 60 res://tests/TraversalArtCheck.tscn
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py -- --checks-only
```

`--checks-only`는 구조 검사만 하며 렌더를 했다고 보고하지 않는다. 외형 검증은 위 창 모드 검사에서 재생성한다. 첫 생성의 Blender 13장과 별개로 이번 게임 렌더는 14장이다. 출력/로그는 버전 관리하지 않는다.

협곡 배경과 나머지 시안 지형/랜드마크는 여전히 기본 도형이다. 전용 잡기/매달리기/사다리 자세, 연출 카메라, 네트워크 탑승 상태, 실제 2PC·6인 장시간 플레이, 다른 GPU/저사양 성능은 남는다. 현재 정식 Stage 2 교체나 묶음 B/C 완료를 의미하지 않는다.
