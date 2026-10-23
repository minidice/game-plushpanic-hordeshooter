# 귀·꼬리 복원과 종별 착용 핏 후보

2026-10-23. 사용자 요청에 따라 별도 후보에서 라쿤·레서판다의 거친 귀·꼬리를 정리하고, 거위·오리·라쿤·레서판다의 체형 변경과 장착물 배율을 함께 검사했다. 게임에는 적용하지 않았다. 이전 후보와 게임 파일은 보존했다.

## 확인한 원인과 변경

- 기존 원본 귀는 정점 용접 후 열린 모서리 0개지만, 게임용 경량화 귀는 한쪽당 334개였다. 꼬리는 원본의 양 끝에만 48개가 있었지만 경량화 후 라쿤 537개, 레서판다 536개였다. 기존 `optimize_game_roster.py`는 분리된 정점을 용접하지 않고 귀·꼬리에 0.19 비율의 Decimate를 적용한다. 연결이 끊긴 채 면이 줄어드는 경로와 실제 구멍을 확인했다. 이전 팔 후보가 이 메시를 그대로 사용했으므로 기존 후보에도 손상이 보였다.
- 새 후보는 경량화 전 원본 귀·꼬리만 가져와 정점을 용접하고 노멀을 다시 계산했다. 귀는 닫힌 외피와 안쪽 면의 원래 모양·배치를 유지하고 면적 가중 노멀을 적용했다. 꼬리는 연속된 줄무늬 표면을 복원하고 양 끝을 닫았다. 몸·얼굴·팔·발 형상은 그대로다.
- 이전 다른 종 후보는 거위의 조끼를 동일 좌표로 복사했다. 다른 세 종은 머리 기준점이 거위보다 0.12 m 낮아 목선이 얼굴에 더 가까워졌다. 새 조끼는 앞·뒤 윗부분만 최대 0.12 m 낮추고 패치와 띠도 같은 변형을 적용했다. 팔 구멍 주변과 밑단은 유지한다. 거위의 승인된 기본 핏은 바꾸지 않았다.
- `FitRoot` 아래 몸·조끼·머리 장비·무기·잡는 위치를 함께 배치했다. 폭·키·전체 크기를 바꿀 때 동일 변환을 한 번만 적용한다. 크기를 원래 값으로 되돌려도 누적 변형이 생기지 않는다. 현재 게임 `CharacterPartShapes.cs`에도 이미 `Model.Scale` 공통 배율이 있으므로 게임에 해당 기능이 없다고 판단한 것은 아니다. 이번 수정은 독립 후보의 종별 기본 핏과 편집 구조에 해당한다.

## 파일과 비교

- [Blender 편집 원본](source/fitted-species-study.blend)
- [귀·꼬리 전후](output/fitted-species/surface-comparison.png)
- [네 종 조끼 핏 전후](output/fitted-species/fit-comparison.png)
- [폭·키를 다르게 조절한 비교](output/fitted-species/size-comparison.png)
- [자산 목록·보호 파일 해시](fitted-species-manifest.json)
- [생성 검사](output/fitted-species/build-checks.json), [GLB 재가져오기 검사](output/fitted-species/validation.json), [원본 표면 진단](output/fit-surface-diagnosis.json)

GLB는 `assets/fitted-species-{goose|duck|raccoon|redpanda}-{ready|aim|aim_up}-{character|preview}.glb` 24개다. `character`는 총을 제외하고 `preview`는 M4를 포함한다. 세 정적 자세이며 애니메이션 클립이 아니다.

| 종 | 캐릭터 메시 수 | 캐릭터 삼각형 수 |
|---|---:|---:|
| 거위 | 14 | 34,298 |
| 오리 | 13 | 24,986 |
| 라쿤 | 15 | 27,215 |
| 레서판다 | 15 | 28,022 |

## 크기 조절 계약

Blender에서 해당 종·자세의 `..._after_FitRoot`를 선택하고 사용자 속성을 조절한다. `body_width`는 폭·깊이 0.88–1.12, `body_height`는 키 0.90–1.10, `uniform_size`는 전체 크기다. 축별 scale은 이 속성의 드라이버가 계산하므로 드라이버와 scale을 중복 적용하지 않는다. 현재 장면은 라쿤 수평 조준 후보가 보이도록 저장했다.

GLB에는 Blender 드라이버가 전달되지 않는다. GLB를 다른 도구에서 사용할 때는 `FitRoot`의 TRS scale을 직접 변경해야 한다. Blender 축에서는 `(폭, 폭, 키)`, glTF/Godot 축에서는 `(폭, 키, 폭)`에 전체 배율을 곱한다. 장비를 추가할 때는 종별 기본 핏을 먼저 맞춘 뒤 이 루트 아래의 해당 장비 피벗에 장착한다. 몸과 장비 각각에 같은 배율을 다시 넣으면 이중 확대된다.

이 규칙은 전체 체형 크기를 바꾸는 경우다. 머리만 확대하거나 날개만 길게 하는 독립 부위 조절, 배낭·모든 옷 후보의 체형별 재피팅은 이번에 검증하지 않았다.

## 검증과 제약

- 생성 검사 1,676항목, GLB 24개 재가져오기 검사 2,811항목 통과. 두 Blender 실행 모두 종료 코드 0. 실제 렌더 20장과 비교판 3장을 만들고 육안 확인했다.
- 라쿤·레서판다의 귀·꼬리는 재가져온 메시에서도 용접 후 모든 모서리가 닫히고, 면적 0인 면이 없으며, 전체 체적 방향이 바깥쪽이다.
- 네 종 × 세 자세의 조끼와 좌우 팔·몸 표면 교차는 모두 0쌍이다. 공통 양의 배율은 이 관계를 유지한다. 몸 안으로 들어가는 팔 뿌리나 의도된 총 손잡이 접촉까지 모두 비접촉이라는 뜻은 아니다.
- 생성 단계에서 기본값, 폭·키 네 극단 조합, 전체 60% 크기, 원상 복귀를 검사했다. 재가져온 GLB에서도 네 극단 조합의 모든 메시가 같은 루트를 따라가는지 확인했다. 실제 렌더는 기본값 및 가늘고 긴/넓고 짧은 두 조합이다.
- 이전 후보와 양방향 최근접 정점 거리로 비교해 수정 대상 밖의 몸·얼굴·팔·발·총·거위 복장이 0.00001 m 안에서 보존되었음을 확인했다. 이전 후보·게임 원본·검토 문서를 포함한 보호 파일 99개의 SHA-256이 일치한다.
- 첫 생성 실행은 귀 메시를 합친 후 삭제된 Blender 객체를 다시 참조한 도구 오류로 종료했다. 살아 있는 객체 목록을 매번 조회하도록 고친 뒤 전체 생성·검사를 완료했다. 게임 오류가 아니다.
- 실게임 애니메이션·멀티·다른 총·모든 복장 조합·성능은 미검증이다. GLB 후보 이름과 FitRoot는 편집용 구조이므로 기존 게임에 파일만 교체해 바로 사용할 수 있다는 보장은 없다. 얼굴의 원본 정점 색 경계와 기존 M4의 퇴화면은 이번 대상이 아니다.

`doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`와 모델 배율 구현을 검토했다. 이번에는 기존 게임 보존·독립 폴더 작업 범위에 따라 제품 기능·결정·이슈 상태를 변경하지 않고, 후보의 원인·수정·검증·제약을 이 문서에 기록했다. 제품 기능 체크리스트·게임 설정도 그대로다. 게임 검사는 실행하지 않았다.

## 재생성

저장소 루트에서 실행한다. 입력은 이전 후보 GLB/Blender 파일 및 기존 `prototypes/character-lab/output/roster/{raccoon|redpanda}-source.glb`다. 후자는 원본 모델 생성 과정의 로컬 산출물이며 이번 작업에서 생성기를 실행하거나 덮어쓰지 않았다. 편집 원본 Blend와 최종 GLB에는 복원한 표면이 포함되어 있다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/inspect_fit_surfaces.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/build_fitted_species.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/validate_fitted_species.py
python prototypes/grounded-art/tools/compare_fitted_species.py
```

생성기는 `-- --no-render` 옵션으로 모델만 재생성할 수 있다. 비교 PNG는 `output/`의 재생성 산출물이다.
