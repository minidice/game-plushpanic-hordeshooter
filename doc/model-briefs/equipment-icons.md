# 그림 지시문: 장비 아이콘 9장 (모자 5 · 갑옷 4)

> **상태**: 2026-10-30 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 UI 아이콘 작업(decisions.md C-34 · C-36, [장비 구현 계획](../features/combat/equipment-implementation-plan.md) Task 3). 사용자 규칙: 모델 · 이펙트 · 아이콘 · 그림은 Codex가 만든다.
> **왜**: 모자 · 갑옷 두 칸이 생겼다. 바닥에 놓인 장비(떠 있는 아이콘)와 HUD 장비 칸에 쓸 아이콘이 필요하다. **옵션은 게임에서 비공개**(C-36)라서 아이콘은 효과를 설명하지 않는다. 그냥 "어떤 모자 / 어떤 조끼"인지만 알아보면 된다.
> **검토자**: Claude가 스크립트를 실행해 PNG를 만들고, HUD와 바닥 장비에 붙여 실제 화면으로 확인한다. Codex는 그림을 만드는 스크립트까지만 만든다.

## 1. 공통 규칙

- 분위기: 지금 무기 아이콘(`game/assets/icons/m4.png` 등)처럼 **장난감 같은 입체감**. 둥글고 두툼한 덩어리, 위에서 비스듬히 비춘 빛(왼쪽 위가 밝고 오른쪽 아래가 어두운 2~3단 명암), 얇은 어두운 외곽선(약 4 px, `1b2230` 70%)으로 어두운 HUD 위에서도 읽히게. 잔무늬 · 글자 없음.
- 크기 **256 × 256**, 배경 **투명**(RGBA PNG), 물체는 가운데에 가로 · 세로 약 70 ~ 85%를 차지. 가장자리 안티에일리어싱(크게 그려 줄이기 등).
- 화면에서 40 ~ 64 px로 작게 보인다. 9장이 실루엣과 색으로 서로 구별돼야 한다.
- Python 3 + **Pillow만** 쓴다(이미 설치됨). 다른 패키지를 설치하지 않는다. 결과는 결정적(무작위를 쓰면 시드 고정).

## 2. 만들 그림 (`game/assets/icons/equipment/<id>.png`)

| id | 이름 | 모양 | 주 색 |
|---|---|---|---|
| `keen_cap` | 날선 모자 | 앞챙이 뾰족하게 각진 야구 모자 | 주황 `f08a3c` |
| `scope_goggles` | 조준 고글 | 끈 달린 고글, 렌즈 두 개 중 하나에 작은 십자 조준선 | 청록 렌즈 `47c6d6`, 테 `5b6472` |
| `lucky_cap` | 행운 모자 | 둥근 니트 비니, 꼭대기 방울, 앞에 네잎클로버 패치 | 초록 `5cb85c` |
| `hunter_hat` | 사냥꾼 모자 | 귀덮개가 달린 체크무늬(큰 칸 2색) 사냥 모자 | 빨강 `c8473f` · 검정 칸 |
| `focus_band` | 집중 머리띠 | 이마에 두르는 띠, 뒤로 휘날리는 끈 두 가닥 | 흰 띠 `f2efe6`, 가운데 빨간 원 `d9473b` |
| `steady_vest` | 안정 조끼 | 주머니 여러 개 달린 전술 조끼(앞면) | 올리브 `7d8a4e` |
| `marksman_vest` | 사수 조끼 | 어깨에 패드가 있는 사격 조끼(앞면), 가슴에 작은 과녁 패치 | 남색 `3e5c8a` |
| `brace_vest` | 버팀 조끼 | 두툼한 패딩 방탄조끼(앞면), 가로 누빔 줄 | 회색 `7b8794` |
| `shoulder_harness` | 견착 멜빵 | X자로 교차하는 멜빵과 한쪽 어깨의 두툼한 견착 패드 | 갈색 가죽 `9a6a3e` |

- 모자 5장은 정면에서 약간 위에서 본 3/4 시점, 조끼 4장은 정면. 시점을 장끼리 통일한다.

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/ui-icons/tools/equipment_icons.py` 하나. 실행하면 위 9장을 `game/assets/icons/equipment/`에 쓰고(폴더가 없으면 만든다), 파일마다 `EQUIPMENT_ICON_READY <파일> 256x256`, 마지막에 `EQUIPMENT_ICONS_BUILT 9`를 출력한다.
2. 스크립트 끝 **assert**: 9장 모두 256 × 256, 모드 RGBA, 네 모서리 픽셀 완전 투명, 불투명 픽셀이 전체의 20 ~ 65%, 9장의 평균 색(불투명 부분)이 서로 다름(어떤 두 장도 RGB 거리 20 미만이 아님).
3. 실행은 하지 않는다. **Claude가 실행**한다: `python prototypes/ui-icons/tools/equipment_icons.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋, `doc/`, 다른 도구 파일을 고치지 않는다. 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `python -m py_compile prototypes/ui-icons/tools/equipment_icons.py` 통과.
- 마지막 메시지에: 만든 파일, 그림 설명, 확인하지 못한 것.

## 6. 결과 (2026-10-30)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/ui-icons/tools/equipment_icons.py` 하나, `py_compile`만 실행. `git status`로 다른 파일 변경 없음 확인.
- **Claude가 실행**: `python prototypes/ui-icons/tools/equipment_icons.py` → 단언 통과, `EQUIPMENT_ICONS_BUILT 9`, `game/assets/icons/equipment/*.png` 9장(256², 투명), Godot 가져오기.
  - 모자: 주황 야구 모자 · 청록 렌즈 고글(십자) · 초록 비니(클로버) · 빨강 체크 귀덮개 모자 · 흰 머리띠(빨간 원)
  - 조끼: 올리브 주머니 조끼 · 남색 과녁 조끼 · 회색 누빔 조끼 · 갈색 X 멜빵
- **게임 연결**: HUD 장비 칸(34 px)과 바닥 장비의 떠 있는 아이콘. 렌더 `output/gear-hud-normal.png` · `gear-hud-dev.png` · `-eng`로 확인([기록 019](../features/combat/log/019-code-equipment-pickup-hud.md)).
