# 캐릭터 모델 규격 — 추가·교체 가이드

> 현재 아트 목표(2026-10-25): [매끈한 데포르메 동물과 입체적인 장비](character-art-direction.md). 몸의 봉제선·솜/패딩·구슬 부품을 공통 스타일로 권하지 않는다. 기존 모델 구현과 앞으로의 스타일 보정은 구분한다.

> **대상**: 사람과 AI 에이전트 모두. 새 종(Q-1 결정 후보)이나 적 모델을 게임에 넣을 때 이 문서만 보고 작업할 수 있게 쓴 규격입니다.
> **구현**: `game/scripts/Characters/` — `CharacterDefinition.cs`, `CharacterRig.cs`, `PlaceholderModel.cs`, `CharacterCatalog.cs`

외형 기획은 [플레이어 동물 가이드](player-species-guide.md)와 [몬스터 가이드](monster-design-guide.md)로 분리한다. 새 종·적을 추가할 때 각 문서의 특징표와 추가 양식을 먼저 작성하고, 이 문서의 기술 계약으로 연결한다. `.tres` 등록만으로 목록에 추가할 수 있으나 새 종의 커스텀 팔레트·얼굴 무늬·귀·꼬리 지원에는 별도 코드 점검이 필요하다.

## 핵심 원칙

2026-10-25 앵무새 연속형 목 후속: `CharacterDefinition.HeadSpringScale`은 머리 스프링의 **표시 진폭**(0~1, 기본1)이다. `common_parrot.tres`만0.05로 지정하며 실제 스프링 계산/전투는 유지한다. 생성 도구도 이 값을 보존한다. [근거·재현](parrot-clean-forms.md).

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
| `WeaponShoulderOffset` | Arm 피벗→실제 어깨 보정. X는 왼쪽에서 반전. `*_Shoulder` 노드가 있으면 그것을 우선. `.tres`의 `script` 선언 뒤에 기록 | `(0,0,0)` |
| `Shape`, `PrimaryColor`, `SecondaryColor` | 대체 모델 모양(`0` 캡슐 / `1` 둥근형 / `2` 풍선 / `3` 황소)과 색 | |
| `ColliderRadius`, `ColliderHeight` | 충돌 캡슐. **게임플레이 판정은 모델이 아니라 이 값 기준** | 0.45 / 1.8 |
| `MoveSpeed`, `MaxHealth` | 이동 속도(m/s), 체력. **플레이어 정의 전용** — 몬스터의 속도 · 체력은 `content/data/monsters.csv`에서 오므로 `content/enemies/*.tres`에는 적지 않는다(I-94) | 4.2 / 100 |
| `StepRate`, `WaddleDegrees` | 뒤뚱거림 속도와 좌우 기울기(도) | 13 / 7 |
| `StuffingColor` | 피격 시 터지는 솜 색 (큐트 고어) | 크림색 |
| `MirrorPlayerSpecies`, `ZombieTint` | 적 정의 전용. 아래 4장 | false / 초록 |

예시는 `game/content/characters/goose.tres`(GLB 사용)와 `test_capsule.tres`(대체 모델)를 보세요.

## 2. GLB 모델 규칙

리깅(뼈대)은 **없어야 합니다.** 부품 노드의 위치·회전·스케일을 코드가 직접 움직입니다(idea.md 7.4 "관절 없는 3D").

- **단위**: 미터. 바닥(발바닥)이 원점 Y=0.
- **위쪽**: +Y.
- **정면**: +Z 권장. -Z면 `ModelFacesPositiveZ = false`.
- **크기 기준**: 거위 키 2.18m, 일반 적 약 1.55m. 쿼터뷰 카메라에서 읽히는 크기(작성 당시 55° · 직교 17 m, 2026-10-27부터 60° · 원근 · 화면 높이 21 m — L-2)입니다.
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
| Muzzle | 총구 위치 또는 감염체의 토사물·연결 효과 시작점 | 총은 총구 끝/Weapon의 자식, 전용 감염체는 입 앞/Head의 자식(6.2) |

대체 모델은 이름이 `Body`, `Deform`, `Head`, `LeftFoot`, `RightFoot`, `Weapon`, `Muzzle`입니다. GLB에서도 이 이름을 쓰면 `.tres`의 `*Node` 필드를 비워도 자동으로 찾습니다. 거위처럼 이름이 다르면(`goose_body` 등) 필드에 적어주세요.

## 2.5 장비 분리 규칙 (좀비화·무기 교체용)

플레이어 모델은 **장비를 떼어낼 수 있게** 만들어야 합니다.

- 헬멧, 베레모, 고글, 헤드셋, 두 종류 조끼, 배낭은 각각 **별도 노드**로 두고 `GearNodes`에 이름을 적습니다.
- 총은 **별도 노드**로 두고 `BuiltInWeaponNode`에 적습니다. 게임은 이 총을 숨기고 지금 든 무기(`doc/weapon-spec.md`)를 `Weapon` 기준점에 붙입니다.
- 손이 총 메시에 합쳐져 있어도 됩니다. 게임이 `HandColor` 색의 손을 따로 붙입니다. 2026-10-23 네 동물은 새 날개·앞발 끝이 손을 이루므로 무기 빌더의 구형 손 메시를 숨깁니다. 다른 모델의 보조 손 경로는 유지합니다.

거위 예시: `GearNodes = ["goose_helmet", "goose_beret", "goose_goggles", "goose_headset", "goose_vest", "goose_vestPadded", "goose_pack"]`, `BuiltInWeaponNode = "goose_weapon_mesh"`. 정적 날개·앞발은 `goose_leftArm`/`goose_rightArm`으로 분리한다. 무기를 장착하면 이 노드를 숨기고 손잡이에 연결되는 날개·앞발로 교체한다.

2026-10-26 거위 현재 기본형은 `goose-clean.glb`이며 원본 `goose-fitted.glb`를 보존한다. 기존 노드/피벗·재질 채널·얼굴 마스크와 날개 무장 변형은 그대로 사용한다. [수정 메시 범위와 독립 재가져오기 검사](goose-clean-forms.md).

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

### 6.1 앞뒤 볼륨 보강 (2026-10-20, A-2·I-68)

동물보다 앞뒤가 납작하다는 사용자 피드백에 따라 생성기와 감염체 5종 GLB를 수정했다. 런타임 전체 모델 스케일은 그대로이며, 아래 값은 조정 가능한 현재 조형값이다.

| 부위 | 이전 → 현재 | 적용 범위 |
|---|---|---|
| 머리 깊이 | `Head`의 Z 배율 1 → 1.18. 두개골 깊이/폭 약 77% → 91% | 5종 공통. 크롤러의 전체 머리 크기 0.78은 유지. 눈·입·턱·머리털·후드·혀도 같은 머리 좌표에서 함께 변형 |
| 일반 몸통 Z 반경 | 0.29 → 0.35 (약 +21%) | `humanoid`, `tether`, `leaper`. 폭·높이·중심·팔/발 피벗 유지 |
| 토사물 적 몸통 Z 반경 | 0.34 → 0.40 (약 +18%) | `spitter`. 배 중심 Z 0.22 → 0.28, 배 Z 반경 0.17 → 0.20, 배꼽 Z 0.383 → 0.473 |
| 셔츠 장식 Z 위치 | 단추 0.255 → 0.31, 주머니 0.245 → 0.295 | `humanoid`. 두꺼워진 몸통 앞면에 맞춤 |
| 크롤러 몸통 | Z 반경 0.48 유지 | 이미 길게 엎드린 실루엣이라 머리만 보강 |

1차 조형 수정에서는 캡슐·공격 거리·전투 수치를 유지했다. 이후 연동 점검에서 필요한 캡슐/입 효과 보정을 수행했으며 현재 계약은 6.2를 따른다. 동물 모델·황소/풍선형은 변경하지 않았다. 삼각형 수는 인간형 4,223 / 크롤러 3,787 / 끌개 3,881 / 덮치는 적 3,896 / 토사물 적 4,005로 기존과 같다.

재생성은 위 명령을 사용한다. 비교 렌더는 빌드·Godot 재가져오기 후 `res://tests/CaptureInfectedVolume.tscn`을 **창 모드**로 실행한다. 기본 출력은 `game/output/infected-volume-after-{front,side,rear,quarter}.png`, 실행 인자 `-- --volume-tag=before`를 주면 before 이름으로 저장한다. before는 수정 전 에셋으로 실행해야 한다. 장비 없는 기본 라쿤을 함께 배치하고 정면·정측면·후면·45° 사선에서 동일 조명/카메라로 비교한다.

검증: GLB 구조·예산 검사, `TailMeleeCheck`, `EnemyAttackCheck`, `HitFlashCheck`, `EnemyExpansion` 통과. `CaptureInfectedVolume` 전후 4방향과 `CaptureExpansion`의 실제 엔진 목록·게임 화면을 확인했다. 헤드리스 일부 종료 경고 및 생성기 경고는 I-68에 별도 기록했다. 정점 수가 같다는 사실만으로 대규모 호드 성능을 새로 측정했다고 해석하지 않는다.

### 6.2 모델과 판정·연출 연동 (2026-10-20)

모델 메시에서 충돌체를 자동 추출하지 않는다. `.tres`의 `ColliderRadius`·`ColliderHeight`가 이동/피격/시체 캡슐의 기준이며, 모델 변경 시 사람이 점검해 필요한 값만 맞춘다.

| 종류 | 반경 이전 → 현재 | 점검 결과 |
|---|---|---|
| 인간형 / 끌개 | 0.36 → 0.40m | 몸통 중심 Z -0.04, Z 반경 0.35로 뒤끝이 -0.39m. 기존 반경 밖 새 몸통에서 탄환 누락 재현 |
| 덮치는 적 | 0.40 → 0.48m | 몸통 중심 Z -0.12, Z 반경 0.35로 뒤끝이 -0.47m. 기존 판정 보강 필요 |
| 토사물 적 | 0.52m 유지 | 배 앞끝 약 +0.48m를 기존 반경이 포함 |
| 크롤러 | 0.43m 유지 | 몸통 길이는 이번에 바꾸지 않음. 낮고 긴 외형을 단순 수직 캡슐로 근사하는 기존 제약 유지; 탄환을 받을 최소 높이 1.4m(C-16) 유지 |

높이는 유지한다. 일반 근접은 적 반경 + 플레이어 반경 + `attack_reach_m`로 판정하므로 중심 간 접촉/타격 거리는 인간형·끌개 +4cm, 덮치는 적 +8cm 조정된다. 덮치는 적의 도약 접촉·유지 경계도 기존 반경 기반 공식을 따른다. `attack_reach_m` 자체, 특수 최대 사거리·피해·시간·이동 속도는 바꾸지 않았다. 커서 선택·회피 밀침·플레이어 근접·군중 분리도 기존 반경 참조 경로를 사용한다. 반경을 덮어쓰는 재생성 문제가 없도록 `write-roster-resources.mjs`도 같은 값으로 갱신했다.

감염체 GLB의 `Muzzle`은 이제 `Weapon` 아래 임의 위치가 아니라 **`Head` 아래 입 앞 좌표 `(0.02, -0.207, 0.39)`**에 있다. 머리 깊이·회전·흔들림을 따라가며 `MuzzleNode = "Muzzle"`로 찾는다. `RangedEnemy`는 토사물만 이 위치에서 생성하고, `CaptureEnemy`는 끌개가 붙잡고 있을 때의 연결 시작점으로 쓴다. 도약 예고/바닥 표시와 무장 동물 총격 경로는 유지한다. 입이 벽 밖으로 나가 공격을 통과시키지 않도록 몸→입과 입→목표의 차폐를 함께 확인한다.

`InfectedAlignmentCheck`는 새 몸통 안의 광선 명중/바깥 빗나감, 실제 Jolt 벽 접촉, 사망 시 반경 전달, 토사물 입 시작점·얇은 벽 차폐, 끌개 연결 끝점을 확인한다. 창 모드로 실행하면 `game/output/infected-alignment-spitter-mouth.png`와 `infected-alignment-tether-mouth.png`도 저장한다. 이번 검사는 캡슐이 귀·손·혀·크롤러의 길게 뻗은 얼굴까지 메시처럼 정밀하게 덮는다는 뜻이 아니다. 뼈별 판정·별도 머리 판정·방향별 크롤러 충돌체는 구현하지 않았다.

### 6.3 몬스터 표면 연결과 사망 자세 (2026-10-23)

현재 몬스터 GLB 생성은 감축 전에 부위 내 위치 중복 정점을 연결하고, 감축 뒤 새 열린 경계가 생기지 않았는지 검사한다. UV/재질은 유지하고 원래 열린 토사물 어미 후드 가장자리를 강제로 막지 않는다. `optimize_game_roster.py -- --monsters-only --no-preview`는 감염체 5종·황소·풍선·보스 3종만 다시 만든다. 플레이어는 이 옵션 대상이 아니다.

현재 삼각형 수: 인간형 4,140 / 크롤러 3,708 / 끌개 3,780 / 덮치는 적 3,816 / 토사물 적 3,924 / 황소 2,592 / 풍선 2,240 / 황소 두목 4,344 / 풍선 대왕 3,690 / 토사물 어미 3,282. 앞 절의 수치는 당시 기록이다. 두목 황소는 형상을 유지하면서 `boss_leftLeg`·`boss_rightLeg` 피벗을 추가했고 발은 각 다리 아래에 있다.

네임드·보스 사망은 `CharacterDeathPose`의 체형별 자세와 `Effects.Topple`을 사용한다. 황소/직립형 0.70초, 둥근 체형 0.74초, 황소 두목 0.80초. 팔·머리·다리/꼬리를 시간차로 접고, 현재 공격 자세에서 전환한다. 기록·강체는 고정, 외형은 실제 메시의 바깥 정점으로 바닥에서 0.02m 여유를 두고 정착한다. `CorpseSurface`가 메시별 단순화하지 않은 convex hull 정점을 약한 참조 캐시에 보관한다. 자세가 바뀌어도 매 프레임 GLB 전체 배열을 다시 읽지 않는다. 무장 정예는 사망 시 조준용 셰이더 팔을 숨기고 실제 휴식 팔을 사용한다. 일반 시체는 기존 물리 전도 경로다.

검증·남은 제약·전후 렌더는 [몬스터 개선 기록](monster-visual-improvements.md). 평지/높은 수평면/공중 사망을 검사하며, 경사·계단에 몸 전체가 밀착하는 관절 물리 래그돌은 구현하지 않았다.

## 7. 종족별 커스텀 작업실

메뉴에서 거위·오리·라쿤·랫서판다를 먼저 고른다. 선택한 종에만 적용되는 `몸 / 얼굴 / 귀·머리 / 날개·발 / 꼬리 / 장비` 탭에서 편집한다. 프리셋은 `content/data/appearance_presets.json`에서 읽는다. 모든 종에 기본형·동글형·정찰형(체형·귀·꼬리·장비)이 있고, 그 뒤에 종별 색 프리셋 2개(예: 흑고니, 한밤 라쿤)가 붙는다. **고르면 바로 적용**되어 현재 외형을 덮어쓰며, 이후 수동 편집도 가능하다. 키는 `appearance.cfg`와 같고, 적지 않은 항목은 기본값이다. 잘못된 키·값은 오류 목록으로 보고한다. `user://appearance.cfg`의 종별 섹션에 자동 저장한다. 이전 색상/장비 설정 파일은 새 부위의 기본값으로 읽는다.

`AppearanceCatalog.Scales`가 슬라이더 범위와 저장 데이터 검증의 공통 기준이다. `AppearanceProfile`은 색·장비·형태·크기만 담고, `CharacterPartShapes`가 매번 원래 변환에서 해당 값을 적용한다. 반복 편집 중 크기가 누적되지 않는다. 몸 폭/키는 모델 전체와 장비를 함께 조절하며, 피격 캡슐·능력치는 원래 종의 값을 유지한다. 발은 가로/세로 폭만, 무기를 잡는 날개는 두께만 변경해 접지와 손잡이 연결을 유지한다.

| 조절 부위 | 보존할 GLB 노드 | 현재 선택 |
|---|---|---|
| 눈·눈썹 | `{id}_leftEye`, `rightEye`, `leftBrow`, `rightBrow` | 크기·간격·기본/또렷한/졸린 표정 |
| 부리·주둥이 | 새의 `{id}_beak`, 포유류의 `{id}_snout` | 크기·기본/넓은/짧은 비율 |
| 귀 | `{id}_leftEar`, `rightEar` | 라쿤·랫서판다: 모양(기본/둥근/뾰족한/접힌), 크기, 바깥쪽 기울기(-20~+40°, 모자를 쓰면 기존처럼 옆으로 접힘) |
| 머리털 | `{id}_crestShort`, `crestSwept` | 없음/작은 털/뒤로 넘긴 털 |
| 날개·발·꼬리 | 기존 `leftArm`, `rightArm`, `leftFoot_mesh`, `rightFoot_mesh`, `tail` | 팔 크기(색은 몸을 따라감), 발 색·크기, 꼬리 모양(기본/풍성한/길고 가는/짧고 동근/말린)·색조·크기 |

눈 피벗은 눈 중심에 있고 반짝임 메시도 함께 움직인다. 눈썹은 자체 중심, 주둥이는 입 주변 중심에서 변형한다. 머리털은 실제 교체 메시이며 모자를 착용하면 숨긴다. 현재 얼굴·부리 모양은 기본 메시의 비율/각도를 바꾸는 방식이다. 다른 윤곽의 새 부품을 추가할 때는 같은 중심/장착 위치를 지키고 Blender 최적화 도구의 보존 목록에 포함한다.

### 7.1 몸 투톤 색

몸 색은 **주색 · 보조색 · (라쿤·랫서판다만) 얼굴 무늬색**을 각각 고른다. 고른 색이 그대로 나오는 절대 색이다(예전의 "몸 색조" 곱하기가 아님).

- 원본 GLB는 생성기(`face-pattern.mjs`)의 정점 색을 포함하지만, 게임은 `character_skin.gdshader`에서 종별 영역 마스크와 선택한 팔레트를 직접 합성한다. 기본색도 같은 렌더를 쓰므로 무늬의 곡선이 메시 감량에 따라 계단지지 않는다.
- 역할: 주색 = 털, 보조색 = 배(라쿤은 얼굴 크림색 포함, 랫서판다는 짙은 배), 무늬색 = 라쿤 눈가 마스크 / 랫서판다 흰 얼굴 무늬.
- **같은 팔레트 색으로 칠한 부위는 함께 바뀐다**: 팔(날개), 손, 귀 바깥, 머리털(따로 고르지 않았을 때), 눈썹, 포유류 주둥이(따로 고르지 않았을 때), 새 꼬리. 팔 색은 따로 고르지 않는다(예전에 저장한 팔 색은 무시). 발은 따로 고를 수 있다.
- 무늬 공식이나 팔레트를 바꾸면 생성기 `face-pattern.mjs`와 `character_skin.gdshader`, `CharacterFacePattern.Regions`/`CharacterBodyTones.Weights`, `AppearanceCatalog.Palette`를 함께 확인한다. `CustomizationToneCheck`는 배/등 색 분리와 기본 팔레트 복원을, `CharacterSurfaceCheck`는 실제 GPU 무늬 렌더를 검사한다.
- 손(총을 잡는 손)은 팔이 바뀐 만큼만 같이 바뀐다. 팔이 그대로면 종의 손 색 그대로다.
- **예전 저장 파일**: `appearance.cfg`의 종 섹션에 `tone_version`이 없으면 `body`는 몸 전체에 곱하던 색조다. 읽을 때 팔레트 색마다 그 색조를 곱해(선형 공간) 주색·보조색·무늬색으로 바꾼다. 몸 공식이 선형이라 예전 모습과 거의 같다.
- 렌더: `shaders/character_skin.gdshader`가 원래 표면의 로컬 좌표로 무늬를 픽셀마다 계산한다. 색 변경은 캐릭터별 셰이더 파라미터 3개만 갱신하며 메시를 다시 만들지 않는다. 원본 메시/그림자 메시를 함께 유지하므로 I-52의 서로 다른 표면 문제가 없다. 원본 GLB 정점 색은 외부 도구용으로 남지만 게임 몸 렌더는 사용하지 않는다.
- `SkinColors`/`SkinToneWeights`는 자동 검사에 쓰는 CPU 참고 샘플이며 실제 렌더 정점 색이 아니다. 무늬 렌더 검증은 `CharacterSurfaceCheck`를 창 모드로 실행한다. I-50의 원본 정점 색과 공식의 잔차는 직접 합성 방식으로 제거했다. 게임 크기에서는 화면 미분으로 경계를 약 한 픽셀만큼 완화한다.

### 7.2 귀·꼬리 메시

2026-10-23 현재 기본형: 라쿤·랫서판다의 귀·꼬리는 복원한 `*-fitted.glb` 메시를 사용한다. 아래 런타임 귀 생성은 둥근·뾰족한·접힌 모양에, 꼬리 생성은 새 기본형 및 다른 모양에 적용한다. 기본형으로 돌아오면 원래 메시와 변환을 복원하고 색 오버라이드는 유지한다. 뾰족 귀 높이는 새 기본형보다 확실히 높도록 0.46 m로 조정했다. [적용 기록](grounded-art-integration.md).

- 귀는 생성기의 납작한 판 대신 **48분할 동심 윤곽의 오목한 껍질**(뒤는 털, 앞은 안쪽 색, 도톰한 테두리에서 맞물림). 안쪽 재질 경계도 같은 둥근 윤곽을 따르도록 게임이 다시 만든다(`CharacterEarMesh.cs`). 귀 메시는 (종, 모양)마다 한 번 만들어 모든 캐릭터가 같이 쓰고(색은 캐릭터별 재질), 종료 때 `QuitHygiene`이 비운다. 좀비 복제도 같은 귀를 쓴다. 귀 안쪽은 주둥이와 같은 색(라쿤 보조색, 랫서판다 무늬색)을 따른다.
- 꼬리는 기존 `CharacterTailMesh`에 길이·굵기·말림 매개변수를 더해 모양별로 다시 만든다. 모양을 바꿔도 캐릭터별 재질(색)은 유지된다. 새 꼬리는 옆 방향 축을 +X로 고정해, 말린 꼬리 끝이 수직을 넘어도 뒤집히지 않는다.

회전·확대·자세·장비/무기 표시 옵션은 미리보기 전용이다. 장비를 숨기면 귀와 선택한 머리털이 드러나며, 무기를 숨기면 정적 날개를 보여준다. 출격 시에는 저장된 실제 착용 설정을 사용한다.

플레이어 원본 재생성은 `export-game-roster.mjs --players-only` 다음 `optimize_game_roster.py -- --players-only` 순서다. 이후 `prototypes/grounded-art/tools/publish_game_models.py`를 Blender로 실행해 `*-fitted.glb`를 다시 만든다. 카탈로그와 `write-roster-resources.mjs`는 fitted 파일을 참조한다. 기존 원본 GLB의 예산은 오리·거위 29,556, 라쿤 27,398, 랫서판다 28,204삼각형이다. 이 값은 새 조끼·날개가 들어간 fitted 파일의 예산이 아니다. 대규모 군중용 LOD/부위 병합은 별도 최적화 대상이다.

검사: `ModularCustomization.tscn`(형태·저장·프리뷰 복원), `AppearanceExpansion.tscn`(색·장비), `CustomizationToneCheck.tscn`(투톤·팔 따라가기·프리셋·귀·꼬리, 창 모드면 `output/custom-tones-*.png`), `WeaponGripCheck.tscn`(총 연결). `CaptureCustomization.tscn`은 별도 테스트 설정 파일로 실제 메뉴를 조작하고 `output/custom-workshop-*.png`를 만든다. 사용자 외형 설정은 바꾸지 않는다.

### 7.3 확대 품질 검사 (2026-10-20)

- 얼굴/몸 무늬는 네 종 모두 같은 연속 마스크 렌더를 쓴다. 오리·거위는 얼굴 무늬와 포유류 귀가 없지만 배색 경계를 부드럽게 표시하고, 눈·부리의 각진 윤곽을 함께 개선했다.
- 모델 생성기의 새 부리는 48×24분할. Blender 감량은 몸 0.75, 눈·눈썹·주둥이·부리 등 얼굴 부품 0.70, 그 밖의 일반 부품 0.19(기존 헤드셋 예외 유지). 부위 피벗·재질 채널·콜라이더는 그대로다.
- `CharacterSurfaceCheck.tscn`은 검정/흰색 얼굴 마스크를 실제 GPU로 렌더해 연속 곡선과 비교한다. 내부 픽셀 오류 기준 1% 미만; 수정 전 6,982/63,635, 수정 후 0/63,635. 네 종 기본/커스텀 색의 앞면·뒷면 GPU 검사를 포함해 33개 통과. 축소 화면의 계단 완화와 색 합성도 실제 렌더로 검토한다.
- `output/surface-after-{종}-front/quarter/custom/gear/game.png`는 네 종의 정면·쿼터뷰·색 변경·장비·실제 게임 크기 캡처다. 기본 체형·색뿐 아니라 커스텀 색, 모자에 따른 귀 접힘, 기존 저장 변환 및 피격 오버레이를 검사한다.
- GLB의 얼굴/부리 형상과 2026-10-23 fitted 모델의 기본 귀 보정은 파일에 포함된다. 연속 얼굴 마스크와 기본형 외 귀 모양은 게임 런타임 기능이므로, GLB만 다른 프로젝트에 가져가면 이 기능을 함께 옮기거나 별도로 구워야 한다.

- 성능 재측정: RTX 5070 Ti, Forward+, 1600×900, VSync 끔, 스테이지 1에서 4초 측정. 동물 시체 100개 평균 5.25ms(190.6fps), p95 5.74ms. 생존 적/네트워크 없는 짧은 렌더 부하 측정이며 저사양·장기 호드·멀티 검증은 아니다. `output/surface-performance.json` 참조.

### 7.4 라쿤·랫서판다 종 구분과 번짐 (2026-10-20)

- 라쿤: 옆으로 이어지며 관자놀이 쪽으로 올라가는 검은 눈띠, 밝은 주둥이, 회색 몸, 짧고 둥근 기본 귀. 두 개의 원형 검은 안경을 붙인 형태를 피한다.
- 랫서판다: 주둥이와 자연스럽게 이어지는 넓은 흰 볼, 짧은 적갈색 눈 아래 무늬, 둥근 흰 입체 눈썹, 붉은 몸, 조금 더 높고 삼각형에 가까운 기본 귀. 이전의 큰 타원 눈썹 페인트 + 막대 눈썹 중복은 제거했다. 기존 귀 모양 4종 선택·모자 접힘은 유지한다.
- 라쿤 밝은 털 기본색은 `#e8e5dc`, 랫서판다 흰 털은 `#f3e4cb`. 랫서판다 눈 아래 색은 주색·보조색을 선형 공간에서 50:50으로 혼합한다. 따라서 사용자 색을 바꿔도 고정 갈색이 남지 않는다.
- 얼굴 탭의 **무늬 경계 번짐**: 0~100%, 기본 25%. 내부 값 `MarkingSoftness`/저장 키 `marking_softness`는 0~1이며 잘못된 값은 범위 제한, NaN/무한대는 .25로 복원한다. 예전 저장에는 .25를 적용한다. 오리·거위에는 보이지 않는다. 기존 `AppearanceCatalog.Scales` 경로로 프리셋 JSON과 종별 저장도 지원한다.
- 번짐은 무늬 경계의 전이 폭만 바꾼다. 눈 간격을 편집하면 무늬의 가로 좌표도 같은 비율로 움직인다. 영역 계산식은 `CharacterFacePattern.cs`(CPU 참고), `character_skin.gdshader`(실제 픽셀), `src/face-pattern.mjs`(GLB 정점 색 대체 표현)에 맞춰 유지한다.
- 검증: `CharacterSurfaceCheck` 43개, `CustomizationToneCheck` 89개(실제 슬라이더 입력 → 재질 갱신 → 저장), `ModularCustomization` 67개, `AppearanceExpansion`, `HitFlashCheck`, 제작기 17개 및 GLB 검증. 번짐 0/100%에서 전이 픽셀 1,565 → 17,115로 증가하며, 눈 간격 .85/1.12에서도 세로 무늬가 따라간다.
- 비교 렌더: `output/surface-after-species-pair.png`. 이전 절 7.3의 수치·성능 측정은 I-53 당시 기록이며 이번 무늬 디자인의 미술적 완성도나 새 성능 측정으로 해석하지 않는다.

## 8. 꼬리·공격 자세·피격 표시 보완

꼬리는 원래 뒤쪽 장착점을 유지한다. 2026-10-26 라쿤·랫서판다의 긴 스타일0/1/2/4는 중간 부풀음 없는 단면·완만한 기본 곡선으로 바꾸고, 고정 루트와3개 굽힘 구간이 끝까지 지연을 전달하는 꼬리 전용 GPU 스킨을 사용한다. 정지하면 잦아들며 Studio 정적 미리보기와 초기화는 중립 자세로 복원한다. 짧은 스타일3·조류는 기존 형상/움직임을 유지한다. 기본 `*-clean.glb`의 Blender 프로파일과 `CharacterTailMesh`의 런타임 형상을 맞추며 예전 감염 변형에도 적용한다. 줄무늬는 서로 맞닿은 면의 재질 구분이며 겹친 껍질이 아니다. 꼬리 색·크기·스타일ID/기본형 복원 계약은 유지한다. 전신 리그나 꼬리 충돌 물리는 추가하지 않는다. [수치·검사·제약](mammal-tail-quality.md).

날개·앞발과 기본 조끼의 현재 게임 적용 계약은 [아트 적용 기록](grounded-art-integration.md)을 따른다. 몸 폭·키는 기존 `Model.Scale`, 날개 크기는 원본 표면에 한 번 적용한다. `FitRoot` 편집용 계층을 게임에 중첩하지 않는다. `CharacterGripSurface`와 공통 변형 셰이더가 한손/양손·달리기·재장전·근접 손잡이를 따르며 피격과 실루엣도 같은 변형을 쓴다.

근접 무기는 손의 파지를 유지한 채 준비·타격·후속 동작·복귀를 한다. 판정은 실제 휘두르는 시점에 1회 실행하며 벽과 높이 차이를 확인한다. 세부 시간과 취소 조건은 [무기 규격](weapon-spec.md)을 따른다.

감염체 5종은 GLB의 `{id}_leftArm`, `{id}_rightArm`에 원래 소매·팔·손을 보존하고 어깨에서 회전시킨다. 몸에 붙은 정적 팔 위에 새 팔을 추가하지 않는다. 동물형은 기존 날개/팔을 쓰고, 팔 노드가 없는 도형형 적만 보조 팔을 생성한다. 최적화 재실행 시 이 피벗을 병합하지 않아야 한다.

실제 피해를 받으면 약 0.1초간 일반 흰색/치명타 노란색 오버레이가 감쇠한다. 최고 불투명도는 0.72이며 기존 색과 좀비 오버레이를 복원한다. 동적으로 생성한 팔도 포함한다. 피해가 무효인 회피/무적 상태에서는 점멸하지 않는다.

검사: `TailMeleeCheck`, `EnemyAttackCheck`, `HitFlashCheck`. 실제 렌더: `output/tails-rear-quarter.png`, `melee-pose-sequence.png`, `enemy-attack-sequence.png`, `hit-feedback-comparison.png`.


<a id="damage-effects"></a>
### 8.1 피격·사망 섬광 (2026-10-22, A-19·C-19)

2026-10-27 바닥 흔적 후속: 아래 기존 기록의 페인트는 이제 짙은 계열별 체액 고임으로 대체한다. 불규칙한 윤곽·가까운 방울·젖은 광택/건조를 코드·셰이더로 표현하며 솜 파편·사망 섬광·시체 동작은 유지한다. [현재 표시 계약](vfx-quality.md#사망-체액-흔적--2026-10-27), [검사·렌더](features/effects-audio/log/017-code-body-fluid.md).

사용자 첨부 `공격효과참고.mp4`·`피격효과.mp4`에서 확인한 짧은 흰 몸 점멸과 주황빛 방사형 타격선을 반영한다. 영상에서 사망 순간은 뚜렷하게 확인되지 않아 사망 연출은 같은 색감으로 확장한 현재 시안이다. Blender 모델·외부 이미지 텍스처 없이 Godot 메시와 절차적 셰이더로 그린다.

- **피격**: `CharacterHitFlashEffect`는 일반 흰색(`#ffffff`), 치명타 노란색(`#ffd34a`) 오버레이를 사용한다(0.1초, 최고 불투명도 0.72 유지). `CharacterRig.FlashHit`가 몸과 같은 색의 6방향 타격선과 작은 중심 섬광을 함께 시작하며, 0.16초 동안 바깥으로 이동·감쇠한다. 직경은 `clamp(ColliderHeight × 1.45, 1.8, 3.1)`m. 몸 중심 높이 `ColliderHeight × 0.55`에 붙어서 움직인다.
- **투명도·색**: 선·원 셰이더의 최고 불투명도는 0.90(투명도 10%)이고 기존 시간 곡선으로 더 투명해진다. 주황 가장자리/흰 중심 혼합을 제거해 하나의 `tint`를 쓴다. 몸 오버레이는 이미 불투명도 0.72였으므로 이를 유지한다. 가산 합성과 배경 밝기에 따라 체감 투명도는 달라질 수 있다. 치명타 뒤 일반 피해는 재사용 재질의 색을 즉시 흰색으로 되돌린다.
- **반복 피격**: 캐릭터마다 활성 타격선 하나를 재시작한다. 산탄 여러 펠릿·화상 틱이 각자 메시나 조명을 쌓지 않는다. 마지막 피격 후 시간이 지나면 제거된다. 기존 점멸의 공유 재질 보호, 생성 팔·장비 포함, 원래 오버레이 복원은 유지한다.
- **사망**: 작은 피격 타격선을 제거하고 몸 점멸을 한 번 시작한다. 일반 처치는 흰색, 치명타 자체로 처치한 경우 몸·선·원 모두 노란색이다. `Enemy.CriticalDeath`를 명중 콜백 전에 확정해 풍선 카드가 콜백 안에서 사망시켜도 색이 유지된다. 치명타가 아니었던 후속 사망이나 주변 연쇄 피해에 이전 치명타 색을 전파하지 않는다. 사망점에 8방향 섬광과 끊어진 확산 링을 0.32초 표시한다. 직경은 `clamp(ColliderHeight × 1.65, 2.2, 4.0)`m. 시체에 붙어 따라가지 않고 독립적으로 사라진다. 기존 솜 파편·페인트·넘어짐·시체 물리/수명/관전 처리는 유지한다. 다운은 사망으로 취급하지 않는다.
- **가시성·비용**: 효과마다 공용 사각형 메시 1개와 독립 재질을 사용한다. 카메라 방향을 향하고 깊이 검사를 유지한다. 몸에 가려지지 않도록 카메라 쪽으로 `clamp(ColliderRadius, 0.2, 1.0)`m 옮긴다. 작은 효과에 추가 조명·화면 흔들림은 없다. 피격 타격선은 Rig의 숨김을 따르며, 사망 버스트는 **시체로 옮겨 공개하기 전** Rig가 보일 때만 생성한다. 기존 사망 후 시체 공개 정책은 그대로다.
- **연결 경로**: 실제 양의 피해를 받은 플레이어·적, 피해가 있는 밀기·화상·기존 수신 피격 경로에서 `FlashHit`를 쓴다. 무적/회피/0 피해의 기존 차단 조건은 유지한다. 일반·네임드·풍선은 `Effects.SpawnCorpse`, 플레이어는 `Player.Die`에서 공용 `Effects.DeathBurst`를 호출한다. 수신 효과는 기존 호스트 이벤트/체력 스냅샷을 따른다. `EnemyHit`·`EnemyDeath`에 치명타 여부 U8을 추가하고 로비 프로토콜을 5→6으로 올렸다. 같은 적의 동일 색 피격은 기존 80ms 제한을 유지하지만 색 변경은 바로 보낸다. [멀티 규격](multiplayer-sync-design.md#damage-color-sync) 참조. 치명타 판정 자체는 기존 규칙을 유지한다. 원격 화상·밀기 등 기존에 피격 이벤트를 보내지 않는 경로의 동기화 확대는 이번 범위가 아니다.
- **수치의 상태**: 위 색·크기·시간은 아트 조정값이며 피해·치명타 배율·넉백·충돌·사망 판정을 바꾸지 않는다. UI 이펙트 개편은 별도 후속이다. 실제 2PC 및 6인 장시간·저사양 성능은 미검증이다.

코드: [DamageBurstEffect.cs](../game/scripts/Effects/DamageBurstEffect.cs), [damage_burst.gdshader](../game/shaders/damage_burst.gdshader), [CharacterHitFlash.cs](../game/scripts/Characters/CharacterHitFlash.cs), [Effects.cs](../game/scripts/World/Effects.cs), [Player.cs](../game/scripts/Player/Player.cs).

재검사: `DamageEffectsCheck.tscn`(양의 피해/무효 피해, 반복 재사용·소멸, 수신 표시, 일반·네임드·풍선·플레이어 사망, 숨김, 다운), `HitFlashCheck.tscn`(원래 재질 복원), `CorpseReactionCheck.tscn`, `CorpseTierCheck.tscn`, `VisionTierCheck.tscn`, `NetSyncCheck.tscn`, `Integration.tscn`. 실제 렌더: `DamageEffectsCheck` 창 모드 → `output/damage-fx-*.png`. `CaptureDamageEffects.tscn`을 창 모드 `--fixed-fps 60 --disable-vsync`로 실행하면 `output/damage-fx-frames/frame-*.png` 150프레임을 생성한다. 왼쪽 플레이어 일반 피격(흰색), 오른쪽 적 치명타/치명 처치(노란색)를 비교한다. 실제 피해·시체 물리를 포함한 통제된 검사 장면이며, HUD·시나리오·시야 갱신을 고정한다. 로컬 `output/damage-fx-preview.gif`는 해당 프레임을 잘라 2배 느리게 보여 준 미리보기다.

## 9. 플레이어 장전 자세

네 동물 모두 `CharacterReloadPose`의 공통 탄창식/개별 탄 삽입 모션을 쓴다. 오른손은 손잡이를 유지하고, 왼손은 기존 손 메시를 잠시 장전 접촉점으로 이동한다. 별도 팔을 덧붙이지 않는다. 총을 몸 앞쪽으로 옮겨 조끼와 부리를 피하고, 장전 종료·취소 뒤 원래 변환으로 복구한다. 체형 조절은 기존 Model 변환을 함께 따르며 피격 캡슐은 유지한다.

현재 적용 무기·타이밍·중단 규칙·미구현 범위는 [무기 규격 6.1](weapon-spec.md#61-손총-장전-모션-2026-10-20)이 기준이다. 새 종/무기를 추가하면 `ReloadPoseCheck`의 종·무기 목록도 갱신한다. 정면 가까운 쿼터뷰에서 기본형/장비형의 손 왕복을 확인하고 실제 게임 거리에서도 총 기울기와 손 움직임이 읽히는지 확인한다. `CaptureReloadPose`의 M4·R870 네 종 비교 렌더를 재사용한다.

## 10. Codex로 만드는 몬스터 모델 (2026-10-21, A-17)

새 보스·네임드 모델은 지시문 한 장으로 Codex CLI에 맡기고, 게임 연결과 확인은 Claude가 한다. 첫 사례: [난전 보스 지시문](model-briefs/boss.md).

1. `doc/model-briefs/<id>.md`에 외형·색·크기·노드 구조(이 문서 2장)·예산·작업 범위·금지 사항·완료 기준을 적는다.
2. `codex exec -C <저장소> -s workspace-write "<지시문을 읽고 5장 작업>"`: Codex가 `prototypes/character-lab/tools/`에 생성 코드를 쓰고 원본 GLB(`output/roster/<id>-source.glb`)를 만든다. `doc/`·`game/` 수정, Blender, 커밋은 하지 않는다.
3. Claude가 `optimize_game_roster.py -- --<id>-only --no-preview`를 Blender로 돌려 `game/assets/enemies/<id>/<id>.glb`(삼각형 4,500 미만)를 만들고, `content/enemies/<id>.tres`와 `monsters.csv`의 `look`을 연결한 뒤 실제 렌더와 전용 검사로 확인한다.


## 9. 사족 펫 모델 (2026-10-22, A-20)

거북이·강아지·토끼는 이족 `CharacterRig`/플레이어 카탈로그와 분리한 시각용 `PetModel`을 사용한다. 네 발·머리·꼬리 피벗과 공용 `BackSocket`으로 같은 등 무기를 교체한다. 고품질/저품질 무기 없는 기본형 6개·공용 무기 GLB 2개와 설정 품질 전환, 좌표/예산/사용 예시/검사와 모드 연결의 남은 범위는 [펫 모델 규격](pet-model-spec.md)을 따른다.

## 10. 드론·부사수 모델 (2026-10-22, A-21)

드론4종은 별도 비행 모델과 하부 공용 장비를 쓴다. 부사수4종은 기존 goose/duck/raccoon/redpanda CharacterRig를 시각 루트에서0.60배로 줄이고 전용 외형 프리셋을 적용한다. 원본 정의·플레이어 외형은 변경하지 않고 총/손/총구도 함께 축소한다. 충돌 참고 치수만 제공하며 물리 바디/AI는 후속. 품질 설정은 드론의 별도 메시와 부사수 자동LOD에 적용한다. [전체 계약·검사](companion-model-spec.md).


## 로컬 웹 검수 시제품 (2026-10-24, T-25)

[`studio/`](../studio/)에서 현재 게임의 `AppearanceEditor` 선택지/범위와 `CharacterRig`·`AppearanceProfile`을 사용한다. 웹은 편집 UI만 제공하며 실제 Forward+ 셰이더로 900×900 정지 이미지를 만든다. 임시 조합은 저장하지 않으며 기존 인게임 저장 규칙과 모델/색/체형 수치를 변경하지 않는다. 조명은 게임 메뉴의 커스터마이징 기준이며 전투 맵별 조명은 별도다. [실행·데이터 계약·검증·제약](studio-prototype.md).

## 플레이어 랫서판다 조형/파지 후속 (2026-10-26)

`redpanda.tres`는 `redpanda-clean.glb`를 읽는다. 원본 `redpanda-fitted.glb`는 재현 입력으로 보존하며 몸통 정점·마스크 좌표·기존 노드/피벗/색 채널을 유지한다. Blender 생성기는 `prototypes/grounded-art/tools/build_redpanda_clean.py`, 독립 재가져오기 검사는 `validate_redpanda_clean.py`다.

새 `redpanda_<left|right>Paw`/`Paw_mesh`/`Wrist`/`Grip`은 기존 Arm 아래에 있고 `Arm_mesh`는 전완이다. `CharacterWeaponGrip`은 사자·순록의 `CharacterLionPaw`를 재사용한다. 새 Arm 자식 `redpanda_<side>Shoulder`는 실제 어깨를 명시한다. 커스터마이징 피벗을 어깨로 가정하지 않으며 해당 노드가 없을 때만 이전 피벗을 사용한다. 손은 균일 크기, 전완만 어깨에서 손목까지 길이 변화. 현재 색을 적용한 원본 재질과 메시를 공유하고 장착 시 원본 팔은 숨긴다. [좌표·커스텀 계약·검증·제약](redpanda-clean-forms.md).

## 플레이어 라쿤 조형/파지 후속 (2026-10-26)

`raccoon.tres`는 새 `raccoon-clean.glb`를 읽고 fitted 원본은 보존한다. 랫서판다와 같은 Paw/Paw_mesh/Wrist/Grip 및 별도 Shoulder 계약을 기존 Arm 아래 사용하며 런타임 고정 손 대상에 raccoon을 추가했다. 원본 피벗/몸 마스크 좌표/재질 채널/커스터마이징 키는 바꾸지 않는다. 라쿤 팔·손은 보조색이 아닌 기존 무늬색을 따른다. `PlayerMammalCleanCheck`는 두 종이 공유하고 기존 RedpandaCleanCheck와 새 RaccoonCleanCheck가 각각 종을 지정한다. [생성기·좌표·검증](raccoon-clean-forms.md).

## 플레이어 오리 조형 후속 (2026-10-26)

`duck.tres`는 새 `duck-clean.glb`를 읽는다. 독립 Blender 생성기 `build_duck_clean.py`가 읽기 전용 `duck-fitted.glb`를 입력으로 부리·눈·눈썹·날개·발 메시만 수정한다. 몸통 정점과 마스크 좌표, 전체 피벗/부모, 재질 채널과 장비는 동일하다. 날개는 기존 `CharacterGripSurface`의 변형 메시를 계속 사용하며 새 손/전완 런타임 경로를 추가하지 않는다. 발은 두 채널의 닫힌 단일 연결 표면으로 재구성한다. [검사·제약](duck-clean-forms.md).

### 조류 얼굴 표시 진폭 (2026-10-26)

거위/오리 clean 모델의 얼굴 Head는 연속 torso와 분리되어 있다. 기존 최대140mm 얼굴 이동이 눈을 몸 안에 넣으므로 두 종 리소스의 `HeadSpringScale=0.05`로 표시만 제한한다. 피벗/몸 마스크 좌표·커스텀 저장과 다른 종 스프링은 그대로다. `BirdFaceVisibilityCheck`와 같은 실제 Stage2 캡처에서 가속·반전·정지·사격 중 눈 노출을 확인했다. [통합 검수](four-species-art.md).

### 2026-10-26 공통 무기 자세 계약

분리된 GLB라도 같은 `CharacterWeaponPose`를 사용한다. 정지 Arm/Shoulder와 몸 배율은 캐릭터에서, `StockContact`·`GripRight`·`GripLeft`·`Muzzle`은 무기에서 읽는다. `StockContact`는 총의 후면 실제 표면이며 일반 GLB는 작성 필수다. 어깨 견착/권총/전용 자세 분기와 기존 형태별 팔 표면 연결을 분리한다. [현재 구현·제작 수치·예외·검수](shared-weapon-poses.md).


### 2026-10-26 포유류 공통 짧은 팔 계약

포유류6종의 기존 Shoulder/Wrist/Grip 및 Blender 공유 구간을 사용한다. 표준 소총·권총에서는 원본 Shoulder–Wrist 거리1.10배를 위팔/아래팔 합으로 유지하고 어깨를 같은 높이/몸통 반경 위에서 앞으로 이동한다. 모델40도 자세와 무기 역회전, 장착 총Z배율0.75~1 및 왼손 받침 위치로 접촉을 맞춘다. 원본 손/GLB/커스텀 저장은 유지하며 조류·전용 재장전/횡단 도달 처리와 구분한다. `PosedShoulder`는 실제 팔 구간 시작점이며 원래 기준점을 덮어쓰지 않는다. [수치·실제 검사·한계](shared-weapon-poses.md#2026-10-26-짧은-팔-비율-유지-후속).
