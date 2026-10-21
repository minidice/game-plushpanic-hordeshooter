# 모델 지시문: 난전 3번 칸 "꽥 경적" (`honk`) · "고무오리" (`rubber_duck`) — 손에 드는 모델 + 아이콘

> **상태**: 2026-10-21 작성. Codex CLI(`codex exec`)에 맡기는 무기 모델 작업(decisions.md A-17 · M-27 계획 13.1, issues.md I-87). 한 번의 Codex 실행에서 두 모델을 **차례로** 만든다.
> **왜**: 두 무기는 모델 · 아이콘이 없어 손에는 색 상자, 무장 카드 · HUD 칸에는 기본 표시("3")로 나온다.
> **검토자**: Claude가 Blender를 돌려 GLB · 아이콘을 만들고, 게임 데이터에 연결해 실제 렌더 · 검사로 확인한다. Codex는 Blender 스크립트까지만 만든다.

## 1. 공통 규칙

- 분위기: 게임 전체처럼 **귀엽고 우스꽝스러운 장난감** 느낌(`doc/character-model-spec.md` 6장). 사실적인 총기가 아니다. 상표 · 글자 · 텍스처 없음.
- **기존 무기 모델 규칙을 그대로 따른다**(`prototypes/character-lab/tools/weapons-assets.md`): 단위 미터, glTF/Godot 기준 **+Z 앞 · +Y 위**, **원점 = 손잡이(손이 붙는 곳)**, `Muzzle` 빈 노드 하나가 앞쪽 끝(소리 · 효과가 나오는 곳)에, 손 메시는 넣지 않음(게임이 손을 붙임). 둥근 모서리는 실제 지오메트리(베벨)로.
- 스타일 · 도우미 함수는 `prototypes/character-lab/tools/weapons-build.py`(`material` · `finish` · `box` · `rod` · `tube` 등)를 참고(복사해도 됨). 메시는 재질별로 합쳐 **메시 · 재질 3~5개**, 삼각형 **2,500개 미만**.
- 길이는 게임 데이터 `game/content/data/weapon_types.csv`의 `length_m`에 맞춘다: 꽥 **0.40 m**, 고무오리 **0.35 m**(`Muzzle`의 앞쪽 거리).
- 카메라는 쿼터뷰로 위에서 내려다보고 손에 든 무기는 작게 보인다: **색 덩어리와 실루엣**이 먼저 읽혀야 한다(가는 장식보다 큰 덩어리).

## 2. 꽥 경적 (`honk`)

- **하는 일**: 쏘면 "꽥!" 소리와 함께 내 주변 4.5 m를 팍 밀쳐낸다(무기 규격 `shout`). 거위 주인공의 비명 "꽥"을 무기로 만든 것.
- **외형**: 광대 · 자전거용 **고무공 경적**. 뒤쪽(원점 근처, 손잡이)은 크고 동그란 **빨간 고무 주머니**(지름 약 0.13 m, 살짝 눌린 공), 앞쪽으로 **금색 관**이 짧게 나와 한 번 굽은 뒤 **크게 벌어진 나팔 입**(지름 약 0.16 m)으로 끝난다. 관과 주머니 사이에 금색 고리 하나. `Muzzle`는 나팔 입 한가운데.
- **색**: 고무 주머니 빨강 `#d8433b`, 관 · 나팔 금색 `#e2b34a`(약간 금속), 나팔 안쪽 어두운 금 `#8a6a24`, 고리 크림 `#f3e3c4`(선택).

## 3. 고무오리 (`rubber_duck`)

- **하는 일**: 조준점에 던지면 삑삑거리며 일반 몹을 모았다가 펑(무기 규격 `throw`). **같은 모델을 던진 오리로도 쓴다**(`throwables.csv`의 `rubber_duck` 모델): 바닥에 놓였을 때도 오리로 보여야 한다.
- **외형**: 목욕탕 **노란 고무오리**. 통통한 몸(길이 약 0.30 m, 폭 0.20 m, 높이 0.16 m), 둥근 머리(지름 약 0.15 m)가 몸 앞쪽 위에, 앞으로 튀어나온 **주황 부리**(납작한 타원), 검은 동그란 눈 두 개(흰 반짝이 점 선택), 엉덩이 쪽 살짝 들린 꼬리 끝, 날개 모양 볼록한 옆면. 바닥에 놓으면 똑바로 서도록 아랫면은 평평하게.
- **원점**: 몸 아랫면 가운데(손이 아래에서 받쳐 드는 곳). `Muzzle`는 부리 끝.
- **색**: 몸 노랑 `#ffd23f`, 부리 주황 `#ff8a3d`, 눈 잉크 `#2a1f1a`, 반짝이 흰색(선택).

## 4. 만들 것 (Codex 작업 범위)

1. `prototypes/character-lab/tools/gadget-models.py` — Blender `bpy` 스크립트. 위 두 모델을 만들어 **`game/assets/weapons/honk.glb`**와 **`game/assets/weapons/rubber_duck.glb`**로 내보낸다(내보낸 GLB에는 루트 아래 메시들과 `Muzzle` 하나). 내보낸 뒤 스크립트 안에서 assert로 확인: 삼각형 2,500 미만, 메시 · 재질 5개 이하, `Muzzle`이 있고 앞쪽(+Z, glTF 기준) 거리가 `length_m`의 ±15% 안, 손 메시 없음. 결과를 `prototypes/character-lab/output/weapons/gadget-report.json`에 쓰고 `GADGET_READY <id> <triangles>`를 출력.
   - `weapons-build.py`는 **고치지 않는다**(전체 무기 GLB를 다시 쓰므로). 도우미 함수는 복사해 쓴다.
2. `prototypes/character-lab/tools/render-weapon-icons.py` — **`--only id1,id2`** 인자(Blender의 `--` 뒤)를 추가해 그 무기만 렌더하게 한다(지금은 `weapons.csv`의 모델 있는 무기를 전부 다시 렌더). 인자가 없으면 지금과 똑같이 동작.
3. 실행은 하지 않는다. **Blender는 Claude가 실행**한다:
   - `blender --background --python prototypes/character-lab/tools/gadget-models.py`
   - (Claude가 `weapons.csv`의 `model`을 채운 뒤) `blender --background --python prototypes/character-lab/tools/render-weapon-icons.py -- --only honk,rubber_duck`

## 5. 하지 말 것

- `game/` 안의 C# · 데이터(`weapons.csv` · `throwables.csv` 등) · 다른 GLB · 아이콘을 고치지 않는다(Claude가 연결). `doc/`도 고치지 않는다.
- `weapons-build.py`와 기존 무기 모델 · 아이콘을 바꾸지 않는다.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 6. 끝났다고 볼 기준

- 두 파일(`gadget-models.py`, `render-weapon-icons.py`의 `--only`)이 문법 오류 없이 준비되어 있다(Python 문법 검사: `python -m py_compile`).
- 마지막 메시지에: 바꾼 파일, 모델별 예상 크기 · 부품 목록, 원점 · `Muzzle` 위치, 외형에서 고민한 점, 확인하지 못한 것.

## 7. 결과 (2026-10-21)

- **Codex**(`codex exec -s workspace-write`): `tools/gadget-models.py`(Blender 스크립트, 꽥 경적 · 고무오리 + 내보낸 GLB 검사), `render-weapon-icons.py`에 `--only`. `game/` · `doc/`은 건드리지 않음.
- **Claude가 고친 것**(I-88): ① 부품을 게임 축(+Y 위 · +Z 앞)으로 놓고 Blender(+Z 위)에 그대로 만들어 **GLB가 위를 향함**(검사가 `Muzzle` (0, 0.4, -0.06)으로 잡아냄) → 내보내기 전에 X축 90° 회전(`weapons-build.py`의 `V()`와 같은 변환). ② 색을 sRGB 값 그대로 Blender 선형 입력에 넣어 **빨강이 분홍, 금이 크림**처럼 바램 → sRGB → 선형 변환. ③ 오리 **부리가 머리 앞 6 cm에 떠 있음**, 눈이 머리 표면 근처 안쪽 → 머리를 앞으로, 부리를 머리에 붙이고 눈을 표면 위로. ④ 지시문 크기(0.40 · 0.35 m)로는 쿼터뷰에서 손에 든 모습이 점처럼 작아 **1.4배 · 1.3배로 키움**(경적 0.56 m · 오리 0.46 m, `weapon_types.csv` `length_m`도 맞춤).
- **최종**: `blender --background --python prototypes/character-lab/tools/gadget-models.py` → `game/assets/weapons/honk.glb` 삼각형 1,568 · `rubber_duck.glb` 2,444(재질 4개씩), `render-weapon-icons.py -- --only honk,rubber_duck` → `game/assets/icons/honk.png` · `rubber_duck.png`(256×160, 다른 무기 아이콘과 같은 조명 · 밝은 톤).
- **게임 연결**: `weapons.csv`의 `honk` · `rubber_duck` `model` · `icon`, `throwables.csv`의 `rubber_duck` `model` · `icon`(던진 오리도 같은 GLB).
- **확인**: `CardLabCheck` 창 모드 렌더 `output/cardlab-arming-3.png`(카드에 경적 아이콘), `cardlab-slot3-honk-close.png`(손의 금색 나팔 · HUD 경적 아이콘), `cardlab-slot3-duck-thrown.png`(바닥의 노란 오리 · "삑!"), `cardlab-slot3-duck-hand.png`. 이전 모습은 로컬 `output/before-gadget/`.
- **남은 것**: 쿼터뷰에서 던진 오리는 여전히 작은 노란 덩어리로 보임(형태보다 색으로 읽힘). 손에 드는 자세는 총과 같은 손잡이 자세.
