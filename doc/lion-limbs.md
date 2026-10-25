# 사자 손·발·허벅지 연결 개선

2026-10-25. 사용자가 사자부터 무기 쥐기, 발 접지 형태, 허벅지–발목 연결을 개선하고 Blender·Studio·실제 게임 렌더로 확인하도록 요청했다. [1차 사자 조형](lion-quality.md)의 후속이며 다른 종과 전투 수치는 유지한다.

## 현재 구현

- Blender 앞발을 전완과 작은 손으로 분리했다. 손은 납작한 벙어리장갑 형태와 작은 엄지, 기존 손바닥 배색을 갖는다. `common_lion_{left,right}Paw` 아래에 `Wrist`·`Grip` 기준점을 둔다(실제 전체 이름은 `common_lion_leftWrist` 등의 형식).
- `CharacterLionPaw`는 손 전체를 균일 크기로 회전·이동하여 무기 손잡이에 맞춘다. 어깨부터 손목까지 전완만 길이를 조절한다. 이전처럼 소총을 잡을 때 손바닥까지 길어지는 변형을 제거했다. 원본 메시/재질을 공유하고 매 프레임 정점이나 메시를 생성하지 않는다.
- 발바닥은 평면, 발등/앞코는 완만한 곡면으로 만들었다. 허벅지는 아래로 좁아지고 발목과 발이 조금 겹쳐 구슬처럼 분리되어 보이는 형태를 줄였다. 양쪽 발바닥 평면 면적은 각각 약0.08767m², 원본 바닥 높이는0이다.
- 기본6,894→7,414삼각형, 조끼 포함7,614→8,134, 비활성 안경 포함7,978→8,498. GLB219,384→221,920바이트. 기본8천 미만이며 성능 향상을 주장하지 않는다. 이번 시작 시점 다른6종 GLB(앞선 독수리 변경 포함)는 해시 불변이다.
- 최종 lion.glb SHA256: `95d9b54eec529e67ac972130b989f1c4d92ae87f60f5a969eae4a70235b774d2`.

## 검증과 발견 사항

- 기존 단일 앞발에서 새 손잡이 기준점 검사가 실패하는 것을 먼저 확인했다(`game/output/lion-limbs-red.log`). 분리 모델과 런타임 적용 뒤 최종 Godot 재가져오기 및 Debug 빌드로 재검사했다.
- Blender 최종 GLB 재가져오기70검사, 같은 카메라/조명 전후22뷰 통과·확인. 메시 이름/부모, 손목·손잡이, 양쪽 평면 발바닥과 예산을 검사했다. `contract-red.log`는 이름과 달리 적용 후 성공 로그이며 실패 증거로 사용하지 않는다.
- `LionLimbCheck`4,464검사 통과. 6무기 공격/재장전/크기0.8·1·1.2에서 실제 손 정점 간 거리, 그립 오차2mm 미만, 어깨/손목 연결, 리셋, 숨김, 피격/상태이상/실루엣의 두 메시 적용을 검사했다. 기존 `ParrotGripCheck -- --lion`도 이 검사로 연결되며 최종4,464 통과.
- 초기에 재장전 중 전체 캐릭터가 눌리는 기존 연출 때문에 월드 좌표의 손 크기 검사가 실패했다. 손 변형 검사는 공통 캐릭터 좌표에서 실제 정점 간 거리를 비교하도록 수정했다. 기존 전체 캐릭터 눌림은 유지하며, 이를 손만 늘어나는 문제와 구분한다.
- 앵무새/독수리 기존 그립 각3,084 통과(최종 사자 전완 상단 좌표 보정 전; 두 종 모델/런타임은 이후 불변). 최종 모델 적용 후 HunterPoseCheck156, CommonRosterCheck264, localhost ENet CommonRosterNetCheck108 통과.
- Studio 황토색6무기 정지 렌더, 붉은색 수렵3무기 각12프레임·소리 API50검사 통과. 브라우저에서 최신 황토색 사자·나무활 공격 타임라인5를 다시 렌더하고 확인했다. 기존 게임 렌더 경로가 새 모델/손 코드를 사용하므로 별도 웹 모델을 만들지 않았다.
- 실제 Stage2 `LayoutMap` 창에서 추격 이동·정지·몽둥이/칼 명중·빗맞음·기절 취소·활 발사/취소와8장 렌더를 포함해38검사 통과. 자동 입력과 검수 위치 이동이며 전체 맵 수동 완주가 아니다.
- Debug/ExportRelease 빌드 오류·경고0. 최종 Godot 검사 로그에 오류/종료 경고 없음. Blender `Material.use_nodes` 폐기 예정 안내는 남으며 생성/검사는 성공했다. I-17 전체 해결을 뜻하지 않는다.
- 독립 코드/렌더 검토에서 차단 결함 없음. 확대 관절 연결선과 활 자세의 기존 발 음영은 확인했으며 손가락/팔꿈치 관절 후속 범위로 남긴다. 색상은 전용 수치 검사 붉은색, Studio 시각 검사 붉은색/황토색 범위다.

## 남은 범위

개별 손가락 감기·팔꿈치 관절·경사면 발 IK는 추가하지 않았다. 길게 뻗은 전완은 여전히 단일 연결 형태이며, 기존 전체 캐릭터 눌림/걸음 연출은 유지한다. 평평한 발바닥은 조형 개선이며 모든 지형에서 발을 지면에 고정하는 기능이 아니다. 이동 자세에서 발이 올라오는 것은 기존 애니메이션이다. 확대 갈기 음영은 1차 한계로 남는다. 전용 검사의 짚라인 검증은 표시 여부 범위이며 전체 이동 시설을 재주행한 것은 아니다. 실제2PC·장시간 부하/FPS·패키지 export·다른 포유류 확대는 이번 검증 범위 밖이다.

## 재현과 산출물

Blender 빌드/검사는 `prototypes/common-monsters/tools/build_common_monsters.py` 및 `validate_common_monsters.py`에 `-- --species lion`을 전달한다. Python 오류 시 실패하도록 Blender에 `--python-exit-code 1`을 사용한다. `review_parrot.py`에 `-- --species lion --review-name lion-limbs --label before` 또는 `after`, `parrot_contact_sheet.py`에 `--species lion --review-name lion-limbs`를 전달한다. 현재 수정 전 모델은 `output/lion-limbs/before.glb`와 기준 해시에 보존했으며 기존 `lion-quality` 증거는 덮어쓰지 않았다.

Godot import/Debug 빌드 후 `--headless --path game --scene res://tests/LionLimbCheck.tscn`; 실제 창은 `--path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --lion-stage2`. `Start-Game.ps1 -Test`는 이 전용 장면을 실행하지 않는다.

Studio 재시작/`POST /api/reload` 후 `STUDIO_URL=http://127.0.0.1:4323`, `STUDIO_SPECIES=lion` 환경에서 `node studio/tests/hunter-attacks.mjs`. 산출물은 `prototypes/common-monsters/output/lion-limbs`(기준/비교/감사/검토), `studio/output/lion-limbs`(정지/브라우저/검사 로그), `studio/output/hunter-attacks-lion`(최신 공격 프레임), `game/output/lion-limbs-*`(로그), `game/output/hunter-combat-lion-stage2-*.png`(최신 실제 게임 화면). 자동 생성물은 커밋하지 않는다.

근거: [계획](../docs/superpowers/plans/2026-10-25-lion-limbs.md), [런타임](../game/scripts/Characters/CharacterLionPaw.cs), [전용 검사](../game/tests/LionLimbCheck.cs), [모델 생성](../prototypes/common-monsters/tools/build_common_monsters.py).

## 독수리·사자 main 통합 재검증 — 2026-10-25

사용자 요청으로 독수리·사자 모델/그립, Studio 검사, 생성 도구와 문서를 함께 커밋한다. 원격 main 확인 시 기준 커밋은 `84b202d`이며 새 제품 결정이나 밸런스 변경은 없다.

- 현재 통합 대상 트리에서 `Start-Game.ps1 -Test` 통합149, 사자 손4,464, 앵무새/독수리 그립 각3,084, HunterPose156, CommonRoster264, localhost ENet108 통과.
- Blender GLB 독립 재가져오기 독수리50/사자70, Studio 실제 공격 렌더/API 독수리50/사자50 통과. Debug/ExportRelease 빌드 오류·경고0, `git diff --check` 통과.
- 통합 검사 종료 ObjectDB4개 경고는 기존 I-17로 유지한다. 이번 전용 검사에는 종료 경고가 없다. 앞선 실제 Stage2 창 검수 결과는 유지하며 이번 커밋 작업에서 전구간 수동 플레이를 추가 수행한 것은 아니다.
- 로그: `game/output/eagle-lion-integration.log`, `game/output/eagle-lion-merge-*.log`, `studio/output/eagle-lion-merge-*.log`, `prototypes/common-monsters/output/merge-*-validate.log`. 자동 생성물·첨부 이미지는 커밋에서 제외한다.
