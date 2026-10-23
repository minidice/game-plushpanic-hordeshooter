# Stage 2 묶음 B 모델·게임 연결 검수

2026-10-23. 사용자 요청으로 [브리프](model-briefs/stage2-landmarks.md) 1·3·5장을 기준으로 Blender 생성·게임 연결·실제 렌더까지 진행한다. 최초 스크립트 전용 역할 분담보다 이번 요청을 우선한다. 기준 커밋 `e781c04`, 기존 A 자산과 생성기는 보존한다.

## 구현 계획

실행은 `superpowers:executing-plans`, 검사 우선·마지막 독립 리뷰로 진행한다. 별도 시안 `LayoutMap`에 적용하며 정식 Stage 2 교체는 하지 않는다.

- [x] 독립 생성기 `stage2_props_b.py`: 6m 다리 조각(2000삼각형 미만), 들린 판(1800 미만), 펌프장/감시소(각5000 미만), 6재질 이하. 기존 생성기 도우미는 복사하며 import/수정하지 않는다. manifest의 다른 묶음과 메타데이터를 보존한다.
- [x] 실제 Blender 생성·GLB 재가져오기 구조/정점/치수/재질 검수. `bridge_leaf`는 브리프대로 `Occlude_Leaf`만 갖는다. 앞쪽 난간 `Occlude_Rail`은 카메라 쪽이므로 뒤쪽 무게중심 조건의 예외다.
- [x] `LayoutMap` 다리 30m에 조각5개. 다리 아래 시안 상자의 **그림만** 걷어내고 얇은 상판을 그린다. 기존 높이 단/충돌/다리 밑 통행 불가 계약 유지. `Door`는 선택적 모델을 움직여 기존 개방/충돌/흐름장 동작을 보존하며 완전 개방 후 판을 숨긴다.
- [x] 펌프 밸브의 모델 기준점과 상호작용을 맞춘다. 앞쪽 3m 개구부 접근을 유지하고 게임이 벽/설비 충돌을 둔다. 감시소는 12×10m·0.4m 벽·±X 3m 출입구의 기존 충돌을 그대로 두고 외형만 교체한다. A에서 만든 부위 가림 처리를 재사용한다.
- [x] `LandmarkArtCheck`: 필수모델/배치/실제 메시 높이, 닫힘/부분/완전개방, 펌프 접근/E 상호작용, 감시소 양쪽 출입·보급 접근·가림과 복원. 게임 Forward+ 렌더를 열어 검수한다. `SizeDraftCheck`, `StageTransitionCheck`, `TraversalArtCheck`, 통합검사를 별도 실행한다.
- [x] 결정·이슈·기능 현황·브리프/평면도 갱신. 실패/경고/남은 제한을 구분하고 생성 출력물은 버전 관리하지 않는다.

## 진행 기록

- 읽기 확인: 기존 감시소 충돌은 브리프와 일치한다. 기존 펌프는 충돌 없는 임시 상자이고 다리 판은 기본 Door 메시다. 부위 구조의 예외를 숨기려고 빈 Keep 메시를 만들지 않는다.

## 최종 결과

Blender 5.2.2 LTS에서 직접 생성한 GLB를 다시 가져와 검사했다. 게임은 Godot 4.7.2 Mono, Windows Forward+, NVIDIA RTX 5070 Ti, 1440×900 창으로 실행했다. 묶음 A의 5개 GLB는 Git 차이가 없으며 기존 manifest 항목5개와 최상위 메타데이터도 그대로임을 비교했다. 새 항목만 추가되어 Stage 2 팩9종, 전체 라이브러리56종이다.

| 자산 | 삼각형 / 예산(미만) | 재질 | 게임 좌표 기준 전체 크기 X/Y/Z(m) | 부위 |
|---|---:|---:|---|---|
| `bridge_truss` | 1116 / 2000 | 3 | 6 / 2.7 / 4 | Keep, Occlude_Rail |
| `bridge_leaf` | 1692 / 1800 | 4 | 4.6 / 3.6 / 1.424 | Occlude_Leaf |
| `pump_house` | 2720 / 5000 | 5 | 6 / 3.2289 / 5.1825 | Keep, Occlude_Roof |
| `watch_post` | 4952 / 5000 | 5 | 12 / 3.1 / 10 | Keep, Occlude_Walls |

- [생성기](../prototypes/outdoor-kit/tools/stage2_props_b.py), [독립 재가져오기·렌더 검사](../prototypes/outdoor-kit/tools/stage2_props_b_review.py), [원본 blend](../prototypes/outdoor-kit/source/stage2-props-b.blend), [manifest](../game/assets/stage2-props/manifest.json). GLB 4종과 Godot 가져오기 설정도 버전 관리 대상이다.
- 다리: 트러스는 상판보다 1.7m 아래까지, 난간은 1m 위까지다. 기존 꽉 찬 시안 바닥의 그림을 해당 영역에서 잘라내고 상판 외형만 두께0.3m로 바꿨다. 원래 `Raised` 충돌 부피와 Tier2=2.4m는 그대로다. 판은 서쪽을 향해 Y−90°, 열리면 기존 이동량−3.9m를 완료한 뒤 숨긴다. 재닫기와 개방 도중 재닫기도 원점/가시성/충돌을 복원한다.
- 펌프: 피벗 `(200,0,-14.5)`, 밸브 로컬 `(0,1.1,1.6)` → 월드 `(200,1.1,-12.9)`. 입구 폭3m, 게임 소유 벽/설비 충돌. 독립 검사에서 실제 빨간 메시 정점의 중심과 지름0.8m를 확인한다. 실제 E 입력으로 크레센도를 시작하고 테스트 훅으로 종료시켜 다리 개방을 확인했다. 50초 전투를 끝까지 플레이한 검사는 아니다.
- 감시소: 기존 12×10m/0.4m 벽/높이2.6m/양쪽3m 입구 충돌을 유지한다. 나무 판벽과 콘크리트 기초, 후면 반지붕, 창2개, 빈 선반, 비발광 초록 표시등. 표시등 포함 최고점3.1m. 탄약·구급상자2개까지 직접 걸어 접근했다.
- 가림: A의 `PropOcclusion` 재사용. `Occlude_*`만 투명도0.85로 완화하고 Keep는 불투명하게 남긴다. 기본 원근 카메라에서 펌프·감시소 내부 가림 및 밖으로 나왔을 때 복원을 검사한다. 다리 판은 전부 가림 대상인 브리프 예외다.

## 실행한 검사

| 검사 | 결과 | 로그 / 산출물 |
|---|---|---|
| C# 빌드 | 경고0, 오류0 | `dotnet build game/HordeShooter.csproj --nologo -v q` |
| Blender 생성 | 4종, 구조·예산·치수·피벗·방향 단언 통과 | `game/output/stage2-props-b-build.log` |
| Blender 독립 GLB 재가져오기 | **78개 통과, 8장 렌더** | `game/output/stage2-props-b-review.log`, `stage2-props-b-review/report.json` |
| Godot 가져오기 | 종료0, 에셋 오류 없음 | `game/output/stage2-props-b-import.log` |
| LandmarkArtCheck 헤드리스 | **63개 통과** | `game/output/b-LandmarkArtCheck.log` |
| LandmarkArtCheck 창 모드 | **74개 통과, 11장 실제 게임 렌더** | `game/output/landmark-render.log`, `landmark-*.png` |
| SizeDraftCheck | **74개 통과**, 펌프 복귀22초·사다리1회, 전체2.6분·곤돌라1회 | `game/output/b-SizeDraftCheck.log` |
| StageTransitionCheck | **180개 통과**, 기본 Stage1/2 문 동작 및 전체 모델 로드 | `game/output/b-StageTransitionCheck.log` |
| TraversalArtCheck | **149개 통과**, 묶음 A 이동·6인 승하차 회귀 확인 | `game/output/b-TraversalArtCheck.log` |
| Start-Game.ps1 -Test | **Integration149개 통과** | `game/output/b-integration.log` |

종료 경고는 통과와 구분한다. 최종 Landmark 헤드리스/창 모드, SizeDraft, Traversal은 종료 경고 없음. StageTransition ObjectDB6개, Integration2개 경고는 I-17로 남긴다. 중간 Landmark 재실행에는4개가 나왔으므로 항상 사라졌다고 주장하지 않는다. Blender 원본 저장은 기본 `Dots Stroke`/`Material` 브러시 라이브러리 경로를 상대화할 수 없다는 경고2건이 있으나 생성 완료/GLB 재가져오기·텍스처 없음 검사는 통과했다.

개발 중 실패/수정:

1. 모델을 만들기 전 전용 검사는 `bridge_truss` 미등록에서 예상대로 실패했다.
2. 첫 SizeDraft 검사 실패는 새 밸브 높이1.1m에 예전 바닥 패널의 3D 접근1.2m 조건을 쓴 것이 원인이었다. 당시 거리 약1.32m는 실제 사용 반경1.9m 안이었다. 검사를 실제 반경 및 `FocusedInteractable`에 맞췄으며 게임 사용 반경을 늘리지 않았다.
3. Blender 렌더에서 감시소 모서리와 다리 난간 접합부의 겹친 면을 확인했다. 판벽 끝을 맞추고 난간/기둥의 면과 끝 높이를 분리해 검은 무늬를 제거했다. 독립 리뷰가 찾은 펌프 배관 연결 누락도 연결관으로 보완했다. 치수·피벗·가림 방향 단언을 보강하고 최종 렌더로 재확인했다.
4. 독립 코드 리뷰 후 두 지적(배관 연결, 계약 검사)을 수정했으며 후속 리뷰에서 남은 차단 문제 없음. 리뷰 자체는 정적 검토였고 실행 결과는 위 표에 별도로 기록했다.

## 렌더 확인 경로

Blender 앞/뒤 8장: `game/output/stage2-props-b-review/<id>-front.png`, `<id>-back.png`.

게임11장: `game/output/landmark-bridge-closed.png`, `bridge-partial`, `bridge-open`, `bridge-crossed`, `pump-approach`, `pump-valve`, `pump-running`, `watch-approach`, `watch-inside`, `watch-supplies`, `watch-exited`(모두 같은 `landmark-` 접두사와 `.png` 확장자).

![다리 닫힘](../game/output/landmark-bridge-closed.png)
![펌프 밸브와 E 안내](../game/output/landmark-pump-valve.png)
![감시소 내부 가림](../game/output/landmark-watch-inside.png)

`output/`은 로컬 재생성 결과이며 Git에 넣지 않는다.

## 재현

저장소 루트의 PowerShell:

```powershell
$blenderB = 'C:\Util\Blender Foundation\Blender 5.2\blender.exe'
$godotB = 'D:\Util\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
& $blenderB --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_b.py
& $blenderB --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_b_review.py
dotnet build game/HordeShooter.csproj --nologo -v q
& $godotB --headless --editor --import --path game --quit
& $godotB --headless --path game --fixed-fps 60 res://tests/LandmarkArtCheck.tscn
& $godotB --path game --resolution 1440x900 --fixed-fps 60 res://tests/LandmarkArtCheck.tscn
& $godotB --headless --path game --fixed-fps 60 res://tests/SizeDraftCheck.tscn
& $godotB --headless --path game --fixed-fps 60 res://tests/StageTransitionCheck.tscn
& $godotB --headless --path game --fixed-fps 60 res://tests/TraversalArtCheck.tscn
./game/Start-Game.ps1 -Test
./game/Start-Game.ps1 -Stage2Draft
```

`-Stage2Draft`는 직접 플레이용이다. `-Test`는 Integration만 실행하므로 나머지 전용 장면을 생략하지 않는다.

## 남은 범위

- 별도 크기 시안에 적용했다. 정식 Stage 2 교체·메뉴 연결·묶음 C·세이프하우스 모델·감시소 부활은 이번 범위가 아니다.
- 다리 아래 통행 불가, 감시소 창의 벽 충돌, 보수적인 부위 AABB 가림은 유지한다. 직교/전용 연출 카메라, 다른 야외 키트 전체의 가림, 젖음·적설은 미검증이다.
- 1PC 검사다. 실제2PC 동기화, 6인 전투 부하, 20분 난이도/보급 밸런스는 검증하지 않았다.
- 기존 `.codex-remote-attachments/`는 작업 시작 전부터 있던 미추적 자료로 건드리지 않았다. 최초 제작 완료 시에는 커밋/푸시를 하지 않았으며, 아래 후속 요청에서 통합 절차를 진행한다.

## 커밋·최신 main 확인 후속 (2026-10-23)

- 사용자 요청으로 묶음 B 자산·원본·도구·코드·검사·관련 문서를 함께 커밋하고 원격 main에 반영한다. `origin`은 `https://github.com/minidice/game-plushpanic-hordeshooter.git`, 작업 브랜치는 `main`이다.
- 커밋 전 `git fetch origin main` 결과 원격과 로컬 기준은 모두 `e781c04`였다(앞섬/뒤처짐0/0). 새로운 원격 변경은 없으며 강제 푸시를 사용하지 않는다.
- 현재 트리를 새로 빌드·가져오기한 뒤 `LandmarkArtCheck`63, `SizeDraftCheck`74, `StageTransitionCheck`180, `TraversalArtCheck`149, `Integration`149를 재실행하여 모두 종료0/통과를 확인했다. 빌드 경고/오류0, 가져오기 오류 없음. 이번 통합 검사는 기존 `Integration.tscn`을 별도로 직접 실행했다.
- 이번 실행의 ObjectDB 종료 경고: SizeDraft10개, StageTransition6개, Integration4개. Landmark/Traversal은 경고 없음. 검사 실패와 구분하며 I-17은 유지한다. 로그는 `game/output/b-publish-<장면명>.log`, 가져오기는 `b-publish-import.log`다. 앞의 실제 렌더 검수 후 게임 동작·모델 외형 변경은 없다.
- 기존 미추적 첨부 자료와 `output/`, 캐시·빌드 산출물은 커밋에서 제외한다.
