# 폭발·화염·독 효과 품질 및 공통 데이터

현재 확장: 2026-10-27 [A-27 화염·연무](features/effects-audio/decisions.md#a-27-화염연무의-시각-계층과-무해한-잔흔), [구현/검사 기록](features/effects-audio/log/004-code-area-effects.md). 아래의 2026-10-24 검사 수치는 당시 이력이며 최신 검사는 기능 기록을 따른다.

2026-10-24. 사용자가 공통 부품+계열별 조합 추천안과 Studio 공통 데이터 연결을 승인했다. 구현/검증 결과는 아래에 별도로 기록한다.

## 범위와 구조

- 짧고 선명한 스타일을 유지하며 수류탄/드럼통/로켓/박격포는 섬광·화염 덩어리·파편·먼지·연기를 공유하고 시간/형태를 구분한다. 화염병은 착탄 확산과 낮은 지속 불꽃, 독은 비행 방울→착탄 물보라→거품 웅덩이를 연결한다.
- `game/content/data/combat_effects.json`은 효과 ID·표시 이름·계열·색·시각 수치·사용처와 Studio 표본 크기/시간의 원본이다. 웹 전용 복사본을 만들지 않는다. 기존 피해·충돌·피해 반경·DPS·유효 시간은 전투 데이터가 소유한다. 미리보기 수치는 피해 데이터가 아니다.
- `CombatEffectCatalog`는 원본을 엄격하게 읽는다. `CombatBurstEffect`와 `CombatSurfaceEffect`, `CombatProjectileEffect`는 판정 없는 외형만 생성하며 Studio도 같은 생성기를 사용한다. 효과 시간은 명시적으로 진행해 Studio 고정 시간 캡처에서 게임과 같은 움직임을 얻는다.
- 런타임은 발생원 키로 효과를 선택한다. Studio는 등록 효과를 자동 열거하고 ID/계열/사용처/원본/시각 수치/표본 조건을 보여 준다. 기존 총구·피격·소나·벽 붕괴는 보존한다.
- 폭발/독은 공유 메시와 셰이더를 유지한다. 지속 화염은 `AreaSurfaceVisual`의 GPU 입자 카드만 사용하고 바닥 면을 만들지 않는다. 연무 표본은 입자 카드와 전용 지면 셰이더를 함께 사용한다. 원본 PNG는 `prototypes/effects/tools/generate_area_textures.py`로 재생성한다. 기존 레벨과 시점에 맞는 실제 엔진 렌더로 검수한다.

## 선택과 제약

전체 효과를 독립 제작하면 중복과 스타일 불일치가 늘고, 색/크기만 바꾸면 재질과 공격 종류가 구분되지 않는다. 승인된 공통 부품+계열별 조합을 적용한다. 시각 수치는 조정값이며 제품 확정 피해 수치가 아니다.

유효 위험 원의 바깥을 웅덩이로 칠하지 않는다. 불꽃은 낮고 군집 사이가 비어 캐릭터를 가리지 않게 한다. 기존 화염의 시야용 광원/음향은 유지한다. 폭발의 추가 광원은 동시에 제한하고, 지속 위험 표시를 부하 제한 때문에 제거하지 않는다. 지형 전체에 투영되는 데칼/새로운 지형 피해 판정은 범위 밖이며 현재 바닥 높이 계약을 유지한다.

## 데이터 계약 (schema_version 1)

| 항목 | 의미 / 적용 |
|---|---|
| `id`, `name`, `kind`, `family` | 고유 ID, 표시 이름, burst/surface/projectile, blast/fire/poison/smoke. smoke는 surface만 허용 |
| `sources`, `used_by` | 런타임 조회 키(중복 금지), `game/scripts/` 기준 사용처. Studio도 그대로 표시 |
| `color`, `accent`, `particles` | 6자리 RGB, 작은 파편/입자 수 0~48 |
| `lobes`, `spread`, `rise` | burst/surface만. 덩어리0~24, 퍼짐0.1~0.85, 상승0~3. surface는 반경에 대한 군집 위치 비율과 불꽃 높이/거품 상승 |
| `smoke_color` | burst 및 fire/smoke surface의 필수6자리 RGB |
| `light_energy`, `duration_s` | burst만. 추가 광원0~6, 시각 수명0.1~3초 |
| `opacity`, `residue_s` | fire/smoke surface만 필수. 카드 농도0.01~1, 무해한 잔흔0~3초. smoke의 잔흔은0만 허용 |
| `preview_radius_m` | 모든 종류의 Studio 표본 크기0.05~10m. 피해 반경 설정이 아님 |
| `preview_lifetime_s` | surface/projectile의 Studio 표본 수명0.01~15초. 실제 전투 지속시간 설정이 아님 |

해당 kind에서 사용하지 않는 키·빠진 필드·중복 필드/ID/발생원·잘못된 색/범위는 로드 오류로 표시한다. 잘못된 발생원을 기본 폭발로 조용히 대체하지 않는다. 실제 웅덩이 크기와 종료는 기존 위험 지대 노드가 결정한다. 프로필 추가는 현재 세 가지 생성 방식 안에서 지원하며, 새로운 물리/효과 종류는 코드를 추가해야 한다. JSON 수정 후 Studio **로컬 데이터 다시 읽기**로 새 프로세스를 로드한다.

| ID | 연결 |
|---|---|
| `blast` | 일반 `weapon`과 기본 `blast` 발생원, 자체 폭발 오브젝트 |
| `grenade`, `lure` | 수류탄·유인 폭발 |
| `barrel`, `rocket`, `mortar` | 연료 팽창·진행 방향·위로 솟는 형태 |
| `fire_impact`, `fire_pool` | 화염병 착탄 및 지속 위험 |
| `smoke_cloud` (`smoke_preview`) | 노란 저층 연무. Studio 표본 전용, 피해/시야 차단 없음 |
| `poison_flight`, `poison_impact`, `poison_pool` | 실제 적 투사체, 착탄, 기존 VomitHazard |

폭발은 최대24개·추가 광원4개이며 오래된 일회성 장식만 줄인다. 지속 위험 노드는 이 제한으로 제거하지 않는다. 시각 RNG는 고정 독립 인스턴스로 전투 RNG를 소비하지 않는다. `visibility_alpha` 인스턴스 셰이더 값으로 불꽃/연기/거품/독 꼬리를 감쇠한다.

### 화염·연무 현재 계약

- 공유 정의는12개다. fire_pool은 불꽃24개/불티12개/얇은 연기12개, opacity0.78, 잔흔2초. smoke_cloud는 연무20개, opacity0.58, 잔흔0초. 수치는 피해 밸런스와 독립적인 제작값이다.
- 사용자 후속 요청으로 화염의 바닥 그림을 제거했다. 지속 화염의 지면 면/그을음과 착탄 화염의 바닥 충격파 링을 만들지 않는다. 불꽃·불티·연기 분포가 대략적인 범위를 전달하며 정확한 판정 경계를 선으로 표시하지 않는다. 불꽃은 활성 시작부터35% 농도로 보이고0.32초 동안 나머지 농도가 차오른다. 일반 연무는 마지막0.6초에 사라진다. `area_ground.gdshader`의 흐름 지면층은 연무 표본에만 사용한다.
- 카드에는 알파 수명 곡선, 무음영 재질, 깊이 기반 Proximity Fade를 적용한다. 입자는 fixed seed6517/SpeedScale0/60Hz로 생성하고 `RequestParticlesProcess`로만 진행한다. 셰이더도 TIME 대신 전달된 clock을 사용하므로 Studio 정지·타임라인과 게임 일시정지에서 벽시계 시간이 흐르지 않는다.
- 자연 종료의 화염은 시각 노드만 같은 Stage.Effects로 이동해2초간 옅은 잔연기만 남긴다. 잔흔은 같은 소유자 아래 최대24개, 넘치면 오래된 장식부터 제거한다. 별도 광원/음향/피해는 추가하지 않는다. 장면이 제거되면 함께 정리된다.
- 연무는2×2아틀라스(각512px), 불꽃은4×4아틀라스(각128px,16프레임의 닫힌 주기), 노이즈는512px2종. 이전 시안의 그을음512px3종은 원본 리소스로 보존하지만 현재 화염에는 로드/표시하지 않는다. 에셋은 `asset_paths.json`의 `vfx.area_*`로 등록하고 `.import`와 밉맵 설정을 함께 관리한다.
- 일반 연무/화염은 벽과의 시각 교차선을 부드럽게 처리하지만 벽 우회 시뮬레이션이나 지형 투영은 하지 않는다. 현재 FireZone의 수평 반경 판정 자체는 바꾸지 않았다.

## 검증 계획

데이터 누락/중복/잘못된 참조/범위 오류, 실제 발생원→프로필 선택, 효과 종료/부모 제거, 위험 반경/수명 일치, Studio 목록/원본/애니메이션/동일 생성기 연결을 검사한다. 실제 창 모드에서 종류별 시간대와 동시 효과 전투를 렌더하고 프레임 시간을 측정한다. 기존 Integration/Destruction 및 Studio media 회귀를 실행한다. 실제2PC·저사양 성능은 실행 여부를 구분한다.

## 실행 결과

- Debug/ExportRelease 빌드 오류·경고0, Godot 가져오기 성공. ExportRelease Compile의 Studio 소스0개 확인. `CombatEffectsCheck`: 헤드리스 **78**, 창 모드 **90**(12장 렌더) 통과. 실제 수류탄·로켓·박격포·드럼통·화염·적 독 발생원, 반경/수명·부모 제거·일회성 한도·움직이며 사격/효과 중첩을 검사했다. 자동 입력 플레이이며 사람이 직접 조작한 장시간 검수는 아니다. 로그 `game/output/vfx-check.log`, `vfx-window.log`, `vfx-*.png`.
- `Integration` **149**, `DestructionCheck` **42**, 최종 `EnemyExpansion` **60** 통과(독 생성/지속 피해/종료 포함, 적 확장 종료 경고 없음). 데이터 연결 뒤 실행한 회귀이며 이후 시각 투명도 수정은 전용 창 검사와 최신 Studio 렌더로 재검증했다.
- Studio `combat-effects.mjs` **68**: 11레코드가 게임 JSON과 일치, 실제 생성기 렌더·전체 수명·프레임 변화·원본 불변. `media.mjs --all` **342**: 23효과·43음원과 잘못된 ID/타입 거부 포함. 로그 `studio/output/combat-final.log`, `combat-media-regression.log`. 최종 셰이더로 서버 재시작 후 전부 재생성했다. `studio/output/combat-final-sheet.jpg`와 효과별 프레임/timing.json은 로컬 산출물.
- 브라우저에서 지속 화염5.20초/79프레임, 0.25배 재생, 1.00초 타임라인 이동, 사용처/조건 및 공통 JSON 펼치기를 확인. 브라우저 경고/오류 로그 없음. 레인지 입력을 fill로 조작하려던 첫 자동화는 실패했으며 AX setValue로 실제 입력 이벤트와16번째 프레임 표시를 확인했다(제품 오류로 판정하지 않음).
- 실제 렌더에서 첫 시안의 달걀 같은 불꽃·규칙적인 바닥 점을 뾰족한 불꽃과 흐르는 잡음으로 수정했다. 최종 부하 렌더에서 연기 투명도 미반영을 발견해 **I-121**로 수정: 실패 검사 `game/output/vfx-opacity-red.log` → 인스턴스 alpha 검사/창 렌더 통과. 최신 `vfx-performance-stress.png`에서 중첩 뒤에도 적의 형태가 보인다.
- 독립 리뷰 2회: 발생원/권한·위험 종료/반경·Studio 공통 생성기·데이터 검증·인스턴스 투명도에 중요한 정적 결함 지적 없음. 리뷰가 지적한 구버전 Studio 캡처는 서버 재시작/전체 재렌더로 교체했다.

### 성능과 남은 제약

최종 `CombatEffectsPerformance`는 RTX5070Ti, Vulkan Forward+, **1600×900**, VSync/프레임 제한 해제. 같은 Stage 2 숲 카메라와 고정된 적20개, 각각6초 중 초기1.5초 제외. 기본→지속 위험6개+초당 폭발10개: 평균 **1.86→2.81ms**, p95 **2.38→3.30ms**, draw calls 약 **861→1,242**. `game/output/vfx-performance.json/log`. 이 값은 현재 장면의 렌더 부하 표본이며 AI/네트워크·저사양·6인/장시간·GPU 단독 처리시간 측정이 아니다. `process_ms` 모니터는 프레임 간격과 측정 구간 의미가 달라 성능 판정에 사용하지 않는다. 초기1600×1000 측정은 투명도 수정 전 자료이므로 최종 수치와 직접 비교하지 않는다.

전용 가속 헤드리스 종료 ObjectDB37개는 AudioStreamWAV7/Playback30, 파괴 상세 재실행25개는 WAV5/Playback20으로 식별했다. 앞선 전용42·파괴23·통합2 경고는 별도 실행이다. 최종 창·부하 렌더는 종료 경고 없음. **I-17 미해결**이며 테스트 통과를 메모리 안전 입증으로 표현하지 않는다. 실제2PC·저사양·장시간 검수, 지형에 밀착 투영하는 데칼은 미실행/범위 밖. 기존 FireZone 바닥 높이 계약을 유지하며 피해 판정은 개편하지 않았다.

### 재실행

저장소 루트 PowerShell 기준:

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --path game --fixed-fps 60 res://tests/CombatEffectsCheck.tscn
& $godot --path game res://tests/CombatEffectsPerformance.tscn
node studio/server.mjs
```

별도 터미널: `node studio/tests/combat-effects.mjs`와 `node studio/tests/media.mjs --all`. 다른 포트를 쓰면 `$env:STUDIO_URL='http://127.0.0.1:4321'`. Studio `이펙트` 메뉴의 폭발·화염·독 분류에서 확인한다. 원본과 테스트 코드만 버전 관리하고 `output/`은 재생성한다.

### 커밋 전 재검증 — 2026-10-24

사용자의 커밋·main 병합·푸시 요청으로 현재 트리를 다시 검사했다. Debug 빌드 오류/경고0, `CombatEffectsCheck`78(종료 경고 없음), `Integration`149 통과. 통합 종료 ObjectDB4개 경고는 이번 실행에서 객체별 식별하지 않았으며 I-17을 유지한다. 로그 `game/output/vfx-commit-check.log`, `vfx-commit-integration.log`. 최초 fetch에서 로컬/원격 main 기준은 `a82c5d8`로 같았다. 생성 산출물과 참고 이미지 첨부 폴더는 커밋에서 제외한다.
