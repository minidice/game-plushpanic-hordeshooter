# 플레이어 오리 부리·날개·물갈퀴 개선

2026-10-26 · A-9·A-13·A-25·D-5. 4종 병렬 조형 요청 중 오리 범위. [현행 아트 방향](character-art-direction.md)과 [종별 가이드](player-species-guide.md)의 짧고 넓은 오리, 거위보다 넓고 낮은 부리, 작은 눈, 깃 끝과 납작한 새 발을 기준으로 했다. 제작과 검수는 최종 사용자 미술 수락과 구분한다.

## 변경과 보존

- [정의](../game/content/characters/duck.tres)가 `duck-clean.glb`를 읽는다. `duck-fitted.glb`를 읽기 전용 입력으로 사용했다. `build_duck_clean.py`는 다른 플레이어 GLB를 수정하지 않고 전후 동일 카메라의 실루엣/단색/배색/장비 24장을 출력한다.
- 부리를 가로로 넓히고 수직 높이와 앞 돌출을 줄여 넓은 뿌리에서 끝까지 얕게 이어지게 했다. 눈/눈썹은 기존 중심 기준으로 작고 얕게 했다. 기존 오리 몸통/거위보다 낮은 실루엣을 유지하며 날개 하단을 넓혀 큰 깃 끝이 읽히게 했다.
- 발은 닫힌 단일 표면으로 다시 만들었다. 바닥 Y=0(Blender Z=0)의 얕은 물갈퀴와 작은 앞 갈래, 좁은 발목을 연결했다. 기존 두 발 재질 채널과 발 피벗을 사용한다. 기존 판처럼 층이 분리된 발 실루엣을 정리했으며 경사면 IK를 추가한 것은 아니다.
- 몸통 정점/로컬 얼굴·배색 마스크, 모든 원본 노드의 부모·세계 변환과 재질 슬롯, 무늬·색 채널, 부리/얼굴/눈/꼬리 스타일 및 체형 범위, 헬멧/베레모·고글/헤드셋·조끼/배낭, 종별 저장/프리셋을 유지한다. 무기 손은 기존 `CharacterGripSurface`의 날개 변형을 사용한다. 충돌 반경0.48m/높이2.06m, 이동속도4.4m/s, 무기·체력·전투 규칙은 변경하지 않았다.

## 생성·재가져오기

`prototypes/grounded-art/tools/build_duck_clean.py`는 Blender5.2에서 `duck-fitted.glb`를 가져와 오리 부리/눈/눈썹/양 날개/양 발 메시만 수정하고 clean GLB를 출력한다. [독립 검사](../prototypes/grounded-art/tools/validate_duck_clean.py)는 두 GLB를 다시 가져와 원본 노드/부모/피벗/재질과 변경하지 않은 모든 메시 정점, 넓고 얕은 부리·작은 눈·날개 폭, 두 발의 닫힌 외향 표면·평평한 밑면, 다른 플레이어 GLB 해시를 확인한다.

최종 GLB는 35,989삼각형/1,290,500바이트(입력36,243삼각형), SHA256 `90c8e4f01f9fea9373aa5f75b2a9627189d5093ac700bfba95a71b5349fccdae`다. Blender 전후24장과 재현 가능한 `.blend`, 검사 JSON은 `prototypes/grounded-art/output/duck-clean/`에 로컬 생성하며 output은 버전 관리하지 않는다.

## 실제 검증

| 실행 | 결과 |
|---|---|
| Blender 생성/독립 재가져오기 | 종료0, 148개 계약 검사 통과. 입력과 다른 플레이어 GLB 해시 불변 |
| Godot 에셋 가져오기 / C# Debug | 가져오기 종료0, 빌드 경고0·오류0 |
| DuckCleanCheck 리그 전용 | 헤드리스53개 통과. 부리/얼굴/꼬리/크기 편집, 7무기 날개 그립 접촉과 움직임 |
| DuckCleanCheck 창 모드 | 59개 통과. 실제 Stage2 자동 입력 이동·사격, 게임 크기와 기본/장비 확대4장 저장 |
| 전용 Studio 4332 | 57개 통과, 실제 Godot 렌더 17장. 정면/사선/측후면·무기/장비·부리2종/꼬리4종·체형 극값·배색. 기본 복원 픽셀 차이0. 개인 외형 저장과 프리셋 파일 해시 불변 |

Stage2 확대 장면은 캐릭터 회전/카메라 배치로 뒤쪽을 보여 준다. 얼굴·부리/눈의 최종 화면은 Studio 정면·사선 렌더와 Blender 전후 화면으로 검사했다. 발의 완전한 실제 보행/경사면 접지는 경사 지형 자동 검사까지 확대하지 않았다. Studio는 실제 게임 모델·리그·셰이더를 사용하며 웹 전용 모형이 아니다.

## 재현

저장소 루트에서 실행한다. 창 모드/Studio는 다른 작업과 GPU 사용을 조정하고, Studio는 독립 프로젝트명과 포트로 실행한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_duck_clean.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_duck_clean.py
dotnet build game/HordeShooter.csproj --configuration Debug --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game --editor --import --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game --scene res://tests/DuckCleanCheck.tscn --resolution 1440x900 --fixed-fps 60 --disable-vsync
node studio/server.mjs --port 4332
$env:STUDIO_URL='http://127.0.0.1:4332'; node studio/tests/duck-clean.mjs
python studio/tests/compare-render.py studio/output/duck-clean/default.png studio/output/duck-clean/reset.png
```

실제 Studio 실행은 `game/project.godot`의 `config/name`을 작업 트리에서 임시로 `Horde Shooter Art Review Duck`으로 바꿔 사용자 설정을 분리한 뒤 진행했다. 이 변경은 검수 후 원래 파일로 되돌려 버전 관리하지 않는다. 원본 `user://appearance.cfg`, 포트4317/4323과 다른 작업 폴더는 사용하지 않았다.

## 남은 범위

거위와 같은 카메라의 최종 쌍 비교, 공통 조끼 어깨/옆면 재조형, 경사면 발 IK, 모든 무기의 수동 감각, 2PC·장시간/저사양 성능과 최종 사용자 아트 수락은 통합/후속 범위다. 기존 단순 피벗 리그와 곡선 변형 날개 손을 유지했다.
