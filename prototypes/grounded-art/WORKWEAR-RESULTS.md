# 거위 작업복 후보 02 제작 기록

2026-10-22. 사용자 첨부 이미지 3장을 참고한 독립 아트 후보. 기존 게임에는 적용하지 않았다.

## 이번 후보

황토색 천 모자와 청회색 작업 조끼 한 벌을 제작했다. 군장 없이도 완성된 복장으로 보이는지 확인하는 후보이며, 몸·얼굴·날개·발·꼬리는 기존 거위의 메시 11개를 그대로 유지했다.

조끼는 실제 몸 표면을 따라 여유를 둔 얇은 옷으로 만들었다. 앞 여밈, 둥근 팔 구멍, 몸통과 이어지는 어깨, 얕은 주머니 두 개, 꼬리 쪽 밑단 여유를 표현했다. 모자는 짧은 챙과 절제된 봉제선을 넣었다. 모자와 조끼는 각각 떼어낼 수 있고 배낭·방탄판·탄창 파우치는 넣지 않았다. 색과 구체적인 의복 종류는 이번 제작안이며 최종 확정값이 아니다.

세 번째 참고 이미지에서는 위쪽 시점에서도 보이는 머리 장식과 상의의 큰 색면, 드러나는 얼굴·날개를 우선했다. 작은 이미지에 보이지 않는 주머니·뒷면 구조를 그대로 재현했다고 주장하지 않는다. 동일한 거위에서 복장 변화만 비교하기 위해 얼굴·체형·포즈는 바꾸지 않았다.

참고 이미지 보존본: [첫 이미지](source/reference/workwear-inspiration-01.png), [두 번째 이미지](source/reference/workwear-inspiration-02.png), [위쪽 시점 이미지](source/reference/workwear-inspiration-03.png).

## 파일과 비교 렌더

- [거위와 작업복 GLB](assets/goose-workwear.glb)
- [모자와 조끼만 GLB](assets/goose-workwear-only.glb)
- [편집용 Blender 원본](source/goose-workwear.blend)
- [측정값과 원본 해시](workwear-manifest.json)
- [기존형과 두 후보의 사선 비교](output/workwear/comparison-quarter.png)
- [위쪽 시점 비교](output/workwear/comparison-top.png)
- [작은 화면 크기 비교](output/workwear/comparison-small.png)
- [작업복 여러 방향 모음](output/workwear/workwear-overview.png)

비교판은 왼쪽부터 기존 기본 군장, 군장 후보 01, 작업복 후보 02다. 동일한 스튜디오에서 모델을 하나씩 같은 원점에 렌더했다. 사선·정면·측면·후면·위쪽·작은 화면 크기의 6시점 × 3모델로 원본 PNG 18장을 생성한다. 각 시점의 조명·카메라·렌더 설정·원래 몸 재질은 동일하다. 비교 도구는 PNG 배치와 표제만 수행한다.

Blender 5.2.2 LTS, Cycles 24 samples, denoise, AgX Medium High Contrast. 일반 720×840, 작은 화면 크기 720×480. 위쪽 시점은 참고용 Blender 카메라이고 Godot 화면 재현은 아니다.

## 측정과 검사

| 대상 | 삼각형 | 메시 | 재질 |
|---|---:|---:|---:|
| 거위와 작업복 전체 | 34,214 | 13 | 18 |
| 모자와 조끼 | 19,016 | 2 | 6 |
| 모자 | 4,516 | 1 | 2 |
| 조끼 | 14,500 | 1 | 4 |

곡면과 옷 테두리를 직접 모델링한 검토용 후보로, 군장 후보 01보다 삼각형이 많다. 런타임 최적화·LOD·실제 성능 검증은 별도다. 메시 개수는 드로콜 수와 같지 않다.

생성기는 원래 메시 11개의 정점·면·변환 해시를 기준 모델과 비교한다. 기존 게임 GLB와 군장 후보 01 Blender 파일은 생성 전후 SHA-256으로 보존 여부를 확인한다. 새 GLB 두 개는 별도 Blender 장면에 다시 가져와 메시 수, 삼각형 수, 면적 0인 면, 축·크기 오차, 부모 노드, 독립 모자·조끼, Muzzle, 배낭 미포함을 검사한다. 결과는 [검사 JSON](output/workwear/validation.json)에 남긴다.

최종 실행 결과: **GLB 재가져오기 38개 검사 통과**, 생성기·검사기·비교 도구 Python 구문 검사 통과. 원본 PNG 18장과 비교판 7장을 생성했다. 최종 사선·위쪽 비교판과 전체 방향 모음을 직접 열어 얼굴·날개 노출, 조끼 연결, 후면 꼬리 여유, 작은 크기의 색면, 표제와 배치를 확인했다.

첫 형태의 열린 옆면은 어깨 연결이 부족했고, 덧댄 어깨 띠는 별도 끈처럼 보여 수정했다. 최종 조끼는 앞·뒤·어깨가 이어진 하나의 천 표면에 타원형 팔 구멍을 만드는 형태다. 고정 자세의 실제 렌더로 착용 위치와 모자·옷의 윤곽을 확인했다.

## 재현

저장소 루트에서 실행한다. 군장 후보 01의 `source/goose-field-kit.blend`가 비교 스튜디오 입력으로 필요하다. 기존 생성기를 수정하거나 게임을 빌드할 필요는 없다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_workwear.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_workwear.py
python prototypes/grounded-art/tools/compare_workwear.py
```

빠른 형태 확인은 생성 명령 끝에 `-- --preview`를 붙인다. 후보의 사선·위쪽 시점만 렌더하므로 전체 비교판을 다시 만들기 전에는 위 전체 명령을 실행해야 한다. Python 비교판 도구는 기존 Pillow 의존성과 맑은 고딕을 사용한다.

## 작업 경계와 남은 확인

쓰기 대상은 `prototypes/grounded-art/` 안의 새 작업복 파일·문서다. 기존 게임 코드·모델·군장 후보 01은 수정하지 않았다. `doc/decisions.md`와 `doc/issues.md`, 캐릭터 모델 계약을 검토했으며 게임 기능·동작·확정 계약 변경이 없어 제품 결정이나 버그 ID를 추가하지 않았다.

현재 고정 자세의 외형 후보다. 조준·걷기·피격·사망, 몸 크기 커스터마이징, 다른 종, 군장과 겹쳐 입기, Godot 가져오기·성능·멀티는 검증하지 않았다. 일반 복장 슬롯이나 저장 형식은 구현하지 않았다. 모자·조끼가 기존 부착 노드를 사용한다는 사실만으로 게임에 즉시 교체할 수 있는 완제품은 아니다.
