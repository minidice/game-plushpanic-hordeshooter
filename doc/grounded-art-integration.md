# 독립 캐릭터 후보의 게임 적용

2026-10-23 사용자 게임 적용 요청. A-4·A-5·A-8·A-10·A-11, I-29·I-30·I-35·I-53 관련.

## 적용 범위와 사용

- 네 종 카탈로그가 `game/assets/characters/{species}/{species}-fitted.glb`를 사용한다. 기존 원본 GLB와 독립 후보는 보존한다.
- 기본 조끼(`Vest=true`, `VestStyle=0`)를 옆구리까지 감싸는 후보로 바꿨다. 거위 핏은 유지하고 다른 종은 앞·뒤 목선을 최대 0.12 m 낮춘다. 팔 구멍과 밑단은 유지한다. 기존 패딩 조끼·모자·배낭 선택은 유지한다.
- 네 종의 맨팔은 접힌 날개/둥근 앞발로 교체했다. 무기를 들면 같은 원본 표면을 손잡이 방향으로 굽힌다. 오리·거위는 깃털 끝, 라쿤·랫서판다는 짧은 발끝 형태다. 기존 구형 손은 새 표면을 가리므로 네 종의 무기 장착 시 숨긴다.
- 라쿤·랫서판다 기본 귀·꼬리는 후보의 연결된 표면을 사용한다. 다른 귀·꼬리 모양, 색·크기, 모자 착용 시 귀 접힘과 복제 좀비는 기존 기능을 유지한다.
- 실행은 기존 `game/Start-Game.ps1`. 저장된 외형은 유지되므로 새 조끼를 보려면 작업실에서 **조끼 켜기 + 기본형**을 선택한다. 귀·꼬리도 기본형에서 새 메시가 보인다.

이번에 이전 모자/복장 후보 전체를 새로운 선택지로 등록한 것은 아니다. 기본 조끼와 네 종의 귀·꼬리·팔을 우선 연결했다.

## 장착·변형 계약

게임은 기존 Body/Deform/Head/Weapon 및 종별 피벗 이름을 유지한다. 편집용 `FitRoot`를 추가하지 않고 기존 `Model.Scale=(폭,키,폭)×ModelScale`을 사용해 몸·옷·무기·팔이 함께 변한다. 콜라이더와 능력치는 유지한다.

`CharacterGripSurface`는 최초 장착 시 원본 피벗에서 정적인 날개/앞발 메시를 하나씩 만들고, 이후 GPU에서 삼차 곡선으로 굽힌다. 매 프레임 메시 재생성은 하지 않는다. 체형 편집 후 최초 장착도 원본 변환을 사용하므로 `WingSize`가 두 번 적용되지 않는다. 기본 한손 총 기준점은 `(0.50,0.96,0.28)`에서 후보에 맞춘 `(0.54,1.22,0.65)`로 조정했다. 양손/중화기·근접·재장전은 기존 위치 경로를 사용하고 공통 표면이 손잡이를 따른다. 사격 수치와 판정 규칙은 그대로지만 실제 총구 위치는 새 장착 위치를 따른다.

변형 함수는 `character_grip_deform.gdshaderinc`로 공유한다. 일반 표면·피격 표시·실루엣이 같은 메시 인스턴스 매개변수를 읽는다. 기존 실루엣 `reveal_amount` 슬롯과 겹치지 않도록 팔 매개변수는 인스턴스 슬롯 4–10 및 12를 사용한다. 일반 메시에는 `grip_surface=false`가 기본이다. 휘어진 표면이 원래 AABB 밖에 있어도 잘리지 않도록 팔의 시각 AABB는 넉넉하게 지정한다.

새 조끼 재질은 `custom_vest_fitted_*` 이름으로 기존 조끼 색 채널에 연결한다. 몸·얼굴 셰이더, 팔레트, 저장 키, 멀티 패킷 형식은 유지한다. 기본 귀·꼬리의 메시와 변환을 보관해 다른 모양을 거친 후 정확히 복원한다. 뾰족 귀 높이는 새 기본 귀보다 높게 보이도록 0.42→0.46 m로 조정한 아트 수치다.

## 재생성

`prototypes/grounded-art/tools/publish_game_models.py`가 기존 게임 원본 모델에 승인 후보의 해당 표면만 이식하고 fitted 파일을 출력한다. 기존 장비 노드·원본 모델은 유지하고 원본 SHA-256을 검사한다. 입력 후보와 산출물 목록은 `prototypes/grounded-art/game-publication.json`에 기록한다. 모델 등록 생성기 `write-roster-resources.mjs`도 fitted 경로를 유지한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/publish_game_models.py
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path game --quit
```

원본 character-lab 모델을 다시 생성했다면 publish 단계도 다시 실행한다. 최종 fitted GLB와 런타임 셰이더·코드·생성 도구는 버전 관리 대상이며 `.godot/`과 `output/`은 제외한다.

## 검증

빌드 경고/오류 0, Blender 게시와 Godot 가져오기 종료 코드 0. 새 검사는 적용 전 `goose fitted wrap vest loaded by catalog`에서 실패했다. 적용 중 추가한 체형 선편집 검사도 이중 배율을 재현해 실패한 뒤 수정으로 통과했다.

| 검사 | 결과 |
|---|---|
| GroundedArtCheck | 74 통과: 카탈로그/기본형 복원/체형 네 극단/편집 후 장착/프리뷰/피격·실루엣 |
| WeaponGripCheck / ReloadPoseCheck | 187 / 279 통과 |
| ModularCustomization / CustomizationToneCheck | 67 / 87 통과 |
| TailMeleeCheck | 16,456 통과; 삼각형별 반복 포함, 기능 수가 아님 |
| AppearanceExpansion / HitFlashCheck | 통과 |
| EnemyAttackCheck / VisionTierCheck | 79 / 23 통과 |
| WeaponHandlingCheck / RifleEffectsCheck | 36 / 17 통과 |
| Integration | 149 통과; 전용 검사와 별도로 실행 |
| NetSyncCheck | 같은 PC ENet 2인 32 통과 |
| CharacterSurfaceCheck | 창 모드 GPU 43 통과 |
| CaptureGroundedArt | 창 모드 실제 Godot 렌더 9장, 육안 확인 |

런타임 표면 교체 후 기존 꼬리 검사가 비인덱스 정점 배열만 가정해 실패했다. 실제 GLB 인덱스를 읽도록 바꾸고 작은 끝마개도 면적과 노멀 방향을 검사한다. 뾰족 귀가 기본형보다 충분히 높아야 하는 기존 검사도 새 기본형에 맞춰 위의 높이를 조정한 후 통과했다. 초기 빌드의 지역 변수 이름 중복 1건도 수정했다.

기능 통과와 별개로 헤드리스 종료 경고가 있다. 재장전 6, 꼬리/소총 효과/통합 각2, 시야4, 무기 조작8, 네트워크12개의 ObjectDB 잔존을 관측했다. 재장전 verbose 재실행에서 AudioStreamWAV 3개 + AudioStreamPlaybackWAV 3개로 식별했다(I-17). 나머지 실행은 객체 종류를 개별 식별하지 않았다. 최종 GroundedArtCheck·실제 아트 캡처·GPU 표면 검사에는 해당 경고가 없었다.

실제 렌더:

- [네 종 장착](../game/output/grounded-lineup.png)
- [귀·꼬리 후면](../game/output/grounded-rear.png)
- [폭 88% / 키 110%](../game/output/grounded-slim-tall.png), [폭 112% / 키 90%](../game/output/grounded-wide-short.png)
- [달리기](../game/output/grounded-running.png), [거위 확대](../game/output/grounded-goose-close.png)
- [피격 표시](../game/output/grounded-hit-flash.png), [실루엣](../game/output/grounded-silhouette.png)
- [실제 스테이지](../game/output/grounded-gameplay.png)

검사는 `--path game res://tests/{검사이름}.tscn`으로 각각 실행한다. 일반 기능 검사는 `--headless`, CaptureGroundedArt·CharacterSurfaceCheck는 창 모드다. `Start-Game.ps1 -Test`만으로 모든 검사가 실행되는 것은 아니다.

모든 무기·체형·복장 조합의 표면 간섭을 수학적으로 보장하는 검사는 아니다. 손잡이 연결·기능 회귀와 대표 렌더를 검증했다. 서로 다른 PC의 실제 플레이, 6인 장기 실행, 저사양 GPU/호드 성능은 미검증이다. 기존 후보의 정적 Blender 교차 검사 결과를 런타임 모든 애니메이션에 확대해 주장하지 않는다.
