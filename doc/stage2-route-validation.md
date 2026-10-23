# Stage 2 전체 동선 플레이·렌더 검수

2026-10-23. 기준 `011c882`. 사용자 요청: A·B·C·세이프하우스가 연결된 전체 경로를 실제 플레이와 렌더로 확인하고 발견한 문제 수정.

**Goal:** 시작부터 보상 분기·임무·곤돌라·세이프하우스까지 동일 세션의 실제 이동과 E 입력으로 검증한다.
**Architecture:** `Stage2RouteCheck` 전용 장면에서 기존 Player의 scripted controls와 InputEventAction을 사용한다. 시험용 순간이동·문 강제 개방·임무 타이머 종료를 사용하지 않는다. 게임이 원래 수행하는 사다리/짚라인/곤돌라 이동은 정상 동작이다.
**Tech Stack:** Godot 4.7.2 Mono, C#, Forward+.
**Spec:** `stage2-floorplan.md` 11~12장, `model-briefs/stage2-landmarks.md`, L-14/L-18/L-29.

## 계획

- [x] `game/tests/Stage2RouteCheck.cs`/`.tscn`: 연속 실제 이동·상호작용 검사. 감시탑 보급/짚라인, 다리 막힘, 협곡 펌프50초, 복귀 사다리, 감시소 양쪽 입구, 컨테이너 사다리3개, 트럭·크레인·정비창고 분기, 고지/닫힌 정문 우회, 곤돌라 승하차, 세이프하우스 E 클리어.
- [x] 헤드리스로 경로/검사 자체를 먼저 확인하고 문제를 재현한다. 실패하면 로그/좌표/실제 화면으로 원인을 구분하고 필요한 제품 변경만 적용한다.
- [x] Forward+ 창 모드로 같은 경로를 실행해 주요 지점과 이동 중 화면을 저장·직접 확인한다. 적과 사격을 켠 실행은 무적 자동 조작으로 기능 검수하며 난이도 평가와 구분한다.
- [x] 관련 회귀 검사와 독립 리뷰. `decisions.md`/`issues.md`에 확인된 원인·해결/유지 판단·재발 방지·남은 제약을 기록한다.

## 검수 초점

1. 현재 위치에서 E 프롬프트가 올바른 사다리·짚라인·버튼을 선택하는지.
2. 순서대로 진행할 때 임무/문/곤돌라 상태가 이어지는지.
3. 가림 중에도 충돌이 유지되고 거위 몸체가 모델과 겹치지 않는지.
4. 주 경로뿐 아니라 선택 보급과 높은 컨테이너에서 왕복 가능한지.
5. 전투/50초 임무 및 연출 뒤 정상 플레이와 세이프하우스 마무리가 가능한지.

실제2PC·4인 난이도·20분 목표 시간·정식 Stage 2 교체는 별도 범위다. 생성 로그/이미지는 `game/output/`에 두며 버전 관리하지 않는다.

## 발견·수정과 재현 근거

- I-114: 최초 전투 실행 `route-headless.log`는 펌프 방어 후 보상을 향해 걸을 때 오른쪽 앞벽 안쪽에서 멈췄다. 출입구를 먼저 나오는 경유점을 넣어 연속 플레이를 확인했지만, 독립 리뷰에서 공용 AI에도 같은 영향 가능성을 지적했다. `Stage2RouteRegression`을 추가해 적/보급 없는 실제 동료가 같은 좌표에서 8초간 정지함을 확인했다(`route-regression-red.log`).
- 막힌 칸의 가장 가까운 이웃만 고르는 가설도 실패했다(`route-regression-nav-green-hud-red.log`, `route-regression-probe.log`). 이 내부 공간은 1m 칸 중심만으로는 출구와 연결되지 않고 가까운 열린 칸도 벽 너머에 있었다. 해당 시도는 제거했다. 최종 수정은 공용 흐름장 비용/방향 계산을 그대로 두고, AI의 막힌 출발 칸에 한해 실제 벽을 가로지르지 않는 가까운 도달 가능 칸을 선택한다. 물리 이동이 몸통 폭을 계속 처리한다.
- 최종 국소 검사에서 실제 동료와 적이 출구로 나왔다. 동료 catch-up 순간이동0, 프레임 최대 이동 약0.08m. `route-regression-nav2.log`에서는 동료 탈출이 통과한 뒤 I-115 HUD 문구가 실패했다. `route-regression-green.log`는 동료/적 탈출 및 한국어/영어 다리 안내·Stage 1 셔터 유지 총8개 통과(종료 ObjectDB10개 경고, I-17 유지).
- I-115: 수정 전 창 모드 `route-10-pump-defense.png`에서 ‘셔터가 열릴 때까지 버티기’를 확인했다. 맵별 `CrescendoHoldOutKey`로 교정했다. 새 키는 두 언어 `fmt_objective_hold_bridge`이며 기존 기본 키는 유지한다.
- 검사 작성 오류도 구분한다: 반복 사다리 노드는 Godot가 이름을 바꾸므로 이름으로 찾던 검사가 실패했고 좌표로 식별하도록 바꿨다. 초기 C# Count 호출 누락/nullable 경고도 검사 작성 단계에서 수정했다. 확인된 제품 수정은 I-114/I-115이며, 후속 사용자 보고는 원인 미확정 I-116으로 별도 추적한다.
- 독립 리뷰는 연속 이동/E/자연 임무 종료를 확인하고 위 두 문제를 지적했다. 보급 결과 확인도 추가해 실제 장착한 무기 ID와 탄약 최대 예비량을 검사한다.

## 수정 전 연속 플레이 결과

- 헤드리스 `route-headless-2.log`:117검사,373.9초,사다리8회/짚라인1회/곤돌라1회,210처치,동시 적 최대61,사격1416회. ObjectDB6개 종료 경고.
- Forward+ `route-render.log`:162검사(45장 포함),362.0초,같은 이동 장치 사용,200처치,동시 적 최대53,사격1468회. 검사 장면의 순간이동/적 삭제/임무 생략 없음. 자동 사격으로 트럭 경보가 지정 근접 사진 전에 이미 켜질 수 있다.
- 초기는 별도 시안이며 지형/컨테이너는 여전히 크기 확인용 기본 도형이다. 모델이 없는 기존 시안 지형을 누락된 A/B/C 자산으로 취급하지 않는다.


## 다리 떨림 보고와 자동 조작 수정 (2026-10-24)

- 사용자가 실제 창을 보며 “앞뒤·좌우로 흔들리며 잘 못 건너감”을 보고했다. `route-render-final-2.log`는 복귀 사다리 이후 다리 중앙까지 약150초가 걸리고 출구에서180초 시간초과, `route-render-verified.log`는 다리 입구 정지다. 이 두 로그에는 당시 접촉/조준 대상 정보가 없어 원인 전체를 확정할 수 없다(I-116 열림).
- `Stage2BridgeCheck`는 두 실패 좌표에서 출발하는 실제 이동, 복귀 사다리 위부터 동쪽까지 이동, 다리 위26마리 전투 통과를 각각 검사한다. 국소 검사이므로 준비 단계의 순간이동/문 개방/적 배치는 사용한다. 순간이동 없는 전체 경로 검사와 구분한다. 고정 다리 충돌/높이 문제는 재현되지 않았고 모델·바닥·문 크기는 변경하지 않았다.
- `route-bridge-probe-7.log`: 다리 이후 하강 지점 `(255.90784,2.2275,-52.8774)`에서 적 충돌로 속도0, 사격 대상 없음, 탄약96/200·발사698회가 고정됨을 확인했다. 접촉 적 높이는1.2m, 체력38로 유지되었다. 원인은 검수 `Drive`의 높이 차이0.8m 필터다. 실제 게임은 `WeaponController.LowerTierTargets`로 하단 적을 사격할 수 있다.
- `bridge-target-red.log`는 해당 좌표와 실제 적을 배치해 타깃 선택 실패를 재현한다. `CanTarget`을 실제 발사 높이의 월드/소품 레이와 하단 사격 계약, 근접의 `TierMap.MeleeReach`에 맞췄다. `bridge-target-green.log`는 대상 선택 후 실제 총알로 제거까지 통과했다. 최근접 적의 작은 이동으로 매 프레임 몸 방향이 바뀌지 않도록 유효한 타깃을 유지한다(다른 적이2m 이상 가까우면 전환하는 검수 구현값).
- `bridge-render-final.log`: 무전투3경로·다리 군중 통과·낮은 적 실제 사격의5사례 통과, `bridge-*.png`11장 저장/확인, 종료 경고 없음. `bridge-combat-middle.png`에는 진행 중의 실제 군중, `bridge-combat-after.png`에는 동쪽 도착이 보인다.
- 초기 전투 검수의 다른 실패도 숨기지 않는다. `route-render-final.log`의 펌프 깊은 내부는 열린 칸이지만 격자 경로가 끊긴 곳이었다. 실제 동료는 기존 직선 복귀로 탈출하며 검수 입력에만 물리상 보이는 목표로 이동하는 복귀를 추가했다. `Stage2RouteRegression`의 깊은 내부 사례도 통과한다. `route-bridge-probe-4.log`는 펌프 출구 군중 접촉, `route-bridge-contact.log`는 컨테이너 구간의 실제 tether 구속이다. 1인 무적 자동 조작은 정상적인 협동 구조/구속 해제나 모든 무작위 전투의 생존을 보장하지 않는다. 디버그 구속 해제나 적 강제 삭제로 이 실패를 통과 처리하지 않았다.
- 독립 읽기 리뷰도 고정 지형/오래된 흐름장/반동만으로 이전 두 다리 실패를 설명할 근거가 부족함을 확인했다. 최신 재통과를 이전 실패의 원인 확정으로 표현하지 않는다.

## 최종 실행 결과

- 최종 `route-final-play.log`: Forward+ **171검사,45장**,367.6초,사다리8회/짚라인1회/곤돌라1회,189처치,동시 적 최대61,사격1210회. 실제50초 방어와 E 상호작용, 시작부터 세이프하우스 닫기까지 동일 세션. 종료 경고 없음.
- 직전 추적 실행 `route-bridge-trace.log`도171검사/45장,363.0초 통과. 실패를 지우거나 성공 실행만으로 무작위 전투의 안정성을 주장하지 않는다. `route-headless-final.log`126검사/364.3초 통과는 마지막 타깃 보정 전 결과이며 종료 ObjectDB6개 경고가 있다.
- 최종 이미지: `route-10-pump-defense.png`의 다리 안내, `route-11-bridge-return.png`/`route-12-bridge-crossed.png`, `route-23-gondola-ride.png`/`route-24-gondola-disembark.png`, `route-26-safehouse-inside.png`/`route-28-cleared.png`. 이미지가 같은 이름으로 재생성되므로 마지막 창 모드 실행 기준이다.
- 빌드 경고/오류0. 에셋 가져오기 성공. 아래 회귀는 최종 제품 코드(I-114/I-115) 기준으로 실행했고 이후 제품 코드는 변경하지 않았다. 검사 조작/문서만 후속 변경했다.

| 장면 / 로그 접두사 `route-final-` | 통과 | 종료 ObjectDB 경고 |
|---|---:|---:|
| Stage2RouteRegression | 10 | 9 |
| SizeDraftCheck | 74 | 없음 |
| ElevationCheck | 76 | 4 |
| SquadCheck | 51 | 없음 |
| EnemyAttackCheck | 79 | 없음 |
| SpawnCheck | 41 | 12 |
| TraversalArtCheck | 149 | 없음 |
| LandmarkArtCheck | 63 | 4 |
| LandmarkCArtCheck | 56 | 없음 |
| SafehouseArtCheck | 54 | 8 |
| StageTransitionCheck | 191 | 없음 |
| Integration | 149 | 2 |

ObjectDB 종료 경고는 I-17로 유지한다. 이 실행들의 잔존 객체를 각각 분석한 것은 아니다. 기능 통과와 종료 경고를 구분한다.

## 재실행

저장소 루트 PowerShell에서 실행한다. `-Test`만으로 아래 전용 장면이 실행되는 것은 아니다.

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --headless --editor --path game --import
& $godot --headless --path game --fixed-fps 60 res://tests/Stage2RouteRegression.tscn
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2BridgeCheck.tscn
& $godot --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2RouteCheck.tscn
& $godot --headless --path game res://tests/Integration.tscn
```

`Integration`은 실시간 오디오 타이밍 검사가 있어 `--fixed-fps`를 쓰지 않았다. 모든 로그·이미지는 로컬 재생성 산출물(`game/output/`)이며 커밋하지 않는다. 정식 Stage 2 교체·손 조작 체감·실제2PC·4인 난이도·20분 목표와 저사양 성능은 여전히 별도다.

- 마지막 검사 조작 수정/가져오기 후 재실행: `bridge-final-headless.log` 5사례 통과(ObjectDB27개 종료 경고), `route-regression-final-2.log`10검사 통과(종료 경고 없음). `git diff --check` 통과; Git의 기존 CRLF→LF 정규화 안내는 별도다.


## 커밋·최신 main 확인 후속 (2026-10-24)

- 사용자 요청으로 현재 수정분을 main에 커밋하고 최신 원격 main 병합 후 푸시한다. 최초 fetch 결과 `origin/main`은 기준 `011c882`와 동일(앞섬/뒤처짐0/0)이었다.
- 사용자는 수동 플레이에서 현재 멈춤이 재현되지 않는다고 보고했다. I-116은 이 관찰을 추가하되 원인 미확정/열림 상태를 유지한다. 이번에는 제품 코드·모델·임시 수치를 추가로 바꾸지 않았다.
- 빌드 오류/경고0, 에셋 가져오기 성공(오류/경고 없음), `git diff --check` 통과. 창 모드171검사/45장 및 다리11장 렌더는 앞선 동일 제품 코드의 결과이며 이번 커밋 단계에서 창 모드를 다시 실행했다고 주장하지 않는다.
- 관련 전용 장면과 통합 검사를 현재 트리에서 재실행했다. 로그는 `game/output/route-push-<장면>.log`. 생성 산출물 및 `.codex-remote-attachments/`는 커밋 대상에서 제외한다.

| 장면 | 통과 | ObjectDB 종료 경고 |
|---|---:|---:|
| Stage2RouteRegression | 10 | 없음 |
| Stage2BridgeCheck | 5사례 | 22 |
| SizeDraftCheck | 74 | 없음 |
| ElevationCheck | 76 | 없음 |
| SquadCheck | 51 | 8 |
| EnemyAttackCheck | 79 | 17 |
| StageTransitionCheck | 191 | 6 |
| Integration | 149 | 4 |

종료 ObjectDB 경고는 기존 I-17로 유지한다. I-114/I-115의 해결과 I-116의 원인 미확정 상태를 구분하며, 이번 확인으로 새 제품 결정이나 새 결함 ID를 추가하지 않는다.
