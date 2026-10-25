# 앵무새 조형 기준 정리

2026-10-25. 사용자가 승인한 추천안: 앵무새 한 종을 먼저 다듬어 일반 동물의 조형 기준을 검수한다. 모든 종의 일괄 교체나 새로운 캐릭터 스타일 확정이 아니다.

## 범위와 유지 계약

- 머리–몸 연결, 배의 돌출과 팔·발 비율을 정리한다. 별도로 붙인 볼과 배 덩어리는 줄이고, 봉제인형의 부드러운 큰 형태를 유지한다.
- 눈 둘레·돌출·반짝임을 절제하되 플레이 크기에서 눈과 부리 관계가 읽히게 한다. 굽은 부리, 뒤로 흐르는 볏·꼬리로 앵무새를 식별한다.
- 원래 날개 메시를 무장 시에도 사용한다. 실제 날개 표면/재질을 보존하면서 손잡이에 맞추고, 숨긴 원래 팔과 중복 표시하지 않는다.
- 조끼는 새 몸통에 맞춘 두께와 어깨 연결·파우치를 검수한다. 기존 결정대로 투구·선글라스·안대를 추가하지 않는다.
- 캐릭터 식별자, 노드 피벗, 배색 채널, 충돌 크기, 전투 수치·동작 타이밍은 유지한다. 다른6종 GLB와 플레이어 외형은 보존한다.

## 비교 방법

1. Blender의 같은 조명·시점에서 기존/수정 형태를 실루엣 → 단색 → 최종색으로 비교.
2. 게임과 같은 Godot 초기화 경로인 Studio에서 몽둥이·칼·활 준비/타격/복귀, 총기·조끼, 다른 배색 확인.
3. 실제 Stage 2 지형의 플레이 크기에서 공격·명중·회피·취소와 렌더 확인. 자동 입력 검사와 수동 플레이를 구분한다.
4. 날개 끝 연결, 원본 표면·배색 유지, 숨김/실루엣/피격·상태 효과, 기존 종 회귀 확인.

## 진행 기록

- 기존 Studio 몽둥이/조끼 렌더와7 GLB 해시를 `studio/output/parrot-quality`에 보존했다. 생성물은 버전 관리하지 않는다.
- 구현 완료: 새 몸통/목 연결·배 표면 패널·작고 얕은 눈 둘레·축소된 반짝임·볼 덩어리 제거·굽은 부리·쓸린 볏/꼬리·깃털 날개·몸통에 맞춘 조끼. Blender에서 실제 제작·GLB 내보내기 후 같은 경로로 게임에 교체했다.
- `CharacterAuthoredWing.cs`가 원래 날개 메시와 팔레트 재질을 그대로 공유한다. 들고 있을 때 실제 메시 상단/하단 정점에서 기준을 구해 어깨와 손잡이에 연결하고, 빈 날개는 원래 휴식 형태를 유지한다. 프레임마다 메시를 재생성하지 않는다.
- 최종 원본: 기본6,040삼각형(기존5,752), 방탄복 포함6,900, 비활성 안경 데이터 포함7,264. GLB185,264바이트(기존211,352). 기본 예산8천 이하. 피벗·배색 채널·충돌 리소스와 다른6종 GLB는 해시 비교로 불변 확인. 이것을 호드 전체 성능 검증으로 해석하지 않는다.
- 날개 전체의 회전과 길이 조절 방식이다. 개별 깃털 관절/손가락 쥐기나 새 스켈레톤은 추가하지 않았다. 다른 종으로의 조형 확장은 이번 앵무새 결과를 기준으로 후속 검수한다.

## 검증 결과

- `dotnet build game/HordeShooter.csproj --configuration ExportRelease` 성공, 경고/오류 0. 폴더(솔루션)를 대상으로 한 첫 명령은 솔루션에 ExportRelease 구성이 없어 MSB4126으로 실패했으며, 실제 Godot 프로젝트를 명시해 확인했다. 패키지 내보내기 검증은 아니다.
- Blender 선택 제작 성공, 앵무새 독립 GLB 재가져오기819검사. 배 패널768면의 중심과 실제 몸통 사이 최소 간격2.586mm. 동일 카메라/조명 전후22장과 비교판을 실제 확인했다.
- 전체7종 명시 선택 재가져오기1,085검사 통과. 검사 입력은 기존6종 audit와 새 앵무새 audit를 합친 로컬 보고서이며 GLB를 재생성하지 않았다.
- `ParrotGripCheck`3,060개 행렬 검사:6무기×2배색의 공격/재장전/몸 변형, 원본 표면·배색, 실제 정점의 손잡이/어깨 연결과 돌출 범위, 상태/피격/숨김/실루엣. 반복 조합 검사 수이며3,060개의 서로 다른 기능이라는 뜻은 아니다.
- 기존 `HunterPoseCheck`156, 플레이어 `ReloadPoseCheck`279, `CommonRosterCheck`264, 실제 localhost `CommonRosterNetCheck`108 통과.
- `HunterCombatCheck -- --parrot-stage2`: 실제 LayoutMap의 앵무새3무기 명중/빗맞음/기절 취소와 렌더35검사 통과. 자동 입력·검수용 배치를 사용하며 전체맵 수동 완주가 아니다. 기존 기본 독수리 검수 모드는 유지한다.
- Studio 실제 reload 후6무기 렌더, 수렵3무기 각12프레임 확인. 브라우저에서도 수렵단→앵무새→나무활→공격 미리보기와 준비 끝 시간 이동을 확인했다. 새 웹 모델/복제 데이터는 만들지 않았다.
- 종료 경고: 이번 플레이어 ReloadPoseCheck에서 ObjectDB6개. 기존 I-17로 유지한다. 위 나머지 최종 Godot 검사 로그에는 종료 경고 없음. Blender의 Material.use_nodes 폐기 예정 안내는 내보내기 실패와 구분한다.
- 실패/보정: 기존 캡슐은 원본 깃털 보존 검사에서 실패 → 원본 날개로 교체. 새 날개 상단이 어깨 뒤로 늘어나는 렌더 문제 → 실제 상단 정점 기준으로 보정하고 돌출 검사 추가. 배 표면 겹침 → 표면 세분과 간격 조절 후 렌더/거리 검사. 공용 이동 포즈의 CapsuleMesh 형변환 실패 → 새 날개 유형 분기.
- 독립 리뷰에서 전체 종을 명시한 생성/검증 명령의 audit 파일 이름 불일치를 발견해 수정했다. 이어 전체 재가져오기에서 앞 종의 미사용 재질이 남아 이름에 .001이 붙어 검사 실패 → 검사 사이 미사용 메시/재질 정리 후 전체1,085통과. 게임 에셋 오류로 기록하지 않는다.

## 재현과 기준 파일

기존 모델 기준 커밋은 `bdc655dd63931c3c11db79d845845f32b0dc6447`의 `game/assets/common-monsters/parrot.glb`이며 보존한 before.glb와 SHA256 일치를 확인했다. 나중에 output을 지웠다면 Python의 `subprocess.check_output(['git','show','bdc655dd63931c3c11db79d845845f32b0dc6447:game/assets/common-monsters/parrot.glb'])` 바이트를 `prototypes/common-monsters/output/parrot-quality/before.glb`에 저장해 복원한다. 기존 작업 GLB를 덮어쓰지 않는다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/build_common_monsters.py -- --species parrot
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/validate_common_monsters.py -- --species parrot
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --label before
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/common-monsters/tools/review_parrot.py -- --label after
python prototypes/common-monsters/tools/parrot_contact_sheet.py
```

새로운 작업 전 비교 기준 보존은 `review_parrot.py -- --capture-baseline --label before`로 하며 이미 있는 기준 파일은 덮어쓰지 않는다. 선택 제작은 해당 종만 내보내고 `audit-parrot.json`에 기록한다. 전체 제작의 `audit.json`과 구분한다.

실제 게임은 Debug 빌드/에셋 import 후 Godot에 `--path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --parrot-stage2`를 전달한다. `Start-Game.ps1 -Test`는 이 검사를 실행하지 않는다. 로그/PNG는 `game/output/parrot-*`, `game/output/hunter-combat-parrot-stage2-*`, `studio/output/parrot-quality`, `prototypes/common-monsters/output/parrot-quality`에 생성한다. 생성물·로컬 blend는 커밋하지 않는다.

계획: [parrot-quality](../docs/superpowers/plans/2026-10-25-parrot-quality.md). 기존 규격: [일반 몬스터](common-monster-roster.md), [수렵 무기 동작](hunter-weapon-presentation.md).

## 커밋/main 통합 재검증 — 2026-10-25

- 사용자 요청으로 수렵 무기·앵무새·Studio·문서 변경을 함께 통합한다. fetch 시 원격 main과 작업 기준 커밋이 동일했다.
- 현재 트리 Debug 빌드 오류/경고0, Integration149, ParrotGripCheck3060, HunterPoseCheck156, HunterSounds87, CommonRosterNetCheck108, Studio hunter-attacks50 통과. JS 문법과 Git diff 공백 검사 통과.
- Integration 종료 ObjectDB4개는 기존 I-17로 유지한다. 나머지 위 전용 검사에는 종료 경고 없음. 상세 로그는 각 output의 `commit-*`이며 생성물은 커밋에서 제외한다. 앞선 렌더 검수 범위와 실제2PC/장시간 부하 미검증 조건은 유지한다.
