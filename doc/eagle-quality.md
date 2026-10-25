# 독수리 조형 개선

> 2026-10-25 사용자 정정: 현재 캐릭터 아트 기준은 [매끈한 데포르메 동물과 입체적인 장비](character-art-direction.md)다. 이 문서의 모델/검사 결과는 제작 이력이며 봉제인형 표현을 다음 작업의 지시로 재사용하지 않는다.

> 이후 새 기준으로 독수리 모델을 다시 제작했다. 현재 모델 수치·실제 검수는 [독수리 clean forms](eagle-clean-forms.md)를 따른다. 아래 수치는 이전 모델 이력이다.

2026-10-25. 사용자 승인 범위: 독수리 한 종 개선 → Blender·Studio·실제 스테이지 검수.

## 방향과 계약

- 앵무새와 형태의 밀도를 맞추되(당시 봉제인형 해석은 2026-10-25 철회), 독수리는 낮고 또렷한 눈매·금색 갈고리 부리·넓은 목깃·층진 날개로 구분한다.
- 머리/목/몸통 연결, 돌출된 배 패널, 눈·목깃의 따로 붙은 덩어리를 다듬는다. 조끼는 몸통에 맞추고 눈 장구류/투구는 추가하지 않는다.
- `common_eagle` 식별자, 피벗·배색 채널·충돌·전투 수치는 유지한다. 다른 6종 GLB를 재생성하지 않는다.
- 원본 날개를 들고 있는 무기에 연결하는 기존 `CharacterAuthoredWing` 경로를 독수리에 적용한다. 개별 깃털 관절이나 새 스켈레톤은 범위 밖이다.
- 확대 화면뿐 아니라 실제 플레이 크기와 무장 상태를 검수한다. 자동 입력·검수 위치 이동을 수동 전체 맵 완주로 표현하지 않는다.

## 구현과 최종 검증

- 기준 커밋 `84b202d90d1c89d9e141fa13d750eb85461e7672`. 기존 Studio 몽둥이/조끼 렌더를 `studio/output/eagle-quality/before-*.png`에 보존했다.
- `ParrotGripCheck.tscn -- --eagle`를 추가하고 기존 코드에서 원본 날개 보존 실패를 확인했다. 독수리만 같은 authored-wing 경로에 포함한 후 기존 독수리 모델에서 3,060조합 검사 통과. 최종 모델 검사는 제작 후 별도로 기록한다.
- Studio 기준 렌더 첫 조끼 요청은 gear=2(비활성 눈 장구류)로 거부됐다. 실제 Vest=1로 수정해 기준 렌더를 다시 저장했다. 게임 데이터 결함이 아닌 검수 요청 값 오류다.

- Blender에서 독수리 전용 몸통·목/머리 연결·작은 눈·부리·겹치는 목깃·넓은 날개·발/꼬리·조끼를 제작하고 기존 eagle.glb 경로에 교체했다. 돌출된 별도 배 패널은 제거했다. 첫 옆면 렌더에서 벌어진 부리 틈을 발견해 아래 부리를 올리고 최종 11뷰를 다시 렌더했다.
- 기본 6,220→5,220삼각형, 조끼 포함5,940, 비활성 안경 포함전체6,304. GLB225,588→166,452바이트. 다른6종 GLB는 SHA256 불변이며 충돌/전투 수치/캐릭터 리소스 내용도 불변이다. 이 수치를 실제 호드 성능 측정으로 해석하지 않는다.
- 독수리 GLB 재가져오기50검사(날개 피벗·2재질 채널·8천 미만 예산 포함), 기존 앵무새 재가져오기819검사 통과. 동일 카메라·조명 전후22장과 비교판에서 실루엣·단색·최종색·조끼·플레이 크기를 확인했다.
- 최종 모델 `ParrotGripCheck -- --eagle`3,060, 기본 앵무새3,060, HunterPoseCheck156, CommonRosterCheck264, localhost ENet CommonRosterNetCheck108 통과. 반복 조합 검사 수이며 모두 별개의 제품 기능이라는 뜻은 아니다.
- 실제 창 `HunterCombatCheck -- --eagle-stage2`35검사 통과: LayoutMap에서 몽둥이·칼·활 명중/빗맞음/기절 취소·소리/화살 연동·준비/타격 렌더. 검수 지점 이동과 자동 입력을 사용했으며 전체 동선 수동 완주가 아니다.
- Studio 실제 reload 후 6무기 렌더, 수렵3무기12프레임과 소리 API50검사 통과. 브라우저에서도 독수리·녹색·나무활의 공격 미리보기와 타임라인5를 확인했다. 기존 웹 경로가 게임 모델/포즈를 그대로 사용하므로 별도 Studio 복제 모델이나 웹 변경은 없다.
- Debug/ExportRelease 빌드 오류·경고0. 독립 검토에서 차단 결함 없음. 확대 시 깃털/목깃의 각진 모서리와 날개 전체를 늘려 연결하는 포즈의 한계는 남는다. 개별 관절·천 미세 재질·최종 아트 수락·실제2PC/장시간 부하·패키지 내보내기는 미검증이다.
- 종료 경고: CommonRosterCheck ObjectDB6개는 기존 I-17로 유지. 위 다른 최종 Godot 검사 로그에는 종료 경고 없음. Blender Material.use_nodes 폐기 예정 안내는 정상 내보내기/검사 성공과 구분한다.

## 재현

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/build_common_monsters.py -- --species eagle
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/validate_common_monsters.py -- --species eagle
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --species eagle --label before
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --species eagle --label after
python prototypes/common-monsters/tools/parrot_contact_sheet.py --species eagle
```

기준 GLB는 위 기준 커밋의 `game/assets/common-monsters/eagle.glb` 바이트를 `prototypes/common-monsters/output/eagle-quality/before.glb`로 복원할 수 있다. 새 수정 작업 전에는 `review_parrot.py -- --species eagle --capture-baseline`으로 현재 모델/7종 해시를 보존하며 기존 기준은 덮어쓰지 않는다. QA 도구 기본 종족은 기존 parrot를 유지한다.

Godot Debug 빌드/import 후 `--path game --scene res://tests/ParrotGripCheck.tscn -- --eagle`, 실제 창은 `--path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --eagle-stage2`. Studio 실행 후 `$env:STUDIO_URL='http://127.0.0.1:4323'; node studio/tests/hunter-attacks.mjs`. `Start-Game.ps1 -Test`는 위 전용 검사를 대신하지 않는다.

산출물은 `prototypes/common-monsters/output/eagle-quality`의 비교판/검증, `studio/output/eagle-quality`의6무기/브라우저 화면, `studio/output/hunter-attacks`의 공격 프레임, `game/output/eagle-*` 로그와 `hunter-combat-eagle-stage2-*.png`. 모두 로컬 자동 생성물로 버전 관리하지 않는다.

계획: [eagle-quality](../docs/superpowers/plans/2026-10-25-eagle-quality.md). 이전 기준: [앵무새](parrot-quality.md), [일반 몬스터](common-monster-roster.md).
