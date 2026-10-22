# 다른 플레이어 종의 무기 잡는 형태

2026-10-23. 사용자 요청: 거위에 만든 무기용 날개와 조준 자세 개선을 다른 종에도 적용. 대상은 현재 플레이어 종인 **오리·라쿤·레서판다**다. 기존 게임과 이전 거위 후보를 보존한 독립 아트 후보이며 런타임에는 적용하지 않았다.

## 종별 변경

| 종 | 변경한 형태 | 보존한 특징 |
|---|---|---|
| 오리 | 납작하게 접힌 날개, 둥근 깃털 끝 세 갈래, 손잡이 쪽으로 굽힘 | 오리의 원래 날개 색과 짧은 몸·부리·머리털·꼬리 |
| 라쿤 | 좁은 앞팔과 짧은 손목, 둥글고 얕은 발가락 세 마디 | 원래 짙은 앞발 색, 얼굴 마스크·귀·주둥이·줄무늬 꼬리 |
| 레서판다 | 라쿤보다 폭13%, 두께8%를 늘린 둥근 앞발 | 원래 갈색 앞발 색, 얼굴 무늬·귀·주둥이·꼬리 |

포유류에는 새의 긴 깃털 끝을 복사하지 않았다. 좌우 팔을 각 종의 새 형태로 만들고, 오른쪽은 어깨부터 손잡이까지 굽혔다. 대기 자세의 중간 부분은 개머리판을 피하도록 바깥으로 돌아간다. 반대쪽 팔은 접어서 내린 상태다. 기존 팔 부모 노드와 피벗, `custom_wing` 재질 채널을 유지했다.

복장은 앞서 만든 기본 방탄조끼04를 세 종에 공통 사용한다. 이 조끼는 각 비교의 이전/이후 양쪽에 동일하게 들어간다. 얼굴·귀·몸·꼬리는 원본 GLB 형상으로 보존하고, 종을 알아보기 쉽도록 머리 장비와 배낭은 제외했다. 머리털은 원본의 짧은 형태 한 가지만 표시했다. 전체 장비를 새로 리디자인한 작업은 아니다.

## 자세와 비교 조건

각 종에 무기 대기·수평 조준·위쪽 조준의 세 자세를 제작했다. 거위 후보와 같은 M4 배치와 기본 조명을 사용했다. 조준경에 눈을 대는 ADS가 아닌 캐릭터 비율에 맞춘 한 손 조준 표현이다.

| 자세 | Blender 총 위치 X,Y,Z (m) | 총 X 회전 |
|---|---|---|
| ready | (0.52, -0.52, 1.02) | +12도 |
| aim | (0.54, -0.65, 1.22) | 0도 |
| aim_up | (0.54, -0.65, 1.22) | -20도 |

수치는 이 독립 후보의 임시 배치이며 게임 규격 변경이 아니다. 전후 비교의 왼쪽은 각 종의 원본 정적 팔을 총 방향으로 통째로 회전시킨 기준형, 오른쪽은 새 팔 형태를 굽힌 후보다. 실제 게임에서 생성되는 캡슐 팔의 화면과 동일하다는 뜻이 아니다. 양쪽의 몸·복장·총 위치·카메라·조명은 같다.

## 이미지와 모델

- [세 종 수평 조준](output/species-weapon-grips/lineup-aim.png)
- [세 종 대기](output/species-weapon-grips/lineup-ready.png), [위쪽 조준](output/species-weapon-grips/lineup-aim_up.png)
- [세 종 × 세 자세 모음](output/species-weapon-grips/all-poses.png)
- 오리: [전신 전후](output/species-weapon-grips/comparison-duck-quarter.png), [날개 확대](output/species-weapon-grips/comparison-duck-detail.png)
- 라쿤: [전신 전후](output/species-weapon-grips/comparison-raccoon-quarter.png), [앞발 확대](output/species-weapon-grips/comparison-raccoon-detail.png)
- 레서판다: [전신 전후](output/species-weapon-grips/comparison-redpanda-quarter.png), [앞발 확대](output/species-weapon-grips/comparison-redpanda-detail.png)
- [Blender 편집 원본](source/species-grips-study.blend):18개 전후 모델을 포함하며 기본 표시는 라쿤 수평 조준 개선형이다.
- [모델 명세](species-grips-manifest.json), [검사 기록](output/species-weapon-grips/validation.json)

각 종·자세마다 총 포함 미리보기(`preview`), 총 없는 캐릭터(`character`), 팔만(`only`)의 GLB를 제공한다. 총27개다. 아래 표는 바로 보기 좋은 총 포함 파일이다.

| 종 | 대기 | 수평 조준 | 위쪽 조준 |
|---|---|---|---|
| 오리 | [preview](assets/species-grips-duck-ready-preview.glb) | [preview](assets/species-grips-duck-aim-preview.glb) | [preview](assets/species-grips-duck-aim_up-preview.glb) |
| 라쿤 | [preview](assets/species-grips-raccoon-ready-preview.glb) | [preview](assets/species-grips-raccoon-aim-preview.glb) | [preview](assets/species-grips-raccoon-aim_up-preview.glb) |
| 레서판다 | [preview](assets/species-grips-redpanda-ready-preview.glb) | [preview](assets/species-grips-redpanda-aim-preview.glb) | [preview](assets/species-grips-redpanda-aim_up-preview.glb) |

수평 조준 팔만: [오리](assets/species-grips-duck-aim-only.glb), [라쿤](assets/species-grips-raccoon-aim-only.glb), [레서판다](assets/species-grips-redpanda-aim-only.glb). 전체 경로와 메시 수는 명세에 기록했다.

| 종 | 총 없는 캐릭터 메시/삼각형 | 양팔 메시/삼각형 |
|---|---:|---:|
| 오리 | 13 / 24,986 | 2 / 2,448 |
| 라쿤 | 15 / 22,828 | 2 / 2,448 |
| 레서판다 | 15 / 23,634 | 2 / 2,448 |

`GripShoulder`, `GripWrist`, `WeaponGripReference`는 편집용 참고 노드다. 기존 게임의 노드 계약에 새 기능을 추가하지 않았다. `prop_` 총 노드는 preview 파일에만 포함한다.

## 검증과 남은 한계

실제 GLB27개를 다시 불러오는 검사 **1,145항목 통과**. 원본 렌더24장과 비교·모음10장을 생성했다. 종별 전신·확대, 개선형의 측면·위쪽 시점을 확인했다.

- 보호 대상71파일 SHA-256 일치: 이전 후보 GLB/Blender 원본, 네 종의 원본 모델, M4와 참조한 무기 팔 소스. 기존 생성기와 게임 파일을 수정하지 않았다.
- 오리는 팔 외 원본10메시, 라쿤·레서판다는 각각12메시를 보존했다. 실제 GLB의 월드 정점 위치를 원본과 양방향 최근접 거리로 비교해0.00001 m 이내인지 확인했다. 공통 조끼도 승인된 조끼 형상과 별도 비교했다.
- 좌우 새 팔의 닫힌 표면, 비영 면적, 인접하지 않은 면의 자기 교차 없음, 원래 부모와 피벗, GLB 경계·축·삼각형 수·참고점·총구 위치를 확인했다.
- 세 종 × 세 자세 × 좌우팔의 조끼 교차는 모두0쌍이다. 뿌리는 몸에 붙도록 일부 들어가며 모든 몸 표면과 비접촉이라는 주장은 아니다.
- 손잡이 중심에서 팔 표면까지의 최근접 거리는 약1.71~3.83 cm다. 이는 손잡이와 팔 사이의 공기 틈을 측정한 값이 아니다. 총과 교차하는 팔 면의 중심이 손잡이 기준점에서10 cm 안인지 검사했고 실제 최대는 약7.08 cm였다. 의도된 손잡이 접촉을 포함하므로 총과 모든 교차가0이라는 뜻은 아니다.
- 기존 M4의 `Recesses and rubber` 메시에는 면적0에 가까운 면208개가 있다. 원본과 동일한 수를 유지하는지 별도로 검사했으며 새 팔의 결함으로 분류하지 않았다.
- Blender 렌더에서 포유류 원본 귀·꼬리의 거친 면과 얼굴 무늬 경계가 보인다. 형상 비교로 이번 팔 변경에서 생긴 손상이 아님을 확인했다. 이번 범위에서 귀·꼬리를 수정하지 않았다. 원본 GLB 정점 색을 표시한 결과이며 Godot의 절차적 피부 셰이더 렌더와 동일하지 않다.
- 다른 총·양손 운용·사격 반동·재장전·체형 변경·연속 보간·런타임 성능은 미검증이다. 세 정적 GLB 사이에 자동 전환 애니메이션이 들어 있지 않다. 실제 게임은 여전히 별도 캡슐 팔을 생성하므로 적용에는 해당 경로의 후속 작업이 필요하다.

검토: `doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`, 기존 무기 날개 후보 기록과 플레이어 종 정의. 기존 게임 보존 요청에 따라 제품 결정·이슈 상태는 바꾸지 않았다. 게임 기능·동작·설정 계약의 변경 없이 독립 아트 후보만 추가했으므로 검토 이유와 검사·제약을 이 폴더에 기록한다. 게임 검사는 실행하지 않았다.

## 재생성

저장소 루트 PowerShell에서 실행한다. 출력은 모두 `prototypes/grounded-art/` 안에 저장한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/build_species_grips.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/validate_species_grips.py
python prototypes/grounded-art/tools/compare_species_grips.py
```

생성 명령 끝에 `-- --preview`를 붙이면 수평 조준의 전신·확대12장만 렌더한다. 전체 비교판은 기본 생성 명령 후 만든다. `output/`은 재생성 가능한 로컬 산출물이다.
