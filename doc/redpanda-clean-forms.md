# 플레이어 랫서판다 얼굴·귀·손발 개선

2026-10-26 · A-9·A-13·A-25·D-5. 사용자가 [4종 점검](player-art-audit-2026-10-26.md)의 랫서판다 우선 개선을 승인했다. 기존 커스터마이징을 보존하며 Blender 조형과 Studio/실제 게임 연결을 함께 검수한다. 최종 사용자 아트 수락은 별개다.

## 자산과 현재 구현

- 읽기 전용 원본은 `game/assets/characters/redpanda/redpanda-fitted.glb`. 새 `redpanda-clean.glb`를 생성하고 [캐릭터 정의](../game/content/characters/redpanda.tres)의 Model 경로로 연결한다. 원본과 다른 플레이어 모델은 보존한다.
- [Blender 생성기](../prototypes/grounded-art/tools/build_redpanda_clean.py)는 원본을 다시 읽어 같은 결과를 생성한다. [독립 재가져오기 검사](../prototypes/grounded-art/tools/validate_redpanda_clean.py)는 출력 GLB를 새로 가져와 원본 노드·피벗·재질/몸통 좌표와 변경 부위 계약을 비교한다.
- 얼굴: 주둥이 뿌리를 넓히고 돌출을 낮춰 흰 볼과의 연결을 보완했다. 기존 눈·눈썹 메시의 돌출을 줄이고 각 피벗·크기/간격 조절은 유지한다. 얼굴 무늬를 평가하는 몸통 로컬 정점과 셰이더는 변경하지 않는다.
- 귀: 기본 귀를 둥근 삼각형의 연결된 오목 귀로 만들었다. 안쪽 밝은 면은 붉은 테두리보다4.7cm 뒤에 있다. 기본 높이0.33m는 아트 조정값이며 기존 뾰족 귀0.46m와 구분된다. 기존 둥근/뾰족/접힌 귀 스타일, 각도·크기 및 모자 착용 시 접힘은 유지한다.
- 손: 기존 팔 전체를 휘게 하는 플레이어 표면 변형 대신 랫서판다만 고정 비율 손과 별도 전완(`CharacterLionPaw`)을 사용한다. 작은 손바닥·엄지 형태를 유지하면서 실제 무기 손잡이에 붙인다. 기존 한손 옆구리 파지/양손 무기 정책은 바꾸지 않는다.
- 발: 둥근 발등과 별도 밑창처럼 읽히던 형태를 낮은 발등·좁은 발목·평평한 밑면으로 바꿨다. 기존 발 피벗, `feet_size`의 가로/깊이 조절, 발 색상 채널을 유지한다. 경사면 발 IK를 추가한 것은 아니다.

## 커스터마이징/리그 계약

- 기존 저장 키·값 범위·프리셋·다른 종 동작, 충돌 반경0.48m/높이2.06m/이동속도4.4m/s는 그대로다. 사용자 appearance.cfg를 수정하거나 초기화하지 않는다.
- 주/보조/얼굴 무늬색·눈/주둥이/발 색, 눈 간격·얼굴 표정·주둥이 크기/형태, 귀4종/꼬리5종, 몸/손발 크기, 머리털·모자·고글·헤드셋·조끼2종·배낭을 유지한다. 플레이어 장비를 일반 몬스터의 제외 정책으로 삭제하지 않는다.
- 새 `redpanda_<left|right>Paw`는 기존 Arm 피벗 아래에 있고 `Paw_mesh`, `Wrist`, `Grip`을 포함한다. 기존 Arm_mesh는 전완만 담는다. 무장하면 휴식 팔 전체를 숨기고 원본 손 메시/재질을 공유하는 동적 손을 표시한다.
- Godot 모델 좌표: 왼 Wrist `(-.430,.752,.207)`, Grip `(-.390,.687,.315)`; 오른쪽은 X만 반대다. Blender는 `(x,y,z)` → Godot `(x,z,-y)`다. 기존 팔 피벗 `(±.430,.890,.200)`은 유지한다. Arm 자식 `redpanda_<side>Shoulder`는 실제 전완 윗끝 `(±.440,1.125,.158)`을 나타낸다. `CharacterWeaponGrip`은 이 선택적 기준점으로 전완의 늘어나는 시작 위치만 정하고, 원본 변환/커스텀 피벗은 그대로 사용한다. 기준점이 없는 사자·순록은 기존 피벗을 사용한다.
- 손·전완은 `custom_wing`의 기존 보조색을 사용한다. 캐릭터별로 소유한 원본 재질을 동적 손도 공유해 장착 전/후 색 변경이 반영되고 다른 캐릭터에는 번지지 않는다. `wing_size`는 손을 균일 확대하며 무기 거리에 맞춘 길이 변화는 전완만 담당한다.
- Studio와 게임 모두 같은 `.tres`와 GLB를 읽는다. Studio만 별도 수정 모델을 그리는 경로는 없다.

## 제작 중 발견과 해결

- 최초 손·귀 타원 면의 정점 순서가 뒤집혀 Godot 후면 제거에서 귀 안쪽이 조각나고 손이 빈 고리처럼 보였다. 닫힌 면의 바깥 법선을 바로잡고 재가져오기 검사를 추가했다.
- 초기 기본 귀가 너무 커 기존 뾰족 귀 선택이 더 커지지 않는 회귀가 발생했다. `CustomizationToneCheck`의 기존 검사를 유지하면서 기본 귀 높이를 조정했다.
- 귀 안쪽의 별도 밝은 판은 측면에서 떠 보이는 문제가 있어 외피와 이어지는 안쪽 면으로 정리했다. 실제 Studio 측면에서 확인했다.
- 최초 전용 검사에서 `new authored rigid palm exists`가 실패하는 것을 확인한 후 새 모델/파지 경로를 연결했다. 검증용 로그 폴더가 없었던 첫 실행은 로그 파일 생성 경고도 발생해 폴더 생성 후 다시 실행했다.

- 장전 렌더에서 전완이 어깨 너머로 길게 뻗는 결함을 발견했다. 기존 랫서판다 Arm 피벗은 팔 중간인데 새 고정 손 경로가 이를 어깨로 가정해 피벗 위 정점이 반대로 늘어났다. 원래 피벗을 이동하지 않고 실제 Shoulder 기준점을 추가해 해결했다. 손잡이 마커뿐 아니라 실제 전완 정점이 어깨–손목 구간 안에 남는 검사도 추가했다. 최초 실패 후 수정으로 통과했다. 검사 좌표도 움직이는 torso 부모 기준으로 보정했다.
- 사다리 최초 캡처는 먼 위치로 순간이동한 뒤 카메라가 따라가는 도중을 찍어 캐릭터가 화면 밖에 있었다. 검사 카메라를 사다리에 직접 배치해 다시 캡처했다. 실제 사다리 동작 코드는 바꾸지 않았다.

## 검증

`RedpandaCleanCheck`는 7종 무기, 실제 손 정점 간 거리, 손발/몸 크기 극값, 색 변경, 귀/꼬리/얼굴 모양과 복원, 재장전, 짚라인 표시, 상태 표면·피격·숨김을 검사한다. 창 모드에서는 Stage2에서 실제 플레이어 이동·발사·사다리와 기본/장비 확대 화면을 추가 캡처한다. 수동 완주가 아니라 자동 입력으로 구동한 실제 게임 검사다.

Studio `redpanda-clean.mjs`는 기존21개 주요 조절 항목의 존재, 기본/정면/측면/후면/장비/무기 및 귀·꼬리·표정·크기/배색 극값21장, 원본 설정과 사용자 저장 불변을 검사한다. 기본 복원은 기존 `compare-render.py`로 실제 픽셀 허용 오차를 확인한다.

2026-10-26 실행 결과:

| 검사 | 결과 |
|---|---|
| Blender 생성/독립 재가져오기 | 214개 통과, 원본/다른 플레이어 GLB 해시 불변 |
| 새 GLB | 35,989삼각형 / 1,271,632바이트 (원본39,279 / 1,372,012) |
| Debug / ExportRelease | 오류0·경고0 |
| RedpandaCleanCheck 창 모드 | 7,144개 통과, 실제 Stage2 이동·발사·사다리 완료 및5장 캡처 |
| Studio | 65개 통과·21장, 기본 복원 픽셀 차이0, 사용자 저장 파일 해시 불변 |
| 표면 / 색·귀 / 모듈 커스텀 | CharacterSurfaceCheck43 / CustomizationToneCheck89 / ModularCustomization67 통과 |
| 공통 손 / 파지 / 장전 | 사자·순록 LionLimbCheck 각각4,464 / WeaponGripCheck187 / ReloadPoseCheck279 통과 |
| 장전 렌더 | CaptureReloadPose 통과, 수정 후 M4 기본/장비 장전과 플레이 크기 캡처 확인 |

새 GLB SHA256: `71f24b8b6a2e068cab00f3cdd2d3417cecbcbec599db658bafa82d4d4f53839f`. 최종 전용 게임/Studio/장전·손 회귀 실행은 오류·종료 경고 없이 끝났다. 앞선 파지 검사 실행의 ObjectDB2개 종료 경고는 별도로 남긴다(I-17 미해결). 표면·색·귀·모듈 검사는 어깨 기준점 추가 전 동일한 얼굴/귀/발 메시에서 통과했고, 마지막 추가 후 손·장전/Studio/전용 게임 검사를 재실행했다.

독립 최종 검토에서 추가 수정이 필요한 사항은 발견되지 않았다. 검토자는 어깨 변환·재질 공유·커스텀 복원·생성/검사 코드·최종 로그와 Studio/장전/Stage2 렌더를 확인했으며 검사를 별도로 재실행하지 않았다.

재현(저장소 루트):

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_redpanda_clean.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_redpanda_clean.py
dotnet build game/HordeShooter.csproj --configuration Debug --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game --scene res://tests/RedpandaCleanCheck.tscn --resolution 1440x900 --fixed-fps 60
# Studio에서 로컬 데이터 다시 읽기를 완료한 뒤, 실행 포트에 맞춰 사용
$env:STUDIO_URL='http://127.0.0.1:4323'
node studio/tests/redpanda-clean.mjs
python studio/tests/compare-render.py studio/output/redpanda-clean/default.png studio/output/redpanda-clean/reset.png
```

산출물은 `prototypes/grounded-art/output/redpanda-clean/`(Blender 전후/검사/.blend), `studio/output/redpanda-clean/`, `game/output/redpanda-clean/`에 생긴다. 자동 생성 output은 버전 관리하지 않는다. Blender 화면에는 게임의 얼굴 마스크 셰이더·모자 귀 접힘·무기 파지/커스텀 변형이 없으므로 최종 외형은 Studio/게임 렌더로 판단한다.

## 남은 범위

이번에는 얼굴·귀·손발을 다듬었다. 몸통 전체 비율·조끼/장구류 재조형, 라쿤·거위·오리 확대, 손가락 개별 관절·경사면 IK, 장시간 호드/저사양·실제2PC·패키지 export와 최종 사용자 아트 수락은 별도다.

2026-10-26 후속: 위 수치는 얼굴·귀·손발 작업 시점 기록이다. 이후 기본 꼬리 메시만 수정했다. 최신 삼각형/꼬리 동작·검증은 [긴 꼬리 기록](mammal-tail-quality.md)을 따른다.
