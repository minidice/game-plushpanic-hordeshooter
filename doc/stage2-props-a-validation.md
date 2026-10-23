# Stage 2 이동 장치 묶음 A 생성·렌더 검수

2026-10-23. 사용자 후속 요청으로 Codex의 범위를 스크립트 작성에서 **Blender 실행, GLB 생성 및 렌더 검수**까지 확장했다. 게임 연결은 포함하지 않는다. 기준은 원격 작업 브랜치 `9412666`의 [모델 브리프](https://github.com/minidice/game-plushpanic-hordeshooter/blob/9412666/doc/model-briefs/stage2-landmarks.md) 1·2·5장이다. 해당 브리프/높이 단 구현이 없는 현재 `main`에 원격 작업 브랜치를 병합하지 않았다.

## 결과와 자산 계약

모든 파일은 [game/assets/stage2-props](../game/assets/stage2-props)에 있다. 원본은 [stage2-props-a.blend](../prototypes/outdoor-kit/source/stage2-props-a.blend). 모델은 시각용이며 충돌은 `none`, 런타임 게임 코드가 소유한다. 부위는 루트 아래 빈 노드와 그 아래 메시 하나씩, 게임 +Y 위·Blender +Z 위, 카메라 쪽은 Blender −Y다. 재질은 텍스처 없이 키트 색상을 선형 변환한다.

| ID | 실제 삼각형 / 예산 미만 | 실제 크기 X×Y×Z(m, 게임 축) | 구조·피벗 |
|---|---:|---|---|
| `ladder_section` | 256 / 600 | 0.620×1.200×0.120 | `Keep`, 바닥 0, 1.2m 간격으로 적층 |
| `ladder_top` | 360 / 500 | 0.626×1.050×0.636 | `Keep`, 윗단 바닥 0, 하단 −0.15·정상 0.9 |
| `zipline_anchor` | 668 / 1,500 | 0.600×3.200×1.050 | `Keep`·`Occlude_Pole`, 바닥 0, 도르래 중심 `(0,3.0,−0.4)` |
| `gondola_cabin` | 1,284 / 3,000 | 4.000×3.730×4.006 | `Keep`·`Occlude_Frame`, 바닥 윗면 0·하단 −0.15, 바퀴 중심 높이 3.4 |
| `gondola_station` | 1,376 / 3,000 | 4.850×5.600×5.200 | `Keep`·`Occlude_Tower`, 4×4 탑승 영역 중심 피벗, 뒤 탑 4×1.2×5, 바퀴 포함 높이 5.6 |

발판·팔·녹 표면 장식까지 포함한 AABB이며, 충돌 크기나 사람이 걷는 영역을 뜻하지 않는다. 재질 개수는 모델별 2/3/4/3/4개. `Occlude_*` 분리는 후속 가림 연출용이며 게임의 자동 숨김/반투명 처리가 구현됐다는 뜻은 아니다.

## 재생성·검사

저장소 루트 PowerShell에서 실행한다. Blender 5.2.2 LTS에서 확인했다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py -- --manifest-merge
python -m py_compile prototypes/outdoor-kit/tools/stage2_props_a.py prototypes/outdoor-kit/tools/stage2_props_a_review.py
```

- 생성기: 5종의 예산·치수·피벗·재질·부위 검사를 통과하고 `STAGE2_PROPS_BUILT A 5`를 출력했다. 기존 다른 묶음의 manifest 항목은 보존하며 기존 키트 생성기를 import/수정하지 않는다.
- 검토기: **내보낸 GLB를 다시 읽어** 헤더·실제 삼각형·재질·부위·치수·축·피벗, 묶음 A 정확히 5종 포함 여부 및 검수 카메라 화면 범위를 확인한다. 최종 **112개 검사, 13장 Cycles 렌더**를 통과했다. 5종의 카메라 쪽/뒷면, 사다리 3단+손잡이(전체 4.5m), 객차의 1.2m 크기 비교용 인형 4개, 상부 프레임 숨김 비교를 확인했다.
- manifest 검사: 임시 팩에 다른 묶음의 항목/사용자 메타데이터와 오래된 A 항목을 넣고 실제 생성기를 두 번 실행했다. 다른 묶음 보존·A 항목 교체·중복 없음·반복 결과 동일 및 검토 입력의 누락/중복 거부를 확인했다. 실제 배포 팩은 이 검사에서 변경하지 않는다.
- 로그: `game/output/stage2-props-a-build-final.log`, `stage2-props-a-review-verified.log`, `stage2-props-a-manifest-verified.log`. 렌더와 보고서는 `game/output/stage2-props-a-review/`에 재생성된다. 이 `output/`은 커밋 대상이 아니다.

## 발견·수정과 경고

- 첫 실제 렌더에서 승강장 기둥·가로보 접합부가 검게 보였다. 기둥 끝과 가로보의 윗면/옆면이 같은 평면을 공유했다. 높이 5m 기둥을 가로보 안에 끝나는 4.84m로 줄이고 단면을 0.24→0.22m로 좁혔다. 가로보가 탑 전체 높이 5m를 유지한다. 검사에서 최초 겹친 삼각형 쌍 40개를 검출했고, 수정 후 남은 10개는 바퀴 허브 안에 가려진 살대 끝면이었다. 재발 검사는 **노출된 탑 상단 좌우 접합부**로 한정했으며 최종 0개와 전후 렌더로 확인했다. 전체 모델의 모든 내부 겹침을 제거했다는 의미가 아니다.
- 독립 검토에서 뒷면 렌더의 하단 잘림과 필수 자산이 manifest에서 빠졌을 때 건너뛰는 검수 도구 문제를 발견했다. 투영된 경계의 중앙으로 카메라를 보정하고 전 정점 화면 포함 검사를 추가했다. 검사 전에 A의 5개 ID/중복 여부를 확인하며 누락·중복 입력이 거부되는 것을 별도로 확인했다.
- Blender `Material.use_nodes`의 6.0 폐기 예정 경고와 기본 브러시 재질 외부 경로를 상대 경로로 만들 수 없다는 저장 경고가 남았다. GLB 생성·재가져오기·최종 렌더 실행은 종료 코드 0이다. 중간 검사 종료 지연 중 로그가 잠겨 재실행 시도가 한 번 실패했으나 후속 실행은 정상 종료했다. 이 경고/시도를 통과 결과에 숨기지 않는다.

## 남은 연결 작업

- 원격 `Zipline.BuildLook` 줄 높이 2.8m와 브리프/모델 도르래 기준 3.0m의 차이, 로컬 기준점 `(0,3.0,−0.4)` 및 설치 회전을 맞춰야 한다. 줄 자체는 게임이 그린다.
- 객차 바닥판 윗면은 0m다. 기존 게임의 탑승판 그림과 겹치지 않도록 교체해야 한다. 승강장에는 중앙 바닥판을 만들지 않았다.
- 객차 매달기 팔은 기본 쿼터뷰에서 일부 위치의 인형 머리를 가린다. 숨김 비교는 부위 분리만 확인한 것이며, 실제 게임의 가림 연출·최대 인원 탑승·승하차 경로 검증은 남는다.
- 실제 캐릭터·맵·Godot 가져오기/전용 장면·이동/충돌/멀티·성능 검사는 실행하지 않았다. C#이나 맵 데이터는 변경하지 않아 `Integration`도 이번에 실행하지 않았다. 자산 렌더 검수와 제품 내 통합 검수를 구분한다.
