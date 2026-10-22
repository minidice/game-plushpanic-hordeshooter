# 모델 지시문: 길 경계 흩뿌림 조각 (`ground-bits`) — 흙길 · 아스팔트 가장자리의 선을 끊는 작은 장식

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 작은 정적 장식 모델 작업(decisions.md A-24). 한 번의 Codex 실행에서 네 모델을 **한 스크립트로** 만든다.
> **왜**: 사용자 지적 — 흙길 · 아스팔트와 풀밭 경계가 4 m 타일 모서리로 딱딱 잘려 보인다. 바닥은 코드(섞임 지도 + 셰이더)로 경계를 노이즈로 섞고(A-24 C안), **경계선을 따라 작은 풀 다발 · 자갈을 인스턴싱으로 흩뿌려** 선을 시각적으로 끊는다(D안). 그 작은 조각을 만든다.
> **검토자**: Claude가 Blender를 돌려 GLB를 만들고, 게임에 인스턴싱으로 깔아 실제 렌더 · 검사로 확인한다. Codex는 Blender 스크립트(모델 코드)까지만 만든다.

## 1. 공통 규칙

- 분위기: 야외 키트와 같은 **둥글고 두툼한 장난감 같은 저폴리**. 키트 `ground_grass` · `path_dirt` 타일 위의 자갈(`Pebble`, `ico` 1분할)과 `grass_patch`를 참고. 텍스처 없음.
- **좌표**: Blender에서 **Z 위**(키트와 같음), 내보낼 때 `export_yup=True`. 게임 축 좌표를 Blender에 그대로 넣지 않는다(I-88). 단위 미터.
- **피벗**: 최하단 z=0, 바닥 면적 가운데. 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`, `ground_bit=True`.
- **부위**: 루트 아래 `Keep` 빈 노드 하나만, 메시는 재질별로 합쳐 `Keep` 아래 `<재질>_Mesh`. 가림 부위 없음, 충돌 없음.
- **바닥면 없음**(`normal.z < -0.5` 면 지우기). 땅에 거의 붙은 작은 것들이라 뒷면은 남겨도 된다(어느 방향으로 돌려 깔아도 보이게).
- **재질**: 키트 팔레트 재사용(`build_models.py`의 `M`): `Grass` `6c8053`, `DryGrass` `a2a16b`, `LeafTip` `7e985b`, `Moss` `637952`, `Rock` `667a7c`, `RockLight` `8c9c96`, `Soil` `685c47`, `Dirt` `967653`, `Concrete` `a4aaa1`. **sRGB → 선형 변환**(키트 `mat()`처럼; I-88). 모델당 재질 **3개 이하**.
- 수백 개를 깔 조각이라 아주 가볍게.

## 2. 모델 (모두 충돌 없음)

| id | 쓰는 곳 | 크기 (x × y × z, m) | 삼각형 | 외형 |
|---|---|---|---|---|
| `bit_grass_tuft` | 흙길 · 공터 가장자리의 풀 다발 | 0.6 × 0.5 × 0.28 | < 160 | 가늘고 뾰족한 풀잎 7~10개가 바깥으로 벌어진 다발(납작한 원뿔 · 잎 판), `Grass` · `LeafTip`, 밑동 약간 `Moss` |
| `bit_grass_clump` | 풀 다발 큰 것(더 띄엄띄엄) | 0.9 × 0.8 × 0.4 | < 260 | 다발 2~3개가 붙은 덩어리, 끝 몇 개는 `DryGrass` |
| `bit_pebbles` | 흙길 · 공터 경계의 돌 몇 개 | 0.7 × 0.6 × 0.09 | < 160 | 납작한 둥근 돌 4~6개(크기 섞임, `Rock` · `RockLight`), 사이에 흙 얼룩 판 하나(`Soil`, 두께 0.01, 가장자리 들쭉날쭉) |
| `bit_gravel` | 아스팔트 갓길의 자갈 띠 조각 | 1.2 × 0.5 × 0.05 | < 220 | 길쭉한 불규칙 판(`Concrete`에 가까운 회색 자갈 흙, 두께 0.015) 위에 작은 자갈 8~12개(`Rock` · `RockLight`) |

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/outdoor-kit/tools/ground_bits.py` — Blender `bpy` 스크립트. 위 네 모델을 **`game/assets/ground-bits/<id>.glb`**로 내보내고, 키트 manifest와 **같은 형식**의 `game/assets/ground-bits/manifest.json`(revision 1, contract `plushpanic_outdoor_v1`, `assets` 배열에 id · name · category(`길 경계`) · note · collision(`{"kind":"none"}`) · occlusion_parts(`[]`) · dimensions_m · bounds_min_y · triangles · file)을 쓴다. 원본 `prototypes/outdoor-kit/source/ground-bits.blend`.
   - 키트 · 다른 도구(`build_models.py` · `nature_shapes.py` · `transition_props.py` · `edge_kit.py`)는 **import하지 말고 고치지도 않는다**. 필요한 도우미는 **복사**해 쓴다(`edge_kit.py` · `transition_props.py`가 가까운 예시). 내보내기 경로만 바꾼다.
   - 스크립트 끝 **assert**: 모델마다 삼각형 예산, 재질 수 ≤ 3, 루트 바로 아래가 `Keep` 하나뿐, 메시 이름 `<재질>_Mesh`, 최저 z = 0(±0.002), 치수가 2장 값 ±25%, 아래를 향하는 면(`normal.z < -0.5`) 없음.
   - 마지막에 `GROUND_BIT_READY <id> <triangles>`를 모델마다, 끝에 `GROUND_BITS_BUILT 4`.
2. 실행은 하지 않는다. **Blender는 Claude가 실행**한다: `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/outdoor-kit/tools/ground_bits.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋(`outdoor-kit` · `stage-props` · `edge-kit`)을 고치지 않는다. `doc/`도 고치지 않는다.
- `prototypes/outdoor-kit/tools/` · `prototypes/character-lab/tools/`의 **기존 파일을 한 줄도 고치지 않는다**(I-84 · I-86 · I-88). 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `ground_bits.py`가 문법 오류 없이 준비되어 있다(`python -m py_compile`).
- 마지막 메시지에: 만든 파일, 모델별 예상 크기 · 부품 · 재질, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/outdoor-kit/tools/ground_bits.py` 하나. `py_compile`만 실행. `git status`로 기존 도구 · `game/` · `doc/` 변경 없음을 확인.
- **Claude가 실행**: `blender --background --python prototypes/outdoor-kit/tools/ground_bits.py` → 단언 전부 통과, 고친 것 없음. `game/assets/ground-bits/` `bit_grass_tuft` 78 · `bit_grass_clump` 182 · `bit_pebbles` 90 · `bit_gravel` 120 삼각형 + `manifest.json`(충돌 없음), 원본 `prototypes/outdoor-kit/source/ground-bits.blend`.
- **게임 연결**: `PropLibrary`가 네 번째 폴더로 읽음. `StageMap.BorderBits`가 `GroundPainter.EdgePoints`를 따라 흙 경계에 풀 다발 · 큰 풀 다발 · 돌, 갓길에 자갈 · 풀 다발을 인스턴싱(그림자 없음). Stage 2 약 220개.
- **확인**: `StageTransitionCheck` 창 모드 렌더 `output/transition-5-arrival.png` · `transition-7-*.png`(이전은 로컬 `output/before-ground/`). 처음에는 자갈 조각이 갓길 바깥 풀밭에 놓임 → 갓길 바깥쪽 절반으로 옮김.
- **남은 것**: 넓게 보면 자갈 조각 모양 반복, 프리셋 밀도 조절(I-93).

## 7. 2차 수정 지시 (2026-10-22, 사용자 피드백)

> **왜**: 사용자 피드백 — 풀 다발은 괜찮다. 돌 조각(`bit_pebbles`)은 밑의 흙 얼룩 판(`SoilPatch`) 테두리가 또렷하고 색이 튀어서 어색하다, 돌이 너무 뭉쳐 있다, **판 없이 돌만** 있는 게 낫다. 바닥 색은 셰이더(그린 바닥)가 맡는다. 같은 이유로 자갈 조각의 판(`GravelPatch`)도 뺀다. 풀밭 · 흙 어디에나 흩뿌릴 **작은 돌 하나짜리**를 더한다.

이번에는 **이번 작업에서 새로 만든 `prototypes/outdoor-kit/tools/ground_bits.py`만 고친다**(아직 커밋 전, 이 지시문의 1차 결과물). 다른 도구 · 키트 · `game/` · `doc/`은 1~4장 규칙 그대로 고치지 않는다.

| id | 바뀌는 것 | 크기 (x × y × z, m) | 삼각형 | 외형 |
|---|---|---|---|---|
| `bit_grass_tuft` | 그대로 | 그대로 | 그대로 | 그대로 |
| `bit_grass_clump` | 그대로 | 그대로 | 그대로 | 그대로 |
| `bit_pebbles` | **`SoilPatch` 판 삭제**, 돌을 듬성하게 | 0.7 × 0.6 × 0.08 | < 120 | 납작한 둥근 돌 **3개**(크기 섞임 0.10~0.22 m), 서로 닿지 않게 **돌 크기 이상 띄워** 넓게 흩어 놓음. `Rock` · `RockLight`만 |
| `bit_gravel` | **`GravelPatch` 판 삭제** | 1.2 × 0.5 × 0.05 | < 200 | 작은 자갈 **8~10개**(0.05~0.12 m)를 길쭉한 영역에 듬성듬성(가운데가 조금 더 촘촘). `Rock` · `RockLight`만 |
| `bit_stone` (**새로**) | 풀밭 · 흙에 흩뿌릴 돌 하나 | 0.24 × 0.19 × 0.09 | < 60 | 납작하고 둥근 돌 하나(키트 `Pebble`처럼 ico 1분할 정도), `Rock`, 윗면 쪽 일부 `RockLight` 가능 |

- 나머지 규칙(피벗 · `Keep` · `<재질>_Mesh` · 바닥면 없음 · sRGB → 선형 · 재질 3개 이하 · 충돌 없음)은 1장 그대로. manifest는 **5종**, 끝 출력 `GROUND_BITS_BUILT 5`. 단언(치수 ±25%, 예산, 최저 z = 0 등)도 새 표 값으로.
- 실행은 하지 않는다(Claude가 Blender 실행). 커밋 · 푸시하지 않는다.

### 7장 결과 (2026-10-22)

- **Codex**: `ground_bits.py`만 고침(`git status`로 다른 파일 변경 없음 확인). `SoilPatch` · `GravelPatch` 삭제, `bit_pebbles` 돌 3개 듬성하게, `bit_gravel` 자갈 9개, `bit_stone` 추가.
- **Claude가 실행**: Blender 단언 통과 — `bit_grass_tuft` 78 · `bit_grass_clump` 182 · `bit_pebbles` 44 · `bit_gravel` 118 · `bit_stone` 15 삼각형, manifest 5종.
- **게임 연결**: 경계에만 깔던 `BorderBits`를 `GroundDressing`으로 바꿔 자연 바닥 전체에 풀 위주로 깔고, 인공 바닥(`GroundPainter.IsArtificial`, 지금은 아스팔트)에는 놓지 않음. 렌더에서 판 테두리가 없어지고 돌이 작게 흩어져 보이는 것을 확인.
