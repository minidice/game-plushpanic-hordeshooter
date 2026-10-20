# 투척물·몬스터 데이터 규격

## 공격 가독성 규칙 — 2026-10-20

일반 근접 공격은 접촉 즉시 피해를 주지 않는다. **0.28초 준비/손 휘두르기 → 타격 1회 → 0.32초 회복**을 거친다. 시작할 때 방향을 고정하고, 타격 순간 거리·전방 약 113° 부채꼴·높이 차이·벽/소품 차폐를 다시 검사한다. 회피로 사거리나 방향을 벗어나면 빗나간다. 공격자가 기절/넉백되거나 유인 대상으로 전환되거나 사망하거나 특수 동작을 시작하면 예약 공격은 취소된다. 공격 간격은 `attack_interval_ms`의 시작-시작 간격이며 최소 0.6초다.

돌격 적은 머리와 몸을 숙인 돌격 상태에서 기존 충돌 피해를 유지한다. 일반 추격 중 근접 공격은 동일한 준비 동작 규칙을 따른다. 붙잡기·토사물·총격 등 특수 능력은 각자의 기존 예고/판정을 사용한다.

플레이어와 적은 실제 양수 피해를 받으면 모델만 약 0.1초 흰색으로 반짝인다. 원래 커스텀 색과 좀비 틴트는 복원되며, 0 피해·기절만 적용·무적 회피는 반짝이지 않는다. 관련 구현은 `Enemy.cs`, `CharacterEnemyPose.cs`, `CharacterHitFlashEffect.cs`, 검사 장면은 `EnemyAttackCheck.tscn`, `HitFlashCheck.tscn`이다.

> 무기 데이터와 같은 방식입니다: **표(CSV) + 고유 동작(JSON)**, 이름은 언어 키(`doc/localization.md`).
> 무기는 `doc/weapon-spec.md`를 보세요.

---

## 1. 투척물

| 파일 | 내용 |
|---|---|
| `content/data/throwables.csv` | 투척물 한 줄에 하나 |
| `content/data/throwable_effects/<이름>.json` | 효과 하나. `effects` 칸에 `;`로 여러 개를 붙이고, **적힌 순서대로** 적용 |

### throwables.csv 열

| 열 | 의미 |
|---|---|
| `id` | 투척물 ID |
| `name` | 이름 키 (`name_throwable_<id>`) |
| `look` | 외형과 비행감: `frag`, `fire`, `flash`, `lure` |
| `max_carry` | 최대 소지 수 |
| `max_range_m` | **최대 투척 거리.** 조준 원이 이 거리를 넘으면 범위 끝에서 멈춤 |
| `fuse_ms` | 던진 순간부터 터질 때까지. `0`이면 효과가 결정(닿으면 터짐 또는 유인 후 터짐) |
| `detonate_on_impact` | 닿자마자 터짐(화염병) |
| `effects` | 효과 파일 이름들 |
| `color`, `model`, `icon` | 외형 |

### 효과 종류 (JSON의 `kind`)

| kind | 하는 일 | 항목 |
|---|---|---|
| `explode` | 폭발 대미지 + 넉백 | `radius_m`, `damage`, `knockback_power`, `self_damage_share` |
| `burn_pool` | 불바다. 좀비와 나에게 초당 대미지 | `radius_m`, `duration_ms`, `enemy_dps`, `player_dps` |
| `stun` | 범위 안 좀비 기절 (벽 뒤는 안 걸림) | `radius_m`, `duration_ms` |
| `blind_self` | 던진 사람이 보고 있으면 화면이 하얘짐 | `radius_m`, `max_ms`, `facing_threshold` |
| `attract` | 착지 후 삑삑거리며 일반 좀비를 끌어모음. 끝나면 나머지 효과가 발동 | `radius_m`, `duration_ms`, `beep_interval_ms` |

예: `lure` = `lure_attract;lure_blast`이므로 6초 동안 끌어모은 뒤 폭발합니다. "섬광 + 화염"처럼 새 조합은 `effects` 칸만 바꾸면 됩니다.

### 조작

| 입력 | 동작 |
|---|---|
| **G** | 투척 조준 시작. 총을 내리고 투척물을 듦. 착탄 범위 원(효과 반경), 최대 거리 원, 궤적 점선 표시 |
| **좌클릭** | 던지기. 던진 뒤 **직전 무기로 자동 복귀** |
| **우클릭 / G** | 취소 |
| **휠** | 들고 있는 종류 바꾸기 |

구르기, 기절, 치료, 무기 교체(1·2)를 하면 조준이 취소됩니다.

---

## 2. 몬스터

| 파일 | 내용 |
|---|---|
| `content/data/monsters.csv` | 능력치 한 줄에 하나 |
| `content/data/monster_traits/<이름>.json` | 특수 감염체의 고유 행동 수치 |
| `content/enemies/<look>.tres` | **외형만** (모델, 충돌 크기, 색). 능력치는 여기 넣지 않음 |

### monsters.csv 열

| 열 | 의미 |
|---|---|
| `id`, `name` | ID, 이름 키 |
| `behavior` | `common`(일반), `balloon`(풍선 기생충), `bull`(박치기 황소), `boss`(서바이버 보스 `BossEnemy`: `bull_charge` 특성 필수, 내려찍기·소환 수치는 `mode_rules.json`) 등. 어떤 코드가 움직일지 결정 |
| `look` | 외형 .tres 이름 |
| `mirror_player` | `true`면 플레이어와 같은 종에서 장비를 뗀 좀비 외형 |
| `hp`, `move_speed_mps` | 체력, 이동 속도 |
| (피격 높이) | 표에는 없음. 총알이 1.0m 높이로 수평으로 날아가므로, 몸통 판정은 외형이 낮아도 **최소 1.4m**(`Enemy.MinHitHeight`)까지 올라감. 기어다니는 좀비도 맞음 |
| **`mass`** | **무게.** 넉백이 무게로 나뉨 (1 = 보통, 0.7 = 가벼움, 5 = 거의 안 밀림) |
| `attack_damage`, `attack_interval_ms`, `attack_reach_m` | 근접 공격 |
| `wake_range_m` | 이 거리 안에 오면 깨어남 |
| `traits` | 고유 행동 (`balloon_pop`, `bull_charge`) |

### 외형 변경 시 판정 동반 점검 (2026-10-20, I-68)

GLB 변경만으로 범위가 자동으로 바뀌지 않는다. 현재 감염체 반경은 인간형/끌개 0.40m, 덮치는 적 0.48m, 크롤러 0.43m, 토사물 적 0.52m다. 앞뒤 볼륨 보강 후 앞의 세 종류만 조정했다. 이동·탄환 피격·시체 반경과 몸 경계 기준 근접/도약 접촉은 같은 `.tres` 값을 사용한다. `attack_reach_m`는 두 몸 표면 사이 추가 여유 거리로 유지하므로 반경이 커진 만큼 중심 간 타격 거리도 늘어난다(인간형/끌개 +4cm, 덮치는 적 +8cm).

토사물과 끌개 연결의 시각적 시작점은 GLB `Head` 아래 `Muzzle`이다. 몸→입→목표 차폐를 확인하며, 특성 JSON의 최대 거리·피해·시간은 유지한다. 자세한 비율·원본 생성기·검사·단순 캡슐 제약은 [모델 규격 6.2](character-model-spec.md#62-모델과-판정연출-연동-2026-10-20)를 따른다.

### 고유 행동

- `balloon_pop`: `trigger_range_m`(부풀기 시작 거리), `inflate_ms`, `radius_m`, `helium_ms`, `summon_count`, `knockback_power`
- `bull_charge`: `trigger_range_m`, `min_range_m`, `windup_ms`, `speed_mps`, `duration_ms`, `dazed_ms`, `cooldown_ms`, `player_damage`, `dazed_damage_multiplier`, `bowl_power`

새 일반 몬스터(예: 무거운 거구 좀비)는 `behavior=common`으로 한 줄 추가하면 됩니다. 새 **행동**이 필요하면 코드가 필요합니다.

---

## 3. 넉백 (몬스터가 맞았을 때)

추천안으로 정한 공식입니다.

```
밀리는 속도(m/s) = 피격 파워 × 0.08 ÷ 무게
멈칫 시간(초)    = clamp(피격 파워 ÷ 무게 × 0.004, 0.05, 0.35)   ← 이 동안 자기 이동 속도 25%
```

- **피격 파워**는 무기의 `hit_power`입니다(산탄은 맞은 펠릿 수만큼 나눠 들어감). 폭발은 `knockback_power`이고, 중심에서 가장자리로 갈수록 약해집니다.
- 밀리는 속도는 초당 14m/s씩 줄어서, 밀리는 거리는 대략 `속도² ÷ 28`입니다.

| 상황 | 파워 | 일반 좀비(1) | 황소(5) |
|---|---|---|---|
| 소총 한 발 | 15 | 약 0.05m + 짧은 멈칫 (**다가오는 데 지장 없음**) | 거의 0 |
| 권총 한 발 | 10 | 미세 | 0 |
| 프라이팬 | 60 | 약 0.8m (밀치기) | 약 0.03m |
| 샷건 전탄 명중 | 90 | **약 1.8m** (실측 1.79m) | 0.06m |
| 로켓·박격포 중심 | 150 | 약 5m | 약 0.2m |

나(플레이어)가 쏠 때 밀리는 반동은 무기의 `recoil_power`이고, 옵션 `GameSettings.RecoilScale`로 줄일 수 있습니다(`doc/weapon-spec.md` 5장).
