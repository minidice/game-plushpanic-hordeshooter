# 접힌 날개 개선 후보

2026-10-23. 사용자 요청: 작고 둥근 날개의 형태와 완성도 개선. 독립 아트 후보이며 **게임 미적용**이다. 기존 얼굴 장비 후보의 고글 모자·방탄조끼 조합을 기준으로 날개만 교체했다.

## 조형 변경

- 어깨는 좁아지며 몸에 들어가도록 연결하고, 가운데는 납작하게 부풀린 접힌 면으로 만들었다.
- 아래 끝은 길이가 다른 둥근 깃털 세 갈래다. 분리된 장식을 붙이지 않고 한쪽당 하나의 닫힌 메시로 제작했다.
- 몸의 앞·아래쪽으로 흐르는 윤곽과 얕은 접힘을 넣었다. 재질은 기존 날개 재질을 유지했다.
- 조끼 구멍과 다른 장비는 수정하지 않았다. 원래 `goose_leftArm` / `goose_rightArm` 피벗과 메시 이름·부모 연결을 유지했다.

오른쪽 날개 월드 경계 상자 기준이며, 펼친 날개의 해부학적 길이를 뜻하지 않는다.

| 항목 | 기존 | 개선 |
|---|---:|---:|
| 세로 길이 Z | 0.310 m | 0.409 m (+32%) |
| 앞뒤 폭 Y | 0.291 m | 0.276 m |
| 옆 두께 X | 0.243 m | 0.105 m (-57%) |
| 양쪽 날개 삼각형 | 272 | 2,448 |
| 캐릭터 전체 삼각형 | 32,122 | 34,298 |

첫 조형 시안은 양쪽 5,100삼각형이었다. 최종 후보는 면 분할을 줄인 뒤 다시 렌더하고 검사했다. 기존 모델보다 폴리곤은 증가했으므로 런타임 성능 개선으로 표현하지 않는다.

## 모델과 비교

- [캐릭터 포함 GLB](assets/goose-wing-character.glb)
- [날개만 GLB](assets/goose-wing-only.glb): 좌우 메시 2개와 기존 부모 노드. 다른 몸·장비 메시 없음.
- [Blender 편집 원본](source/goose-wing-study.blend): 기존과 개선 모델을 보존. 기본 표시 상태는 개선 모델.
- [전신 전후](output/wing-study/comparison-quarter.png)
- [날개 확대 전후](output/wing-study/comparison-detail.png)
- [측면 전후](output/wing-study/comparison-side.png)
- [위쪽 시점 전후](output/wing-study/comparison-top.png)
- [무기 착용 형태 참고](output/wing-study/comparison-held.png)
- [모델 명세](wing-study-manifest.json), [검사 결과](output/wing-study/validation.json)

비교는 동일한 몸·장비·재질·중립 조명·카메라를 사용했다. 이미지 합성 도구는 렌더를 나란히 배치하고 제목만 추가한다. 모델 픽셀을 보정하지 않는다. 무기 착용은 기존 M4 GLB를 읽어 Blender에서 날개와 함께 배치한 참고 포즈다. 이전 날개와 새 날개에 같은 회전을 적용했으며, 게임의 캡슐 팔을 재현한 렌더는 아니다.

## 검사와 제약

실행 확인: Blender 5.2.2 LTS 생성·렌더 정상 종료, GLB 재수입 검사 **112항목 통과**, 비교 이미지 5장 생성. 원본 렌더 10장을 생성했고 전신·확대·측면·위쪽·무기 참고 비교를 시각 검토했다. 최종 로그에 Python 예외나 종료 경고는 없었다.

- 보호 대상 53파일 SHA-256 일치: 이전 GLB/Blender 원본, 게임 거위와 M4 모델. 이전 생성기와 게임 소스는 수정하지 않았다.
- 내보낸 두 GLB의 메시·삼각형 수, 축·경계, 부모·피벗, 비영 면적과 닫힌 날개 표면을 확인했다.
- 캐릭터 GLB의 날개 외 12개 메시가 기준 모델과 동일하다. 내보내기 전 정점·면·행렬 일치를 확인하고, 재수입 후 월드 정점 위치의 양방향 최근접 오차가 0.00001 m 미만인지 검사했다.
- 첫 검증에서는 소수점 반올림한 정점 집합의 완전 일치가 발에서 실패했다. glTF 왕복 부동소수점 오차를 직접 측정하도록 검사법을 수정했다. 실제 모델을 바꿔 검사를 맞추지 않았다.
- 기본·앞20도·뒤15도·바깥20도·앞20도+바깥15도: 좌우 총10개 자세에서 조끼와 날개 표면 교차 0쌍. 날개 뿌리는 몸에 연결되도록 일부 들어간다. 모든 몸 표면과 비접촉을 주장하지 않는다.
- 연속 동작, 체형 슬라이더, 다른 복장, Godot 사격·재장전·성능은 미검증이다. `CharacterWeaponGrip.cs`는 무기 착용 시 원래 날개를 숨기고 별도 팔을 생성한다. 따라서 이 GLB 교체만으로 무기 팔까지 바뀌지는 않는다. 무기 참고 그림은 그 통합 작업의 완료 증거가 아니다.

검토 문서: `doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`. 제품 기능·동작·수치 계약을 변경하지 않은 격리된 아트 후보이므로 기존 결정·이슈 상태를 변경하지 않았다. 기존 게임 파일을 보존하라는 요청에 따라 검토 이유와 새 조형·검사 기록을 이 독립 폴더에 남겼다. 게임 검사는 실행하지 않았다.

## 재생성

저장소 루트 PowerShell에서 실행한다. 출력은 `prototypes/grounded-art/` 내부에만 쓴다. `--python-exit-code 1`은 Python 예외를 프로세스 실패로 전달한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/build_wing_study.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/validate_wing_study.py
python prototypes/grounded-art/tools/compare_wing_study.py
```

빠른 조형 확인은 생성 명령 끝에 `-- --preview`를 붙인다. 전신·확대 4장만 생성하므로 전체 비교 생성 전에는 기본 명령으로 모든 렌더를 만든다. `output/`은 재생성 가능한 로컬 산출물이며 버전 관리 대상이 아니다.
