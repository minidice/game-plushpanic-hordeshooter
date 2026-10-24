# 오브젝트 품질 비교와 상호작용 데이터 방향

2026-10-24 · T-25/C-29 후속. 품질 비교, 이동/탄환 차단, 낮은 엄폐물, 파괴 조건·내구도와 자체 폭발을 실제 게임 데이터로 연결했다. 신규 균열 콘크리트 벽은 폭발로 중앙 통로가 열리며 게임과 Studio가 같은 모델 생성기를 사용한다. 수치·배치는 검수용 임시값이다.

## 확인된 현재 구조

- 펫 3종과 드론 4종은 `{id}.glb` / `{id}_low.glb`가 따로 있다. 실제 `PetModel.Create` / `DroneModel.Create`에 명시적 품질을 전달한다. 전역 `GameSettings.PetQuality`나 저장 파일을 변경하지 않는다.
- 부사수 4개 프리셋은 `sidekick_presets.json`과 `SidekickModel.Create`를 읽는다. 별도 저품질 GLB가 아니라 `LodBias` 4 / 0.5 설정을 비교한다. 가까운 시점에서는 그림 차이가 없을 수 있다.
- 환경 모델의 `PropLibrary` manifest는 파일·크기·추천 충돌을 제공한다. `StageMap.Prop`의 box/cylinder 기본 배치는 공통 차단 데이터를 읽는다. `collision.kind = none/compound`와 별도 맵 충돌은 이 계약 밖이며, 이동 또는 탄환 통과로 단정하지 않는다.
- `StageTwoMap`의 철망 울타리는 실제 모델 충돌을 활성화했고 겹친 앞쪽 경계 벽도 같은 철망 정책을 적용한다. 철망 모델만 바꾸고 보이지 않는 벽이 총알을 막는 상태를 남기지 않는다. 뒤쪽 함석 울타리와 일반 경계 벽은 둘 다 차단한다.
- `ExplosiveBarrel`은 `runtime:barrel`의 실제 파괴·폭발 데이터를 읽는다. 플레이어/몬스터 총탄, 폭발은 피해를 누적하고 황소 충돌은 남은 내구도만큼 Impact 공격한다. `gun`은 이 충돌도 허용하고 `explosive`는 폭발만 허용한다. 장식용 manifest 드럼통과는 별개다.

## 구현한 품질 비교

- 오브젝트당 목록 항목 하나. 지원 항목을 선택하면 **고품질 / 저품질을 나란히** 표시한다. 단일 모델에는 비교판을 만들지 않는다.
- 두 실제 게임 모델의 경계 합집합으로 카메라를 한 번 맞추고, 가시성만 전환해 두 장을 캡처한다. 조명·시점·회전·확대·자세가 같다. 회전/확대 입력 한 번이 양쪽에 적용된다.
- 삼각형 수는 실제 가져온 기본 메시 표면에서 계산한다. 자동 LOD 이후 그려진 프리미티브 수, 드로 콜, FPS를 뜻하지 않는다. 부사수의 원본 삼각형 수는 같을 수 있다.
- 연결: `game/tests/StudioQuality.cs`, `StudioLibrary.cs`, `studio/web/library.*`. Debug 전용 `tests/Studio*.cs` 제외 규칙을 유지한다.

## 구현한 차단 데이터 계약

원본은 [`game/content/data/object_interactions.json`](../game/content/data/object_interactions.json), 로더는 [`ObjectInteractions.cs`](../game/scripts/World/ObjectInteractions.cs)다. 웹용 복사 데이터는 없다.

| 키 | 현재 의미 |
|---|---|
| `default_solid_profile` | box/cylinder 기본 배치와 `StageMap.Solid/Wall`이 사용하는 프로필 ID. 현재 `structure` |
| `profiles.<id>.blocks_movement` | 이동 및 흐름장 차단 여부 |
| `profiles.<id>.blocks_projectiles` | 기본 총탄/적 사격 레이 차단 여부 |
| `objects.<ID>` | manifest 모델 또는 명시적 runtime ID의 프로필 재정의. `wire_fence`, `sandbags`, `concrete_barrier`, `runtime:barrel`, `runtime:breach_wall` |

- `structure`: 이동 true, 탄환 true. `wire_fence`: 이동 true, 탄환 false. 두 false는 비차단 정책이다. 이동 false/탄환 true 조합은 현재 지원하지 않으며 로더가 거부한다. 필수 bool 누락, 알 수 없는 프로필 참조와 중복 이름도 오류다.
- 두 차단은 기존 `World` 레이어, 이동 전용은 `MovementOnly`(32), 둘 다 통과는 레이어 0으로 연결한다. 플레이어/구르기/적/황소/이동 소품/시체/투척물 몸체 및 이동 경로 복구는 `MovementWorld`를 사용한다. 총탄과 적 사격은 기존 총탄 마스크를 유지해 철망을 통과한다. 같은 높이 근접 공격은 양쪽 모두 철망을 통과하지 않는다. 철망은 시야도 가리지 않는다.
- **여기서 탄환은 총탄/사격 레이다.** 수류탄 같은 투척물 몸체는 철망에 충돌한다. 관통탄의 별도 조건, 폭발 피해 전파, 파괴 조건은 이번 데이터 계약에 포함하지 않는다.
- `solid:false`는 장식 배치로 기존처럼 충돌/흐름장을 만들지 않는다. `none/compound`, 문/기믹, 대표 코드 생성 외형, 동료 모델의 모든 충돌을 자동 데이터화한 것은 아니다. 맵 소유의 충돌은 기존 코드를 유지한다.
- Studio는 같은 `ObjectInteractions.Current.ForProp`를 호출해 **이동 차단 / 탄환 차단 / 프로필 / 출처 / 적용 범위**와 원본 JSON을 표시한다. API의 `interaction`에 `source`, `profile`, 두 bool을 제공하고 미연결은 null로 유지한다. 표의 값은 기본 배치 정책이며 개별 맵 인스턴스의 최종 충돌 검사 결과가 아니다.
- JSON 변경 후 게임은 재실행, Studio는 **로컬 데이터 다시 읽기**를 사용한다. 현재 프로세스에서 자동 감시하지 않으며 웹에서 값을 저장하지 않는다. 공통 데이터/로더는 실제 게임 구성 요소이고 Studio UI/렌더 bridge만 배포 제외다.

## 엄폐 · 파괴 데이터 (2026-10-24 구현, C-29)

같은 `object_interactions.json`에 더했다. 값이 없는 기존 프로필은 이전과 같다(엄폐 아님 · 파괴 정의 없음).

| 키 | 의미 | 지금 값 |
|---|---|---|
| `cover_rules.shoot_over_gap_bodies` | 쏘는 사람 몸과 엄폐물 사이 틈이 **자기 몸 폭의 이 배수 미만**이면 그 엄폐물을 넘겨 쏜다. 그 이상이면 엄폐물이 총알을 막는다 | `1.0` (캐릭터 하나 들어갈 틈이면 막힘) |
| `profiles.<id>.cover` | 낮은 엄폐물: 걷기 · 던진 물건은 막고, 시야 · 근접 · 잡기는 넘어감, 총알은 위 규칙. `blocks_movement`와 `blocks_projectiles`가 둘 다 true여야 함(아니면 로더 오류) | `sandbag_cover`, `barrier_cover` |
| `profiles.<id>.destruction.breakable_by` | 깰 수 있는 강도: `gun`(총탄 · 몬스터 총탄 · 폭발 모두) / `explosive`(폭발 계열만 — 총은 막기만). 다른 값은 오류 | 모래자루 `gun` |
| `profiles.<id>.destruction.max_health` | 내구도(누적 피해). 0 이하 오류. `destruction`이 없으면 **안 깨짐** | 모래자루 `50` (캐릭터 체력 100의 절반) |
| `objects` | `sandbags: sandbag_cover`, `concrete_barrier: barrier_cover`(안 깨지는 엄폐물) | |

- **충돌 층**: 엄폐물의 실제(낮은) 모양은 `Cover`(64) — 이동 마스크(`MovementWorld`)에 포함, 시야 · 근접(`MeleeMask`)에서 제외. 총알은 낮은 모양 위로 지나가므로 같은 자리에 키 큰(2.4 m) 보이지 않는 **사격막** `CoverShot`(128)을 두고, 총탄 마스크(`BulletMask`)와 몬스터 사격(`EnemyShotMask`)만 이것을 본다.
- **규칙 위치**: `CoverRule`(`game/scripts/World/ObjectBody.cs`) 하나를 플레이어 · 분대원 총(`WeaponController.Hitscan`), 로켓, 몬스터 총탄(`EnemyProjectile`), 몬스터 · 분대원의 사격 가능 판단(`RangedEnemy.ShotClear`, `SquadBrain.InSight`)이 함께 쓴다. 곡사(박격포 · 투척)는 포물선이라 엄폐물을 넘어간다.
- **파괴**: `ObjectBody`가 내구도를 들고, 총탄은 맞은 피해만큼(거리 감소 포함), 폭발은 몬스터와 같은 식으로 깎는다. 0이 되면 먼지 · 소리와 함께 사라지고 흐름장 칸이 열린다. 판정은 방장만, 다른 PC는 드럼통처럼 번호(만든 순서)로 같은 물체를 지운다(이벤트 + 월드 스냅샷 목록).
- **Studio**: 오브젝트 속성에 「엄폐 (낮은 엄폐물)」 · 「파괴」 · 「내구도」 · 「폭발 (부서질 때)」 줄을 같은 로더(`ObjectInteractions.Current`)에서 표시. API `interaction`에 `cover` · `breakable_by` · `max_health`를 더함.
- **검사**: `game/tests/CoverCheck.tscn` 27개 — 데이터 검증, 붙어서 넘겨 쏘기 · 몸 폭 0.9/1.1배 경계 · 양쪽 붙음, 시야 · 걷기 · 흐름장, 실제 M4 사격, 근접 넘기, 몬스터 총탄 막힘/넘김, 총 · 폭발 파괴와 흐름장 해제, 방벽 무적.

## 파괴·폭발 구현 계약 (2026-10-24)

사용자 요구는 외형과 이동 차단·탄환 차단·파괴·폭발 조건을 하나의 오브젝트 정의에서 읽고 웹에도 표시하는 것이다. `blocks_everything` 같은 단일 값으로 묶지 않는다.

| 구분 | 필드 | 의미 |
|---|---|---|
| 이동 (구현) | `blocks_movement` | 캐릭터 이동을 막는지 |
| 탄환 (구현) | `blocks_projectiles` | 기본 탄환 충돌을 막는지. 관통탄/폭발 피해 전파 규칙은 별도 |
| 파괴 (구현, 단순화) | `destruction` 있음/없음 | 부서지는 오브젝트인지 — 위 「엄폐 · 파괴 데이터」 |
| 공격 종류 · 등급 | `destruction.breakable_by` | `gun`은 총·폭발·황소 충돌, `explosive`는 폭발만 허용. 강도 타입과 누적 내구도는 별개 |
| 내구도 (구현) | `destruction.max_health` | 허용된 공격으로 누적 감소하는 내구도 |
| 폭발 | `explosion.on_destroy` | true이면 파괴 후 자체 폭발. false/필드 없음은 자체 폭발 없음. true에는 destruction 필수 |
| 폭발 강도 | `explosion.damage`, `radius_m`, `impulse`, `delay_s` | 기존 Blast의 피해·반경·밀침 계수·지연. 반경은 양수, 나머지는 0 이상 유한값. 파괴에 필요한 공격 종류와 별개 |

철조망의 이동 차단/탄환 통과, 일반 구조물의 양쪽 차단은 위 범위에서 구현했다. 나무 상자는 총탄으로 파괴되지만 비폭발, 튼튼한 폭발물은 폭발 계열 공격으로만 파괴되고 자체 폭발한다는 표현은 **후속 예시이며 적용된 값이 아니다.** 탄종/관통/폭발 전파와 구조물 종류별 수치는 후속 확정 대상이다.

프로필 자체가 연결되지 않은 자산은 **미연결**이다. 연결된 프로필에서 destruction이 없으면 파괴 불가, explosion이 없으면 자체 폭발 없음이다. 둘을 혼동하지 않는다. 고/저품질 외형은 같은 상호작용 정의를 공유한다.

### 현재 적용값과 동작

| 오브젝트 | 파괴 | 내구도 | 자체 폭발 |
|---|---|---|---|
| 모래자루 | gun | 50 (C-29 사용자 결정 유지) | 없음 |
| 일반 구조물·콘크리트 방벽 | 불가 | — | 없음 |
| `runtime:breach_wall` 균열 콘크리트 벽 | explosive | 60 (임시) | 없음 |
| `runtime:barrel` 실제 드럼통 | gun | 10 (임시) | 반경4.6m / 피해계수125 / 충격계수140 / 지연0.12초 |

- 양수 피해는 기존 Blast 식 `(damage × 거리감쇠 + 15) × 공격자 배율`로 적/파괴물에 적용한다. 배럴 연쇄는 반경1.4배 안에서 같은 최소 피해를 사용한다. 플레이어 피해는 `damage × 0.2 × 거리감쇠`. damage=0은 최소 피해도 없고 연쇄도 점화하지 않는다. impulse=0은 밀침/날리기 없음. 양수 impulse는 기존 적 질량/거리·시체 반응 규칙을 따른다. 이 값은 최종 HP 피해나 모든 몸체의 뉴턴 단위가 아니다.
- 연쇄 점화 지연도 데이터의 delay_s를 사용한다. 이전의 거리별 지연 `0.18 + 거리 × 0.03`은 제거했다. 실제 피격을 누적해 0이 될 때 한 번 점화하며 최초 공격자를 연쇄까지 전달한다.
- 일반 ObjectBody도 explosion이 있으면 제거 후 별도 `ObjectDetonation`이 지연 폭발한다. 방장만 피해·점화·흐름장 해제를 결정하고 클라이언트는 효과와 로컬 시체 반응을 재생한다. 늦은 합류는 과거 폭발을 다시 실행하지 않고 파괴 후 상태만 복원한다.
- `NetIds.DataHash`에 상호작용과 시험 배치 JSON을 포함해 서로 다른 데이터의 접속을 거부한다. 실제 2PC 환경 검수는 별도다.

### 벽 모델과 시험 배치

- [`BreakableWallArt.cs`](../game/scripts/World/BreakableWallArt.cs): 실제 코드 생성 메시. 가로4.8m·높이2.6m·두께0.45m, 중앙 개방폭3.2m. 같은 콘크리트 패널 구조에 양면 균열·깨진 단면·철근을 더했다. 0.8초 동안 조각과 먼지가 움직이고 양쪽 기둥·작은 잔해가 남는다. 조각은 장식이며 피해/동적 충돌은 없다. 중앙 통로에는 큰 잔해나 숨은 충돌이 남지 않는다.
- [`destruction_placements.json`](../game/content/data/stages/destruction_placements.json): 기존 Stage 2 두 곳, 확장 시안 `stage2_draft` 야적장 세 곳. 각 벽 옆 실제 폭발 드럼통, 시안에는 비교용 일반 패널도 배치. 정규 이동장치·등장·진행 경로의 측면 시험 배치다. [`StageMapDestruction.cs`](../game/scripts/World/StageMapDestruction.cs)가 실제 충돌·흐름장·배치를 만든다.
- Studio 오브젝트의 **균열 콘크리트 벽** 한 항목에서 파괴 전/붕괴 중/파괴 후를 선택한다. 동일 카메라 범위로 실제 게임 메시를 정지 렌더하며, 게임에서는 시간에 따라 재생한다. 속성·원본 JSON·시험 배치 출처와 드럼통의 실제 폭발 수치를 표시한다.

## 차단 연결 검증 (2026-10-24)

- `ObjectInteractionCheck.tscn` 30개: JSON 변경에 따른 정책 변화/잘못된 참조 거부, 실제 이동·적 몸체·구르기·흐름장, M4 피해의 통과/차단, 근접 차단과 같은 쪽 정상 피해, 장식 예외, Stage 2 철망에 겹친 두 경계 벽의 사격 통과와 일반 경계 차단. `studio/output/object-interaction.log`. 최종 전용 실행 종료 시 ObjectDB 2개 경고(I-17), 기능 검사와 구분한다.
- `studio/tests/interactions.mjs`: 현재 manifest 대상 데이터/웹 표시/출처/예외 검사 131개 및 실제 두 모델 렌더·픽셀 검사. `studio/output/interactions-web.log`. `library.mjs` 199개·8종 렌더, `quality.mjs`33개·11쌍/22렌더 회귀 통과. 품질 비교의 사용자/원본 파일 해시 불변. 캐릭터 전용 재검사는 이번 차단 작업에서 미실행이다.
- `WorkshopArtCheck` 420개 통과: 기존 등장 지점의 경로 연결·작업장 소품·중앙 통로. 종료 경고 없음. `studio/output/interactions-workshop.log`.
- `Start-Game.ps1 -Test` 첫 실행은 기존 I-65 산탄 무작위 검사에서 실패(74개 통과), 재실행 및 근접 보정 후 최종 `Integration`149개 통과·각 ObjectDB4개 경고. `StageTransitionCheck`는 기존 하드코딩65와 현재 로컬 자산67 불일치로 초기에 실패. 다른 작업의 자산 수 변경을 이 기능에서 임의 수정하지 않았고 해당 장면 전체 통과를 주장하지 않는다. `studio/output/interactions-integration*.log`, `interactions-stage-transition.log`.
- Debug/ExportRelease 빌드 오류·경고0. ExportRelease Compile 목록에서 Studio 제외·ObjectInteractions 포함 확인. 실제 브라우저에서 철망(차단/통과)과 함석(차단/차단)의 모델·속성 표시 확인: `studio/output/studio-interaction-wire.png`, `studio-interaction-structure.png`. 문서 상대 링크14개 통과. `git diff --check`는 내용 오류 없이 줄바꿈 안내만 출력했다.
- 실제 2PC 동기화, 모든 맵 배치, 전체 게임 패키지 export는 미검증이다.

2026-10-24 main 통합 후속: 앞선 자산 수 불일치는 `StageTransitionCheck`의 기대를 현재67개로 갱신하여 전체200검사 통과했다. 단독 Integration149·전용30·웹131 및 Studio 전체도감/품질/캐릭터 회귀를 다시 통과했다. [통합 검증·남은 제약](studio-prototype.md#main-integration-20261024).

## 파괴 벽·폭발 후속 검증 (2026-10-24)

`git pull --ff-only`로 main을 `78f8856 → 6b284f2` 최신화한 뒤 구현했다. 출력은 버전 관리하지 않는 로컬 `game/output/`, `studio/output/`이며 아래 장면/스크립트로 재생성한다.

| 검사 | 결과·범위 |
|---|---|
| `DestructionCheck.tscn` | 42 통과. 실제 M4→드럼통→벽, 총탄 면역, 누적 내구도, 연쇄/공격자, 지연/중복 방지, 실제 걸어서 통과, 일반벽 유지, 0피해/0충격, 클라이언트 피해 거부·시체 반응 |
| `DestructionPlacementCheck.tscn` | 185 통과. 실제 두 맵/다섯 배치의 바닥·드럼통 거리·충돌/흐름장·기존 등장/진행/이동장치 경로, 파괴 후 통로·좌우 충돌·일반 패널 유지 |
| `DestructionNetCheck.tscn` | 29 통과. 실제 localhost ENet·독립 물리월드, 늦은 합류 잔해/통로 복원·실시간 붕괴·반복 상태의 중복 폭발/잔해/흐름장 방지. 최종 실행 종료 경고 없음; 최초 실행은 ObjectDB6개 종료 경고 |
| `DestructionCompatibilityCheck.tscn` | 14 통과. 상호작용/폭발/배치 변경 시 데이터 해시 변경, 줄바꿈 동일성, 원본 불변 |
| `BreakableWallArtCheck.tscn` | Forward+ 실제 렌더7 통과. 정지 상태 재현·0.8초 종료·장식 충돌 없음·8072삼각형·통로 잔해6.5cm 이하 |
| `DestructionCapture.tscn` | 실제 확장 시안 yard_west의 드럼통 피해→붕괴→통로를 같은 촬영 카메라로 3장 확인. `game/output/destruction-map-contact-sheet.png`. 오류/종료 경고 없음 |
| 기존 전용 회귀 | Cover27, ObjectInteraction30, StageTransition200, WorkshopArt438, CorpseReaction29 통과. ObjectInteraction ObjectDB2개·StageTransition6개 종료 경고, 나머지 해당 최종 로그 경고 없음 |
| Studio | destruction24/3렌더, interactions192/2렌더, library200/8렌더, quality33/22렌더 통과. PNG 크기/비어 있지 않은 픽셀·서로 다른 상태·동일 카메라 범위·원본 불변. 실제 브라우저 상태 선택 및 벽/드럼통 속성 확인 |
| `Integration.tscn` | 최종149 통과, ObjectDB2개 종료 경고(I-17). 앞선 실행도149 통과·ObjectDB4개. 전용 장면은 별도 실행 |
| 빌드·내보내기 경계 | Debug/ExportRelease 오류·경고0. ExportRelease Compile에서 Studio0개, 신규 런타임 파괴 소스3개 포함. 전체 게임 패키지 export는 미실행 |

검사 보완 이력: 최초 데이터 검사는 폭발 프로필 미연결로 실패한 뒤 연결 후 통과했다. 0피해/충격 회귀의 첫 실행은 검사에서 몬스터의 실제 등장 높이 +0.05m를 제외해 예상 거리/밀침을 틀리게 계산한 실패였고, 실제3D 위치로 계산하도록 검사만 수정해42개 통과했다. 제품의 양수 폭발 수식을 이 실패에 맞춰 바꾸지 않았다.

리뷰에서 확인한 회귀 예방 조치: 클라이언트 무피해 분기에서도 로컬 시체 충격은 실행, 0피해/0충격이 기존 최소15피해/최소밀침을 발생시키지 않도록 분리, 양쪽 PC의 파괴 규칙/배치 해시 일치 검사. 기존 시체 정책 T-15와 I-120 범위를 유지한다.

남은 제약: 실제 서로 다른 두 PC/Steam 연결, 모든 기존 배치의 데이터화, 완성 게임 패키지 내보내기는 미검증. 참고 이미지에 맞춘 벽·드럼통 시험 배치이며 최종 아트/밸런스 확정이 아니다. 파편은 로컬 장식이고 피해·통로 차단 없음. 웹은 정지 상태 선택이며 실시간 애니메이션은 게임에서 확인한다. 좀비의 낮은 엄폐물 넘어오기는 I-120 후속이다.
