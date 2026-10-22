# 모델 지시문: 시나리오 Stage 1 → 2 전환 소품 (`floor_hatch` · `bunker_hatch` · `concrete_barrier` · `warning_sign`)

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 정적 소품 모델 작업(decisions.md L-14 · A-22). 한 번의 Codex 실행에서 네 모델을 **한 스크립트로** 만든다.
> **왜**: 사용자 참고 영상(`D:\Recored\커스텀\시점변경및지형오브텍트참고.mp4`)처럼 실내 구역의 출구(사다리 해치)에서 나가 → 화면이 원으로 닫히고 → 야외의 **모래주머니로 둘러싼 벙커 해치**에서 나타나며 주변 오브젝트(침엽수 · 철망 · 바위 · 컨테이너 · 드럼통 · 콘크리트 방벽 · 경고 표지)와 시점이 바뀌는 전환을 Stage 1 → 2에 넣는다. 나무 · 바위 · 컨테이너 · 철망 · 모래주머니 · 드럼통은 야외 키트(`game/assets/outdoor-kit`)에 있고, **해치 두 종 · 콘크리트 방벽 · 경고 표지만 없다.**
> **검토자**: Claude가 Blender를 돌려 GLB를 만들고, 게임에 배치해 실제 렌더 · 검사로 확인한다. Codex는 Blender 스크립트(모델 코드)까지만 만든다.

## 0. 참고 영상에서 본 것 (Claude가 프레임 추출로 확인)

| 시점 | 화면 |
|---|---|
| 0~6초 | 어두운 실내 벙커. 타일 바닥, 녹색 띠 벽, 바깥은 검은 허공. 카메라는 가파르게 내려다봄(약 55~60°). 캐릭터가 통로 끝 사다리 공간으로 감 |
| 7~8.5초 | 사다리 앞에서 "벙커 나가기 [F]"를 **길게 누름**(아래 진행 막대) |
| 9초 | 캐릭터를 중심으로 **원이 줄어들며** 화면이 닫힘(아이리스 아웃) |
| 10~13초 | 검은 화면 → 다음 구역 제목 카드 → 검은 화면 |
| 14초 | 야외에서 캐릭터를 중심으로 **원이 커지며** 열림. 캐릭터는 **모래주머니로 둘러싼 원형 벙커 해치**(두꺼운 원형 뚜껑이 열려 서 있고 빨간 밸브 핸들) 옆에 서 있음 |
| 14.5~21초 | 야외 카메라: 더 낮은 각도(약 40~45°), 원근감, 멀리 안개와 지평선, 따뜻한 빛과 비. 카메라 쪽 아래 가장자리에 침엽수, 해치 둘레 철망 울타리, 뒤쪽에 이끼 낀 바위 절벽, 기울어진 노란 컨테이너, 드럼통, 경고 삼각 표지, 낮은 콘크리트 방벽, 흙길과 풀 |

## 1. 공통 규칙 (야외 키트 계약 `plushpanic_outdoor_v1`을 그대로 따름)

- 분위기: 게임 전체처럼 **둥글고 두툼한 장난감 같은** 조형(`doc/character-model-spec.md` 6장). 사실적 디테일 · 상표 · 글자 · 텍스처 없음(경고 표지의 "!"도 지오메트리).
- **좌표**: Blender에서는 **Z 위**로 만든다(키트의 `tools/build_models.py`와 같음). 내보낼 때 `export_yup=True`로 게임 +Y 위가 된다. **게임 축으로 생각한 좌표를 Blender에 그대로 넣지 않는다**(지난 I-88: GLB가 위를 향함). 단위 미터.
- **카메라 쪽**: 게임 카메라는 월드 **+Z(약간 −X) 쪽 위에서 −Z 쪽을 내려다본다**(Stage 1 수직 55°, Stage 2 약 42°). Blender에서는 **−Y 쪽이 카메라 쪽 앞면**(내보내면 +Z)이다. **카메라에 보이는 면 = 윗면 + 카메라 쪽 앞면**에 각 모델의 표식(밸브 핸들 · 경고 줄무늬 · "!")을 둔다. 키 큰 부분(열린 뚜껑)은 **카메라 반대쪽(Blender +Y, 게임 −Z)**에 세워 캐릭터를 가리지 않게 한다.
- **피벗**: 최하단 y=0, 수평 중심. 루트 이름 = 모델 id, 루트 커스텀 속성 `asset_contract='plushpanic_outdoor_v1'`.
- **부위 구조**: 루트 아래 `Keep`(항상 보임) 빈 노드와, 키 큰 가림 후보는 `Occlude_<이름>` 빈 노드. 메시는 부위 노드 아래 `<부위명>_Mesh` 하나로 합침(키트의 `finish_asset`과 같은 방식: 그룹별 join → 삼각화 → 바닥 맞춤 → 이름 정리 → GLB). 숫자 접미사로 부위를 구분하지 않는다.
- **재질**: 키트 팔레트 이름 · 색을 재사용(`build_models.py`의 `M` 표: `Concrete` `a4aaa1`, `Metal` `56636a`, `MetalLight` `a8b7b1`, `Rust` `98674f`, `Sandbag` `b3a17a`, `Ochre` `c7a35c`, `Rubber` `35444a`, `Red` `b96551` 등). 새 색이 필요하면 `HatchRed` `c8413a`(밸브), `Hazard` `e2b84a`(경고 노랑), `Ink` `2a2a2a`(검은 줄 · 구멍 안). **sRGB → 선형 변환**(키트 `mat()`처럼; I-88의 색 바램 재발 방지). 모델당 재질 **6개 이하**, 외부 텍스처 없음.
- 삼각형: 모델당 **6,000개 미만**(해치 두 종), **2,500개 미만**(방벽 · 표지).
- 둥근 모서리는 실제 지오메트리(베벨). 얇은 막대보다 **큰 덩어리와 실루엣**이 먼저 읽히게.

## 2. 모델

### 2.1 `floor_hatch` — Stage 1 세이프하우스의 출구(바닥 해치, 사다리로 내려감)

- **하는 일**: Stage 1을 깨면(세이프하우스 문을 닫으면) 세이프하우스 바닥의 이 해치에서 "다음 구역으로 [E 길게]"를 해서 Stage 2로 넘어간다. 실내 타일 바닥 위에 놓인다.
- **외형**: 바닥에 붙은 **정사각 강철 틀**(바깥 1.5 × 1.5 m, 높이 0.08 m, 폭 0.18 m의 테두리), 틀 위에 **노랑/검정 경고 줄무늬**(대각 띠, 테두리 윗면), 안쪽 1.1 × 1.1 m는 **검은 구멍**(얕은 상자 `Ink`, 윗면이 y≈0.01로 바닥 아래가 깊어 보이게), 구멍 안에서 **사다리 두 기둥 끝**이 0.35 m 위로 튀어나와 둥글게 휘어 있음(`MetalLight`). **뚜껑**: 1.15 × 1.15 × 0.08 m 강철판, 카메라 반대쪽 테두리(Blender +Y)에 경첩으로 붙어 **약 100° 열려 거의 수직으로 서 있음**, 뚜껑 안쪽 면(카메라 쪽을 봄)에 보강 리브 두 줄과 손잡이 하나.
- **부위**: `Keep`(틀 · 줄무늬 · 구멍 · 사다리 끝), `Occlude_Lid`(뚜껑).
- **크기**: 약 1.5 × 1.3(높이, 뚜껑 포함) × 1.6 m.
- **충돌 추천**: `none`(바닥과 같은 높이. 레벨이 소유).

### 2.2 `bunker_hatch` — Stage 2 도착 지점(야외 원형 벙커 해치)

- **하는 일**: Stage 2가 시작될 때 생존자들이 이 해치 옆에 나타난다(영상 14초). 첫 화면의 랜드마크.
- **외형**: 원형 **콘크리트 목**(바깥 지름 2.1 m, 높이 0.45 m, 윗면 가장자리 둥글게), 안쪽 지름 1.2 m의 **검은 구멍**, 목 윗면에 강철 **테 링**(지름 1.3 m, `Metal`), 구멍에서 **사다리 두 기둥 끝**이 0.4 m 위로. **뚜껑**: 지름 1.25 m · 두께 0.16 m의 둥근 강철 뚜껑, 카메라 반대쪽(Blender +Y)에 굵은 경첩 블록으로 붙어 **약 105° 열려 서 있음**, 뚜껑의 카메라 쪽 면 가운데에 **빨간 밸브 핸들**(지름 0.55 m 링 + 십자 살 4개 + 가운데 허브, `HatchRed`, 뚜껑 면에서 0.08 m 떨어짐). **모래주머니 반원**: 목의 카메라 반대쪽 절반(약 200°)을 감싸는 2단 모래주머니(주머니 하나 약 0.55 × 0.3 × 0.22 m의 둥근 베개 모양, 단마다 반 칸 엇갈림, `Sandbag`), 카메라 쪽은 비워 둠. 카메라 쪽에 짧은 **철제 디딤 계단** 3단(목 높이까지, `MetalLight`)은 선택.
- **부위**: `Keep`(목 · 테 · 구멍 · 사다리 · 모래주머니 · 계단), `Occlude_Lid`(뚜껑 + 밸브 + 경첩).
- **크기**: 약 3.0 × 1.9(높이) × 2.8 m.
- **충돌 추천**: `cylinder` 반지름 1.05, 높이 0.45(목). 모래주머니는 시각용.

### 2.3 `concrete_barrier` — 낮은 콘크리트 방벽(저지 장벽)

- **외형**: 길이 2.0 m의 저지(Jersey) 장벽 단면(아래 폭 0.6 m → 위 폭 0.22 m, 높이 0.8 m, 아래 0.25 m는 수직 후 경사), 양 끝 살짝 둥글게, 윗면 가까이 **노랑 반사 띠** 하나(카메라 쪽 경사면, `Hazard`), 양 끝 아래 들어 올리는 홈(작은 사각 홈) 하나씩. 약간 낡은 모서리 이 빠짐 2~3곳(선택).
- **부위**: 전부 `Keep`.
- **충돌 추천**: `box` 크기 [2.0, 0.8, 0.5], 중심 [0, 0.4, 0].

### 2.4 `warning_sign` — 경고 삼각 표지

- **외형**: 둥근 쇠기둥(높이 1.45 m, 지름 0.07 m, `Metal`)과 아래 콘크리트 받침(0.35 × 0.12 × 0.35 m), 위에 **둥근 모서리 삼각판**(한 변 0.75 m, 두께 0.04 m, 빨간 테 `HatchRed` 폭 0.08 m + 안쪽 흰/크림 바탕), 바탕 위에 **두꺼운 "!"**(세로 막대 + 점, `Ink`, 0.01 m 돌출). 판은 **카메라 쪽(Blender −Y)을 향해** 뒤로 15° 젖힘(위에서 보는 카메라에 잘 보이게).
- **부위**: `Keep`(기둥 · 받침), `Occlude_Sign`(삼각판 + "!").
- **충돌 추천**: `cylinder` 반지름 0.12, 높이 1.4.

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/outdoor-kit/tools/transition_props.py` — Blender `bpy` 스크립트. 위 네 모델을 만들어 **`game/assets/stage-props/<id>.glb`**로 내보내고, 키트 manifest와 **같은 형식**의 `game/assets/stage-props/manifest.json`(revision 1, contract `plushpanic_outdoor_v1`, `assets` 배열에 id · name · category · note · collision · occlusion_parts · dimensions_m · bounds_min_y · triangles · file)을 쓴다. 원본은 `prototypes/outdoor-kit/source/transition-props.blend`로 저장한다(라이브러리 보기용 격자 배치는 저장 직전에만, 내보낸 GLB 루트는 원점).
   - 키트의 `build_models.py` · `nature_shapes.py`는 **import하지 말고 고치지도 않는다**(import하면 키트 전체를 다시 만들어 `pack/`을 덮어씀). 도우미 함수(`mat` · `empty` · `finish` · `box` · `cyl` · `ico` · `beam` · `mesh` · `start` · `occ` · `finish_asset`)는 **복사**해서 쓴다. 내보내기 경로만 `game/assets/stage-props`로.
   - 스크립트 끝에서 **assert로 확인**하고 실패하면 예외: 모델마다 삼각형 예산, 재질 수 ≤ 6, 루트 바로 아래 노드 이름이 `Keep` 또는 `Occlude_*`뿐, 각 부위 노드 아래 메시 이름이 `<부위>_Mesh`, 최저 z = 0(±0.002), 치수가 2장의 값 ±25%, 뚜껑(`Occlude_Lid`)의 무게중심이 **Blender +Y 쪽**(카메라 반대쪽)에 있음, `bunker_hatch`의 밸브 핸들 중심이 뚜껑 중심보다 **−Y 쪽**(카메라 쪽 면)에 있음, `warning_sign` 판의 법선이 −Y 성분을 가짐.
   - 마지막에 `TRANSITION_PROP_READY <id> <triangles>`를 모델마다, 끝에 `TRANSITION_PROPS_BUILT 4`를 출력.
2. 실행은 하지 않는다. **Blender는 Claude가 실행**한다: `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/outdoor-kit/tools/transition_props.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋(특히 `game/assets/outdoor-kit/`)을 고치지 않는다. `doc/`도 고치지 않는다(Claude가 연결 · 기록).
- `prototypes/outdoor-kit/tools/` · `prototypes/character-lab/tools/`의 **기존 파일을 한 줄도 고치지 않는다**(지난 I-84 · I-86 · I-88: Codex가 공용 도구의 기존 줄을 바꿔 다른 모델 생성이 깨짐). 새 파일 하나만 만든다.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `transition_props.py`가 문법 오류 없이 준비되어 있다(`python -m py_compile`).
- 마지막 메시지에: 만든 파일, 모델별 예상 크기 · 부품 목록 · 부위(Keep/Occlude) 구분, 카메라 쪽 면에 둔 표식, 외형에서 고민한 점, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/outdoor-kit/tools/transition_props.py` 하나(529줄, 키트 도우미 복사 · 내보내기 후 단언). `py_compile`만 실행. 기존 도구 · `game/` · `doc/`은 건드리지 않음 — `diff -rq`로 키트 원본 `tools/`와 비교해 새 파일 하나만 다른 것을 확인(I-84 · I-86 · I-88 재발 없음).
- **Claude가 실행**: `blender --background --python prototypes/outdoor-kit/tools/transition_props.py` → 단언 전부 통과, `game/assets/stage-props/` `floor_hatch.glb` 1,759 · `bunker_hatch.glb` 5,380 · `concrete_barrier.glb` 552 · `warning_sign.glb` 968 삼각형 + `manifest.json`, 원본 `prototypes/outdoor-kit/source/transition-props.blend`. 고친 것 없음.
- **근접 렌더**(Blender, 게임 카메라 방향 55° · 25°): 뚜껑은 카메라 반대쪽에 서서 구멍 · 사다리가 보이고, 밸브 · 경고 줄무늬 · "!"가 카메라 쪽. 벙커 해치의 모래주머니는 1단으로 보임(지시문은 2단) — 쿼터뷰에서 읽히는 데 문제없어 그대로 둠.
- **게임 연결**: `PropLibrary`가 두 manifest(키트 32 + 전환 4)를 읽음. `floor_hatch`는 Stage 1 세이프하우스(124.2, 0, 1.4)의 다음 구역 해치, `bunker_hatch`는 Stage 2 도착점(6, 0, −1.6), 방벽 · 표지는 Stage 2 공터 · 검문소. 표지는 1.35배로 키워 배치.
- **확인**: `StageTransitionCheck` 창 모드 렌더 `output/transition-1-safehouse-hatch.png`(바닥 해치 옆 길게 누르기 안내), `transition-4-opening.png`(원 안에 벙커 해치와 생존자), `transition-5-arrival.png` · `transition-6-arrival-close.png`(밸브 · 모래주머니 · 경고 표지 · 방벽), `transition-7-yard.png`(검문소 방벽 · 표지).
- **남은 것**: 열린 뚜껑을 실제로 여닫는 동작 없음(정적 모델). `Occlude_Lid` · `Occlude_Sign` 옅게 하기는 키트와 함께 미연결(I-91).
