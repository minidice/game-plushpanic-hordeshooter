# 모델 지시문: 야외 외곽 전용 세트 (`edge-kit`) — 갈 수 없는 곳을 빽빽하게 채우는 배경

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 정적 배경 모델 작업(decisions.md A-22 · L-14, 이번 결정 A-23). 한 번의 Codex 실행에서 여섯 모델을 **한 스크립트로** 만든다.
> **왜**: 사용자 지적 — Stage 2 바깥(갈 수 없는 곳)의 나무가 듬성듬성해 보기 흉하고, 바닥이 끝나는 곳이 보인다. 야외 키트(`plushpanic_outdoor_v1`) 모델은 플레이 구역용이라(충돌 · 뒷면 · 가림 옅게 하기 부위) 수백 개를 깔기에는 무겁다. **갈 수 없는 곳 전용**으로 충돌 · 뒷면 · 가림 부위가 없는 가벼운 덩어리 모델을 만든다.
> **검토자**: Claude가 Blender를 돌려 GLB를 만들고, 게임에 인스턴싱으로 깔아 실제 렌더 · 검사로 확인한다. Codex는 Blender 스크립트(모델 코드)까지만 만든다.

## 1. 공통 규칙

- 분위기: 야외 키트와 같은 **둥글고 두툼한 장난감 같은 저폴리** 조형(키트 `tools/build_models.py` · `nature_shapes.py`의 나무 · 바위 모양을 참고). 사실적 디테일 · 텍스처 없음.
- **좌표**: Blender에서 **Z 위**로 만든다(키트와 같음). 내보낼 때 `export_yup=True`로 게임 +Y 위. **게임 축 좌표를 Blender에 그대로 넣지 않는다**(I-88). 단위 미터.
- **카메라 쪽 = 앞**: 게임 카메라는 고정 각도로 월드 **+Z 쪽 위에서 −Z 쪽을** 내려다본다(Stage 2는 원근 약 41°, Stage 1은 직교 55°). Blender에서는 **−Y 쪽이 앞면**(내보내면 게임 +Z). 이 모델들은 갈 수 없는 곳에만 놓이고 **카메라가 뒤쪽을 볼 수 없다**:
  - **뒷면(Blender +Y를 향하는 면)과 바닥면(−Z를 향하는 면)은 만들지 않는다.** 앞 · 위 · 양옆만.
  - 덩어리 안쪽이 보이지 않도록 앞에서 본 실루엣이 **빈틈 없이 겹치게** 쌓는다(나무 사이로 하늘 · 바닥이 보이면 안 됨).
- **피벗**: 최하단 z=0, 바닥 면적의 가운데(x, y 중심). 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`, `edge_only=True`.
- **부위 구조**: 루트 아래 `Keep` 빈 노드 하나만(가림 부위 `Occlude_*` 없음 — 플레이어와 카메라 사이에 놓이지 않는다). 메시는 **재질별로 합쳐** `Keep` 아래 `<재질>_Mesh`(예: `PineDark_Mesh`). 재질 수만큼 메시.
- **재질**: 키트 팔레트 이름 · 색을 재사용(`build_models.py`의 `M`: `PineDark` `37594d`, `Pine` `4f7b57`, `LeafTip` `7e985b`, `Leaf` `668a59`, `LeafLight` `91a765`, `Rock` `667a7c`, `RockLight` `8c9c96`, `Moss` `637952`, `Soil` `685c47`, `Grass` `6c8053`), 줄기가 필요하면 `Bark` `6b5440`. **sRGB → 선형 변환**(키트 `mat()`처럼; I-88). 모델당 재질 **4개 이하**, 외부 텍스처 · 정점색 없음.
- 둥근 덩어리는 낮은 분할(구 8~12분할)로 — 수백 개를 깔 배경이다.

## 2. 모델 (모두 충돌 없음 `none`, 가림 부위 없음)

| id | 하는 일 | 크기 (폭 x × 깊이 y × 높이 z, m) | 삼각형 | 외형 |
|---|---|---|---|---|
| `edge_forest_pine` | 먼 쪽(화면 위) 숲의 기본 덩어리 | 8 × 4 × 7 | < 2,500 | 높이가 다른 침엽수 6~8그루를 앞뒤 2줄로 엇갈려 하나로. 줄기는 앞줄 아래만 짧게 보이고 뒤는 잎에 묻힘. 원뿔 2~3단 층(`PineDark` · `Pine` · 끝에 `LeafTip`), 발치에 낮은 덤불 띠(`Leaf`)로 밑동 사이를 막음 |
| `edge_forest_round` | 먼 쪽 숲에 섞는 활엽수 덩어리 | 6 × 4 × 5.5 | < 2,500 | 둥근 수관 3~4개를 겹친 활엽 덩어리(`Leaf` · `LeafLight` · `LeafTip`), 줄기 1~2개, 발치 덤불 |
| `edge_forest_low` | **카메라 쪽(화면 아래)** 숲 — 키가 낮아 생존자를 가리지 않음 | 7 × 3.5 × 3.6 | < 2,000 | 키 작은 침엽수 · 관목을 낮고 넓게, 윗면이 평평하게 이어지는 실루엣(위에서 내려다볼 때 지붕처럼 덮임) |
| `edge_backdrop` | 숲 뒤 맨 끝 줄 · 안개 속 실루엣 — 가장 쌈 | 12 × 3 × 9 | < 600 | 어두운 침엽수 원뿔 실루엣 10~14개가 붙은 띠(`PineDark` 하나 + 끝 `Pine`), 앞면은 들쭉날쭉, 높이 6~9 m |
| `edge_shrub_band` | 걸을 수 있는 가장자리 바로 바깥의 낮은 덤불 띠(밑동 · 바닥 끝 가리기) | 5 × 1.4 × 1.0 | < 900 | 둥근 덤불 7~9개가 이어진 낮은 띠(`Leaf` · `LeafLight`), 사이사이 풀 다발(`Grass`), 작은 돌 2~3개(`Rock`) |
| `edge_rock_wall` | 먼 쪽 바위 절벽 띠(바닥 끝을 막는 벽) | 6 × 2.5 × 2.6 | < 1,500 | 이끼 낀 바위 덩어리 4~6개가 겹친 낮은 벽(`Rock` · `RockLight`, 윗면 `Moss`), 앞면은 수직에 가까운 경사, 윗면 들쭉날쭉 |

- **양옆**: 띠를 이어 깔면 옆면이 서로 맞닿는다. 양 끝 단면은 **바깥 폭의 90~100%까지 잎 · 바위가 차서** 이어 붙였을 때 틈이 안 보이게(끝에서 2~5% 정도 넘쳐도 됨).
- 앞에서 본 실루엣: 위쪽 가장자리는 들쭉날쭉(자연스러운 숲 · 바위선), 아래쪽은 바닥에 붙음.

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/outdoor-kit/tools/edge_kit.py` — Blender `bpy` 스크립트. 위 여섯 모델을 만들어 **`game/assets/edge-kit/<id>.glb`**로 내보내고, 키트 manifest와 **같은 형식**의 `game/assets/edge-kit/manifest.json`(revision 1, contract `plushpanic_outdoor_v1`, `assets` 배열에 id · name · category(`외곽 배경`) · note · collision(`{"kind":"none"}`) · occlusion_parts(`[]`) · dimensions_m · bounds_min_y · triangles · file)을 쓴다. 원본은 `prototypes/outdoor-kit/source/edge-kit.blend`.
   - 키트의 `build_models.py` · `nature_shapes.py` · `transition_props.py`는 **import하지 말고 고치지도 않는다**. 필요한 도우미(`mat` · `empty` · `finish` · `ico` · `cyl` · `mesh` · `start` · `finish_asset` 등)는 **복사**해 쓰고 내보내기 경로만 바꾼다. `transition_props.py`가 가장 가까운 예시다.
   - **뒷면 · 바닥면 없애기**: 모델을 합친 뒤 법선이 Blender +Y 쪽(뒤, `normal.y > 0.35`)이거나 아래(`normal.z < -0.5`)인 면을 지운다(또는 처음부터 만들지 않는다). 앞에서 보이는 실루엣이 깨지지 않게 뒤쪽 면만.
   - 스크립트 끝 **assert**(실패 시 예외): 모델마다 삼각형 예산, 재질 수 ≤ 4, 루트 바로 아래가 `Keep` 하나뿐, 메시 이름이 `<재질>_Mesh`, 최저 z = 0(±0.002), 치수가 2장의 값 ±20%, **뒤를 향하는 면(`normal.y > 0.35`)의 면적이 전체의 3% 이하, 아래를 향하는 면(`normal.z < -0.5`) 없음**, 앞에서 본 실루엣 덮임(정면 직교 투영에서 바닥 위 높이 1.5 m 이하 구간의 폭 덮임이 90% 이상 — 대략 계산이면 됨).
   - 마지막에 `EDGE_KIT_READY <id> <triangles>`를 모델마다, 끝에 `EDGE_KIT_BUILT 6`.
2. 실행은 하지 않는다. **Blender는 Claude가 실행**한다: `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/outdoor-kit/tools/edge_kit.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋(특히 `game/assets/outdoor-kit/` · `stage-props/`)을 고치지 않는다. `doc/`도 고치지 않는다.
- `prototypes/outdoor-kit/tools/` · `prototypes/character-lab/tools/`의 **기존 파일을 한 줄도 고치지 않는다**(I-84 · I-86 · I-88). 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `edge_kit.py`가 문법 오류 없이 준비되어 있다(`python -m py_compile`).
- 마지막 메시지에: 만든 파일, 모델별 예상 크기 · 부품 · 재질, 뒷면 · 바닥면을 없앤 방법, 양옆 이음 처리, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/outdoor-kit/tools/edge_kit.py` 하나. `py_compile`만 실행. `git status`로 기존 도구 · `game/` · `doc/` 변경 없음을 확인(I-84 · I-86 · I-88 재발 없음).
- **Claude가 실행**: `blender --background --python prototypes/outdoor-kit/tools/edge_kit.py` → 단언 전부 통과, 고친 것 없음. `game/assets/edge-kit/` `edge_forest_pine` 998 · `edge_forest_round` 756 · `edge_forest_low` 828 · `edge_backdrop` 274 · `edge_shrub_band` 810 · `edge_rock_wall` 402 삼각형 + `manifest.json`(충돌 없음 · 가림 부위 없음), 원본 `prototypes/outdoor-kit/source/edge-kit.blend`. 예산보다 훨씬 가볍다.
- **게임 연결**: `PropLibrary`가 세 번째 폴더로 읽음. `StageMap.DressEdge`(줄 배치 · 인스턴싱 · 가장자리 6 m 안만 그림자) · `FarGround` · `CameraBounds`, Stage 2의 듬성한 배경 나무 · 바위를 외곽 세트로 바꿈.
- **확인**: `StageTransitionCheck` 창 모드 렌더 `output/transition-5-arrival.png` · `transition-7-forest/bridge/yard/safehouse.png`(이전 모습은 로컬 `output/before-edge/`). 처음에는 서쪽 끝에 큰 숲을 둬서 카메라(남서쪽) 앞 왼쪽 아래를 큰 덩어리가 덮음 → 서쪽 끝도 낮은 줄로. 카메라 쪽 줄이 띠처럼 보임 → 깊이 · 각도를 흔들고 덤불 · 낮은 숲을 섞음.
- **남은 것**: 가까이 보이는 외곽 덩어리의 각진 면, 프리셋 밀도 조절(I-92).
