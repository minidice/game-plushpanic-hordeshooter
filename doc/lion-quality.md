# 사자 조형 개선

> 이 문서는 1차 조형의 검증 이력이다. 같은 날 진행한 [손·발·허벅지 연결 후속](lion-limbs.md)에서 앞발 변형 방식과 모델 수치/해시가 변경됐다. 현재 구현과 최신 결과는 후속 문서를 기준으로 한다.

2026-10-25. 사용자가 다음 추천 작업 진행을 요청하여 독수리 다음 순서인 사자 한 종을 포유류 조형 기준으로 개선한다.

## 방향과 유지 계약

- 둥근 구슬을 두른 듯한 갈기 대신 연결된 큰 갈기 형태, 몸통과 이어지는 목/머리, 얕고 작은 눈과 읽기 쉬운 주둥이를 만든다. 배의 별도 돌출 덩어리를 줄이고 봉제인형의 부드러움과 사자 특징을 유지한다.
- 앞발은 무기를 들어도 원본 메시와 배색을 유지한다. 조류에 사용한 원본 팔다리 연결 방식의 사자 적용을 먼저 렌더로 검증한다. 개별 손가락/관절이나 새 스켈레톤은 범위 밖이다.
- 기존 사자 식별자·피벗·팔레트·충돌·전투 수치를 유지한다. 이번 작업 시작 시점의 다른6종 GLB(앞선 독수리 수정 포함)는 보존한다. 투구·눈 장구류는 추가하지 않는다.
- Blender 실루엣/단색/색상 전후, Studio6무기/수렵3공격, 실제 Stage2 자동 전투와 렌더로 확인한다. 전체 맵 수동 완주·최종 아트 수락·장시간 성능 검수와 구분한다.

## 검증 기록

- 사자 기준 모델은 `84b202d`의 lion.glb와 같다. 현재 독수리 작업은 아직 커밋되지 않았으므로 다른 모델 불변 비교는 작업 시작 시 7종 해시를 사용한다.
- Studio 황토색 몽둥이/조끼 기준 렌더를 `studio/output/lion-quality/before-*.png`에 저장했다.
- `ParrotGripCheck -- --lion`에서 기존 캡슐 팔의 원본 메시 보존 실패를 먼저 확인했다. 기존 원본 팔다리 경로를 사자에 적용하여 같은 앞발 메시/배색을 보존한다. 프레임마다 메시를 생성하지 않는다.

## 최종 구현과 결과

- Blender에서 연결된9개 큰 갈기 굴곡, 작은 눈/귀, 짧은 주둥이·코·입, 두툼한 몸통/발, 둥근 손바닥 패치와 조끼를 제작했다. 별도의 돌출된 배 패널과 갈기 구슬 고리는 제거했다.
- 기본6,628→6,894삼각형, 조끼 포함7,534→7,614, 비활성 안경 포함7,954→7,978. GLB234,664→219,384바이트. 기본8천 미만이며 삼각형 증가를 성능 향상으로 표현하지 않는다. 다른6종 GLB와 캐릭터 리소스·전투 수치는 불변이다.
- 최종 GLB 재가져오기50검사, 고정 카메라/조명 전후22뷰(실루엣·단색·색상·조끼·게임 크기)를 확인했다. 최종 SHA256은 `fff4ae415c9fb3086e49ddb1b65c16a524de92fd4dd6a147b2ffbb3a3f3c6d78`.
- 최종 사자 그립3,084조합 통과. 실제 정점의 어깨 뒤 돌출8.5cm 미만, 손잡이 바깥 돌출12cm 미만, 손잡이 기준점 오차2mm 미만 등을 검사한다. 이 값들은 정확한 실측 최대치가 아닌 통과 기준이다.
- 독수리/앵무새 그립 각3,084, HunterPoseCheck156, CommonRosterCheck264, localhost ENet CommonRosterNetCheck108 통과. 이 검사는 마지막 이마 경계 좌표 보정 직전 실행했으며, 해당 보정 뒤 사자 그립·실제 전투·Studio를 다시 검증했다.
- 실제 LayoutMap 창에서 `HunterCombatCheck -- --lion-stage2`35검사 통과: 수렵3무기 명중/빗맞음/기절 취소·소리/화살 연동·준비/타격 렌더. 자동 입력과 검수 지점 이동이며 전체 맵 수동 완주가 아니다.
- Studio6무기 황토색 정지 렌더, 붉은색 수렵3무기 각12프레임 및 소리 API50검사 통과. 브라우저에서도 사자·황토색·나무활 공격 미리보기와 타임라인5를 확인했다. 웹 모델 복제 없이 기존 게임/Studio 경로를 사용한다.
- Debug/ExportRelease 오류·경고0. 독립 코드/렌더 검토에서 차단 결함 없음. 위 최종 Godot 검사에는 종료 경고 없음(기존 I-17 전체 해결은 아님). Blender Material.use_nodes 폐기 예정 안내는 정상 생성/검사와 구분한다.

## 발견한 문제와 남은 한계

- 앞발이 단일 메시가 되면서 병합 함수의 이름 지정이 생략돼 최초 연결 검사에서 이름 조회 실패. 생성 시 `common_lion_leftArm_mesh`/`common_lion_rightArm_mesh`를 명시하고 Blender/게임 이름·피벗 검사를 추가했다.
- 갈기 안쪽의 일정한 굴곡 크기가 중심에서 교차해 검게 보였다. 반지름에 따라 굴곡을 줄이고 이마 쪽 안쪽 면을 머리 아래로 이동했다. 사각 손바닥 배색은 둥근 패치로 교체하고 발 표면에 묻혀 점처럼 보이던 봉제선을 제거했다.
- 새 이름 검사의 C# nullable 경고1개는 실패 시 예외를 던지는 검사 함수에 DoesNotReturnIf 계약을 표시한 후 경고0으로 확인했다.
- 확대 시 갈기 외곽의 각진 면, Studio 조명에서 이마/갈기 안쪽의 짙은 음영, 소총 손잡이까지 길어진 앞발과 활 자세의 발 음영이 남는다. 전체 팔다리 변환이므로 개별 손가락/발 관절 동작은 없다. 최종 아트 수락·다른 포유류 확대·실제2PC/장시간 부하·패키지 export는 후속이다.

## 재현

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/build_common_monsters.py -- --species lion
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/validate_common_monsters.py -- --species lion
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --species lion --label before
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --species lion --label after
python prototypes/common-monsters/tools/parrot_contact_sheet.py --species lion
$env:STUDIO_URL='http://127.0.0.1:4323'
$env:STUDIO_SPECIES='lion'
node studio/tests/hunter-attacks.mjs
```

새 수정 전 `review_parrot.py -- --species lion --capture-baseline`으로 현재 모델/7종 해시를 보존한다. 이미 존재하는 기준을 덮어쓰지 않는다. 이번 원본은 `84b202d:game/assets/common-monsters/lion.glb` 바이트로 복원할 수 있고, 다른 종 해시는 작업 시작 시점을 기준으로 한다.

Debug 빌드/import 후 Godot에 `--path game --scene res://tests/ParrotGripCheck.tscn -- --lion`, 실제 창에는 `--path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --lion-stage2`. `Start-Game.ps1 -Test`는 이 전용 검사를 실행하지 않는다.

산출물: `prototypes/common-monsters/output/lion-quality`(기준 before.glb/해시/비교판/감사), `studio/output/lion-quality`(정지/브라우저), `studio/output/hunter-attacks-lion`(공격 프레임), `game/output/lion-*`(로그), `hunter-combat-lion-stage2-*.png`(게임 렌더). 로컬 자동 생성물로 버전 관리하지 않는다.

계획: [lion-quality](../docs/superpowers/plans/2026-10-25-lion-quality.md). 관련 규격: [일반 몬스터](common-monster-roster.md), [독수리 검수](eagle-quality.md).
