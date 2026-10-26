# 순록·인간형 감염체 조형 후속

2026-10-25 · A-25 / C-31. 사용자의 순록·좀비 후속 요청으로 [현재 아트 기준](character-art-direction.md)을 적용했다. 순록은 가지뿔을 핵심 식별 형태로 삼고, 좀비는 기존 인간형 감염체를 다듬었다. 최종 사용자 아트 수락과 구현·검수 완료는 구분한다.

## 현재 구현

- 순록: 연속된 몸/목, 긴 주둥이, 작은 검은 눈, 분리된 귀, 끝으로 가늘어지는 열린 가지뿔, 납작한 갈라진 발굽, 몸에 맞는 조끼와 사각 파우치. 뿔은 장식 메시이며 충돌 반경을 늘리지 않는다.
- 순록 손: 사자의 고정 비율 손/별도 전완 경로(`CharacterLionPaw`)를 재사용한다. `common_reindeer_<left|right>Paw` 아래 `Paw_mesh`, `Wrist`, `Grip`을 둔다. 손 전체를 무기 길이만큼 늘리지 않는다. Blender 어깨 `(±.34,0,1.055)`, 손목 `(±.465,-.07,.735)`, 그립 `(±.465,-.09,.69)`; GLB는 `(x,y,z)` → Godot `(x,z,-y)` 변환이다.
- 순록 눈썹: 공통 10프리셋을 기존 Variant로 선택한다. [공통 데이터](../game/content/data/common_brows.json)의 Head 로컬 부착점은 왼쪽 `(-.20,.46,.25)`, 오른쪽 `(.20,.46,.25)`, yaw 30°. 동물 지원 범위는 앵무새·독수리·사자·순록 4종이다.
- 좀비: 기존 인간형 원본의 두개골·귀·비대칭 얼굴·삐뚤어진 머리카락을 보존하면서 눈 돌출/반짝임과 입·치아 크기를 줄이고, 턱·입·치아의 접합 깊이를 맞췄다. 손은 작은 손바닥/엄지, 신발은 평평한 밑창, 셔츠/양복 소매와 허리 연결은 몸에 맞게 정리했다. 원본 `humanoid.glb`는 읽기 전용으로 사용한다.
- 감염체는 인간 한 종·비무장 근접·일상복 10프리셋을 유지한다. 눈 장구류/스킬을 추가하지 않는다. 배색·이동속도·피해량·무리 스폰·네트워크 형식도 변경하지 않는다. Studio는 게임과 같은 GLB/공통 카탈로그/실제 Enemy 생성 경로를 사용한다.

## 검증과 실제 화면

| 검사 | 최종 결과와 범위 |
|---|---|
| Blender 독립 GLB 재가져오기 | 120 통과. 피벗/부품/그립, 뿔 높이·폭, 발바닥 면적, 눈·치아 접합 및 선택 부품 계약 |
| Debug / ExportRelease | 경고·오류 0 |
| CommonBrowCheck | 503 통과, 4종×4배색×10조합과 상태/숨김/머리 추종 |
| CommonRosterCheck / CommonRosterNetCheck | 264 / 130 통과, localhost ENet 생성·늦은 합류의 순록 눈썹 포함 |
| LionLimbCheck / `--reindeer` | 각각 4,464 통과, 6무기·공격/재장전·크기 0.8/1/1.2·손 정점 거리·표면 접합·상태/숨김 |
| ReindeerZombieCheck | 창 모드 8,024 통과. 순록 6무기와 감염체 10복장, 이동/피격/상태. Stage 2 실제 물리에서 좀비 추격 및 근접 타격으로 플레이어 HP 감소 확인 |
| HunterCombatCheck `--reindeer-stage2 --brow-review` | 창 모드 42 통과, 순록 추격·몽둥이/칼 타격·활 발사/취소와 렌더 |
| Studio `reindeer-zombie.mjs` | 140 통과. 순록 6무기/4배색, 좀비 10복장×2배색 실제 렌더·원본 불변 |
| Studio `common-brows.mjs` | 199 통과. 4종 눈썹 조합/배색·무리/미지원 종 검사 |
| Studio `hunter-attacks.mjs` (reindeer) | 50 통과. 수렵3무기 실제12프레임/효과음·초기화·원본 불변 |

Blender는 같은 카메라의 수정 전/후 실루엣·단색·최종색·장비·플레이 크기 및 머리 극단 자세를 렌더했다. 최종 좀비 측면 실루엣에서 떨어져 보이던 치아가 연결됐음을 확인했다. Studio와 실제 Stage 2의 정면/확대 및 게임 거리 렌더도 확인했다. 자동 이동·공격 검사이며 수동 스테이지 완주로 표현하지 않는다.

기본 삼각형은 순록 3,928, 좀비 4,272. 순록 조끼 1,096, 모든 선택 모듈 포함 5,360. 좀비 모든 선택 모듈의 합은 13,727이며 한 번에 모두 표시하지 않는다(실제 최대 복장 조합 8,000 미만). 평평한 밑창 면적은 순록 좌우 각 0.02656m², 좀비 각 0.04995m². 다른 5종 GLB는 작업 전 해시와 동일하다.

## 발견·수정한 문제와 검사 가정

- 순록 무장 시 기존 공통 캡슐 팔은 새 손 형태를 보존하지 못했다. GLB 손/전완 분리 및 기존 사자 경로 재사용으로 보완했다. 눈썹은 순록 미지원 실패를 먼저 확인한 뒤 카탈로그/검사를 4종으로 확대했다.
- 좀비 측면의 떨어진 두 점은 축소한 입에서 분리된 기존 치아였다. Blender 치아 깊이 약 `y=-.426`과 입 `y=-.381`의 차이를 없애고 턱/입/치아와 얕은 눈 층을 맞췄다. 재가져오기 치아 깊이 회귀 조건을 추가했다.
- 복장 검사 최초 실패: 숨긴 부모 아래 자식 메시의 로컬 Visible을 세던 검사 오류. 실제 모듈 루트와 IsVisibleInTree로 수정했다. 비무장 그립 오류는 무기가 없으면 Infinity를 반환하는 계약을 무장과 동일하게 검사한 오류여서 비무장만 제외했다.
- 순록 어깨 검사 최초 실패: 가장 가까운 정점까지 0.10364m지만 삼각형 면은 어깨와 접했다. 0.1m 허용치를 늘리지 않고 최근접 삼각형 **표면** 거리로 바로잡고 기존 사자도 재검증했다.
- 최종 전용 Godot 로그에는 오류·종료 경고가 없었다. Blender의 `Material.use_nodes` 폐기 예정 경고는 남으며 모든 최종 Blender 프로세스는 exit 0이었다. 기존 I-17 전체 해결을 뜻하지 않는다.

## 재현과 산출물

저장소 루트에서 실행한다. Studio는 실행 중인 포트에 맞추고 GLB 변경 후 로컬 데이터 다시 읽기를 먼저 실행한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/build_common_monsters.py -- --species reindeer zombie
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/common-monsters/tools/validate_common_monsters.py -- --species reindeer zombie
dotnet build game/HordeShooter.csproj --configuration Debug --nologo -v q
$reviewGodot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $reviewGodot --headless --path game --scene res://tests/LionLimbCheck.tscn -- --reindeer
& $reviewGodot --path game --scene res://tests/ReindeerZombieCheck.tscn --resolution 1440x900 --fixed-fps 60
& $reviewGodot --path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --reindeer-stage2 --brow-review
$env:STUDIO_URL='http://127.0.0.1:4323'
node studio/tests/reindeer-zombie.mjs
node studio/tests/common-brows.mjs
```

로컬 증거는 `prototypes/common-monsters/output/{reindeer,zombie}-clean-forms/`, `game/output/reindeer-zombie/`, `game/output/hunter-combat-reindeer-stage2-*.png`, `studio/output/reindeer-zombie/`에 있다. 비교 도구 `review_parrot.py -- --species <species> --review-name <species>-clean-forms`와 `parrot_contact_sheet.py`는 두 종도 지원한다. 수정 전 baseline은 재생성 전에 보존한 로컬 증거이며 새 체크아웃에서 자동 복구되는 파일은 아니다. output은 커밋하지 않는다.

독립 코드/규격 검토에서 차단 결함은 발견되지 않았다. 검토에서 비교 도구의 다른 자산 개수 출력이 고정6으로 남아 있음을 찾아 실제 검사 집합의 개수를 출력하게 수정했다. 이 작업에서는 다른5종 보존을 확인했으며 모델/런타임 변경은 없다. 문서 상대 링크411개도 확인했다.

## 남은 한계

- 별도 머리 피벗과 기존 표시 배율1을 유지했다. 극단적인 뒤로 젖힘에서 순록 조끼 목둘레가 조금 드러나고, 좀비를 앞으로 숙이면 셔츠 위 목 전환선이 보인다. 단일 스킨 메시/경사면 발 IK/손가락 관절 애니메이션은 아니다.
- 작은 눈썹 차이는 게임 거리보다 정면 확대에서 잘 보인다. 종 식별은 순록 뿔·주둥이, 감염체 피부·자세·복장으로 판단한다.
- 호랑이·코뿔소·플레이어 모델의 새 기준 적용, 실제 2PC·장시간 호드/저사양 성능·패키지 export·최종 사용자 아트 수락은 별도다.
