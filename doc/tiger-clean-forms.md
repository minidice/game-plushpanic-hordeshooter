# 호랑이 — 매끈한 데포르메 조형 후속

2026-10-26. [공통 아트 기준](character-art-direction.md)의 호랑이 적용이다. 사용자가 요청한 거위·오리·호랑이·코뿔소 병렬 작업 중 호랑이만 별도 작업 트리에서 수정했다. 최종 사용자 아트 수락과 네 종 통합 검수는 별도다.

## 변경과 유지 계약

- 구형 구체 몸·볼2개·큰 광택 눈·장식 봉제선·돌출 관형 줄무늬를 정리했다. 머리와 몸통은 각각 연속된 매끈한 큰 표면이고, 양쪽 둥근 귀는 좁은 림과 얕은 안쪽 면이 있다. 작고 얕은 눈, 단일 납작한 크림색 주둥이·짧은 코/중립 입선, 표면을 따라간 검은 얼굴/옆구리/등 줄무늬를 사용한다.
- 전완은 연결되며 늘어나는 메시, 앞발은 작은 고정 비율 메시다. `common_tiger_{left,right}Arm_mesh`와 `Paw`/`Paw_mesh`/`Shoulder`/`Wrist`/`Grip`으로 사자의 기존 무기 손 계약을 재사용한다. 다리 끝은 좁은 발목과 평평한 발바닥으로 연결한다. 조끼는 몸에 맞는 표면과 각진 파우치다.
- `common_primary`는 털·손발, `fixed_ink`는 검은 줄무늬/코, `fixed_cream`은 주둥이, `common_secondary`는 꼬리 끝이다. 기존 팔레트 키/값, `common_tiger` 리소스 ID, 머리·몸·발·무기 피벗, 수렵/무장 장비 정책, 전투/충돌 수치는 유지한다. 생성기는 호랑이 모듈만 선택적으로 호출하고 다른6종 GLB는 변경하지 않는다.
- `Gear_Glasses` 노드는 기존 자산 호환 때문에 남아 있으나, 일반 몬스터 조합은 안경·투구를 선택하지 않는다. 이를 신규 장착 허용으로 해석하지 않는다.

## 재현과 검사

기준/수정 후 같은 카메라·조명11뷰를 `prototypes/common-monsters/output/tiger-clean-forms/`에 로컬 생성했다. 정면/측면/후면·조끼·중립 재질·실루엣·작은 게임 화면을 실제 Blender 렌더로 확인했다. 첫 렌더에서 가슴 보조색 면이 피부와 교차하고 실전 보조 팔레트가 진한색이라는 점을 찾아 가슴 패널을 없애고 보조색을 꼬리 끝으로 옮겼다. 현재 몸 줄무늬는 얇은 표면 메시다.

```powershell
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/build_common_monsters.py -- --species tiger --no-render
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/validate_common_monsters.py -- --species tiger
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/validate_tiger_clean.py
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/review_parrot.py -- --species tiger --review-name tiger-clean-forms --label after
```

생성/재가져오기/전용 형상 검사는 각각 성공, 공통 재가져오기47검사·전용41검사다. 양쪽 발바닥 평면 면적은 각각 약0.048346m², 기본 메시 감사5,164삼각형(기존6,512), 조끼/비활성 안경을 포함한 저장 자산 총6,496삼각형(기존7,838)이다. 삼각형 감소가 실제 FPS 향상을 입증하지는 않는다. 최종 GLB SHA-256은 `85bf4ba874bdd0fc4d30b901cf3d443b5fdf12a312a4769b4cff68f33fc06946`이다. 생성기는 선택하지 않은 다른6종의 해시 불변을 확인했다.

Godot 4.7.2 .NET에서 개인용 임시 프로젝트 이름으로 임포트 성공, `dotnet build` 경고/오류0, `LionLimbCheck.tscn -- --tiger` 4,464검사와 `CommonRosterCheck.tscn` 264검사가 통과했다. 손 검사는 수렵3무기·총3무기의 파지/공격/재장전·전완 접합·상태이상/피격/숨김을 포함한다.

실제 창에서 `HunterCombatCheck.tscn -- --tiger-stage2` 38검사 통과, 접근 이동/정지·몽둥이·칼·활 8화면을 `game/output/hunter-combat-tiger-stage2-*.png`에 저장해 확인했다. 검수 위치로 적을 생성하고 자동 입력한 Stage 2 화면이며 전구간 사람 손 플레이는 아니다. 창 로그는 `game/output/tiger-clean-stage2.log`에 있고 오류·종료 경고가 없다. 독립 Studio 포트4333에서 `STUDIO_SPECIES=tiger`의 `studio/tests/hunter-attacks.mjs` 50검사 통과, 붉은 수렵 몽둥이/활의 프레임 및 녹색 무장 M4+조끼의 실제 렌더를 확인했다. Studio는 기존 게임 모델/포즈를 사용하며 테스트 후 자체 프로세스를 종료하고 임시 프로젝트 이름을 원래대로 복원했다.

## 남은 한계

확대하면 별도 머리 피벗과 목의 겹침, 얇은 표면 줄무늬 경계, 발과 다리의 조합 경계가 보일 수 있다. 무장 M4에서 전완이 총 손잡이까지 길게 늘어나며 손가락·팔꿈치 관절은 없다. 경사면 발 IK도 없다. 수동 전구간 플레이·실제2PC·장시간 호드 성능 및 사용자 최종 아트 수락은 아직 수행하지 않았다.
