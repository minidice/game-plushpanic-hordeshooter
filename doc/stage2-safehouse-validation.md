# Stage 2 세이프하우스 제작·연결 계획과 검수

2026-10-23. 기준 `4df32c7`. 사용자 요청으로 남은 세이프하우스를 Blender로 만들고 별도 Stage 2 시안에 연결·실제 렌더 검수한다.

**Goal:** 기존 14×12m 도착 건물을 모델로 교체하고 실제 출입·보급 접근·문 닫기/클리어·가림을 확인한다.
**Architecture:** 독립 Blender 생성기/재가져오기 검사, `stage2-props` manifest에 `safehouse`/`safehouse_door` 추가. `LayoutMap`의 벽 충돌·게임 소유 바닥·Door/Stage의 클리어 규칙을 재사용한다.
**Tech Stack:** Blender5.2.2 bpy, GLB, Godot4.7.2 Mono/C#.
**Spec:** `stage2-floorplan.md` 12장, `LayoutMap.BuildSafehouse`, `Stage.TryCloseSafehouse`, L-14·L-29·A-22·A-25, 아래 모델 세부 규격.

## 설계와 모델 규격

전용 세이프하우스 모델 지시문은 아직 없다. 기존 평면도/코드가 확정하는 방 크기·충돌·문 규칙을 바탕으로 아래 조형을 구현 판단으로 기록한다. 사용자 확정 수치나 신규 게임 규칙으로 취급하지 않는다.

- 별도 시안에만 적용. 정식 Stage 2·메뉴·부활(M-32)·멀티 동기화는 변경하지 않는다.
- 두 가지 구현 방식 중 건물+문 분리 GLB를 채택한다. 한 GLB에 문까지 합치면 기존 Door의 이동/숨김을 분리하기 어렵고, 문을 기본 상자로 남기면 도착 건물 외형 교체가 불완전해진다. 현재 선택적 Door visual과 잘 맞는 두 모델을 사용한다.
- `safehouse`: 기존 외곽14×12m, 벽두께0.4m/높이2.6m. 서쪽 X−7..−6.6 벽 가운데 Z±1.3m 출입구, 입구 유효 높이2.4m. 콘크리트 벽·초록 띠·강철 프레임/환기구·후면 반지붕/안전 표시. 카메라 쪽은 낮은 벽만 남기도록 상부를 가림 대상에 둔다. 바닥판은 만들지 않는다. 천장 최저3m 이상으로 거위2.18m 머리 공간 확보. 지붕까지 약3.5m(세부 실측은 결과표).
- `Keep`: 아래1.2m 벽·초록 띠·안쪽 문 제어판. `Occlude_Walls`: 위쪽 벽/문틀/반지붕·표시등. 아래/위 벽의 접합은 정확히 맞추고 겹친 면을 만들지 않는다. 모델 장식은 기존 벽 충돌 가까이 붙여 통행/도착 탄약을 막지 않는다.
- 제어판은 안쪽 서쪽 벽의 문 옆, 게임 로컬 `(-6.5,1.4,1.8)`에 위를 향한 빨간 버튼을 둔다(Keep 벽1.2m보다 높여 읽힘 확보, 제어판은 Keep 높이 예외). E 대상도 그 위치로 맞추며 사용 반경1.9m와 클리어 조건은 유지한다.
- `safehouse_door`: 게임 X두께0.3m/Y높이2.4m/Z폭2.6m, 바닥 중심 피벗. 둥근 철문·경고 줄·손잡이. Keep 하부/상부 Occlude_Door 분리, 외형만 이동. 기존 +Z2.5m 열림 이동·완전 열림 시 벽 속 숨김·닫힘 복원 사용. 닫힌 Collider/흐름장은 원래 크기 유지.
- 공통 계약: `plushpanic_outdoor_v1`, Blender Z→GLB Y, 피벗 바닥0, 루트/Keep/Occlude/각 부위당 메시1개,6재질 이하/텍스처 없음/팔레트 선형변환. 삼각형 예산 건물6000미만·문1800미만(이번 제작 기준). 기존 A/B/C 메타데이터·GLB 보존.

## 실행 계획

- [x] 필수 모델 미등록 검사를 작성하고 실패 확인.
- [x] `stage2_safehouse.py` 독립 생성기와 `stage2_safehouse_review.py` 독립 재가져오기/렌더 검사, 원본 blend·GLB2종 준비.
- [x] `LayoutMap.BuildSafehouse` 그림만 교체하고 제어판 위치 연결. 기존 충돌·목표·보급 유지.
- [x] `SafehouseArtCheck`로 실제 걸어 출입/탄약/제어판 접근, 문 개방/닫힘/재개방, 적/팀원 조건, E 입력 클리어, 가림/복원 및 실제 렌더 검사.
- [x] SizeDraft/StageTransition/기존 A·B·C/Integration 회귀, 독립 리뷰와 수정 검증.
- [x] decisions/issues/체크리스트/평면도/브리프 및 이 보고서에 최종 수치·경고·명령·제약 갱신. 커밋/푸시는 별도 요청 때 진행.

## 최종 모델·게임 결과

| 모델 | 삼각형 / 예산(미만) | 재질 | 게임 X/Y/Z 전체 크기(m) | 부위 |
|---|---:|---:|---|---|
| `safehouse` | 2376 / 6000 | 6 | 14.1615 / 3.75 / 12.0965 | Keep, Occlude_Walls |
| `safehouse_door` | 616 / 1800 | 3 | 0.3 / 2.4 / 2.6 | Keep, Occlude_Door |

장식 포함 전체 크기이며 벽 외곽은 기존14×12m다. 방과 문의 상부는 둘레 전체/이동 판이므로 후면 무게중심 조건의 명시적 예외다. 같은 팔레트/피벗/부위 명명·텍스처 없음 계약은 유지한다.

- [생성기](../prototypes/outdoor-kit/tools/stage2_safehouse.py), [독립 GLB 재가져오기/렌더 검사](../prototypes/outdoor-kit/tools/stage2_safehouse_review.py), [원본 blend](../prototypes/outdoor-kit/source/stage2-safehouse.blend), [manifest](../game/assets/stage2-props/manifest.json), [게임 검사](../game/tests/SafehouseArtCheck.cs).
- `LayoutMap.BuildSafehouse`가 기존 RoomWall 충돌을 그대로 두고 그림만 GLB로 교체한다. 게임의 바닥4.8m를 그대로 사용하며 모델 바닥을 추가하지 않았다. 도착 탄약과 기존 조명 위치/세기/시야 그룹은 유지한다. 중앙 시안 표식 기둥/라벨은 제거하고 기존 조명을 반지붕 가장자리의 모델 지지대에 연결했다.
- 서쪽 슬라이딩 철문은 기존 `Door`에 선택적 GLB를 전달한다. 열리면+Z2.5m로 이동한 뒤 벽 속에 숨고, 닫힐 때 원래 위치·가시성·충돌을 복구한다. 기본 Stage1/Stage2 및 B 다리 문 구현을 바꾸지 않았다.
- 제어판은 위를 향한 빨간 버튼이며 모델과 E 위치가 게임로컬`(-6.5,1.4,1.8)`로 일치한다. 사용 반경1.9m 유지. 실제 게임에서 거위가 문 옆으로 걸어 접근하고 E 입력으로 문을 닫아 클리어했다.
- 같은 PC에서 살아 있는 사람2명을 만들어 전원 입실 전 사용 금지, 전원 입실 후 활성, 적이 실내에 있으면 거부를 확인했다. 이는 기존 규칙 검증이며 실제2PC 네트워크 검증은 아니다.
- 기존 A/B/C manifest13항목·메타데이터 및 GLB13개의 SHA-256이 기준HEAD와 동일하다. 기존 생성기를 import/수정하지 않았다.

## 실행한 검사

| 검사 | 결과 | 로컬 로그/산출물 |
|---|---|---|
| C# 빌드 | 오류0/경고0 | `dotnet build game/HordeShooter.csproj --nologo -v q` |
| Blender 생성 | 건물/문2종 생성, 계약 단언 통과 | `game/output/safehouse-build.log` |
| 독립 GLB 재가져오기 | **42개 통과 / Cycles4장** | `game/output/safehouse-review.log`, `stage2-safehouse-review/report.json` |
| Godot 가져오기 | 종료0, 에셋 오류 없음 | `game/output/safehouse-import.log` |
| SafehouseArtCheck 헤드리스 | **54개 통과** | `game/output/safehouse-headless.log` |
| SafehouseArtCheck Forward+ | **63개 통과 / 1440×900 실제 렌더9장** | `game/output/safehouse-render.log` |
| SizeDraftCheck | **74개 통과**, 전체2.6분/곤돌라1회 | `game/output/safehouse-SizeDraftCheck.log` |
| StageTransitionCheck | **191개 통과**, 전체62자산/기본문 | `game/output/safehouse-StageTransitionCheck.log` |
| TraversalArtCheck | **149개 통과** | `game/output/safehouse-TraversalArtCheck.log` |
| LandmarkArtCheck | **63개 통과** | `game/output/safehouse-LandmarkArtCheck.log` |
| LandmarkCArtCheck | **56개 통과** | `game/output/safehouse-LandmarkCArtCheck.log` |
| Integration 직접 실행 | **149개 통과** | `game/output/safehouse-Integration.log` |

전용 헤드리스는 ObjectDB8개, Integration은2개 종료 경고가 남았다. 전용 창 모드와 이번 시안/전환/A/B/C 회귀에는 종료 경고 없음. Blender/가져오기 오류·경고 없음. 종료 경고는 기능 검사 통과와 구분하며 I-17을 유지한다. 회귀 검사는 최초 연결 후 실행했고, 이후 버튼 외형/좌표·중앙 표식 제거·조명 지지대 수정은 전용 헤드리스/실제 렌더 및 독립 GLB 검사로 재검증했다.

## 발견·수정·리뷰

1. 모델 생성 전 필수 자산 미등록 검사가 예상대로 실패했다(`safehouse-red.log`).
2. 최초 독립 GLB 검사에서 출입구 조명 하단2.38m가 유효 높이2.4m 안으로 내려오는 것을 확인했다.3cm 올린 뒤 출입구와 내부 거위2.18m 몸체/머리 공간 검사가 통과했다.
3. 실제 게임 렌더에서 기존 빨간 시안 표식이 새 건물 중앙에 남았으며 독립 리뷰도 지적했다. 해당 marker의 그림만 제외했다. 초기1.1m 세로 버튼은 낮은 Keep 벽에 가려져,1.4m 위를 향한 버튼과 같은 위치의 E 대상으로 수정했다. 반지붕 가장자리 지지대로 기존 조명 부착도 표현했다.
4. 독립 리뷰는 계약·벽/문 충돌·버튼 축/좌표·기존 데이터 보존·실제 화면을 검토했다. 후속 검토에서 표식 제거/버튼 가시성·지지대/통과 공간과 최종 로그를 확인했고 남은 구체적 지적 없음. 리뷰는 읽기/이미지·로그 확인이며 별도 실행 검사는 위 표와 구분한다.

## 실제 렌더

Blender4장: `game/output/stage2-safehouse-review/safehouse-front.png`, `safehouse-back.png`, `safehouse_door-front.png`, `safehouse_door-back.png`.

게임9장: `game/output/safehouse-` 접두사 뒤 `approach`, `closed-exterior`, `inside-wide`, `inside-close`, `ammo`, `exited`, `button`, `closing`, `cleared`와 `.png`. 기본 야외 원근 및 Tab 근접 줌을 사용했다. 테스트 중 적이 있을 때의 거부 안내는 적 제거 뒤에도 잠시 남는 기존 HUD 표시 시간이다.

![입구와 열린 문](../game/output/safehouse-approach.png)
![문 옆 버튼과 가림 처리](../game/output/safehouse-button.png)
![실제 E 입력 후 클리어](../game/output/safehouse-cleared.png)

`output/`은 재실행하면 생기는 로컬 산출물로 Git에 넣지 않는다.

## 재현

저장소 루트 PowerShell:

```powershell
$blenderSafe = 'C:/Util/Blender Foundation/Blender 5.2/blender.exe'
$godotSafe = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $blenderSafe --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_safehouse.py
& $blenderSafe --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_safehouse_review.py
dotnet build game/HordeShooter.csproj --nologo -v q
& $godotSafe --headless --editor --import --path game --quit
& $godotSafe --headless --path game --fixed-fps 60 res://tests/SafehouseArtCheck.tscn
& $godotSafe --path game --resolution 1440x900 --fixed-fps 60 res://tests/SafehouseArtCheck.tscn
foreach ($sceneSafe in @('SizeDraftCheck','StageTransitionCheck','TraversalArtCheck','LandmarkArtCheck','LandmarkCArtCheck')) {
    & $godotSafe --headless --path game --fixed-fps 60 "res://tests/$sceneSafe.tscn"
}
& $godotSafe --headless --path game res://tests/Integration.tscn
./game/Start-Game.ps1 -Stage2Draft
```

`-Stage2Draft`는 직접 플레이용. `-Test`는 Integration만 실행하므로 전용 장면을 대신하지 않는다. Integration은 시간 기반 오디오 검사 때문에 고정FPS 없이 실행했다.

## 남은 범위

- 별도 크기 시안 적용. 정식 Stage 2 교체·메뉴 연결·M-32 부활·실제2PC 동기화·20분 전투/보급 밸런스는 후속이다.
- 건물 주위/내부 지형은 기존 시안 바닥이다. Keep는 그대로 보이고 상부 전체를 AABB로 가림 완화한다. 다른 팩의 모든 가림이나 젖음·적설은 이번 범위가 아니다.
- 최초 제작 요청에서는 커밋·푸시하지 않았으며, 아래 후속 요청에서 원격 반영을 진행한다. 기존 미추적 `.codex-remote-attachments/`는 건드리지 않았다.

## 커밋·최신 main 확인 후속 (2026-10-23)

- 사용자 요청으로 세이프하우스 GLB2종·원본 blend·생성/검수 도구·게임 코드·테스트·관련 문서를 함께 커밋하고 원격 main에 반영한다. 브랜치는 `main`, 원격은 `https://github.com/minidice/game-plushpanic-hordeshooter.git`이다.
- `git fetch origin main` 결과 원격/로컬 기준은 모두 `4df32c7`, 앞섬/뒤처짐0/0이었다. `git merge --ff-only origin/main`은 이미 최신 상태로 끝났다. 추가 원격 변경이나 병합 충돌은 없으며 강제 푸시를 사용하지 않는다.
- 현재 트리 새 빌드: 오류0/경고0. Godot 가져오기 종료0. `SafehouseArtCheck`54, `SizeDraftCheck`74, `StageTransitionCheck`191, `TraversalArtCheck`149, `LandmarkArtCheck`63, `LandmarkCArtCheck`56, `Integration`149 모두 종료0/통과. Integration은 고정 FPS 없이 실제 시간으로 직접 실행했다.
- 이번 ObjectDB 종료 경고: StageTransition6개, Integration2개. 나머지5장면에는 없음. 기존 I-17을 유지하며 기능 검사 실패와 구분한다. 로그는 `game/output/safehouse-publish-<장면명>.log`, 가져오기는 `safehouse-publish-import.log`다.
- 앞의 최종 Blender/게임 렌더 이후 외형·동작 변경은 없어 렌더는 재실행하지 않았다. 기존 A/B/C manifest13항목과 GLB13개 해시 보존, 관련 문서 링크·UTF-8를 확인했다. 기존 `.codex-remote-attachments/`와 output/캐시/빌드 산출물은 커밋에서 제외한다.
