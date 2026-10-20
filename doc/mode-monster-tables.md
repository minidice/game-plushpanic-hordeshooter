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
