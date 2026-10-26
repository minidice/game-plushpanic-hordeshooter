# 포유류 무기 파지와 팔꿈치

2026-10-26. 호랑이 M4부터 MP5·AK47과 다른 포유류로 확장하는 추천안을 사용자가 승인했다. A-25의 매끈한 동물·입체 장비 기준을 따른다. 미술 최종 수락과 전투 밸런스 변경은 별개다.

## 원인과 현재 구현

- 기존 `CharacterLionPaw`는 손은 고정했지만 짧은 원본 전완 하나를 어깨–손목 전체 길이로 늘렸다. 실제 호랑이 M4 오른팔/왼팔 거리는 0.398/0.860m, 순록은 0.469/0.917m였다. 팔꿈치가 없어 긴 막대처럼 보였다.
- Blender로 닫힌 위팔/아래팔 두 표면을 만들었다. `mammal-arm-segments.glb`의 위팔252·아래팔220삼각형을 공유하고 종별 원본 팔 재질/두께를 사용한다. 기존 종별 손 메시와 손목·그립 기준점은 유지한다. 매 프레임 정점/메시를 만들거나 공유 원본을 수정하지 않는다.
- 두 구간으로 팔꿈치를 계산한다. 바깥/아래 방향으로 굽히며, 손은 아래팔 방향에 맞춰 균일 크기로 회전한다. 양쪽 실제 손잡이 접촉은2mm 미만 기준으로 검사한다. 무기를 잡지 않는 팔은 원본 연속 표면 `RestArm`을 표시한다.
- 대상: 호랑이·사자·순록·코뿔소·랫서판다·라쿤. 조류/좀비 경로는 그대로다. 일반 포유류의 양손 총기와 플레이어의 기존 한손 소총 정책을 보존한다. 근접·활·재장전·짚라인도 같은 손 경로를 사용한다.
- 현재 제작값: 위팔/아래팔 기준 각0.36m×팔 크기, 아래팔 계산에는 원본 손목–손잡이 길이를 더한다. 닿지 않는 자세는 두 길이를 비례 연장해 접촉을 우선한다. 양손 포유류 총기(쌍권총 제외)만 몸 쪽으로0.10m 당긴다. 무기 데이터·충돌·속도·공격 수치는 바꾸지 않는다. 시각 총구 위치는 바뀌며 실제 적 탄환도 이동한 총구에서 출발한다.
- Studio는 게임과 같은 GLB/런타임을 사용한다. 새 커스터마이징 저장 키나 별도 웹 모델은 없다. 기존 색/크기/장비 조합과 피격·상태이상·실루엣/숨김이 새 표면까지 적용된다.

## 제작 중 실패와 보완

- 최초 검사에서36개 팔 모두 `UpperArm`이 없어 실패했다(`red.log`). 이후 실제 팔꿈치 굽힘·구간 길이·삼각형 표면 접촉·재질 공유·유한 변환·원본 쉬는 팔 표시 검사를 추가했다.
- 첫0.20m 당김은 Stage2 확대에서 방아쇠 손을 조끼에 너무 가깝게 놓았다. 재현 검사(`vest-red.log`) 후0.10m로 줄이고 구간 길이를 조정했다. 이 검사는 현재 조끼 앞 기준 위치 검사이며 모든 장비 메시와의 충돌 검사라는 뜻은 아니다.
- 첫 측면 렌더의 어깨 단면은 Blender 끝단을 둥글게 하고 어깨 안으로 조금 겹쳐 완화했다. 기존 쉬는 팔을 두 토막으로 보이게 만들지 않도록 원본 표면을 따로 보존했다.
- 독립 검토에서 검사 카메라의 확대 크기가 다음 게임 화면에 남는 문제를 지적해 `Size`도 복원했다. 새 `RestArm`의 효과 검사도 추가했다. 최종 검토에서 차단 기능 문제는 없었으며 확대 관절 연결선은 후속 조형 한계다.
- 검사 실행 중 `ModularCustomizationCheck`/`ToneCheck`라는 잘못된 장면명을 사용해 로딩 실패했다. 실제 `ModularCustomization`/`CustomizationToneCheck`로 바로잡아 실행했다. 제품 오류나 성공 검사로 계산하지 않는다.

## 검증

- Debug/ExportRelease 빌드 오류/경고0. Godot 모델 가져오기 성공. Blender GLB 독립 재가져오기11검사(닫힌 면/양의 체적/축·길이/재질·삼각형 예산) 통과. 생성 시 Blender6.0의 `Material.use_nodes` 폐기 예정 안내는 남지만 생성/검사는 성공했다.
- `MammalArmCheck`1,044: 여섯 종×M4/MP5/AK47×팔 크기1·0.8·1.25, 실제 손잡이 접촉·팔꿈치·구간/표면·재질·표시 정책.
- 최종 `LionLimbCheck`: 호랑이/사자/순록 각4,536, 코뿔소4,272. 여섯 무기·공격/재장전·크기·리셋·피격/상태/실루엣/숨김 검사. `GroundedArtCheck`212, `WeaponGripCheck`187, `ReloadPoseCheck`279, `ModularCustomization`67, `CustomizationToneCheck`87.
- 실제 Stage2 창 `MammalArmStageCheck`539: M4/MP5/AK47 적 AI 사격, 탄환 생성 위치와 실제 총구 일치, 진행 중 파지, 게임 거리/확대/재장전9장. 재장전 화면은 검사에서 재장전 상태를 적용한 연출 검수이며 일반몹의 새로운 재장전 AI를 뜻하지 않는다.
- Studio 실제 Godot 렌더51검사/21장: 네 일반 포유류×3소총, 호랑이 정면/측면/후면, 플레이어2종 기본/작은/큰 커스터마이징. 호랑이 수렵3무기 각12프레임 API50검사도 통과했다.
- 최종 실제 창의 랫서판다/라쿤 각7,620검사: Stage2 조작 이동·사격·사다리 완료와 확대/장비 렌더 포함. 호랑이 수렵 Stage2 추격·몽둥이/칼 접촉·활 발사/취소와 렌더42검사 통과. 자동 조작 범위다.
- 최종 조정 뒤 Integration149·CommonRoster264 통과. WeaponAim32도 이번 작업에서 실행했다. 성공한 최종 Godot 로그에 오류/종료 경고 없음. 이전 실패 로그는 위 제작 이력과 구분해 보존한다.

## 남은 범위

닫힌 관절 표면이 겹치는 방식이라 확대 시 팔꿈치·손목 연결선이 남는다. 전신 스킨 리그/개별 손가락 감기/모든 장비 관통 해결은 이번 범위가 아니다. 긴 활·재장전·이동 시설의 극단 목표는 여전히 길이를 연장하며 완전한 고정 뼈 길이 IK를 주장하지 않는다. 팔마다 활성 표면이 하나 늘어나는 비용이 있고 장시간 FPS·저사양·2PC 검사는 하지 않았다. 자동 조작/검사 위치 이동에 의한 플레이이며 전체 맵 수동 완주가 아니다.

## 재현·근거

- Blender: `prototypes/grounded-art/tools/build_mammal_arm_segments.py`, `validate_mammal_arm_segments.py`를 `--background --python-exit-code 1 --python`으로 실행. 빌드는GLB와 `prototypes/grounded-art/output/mammal-arms/segments.png`를 생성한다.
- Godot import/빌드 뒤 `--headless --path game --scene res://tests/MammalArmCheck.tscn`. 실제 창은 `--path game --scene res://tests/MammalArmStageCheck.tscn --resolution 1440x900 --fixed-fps 60`. `Start-Game.ps1 -Test`는 전용 장면을 대신하지 않는다.
- Studio 재시작/reload 후 `STUDIO_URL`에 테스트 서버를 지정하고 `node studio/tests/mammal-arms.mjs`. 기본4334는 이번 격리 검수 포트다. 기존4323 서버를 중단하거나 수정하지 않았다.
- 로그/게임 화면: `game/output/mammal-arms/`. 최신 Studio 화면: `studio/output/mammal-arms/`. `tiger-before.png`는 수정 전, 최종 비교는 Studio의 `tiger-m4.png`를 사용한다. 초기 `tiger-after.png`는 중간 제작물이다. output은 재실행으로 생성하는 로컬 자료이며 커밋하지 않는다.
- [실행 계획](../docs/superpowers/plans/2026-10-26-mammal-weapon-arms.md), [팔 런타임](../game/scripts/Characters/CharacterLionPaw.cs), [파지 배치](../game/scripts/Characters/CharacterWeaponGrip.cs), [기존 손 계약](lion-limbs.md).

검수용 임시 프로젝트명은 원본 바이트로 복원했고 독립4334 서버만 종료했다. 기존 사용자 설정/원래 main은 변경하지 않았으며 현재 작업은 `codex/mammal-weapon-arms`의 미커밋 변경이다.


2026-10-26 후속: 위의0.36m 구간/자동 신장 정책은 표준 포유류 총기 자세에서 원본 비율 제한으로 교체했다. 제작 이력과 이전 결과는 보존하며 현재 계약은 [공통 자세 후속](shared-weapon-poses.md#2026-10-26-짧은-팔-비율-유지-후속)을 따른다. 전용 재장전/횡단은 도달 예외가 남는다.
