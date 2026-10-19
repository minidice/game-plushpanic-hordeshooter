# 사운드 규격 — 합성 효과음과 교체 가이드

> **대상**: 사람과 AI 에이전트 모두.
> **구현**: `game/scripts/Audio/` — `Synth.cs`(합성기), `SoundRecipes.cs`(효과음 44종 레시피), `Sfx.cs`(재생), `AudioSetup.cs`(버스)
> **들어보기**: `./Start-Game.ps1 -Test`를 실행하면 모든 효과음이 `game/output/sounds/<id>.wav`로 저장됩니다.

## 핵심 원칙

- 프로젝트에는 **음원 파일이 하나도 없습니다.** 모든 효과음은 장면이 열릴 때 코드로 합성됩니다(노이즈, 파형, 엔벨로프, 필터). 결정적 시드라서 매번 같은 소리가 납니다.
- **교체는 파일 하나로 합니다.** `game/content/sounds/<id>.ogg`(또는 `.wav`, `.mp3`)를 넣으면 같은 ID의 합성음 대신 그 파일을 씁니다. 코드 수정은 필요 없습니다. 음량, 음정 흔들림, 동시 재생 수는 레시피 설정을 그대로 따릅니다.
- **귀는 거위에 있습니다.** `AudioListener3D`가 카메라(34m 위)가 아니라 플레이어 머리 높이에 있습니다. 그래서 등 뒤 좀비 발소리는 뒤에서, 왼쪽 소나는 왼쪽에서 들립니다. 화면 오른쪽이 오른쪽 귀입니다.

## 효과음 목록 (ID → 언제)

| 분류 | ID | 언제 나나 |
|---|---|---|
| 무기 | `shot_pistol` `shot_smg` `shot_rifle` `shot_lmg` `shot_shotgun` `shot_sniper` | 발사. 무기의 `FireSound`가 비어 있으면 분류로 고름 (`WeaponDefinition.ShotSound`) |
| | `rocket_launch` `mortar_fire` `mortar_whistle` | 로켓 발사 / 박격포 발사 / 포탄이 날아가는 동안 따라다니는 휘파람 |
| | `melee_swing` `pan_bong` | 휘두르기 / 프라이팬이 맞았을 때 "뎅" |
| | `dry_fire` `reload_start` `reload_end` | 빈 탄창 / 장전 시작 / 장전 끝 |
| | `throw` | 투척 |
| 폭발·투척물 | `explosion` | 드럼통, 로켓, 박격포, 수류탄, 유인 폭탄 (`Blast.Explode`) |
| | `glass_break` `fire_loop` | 화염병 깨짐 / 불이 타는 동안 반복 |
| | `flash_bang` `ear_ring` | 섬광탄 / 실명했을 때 귀 울림 |
| | `lure_beep` | 유인 폭탄 삑. 터질 때가 가까울수록 음이 올라감 |
| 플레이어 | `step` | 0.75m마다 발소리 (좌우 음정이 번갈아 바뀌어 뒤뚱거림) |
| | `roll` `heal` `pickup` | 구르기 / 치료 완료 / 줍기 |
| | `honk` `honk_long` | 피격 시 "꽥"(0.4초 쿨다운) / 쓰러질 때 |
| 좀비 | `zombie_step` | 깨어 있는 좀비가 16m 안에서 움직일 때 |
| | `groan` | 1.2~3.2초마다 22m 안의 깨어 있는 좀비 하나가 신음 |
| | `zombie_hit` `zombie_die` | 맞고 살아남음 / 쓰러짐 |
| | `sonar` | **보이지 않는 적**이 움직일 때 소나 파문과 함께 (특수 감염체는 낮은 음) |
| 특수 감염체 | `balloon_inflate` `balloon_pop` | 풍선 기생충 부풂 / 터짐 |
| | `bull_snort` `bull_bonk` | 황소 돌진 예고 / 충돌 |
| 스테이지 | `ambience_loop` | 밤바람 배경음 (Ambience 버스) |
| | `door` `alarm_loop` `shutter` | 문 / 크레센도 동안 경보 / 셔터 열림 |
| | `stage_clear` `game_over` | 클리어 팡파르 / "빠밤빠밤…" 게임 오버 |
| UI | `ui_click` | 메뉴 선택 |

## 버스와 상태 효과 (`AudioSetup.cs`)

| 버스 | 내용 | 효과 |
|---|---|---|
| `SFX` | 모든 효과음 | **헬륨 중독** 중 PitchShift ×1.55 (모든 소리가 높아짐), **섬광 실명** 중 LowPass 550Hz (먹먹해짐) |
| `Ambience` | 배경음 | 상태 효과의 영향을 받지 않음 |
| `Master` 직행 | `ear_ring`, `game_over`, `stage_clear`, `ui_click` | 귀 울림과 결과 효과음은 헬륨·먹먹함에 묻히지 않게 효과를 거치지 않음 (`SoundDef.Bus`) |

스테이지를 나가면(재시작, 메뉴) 두 효과 모두 꺼집니다.

## 새 효과음 추가

1. `SoundRecipes.All`에 `new("my_sound", "설명", s => ..., VolumeDb, PitchJitter, MaxVoices)`를 추가합니다.
2. 게임 코드에서 `Stage.Sfx.PlayAt("my_sound", 위치)`(3D) 또는 `Stage.Sfx.Play("my_sound")`(2D)로 재생합니다. 따라다니는 반복음은 `Sfx.Attach`, 배경 반복음은 `Sfx.Bed`를 씁니다.
3. `./Start-Game.ps1 -Test`의 "every sound recipe synthesizes a clean, audible clip" 검사가 무음, 클리핑, 길이 이상을 잡아냅니다.

## 알려진 제약

- 합성음이라 실제 녹음보다 단순합니다. 총소리는 "펑" 계열이고, 좀비 신음은 톱니파 웅얼거림입니다. 실제 음원으로 바꾸려면 위 교체 방법을 쓰세요.
- 3D 재생기는 40개를 공유합니다. 소리마다 동시 재생 수(`MaxVoices`)와 우선순위(`Priority`: 0 군중, 1 보통, 2 반드시 들려야 함)가 있습니다.
  - 자기 한도에 차면 가장 오래된 재생을 다시 쓰고(연사), 소나·신음처럼 `RestartWhenFull = false`인 소리는 새 소리를 건너뜁니다.
  - 재생기가 다 차면 우선순위가 같거나 낮은 소리의 자리를 가져옵니다. 그래서 좀비 군중 소리 때문에 내 총소리나 폭발음이 빠지지 않습니다.
- 합성은 실행당 한 번만 하고 장면끼리 공유합니다. 교체 파일 중 반복용(`fire_loop`, `alarm_loop`, `ambience_loop`)은 자동으로 반복 설정됩니다.
- 음악은 없습니다(게임 오버, 클리어 효과음만 있음).
