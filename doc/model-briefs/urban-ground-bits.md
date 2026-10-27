# 모델 지시문: 도시 바닥 조각 (`urban-bits`) — Stage 1 도로 · 보도 위의 작은 장식

> **상태**: 2026-10-28 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 작은 정적 장식 모델 작업(decisions.md A-30). 한 번의 Codex 실행에서 일곱 모델을 **한 스크립트로** 만든다.
> **왜**: 사용자 — "스테이지 1은 바닥 퀄리티만 먼저 스테이지 3 정도 퀄리티까지 개선", "길 주변 정리 도로처럼 처리", "여긴 풀은 안 어울리겠지", "모델링은 모두 Codex로". 바닥 면(아스팔트 · 보도블록 · 콘크리트 · 차선)은 코드(셰이더)가 그리고, 그 위에 흩뿌릴 **작은 도시 조각**을 만든다. Stage 2 · 3의 풀 · 돌 조각(`ground-bits`, [지시문](ground-border-bits.md))의 도시판이다.
> **검토자**: Claude가 Blender를 돌려 GLB를 만들고, 게임에 인스턴싱으로 깔아 실제 렌더 · 검사로 확인한다. Codex는 Blender 스크립트(모델 코드)까지만 만든다.

## 1. 공통 규칙

- 분위기: 야외 키트 · 큰 스테이지 키트와 같은 **둥글고 두툼한 장난감 같은 저폴리**. 텍스처 없음. 카메라는 위에서 약 60° 내려다본다 — 윗면 모양이 읽혀야 한다.
- **좌표**: Blender에서 **Z 위**(키트와 같음), 내보낼 때 `export_yup=True`. 게임 축 좌표를 Blender에 그대로 넣지 않는다(I-88). 단위 미터.
- **피벗**: 최하단 z=0, 바닥 면적 가운데. 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`, `ground_bit=True`.
- **부위**: 루트 아래 `Keep` 빈 노드 하나만, 메시는 재질별로 합쳐 `Keep` 아래 `<재질>_Mesh`. 가림 부위 없음, 충돌 없음.
- **바닥면 없음**(`normal.z < -0.5` 면 지우기).
- **재질**(sRGB → 선형 변환, 키트 `mat()`처럼; I-88). 모델당 **3개 이하**:

  | 이름 | 색 | 거칠기 | 쓰는 곳 |
  |---|---|---|---|
  | `Iron` | `3e4144` | 0.6 | 맨홀 · 빗물받이 |
  | `IronLight` | `5d6266` | 0.6 | 맨홀 무늬 볼록한 곳 |
  | `Paper` | `e6e0cf` | 0.9 | 종이 |
  | `Can` | `b8452f` | 0.4 | 찌그러진 캔 |
  | `Cup` | `d9d3c2` | 0.8 | 종이컵 |
  | `Leaf` | `a8743a` | 0.9 | 낙엽 |
  | `LeafDark` | `7b5530` | 0.9 | 낙엽 어두운 것 |
  | `Brick` | `9e5640` | 0.9 | 깨진 벽돌 |
  | `Concrete` | `a4aaa1` | 0.95 | 콘크리트 조각 |
  | `Weed` | `6c8053` | 0.9 | 틈 잡초 |
  | `WeedTip` | `8a9a5b` | 0.9 | 잡초 끝 |
  | `Water` | `2f3a44` | 0.08 | 물웅덩이(어둡고 반짝임) |

- 수백 개를 깔 조각이라 아주 가볍게. 땅에 거의 붙은 것들이다.

## 2. 모델 (모두 충돌 없음)

| id | 쓰는 곳 | 크기 (x × y × z, m) | 삼각형 | 외형 |
|---|---|---|---|---|
| `bit_manhole` | 도로 가운데 쪽 | 0.9 × 0.9 × 0.03 | < 160 | 둥근 주철 맨홀 뚜껑(16 ~ 20각형). 바깥 테두리 고리가 살짝 높고, 윗면에 볼록한 격자 · 동심원 무늬 몇 줄(`IronLight`), 나머지 `Iron` |
| `bit_drain` | 보도 연석 바로 옆 도로 | 0.9 × 0.45 × 0.03 | < 160 | 직사각형 빗물받이 격자. 테두리 틀 + 긴 쪽으로 6 ~ 7개 살(`Iron`), 살 사이는 비어 아래 바닥이 보임 |
| `bit_litter` | 보도 · 골목 | 0.8 × 0.6 × 0.07 | < 160 | 구겨진 종이 2 ~ 3장(살짝 접힌 판, `Paper`), 찌그러진 캔 하나(`Can`, 눌린 원기둥), 쓰러진 종이컵 하나(`Cup`). 서로 조금씩 떨어짐 |
| `bit_leaves` | 보도 가장자리 · 구석 | 0.7 × 0.5 × 0.04 | < 140 | 마른 낙엽 8 ~ 12장이 얇게 흩어진 무더기(살짝 휜 잎 판, `Leaf` · `LeafDark`) |
| `bit_rubble` | 골목 · 공터 · 건물 옆 | 0.7 × 0.5 × 0.12 | < 160 | 깨진 벽돌 2 ~ 3개(`Brick`)와 모난 콘크리트 조각 3 ~ 4개(`Concrete`)가 듬성하게 |
| `bit_crack_weed` | 보도블록 · 콘크리트 틈 | 0.3 × 0.25 × 0.15 | < 90 | 틈에서 난 작은 잡초 한 포기(뾰족한 잎 5 ~ 7개, `Weed` · `WeedTip`). 풀밭 풀 다발보다 훨씬 작고 드문 것 |
| `bit_puddle` | 도로 · 골목의 낮은 곳 | 1.4 × 0.9 × 0.006 | < 60 | 납작하고 가장자리가 들쭉날쭉한 물웅덩이 판 하나(`Water`, 두께 0.004 ~ 0.006) |

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/outdoor-kit/tools/urban_ground_bits.py` — Blender `bpy` 스크립트. 위 일곱 모델을 **`game/assets/urban-bits/<id>.glb`**로 내보내고, `game/assets/ground-bits/manifest.json`과 **같은 형식**의 `game/assets/urban-bits/manifest.json`(revision 1, contract `plushpanic_outdoor_v1`, `assets` 배열에 id · name · category(`도시 바닥`) · note · collision(`{"kind":"none"}`) · occlusion_parts(`[]`) · dimensions_m · bounds_min_y · triangles · file)을 쓴다. 원본 `prototypes/outdoor-kit/source/urban-bits.blend`.
   - 키트 · 다른 도구(`build_models.py` · `ground_bits.py` · `edge_kit.py` · `large_stage_kit.py` 등)는 **import하지 말고 고치지도 않는다**. 필요한 도우미는 **복사**해 쓴다(`ground_bits.py`가 가장 가까운 예시). 내보내기 경로만 바꾼다.
   - 스크립트 끝 **assert**: 모델마다 삼각형 예산, 재질 수 ≤ 3, 루트 바로 아래가 `Keep` 하나뿐, 메시 이름 `<재질>_Mesh`, 최저 z = 0(±0.002), 치수가 2장 값 ±25%, 아래를 향하는 면(`normal.z < -0.5`) 없음.
   - 마지막에 `URBAN_BIT_READY <id> <triangles>`를 모델마다, 끝에 `URBAN_BITS_BUILT 7`.
2. 실행은 하지 않는다. **Blender는 Claude가 실행**한다: `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/outdoor-kit/tools/urban_ground_bits.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 셰이더 · 다른 에셋을 고치지 않는다. `doc/`도 고치지 않는다.
- `prototypes/outdoor-kit/tools/` · `prototypes/character-lab/tools/`의 **기존 파일을 한 줄도 고치지 않는다**(I-84 · I-86 · I-88). 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `urban_ground_bits.py`가 문법 오류 없이 준비되어 있다(`python -m py_compile`).
- 마지막 메시지에: 만든 파일, 모델별 예상 크기 · 부품 · 재질, 확인하지 못한 것.

## 6. 결과 (2026-10-28)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/outdoor-kit/tools/urban_ground_bits.py` 하나. `py_compile`만 실행. `git status`로 기존 도구 · `game/` · `doc/` 변경 없음을 확인.
- **Claude가 실행**: `blender --background --python prototypes/outdoor-kit/tools/urban_ground_bits.py` → 단언 전부 통과, 고친 것 없음. `game/assets/urban-bits/` `bit_manhole` 152 · `bit_drain` 100 · `bit_litter` 96 · `bit_leaves` 60 · `bit_rubble` 120 · `bit_crack_weed` 60 · `bit_puddle` 54 삼각형 + `manifest.json`(충돌 없음), 원본 `prototypes/outdoor-kit/source/urban-bits.blend`.
- **게임 연결**: `asset_paths.json` `prop_kits`에 `res://assets/urban-bits`. `LayoutMap.BuildUrbanBits`가 Stage 1 바닥에 인스턴싱(그림자 없음).
- **확인 · 조정**: 렌더에서 물웅덩이가 밝은 콘크리트 위에서 검은 구멍처럼 보여 아스팔트 도로에만 놓음. 모델은 그대로.
