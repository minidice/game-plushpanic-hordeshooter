# 거위 기본형 조형 후속 (2026-10-26)

## 범위와 계약

`goose.tres`는 `goose-clean.glb`를 읽는다. 원본 `goose-fitted.glb`는 재현 입력으로 보존한다. 제작기는 `prototypes/grounded-art/tools/build_goose_clean.py`, 독립 재가져오기 검사는 `validate_goose_clean.py`다. Blender의 Z 위쪽·-Y 앞쪽 좌표에서 수정하고 GLB로 내보낸다.

거위의 오리 대비 큰 세로 윤곽과 기존 몸통/얼굴 마스크를 유지하면서 부리 폭과 돌출을 줄이고 눈을 작고 얕게 했다. 기존 날개의 깃털 표면을 끝으로 좁혀 무장 자세의 파지 형태를 작게 하며, 겹친 발 판을 닫힌 단일 물갈퀴/발목 표면으로 교체했다. 몸통·꼬리·볏·복장·무기 메시는 변경하지 않았다. 게임의 기존 `CharacterGripSurface` 날개 변형 방식을 사용하며 새 런타임 특례는 없다.

기존 47개 노드와 부모/전체 변환, 수정하지 않은 메시의 세계 좌표, 모든 메시의 재질 순서를 유지하고 발 이외 메시의 정점/삼각형 수를 보존했다. 바뀐 메시 이름은 `goose_beak_mesh`, 좌우 `Eye_mesh`/`Arm_mesh`/`Foot_mesh`뿐이다. 얼굴·몸 색, 부리/눈/날개/발 팔레트, 얼굴·부리 스타일, 크기, 투구·고글·조끼·배낭 및 저장 키는 그대로다. 충돌 캡슐 0.48×2.18m, 속도 4.4m/s와 전투 수치도 바꾸지 않았다. 조형 수치는 현재 기본형의 제작값이며 최종 사용자 아트 수락으로 보지 않는다.

## 재현과 검사

작업 기준 `de43ab9`의 source SHA-256은 `7a4352c961f4868c3c709f6a73d552cbdf9e584b1c7881119008ac69462a4735`; clean SHA-256은 `62d2014258ab8f9abd822dcf40415733cb6ddd948d22e60ef2c879883717d9f3`다. Blender 전후 각 정면/쿼터/측면 × 실루엣/단색/최종색/장비, 총 24장을 `prototypes/grounded-art/output/goose-clean/`에 생성한다. 폴더는 무시되는 로컬 산출물이다.

| 검사 | 결과 |
|---|---|
| `blender --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_goose_clean.py` | 생성 및 24장 렌더. 원본과 다른 플레이어 GLB 해시 유지 |
| `blender --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_goose_clean.py` | 175개 계약 검사. 47→47 노드, 36,243→35,989 삼각형. 양발 닫힌 외향 단일 연결 표면/지면 밑면/두 재질 채널 포함 |
| `dotnet build game/HordeShooter.csproj --nologo -v q` / Godot `--headless --editor --import` | 최종 C# 빌드 0오류·0경고, GLB 임포트 성공 |
| `ModularCustomization.tscn` / `WeaponGripCheck.tscn` / `ReloadPoseCheck.tscn` | 최종 GLB에서 각각 67 / 187 / 279 통과 |
| `CustomizationToneCheck.tscn` (headless) | 87 통과. 별도 GPU 전용 `CharacterSurfaceCheck.tscn`은 이 종의 몸 메시가 원본과 좌표 동일함을 독립 Blender 검사로 확인했고 이번 가지에서 실행하지 않았다 |
| `GooseCleanCheck.tscn` (실제 Forward+) | 최종본 61 통과. 7무기 × 3날개/발/부리/눈 비율, 도약 파지, 실제 Stage 2 이동·사격·기본/장비 확대 PNG |
| `studio/tests/goose-clean.mjs` (`STUDIO_URL=http://127.0.0.1:4331`) | 최종본 40 통과. 기본/앞/옆/뒤/무장/장비, 얼굴·부리 스타일2개, 크기 극값/재색, 기본 복원 PNG 동일, 실제 `appearance.cfg` 해시 유지 |

Studio·게임 검사는 임시 프로젝트 이름 `Horde Shooter Art Review Goose`를 사용해 별도 `user://`를 썼다. `game/project.godot`은 검사 뒤 원본으로 복원한다. 출력 PNG는 `studio/output/goose-clean/`와 `game/output/goose-clean/`에 있다.

최종 전용/회귀 Godot 로그에는 오류·종료 경고가 없었다. Blender 제작 로그에는 `World.use_nodes`/`Material.use_nodes`의 Blender 6.0 폐기 예정 경고 3건이 있으며 5.2.2 생성/재가져오기 결과와 구분한다.

## 남은 제약

별도 머리/날개/발 피벗의 접합선과 기존 날개 전체 신장 방식은 유지되어 극단적인 포즈에서 넓은 깃 가장자리가 보일 수 있다. 경사면 발 IK, 개별 깃 손가락 관절, 장시간/실제 2 PC 플레이, 모든 무기 조합의 수동 아트 수락은 이번 검증 범위 밖이다. 렌더와 자동 검사는 개별 모델의 최종 미술 승인을 대신하지 않는다.
