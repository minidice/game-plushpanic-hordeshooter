# 모델 지시문: 큰 스테이지 키트 (`large-stage-kit`)

> **상태**: 2026-10-27 작성. 사용자 요청: "지금 테스트 지역(Stage 2 크기 시안) 규모를 Stage 3으로 치고, Stage 1 · 2도 그 규모로 바꾸고, 필요한 리소스는 Codex로 생성". 결정 L-30 · L-31 · L-32([레벨 디자인 규칙](../level-design-rules.md)).
> **왜**: 새 Stage 1(도시 · 창고 테마, 주 경로 약 600 m)은 지금 키트로 지을 수 없다 — 야외 키트는 숲 · 야적장용이고 기존 Stage 1 벽은 실내 통로용 절차 생성이다. 또 **버튼 · 사다리 노출 규칙(L-32)** 때문에 세이프하우스 닫기 버튼을 화면 위쪽 벽(월드 북쪽 벽)으로 옮긴 세이프하우스와, 어느 벽에나 붙일 수 있는 벽 버튼 상자가 필요하다.
> **역할**: Codex는 새 Blender 스크립트 하나와 그 결과물(GLB · manifest · 원본 blend · 확인 렌더)을 만든다. Claude가 게임(`LayoutMap`)에 연결하고 실제 렌더 · 검사로 확인한다. **기존 키트 도구 · manifest · GLB는 수정하지 않는다**(I-84 · I-86 · I-88).

## 1. 공통 규칙 (야외 키트 계약 `plushpanic_outdoor_v1`, [`stage2-landmarks.md`](stage2-landmarks.md) 1장과 같음)

- 분위기: 덕코프식(A-25) — **둥글고 두툼한 장난감 같은** 조형, 낡은 도시 · 산업 시설. 사실적 디테일 · 상표 · 글자 · 텍스처 없음. 모서리는 베벨.
- **좌표**: Blender **Z 위**, `export_yup=True`로 내보냄(I-88 재발 금지). 단위 미터.
- **카메라 쪽**: 게임 카메라는 월드 +Z(약간 −X) 위에서 −Z 쪽을 약 41~55°로 내려다본다. **Blender −Y가 카메라 쪽**(게임 +Z), Blender +Y가 먼 쪽(게임 −Z), Blender X = 게임 X.
- **피벗**: 최하단 z=0, 수평 중심(예외는 모델 절에 적음). 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`.
- **부위 구조**: 루트 아래 `Keep`과 `Occlude_<이름>` 빈 노드만, 그 아래 `<부위명>_Mesh` 하나씩(부위 안의 조각은 합침). 게임은 `Occlude_*`를 생존자를 가릴 때 옅게 한다.
- **재질**: 팔레트(`Concrete` `a4aaa1`, `Metal` `56636a`, `MetalLight` `a8b7b1`, `Rust` `98674f`, `Wood` `8a6a46`, `WoodDark` `5e4632`, `Ochre` `c7a35c`, `Rubber` `35444a`, `Red` `b96551`, `Hazard` `e2b84a`, `Ink` `2a2a2a`, `Green` `8fd19e`, 이 키트 추가: `Brick` `a8634c`, `BrickDark` `7c4a3b`, `Plaster` `d8cdb4`, `Glass` `5f7f8c`), sRGB → 선형 변환. 모델당 재질 **6개 이하**, 텍스처 없음.
- **충돌은 게임이 만든다**: manifest `collision`에 추천만 적는다(box/none). 모델에 물리 없음.
- **버튼 · 사다리 노출 규칙(L-32)**: 사람이 누르는 버튼 · 밸브 · 손잡이는 **카메라 쪽(Blender −Y)을 향하는 면**에 둔다. 방 · 건물 **안쪽** 버튼은 **먼 쪽 벽(Blender +Y 벽)의 안쪽 면** 또는 **동쪽 벽(Blender +X 벽)의 안쪽 면**에만 둔다. 카메라 쪽 벽(Blender −Y)과 서쪽 벽(Blender −X)의 안쪽에는 두지 않는다.

## 2. 모델

### 2.1 `safehouse_b` — 버튼을 먼 쪽 벽으로 옮긴 세이프하우스
- 기존 `safehouse`([`stage2_safehouse.py`](../../prototypes/outdoor-kit/tools/stage2_safehouse.py)의 `safehouse`)와 **같은 크기 · 벽 · 입구 · 지붕 · 띠**: 외부 14 × 12 m, 벽 두께 0.4 m, 높이 2.6 m, 서쪽(Blender −X) 벽 가운데 입구 폭 2.6 m · 높이 2.4 m, 콘크리트 · 초록 띠 · 후면 반지붕. 기존 스크립트를 **import 하지 말고** 필요한 형상 함수를 새 스크립트로 복사한다.
- **다른 점 하나**: 닫기 버튼(제어판 · 빨간 버튼 · 줄무늬 테)을 서쪽 벽 안쪽에서 **먼 쪽 벽(Blender +Y, 게임 북쪽) 안쪽 면**으로 옮긴다. 버튼 중심 = Blender `(-4.6, 5.55, 1.4)`(게임 로컬 `(-4.6, 1.4, -5.55)`), 버튼 면이 Blender −Y(카메라)를 향함. 제어판 0.46 × 0.14 × 0.28 m를 벽에 붙이고, 그 위 0.35 m에 작은 `Hazard` 경고등 하나. 서쪽 벽 안쪽에는 버튼이 없어야 한다.
- 부위: `Keep`(바닥 테두리 · 낮은 벽 · 버튼), `Occlude_Walls`(위쪽 벽 · 지붕). 삼각형 **6,000 미만**. 문(`safehouse_door`)은 기존 것을 그대로 쓴다.

### 2.2 `wall_button_panel` — 벽 버튼 상자(임무 장치 · 문 스위치 공용)
- 벽에 붙는 철제 제어함: 폭 0.9 · 높이 1.3 · 깊이 0.25 m(`Metal`), 바닥에서 0.5 m 띄워 붙음(모델 최하단 z=0.5가 아니라 **피벗 = 벽면 바닥 중심**, 상자 아래 끝 z=0.5, 위 끝 z=1.8 — 예외). 앞면(Blender −Y)에 **큰 빨간 버섯 버튼**(지름 0.32 m, 튀어나옴 0.12 m, `Red`) — **버튼 중심 z=1.2 m, y=−0.37 m**, 둘레 `Hazard` 줄무늬 테, 위에 작은 회전 경고등(`Hazard`, 지름 0.16 m). 상자에서 바닥으로 내려가는 굵은 전선관 하나(`Rubber`).
- 피벗 = 벽면(Blender y=0)이 등판, 앞은 −Y로 나옴. 게임이 이 모델을 벽의 카메라 쪽 면에 붙인다.
- 부위: `Keep`. 삼각형 **1,200 미만**. collision `none`(게임이 벽을 가짐). manifest `note`에 버튼 중심 좌표(게임 로컬)를 적는다.

### 2.3 `shutter_leaf` — 말아 올리는 철 셔터 판(막힌 길)
- 폭 6.0 · 높이 3.6 · 두께 0.2 m의 골이 진 철판(가로 골 0.3 m 간격, `MetalLight`), 아래 끝 `Hazard`/`Ink` 경고 줄무늬 띠(높이 0.25 m), 아래 손잡이 2개. 게임은 문처럼 이 판을 **위로 올려** 열린 것을 보여준다.
- 피벗 = 판 아래 끝 중심(z=0), 판은 Blender X 방향으로 넓다. 부위: `Occlude_Leaf`(전부). 삼각형 **1,500 미만**. collision `none`(게임 문이 가짐).

### 2.4 `urban_rowhouse` — 2층 벽돌 연립 건물(못 들어가는 블록)
- 12 (X) × 10 (Y) × 7 m. 1층 `Brick`, 2층 `Plaster`, 납작한 지붕과 낮은 난간(`Concrete`), 판자로 막은 창(`WoodDark` 판 2~3장 X자)이 카메라 쪽(−Y)에 층마다 3개, 동쪽 · 서쪽 면에 2개. 카메라 쪽 1층 가운데 닫힌 문과 작은 차양. 지붕 위 환기통 · 물탱크 하나.
- 부위: `Keep`(1층 높이 2.4 m까지), `Occlude_Upper`(2층 · 지붕). 삼각형 **4,000 미만**. collision `box` 12 × 7 × 10.

### 2.5 `urban_shop` — 1층 상점(못 들어가는 블록)
- 10 (X) × 8 (Y) × 4.2 m. `Plaster` 벽, 카메라 쪽 전면에 **내려간 셔터 두 칸**(`MetalLight`)과 줄무늬 천막 차양(`Red`/`Plaster` 줄, 앞으로 1.2 m), 간판 자리의 빈 판(글자 없음, `Ochre`). 지붕에 실외기 2개.
- 부위: `Keep`(벽 2.4 m까지 · 셔터), `Occlude_Upper`(위 벽 · 차양 · 지붕). 삼각형 **3,000 미만**. collision `box` 10 × 4.2 × 8.

### 2.6 `urban_warehouse` — 창고 블록(못 들어가는 블록)
- 24 (X) × 16 (Y) × 7.5 m. 골함석 벽(`Metal` 세로 골), 아래 1 m `Concrete` 기초, 톱날 지붕 3칸(`MetalLight`, 북쪽 유리 `Glass`), 카메라 쪽에 닫힌 큰 미닫이문(폭 6 m, `Rust` 얼룩). 모서리 배수관.
- 부위: `Keep`(벽 2.4 m까지 · 기초 · 문 아래), `Occlude_Upper`(위 벽 · 지붕). 삼각형 **5,000 미만**. collision `box` 24 × 7.5 × 16.

### 2.7 `car_wreck` — 버려진 승용차(엄폐물 · 경보 차)
- 4.4 (X) × 1.9 (Y) × 1.5 m, 둥근 장난감 같은 세단. 몸통 `Ochre`, 창 `Glass`, 바퀴 4개 `Rubber`(한쪽 바람 빠져 살짝 기움), 범퍼 `MetalLight`, 지붕 위 경보등 자리 없음. 문 손잡이 · 사이드미러 정도만.
- **2026-10-27 후속(경보 차)**: Stage 1의 경보 차로 쓰인다. 게임(`AlarmCar.BindModelLights`)은 이름이 `Red`로 시작하는 재질을 경보 때 빨갛게, `Hazard`로 시작하는 재질을 주황으로 깜빡인다. 그래서 **뒤 끝 미등 2개(`Red`, 0.28 × 0.12 m)와 네 모서리 깜빡이 4개(`Hazard`, 0.14 × 0.1 m)**를 몸통 모서리에 붙이고, 앞 유리 안쪽 계기판 위 작은 경보등 하나(`Red`, 0.12 m)를 둔다. 재질은 6개 이하 그대로(`Ochre` · `Glass` · `Rubber` · `MetalLight` · `Red` · `Hazard`).
- 부위: `Keep`. 삼각형 **2,000 미만**. collision `box` 4.4 × 1.5 × 1.9.

### 2.8 `bus_wreck` — 버려진 버스(큰 엄폐물)
- 11 (X) × 2.6 (Y) × 3.0 m. 몸통 `Plaster` + `Green` 띠, 창 줄(`Glass`, 일부 깨져 `Ink` 구멍), 바퀴 6개, 앞쪽 범퍼 찌그러짐.
- 부위: `Keep`(아래 1.4 m), `Occlude_Upper`(창 위 · 지붕). 삼각형 **3,000 미만**. collision `box` 11 × 3 × 2.6.

### 2.9 `dumpster` — 쓰레기 컨테이너(작은 엄폐물)
- 2.0 (X) × 1.3 (Y) × 1.3 m, `Green` 철함, 뚜껑 반쯤 열림(`Rubber`), 바퀴 4개.
- 부위: `Keep`. 삼각형 **900 미만**. collision `box` 2 × 1.3 × 1.3.

## 3. 결과물과 실행

- 새 스크립트 **하나**: `prototypes/outdoor-kit/tools/large_stage_kit.py`(표준 bpy만, 다른 키트 스크립트 import 금지). 실행:
  `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/large_stage_kit.py`
- 출력: `game/assets/large-stage-kit/<id>.glb` 9개, `game/assets/large-stage-kit/manifest.json`(기존 키트 manifest와 같은 형식: 최상위 `assets` 배열, 항목마다 `id` · `file` · `name` · `category` · `note` · `dimensions_m` · `bounds_min_y` · `triangles` · `collision` · `occlusion_parts`), 원본 `prototypes/outdoor-kit/source/large-stage-kit.blend`, 확인 렌더 `prototypes/outdoor-kit/output/large-stage-kit/<id>.png`(카메라 쪽 3/4 시점, 게임과 비슷한 위에서 내려다본 각도) 9장.
- 스크립트 끝에서 스스로 검사하고 실패하면 예외: 삼각형 예산, 재질 6개 이하, 루트 아래 자식 이름이 `Keep`/`Occlude_*`뿐, 최하단 z=0(예외 모델 제외), 치수 ±3%, 버튼 모델의 버튼 중심 좌표. 성공하면 `LARGE_STAGE_KIT_BUILT 9` 한 줄을 출력.
- **건드리지 않는 것**: 다른 tools 스크립트, `game/assets/` 아래 다른 폴더, `game/content/data/asset_paths.json`, 게임 코드 · 장면. (키트 등록과 게임 연결은 Claude가 한다.)
