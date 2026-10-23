# 오브젝트 품질 비교와 상호작용 데이터 방향

2026-10-24 · T-25 후속. **품질 비교와 이동/탄환 차단 공통 데이터 연결은 구현**. 같은 날 **낮은 엄폐물(`cover`)과 파괴(`destruction`: 깰 수 있는 강도 타입 · 내구도)도 구현**(C-29, 아래 「엄폐 · 파괴 데이터」). **폭발 조건만 후속 계획**이다.

## 확인된 현재 구조

- 펫 3종과 드론 4종은 `{id}.glb` / `{id}_low.glb`가 따로 있다. 실제 `PetModel.Create` / `DroneModel.Create`에 명시적 품질을 전달한다. 전역 `GameSettings.PetQuality`나 저장 파일을 변경하지 않는다.
- 부사수 4개 프리셋은 `sidekick_presets.json`과 `SidekickModel.Create`를 읽는다. 별도 저품질 GLB가 아니라 `LodBias` 4 / 0.5 설정을 비교한다. 가까운 시점에서는 그림 차이가 없을 수 있다.
- 환경 모델의 `PropLibrary` manifest는 파일·크기·추천 충돌을 제공한다. `StageMap.Prop`의 box/cylinder 기본 배치는 공통 차단 데이터를 읽는다. `collision.kind = none/compound`와 별도 맵 충돌은 이 계약 밖이며, 이동 또는 탄환 통과로 단정하지 않는다.
- `StageTwoMap`의 철망 울타리는 실제 모델 충돌을 활성화했고 겹친 앞쪽 경계 벽도 같은 철망 정책을 적용한다. 철망 모델만 바꾸고 보이지 않는 벽이 총알을 막는 상태를 남기지 않는다. 뒤쪽 함석 울타리와 일반 경계 벽은 둘 다 차단한다.
- `ExplosiveBarrel`은 현재 총 피격(`WeaponController`), 주변 폭발(`Blast`), 황소 충돌(`BullEnemy`)로 점화한다. 내구도·공격 등급을 읽는 공통 파괴 데이터는 없다. 폭발 반경은 실제 상수에서 읽지만 총/폭탄 내구도 값을 임의로 만들지 않는다.

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
| `objects.<PropLibrary ID>` | 해당 모델의 프로필 재정의. 현재 `wire_fence: wire_fence` |

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

## 파괴·폭발 후속 계약 초안

사용자 요구는 외형과 이동 차단·탄환 차단·파괴·폭발 조건을 하나의 오브젝트 정의에서 읽고 웹에도 표시하는 것이다. `blocks_everything` 같은 단일 값으로 묶지 않는다.

| 구분 | 제안 필드 | 의미 |
|---|---|---|
| 이동 (구현) | `blocks_movement` | 캐릭터 이동을 막는지 |
| 탄환 (구현) | `blocks_projectiles` | 기본 탄환 충돌을 막는지. 관통탄/폭발 피해 전파 규칙은 별도 |
| 파괴 (구현, 단순화) | `destruction` 있음/없음 | 부서지는 오브젝트인지 — 위 「엄폐 · 파괴 데이터」 |
| 공격 종류 · 등급 (구현, 단순화) | `destruction.breakable_by` | 초안의 종류 목록 · 최소 등급 두 칸 대신 `gun` / `explosive` 한 칸으로 시작(사용자: "총으로 터지나, 폭탄류급으로 터지나"). 충돌(황소 등) 파괴는 아직 없음 |
| 내구도 (구현) | `destruction.max_health` | 허용된 공격으로 누적 감소하는 내구도 |
| 폭발 | `explosion.on_destroy` | 부서질 때 자체 폭발하는지. 단순 파괴와 별개 |
| 폭발 강도 | `explosion.damage`, `radius_m`, `impulse`, `delay_s` | 결과 폭발의 피해·반경·밀침·지연. 파괴에 필요한 공격 강도와 별개 |

철조망의 이동 차단/탄환 통과, 일반 구조물의 양쪽 차단은 위 범위에서 구현했다. 나무 상자는 총탄으로 파괴되지만 비폭발, 튼튼한 폭발물은 폭발 계열 공격으로만 파괴되고 자체 폭발한다는 표현은 **후속 예시이며 적용된 값이 아니다.** 탄종/관통/폭발 전파와 구조물 종류별 수치는 후속 확정 대상이다.

값이 누락된 기존 자산은 **미정/미연결**로 표시한다. `false`, 내구도 0, 무적 등으로 해석하지 않는다. 배치별 예외가 필요하면 오브젝트 기본값과 배치 재정의의 출처를 웹에 함께 표시한다. 고/저품질 외형은 동일한 상호작용 정의를 공유해야 한다.

## 후속 구현 순서와 확인 조건

1. 이동/탄환 두 필드와 로더·게임/웹 연결 완료. 나머지 파괴/폭발 필드는 후속 확정.
2. 파괴/폭발 및 멀티 호스트 판정에 후속 정의를 연결. 전용 검사와 Integration 실행.
3. 총으로 파괴되는 상자/폭발 계열만 허용하는 물체의 테스트와 웹 표시 추가.
4. 내구도 감소, 파괴 후 통로, 연쇄 폭발, 네트워크 중복 폭발 방지, 배치별 재정의와 고/저 동일 충돌을 검증.

드럼통 파괴/폭발의 공통 데이터 연결은 아직 하지 않았다. 웹에서 미연결 상태를 유지한다.

## 차단 연결 검증 (2026-10-24)

- `ObjectInteractionCheck.tscn` 30개: JSON 변경에 따른 정책 변화/잘못된 참조 거부, 실제 이동·적 몸체·구르기·흐름장, M4 피해의 통과/차단, 근접 차단과 같은 쪽 정상 피해, 장식 예외, Stage 2 철망에 겹친 두 경계 벽의 사격 통과와 일반 경계 차단. `studio/output/object-interaction.log`. 최종 전용 실행 종료 시 ObjectDB 2개 경고(I-17), 기능 검사와 구분한다.
- `studio/tests/interactions.mjs`: 현재 manifest 대상 데이터/웹 표시/출처/예외 검사 131개 및 실제 두 모델 렌더·픽셀 검사. `studio/output/interactions-web.log`. `library.mjs` 199개·8종 렌더, `quality.mjs`33개·11쌍/22렌더 회귀 통과. 품질 비교의 사용자/원본 파일 해시 불변. 캐릭터 전용 재검사는 이번 차단 작업에서 미실행이다.
- `WorkshopArtCheck` 420개 통과: 기존 등장 지점의 경로 연결·작업장 소품·중앙 통로. 종료 경고 없음. `studio/output/interactions-workshop.log`.
- `Start-Game.ps1 -Test` 첫 실행은 기존 I-65 산탄 무작위 검사에서 실패(74개 통과), 재실행 및 근접 보정 후 최종 `Integration`149개 통과·각 ObjectDB4개 경고. `StageTransitionCheck`는 기존 하드코딩65와 현재 로컬 자산67 불일치로 초기에 실패. 다른 작업의 자산 수 변경을 이 기능에서 임의 수정하지 않았고 해당 장면 전체 통과를 주장하지 않는다. `studio/output/interactions-integration*.log`, `interactions-stage-transition.log`.
- Debug/ExportRelease 빌드 오류·경고0. ExportRelease Compile 목록에서 Studio 제외·ObjectInteractions 포함 확인. 실제 브라우저에서 철망(차단/통과)과 함석(차단/차단)의 모델·속성 표시 확인: `studio/output/studio-interaction-wire.png`, `studio-interaction-structure.png`. 문서 상대 링크14개 통과. `git diff --check`는 내용 오류 없이 줄바꿈 안내만 출력했다.
- 실제 2PC 동기화, 모든 맵 배치, 전체 게임 패키지 export는 미검증이다.

2026-10-24 main 통합 후속: 앞선 자산 수 불일치는 `StageTransitionCheck`의 기대를 현재67개로 갱신하여 전체200검사 통과했다. 단독 Integration149·전용30·웹131 및 Studio 전체도감/품질/캐릭터 회귀를 다시 통과했다. [통합 검증·남은 제약](studio-prototype.md#main-integration-20261024).
