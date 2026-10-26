# 사운드 규격 — 녹음·합성 효과음과 교체 가이드

> **대상**: 사람과 AI 에이전트 모두.
> **구현**: `game/scripts/Audio/` — `Synth.cs`(합성기), `SoundRecipes.cs`(전체 등록 목록), `Sfx.cs`(파일/합성 로딩·재생), `AudioSetup.cs`(버스)
> **들어보기**: 현재 로더 음원은 Studio `/sounds`, 14종 비교는 `prototypes/audio-lab/output/index.html`. `./Start-Game.ps1 -Test`는 fallback 합성 레시피를 `game/output/sounds/<id>.wav`로 내보내므로 현재 교체 음원과 구별합니다.

## 핵심 원칙

- 최초 요청9종·전투 반복음5종·재질 접촉음17종은 배포 WAV55개(변형 포함)를 사용합니다. 나머지 효과와 파일 누락 시 fallback은 코드로 합성합니다(노이즈, 파형, 엔벨로프, 필터). 합성 레시피는 결정적 시드이며 실행당 한 번 생성해 공유합니다.
- **교체는 파일 하나로 합니다.** `game/content/sounds/<id>.ogg`(또는 `.wav`, `.mp3`)를 넣으면 같은 ID의 합성음 대신 그 파일을 씁니다(우선순위 ogg→wav→mp3). 음량, 음정 흔들림, 동시 재생 수는 레시피 설정을 따릅니다. `SoundDef.Variants > 1`이면 `<id>_2.wav`부터 지정 개수까지 추가 로드합니다. 단발 재생은 직전 변형을 연속 선택하지 않고, 누락 변형은 건너뛰며 대표 파일도 없으면 합성으로 돌아갑니다. `Stream(id)`/Studio는 대표 파일을 반환합니다.
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
| 플레이어 | `step` | 0.75m마다 발소리 (좌우 음정이 번갈아 바뀌어 뒤뚱거림). 발바닥 툭(150Hz) + 짧은 스침(0.9~3.2kHz). 레시피 -12dB, `NearCap`(실제 걷기 최대 약 -20dB, 휘두르기 약 -5dB, 소총 0dB). 150Hz 툭 소리만으로는 작은 스피커에서 거의 안 들려 스침을 더함. 분대원은 추가 -5dB. 재질별 발소리는 아래 surface_step 계열을 사용하며 무충돌 수면 장식은 제외 |
| | `roll` `heal` `pickup` | 구르기 / 치료 완료 / 줍기 |
| | `honk` `honk_long` | 피격의 짧은 거위 반응(0.4초 쿨다운, 2변형) / 쓰러질 때 긴 반응 |
| 무기·카드 | `honk_ability` | 꽥 무기, 꽥 장전·광선, 경적 소품. 피격용 `honk`와 분리한 기존 능력 합성음 |
| 좀비 | `zombie_step` | 깨어 있는 좀비가 16m 안에서 움직일 때. 레시피 -17dB(최대 약 -24dB), `NearCap` |
| | `groan` | 1.2~3.2초마다 22m 안의 깨어 있는 좀비 하나가 신음 |
| | `zombie_hit` `zombie_die` | 맞고 살아남음 / 쓰러짐 |
| | `sonar` | **보이지 않는 적**이 움직일 때 소나 파문과 함께 (특수 감염체는 낮은 음) |
| 특수 감염체 | `balloon_inflate` `balloon_pop` | 풍선 기생충 부풂 / 터짐 |
| | `bull_snort` `bull_bonk` | 황소 돌진 예고 / 충돌 |
| 스테이지 | `ambience_loop` | 밤바람 배경음 (Ambience 버스) |
| | `door` `alarm_loop` `shutter` | 문 / 크레센도·차 경보 동안 사이렌(**작게**: 레시피 -20dB, 평균 약 -24dB로 좀비 신음보다 작고 앰비언스보다 큼) / 셔터 열림 |
| | `stage_clear` `game_over` | 부드러운 상승 / 짧은 하강 악구 |
| UI | `ui_click` | 메뉴 선택 |

## 버스와 상태 효과 (`AudioSetup.cs`)

| 버스 | 내용 | 효과 |
|---|---|---|
| `SFX` | 공간·행동 효과음 | **헬륨 중독** 중 PitchShift ×1.55, **섬광 실명** 중 LowPass 550Hz |
| `Ambience` | 배경음 | 상태 효과의 영향을 받지 않음 |
| `Cues` → `Master` | `ear_ring`, `game_over`, `stage_clear`, `ui_click` | 상태효과를 우회하며 `SfxVolume`(효과음 볼륨·음소거)을 따름 |
| `Master` | 전체 합산 | HardLimiter ceiling -1 dB, pre-gain 0 dB, release 0.1초. 반복 초기화에도 하나만 설치. sample peak 초과 방지이며 true-peak/외부 장치 왜곡 검사는 별도 |

스테이지를 나가면(재시작, 메뉴) 두 효과 모두 꺼집니다.

## 새 효과음 추가

1. `SoundRecipes.All`에 `new("my_sound", "설명", s => ..., VolumeDb, PitchJitter, MaxVoices)`를 추가합니다.
2. 게임 코드에서 `Stage.Sfx.PlayAt("my_sound", 위치)`(3D) 또는 `Stage.Sfx.Play("my_sound")`(2D)로 재생합니다. 따라다니는 반복음은 `Sfx.Attach`, 배경 반복음은 `Sfx.Bed`를 씁니다.
3. `./Start-Game.ps1 -Test`의 "every sound recipe synthesizes a clean, audible clip" 검사가 무음, 클리핑, 길이 이상을 잡아냅니다.

## 알려진 제약

- 최초9종·전투 반복음5종·재질 접촉음17종은 파일 교체본을 사용합니다. 나머지 총·능력음 등과 파일 누락 시 fallback은 기존 합성 레시피입니다. 모든 소리가 실사 녹음으로 교체된 것은 아닙니다.
- 3D 재생기는 40개를 공유합니다. 소리마다 동시 재생 수(`MaxVoices`)와 우선순위(`Priority`: 0 군중, 1 보통, 2 반드시 들려야 함)가 있습니다.
  - 자기 한도에 차면 가장 오래된 재생을 다시 쓰고(연사), 소나·신음처럼 `RestartWhenFull = false`인 소리는 새 소리를 건너뜁니다.
  - 재생기가 다 차면 우선순위가 같거나 낮은 소리의 자리를 가져옵니다. 그래서 좀비 군중 소리 때문에 내 총소리나 폭발음이 빠지지 않습니다.
- 합성은 실행당 한 번만 하고 장면끼리 공유합니다. 교체 파일 중 반복용(`fire_loop`, `alarm_loop`, `ambience_loop`)은 자동으로 반복 설정됩니다.
- 음악은 없습니다(게임 오버, 클리어 효과음만 있음).

## 몬스터가 듣는 소리와 플레이어가 듣는 소리 (2026-10-20)

몬스터를 깨우는 **소음 반경**(`spawn_rules.json`, 무기 `noise_m`)과 실제 **재생 음량**(이 문서의 레시피)은 따로 정한다. 큰 소리 장치는 몬스터에게는 60m까지 들리지만, 플레이어에게는 알아챌 정도면 충분하다(사용자 요청, `decisions.md` L-6). ### 3D 소리의 가까운 거리 증폭 (2026-10-20, I-63)

3D 효과음은 `UnitSize` 7m, 로그 감쇠다. Godot의 로그 감쇠는 **20·ln(7 / 거리)** dB라서 7m 안에서는 오히려 커진다: 1.5m에서 약 +30dB, 3m +17dB, 7m 0dB, 20m -21dB, 48m -39dB. 결과는 3D 소리의 상한(`MaxDb`, 기본 +3dB)에서 잘린다. 그래서 **가까운 3D 소리는 레시피 음량과 관계없이 +3dB 근처**가 된다.

- `NearCap: true`는 상한을 그 소리의 음량(호출부 추가 dB 포함)으로 낮춘다. 2026-10-27 후속부터 `SoundDef`의 기본값이 true이며 모든 등록 효과음에 적용된다. 새로운 레시피도 이를 기본으로 따른다.
- 사격음은 총구 위치의 3D 소리다. 모든 `shot_*`에 같은 상한 계약을 적용한다. 폭발 기본 게인은 +1dB(큰 폭발 추가 +2), 황소 경고는 -3dB(보스 추가 +6)로 기존 근접 +3dB를 넘지 않도록 보정했다.
- 보스 산탄은 공유 레시피 +2를 유지하고 `FanShot` 호출만 추가 +3→+1로 보정해 기존 근거리 상한 +3dB를 유지한다. 준비음과 실제 스킬 호출을 포함하는 `AudioBossGainCapture`로 별도 확인한다. [후속 보정](features/effects-audio/log/010-code-boss-shotgun-gain.md).

### 과거 실제 출력 기준표 (2026-10-20, `AudioMixCheck`, 창 모드, SFX 버스 최대값)

음량을 말할 때는 **소총 사격 = 0dB** 기준으로 몇 dB 아래인지로 말한다. 3dB는 “조금”, 6dB는 “확실히”, 10dB는 “절반 정도로 들림”.

| 소리 | 소총 대비 | 메모 |
|---|---:|---|
| 소총 사격 (M4) | 0 | 기준 (+0.2dB) |
| 맞을 때 꽥 (`honk`) | 약 -7 | I-74. 이전 약 -1(소총과 비슷) → `NearCap` -3dB |
| 프라이팬 휘두르기 | 약 -4 | 2026-10-20 사용자 요청으로 약간 낮춤 (이전 약 -1, +3 상한) |
| 차 경보 사이렌, 2m | 약 -24 | I-62 |
| 걷기 발소리 | 약 -20 | I-63. -4(처음, 쿵쾅) → -35, -27(둘 다 “아무 소리 안 남”) → 스침 추가 후 -20. 휘두르기보다 작게 유지 |
| 서 있을 때 | 무음 | 앰비언스는 별도 버스 |
- 현재 코드 기준 장전·치료 등은 `Player.PlayOwn`(로컬2D/원격3D), 사격은 `WeaponController`의 총구 `PlayAt`(3D)이다. 이 차이는 변형 비교 검사에도 동일하게 적용한다.
- 박격포 낙하·소나·황소 경고는 priority2, 화염·환경 반복음은 priority0/초과 재시작 없음, 좀비 발소리도 초과 재시작을 생략한다. [후속 변경](features/effects-audio/log/008-code-audio-balance-followup.md). I-64의 사람 청음·실제 두 PC 환경 검증은 별도다.
- 레시피 값만 보는 `AudioLevelCheck`는 이 증폭을 못 본다. 실제 출력은 `AudioMixCheck`(창 모드)로 녹음해 확인한다.

`AudioLevelCheck`가 합성음의 크기를 비교한다(가장 큰 50ms 구간 RMS + 레시피 음량, 스피커·버스 음량·3D 감쇠는 제외).

| 소리 | 2026-10-20 측정 | 규칙 |
|---|---:|---|
| 소총 사격 `shot_rifle` | -4.6dB | 기준 |
| 좀비 신음 `groan` | -15.0dB | |
| 경보 사이렌 `alarm_loop` | -23.4dB | 소총보다 10dB 이상 작고, 신음 이하, 앰비언스보다 큼 |
| 좀비 발소리 `zombie_step` | -24.1dB (이전 -19.1) | 신음보다 6dB 이상 작고, 플레이어 발소리보다는 큼(다가오는 무리의 신호) |
| 플레이어 발소리 `step` | -35.7dB (처음 -17.7) | 들릴락 말락: 밤바람보다 작고, 밤바람 - 12dB보다는 큼 |
| 밤바람 `ambience_loop` | -27.0dB | |

자주·계속 나는 소리(발소리, 사이렌)는 전투 신호(사격·피격·특수 몬스터 예고)보다 충분히 작게 둔다. 새 소리를 넣거나 음량을 바꾸면 이 검사를 함께 돌린다.


## 로컬 Studio 사운드 도감 (2026-10-24, T-25)

`studio/Start-Studio.ps1` → `/sounds`에서 실제 `SoundRecipes.All` 목록과 음원을 조회한다. `Sfx` 로더를 그대로 사용해 파일 교체 여부·현재 음원·길이·반복·기본 음량·피치 흔들림·버스·동시 재생 수를 표시한다. 합성 PCM16은 메모리 WAV로, 교체 음원은 선택된 원본 파일로 브라우저에서 재생한다. 브라우저가 지원하지 않는 교체 코덱은 재생 오류를 표시한다.

원음의 기준 피치1 재생이며 게임 기본dB/거리 감쇠/상태 버스 효과/랜덤 피치/동시 음성 제한은 미적용이다. 이 화면의 재생 음량으로 게임 혼합 밸런스를 확정하지 않는다. 반복은 전체 파일 단위다. 음악/환경음도 레시피에 등록된 항목은 함께 표시하지만 등록되지 않은 파일을 임의 수집하지 않는다. 실제 게임의 사운드 재생·레시피를 수정하지 않고 조회용 bridge만 추가한다. [도구 계약·검증](studio-prototype.md).


## 수렵단3무기 전용음 — 2026-10-25

`hunter_club_raise/swing/hit`, `hunter_knife_ready/swing/hit`, `hunter_bow_draw/release/hit`를 SoundRecipes에 추가. 각 무기의 준비/궤적/실제 접촉을 구별하며 소리9개 모두 근거리 상한·동시 발음 제한을 적용한다. 활 당김1.2초는 실제 스트림 길이를 종족별 준비시간에 맞춘 피치로 재생. Studio 사운드 목록은 같은 등록 데이터에서 자동 인식한다. [상세 타이밍·검증·범위](hunter-weapon-presentation.md).

## 사운드 품질 비교 시제품 — 2026-10-27 (A-26)

[설계](features/effects-audio/sound-quality-pilot.md) · [제작/재현](../prototypes/audio-lab/README.md). 먼저 피격·신음·사망 각 A/B를 무료 CC0 소재로 편집했다. 후보는 `prototypes/audio-lab/candidates/`에 보존한다. 원본의 출처·라이선스·해시는 `sources.json`, 시제품 편집 수치는 `recipes.json`에 남긴다. 시제품 단계에는 게임을 바꾸지 않았으며 이후 적용은 아래와 같다.

`tests/AudioPilotCapture.tscn`은 현재 Stage 리스너/Sfx를 통한 0.5·3·8m 단독음, 군중 사망, 신음·사망·사격 겹침의 Master 출력을 측정한다. 결과는 `output/audio-pilot/capture.json`과 unclamped `.f32le`. 청취 자료는 같은 float 원본을 공통 감쇠해 생성한다. 캡처 성공은 원래 믹스의 0 dBFS 초과가 없다는 뜻이 아니며, 클리핑 수치와 별개다. 기존 `AudioMixCheck`는 SFX 일부 행동만 보므로 이 군중 중첩 조건의 대체 검사가 아니다.

## 사운드 품질 9종 게임 적용 — 2026-10-27 (A-26)

`game/content/sounds/`에 mono PCM16 48 kHz 12파일(9종+변형3)을 배포한다. `.import`는 무압축·정규화 없음. 전부 source sample peak ≤ -6 dBFS, 최대50 ms RMS 목표 -18 dBFS 중 더 낮은 게인으로 제작한다. 48 kHz 변환이 원본 품질을 복원하지는 않는다. [배포 레시피](../prototypes/audio-lab/release-recipes.json) · [파일별 출처/해시](../game/content/sounds/credits.json).

| ID | 길이(초) | 재생 dB / pitch jitter / 동시수 | 내용 |
|---|---:|---|---|
| honk | .29 / .34 | -1 / .04 / 2 | A/B 거위 반응. 양 끝 피치 검사에서 작은 변형의 식별 여유를 확보하도록 1 dB 보정 |
| honk_long | .72 | -2 / .03 / 1 | 긴 거위 발성 편집 |
| groan | 1.22 / .98 | +2 / .05 / 3, 초과 생략 | 저음 목 울림/짧은 발성. 파일 자체가 작으므로 +2 dB가 증폭 과다라는 뜻은 아님 |
| zombie_die | .53 / .40 | -4 / .05 / 3, 초과 생략 | 짧은 발성+낙하 |
| balloon_inflate | .78 | 0 / .03 / 2 | 녹음된 공기 압력 소재를 팽창감으로 편집 |
| balloon_pop | .30 | 0 / .04 / 3 | 공기 파열+짧은 충격 |
| bull_bonk | .52 | +1 / .04 / 3 | 저음 충격+목재 접촉 |
| game_over | 1.65 | -2 / 0 / 1 | 직접 합성한 감쇠 악기 하강 악구 |
| stage_clear | 1.45 | -3 / 0 / 1 | 직접 합성한 감쇠 악기 상승 악구 |

발성 종별 분리는 후속이다. 아직 모든 플레이어는 거위 반응을 공유한다. 거위/공기는 공개 MP3 미리듣기 소스이며 녹음·압축 한계가 남는다. 사용자 기준 게임의 음원은 추출하지 않았다.

검사: `AudioQualityCheck`는 로딩/변형/상한/군중 동시수/버스/효과음 볼륨/limiter 중복을 확인한다. `AudioQualityCapture`는 같은 캡처 코드에 `EnforceCeiling=true`, 출력 폴더 `audio-quality`를 사용하여 20조건(단독거리9, 군중2, 추가6종, 헬륨/먹먹함, 복합 전투)을 측정한다. 실신호·프레임 수·유한값·0 dBFS 초과0·피크≤-0.8 dBFS 허용오차가 필요하다. Master 최종 캡처와 limiter 이전 SFX의 `AudioMixCheck` 값은 구분한다. [실제 실행 결과와 제약](features/effects-audio/log/002-art-sound-quality-rollout.md).

상태효과 조건은 `Player.ApplyHelium`/`Blind`로 실제 상태를 적용하고, 물리 프레임 이후 및 캡처 종료 시 버스 활성 여부를 검사한다. Stage가 상태를 매 프레임 갱신하므로 버스 직접 토글만으로 검증하지 않는다. `AudioCombatSoak.tscn`은 별도로 기본 Stage1에서 300초 통제 전투·앰비언스·이동·사격·피격·군중 반복의 최종 출력과 전투 발생량을 검사한다. [검사 보완·메인 통합 기록](features/effects-audio/log/003-check-combat-soak-main-integration.md).

## 전투 반복음 5종 추가 — 2026-10-27 (A-26)

기존12파일을 유지하고9파일 추가, 총14종/21파일. 소총과 좀비 피격은 각각3변형이며 바로 전 변형을 연속 선택하지 않는다. [제작·검증](features/effects-audio/log/004-art-combat-sound-update.md).

| ID | 길이(초) | 재생 dB / jitter / 동시수 | 편집 방향 |
|---|---:|---|---|
| shot_rifle | .30 ×3 | +7 / .025 / 6 | CC0 AR-15 녹음3발 발췌, 상단/잔향 억제·짧은 기계적 타격감 |
| zombie_hit | .20 ×3 | -2 / .05 / 3, 초과 생략 | soft impact+작은 목재 접촉. 사망 발성과 구분 |
| reload_start | .28 | 0 / .025 / 2 | 에어소프트 녹음의 분리 동작 |
| reload_end | .23 | +1 / .025 / 2 | 별도 잠금 동작 |
| dry_fire | .15 | +1 / .03 / 2 | 잠금 소재 일부로 만든 짧은 단일 클릭 |

5종 모두 NearCap. 파일은 mono PCM16 48kHz·피크≤-6dBFS. 소총 최대50ms RMS 목표 -14dBFS, 나머지 -18dBFS지만 피크 제한이 우선한다(소총 실제 약 -19.0~-19.3dBFS). 양수 재생 게인이 파일 누락시 구형 합성음을 더 키우지 않도록 fallback 원음에는 반대 보정을 적용했다. 장전음은 아직 여러 무기 공용이며 실제 무기별 재질·애니메이션 전용화는 후속이다.

`AudioMixCheck`는 실제 총구3D 소총의 3변형×pitch0.975/1.025를 기준으로 휘두르기/걷기/피격 상·하한을 검사한다. `AudioQualityCapture`는27조건으로 확장(5종 단독+30발 연사+군중 피격 중첩7조건 추가). 연사 녹음은 Sfx 직접 호출의 통제 시나리오로 실제 총알/적 판정 플레이와 구별한다. 캡처에 credits.json과 오디오 구현3파일/캡처 시나리오 코드의 SHA-256을 기록하고 비교 페이지는 현재 에셋·오디오 코드와 일치하는 녹음만 싣는다.

페이지는14종 동일50ms RMS 단발과 소총30발·피격30회의 전후 반복 비교를 제공한다. 비교용 변형 순환은 편집 순서이며 게임의 랜덤 선택과 구별한다. 출력 수치 통과는 인간의 피로도/음색 선호 확정이 아니다.


## 재질 접촉음 (2026-10-27)

[표면 데이터](../game/content/data/surface_profiles.json)와 [오브젝트 규격](object-data-spec.md)의 7계열/15프리셋을 사용한다. 새17종은 각2변형, 전체 배포 WAV는55개(기존21개 불변)다.

| 이벤트 | ID | 재생 게인 / 동시수 / 피치 변동 |
|---|---|---|
| 발소리6계열 | `surface_step_{metal,wood,soft,soil,gravel,grass}` | -10dB / 3 / ±0.06 |
| 환경 명중7계열 | `surface_hit_{stone,metal,wood,soft,soil,gravel,grass}` | -3dB / 3 / ±0.05 |
| 파손4계열 | `surface_break_{stone,metal,wood,soft}` | 0dB / 2 / ±0.04 |

모두 priority0·NearCap·초과 요청 생략·같은 변형 연속 재생 방지. 수치는 제작 초기값이며 프리셋 추가 게인과 더한다. 좀비의 새 발소리는 -5dB 추가, 봇의 기존 추가 감쇠는 유지한다. 파일은 mono PCM16 48kHz, 피크≤-6dBFS, 부드러운 입출력 fade. 기본 RMS 목표는 발 -12 / 명중 -18 / 파손 -16dBFS이며 피크 제한이 우선한다. 누락 파일은 조용한 기존 합성으로 fallback하므로 재질 음색은 잃지만 큰 소리가 새로 나지 않는다.

실제 플레이어/분대 발소리, 좀비 발소리, WeaponController hitscan의 환경 충돌, ObjectBody의 일회성 파손에 연결한다. 적의 zombie_hit와 분리하며, 엄폐 사격막은 소유 본체의 재질로 해석한다. 배럴 폭발음/피해는 기존 경로다. 원격 Shot 이벤트는 충돌체·재질 정보를 보내지 않아 타 플레이어의 환경 명중은 이번에 재현하지 않는다(발소리와 동기화된 ObjectBreak 파손은 각 PC의 기존 호출에서 적용). 적 투사체/로켓/근접 명중·동적 물체끼리의 충돌·메시별 복합 재질은 범위 밖이다.

무료 Kenney CC0 Impact Sounds 소재를 재편집했다. 흙/자갈/풀의 명중·파손 등 일부는 녹음 조합으로 설계한 소리이며 실제 해당 재질 현장 녹음이라고 주장하지 않는다. `AudioSurfaceCapture.tscn`은 A/B 34단발+7계열 10회 발소리+중첩1의42조건을 실제 Master에서 녹음한다. `tools/surfaces.py`는 최신 파일/구현/표면 데이터 해시를 검사하고 정규화 없는 청음 페이지를 만든다. [작업 기록](features/effects-audio/log/005-code-surface-audio.md).
