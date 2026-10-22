# 독립 아트 작업 폴더

**2026-10-23 게임 적용:** 사용자 요청으로 최신 기본 조끼·네 종 날개/앞발·포유류 기본 귀/꼬리를 게임에 연결했다. 독립 후보는 그대로 보존하며, 명시적 게시 도구 `tools/publish_game_models.py`만 `game/assets/characters/*/*-fitted.glb`를 만든다. [게임 적용 범위·실행·검증](../../doc/grounded-art-integration.md). 아래 후보 제작 당시의 미적용 기록과 쓰기 제한은 당시 작업 범위다. 일반 후보 생성기는 계속 이 폴더 안에서만 동작한다.

최신 표면·핏: **네 종의 귀·꼬리 복원 및 체형 연동 후보** — 라쿤·레서판다의 갈라진 면을 복원하고, 거위 기준을 유지하면서 다른 종의 조끼 목선을 조정했다. 공통 FitRoot로 몸·옷·장착물을 함께 조절한다. [모델·검사 기록](FITTED-SPECIES-RESULTS.md), [귀·꼬리 전후](output/fitted-species/surface-comparison.png), [종별 착용 전후](output/fitted-species/fit-comparison.png), [체형 비교](output/fitted-species/size-comparison.png), [Blender 원본](source/fitted-species-study.blend). 게임에는 미적용이다.

최신 종별 팔: **오리·라쿤·레서판다 무기 자세 후보** — 오리의 깃털과 포유류의 둥근 앞발을 구분해 대기·수평·위쪽 조준을 제작했다. [모델·검사 기록](SPECIES-GRIPS-RESULTS.md), [세 종 조준 모음](output/species-weapon-grips/lineup-aim.png), [세 종 × 세 자세](output/species-weapon-grips/all-poses.png), [Blender 원본](source/species-grips-study.blend). 정적 아트 후보이며 게임에는 미적용이다.

최신 무기 날개: **대기·수평 조준·위쪽 조준 후보** — 접힌 면을 유지하며 손잡이를 감싸는 날개. [모델·검사 기록](WEAPON-WINGS-RESULTS.md), [세 자세 모음](output/weapon-wings/pose-lineup.png), [조준 확대 전후](output/weapon-wings/comparison-aim-detail.png), [Blender 원본](source/goose-weapon-wings-study.blend). 정적 후보이며 게임 무기 팔·애니메이션에는 미적용이다.

최신 날개: **접힌 날개 개선 후보** — 좁은 어깨 연결, 납작한 면과 둥근 깃털 끝 세 갈래. [모델·검사 기록](WING-STUDY-RESULTS.md), [전신 비교](output/wing-study/comparison-quarter.png), [날개 확대](output/wing-study/comparison-detail.png), [Blender 원본](source/goose-wing-study.blend). 게임 미적용이며 무기 착용 그림은 별도 참고 포즈다.

최신 얼굴 장비: **후보 15–19** — 모자 없는 안경·선글라스·헤드셋·얼굴 고글, 비율을 개선한 고글 모자와 선글라스+헤드셋 조합. [모델과 제작·검사 기록](FACE-ACCESSORIES-RESULTS.md), [얼굴 확대 비교](output/face-accessories/accessories-details.png), [고글·모자 전후](output/face-accessories/goggles-before-after-detail.png), [Blender 원본](source/goose-face-accessories.blend). 장비 단독 6개와 캐릭터 포함 6개 GLB이며 게임에는 적용하지 않았다.

최신 모자: **후보 08–14** — 중앙 줄무늬 투구·캡·곡면 고글·오토바이 헬멧·야시경·토끼귀·꽃 모자. [모델과 제작·검사 기록](HEADWEAR-COLLECTION-RESULTS.md), [모자 확대 모음](output/headwear-collection/headwear-details.png), [고글 전후 비교](output/headwear-collection/goggles-before-after.png), [Blender 원본](source/goose-headwear-collection.blend). 머리 장비 단독 7개와 캐릭터 포함 7개의 GLB이며 게임에는 적용하지 않았다.

최신 추가: **경량형 05·정찰형 06·중장비형 07** — 투구 3종과 조끼 3종, 캐릭터 포함 GLB 3개. [제작·검사 기록과 모델](OUTFIT-COLLECTION-RESULTS.md), [새 세 벌 비교](output/outfit-collection/new-mood-quarter.png), [기본형 포함 비교](output/outfit-collection/lineup-neutral.png), [Blender 편집 원본](source/goose-outfit-collection.blend). 게임 미적용이며 이전 후보는 보존했다.

기본형 정리: **기본 방탄형 후보 04** — 큰 면·얇은 테두리·재질을 정리하고 조끼 삼각형을 54.8% 줄였다. [제작·검사 기록과 GLB](REFINED-ARMOR-RESULTS.md), [모델×조명 비교](output/refined-armor/lighting-matrix.png), [방향별 모음](output/refined-armor/candidate-overview.png). 이전 후보는 모두 보존했다.

상태: 2026-10-22 **거위 군장 후보 01 제작**. 기존 게임에는 적용하지 않았다. 제작 내용·검사·재현 방법은 [결과 기록](RESULTS.md)을 참조한다.

후속 제작: **작업복 후보 02** — 황토색 천 모자와 청회색 작업 조끼. [제작 기록과 GLB](WORKWEAR-RESULTS.md), [기존형·군장·작업복 비교](output/workwear/comparison-quarter.png), [위쪽 시점 비교](output/workwear/comparison-top.png). 군장 후보 01은 유지했다.

최신 군장: **방탄조끼 후보 03** — 옆구리까지 연결된 조끼와 날개 개구부. [제작·검사 기록과 GLB](WRAP-ARMOR-RESULTS.md), [후보 01과 측면 비교](output/wrap-armor/comparison-side.png), [비교 모음](output/wrap-armor/comparison-overview.png). 헬멧·배낭·몸은 유지했다.

- [기존/후보 사선 비교](output/comparison-quarter.png)
- [정면·측면·후면 비교 모음](output/comparison-overview.png)
- [상단 시점·작은 크기 비교](output/comparison-gameplay.png)
- [거위 포함 GLB](assets/goose-field-kit.glb) / [군장만 GLB](assets/goose-equipment-only.glb)
- [편집 가능한 Blender 원본](source/goose-field-kit.blend) / [자산 명세](manifest.json)

이미지는 이 폴더의 `output/`에 생성하는 로컬 산출물이다. 원본·생성기·GLB는 버전 관리 대상이다.

목표와 변경안: [진지한 공간 속 귀여운 동물](../../doc/grounded-art-direction.md).

후속 참고: [군장과 일반 복장의 공통 조형 기준](wardrobe-direction.md). 사용자 첨부 이미지의 옷 형태를 검토하고 작업복·점퍼·우비까지 확장할 때의 제작 기준을 정리했다. 작업 조끼는 후보 02로 제작했고 점퍼·우비·겹쳐 입기 등은 후속 제안이다.

## 이번 제작 범위

거위 기본형을 기준으로 헬멧·조끼·파우치·배낭 후보 한 세트를 제작했다. 이번 비교는 군장에 집중하며, 문·드럼통·컨테이너를 이용한 상대 크기 확인과 배경 개편은 후속 검토 범위다.

기존 체형과 종 식별 특징을 유지하고, 착용 위치·연결 구조·두께·재질을 우선한다. 기존 원본을 참고할 수 있으나 원본이나 생성기를 수정하지 않는다.

## 디렉터리 계약

필요한 시점에 하위 폴더를 만든다.

| 위치 | 용도 | 버전 관리 |
|---|---|---|
| `tools/` | 이 폴더 안으로만 내보내는 생성·검사·렌더 도구 | 포함 |
| `source/` | 편집 원본 `.blend` 등 | 포함 |
| `assets/` | 검토 대상 GLB·텍스처 | 포함 |
| `manifest.json` | 자산 ID·경로·크기·축·노드·삼각형·재질 수·검증 상태 | 제작 시 포함 |
| `output/` | 비교 PNG·영상·검사 중간 산출물 | 제외 — 저장소 `.gitignore` 적용 |

## 병행 작업 규칙

- 파일 쓰기는 이 폴더 내부로 제한한다.
- `game/`과 기존 `prototypes/character-lab/`, `prototypes/outdoor-kit/`는 읽기 참고만 한다.
- 기존 생성기를 그대로 실행하면 `game/assets/` 등을 덮어쓸 수 있다. 출력 경로를 점검한 독립 도구만 실행한다.
- 새 GLB를 기존 `.tres`에 연결하거나 기존 자산 파일명으로 교체하지 않는다.
- 상대 크기 검토를 위해 게임의 캡슐·이동·무기·카메라·네트워크 코드를 변경하지 않는다.
- 전역 패키지 변경·공유 Git 브랜치 변경·전체 파일 stage를 하지 않는다.
- 게임 통합은 외형 검토와 멀티 작업 상황 확인 후 별도 진행한다.

## 모델 계약과 확인

- [현재 캐릭터 계약](../../doc/character-model-spec.md)의 미터 단위, Godot/glTF +Y 위·+Z 정면, 발바닥 원점, 부품 분리·피벗·노드 연결을 참고한다.
- Blender 내부 축과 glTF 내보내기 축을 구분한다. 좌표를 바꿀 때 가정과 실제 내보내기 결과를 기록한다.
- 기존 런타임이 참조하는 노드·장비 분리 구조를 유지하는 후보를 우선한다. 변경이 필요한 구조는 manifest에 통합 필요 항목으로 명시한다.
- 정면·측면·후면·쿼터뷰와 플레이 크기에서 기존형/후보를 같은 조건으로 비교한다.
- 모델링 렌더 확인과 Godot 적용·멀티·성능 검증을 구별해 기록한다.

## 작업 시작점

먼저 원본 거위·군장과 관련 생성기의 출력 경로·노드 구조를 읽는다. 기존 자료의 필요한 부분만 이 폴더에 복사한 뒤 기준 렌더를 만든다. 첫 후보에서 모든 종과 배경 전체를 동시에 변경하지 않는다.
