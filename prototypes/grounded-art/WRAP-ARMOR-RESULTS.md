# 옆구리를 감싸는 방탄조끼 후보 03

2026-10-22. 사용자 요청: 군장 후보의 빈 옆구리를 채우되 날개 움직임 공간을 남기고, 기존 후보와 비교 렌더한다. 기존 게임 파일은 변경하지 않는다.

## 변경 내용

후보 01의 가슴판과 얇은 옆 끈을 **앞·옆·등이 이어지는 조끼 본체**로 교체했다. 실제 거위 몸 표면에서 여유를 둔 두께 있는 패널을 만들고, 날개가 붙는 앞쪽에 둥근 개구부를 냈다. 날개 뒤부터 등판까지 넓은 옆면을 유지하고, 그 위에 납작한 고정 띠와 가로 웨빙을 배치했다. 어깨도 조끼 본체와 이어진 넓은 면으로 바꿨다.

두 개의 앞 파우치는 유지하는 방향으로 새 본체에 맞춰 재제작했다. 몸·얼굴·날개·발·꼬리의 메시 11개와 헬멧·배낭 메시 2개는 보존했다. 방탄조끼는 외형 명칭이며 방어력이나 게임 수치를 바꾸지 않았다.

참고: [조끼 단품](source/reference/wrap-armor-reference-01.png), [여러 복장](source/reference/wrap-armor-reference-02.png). 이 후보의 구체적인 크기와 세부 디자인은 검토용 제작값이다.

## 파일

- [거위와 군장 전체 GLB](assets/goose-wrap-armor.glb)
- [방탄조끼만 GLB](assets/goose-wrap-vest-only.glb)
- [편집용 Blender 원본](source/goose-wrap-armor.blend)
- [자산 수치와 원본 해시](wrap-armor-manifest.json)
- [사선 비교](output/wrap-armor/comparison-quarter.png), [측면 비교](output/wrap-armor/comparison-side.png)
- [정면](output/wrap-armor/comparison-front.png), [후면](output/wrap-armor/comparison-rear.png), [위쪽](output/wrap-armor/comparison-top.png)
- [비교 모음](output/wrap-armor/comparison-overview.png), [날개 자세 검사 렌더](output/wrap-armor/wing-probes.png)

비교판은 **왼쪽 후보 01, 오른쪽 후보 03**이다. 같은 원점·몸·헬멧·배낭·카메라·조명에서 하나씩 렌더했다. Blender 5.2.2 LTS / Cycles 24 samples / denoise / AgX Medium High Contrast / 720×840. 실제 Blender 렌더를 배치하고 표제만 추가하며 모델 픽셀을 보정하지 않는다.

## 검증

GLB 두 개를 새 Blender 장면에 재가져오는 검사와 원래 날개의 제한된 자세 검사 **44개 통과**. 상세 결과는 [validation.json](output/wrap-armor/validation.json)에 있다.

생성·검사·비교 도구 3개의 Python 구문 검사도 통과했다. 최종 측면 비교, 4시점 비교 모음, 날개 자세 비교판을 직접 열어 옆면 연결·개구부·표제·구도를 확인했다.

- 기존 게임 거위 GLB, 후보 01 Blender·GLB, 작업복 후보 02 Blender의 SHA-256 보존.
- 보존 대상 메시 13개의 정점·면은 기존 후보와 같고, 복제 직후 세계 변환 오차는 0.000001 미만. 조끼 제작 전후에는 같은 메시·변환 해시인지 확인.
- GLB 메시 수·삼각형 수 일치, 면적 0인 면 없음, 축·경계 오차 0.0001 미만, 조끼 부모 노드·Muzzle 보존, 조끼 단독 내보내기에 몸 미포함.
- 양쪽 원래 날개에 기본 / 앞 20° / 뒤 15° / 바깥 20° / 앞 20°+바깥 15°의 **5가지 이산 자세**를 적용했다. 총 10개 날개 자세에서 조끼와 삼각형 표면 교차가 0이다. 기본·앞·바깥 자세는 별도 렌더도 생성했다.

현재 게임의 `CharacterWeaponGrip.BindWeaponGrip`은 원래 날개를 숨기고 별도의 무기용 팔을 만든다. 따라서 위 검사는 **연속적인 모든 동작이나 실제 총기·장전 동작의 무간섭 보장이 아니다.** 실제 무기용 팔, 체형 슬라이더, 다른 종, 피격·사망, Godot 성능·멀티는 통합 시 확인해야 한다.

| 대상 | 삼각형 | 메시 | 재질 |
|---|---:|---:|---:|
| 후보 01 조끼 | 5,436 | 1 | 4 |
| 후보 03 조끼 | 15,644 | 1 | 3 |
| 후보 03 캐릭터 전체 | 37,122 | 14 | 19 |

옆·등·어깨의 두께 있는 곡면과 테두리로 삼각형 수가 증가했다. 아직 LOD나 런타임 최적화는 하지 않았다. 재질 슬롯과 실제 드로콜은 별도다.

제작 중 객체 복제의 부모 변환 평가 시점 때문에 보존 검사가 먼저 실패했다. 부모 역행렬·로컬 변환을 보존하고 의존성 그래프를 갱신하도록 수정했다. 기존 후보와의 비교는 정점·면의 정확한 일치와 세계 변환 오차를 따로 검사해 부동소수점 재평가를 구별한다. 기존 원본 파일은 수정하지 않았다.

## 재현

저장소 루트에서 실행한다. 기존 후보 01 Blender 파일을 읽기 전용 스튜디오 입력으로 사용한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_wrap_armor.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_wrap_armor.py
python prototypes/grounded-art/tools/compare_wrap_armor.py
```

생성 명령 끝에 `-- --preview`를 붙이면 새 후보 사선·측면만 빠르게 렌더한다. 전체 비교판에는 전체 실행이 필요하다. 비교 도구는 기존 Pillow 의존성을 사용한다.

## 기록과 작업 경계

`doc/decisions.md`의 A-1, `doc/issues.md`의 I-28, 캐릭터 모델 계약과 무기용 팔 코드를 읽었다. 기존 제품 기능·동작·확정 계약은 바꾸지 않았으므로 게임 결정·이슈 ID를 새로 만들지 않았다. 이번 외형 수정 및 제작 중 수정은 이 기록과 별도 폴더의 도구·manifest에 보존한다.

출력은 `prototypes/grounded-art/` 내부 새 후보 파일뿐이다. 기존 게임 파일, 후보 01·02의 모델·생성기, Git 브랜치·인덱스는 수정하지 않았다. Godot 실행·게임 테스트는 수행하지 않았다.
