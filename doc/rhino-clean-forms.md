# 코뿔소 조형 개선 — 2026-10-26

상태: 모델·게임 연결 및 Blender/Stage 2 검수 완료. Studio87·수렵 동작50 및 네 종 통합 검수 결과는 [통합 기록](four-species-art.md)을 따른다. 최종 사용자 아트 수락과 구분한다.

## 형태와 유지 계약

현행 A-25의 매끈한 무광 캐릭터/입체 장비 기준을 적용했다. 큰 앞뿔과 작은 뒷뿔, 넓게 연결된 턱과 주둥이, 얕은 작은 눈, 잎 모양 귀, 묵직한 몸과 좁아지는 발목, 평평한 세 발톱 발을 사용한다. 조끼는 몸을 감싸고 사각 파우치/덮개/잠금으로 구성한다. 제작 수치는 시안이며 새 종족 능력 결정이 아니다.

- `prototypes/common-monsters/tools/rhino_clean.py`를 기존 `build_common_monsters.py --species rhino`에서 호출한다. 선택하지 않은 종은 재생성하지 않는다.
- Root/Body/Deform/Head/Arm/Weapon/Muzzle/발 피벗과 semantic material, 팔레트·종족·무기·무리·충돌·속도 계약은 유지한다. 일반몹 투구/눈 장구류 제외도 동일하다.
- 손은 원본 전체를 늘리던 캡슐 방식에서 기존 `CharacterLionPaw`의 고정 비율 손/별도 전완으로 연결했다. 명시적 Shoulder/Wrist/Grip을 사용한다. 기본 Arm 피벗은 유지하고 실제 어깨 꼭대기를 별도 기준점으로 둔다.
- 코뿔소 눈 위 낮은 능선은 모델 조형이다. 런타임 눈썹 조합 지원을 확대하지 않았다.
- 기본 5,692삼각형, 조끼1,184, 숨기는 기존 안경 모듈288, 전체7,164. 기본8천 예산 이내이나 호드 성능 측정으로 해석하지 않는다.

## 검증과 재현

- Blender GLB 재가져오기 일반47, 전용78 통과(선택 전 다른6종 해시 비교 포함). 원본 기준점·작은 눈/뿔 비율·얕은 단일 연결 손·그립 표면 거리·닫힌 외향 표면·평평한 밑면을 검사했다. `--baseline-hashes`를 생략하면 모델 자체77개를 검사하며, 무시되는 output을 처음부터 요구하지 않는다.
- Debug 빌드0경고/0오류, Godot import 성공. `LionLimbCheck --rhino`4,200 통과: 6무기 공격/장전/크기/상태이상/피격/숨김에서 실제 손 정점 거리와 어깨·손목 표면 연결 검사.
- 창 모드 `HunterCombatCheck --rhino-stage2 --form-review`42 통과. Stage 2 실제 물리 추격, 몽둥이/칼 타격과 취소, 활 발사/취소 및 게임 거리/정면 확대 렌더. 자동 입력 검사이며 수동 스테이지 완주를 뜻하지 않는다.
- Blender 수정 전후 동일 카메라 실루엣/단색/최종색/장비/게임 크기11장씩. 최종 측면 조끼와 3/4 기본형, 실제 Stage 2 확대와 활 동작을 확인했다.
- 전용 Godot 로그 오류·종료 경고 없음. Blender Material.use_nodes 폐기 예정 경고는 남으며 exit0이었다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/build_common_monsters.py -- --species rhino --no-render
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/validate_rhino_clean.py
dotnet build game/HordeShooter.csproj --nologo -v q
$reviewGodot='D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $reviewGodot --headless --path game --scene res://tests/LionLimbCheck.tscn -- --rhino
& $reviewGodot --path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --rhino-stage2 --form-review
$env:STUDIO_URL='http://127.0.0.1:4334'
node studio/tests/rhino-clean.mjs
```

로컬 산출물: `prototypes/common-monsters/output/rhino-clean-forms/`, `game/output/four-species-art/`, `game/output/hunter-combat-rhino-stage2-*.png`, `studio/output/rhino-clean/`. output은 버전 관리하지 않는다. 수정 전 baseline은 모델 변경 전에 확보한 로컬 비교 자료다.

## 발견한 문제와 한계

첫 전용 형상 검사에서 손의 앞뒤 두께가0.134m로 기준0.13m를 넘었다. 손등을 얕게 정리하고 Grip을 손 표면 쪽으로 옮긴 뒤 동일 검사를 통과했다. 허용치를 늘리지 않았다.

분리 피벗 리그의 목/어깨 전환선과 먼 무기 그립까지 늘어나는 전완은 남는다. 손가락 관절/팔꿈치/경사면 발 IK는 이번 범위가 아니다. 최종 아트 수락, 수동 전구간 플레이, 실제2PC, 장시간 호드/저사양 성능은 미검증이다.
