# Horde Shooter (가칭) — Stage 1 싱글 플레이 프로토타입

Godot **4.7.2 .NET (C#) + Jolt**. 기획 기준은 [`doc/idea.md`](../doc/idea.md)입니다. 캐릭터 추가·교체는 [`doc/character-model-spec.md`](../doc/character-model-spec.md), 무기는 [`doc/weapon-spec.md`](../doc/weapon-spec.md), 투척물·몬스터는 [`doc/throwable-monster-spec.md`](../doc/throwable-monster-spec.md), 다국어는 [`doc/localization.md`](../doc/localization.md), 효과음은 [`doc/sound-spec.md`](../doc/sound-spec.md)를 보세요.

## 실행

현재 기능 확인은 [`기능 체크리스트`](../doc/feature-checklist.md), 진행 방향은 [`레벨 디자인 규칙`](../doc/level-design-rules.md), 최신 탄창·재장전 수치는 [`전투 템포 조사`](../doc/combat-pacing-research.md)에 정리합니다.

`Start-Game.cmd`를 더블클릭하면 C# 빌드, 에셋 가져오기 후 게임 창이 열립니다.

```powershell
./Start-Game.ps1              # 플레이
./Start-Game.ps1 -Test        # 헤드리스 통합 검사 147개 → output/integration-report.json, 효과음 44종 → output/sounds/*.wav
./Start-Game.ps1 -InputCheck  # 창 모드 입력 경로 검사 14개 (이동·조준·E·1/2·사격·R·G 조준/우클릭 취소/클릭 투척·Space·Esc)
./Start-Game.ps1 -Capture     # 장면 10개 렌더 → output/*.png
./Start-Game.ps1 -NetPair     # 멀티 2인 테스트: 한 PC에서 방장 창 + 참가 창 (Start-Multi-Test.cmd와 같음)
./Start-Game.ps1 -NetHost     # 멀티 2인 테스트: 방장 창 (UDP 24680)
./Start-Game.ps1 -NetJoin 127.0.0.1  # 멀티 2인 테스트: 파티원 창 (같은 PC면 127.0.0.1)
./Start-Game.ps1 -GodotDir 'C:/다른/경로'     # Godot 위치가 다를 때
./Export-Game.ps1             # 다른 PC용 실행 파일 → ../build/windows/, ../build/HordeShooter-windows.zip (Export-Game.cmd와 같음)
```

다른 PC에서 실행: `build/HordeShooter-windows.zip`을 풀고 `HordeShooter.exe` 실행(Godot·.NET 설치 불필요). 처음 내보낼 때 Godot 4.7.2 .NET 내보내기 템플릿이 필요합니다(에디터 → 내보내기 템플릿 관리, `%APPDATA%\Godot\export_templates\4.7.2.stable.mono`).

필요 환경: Godot 4.7.2 .NET(기본 경로 `D:\Util\Godot\Godot_v4.7.2-stable_mono_win64`), .NET SDK 8 이상. 에디터에서 열려면 Godot .NET 에디터로 `game/project.godot`를 가져옵니다.

## 조작

| 입력 | 동작 |
|---|---|
| WASD | 이동 |
| 마우스 | 조준. 적 위에 0.5초 고정하면 노란 원이 차고, 그 적에게 치명타(기본 ×2.5, 저격은 더 큼) |
| 좌클릭 | 사격/휘두르기. 자동 무기는 누르고 있으면 연사, 그 외는 클릭마다. **쏠 때마다 반동으로 뒤로 밀림**(권총 미세, 샷건 약 1m) |
| **1 / 2 / 3** | 무기 교체. 1번 = 근접·권총(탄약 무제한), 2번 = 주무기, 3번 = 특수무기(로켓런처·박격포·레일건). 무기마다 이동속도가 다름(기관단총 빠름, 경기관총·저격 느림) |
| **우클릭 / Ctrl** (누르고 있기) | 조준. 낮은 자세, 사거리↑, 퍼짐↓, 집중이 빨리 참, 카메라가 커서 쪽으로, 대신 천천히 걸음. 박격포는 들고 있으면 착탄 원이 보이고 클릭하면 거치 후 발사 |
| **G** | **투척 조준.** 착탄 범위·최대 거리·궤적이 보임, 조준 중 이동 -50%. **좌클릭으로 던지면 직전 무기로 복귀**, 우클릭/G 취소, 휠로 종류 변경 (기본은 마지막에 주운 종류) |
| **Space** | 낮은 전방 도약. 쿨다운 5초. 도약 중 무적이고 적 사이를 빠져나감 |
| **F8** | 붙잡힘 즉시 해제. 메뉴의 테스트 치트가 켜져 있을 때만 가능 |
| R | 재장전. R870·Benelli는 한 발씩 넣으며, 좌클릭으로 중단하고 삽입된 탄을 발사 가능 |
| E | 줍기·상호작용 (바닥 무기, 문, 제어판, 탄약, 구급상자) |
| Q | 구급상자 사용 (2.6초, 잃은 체력의 80% 회복) |
| Tab | 카메라 확대/축소 |
| Esc | 일시정지 (Enter 재시작, M 메뉴) |

## 지형·전투 HUD 아트 패스 (2026-10-20)

Stage 1은 타일 줄눈과 청록색 벽 패널, 모서리를 깎은 금속 상자, 선반·종이 상자, 보관함·작업대·단말기와 구역 조명을 사용합니다. 장식 메시와 재질은 `scripts/World/StageArt.cs`, 구역 배치는 `StageOneLayout.cs`, 조명은 `Stage.cs`에서 조정합니다. 기존 충돌체·길찾기·아이템·상호작용 위치를 유지합니다.

카메라는 수직 55° / 수평 -30° 직교 뷰입니다. WASD는 화면 방향을 따르고 Tab 확대를 지원합니다. 하단 HUD에는 실제 장비 4칸(1/2/G/Q), 체력, 도약 대기시간, 현재 탄약을 표시합니다. 무기 16종의 투명 아이콘은 기존 모델을 렌더한 PNG이며 CSV의 `icon` 경로로 연결됩니다. 아이콘 재생성 스크립트는 `../prototypes/character-lab/tools/render-weapon-icons.py`입니다.

메인 메뉴·설정 검사는 `res://tests/MenuCheck.tscn`(24개)입니다. 창 모드로 실행하면 `output/menu-*.png`도 저장합니다. 설정은 `user://settings.cfg`에 저장되며, 검사는 별도 파일을 써서 실제 설정을 건드리지 않습니다.

빌드·가져오기 후 `res://tests/CaptureArtDirection.tscn`을 창 모드로 실행하면 세이프룸·창고·확대·거리·1280×720 화면을 `output/art-*.png`로 저장하고 카메라 방향, HUD 영역, 아이콘과 상태 표시를 검사합니다. 실제 렌더 기준으로 확인하는 1차 환경 아트이며, 개별 소품의 최종 조형과 대규모 전투 성능 최적화는 이후 작업입니다.

## 외형·적 확장 (2026-10-19)

메뉴의 **커스터마이징**에서 거위·오리·라쿤·랫서판다를 고르고 몸 색조, 부리, 모자/조끼/배낭 색상을 변경합니다. 몸 무늬는 유지됩니다. 모자는 없음/헬멧/베레모, 조끼와 배낭은 표시 여부를 선택하며 능력치에는 영향이 없습니다. 외형은 `user://appearance.cfg`에 종별로 저장됩니다.

감염체/야생 동물/무장 동물 선택은 현재 Stage 1의 적 구성을 바꿉니다. 별도 맵 3개가 아닙니다. 감염체에는 인간형, 기어오는 적, 끌개, 덮치는 적, 토사물 적과 기존 특수 적이 섞입니다. 무장 동물은 예고 조준 후 발사하며 엄폐물에 막힙니다.

붙잡힘은 시간이 지나거나 일반 행동키를 눌러도 풀리지 않습니다. 현재 동료 플레이가 없으므로 **테스트 치트가 기본 켜짐**이며 **F8**로 해제하고 해당 적을 2초 기절시킵니다. 메뉴에서 치트를 끄면 자력 해제가 막힙니다. 적 처치/기절 또는 구속 연결이 장애물에 막히면 해제됩니다. 향후 동료 구조가 준비되면 `GameSession.DebugEscapeEnabled` 기본값을 false로 바꿀 수 있습니다.

총기 15종은 별도 GLB가 연결되어 있으며 일부 변형은 같은 기본 조형을 공유합니다. 캐릭터/적과 함께 첫 게임 적용용 모델이며 최종 아트/LOD 및 멀티플레이는 별도입니다.

추가 검사는 빌드·에셋 가져오기 후 Godot으로 `res://tests/AppearanceExpansion.tscn`, `res://tests/EnemyExpansion.tscn`을 `--headless` 실행합니다. 실제 렌더는 `res://tests/CaptureExpansion.tscn`이며 `output/expansion-*.png`에 저장합니다. 계획과 범위는 [`doc/character-enemy-expansion-plan.md`](../doc/character-enemy-expansion-plan.md)를 참고하세요.
## 장비 체계 (L4D식 4칸)

| 칸 | 내용 | 규칙 |
|---|---|---|
| **1번** | 프라이팬 ↔ 권총(M1911, 글록 17) | 시작은 프라이팬. 권총을 주우면 교체되고 프라이팬은 바닥에 떨어짐. **같은 권총을 하나 더 주우면 쌍권총**(쌍 M1911). 다른 권총이면 교체. 탄약 무제한(탄창 재장전은 있음) |
| **2번** | 소총·산탄총·저격총 3종씩 + 로켓런처, 박격포 | 새 무기를 주우면 들고 있던 무기가 **탄약 그대로** 바닥에 떨어짐. 같은 무기를 또 주우면 탄약만 추가 |
| **G** | 수류탄, 화염병, 섬광탄, 삑삑이 유인 폭탄 | 종류별 최대 2개. 수치·효과는 `content/data/throwables.csv` + `throwable_effects/*.json` |
| **Q** | 구급상자 | 1개 |

**탄약 더미는 2번 무기만 채웁니다.** 로켓런처와 박격포는 채워지지 않습니다.

### 무기 목록

`content/data/weapons.csv`에서 뽑은 값입니다. 빈 칸은 `weapon_types.csv`의 분류 기본값을 씁니다. 규격은 [`doc/weapon-spec.md`](../doc/weapon-spec.md)에 있습니다.

| 칸 | 분류 | 무기 (id) | 대미지 | 사거리 유효/최대 | 탄창 / 최대 예비 | 재장전 | 반동 파워 | 특성 |
|---|---|---|---|---|---|---|---|---|
| 1 | 근접 무기 | 프라이팬 (`frying_pan`) | 34 | 1.9/1.9m | — | — | 0 | 부채꼴 120° 최대 4 |
| 1 | 권총 | M1911 (`m1911`) | 5 | 10/20m | 15 / 무제한 | 1.5초 | 8 | 두 자루 → 쌍 |
| 1 | 권총 | 글록 17 (`glock17`) | 4 | 10/20m | 17 / 무제한 | 1.5초 | 6 | 두 자루 → 쌍 |
| 2 | 기관단총 | MP5 (`mp5`) | 6 | 30/40m | 50 / 650 | 2.2초 | 4 | 연사 |
| 2 | 돌격소총 | M4 (`m4`) | 11 | 30/40m | 50 / 500 | 2.2초 | 7 | 연사 |
| 2 | 돌격소총 | M16 (`m16`) | 12 | 30/40m | 60 / 480 | 2.4초 | 7 | 3점사 |
| 2 | 돌격소총 | AK47 (`ak47`) | 7 | 30/40m | 40 / 480 | 2.3초 | 7 | 연사 |
| 2 | 경기관총 | M249 (`m249`) | 11 | 28/40m | 100 / 300 | 3초 | 9 | 연사 |
| 2 | 샷건 | 레밍턴 870 (`r870`) | 30 (산탄 9발 합계) | 10/20m | 10 / 100 | 첫 발 0.55초 + 발당 0.38초 | 60 |  |
| 2 | 샷건 | 베넬리 M4 (`benelli_m4`) | 26 (산탄 9발 합계) | 10/20m | 10 / 90 | 첫 발 0.5초 + 발당 0.32초 | 40 | 연사 |
| 2 | 샷건 | 더블배럴 (`double_barrel`) | 40 (산탄 14발 합계) | 10/20m | 2 / 60 | 1.3초 | 90 |  |
| 2 | 볼트액션 저격총 | AWM (`awm`) | 100 | 60/80m | 10 / 200 | 2초 | 50 | 관통 4 |
| 2 | 반자동 저격총 | SVD (`svd`) | 48 | 45/60m | 15 / 90 | 2.6초 | 20 | 관통 2 |
| 2 | 볼트액션 저격총 | 바렛 M82 (`m82`) | 220 | 60/80m | 5 / 30 | 2.5초 | 80 | 관통 무제한 |
| 2 | 로켓런처 | 알라봉 (`alabong`) | 100 | 40/40m | 1 / 5 | 3.2초 | 100 | 폭발 170 / 4.8m, **보충 불가** |
| 2 | 박격포 | 박격포 (`mortar`) | 0 | 32/32m | 1 / 6 | 1.6초 | 25 | 곡사 1.3초, 최소 6m, 폭발 190 / 5.2m, **보충 불가** |

### 투척물

| 투척물 | 효과 |
|---|---|
| 수류탄 | 던지고 2.2초 뒤 폭발 (반경 4.6m, 피해 160) |
| 화염병 | 깨지는 자리에 8초간 불바다 (반경 3.4m). 좀비는 초당 45, 들어가면 나도 초당 9 피해 |
| 섬광탄 | 1.6초 뒤 반경 8m 좀비를 4초간 기절. **바라보고 있으면 화면이 하얘짐** |
| 삑삑이 유인 폭탄 | 6초간 삑삑거리며 반경 30m 일반 좀비를 끌어모은 뒤 폭발 (반경 5m, 피해 220). 특수 감염체는 안 넘어감 |

### 바닥에 놓인 무기 위치

| 구역 | 무기 |
|---|---|
| 세이프룸 (시작) | 권총, 돌격소총, 펌프 산탄총, 볼트액션 저격총, 수류탄, 화염병, 구급상자, 탄약 |
| 거리 | 권총(→ 쌍권총), 점사 소총, 섬광탄, 탄약 |
| 창고 | 자동 산탄총, 반자동 저격총, 경기관총, 더블배럴, **로켓런처(셔터 제어판 옆)**, 수류탄, 유인 폭탄, 구급상자, 탄약 |
| 야적장 | 박격포, 대물 저격총, 펌프 산탄총, 화염병, 섬광탄 |

바닥 무기는 고리 색으로 구분합니다. 파랑은 1번, 주황은 2번, 초록은 투척물입니다. 가까이 가면 이름이 뜹니다.

## 사운드

음원 파일 없이 효과음 44종을 코드로 합성합니다(`scripts/Audio/`). `content/sounds/<id>.ogg`를 넣으면 그 소리로 교체됩니다.

- **듣는 위치는 거위**입니다. 등 뒤 좀비 발소리는 뒤에서, 보이지 않는 적의 소나음은 그쪽 방향에서 들립니다.
- 총 종류별 발사음, 장전 딸깍, 프라이팬 "뎅", 빈 탄창.
- 발소리(거위는 뒤뚱 박자, 좀비는 질질 끄는 소리), 좀비 신음, 소나 "퐁", 유인 폭탄 삑삑(점점 높아짐), 박격포 휘파람.
- 피격 "꽥", 쓰러질 때 긴 "꽥" + "빠밤빠밤…" 게임 오버. 클리어 팡파르. 경보 사이렌, 밤바람.
- **헬륨 중독 중에는 모든 효과음이 높아지고, 섬광탄 실명 중에는 소리가 먹먹해집니다.**

ID와 발생 시점 목록, 교체 방법은 [`doc/sound-spec.md`](../doc/sound-spec.md)에 있습니다.

## 스테이지 1 "창고 지구" 흐름

```
세이프룸 ─[E 문 열기]→ 거리 ─→ 창고 ─[E 셔터 제어판: 경보 50초 크레센도]→ 야적장 ─→ 세이프하우스 [E 문 닫기 = 클리어]
```

- 출발 후 AI 디렉터가 작동합니다. 30~45초 뒤 첫 호드가 오고, 이후 45~70초 간격입니다. 특수 감염체는 30~45초마다 나옵니다.
- 셔터 제어판을 누르면 50초 동안 6~7초마다 호드가 몰려오고, 특수 감염체 2마리가 섞입니다. 버티면 셔터가 열립니다.
- 세이프하우스 안에 적이 남아 있으면 문이 닫히지 않습니다.
- **일반 좀비는 선택한 캐릭터와 같은 종**에서 장비(헬멧·조끼·배낭·총)를 뗀 모습입니다. 초록빛을 띠고, 몸을 숙인 채 팔을 앞으로 뻗습니다. 거위로 하면 좀비 거위가 나옵니다.

## 구현된 기획 요소 (idea.md 대응)

| 기획 | 구현 | 위치 |
|---|---|---|
| 5.1 캠페인 루프 | 세이프룸 → 구간 → 크레센도 → 세이프하우스 | `World/Stage.cs`, `World/StageOneLayout.cs` |
| 5.3① 부채꼴 시야 | 114° 시야 + 벽 가림. 적은 보임 / 실루엣 / 숨김 3단계: 원뿔·2.6m·아군 손전등이 비추면 보임, 원뿔 밖 7m·가로등 빛 안·나를 공격 중이면 실루엣, 그 밖은 숨김(움직이면 붉은 소나 파문). 맞으면 맞은 방향 빨간 호 | `Player/VisionSystem.cs`, `Characters/CharacterSilhouette.cs` |
| 5.3② 정밀 사격 | 블룸, 0.5초 조준 고정 치명타 + 조준 보정, 거리별 대미지 감쇠, 사격 반동 넉백 | `Weapons/WeaponState.cs`, `Weapons/WeaponController.cs` |
| 5.3③ 큐트 고어 | 솜 파편, 페인트 자국, 시체 더미(일반 100구 목표·화면 보호, 네임드 별도 5분, 정지하면 고정) | `World/Effects.cs`, `World/Corpse.cs` |
| 5.5② 슬랩스틱 | 구르기, 반동에 밀리는 더블배럴·대물 저격총·로켓런처, 내 폭발에 내가 날아감, 섬광탄 자폭 | `Player/Player.cs`, `World/Blast.cs` |
| 5.5③ 특수 감염체 | 풍선 기생충(헬륨: 시야 축소 + 호드 소환), 박치기 황소(돌진·볼링핀·벽 충돌 시 기절) | `Enemies/BalloonEnemy.cs`, `Enemies/BullEnemy.cs` |
| 5.4 지형지물 | 폭발 드럼통(연쇄 폭발, 본인도 날아감), 밀리는 상자 | `World/ExplosiveBarrel.cs` |
| 7.4 관절 없는 3D 물리 | 절차적 뒤뚱거림, 보블헤드 스프링, 피격 스쿼시, 볼링핀 사망 | `Characters/CharacterRig.cs` |
| 8.2 물리 이원화 | 시체는 로컬 물리 전용, 판정과 방향만 게임 로직에 반영 | `Enemies/Enemy.cs` → `Effects.SpawnCorpse` |
| 8.4 호드 최적화 | 공유 플로우 필드(다익스트라 격자) + 공간 해시 분리 조향. 유인 폭탄은 두 번째 플로우 필드 사용 | `Enemies/FlowField.cs`, `Enemies/CrowdGrid.cs` |
| L4D AI 디렉터 | 시야 밖·도달 가능한 지점에서만 스폰, 긴장도가 높으면 호드 지연 | `Enemies/HordeDirector.cs` |
| 모델·무기 교체 구조 | 캐릭터·무기·투척물 모두 `.tres` 데이터 + GLB 또는 도형 대체 모델 | `Characters/*`, `Weapons/*`, `content/*` |

## 튜닝 수치 (한곳에 모음)

| 항목 | 값 | 파일 |
|---|---|---|
| 플레이어 | 속도 4.4, 체력 100 | `content/characters/*.tres` |
| 무기 | 위 무기 목록 표 | `content/data/weapon_types.csv`, `weapons.csv`, `weapon_traits/*.json` |
| 투척물 | 위 투척물 표 | `content/throwables/*.tres` |
| 구르기 | 0.42초, 초속 10m, 쿨다운 5초 | `Player/Player.cs` 상단 상수 |
| 반동 넉백 | 속도 = 파워 × 0.08m/s × `GameSettings.RecoilScale` | `Weapons/WeaponDefinition.cs` (`Knockback`) |
| 시야 | 반각 57°(헬륨 30°), 거리 24m, 근접 감지 2.6m | `Player/VisionSystem.cs` |
| 몬스터 | 체력·속도·**무게**·공격 (좀비 30/4.5/1, 풍선 25/2.1/0.7, 황소 260/3.4/5) | `content/data/monsters.csv` |
| 특수 감염체 행동 | 풍선 터짐(반경 4.2m, 헬륨 15초, 12마리 소환), 황소 돌진(예고 0.9초, 12.5m/s, 벽 충돌 시 1.9초 기절), 적 사격탄 40m/s(정예 48m/s, 가는 구적탄), 토사물 10m/s | `content/data/monster_traits/*.json` |
| 피격 넉백 | 속도 = 피격 파워 × 0.08 ÷ 무게, 멈칫 0.05~0.35초 (소총은 멈칫만, 샷건·폭발은 밀림) | `Enemies/Enemy.cs` (`ApplyKnock`) |
| 디렉터 | 일반 적 최대 70, 특수 최대 2, 크레센도 50초 | `Enemies/HordeDirector.cs`, `World/StageOneLayout.cs` |
| 몬스터 등장·반응 | 연쇄 깨움 6m, 경계형 시야 14m, 지나온 길 15~40m 뒤, 출입구 보호 8m, 차 경보 20초(6초마다 6~9마리), 소음 폭발 30·화염 18·풍선 20·섬광 25·경보 60m | `content/data/spawn_rules.json` |
| 보급 지점 | Stage 1: 시작 정비, 주요 2(창고·야적장), 선택 1(거리), 이벤트 보상 1(셔터 제어판), 도착 1 | `content/data/stages/stage1_supplies.json` |
| 구역·미리 배치·등장 지점 | 스테이지 1의 5구역, 구역별 대기형·경계형 무리, 숨은 자리 22곳·벽 위 7곳 | `content/data/stages/stage1_encounters.json` |
| 무기 소음 | 근접 4, 권총 14, SMG 16, 소총 20, 산탄·LMG 22, DMR 24, 저격 26, 로켓·박격포 30m | `content/data/weapon_types.csv` `noise_m` |

## 폴더 구조

```
game/
  content/characters/   플레이어 정의 (.tres) — 추가하면 메뉴에 자동 표시
  content/enemies/      적 외형 (.tres) — 능력치는 monsters.csv
  content/data/         weapon_types.csv · weapons.csv · weapon_traits/   무기
                        throwables.csv · throwable_effects/                투척물
                        monsters.csv · monster_traits/                     몬스터 능력치(무게 포함)
                        spawn_rules.json · stages/                         몬스터 등장·반응·경보, 보급 지점
  content/lan/kor|eng/  이름 문구 (weapon.csv, throwable.csv, monster.csv, character.csv)
  content/throwables/   투척물 정의 (.tres) 4종
  assets/characters/    GLB 모델
  scenes/               Menu.tscn(시작), Stage1.tscn
  scripts/Core|Audio|Characters|Player|Weapons|Enemies|World|UI
  tests/                Integration(헤드리스 검사), InputCheck(창 모드 입력), Capture(렌더 캡처), MenuCheck(메뉴·설정), SquadCheck(AI 분대·부활), SpawnCheck(몬스터 등장·반응·경보), SupplyCheck(보급 배치), SupplyCapture(보급 지점 전후 렌더), AudioLevelCheck(효과음 레시피 크기), AudioMixCheck(창 모드 실제 출력 녹음), CaptureEnemyShots(적 탄 렌더), VisionTierCheck(보임·실루엣·숨김), NetCodecCheck·NetSyncCheck(멀티 동기화) 외 전용 검사
```

## 아직 없는 것

- 멀티플레이(메뉴에 "준비 중" 페이지만 있음, 계획은 `doc/menu-multiplayer-plan.md`), AI 분대원(메뉴 선택만 저장), 근접 보이스챗
- 음악, 키 재설정. 효과음은 합성음이라 실제 녹음보다 단순합니다.
- 인형 뽑기 손(스모커 포지션), 쓰러짐 후 구조, 아군 오사(싱글이라 해당 없음)
- 무기 모델은 도형 조합입니다(`WeaponDefinition.Model`에 GLB를 넣으면 교체). 특수 감염체도 도형 대체 모델입니다.
- 장비 드롭 물리, 표정 텍스처 교체, 키 재설정 UI, 세이브
- 검증 범위: 입력 검사는 입력 액션 이벤트와 마우스 위치를 주입하는 방식이라, 실제 물리 키보드로 끝까지 플레이한 검증은 아닙니다. 무기 밸런스와 체감은 사람이 플레이해서 조정해야 합니다.

시체 정책과 실제 성능 측정: [시체 유지·정리](../doc/corpse-retention.md). 전용 검사 `CorpseRetentionCheck.tscn`·`CorpseTierCheck.tscn`, 렌더 측정 `CorpsePerformance.tscn`은 통합 검사와 별도로 실행합니다.

시체 수명은 `content/data/corpse_retention.json`의 `named.lifetime_seconds`(기본 300초), 몬스터 등급은 `content/data/monsters.csv`의 `tier`에서 지정합니다. 네임드 루팅은 아직 구현하지 않았습니다.
