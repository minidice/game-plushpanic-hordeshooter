# 모델 지시문: Stage 1 길 안내 랜드마크 — 창고 간판 (`warehouse_sign`)

> **상태**: 2026-10-27 작성. 사용자 요청: "확정된 Stage 1 설계안의 맵 변경 8개를 반영해줘(랜드마크 모델은 Codex 지시문으로)". 결정 L-33(레벨은 가이드 맵을 먼저), [가이드 맵](../features/level/guide-map.md) 7.3장 변경 8, [레벨 디자인 참고 리서치](../features/level/reference-level-design-research.md) 규칙 후보 1 · 3.
> **왜**: Stage 1 설계안은 큰 구역마다 길 안내 수단을 둔다. B 상가 거리 끝에서 C 골목을 지나 D 창고 단지로 가는 동안 "저기가 다음 목적지"라고 알려 줄 **멀리서 눈에 띄는 큰 것**이 필요하다(L4D: 어두운 맵에서 몇 개의 핵심 조명 · 큰 형태로 방향을 이끈다). 지금 Stage 1의 랜드마크는 F 야적장의 크레인 하나뿐이다.
> **역할**: Codex는 새 Blender 스크립트 하나와 결과물(GLB · manifest · 원본 blend · 확인 렌더)을 만든다. Claude가 게임(`stage1_large_layout.json` 표식 · `LayoutMap`)에 연결하고 조명을 붙여 실제 렌더로 확인한다. **기존 키트 도구 · manifest · GLB는 수정하지 않는다**(I-84 · I-86 · I-88).

## 1. 공통 규칙

[큰 스테이지 키트 지시문](large-stage-kit.md) 1장과 같다(야외 키트 계약 `plushpanic_outdoor_v1`): 덕코프식 둥글고 두툼한 장난감 같은 조형, **글자 · 상표 · 텍스처 없음**, Blender Z 위 + `export_yup=True`, **Blender −Y가 카메라 쪽**, 피벗은 최하단 z=0 수평 중심, 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`, 부위는 `Keep` / `Occlude_<이름>` 아래 `<부위명>_Mesh`, 키트 팔레트 재질 **6개 이하**, 충돌은 manifest에 추천만.

카메라: 지금 기본 시점은 **모든 맵 수직 60° · 원근 34°**(L-2, 2026-10-27)로, 이전 지시문의 41~55°보다 더 내려다본다. 위에서 봤을 때 읽히는 **윗면 · 카메라 쪽 앞면**이 중요하다.

## 2. 모델 `warehouse_sign` — 지붕 위 창고 간판

- **무엇**: 창고 단지 입구를 알리는 큰 옥외 간판. 철골 다리 위에 넓은 판이 서 있고, 판 둘레에 전구가 줄지어 있다. 글자 대신 **창고 그림 표시**: 판 앞면(Blender −Y)에 큰 상자 더미 픽토그램(쌓인 상자 3개, 단순한 사각형 덩어리, `Ink` 윤곽 없이 색 면으로) + 오른쪽을 가리키는 굵은 화살표(`Hazard`). 화살표 방향은 Blender +X(게임 +X = 진행 방향, 화면 오른쪽 위).
- **크기**: 전체 높이 **7.0 m**, 판 폭 **6.0 m**(X) · 높이 2.6 m · 두께 0.3 m, 판 아래 끝 z = 4.2 m. 다리는 철골 기둥 2개(0.3 m 각, `Metal`) + X자 가새, 바닥판 1.6 × 1.2 m(`Concrete`) 두 개. 판은 카메라 쪽으로 **20° 뒤로 젖혀**(윗면이 하늘 쪽, 앞면이 카메라를 향하게) 60° 내려다보는 시점에서 앞면이 잘 보이게.
- **색 · 재질**: 판 바탕 `Ochre`, 픽토그램 상자 `Plaster`/`WoodDark`, 화살표 `Hazard`, 철골 `Metal`, 바닥판 `Concrete`, 전구 `Lamp` — 키트 팔레트에 없는 새 재질 1개 `Lamp`(`fff1c9`, 이름이 `Lamp`로 시작). 합계 6개 이하가 되게 `WoodDark`는 빼도 된다.
- **전구**: 판 앞면 테두리를 따라 지름 0.18 m 전구 18~24개(`Lamp`). 게임이 간판 앞에 따뜻한 조명(OmniLight)을 따로 둔다 — 모델에 빛 없음. manifest `note`에 "판 앞면 중심(게임 로컬 좌표)"을 적는다(조명 위치용).
- **피벗**: 바닥판 중심, z=0.
- **부위**: `Keep`(다리 아래 2.4 m까지 · 바닥판), `Occlude_Sign`(위 다리 · 판 · 전구). 가까이 가면 생존자를 가리지 않게 옅어진다.
- **삼각형 2,500 미만**. collision 추천 `box` 1.6 × 2.4 × 1.2(다리 아래만 — 판은 머리 위라 막지 않음).

## 3. 결과물과 실행

- 새 스크립트 **하나**: `prototypes/outdoor-kit/tools/stage1_guide_landmark.py`(표준 bpy만, 다른 키트 스크립트 import 금지). 실행:
  `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage1_guide_landmark.py`
- 출력: `game/assets/large-stage-kit/warehouse_sign.glb`, manifest는 **기존 `game/assets/large-stage-kit/manifest.json`을 고치지 말고** 새 파일 `game/assets/large-stage-kit/manifest_guide.json`(같은 형식: 최상위 `assets` 배열, `id` · `file` · `name` · `category` · `note` · `dimensions_m` · `bounds_min_y` · `triangles` · `collision` · `occlusion_parts`), 원본 `prototypes/outdoor-kit/source/stage1-guide-landmark.blend`, 확인 렌더 `prototypes/outdoor-kit/output/stage1-guide-landmark/warehouse_sign.png`(카메라 쪽에서 60° 내려다본 시점) 1장 + 정면 1장.
- 스크립트 끝에서 스스로 검사하고 실패하면 예외: 삼각형 예산, 재질 6개 이하, 루트 아래 자식 이름이 `Keep`/`Occlude_*`뿐, 최하단 z=0, 치수 ±3%(높이 7.0 · 판 폭 6.0), 판이 카메라 쪽(−Y)으로 20° 젖혀 있음. 성공하면 `GUIDE_LANDMARK_BUILT 1` 한 줄을 출력.
- **건드리지 않는 것**: 다른 tools 스크립트, `game/assets/` 아래 다른 파일(기존 `manifest.json` 포함), `game/content/data/`, 게임 코드 · 장면. (키트 등록 · 맵 배치 · 조명은 Claude가 한다.)

## 4. 게임 배치(Claude, 모델이 나온 뒤)

- 위치: C 골목 출구 북쪽, D 창고 단지 서쪽 끝(대략 게임 `(204, -86)`, 골목 북쪽 벽 바깥). 골목을 지나는 동안 화면 위쪽에 간판 판과 전구가 보이게. 정확한 자리는 렌더로 맞춘다.
- `stage1_large_layout.json`에 표식(`landmark`, 모델 `warehouse_sign`) 추가, 판 앞면 앞에 따뜻한 길 안내 조명(`lamps` 객체, `post: false`).
- 확인: `LargeStageCapture`(창 모드) `c-alley` · `b-terrace` 장면, `LargeStageCheck`(등장 지점 · 경로 막힘).
