# 모델 지시문: 카드 소품 3종 (`card-props`) — 단추 미사일 · 장난감 기차 · 깜짝 상자

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 작은 정적 모델 작업(decisions.md M-29 4단계 2묶음, [카드 계획](../onslaught-cards-plan.md) 16.6). 사용자 규칙: 이펙트 · 모델 · 아이콘 같은 리소스는 Codex가 만든다.
> **왜**: 난전 카드 세 장이 화면에 보이는 물건을 쓴다. ① **단추 미사일**: 총알 N번째 명중마다 단추 하나가 날아가 근처 다른 적을 맞힘. ② **장난감 기차**(팀 특별 카드): 누가 쓰러지면 그 자리에서 공격이 온 쪽으로 나무 장난감 기차가 칙칙폭폭 돌진, 지나간 적은 데굴. ③ **깜짝 상자**(팀 특별 카드): 누가 쓰러지면 그 자리에 깜짝 상자가 생겨 1.5초 적을 모은 뒤 뚜껑이 열리며 용수철 거위 광대가 튀어나와 넉백.
> **검토자**: Claude가 Blender를 돌려 GLB를 만들고, 게임에서 움직임(기차 바퀴 · 상자 뚜껑 · 용수철)을 코드로 붙여 실제 렌더로 확인한다. Codex는 Blender 스크립트(모델 코드)까지만 만든다.

## 1. 공통 규칙

- 분위기: 봉제 인형 · 장난감 톤의 **둥글고 두툼한 저폴리**(야외 키트 · 전환 소품과 같은 결). 텍스처 없음, 단색 재질.
- **좌표**: Blender에서 **Z 위**, 내보낼 때 `export_yup=True`. 게임 축 좌표를 Blender에 그대로 넣지 않는다(I-88). 단위 미터. **앞(진행 방향)은 Blender −Y**(게임에서 −Z = 앞, 키트와 같음).
- **피벗**: 최하단 z=0, 바닥 면적 가운데. 루트 이름 = 모델 id, 루트 커스텀 속성 `card_prop=True`.
- 재질 **sRGB → 선형 변환**(`transition_props.py`의 `mat()`처럼). 모델당 재질 **5개 이하**. 색은 아래 팔레트에서.
  - 나무 `c99a62` · 진한 나무 `8a6240` · 빨강 `e5484d` · 파랑 `3f6fd8` · 노랑 `ffd23f` · 초록 `4caf6a` · 흰 `f3f4f1` · 검정 고무 `2b2f36` · 분홍 `ff9ccc` · 주황 `ff9b4a`
- **움직이는 부품은 따로 빈 노드(Empty)** 아래에 두고 이름을 아래 표대로. 그 노드의 원점이 **회전축/경첩/바닥**이어야 게임에서 회전 · 이동만으로 움직일 수 있다. 메시는 재질별로 합치되 움직이는 부품끼리는 섞지 않는다.
- 바닥면(`normal.z < -0.5`)은 지운다(바퀴처럼 아래가 보이는 회전체 제외).

## 2. 모델

| id | 크기 (x × y × z, m) | 삼각형 | 내용 · 노드 |
|---|---|---|---|
| `button_missile` | 0.20 × 0.20 × 0.06 | < 160 | 옷 단추 하나: 둥근 납작한 원판(가장자리 둥글게), 가운데 구멍 4개(얕은 홈으로 표현 가능), 빨강 + 흰 테두리 선. 노드 하나 `Body`(원점 가운데 — 날아갈 때 굴리기 위해 **z 가운데 0.03**에 두고 루트 피벗은 바닥) |
| `toy_train` | 0.9 × 2.4 × 1.0 | < 2600 | 나무 장난감 기차: 앞 기관차(보일러 원통 · 굴뚝 · 운전실, 빨강 · 노랑 · 진한 나무) + 뒤 화물칸 하나(파랑 상자). 바퀴 8개(검정 고무 + 노랑 가운데)는 **각각** 빈 노드 `Wheel_0`~`Wheel_7`(원점 = 바퀴 축, 축 방향 X)에. 굴뚝 꼭대기에 빈 노드 `Smoke`(연기 나오는 자리, 메시 없음) |
| `jack_box` | 0.75 × 0.75 × 0.8 (닫힌 상태) | < 2400 | 깜짝 상자: 정육면체 상자(빨강 · 노랑 · 파랑 면, 모서리 둥글게), 옆면에 크랭크 손잡이. 뚜껑은 빈 노드 `Lid`(원점 = **뒤쪽 위 모서리 경첩**, 축 X — 게임에서 X축으로 −110° 돌리면 열림). 상자 안에 접힌 `Jack` 빈 노드(원점 = 상자 안 바닥 가운데): 용수철(나선 원통 고리 6~8개, 흰) + 그 위 **거위 광대 머리**(흰 거위 머리 · 주황 부리 · 빨간 광대 코 · 노랑 고깔모자). 닫힌 상태에서 `Jack`은 상자 안에 들어가 보이지 않게(게임이 z로 0.9 m 올리고 늘림). 크랭크는 빈 노드 `Crank`(원점 = 축, 축 X) |

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/card-props/tools/card_props.py` 하나 — Blender `bpy` 스크립트. 위 세 모델을 **`game/assets/card-props/<id>.glb`**로 내보내고, 원본 `prototypes/card-props/source/card-props.blend`를 저장.
   - 다른 도구(`prototypes/outdoor-kit/tools/*`, `prototypes/character-lab/tools/*`, `prototypes/ui-icons/tools/*`)는 **import하지 말고 고치지도 않는다**. 필요한 도우미는 복사(`transition_props.py`가 가까운 예시).
2. 스크립트 끝 **assert**: 모델마다 삼각형 예산, 재질 수 ≤ 5, 최저 z = 0(±0.002), 치수가 표의 ±25%, 표에 적은 빈 노드가 이름 그대로 모두 있음(`Wheel_0`~`Wheel_7` 8개 · `Smoke` · `Lid` · `Jack` · `Crank` · `Body`).
3. 마지막에 모델마다 `CARD_PROP_READY <id> <triangles>`, 끝에 `CARD_PROPS_BUILT 3`.
4. 실행은 하지 않는다. **Blender는 Claude가 실행**한다: `& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/card-props/tools/card_props.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋, `doc/`, 다른 도구 파일을 고치지 않는다. 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `ground_bits.py`처럼 `python -m py_compile prototypes/card-props/tools/card_props.py` 통과.
- 마지막 메시지에: 만든 파일, 모델별 예상 크기 · 부품 · 노드, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/card-props/tools/card_props.py` 하나. `py_compile`만 실행. `git status`로 다른 파일 변경 없음 확인.
- **Claude가 실행**: Blender 단언 통과 — `button_missile` 142 · `toy_train` 1394 · `jack_box` 1168 삼각형, `game/assets/card-props/` GLB 3개 + 원본 `prototypes/card-props/source/card-props.blend`, Godot 가져오기.
- **게임 연결**: `CardProps.cs` — 단추는 굴리며 날리고, 기차는 `Wheel_0~7`을 돌리고 `Smoke`에서 연기, 상자는 `Crank`를 돌리다 `Lid`를 X축 −110° 열고 `Jack`을 0.9 m 올려 출렁임. 렌더 `output/cardlab-toy-train.png`(빨강 · 노랑 기관차와 파란 화물칸, 연기) · `cardlab-jack-box.png`(열린 상자와 거위 광대, "깜짝이야!").
- 같은 날 아이콘 3장(`card_button_missile` · `card_toy_train` · `card_jack_box`)도 Codex가 [카드 아이콘 지시문](card-icons.md) 7장으로 `card_icons.py`에 더함(기존 16장은 그대로 같은 결과).
