# Stage 2 묶음 C 제작·연결 계획과 검수

2026-10-23. 기준 `ee0a782`. 사용자 요청으로 [브리프](model-briefs/stage2-landmarks.md) 1·4·5장을 읽고 Blender 생성·게임 연결·실제 렌더까지 수행한다. 최초 스크립트 전용 역할 분담보다 이번 직접 실행 요청을 우선한다. 기존 A/B는 보존한다.

**Goal:** 감시탑·크레인·경보 트럭·벙커 정문을 별도 Stage 2 시안에서 실제로 쓰고 검증한다.
**Architecture:** 독립 Blender 생성기/GLB 검수, 기존 `PropLibrary`와 `PropOcclusion`, 선택적 모델을 받는 `AlarmCar`, `LayoutMap` 배치를 사용한다. 현재 높이 단 물리 바닥과 정식 Stage 2는 유지한다.
**Tech Stack:** Blender 5.2.2 bpy, GLB, Godot 4.7.2 Mono/C#.
**Spec:** `doc/model-briefs/stage2-landmarks.md` 4장, `stage2-floorplan.md`, L-8·L-15·L-29·A-22·A-25.

## 제약·검수 초점

- `plushpanic_outdoor_v1`, 미터, Blender Z-up→GLB Y-up, 모델당6재질 이하/텍스처 없음. 루트/Keep/Occlude/메시 명명 계약과 모델별 삼각형 예산 준수.
- 감시탑14×10m, 상판4.8m·지붕7.5m. 남쪽 사다리 출입구와 기존 짚라인 기둥/줄이 지붕·난간을 관통하지 않게 후면 반지붕 앞쪽 일부를 비운다. 상판은 게임 소유, 아래 통행은 기존처럼 불가.
- 크레인14m·팔18m(−X6/+X12), 밑4m Keep. 몸체 바닥 부근 충돌만 별도로 둔다.
- 트럭5.2×2.2m·높이2.2m, 추천 충돌5.2×2.0×2.2. 사격/폭발 발동, AI 총알 제외, 1회성·시간 종료·사이렌·웨이브를 기존 동작으로 검사한다. 부딪힘 발동을 새로 추가하지 않는다.
- 정문8×4m·충돌깊이1.4m, 배경용 닫힌 문. 주 경로·곤돌라 접근을 막지 않아야 한다.
- 전체 가림은 보수적 AABB 판정이며 Keep 불투명과 가림 해제까지 확인한다. 기존 A/B manifest 항목·GLB 보존, 수치/문서/코드 일치 확인.

## 실행 순서

- [x] `LandmarkCArtCheck.cs/.tscn` 필수 모델/연결 검사를 먼저 실패시킨다.
- [x] `prototypes/outdoor-kit/tools/stage2_props_c.py`로4종 생성. 원본 `source/stage2-props-c.blend`, GLB4개, manifest는 C만 병합한다.
- [x] 독립 `stage2_props_c_review.py`로 재가져오기·실제 치수/피벗/부위/예산·사다리/짚라인 공간 검수와 앞뒤 렌더를 만든다.
- [x] `LayoutMap` 감시탑 외형·랜드마크 배치, `AlarmCar` 선택적 외형/발광 재질을 연결한다. 기존 기본 경보차는 유지한다.
- [x] 전용 장면에서 실제 사다리 승강·보급 접근·짚라인 출발/복귀, 크레인/정문 가림, 트럭 사격·폭발·재발동 방지·시간 종료·AI 제외를 검사하고 Forward+ 화면을 저장/검수한다.
- [x] SizeDraft/StageTransition/Spawn/기존 A·B/Integration 회귀 검사. 독립 리뷰 후 필요한 수정·재검증.
- [x] 결정·이슈·체크리스트·브리프·평면도와 최종 결과/경고/재현 명령 갱신. 이번 요청은 제작·연결까지이며 커밋/푸시는 별도다.

## 최종 결과

| 자산 | 삼각형 / 예산(미만) | 재질 | 게임 X/Y/Z 전체 크기(m) | 부위 |
|---|---:|---:|---|---|
| `watchtower` | 3076 / 6000 | 5 | 14 / 8.2 / 10 | Keep, Occlude_Top |
| `crane` | 1920 / 4000 | 3 | 18.075 / 14 / 2.4 | Keep, Occlude_Mast |
| `alarm_truck` | 1536 / 4500 | 6 | 5.2 / 2.2 / 2.22 | Keep |
| `bunker_gate` | 1364 / 5000 | 5 | 8 / 4 / 1.4 | Keep, Occlude_Gate |

- [생성기](../prototypes/outdoor-kit/tools/stage2_props_c.py), [독립 GLB 재가져오기/렌더 검사](../prototypes/outdoor-kit/tools/stage2_props_c_review.py), [원본 blend](../prototypes/outdoor-kit/source/stage2-props-c.blend), [manifest](../game/assets/stage2-props/manifest.json), [게임 전용 검사](../game/tests/LandmarkCArtCheck.cs).
- 감시탑 바닥은 게임이4.8m에 깔고 그림만0.3m 두께로 만든다. 기존 물리 높이 단은 유지한다. 꼭대기7.5m, 깃발 포함8.2m. 처마7.15m/밑면7.03m 이상, 남쪽 사다리 입구 폭2.4m, 동쪽 짚라인 출발 지붕/난간 공간을 비웠다.
- 크레인 철골/줄/갈고리는 시각 전용이며 기초2.4×4×2.4m만 충돌한다. 수평 팔은 X−6..12m, 갈고리 곡선 끝 때문에 전체 실측 X길이18.075m다. 트럭 허브 포함 폭2.22m는 규격 허용오차 안이며 충돌은 정확히5.2×2×2.2m다.
- 트럭은 기존 `AlarmCar`의 이벤트/동기화 진입점을 사용한다. 게임이 Red/Hazard 재질을 각 인스턴스에 복제해 대기 점멸/경보 점멸을 연결한다. 플레이어 사격·폭발, AI 총알 제외, 실제 사이렌 스트림, 호드 호출, 설정시간 종료/발광 종료/재발동 방지까지 검사했다. 기존 기본 차량은 기존 외형을 사용한다.
- 벙커 정문은8×4×1.4m 막힌 배경이다. 동쪽으로 실제 걸어 우회하고 곤돌라 안에 탑승하는 경로를 검사했다. 문 개방 기능은 없다.
- A/B의 manifest9항목·기존 메타데이터 및 GLB9개 SHA-256이 기준 HEAD와 동일함을 확인했다. 다른 생성 도구는 수정하거나 import하지 않았다.

## 실행한 검사

| 검사 | 결과 | 로컬 로그 |
|---|---|---|
| C# 빌드 | 오류0/경고0 | `dotnet build game/HordeShooter.csproj --nologo -v q` |
| Blender 생성 | 4종 계약/예산/치수 통과 | `game/output/c-build.log` |
| 독립 GLB 재가져오기 | **101개 통과 / Cycles8장** | `game/output/c-review.log`, `stage2-props-c-review/report.json` |
| Godot 가져오기 | 종료0/에셋 오류 없음 | `game/output/c-import.log` |
| LandmarkCArtCheck 헤드리스 | **56개 통과** | `game/output/c-LandmarkCArtCheck.log` |
| LandmarkCArtCheck Forward+ | **71개 통과 / 1440×900 실제 게임15장** | `game/output/c-render.log` |
| SizeDraftCheck | **74개 통과**, 전체2.6분·곤돌라1회, 펌프 왕복22초·사다리1회 | `game/output/c-SizeDraftCheck.log` |
| StageTransitionCheck | **187개 통과**, 전체60자산 로드/기본 문 동작 | `game/output/c-StageTransitionCheck.log` |
| SpawnCheck | **41개 통과**, 기본 차량·AI·호드 회귀 | `game/output/c-SpawnCheck.log` |
| TraversalArtCheck | **149개 통과**, A 이동/6인 승하차 회귀 | `game/output/c-TraversalArtCheck.log` |
| LandmarkArtCheck | **63개 통과**, B 임무/통행/가림 회귀 | `game/output/c-LandmarkArtCheck.log` |
| Integration.tscn 직접 실행 | **149개 통과**, 실제 시간으로 실행 | `game/output/c-Integration.log` |

검사 통과와 종료 경고는 구분한다. 회귀 실행의 SizeDraft/StageTransition은 각각 ObjectDB6개 종료 경고가 남았다(I-17 유지). 이 실행의 C 헤드리스/창 모드, Spawn, Traversal, Landmark, Integration 및 Blender 생성/검수/가져오기는 경고 없음. 회귀 검사는 C 연결 후 실행했고 이후 감시탑 외형 수정은 C 전용 헤드리스/실제 렌더 및 독립 GLB 검사로 재검증했다.

## 발견·수정 및 독립 리뷰

1. 모델 생성 전 `watchtower` 미등록으로 필수 자산 검사가 실패함을 확인했다(`c-red.log`). 검사 작성 중 컬렉션 확장 메서드 using 누락을 수정한 뒤 빌드가 통과했다.
2. 독립 리뷰가 초기 감시탑의 낮은 처마와 짚라인 출발 직후 지붕 관통을 지적했다. 짚라인 `t=.03`의 발 위치는 `(125.56,5.48832,-75.4)`이고 기존 지붕6.268~6.388m가 거위2.18m 몸 안에 들어왔다. 처마를 높이고 출발 동쪽 끝까지 지붕 홈 및 난간 출구를 넓혔다. 가림으로 감추는 방식에 의존하지 않는다.
3. 실제 Godot GLB 정점과 `RidePoint`로 거위0.96×2.18×0.96m 상자17지점의 교차를 검사한다. 이전 가져오기 모델로 실패(`c-clearance-red.log`) → 수정 모델 재가져오기 후 통과했다. 양쪽 처마 아래 머리 공간과 Blender 독립 검사도 보강했다. 대각선 동선을 큰 직육면체 하나로 검사하던 중간 검수 도구의 과대 범위는 실제 경로 표본으로 바로잡았다.
4. 최종 10프레임 짚라인 출발 렌더와 감시탑 앞뒤 렌더를 확인했다. 후속 독립 리뷰에서 P2 해결과 남은 차단 지적 없음. 리뷰는 정적 검토·로그/이미지 확인이며 별도 실행 결과와 구분한다.

## 렌더

Blender8장: `game/output/stage2-props-c-review/<id>-front.png`, `<id>-back.png`.

게임15장: `game/output/landmark-c-` 뒤 `tower-approach`, `tower-climb`, `tower-interior`, `tower-zipline`, `tower-roof-clearance`, `tower-departure`, `crane-approach`, `crane-overview`, `crane-occlusion`, `bunker-closed`, `bunker-gondola`, `bunker-occlusion`, `truck-idle`, `truck-alarm`, `truck-spent`와 `.png`.

일반 이동/가림 검사는 원래 야외 카메라, 트럭은 기본 근접 줌이다. `crane-overview`만 높이14m 전체 외형 확인을 위해 테스트 중 시야폭36m로 넓혔다가 복원했다. 제품 카메라 설정 변경은 없다.

![감시탑 짚라인 출발 공간](../game/output/landmark-c-tower-roof-clearance.png)
![크레인 전체](../game/output/landmark-c-crane-overview.png)
![트럭 경보](../game/output/landmark-c-truck-alarm.png)
![벙커 정문](../game/output/landmark-c-bunker-closed.png)

`output/`은 로컬 검사/렌더 재생성 결과이며 버전 관리 대상이 아니다.

## 재현

저장소 루트 PowerShell:

```powershell
$blenderC = 'C:/Util/Blender Foundation/Blender 5.2/blender.exe'
$godotC = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $blenderC --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_c.py
& $blenderC --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_c_review.py
dotnet build game/HordeShooter.csproj --nologo -v q
& $godotC --headless --editor --import --path game --quit
& $godotC --headless --path game --fixed-fps 60 res://tests/LandmarkCArtCheck.tscn
& $godotC --path game --resolution 1440x900 --fixed-fps 60 res://tests/LandmarkCArtCheck.tscn
foreach ($sceneC in @('SizeDraftCheck','StageTransitionCheck','SpawnCheck','TraversalArtCheck','LandmarkArtCheck')) {
    & $godotC --headless --path game --fixed-fps 60 "res://tests/$sceneC.tscn"
}
& $godotC --headless --path game res://tests/Integration.tscn
./game/Start-Game.ps1 -Stage2Draft
```

`-Stage2Draft`는 직접 플레이용이다. `Start-Game.ps1 -Test`는 Integration만 실행하므로 전용 검사를 대신하지 않는다. Integration은 시간 기반 오디오 검사가 있어 위 명령처럼 `--fixed-fps` 없이 실행한다.

## 남은 범위

- 별도 `LayoutMap` 시안에 적용했으며 정식 Stage 2 교체·메뉴 연결·세이프하우스 모델·감시소 부활은 후속이다. 실제2PC 동기화와20분 전투/보급 밸런스는 검증하지 않았다.
- 감시탑 아래 통행은 기존 높이 단 충돌로 막혀 있다. 지형은 계속 크기 시안이다. 가림은 부위 전체 AABB 판정이며 바닥/Keep·물리/흐름장과 별개다. 다른 야외 키트 전체의 가림이나 젖음/적설은 이번 범위가 아니다.
- 최초 제작 요청에서는 커밋/푸시하지 않았으며, 아래 후속 요청에서 원격 반영을 진행한다. 기존 미추적 `.codex-remote-attachments/`는 건드리지 않았다.

## 커밋·최신 main 확인 후속 (2026-10-23)

- 사용자 요청으로 묶음 C 자산·원본·생성/검수 도구·게임 코드·테스트·관련 문서를 함께 커밋하고 원격 main에 반영한다. 브랜치는 `main`, 원격은 `https://github.com/minidice/game-plushpanic-hordeshooter.git`이다.
- `git fetch origin main` 결과 원격/로컬 기준은 모두 `ee0a782`, 앞섬/뒤처짐0/0이었다. `git merge --ff-only origin/main`은 이미 최신 상태로 끝났다. 추가 원격 변경이나 병합 충돌은 없으며 강제 푸시를 사용하지 않는다.
- 현재 트리 새 빌드: 오류0/경고0. Godot 가져오기 종료0. `LandmarkCArtCheck`56, `SizeDraftCheck`74, `StageTransitionCheck`187, `SpawnCheck`41, `TraversalArtCheck`149, `LandmarkArtCheck`63, `Integration`149 모두 종료0/통과. Integration은 고정 FPS 없이 실제 시간으로 직접 실행했다.
- 이번 ObjectDB 종료 경고: StageTransition6개, LandmarkArt4개, Integration2개. C/SizeDraft/Spawn/Traversal에는 없음. 기존 I-17을 유지하고 검사 실패와 구분한다. 로그는 `game/output/c-publish-<장면명>.log`, 가져오기는 `c-publish-import.log`다.
- 앞의 최종 렌더 이후 외형/동작 변경은 없다. A/B manifest9항목 및 GLB9개 해시 보존, 문서 링크·UTF-8·diff 공백 검사를 확인했다. 기존 `.codex-remote-attachments/`와 output/캐시/빌드 산출물은 커밋에서 제외한다.
