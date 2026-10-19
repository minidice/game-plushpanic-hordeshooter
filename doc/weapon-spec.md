# 무기 데이터 규격 — 기본값·실데이터·특성

> **대상**: 사람과 AI 에이전트 모두. 무기를 추가하거나 수치를 바꿀 때 이 문서만 보면 됩니다.
> **데이터**: `game/content/data/`
> **로더**: `game/scripts/Weapons/WeaponDatabase.cs`
> **투척물·몬스터**는 `doc/throwable-monster-spec.md`, **이름(다국어)** 은 `doc/localization.md`를 보세요.

## 1. 파일 세 종류

| 파일 | 역할 | 형식 |
|---|---|---|
| `weapon_types.csv` | **분류 기본값.** 권총, 샷건, 돌격소총… 한 줄에 분류 하나 | CSV (엑셀로 열고 저장 가능, UTF-8) |
| `weapons.csv` | **실제 무기.** M4, AK47, 알라봉… **빈 칸은 분류 기본값을 그대로 사용** | CSV |
| `weapon_traits/<이름>.json` | **고유 특성.** 산탄, 폭발, 쌍권총, 근접 부채꼴, 점사, 투사체, 곡사처럼 표에 넣기 애매한 동작 | JSON (파일 하나 = 특성 하나) |

```
weapon_types.csv          weapons.csv                      weapon_traits/
type=assault_rifle  ◀──── m4,assault_rifle,M4,11,,,,       buckshot.json
  damage 10                  (빈 칸 → 분류 값)              rocket_warhead.json
  max_range 40 …          ak47,assault_rifle,AK47,7        dual_wield.json …
  traits = (없음)         m16,…,traits=burst_3 ───────────▶ burst_3.json
```

- CSV 첫 줄은 열 이름(영문)입니다. **`#`로 시작하는 줄은 주석**이고, 둘째 줄에 한글 설명을 넣어 두었습니다.
- 숫자는 소수점 `.`, 참거짓은 `true/false`(`1/0`, `y/n`도 가능), 무제한은 `inf`(`-`, `무제한`도 가능)입니다.
- **오타는 게임이 멈추지 않고 오류 목록으로 보고됩니다.** 예: `weapons.csv 5행 (m16): damage 값 'abc'는 숫자이(가) 아닙니다`. Godot 출력 창과 `./Start-Game.ps1 -Test`의 "weapon data loads without errors"에서 보입니다.

## 2. 열 목록 (분류·무기 공통)

| 열 | 의미 | 단위·값 |
|---|---|---|
| `type` | 분류 ID (weapons.csv에서는 어느 분류를 따를지) | weapon_types.csv의 type |
| `id` | 무기 ID (weapons.csv 전용, 코드·맵 배치에서 이 이름으로 찾음) | 영문 소문자 |
| `name` | **이름 키** (`name_weapon_<id>`). 실제 문구는 `content/lan/<언어>/weapon.csv` | |
| `slot` | 칸 | `1` 보조(근접·권총, **탄약 무제한**) / `2` 주무기 |
| `category` | 분류(외형·발사음·화면 흔들림) | `melee pistol smg rifle lmg shotgun sniper heavy` |
| `fire_mode` | 발사 방식 | `melee` 휘두르기, `hitscan` 즉발 총알, `projectile` 날아가는 투사체, `lob` 곡사 |
| `automatic` | 누르고 있으면 연사 | `true` / `false`(클릭마다) |
| `damage` | 한 발 대미지. **산탄은 전탄 합계**, 로켓은 직격 대미지 | |
| `max_range_m` | 최대 사거리. 총알이 여기서 멈춤. 근접은 손이 닿는 거리 | m |
| `effective_range_m` | 유효 사거리. 여기까지 대미지 100%, 이후 최대 사거리까지 선형 감소 | m |
| `falloff_min` | 최대 사거리에서의 대미지 배율 | 0~1 |
| `fire_interval_ms` | 발사 간격 (근접은 휘두르는 간격) | ms |
| `reload_ms` | 탄창식 전체 재장전 시간 / 개별 장전식 첫 탄 삽입까지 시간 | ms |
| `reload_shell_ms` | 첫 탄 이후 한 발씩 추가 장전 간격. 0 또는 빈 값이면 탄창식 | ms |
| `mag_size` | 1탄창 총알 수 | |
| `max_mags` | 최대 탄창 수. 예비탄 최대치 = `mag_size × max_mags` | `inf` 무제한 |
| `start_mags` | 처음 주웠을 때 예비 탄창 수 | |
| `refillable` | 탄약 더미로 충전 가능 | `true/false` |
| `pierce` | 한 발이 뚫고 지나가는 적 수(관통마다 대미지 ×0.8) | 1 = 관통 없음 |
| `spread_deg` `spread_per_shot_deg` `move_spread_deg` `max_spread_deg` | 퍼짐(조준원): 기본, 발당 증가, 이동 중 추가, 최대 | 도 |
| `recoil_power` | **반동 넉백 파워(쏜 사람).** 거리 대신 파워라 옵션으로 줄일 수 있음 (5장) | 권총 8, 샷건 60, 로켓 100 |
| `hit_power` | **피격 넉백 파워(맞은 적).** 몬스터 무게로 나뉨. 산탄은 맞은 펠릿 수만큼 나눠 들어감 | 소총 15(멈칫만), 샷건 90, 근접 60 |
| `traits` | 붙일 특성 이름, `;`로 구분. 무기 칸에서 `-이름`을 쓰면 분류에서 물려받은 특성을 뺌 | |
| `fire_sound` | 발사음 ID (doc/sound-spec.md). 빈 칸이면 category로 고름 | |
| `model` | 모델 경로(예: `res://assets/weapons/m4.glb`). 빈 칸이면 도형으로 만듦 | |
| `icon` | 아이콘 경로(예: `res://assets/icons/m4.png`). 하단 장비 HUD에 표시 | |
| `color` `accent_color` `length_m` | 도형 외형의 색과 길이 | |

**합치는 규칙**: 무기 칸이 비어 있으면 분류 값을 씁니다. 분류에도 없으면 코드 기본값을 씁니다. `name, slot, category, fire_mode, damage, max_range_m, fire_interval_ms`는 둘 중 한 곳에 반드시 있어야 합니다.

## 3. 특성 (weapon_traits/*.json)

파일 이름이 특성 이름입니다. `kind`가 동작 종류이고, `description`은 사람용 메모입니다. 모르는 항목은 오류로 보고합니다.

| kind | 하는 일 | 항목 |
|---|---|---|
| `pellets` | **산탄.** 한 번에 여러 발이 원뿔로 퍼짐. `damage`·`hit_power`를 발 수로 나눔 → **다 맞으면 전부, 일부만 맞으면 그만큼만** | `count`, `spread_deg` |
| `explode_on_hit` | **타격 후 폭발.** 총알·로켓·포탄이 멈춘 자리에서 폭발 | `radius_m`, `damage`(폭발 대미지), `self_damage_share`(내가 맞을 때 비율), `knockback_power`(폭발 넉백) |
| `projectile` | 직선 투사체 (`fire_mode=projectile`에 필요) | `speed_mps`, `max_flight_ms` |
| `lob` | 조준점으로 곡사, 벽 너머 가능 (`fire_mode=lob`에 필요) | `flight_ms`, `min_range_m` |
| `burst` | 점사 | `count`, `interval_ms` |
| `melee_arc` | **근접.** 앞쪽 부채꼴 안의 적을 가까운 순서로 여러 마리 | `arc_deg`, `max_targets` |
| `dual_wield` | **쌍권총.** 같은 무기를 하나 더 주우면 쌍으로 바뀜 | `name_format_key`(언어 키, 기본 `fmt_weapon_dual`), `mag_multiplier`, `fire_interval_multiplier`, `spread_add_deg`, `recoil_multiplier` |

같은 kind의 특성이 둘이면 나중 것(무기 칸)이 이깁니다. 예: 더블배럴은 `-buckshot;buckshot_wide`로 9발 대신 14발 산탄을 씁니다.

### 쌍권총 처리

**쌍권총은 별도 무기가 아니라 "같은 권총 두 자루"입니다.** 권총 분류에 `dual_wield`가 붙어 있어서 모든 권총이 자동으로 쌍 버전을 가집니다.

- M1911을 들고 M1911을 또 주우면 **쌍 M1911**이 됩니다. 탄창 ×2, 발사 간격 ×0.6, 퍼짐 +1°, 반동 ×1.3이고, 수치는 `dual_wield.json`에서 바꿉니다.
- 글록을 들고 M1911을 주우면 쌍이 아니라 **교체**됩니다. 들고 있던 글록은 바닥에 떨어집니다.
- 특정 권총만 쌍을 막고 싶으면 그 무기의 `traits`에 `-dual_wield`를 씁니다.

### 근접 무기 처리

2026-10-20: 클릭 즉시 피해를 주지 않고, 준비 0.11초 → 0.17초 타격 → 0.23초 후속 동작 → 0.42초 복귀로 움직입니다. 오른손 파지점을 중심으로 무기와 팔이 함께 움직입니다. 피해는 타격 순간 한 번만 판정하며 거리·앞쪽 부채꼴·벽/소품 차폐를 다시 확인합니다. 기절·도약/회복 등 행동 차단·무기 교체·투척 조준·사망은 예약된 타격을 취소합니다. 최신 총기 수치와 조사 근거는 [전투 템포 조정](combat-pacing-research.md)을 기준으로 합니다.

**근접도 같은 표의 한 분류(`melee`)입니다.** 칸은 1번이라서 권총과 서로 교체됩니다(L4D2 방식).

| 열 | 근접에서의 의미 |
|---|---|
| `damage` | 한 번 휘두를 때 대미지 |
| `max_range_m` | 손이 닿는 거리 |
| `fire_interval_ms` | 휘두르는 간격 |
| `hit_power` | 맞은 적이 밀려나는 힘 |

부채꼴 각도와 한 번에 칠 수 있는 수는 `melee_arc` 특성에 있습니다. 탄약 관련 칸은 무시합니다. 새 근접 무기(야구방망이 등)는 weapons.csv에 `type=melee`로 한 줄 추가하면 됩니다.

## 4. 새 무기 추가 예

```csv
id,type,name,damage,...,traits,model,icon
scar,assault_rifle,SCAR-H,16,...,,res://assets/weapons/scar.glb,res://assets/icons/scar.png
```

1. `weapons.csv`에 한 줄을 추가합니다. 바꿀 칸만 채우고 나머지는 비워 둡니다.
2. 맵에 놓으려면 `StageOneLayout.cs`에 `Weapon(new Vector3(x, 0, z), "scar");` 한 줄을 추가합니다.
3. `./Start-Game.ps1 -Test`로 데이터 오류가 없는지 확인합니다.

## 5. 넉백 파워와 옵션

- 넉백은 **거리가 아니라 파워**입니다. 실제로 밀리는 속도 = `파워 × 0.08 m/s × 옵션 배율`이고, 땅에서 감속하므로 밀리는 거리는 대략 파워의 제곱에 비례합니다.
- 측정값: 권총(8) 약 1.5cm, 샷건(60) 약 1.1m, 로켓(100) 약 3m.
- 옵션 배율은 `GameSettings.RecoilScale`입니다(기본 1, 0이면 반동 넉백 없음). 설정 화면은 아직 없고, 화면을 만들면 이 값만 바꾸면 됩니다.
- 적이 맞아서 밀리는 `hit_power`는 옵션의 영향을 받지 않습니다. 몬스터 무게로 나뉘고 짧은 멈칫도 걸립니다(`doc/throwable-monster-spec.md` 3장).

## 6. 재장전

R을 누르면 `reload_ms` 동안 장전합니다. 타이밍을 맞추는 액티브 리로드는 없앴습니다. 장전 중에는 이동 속도가 15% 줄고, 무기를 바꾸면 장전이 취소됩니다.

## 7. 손잡이와 팔 연결 (2026-10-20)

게임용 무기는 `WeaponVisual.Built`의 `RightGrip`/`LeftGrip`으로 손 위치를 제공한다. `CharacterRig.BindWeaponGrip`은 모델의 정적인 `leftArm`/`rightArm`을 숨기고, 어깨에서 손잡이까지 짧고 납작한 날개·앞발을 연결한다. 일반 총은 몸 오른쪽에서 한 손으로 들며 반대 날개는 몸 옆에 둔다. 쌍권총과 중화기는 양손을 쓴다. 이동/반동/시각적 변형/도약 뒤에 연결을 갱신한다. 무기가 숨겨진 투척 조준 중에는 이 날개·앞발도 숨긴다.

일반 총을 들고 달리면 총구를 위로 들어 올리고 빈 날개를 옆으로 벌린다. 발사 입력이 들어오면 총구를 즉시 수평으로 내린 뒤 발사 처리를 한다. 메뉴의 서 있기/달리기/사격 자세 버튼으로 비교할 수 있다.

무기 GLB에 `GripRight`, `GripLeft` Node3D(Blender Empty)를 추가하면 그 위치를 우선 사용한다. 없으면 현재 총기 생성 도구의 손잡이·앞손잡이 위치와 길이에 맞춘 분류별 기본값을 사용한다. 쌍권총은 각 총의 독립 손잡이를 사용하고, 근접 무기는 한 손만 연결한다. 손과 아래팔은 캐릭터의 커스텀 색을 따른다. 바닥에 놓인 무기에는 손이 생성되지 않는다.

이 방식은 현행 뼈 없는 캐릭터 구조의 절차적 자세다. 개별 손가락 조작, 탄창 교체 애니메이션, 임의 길이/체형에 맞춘 두 관절 IK는 별도 작업이다. 현재 네 종과 기본 비율에 맞춰져 있다. 모델의 팔 위치를 바꿀 경우 `CharacterWeaponGrip.Connect`의 어깨 기준도 맞춰야 한다.

검사: `res://tests/WeaponGripCheck.tscn` (4종 × 7무기, 반동/도약, 숨김, 쌍권총), 시각 검증: `res://tests/CaptureWeaponGrip.tscn` → `output/weapon-grip-*.png`.
