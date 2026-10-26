# 플레이어 라쿤 얼굴·귀·손발 개선

2026-10-26 · A-9·A-13·A-25·D-5. 사용자 “다음 추천작업 진행”에 따라 [4종 점검](player-art-audit-2026-10-26.md)의 랫서판다 다음 순서인 라쿤을 개선했다. 매끈한 데포르메, 가로 눈띠·회색 몸·밝은 배/주둥이·줄무늬 꼬리를 유지한다. 구현/검수와 최종 사용자 아트 수락은 구분한다.

## 변경과 보존

- [캐릭터 정의](../game/content/characters/raccoon.tres)의 Model은 새 `game/assets/characters/raccoon/raccoon-clean.glb`다. 읽기 전용 제작 입력 `raccoon-fitted.glb`를 포함한 기존 플레이어 GLB9개를 보존했다.
- 주둥이는 폭을 넓히고 깊이/높이를 낮췄다. 눈/눈썹 돌출도 줄였으며 눈썹은 실제 부모 피벗을 기준으로 조형했다. 가로 눈띠를 그리는 몸통 좌표·메시·재질/셰이더는 그대로다.
- 기본 귀는 높이0.29m의 둥근 컵 형태로 정리했다. 밝은 안쪽과 회색 테두리가 하나로 연결되고, 중앙은 테두리보다0.037m 들어간다. 랫서판다0.33m 귀보다 낮고 둥글다. 기존 둥근/뾰족/접힌 스타일·크기·각도와 모자 접힘은 유지한다.
- 손은 작은 손바닥과 엄지, 팔은 별도 전완으로 분리했다. 랫서판다의 고정 비율 손과 실제 어깨 기준점을 재사용하며 손등은 납작하고 손끝으로 좁아지며 엄지가 하나의 닫힌 면으로 이어진다. 발은 좁은 발목·낮은 발등·지면0의 평평한 밑면으로 바꿨다. 밑창처럼 층이 나뉘던 기존 실루엣을 정리했다.
- 기존 색/무늬·표정·눈 간격·주둥이/몸/손발 크기·귀4종·꼬리5종·머리털·헬멧/베레모·고글/헤드셋·조끼2종·배낭·저장 키와 프리셋을 보존한다. 라쿤 손/전완은 기존 팔 재질의 **무늬색(가로 눈띠 색)**을 따르며, 랫서판다의 보조색과 구분한다.
- 충돌 반경0.48m/높이2.06m, 이동속도4.4m/s, 한손 M4/양손 무기 정책은 변경하지 않았다. 플레이어 장비는 일반 몬스터의 눈 장구류 제외 정책과 별개다.

## 리그와 생성 계약

`raccoon_<side>Arm_mesh`는 전완이다. 기존 Arm 피벗 아래 새 `Paw`와 `Shoulder`, Paw 아래 `Paw_mesh`/`Wrist`/`Grip`을 둔다. 왼쪽은 X가 음수, 오른쪽은 양수다.

| 기준점 | Godot 모델 좌표 |
|---|---|
| 원래 Arm 피벗(보존) | (±0.430,0.890,0.200) |
| Shoulder | (±0.440,1.125,0.158) |
| Paw/Wrist | (±0.430,0.752,0.207) |
| Grip | (±0.390,0.687,0.315) |

Blender(x,y,z)→Godot(x,z,-y). `CharacterWeaponGrip`의 고정 손 대상에 라쿤만 추가했으며 기존 선택적 Shoulder 처리는 그대로 사용한다. 중간 팔 피벗을 어깨로 오해해 전완을 역방향으로 늘리지 않는다. 장착 때 원본 팔을 숨기고 캐릭터별 원본 메시/소유 재질을 공유하는 손과 전완을 표시한다. 손은 균일 확대, 전완만 어깨–손목 길이를 조정한다.

[생성기](../prototypes/grounded-art/tools/build_raccoon_clean.py)는 독립 실행/안전한 import가 가능하다. [독립 재가져오기 검사](../prototypes/grounded-art/tools/validate_raccoon_clean.py)는 소스/출력 파일을 다시 읽어 원본51개 노드의 전체 변환·부모·재질, 변경하지 않은 메시 정점, 새 계층, 면 방향/닫힘, 실제 손 표면과 그립 거리, 전완 범위, 귀 오목함·연결, 밑면을 검사한다. 다른 종 빌더를 실행하거나 변경하지 않는다.

새 GLB는35,526삼각형/1,264,720바이트/61노드(원본38,472삼각형/51노드), SHA256 `0b540b51ca1adf24fcb625b3b84e05f982af376568b0351929b056671a289cf7`다.

## 검증과 발견

| 실행 | 결과 |
|---|---|
| Blender 생성/재가져오기 | 301개 검사 통과, 원본·다른 플레이어9개 GLB 해시 불변; 전후24장 및 재현 가능한 .blend |
| Debug / ExportRelease | 오류0·경고0 |
| RaccoonCleanCheck 창 모드 | 7,144개 통과; Stage2 실제 플레이어 자동 입력 이동·사격·사다리 완료, 기본/장비 확대 포함5장 |
| Studio 라쿤 | 65개·21장 통과, 기본 복원 픽셀 차이0, 사용자 저장/프리셋 파일 해시 불변 |
| 공유 검사 회귀 | 랫서판다7133(리그 전용), Studio 랫서판다65/복원픽셀0 |
| 표면 / 색·귀 / 모듈 커스텀 | CharacterSurfaceCheck43 / CustomizationToneCheck89 / ModularCustomization67 통과 |
| 파지 / 장전 | WeaponGripCheck187 / ReloadPoseCheck279, CaptureReloadPose 통과 |

랫서판다 전용 검사 내용을 `PlayerMammalCleanCheck`와 Studio `player-mammal-clean.mjs`로 공유하고 종별 진입점은 유지했다. 7무기(M4/M1911/R870/M249/박격포/알라봉/프라이팬)에서 손 정점 거리·전완 실제 정점 범위·그립 연결·크기 극값·재장전·짚라인 표시·피격/상태 표면/숨김, 귀/꼬리/얼굴 전환과 복원을 검사한다. 실제 게임 검사는 자동 입력이며 수동 스테이지 완주가 아니다. Studio는 실제 게임 GLB/리그/셰이더를 사용하며 웹 전용 대체 모델은 없다.

최초 전용 검사는 새 손 부재로 실패해 연결 전 조건을 확인했다. 연결 후 색 검사에서 랫서판다의 보조색을 라쿤에도 기대한 오류가 드러났다. 기존 라쿤 팔레트는 손이 눈띠 무늬색을 따르므로 제품 색 정책을 바꾸지 않고 검사를 종별 계약에 맞게 수정했다. 실제 변경 배색 렌더로도 확인했다.

독립 모델 검토에서 초기 손바닥·엄지가 둥근 구슬 두 개처럼 읽히는 문제가 확인됐다. 두 타원 메시를 하나의 납작하고 끝이 좁아지는 손으로 교체했다. 이전 모델에서 연결성 검사가 실패함을 확인하고, 최종301개 검사와 실제 Studio/Stage2/장전 렌더를 재확인했다. 양쪽 Paw_mesh만 바뀌고 나머지59개 노드 및 전체 변환/부모/재질/기준점이 그대로임을 별도 비교했다. 수정 재검토에서 해당 지적 해결·새 문제 없음으로 판정했다.

Blender 생성에서 `World.use_nodes`/`Material.use_nodes`의 Blender6.0 폐기 예정 경고3건이 있었으며 export/render는 종료0으로 완료됐다. 파지 검사 종료 ObjectDB2개 경고는 기존 I-17로 구분하며 해결했다고 표시하지 않는다. 나머지 전용 검사/가져오기/렌더 로그는 오류·종료 경고 없음.

대표 검토 화면: Blender 단색 쿼터, Studio 기본/측면/무장/배색, 실제 Stage2 기본/장비/사다리/사격, M4 기본형·장비형 장전. 모든 생성 이미지/커스텀 조합을 독립 미술 판정했다고 주장하지 않는다.

최종 독립 검토에서 추가 수정이 필요한 사항은 없었다. 런타임 연결·색/저장 계약·공유 검사·문서와 최종 Studio/실제 게임 렌더를 확인했으며 검토자가 검사를 중복 실행한 것은 아니다.

## 재현

저장소 루트에서:

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_raccoon_clean.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_raccoon_clean.py
dotnet build game/HordeShooter.csproj --configuration Debug --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game --editor --import --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game --scene res://tests/RaccoonCleanCheck.tscn --resolution 1440x900 --fixed-fps 60
# Studio에서 로컬 데이터 다시 읽기가 완료된 뒤 실행
$env:STUDIO_URL='http://127.0.0.1:4323'
node studio/tests/raccoon-clean.mjs
python studio/tests/compare-render.py studio/output/raccoon-clean/default.png studio/output/raccoon-clean/reset.png
```

산출물: `prototypes/grounded-art/output/raccoon-clean/`, `studio/output/raccoon-clean-before/`, `studio/output/raccoon-clean/`, `game/output/raccoon-clean/`, `game/output/reload-*.png`. output은 버전 관리하지 않는다. Blender는 Godot 얼굴 무늬 셰이더·모자 접힘/커스텀/실시간 파지를 그리지 않으므로 최종 판단은 게임 렌더 기준이다.

## 남은 범위

몸통 전체 비율·공통 장구류 재조형, 거위·오리 개선, 개별 손가락/팔꿈치 관절·경사면 발 IK는 후속이다. 손바닥과 엄지는 하나의 연결된 고정 메시이며 개별 손가락/엄지 관절은 없다. 실제2PC·장시간/저사양 성능·패키지 export와 최종 사용자 아트 수락은 이번 검증에 포함되지 않는다.

2026-10-26 후속: 위 수치는 얼굴·귀·손발 작업 시점 기록이다. 이후 기본 꼬리 메시만 수정했다. 최신 삼각형/꼬리 동작·검증은 [긴 꼬리 기록](mammal-tail-quality.md)을 따른다.
