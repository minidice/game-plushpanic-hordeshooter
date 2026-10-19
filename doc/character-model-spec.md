# 캐릭터 모델 규격 — 추가·교체 가이드

> **대상**: 사람과 AI 에이전트 모두. 새 종(Q-1 결정 후보)이나 적 모델을 게임에 넣을 때 이 문서만 보고 작업할 수 있게 쓴 규격입니다.
> **구현**: `game/scripts/Characters/` — `CharacterDefinition.cs`, `CharacterRig.cs`, `PlaceholderModel.cs`, `CharacterCatalog.cs`

## 핵심 원칙

게임 코드는 모델 파일을 **직접 참조하지 않습니다.** 캐릭터 하나 = 정의 파일(`.tres`) 하나이고, 정의 파일이 어떤 모델을 쓸지 가리킵니다.

| 하고 싶은 것 | 할 일 | 코드 수정 |
|---|---|---|
| 새 플레이어 종 추가 | `game/content/characters/`에 `.tres` 추가 | 없음 — 메뉴에 자동으로 나타남 |
| 기존 모델 교체 | 해당 `.tres`의 `Model` 경로만 변경 | 없음 |
| 모델 없이 자리만 잡기 | `Model`을 비우고 `Shape`/색상 지정 → 도형 대체 모델 | 없음 |
| 적 외형 교체 | `game/content/enemies/plush.tres` 등의 `Model` 지정 | 없음 |
| 새 **행동**을 가진 적 | `Enemy`를 상속한 클래스 + `.tres` | 필요 |

## 1. 정의 파일(.tres) 필드

| 필드 | 의미 | 기본값 |
|---|---|---|
| `Id` | 고유 ID. 코드·세이브에서 이 값으로 찾음 | 파일명 |
| `DisplayName`, `Description` | 메뉴 표시용 | |
| `Role` | `0` 플레이어 / `1` 적 | 0 |
| `SortOrder` | 메뉴 정렬 순서 | 0 |
| `Model` | GLB/씬. **비우면 도형 대체 모델** | 없음 |
| `ModelFacesPositiveZ` | 모델의 정면이 +Z면 `true` (glTF 기본), Godot식 -Z면 `false` | true |
| `ModelScale` | 균일 스케일 | 1 |
| `BodyNode` … `MuzzleNode` | 모델 안의 부품 노드 이름 (아래 2장) | 빈 값 |
| `GearNodes` | 장비 부품 노드 목록(헬멧·조끼·배낭). **좀비 버전에서 숨김** | 없음 |
| `BuiltInWeaponNode` | 모델에 포함된 총 노드. 게임 무기를 쥐면 숨기고, 좀비 버전에서도 숨김 | 빈 값 |
| `HandColor` | 게임 무기를 쥐는 손과 좀비 팔의 색 | 크림색 |
| `Shape`, `PrimaryColor`, `SecondaryColor` | 대체 모델 모양(`0` 캡슐 / `1` 둥근형 / `2` 풍선 / `3` 황소)과 색 | |
| `ColliderRadius`, `ColliderHeight` | 충돌 캡슐. **게임플레이 판정은 모델이 아니라 이 값 기준** | 0.45 / 1.8 |
| `MoveSpeed`, `MaxHealth` | 이동 속도(m/s), 체력 | 4.2 / 100 |
| `StepRate`, `WaddleDegrees` | 뒤뚱거림 속도와 좌우 기울기(도) | 13 / 7 |
| `StuffingColor` | 피격 시 터지는 솜 색 (큐트 고어) | 크림색 |
| `MirrorPlayerSpecies`, `ZombieTint` | 적 정의 전용. 아래 4장 | false / 초록 |

예시는 `game/content/characters/goose.tres`(GLB 사용)와 `test_capsule.tres`(대체 모델)를 보세요.

## 2. GLB 모델 규칙

리깅(뼈대)은 **없어야 합니다.** 부품 노드의 위치·회전·스케일을 코드가 직접 움직입니다(idea.md 7.4 "관절 없는 3D").

- **단위**: 미터. 바닥(발바닥)이 원점 Y=0.
- **위쪽**: +Y.
- **정면**: +Z 권장. -Z면 `ModelFacesPositiveZ = false`.
- **크기 기준**: 거위 키 2.18m, 일반 적 약 1.55m. 쿼터뷰 카메라(55°, 직교 크기 17)에서 읽히는 크기입니다.
- **재질**: 메시 수와 재질 슬롯은 적을수록 좋습니다. 거위는 메시 10개·삼각형 1.8만 개로 문제없이 돌았지만, **적 모델은 최대 70마리가 동시에 나오므로** 삼각형 2천 개·재질 2~3개 이하를 목표로 하세요.

### 부품 노드 (전부 선택 사항)

이름이 없거나 틀려도 **동작합니다.** 빠진 부품은 빈 기준점으로 자동 생성되고, 모델이 한 덩어리로 뒤뚱거릴 뿐입니다.

| 부품 | 코드가 하는 일 | 피벗 위치 권장 |
|---|---|---|
| Body | 걸을 때 위아래 출렁임, 좌우 기울기, 반동 시 뒤로 젖힘 | 발바닥 높이, 몸 중심 |
| Deform | 피격·착지 시 납작해지는 스쿼시 | 발바닥 높이 (아래로 눌리도록) |
| Head | 오뚜기(보블헤드) 스프링 — 가속·반동에 출렁임 | 목 위치 |
| LeftFoot / RightFoot | 걸음에 맞춰 앞뒤로 들썩임 | 각 발 중심 |
| Weapon | 사격 반동으로 뒤로 밀림 | 손잡이 |
| Muzzle | 총구 위치 (예광탄 시작점, 총구 섬광) | 총구 끝, Weapon의 자식 |

대체 모델은 이름이 `Body`, `Deform`, `Head`, `LeftFoot`, `RightFoot`, `Weapon`, `Muzzle`입니다. GLB에서도 이 이름을 쓰면 `.tres`의 `*Node` 필드를 비워도 자동으로 찾습니다. 거위처럼 이름이 다르면(`goose_body` 등) 필드에 적어주세요.

## 2.5 장비 분리 규칙 (좀비화·무기 교체용)

플레이어 모델은 **장비를 떼어낼 수 있게** 만들어야 합니다.

- 헬멧, 베레모, 고글, 헤드셋, 두 종류 조끼, 배낭은 각각 **별도 노드**로 두고 `GearNodes`에 이름을 적습니다.
- 총은 **별도 노드**로 두고 `BuiltInWeaponNode`에 적습니다. 게임은 이 총을 숨기고 지금 든 무기(`doc/weapon-spec.md`)를 `Weapon` 기준점에 붙입니다.
- 손이 총 메시에 합쳐져 있어도 됩니다. 게임이 `HandColor` 색의 손을 따로 붙입니다.

거위 예시: `GearNodes = ["goose_helmet", "goose_beret", "goose_goggles", "goose_headset", "goose_vest", "goose_vestPadded", "goose_pack"]`, `BuiltInWeaponNode = "goose_weapon_mesh"`. 정적 날개·앞발은 `goose_leftArm`/`goose_rightArm`으로 분리한다. 무기를 장착하면 이 노드를 숨기고 손잡이에 연결되는 날개·앞발로 교체한다.

외형 색상 채널은 `custom_body`, `custom_beak`, `custom_wing`, `custom_tail`, `custom_feet`, `custom_eyes`, `custom_hair`, `custom_hat`, `custom_vest`, `custom_pack` 재질 이름 접두사로 구분한다. 몸/꼬리는 원래 무늬를 보존하는 색조 곱셈이고 나머지는 선택색으로 교체한다. 새의 날개색이나 머리털 색을 지정하지 않으면 몸 색조를 따른다. 고글/헤드셋은 독립 착용, 조끼는 아래쪽을 감싸는 파우치형과 패딩형 중 선택한다. 종별 선택은 기존 외형 설정 파일에 저장한다.

## 3. 새 종 추가 절차 (예: 수달)

1. `otter.glb`를 `game/assets/characters/otter/`에 둡니다.
2. `game/content/characters/goose.tres`를 복사해 `otter.tres`로 만들고 `Id`, `DisplayName`, `Model` 경로, 부품 노드 이름, 충돌 캡슐 크기를 바꿉니다. 경로를 바꿀 때는 `[ext_resource ... path=...]` 줄을 수정합니다.
3. 검증합니다.

```powershell
cd game
./Start-Game.ps1 -Test      # 헤드리스 통합 검사
./Start-Game.ps1 -Capture   # output/*.png 렌더 확인
./Start-Game.ps1            # 메뉴에서 선택해 직접 플레이
```

통합 검사의 `catalog lists GLB and placeholder player characters`와 `swapping to the GLB goose plays with the same systems`가 교체 구조를 검증합니다. 새 종을 기본 검사 대상으로 삼으려면 `tests/Integration.cs`의 `SwapCheck("goose")` 인자를 바꾸세요.

## 4. 좀비 버전 (일반 좀비 = 플레이어와 같은 종)

`content/enemies/plush.tres`의 `MirrorPlayerSpecies = true`이면, 일반 좀비는 **선택한 플레이어 캐릭터의 모델**로 만들어집니다(`CharacterDefinition.MakeZombie`).

- 모델과 충돌 크기는 플레이어 종을 따릅니다. 체력, 속도, 솜 색, 뒤뚱거림은 `plush.tres`를 따릅니다.
- `GearNodes`와 `BuiltInWeaponNode`는 숨기고, 모든 메시에 `ZombieTint` 색을 덧칠합니다.
- 몸을 앞으로 숙이고, 모델에 있는 `_leftArm` / `_rightArm` 날개·팔을 그대로 사용해 공격 동작을 만듭니다. 해당 팔 노드가 없는 쪽에만 `HandColor`와 좀비 색을 섞은 보조 팔을 붙입니다(`CharacterRig.Zombify`).
- `MirrorPlayerSpecies = false`로 바꾸면 `plush.tres`의 도형 모델(봉제 감염체)로 돌아갑니다.

새 종을 넣으면 그 종의 좀비도 자동으로 생깁니다. 장비 노드만 나뉘어 있으면 추가 작업이 없습니다.

## 5. 알려진 제약

- 피격 판정은 캡슐 하나입니다. 모델 모양과 판정이 크게 다르면 `ColliderRadius/Height`를 맞추세요.
- 사망 연출은 모델 전체가 캡슐 하나로 굴러가는 "볼링핀" 방식입니다. 장비가 떨어져 날아가는 드롭 물리(idea.md 7.4-2)는 아직 없습니다.
- 표정 텍스처 교체(놀람/기절 UV 교체, idea.md 7.2)는 아직 지원하지 않습니다. 추가하면 `CharacterDefinition`에 표정 텍스처 슬롯이 생길 예정입니다.

## 6. 2026-10-20 외형 방향: 귀엽고 우스꽝스러운 감염체

현재 감염체 테마의 일반 적은 플레이어 복제 대신 `humanoid`와 `crawler` 전용 모델을 쓴다. `plush`의 종 복제 기능은 별도로 남아 있다.

플레이어 동물의 둥근 일체형 체형에 맞춰 신규 감염체 5종은 큰 얼굴(서 있는 모델의 약 40~45%), 감춘 목, 짧은 다리, 둥근 손과 비대칭 눈/이빨을 사용한다. 크롤러는 낮게 엎드리고, 끌개는 늘어진 혀, 덮치는 적은 둥근 후드, 토사물 적은 부푼 볼/배로 구분한다. 황소/풍선형도 큰 얼굴과 둥근 몸으로 수정했다. 전투 수치·캡슐 크기·공격 규칙은 유지한다.

원본: `prototypes/character-lab/tools/infected-models.mjs`. 재생성은 아래 순서로 한다. `--enemies-only`를 사용하면 플레이어 모델은 덮어쓰지 않는다.

```powershell
node prototypes/character-lab/tools/export-game-roster.mjs --enemies-only
& 'C:\Util\Blender Foundation\Blender 5.2\blender.exe' --background --python prototypes/character-lab/tools/optimize_game_roster.py -- --enemies-only
node prototypes/character-lab/tools/validate-game-roster.mjs
```

신규 감염체는 3,787~4,223삼각형/6개 메시/최대 6개 재질. 양팔은 별도 어깨 피벗 아래에 보존한다. 생성기의 디테일을 Blender에서 군중용으로 줄인다. Godot 재가져오기가 필요하다. Blender 원본/비교 렌더는 `prototypes/character-lab/output/roster/roster-contact.blend`와 `.png`, 실제 엔진 비교 렌더는 `game/output/expansion-roster.png`에 저장한다.

## 7. 종족별 커스텀 작업실

메뉴에서 거위·오리·라쿤·랫서판다를 먼저 고른다. 선택한 종에만 적용되는 `몸 / 얼굴 / 귀·머리 / 날개·발 / 꼬리 / 장비` 탭에서 편집한다. 기본형·동글형·정찰형 프리셋은 현재 종의 외형을 덮어쓰는 시작점이며, 이후 수동 편집도 가능하다. `user://appearance.cfg`의 종별 섹션에 자동 저장한다. 이전 색상/장비 설정 파일은 새 부위의 기본값으로 읽는다.

`AppearanceCatalog.Scales`가 슬라이더 범위와 저장 데이터 검증의 공통 기준이다. `AppearanceProfile`은 색·장비·형태·크기만 담고, `CharacterPartShapes`가 매번 원래 변환에서 해당 값을 적용한다. 반복 편집 중 크기가 누적되지 않는다. 몸 폭/키는 모델 전체와 장비를 함께 조절하며, 피격 캡슐·능력치는 원래 종의 값을 유지한다. 발은 가로/세로 폭만, 무기를 잡는 날개는 두께만 변경해 접지와 손잡이 연결을 유지한다.

| 조절 부위 | 보존할 GLB 노드 | 현재 선택 |
|---|---|---|
| 눈·눈썹 | `{id}_leftEye`, `rightEye`, `leftBrow`, `rightBrow` | 크기·간격·기본/또렷한/졸린 표정 |
| 부리·주둥이 | 새의 `{id}_beak`, 포유류의 `{id}_snout` | 크기·기본/넓은/짧은 비율 |
| 귀 | `{id}_leftEar`, `rightEar` | 라쿤·랫서판다의 귀 크기 |
| 머리털 | `{id}_crestShort`, `crestSwept` | 없음/작은 털/뒤로 넘긴 털 |
| 날개·발·꼬리 | 기존 `leftArm`, `rightArm`, `leftFoot_mesh`, `rightFoot_mesh`, `tail` | 색·크기 |

눈 피벗은 눈 중심에 있고 반짝임 메시도 함께 움직인다. 눈썹은 자체 중심, 주둥이는 입 주변 중심에서 변형한다. 머리털은 실제 교체 메시이며 모자를 착용하면 숨긴다. 현재 얼굴·부리 모양은 기본 메시의 비율/각도를 바꾸는 방식이다. 다른 윤곽의 새 부품을 추가할 때는 같은 중심/장착 위치를 지키고 Blender 최적화 도구의 보존 목록에 포함한다.

회전·확대·자세·장비/무기 표시 옵션은 미리보기 전용이다. 장비를 숨기면 귀와 선택한 머리털이 드러나며, 무기를 숨기면 정적 날개를 보여준다. 출격 시에는 저장된 실제 착용 설정을 사용한다.

플레이어 재생성은 `export-game-roster.mjs --players-only` 다음 `optimize_game_roster.py -- --players-only` 순서다. 현재 네 종은 약 2.2만 삼각형이며, 편집 가능한 부위 분리로 21~23개 메시를 가진다. 대규모 군중용 LOD/부위 병합은 별도 최적화 대상이다.

검사: `ModularCustomization.tscn`(형태·저장·프리뷰 복원), `AppearanceExpansion.tscn`(색·장비), `WeaponGripCheck.tscn`(총 연결). `CaptureCustomization.tscn`은 별도 테스트 설정 파일로 실제 메뉴를 조작하고 `output/custom-workshop-*.png`를 만든다. 사용자 외형 설정은 바꾸지 않는다.

## 8. 꼬리·공격 자세·피격 표시 보완

꼬리는 원래 뒤쪽 장착점을 기준으로 별도 피벗에서 움직인다. 새는 짧고 납작한 깃털, 포유류는 아래로 내려갔다 끝이 올라오는 둥근 형태이며 이동 중 작은 흔들림만 준다. 원본 최적화 과정에서 포유류 꼬리의 얇은 면이 뒤집히는 문제가 확인되어, 게임 로딩 시 `CharacterTailMesh`가 닫힌 연속 표면으로 교체한다. 줄무늬는 서로 맞닿은 면의 재질 구분이며 겹친 껍질이 아니다. 기존 꼬리 색·크기 커스터마이징을 유지한다. 현재 이 보정은 런타임에 적용되므로 원본 GLB를 다른 프로젝트에 직접 가져가면 보정 코드를 함께 옮기거나 메시를 다시 출력해야 한다.

근접 무기는 손의 파지를 유지한 채 준비·타격·후속 동작·복귀를 한다. 판정은 실제 휘두르는 시점에 1회 실행하며 벽과 높이 차이를 확인한다. 세부 시간과 취소 조건은 [무기 규격](weapon-spec.md)을 따른다.

감염체 5종은 GLB의 `{id}_leftArm`, `{id}_rightArm`에 원래 소매·팔·손을 보존하고 어깨에서 회전시킨다. 몸에 붙은 정적 팔 위에 새 팔을 추가하지 않는다. 동물형은 기존 날개/팔을 쓰고, 팔 노드가 없는 도형형 적만 보조 팔을 생성한다. 최적화 재실행 시 이 피벗을 병합하지 않아야 한다.

실제 피해를 받으면 약 0.1초간 흰색 오버레이가 감쇠한다. 최고 불투명도는 0.72이며 기존 색과 좀비 오버레이를 복원한다. 동적으로 생성한 팔도 포함한다. 피해가 무효인 회피/무적 상태에서는 점멸하지 않는다.

검사: `TailMeleeCheck`, `EnemyAttackCheck`, `HitFlashCheck`. 실제 렌더: `output/tails-rear-quarter.png`, `melee-pose-sequence.png`, `enemy-attack-sequence.png`, `hit-feedback-comparison.png`.
