# 얼굴 액세서리 15–19와 고글 비율 개선

2026-10-23. 사용자는 모자 없는 일반 안경·선글라스·헤드셋 추가를 검토하면서, 후보 10 고글의 렌즈 볼륨과 눌린 듯한 모자 형태를 지적했다. 얼굴에 맞춘 액세서리를 먼저 만들고 고글·모자 비율을 개선하는 제안을 승인했다. 모든 작업은 이 독립 폴더에서 진행했고 기존 게임에는 적용하지 않았다.

## 추가·수정한 형태

| 후보 | 내용 | 장비 단독 | 캐릭터 포함 |
|---|---|---|---|
| 15 일반 안경 | 실제 눈 위치에 맞춘 작은 렌즈, 얇은 테·연결부·안경다리. 눈이 보이는 투명 재질 | [안경 GLB](assets/goose-face-glasses.glb) | [전체 GLB](assets/goose-face-glasses-character.glb) |
| 16 선글라스 | 작고 어두운 렌즈, 둥근 사각 테, 머리를 따라가는 얇은 다리 | [선글라스 GLB](assets/goose-face-sunglasses.glb) | [전체 GLB](assets/goose-face-sunglasses-character.glb) |
| 17 헤드셋 | 좌우 이어컵·패드, 머리띠와 쿠션, 한쪽 마이크 | [헤드셋 GLB](assets/goose-face-headset.glb) | [전체 GLB](assets/goose-face-headset-character.glb) |
| 18 얼굴 착용 고글 | 눈앞에 맞춘 얕은 곡면 렌즈, 얇아진 프레임, 머리를 감싸는 밴드 | [고글 GLB](assets/goose-face-goggles.glb) | [전체 GLB](assets/goose-face-goggles-character.glb) |
| 19 개선한 고글 모자 | 높인 정수리와 앞·옆판 구분, 크기와 돌출을 줄인 이마 위 고글 | [모자 GLB](assets/goose-face-aviator-cap.glb), [올린 고글 GLB](assets/goose-face-raised-goggles.glb) | [전체 GLB](assets/goose-face-refined-character.glb) |
| 조합 예시 | 모자 없이 선글라스 + 헤드셋. 두 장비는 별도 노드·메시 | 위의 각 단독 파일과 동일 형상 | [조합 GLB](assets/goose-face-combo-character.glb) |

장비 단독 6개 + 캐릭터 포함 6개 = **GLB 12개**다. 몸·표정·눈·부리·기본 방탄형 04 조끼는 보존했다. 모자 없는 기준, 이전 고글 모자 10, 새 후보 다섯 가지와 조합 예시를 같은 조건에서 렌더했다. 배낭은 비교에 포함하지 않았다.

### 고글과 모자의 변경

- 후보 10의 큰 볼록 타원에서, 크기를 줄인 둥근 사각 형태로 변경했다. 테두리와 연결부도 얇게 했다.
- 생성기 설계값 기준 렌즈 중앙의 추가 돌출을 **28 mm → 3 mm**로 줄였다. 얼굴 전체를 따르는 곡률은 남기되 렌즈 자체가 부푼 인상을 줄였다.
- 올린 고글의 명목 외곽 폭은 약 **0.791 m → 0.626 m**, 약 21% 감소했다. 미터 수치는 키 약 2 m인 기존 거위 모델 좌표 기준이며 실제 사람 장비 치수 제안이 아니다.
- 모자의 정수리 설계 높이를 **2.055 m → 2.155 m**로 올리고, 가로 반지름은 0.465 m → 0.445 m로 조금 좁혔다. 앞·옆판 색면과 얇은 봉제선을 구분했다.
- 눈에 착용하는 고글과 모자 위로 올린 고글은 각각 맞춘 별도 파일이다. 단순 위치 이동으로 모든 착용 상태에 맞는다고 가정하지 않는다.

일반 안경과 고글에는 glTF 알파 블렌딩을 사용한다. 일반 안경은 불투명도 0.035로 눈이 잘 보이게 했고, 고글은 0.72로 색이 있는 렌즈를 표현했다. 선글라스는 불투명한 어두운 재질이다. 물리적인 도수·굴절·투과 시뮬레이션은 구현하지 않았다.

## 분리와 편집

- 일반 안경·선글라스·고글은 `goose_goggles` 아래, 헤드셋은 `goose_headset` 아래, 모자는 `goose_helmet` 아래에 둔다. 세 부착점 모두 `goose_head`를 따른다.
- 후보 19에서도 모자와 고글을 합치지 않았다. 조합 예시에서도 선글라스와 헤드셋이 분리되어 있다.
- 단독 GLB에는 장비 메시와 필요한 조상 노드만 포함한다. 얼굴·몸 메시를 포함하지 않는다.
- 게임 복장 UI·저장 슬롯·색상 채널 등록은 변경하지 않았다. 노드를 준비한 것과 실제 게임에서 조합 선택을 지원하는 것은 구분한다.
- Blender 축은 +Z 위/-Y 정면, GLB는 +Y 위/+Z 정면, 단위는 미터다.

[편집용 Blender 원본](source/goose-face-accessories.blend)에는 여덟 비교 구성이 같은 위치에 있다. 기본 표시 상태는 개선한 고글 모자다. 객체 접두사 `base_`, `glasses_`, `sunglasses_`, `headset_`, `goggles_`, `refined_`, `combo_`로 구분한다. `base_`에서 머리 장비 메시를 숨기면 모자 없는 기준이다.

## 렌더와 검사

- [얼굴 액세서리 확대 비교](output/face-accessories/accessories-details.png)
- [고글·모자 전후 확대](output/face-accessories/goggles-before-after-detail.png), [정면](output/face-accessories/goggles-before-after-front.png), [측면](output/face-accessories/goggles-before-after-side.png)
- [전체 캐릭터 중립 조명](output/face-accessories/accessories-neutral.png), [분위기 조명](output/face-accessories/accessories-mood.png), [작은 화면 크기](output/face-accessories/accessories-small.png)
- 방향별 모음: [안경](output/face-accessories/glasses-overview.png), [선글라스](output/face-accessories/sunglasses-overview.png), [헤드셋](output/face-accessories/headset-overview.png), [얼굴 고글](output/face-accessories/goggles-overview.png), [개선 모자](output/face-accessories/refined-overview.png), [조합](output/face-accessories/combo-overview.png)
- [자산 측정값·보호 해시](face-accessories-manifest.json), [검사 결과](output/face-accessories/validation.json)

| 구성 | 장비 삼각형 | 캐릭터 전체 삼각형 | 전체 메시 수 |
|---|---:|---:|---:|
| 일반 안경 | 4,200 | 26,466 | 13 |
| 선글라스 | 4,200 | 26,466 | 13 |
| 헤드셋 | 4,392 | 26,658 | 13 |
| 얼굴 고글 | 4,124 | 26,390 | 13 |
| 개선 모자 + 올린 고글 | 9,856 | 32,122 | 14 |
| 선글라스 + 헤드셋 | 8,592 | 30,858 | 14 |

실제 내보낸 GLB 12개를 빈 장면에 다시 가져와 **295개 검사 통과**:

- 이전 GLB·Blender 원본·게임 거위 GLB의 해시 유지.
- 생성 단계에서 기존 몸 11개와 조끼 1개 메시의 정점·면 완전 일치, 세계 변환 오차 0.000001 미만 확인.
- 메시·삼각형 수, 축·경계, 면적 0인 면 없음, 머리 부착점, 단독 파일에 몸 없음, Muzzle 유지, 스튜디오·리그 제외 확인.
- GLB JSON에 투명 렌즈의 `alphaMode: BLEND`와 지정 불투명도가 보존되는지 확인.
- 새 장비와 몸통·부리·양쪽 눈·양쪽 눈썹·조끼 사이의 기본 자세 표면 교차 없음. 후보 19의 모자/고글, 선글라스/헤드셋 조합도 교차 없음. 총 **58개 정적 맞춤 검사**.

첫 조합 검사에서는 선글라스 안경다리와 이어패드의 표면 교차 110쌍을 발견했다. 부품별 재질과 교차 위치를 확인해 안경다리의 몸 표면 여유를 25 mm에서 10 mm로 줄이고, 이어패드를 바깥쪽으로 14 mm 옮겼다. 독립 파일과 조합 파일에 동일한 형상을 사용했으며 수정 후 전체 검사를 다시 실행해 교차가 없어졌다. 일반 안경의 초기 렌즈는 뿌옇게 보여 불투명도도 줄였다.

중립 6시점×8구성, 분위기 2시점×8구성으로 원본 PNG 64장과 비교판 13장을 생성한다. Blender 5.2.2 LTS / Cycles 32 samples / denoise. 일반 720×840, 작은 화면 720×480. 같은 시점의 모델은 몸·조끼·카메라·조명이 동일하다. 비교판은 실제 모델 렌더를 배열하고 표제를 붙였으며 모델 픽셀 재생성이나 보정은 하지 않았다.

최종 생성·검사·비교판 명령은 모두 종료 코드 0이었다. PNG 77개의 파일 무결성, 문서 링크 99개, 스크립트 3개의 구문을 확인했고 보호 대상 기존 파일 39개의 해시가 유지됐다. 확대 모음·고글 전후·작은 화면 비교와 안경·헤드셋·개선 모자의 측면을 직접 확인했다. 작은 화면에서는 일반 안경의 얇은 테가 약하게 보이므로 실제 게임 카메라에서의 가독성 조정은 후속 검토다. 생성 로그에는 Blender 6.0에서 제거 예정인 `Material.use_nodes`의 `DeprecationWarning`이 있으며, 현재 5.2.2에서는 성공했다. 6.0 호환은 미검증이다.

## 재현과 남은 범위

저장소 루트에서 실행한다. 기존 모자 컬렉션의 후보 10을 읽어 시작하며, 출력은 이 폴더 안으로 제한한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_face_accessories.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_face_accessories.py
python prototypes/grounded-art/tools/compare_face_accessories.py
```

생성 명령 뒤 `-- --preview`는 여덟 구성의 중립 확대 이미지까지만 만든다. 전체 비교판에는 전체 렌더가 필요하다. 비교 도구는 기존 Pillow를 사용한다. `output/`은 로컬 재생성 산출물이다.

Godot의 투명 재질 정렬·그림자·렌더 결과, 실제 머리 흔들림·표정·체형 변경, 다른 동물, 다른 모자와 헤드셋의 조합, 게임 성능은 검증하지 않았다. 이번 정적 맞춤 결과는 모든 동작과 모든 장비 조합을 보장하지 않는다. 기존 조끼와 몸은 수정하지 않았다.

2026-10-23 `doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`를 검토했다. 게임 기능·동작·확정 계약을 바꾸지 않은 독립 아트 후보이므로 제품 결정·이슈 ID를 새로 만들지 않았다. 기존 게임 파일을 보존하라는 요청에 따라 제작과 검사 기록은 이 폴더에 남겼다. 이전 모델·생성기, 게임 파일, Git 브랜치·인덱스를 변경하지 않았다.
