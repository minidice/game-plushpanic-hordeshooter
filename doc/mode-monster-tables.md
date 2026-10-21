# 모드별 몬스터 테이블 · 보스 페이즈 규격

> **상태**: 2026-10-21 구현 (개편 2단계, decisions.md T-21·M-24). 수치는 모두 첫 제안값이며 플레이로 조정한다.
> **대상**: 거점(코드 `Survival`)·난전(코드 `Survivor`) 모드와 보스. 시나리오는 지금처럼 `stages/stage1_encounters.json`을 쓴다.
> **코드**: `game/scripts/World/ModeSpawnTable.cs`, `game/scripts/Enemies/BossDefinition.cs`, `game/scripts/Enemies/BossEnemy.cs`, `game/scripts/World/SurvivalRules.cs`·`SurvivorRules.cs`
> **검사**: `game/tests/ModeCheck.tscn` (창 모드면 `output/mode-*.png`)

## 1. 몬스터 등급 (T-21)

`monsters.csv`의 `tier`는 `common` · `named` · `boss` 세 가지다.

| 등급 | 시체 | 지금 해당 |
|---|---|---|
| `common` | 일반 시체 정책(100개 목표, 밀리고 휴면, T-14·T-15) | 감염체, 인간형·기어오는 감염체, 야생 라쿤, 무장 랫서판다 |
| `named` | 고정 시체, 별도 수명(T-16) | 풍선·황소·낚아채는·덮치는·토사물 감염체, 랫서판다 정예병 |
| `boss` | **`named`와 같음** (`MonsterDefinition.KeepsCorpse`) | 거대 두목 황소(`boss`) |

## 2. 모드별 몬스터 테이블 (M-24)

파일: `game/content/data/modes/<mode>_spawns.csv` — `survival_spawns.csv`(거점), `survivor_spawns.csv`(난전). 새 CSV는 `.import`를 `importer="keep"`으로 둔다(T-7과 같은 이유).

| 열 | 뜻 |
|---|---|
| `monster` | `monsters.csv`의 id, 또는 `@common`(판의 테마 일반 풀을 돌아가며) · `@special`(테마 특수 풀) |
| `role` | `common` = 무리를 채울 때 가중치로 뽑음 / `named` = 그 시점에 `count`마리가 한 번 등장 |
| `theme` | `any` 또는 `zombie` · `animals` · `armed` (메뉴의 조우 테마와 같을 때만) |
| `weight` | common 행의 가중치 (같은 시점·테마의 행끼리 비율) |
| `count` | named 행의 마리 수 |
| `from` · `until` | 이 행이 쓰이는 구간. **거점은 웨이브 번호(1부터), 난전은 경기 시간(초)**. `until`은 미포함, 빈 칸 = 끝까지. named는 `from`에 도달할 때 한 번 |
| `hp_scale` · `damage_scale` | 생길 때의 체력·공격력 배율 |

**지금 값**
- 거점: 웨이브 1은 일반만, 웨이브 2는 일반 80 : 무장 20, 웨이브 3부터 70 : 30. 웨이브 2 시작에 테마 특수 1, 웨이브 3 시작에 랫서판다 정예병 1.
- 난전: 일반(체력 60%)이 기본, 60초부터 테마별로 빠르거나 무장한 일반이 섞임. 45초에 테마 특수 1, 100초에 2.
- 웨이브 길이·무리 크기·간격은 그대로 `mode_rules.json`이다. 거점의 무장 비율·정예병(예전 `armed_pct`·`veterans`)과 난전의 `common_hp_scale`은 이 표로 옮겼다.

**정비 시간**
- 거점: 웨이브 사이의 휴식(`break_s` 10초)이 그대로 정비 시간.
- 난전: 표의 named가 모두 쓰러지면 `rest_after_named_s`(12초) 동안 무리가 오지 않는다. 화면 목표: 강한 적이 있으면 "강한 적을 쓰러뜨리세요", 정비 중에는 남은 초.

## 3. 보스 페이즈와 스킬 (M-24)

파일: `game/content/data/bosses/<monster id>.json`. 파일 이름이 `monsters.csv`의 id다(`boss.json` = 거대 두목 황소).

- `scale`: 외형과 충돌 크기 배율 (예전 `mode_rules.json` survivor.boss.scale). 외형 자체는 `monsters.csv`의 `look`(`boss` → `content/enemies/boss.tres` → `assets/enemies/boss/boss.glb`, 2026-10-21 전용 모델).
- `phases`: 체력이 `from_hp_pct` % **이하**가 되면 그 페이즈. 첫 페이즈는 100, 내림차순. 각 페이즈: `skills`(쓸 스킬 이름), `speed`(이동 배율), `cooldown_scale`(스킬 대기 배율), `notice`(들어갈 때 알림 언어 키, 생략 가능).
- `skills`: 이름 → `kind`와 수치.

| kind | 하는 일 | 항목 |
|---|---|---|
| `charge` | 황소 돌진. 이 스킬이 있는 페이즈에서만 돌진. 수치는 `monster_traits/boss_charge.json` | 없음 |
| `slam` | 대상이 `range_m` 안이면 `windup_s` 동안 빨간 원이 차오른 뒤 반경 `radius_m` 안 모두 `damage` + 날려 보냄(자기 부하도 날아감) | `range_m` `radius_m` `windup_s` `damage` `cooldown_s` |
| `summon` | 포효하고 주위에 테마 일반 `count`마리, 체력 ×`hp_scale` | `count` `cooldown_s` `hp_scale` |
| `fan_shot` | 대상이 `range_m` 안이고 보이면, `windup_s` 동안 바닥에 빨간 선이 뻗은 뒤 `spread_deg` 부채꼴로 `count`발(발당 `damage`, 속도 `speed_mps`) | `range_m` `windup_s` `damage` `cooldown_s` `count` `spread_deg` `speed_mps` |

**지금 값 (`boss.json`)**: 체력 12000(`monsters.csv`, 2~3분 전투 목표의 제안값).
- 1페이즈(100%): 돌진·내려찍기.
- 2페이즈(66%): 소환이 더해짐, 이동 1.1배, 대기 0.85배, "보스가 더 사나워졌습니다".
- 3페이즈(33%): 부채꼴 탄막이 더해짐, 이동 1.35배, 대기 0.6배, "보스가 격노했습니다".

예전의 "체력 50% 격노"(M-22)는 이 3페이즈로 바뀌었다. 섬광탄은 여전히 내려찍기·탄막 준비를 끊는다.

**오류 보고(T-4)**: 모르는 kind·항목, 없는 스킬을 쓰는 페이즈, 100에서 시작하지 않거나 내림차순이 아닌 페이즈, 언어 표에 없는 알림 키, 0 이하의 대기·속도. 파일이 없으면 돌진만 하는 한 페이즈 보스로 동작한다.

## 4. 멀티

방장만 테이블과 페이즈를 계산한다. 파티원은 생기는 몬스터와 탄을 기존 동기화로 받는다. 보스의 빨간 원·탄막 선·페이즈 알림 글자는 파티원 화면에 나오지 않는다(I-77). 난전의 정비 문구도 방장 화면에만 나온다.

## 5. 난전 무기 진열대 · 탄약 (2026-10-21 구현, M-26)

`mode_rules.json`의 `survivor`:

| 항목 | 뜻 | 지금 값 |
|---|---|---|
| `weapon_pool` | 진열대에 나올 수 있는 무기 id(근접 제외) | mp5 · m4 · ak47 · m16 · m249 · r870 · benelli_m4 · double_barrel · svd · awm · alabong · mortar · railgun |
| `rack_size` | 진열대 하나에 놓이는 무기 수 | 3 |
| `special_cooldown_s` | 3번 칸 종류(로켓런처·박격포·레일건)가 쏜 뒤 다시 쓸 수 있을 때까지 | 25초 |

- 시작: 시작점 앞에 진열대 1(무기 3개, 바닥 글씨 "무기 고르기"). 고른 무기는 **1번 칸**. 모든 사람이 하나씩 고르면 진열대 2가 나오고 고른 무기는 **2번 칸**. 둘 다 고르면 목표가 `ModeArming` → `ModeGoal`로 바뀌고 그때부터 시계(보스까지 2분 30초, 표의 초)가 간다. AI 분대원은 고르지 않는다.
- 탄약: 난전의 모든 무기는 예비탄 무제한(`Loadout.UseOnslaughtAmmo`). 3번 칸 종류는 장전 대신 충전: 쏘면 탄창이 비고 `special_cooldown_s` 동안 다시 쏠 수 없으며(넣어 둬도 시간이 감) 끝나면 탄창이 찬다. 거치·충전이 끊기면 탄과 충전 시간을 돌려받는다.
- 강화: 표의 네임드가 쓰러지면 그 자리에 강화 진열대(바닥 글씨 "강화 무기"). 하나를 주우면 **손에 든 칸의 무기와 교체**되고 진열대는 사라진다.
- HUD: 탄약 칸에 `∞`, 충전 중이면 "충전 N초". 난전에서는 3번 칸 타일을 숨긴다.

## 6. 거점 배낭 · 탄 종류 · 전리품 (2026-10-21 구현, M-25)

**배낭**: 거점에서는 모든 생존자가 4×5 = 20칸 배낭을 가진다(`Backpack`). 한 칸에 한 묶음: 무기 1개(자기 탄창 유지), 같은 탄 종류, 같은 투척물(`max_carry`까지), 구급상자(3개까지).

**탄 종류** `content/data/calibers.csv`:

| 열 | 뜻 |
|---|---|
| `id` | 탄 종류 id. `weapon_types.csv`·`weapons.csv`의 `caliber` 열이 이것을 가리킴 |
| `name` | 이름 키(`name_caliber_<id>`, `content/lan/<언어>/weapon.csv`) |
| `stack` | 한 칸에 쌓이는 수 |
| `color` | 배낭 칸·바닥 상자 색 |

지금 값: 9mm 60 · 5.56 60 · 7.62 40 · 12게이지 12 · .50 5 · 로켓탄 2 · 포탄 3 · 레일건 배터리 2. 분류 기본값은 권총·기관단총 9mm, 돌격소총·경기관총 5.56, 지정사수·저격 7.62, 산탄총 12게이지, 로켓런처·박격포·레일건 각자. 무기 줄에서 덮어씀(AK47 7.62, M82 .50).

**규칙**
- 장전: 2·3번 칸 총의 예비탄 = 배낭의 같은 탄 합계. 장전하면 그만큼 배낭에서 빠진다(작은 묶음부터). 1번 칸(권총·근접)은 무제한 그대로.
- 바닥 무기(E): 그 칸이 비면 바로 장착, 아니면 배낭으로. 무기의 예비탄은 탄 묶음이 되어 배낭에 들어간다. 배낭이 가득이면 줍지 않음("배낭이 가득 찼습니다"). 1번 칸 무기는 전처럼 교체.
- 바닥 투척물은 배낭으로. 탄약 더미는 2·3번 칸 총의 탄 종류마다 한 묶음(충전 불가 무기 제외). 구급상자 보급은 Q가 차 있으면 배낭으로.
- 구급상자를 다 쓰면 배낭에서 하나가 Q로 자동으로 온다.
- 배낭 화면(I, 게임은 멈추지 않음): 칸 → 칸 = 옮기기(같은 종류면 합치기, 아니면 바꾸기), 칸 → 위 장착 칸 = 장착(무기는 자기 칸으로 가고 원래 무기는 그 칸으로, 투척물은 G, 구급상자는 Q가 비었을 때), 장착 칸 → 빈 칸 = 넣기(1번 칸은 비울 수 없음), 창 밖 = 바닥에 내려놓기(무기는 무기 줍기, 나머지는 전리품 상자). 마우스를 올리면 무기 이름·분류·탄 종류·피해·탄창.
- 전리품: 네임드(·보스)가 쓰러지면 `mode_rules.json` `survival`의 `loot_min`~`loot_max`(3~5)개를 주변에 흩뿌린다. 55% 팀이 든 총의 탄 묶음(반~한 칸), 15% 구급상자, 15% 수류탄·화염병, 나머지 `loot_weapons`(mp5 · m4 · ak47 · m249 · r870 · svd · alabong) 중 무기 하나. 일반 몹은 떨어뜨리지 않음.
- 멀티: 배낭은 방장이 가지고 스냅샷으로 보낸다(칸·종류·id·수·탄창). 파티원의 드래그는 `(from, to)` 두 숫자로 방장에게 요청(`ClientInventory`). 전리품은 `LootSpawn` 사건으로 생기고 세계 메시지로 사라진다. 프로토콜 5. 한계는 I-81.
- 검사: `game/tests/InventoryCheck.tscn`.

