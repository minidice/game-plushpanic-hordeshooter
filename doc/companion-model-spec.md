# 드론·이족 부사수 모델 라이브러리

2026-10-22, A-21. 사용자 승인 구성은 드론 4종과 기존 캐릭터를 축소한 부사수 프리셋 4종, 드론 장비 공용 교체, 고/저품질이다. [제작 계획](model-briefs/drones-sidekicks.md). 이 문서는 시각 모델·프리셋·장착·품질 설정을 다룬다. (2026-10-22 게임 적용 1차 → 맨 끝 "게임 안 동작", M-28) 실제 소환/추종/공격/지원 효과/피해/네트워크는 구현하지 않았으며 M-27/I-85의 남은 범위다. 구체적인 복장·색·무기·60% 크기는 제작안이며 사용자 최종 아트 확정 전이다.

## 드론 에셋

실행용 파일은 `game/assets/drones/`. [build-drones.py](../prototypes/character-lab/tools/build-drones.py)가 Blender에서 생성하고 수정 가능한 `.blend`도 `prototypes/character-lab/output/drones/`에 저장한다. GLB·import 설정·생성기는 버전 관리하고 output은 제외한다. 기존 [펫 생성기](../prototypes/character-lab/tools/build-pets.py)의 조형/내보내기 헬퍼를 재사용하며 import만으로 펫을 다시 만들지 않는다.

| ID | 실루엣/움직임 | 고품질 삼각형 | 저품질 삼각형 | 메시/재질 |
|---|---|---:|---:|---:|
| quadcopter | 보호 링이 있는 로터 4개, 카메라 눈, 착륙 스키드 | 13,392 | 4,012 | 5/7 |
| ladybug | 갈라진 붉은 덮개·점무늬, 더듬이, 옆 로터 2개 | 14,636 | 4,387 | 3/7 |
| bee | 줄무늬 몸체, 날개 흔들기, 뒤 추진구 | 11,356 | 3,402 | 3/7 |
| ufo | 돔·원반·테두리와 회전 표시등 | 10,544 | 3,162 | 2/6 |

고품질 `{id}.glb`, 저품질 `{id}_low.glb`. 저품질은 같은 조형을 약70% 감량한다. 제작 상한은 고품질30,000 / 저품질10,000이며 성능 보장 수치가 아니다. 재질/메시 개수는 같고 전역 그림자나 해상도는 낮추지 않는다.

| 공용 장비 ID | 외형 | 삼각형 | 메시/재질 |
|---|---|---:|---:|
| light | 전방 조명 렌즈 | 396 | 3/5 |
| laser | 냉각 핀과 청록 발광구 | 720 | 3/4 |
| blaster | 작은 쌍열 총신·탄창 | 624 | 3/4 |
| support | 십자 표시 지원 포드·캡슐 | 720 | 3/5 |

장비는 이미 작은 공용 메시이므로 고/저에서 동일 파일을 쓴다. 발광 재질/표시는 외형이며 실제 광원·레이저 피해·지원 능력을 뜻하지 않는다.

## 드론 장착·자세 계약

- 미터, +Y 위, +Z 앞, 루트 배율1. 루트는 **공중 몸체 중심**이며 발바닥 원점이 아니다. 높이는 사용하는 모드가 지정한다.
- `Body` 아래 `EquipmentSocket` 위치 `(0,-0.22,0)`, 회전0/배율1, 규격 `drone_belly_v1`. 장비 루트는 상단 접촉면으로 아래쪽에 매달린다. 기본형 소켓에는 자식 장비가 없다.
- 모든 드론/품질에 추가 위치·회전·크기 보정 없이 장비를 붙인다. 펫의 등 장비 규격 `quadruped_back_v1`과는 다르며 **드론끼리 공용**이다.
- 장비에는 `Yaw → Pitch → Muzzle`, 대표 출력 마커는 Pitch 로컬 `(0,0,0.47)`이고 +Z 앞이다. 조명/지원도 같은 출력 기준점을 제공하지만 실제 사격 동작은 없다.
- `DroneModel.SetEquipment(null)`은 장착판까지 제거한다. 품질 변경은 기존 장비 객체와 조준 상태·루트 변환·현재 비행 자세를 보존한다. 잘못된 ID/품질은 기존 장비를 바꾸기 전에 거부한다.
- `Pose(absoluteSeconds, movingBlend)`로 둥실거림·기울기·로터/날개/테두리 동작을 적용한다. 이동/추적·충돌·소음은 구현하지 않는다. `Body`/움직이는 부품을 직접 조정하면 다음 Pose가 덮어쓴다. 중립 장착 간격은 확인했으며 자유 조준 허용각은 후속이다.

```csharp
var drone = DroneModel.Create("ladybug");
AddChild(drone);
drone.Position = new Vector3(0, 1.5f, 0);
drone.SetEquipment("laser");
drone.Pose(elapsedSeconds, movingBlend);
drone.SetQuality(PetModelQuality.Low);
drone.SetEquipment(null);
```

## 부사수 프리셋

[sidekick_presets.json](../game/content/data/sidekick_presets.json)과 [SidekickModel.cs](../game/scripts/Companions/SidekickModel.cs)를 사용한다. 종 ID를 유지하므로 기존 귀/날개/색/손 동작 경로를 그대로 재사용한다. 플레이어 카탈로그에 새 플레이어 종을 추가하지 않는다.

| 프리셋 | 기존 종 | 복장 | 표시용 무기 |
|---|---|---|---|
| cadet | goose | 청록 베레모·헤드셋·조끼 | m4 |
| scout | duck | 고글·황갈색 조끼·배낭 | glock17 |
| engineer | raccoon | 노란 헬멧·헤드셋·배낭 | m1911 |
| guard | redpanda | 회청색 헬멧·보호 조끼 | r870 |

모두 시각 루트를0.60배로 축소한다. 원본 정의의 `ModelScale`·체력·이동속도·충돌 크기는 변경하지 않는다. `ColliderHeight/ColliderRadius`는 원본×0.60의 **참고 치수**만 노출하고 물리 바디를 만들지 않는다. 총·손·총구도 동일한 시각 루트에 있어 함께 축소된다. 생성마다 별도 AppearanceProfile을 읽고 기존 CharacterRig가 재질을 분리하므로 사용자 외형/다른 인스턴스를 덮어쓰지 않는다.

```csharp
var buddy = SidekickModel.Create("engineer");
AddChild(buddy);
buddy.Animate(delta, movingBlend); // 기존 걷기와 무기 잡기
```

위 무기/이름은 모델 프리뷰용 기본 구성이다. 직업 능력·능력치·장비 구매/선택 화면·추종 AI를 추가한 것은 아니다. 장비 교체 API는 이번 드론에 제공하며 부사수의 전투 로드아웃은 후속이다.

## 공용 품질 설정

설정 표시명은 **소환수 모델 품질**(고품질/저품질). 기존 `GameSettings.PetQuality`, `PetModelQuality` enum, `[graphics] pet_quality` 0/1 저장 키와 `settings_pet_quality` 번역 키를 유지해 이전 설정을 그대로 읽는다. 의미를 펫·드론·부사수까지 확장했다.

- 펫/드론: 별도 고/저 메시. 드론 High `LodBias=4`, Low=1.
- 부사수: 원본 캐릭터 GLB를 공유하고 **자동 메시 LOD 편향**만 High4 / Low0.5로 변경한다. 별도 저품질 GLB가 아니다. 원본에 LOD가 없는 작은 부품/절차적 손은 같은 메시로 남는다. 화면 크기에 따른 감소이므로 고정 감량률을 보장하지 않는다.
- 품질 인자를 생략해 생성한 모델은 트리 진입 때 현재 설정을 적용하고 활성 중 설정 변경도 따른다. 트리 이탈 때 구독 해제. 명시적 품질 인자는 설정과 독립적인 프리뷰에 사용한다. 직접 SetQuality 호출은 구독 정책을 변경하지 않는다.

## 생성·검사·미리보기

저장소 루트, 설치된 실행 파일 경로:

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/character-lab/tools/build-drones.py
& 'D:/Util/Python314/python.exe' prototypes/character-lab/tools/validate-drones.py
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path game --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game res://tests/CompanionModelCheck.tscn
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game res://tests/CaptureCompanions.tscn -- --motion
```

Blender 끝에 `-- ufo`처럼 ID를 주면 부분 재생성. 캡처 `--motion`을 생략하면 정적 이미지, `--perf` / `--perf --low`는 6드론 단독 측정. 결과는 `game/output/companions/`, 로그는 `game/output/companion-*.log`다. 비교는 Godot 실제 렌더를 사용하며 컨셉 이미지가 아니다.

2026-10-22 검사: 12GLB 예산/피벗/소켓 통과, 빌드 경고/오류0, CompanionModelCheck253·PetModelCheck185·MenuCheck44 통과. 드론 고/저4방향·4장비 교차 장착·48프레임 비행, 부사수4방향·고/저·48프레임 걷기 및 설정 메뉴 렌더 확인. 전용 검사/가져오기/캡처 종료 경고 없음. 기존 전투/네트워크 코드를 바꾸지 않아 통합/멀티 검사는 이번 작업에서 반복하지 않았다. Blender `Material.use_nodes`의6.0 제거 예정 경고는 기존처럼 남으며 생성은 성공했다.

독립 리뷰에서 기능 결함은 발견되지 않았고, 지적된 프리셋 격리 검사 공백은 동시 인스턴스/기존·이후 플레이어 색 보존 검사로 보완했다. 초기 오프 트리 생성에서 손 연결이 계산되지 않는 문제는 Ready에서 연결을 갱신해 해결했다. 저품질 UFO 테두리와 본체 간섭은 테두리를 바깥으로 조정해 렌더 재검증했다.

### 6드론 단독 측정

RTX5070Ti, 1600×1000, Forward+, 그림자, VSync 해제, 준비90프레임+4초. 쿼드/무당벌레 각2·꿀벌/UFO 각1, 모두 blaster. 고품질 평균/P95 **0.643/1.022ms**, 저품질 **0.626/0.923ms**. 마지막 프레임 렌더 프리미티브100,038/33,292, 드로 콜120/117. 전체 프레임 간격이며 GPU 시간만의 측정이 아니다. 자동 LOD와 그림자 영향 포함. 시간 차이가 작으므로 삼각형 감량과 FPS 개선을 동일시하지 않는다. 부사수6개·실제 저사양·전투/멀티 부하는 미검증이다.

## 게임 안 동작 (2026-10-22, M-28)

난전과 카드 실험실에서 쓰는 실제 소환수. `game/scripts/Companions/Companion.cs`(한 마리) · `CompanionSquad.cs`(판 전체 · 카드 · 네트워크). 시각 모델은 위 라이브러리(`PetModel` · `DroneModel` · `SidekickModel`)를 그대로 쓴다. 규칙은 사용자 지시, 수치는 임시.

| | 펫(사족) | 드론 | 부사수(이족) |
|---|---|---|---|
| 키 | `pet:<dog·turtle·rabbit>:<twin_blaster·rocket_pod>` | `drone:<quadcopter·ladybug·bee·ufo>:<blaster·laser·support·light>` | `sidekick:<cadet·scout·engineer·guard>` |
| 크기 | 0.6배(땅) | 0.4배, 높이 2.4 m | 프리셋 0.6배(땅) |
| 몸 · 피해 | 몸 없음, 무적 | 몸 없음, 무적 | 몸 없음. 몸에 닿은 몹(0.6 m + 몹 반지름)이 0.5초마다 공격력 × 50% 비율로 깎음, 체력 60 → 쓰러짐(솜) → 8초 뒤 생존자 곁에서 부활 |
| 공격 | 쌍열 블래스터 7 / 0.35초 · 로켓 포드 0.45초 조준 → 반경 2.6 m 45 | 블래스터 5 / 0.3초 · 레이저 0.9초 조준 → 16 m 관통 70 · 지원 포드 생존자 4 HP / 2.5초 · 조명 빛만 | 자기 총 피해 × 50%, 간격 × 1.6 |

- **따라가기**: 생존자 주위 자리(뒤 · 양옆, 드론은 위)를 따라감. 땅 소환수는 싸울 때 생존자에게서 최대 6 m까지만 다가감. 생존자와 **12 m** 넘게 벌어지면 공격을 멈추고 돌아오며(1.35배 빠르게) **5 m** 안에 들어오면 다시 싸움, **28 m** 넘으면 순간 이동. 이미 시작한 조준(레이저 · 로켓)은 끝까지 쏜 뒤 돌아옴.
- **몸이 없음**: 물리 몸 · 충돌 모양이 없어 몹 · 총알 · 생존자 이동을 막지 않고, 몹의 목표도 아님(`CompanionCheck`가 충돌 노드 없음을 확인).
- **움직임**: 펫 걸음은 이동 거리만큼 돌아 발이 덜 미끄러지고 서면 멈춤, 드론은 둥실 · 로터 · 날갯짓(0.4배에서 보이도록 폭 0.55 rad · 30 Hz), 부사수는 캐릭터 걷기 자세.
- **명중**: 생존자 이름으로 맞혀(`HitInfo.Shooter`) 생존자의 카드 효과(풍선탄 등)가 소환수 공격에도 걸림. 무기 분류는 `companion`이라 당구는 안 걸림.
- **처음부터 기본 공격**(사용자 확정 2026-10-22, M-28): 따라만 다니는 단계 없이 얻자마자 싸운다. 소환수 카드는 이 공격을 강하게 하거나 새 능력을 더하는 쪽.
- **얻는 법**: 난전은 무장 네 번째 카드(펫 · 드론 · 부사수 하나씩 3장, 모양 · 장비 무작위, 장비 중 조명은 카드에 안 나옴). 실험실은 C로 차례로 추가, Shift+C로 치우기, Home 치트 초기화 때 무장 카드에 포함.
- **품질**: 설정 → 소환수 모델 품질, 그리고 **게임 중 Esc 메뉴의 버튼**으로 바꾸면 펫 · 드론은 고/저 GLB가, 부사수는 LOD 편향이 바로 바뀜(장비 유지). 나중에 다른 모델(네임드 · 보스 고/저, [PC 사양 목표](performance-targets.md))도 같은 설정 이벤트(`GameSettings.PetQualityChanged`)를 구독하면 된다.
- **멀티**: 방장이 소환수를 만들고 피해 · 쓰러짐을 정함. 파티원은 카드 동기화에 붙은 목록(생존자 · 키 · 쓰러짐)을 받아 자기 화면에 같은 소환수를 만들고 움직임 · 사격 연출은 스스로 계산(`OnslaughtNetCheck`).
- 검사: `tests/CompanionCheck.tscn`(키 · 카드, 따라가기 · 멈춤, 몸 없음 · 무적, 종류별 공격, 벗어나면 멈추고 복귀, 레이저 조준 끝까지, 부사수 쓰러짐 · 부활, 게임 중 품질 전환 · Esc 버튼, 목록 왕복), 창 모드 렌더 `output/companion-follow.png` · `companion-fight.png`. 한계 issues.md I-89.
